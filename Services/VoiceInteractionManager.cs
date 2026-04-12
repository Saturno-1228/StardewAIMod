using System;
using StardewModdingAPI;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI.Events;
using StardewValley;
using StardewLivingValley.Configuration;

namespace StardewLivingValley.Services
{
    /// <summary>
    /// Gestiona las interacciones de voz del jugador, interceptando la tecla configurada (Push-to-Talk)
    /// y localizando a los NPCs cercanos válidos para conversar.
    /// </summary>
    public class VoiceInteractionManager
    {
        private readonly IModHelper _helper;
        private readonly ModConfig _config;

        private NPC? _targetNpc;
        private bool _isInteractionActive;
        private bool _isRecordingVoice;
        private double _lastInteractionTime;

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
        public VoiceInteractionManager(IModHelper helper, ModConfig config)
        {
            _helper = helper;
            _config = config;

            // Suscribirse a los eventos de presión y liberación de botones
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
                        ModEntry.Logger?.Log($"Cambiando de objetivo. Liberando a {_targetNpc.Name}...", LogLevel.Debug);
                        _targetNpc.movementPause = 0;
                    }

                    ModEntry.Logger?.Log($"NPC válido encontrado: {nearestNpc.Name}. Iniciando interacción y deteniendo...", LogLevel.Debug);

                    // Guardamos la referencia, activamos el estado de interacción y marcamos que estamos grabando
                    _targetNpc = nearestNpc;
                    _isInteractionActive = true;
                    _isRecordingVoice = true;

                    // Respuesta visual inmediata en lugar de esperar hasta el próximo UpdateTicked
                    _targetNpc.movementPause = 5000;
                    _targetNpc.facePlayer(Game1.player);
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
        /// Verifica las 4 reglas estipuladas para que un NPC sea válido para interactuar.
        /// </summary>
        private bool IsValidNpc(NPC npc)
        {
            // 1. Debe ser un aldeano real (ignorando monstruos, animales, etc.)
            if (!npc.IsVillager) return false;

            // 2. Debe poder socializar actualmente
            if (!npc.CanSocialize) return false;

            // 3. No debe estar en la lista negra
            if (_npcBlacklist.Contains(npc.Name)) return false;

            // 4. El jugador ya debe haberlo conocido (estar en los datos de amistad)
            if (Game1.player.friendshipData == null || !Game1.player.friendshipData.ContainsKey(npc.Name)) return false;

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
            }
        }

        /// <summary>
        /// Evento disparado en cada ciclo de actualización del juego (60 veces por segundo).
        /// </summary>
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
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
                    ModEntry.Logger?.Log($"El jugador ha cambiado de locación. Finalizando interacción con {_targetNpc.Name}.", LogLevel.Debug);
                    _targetNpc.movementPause = 0;
                    _isInteractionActive = false;
                    _targetNpc = null;
                    return;
                }

                float distance = Vector2.Distance(Game1.player.Tile, _targetNpc.Tile);

                // Condición de Salida: Alejamiento (Distancia mayor a 6 tiles)
                if (distance > 6f)
                {
                    ModEntry.Logger?.Log($"El jugador se ha alejado de {_targetNpc.Name}. Finalizando interacción.", LogLevel.Debug);
                    _targetNpc.movementPause = 0; // Liberamos al NPC
                    _isInteractionActive = false;
                    _targetNpc = null;
                    return;
                }

                // Condición de Salida: Timeout por inactividad máxima de 15 segundos
                // Solo se cuenta el tiempo si NO estamos grabando la voz actualmente
                if (!_isRecordingVoice)
                {
                    double timeSinceLastInteraction = Game1.currentGameTime.TotalGameTime.TotalSeconds - _lastInteractionTime;
                    if (timeSinceLastInteraction > 15.0)
                    {
                        ModEntry.Logger?.Log($"Tiempo de espera superado (15s) con {_targetNpc.Name}. Finalizando interacción.", LogLevel.Debug);
                        _targetNpc.movementPause = 0; // Liberamos al NPC
                        _isInteractionActive = false;
                        _targetNpc = null;
                        return;
                    }
                }

                // Mantener al NPC detenido y mirándonos pacientemente
                _targetNpc.movementPause = 5000; // Pausa de 5 segundos, renovada constantemente
                _targetNpc.facePlayer(Game1.player);
            }
        }
    }
}
