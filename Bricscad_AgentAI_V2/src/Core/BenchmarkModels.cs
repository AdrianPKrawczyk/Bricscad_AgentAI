using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Core
{
    // ==========================================
    // STRUKTURY DANYCH FORMATU TESTOWEGO V2
    // ==========================================

    /// <summary>
    /// Opcje pojedynczego uruchomienia benchmarku. Domyslnie zachowuje dotychczasowe dzialanie manualne.
    /// </summary>
    public class BenchmarkRunOptions
    {
        public string ProfileName { get; set; }
        public string PromptOverridePath { get; set; }
        public string OutputRoot { get; set; }
        public string RunMode { get; set; } = "manual";
        public string CandidateId { get; set; }
        public string JobId { get; set; }
        public string ProviderId { get; set; }
        public bool SaveToUserBenchmarkHistory { get; set; } = true;
        public bool SaveErrors { get; set; } = true;

        public static BenchmarkRunOptions Manual(string profileName, bool saveErrors = true)
        {
            return new BenchmarkRunOptions
            {
                ProfileName = profileName,
                RunMode = "manual",
                SaveToUserBenchmarkHistory = true,
                SaveErrors = saveErrors
            };
        }
    }

    /// <summary>
    /// Kontrakt joba dla laboratoryjnego workera benchmarkow.
    /// </summary>
    public class BenchmarkLabJob
    {
        public int SchemaVersion { get; set; } = 1;
        public string JobId { get; set; }
        public string RunMode { get; set; } = "optimizer_lab";
        public string BenchmarkPath { get; set; }
        public List<string> BenchmarkPaths { get; set; } = new List<string>();
        public string ProfileName { get; set; }
        public string ProviderId { get; set; }
        public string PromptOverridePath { get; set; }
        public string CandidateId { get; set; }
        public string OutputRoot { get; set; }
        public double? TargetScore { get; set; }
        public bool SaveToUserBenchmarkHistory { get; set; } = false;
    }

    /// <summary>
    /// Korzeń pliku JSON z zestawem testów benchmarkowych.
    /// </summary>
    public class BenchmarkConfig
    {
        public RunMetadata RunMetadata { get; set; } = new RunMetadata();
        public List<BenchmarkTest> Tests { get; set; } = new List<BenchmarkTest>();
    }

    public class BenchmarkQueueItem
    {
        public string FilePath { get; set; }
        public string DisplayName { get; set; }
        public string BenchmarkName { get; set; }
        public int TestCount { get; set; }
        public string Status { get; set; } = "Oczekuje";
        public string CleanConfigJson { get; set; }
        public BenchmarkConfig LoadedConfig { get; set; }
        public BenchmarkConfig FinalConfig { get; set; }
        public double? Score { get; set; }
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
        public long DurationMs { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class BenchmarkBatchSummary
    {
        public string BatchName { get; set; }
        public string ProfileName { get; set; }
        public string ProviderName { get; set; }
        public string ModelName { get; set; }
        public string RunDate { get; set; }
        public int TotalBenchmarks { get; set; }
        public int CompletedBenchmarks { get; set; }
        public int CancelledBenchmarks { get; set; }
        public int FailedBenchmarks { get; set; }
        public int PassedTests { get; set; }
        public int TotalTests { get; set; }
        public double WeightedGlobalScore { get; set; }
        public double AverageBenchmarkScore { get; set; }
        public long TotalExecutionTimeMs { get; set; }
        public List<BenchmarkBatchSummaryItem> Benchmarks { get; set; } = new List<BenchmarkBatchSummaryItem>();
    }

    public class BenchmarkBatchSummaryItem
    {
        public string FilePath { get; set; }
        public string DisplayName { get; set; }
        public string BenchmarkName { get; set; }
        public string Status { get; set; }
        public int TestCount { get; set; }
        public int PassedCount { get; set; }
        public int FailedCount { get; set; }
        public double? Score { get; set; }
        public long DurationMs { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Metadane przebiegu benchmarku (wypełniane przez silnik po zakończeniu).
    /// </summary>
    public class RunMetadata
    {
        public string BenchmarkName { get; set; }
        public string ModelName { get; set; }
        public string ProviderName { get; set; }
        public string ProviderEndpoint { get; set; }

        // v2.28.83: Unikalne ID providera (z llm_providers.json) - rozroznia komputery z tym samym modelem.
        // Pusty/null = kompatybilnosc wsteczna (folder budowany tylko z ModelName).
        public Guid? ProviderId { get; set; }
        public string ProfileName { get; set; }
        public double Temperature { get; set; }
        public int MaxTokens { get; set; }
        public double TopP { get; set; }
        public int TopK { get; set; }
        public double MinP { get; set; }
        public double RepetitionPenalty { get; set; }
        public string ReasoningEffort { get; set; }
        public bool AutoLoadModel { get; set; }
        public string GpuOffload { get; set; }
        public int LoadContextLength { get; set; }
        public int TtlSeconds { get; set; }
        public bool FlashAttention { get; set; }
        public bool OffloadKvCache { get; set; }
        public int MaxContextTokens { get; set; }
        public int ContextCompressionThreshold { get; set; }
        public string RunDate { get; set; }
        public string Comment { get; set; }
        public string RunMode { get; set; }
        public string PromptOverridePath { get; set; }
        public string OutputRoot { get; set; }
        public string CandidateId { get; set; }
        public string JobId { get; set; }
        public bool SaveToUserBenchmarkHistory { get; set; } = true;

        // Wyniki (wypełniane przez silnik)
        public double GlobalScore { get; set; }
        public Dictionary<string, double> CategoriesScores { get; set; } = new Dictionary<string, double>();
        public double AverageExecutionTimeMs { get; set; }
    }

    /// <summary>
    /// Definicja pojedynczego testu benchmarkowego w formacie Tool Calling V2.
    /// </summary>
    public class BenchmarkTest
    {
        // --- Dane wejściowe (definicja testu) ---
        public int Id { get; set; }
        public string Category { get; set; }
        public int Difficulty { get; set; }
        public string TestName { get; set; }
        public string Description { get; set; }
        public string UserPrompt { get; set; }

        /// <summary>
        /// Zmienne wstrzykiwane do AgentMemoryState.Variables przed startem testu.
        /// Pozwala symulować środowisko (np. wcześniej wykonane kroki).
        /// </summary>
        public Dictionary<string, string> MockMemoryVariables { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Słownik mockowanych odpowiedzi CAD: klucz=NazwaNarzędzia, wartość=tekst odpowiedzi.
        /// Jeśli narzędzie nie ma wpisu, silnik zwróci domyślny komunikat "[MOCK] Narzędzie {name} wywołane.".
        /// </summary>
        public Dictionary<string, string> SimulatedCADResponses { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Reguły walidacyjne, które muszą być spełnione, aby test przeszedł.
        /// </summary>
        public List<ValidationRule> ValidationRules { get; set; } = new List<ValidationRule>();

        // --- Wyniki (wypełniane przez silnik) ---
        public bool Passed { get; set; }
        public List<RecordedToolCall> RecordedToolCalls { get; set; } = new List<RecordedToolCall>();

        public List<string> FailedRulesErrors { get; set; } = new List<string>();
        public long ExecutionTimeMs { get; set; }
    }

    /// <summary>
    /// Zapis wywołania narzędzia przez LLM podczas testu (materiał dowodowy dla walidatora).
    /// </summary>
    public class RecordedToolCall
    {
        public string ToolName { get; set; }
        public JObject Arguments { get; set; }
    }

    /// <summary>
    /// Reguła walidacji wyniku testu.
    /// </summary>
    public class ValidationRule
    {
        /// <summary>
        /// Typ reguły: ToolCalled | ArgumentMatch | SequenceMatch | EvaluateRPN_Argument
        /// </summary>
        public string RuleType { get; set; }

        /// <summary>
        /// Nazwa narzędzia (ToolCalled, SequenceMatch) lub oczekiwana wartość (ArgumentMatch).
        /// </summary>
        public string TargetValue { get; set; }

        /// <summary>
        /// Ścieżka do argumentu JSON np. "Properties[0].PropertyName" (dla ArgumentMatch, EvaluateRPN_Argument).
        /// </summary>
        public string TargetArgument { get; set; }

        /// <summary>
        /// Dane do podstawienia w formule RPN (dla EvaluateRPN_Argument).
        /// </summary>
        public Dictionary<string, string> MockData { get; set; }

        /// <summary>
        /// Oczekiwany wynik po wykonaniu formuły RPN (dla EvaluateRPN_Argument).
        /// </summary>
        public string ExpectedOutput { get; set; }

        /// <summary>
        /// Komunikat błędu dołączany do raportu, gdy reguła nie przejdzie.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}
