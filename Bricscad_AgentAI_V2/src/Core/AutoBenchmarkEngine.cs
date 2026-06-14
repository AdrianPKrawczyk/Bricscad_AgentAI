using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Bricscad_AgentAI_V2.Core
{
    // ==========================================
    // EVENTY POSTÄPU (dla UI)
    // ==========================================
    public class BenchmarkProgressEventArgs : EventArgs
    {
        public int CurrentTestIndex { get; set; }
        public int TotalTests { get; set; }
        public BenchmarkTest TestResult { get; set; }
    }

    public class BenchmarkCompletedEventArgs : EventArgs
    {
        public BenchmarkConfig FinalConfig { get; set; }
        public int PassedCount { get; set; }
        public bool WasCancelled { get; set; }
    }

    // ==========================================
    // GĹĂ“WNY SILNIK BENCHMARKU V2
    // ==========================================

    /// <summary>
    /// Izolowane laboratorium analityczne do testowania modeli LLM z architekturÄ… Tool Calling.
    /// Mierzy zdolnoĹ›Ä‡ modelu do wybierania poprawnych narzÄ™dzi i konstruowania prawidĹ‚owego JSON-a argumentĂłw.
    /// WAĹ»NE: Celowo nie eksportuje do JSONL â€” stanowi wyizolowany Test Set (brak Data Leakage).
    /// </summary>
    public class AutoBenchmarkEngine
    {
        public event EventHandler<BenchmarkProgressEventArgs> OnTestFinished;
        public event EventHandler<BenchmarkCompletedEventArgs> OnBenchmarkCompleted;
        public event EventHandler<string> OnLogMessage;

        private readonly LLMClient _llmClient;

        public AutoBenchmarkEngine(LLMClient llmClient)
        {
            _llmClient = llmClient;
        }

        // ==========================================
        // FAZA 0: PRE-FLIGHT SCHEMA CHECK
        // ==========================================
        
        /// <summary>
        /// Weryfikuje integralnoĹ›Ä‡ schematĂłw wszystkich zarejestrowanych narzÄ™dzi.
        /// JeĹ›li choÄ‡by jedno narzÄ™dzie ma wadliwy schemat, rzuca wyjÄ…tek blokujÄ…cy caĹ‚y benchmark.
        /// </summary>
        private void RunPreflightCheck()
        {
            OnLogMessage?.Invoke(this, "=== PRE-FLIGHT SCHEMA CHECK ===");
            
            var orchestrator = ToolOrchestrator.Instance;

            var toolType = typeof(IToolV2);
            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(p => toolType.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract)
                .ToList();

            var errors = new List<string>();

            foreach (var type in types)
            {
                try
                {
                    var instance = (IToolV2)Activator.CreateInstance(type);
                    var schema = instance.GetToolSchema();

                    if (schema == null)
                    {
                        errors.Add($"[{type.Name}] GetToolSchema() zwrĂłciĹ‚ null.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(schema.Function?.Name))
                        errors.Add($"[{type.Name}] Brakuje pola 'Function.Name' w schemacie.");
                    if (string.IsNullOrWhiteSpace(schema.Function?.Description))
                        errors.Add($"[{type.Name}] Brakuje pola 'Function.Description' w schemacie.");

                    OnLogMessage?.Invoke(this, $"  âś“ {type.Name}: OK (Name='{schema.Function?.Name}')");
                }
                catch (Exception ex)
                {
                    errors.Add($"[{type.Name}] BĹ‚Ä…d instancjonowania: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                string errorReport = "PRE-FLIGHT FAILED:\n" + string.Join("\n", errors);
                OnLogMessage?.Invoke(this, errorReport);
                throw new InvalidOperationException(errorReport);
            }

            OnLogMessage?.Invoke(this, $"Pre-flight zakoĹ„czony sukcesem. Zweryfikowano {types.Count} narzÄ™dzi.");
        }

        // ==========================================
        // GĹĂ“WNA METODA URUCHOMIENIA
        // ==========================================
        public async Task<BenchmarkConfig> RunBenchmarkAsync(
            string jsonFilePath,
            string profileName = null,
            CancellationToken ct = default,
            bool saveErrors = true)
        {
            return await RunBenchmarkAsync(jsonFilePath, BenchmarkRunOptions.Manual(profileName, saveErrors), ct);
        }

        public async Task<BenchmarkConfig> RunBenchmarkAsync(
            string jsonFilePath,
            BenchmarkRunOptions options,
            CancellationToken ct = default)
        {
            options = options ?? BenchmarkRunOptions.Manual(null);
            string profileName = options.ProfileName;
            bool saveErrors = options.SaveErrors;

            // --- FAZA 0: Pre-flight ---
            RunPreflightCheck();

            OnLogMessage?.Invoke(this, "=== START BENCHMARKU V2 ===");
            if (!string.IsNullOrEmpty(profileName))
            {
                OnLogMessage?.Invoke(this, $"Aktywny profil benchmarku: {profileName}");
            }

            string jsonContent = File.ReadAllText(jsonFilePath);
            BenchmarkConfig config = JsonConvert.DeserializeObject<BenchmarkConfig>(jsonContent);
            if (config.RunMetadata == null)
            {
                config.RunMetadata = new RunMetadata();
            }

            var activeProvider = LLMConfigManager.ResolveProviderForProfile(profileName);
            string benchmarkName = !string.IsNullOrWhiteSpace(config.RunMetadata.BenchmarkName)
                ? config.RunMetadata.BenchmarkName
                : (!string.IsNullOrWhiteSpace(config.RunMetadata.ModelName)
                    ? config.RunMetadata.ModelName
                    : Path.GetFileNameWithoutExtension(jsonFilePath));

            config.RunMetadata.BenchmarkName = benchmarkName;
            config.RunMetadata.ProfileName = profileName ?? string.Empty;
            config.RunMetadata.ProviderName = activeProvider?.Name ?? string.Empty;
            config.RunMetadata.ProviderEndpoint = activeProvider?.EndpointUrl ?? string.Empty;
            // v2.28.83: ProviderId dla rozroznienia providerow (np. 2 PC z tym samym modelem).
            config.RunMetadata.ProviderId = activeProvider?.Id;
            config.RunMetadata.ModelName = activeProvider?.ModelName ?? config.RunMetadata.ModelName;
            config.RunMetadata.Temperature = activeProvider?.Temperature ?? 0.0;
            config.RunMetadata.MaxTokens = activeProvider?.MaxTokens ?? 0;
            config.RunMetadata.TopP = activeProvider?.TopP ?? 0.0;
            config.RunMetadata.TopK = activeProvider?.TopK ?? 0;
            config.RunMetadata.MinP = activeProvider?.MinP ?? 0.0;
            config.RunMetadata.RepetitionPenalty = activeProvider?.RepetitionPenalty ?? 0.0;
            config.RunMetadata.ReasoningEffort = activeProvider?.ReasoningEffort ?? string.Empty;
            config.RunMetadata.AutoLoadModel = activeProvider?.AutoLoadModel ?? false;
            config.RunMetadata.GpuOffload = activeProvider?.GpuOffload ?? string.Empty;
            config.RunMetadata.LoadContextLength = activeProvider?.LoadContextLength ?? 0;
            config.RunMetadata.TtlSeconds = activeProvider?.TtlSeconds ?? 0;
            config.RunMetadata.FlashAttention = activeProvider?.FlashAttention ?? false;
            config.RunMetadata.OffloadKvCache = activeProvider?.OffloadKvCache ?? false;
            config.RunMetadata.MaxContextTokens = LLMConfigManager.Current?.MaxContextTokens ?? 0;
            config.RunMetadata.ContextCompressionThreshold = LLMConfigManager.Current?.ContextCompressionThreshold ?? 0;
            config.RunMetadata.RunDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            config.RunMetadata.RunMode = string.IsNullOrWhiteSpace(options.RunMode) ? "manual" : options.RunMode;
            config.RunMetadata.PromptOverridePath = options.PromptOverridePath ?? string.Empty;
            config.RunMetadata.OutputRoot = options.OutputRoot ?? string.Empty;
            config.RunMetadata.CandidateId = options.CandidateId ?? string.Empty;
            config.RunMetadata.JobId = options.JobId ?? string.Empty;
            config.RunMetadata.SaveToUserBenchmarkHistory = options.SaveToUserBenchmarkHistory;
            if (!string.IsNullOrWhiteSpace(options.PromptOverridePath))
            {
                config.RunMetadata.Comment = string.IsNullOrWhiteSpace(config.RunMetadata.Comment)
                    ? $"PromptOverridePath={options.PromptOverridePath}"
                    : $"{config.RunMetadata.Comment}\nPromptOverridePath={options.PromptOverridePath}";
            }
            OnLogMessage?.Invoke(this, $"Model benchmarku: {config.RunMetadata.ModelName} | Provider: {config.RunMetadata.ProviderName}");

            // v2.28.78: Jesli provider to llama.cpp/LM Studio, pobierz aktualnie zaladowany model
            // z /api/v1/models i zaktualizuj RunMetadata.ModelName (inaczej zostaje stary z llm_providers.json).
            if (activeProvider != null && !string.IsNullOrEmpty(activeProvider.EndpointUrl))
            {
                try
                {
                    var llmClientForModelCheck = new LLMClient(ToolOrchestrator.Instance);
                    var loadedDesc = await llmClientForModelCheck.GetLoadedModelInfoAsync(activeProvider);
                    if (loadedDesc != null)
                    {
                        string detectedModel = !string.IsNullOrEmpty(loadedDesc.DisplayName) ? loadedDesc.DisplayName : loadedDesc.Id;
                        if (!string.IsNullOrEmpty(detectedModel) && detectedModel != config.RunMetadata.ModelName)
                        {
                            OnLogMessage?.Invoke(this, $"Wykryto aktualnie zaladowany model: {detectedModel} (poprzednio: {config.RunMetadata.ModelName})");
                            config.RunMetadata.ModelName = detectedModel;
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnLogMessage?.Invoke(this, $"Nie udalo sie wykryc aktualnego modelu: {ex.Message}");
                }
            }

            int passedCount = 0;
            long totalTimeMs = 0;
            int currentIndex = 0;
            bool isCancelled = false;

            Document doc = Application.DocumentManager.MdiActiveDocument;

            // --- FAZA 1: PÄ™tla Testowa ---
            foreach (var test in config.Tests)
            {
                if (ct.IsCancellationRequested)
                {
                    OnLogMessage?.Invoke(this, "PRZERWANO przez uĹĽytkownika.");
                    isCancelled = true;
                    break;
                }

                currentIndex++;
                OnLogMessage?.Invoke(this, $"\n--- Test {currentIndex}/{config.Tests.Count}: [{test.Category}] {test.TestName} ---");

                // SANDBOX: Reset pamiÄ™ci Agenta przed kaĹĽdym testem
                AgentMemoryState.Variables.Clear();
                AgentMemoryState.Clear();

                // WstrzykniÄ™cie MockMemoryVariables
                if (test.MockMemoryVariables != null)
                {
                    foreach (var kvp in test.MockMemoryVariables)
                    {
                        AgentMemoryState.Variables[kvp.Key] = kvp.Value;
                        OnLogMessage?.Invoke(this, $"  [MOCK VAR] @{kvp.Key} = '{kvp.Value}'");
                    }
                }

                // Budowanie historii konwersacji
                var history = new List<ChatMessage>();

                // Dodanie system promptu profilu jesli wybrano
                if (!string.IsNullOrEmpty(profileName))
                {
                    string systemPromptContent = LoadBenchmarkPrompt(profileName, options.PromptOverridePath);
                    if (!string.IsNullOrWhiteSpace(systemPromptContent))
                    {
                        history.Add(new ChatMessage { Role = "system", Content = systemPromptContent });
                    }
                }

                history.Add(new ChatMessage { Role = "user", Content = test.UserPrompt });
                test.RecordedToolCalls = new List<RecordedToolCall>();
                test.FailedRulesErrors = new List<string>();

                // âŹ±ď¸Ź START STOPERA
                var sw = Stopwatch.StartNew();

                // WywoĹ‚anie LLM w trybie Benchmark
                await _llmClient.SendMessageBenchmarkAsync(
                    history,
                    test.SimulatedCADResponses,
                    test.RecordedToolCalls,
                    doc,
                    profileName,
                    ct: ct);

                // âŹ±ď¸Ź STOP STOPERA
                sw.Stop();
                test.ExecutionTimeMs = sw.ElapsedMilliseconds;
                totalTimeMs += test.ExecutionTimeMs;

                if (ct.IsCancellationRequested)
                {
                    isCancelled = true;
                    break;
                }

                // v2.28.85: Jesli LLM zwrocil blad HTTP (recordedCalls ma _LLM_ERROR_*),
                // zaloguj to WYRAZNIE z body bledu - inaczej walidator pokaze tylko
                // "tool not called" co nie wyjasnia prawdziwej przyczyny.
                var llmError = test.RecordedToolCalls.FirstOrDefault(c => c.ToolName != null && c.ToolName.StartsWith("_LLM_ERROR_"));
                if (llmError != null)
                {
                    string errArgs = llmError.Arguments?.ToString() ?? "(brak args)";
                    OnLogMessage?.Invoke(this, $"  ⚠ LLM ERROR (HTTP): {llmError.ToolName} | {errArgs}");
                }

                // FAZA 2: Walidacja
                test.Passed = ValidateTest(test);
                if (test.Passed)
                {
                    passedCount++;
                    OnLogMessage?.Invoke(this, $"  âś“ ZALICZONY ({test.ExecutionTimeMs}ms)");
                }
                else
                {
                    OnLogMessage?.Invoke(this, $"  âś— OBLANY ({test.ExecutionTimeMs}ms). BĹ‚Ä™dy:");
                    foreach (var err in test.FailedRulesErrors)
                        OnLogMessage?.Invoke(this, $"    - {err}");
                }

                OnTestFinished?.Invoke(this, new BenchmarkProgressEventArgs
                {
                    CurrentTestIndex = currentIndex,
                    TotalTests = config.Tests.Count,
                    TestResult = test
                });
            }

            // --- FAZA 3: Obliczenia KoĹ„cowe i Raportowanie ---
            if (config.Tests.Count > 0 && !isCancelled)
            {
                config.RunMetadata.GlobalScore = Math.Round((double)passedCount / config.Tests.Count * 100, 2);
                config.RunMetadata.AverageExecutionTimeMs = Math.Round((double)totalTimeMs / currentIndex, 0);
                config.RunMetadata.CategoriesScores = config.Tests
                    .GroupBy(t => t.Category)
                    .ToDictionary(
                        g => g.Key,
                        g => Math.Round((double)g.Count(t => t.Passed) / g.Count() * 100, 2));
            }

            SaveReports(config, jsonFilePath, saveErrors, options);

            OnLogMessage?.Invoke(this, $"\n=== ZAKOĹCZONO. Wynik: {config.RunMetadata.GlobalScore}% ({passedCount}/{currentIndex}) | Czas Ĺ›r.: {config.RunMetadata.AverageExecutionTimeMs}ms ===");
            OnBenchmarkCompleted?.Invoke(this, new BenchmarkCompletedEventArgs
            {
                FinalConfig = config,
                PassedCount = passedCount,
                WasCancelled = isCancelled
            });

            return config;
        }

        // ==========================================
        // WALIDATOR (AUTO-SÄDZIA V2)
        // ==========================================
        private bool ValidateTest(BenchmarkTest test)
        {
            bool allPassed = true;

            foreach (var rule in test.ValidationRules)
            {
                bool rulePassed = true;
                string ruleError = rule.ErrorMessage ?? $"ReguĹ‚a '{rule.RuleType}' nie przeszĹ‚a.";

                try
                {
                    switch (rule.RuleType)
                    {
                        // --- Sprawdzenie, czy LLM w ogĂłle wywoĹ‚aĹ‚ narzÄ™dzie ---
                        case "ToolCalled":
                            rulePassed = test.RecordedToolCalls.Any(c =>
                                string.Equals(c.ToolName, rule.TargetValue, StringComparison.OrdinalIgnoreCase));
                            break;

                        // --- Sprawdzenie wartoĹ›ci konkretnego argumentu JSON ---
                        case "ArgumentMatch":
                            // Szukamy ostatniego wywoĹ‚ania narzÄ™dzia, ktĂłre faktycznie posiada szukany argument
                            var callForMatch = test.RecordedToolCalls
                                .LastOrDefault(c => c.Arguments != null && ResolveJsonPath(c.Arguments, rule.TargetArgument) != null);

                            if (callForMatch == null)
                            {
                                rulePassed = false;
                                ruleError = $"{ruleError} (Argument '{rule.TargetArgument}' nie zostaĹ‚ znaleziony w ĹĽadnym wywoĹ‚aniu)";
                                break;
                            }

                            string actualValue = ResolveJsonPath(callForMatch.Arguments, rule.TargetArgument);
                            rulePassed = ValuesMatch(actualValue, rule.TargetValue);
                            if (!rulePassed)
                                ruleError = $"{ruleError} (Znaleziono: '{actualValue}', Oczekiwano: '{rule.TargetValue}')";
                            break;

                        case "AnyArgumentMatch":
                            var matchingValues = test.RecordedToolCalls
                                .Where(c => c.Arguments != null)
                                .Select(c => ResolveJsonPath(c.Arguments, rule.TargetArgument))
                                .Where(v => v != null)
                                .ToList();

                            if (matchingValues.Count == 0)
                            {
                                rulePassed = false;
                                ruleError = $"{ruleError} (Argument '{rule.TargetArgument}' nie zostal znaleziony w zadnym wywolaniu)";
                                break;
                            }

                            rulePassed = matchingValues.Any(v => ValuesMatch(v, rule.TargetValue));
                            if (!rulePassed)
                                ruleError = $"{ruleError} (Znaleziono: '{string.Join(" | ", matchingValues)}', Oczekiwano: '{rule.TargetValue}')";
                            break;

                        // v2.28.71: Wariant AnyArgumentMatch, w ktorym wartosc moze byc na DOWOLNEJ z wielu sciezek.
                        // TargetArgument zawiera sciezki oddzielone "|" (np. "FilterValue|Items[0]|Items[1]").
                        // PASS gdy wartosc TargetValue wystapi na ktorejkolwiek sciezce w ktorymkolwiek wywolaniu.
                        // Uzywane dla testow Foreach vs 2 EditAttributes, gdzie Items[X] (Foreach)
                        // i FilterValue (EditAttributes) to alternatywne miejsca na te sama wartosc.
                        case "AnyArgumentMatchAnyPath":
                            var allPathValues = new List<string>();
                            var pathsToCheck = rule.TargetArgument.Split('|').Select(p => p.Trim()).ToList();

                            foreach (var path in pathsToCheck)
                            {
                                var pathValues = test.RecordedToolCalls
                                    .Where(c => c.Arguments != null)
                                    .Select(c => ResolveJsonPath(c.Arguments, path))
                                    .Where(v => v != null);
                                allPathValues.AddRange(pathValues);
                            }

                            if (allPathValues.Count == 0)
                            {
                                rulePassed = false;
                                ruleError = $"{ruleError} (Zadna z sciezek [{string.Join(", ", pathsToCheck)}] nie zostala znaleziona w zadnym wywolaniu)";
                                break;
                            }

                            rulePassed = allPathValues.Any(v => ValuesMatch(v, rule.TargetValue));
                            if (!rulePassed)
                                ruleError = $"{ruleError} (Znaleziono: '{string.Join(" | ", allPathValues)}', Oczekiwano: '{rule.TargetValue}')";
                            break;

                        case "AnyOfArgumentMatch":
                            var candidateValues = test.RecordedToolCalls
                                .Where(c => c.Arguments != null)
                                .Select(c => ResolveJsonPath(c.Arguments, rule.TargetArgument))
                                .Where(v => v != null)
                                .ToList();

                            if (candidateValues.Count == 0)
                            {
                                rulePassed = false;
                                ruleError = $"{ruleError} (Argument '{rule.TargetArgument}' nie zostal znaleziony w zadnym wywolaniu)";
                                break;
                            }

                            var expectedVariants = (rule.TargetValue ?? string.Empty)
                                .Split(new[] { " || " }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(v => v.Trim())
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .ToList();

                            rulePassed = candidateValues.Any(actual =>
                                expectedVariants.Any(expected =>
                                    ValuesMatch(actual, expected)));

                            if (!rulePassed)
                                ruleError = $"{ruleError} (Znaleziono: '{string.Join(" | ", candidateValues)}', Dozwolone: '{string.Join(" || ", expectedVariants)}')";
                            break;

                        // v2.28.54: Wariant AnyOfArgumentMatch, w ktorym BRAK argumentu rowniez akceptowany (np. Items LUB GenerateSequence.Count - oba poprawne).
                        case "AnyOfArgumentMatchOrAbsent":
                            var candidateValuesOrAbsent = test.RecordedToolCalls
                                .Where(c => c.Arguments != null)
                                .Select(c => ResolveJsonPath(c.Arguments, rule.TargetArgument))
                                .Where(v => v != null)
                                .ToList();

                            if (candidateValuesOrAbsent.Count == 0)
                            {
                                // Brak argumentu = PASS (warunek OR ABSENT)
                                rulePassed = true;
                                break;
                            }

                            var expectedVariantsOrAbsent = (rule.TargetValue ?? string.Empty)
                                .Split(new[] { " || " }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(v => v.Trim())
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .ToList();

                            rulePassed = candidateValuesOrAbsent.Any(actual =>
                                expectedVariantsOrAbsent.Any(expected =>
                                    ValuesMatch(actual, expected)));

                            if (!rulePassed)
                                ruleError = $"{ruleError} (Znaleziono: '{string.Join(" | ", candidateValuesOrAbsent)}', Dozwolone: '{string.Join(" || ", expectedVariantsOrAbsent)}')";
                            break;


                        // v2.28.69: Sprawdzenie czy wartosc argumentu zawiera znak nowej linii (\n).
                        // Uzywane dla testow wieloliniowych (np. MText, OPIS z wielolinijkowym tekstem).
                        // Akceptuje \n w roznych formach: \\n (escaped w JSON), rzeczywisty newline (\n).
                        case "StringContainsNewline":
                            var newlineValues = test.RecordedToolCalls
                                .Where(c => c.Arguments != null)
                                .Select(c => ResolveJsonPath(c.Arguments, rule.TargetArgument))
                                .Where(v => v != null)
                                .ToList();

                            if (newlineValues.Count == 0)
                            {
                                rulePassed = false;
                                ruleError = $"{ruleError} (Argument '{rule.TargetArgument}' nie zostal znaleziony w zadnym wywolaniu)";
                                break;
                            }

                            // Sprawdzamy czy wartosc zawiera \n (escaped lub rzeczywisty)
                            rulePassed = newlineValues.Any(v => v.Contains("\\n") || v.Contains("\n"));
                            if (!rulePassed)
                                ruleError = $"{ruleError} (Znaleziono: '{string.Join(" | ", newlineValues)}', oczekiwano wartosci z \\n)";
                            break;


                        case "ToolCallCountMax":
                            if (!int.TryParse(rule.ExpectedOutput, out int maxAllowedCalls))
                            {
                                rulePassed = false;
                                ruleError = $"{ruleError} (Niepoprawny limit ExpectedOutput='{rule.ExpectedOutput}')"; 
                                break;
                            }

                            int matchingCallCount = test.RecordedToolCalls.Count(c =>
                                string.Equals(c.ToolName, rule.TargetValue, StringComparison.OrdinalIgnoreCase));

                            rulePassed = matchingCallCount <= maxAllowedCalls;
                            if (!rulePassed)
                                ruleError = $"{ruleError} (Znaleziono {matchingCallCount} wywolan narzedzia '{rule.TargetValue}', limit: {maxAllowedCalls})";
                            break;

                        // --- Sprawdzenie kolejnoĹ›ci wywoĹ‚aĹ„ narzÄ™dzi ---
                        case "SequenceMatch":
                            var expectedSequence = rule.TargetValue
                                .Split(',')
                                .Select(s => s.Trim())
                                .ToArray();

                            int lastFoundIndex = -1;
                            foreach (var expectedTool in expectedSequence)
                            {
                                int foundAt = -1;
                                for (int i = lastFoundIndex + 1; i < test.RecordedToolCalls.Count; i++)
                                {
                                    if (string.Equals(test.RecordedToolCalls[i].ToolName, expectedTool, StringComparison.OrdinalIgnoreCase))
                                    {
                                        foundAt = i;
                                        break;
                                    }
                                }
                                if (foundAt == -1)
                                {
                                    rulePassed = false;
                                    ruleError = $"{ruleError} (Nie znaleziono '{expectedTool}' po pozycji {lastFoundIndex})";
                                    break;
                                }
                                lastFoundIndex = foundAt;
                            }
                            break;

                        // --- Weryfikacja poprawnoĹ›ci formuĹ‚y algebry w argumencie ---
                        case "EvaluateMath_Argument":
                            var callForMath = test.RecordedToolCalls
                                .LastOrDefault(c => string.Equals(c.ToolName, "CalculateMath", StringComparison.OrdinalIgnoreCase)
                                                 && c.Arguments != null
                                                 && ResolveJsonPath(c.Arguments, rule.TargetArgument) != null);

                            if (callForMath == null)
                            {
                                rulePassed = false;
                                ruleError = "Brak zarejestrowanych wywoĹ‚aĹ„ narzÄ™dzia CalculateMath z podanym argumentem.";
                                break;
                            }

                            string mathFormula = ResolveJsonPath(callForMath.Arguments, rule.TargetArgument);
                            if (string.IsNullOrEmpty(mathFormula))
                            {
                                rulePassed = false;
                                ruleError = $"Nie znaleziono formuĹ‚y pod Ĺ›cieĹĽkÄ… '{rule.TargetArgument}'.";
                                break;
                            }

                            string targetUnit = ResolveJsonPath(callForMath.Arguments, "TargetUnit");

                            // Podstawianie MockData
                            if (rule.MockData != null)
                                foreach (var kvp in rule.MockData)
                                    mathFormula = mathFormula.Replace(kvp.Key, kvp.Value);

                            try
                            {
                                string rpnFormula = RpnCalculator.ConvertInfixToRpn(mathFormula);
                                if (!string.IsNullOrEmpty(targetUnit))
                                {
                                    rpnFormula += $" '{targetUnit}' CONVE";
                                }

                                string mathResult = RpnCalculator.Evaluate(rpnFormula);
                                if (!RpnCalculator.AreValuesPhysicallyEqual(rule.ExpectedOutput, mathResult))
                                {
                                    rulePassed = false;
                                    ruleError = $"BĹ‚Ä…d obliczeĹ„. Oczekiwano '{rule.ExpectedOutput}', obliczono '{mathResult}' (wzĂłr: {mathFormula})";
                                }
                            }
                            catch (Exception ex)
                            {
                                rulePassed = false;
                                ruleError = $"Silnik odrzuciĹ‚ formuĹ‚Ä™: {ex.Message} (wzĂłr: {mathFormula})";
                            }
                            break;

                        default:
                            ruleError = $"Nieznany typ reguĹ‚y: '{rule.RuleType}'.";
                            rulePassed = false;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // Walidator jest defensywny â€“ nie wysadza aplikacji
                    rulePassed = false;
                    ruleError = $"WyjÄ…tek podczas walidacji reguĹ‚y '{rule.RuleType}': {ex.Message}";
                }

                if (!rulePassed)
                {
                    allPassed = false;
                    test.FailedRulesErrors.Add(ruleError);
                }
            }

            return allPassed;
        }

        // ==========================================
        // HELPER: Resolver Ĺ›cieĹĽki JSON
        // ==========================================
        /// <summary>
        /// RozwiÄ…zuje prostÄ… Ĺ›cieĹĽkÄ™ do wartoĹ›ci w JObject (np. "Properties[0].PropertyName").
        /// Defensywny â€“ zwraca null przy bĹ‚Ä™dzie zamiast rzucaÄ‡ wyjÄ…tek.
        /// </summary>
        private string ResolveJsonPath(JObject obj, string path)
        {
            if (obj == null || string.IsNullOrEmpty(path)) return null;

            try
            {
                JToken current = obj;
                // Rozbijamy Ĺ›cieĹĽkÄ™ na segmenty (klucze i indeksy tablic)
                var segments = path.Replace("[", ".[").Split('.');
                foreach (var seg in segments)
                {
                    if (seg.StartsWith("[") && seg.EndsWith("]"))
                    {
                        // Indeks tablicy: [0]
                        if (int.TryParse(seg.Trim('[', ']'), out int idx))
                            current = current[idx];
                        else return null;
                    }
                    else
                    {
                        current = current[seg];
                    }
                    if (current == null) return null;
                }
                return TokenToInvariantString(current);
            }
            catch
            {
                return null;
            }
        }

        // v2.28.52a: Helper do konwersji JToken na string z InvariantCulture.
        // Powod: Newtonsoft ma extension method ToString(Formatting) ktory zaciemnia wbudowany overload JToken.ToString(IFormatProvider).
        // Rozwiazanie: dla JValue z liczba uzywamy Convert.ToString z InvariantCulture.
        //              dla reszty JToken uzywamy default ToString() (stringi, JSON objects, itp. nie maja problemu locale).
        private string TokenToInvariantString(JToken token)
        {
            if (token == null) return null;
            if (token is JValue jv)
            {
                if (jv.Value == null) return null;
                if (jv.Type == JTokenType.Float || jv.Type == JTokenType.Integer)
                {
                    return Convert.ToString(jv.Value, System.Globalization.CultureInfo.InvariantCulture);
                }
                return jv.Value.ToString();
            }
            return token.ToString(Newtonsoft.Json.Formatting.None);
        }

        private bool ValuesMatch(string actual, string expected)
        {
            if (string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (TryParseJsonToken(actual, out var actualToken) && TryParseJsonToken(expected, out var expectedToken))
            {
                return JToken.DeepEquals(actualToken, expectedToken);
            }

            // v2.28.52: Obsluga wartosci numerycznych z roznych locale (np. "0,5" w polskim vs "0.5" w InvariantCulture)
            if (double.TryParse(actual, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double actualNum) &&
                double.TryParse(expected, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double expectedNum))
            {
                return Math.Abs(actualNum - expectedNum) < 1e-9;
            }

            return false;
        }

        private bool TryParseJsonToken(string value, out JToken token)
        {
            token = null;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string trimmed = value.Trim();
            if (!(trimmed.StartsWith("{") && trimmed.EndsWith("}")) &&
                !(trimmed.StartsWith("[") && trimmed.EndsWith("]")))
            {
                return false;
            }

            try
            {
                token = JToken.Parse(trimmed);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string LoadBenchmarkPrompt(string profileName, string promptOverridePath)
        {
            if (!string.IsNullOrWhiteSpace(promptOverridePath))
            {
                if (!File.Exists(promptOverridePath))
                {
                    throw new FileNotFoundException($"Nie znaleziono pliku PromptOverridePath: {promptOverridePath}", promptOverridePath);
                }

                OnLogMessage?.Invoke(this, $"[Benchmark] Uzywam PromptOverridePath: {promptOverridePath}");
                return File.ReadAllText(promptOverridePath, Encoding.UTF8);
            }

            return ToolConfigManager.LoadEffectivePromptForProfile(profileName);
        }

        // ==========================================
        // RAPORTOWANIE
        // ==========================================
        // v2.28.83: Buduje klucz folderu z ModelName + 6-znakowym skrotem ProviderId.
        // Zapobiega kolizji folderow gdy 2+ providerow (np. 2 PC) uzywa tego samego modelu.
        // Format: "gemma-4-31b-it-qat@a1b2c3" (ModelName + "@" + 6 hex z GUID-a).
        // Gdy providerId jest null/Empty - zwraca sam ModelName (kompatybilnosc wsteczna).
        private string BuildModelDirKey(string modelName, Guid? providerId)
        {
            string safeModel = string.Join("_", (modelName ?? "UnknownModel")
                .Split(Path.GetInvalidFileNameChars()));
            if (providerId.HasValue && providerId.Value != Guid.Empty)
            {
                // "N" = 32 znaki hex bez kresek. Bierzemy pierwsze 6 - wystarczajaco unikalne dla 2-3 PC.
                string shortId = providerId.Value.ToString("N").Substring(0, 6);
                return $"{safeModel}@{shortId}";
            }
            return safeModel;
        }

        private void SaveReports(BenchmarkConfig config, string sourceJsonPath, bool saveErrors, BenchmarkRunOptions options = null)
        {
            try
            {
                string resultJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string rootDir = Path.GetDirectoryName(sourceJsonPath);
                string origName = Path.GetFileNameWithoutExtension(sourceJsonPath);

                string safeModel = BuildModelDirKey(config.RunMetadata.ModelName, config.RunMetadata.ProviderId);
                bool labOutput = options != null
                    && !options.SaveToUserBenchmarkHistory
                    && !string.IsNullOrWhiteSpace(options.OutputRoot);
                string modelDir = labOutput
                    ? options.OutputRoot
                    : Path.Combine(rootDir, safeModel);
                if (!Directory.Exists(modelDir)) Directory.CreateDirectory(modelDir);

                string fullName = labOutput
                    ? $"{origName}_FULL_{timestamp}.json"
                    : $"{origName}_{safeModel}_FULL_{timestamp}.json";
                string fullPath = Path.Combine(modelDir, fullName);
                File.WriteAllText(fullPath, resultJson, System.Text.Encoding.UTF8);
                OnLogMessage?.Invoke(this, $"Raport FULL zapisany: {fullPath}");

                if (saveErrors)
                {
                    var failed = config.Tests.Where(t => !t.Passed).ToList();
                    if (failed.Count > 0)
                    {
                        var errConfig = new BenchmarkConfig { RunMetadata = config.RunMetadata, Tests = failed };
                        string errJson = JsonConvert.SerializeObject(errConfig, Formatting.Indented);
                        string errName = labOutput
                            ? $"{origName}_ERRORS_{timestamp}.json"
                            : $"{origName}_{safeModel}_ERRORS_{timestamp}.json";
                        string errPath = Path.Combine(modelDir, errName);
                        File.WriteAllText(errPath, errJson, System.Text.Encoding.UTF8);
                        OnLogMessage?.Invoke(this, $"Raport ERRORS zapisany: {errPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke(this, $"BĹ‚Ä…d zapisu raportu: {ex.Message}");
            }
        }

        public BenchmarkBatchSummary SaveBatchSummaryReport(
            IReadOnlyList<BenchmarkQueueItem> items,
            string profileName,
            string providerName,
            string providerEndpoint,
            Guid? providerId,
            string modelName)
        {
            if (items == null || items.Count == 0)
            {
                return null;
            }

            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string firstPath = items.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.FilePath))?.FilePath;
                string rootDir = !string.IsNullOrWhiteSpace(firstPath)
                    ? Path.GetDirectoryName(firstPath)
                    : Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

                var firstMeta = items.FirstOrDefault(i => i.FinalConfig?.RunMetadata != null)?.FinalConfig?.RunMetadata;
                string effectiveModelName = !string.IsNullOrWhiteSpace(firstMeta?.ModelName)
                    ? firstMeta.ModelName
                    : modelName;
                Guid? effectiveProviderId = firstMeta?.ProviderId ?? providerId;
                string effectiveProviderName = !string.IsNullOrWhiteSpace(firstMeta?.ProviderName)
                    ? firstMeta.ProviderName
                    : providerName;
                string effectiveProviderEndpoint = !string.IsNullOrWhiteSpace(firstMeta?.ProviderEndpoint)
                    ? firstMeta.ProviderEndpoint
                    : providerEndpoint;

                string safeModel = BuildModelDirKey(effectiveModelName, effectiveProviderId);
                string modelDir = Path.Combine(rootDir, safeModel);
                if (!Directory.Exists(modelDir))
                {
                    Directory.CreateDirectory(modelDir);
                }

                int totalTests = items.Sum(i => i.TestCount);
                int passedTests = items.Sum(i => i.PassedCount);
                var scoredItems = items.Where(i => i.Score.HasValue).ToList();

                var summary = new BenchmarkBatchSummary
                {
                    BatchName = $"BenchmarkBatch_{timestamp}",
                    ProfileName = profileName ?? string.Empty,
                    ProviderName = effectiveProviderName ?? string.Empty,
                    ProviderEndpoint = effectiveProviderEndpoint ?? string.Empty,
                    ProviderId = effectiveProviderId,
                    ModelName = effectiveModelName ?? string.Empty,
                    RunDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                    TotalBenchmarks = items.Count,
                    CompletedBenchmarks = items.Count(i => string.Equals(i.Status, "Zakonczono", StringComparison.OrdinalIgnoreCase)),
                    CancelledBenchmarks = items.Count(i => string.Equals(i.Status, "Przerwano", StringComparison.OrdinalIgnoreCase)),
                    FailedBenchmarks = items.Count(i => string.Equals(i.Status, "Blad krytyczny", StringComparison.OrdinalIgnoreCase)),
                    TotalTests = totalTests,
                    PassedTests = passedTests,
                    WeightedGlobalScore = totalTests > 0 ? Math.Round((double)passedTests / totalTests * 100, 2) : 0,
                    AverageBenchmarkScore = scoredItems.Count > 0 ? Math.Round(scoredItems.Average(i => i.Score.Value), 2) : 0,
                    TotalExecutionTimeMs = items.Sum(i => i.DurationMs),
                    Benchmarks = items.Select(i => new BenchmarkBatchSummaryItem
                    {
                        FilePath = i.FilePath,
                        DisplayName = i.DisplayName,
                        BenchmarkName = i.BenchmarkName,
                        Status = i.Status,
                        TestCount = i.TestCount,
                        PassedCount = i.PassedCount,
                        FailedCount = i.FailedCount,
                        Score = i.Score,
                        DurationMs = i.DurationMs,
                        ErrorMessage = i.ErrorMessage
                    }).ToList()
                };

                string summaryPath = Path.Combine(modelDir, $"{summary.BatchName}_{safeModel}_SUMMARY.json");
                string summaryJson = JsonConvert.SerializeObject(summary, Formatting.Indented);
                File.WriteAllText(summaryPath, summaryJson, System.Text.Encoding.UTF8);
                OnLogMessage?.Invoke(this, $"Raport SUMMARY zapisany: {summaryPath}");

                return summary;
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke(this, $"Blad zapisu raportu zbiorczego: {ex.Message}");
                return null;
            }
        }
    }
}
