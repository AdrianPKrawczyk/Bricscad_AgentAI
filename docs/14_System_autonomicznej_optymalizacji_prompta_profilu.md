# System Autonomicznej Optymalizacji Promptu Profilu

> **Wersja dokumentu**: 1.0
> **Data**: 2026-06-10
> **Status**: Wytyczne do implementacji (handoff dla agenta kodującego)
> **Cel**: Aplikacja terminalowa .NET 8/9 (niezależna od BricsCAD) do autonomicznej optymalizacji promptów profilowych z wykorzystaniem benchmarków i agentów LLM.

---

## 1. Kontekst i motywacja

### 1.1 Obecny stan

Projekt `Bricscad_AgentAI_V2` zawiera działający silnik benchmarków:
- `AutoBenchmarkEngine.cs` (670 linii) - logika benchmarku
- `LLMClient.cs` (880 linii) - komunikacja z LLM + Tool Calling
- `IToolV2.cs` (32 linie) - interfejs narzędzi
- `ToolConfigManager.cs` (629 linii) - profile + prompty
- 10 benchmarków (`Benchmark_01` do `Benchmark_09_Extended`)
- 8 profili promptów (`system_prompt_*.txt`)

**Problemy**:
1. Benchmarki działają **tylko wewnątrz BricsCAD** (zależność od `Bricscad.ApplicationServices.Document`)
2. Wymagana interakcja użytkownika (kliknięcie "Start" w GUI)
3. Brak możliwości uruchomienia w pętli bez nadzoru
4. Brak wsparcia dla agentów LLM (opencode, hermes) jako optymalizatorów

### 1.2 Oczekiwany efekt

Aplikacja terminalowa `.NET 8/9` (kompilowalna w linii komend), która:
- Wczytuje istniejące benchmarki (format JSON - **identyczny z obecnym**)
- Ładuje profile promptów (pliki `.txt` - **identyczne z obecnymi**)
- Komunikuje się z LLM przez OpenAI-compatible HTTP API (LM Studio, OpenAI, OpenRouter)
- Uruchamia benchmarki w pętli **bez BricsCAD**
- Pozwala agentowi LLM (opencode, hermes) optymalizować prompt w pętli do osiągnięcia target %
- Zapisuje raporty w formacie kompatybilnym z obecnym (do porównywania ewolucji)

### 1.3 Kluczowa obserwacja

W benchmarkach **narzędzia CAD nigdy nie są realnie wywoływane**. Zamiast tego, z góry przygotowane odpowiedzi (`SimulatedCADResponses`) są wstrzykiwane jako `tool` messages. Dlatego benchmark **nie potrzebuje BricsCAD** - potrzebuje tylko:
1. LLM, który produkuje Tool Calls
2. Mockowanej odpowiedzi (z JSON-a benchmarku)
3. Walidatora (czysta logika - działa wszędzie)

---

## 2. Architektura docelowa

### 2.1 Struktura katalogów

```
D:\GitHub\Bricscad_AgentAI\
├── Bricscad_AgentAI_V2\          # ISTNIEJĄCY projekt .NET 4.8 (BricsCAD)
│   ├── src\Core\                 # Klasy do REFAKTORINGU
│   │   ├── BenchmarkModels.cs    # BenchmarkConfig, RunMetadata, BenchmarkTest, RecordedToolCall → PRZENIEŚ
│   │   ├── AutoBenchmarkEngine.cs # ValidateTest(), ValuesMatch(), ResolveJsonPath(), TokenToInvariantString() → PRZENIEŚ
│   │   └── LLMClient.cs          # SendMessageBenchmarkAsync (bez CAD) → PRZENIEŚ
│   ├── tests\                    # Benchmarki JSON → UŻYJ BEZ ZMIAN
│   │   ├── Benchmark_01_CreateObject.json
│   │   ├── ...
│   │   └── Benchmark_09_InsertBlock_Extended.json
│   ├── resources\prompts\        # Profile promptów → UŻYJ BEZ ZMIAN
│   │   ├── system_prompt_blocks.txt
│   │   ├── ...
│   │   └── system_prompt_supervisor.txt
│   └── llm_providers.json        # Konfiguracja LLM → UŻYJ BEZ ZMIAN
│
├── BricscadPromptBench\          # NOWY projekt .NET 8/9 (Console)
│   ├── BricscadPromptBench.csproj
│   ├── Program.cs                # Punkt wejścia (CLI parser)
│   ├── Cli\                      # Komendy CLI
│   │   ├── RunCommand.cs
│   │   ├── EvalCommand.cs
│   │   ├── DiffCommand.cs
│   │   ├── OptimizeCommand.cs
│   │   └── ValidateCommand.cs
│   ├── Core\                     # Przeniesione z V2 (refaktoryzowane)
│   │   ├── Models\
│   │   │   ├── BenchmarkConfig.cs
│   │   │   ├── RunMetadata.cs
│   │   │   ├── BenchmarkTest.cs
│   │   │   └── RecordedToolCall.cs
│   │   ├── Validation\
│   │   │   ├── RuleType.cs        # enum: ToolCalled, ArgumentMatch, AnyArgumentMatch, AnyOfArgumentMatch, AnyOfArgumentMatchOrAbsent, SequenceMatch, ToolCallCountMax, EvaluateMath_Argument
│   │   │   ├── ValidationRule.cs
│   │   │   ├── RuleEvaluator.cs   # główna logika walidacji (wycięta z AutoBenchmarkEngine)
│   │   │   ├── ValueMatcher.cs    # ValuesMatch, TokenToInvariantString, ResolveJsonPath
│   │   │   └── AnyOfArgumentMatchOrAbsent.cs
│   │   ├── Benchmark\
│   │   │   ├── BenchmarkRunner.cs  # orkiestracja 1 benchmarku
│   │   │   ├── LLMClient.cs       # OpenAI-compatible HTTP client (bez BricsCAD)
│   │   │   ├── ToolDefinition.cs  # schemat narzędzia (uproszczony - tylko nazwa + parametry)
│   │   │   └── ChatMessage.cs
│   │   ├── Profile\
│   │   │   ├── ProfileManager.cs  # wczytuje profile + prompty z plików .txt
│   │   │   ├── PromptLoader.cs
│   │   │   └── LLMConfigManager.cs # wczytuje llm_providers.json
│   │   └── Optimizer\
│   │       ├── PromptOptimizer.cs # pętla optymalizacji
│   │       ├── IterationLog.cs    # log każdej iteracji
│   │       └── ResultStore.cs     # persystencja wyników
│   ├── Reports\                  # Generowane raporty (identyczne z V2)
│   │   ├── Full\
│   │   └── Errors\
│   └── tests\                    # Testy jednostkowe projektu
│       ├── Core\
│       │   ├── Validation\
│       │   └── Optimizer\
│       └── Integration\
│
├── docs\                         # Dokumentacja (ten folder)
│   ├── 14_System_autonomicznej_optymalizacji_prompta_profilu.md
│   └── ...
│
└── .agents\skills\               # Skille dla agentów LLM
    └── bench-optimizer\          # NOWY skill - dokumentacja jak używać CLI
        └── SKILL.md
```

### 2.2 Podział odpowiedzialności

| Komponent | Odpowiedzialność | Zależności |
|-----------|------------------|------------|
| **Cli** | Parsowanie argumentów, wywoływanie komend | Benchmark, Profile, Optimizer |
| **BenchmarkRunner** | 1 przebieg benchmarku: prompt → LLM → narzędzia (mockowane) → walidacja → raport | LLMClient, RuleEvaluator, ProfileManager |
| **LLMClient** | HTTP do OpenAI-compatible API, parsowanie tool_calls, pętla ReAct (max 10 iter) | HttpClient |
| **RuleEvaluator** | Wykonuje reguły walidacyjne, zwraca PASS/FAIL + błędy | ValidationRule, ValueMatcher |
| **ValueMatcher** | Porównuje wartości JSON (string, number, JSON DeepEquals, InvariantCulture) | Newtonsoft.Json |
| **ProfileManager** | Wczytuje profile + prompty z `resources/prompts/*.txt` | System.IO |
| **LLMConfigManager** | Wczytuje `llm_providers.json` z aktywnym providerem | System.IO, Newtonsoft.Json |
| **PromptOptimizer** | Pętla: uruchom benchmark → oceń → zapisz feedback → exit jeśli target | BenchmarkRunner, ResultStore |
| **ResultStore** | Zapisuje raporty FULL i ERRORS w formacie V2 | System.IO, Newtonsoft.Json |

### 2.3 Przepływ danych (krok po kroku)

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Użytkownik: dotnet run --project BricscadPromptBench \   │
│    optimize --benchmark Benchmark_09 --profile CadBlocks \ │
│    --target 90 --max-iter 20 --provider gemma-4-12b       │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ 2. Cli: parsuje argumenty, tworzy OptimizeCommand           │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ 3. PromptOptimizer.Initialize():                           │
│    - ProfileManager.LoadProfile("CadBlocks")                │
│    - wczytuje system_prompt_blocks.txt                       │
│    - LLMConfigManager.Load()                                 │
│    - wczytuje llm_providers.json → provider gemma-4-12b      │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ 4. Pętla (max 20 iteracji):                                 │
│    a) BenchmarkRunner.Run(benchmarkJson, profile, provider) │
│    b) Walidacja (RuleEvaluator)                              │
│    c) Zapis raportu (ResultStore)                            │
│    d) Sprawdź score vs target                                │
│       - jeśli ≥ target → SUCCESS, exit                      │
│       - jeśli iter == max → FAIL, exit                       │
│       - else: czekaj na decyzję agenta LLM                   │
└─────────────────────────────────────────────────────────────┘
                            ↓
┌─────────────────────────────────────────────────────────────┐
│ 5. Agent LLM (opencode/hermes) wczytuje raport             │
│    i decyduje: modyfikacja promptu, exit, inne podejście  │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. Refaktoring - co przenieść z V2

### 3.1 Pliki do skopiowania bez zmian (1:1)

Te pliki **nie wymagają modyfikacji** - kopiujesz je 1:1 do nowego projektu:

| Plik źródłowy (V2) | Plik docelowy (Bench) | Powód |
|---------------------|----------------------|-------|
| `src/Core/BenchmarkModels.cs` | `Core/Models/BenchmarkModels.cs` | Czyste modele danych, brak zależności od BricsCAD |
| `src/Models/ChatMessage.cs` | `Core/Benchmark/ChatMessage.cs` | Prosta klasa wiadomości |
| `src/Models/ToolDefinition.cs` | `Core/Benchmark/ToolDefinition.cs` | Schemat narzędzia (LLM contract) |
| `src/Models/RecordedToolCall.cs` | `Core/Models/RecordedToolCall.cs` | Rejestrowanie wywołań |

**Weryfikacja** - te pliki NIE powinny importować `Bricscad.*`:
- Otwórz każdy plik i sprawdź `using Bricscad.*`
- Jeśli są takie importy → **nie kopiuj**, zostaw w V2

### 3.2 Pliki do wycięcia (częściowa kopia)

#### 3.2.1 `AutoBenchmarkEngine.cs` (670 linii)

**Co wyciąć** (wyciągnij jako osobne pliki):

| Sekcja | Linie (orientacyjnie) | Nowa klasa |
|--------|------------------------|------------|
| `TokenToInvariantString()` | ~534-550 | `ValueMatcher.cs` |
| `ValuesMatch()` | ~552-571 | `ValueMatcher.cs` |
| `TryParseJsonToken()` | ~573-589 | `ValueMatcher.cs` |
| `ResolveJsonPath()` | ~501-530 | `ValueMatcher.cs` |
| `ValidateTest()` switch | ~312-410 | `RuleEvaluator.cs` |
| `EvaluateMath_Argument` case | ~430-475 | `RuleEvaluator.cs` |
| `BenchmarkModels` (częściowa) | - | `BenchmarkConfig.cs` (w Models) |

**Czego NIE kopiować**:
- `RunPreflightCheck()` - zależna od `IToolV2` i reflection
- `RunBenchmarkAsync()` - wymaga `Document` (CAD)
- Cała klasa `AutoBenchmarkEngine` jako całość
- Eventy UI (`OnTestFinished`, `OnLogMessage`)

#### 3.2.2 `LLMClient.cs` (880 linii)

**Co wyciąć**:

| Sekcja | Nowa klasa |
|--------|------------|
| `SendMessageBenchmarkAsync()` (linie 457-700) | `LLMClient.RunBenchmarkAsync()` w `Core/Benchmark/` |
| Cała logika HTTP (budowanie payload, parsowanie tool_calls) | `LLMClient.SendRequestAsync()` |
| Tool schema loading | Osobna metoda `LLMClient.GetToolsForProfileAsync()` |

**Kluczowa modyfikacja** - `SendMessageBenchmarkAsync` w oryginale ma parametr `Document doc`. **Usuń ten parametr**. Narzędzia nie są realnie wywoływane - odpowiedzi są z `SimulatedCADResponses`.

**Co zostawić w V2**: cały `LLMClient` (zostaje nietknięty).

#### 3.2.3 `ToolConfigManager.cs` (629 linii)

**Co wyciąć**:

| Sekcja | Nowa klasa |
|--------|------------|
| `LoadEffectivePromptForProfile()` | `ProfileManager.LoadProfilePrompt()` |
| `GetToolsPayloadForProfile()` | `ProfileManager.GetProfileTools()` - **ALE**: profile w V2 używają tagów narzędzi (`#all`, `ListBlocks`, itp.) - w nowym Bench **uproszczona wersja** (tylko lista nazw narzędzi w JSON) |
| `GenerateDefaultConfig()` | `LLMConfigManager.GenerateDefaultConfig()` |

**Co uprościć**:
- Profile w V2 używają mechanizmu **tag-based tool selection**. Nowy Bench użyje **prostszej listy** nazw narzędzi w `llm_providers.json` lub dedykowanego `profiles.json`.

### 3.3 Nowe klasy do utworzenia

#### 3.3.1 `RuleType.cs` (enum)

```csharp
namespace BricscadPromptBench.Core.Validation
{
    public enum RuleType
    {
        ToolCalled,
        ArgumentMatch,
        AnyArgumentMatch,
        AnyOfArgumentMatch,
        AnyOfArgumentMatchOrAbsent, // NOWY w v2.28.54
        SequenceMatch,
        ToolCallCountMax,
        EvaluateMath_Argument
    }
}
```

#### 3.3.2 `ValidationRule.cs`

```csharp
namespace BricscadPromptBench.Core.Validation
{
    public class ValidationRule
    {
        public RuleType RuleType { get; set; }
        public string TargetValue { get; set; }
        public string TargetArgument { get; set; }
        public string ExpectedOutput { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, string> MockData { get; set; } // dla EvaluateMath
    }
}
```

#### 3.3.3 `RuleEvaluator.cs`

Główna klasa walidacji. Wyciągnij logikę z `ValidateTest()` w `AutoBenchmarkEngine.cs:282-409`.

**Metody publiczne**:
- `bool Validate(BenchmarkTest test)` - zwraca true/false dla całego testu
- `IReadOnlyList<string> GetErrors()` - lista błędów

**Wewnętrzne**:
- `bool EvaluateRule(ValidationRule rule, BenchmarkTest test, List<RecordedToolCall> calls)`
- Helpery dla każdego `RuleType`

**Zachowanie** (identyczne z V2):
- `ToolCalled` - czy `ToolName` istnieje w calls
- `ArgumentMatch` - ostatni call z danym argumentem ma wartość = TargetValue
- `AnyArgumentMatch` - jakikolwiek call z danym argumentem pasuje
- `AnyOfArgumentMatch` - wiele wariantów `||`
- `AnyOfArgumentMatchOrAbsent` - argument opcjonalny (nowy w v2.28.54)
- `SequenceMatch` - narzędzia w podanej kolejności
- `ToolCallCountMax` - max wywołań narzędzia
- `EvaluateMath_Argument` - RPN math evaluation (opcjonalny - zależnie od potrzeb)

#### 3.3.4 `ValueMatcher.cs`

Wyciągnij `ValuesMatch`, `TokenToInvariantString`, `ResolveJsonPath`, `TryParseJsonToken` z `AutoBenchmarkEngine.cs`.

**Nowa metoda w `ValueMatcher.cs` (oprócz istniejących)**:
- `IReadOnlyList<string> SplitVariants(string targetValue)` - dzieli string po `||` (separator wariantów)

#### 3.3.5 `BenchmarkRunner.cs`

```csharp
namespace BricscadPromptBench.Core.Benchmark
{
    public class BenchmarkRunner
    {
        private readonly LLMClient _llmClient;
        private readonly ProfileManager _profileManager;
        private readonly RuleEvaluator _ruleEvaluator;
        private readonly ResultStore _resultStore;
        
        public async Task<BenchmarkReport> RunAsync(
            string benchmarkPath, 
            string profileName, 
            string providerName,
            CancellationToken ct = default)
        {
            // 1. Wczytaj benchmark JSON
            // 2. Wczytaj prompt profilu
            // 3. Wykonaj każdy test (LLMClient.SendBenchmarkAsync)
            // 4. Walidacja (RuleEvaluator)
            // 5. Zapisz raport (ResultStore)
            // 6. Zwróć BenchmarkReport ze statystykami
        }
    }
}
```

**Symulacja narzędzi** - kluczowa logika:
```csharp
// W pętli ReAct (z SendMessageBenchmarkAsync):
foreach (var toolCall in response.ToolCalls) {
    var toolName = toolCall.Function.Name;
    
    // Rejestruj wywołanie
    test.RecordedToolCalls.Add(new RecordedToolCall {
        ToolName = toolName,
        Arguments = JObject.Parse(toolCall.Function.Arguments)
    });
    
    // ZNAJDŹ MOCKOWANĄ ODPOWIEDŹ (zamiast realnego CAD)
    string mockResponse = test.SimulatedCADResponses.ContainsKey(toolName)
        ? test.SimulatedCADResponses[toolName]
        : $"WYNIK: Narzędzie {toolName} wykonane (brak mocka).";
    
    // Wstrzyknij jako tool message (kontynuacja pętli ReAct)
    history.Add(new ChatMessage { Role = "tool", Content = mockResponse });
}
```

#### 3.3.6 `LLMClient.cs` (nowy, uproszczony)

```csharp
namespace BricscadPromptBench.Core.Benchmark
{
    public class LLMClient
    {
        private readonly HttpClient _httpClient;
        private readonly LLMConfig _config;
        
        public LLMClient(LLMConfig config)
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            _config = config;
        }
        
        // ReAct pętla dla benchmarku (bez Document)
        public async Task<AgentRunResult> RunBenchmarkAsync(
            BenchmarkTest test,
            string systemPrompt,
            List<ToolDefinition> tools,
            int maxIterations = 10,
            CancellationToken ct = default)
        {
            // Wycięta logika z LLMClient.SendMessageBenchmarkAsync
            // BEZ parametru Document doc
        }
    }
}
```

#### 3.3.7 `ProfileManager.cs`

```csharp
namespace BricscadPromptBench.Core.Profile
{
    public class ProfileManager
    {
        private readonly string _promptsDir;
        
        public ProfileManager(string promptsDir) {
            _promptsDir = promptsDir;
        }
        
        public List<string> ListProfiles() {
            // Zwraca ["blocks", "cad", "geometry", "math", "metadata", "notes", "supervisor", "auditor"]
            // Parsuje pliki system_prompt_*.txt
        }
        
        public string LoadPrompt(string profileName) {
            // Wczytuje system_prompt_<profileName>.txt
            // UWAGA: profileName to nazwa BEZ "system_prompt_" prefix i BEZ ".txt"
            //   np. "blocks" → "system_prompt_blocks.txt"
        }
        
        public List<ToolDefinition> GetToolsForProfile(string profileName) {
            // W V2 używa tag-based. W Bench uproszczona wersja:
            //   - Lista nazw narzędzi z benchmarków (lub hardcoded dla CadBlocksProfile)
            //   - Albo: ładowane z `tools_<profile>.json`
        }
    }
}
```

#### 3.3.8 `LLMConfigManager.cs` (nowy, uproszczony)

```csharp
namespace BricscadPromptBench.Core.Profile
{
    public static class LLMConfigManager
    {
        public static LLMConfig Load(string configPath) { ... }
        public static LLMConfig GenerateDefault() { ... }
        public static LLMConfig GetProvider(string name) { ... }
    }
}
```

#### 3.3.9 `PromptOptimizer.cs` - **główna logika optymalizacji**

```csharp
namespace BricscadPromptBench.Core.Optimizer
{
    public class PromptOptimizer
    {
        private readonly BenchmarkRunner _runner;
        private readonly ProfileManager _profileManager;
        private readonly string _promptsDir;
        
        public async Task<OptimizationResult> OptimizeAsync(
            string benchmarkPath,
            string profileName,
            double targetScore,
            int maxIterations,
            string providerName,
            CancellationToken ct = default)
        {
            var log = new List<IterationLog>();
            string currentPrompt = _profileManager.LoadPrompt(profileName);
            
            for (int i = 1; i <= maxIterations; i++) {
                Console.WriteLine($"=== Iteracja {i}/{maxIterations} ===");
                
                // 1. Zapisz prompt do pliku (backup)
                _profileManager.SavePrompt(profileName, currentPrompt);
                
                // 2. Uruchom benchmark
                var report = await _runner.RunAsync(
                    benchmarkPath, profileName, providerName, ct);
                
                // 3. Loguj wynik
                var iter = new IterationLog {
                    Iteration = i,
                    Score = report.GlobalScore,
                    PromptSnapshot = currentPrompt,
                    ReportPath = report.FullPath
                };
                log.Add(iter);
                Console.WriteLine($"  Score: {report.GlobalScore}% (target: {targetScore}%)");
                
                // 4. Sprawdź cel
                if (report.GlobalScore >= targetScore) {
                    return new OptimizationResult {
                        Success = true,
                        Iterations = i,
                        FinalScore = report.GlobalScore,
                        FinalPrompt = currentPrompt,
                        Log = log
                    };
                }
                
                if (i == maxIterations) break;
                
                // 5. Czekaj na decyzję agenta LLM (NIE automatyczna)
                //    - PromptOptimizer nie modyfikuje promptu sam
                //    - Wypisuje sugestie na podstawie FailedRulesErrors
                //    - Agent LLM (opencode/hermes) edytuje plik system_prompt_*.txt
                //    - Agent uruchamia komendę `optimize` ponownie
                //    - Kolejna iteracja wczyta zmieniony prompt
            }
            
            return new OptimizationResult {
                Success = false,
                Iterations = maxIterations,
                FinalScore = log.Last().Score,
                FinalPrompt = currentPrompt,
                Log = log
            };
        }
    }
}
```

**WAŻNE**: `PromptOptimizer` **nie modyfikuje promptu automatycznie**. To agent LLM (opencode/hermes) podejmuje decyzje co zmienić w pliku `.txt`. Aplikacja tylko:
- Wczytuje prompt
- Uruchamia benchmark
- Raportuje wynik + błędy
- Czeka na następną iterację (gdy agent LLM uruchomi komendę ponownie)

#### 3.3.10 `ResultStore.cs`

```csharp
namespace BricscadPromptBench.Core.Optimizer
{
    public class ResultStore
    {
        private readonly string _reportsDir;
        
        public ResultStore(string reportsDir) {
            _reportsDir = reportsDir;
        }
        
        public string SaveFullReport(BenchmarkConfig config) {
            // Generuje ścieżkę: reports/<model_dir>/Benchmark_NN_<Model>_FULL_<timestamp>.json
            // Zwraca pełną ścieżkę
        }
        
        public string SaveErrorsReport(BenchmarkConfig config) {
            // Generuje raport ERRORS (tylko testy z FailedRulesErrors.Count > 0)
        }
    }
}
```

---

## 4. CLI - komendy

### 4.1 Główna struktura

```
dotnet run --project BricscadPromptBench [global-options] <command> [command-options]
```

### 4.2 Globalne opcje

| Opcja | Opis | Domyślnie |
|-------|------|-----------|
| `--config <path>` | Ścieżka do `llm_providers.json` | `<Bench>/llm_providers.json` |
| `--prompts <path>` | Katalog z plikami `system_prompt_*.txt` | `<V2>/resources/prompts/` |
| `--benchmarks <path>` | Katalog z plikami `Benchmark_*.json` | `<V2>/tests/` |
| `--reports <path>` | Katalog na raporty | `<Bench>/Reports/` |
| `--verbose` / `-v` | Szczegółowe logi | false |
| `--json` | Output w formacie JSON (dla agenta LLM) | false |

### 4.3 Komendy

#### 4.3.1 `run` - uruchom benchmark jeden raz

```bash
dotnet run --project BricscadPromptBench run \
    --benchmark Benchmark_09_InsertBlock_Extended.json \
    --profile blocks \
    --provider gemma-4-12b-qat
```

**Output (tekst)**:
```
=== START BENCHMARKU ===
Benchmark: Benchmark_09_InsertBlock_Extended.json
Profile: blocks (system_prompt_blocks.txt, 98 linii)
Provider: gemma-4-12b-qat (LM Studio, temperature=0.2)
Tests: 25
[1/25] Test 1: ListBlocks_NoArgs (D1) - PASS (1500ms)
[2/25] Test 2: ListBlocks_EmptyDrawing (D1) - PASS (1200ms)
...
[25/25] Test 25: InsertBlock_ListBlocksForeach_NoAtSign (D5) - FAIL (3500ms)
  ERR: Foreach.TargetVariable='All' (BEZ znaku @). (Argument 'TargetVariable' nie zostal znaleziony w zadnym wywolaniu)

=== PODSUMOWANIE ===
Score: 84% (21/25)
Avg: 2534ms
Total: 63350ms
Per Category:
  ListBlocksBasic: 100% (3/3)
  ListBlocksAdvanced: 100% (5/5)
  ...
Report: Reports/gemma-4-12b-qat/Benchmark_09_InsertBlock_Extended_gemma-4-12b-qat_FULL_20260610_1530.json
```

**Output (--json)**:
```json
{
  "success": true,
  "exitCode": 0,
  "benchmark": "Benchmark_09_InsertBlock_Extended.json",
  "profile": "blocks",
  "provider": "gemma-4-12b-qat",
  "globalScore": 84.0,
  "passed": 21,
  "total": 25,
  "averageMs": 2534,
  "totalMs": 63350,
  "categories": {
    "ListBlocksBasic": {"passed": 3, "total": 3, "pct": 100.0},
    ...
  },
  "reportPath": "Reports/gemma-4-12b-qat/Benchmark_09_..._FULL_20260610_1530.json"
}
```

**Exit codes**:
- `0` - benchmark PASS (score >= 50% lub wszystkie 5+ testów)
- `1` - benchmark FAIL (score < 50%)
- `2` - błąd techniczny (brak benchmarku, brak profilu, timeout LLM)
- `3` - niepoprawna argumentacja CLI

#### 4.3.2 `eval` - oceń prompt bez uruchamiania LLM (walidacja statyczna)

```bash
dotnet run --project BricscadPromptBench eval \
    --benchmark Benchmark_09.json \
    --profile blocks
```

**Cel**: Sprawdzenie czy prompt profilu ma wymagane sekcje (np. "Zasady dla ListBlocks" dla CadBlocksProfile). Używane przez agenta LLM **przed** uruchomieniem benchmarku.

**Output**:
```
=== EVALUATION ===
Profile: blocks
Length: 98 lines, 14564 chars
Has Section "Zasady": YES
Has Section "Zasady dla EditBlock": YES
Has Section "Zasady dla CreateBlock": YES
Has Section "Zasady dla InsertBlock": YES
Has Section "Zasady dla ListBlocks": YES
Has Section "Zasady kanoniczne dla Foreach": YES
Score: 100% (wszystkie sekcje obecne)
```

#### 4.3.3 `diff` - porównanie dwóch promptów

```bash
dotnet run --project BricscadPromptBench diff \
    --before Reports/iter1/prompt.txt \
    --after Reports/iter2/prompt.txt
```

**Output** (unified diff):
```diff
--- before
+++ after
@@ -100,6 +100,8 @@
 Zasady kanoniczne dla Foreach w profilu blokow:
 - W `Foreach + InsertBlock` z `Items=[A1,B2,C3]`: `Action` MUSI miec `BlockName={item}`.
+- W `Foreach + InsertBlock` z `Items` jako MText: uzyj `Value={item}`.
+- W `Foreach + EditAttributes` z `FilterTag`: musi byc stabilny identyfikator (np. ID).
```

**Użycie**: agent LLM może zobaczyć co zmienił.

#### 4.3.4 `optimize` - uruchom benchmark i podaj rekomendacje

```bash
dotnet run --project BricscadPromptBench optimize \
    --benchmark Benchmark_09_InsertBlock_Extended.json \
    --profile blocks \
    --provider gemma-4-12b-qat \
    --target 90 \
    --max-iter 20
```

**Output (iteracja 1)**:
```
=== OPTIMIZATION ITERATION 1/20 ===
Target: 90%
Current prompt: system_prompt_blocks.txt (98 lines)

Running benchmark...
[1/25] Test 1: PASS (1500ms)
...
[25/25] Test 25: FAIL

Score: 84% (21/25)
Target: 90%
Gap: 6pp

=== FAILED TESTS (4) ===
Test 6 (D4) ListBlocks_ForeachSaveAs_NoAtSign - FAIL
  ERR: Brak Foreach
Test 14 (D3) InsertBlock_AttributeName_WithItem - FAIL
  ERR: Model powinien uzyc Foreach
Test 15 (D4) InsertBlock_MultipleAttributes_Foreach - FAIL
  ERR: Model powinien uzyc Foreach
Test 20 (D5) InsertBlock_FullPipeline_AfterCreateAndEdit - FAIL
  ERR: Nie znaleziono 'EditAttributes' po pozycji 3

=== SUGGESTED EDITS ===
Na podstawie failed tests, dodaj do promptu:
1. (Test 14, 15) Dodaj regule: 'Gdy user wymienia 2+ obiektow (A, B, C), PIERWSZY wybor to Foreach.'
2. (Test 20) Dodaj regule: 'Pipeline 5-krokowy wymaga EditAttributes na koncu.'
3. (Test 6) Dodaj regule: 'TargetVariable MUSI byc podane gdy uzywasz TargetVariable zamiast Items.'

Edits saved to: .bench-cache/iter1-suggestions.md
Prompt unchanged. Re-run after manual edit.
```

**WAŻNE**: `optimize` **NIE modyfikuje promptu**. Wypisuje tylko sugestie. Agent LLM musi:
1. Przeczytać sugestie
2. Edytować `system_prompt_blocks.txt` ręcznie
3. Uruchomić `optimize` ponownie (kolejna iteracja)
4. Powtarzać aż score >= target lub max-iter osiągnięte

#### 4.3.5 `validate` - walidacja pliku benchmarku (bez uruchamiania LLM)

```bash
dotnet run --project BricscadPromptBench validate \
    --benchmark Benchmark_09_InsertBlock_Extended.json
```

**Output**:
```
=== VALIDATION ===
Tests: 25
Rules: 79
Categories: 7
Difficulties: D1:2, D2:5, D3:8, D4:8, D5:2
RuleTypes: ToolCalled, ArgumentMatch, AnyArgumentMatch, AnyOfArgumentMatch, AnyOfArgumentMatchOrAbsent, SequenceMatch, ToolCallCountMax
Duplicates: 0
Valid: YES
```

---

## 5. Algorytm optymalizacji - szczegóły

### 5.1 Pętla agent-driven

```
┌─────────────────────────────────────────────────────────────┐
│ Agent LLM (opencode/hermes) - ITERACJA N                   │
└─────────────────────────────────────────────────────────────┘
                            ↓
1. Czyta: 
   - `Reports/iter_N-1/Benchmark_XX_FULL_*.json` (raport z poprzedniej iteracji)
   - `Reports/iter_N-1/iter_N-1-suggestions.md` (sugestie z poprzedniej iteracji)
   - `resources/prompts/system_prompt_blocks.txt` (aktualny prompt)
                            ↓
2. Decyduje:
   a) Zmodyfikować prompt (edytuje .txt)
   b) Zrezygnować (exit)
   c) Poczekać i spróbować innego podejścia
                            ↓
3. Wywołuje:
   `dotnet run --project BricscadPromptBench optimize --benchmark ... --profile blocks --provider gemma-4-12b-qat --target 90`
                            ↓
4. Aplikacja:
   a) Wczytuje prompt z .txt
   b) Uruchamia benchmark (LLMClient + RuleEvaluator)
   c) Generuje raport i sugestie
   d) Wypisuje score, target, gap, failed tests
   e) Czeka na kolejną iterację
                            ↓
5. Agent wraca do kroku 1 z nowymi danymi
```

### 5.2 Algorytm generowania sugestii

PromptOptimizer po każdym `run` zbiera:
- Testy FAIL z `FailedRulesErrors`
- Kategoryzuje błędy (BENCHMARK_BUG vs MODEL_BUG)
- Generuje sugestie na podstawie wzorców z wcześniejszych iteracji projektu:

```csharp
private List<string> GenerateSuggestions(List<BenchmarkTest> failedTests) {
    var suggestions = new List<string>();
    
    // Grupuj testy po kategorii
    var byCategory = failedTests.GroupBy(t => t.Category);
    
    foreach (var group in byCategory) {
        // Dla każdej kategorii wygeneruj rekomendację
        switch (group.Key) {
            case "InsertBlockDynamicAttributes":
                suggestions.Add("Dodaj regule: 'Gdy atrybut ma wartosc dynamiczna, uzyj {item} w Value, nie placeholder.'");
                break;
            case "InsertBlockWorkflows":
                suggestions.Add("Dodaj regule: 'Pipeline z EditAttributes MUSI konczyc na EditAttributes po Foreach.'");
                break;
            case "InsertBlockAdvancedForeach":
                suggestions.Add("Dodaj regule: 'ListBlocks+Foreach=TargetVariable, BEZ znaku @.'");
                break;
            // ...
        }
    }
    
    return suggestions;
}
```

**WAŻNE**: Sugestie to tylko **podpowiedzi**. Agent LLM (opencode/hermes) ma pełną autonomię - może je zignorować, dodać własne, lub całkowicie zmienić prompt.

### 5.3 Stan sesji optymalizacji

Aplikacja utrzymuje stan w katalogu `.bench-cache/`:
```
.bench-cache/
├── iter_1/
│   ├── prompt.txt          # snapshot promptu
│   ├── Benchmark_09_..._FULL_*.json
│   ├── Benchmark_09_..._ERRORS_*.json
│   └── suggestions.md      # rekomendacje
├── iter_2/
│   ├── ...
└── iteration-log.json     # pełna historia (score, timestamp, model)
```

---

## 6. Format plików

### 6.1 `llm_providers.json` (kompatybilny z V2)

```json
{
  "Providers": [
    {
      "Id": "gemma-4-12b-qat",
      "Name": "gemma-4-12b-qat",
      "EndpointUrl": "http://100.104.226.29:1234/v1/chat/completions",
      "ModelName": "google/gemma-4-12b-qat",
      "ApiKey": "not-needed",
      "Temperature": 0.2,
      "MaxTokens": 3500,
      "TopP": 0.95,
      "TopK": 0,
      "MinP": 0.0,
      "RepetitionPenalty": 1.0,
      "ReasoningEffort": "none",
      "AutoLoadModel": false,
      "GpuOffload": "default",
      "LoadContextLength": 20000,
      "TtlSeconds": 0,
      "FlashAttention": true,
      "OffloadKvCache": true,
      "MaxContextTokens": 8192,
      "ContextCompressionThreshold": 90
    }
  ],
  "ActiveProviderId": "gemma-4-12b-qat"
}
```

### 6.2 Profile promptów

Identyczny format jak w V2:
- `system_prompt_blocks.txt` - prompt dla operacji blokowych
- `system_prompt_cad.txt` - ogólny prompt CAD
- ... (8 plików)

**Aplikacja Bench używa tych samych plików** (ścieżka wskazywana przez `--prompts`).

### 6.3 Benchmarki

Identyczny format jak w V2 (BenchmarkConfig, Tests, ValidationRules). **Żadnych zmian**.

### 6.4 Output raportu (FULL)

Identyczny format jak w V2 - kompatybilność wsteczna:

```json
{
  "RunMetadata": {
    "BenchmarkName": "Benchmark_09_InsertBlock_Extended",
    "ModelName": "google/gemma-4-12b-qat",
    "ProfileName": "blocks",
    "ProviderName": "LM Studio (Lokalny)",
    "ProviderEndpoint": "http://...:1234/v1/chat/completions",
    "Temperature": 0.2,
    "MaxTokens": 3500,
    "TopP": 0.95,
    "RunDate": "2026-06-10 15:30",
    "GlobalScore": 84.0,
    "AverageExecutionTimeMs": 2534,
    "CategoriesScores": {
      "ListBlocksBasic": 100.0,
      "ListBlocksAdvanced": 100.0,
      ...
    }
  },
  "Tests": [
    {
      "Id": 1,
      "Category": "ListBlocksBasic",
      "Difficulty": 1,
      "TestName": "ListBlocks_NoArgs",
      "UserPrompt": "...",
      "Passed": true,
      "ExecutionTimeMs": 1500,
      "RecordedToolCalls": [...],
      "FailedRulesErrors": []
    },
    ...
  ]
}
```

### 6.5 ERRORS report

Identyczny z V2 - zawiera tylko testy z `Passed=false`.

---

## 7. Testowanie

### 7.1 Testy jednostkowe (xUnit)

```csharp
[Fact]
public void RuleEvaluator_AnyOfArgumentMatchOrAbsent_AcceptsMissingArgument() {
    // Arrange
    var test = new BenchmarkTest {
        RecordedToolCalls = new List<RecordedToolCall>() // puste
    };
    var rule = new ValidationRule {
        RuleType = RuleType.AnyOfArgumentMatchOrAbsent,
        TargetArgument = "GenerateSequence.Count",
        TargetValue = "2"
    };
    
    // Act
    var evaluator = new RuleEvaluator();
    var result = evaluator.Validate(test, new[] { rule });
    
    // Assert
    Assert.True(result.IsValid); // PASS mimo braku argumentu
}
```

**Pokrycie testami**:
- `RuleEvaluator`: każdy RuleType ma 2-3 testy (PASS, FAIL, edge case)
- `ValueMatcher`: ValuesMatch, ResolveJsonPath, TokenToInvariantString
- `BenchmarkRunner`: pełny przebieg z mockowanym LLM
- `PromptOptimizer`: pętla z mockowanym BenchmarkRunner

### 7.2 Testy integracyjne

```csharp
[Fact]
public async Task BenchmarkRunner_RunBenchmark_WithoutBricsCAD() {
    // Użycie prawdziwego LM Studio (jeśli dostępne) lub mockowanego HttpClient
    var llmClient = new LLMClient(testConfig);
    var runner = new BenchmarkRunner(llmClient, profileManager, ruleEvaluator, resultStore);
    
    var report = await runner.RunAsync(
        "tests/Benchmark_09_InsertBlock_Extended.json",
        "blocks",
        "gemma-4-12b-qat");
    
    Assert.NotNull(report);
    Assert.Equal(25, report.Tests.Count);
}
```

**Kryteria akceptacji**:
- Aplikacja kompiluje się bez `Bricscad.ApplicationServices` (verify `using Bricscad.*` w kodzie - powinno być 0 wystąpień)
- Benchmark z LM Studio daje wynik ±2% vs V2 (dla tego samego modelu i promptu)
- Format raportu FULL identyczny z V2 (diff JSON: 0 różnic poza timestamp i path)

---

## 8. Kompilacja i deployment

### 8.1 Struktura projektu .NET 8/9

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <RootNamespace>BricscadPromptBench</RootNamespace>
    <AssemblyName>BricscadPromptBench</AssemblyName>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
  </ItemGroup>
</Project>
```

### 8.2 Komendy

```bash
# Build
dotnet build BricscadPromptBench/BricscadPromptBench.csproj

# Run tests
dotnet test BricscadPromptBench/BricscadPromptBench.csproj

# Run benchmark
dotnet run --project BricscadPromptBench -- run --benchmark Benchmark_09.json --profile blocks --provider gemma-4-12b-qat

# Optimize (agent LLM driven)
dotnet run --project BricscadPromptBench -- optimize --benchmark Benchmark_09.json --profile blocks --provider gemma-4-12b-qat --target 90 --max-iter 20
```

### 8.3 Wymagania systemowe

- .NET 8.0 SDK (lub nowszy) - user ma .NET 10
- LM Studio (lub OpenAI API) z modelem lokalnym
- ~500 MB RAM dla aplikacji + 4-16 GB dla modelu LLM
- Bez wymagań sprzętowych CAD

---

## 9. Migracja z V2 - checklist

### 9.1 Pliki do skopiowania 1:1 (po weryfikacji braku BricsCAD.*)

- [ ] `Bricscad_AgentAI_V2/src/Core/BenchmarkModels.cs` → `Bench/Core/Models/`
- [ ] `Bricscad_AgentAI_V2/src/Models/ChatMessage.cs` → `Bench/Core/Benchmark/`
- [ ] `Bricscad_AgentAI_V2/src/Models/ToolDefinition.cs` → `Bench/Core/Benchmark/`
- [ ] `Bricscad_AgentAI_V2/src/Models/RecordedToolCall.cs` → `Bench/Core/Models/`

### 9.2 Pliki do wycięcia (częściowa kopia)

- [ ] `BenchmarkConfig`, `RunMetadata` (z `BenchmarkModels.cs`)
- [ ] `BenchmarkTest`, `RecordedToolCall` (z `BenchmarkModels.cs`)
- [ ] `TokenToInvariantString`, `ValuesMatch`, `ResolveJsonPath`, `TryParseJsonToken` (z `AutoBenchmarkEngine.cs`)
- [ ] `ValidateTest()` switch statement (z `AutoBenchmarkEngine.cs`)
- [ ] `SendMessageBenchmarkAsync` (z `LLMClient.cs`, BEZ `Document`)

### 9.3 Pliki do utworzenia od zera

- [ ] `Bench/Core/Validation/RuleType.cs`
- [ ] `Bench/Core/Validation/ValidationRule.cs`
- [ ] `Bench/Core/Validation/RuleEvaluator.cs`
- [ ] `Bench/Core/Validation/ValueMatcher.cs`
- [ ] `Bench/Core/Benchmark/BenchmarkRunner.cs`
- [ ] `Bench/Core/Benchmark/LLMClient.cs` (nowy, uproszczony)
- [ ] `Bench/Core/Profile/ProfileManager.cs`
- [ ] `Bench/Core/Profile/LLMConfigManager.cs`
- [ ] `Bench/Core/Optimizer/PromptOptimizer.cs`
- [ ] `Bench/Core/Optimizer/ResultStore.cs`
- [ ] `Bench/Cli/RunCommand.cs`
- [ ] `Bench/Cli/EvalCommand.cs`
- [ ] `Bench/Cli/DiffCommand.cs`
- [ ] `Bench/Cli/OptimizeCommand.cs`
- [ ] `Bench/Cli/ValidateCommand.cs`
- [ ] `Bench/Program.cs` (CLI parser)
- [ ] `Bench/BricscadPromptBench.csproj`

### 9.4 Pliki do NIE kopiowania

- ❌ `AutoBenchmarkEngine` (cała klasa - za bardzo powiązana z BricsCAD)
- ❌ `LLMClient` (cała klasa - ma `Document` w sygnaturach metod)
- ❌ `ToolConfigManager` (ma za dużo zależności od V2)
- ❌ `IToolV2` (interface ma `Document` w sygnaturze)
- ❌ Cały `src/Tools/` (narzędzia CAD - nie potrzebne w Bench)
- ❌ Cały `src/UI/` (WinForms - nie potrzebne)

---

## 10. Rozszerzenia (future, poza MVP)

### 10.1 Auto-edycja promptu (opcjonalne)

Aplikacja może **sama** modyfikować prompt na podstawie reguł. **Domyślnie wyłączone**. Włącza się przez `--auto-edit`:
- `BenchmarkRunner` po `run` emituje sugestie jako JSON
- `PromptOptimizer` (gdy `--auto-edit`) parsuje sugestie i modyfikuje `.txt` automatycznie
- Historia zmian zapisywana w `iteration-log.json`

### 10.2 Integracja z opencode/hermes jako tool

Aplikacja może być uruchamiana jako **tool** przez agenta opencode:
```json
// .opencode/tools/bench-optimizer.json
{
  "name": "bench-optimizer",
  "command": "dotnet run --project BricscadPromptBench -- optimize --benchmark {{benchmark}} --profile {{profile}} --provider {{provider}} --target {{target}}",
  "description": "Optimize profile prompt to achieve target benchmark score"
}
```

### 10.3 Web UI (Blazor)

Lekka aplikacja Blazor Server do wizualizacji:
- Dashboard z wynikami benchmarków
- Diff viewer dla promptów
- Real-time log stream z `optimize`

### 10.4 Eksport do innych formatów

- HTML report (z wykresami Chart.js)
- CSV (do analizy w Excelu/Pandas)
- Markdown summary (dla GitHub)

---

## 11. Ryzyka i mitygacje

| Ryzyko | Prawdopodobieństwo | Wpływ | Mitygacja |
|--------|---------------------|-------|-----------|
| Format benchmarków V2 niekompatybilny | Średnie | Wysoki | Skopiuj 1:1, nie modyfikuj struktury JSON |
| LM Studio API breaking changes | Niskie | Średnie | Użyj standardowego OpenAI format (kompatybilny) |
| Niedeterministyczność modelu | Wysokie | Średni | Dokumentuj w raporcie, akceptuj ±2% wariancję |
| Prompt staje się za długi (token limit) | Średnie | Wysoki | Walidacja długości + ostrzeżenie > 120 linii |
| Agent LLM (opencode) nie rozumie output | Niskie | Wysoki | `--json` mode + clear schema + exit codes |
| Pętla optymalizacji nie zbiega | Średnie | Wysoki | `--max-iter` (default 20) + timeout per iter |
| Brak sieci (LLM offline) | Niskie | Wysoki | Wykryj timeout, zakończ z exit code 2 |

---

## 12. Definition of Done

MVP jest gotowe, gdy:
- [ ] Aplikacja kompiluje się przez `dotnet build` (bez warnings/errors)
- [ ] `dotnet test` - 100% testów jednostkowych PASS (min 30 testów)
- [ ] `dotnet run --project Bench -- run --benchmark Benchmark_09.json --profile blocks --provider gemma-4-12b-qat` daje wynik ±2% vs V2
- [ ] `dotnet run --project Bench -- optimize ...` wypisuje score + sugestie (bez modyfikacji plików)
- [ ] `dotnet run --project Bench -- validate --benchmark Benchmark_09.json` waliduje JSON bez uruchamiania LLM
- [ ] Raport FULL ma identyczny format z V2 (kompatybilność wsteczna)
- [ ] Brak `using Bricscad.*` w kodzie Bench (grep powinien zwrócić 0)
- [ ] README.md w katalogu `BricscadPromptBench/` z przykładami użycia

---

## 13. Następne kroki po implementacji

Po zakończeniu MVP (sekcja 12):
1. Stworzyć skill `.agents/skills/bench-optimizer/SKILL.md` dla agentów LLM (opencode, hermes) - dokumentacja jak używać CLI
2. Benchmark_10 (EditAttributes rozszerzony) - 5 parametrów (Action, Attributes, FilterTag, FilterValue, BlockName)
3. Testowanie auto-edycji (`--auto-edit`) na 2-3 modelach lokalnych
4. Porównanie wyników Bench vs V2 na tych samych benchmarkach (powinno być ±2% różnicy)

---

## 14. Referencje

- **Obecna implementacja**: `Bricscad_AgentAI_V2/` (szczególnie `src/Core/AutoBenchmarkEngine.cs`, `src/Core/LLMClient.cs`)
- **Benchmarki**: `Bricscad_AgentAI_V2/tests/Benchmark_01-09_*.json` (10 plików, format zgodny z `BenchmarkConfig`)
- **Profile**: `Bricscad_AgentAI_V2/resources/prompts/system_prompt_*.txt` (8 profili)
- **Konfiguracja LLM**: `Bricscad_AgentAI_V2/llm_providers.json`
- **Memory projektu**: `Bricscad_AgentAI_V2/memory.md` (pełna historia benchmark_09 v2.28.51-v2.28.57)
- **Skill workflow**: `.agents/skills/cad-benchmark-workflow/SKILL.md` (workflow benchmarków - warto przeczytać przed implementacją)

---

**Koniec dokumentu**. Wytyczne są gotowe do przekazania agentowi kodującemu.
