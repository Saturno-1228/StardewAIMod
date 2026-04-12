using System;
using StardewModdingAPI;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;
using LivingCompanionsValley.Configuration;

namespace LivingCompanionsValley.Services
{
    /// <summary>
    /// Gestiona las interacciones de voz del jugador, interceptando la tecla configurada (Push-to-Talk)
    /// y localizando a los NPCs cercanos válidos para conversar.
    /// </summary>
    using Microsoft.Xna.Framework.Audio;
    using System.Threading.Tasks;
    using System.Collections.Concurrent;

    public class VoiceInteractionManager
    {
        private readonly IModHelper _helper;
        private readonly ModConfig _config;
        private readonly VeniceApiService _veniceApiService;
        private readonly LocalWhisperService _whisperService;

        private NPC? _targetNpc;
        private bool _isInteractionActive;
        private bool _isRecordingVoice;
        private double _lastInteractionTime;

        // Microphone state
        private Microphone? _microphone;
        private byte[]? _audioBuffer;
        private MemoryStream? _audioMemoryStream;
        private readonly ConcurrentQueue<Action> _mainThreadActions = new ConcurrentQueue<Action>();

        /// <summary>
        /// Lista de NPCs que nunca deben ser contactados mediante el mod de IA.
        /// </summary>
        private readonly HashSet<string> _npcBlacklist = new HashSet<string>
        {
            "Bouncer",
            "Mister Qi",
            "Grandpa",
            "Henchman",
            "Gunther",
            "Marlon",
            "Birdie",
            "Professor Snail",
            "Welwick",
            "Leo" // Leo temporalmente, a veces requiere lógica específica antes de mudarse
        };

        /// <summary>
        /// Inicializa una nueva instancia del VoiceInteractionManager.
        /// </summary>
        /// <param name="helper">Helper de SMAPI para acceder a eventos y utilidades.</param>
        /// <param name="config">Configuración actual del mod para obtener la tecla de voz.</param>
        /// <param name="veniceApiService">Servicio de la API de Venice.</param>
        public VoiceInteractionManager(IModHelper helper, ModConfig config, VeniceApiService veniceApiService)
        {
            _helper = helper;
            _config = config;
            _veniceApiService = veniceApiService;
            _whisperService = new LocalWhisperService(helper);

            // Inicializar Whisper.net en background
            Task.Run(async () => await _whisperService.InitializeAsync());

            // Inicializar Micrófono
            try
            {
                if (Microphone.Default != null)
                {
                    _microphone = Microphone.Default;
                    _microphone.BufferDuration = TimeSpan.FromMilliseconds(100);
                    _audioBuffer = new byte[_microphone.GetSampleSizeInBytes(_microphone.BufferDuration)];
                }
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"No se pudo inicializar el micrófono: {ex.Message}", LogLevel.Error);
            }

            // Suscribirse a los eventos
            _helper.Events.Input.ButtonPressed += OnButtonPressed;
            _helper.Events.Input.ButtonReleased += OnButtonReleased;
            _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        /// <summary>
        /// Evento disparado cuando el jugador presiona cualquier botón.
        /// Verifica si es la tecla de voz configurada y comienza el flujo.
        /// </summary>
        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            // Evitar procesar si no hay partida cargada o si el jugador no puede actuar
            if (!Context.IsWorldReady || !Context.CanPlayerMove)
                return;

            // Solo actuar si el botón presionado coincide con el configurado
            if (e.Button == _config.VoiceKey)
            {
                ModEntry.Logger?.Log("Iniciando captura de voz...", LogLevel.Debug);

                NPC? nearestNpc = GetNearestValidNpc(3f);

                if (nearestNpc != null)
                {
                    // Si ya estábamos hablando con OTRO NPC y no lo hemos liberado, lo liberamos primero
                    if (_targetNpc != null && _targetNpc != nearestNpc)
                    {
                        ReleaseTargetNpc("Cambiando de objetivo.");
                    }

                    ModEntry.Logger?.Log($"NPC válido encontrado: {nearestNpc.Name}. Iniciando interacción y deteniendo...", LogLevel.Debug);

                    // Guardamos la referencia, activamos el estado de interacción y marcamos que estamos grabando
                    _targetNpc = nearestNpc;
                    _isInteractionActive = true;
                    _isRecordingVoice = true;

                    // Respuesta visual inmediata: detenerlo, mirarnos y pausarlo.
                    _targetNpc.Halt();
                    _targetNpc.facePlayer(Game1.player);
                    // Aplicar una pausa inicial. No la renovaremos en UpdateTicked para evitar
                    // conflictos con el motor de rutas nativo.
                    _targetNpc.movementPause = 5000;
                    
                    // Iniciar grabación de audio
                    if (_microphone != null && _microphone.State == MicrophoneState.Stopped)
                    {
                        _audioMemoryStream = new MemoryStream();
                        _microphone.Start();
                    }
                }
                else
                {
                    ModEntry.Logger?.Log("No se encontró ningún NPC válido cerca.", LogLevel.Debug);
                }
            }
        }

        /// <summary>
        /// Busca al NPC interactuable más cercano en el radio especificado.
        /// </summary>
        /// <param name="radiusTiles">Radio de búsqueda en tiles.</param>
        /// <returns>El NPC más cercano y válido, o null si no se encuentra ninguno.</returns>
        private NPC? GetNearestValidNpc(float radiusTiles)
        {
            NPC? closestNpc = null;
            float closestDistance = float.MaxValue;
            Vector2 playerTile = Game1.player.Tile;

            foreach (var character in Game1.currentLocation.characters)
            {
                if (character == null) continue;

                float distance = Vector2.Distance(playerTile, character.Tile);

                if (distance <= radiusTiles)
                {
                    if (IsValidNpc(character) && distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestNpc = character;
                    }
                }
            }

            return closestNpc;
        }

        /// <summary>
        /// Verifica las reglas estipuladas para que un NPC sea válido para interactuar.
        /// </summary>
        private bool IsValidNpc(NPC npc)
        {
            // 1. Debe ser un aldeano real (ignorando monstruos, animales, etc.)
            if (!npc.IsVillager) return false;

            // 2. Debe poder socializar actualmente
            if (!npc.CanSocialize) return false;

            // 3. No debe estar en la lista negra
            if (_npcBlacklist.Contains(npc.Name)) return false;

            return true;
        }

        /// <summary>
        /// Evento disparado cuando el jugador suelta cualquier botón.
        /// Verifica si es la tecla de voz configurada y finaliza el flujo.
        /// </summary>
        private void OnButtonReleased(object? sender, ButtonReleasedEventArgs e)
        {
            // Solo actuar si el botón soltado coincide con el configurado
            if (e.Button == _config.VoiceKey)
            {
                ModEntry.Logger?.Log("Captura de voz finalizada. Procesando (el NPC espera)...", LogLevel.Debug);
                
                // Dejamos de grabar, pero MANTENEMOS la interacción activa para que espere
                _isRecordingVoice = false;
                
                // Registramos el momento exacto en que soltamos el botón para el temporizador de inactividad
                _lastInteractionTime = Game1.currentGameTime.TotalGameTime.TotalSeconds;

                // Detener el micrófono y procesar el audio
                if (_microphone != null && _microphone.State == MicrophoneState.Started)
                {
                    _microphone.Stop();

                    if (_audioMemoryStream != null && _targetNpc != null)
                    {
                        byte[] finalAudioData = _audioMemoryStream.ToArray();
                        _audioMemoryStream.Dispose();
                        _audioMemoryStream = null;

                        // Lanzar el procesamiento en background
                        string npcName = _targetNpc.Name;
                        Task.Run(() => ProcessAudioAndGetResponseAsync(npcName, finalAudioData));
                    }
                }
            }
        }

        private async Task ProcessAudioAndGetResponseAsync(string npcName, byte[] audioData)
        {
            try
            {
                // Convertir PCM 16-bit a Float 32-bit (16kHz asumido)
                float[] floatAudio = ConvertPcm16ToFloat(audioData);

                // 1. Transcribir audio
                string transcription = await _whisperService.TranscribeAudioAsync(floatAudio);

                if (string.IsNullOrWhiteSpace(transcription) || transcription.Contains("[Error]"))
                {
                    ShowBubble(npcName, "...");
                    return;
                }

                ModEntry.Logger?.Log($"Usuario dijo: {transcription}", LogLevel.Debug);

                // 2. Obtener respuesta de Venice
                string npcResponse = await _veniceApiService.GetNpcResponseAsync(npcName, transcription);

                ModEntry.Logger?.Log($"{npcName} responde: {npcResponse}", LogLevel.Debug);

                // 3. Mostrar la respuesta en la UI principal
                ShowBubble(npcName, npcResponse);
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error al procesar la interacción: {ex.Message}", LogLevel.Error);
                ShowBubble(npcName, "Lo siento, me he distraído...");
            }
        }

        private void ShowBubble(string npcName, string text)
        {
            _mainThreadActions.Enqueue(() =>
            {
                // Asegurarse de que el NPC sigue existiendo
                foreach (var character in Game1.currentLocation.characters)
                {
                    if (character.Name == npcName)
                    {
                        character.showTextAboveHead(text);
                        // Refrescar el movement pause para darle tiempo de mostrar el texto sin huir
                        character.movementPause = Math.Max(character.movementPause, 3000);
                        break;
                    }
                }
            });
        }

        private float[] ConvertPcm16ToFloat(byte[] pcmData)
        {
            int numSamples = pcmData.Length / 2;
            float[] floatData = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                short sample = BitConverter.ToInt16(pcmData, i * 2);
                floatData[i] = sample / 32768f;
            }
            return floatData;
        }

        /// <summary>
        /// Libera de manera limpia al NPC para que pueda continuar su ruta normal.
        /// </summary>
        private void ReleaseTargetNpc(string reason)
        {
            if (_targetNpc == null) return;

            ModEntry.Logger?.Log($"{reason} Finalizando interacción con {_targetNpc.Name}.", LogLevel.Debug);
            
            // Permitimos que el juego recupere el control del movimiento
            _targetNpc.movementPause = 0;

            // Limpiamos referencias
            _isInteractionActive = false;
            _targetNpc = null;
        }

        /// <summary>
        /// Evento disparado en cada ciclo de actualización del juego (60 veces por segundo).
        /// </summary>
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            // Procesar acciones de la UI programadas desde hilos en background
            while (_mainThreadActions.TryDequeue(out var action))
            {
                action?.Invoke();
            }

            // Capturar datos de audio si estamos grabando
            if (_microphone != null && _microphone.State == MicrophoneState.Started && _audioMemoryStream != null)
            {
                int sampleSize = _microphone.GetSampleSizeInBytes(_microphone.BufferDuration);
                if (_audioBuffer == null || _audioBuffer.Length < sampleSize)
                    _audioBuffer = new byte[sampleSize];
                    
                int bytesRead = _microphone.GetData(_audioBuffer);
                if (bytesRead > 0)
                {
                    _audioMemoryStream.Write(_audioBuffer, 0, bytesRead);
                }
            }

            if (!Context.IsWorldReady || Game1.player == null)
                return;

            if (!_isInteractionActive || _targetNpc == null)
                return;

            // Optimización: Solo ejecutar la lógica 2 veces por segundo (cada 30 ticks)
            if (e.IsMultipleOf(30))
            {
                // Condición de Salida: Cambio de locación
                if (Game1.player.currentLocation != _targetNpc.currentLocation)
                {
                    ReleaseTargetNpc("El jugador ha cambiado de locación.");
                    return;
                }

                float distance = Vector2.Distance(Game1.player.Tile, _targetNpc.Tile);

                // Condición de Salida: Alejamiento (Distancia mayor a 6 tiles)
                if (distance > 6f)
                {
                    ReleaseTargetNpc($"El jugador se ha alejado de {_targetNpc.Name}.");
                    return;
                }

                // Condición de Salida: Timeout por inactividad máxima de 15 segundos
                // Solo se cuenta el tiempo si NO estamos grabando la voz actualmente
                if (!_isRecordingVoice)
                {
                    double timeSinceLastInteraction = Game1.currentGameTime.TotalGameTime.TotalSeconds - _lastInteractionTime;
                    if (timeSinceLastInteraction > 15.0)
                    {
                        ReleaseTargetNpc($"Tiempo de espera superado (15s) con {_targetNpc.Name}.");
                        return;
                    }
                }

                // Ya no renovamos el _targetNpc.movementPause = 5000 aquí.
                // Permitimos que la pausa inicial persista para que al final el motor nativo la recupere.
                // Si la pausa expira, simplemente se quedarán cerca pero no se congelarán permanentemente.
            }
        }
    }
}
