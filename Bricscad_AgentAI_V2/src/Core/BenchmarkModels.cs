using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Core
{
    // ==========================================
    // STRUKTURY DANYCH FORMATU TESTOWEGO V2
    // ==========================================

    /// <summary>
    /// Korzeń pliku JSON z zestawem testów benchmarkowych.
    /// </summary>
    public class BenchmarkConfig
    {
        public RunMetadata RunMetadata { get; set; } = new RunMetadata();
        public List<BenchmarkTest> Tests { get; set; } = new List<BenchmarkTest>();
    }

    /// <summary>
    /// Metadane przebiegu benchmarku (wypełniane przez silnik po zakończeniu).
    /// </summary>
    public class RunMetadata
    {
        public string ModelName { get; set; }
        public string RunDate { get; set; }
        public string Comment { get; set; }

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
