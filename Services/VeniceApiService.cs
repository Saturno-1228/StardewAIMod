using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using StardewModdingAPI;

namespace StardewLivingValley.Services
{
    /// <summary>
    /// Servicio para comunicarse con la API de Venice AI.
    /// </summary>
    public class VeniceApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private const string ApiUrl = "https://api.venice.ai/api/v1/chat/completions";
        private const string ModelName = "e2ee-glm-4-7-flash-p";

        public VeniceApiService(string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        /// <summary>
        /// Envía un mensaje a la API de Venice y devuelve la respuesta.
        /// </summary>
        /// <param name="userMessage">El mensaje transcrito del jugador.</param>
        /// <returns>La respuesta del NPC generada por la IA.</returns>
        public async Task<string> GetNpcResponseAsync(string npcName, string userMessage)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return "[Error] La Venice API Key no está configurada en SecretConfig.json.";
            }

            try
            {
                var requestBody = new
                {
                    model = ModelName,
                    messages = new[]
                    {
                        new { role = "system", content = $"Eres {npcName} de Stardew Valley. Responde de manera corta y natural a lo que te dice el granjero." },
                        new { role = "user", content = userMessage }
                    },
                    venice_parameters = new
                    {
                        include_venice_system_prompt = false
                    }
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(ApiUrl, jsonContent);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    ModEntry.Logger?.Log($"Error de Venice API: {response.StatusCode} - {errorContent}", LogLevel.Error);
                    return "[Error] Hubo un problema al contactar a la IA.";
                }

                var responseString = await response.Content.ReadAsStringAsync();

                // Parseando la respuesta JSON de OpenAI format
                using JsonDocument doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    if (firstChoice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content))
                    {
                        return content.GetString()?.Trim() ?? "[No response]";
                    }
                }

                return "[No response] Formato de respuesta inesperado.";
            }
            catch (Exception ex)
            {
                ModEntry.Logger?.Log($"Excepción al contactar Venice API: {ex.Message}", LogLevel.Error);
                return "[Error] Excepción al procesar la solicitud de red.";
            }
        }
    }
}