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
        private Services.WhisperTranscriptionService WhisperApi;
        private Services.MemoryService Memory;
        private Services.VoiceInteractionManager VoiceManager;

        // Catálogo de Items
        public static System.Collections.Generic.Dictionary<string, string> ItemCatalog = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);

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
            helper.Events.Input.ButtonReleased += this.OnButtonReleased;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.GameLoop.Saving += this.OnSaving;
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

            // Inicializar WhisperTranscriptionService
            string whisperModelFullPath = Path.Combine(this.Helper.DirectoryPath, this.Config.WhisperModelPath);
            this.WhisperApi = new Services.WhisperTranscriptionService(whisperModelFullPath, this.Monitor);

            // Inicializar MemoryService
            this.Memory = new Services.MemoryService(this.Helper, this.Monitor, this.Config.MaxMemoryPerNpc);

            // Inicializar VoiceInteractionManager
            this.VoiceManager = new Services.VoiceInteractionManager(
                this.VeniceApi,
                this.WhisperApi,
                this.Memory,
                this.Config,
                this.Monitor,
                this.Helper.DirectoryPath
            );

            // Inicializar y Aplicar Harmony Patches
            var harmony = new HarmonyLib.Harmony(this.ModManifest.UniqueID);
            HarmonyPatches.Initialize(this.Monitor, this.Memory);
            harmony.PatchAll();

            this.Monitor.Log("[Studio Corvus] 💉 Harmony patches applied.", LogLevel.Info);
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

            // Populate ItemCatalog from Game1.objectData
            ItemCatalog.Clear();
            if (Game1.objectData != null)
            {
                foreach (var kvp in Game1.objectData)
                {
                    if (kvp.Value != null && !string.IsNullOrEmpty(kvp.Value.Name))
                    {
                        ItemCatalog[kvp.Value.Name] = kvp.Key;
                    }
                }
                this.Monitor.Log($"[Studio Corvus] 📦 Loaded {ItemCatalog.Count} items into ItemCatalog.", LogLevel.Info);
            }
        }

        /// <summary>
        /// Se ejecuta cuando el juego se está guardando.
        /// </summary>
        private void OnSaving(object sender, SavingEventArgs e)
        {
            this.Monitor.Log("[Studio Corvus] 💾 Saving game... Saving AI memories.", LogLevel.Info);
            this.Memory.SaveAll();
        }

        /// <summary>
        /// Se ejecuta cada nuevo día en el juego.
        /// </summary>
        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            int day = Game1.dayOfMonth;
            StardewValley.Season season = Game1.season;
            string weather = Game1.isRaining ? "Raining" : (Game1.isSnowing ? "Snowing" : "Sunny");

            this.Monitor.Log(
                $"[Studio Corvus] 🌅 New day: {season} {day}, Weather: {weather}",
                LogLevel.Debug
            );

            // Verificar si es día de festival
            bool isFestival = Utility.isFestivalDay(day, season);
            string festivalInfo = isFestival ? $" Today is a festival day!" : "";

            // Actualizar contexto diario: Agregar una memoria para todos los NPCs indicando el inicio del nuevo día y sus condiciones.
            string dailyContext = $"New day started. Season: {season}, Day: {day}, Weather: {weather}.{festivalInfo}";

            // Recorremos todos los NPCs para agregar la memoria de inicio del día
            foreach (var npc in Utility.getAllCharacters())
            {
                if (npc.IsVillager)
                {
                    this.Memory.AddMemory(npc.Name, dailyContext, 1, "neutral");
                }
            }

            // Process pending favors
            foreach (var npcName in this.Memory.GetAllNpcsWithMemories())
            {
                var favors = this.Memory.GetPendingFavorsAndClear(npcName);
                foreach (var favorArgs in favors)
                {
                    // Expecting format: ItemName, Amount, "Letter Text"
                    var match = System.Text.RegularExpressions.Regex.Match(favorArgs, @"^([^,]+),\s*(\d+),\s*""(.*)""$");
                    if (match.Success)
                    {
                        string itemName = match.Groups[1].Value.Trim();
                        int amount = int.Parse(match.Groups[2].Value);
                        string letterText = match.Groups[3].Value;

                        if (ItemCatalog.TryGetValue(itemName, out string itemId))
                        {
                            // Add to Mailbox
                            string mailId = $"StardewAIMod_Favor_{npcName}_{Game1.Date.TotalDays}_{System.Guid.NewGuid()}";

                            // 1.6 Mail format: "Text %item object (O)ItemId Amount %%"
                            string mailContent = $"{letterText} %item object (O){itemId} {amount} %%";

                            Game1.content.Load<System.Collections.Generic.Dictionary<string, string>>("Data\\mail").Add(mailId, mailContent);
                            Game1.player.mailbox.Add(mailId);

                            this.Monitor.Log($"[Studio Corvus] ✉️ Favor delivered from {npcName}: {amount}x {itemName}", LogLevel.Info);
                        }
                        else
                        {
                            this.Monitor.Log($"[Studio Corvus] ⚠️ Could not find item ID for favor item: {itemName}. Aborting silently.", LogLevel.Warn);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Se ejecuta en cada tick del juego.
        /// </summary>
        private void OnUpdateTicked(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            if (this.VoiceManager != null)
            {
                this.VoiceManager.Update();
            }
        }

        /// <summary>
        /// Se ejecuta cuando el jugador presiona cualquier tecla.
        /// </summary>
        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu != null)
                return;

            // Detectar tecla de voz (mantener presionada)
            if (e.Button.ToString() == this.Config.VoiceKey)
            {
                if (string.IsNullOrEmpty(this.Secrets.VeniceApiKey))
                {
                    Game1.addHUDMessage(new HUDMessage("Stardew AI: Missing Venice API Key in secrets.json!", HUDMessage.error_type));
                    return;
                }

                NPC targetNpc = FindClosestNpc(this.Config.MaxInteractionDistance);

                if (targetNpc != null)
                {
                    this.VoiceManager.StartRecording(targetNpc);
                }
            }
        }

        /// <summary>
        /// Se ejecuta cuando el jugador suelta una tecla.
        /// </summary>
        private void OnButtonReleased(object sender, ButtonReleasedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (e.Button.ToString() == this.Config.VoiceKey)
            {
                this.VoiceManager.StopRecordingAndProcess();
            }
        }

        /// <summary>
        /// Busca al NPC más cercano al jugador dentro de un radio.
        /// </summary>
        private NPC FindClosestNpc(float maxDistance)
        {
            NPC closest = null;
            float minDistance = float.MaxValue;
            var playerPos = Game1.player.Tile;

            foreach (var npc in Game1.currentLocation.characters)
            {
                if (!npc.IsVillager || !npc.CanSocialize) continue;

                float dist = Microsoft.Xna.Framework.Vector2.Distance(playerPos, npc.Tile);
                if (dist <= maxDistance && dist < minDistance)
                {
                    minDistance = dist;
                    closest = npc;
                }
            }

            return closest;
        }
    }
}