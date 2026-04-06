using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
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
        public event Action<long, int, int> OnStatsUpdate; // ms, sentTokens, recvTokens

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
        public async Task<string> SendMessageReActAsync(List<ChatMessage> conversationHistory, Document doc, IEnumerable<string> initialTags = null, int maxIterations = 5)
        {
            var currentTags = new HashSet<string>(initialTags ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            int iterations = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int totalSentChars = 0;
            int totalRecvChars = 0;

            while (iterations < maxIterations)
            {
                iterations++;
                OnStatusUpdate?.Invoke($"Wysyłanie zapytania do struktury (iteracja {iterations}/{maxIterations})...");

                // 1. Przygotuj payload
                var requestPayload = new
                {
                    model = "local-model",
                    messages = conversationHistory,
                    tools = _orchestrator.GetToolsPayload(currentTags),
                    tool_choice = "auto"
                };

                string jsonContent = JsonConvert.SerializeObject(requestPayload, new JsonSerializerSettings 
                { 
                    NullValueHandling = NullValueHandling.Ignore 
                });
                totalSentChars += jsonContent.Length;
                
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
                    sw.Stop();
                    OnStatusUpdate?.Invoke("Błąd połączenia z lokalnym LLM API.");
                    return $"Błąd połączenia: {ex.Message}";
                }

                string responseBody = await response.Content.ReadAsStringAsync();
                totalRecvChars += responseBody.Length;

                var jsonResponse = JObject.Parse(responseBody);
                var messageNode = jsonResponse["choices"]?[0]?["message"];
                if (messageNode == null)
                {
                    sw.Stop();
                    return "Błąd parsowania odpowiedzi z modelu (brak 'message').";
                }

                // Deserializacja asystenta
                var assistantMessage = messageNode.ToObject<ChatMessage>();
                conversationHistory.Add(assistantMessage);

                // 3. Sprawdź warunek zakończenia: jeśli brak wywołań funkcji -> koniec.
                if (assistantMessage.ToolCalls == null || !assistantMessage.ToolCalls.Any())
                {
                    sw.Stop();
                    OnStatusUpdate?.Invoke("Formułowanie ostatecznej odpowiedzi...");
                    TrimHistory(conversationHistory);
                    
                    // Zgłoś statystyki (aproksymacja 4 znaki = 1 token)
                    OnStatsUpdate?.Invoke(sw.ElapsedMilliseconds, totalSentChars / 4, totalRecvChars / 4);
                    
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

                        // Agentic Fallback: Jeśli model poprosił o dodatkowe pule narzędzi, 
                        // aktualizujemy lokalny zbiór tagów dla następnych iteracji pętli ReAct.
                        // Agentic Fallback: Obsługa nowego formatu RequestAdditionalTools
                        if (functionName.Equals("RequestAdditionalTools", StringComparison.OrdinalIgnoreCase))
                        {
                            string action = argumentsParsed["Action"]?.ToString() ?? "";
                            if (action.Equals("LoadCategory", StringComparison.OrdinalIgnoreCase))
                            {
                                string tag = argumentsParsed["CategoryName"]?.ToString() ?? "";
                                if (!string.IsNullOrEmpty(tag))
                                {
                                    tag = tag.Trim();
                                    if (!tag.StartsWith("#")) tag = "#" + tag;
                                    currentTags.Add(tag);
                                }
                            }
                        }
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

            sw.Stop();
            OnStatsUpdate?.Invoke(sw.ElapsedMilliseconds, totalSentChars / 4, totalRecvChars / 4);
            OnStatusUpdate?.Invoke("Przerwano zapętlenie (zbyt skomplikowany problem lub pętla logiczna LLMa).");
            return "[LLMClient] Przekroczono maksymalną liczbę iteracji (pętla powtórzeń Tool Calls). Przerywam zadanie.";
        }

        /// <summary>
        /// Skraca historię konwersacji, zastępując obszerne wyniki narzędzi krótkimi podsumowaniami,
        /// aby oszczędzać okno kontekstowe bez utraty struktury tool calling.
        /// </summary>
        private void TrimHistory(List<ChatMessage> history)
        {
            const int maxLength = 500;
            foreach (var message in history)
            {
                if (message.Role == "tool" && message.Content?.Length > maxLength)
                {
                    int originalLength = message.Content.Length;
                    message.Content = $"{message.Content.Substring(0, 100)}... [PRZYCIĘTO {originalLength - 100} znaków dla oszczędności tokenów]";
                }
            }
        }

        /// <summary>
        /// Benchmark Mode: Wysyła konwersację do prawdziwego LLM, ale zamiast wywoływać realne narzędzia CAD,
        /// wstrzykuje z góry przygotowane odpowiedzi (SimulatedCADResponses) jako wiadomości roli "tool".
        /// Rejestruje wszystkie wywołania w liście recordedCalls dla późniejszej walidacji.
        /// </summary>
        public async Task SendMessageBenchmarkAsync(
            List<ChatMessage> history,
            Dictionary<string, string> simulatedResponses,
            List<RecordedToolCall> recordedCalls,
            Document doc,
            int maxIterations = 10,
            CancellationToken ct = default)
        {
            simulatedResponses = simulatedResponses ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int iterations = 0;

            while (iterations < maxIterations)
            {
                if (ct.IsCancellationRequested) break;
                iterations++;

                // 1. Buduj i wyślij payload do prawdziwego LLM
                var requestPayload = new
                {
                    model = "local-model",
                    messages = history,
                    tools = _orchestrator.GetToolsPayload(),
                    tool_choice = "auto"
                };

                string jsonContent = JsonConvert.SerializeObject(requestPayload, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, _endpointUrl);
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");
                request.Content = new System.Net.Http.StringContent(jsonContent, Encoding.UTF8, "application/json");

                System.Net.Http.HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(request, ct);
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    OnStatusUpdate?.Invoke($"[Benchmark] Błąd połączenia z LLM: {ex.Message}");
                    return;
                }

                string responseBody = await response.Content.ReadAsStringAsync();
                var jsonResponse = JObject.Parse(responseBody);
                var messageNode = jsonResponse["choices"]?[0]?["message"];
                if (messageNode == null) return;

                var assistantMessage = messageNode.ToObject<ChatMessage>();
                history.Add(assistantMessage);

                // 2. Sprawdź warunek zakończenia – LLM nie chce wywoływać narzędzi
                if (assistantMessage.ToolCalls == null || !assistantMessage.ToolCalls.Any())
                    return;

                // 3. LLM chce wywołać narzędzia – przechwytujemy i mockujemy zamiast wykonywać
                foreach (var toolCall in assistantMessage.ToolCalls)
                {
                    if (ct.IsCancellationRequested) break;

                    string toolName = toolCall.Function.Name;
                    JObject arguments = null;
                    try
                    {
                        arguments = string.IsNullOrWhiteSpace(toolCall.Function.Arguments)
                            ? new JObject()
                            : JObject.Parse(toolCall.Function.Arguments);
                    }
                    catch { arguments = new JObject(); }

                    // Rejestrujemy wywołanie (materiał dowodowy dla walidatora)
                    recordedCalls.Add(new RecordedToolCall { ToolName = toolName, Arguments = arguments });
                    OnStatusUpdate?.Invoke($"[Benchmark] Przechwycono wywołanie: {toolName}");

                    // Szukamy mockowanej odpowiedzi – jeśli brak, zwracamy domyślny string
                    string mockContent;
                    if (!simulatedResponses.TryGetValue(toolName, out mockContent))
                        mockContent = $"[MOCK] Narzędzie '{toolName}' zostało wywołane.";

                    // 4. Wstrzykujemy odpowiedź jako wiadomość roli "tool" (standard OpenAI)
                    history.Add(new ChatMessage
                    {
                        Role = "tool",
                        ToolCallId = toolCall.Id,
                        Content = mockContent
                    });
                }
            }
        }
    }
}
