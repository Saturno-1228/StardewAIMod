using System.IO;
using System.Text.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewAIMod
{
    /// <summary>
    /// Punto de entrada principal del mod.
    /// SMAPI llama a este archivo al cargar el juego.
    /// </summary>
    public class ModEntry : Mod
    {
        // Configuración pública (config.json)
        private ModConfig Config;

        // Credenciales privadas (secrets.json)
        private SecretConfig Secrets;

        // Servicios
        private Services.VeniceApiService VeniceApi;
        private Services.MemoryService Memory;

        /// <summary>
        /// Método principal. Se ejecuta cuando SMAPI carga el mod.
        /// </summary>
        public override void Entry(IModHelper helper)
        {
            // 1. Cargar configuración pública
            this.Config = helper.ReadConfig<ModConfig>();

            // 2. Cargar credenciales privadas
            this.Secrets = LoadSecrets();

            // 3. Verificar API Key
            if (string.IsNullOrEmpty(this.Secrets.VeniceApiKey))
            {
                this.Monitor.Log(
                    "[Studio Corvus] ⚠️ No Venice API Key found! Create a secrets.json file. See secrets.example.json",
                    LogLevel.Warn
                );
            }
            else
            {
                this.Monitor.Log(
                    "[Studio Corvus] ✅ Stardew AI Mod loaded successfully!",
                    LogLevel.Info
                );
            }

            // 4. Registrar eventos del juego
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        }

        /// <summary>
        /// Carga secrets.json desde la carpeta del mod.
        /// Si no existe, devuelve un SecretConfig vacío.
        /// </summary>
        private SecretConfig LoadSecrets()
        {
            string path = Path.Combine(this.Helper.DirectoryPath, "secrets.json");

            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var secrets = JsonSerializer.Deserialize<SecretConfig>(json);
                    this.Monitor.Log("[Studio Corvus] 🔐 Secrets loaded.", LogLevel.Info);
                    return secrets ?? new SecretConfig();
                }
                catch
                {
                    this.Monitor.Log("[Studio Corvus] ❌ Error reading secrets.json", LogLevel.Error);
                    return new SecretConfig();
                }
            }
            else
            {
                this.Monitor.Log(
                    "[Studio Corvus] 📄 No secrets.json found. Copy secrets.example.json and rename it.",
                    LogLevel.Warn
                );
                return new SecretConfig();
            }
        }

        /// <summary>
        /// Se ejecuta cuando el juego termina de cargar.
        /// </summary>
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            this.Monitor.Log("[Studio Corvus] 🎮 Game launched. AI services initializing...", LogLevel.Info);

            // Inicializar VeniceApiService
            this.VeniceApi = new Services.VeniceApiService(
                this.Secrets.VeniceApiKey,
                this.Config.VeniceModel,
                this.Config.VeniceEndpoint
            );

            // Inicializar MemoryService
            this.Memory = new Services.MemoryService(this.Helper, this.Monitor, this.Config.MaxMemoryPerNpc);
        }

        /// <summary>
        /// Se ejecuta cuando se carga una partida guardada.
        /// </summary>
        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            string farmName = Game1.player.farmName.Value;
            string playerName = Game1.player.Name;
            this.Monitor.Log(
                $"[Studio Corvus] 📂 Save loaded: {playerName} @ {farmName} Farm",
                LogLevel.Info
            );

            // Cargar memorias de NPCs desde archivo
            this.Memory.LoadAll();
        }

        /// <summary>
        /// Se ejecuta cada nuevo día en el juego.
        /// </summary>
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            int day = Game1.dayOfMonth;
            string season = Game1.currentSeason;
            string weather = Game1.isRaining ? "Raining" : (Game1.isSnowing ? "Snowing" : "Sunny");

            this.Monitor.Log(
                $"[Studio Corvus] 🌅 New day: {season} {day}, Weather: {weather}",
                LogLevel.Debug
            );

            // TODO: Actualizar contexto diario de NPCs
        }

        /// <summary>
        /// Se ejecuta cuando el jugador presiona cualquier tecla.
        /// </summary>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // Detectar tecla de chat (configurable)
            if (e.Button.ToString() == this.Config.ChatKey)
            {
                // Prevenir abrir el chat si no hay API Key configurada
                if (string.IsNullOrEmpty(this.Secrets.VeniceApiKey))
                {
                    Game1.addHUDMessage(new HUDMessage("Stardew AI: Missing Venice API Key in secrets.json!", HUDMessage.error_type));
                    return;
                }

                // Detectar si el jugador está frente a un NPC
                NPC targetNpc = null;

                // Intentar encontrar al NPC frente al jugador
                var grabTile = new Microsoft.Xna.Framework.Vector2(
                    (int)(Game1.player.GetGrabTile().X),
                    (int)(Game1.player.GetGrabTile().Y)
                );

                targetNpc = Game1.currentLocation.isCharacterAt(grabTile);

                if (targetNpc != null)
                {
                    this.Monitor.Log($"[Studio Corvus] 💬 Initiating chat with {targetNpc.Name}", LogLevel.Info);

                    // Abrir interfaz de chat interactiva
                    Game1.activeClickableMenu = new Menus.ChatMenu(
                        targetNpc,
                        this.VeniceApi,
                        this.Memory,
                        this.Config
                    );
                }
            }
        }
    }
}