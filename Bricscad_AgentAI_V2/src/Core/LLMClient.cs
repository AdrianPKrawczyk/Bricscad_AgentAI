using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
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
        private readonly ToolOrchestrator _orchestrator;

        // Delegaty do aktualizacji interfejsu użytkownika w trybie asynchronicznym (niewątkującym bazy CAD)
        public event Action<string> OnStatusUpdate;
        public event Action<string> OnToolCallLogged;
        public event Action<LLMStats> OnStatsUpdate; // Obiekt ze statystykami
        public LLMStats LastStats { get; private set; }

        public LLMClient(ToolOrchestrator orchestrator)
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMinutes(5);
            _orchestrator = orchestrator;
        }

        /// <summary>
        /// Wysyła konwersację do serwera LLM i w razie zgłoszenia potrzeby użycia narzędzia (Tool Call)
        /// zarządza pętlą ReAct - samodzielnie wywołuje narzędzie i odsyła wynik.
        /// Zwraca obiekt AgentExecutionResult po zakończeniu cyklu Workera.
        /// </summary>
        public async Task<AgentExecutionResult> SendMessageReActAsync(List<ChatMessage> conversationHistory, IExecutionContext context, IEnumerable<string> initialTags = null, bool earlyExitEnabled = true, int maxIterations = 5, string profileName = null)
        {
            PreProcessRecipes(conversationHistory);

            var config = LLMConfigManager.GetActiveProvider();
            
            // Dynamiczne ładowanie modelu (tylko lokalnie dla LM Studio)
            if (config.AutoLoadModel && (config.EndpointUrl.Contains("1234") || config.EndpointUrl.Contains("localhost") || config.EndpointUrl.Contains("127.0.0.1") || config.EndpointUrl.Contains("100.104.")))
            {
                OnStatusUpdate?.Invoke("Inicjalizacja automatycznego ładowania modelu...");
                await TryLoadModelAsync(config);
            }

            List<ToolDefinition> staticToolsPayload;
            if (!string.IsNullOrEmpty(profileName))
            {
                staticToolsPayload = _orchestrator.GetToolsPayloadForProfile(profileName);
            }
            else
            {
                var currentTags = new HashSet<string>(initialTags ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
                staticToolsPayload = _orchestrator.GetToolsPayload(currentTags);
            }

            int iterations = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            int totalSentChars = 0;
            int totalRecvChars = 0;

            while (iterations < maxIterations)
            {
                iterations++;
                OnStatusUpdate?.Invoke($"Wysyłanie zapytania do struktury (iteracja {iterations}/{maxIterations})...");

                config = LLMConfigManager.GetActiveProvider();
                var requestPayload = new Dictionary<string, object>
                {
                    { "model", config.ModelName },
                    { "messages", conversationHistory },
                    { "tools", staticToolsPayload },
                    { "tool_choice", "auto" },
                    { "temperature", config.Temperature },
                    { "max_tokens", config.MaxTokens }
                };

                if (config.TopP > 0.0 && config.TopP != 1.0) requestPayload["top_p"] = config.TopP;

                // Safeguard dla oficjalnego API OpenAI/Azure OpenAI, które nie akceptują lokalnych parametrów samplingu
                bool isStrictOpenAI = config.EndpointUrl.Contains("api.openai.com") || config.EndpointUrl.Contains("openai.azure.com");
                if (!isStrictOpenAI)
                {
                    if (config.TopK > 0) requestPayload["top_k"] = config.TopK;
                    if (config.MinP > 0.0) requestPayload["min_p"] = config.MinP;
                    if (config.RepetitionPenalty > 0.0 && config.RepetitionPenalty != 1.0) requestPayload["repetition_penalty"] = config.RepetitionPenalty;
                }

                if (!string.IsNullOrEmpty(config.ReasoningEffort) && config.ReasoningEffort != "none")
                {
                    requestPayload["reasoning_effort"] = config.ReasoningEffort;
                }

                string jsonContent = JsonConvert.SerializeObject(requestPayload, new JsonSerializerSettings 
                { 
                    NullValueHandling = NullValueHandling.Ignore 
                });
                
                // Obliczamy długość wysłanych znaków (aproksymacja tokenów)
                int payloadLength = jsonContent.Length;
                totalSentChars += payloadLength;
                var request = new HttpRequestMessage(HttpMethod.Post, config.EndpointUrl);
                
                if (!string.IsNullOrEmpty(config.ApiKey) && config.ApiKey != "not-needed")
                {
                    request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                }
                
                if (config.EndpointUrl.Contains("openrouter"))
                {
                    if (!string.IsNullOrEmpty(config.SiteUrl)) request.Headers.Add("HTTP-Referer", config.SiteUrl);
                    if (!string.IsNullOrEmpty(config.SiteName)) request.Headers.Add("X-Title", config.SiteName);
                }

                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 2. Wyślij zapytanie
                HttpResponseMessage response;
                try
                {
                    BielikLogger.LogInfo($"[LLM REQ] Model={config.ModelName}, Endpoint={config.EndpointUrl}, Tools={staticToolsPayload?.Count ?? 0}, Iteration={iterations}");
                    response = await _httpClient.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    BielikLogger.LogError($"[LLM ERR] Połączenie nieudane: {ex.Message}", ex);
                    OnStatusUpdate?.Invoke("Błąd połączenia z lokalnym LLM API.");
                    return AgentExecutionResult.Failure($"Błąd połączenia: {ex.Message}");
                }

                string responseBody = await response.Content.ReadAsStringAsync();
                totalRecvChars += responseBody.Length;

                BielikLogger.LogInfo($"[LLM RESP] Status={(int)response.StatusCode} ({response.StatusCode}), BodyLength={responseBody.Length}");

                var jsonResponse = JObject.Parse(responseBody);
                var messageNode = jsonResponse["choices"]?[0]?["message"];
                if (messageNode == null)
                {
                    sw.Stop();
                    BielikLogger.LogWarn("[LLM WARN] Otrzymano nieprawidłową odpowiedź (brak węzła 'choices[0].message').");
                    return AgentExecutionResult.Failure("Błąd parsowania odpowiedzi z modelu (brak 'message').");
                }

                // Deserializacja asystenta
                var assistantMessage = messageNode.ToObject<ChatMessage>();
                conversationHistory.Add(assistantMessage);

                if (assistantMessage.ToolCalls != null && assistantMessage.ToolCalls.Any())
                {
                    foreach (var tc in assistantMessage.ToolCalls)
                    {
                        string argsStr = tc.Function?.Arguments;
                        if (argsStr != null && argsStr.Length > 200) argsStr = argsStr.Substring(0, 200) + "...";
                        BielikLogger.LogInfo($"[LLM TOOLCALL] ID={tc.Id}, Name={tc.Function?.Name}, Args={argsStr}");
                    }
                }
                else
                {
                    string contentStr = assistantMessage.Content?.ToString();
                    if (contentStr != null && contentStr.Length > 150) contentStr = contentStr.Substring(0, 150) + "...";
                    BielikLogger.LogInfo($"[LLM TEXT] Response: {contentStr}");
                }

                // 3. Sprawdź warunek zakończenia: jeśli brak wywołań funkcji -> koniec.
                if (assistantMessage.ToolCalls == null || !assistantMessage.ToolCalls.Any())
                {
                    sw.Stop();
                    OnStatusUpdate?.Invoke("Formułowanie ostatecznej odpowiedzi...");
                    TrimHistory(conversationHistory);
                    
                    // Zgłoś statystyki (aproksymacja 4 znaki = 1 token)
                    RaiseStatsUpdate(new LLMStats 
                    { 
                        TotalTimeMs = sw.ElapsedMilliseconds, 
                        PromptTokens = totalSentChars / 4, 
                        CompletionTokens = totalRecvChars / 4 
                    });
                    
                    return AgentExecutionResult.Success(assistantMessage.Content?.ToString() ?? "(Model nie zwrócił tekstu)");
                }

                // 4. Mamy tool_calls! Realizujemy ich logikę na lokalnej maszynie C#
                OnStatusUpdate?.Invoke($"Odebrano zapytanie narzędziowe. Liczba operacji do wykonania: {assistantMessage.ToolCalls.Count}.");
                
                // Flaga wczesnego wyjścia - inicjalnie true (jeśli włączone), resetowana jeśli dowolne narzędzie nie wspiera lub zawiedzie.
                bool canEarlyExitThisTurn = earlyExitEnabled && assistantMessage.ToolCalls.Any();

                foreach (var toolCall in assistantMessage.ToolCalls)
                {
                    var functionName = toolCall.Function.Name;
                    var argumentsString = toolCall.Function.Arguments;

                    // Logujemy surowy JSON wywołania do nowego interfejsu 'Logi Narzędzi'
                    OnToolCallLogged?.Invoke(JsonConvert.SerializeObject(toolCall, Newtonsoft.Json.Formatting.Indented));
                    OnStatusUpdate?.Invoke($"Uruchamiam narzędzie: {functionName}...");

                    JObject argumentsParsed;
                    string toolExecutionResult;
                    try
                    {
                        argumentsParsed = string.IsNullOrWhiteSpace(argumentsString) 
                            ? new JObject() 
                            : JObject.Parse(argumentsString);
                        
                        // Sprawdź właściwości narzędzia przed wykonaniem dla Early Exit
                        var toolSettings = ToolConfigManager.GetSettings(functionName);
                        if (toolSettings == null || !toolSettings.SupportsEarlyExit)
                        {
                            canEarlyExitThisTurn = false;
                        }

                        // Przekazanie kontekstu do doca (tymczasowy most dla ToolOrchestrator, który wymaga Doc)
                        toolExecutionResult = _orchestrator.ExecuteTool(functionName, argumentsParsed, context);

                        // Jeśli wynik zawiera błąd, nie możemy zrobić Early Exit
                        if (toolExecutionResult.ToLower().Contains("błąd") || toolExecutionResult.ToLower().Contains("error"))
                        {
                            canEarlyExitThisTurn = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        toolExecutionResult = $"Błąd podczas parsowania argumentów lub wywołania '{functionName}': {ex.Message}";
                    }

                    // 5. Dodaj odpowiedź z roli zastrzeżonej "tool"
                    // V2 VISION: Specjalne traktowanie zrzutów ekranu
                    if (toolExecutionResult.StartsWith("[VISION_IMAGE_CAPTURED]|"))
                    {
                        string imagePath = toolExecutionResult.Split('|')[1];
                        
                        // Dodajemy standardową odpowiedź 'tool' aby zamknąć strukturę wywołania
                        conversationHistory.Add(new ChatMessage
                        {
                            Role = "tool",
                            ToolCallId = toolCall.Id,
                            Content = "Obraz został przechwycony pomyślnie. Zrzut ekranu jest dołączony poniżej."
                        });

                        try
                        {
                            // Konwersja na Base64 i wstrzyknięcie roli 'user' (wymóg większości VLM)
                            byte[] imageBytes = System.IO.File.ReadAllBytes(imagePath);
                            string base64String = Convert.ToBase64String(imageBytes);

                            var visionContent = new List<VisionContentPart>
                            {
                                new VisionContentPart { Type = "text", Text = "Oto zrzut ekranu obszaru rysunku, o który prosiłeś. Przeanalizuj go uważnie." },
                                new VisionContentPart
                                {
                                    Type = "image_url",
                                    ImageUrl = new VisionImageUrl { Url = $"data:image/jpeg;base64,{base64String}" }
                                }
                            };

                            conversationHistory.Add(new ChatMessage { Role = "user", Content = visionContent });
                            OnStatusUpdate?.Invoke("Obraz wstrzyknięty do rozmowy. Czekam na analizę wizualną...");
                        }
                        catch (Exception ex)
                        {
                            OnStatusUpdate?.Invoke($"Błąd przetwarzania obrazu Vision: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Standardowa odpowiedź tekstowa
                        var toolResponseMessage = new ChatMessage
                        {
                            Role = "tool",
                            ToolCallId = toolCall.Id,
                            Content = toolExecutionResult
                        };
                        conversationHistory.Add(toolResponseMessage);
                    }
                }
                
                // KRYTYCZNE: Sprawdzenie Early Exit PO dodaniu wszystkich wyników do historii.
                if (canEarlyExitThisTurn)
                {
                    sw.Stop();
                    OnStatusUpdate?.Invoke("⚡ [Early Exit] Operacja wykonana pomyślnie. Zamykam pętlę ReAct.");
                    RaiseStatsUpdate(new LLMStats 
                    { 
                        TotalTimeMs = sw.ElapsedMilliseconds, 
                        PromptTokens = totalSentChars / 4, 
                        CompletionTokens = totalRecvChars / 4 
                    });
                    return AgentExecutionResult.Success("Operacja wykonana pomyślnie (Tryb Szybki).");
                }

                // Po obsłużeniu WSZYSTKICH narzedzi w tej paczce, pętla 'while' wróci na samą górę 
                OnStatusUpdate?.Invoke("Wyniki narzędzi przeanalizowane. Zwracam do modelu...");
            }

            sw.Stop();
            RaiseStatsUpdate(new LLMStats 
            { 
                TotalTimeMs = sw.ElapsedMilliseconds, 
                PromptTokens = totalSentChars / 4, 
                CompletionTokens = totalRecvChars / 4 
            });
            OnStatusUpdate?.Invoke("Przerwano zapętlenie (zbyt skomplikowany problem lub pętla logiczna LLMa).");
            return AgentExecutionResult.Failure("[LLMClient] Przekroczono maksymalną liczbę iteracji (pętla powtórzeń Tool Calls). Przerywam zadanie.");
        }

        /// <summary>
        /// Skanuje historię w poszukiwaniu triggerów recept ($trigger) i wstrzykuje 
        /// przykłady Few-Shot bezpośrednio po system prompcie.
        /// </summary>
        private void PreProcessRecipes(List<ChatMessage> history)
        {
            var userMsgs = history.Where(m => m.Role == "user" && m.Content != null).ToList();
            if (!userMsgs.Any()) return;

            var discoveredTriggers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var msg in userMsgs)
            {
                if (msg.Content is string textContent)
                {
                    var matches = Regex.Matches(textContent, @"\$(\w+)");
                    foreach (Match m in matches) discoveredTriggers.Add(m.Groups[1].Value);
                }
            }

            if (!discoveredTriggers.Any()) return;

            int injectionIdx = history.FindIndex(m => m.Role == "system") + 1;
            if (injectionIdx <= 0) injectionIdx = 0;

            foreach (var trigger in discoveredTriggers)
            {
                var recipe = RecipeManager.GetByTrigger(trigger);
                if (recipe != null)
                {
                    history.Insert(injectionIdx++, new ChatMessage 
                    { 
                        Role = "user", 
                        Content = $"PRZYKŁAD DLA TRIGGERA ${trigger}:\n{recipe.Description}" 
                    });

                    history.Insert(injectionIdx++, new ChatMessage 
                    { 
                        Role = "assistant", 
                        ToolCalls = recipe.ToolExample.ToObject<List<ToolCall>>() 
                    });

                    if (recipe.AutoLoadCategories != null)
                    {
                        foreach (var cat in recipe.AutoLoadCategories)
                        {
                            ToolConfigManager.SessionDynamicTags.Add(cat);
                        }
                    }
                }
            }
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
                if (message.Role == "tool" && message.Content is string contentStr && contentStr.Length > maxLength)
                {
                    int originalLength = contentStr.Length;
                    message.Content = $"{contentStr.Substring(0, 100)}... [PRZYCIĘTO {originalLength - 100} znaków dla oszczędności tokenów]";
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
            string profileName = null,
            int maxIterations = 10,
            CancellationToken ct = default)
        {
            simulatedResponses = simulatedResponses ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int iterations = 0;

            while (iterations < maxIterations)
            {
                if (ct.IsCancellationRequested) break;
                iterations++;

                var config = LLMConfigManager.GetActiveProvider();
                
                List<ToolDefinition> toolsPayload;
                if (!string.IsNullOrEmpty(profileName))
                {
                    toolsPayload = _orchestrator.GetToolsPayloadForProfile(profileName);
                }
                else
                {
                    toolsPayload = _orchestrator.GetToolsPayload(new[] { "#all" });
                }

                // 1. Buduj i wyślij payload do prawdziwego LLM
                var requestPayload = new Dictionary<string, object>
                {
                    { "model", config.ModelName },
                    { "messages", history },
                    { "tools", toolsPayload },
                    { "tool_choice", "auto" },
                    { "temperature", config.Temperature },
                    { "max_tokens", config.MaxTokens }
                };

                if (config.TopP > 0.0 && config.TopP != 1.0) requestPayload["top_p"] = config.TopP;

                // Safeguard dla oficjalnego API OpenAI/Azure OpenAI, które nie akceptują lokalnych parametrów samplingu
                bool isStrictOpenAI = config.EndpointUrl.Contains("api.openai.com") || config.EndpointUrl.Contains("openai.azure.com");
                if (!isStrictOpenAI)
                {
                    if (config.TopK > 0) requestPayload["top_k"] = config.TopK;
                    if (config.MinP > 0.0) requestPayload["min_p"] = config.MinP;
                    if (config.RepetitionPenalty > 0.0 && config.RepetitionPenalty != 1.0) requestPayload["repetition_penalty"] = config.RepetitionPenalty;
                }

                if (!string.IsNullOrEmpty(config.ReasoningEffort) && config.ReasoningEffort != "none")
                {
                    requestPayload["reasoning_effort"] = config.ReasoningEffort;
                }

                string jsonContent = JsonConvert.SerializeObject(requestPayload, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, config.EndpointUrl);
                
                if (!string.IsNullOrEmpty(config.ApiKey) && config.ApiKey != "not-needed")
                {
                    request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                }
                
                if (config.EndpointUrl.Contains("openrouter"))
                {
                    if (!string.IsNullOrEmpty(config.SiteUrl)) request.Headers.Add("HTTP-Referer", config.SiteUrl);
                    if (!string.IsNullOrEmpty(config.SiteName)) request.Headers.Add("X-Title", config.SiteName);
                }

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

                    // Szukamy mockowanej odpowiedzi lub wykonujemy realne narzędzia matematyczno-pomocnicze
                    string mockContent;
                    if (string.Equals(toolName, "CalculateRpn", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(toolName, "ReadFromBlackboard", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(toolName, "WriteToBlackboard", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var context = new CadExecutionContext(doc);
                            mockContent = _orchestrator.ExecuteTool(toolName, arguments, context);
                            OnStatusUpdate?.Invoke($"[Benchmark] Wykonano realne narzędzie {toolName} -> Wynik: {mockContent}");
                        }
                        catch (Exception ex)
                        {
                            mockContent = $"BŁĄD REALNEGO WYKONANIA ({toolName}): {ex.Message}";
                            OnStatusUpdate?.Invoke($"[Benchmark] {mockContent}");
                        }
                    }
                    else
                    {
                        if (!simulatedResponses.TryGetValue(toolName, out mockContent))
                            mockContent = $"[MOCK] Narzędzie '{toolName}' zostało wywołane.";
                    }

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

        /// <summary>
        /// Wysyła żądanie ładowania modelu do LM Studio REST API.
        /// </summary>
        private async Task TryLoadModelAsync(LLMProviderConfig config)
        {
            try
            {
                string baseUrl = config.EndpointUrl;
                if (baseUrl.Contains("/v1/chat/completions"))
                {
                    baseUrl = baseUrl.Replace("/v1/chat/completions", "");
                }
                else if (baseUrl.Contains("/chat/completions"))
                {
                    baseUrl = baseUrl.Replace("/chat/completions", "");
                }
                
                string loadUrl = baseUrl.TrimEnd('/') + "/api/v1/models/load";

                var loadPayload = new Dictionary<string, object>
                {
                    { "model", config.ModelName }
                };
                if (config.LoadContextLength > 0)
                {
                    loadPayload["context_length"] = config.LoadContextLength;
                }

                if (config.TtlSeconds > 0)
                {
                    loadPayload["ttl"] = config.TtlSeconds;
                }

                if (config.FlashAttention)
                {
                    loadPayload["flash_attention"] = true;
                }

                if (config.OffloadKvCache)
                {
                    loadPayload["offload_kv_cache_to_gpu"] = true;
                }

                string jsonContent = JsonConvert.SerializeObject(loadPayload);
                var request = new HttpRequestMessage(HttpMethod.Post, loadUrl);
                
                if (!string.IsNullOrEmpty(config.ApiKey) && config.ApiKey != "not-needed")
                {
                    request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                }

                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    string errorMsg = await response.Content.ReadAsStringAsync();
                    OnStatusUpdate?.Invoke($"[Auto-Load] Błąd ładowania: {response.StatusCode} - {errorMsg}");
                }
                else
                {
                    OnStatusUpdate?.Invoke($"[Auto-Load] Model {config.ModelName} załadowany pomyślnie.");
                }
            }
            catch (Exception ex)
            {
                OnStatusUpdate?.Invoke($"[Auto-Load] Wyjątek podczas ładowania: {ex.Message}");
            }
        }

        private void RaiseStatsUpdate(LLMStats stats)
        {
            LastStats = stats;
            OnStatsUpdate?.Invoke(stats);
        }
    }
}
