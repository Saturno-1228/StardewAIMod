using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading.Channels;
using System.Threading.Tasks;
using LivingCompanionsValley.Models;
using StardewModdingAPI;

namespace LivingCompanionsValley.Services
{
    using System.Collections.Generic;

    public class LoreRoutingService
    {
        public static readonly ConcurrentDictionary<string, NpcIdentityDto> NpcIdentityCache = new();
        public static readonly List<string> KnownNpcs = new();
        private readonly IMonitor _monitor;

        public LoreRoutingService(IMonitor monitor)
        {
            _monitor = monitor;
        }

        private void EnsureNpcFoldersExist(string charactersPath)
        {
            string[] coreNpcs = { "Alex", "Elliott", "Harvey", "Sam", "Sebastian", "Shane", "Abigail", "Emily", "Haley", "Leah", "Maru", "Penny", "Caroline", "Clint", "Demetrius", "Dwarf", "Evelyn", "George", "Gus", "Jas", "Jodi", "Kent", "Krobus", "Leo", "Lewis", "Linus", "Marnie", "Pam", "Pierre", "Robin", "Sandy", "Vincent", "Willy", "Wizard" };
            int carpetasCreadas = 0;
            int archivosCreados = 0;

            foreach (var npcName in coreNpcs)
            {
                string npcPath = Path.Combine(charactersPath, npcName);

                if (!Directory.Exists(npcPath))
                {
                    Directory.CreateDirectory(npcPath);
                    carpetasCreadas++;
                }

                string pFile = Path.Combine(npcPath, "01_Personalidad.json");
                if (!File.Exists(pFile))
                {
                    File.WriteAllText(pFile, "{\"NpcName\": \"" + npcName + "\", \"TonalStyle\": \"\", \"SystemPrompt\": \"\"}");
                    archivosCreados++;
                }

                string[] emptyFiles = { "02_Informacion.json", "03_Relaciones.json", "04_Historia.json", "05_Secretos.json" };
                foreach (var f in emptyFiles)
                {
                    string fPath = Path.Combine(npcPath, f);
                    if (!File.Exists(fPath))
                    {
                        File.WriteAllText(fPath, "[]");
                        archivosCreados++;
                    }
                }
            }

            if (carpetasCreadas > 0 || archivosCreados > 0)
            {
                _monitor.Log($"Living Companions Valley: Se aseguraron las plantillas de Lore. (Nuevas carpetas: {carpetasCreadas}, Nuevos archivos: {archivosCreados})", LogLevel.Info);
            }
        }

        public async Task IngestAllLoreAsync(string modDirectory)
        {
            _monitor.Log("Starting Soul Router (LoreRoutingService) ingestion...", LogLevel.Info);

            KnownNpcs.Clear();

            string charactersPath = Path.Combine(modDirectory, "Assets", "Characters");
            if (!Directory.Exists(charactersPath))
            {
                Directory.CreateDirectory(charactersPath);
            }

            EnsureNpcFoldersExist(charactersPath);

            var channel = Channel.CreateUnbounded<string>();

            // Producer
            var producer = Task.Run(() =>
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(charactersPath, "*.json", SearchOption.AllDirectories))
                    {
                        channel.Writer.TryWrite(file);
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log($"Error enumerating files in {charactersPath}: {ex.Message}", LogLevel.Error);
                }
                finally
                {
                    channel.Writer.Complete();
                }
            });

            // Consumers
            int processorCount = Environment.ProcessorCount;
            var consumers = new Task[processorCount];

            for (int i = 0; i < processorCount; i++)
            {
                consumers[i] = Task.Run(async () =>
                {
                    await foreach (var file in channel.Reader.ReadAllAsync())
                    {
                        try
                        {
                            string fileName = Path.GetFileName(file);

                            if (fileName == "01_Personalidad.json")
                            {
                                using var stream = File.OpenRead(file);
                                var identity = await JsonSerializer.DeserializeAsync<NpcIdentityDto>(stream, LoreJsonContext.Default.NpcIdentityDto);
                                if (identity.NpcName != null)
                                {
                                    NpcIdentityCache.TryAdd(identity.NpcName, identity);
                                }
                            }
                            else if (fileName.StartsWith("02") || fileName.StartsWith("03") || fileName.StartsWith("04") || fileName.StartsWith("05"))
                            {
                                // TODO: Enrutar a LiteDB v6 en la siguiente fase.
                            }
                        }
                        catch (Exception ex)
                        {
                            _monitor.Log($"Failed to route file {file}: {ex.Message}", LogLevel.Error);
                        }
                    }
                });
            }

            await Task.WhenAll(consumers);

            var directories = Directory.GetDirectories(charactersPath);
            foreach (var dir in directories)
            {
                KnownNpcs.Add(Path.GetFileName(dir));
            }

            _monitor.Log($"Soul Router ingestion complete. {NpcIdentityCache.Count} identities loaded into RAM Cache.", LogLevel.Info);
        }
    }
}
