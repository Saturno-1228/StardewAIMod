using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewAIMod.Models;

namespace StardewAIMod.Services
{
    public class VoiceInteractionManager
    {
        private readonly VeniceApiService _veniceApi;
        private readonly WhisperTranscriptionService _whisperApi;
        private readonly MemoryService _memoryService;
        private readonly AudioRecorder _audioRecorder;
        private readonly ModConfig _config;
        private readonly IMonitor _monitor;
        private readonly string _modDirectory;

        private NPC _targetNpc;
        private bool _isRecordingPhase = false;
        private readonly ConcurrentQueue<Action> _uiTaskQueue = new ConcurrentQueue<Action>();

        public VoiceInteractionManager(
            VeniceApiService veniceApi,
            WhisperTranscriptionService whisperApi,
            MemoryService memoryService,
            ModConfig config,
            IMonitor monitor,
            string modDirectory)
        {
            _veniceApi = veniceApi;
            _whisperApi = whisperApi;
            _memoryService = memoryService;
            _config = config;
            _monitor = monitor;
            _modDirectory = modDirectory;
            _audioRecorder = new AudioRecorder();
        }

        public void StartRecording(NPC targetNpc)
        {
            if (_isRecordingPhase) return;

            _targetNpc = targetNpc;
            _isRecordingPhase = true;

            // Make NPC face player
            _targetNpc.faceTowardFarmerForPeriod(5000, 3, false, Game1.player);
            _targetNpc.Halt();
            _targetNpc.movementPause = 5000;

            _monitor.Log($"[Studio Corvus] 🎙️ Empezando a grabar para {_targetNpc.Name}...", LogLevel.Info);
            _audioRecorder.StartRecording();
            Game1.addHUDMessage(new HUDMessage($"Escuchando a {_targetNpc.Name}...", 1));
        }

        public void Update()
        {
            if (_isRecordingPhase)
            {
                _audioRecorder.Update();
            }

            // Execute UI tasks on the main thread
            while (_uiTaskQueue.TryDequeue(out Action uiAction))
            {
                uiAction?.Invoke();
            }
        }

        public void StopRecordingAndProcess()
        {
            if (!_isRecordingPhase || _targetNpc == null) return;

            _isRecordingPhase = false;
            _monitor.Log("[Studio Corvus] 🎙️ Grabación detenida. Procesando audio...", LogLevel.Info);

            float[] audioData = _audioRecorder.StopRecording();

            // We require at least some length.
            // 0.5s of 16kHz audio = 8000 samples.
            if (audioData == null || audioData.Length < 8000)
            {
                _monitor.Log("[Studio Corvus] ⚠️ Audio demasiado corto o inválido.", LogLevel.Warn);
                _uiTaskQueue.Enqueue(() => Game1.addHUDMessage(new HUDMessage("No se detectó voz.", HUDMessage.error_type)));
                return;
            }

            var npcName = _targetNpc.Name;
            var targetNpc = _targetNpc;

            // Run API requests and processing on a background thread
            Task.Run(async () =>
            {
                // Transcribe audio locally with Whisper
                _monitor.Log($"[Studio Corvus] 🧠 Transcribiendo con Whisper local...", LogLevel.Info);
                string userText = await _whisperApi.TranscribeAudioAsync(audioData);

                if (string.IsNullOrWhiteSpace(userText) || userText.StartsWith("["))
                {
                    _monitor.Log($"[Studio Corvus] ❌ Error de transcripción o texto vacío: {userText}", LogLevel.Error);
                    _uiTaskQueue.Enqueue(() => Game1.addHUDMessage(new HUDMessage("No pude entenderte.", HUDMessage.error_type)));
                    return;
                }

                _monitor.Log($"[Studio Corvus] 🗣️ Jugador dijo: {userText}", LogLevel.Info);

                // Fetch Memory & Build Prompt
                var memory = _memoryService.GetMemory(npcName);
                var recentHistory = memory.ConversationHistory;
                _memoryService.AddToConversationHistory(npcName, "player", userText);

                // Current context variables must be read carefully from background thread.
                // Normally this is risky without locks, but game state is mostly read-only here.
                var currentContext = new Dictionary<string, string>();

                // We use _uiTaskQueue to safely fetch properties that might throw on BG threads,
                // but for simple properties we might be fine.
                // A safer approach is grabbing these BEFORE Task.Run, but we'll try reading them directly.
                try
                {
                    currentContext = new Dictionary<string, string>
                    {
                        { "Season", Game1.season.ToString() },
                        { "Day", Game1.dayOfMonth.ToString() },
                        { "Time", Game1.timeOfDay.ToString() },
                        { "Weather", Game1.isRaining ? "Raining" : (Game1.isSnowing ? "Snowing" : "Sunny") },
                        { "Location", Game1.currentLocation?.Name ?? "Unknown" },
                        { "Player Health", $"{Game1.player.health}/{Game1.player.maxHealth}" },
                        { "Player Stamina", $"{Math.Round(Game1.player.stamina)}/{Game1.player.MaxStamina}" }
                    };

                    if (Game1.player.ActiveObject != null)
                    {
                        currentContext["Holding Item"] = Game1.player.ActiveObject.DisplayName;
                    }

                    if (targetNpc.isBirthday())
                    {
                        currentContext["Event"] = "Today is your birthday!";
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[Studio Corvus] ⚠️ Error reading game state in BG thread: {ex.Message}", LogLevel.Warn);
                }

                var promptBuilder = new PromptBuilder(_modDirectory);
                string systemPrompt = promptBuilder.BuildSystemPrompt(npcName, memory, currentContext, userText);

                // Get AI Response
                _monitor.Log($"[Studio Corvus] 🧠 Consultando a Venice AI para {npcName}...", LogLevel.Info);
                string aiResponse = await _veniceApi.SendMessageAsync(systemPrompt, recentHistory);

                _monitor.Log($"[Studio Corvus] 🤖 {npcName} responde: {aiResponse}", LogLevel.Info);

                _memoryService.AddToConversationHistory(npcName, "assistant", aiResponse);

                // Clean response for native dialogue box
                string cleanResponse = aiResponse.Replace("$h", "").Replace("$s", "").Replace("$u", "")
                                                 .Replace("$l", "").Replace("$a", "").Trim();

                // Intercept errors
                if (cleanResponse.StartsWith("[") && (cleanResponse.Contains("Error") || cleanResponse.Contains("No response")))
                {
                    cleanResponse = "Uh... me siento un poco mareado. ¿Hablamos luego?";
                }

                // Format dialogue with pagination to avoid overflow
                cleanResponse = FormatDialogueText(cleanResponse);

                // Queue all game modifications back on the main UI thread
                _uiTaskQueue.Enqueue(() =>
                {
                    // Check for friendship commands
                    if (aiResponse.Contains("$h") || aiResponse.Contains("$l"))
                    {
                        Game1.player.changeFriendship(10, targetNpc);
                    }
                    else if (aiResponse.Contains("$s") || aiResponse.Contains("$a"))
                    {
                        Game1.player.changeFriendship(-10, targetNpc);
                    }

                    // Show in native DialogueBox
                    targetNpc.CurrentDialogue.Clear();
                    targetNpc.CurrentDialogue.Push(new Dialogue(targetNpc, null, cleanResponse));
                    Game1.drawDialogue(targetNpc);

                    // Prevent first-time greeting
                    if (Game1.player.friendshipData.ContainsKey(npcName))
                    {
                        Game1.player.friendshipData[npcName].TalkedToToday = true;
                    }
                });

            }); // End of Task.Run
        }

        private string FormatDialogueText(string text)
        {
            var words = text.Split(' ');
            var formatted = new System.Text.StringBuilder();
            int lineLength = 0;

            foreach (var word in words)
            {
                if (lineLength + word.Length > 120)
                {
                    formatted.Append("#$b#");
                    lineLength = 0;
                }
                formatted.Append(word + " ");
                lineLength += word.Length + 1;
            }

            return formatted.ToString().Trim();
        }
    }
}
