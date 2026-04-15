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
    public class LoreRoutingService
    {
        public static readonly ConcurrentDictionary<string, NpcIdentityDto> NpcIdentityCache = new();
        private readonly IMonitor _monitor;

        public LoreRoutingService(IMonitor monitor)
        {
            _monitor = monitor;
        }

        public async Task IngestAllLoreAsync(string modDirectory)
        {
            _monitor.Log("Starting Soul Router (LoreRoutingService) ingestion...", LogLevel.Info);

            string charactersPath = Path.Combine(modDirectory, "Assets", "Characters");
            if (!Directory.Exists(charactersPath))
            {
                _monitor.Log($"Characters directory not found at {charactersPath}. Aborting ingestion.", LogLevel.Warn);
                return;
            }

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

                            if (fileName.StartsWith("01"))
                            {
                                using var stream = File.OpenRead(file);
                                var identity = await JsonSerializer.DeserializeAsync<NpcIdentityDto>(stream, LoreJsonContext.Default.NpcIdentityDto);
                                if (identity.NpcName != null)
                                {
                                    NpcIdentityCache.TryAdd(identity.NpcName, identity);
                                }
                            }
                            else if (fileName.StartsWith("02") || fileName.StartsWith("03"))
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

            _monitor.Log($"Soul Router ingestion complete. {NpcIdentityCache.Count} identities loaded into RAM Cache.", LogLevel.Info);
        }
    }
}
