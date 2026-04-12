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
    using NAudio.Wave;
    using System.Threading.Tasks;
    using System.Collections.Concurrent;

    public class VoiceInteractionManager : IDisposable
    {
        private readonly IModHelper _helper;
        private readonly ModConfig _config;
        private readonly VeniceApiService _veniceApiService;
        private readonly LocalWhisperService _whisperService;

        private NPC? _targetNpc;
        private bool _isInteractionActive;
        private bool _isRecordingVoice;
        private double _lastInteractionTime;

        // Microphone state using NAudio
        private WaveInEvent? _waveIn;
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

            // Inicializar Micrófono con NAudio
            try
            {
                if (WaveInEvent.DeviceCount > 0)
                {
                    _waveIn = new WaveInEvent
                    {
                        DeviceNumber = 0, // Default device
                        WaveFormat = new WaveFormat(16000, 1) // 16kHz, mono
                    };
                    _waveIn.DataAvailable += OnDataAvailable;
                }
                else
                {
                    ModEntry.Logger?.Log("No se encontraron dispositivos de grabación de audio (NAudio).", LogLevel.Error);
                }
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"No se pudo inicializar el micrófono con NAudio: {ex.Message}", LogLevel.Error);
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

                    // Validación crucial: Si el micrófono falló al inicializarse, no podemos continuar.
                    if (_waveIn == null)
                    {
                        ModEntry.Logger?.Log("ERROR CRÍTICO: Intento de grabar voz fallido. El micrófono no está disponible. Interacción abortada.", LogLevel.Error);
                        Game1.addHUDMessage(new HUDMessage("Error: Micrófono no detectado", HUDMessage.error_type));
                        return;
                    }

                    ModEntry.Logger?.Log($"NPC válido encontrado: {nearestNpc.Name}. Iniciando interacción y deteniendo...", LogLevel.Debug);

                    // Guardamos la referencia y activamos el estado de interacción
                    _targetNpc = nearestNpc;
                    _isInteractionActive = true;

                    // Verificar si el NPC está realizando una animación especial (ej. sentado).
                    // Si el sprite tiene una animación actual, evitamos Halt() y facePlayer() para no romperla.
                    bool isPlayingSpecialAnimation = _targetNpc.Sprite?.CurrentAnimation != null;

                    if (!isPlayingSpecialAnimation)
                    {
                        // Respuesta visual inmediata: detenerlo y mirarnos.
                        _targetNpc.Halt();
                        _targetNpc.facePlayer(Game1.player);
                    }
                    else
                    {
                        ModEntry.Logger?.Log($"{_targetNpc.Name} está en una animación especial. Omitiendo Halt() para no romperla.", LogLevel.Debug);
                    }
                    
                    // Congelar al NPC usando la propiedad nativa del motor (detiene los pies y el movimiento)
                    // sin corromper el horario como lo hace movementPause, ni fallar visualmente como speed.
                    // Se usa Reflection de SMAPI porque 'freezeMotion' es un campo protegido.
                    _helper.Reflection.GetField<bool>(_targetNpc, "freezeMotion").SetValue(true);
                    
                    // Iniciar grabación de audio
                    if (_waveIn != null && !_isRecordingVoice)
                    {
                        ModEntry.Logger?.Log("Intentando iniciar la grabación de audio con NAudio...", LogLevel.Trace);
                        _audioMemoryStream = new MemoryStream();
                        try
                        {
                            _waveIn.StartRecording();
                            _isRecordingVoice = true; // Marcamos que estamos grabando
                            ModEntry.Logger?.Log("Micrófono grabando activamente.", LogLevel.Debug);
                        }
                        catch (Exception ex)
                        {
                            ModEntry.Logger?.Log($"Error al iniciar grabación: {ex.Message}", LogLevel.Error);
                            ReleaseTargetNpc("Error al grabar");
                            return;
                        }
                    }
                    else if (_waveIn == null)
                    {
                        ModEntry.Logger?.Log("No se puede iniciar grabación: el objeto _waveIn es nulo.", LogLevel.Error);
                    }
                }
                else
                {
                    ModEntry.Logger?.Log("No se encontró ningún NPC válido cerca.", LogLevel.Debug);
                }
            }
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_isRecordingVoice && _audioMemoryStream != null)
            {
                _audioMemoryStream.Write(e.Buffer, 0, e.BytesRecorded);
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
                if (_waveIn != null)
                {
                    try
                    {
                        _waveIn.StopRecording();
                    }
                    catch (Exception ex)
                    {
                        ModEntry.Logger?.Log($"Error al detener grabación: {ex.Message}", LogLevel.Error);
                    }

                    if (_audioMemoryStream != null && _targetNpc != null)
                    {
                        byte[] finalAudioData = _audioMemoryStream.ToArray();
                        _audioMemoryStream.Dispose();
                        _audioMemoryStream = null;

                        ModEntry.Logger?.Log($"Se detuvo la grabación. Tamaño del buffer capturado: {finalAudioData.Length} bytes.", LogLevel.Trace);

                        // Validar si el audio grabado es demasiado corto (ej. toque rápido por error).
                        // Asumiendo formato de 16 bits (2 bytes) a 16kHz, 1 segundo son 32,000 bytes.
                        // 0.5 segundos son 16,000 bytes.
                        if (finalAudioData.Length < 16000)
                        {
                            ModEntry.Logger?.Log($"Grabación demasiado corta ({finalAudioData.Length} bytes, menos de 0.5s). Cancelando interacción por toque accidental.", LogLevel.Info);
                            ReleaseTargetNpc("Interacción cancelada (grabación corta).");
                            return;
                        }

                        // Lanzar el procesamiento en background
                        string npcName = _targetNpc.Name;
                        Task.Run(() => ProcessAudioAndGetResponseAsync(npcName, finalAudioData));
                    }
                    else
                    {
                        ModEntry.Logger?.Log("Al finalizar la interacción, _audioMemoryStream era nulo. No se capturó audio.", LogLevel.Warn);
                    }
                }
                else
                {
                    ModEntry.Logger?.Log("Al soltar la tecla, _waveIn era nulo. No se pudo detener ni procesar audio.", LogLevel.Error);
                }
            }
        }

        private async Task ProcessAudioAndGetResponseAsync(string npcName, byte[] audioData)
        {
            try
            {
                ModEntry.Logger?.Log($"Iniciando procesamiento de audio ({audioData.Length} bytes) para {npcName}...", LogLevel.Info);
                
                // Convertir PCM 16-bit a Float 32-bit (16kHz asumido)
                float[] floatAudio = ConvertPcm16ToFloat(audioData);
                ModEntry.Logger?.Log($"Audio convertido a {floatAudio.Length} muestras float.", LogLevel.Info);

                // 1. Transcribir audio
                ModEntry.Logger?.Log("Llamando a Whisper para transcribir...", LogLevel.Info);
                string transcription = await _whisperService.TranscribeAudioAsync(floatAudio);

                if (string.IsNullOrWhiteSpace(transcription) || transcription.Contains("[Error]"))
                {
                    ModEntry.Logger?.Log($"La transcripción fue nula, vacía o devolvió error: '{transcription}'. Cancelando llamado a Venice.", LogLevel.Debug);
                    ShowBubble(npcName, "...");
                    return;
                }

                ModEntry.Logger?.Log($"Usuario dijo (Transcrito): {transcription}", LogLevel.Info);

                // 2. Obtener respuesta de Venice
                ModEntry.Logger?.Log("Llamando a Venice API para obtener respuesta...", LogLevel.Info);
                string npcResponse = await _veniceApiService.GetNpcResponseAsync(npcName, transcription);

                ModEntry.Logger?.Log($"{npcName} responde: {npcResponse}", LogLevel.Info);

                // 3. Mostrar la respuesta en la UI principal
                ShowBubble(npcName, npcResponse);
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Error al procesar la interacción de voz: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
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
            
            // Liberamos el congelamiento nativo para que el NPC retome su ruta y animaciones
            _helper.Reflection.GetField<bool>(_targetNpc, "freezeMotion").SetValue(false);

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

                // Se utiliza freezeMotion en lugar de movementPause, deteniendo perfectamente al NPC y sus animaciones (pies) sin corromper el motor de Stardew.
            }
        }

        public void Dispose()
        {
            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.Dispose();
                _waveIn = null;
            }

            if (_audioMemoryStream != null)
            {
                _audioMemoryStream.Dispose();
                _audioMemoryStream = null;
            }
        }
    }
}
