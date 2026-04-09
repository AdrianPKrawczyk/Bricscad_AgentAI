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
using System.Threading;
using System.Threading.Tasks;

namespace Bricscad_AgentAI_V2.Core
{
    // ==========================================
    // EVENTY POSTĘPU (dla UI)
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
    // GŁÓWNY SILNIK BENCHMARKU V2
    // ==========================================

    /// <summary>
    /// Izolowane laboratorium analityczne do testowania modeli LLM z architekturą Tool Calling.
    /// Mierzy zdolność modelu do wybierania poprawnych narzędzi i konstruowania prawidłowego JSON-a argumentów.
    /// WAŻNE: Celowo nie eksportuje do JSONL — stanowi wyizolowany Test Set (brak Data Leakage).
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
        /// Weryfikuje integralność schematów wszystkich zarejestrowanych narzędzi.
        /// Jeśli choćby jedno narzędzie ma wadliwy schemat, rzuca wyjątek blokujący cały benchmark.
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
                        errors.Add($"[{type.Name}] GetToolSchema() zwrócił null.");
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(schema.Function?.Name))
                        errors.Add($"[{type.Name}] Brakuje pola 'Function.Name' w schemacie.");
                    if (string.IsNullOrWhiteSpace(schema.Function?.Description))
                        errors.Add($"[{type.Name}] Brakuje pola 'Function.Description' w schemacie.");

                    OnLogMessage?.Invoke(this, $"  ✓ {type.Name}: OK (Name='{schema.Function?.Name}')");
                }
                catch (Exception ex)
                {
                    errors.Add($"[{type.Name}] Błąd instancjonowania: {ex.Message}");
                }
            }

            if (errors.Count > 0)
            {
                string errorReport = "PRE-FLIGHT FAILED:\n" + string.Join("\n", errors);
                OnLogMessage?.Invoke(this, errorReport);
                throw new InvalidOperationException(errorReport);
            }

            OnLogMessage?.Invoke(this, $"Pre-flight zakończony sukcesem. Zweryfikowano {types.Count} narzędzi.");
        }

        // ==========================================
        // GŁÓWNA METODA URUCHOMIENIA
        // ==========================================
        public async Task<BenchmarkConfig> RunBenchmarkAsync(
            string jsonFilePath,
            CancellationToken ct = default,
            bool saveErrors = true)
        {
            // --- FAZA 0: Pre-flight ---
            RunPreflightCheck();

            OnLogMessage?.Invoke(this, "=== START BENCHMARKU V2 ===");

            string jsonContent = File.ReadAllText(jsonFilePath);
            BenchmarkConfig config = JsonConvert.DeserializeObject<BenchmarkConfig>(jsonContent);
            config.RunMetadata.RunDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

            int passedCount = 0;
            long totalTimeMs = 0;
            int currentIndex = 0;
            bool isCancelled = false;

            Document doc = Application.DocumentManager.MdiActiveDocument;

            // --- FAZA 1: Pętla Testowa ---
            foreach (var test in config.Tests)
            {
                if (ct.IsCancellationRequested)
                {
                    OnLogMessage?.Invoke(this, "PRZERWANO przez użytkownika.");
                    isCancelled = true;
                    break;
                }

                currentIndex++;
                OnLogMessage?.Invoke(this, $"\n--- Test {currentIndex}/{config.Tests.Count}: [{test.Category}] {test.TestName} ---");

                // SANDBOX: Reset pamięci Agenta przed każdym testem
                AgentMemoryState.Variables.Clear();
                AgentMemoryState.Clear();

                // Wstrzyknięcie MockMemoryVariables
                if (test.MockMemoryVariables != null)
                {
                    foreach (var kvp in test.MockMemoryVariables)
                    {
                        AgentMemoryState.Variables[kvp.Key] = kvp.Value;
                        OnLogMessage?.Invoke(this, $"  [MOCK VAR] @{kvp.Key} = '{kvp.Value}'");
                    }
                }

                // Budowanie historii konwersacji
                var history = new List<ChatMessage>
                {
                    new ChatMessage { Role = "user", Content = test.UserPrompt }
                };
                test.RecordedToolCalls = new List<RecordedToolCall>();
                test.FailedRulesErrors = new List<string>();

                // ⏱️ START STOPERA
                var sw = Stopwatch.StartNew();

                // Wywołanie LLM w trybie Benchmark
                await _llmClient.SendMessageBenchmarkAsync(
                    history,
                    test.SimulatedCADResponses,
                    test.RecordedToolCalls,
                    doc,
                    ct: ct);

                // ⏱️ STOP STOPERA
                sw.Stop();
                test.ExecutionTimeMs = sw.ElapsedMilliseconds;
                totalTimeMs += test.ExecutionTimeMs;

                if (ct.IsCancellationRequested)
                {
                    isCancelled = true;
                    break;
                }

                // FAZA 2: Walidacja
                test.Passed = ValidateTest(test);
                if (test.Passed)
                {
                    passedCount++;
                    OnLogMessage?.Invoke(this, $"  ✓ ZALICZONY ({test.ExecutionTimeMs}ms)");
                }
                else
                {
                    OnLogMessage?.Invoke(this, $"  ✗ OBLANY ({test.ExecutionTimeMs}ms). Błędy:");
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

            // --- FAZA 3: Obliczenia Końcowe i Raportowanie ---
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

            SaveReports(config, jsonFilePath, saveErrors);

            OnLogMessage?.Invoke(this, $"\n=== ZAKOŃCZONO. Wynik: {config.RunMetadata.GlobalScore}% ({passedCount}/{currentIndex}) | Czas śr.: {config.RunMetadata.AverageExecutionTimeMs}ms ===");
            OnBenchmarkCompleted?.Invoke(this, new BenchmarkCompletedEventArgs
            {
                FinalConfig = config,
                PassedCount = passedCount,
                WasCancelled = isCancelled
            });

            return config;
        }

        // ==========================================
        // WALIDATOR (AUTO-SĘDZIA V2)
        // ==========================================
        private bool ValidateTest(BenchmarkTest test)
        {
            bool allPassed = true;

            foreach (var rule in test.ValidationRules)
            {
                bool rulePassed = true;
                string ruleError = rule.ErrorMessage ?? $"Reguła '{rule.RuleType}' nie przeszła.";

                try
                {
                    switch (rule.RuleType)
                    {
                        // --- Sprawdzenie, czy LLM w ogóle wywołał narzędzie ---
                        case "ToolCalled":
                            rulePassed = test.RecordedToolCalls.Any(c =>
                                string.Equals(c.ToolName, rule.TargetValue, StringComparison.OrdinalIgnoreCase));
                            break;

                        // --- Sprawdzenie wartości konkretnego argumentu JSON ---
                        case "ArgumentMatch":
                            // Szukamy ostatniego wywołania narzędzia, które faktycznie posiada szukany argument
                            var callForMatch = test.RecordedToolCalls
                                .LastOrDefault(c => c.Arguments != null && ResolveJsonPath(c.Arguments, rule.TargetArgument) != null);

                            if (callForMatch == null)
                            {
                                rulePassed = false;
                                ruleError = $"{ruleError} (Argument '{rule.TargetArgument}' nie został znaleziony w żadnym wywołaniu)";
                                break;
                            }

                            string actualValue = ResolveJsonPath(callForMatch.Arguments, rule.TargetArgument);
                            rulePassed = string.Equals(actualValue, rule.TargetValue, StringComparison.OrdinalIgnoreCase);
                            if (!rulePassed)
                                ruleError = $"{ruleError} (Znaleziono: '{actualValue}', Oczekiwano: '{rule.TargetValue}')";
                            break;

                        // --- Sprawdzenie kolejności wywołań narzędzi ---
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

                        // --- Weryfikacja poprawności formuły RPN w argumencie ---
                        case "EvaluateRPN_Argument":
                            var callForRpn = test.RecordedToolCalls
                                .FirstOrDefault(c => c.Arguments != null);

                            if (callForRpn == null)
                            {
                                rulePassed = false;
                                ruleError = "Brak zarejestrowanych wywołań narzędzi z argumentami.";
                                break;
                            }

                            string rpnFormula = ResolveJsonPath(callForRpn.Arguments, rule.TargetArgument);
                            if (string.IsNullOrEmpty(rpnFormula))
                            {
                                rulePassed = false;
                                ruleError = $"Nie znaleziono formuły RPN pod ścieżką '{rule.TargetArgument}'.";
                                break;
                            }

                            // Podstawianie MockData
                            if (rule.MockData != null)
                                foreach (var kvp in rule.MockData)
                                    rpnFormula = rpnFormula.Replace(kvp.Key, kvp.Value);

                            try
                            {
                                // V2 zarządza stosem per-Document — po prostu ewaluujemy wyrażenie
                                string rpnResult = RpnCalculator.Evaluate(rpnFormula);
                                if (rpnResult != rule.ExpectedOutput)
                                {
                                    rulePassed = false;
                                    ruleError = $"Błąd RPN. Oczekiwano '{rule.ExpectedOutput}', obliczono '{rpnResult}' (wzór: {rpnFormula})";
                                }
                            }
                            catch (Exception ex)
                            {
                                rulePassed = false;
                                ruleError = $"Silnik RPN odrzucił formułę: {ex.Message} (wzór: {rpnFormula})";
                            }
                            break;

                        default:
                            ruleError = $"Nieznany typ reguły: '{rule.RuleType}'.";
                            rulePassed = false;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // Walidator jest defensywny – nie wysadza aplikacji
                    rulePassed = false;
                    ruleError = $"Wyjątek podczas walidacji reguły '{rule.RuleType}': {ex.Message}";
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
        // HELPER: Resolver ścieżki JSON
        // ==========================================
        /// <summary>
        /// Rozwiązuje prostą ścieżkę do wartości w JObject (np. "Properties[0].PropertyName").
        /// Defensywny – zwraca null przy błędzie zamiast rzucać wyjątek.
        /// </summary>
        private string ResolveJsonPath(JObject obj, string path)
        {
            if (obj == null || string.IsNullOrEmpty(path)) return null;

            try
            {
                JToken current = obj;
                // Rozbijamy ścieżkę na segmenty (klucze i indeksy tablic)
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
                return current.ToString();
            }
            catch
            {
                return null;
            }
        }

        // ==========================================
        // RAPORTOWANIE
        // ==========================================
        private void SaveReports(BenchmarkConfig config, string sourceJsonPath, bool saveErrors)
        {
            try
            {
                string resultJson = JsonConvert.SerializeObject(config, Formatting.Indented);
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
                string rootDir = Path.GetDirectoryName(sourceJsonPath);
                string origName = Path.GetFileNameWithoutExtension(sourceJsonPath);

                string safeModel = string.Join("_", (config.RunMetadata.ModelName ?? "UnknownModel")
                    .Split(Path.GetInvalidFileNameChars()));
                string modelDir = Path.Combine(rootDir, safeModel);
                if (!Directory.Exists(modelDir)) Directory.CreateDirectory(modelDir);

                string fullPath = Path.Combine(modelDir, $"{origName}_{safeModel}_FULL_{timestamp}.json");
                File.WriteAllText(fullPath, resultJson, System.Text.Encoding.UTF8);
                OnLogMessage?.Invoke(this, $"Raport FULL zapisany: {fullPath}");

                if (saveErrors)
                {
                    var failed = config.Tests.Where(t => !t.Passed).ToList();
                    if (failed.Count > 0)
                    {
                        var errConfig = new BenchmarkConfig { RunMetadata = config.RunMetadata, Tests = failed };
                        string errJson = JsonConvert.SerializeObject(errConfig, Formatting.Indented);
                        string errPath = Path.Combine(modelDir, $"{origName}_{safeModel}_ERRORS_{timestamp}.json");
                        File.WriteAllText(errPath, errJson, System.Text.Encoding.UTF8);
                        OnLogMessage?.Invoke(this, $"Raport ERRORS zapisany: {errPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                OnLogMessage?.Invoke(this, $"Błąd zapisu raportu: {ex.Message}");
            }
        }
    }
}
