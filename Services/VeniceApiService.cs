using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StardewAIMod.Models;

namespace StardewAIMod.Services
{
    /// <summary>
    /// Servicio para comunicarse con Venice AI API.
    /// Compatible con formato OpenAI.
    /// </summary>
    public class VeniceApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly string _endpoint;

        // Cooldown configuration
        private DateTime _lastRequestTime = DateTime.MinValue;
        private readonly TimeSpan _minTimeBetweenRequests = TimeSpan.FromSeconds(3); // 3 seconds cooldown

        public VeniceApiService(string apiKey, string model, string endpoint)
        {
            _apiKey = apiKey;
            _model = model;
            _endpoint = endpoint;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        /// <summary>
        /// Envía un mensaje al modelo de Venice y recibe la respuesta del NPC.
        /// </summary>
        /// <param name="systemPrompt">Personalidad + contexto del NPC.</param>
        /// <param name="conversationHistory">Historial de la conversación actual.</param>
        /// <returns>Texto de respuesta del NPC.</returns>
        public async Task<string> SendMessageAsync(
            string systemPrompt,
            List<ChatMessage> conversationHistory)
        {
            // Cooldown logic
            TimeSpan timeSinceLastRequest = DateTime.Now - _lastRequestTime;
            if (timeSinceLastRequest < _minTimeBetweenRequests)
            {
                await Task.Delay(_minTimeBetweenRequests - timeSinceLastRequest);
            }

            int maxRetries = 3;
            int currentTry = 0;

            while (currentTry < maxRetries)
            {
                try
                {
                    // Construir mensajes en formato OpenAI
                var messages = new List<object>
                {
                    new { role = "system", content = systemPrompt }
                };

                foreach (var msg in conversationHistory)
                {
                    string role = msg.Role == "player" ? "user" : "assistant";
                    messages.Add(new { role = role, content = msg.Content });
                }

                // Construir el body del request
                var requestBody = new
                {
                    model = _model,
                    messages = messages,
                    max_tokens = 300,
                    temperature = 0.8,
                    venice_parameters = new
                    {
                        include_venice_system_prompt = false
                    }
                };

                string json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                    // Update last request time right before making the call
                    _lastRequestTime = DateTime.Now;

                    // Enviar request
                    var response = await _httpClient.PostAsync(_endpoint, content);
                    string responseJson = await response.Content.ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        if ((int)response.StatusCode == 429) // Too Many Requests
                        {
                            currentTry++;
                            if (currentTry >= maxRetries)
                            {
                                return "[Venice API Error: Too many requests, please wait a moment.]";
                            }
                            // Exponential backoff: 2s, 4s, 8s...
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, currentTry)));
                            continue; // Retry
                        }

                        return $"[Venice API Error: {response.StatusCode}]";
                    }

                    // Parsear respuesta
                using var doc = JsonDocument.Parse(responseJson);
                var root = doc.RootElement;
                string reply = root
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();

                    return reply ?? "[No response from Venice]";
                }
                catch (Exception ex)
                {
                    return $"[Error: {ex.Message}]";
                }
            }

            return "[Venice API Error: Max retries reached.]";
        }

        /// <summary>
        /// Test rápido de conexión con Venice.
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var testMessages = new List<ChatMessage>
                {
                    new ChatMessage { Role = "player", Content = "Hello" }
                };
                string result = await SendMessageAsync("Reply with: CONNECTION OK", testMessages);
                return !result.StartsWith("[");
            }
            catch
            {
                return false;
            }
        }
    }
}