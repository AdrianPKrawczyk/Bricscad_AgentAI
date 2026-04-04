using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Klient API do komunikacji z modelem językowym (kompatybilność z OpenAI API).
    /// Obsługuje natywne parsowanie 'tool_calls' i rekurencyjną pętlę ReAct.
    /// </summary>
    public class LLMClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _endpointUrl;
        private readonly string _apiKey;
        private readonly ToolOrchestrator _orchestrator;

        // Delegaty do aktualizacji interfejsu użytkownika w trybie asynchronicznym (niewątkującym bazy CAD)
        public event Action<string> OnStatusUpdate;
        public event Action<string> OnToolCallLogged;

        public LLMClient(string endpointUrl, string apiKey, ToolOrchestrator orchestrator)
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _endpointUrl = endpointUrl;
            _apiKey = apiKey;
            _orchestrator = orchestrator;
        }

        /// <summary>
        /// Wysyła konwersację do serwera LLM i w razie zgłoszenia potrzeby użycia narzędzia (Tool Call)
        /// zarządza pętlą ReAct - samodzielnie wywołuje narzędzie i odsyła wynik.
        /// </summary>
        public async Task<string> SendMessageReActAsync(List<ChatMessage> conversationHistory, Document doc, int maxIterations = 5)
        {
            int iterations = 0;

            while (iterations < maxIterations)
            {
                iterations++;
                OnStatusUpdate?.Invoke($"Wysyłanie zapytania do struktury (iteracja {iterations}/{maxIterations})...");

                // 1. Przygotuj payload
                var requestPayload = new
                {
                    model = "local-model", // do konfiguracji / zmiany
                    messages = conversationHistory,
                    tools = _orchestrator.GetToolsPayload(),
                    tool_choice = "auto"
                };

                string jsonContent = JsonConvert.SerializeObject(requestPayload, new JsonSerializerSettings 
                { 
                    NullValueHandling = NullValueHandling.Ignore 
                });
                
                var request = new HttpRequestMessage(HttpMethod.Post, _endpointUrl);
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 2. Wyślij zapytanie
                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    OnStatusUpdate?.Invoke("Błąd połączenia z lokalnym LLM API.");
                    return $"Błąd połączenia: {ex.Message}";
                }

                string responseBody = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseBody);
                
                var messageNode = jsonResponse["choices"]?[0]?["message"];
                if (messageNode == null)
                    return "Błąd parsowania odpowiedzi z modelu (brak 'message').";

                // Deserializacja asystenta
                var assistantMessage = messageNode.ToObject<ChatMessage>();
                conversationHistory.Add(assistantMessage);

                // 3. Sprawdź warunek zakończenia: jeśli brak wywołań funkcji -> koniec.
                if (assistantMessage.ToolCalls == null || !assistantMessage.ToolCalls.Any())
                {
                    OnStatusUpdate?.Invoke("Formułowanie ostatecznej odpowiedzi...");
                    return assistantMessage.Content ?? "(Model nie zwrócił tekstu)";
                }

                // 4. Mamy tool_calls! Realizujemy ich logikę na lokalnej maszynie C#
                OnStatusUpdate?.Invoke($"Odebrano zapytanie narzędziowe. Liczba operacji do wykonania: {assistantMessage.ToolCalls.Count}.");
                foreach (var toolCall in assistantMessage.ToolCalls)
                {
                    var functionName = toolCall.Function.Name;
                    var argumentsString = toolCall.Function.Arguments;

                    // Logujemy surowy JSON wywołania do nowego interfejsu 'Logi Narzędzi'
                    OnToolCallLogged?.Invoke(JsonConvert.SerializeObject(toolCall, Formatting.Indented));
                    OnStatusUpdate?.Invoke($"Uruchamiam narzędzie: {functionName}...");

                    JObject argumentsParsed;
                    string toolExecutionResult;
                    try
                    {
                        argumentsParsed = string.IsNullOrWhiteSpace(argumentsString) 
                            ? new JObject() 
                            : JObject.Parse(argumentsString);
                        
                        toolExecutionResult = _orchestrator.ExecuteTool(functionName, argumentsParsed, doc);
                    }
                    catch (Exception ex)
                    {
                        toolExecutionResult = $"Błąd podczas parsowania argumentów lub wywołania '{functionName}': {ex.Message}";
                    }

                    // 5. Dodaj odpowiedź z roli zastrzeżonej "tool"
                    var toolResponseMessage = new ChatMessage
                    {
                        Role = "tool",
                        ToolCallId = toolCall.Id,
                        Content = toolExecutionResult
                    };
                    
                    conversationHistory.Add(toolResponseMessage);
                }
                
                // Po obsłużeniu WSZYSTKICH narzedzi w tej paczce, pętla 'while' wróci na samą górę 
                OnStatusUpdate?.Invoke("Wyniki narzędzi przeanalizowane. Zwracam do modelu...");
            }

            OnStatusUpdate?.Invoke("Przerwano zapętlenie (zbyt skomplikowany problem lub pętla logiczna LLMa).");
            return "[LLMClient] Przekroczono maksymalną liczbę iteracji (pętla powtórzeń Tool Calls). Przerywam zadanie.";
        }
    }
}
