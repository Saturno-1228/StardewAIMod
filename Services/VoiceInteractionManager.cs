using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using System;
using System.Collections.Concurrent;
using StardewAIMod.Models;

namespace StardewAIMod.Services
{
    public class VoiceInteractionManager
    {
        private readonly VeniceApiService _veniceApi;
        private readonly LocalTranscriptionService _localTranscriptionService;
        private readonly MemoryService _memoryService;
        private readonly AudioRecorder _audioRecorder;
        private readonly ModConfig _config;
        private readonly IMonitor _monitor;
        private readonly string _modDirectory;

        private NPC _targetNpc;
        private bool _isRecordingPhase = false;
        private readonly ConcurrentQueue<Action> _uiThreadActions = new();

        public VoiceInteractionManager(
            VeniceApiService veniceApi,
            LocalTranscriptionService localTranscriptionService,
            MemoryService memoryService,
            ModConfig config,
            IMonitor monitor,
            string modDirectory)
        {
            _veniceApi = veniceApi;
            _localTranscriptionService = localTranscriptionService;
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
            while (_uiThreadActions.TryDequeue(out var action))
            {
                action();
            }

            if (_isRecordingPhase)
            {
                _audioRecorder.Update();
            }
        }

        public void StopRecordingAndProcess()
        {
            if (!_isRecordingPhase || _targetNpc == null) return;

            _isRecordingPhase = false;
            _monitor.Log("[Studio Corvus] 🎙️ Grabación detenida. Procesando audio...", LogLevel.Info);

            byte[] audioData = _audioRecorder.StopRecording();
            NPC currentNpc = _targetNpc; // Capture for the background task

            // Extract context safely on the main thread
            var currentContext = new Dictionary<string, string>
            {
                { "Season", Game1.season.ToString() },
                { "Day", Game1.dayOfMonth.ToString() },
                { "Time", Game1.timeOfDay.ToString() },
                { "Weather", Game1.isRaining ? "Raining" : (Game1.isSnowing ? "Snowing" : "Sunny") },
                { "Location", Game1.currentLocation.Name },
                { "Player Health", $"{Game1.player.health}/{Game1.player.maxHealth}" },
                { "Player Stamina", $"{Math.Round(Game1.player.stamina)}/{Game1.player.MaxStamina}" }
            };

            if (Game1.player.ActiveObject != null)
            {
                currentContext["Holding Item"] = Game1.player.ActiveObject.DisplayName;
            }

            bool isBirthday = currentNpc.isBirthday();
            if (isBirthday)
            {
                currentContext["Event"] = "Today is your birthday!";
            }

            // Run everything else on a background thread so we don't freeze the game
            Task.Run(async () =>
            {
                try
                {
                    // 44 bytes is just the WAV header. We need actual audio data.
                    // 1 second of 16-bit mono audio at 48000Hz (typical XNA mic) is 96000 bytes.
                    // Let's require at least 0.5 seconds of audio (~48000 bytes) to avoid useless requests.
                    if (audioData == null || audioData.Length < 48000)
                    {
                        _monitor.Log("[Studio Corvus] ⚠️ Audio demasiado corto o inválido.", LogLevel.Warn);
                        _uiThreadActions.Enqueue(() =>
                        {
                            Game1.addHUDMessage(new HUDMessage("No se detectó voz.", HUDMessage.error_type));
                        });
                        return;
                    }

                    // Transcribe audio using LocalTranscriptionService
                    string userText = await _localTranscriptionService.TranscribeAsync(audioData);

                    if (string.IsNullOrWhiteSpace(userText) || userText.StartsWith("["))
                    {
                        _monitor.Log($"[Studio Corvus] ❌ Error de transcripción o texto vacío: {userText}", LogLevel.Error);
                        _uiThreadActions.Enqueue(() =>
                        {
                            Game1.addHUDMessage(new HUDMessage("No pude entenderte.", HUDMessage.error_type));
                        });
                        return;
                    }

                    _monitor.Log($"[Studio Corvus] 🗣️ Jugador dijo: {userText}", LogLevel.Info);

                    // Fetch Memory & Build Prompt
                    var memory = _memoryService.GetMemory(currentNpc.Name);
                    var recentHistory = memory.ConversationHistory;
                    _memoryService.AddToConversationHistory(currentNpc.Name, "player", userText);

                    var promptBuilder = new PromptBuilder(_modDirectory);
                    string systemPrompt = promptBuilder.BuildSystemPrompt(currentNpc.Name, memory, currentContext, userText);

                    // Get AI Response
                    _monitor.Log($"[Studio Corvus] 🧠 Consultando a Venice AI...", LogLevel.Info);
                    string aiResponse = await _veniceApi.SendMessageAsync(systemPrompt, recentHistory);

                    _monitor.Log($"[Studio Corvus] 🤖 {currentNpc.Name} responde: {aiResponse}", LogLevel.Info);

                    _memoryService.AddToConversationHistory(currentNpc.Name, "assistant", aiResponse);

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

                    // Enqueue UI updates
                    _uiThreadActions.Enqueue(() =>
                    {
                        // Check for friendship commands
                        if (aiResponse.Contains("$h") || aiResponse.Contains("$l"))
                        {
                            Game1.player.changeFriendship(10, currentNpc);
                        }
                        else if (aiResponse.Contains("$s") || aiResponse.Contains("$a"))
                        {
                            Game1.player.changeFriendship(-10, currentNpc);
                        }

                        // Show in native DialogueBox
                        currentNpc.CurrentDialogue.Clear();
                        currentNpc.CurrentDialogue.Push(new Dialogue(currentNpc, null, cleanResponse));
                        Game1.drawDialogue(currentNpc);

                        // Prevent first-time greeting
                        if (Game1.player.friendshipData.ContainsKey(currentNpc.Name))
                        {
                            Game1.player.friendshipData[currentNpc.Name].TalkedToToday = true;
                        }
                    });

                }
                catch (Exception ex)
                {
                    _monitor.Log($"[Studio Corvus] ❌ Excepción no manejada en procesamiento de voz: {ex}", LogLevel.Error);
                }
            });
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
