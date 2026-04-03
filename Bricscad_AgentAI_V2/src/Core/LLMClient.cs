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
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                string responseBody = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseBody);
                
                var messageNode = jsonResponse["choices"]?[0]?["message"];
                if (messageNode == null)
                    return "Błąd parsowania odpowiedzi z modelu (brak 'message').";

                // Deserializacja asystenta
                var assistantMessage = messageNode.ToObject<ChatMessage>();
                
                // Od razu dodaj odpowiedź asystenta do historii, zanim zareagujemy.
                conversationHistory.Add(assistantMessage);

                // 3. Sprawdź warunek zakończenia: jeśli brak wywołań funkcji -> koniec.
                if (assistantMessage.ToolCalls == null || !assistantMessage.ToolCalls.Any())
                {
                    return assistantMessage.Content ?? "(Model nie zwrócił tekstu)";
                }

                // 4. Mamy tool_calls! Realizujemy ich logikę na lokalnej maszynie C#
                foreach (var toolCall in assistantMessage.ToolCalls)
                {
                    var functionName = toolCall.Function.Name;
                    var argumentsString = toolCall.Function.Arguments;

                    JObject argumentsParsed;
                    string toolExecutionResult;
                    try
                    {
                        // Zabezpieczenie przed twardym JSON stringiem
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
                // i automatycznie (rekurencyjnie) prześle do LLM zaktualizowaną historię.
            }

            return "[LLMClient] Przekroczono maksymalną liczbę iteracji (pętla powtórzeń Tool Calls). Przerywam zadanie.";
        }
    }
}
