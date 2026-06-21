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
        public event Action<string> OnLoopLogged;
        public event Action<LLMStats> OnStatsUpdate; // Obiekt ze statystykami
        public LLMStats LastStats { get; private set; }
        public string LastVisionOcrDiagnostics { get; private set; } = string.Empty;

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
            PreProcessLisps(conversationHistory);

            var config = LLMConfigManager.ResolveProviderForProfile(profileName);
            var llmBinding = ToolConfigManager.GetAgentLlmBinding(profileName);
            
            // Dynamiczne ładowanie modelu (tylko lokalnie dla LM Studio)
            if (config.AutoLoadModel && SupportsLocalModelManagement(config))
            {
                OnStatusUpdate?.Invoke("Inicjalizacja automatycznego ładowania modelu...");
                await TryLoadModelAsync(config, llmBinding?.ContextPolicy);
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
                LogLoop(profileName, $"[ITERACJA {iterations}/{maxIterations}] Wysylam rozmowe do modelu.");
                OnStatusUpdate?.Invoke($"Wysyłanie zapytania do struktury (iteracja {iterations}/{maxIterations})...");

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
                var usageNode = jsonResponse["usage"];
                
                int currentPromptTokens = totalSentChars / 4;
                int currentCompletionTokens = totalRecvChars / 4;

                if (usageNode != null)
                {
                    currentPromptTokens = usageNode["prompt_tokens"]?.Value<int>() ?? currentPromptTokens;
                    currentCompletionTokens = usageNode["completion_tokens"]?.Value<int>() ?? currentCompletionTokens;
                }

                if (messageNode == null)
                {
                    sw.Stop();
                    BielikLogger.LogWarn("[LLM WARN] Otrzymano nieprawidłową odpowiedź (brak węzła 'choices[0].message').");
                    return AgentExecutionResult.Failure("Błąd parsowania odpowiedzi z modelu (brak 'message').");
                }

                // Zgłoś statystyki bieżącego kroku
                RaiseStatsUpdate(new LLMStats 
                { 
                    TotalTimeMs = sw.ElapsedMilliseconds, 
                    PromptTokens = currentPromptTokens, 
                    CompletionTokens = currentCompletionTokens 
                });

                // Deserializacja asystenta
                var assistantMessage = messageNode.ToObject<ChatMessage>();
                conversationHistory.Add(assistantMessage);

                if (assistantMessage.ToolCalls != null && assistantMessage.ToolCalls.Any())
                {
                    var loopTools = new List<string>();
                    foreach (var tc in assistantMessage.ToolCalls)
                    {
                        string argsStr = tc.Function?.Arguments;
                        if (argsStr != null && argsStr.Length > 200) argsStr = argsStr.Substring(0, 200) + "...";
                        BielikLogger.LogInfo($"[LLM TOOLCALL] ID={tc.Id}, Name={tc.Function?.Name}, Args={argsStr}");
                        loopTools.Add($"{tc.Function?.Name}({argsStr})");
                    }
                    LogLoop(profileName, "[ASSISTANT -> TOOL]\n" + string.Join("\n", loopTools));
                }
                else
                {
                    string contentStr = assistantMessage.Content?.ToString();
                    if (contentStr != null && contentStr.Length > 150) contentStr = contentStr.Substring(0, 150) + "...";
                    BielikLogger.LogInfo($"[LLM TEXT] Response: {contentStr}");
                    LogLoop(profileName, "[ASSISTANT]\n" + TruncateForLoopLog(assistantMessage.Content?.ToString(), 1200));
                }

                // 3. Sprawdź warunek zakończenia: jeśli brak wywołań funkcji -> koniec.
                if (assistantMessage.ToolCalls == null || !assistantMessage.ToolCalls.Any())
                {
                    bool shouldForceContinue = ShouldForceContinueAfterLastTool(conversationHistory);

                    if (shouldForceContinue && iterations < maxIterations)
                    {
                        string forceHint = BuildForceContinueHint(conversationHistory);
                        conversationHistory.Add(new ChatMessage
                        {
                            Role = "user",
                            Content = forceHint
                        });
                        OnStatusUpdate?.Invoke($"[AgentControl] Wymuszam iteracje po {forceHint.Substring(0, Math.Min(60, forceHint.Length))}...");
                        BielikLogger.LogInfo($"[AGENT CONTROL] Forcing continue: {forceHint}");
                        LogLoop(profileName, "[SYSTEM -> ASSISTANT] Wymuszam kontynuacje po niepelnej akcji.\n" + TruncateForLoopLog(forceHint, 800));
                        continue;
                    }

                    sw.Stop();
                    OnStatusUpdate?.Invoke("Formułowanie ostatecznej odpowiedzi...");
                    TrimHistory(conversationHistory);

                    // Zgłoś statystyki (aproksymacja 4 znaki = 1 token)
                    RaiseStatsUpdate(new LLMStats
                    {
                        TotalTimeMs = sw.ElapsedMilliseconds,
                        PromptTokens = currentPromptTokens,
                        CompletionTokens = currentCompletionTokens
                    });

                    return AgentExecutionResult.Success(assistantMessage.Content?.ToString() ?? "(Model nie zwrócił tekstu)");
                }

                // 4. Mamy tool_calls! Realizujemy ich logikę na lokalnej maszynie C#
                OnStatusUpdate?.Invoke($"Odebrano zapytanie narzędziowe. Liczba operacji do wykonania: {assistantMessage.ToolCalls.Count}.");
                
                // Flaga wczesnego wyjścia - inicjalnie true (jeśli włączone), resetowana jeśli dowolne narzędzie nie wspiera lub zawiedzie.
                bool canEarlyExitThisTurn = earlyExitEnabled && assistantMessage.ToolCalls.Any();
                var earlyExitBlockers = new List<string>();
                if (!earlyExitEnabled)
                {
                    earlyExitBlockers.Add("Early Exit jest wylaczony w UI.");
                }

                foreach (var toolCall in assistantMessage.ToolCalls)
                {
                    var functionName = toolCall.Function.Name;
                    var argumentsString = toolCall.Function.Arguments;

                    // Logujemy surowy JSON wywołania do nowego interfejsu 'Logi Narzędzi'
                    OnToolCallLogged?.Invoke(JsonConvert.SerializeObject(toolCall, Newtonsoft.Json.Formatting.Indented));
                    LogLoop(profileName, $"[TOOL START] {functionName}\nARG: {TruncateForLoopLog(argumentsString, 800)}");
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
                            earlyExitBlockers.Add($"{functionName}: SupportsEarlyExit=false.");
                        }

                        // Przekazanie kontekstu do doca (tymczasowy most dla ToolOrchestrator, który wymaga Doc)
                        toolExecutionResult = _orchestrator.ExecuteTool(functionName, argumentsParsed, context, profileName);
                        if (LooksLikeToolFailure(toolExecutionResult))
                        {
                            canEarlyExitThisTurn = false;
                            earlyExitBlockers.Add($"{functionName}: wynik narzedzia wyglada na blad.");
                        }

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
                    LogLoop(profileName, $"[TOOL RESULT] {functionName}\n{TruncateForLoopLog(toolExecutionResult, 1200)}");
                    if (toolExecutionResult.StartsWith(MetricVisionRenderer.MetricVisionToken))
                    {
                        string metricJson = toolExecutionResult.Substring(MetricVisionRenderer.MetricVisionToken.Length);
                        try
                        {
                            JObject metricPayload = JObject.Parse(metricJson);
                            string imagePath = metricPayload["tile"]?["image_path"]?.ToString();
                            if (string.IsNullOrWhiteSpace(imagePath) || !System.IO.File.Exists(imagePath))
                            {
                                throw new System.IO.FileNotFoundException("Nie znaleziono pliku obrazu metrycznego.", imagePath);
                            }

                            byte[] imageBytes = System.IO.File.ReadAllBytes(imagePath);
                            string base64String = Convert.ToBase64String(imageBytes);
                            string imageDataUrl = $"data:image/png;base64,{base64String}";

                            if (ToolConfigManager.GetVisionOcrBinding()?.Enabled == true)
                            {
                                var ocr = await AnalyzeImageWithVisionOcrAsync(
                                    imageDataUrl,
                                    "Oto metryczny render CAD. Odczytaj tekst, symbole i relacje widoczne na obrazie.",
                                    metricPayload.ToString(Newtonsoft.Json.Formatting.Indented));

                                conversationHistory.Add(new ChatMessage
                                {
                                    Role = "tool",
                                    ToolCallId = toolCall.Id,
                                    Content = ocr.ok
                                        ? "Metryczny obraz CAD zostal przeanalizowany przez globalny Vision/OCR.\n\n[VISION/OCR]\n" + ocr.text
                                        : "Blad globalnego Vision/OCR dla metrycznego obrazu CAD: " + ocr.text
                                });

                                OnStatusUpdate?.Invoke(ocr.ok ? "Metryczny obraz CAD przeanalizowany przez Vision/OCR." : "Blad Vision/OCR dla metrycznego obrazu CAD.");
                                canEarlyExitThisTurn = false;
                                continue;
                            }

                            conversationHistory.Add(new ChatMessage
                            {
                                Role = "tool",
                                ToolCallId = toolCall.Id,
                                Content = "Metryczny obraz CAD zostal wyrenderowany. Dolaczam obraz i dane kalibracyjne pixel->CAD jako kolejna wiadomosc uzytkownika."
                            });

                            var visionContent = new List<VisionContentPart>
                            {
                                new VisionContentPart { Type = "text", Text = "Oto metryczny render CAD. Uzyj danych kalibracyjnych JSON do przeliczania pikseli na wspolrzedne CAD:\n" + metricPayload.ToString(Newtonsoft.Json.Formatting.Indented) },
                                new VisionContentPart
                                {
                                    Type = "image_url",
                                    ImageUrl = new VisionImageUrl { Url = imageDataUrl }
                                }
                            };

                            conversationHistory.Add(new ChatMessage { Role = "user", Content = visionContent });
                            OnStatusUpdate?.Invoke("Metryczny obraz Vision wstrzykniety do rozmowy z kalibracja pixel->CAD.");
                        }
                        catch (Exception ex)
                        {
                            conversationHistory.Add(new ChatMessage
                            {
                                Role = "tool",
                                ToolCallId = toolCall.Id,
                                Content = $"Blad przetwarzania metrycznego obrazu Vision: {ex.Message}. Surowy wynik narzedzia: {metricJson}"
                            });
                            OnStatusUpdate?.Invoke($"Blad przetwarzania Metric Vision: {ex.Message}");
                            canEarlyExitThisTurn = false;
                        }
                    }
                    else if (toolExecutionResult.StartsWith("[VISION_IMAGE_CAPTURED]|"))
                    {
                        string imagePath = toolExecutionResult.Split('|')[1];
                        if (string.IsNullOrWhiteSpace(imagePath) || !System.IO.File.Exists(imagePath))
                        {
                            conversationHistory.Add(new ChatMessage
                            {
                                Role = "tool",
                                ToolCallId = toolCall.Id,
                                Content = $"Blad przetwarzania obrazu Vision: nie znaleziono pliku '{imagePath}'."
                            });
                            OnStatusUpdate?.Invoke($"Blad przetwarzania obrazu Vision: nie znaleziono pliku '{imagePath}'.");
                            canEarlyExitThisTurn = false;
                            continue;
                        }
                        
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
                            string imageDataUrl = $"data:image/jpeg;base64,{base64String}";

                            if (ToolConfigManager.GetVisionOcrBinding()?.Enabled == true)
                            {
                                var ocr = await AnalyzeImageWithVisionOcrAsync(
                                    imageDataUrl,
                                    "Oto zrzut ekranu obszaru rysunku. Odczytaj widoczny tekst, symbole, obiekty CAD i relacje przestrzenne.");

                                conversationHistory.Add(new ChatMessage
                                {
                                    Role = "tool",
                                    ToolCallId = toolCall.Id,
                                    Content = ocr.ok
                                        ? "Obraz zostal przechwycony i przeanalizowany przez globalny Vision/OCR.\n\n[VISION/OCR]\n" + ocr.text
                                        : "Blad globalnego Vision/OCR dla przechwyconego obrazu: " + ocr.text
                                });

                                OnStatusUpdate?.Invoke(ocr.ok ? "Obraz przeanalizowany przez Vision/OCR." : "Blad Vision/OCR dla obrazu.");
                                canEarlyExitThisTurn = false;
                                continue;
                            }

                            var visionContent = new List<VisionContentPart>
                            {
                                new VisionContentPart { Type = "text", Text = "Oto zrzut ekranu obszaru rysunku, o który prosiłeś. Przeanalizuj go uważnie." },
                                new VisionContentPart
                                {
                                    Type = "image_url",
                                    ImageUrl = new VisionImageUrl { Url = imageDataUrl }
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
                if (ShouldForceContinueAfterLastTool(conversationHistory) && iterations < maxIterations)
                {
                    string forceHint = BuildForceContinueHint(conversationHistory);
                    conversationHistory.Add(new ChatMessage
                    {
                        Role = "user",
                        Content = forceHint
                    });
                    canEarlyExitThisTurn = false;
                    OnStatusUpdate?.Invoke($"[AgentControl] Wymuszam iteracje po {forceHint.Substring(0, Math.Min(60, forceHint.Length))}...");
                    BielikLogger.LogInfo($"[AGENT CONTROL] Forcing continue before early exit: {forceHint}");
                    string continueReason = earlyExitEnabled
                        ? "[SYSTEM -> ASSISTANT] Wymuszam kontynuacje przed Early Exit."
                        : "[SYSTEM -> ASSISTANT] Wymuszam kontynuacje po liscie layoutow.";
                    LogLoop(profileName, continueReason + "\n" + TruncateForLoopLog(forceHint, 800));
                    continue;
                }

                if (!canEarlyExitThisTurn && earlyExitBlockers.Count > 0)
                {
                    LogLoop(profileName, "[EARLY EXIT POMINIETY]\n" + string.Join("\n", earlyExitBlockers.Distinct()));
                }

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

                    string earlyExitMessage = ExtractLastToolResult(conversationHistory);
                    if (string.IsNullOrEmpty(earlyExitMessage))
                    {
                        earlyExitMessage = "Operacja wykonana pomyślnie (Tryb Szybki).";
                    }

                    // Fix v2.34.20: wycinaj Auto-Inject z preview (taki sam filtr jak w LogLoop).
                    earlyExitMessage = SanitizeForLoopLog(earlyExitMessage);
                    string earlyExitPreview = earlyExitMessage;
                    if (earlyExitPreview.Length > 600)
                    {
                        earlyExitPreview = earlyExitPreview.Substring(0, 600) + "...";
                    }

                    OnToolCallLogged?.Invoke(
                        "--- EARLY EXIT ---\n" +
                        JsonConvert.SerializeObject(new
                        {
                            Profile = profileName ?? "(default)",
                            Iteration = iterations,
                            Reason = "Wszystkie narzedzia w tej iteracji obsluguja SupportsEarlyExit i zwrocily wynik bez bledu.",
                            ReturnedMessage = earlyExitPreview
                        }, Newtonsoft.Json.Formatting.Indented));
                    LogLoop(profileName, "[EARLY EXIT] Zakonczono petle bez kolejnego kroku modelu.\n" + TruncateForLoopLog(earlyExitMessage, 1200));

                    return AgentExecutionResult.Success(earlyExitMessage);
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
        /// <summary>
        /// Wysyla minimalny cichy request bez narzedzi, aby lokalny serwer LLM mogl zbudowac prompt/KV cache.
        /// Nie modyfikuje historii rozmowy ani sesji.
        /// </summary>
        public async Task<bool> WarmupPromptAsync(List<ChatMessage> messages, string profileName = "SupervisorProfile", CancellationToken ct = default)
        {
            if (messages == null || messages.Count == 0) return false;

            var config = LLMConfigManager.ResolveProviderForProfile(profileName);
            if (config == null || string.IsNullOrWhiteSpace(config.EndpointUrl)) return false;
            if (IsCloudProvider(config))
            {
                BielikLogger.LogInfo($"[LLM WARMUP] Pomijam warmup dla providera chmurowego: {config.EndpointUrl}");
                return false;
            }

            var llmBinding = ToolConfigManager.GetAgentLlmBinding(profileName);
            if (config.AutoLoadModel && SupportsLocalModelManagement(config))
            {
                await TryLoadModelAsync(config, llmBinding?.ContextPolicy);
            }

            var warmupMessages = new List<ChatMessage>(messages);
            warmupMessages.Add(new ChatMessage
            {
                Role = "user",
                Content = "Przygotuj kontekst. Odpowiedz jednym tokenem."
            });

            var requestPayload = new Dictionary<string, object>
            {
                { "model", config.ModelName },
                { "messages", warmupMessages },
                { "temperature", 0.0 },
                { "max_tokens", 1 }
            };

            string jsonContent = JsonConvert.SerializeObject(requestPayload, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            using (var request = new HttpRequestMessage(HttpMethod.Post, config.EndpointUrl))
            {
                if (!string.IsNullOrEmpty(config.ApiKey) && config.ApiKey != "not-needed")
                {
                    request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                }

                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                try
                {
                    BielikLogger.LogInfo($"[LLM WARMUP] Model={config.ModelName}, Endpoint={config.EndpointUrl}, Messages={warmupMessages.Count}");
                    using (var response = await _httpClient.SendAsync(request, ct))
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        if (!response.IsSuccessStatusCode)
                        {
                            BielikLogger.LogWarn($"[LLM WARMUP] Nieudany status {(int)response.StatusCode}: {body}");
                            return false;
                        }

                        BielikLogger.LogInfo($"[LLM WARMUP] OK, BodyLength={body.Length}");
                        return true;
                    }
                }
                catch (OperationCanceledException)
                {
                    BielikLogger.LogInfo("[LLM WARMUP] Anulowano.");
                    return false;
                }
                catch (Exception ex)
                {
                    BielikLogger.LogWarn($"[LLM WARMUP] Blad: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Analizuje obraz przez globalny model Vision/OCR bez tool callingu i zwraca tekstowy opis/OCR.
        /// </summary>
        public async Task<(bool ok, string text)> AnalyzeImageWithVisionOcrAsync(string imageDataUrl, string userInstruction, string technicalContext = null, CancellationToken ct = default)
        {
            return await AnalyzeImagesWithVisionOcrAsync(new List<string> { imageDataUrl }, userInstruction, technicalContext, ct);
        }

        /// <summary>
        /// Analizuje jeden lub wiele obrazow przez globalny model Vision/OCR bez tool callingu.
        /// </summary>
        public async Task<(bool ok, string text)> AnalyzeImagesWithVisionOcrAsync(IList<string> imageDataUrls, string userInstruction, string technicalContext = null, CancellationToken ct = default)
        {
            var binding = ToolConfigManager.GetVisionOcrBinding();
            if (binding == null || !binding.Enabled)
            {
                return (false, "Globalny Vision/OCR jest wylaczony.");
            }

            var config = LLMConfigManager.ResolveVisionOcrProvider();
            if (config == null || string.IsNullOrWhiteSpace(config.EndpointUrl))
            {
                return (false, "Globalny Vision/OCR nie ma skonfigurowanego providera.");
            }

            var validImageUrls = imageDataUrls?
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .ToList() ?? new List<string>();

            if (validImageUrls.Count == 0)
            {
                return (false, "Brak danych obrazu dla Vision/OCR.");
            }

            LastVisionOcrDiagnostics = string.Empty;

            if (await EnsureVisionOcrModelFromProviderAsync(config, binding, ct))
            {
                OnStatusUpdate?.Invoke($"Vision/OCR uzyje modelu: {config.ModelName}");
            }

            if (string.IsNullOrWhiteSpace(config.ModelName) || IsDefaultModelPlaceholder(config.ModelName))
            {
                return (false, "Globalny Vision/OCR nie ma wybranego modelu. Otworz Ustawienia -> Vision/OCR i wybierz model albo uzyj przycisku Modele.");
            }

            bool shouldAutoLoad = binding.AutoLoadModel ?? config.AutoLoadModel;
            if (!binding.AutoLoadModel.HasValue && SupportsLocalModelManagement(config))
            {
                shouldAutoLoad = true;
            }

            if (shouldAutoLoad && SupportsLocalModelManagement(config))
            {
                OnStatusUpdate?.Invoke("Ladowanie modelu Vision/OCR...");
                await TryLoadModelAsync(config, binding.ContextPolicy);
            }

            string textPrompt = BuildVisionOcrUserPrompt(userInstruction, technicalContext);

            var visionParts = new List<VisionContentPart>
            {
                new VisionContentPart { Type = "text", Text = textPrompt }
            };
            foreach (string imageDataUrl in validImageUrls)
            {
                visionParts.Add(new VisionContentPart { Type = "image_url", ImageUrl = new VisionImageUrl { Url = imageDataUrl } });
            }

            var messages = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = "system",
                    Content = BuildVisionOcrSystemPrompt()
                },
                new ChatMessage
                {
                    Role = "user",
                    Content = visionParts
                }
            };

            var requestPayload = new Dictionary<string, object>
            {
                { "model", config.ModelName },
                { "messages", messages },
                { "temperature", config.Temperature },
                { "max_tokens", config.MaxTokens }
            };

            if (config.TopP > 0.0 && config.TopP != 1.0) requestPayload["top_p"] = config.TopP;
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

            for (int attempt = 0; attempt < 2; attempt++)
            {
                requestPayload["model"] = config.ModelName;
                string jsonContent = JsonConvert.SerializeObject(requestPayload, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

                using (var request = new HttpRequestMessage(HttpMethod.Post, config.EndpointUrl))
                {
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

                    try
                    {
                        BielikLogger.LogInfo($"[VISION OCR REQ] Model={config.ModelName}, Endpoint={config.EndpointUrl}, Attempt={attempt + 1}");
                        using (var response = await _httpClient.SendAsync(request, ct))
                        {
                            string responseBody = await response.Content.ReadAsStringAsync();
                            if (!response.IsSuccessStatusCode)
                            {
                                string msg = $"Vision/OCR HTTP {(int)response.StatusCode}: {responseBody}";
                                LastVisionOcrDiagnostics = $"http_status={(int)response.StatusCode}, model={config.ModelName}, endpoint={config.EndpointUrl}, raw_chars={(responseBody ?? string.Empty).Length}";
                                BielikLogger.LogWarn("[VISION OCR ERR] " + msg);
                                if (attempt == 0 && SupportsLocalModelManagement(config))
                                {
                                    await PrepareVisionOcrRetryAsync(config, binding, ct);
                                    continue;
                                }
                                return (false, msg);
                            }

                            var jsonResponse = JObject.Parse(responseBody);
                            string content = ExtractVisionOcrContent(jsonResponse);
                            LastVisionOcrDiagnostics = BuildVisionOcrDiagnostics(jsonResponse, responseBody, content);
                            if (string.IsNullOrWhiteSpace(content))
                            {
                                string finishReason = jsonResponse["choices"]?[0]?["finish_reason"]?.ToString() ?? "<brak>";
                                BielikLogger.LogWarn($"[VISION OCR WARN] Empty response content. finish_reason={finishReason}, {LastVisionOcrDiagnostics}, raw={TruncateForLog(responseBody, 2000)}");
                                return (false, "Vision/OCR zwrocil pusta odpowiedz. Szczegoly zapisano w logu Engine.");
                            }

                            BielikLogger.LogInfo($"[VISION OCR RESP] OK, Length={content.Length}, {LastVisionOcrDiagnostics}");
                            return (true, content.Trim());
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        BielikLogger.LogInfo("[VISION OCR] Anulowano.");
                        LastVisionOcrDiagnostics = $"cancelled=true, model={config.ModelName}, endpoint={config.EndpointUrl}";
                        return (false, "Vision/OCR anulowany.");
                    }
                    catch (Exception ex)
                    {
                        BielikLogger.LogError($"[VISION OCR ERR] {ex.Message}", ex);
                        LastVisionOcrDiagnostics = $"exception={ex.GetType().Name}, message={ex.Message}, model={config.ModelName}, endpoint={config.EndpointUrl}";
                        if (attempt == 0 && SupportsLocalModelManagement(config))
                        {
                            await PrepareVisionOcrRetryAsync(config, binding, ct);
                            continue;
                        }
                        return (false, ex.Message);
                    }
                }
            }

            return (false, "Vision/OCR nie powiodl sie po ponowieniu requestu.");
        }

        public static string BuildVisionOcrSystemPrompt()
        {
            return "Jestes wyspecjalizowanym modelem Vision/OCR dla systemu CAD. Twoim zadaniem jest zamiana obrazu lub zestawu kafelkow jednego obrazu na uzyteczny opis tekstowy dla glownego agenta. Nie planujesz narzedzi i nie wykonujesz operacji CAD.";
        }

        public static string BuildVisionOcrUserPrompt(string userInstruction, string technicalContext = null)
        {
            string textPrompt = "Przeanalizuj obraz albo zestaw kafelkow jednego obrazu pod katem OCR i kontekstu CAD. " +
                "Odczytaj widoczny tekst, symbole, etykiety, wymiary, relacje przestrzenne i istotne problemy. " +
                "Nie ograniczaj sie do srodka rysunku: sprawdz krawedzie arkusza, tabliczke rysunkowa, legendy, opisy detali i male napisy. " +
                "Jesli widzisz tabliczke rysunkowa, odczytaj inwestora, projekt, adres, stadium, branze, tytul rysunku, numer, date, skale i autorow. " +
                "Jesli widzisz elementy typu rząpia/rzap, legenda, wodomierz, zawory, rury lub materialy, wypisz je doslownie. " +
                "Jesli polecenie uzytkownika pyta o konkretny element, odpowiedz tylko na ten element i podaj krotkie NIEPEWNE_DO_SPRAWDZENIA. " +
                "Pelny raport w sekcjach OPIS_OGOLNY, TEKSTY_OCR, TABLICZKA_RYSUNKOWA, LEGENDA, WYMIARY_DETALE, NIEPEWNE_DO_SPRAWDZENIA tworz tylko przy ogolnej analizie obrazu. " +
                "Jesli czegos nie ma albo jest nieczytelne, napisz to jako niepewne zamiast zgadywac. Nie wywoluj narzedzi.";

            if (!string.IsNullOrWhiteSpace(userInstruction))
            {
                textPrompt += "\n\nPolecenie uzytkownika:\n" + userInstruction;
            }

            if (!string.IsNullOrWhiteSpace(technicalContext))
            {
                textPrompt += "\n\nKontekst techniczny / kalibracja:\n" + technicalContext;
            }

            return textPrompt;
        }

        private static string ExtractVisionOcrContent(JObject jsonResponse)
        {
            var choice = jsonResponse["choices"]?[0];
            var message = choice?["message"];

            string content = ExtractTextFromContentToken(message?["content"]);
            if (!string.IsNullOrWhiteSpace(content)) return content.Trim();

            content = ExtractTextFromContentToken(message?["reasoning_content"]);
            if (!string.IsNullOrWhiteSpace(content)) return content.Trim();

            content = ExtractTextFromContentToken(message?["reasoning"]);
            if (!string.IsNullOrWhiteSpace(content)) return content.Trim();

            content = ExtractTextFromContentToken(choice?["text"]);
            if (!string.IsNullOrWhiteSpace(content)) return content.Trim();

            content = ExtractTextFromContentToken(jsonResponse["content"]);
            if (!string.IsNullOrWhiteSpace(content)) return content.Trim();

            content = ExtractTextFromContentToken(jsonResponse["response"]);
            if (!string.IsNullOrWhiteSpace(content)) return content.Trim();

            content = ExtractTextFromContentToken(jsonResponse["generated_text"]);
            return string.IsNullOrWhiteSpace(content) ? string.Empty : content.Trim();
        }

        private static string BuildVisionOcrDiagnostics(JObject jsonResponse, string responseBody, string extractedContent)
        {
            var choice = jsonResponse["choices"]?[0];
            var message = choice?["message"];
            string content = ExtractTextFromContentToken(message?["content"]);
            string reasoningContent = ExtractTextFromContentToken(message?["reasoning_content"]);
            string reasoning = ExtractTextFromContentToken(message?["reasoning"]);
            string choiceText = ExtractTextFromContentToken(choice?["text"]);
            string rootResponse = ExtractTextFromContentToken(jsonResponse["response"]);
            string rootGenerated = ExtractTextFromContentToken(jsonResponse["generated_text"]);
            string finishReason = choice?["finish_reason"]?.ToString() ?? "<brak>";
            string model = jsonResponse["model"]?.ToString() ?? "<brak>";

            var usage = jsonResponse["usage"];
            string promptTokens = usage?["prompt_tokens"]?.ToString() ?? "<brak>";
            string completionTokens = usage?["completion_tokens"]?.ToString() ?? "<brak>";
            string totalTokens = usage?["total_tokens"]?.ToString() ?? "<brak>";
            string reasoningTokens = usage?["completion_tokens_details"]?["reasoning_tokens"]?.ToString() ?? "<brak>";

            return $"model={model}, finish_reason={finishReason}, prompt_tokens={promptTokens}, completion_tokens={completionTokens}, total_tokens={totalTokens}, reasoning_tokens={reasoningTokens}, extracted_chars={(extractedContent ?? string.Empty).Length}, content_chars={content.Length}, reasoning_content_chars={reasoningContent.Length}, reasoning_chars={reasoning.Length}, choice_text_chars={choiceText.Length}, response_chars={rootResponse.Length}, generated_text_chars={rootGenerated.Length}, raw_chars={(responseBody ?? string.Empty).Length}";
        }

        private static string ExtractTextFromContentToken(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return string.Empty;

            if (token.Type == JTokenType.String)
            {
                return token.ToString();
            }

            if (token.Type == JTokenType.Array)
            {
                var parts = new List<string>();
                foreach (var part in token.Children())
                {
                    string text = ExtractTextFromContentToken(part);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        parts.Add(text.Trim());
                    }
                }
                return string.Join("\n", parts);
            }

            if (token.Type == JTokenType.Object)
            {
                var obj = (JObject)token;
                foreach (string key in new[] { "text", "content", "value", "reasoning_content", "reasoning" })
                {
                    string text = ExtractTextFromContentToken(obj[key]);
                    if (!string.IsNullOrWhiteSpace(text)) return text;
                }
            }

            return string.Empty;
        }

        private static string TruncateForLog(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength) return value ?? string.Empty;
            return value.Substring(0, maxLength) + "...";
        }

        /// <summary>
        /// Skanuje historie w poszukiwaniu triggerow recept ($trigger) i wstrzykuje
        /// przyklady Few-Shot bezposrednio po system prompcie.
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
        /// Skanuje historię w poszukiwaniu znaczników LISP (%lisp_id lub %lisp_id%) i instruuje agenta do ich uruchomienia.
        /// </summary>
        private void PreProcessLisps(List<ChatMessage> history)
        {
            var userMsgs = history.Where(m => m.Role == "user" && m.Content != null).ToList();
            if (!userMsgs.Any()) return;

            var discoveredLisps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var msg in userMsgs)
            {
                if (msg.Content is string textContent)
                {
                    var matches = Regex.Matches(textContent, @"%([a-zA-Z0-9_:]+)%?");
                    foreach (Match m in matches) discoveredLisps.Add(m.Groups[1].Value);
                }
            }

            if (!discoveredLisps.Any()) return;

            int injectionIdx = history.FindIndex(m => m.Role == "system") + 1;
            if (injectionIdx <= 0) injectionIdx = 0;

            foreach (var lispId in discoveredLisps)
            {
                history.Insert(injectionIdx++, new ChatMessage 
                { 
                    Role = "system", 
                    Content = $"UŻYTKOWNIK UŻYŁ ZNACZNIKA %{lispId}. Oznacza to jawne żądanie uruchomienia tego skryptu LISP. Natychmiast użyj narzędzia manage_lisps z Action='execute_lisp' i LispId='{lispId}'." 
                });
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
                    // Odczyty narzędziowe (np. atrybuty bloków, właściwości) muszą pozostać wierne,
                    // bo kolejne kroki agenta mogą polegać na pełnej liście tagów i wartości.
                    if (contentStr.StartsWith("WYNIK (Read):", StringComparison.OrdinalIgnoreCase) ||
                        contentStr.StartsWith("WYNIK ODCZYTU", StringComparison.OrdinalIgnoreCase) ||
                        contentStr.StartsWith("Wartość dla klucza", StringComparison.OrdinalIgnoreCase) ||
                        contentStr.StartsWith("Zawartość Blackboard", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

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
            var initialConfig = LLMConfigManager.ResolveProviderForProfile(profileName);
            var llmBinding = ToolConfigManager.GetAgentLlmBinding(profileName);
            if (initialConfig != null && initialConfig.AutoLoadModel && SupportsLocalModelManagement(initialConfig))
            {
                OnStatusUpdate?.Invoke("[Benchmark] Inicjalizacja automatycznego ladowania modelu...");
                await TryLoadModelAsync(initialConfig, llmBinding?.ContextPolicy);
            }

            while (iterations < maxIterations)
            {
                if (ct.IsCancellationRequested) break;
                iterations++;

                var config = initialConfig;
                
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
                    if (!response.IsSuccessStatusCode)
                    {
                        // v2.28.85: Lepsza diagnostyka bledow HTTP - czytamy body i logujemy pelna odpowiedz.
                        // Wczesniej: cichy return - RecordedToolCalls zostawaly puste, benchmark dawal 0% bez wyjasnienia.
                        // Teraz: zarejestruj specjalny tool call z kodem bledu i body,
                        //       nastepnie dodaj asystencka wiadomosc z opisem bledu i return.
                        string errBody = await response.Content.ReadAsStringAsync();
                        int status = (int)response.StatusCode;
                        string errMsg = $"[Benchmark] Błąd HTTP {status} z LLM ({response.StatusCode}): {errBody}";
                        OnStatusUpdate?.Invoke(errMsg);

                        // Rejestrujemy "blad" jako specjalny tool call - widoczne w raporcie i materiale dowodowym
                        recordedCalls.Add(new RecordedToolCall
                        {
                            ToolName = $"_LLM_ERROR_{status}",
                            Arguments = JObject.Parse($"{{\"HttpStatus\":{status},\"ErrorBody\":\"{System.Text.RegularExpressions.Regex.Replace(errBody, @"[\\\""]", " ").Substring(0, Math.Min(500, errBody.Length))}\"}}")
                        });

                        // Dodaj asystencka wiadomosc - walidator widzi ze LLM nie zwrocil poprawnej odpowiedzi
                        history.Add(new ChatMessage
                        {
                            Role = "assistant",
                            Content = $"[LLM ERROR {status}] Nie udalo sie uzyskac odpowiedzi z LLM. Body: {errBody.Substring(0, Math.Min(500, errBody.Length))}"
                        });
                        return;
                    }
                }
                catch (Exception ex)
                {
                    OnStatusUpdate?.Invoke($"[Benchmark] Błąd połączenia z LLM: {ex.Message}");
                    recordedCalls.Add(new RecordedToolCall
                    {
                        ToolName = "_LLM_ERROR_CONNECTION",
                        Arguments = JObject.Parse($"{{\"ErrorMessage\":\"{System.Text.RegularExpressions.Regex.Replace(ex.Message, @"[\\\""]", " ")}\"}}")
                    });
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
        /// Wysyła żądanie ładowania modelu do LM Studio REST API (wywoływane automatycznie przed czatem, gdy AutoLoadModel=true).
        /// </summary>
        private async Task TryLoadModelAsync(LLMProviderConfig config, string contextPolicy = null)
        {
            if (config != null && SupportsLocalModelManagement(config))
            {
                try
                {
                    var loaded = await GetLoadedModelInfoAsync(config);
                    if (loaded != null && IsSameLoadedModel(loaded, config.ModelName))
                    {
                        if (config.LoadContextLength <= 0 || loaded.LoadedContextLength <= 0 || loaded.LoadedContextLength >= config.LoadContextLength)
                        {
                            OnStatusUpdate?.Invoke($"[Auto-Load] Model {config.ModelName} juz zaladowany; pomijam ponowne ladowanie.");
                            return;
                        }

                        if (string.Equals(contextPolicy, "NeverReloadAutomatically", StringComparison.OrdinalIgnoreCase))
                        {
                            OnStatusUpdate?.Invoke($"[Auto-Load] Model {config.ModelName} ma ctx={loaded.LoadedContextLength}, wymagane ctx={config.LoadContextLength}; polityka profilu blokuje przeladowanie.");
                            return;
                        }

                        OnStatusUpdate?.Invoke($"[Auto-Load] Zaladowany kontekst ({loaded.LoadedContextLength}) jest mniejszy niz wymagany ({config.LoadContextLength}). Przeladowuje model...");
                        var (unloadOk, unloadMessage) = await UnloadModelAsync(config);
                        if (!unloadOk)
                        {
                            OnStatusUpdate?.Invoke($"[Auto-Load] Nie udalo sie rozladowac modelu przed zmiana kontekstu: {unloadMessage}");
                        }
                    }
                    else if (loaded != null && config.LoadContextLength > 0 && loaded.LoadedContextLength > config.LoadContextLength)
                    {
                        OnStatusUpdate?.Invoke($"[Auto-Load] Aktualnie zaladowany model ma wiekszy kontekst ({loaded.LoadedContextLength}) niz wymagany ({config.LoadContextLength}); nie zmniejszam kontekstu.");
                    }
                }
                catch (Exception ex)
                {
                    OnStatusUpdate?.Invoke($"[Auto-Load] Nie udalo sie sprawdzic aktualnego kontekstu: {ex.Message}");
                }
            }

            var (ok, message) = await LoadModelAsync(config);
            if (ok)
                OnStatusUpdate?.Invoke($"[Auto-Load] Model {config.ModelName} załadowany pomyślnie. {message}");
            else if (!string.IsNullOrEmpty(message))
                OnStatusUpdate?.Invoke($"[Auto-Load] {message}");
        }

        private async Task<bool> EnsureVisionOcrModelFromProviderAsync(LLMProviderConfig config, VisionOcrBinding binding, CancellationToken ct)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.EndpointUrl)) return false;
            if (!SupportsLocalModelManagement(config) && !IsDefaultModelPlaceholder(config.ModelName)) return false;

            try
            {
                var models = await GetAvailableModelsAsync(config, ct);
                var modelList = models?
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Select(m => m.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();

                if (modelList.Count == 0)
                {
                    BielikLogger.LogWarn("[VISION OCR MODEL] /v1/models zwrocil pusta liste modeli.");
                    return false;
                }

                string current = config.ModelName?.Trim();
                string selected = null;
                bool hasExplicitModel = !string.IsNullOrWhiteSpace(current) && !IsDefaultModelPlaceholder(current);
                if (hasExplicitModel &&
                    modelList.Any(m => string.Equals(m, current, StringComparison.OrdinalIgnoreCase)))
                {
                    selected = modelList.First(m => string.Equals(m, current, StringComparison.OrdinalIgnoreCase));
                }

                if (hasExplicitModel && string.IsNullOrWhiteSpace(selected))
                {
                    BielikLogger.LogInfo($"[VISION OCR MODEL] Zachowuje jawnie wybrany model spoza /v1/models: {current}");
                    return false;
                }

                if (string.IsNullOrWhiteSpace(selected))
                {
                    selected = SelectPreferredVisionOcrModel(modelList);
                }

                if (string.IsNullOrWhiteSpace(selected)) return false;
                bool changed = !string.Equals(config.ModelName, selected, StringComparison.OrdinalIgnoreCase);

                config.ModelName = selected;
                if (changed && binding != null)
                {
                    binding.ModelName = selected;
                    ToolConfigManager.UpdateVisionOcrBinding(binding);
                }

                BielikLogger.LogInfo($"[VISION OCR MODEL] Ensured model from /v1/models: {selected}");
                return changed;
            }
            catch (Exception ex)
            {
                BielikLogger.LogWarn("[VISION OCR MODEL] Nie udalo sie pobrac listy modeli: " + ex.Message);
                return false;
            }
        }

        private async Task PrepareVisionOcrRetryAsync(LLMProviderConfig config, VisionOcrBinding binding, CancellationToken ct)
        {
            BielikLogger.LogInfo("[VISION OCR RETRY] Przygotowuje ponowienie po pierwszym bledzie.");
            OnStatusUpdate?.Invoke("Vision/OCR ponawia po przygotowaniu modelu...");
            await Task.Delay(1000, ct);
            await EnsureVisionOcrModelFromProviderAsync(config, binding, ct);
            if (SupportsLocalModelManagement(config))
            {
                await TryLoadModelAsync(config, binding?.ContextPolicy);
            }
        }

        private static string SelectPreferredVisionOcrModel(IEnumerable<string> models)
        {
            var list = models?
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (list.Count == 0) return null;
            if (list.Count == 1) return list[0];

            string[] preferredMarkers = { "vision", "vl", "vlm", "mm", "ocr", "gemma", "qwen2-vl", "qwen2.5-vl", "llava", "pixtral", "moondream", "minicpm" };
            foreach (string marker in preferredMarkers)
            {
                var match = list.FirstOrDefault(m => m.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrWhiteSpace(match)) return match;
            }

            return list[0];
        }

        private static bool IsDefaultModelPlaceholder(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName)) return true;
            string m = modelName.Trim();
            return string.Equals(m, "local-model", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(m, "model", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(m, "llama3", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSameLoadedModel(LlmModelDescriptor loaded, string requestedModel)
        {
            if (loaded == null || string.IsNullOrWhiteSpace(requestedModel)) return false;
            return string.Equals(loaded.ModelKey, requestedModel, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(loaded.DisplayName, requestedModel, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(loaded.Id, requestedModel, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Zwraca true, jeśli dany provider obsługuje natywne LM Studio load/unload API
        /// (czyli jest lokalnym serwerem, nie OpenAI/OpenRouter/Azure).
        /// </summary>
        public static bool SupportsLocalModelManagement(LLMProviderConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.EndpointUrl)) return false;
            string u = config.EndpointUrl.ToLowerInvariant();
            if (IsCloudProvider(config))
                return false;
            return true;
        }

        private static bool IsCloudProvider(LLMProviderConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.EndpointUrl)) return false;
            string u = config.EndpointUrl.ToLowerInvariant();
            return u.Contains("openrouter.ai") || u.Contains("api.openai.com") || u.Contains("openai.azure.com");
        }

        /// <summary>
        /// Zwraca bazowy URL providera (bez /v1/chat/completions itp.).
        /// </summary>
        public static string GetBaseUrl(LLMProviderConfig config)
        {
            string url = config.EndpointUrl ?? string.Empty;
            if (url.Contains("/v1/chat/completions"))
                url = url.Replace("/v1/chat/completions", "");
            else if (url.Contains("/chat/completions"))
                url = url.Replace("/chat/completions", "");
            return url.TrimEnd('/');
        }

        /// <summary>
        /// Buduje słownik payload dla POST /api/v1/models/load (LM Studio) na podstawie config providera.
        /// </summary>
        public static Dictionary<string, object> BuildLoadPayload(LLMProviderConfig config)
        {
            var loadPayload = new Dictionary<string, object>
            {
                { "model", config.ModelName }
            };
            if (config.LoadContextLength > 0)
                loadPayload["context_length"] = config.LoadContextLength;
            if (config.TtlSeconds > 0)
                loadPayload["ttl"] = config.TtlSeconds;
            if (config.FlashAttention)
                loadPayload["flash_attention"] = true;
            if (config.OffloadKvCache)
                loadPayload["offload_kv_cache_to_gpu"] = true;
            return loadPayload;
        }

        /// <summary>
        /// Ładuje model do VRAM w LM Studio (POST {baseUrl}/api/v1/models/load).
        /// Zwraca (sukces, komunikat). Odmawia dla providerów chmurowych.
        /// </summary>
        public async Task<(bool ok, string message)> LoadModelAsync(LLMProviderConfig config, CancellationToken ct = default)
        {
            if (config == null) return (false, "Brak konfiguracji providera.");
            if (!SupportsLocalModelManagement(config))
                return (false, "Dostawcy chmurowi (OpenRouter, OpenAI, Azure) nie obsługują dynamicznego ładowania modeli przez API.");

            try
            {
                string loadUrl = GetBaseUrl(config) + "/api/v1/models/load";
                string jsonContent = JsonConvert.SerializeObject(BuildLoadPayload(config));

                using (var request = new HttpRequestMessage(HttpMethod.Post, loadUrl))
                {
                    if (!string.IsNullOrEmpty(config.ApiKey) && config.ApiKey != "not-needed")
                        request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                    request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(request, ct);
                    string body = await response.Content.ReadAsStringAsync();
                    if (response.IsSuccessStatusCode)
                    {
                        string extra = "";
                        try
                        {
                            var jo = JObject.Parse(body);
                            var loadTime = jo["load_time_seconds"]?.Value<double?>();
                            if (loadTime.HasValue) extra = $" (czas: {loadTime.Value:F1}s)";
                        }
                        catch { }
                        return (true, $"Model '{config.ModelName}' załadowany{extra}.");
                    }
                    return (false, $"Błąd serwera ({(int)response.StatusCode}): {body}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Wyjątek komunikacji: {ex.Message}");
            }
        }

        /// <summary>
        /// Rozładowuje model z VRAM w LM Studio (POST {baseUrl}/api/v1/models/unload).
        /// Najpierw pobiera aktualny stan (/api/v1/models), wyciąga `instance_id` z `loaded_instances`
        /// (dla modelu pasującego do config.ModelName, a jeśli brak — dla pierwszego załadowanego)
        /// i wysyła unload z `instance_id`. LM Studio wymaga pola `instance_id`, nie `model`.
        /// Jeśli żaden model nie jest załadowany, zwraca sukces (nic do zrobienia).
        /// Odmawia dla providerów chmurowych.
        /// </summary>
        public async Task<(bool ok, string message)> UnloadModelAsync(LLMProviderConfig config, CancellationToken ct = default)
        {
            if (config == null) return (false, "Brak konfiguracji providera.");
            if (!SupportsLocalModelManagement(config))
                return (false, "Dostawcy chmurowi nie obsługują unload.");

            try
            {
                // 1) Pobierz aktualny stan - potrzebujemy instance_id
                var loaded = await GetLoadedModelInfoAsync(config, ct);
                if (loaded == null || string.IsNullOrEmpty(loaded.Id))
                {
                    return (true, "Brak załadowanego modelu w LM Studio (nic do zwolnienia).");
                }

                // 2) Wyślij unload z instance_id
                string unloadUrl = GetBaseUrl(config) + "/api/v1/models/unload";
                var payload = new Dictionary<string, object> { { "instance_id", loaded.Id } };
                string jsonContent = JsonConvert.SerializeObject(payload);

                using (var request = new HttpRequestMessage(HttpMethod.Post, unloadUrl))
                {
                    if (!string.IsNullOrEmpty(config.ApiKey) && config.ApiKey != "not-needed")
                        request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");
                    request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    var response = await _httpClient.SendAsync(request, ct);
                    if (response.IsSuccessStatusCode)
                        return (true, $"Model '{loaded.DisplayName ?? loaded.Id}' rozładowany.");
                    string body = await response.Content.ReadAsStringAsync();
                    return (false, $"Błąd serwera ({(int)response.StatusCode}): {body}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Wyjątek komunikacji: {ex.Message}");
            }
        }

        /// <summary>
        /// Pobiera listę modeli dostępnych u providera (OpenAI-compat GET /v1/models).
        /// Dla zdalnych providerów zwraca pustą listę.
        /// </summary>
        public async Task<List<string>> GetAvailableModelsAsync(LLMProviderConfig config, CancellationToken ct = default)
        {
            var result = new List<string>();
            if (config == null || string.IsNullOrEmpty(config.EndpointUrl)) return result;

            try
            {
                string url = config.EndpointUrl.Replace("/chat/completions", "/models");
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(10);
                    if (!string.IsNullOrEmpty(config.ApiKey) && config.ApiKey != "not-needed")
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

                    var response = await client.GetAsync(url, ct);
                    response.EnsureSuccessStatusCode();
                    var body = await response.Content.ReadAsStringAsync();
                    var json = JObject.Parse(body);
                    var modelsArray = json["data"] as JArray;
                    if (modelsArray != null)
                    {
                        foreach (var m in modelsArray)
                        {
                            var id = m["id"]?.ToString();
                            if (!string.IsNullOrEmpty(id)) result.Add(id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnStatusUpdate?.Invoke($"[ListModels] Błąd pobierania listy modeli: {ex.Message}");
            }
            return result;
        }

        /// <summary>
        /// Pobiera informacje o aktualnie załadowanym modelu z LM Studio (GET {baseUrl}/api/v1/models).
        /// Szuka modelu, którego pole 'loaded_instances' jest niepuste i pasuje do config.ModelName.
        /// Zwraca null, jeśli żaden model nie jest załadowany lub provider nie jest lokalny.
        /// </summary>
        public async Task<LlmModelDescriptor> GetLoadedModelInfoAsync(LLMProviderConfig config, CancellationToken ct = default)
        {
            if (config == null) return null;
            if (!SupportsLocalModelManagement(config)) return null;

            try
            {
                string baseUrl = GetBaseUrl(config);
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    if (!string.IsNullOrEmpty(config.ApiKey) && config.ApiKey != "not-needed")
                        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");

                    // Najpierw probuj natywne LM Studio /api/v1/models, bo tylko tam
                    // widac loaded_instances, instance_id i rzeczywisty context_length.
                    // Fallback do OpenAI-compat /v1/models zostaje dla llama.cpp/vLLM.
                    string openAIPath = "/v1/models";
                    string lmStudioPath = "/api/v1/models";
                    JArray modelsArray = null;
                    string responseBody = null;

                    // Proba 1: LM Studio /api/v1/models
                    try
                    {
                        var lmResp = await client.GetAsync(baseUrl + lmStudioPath, ct);
                        if (lmResp.IsSuccessStatusCode)
                        {
                            responseBody = await lmResp.Content.ReadAsStringAsync();
                            var lmJson = JObject.Parse(responseBody);
                            modelsArray = lmJson["models"] as JArray;
                        }
                    }
                    catch { /* fallback */ }

                    // Proba 2: OpenAI-compat (llama.cpp)
                    if (modelsArray == null)
                    {
                        try
                        {
                            var openAIResp = await client.GetAsync(baseUrl + openAIPath, ct);
                            if (openAIResp.IsSuccessStatusCode)
                            {
                                responseBody = await openAIResp.Content.ReadAsStringAsync();
                                var openAIJson = JObject.Parse(responseBody);
                                // llama.cpp: {"object":"list","data":[{"id":"...gguf","object":"model",...}]}
                                modelsArray = openAIJson["data"] as JArray;
                            }
                        }
                        catch { /* fallback */ }
                    }

                    if (modelsArray == null) return null;

                    // Sprawdzamy czy to OpenAI-compat (llama.cpp) - pole "data" zamiast "models"
                    bool isOpenAICompat = modelsArray.Count > 0 && modelsArray[0]["object"]?.ToString() == "model";

                    if (isOpenAICompat)
                    {
                        // llama.cpp/vLLM: data[].id jest modelem, nie ma loaded_instances
                        // Nie wiadomo ktory jest "aktywnie ladowany" (wszystkie sa dostepne)
                        // Proba: dopasuj do config.ModelName, jesli brak - pierwszy
                        LlmModelDescriptor firstId = null;
                        LlmModelDescriptor matchId = null;
                        foreach (var m in modelsArray)
                        {
                            string id = m["id"]?.ToString();
                            if (string.IsNullOrEmpty(id)) continue;
                            var desc = new LlmModelDescriptor
                            {
                                Id = id,
                                ModelKey = id,
                                DisplayName = id,
                                Quantization = null,
                                ParamsString = null,
                                SizeBytes = 0,
                                IsLoaded = true,
                                Architecture = null,
                                Publisher = m["owned_by"]?.ToString()
                            };
                            if (firstId == null) firstId = desc;
                            if (!string.IsNullOrEmpty(config.ModelName) &&
                                string.Equals(id, config.ModelName, StringComparison.OrdinalIgnoreCase))
                            {
                                matchId = desc;
                                break;
                            }
                        }
                        return matchId ?? firstId;
                    }


                    LlmModelDescriptor firstLoaded = null;
                    LlmModelDescriptor match = null;
                    foreach (var m in modelsArray)
                    {
                        if (m["type"]?.ToString() != "llm") continue;
                        var loadedArr = m["loaded_instances"] as JArray;
                        bool isLoaded = loadedArr != null && loadedArr.Count > 0;
                        if (!isLoaded) continue;

                        // instance_id (z loaded_instances[0].id) jest wymagane do unload.
                        // Jeśli brak, fallback do key/id modelu.
                        string instanceId = loadedArr[0]?["id"]?.ToString();
                        string modelKey = m["key"]?.ToString() ?? m["id"]?.ToString();

                        var desc = new LlmModelDescriptor
                        {
                            Id = !string.IsNullOrEmpty(instanceId) ? instanceId : modelKey,
                            ModelKey = modelKey,
                            DisplayName = m["display_name"]?.ToString(),
                            Quantization = m["quantization"]?["name"]?.ToString(),
                            ParamsString = m["params_string"]?.ToString(),
                            SizeBytes = m["size_bytes"]?.Value<long>() ?? 0,
                            IsLoaded = true,
                            Architecture = m["architecture"]?.ToString(),
                            Publisher = m["publisher"]?.ToString()
                        };
                        if (loadedArr.Count > 0)
                        {
                            desc.LoadedContextLength = loadedArr[0]["config"]?["context_length"]?.Value<int>() ?? 0;
                        }

                        if (firstLoaded == null) firstLoaded = desc;
                        if (IsSameLoadedModel(desc, config.ModelName))
                        {
                            match = desc;
                            break;
                        }
                    }
                    return match ?? firstLoaded;
                }
            }
            catch (Exception ex)
            {
                OnStatusUpdate?.Invoke($"[GetLoaded] Błąd: {ex.Message}");
                return null;
            }
        }

        private void RaiseStatsUpdate(LLMStats stats)
        {
            LastStats = stats;
            OnStatsUpdate?.Invoke(stats);
        }

        private void LogLoop(string profileName, string message)
        {
            string profile = string.IsNullOrWhiteSpace(profileName) ? "(default)" : profileName;
            // Fix v2.34.20: wycinaj bloki [AUTO-INJECTED PROPERTIES ...] z logow Loop.
            // Auto-Inject jest wstrzykiwany do kontekstu modelu (result), ale propagowany
            // przez pipeline do [EARLY EXIT], [CadProfile -> Supervisor] itp.
            // Powoduje ogromny rozrost logow (4 kopie tego samego ~250-znakowego bloku).
            string sanitized = SanitizeForLoopLog(message);
            OnLoopLogged?.Invoke($"[{profile}] {sanitized}");
        }

        private static string SanitizeForLoopLog(string message)
        {
            if (string.IsNullOrEmpty(message)) return message;
            const string startMarker = "[AUTO-INJECTED PROPERTIES";
            int startIdx = message.IndexOf(startMarker, StringComparison.Ordinal);
            if (startIdx < 0) return message;
            // Znajdz koniec bloku - "\n\n" po linii "(...)" lub koniec tekstu
            int endIdx = message.IndexOf("\n\n", startIdx, StringComparison.Ordinal);
            if (endIdx < 0) endIdx = message.Length;
            else endIdx += 2; // zachowaj \n\n separator
            return message.Substring(0, startIdx) + message.Substring(endIdx);
        }

        private static string TruncateForLoopLog(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "\n...[przycieto]";
        }

        private static string ExtractLastToolResult(List<ChatMessage> history)
        {
            if (history == null || history.Count == 0) return null;

            var results = new List<string>();
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var msg = history[i];
                if (msg != null && string.Equals(msg.Role, "tool", StringComparison.OrdinalIgnoreCase))
                {
                    if (msg.Content is string s && !string.IsNullOrEmpty(s))
                    {
                        results.Add(s);
                    }
                    continue;
                }

                if (results.Count > 0)
                {
                    results.Reverse();
                    return string.Join("\n", results);
                }
            }
            if (results.Count > 0)
            {
                results.Reverse();
                return string.Join("\n", results);
            }
            return null;
        }

        private static bool LooksLikeToolFailure(string toolExecutionResult)
        {
            if (string.IsNullOrWhiteSpace(toolExecutionResult)) return false;

            string lower = toolExecutionResult.ToLowerInvariant();
            if (lower.StartsWith("blad", StringComparison.Ordinal) ||
                lower.StartsWith("błąd", StringComparison.Ordinal) ||
                lower.StartsWith("error", StringComparison.Ordinal))
            {
                return true;
            }

            return lower.Contains(" błąd ") ||
                   lower.Contains(" blad ") ||
                   lower.Contains("błędy:") ||
                   lower.Contains("bledy:") ||
                   lower.Contains("error:") ||
                   lower.Contains("wykonano 0/") ||
                   lower.Contains("nie istnieje") ||
                   lower.Contains("nie mozna") ||
                   lower.Contains("nie można");
        }

        private static bool ShouldForceContinueAfterLastTool(List<ChatMessage> history)
        {
            if (history == null || history.Count < 2) return false;
            if (!LastUserMessageRequestsLayoutAction(history)) return false;

            for (int i = history.Count - 1; i >= 0; i--)
            {
                var msg = history[i];
                if (msg == null) continue;
                if (string.Equals(msg.Role, "tool", StringComparison.OrdinalIgnoreCase))
                {
                    string content = msg.Content?.ToString() ?? "";
                    bool isListLayouts = false;
                    bool listHasLayouts = false;
                    if (msg.ToolCallId != null && content.Length > 0)
                    {
                        int prevIdx = i - 1;
                        if (prevIdx >= 0 && history[prevIdx]?.ToolCalls != null)
                        {
                            foreach (var tc in history[prevIdx].ToolCalls)
                            {
                                if (tc.Id == msg.ToolCallId &&
                                    string.Equals(tc.Function?.Name, "ListLayoutsTool", StringComparison.OrdinalIgnoreCase) &&
                                    IsAllLayoutsListCall(tc.Function?.Arguments))
                                {
                                    isListLayouts = true;
                                    break;
                                }
                            }
                        }
                        if (isListLayouts)
                        {
                            if (content.Contains("ARKUSZ") || content.Contains("layout(ow)"))
                            {
                                listHasLayouts = true;
                            }
                        }
                    }
                    if (isListLayouts && listHasLayouts) return true;
                    return false;
                }
                if (string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return false;
        }

        private static bool IsAllLayoutsListCall(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments)) return true;

            try
            {
                var parsed = JObject.Parse(arguments);
                return parsed["LayoutName"] == null || string.IsNullOrWhiteSpace(parsed["LayoutName"]?.ToString());
            }
            catch
            {
                return arguments.IndexOf("LayoutName", StringComparison.OrdinalIgnoreCase) < 0;
            }
        }

        private static bool LastUserMessageRequestsLayoutAction(List<ChatMessage> history)
        {
            string text = null;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var msg = history[i];
                if (msg == null || !string.Equals(msg.Role, "user", StringComparison.OrdinalIgnoreCase)) continue;
                text = msg.Content?.ToString();
                if (!string.IsNullOrWhiteSpace(text)) break;
            }

            if (string.IsNullOrWhiteSpace(text)) return false;

            string lower = text.ToLowerInvariant();
            bool mentionsManyLayouts =
                lower.Contains("wszystkich arkusz") ||
                lower.Contains("wszystkie arkusz") ||
                lower.Contains("kazdym layout") ||
                lower.Contains("każdym layout") ||
                lower.Contains("kazdego layout") ||
                lower.Contains("każdego layout") ||
                lower.Contains("wszystkich layout") ||
                lower.Contains("wszystkie layout");

            bool hasActionVerb =
                lower.Contains("ustaw") ||
                lower.Contains("zmien") ||
                lower.Contains("zmień") ||
                lower.Contains("zastosuj") ||
                lower.Contains("skonfiguruj") ||
                lower.Contains("przypisz") ||
                lower.Contains("ustawić") ||
                lower.Contains("zmienić") ||
                lower.Contains("ma byc") ||
                lower.Contains("ma być");

            bool mentionsPageSetupValue =
                lower.Contains("drukark") ||
                lower.Contains("plotdevice") ||
                lower.Contains("format papieru") ||
                lower.Contains("media") ||
                lower.Contains("styl wydruku") ||
                lower.Contains("ctb") ||
                lower.Contains("stb") ||
                lower.Contains("portrait") ||
                lower.Contains("pionowo");

            return hasActionVerb && (mentionsManyLayouts || mentionsPageSetupValue);
        }

        private static string BuildForceContinueHint(List<ChatMessage> history)
        {
            int layoutCount = 0;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                var msg = history[i];
                if (msg != null && string.Equals(msg.Role, "tool", StringComparison.OrdinalIgnoreCase))
                {
                    string content = msg.Content?.ToString() ?? "";
                    int idx = content.IndexOf("layout(", StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        int startIdx = idx + "layout(".Length;
                        int parenIdx = content.IndexOf(')', startIdx);
                        if (parenIdx > startIdx)
                        {
                            int.TryParse(content.Substring(startIdx, parenIdx - startIdx), out layoutCount);
                        }
                    }
                    break;
                }
            }

            if (layoutCount <= 0) layoutCount = 2;

            string hint = "[SYSTEM REMINDER] User prosił o AKCJĘ na layoutach, nie tylko o ich listę. " +
                          $"Właśnie wylistowałeś {layoutCount} layout(ów) - teraz MUSISZ wykonać akcję na każdym z nich. " +
                          "Użyj ForeachTool z ActionTemplate zawierającym '{item}' jako placeholder nazwy layoutu, np.: " +
                          "Foreach(Items=[listę nazw layoutów z poprzedniego ListLayoutsTool], " +
                          "ActionTemplate='PageSetupTool LayoutName=\"{item}\" ...'). " +
                          "PO wykonaniu akcji na wszystkich layoutach (lub gdy już nie ma nic do zrobienia) - wtedy możesz zakończyć odpowiedź.";
            return hint;
        }
    }
}
