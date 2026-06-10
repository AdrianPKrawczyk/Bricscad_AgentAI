# Prompt Candidate Optimizer

> **Wersja dokumentu**: 1.1  
> **Data**: 2026-06-10  
> **Status**: plan wdrożenia  
> **Cel**: narzędzie do bezpiecznego proponowania kandydatów promptu na podstawie raportów benchmarkowych z BricsCAD, bez modyfikowania promptów produkcyjnych.

---

## 1. Decyzja architektoniczna

Nie budujemy autonomicznego runnera benchmarków poza BricsCAD. Benchmark pozostaje ręcznie uruchamiany w BricsCAD, bo to jest obecne źródło prawdy dla:

- rzeczywistych profili,
- rzeczywistych schematów narzędzi,
- aktualnej konfiguracji LLM,
- raportów `_FULL.json` i `_ERRORS.json`,
- porównywalności wyników historycznych.

Nowa funkcjonalność ma być **optimizerem kandydatów promptu**, nie optimizerem promptu produkcyjnego.

Główna zasada:

```text
production prompt: read-only
candidate prompt: editable by optimizer
BricsCAD benchmark: source of truth
human review: promotion gate
lab mode: isolated benchmark worker
```

Optimizer analizuje raporty benchmarkowe, proponuje zmiany i zapisuje je jako osobny kandydat w katalogu roboczym. Nie ma prawa edytować plików w `Bricscad_AgentAI_V2/resources/prompts/`.

Dopuszczamy dwa tryby pracy:

- **Manual review loop**: użytkownik ręcznie uruchamia benchmark w BricsCAD z `PromptOverridePath`.
- **Optimizer lab loop**: zewnętrzny agent LLM tworzy kandydatów i joby, a uruchomiony BricsCAD działa jako izolowany worker do pomiaru skuteczności benchmarku.

W obu trybach prompt produkcyjny pozostaje nietknięty.

---

## 2. Problem do rozwiązania

Obecny workflow optymalizacji promptu wygląda tak:

1. Użytkownik uruchamia benchmark w BricsCAD.
2. Powstają raporty `_FULL.json` i `_ERRORS.json`.
3. Agent lub człowiek analizuje failed tests.
4. Prompt jest edytowany ręcznie.
5. Benchmark jest uruchamiany ponownie.

Problemem nie jest brak runnera benchmarków. Problemem jest brak bezpiecznego, audytowalnego warsztatu do tworzenia wariantów promptu:

- trudno śledzić, dlaczego dana reguła została dodana,
- łatwo przypadkowo nadpisać prompt produkcyjny,
- trudno porównać kandydatów między iteracjami,
- trudno odróżnić `prompt_gap` od `benchmark_bug`,
- trudno wrócić do wcześniejszej wersji kandydata.

---

## 3. Docelowy workflow

### 3.1 Pętla pracy

```text
1. Użytkownik uruchamia benchmark w BricsCAD na aktualnym profilu.
   ↓
2. BricsCAD zapisuje raport FULL i ERRORS.
   ↓
3. Prompt Candidate Optimizer czyta:
   - prompt źródłowy,
   - raport FULL,
   - raport ERRORS,
   - opcjonalnie poprzednie kandydaty.
   ↓
4. Optimizer tworzy nowy katalog candidate_NNN.
   ↓
5. W katalogu zapisuje:
   - candidate prompt,
   - diff,
   - rationale,
   - checklistę ręcznej kontroli,
   - manifest.
   ↓
6. Użytkownik sprawdza diff i uruchamia BricsCAD benchmark z prompt override.
   ↓
7. Nowy raport wraca do optimizera jako wejście kolejnej iteracji.
   ↓
8. Tylko człowiek może promować kandydata do promptu produkcyjnego.
```

### 3.2 Pętla laboratoryjna z zewnętrznym agentem

Tryb laboratoryjny pozwala zewnętrznemu agentowi LLM działać w pętli do osiągnięcia założonej skuteczności, np. `targetScore >= 90%`. BricsCAD musi być uruchomiony, ale jego rola jest ograniczona do środowiska pomiarowego.

```text
External LLM Agent
   ↓
PromptCandidateOptimizer suggest
   ↓
candidate_NNN + benchmark job
   ↓
BricsCAD Benchmark Lab Worker
   ↓
FULL/ERRORS report w katalogu kandydata
   ↓
PromptCandidateOptimizer record-result
   ↓
jeśli score >= target → stop
jeśli score < target i iter < maxIter → kolejny kandydat
```

W tym trybie BricsCAD:

- nie modyfikuje promptów produkcyjnych,
- nie zapisuje wyników do standardowej historii benchmarków użytkownika,
- zapisuje raporty wyłącznie do katalogu kandydata,
- oznacza każdy raport jako `RunMode = optimizer_lab`,
- zachowuje normalne profile, tool schemas i provider/model,
- podmienia tylko treść system promptu na czas jednego runu.

### 3.3 Minimalny przykład komendy

```powershell
dotnet run --project PromptCandidateOptimizer -- suggest `
  --profile CadBlocksProfile `
  --source Bricscad_AgentAI_V2/resources/prompts/system_prompt_blocks.txt `
  --full-report Bricscad_AgentAI_V2/tests/google_gemma-4-12b-qat/Benchmark_09_InsertBlock_Extended_google_gemma-4-12b-qat_FULL_20260610_1406.json `
  --errors-report Bricscad_AgentAI_V2/tests/google_gemma-4-12b-qat/Benchmark_09_InsertBlock_Extended_google_gemma-4-12b-qat_ERRORS_20260610_1406.json `
  --out prompt-lab/blocks
```

### 3.4 Wynik działania

```text
prompt-lab/
└── blocks/
    └── candidate_001/
        ├── system_prompt_blocks.candidate.txt
        ├── diff.patch
        ├── rationale.md
        ├── review_checklist.md
        ├── classified_failures.json
        ├── manifest.json
        ├── bricscad-job.json
        └── bricscad-results/
```

---

## 4. Zakres MVP

MVP ma być małe i bezpieczne. Nie implementujemy jeszcze headless benchmark runnera.

### 4.1 W MVP robimy

- Parser raportów benchmarkowych `_FULL.json` i `_ERRORS.json`.
- Parser promptu źródłowego.
- Klasyfikację failed tests.
- Generator kandydata promptu na kopii.
- Generator `diff.patch`.
- Generator `rationale.md`.
- Generator `review_checklist.md`.
- `manifest.json` z metadanymi.
- Opcję BricsCAD benchmarku z `PromptOverridePath`.
- Izolowany tryb `optimizer_lab` dla BricsCAD Benchmark Lab Workera.
- Osobny `OutputRoot` dla wyników laboratoryjnych.
- Kontrakt joba JSON między optimizerem / zewnętrznym agentem a BricsCAD.

### 4.2 W MVP nie robimy

- Automatycznego uruchamiania benchmarków poza działającym BricsCAD.
- Automatycznej promocji kandydata do promptu produkcyjnego.
- Automatycznej edycji `resources/prompts/*.txt`.
- Samodzielnego wykonywania narzędzi CAD.
- Web UI.
- Ingerencji w standardowe wyniki benchmarków użytkownika.

---

## 5. Bezpieczniki

Te reguły są nienegocjowalne.

### 5.1 Prompt produkcyjny jest read-only

Optimizer nigdy nie zapisuje do:

```text
Bricscad_AgentAI_V2/resources/prompts/
```

Jeżeli ścieżka `--source` znajduje się w katalogu produkcyjnym, optimizer może ją tylko czytać.

### 5.2 Kandydat zawsze jest pełną kopią

Kandydat to kompletny plik promptu, nie tylko patch. Dzięki temu BricsCAD może uruchomić benchmark bez składania promptu z wielu źródeł.

### 5.3 Każda zmiana musi mieć uzasadnienie

Każda dodana, usunięta lub zmieniona reguła musi być powiązana z:

- konkretnym testem,
- kategorią błędu,
- albo świadomą hipotezą regresyjną.

### 5.4 Najpierw benchmark bug, potem prompt

Optimizer musi klasyfikować błędy zanim zaproponuje zmianę promptu.

Kategorie:

| Kategoria | Znaczenie | Akcja |
|-----------|-----------|-------|
| `benchmark_bug` | Walidator lub test jest zbyt restrykcyjny / błędny | Nie zmieniać promptu, zgłosić w rationale |
| `model_bug` | Model łamie istniejące jasne zasady | Dodać ostrożną regułę tylko gdy wzorzec powtarzalny |
| `prompt_gap` | Prompt nie zawiera potrzebnej instrukcji | Kandydat może dodać regułę |
| `schema_gap` | Problem wynika z niejasnego kontraktu narzędzia | Wskazać ryzyko, nie maskować promptem |
| `unclear` | Brak pewności | Zaproponować ręczny review |

### 5.5 Małe iteracje

Domyślne limity:

- maksymalnie 3-5 zmian merytorycznych na kandydata,
- maksymalnie 20 nowych linii,
- zero masowych przepisań promptu,
- brak zmian stylistycznych bez wpływu na failed tests.

### 5.6 Izolacja trybu laboratoryjnego

Tryb `optimizer_lab` musi być izolowany od normalnej pracy użytkownika.

BricsCAD Benchmark Lab Worker nie może:

- zapisywać wyników do `Bricscad_AgentAI_V2/tests/<model>/`,
- zmieniać `resources/prompts/*.txt`,
- zmieniać profili w `ToolConfigManager`,
- zmieniać aktywnego providera na stałe,
- promować kandydata do produkcji,
- mieszać wyników laboratoryjnych z ręcznie uruchamianymi benchmarkami.

Wyniki laboratoryjne muszą trafiać do katalogu wskazanego przez job, najczęściej:

```text
prompt-lab/blocks/candidate_001/bricscad-results/
```

---

## 6. Projekt narzędzia CLI

### 6.1 Nazwa projektu

Proponowana nazwa:

```text
PromptCandidateOptimizer
```

Alternatywy:

- `PromptLab`
- `PromptCandidateLab`
- `BricscadPromptOptimizer`

Rekomendacja: `PromptCandidateOptimizer`, bo dobrze komunikuje ograniczenie: to narzędzie tworzy kandydatów, nie promuje zmian.

### 6.2 Struktura katalogów

```text
PromptCandidateOptimizer/
├── PromptCandidateOptimizer.csproj
├── Program.cs
├── Cli/
│   ├── SuggestCommand.cs
│   ├── CompareCommand.cs
│   ├── InspectCommand.cs
│   └── PromoteCommand.cs
├── Core/
│   ├── Reports/
│   │   ├── BenchmarkReportReader.cs
│   │   ├── BenchmarkReportModels.cs
│   │   └── FailureExtractor.cs
│   ├── Analysis/
│   │   ├── FailureClassifier.cs
│   │   ├── FailurePattern.cs
│   │   └── PromptGapDetector.cs
│   ├── Candidate/
│   │   ├── CandidateGenerator.cs
│   │   ├── CandidateManifest.cs
│   │   ├── CandidateStore.cs
│   │   └── DiffGenerator.cs
│   └── Safety/
│       ├── ProductionPromptGuard.cs
│       ├── PromptLengthGuard.cs
│       └── DuplicateRuleDetector.cs
└── tests/
    ├── FailureClassifierTests.cs
    ├── CandidateGeneratorTests.cs
    └── ProductionPromptGuardTests.cs
```

### 6.3 Komenda `suggest`

Tworzy nowego kandydata.

```powershell
dotnet run --project PromptCandidateOptimizer -- suggest `
  --profile CadBlocksProfile `
  --source Bricscad_AgentAI_V2/resources/prompts/system_prompt_blocks.txt `
  --full-report path/to/FULL.json `
  --errors-report path/to/ERRORS.json `
  --out prompt-lab/blocks `
  --max-changes 5
```

Wyjście:

```text
=== PROMPT CANDIDATE CREATED ===
Candidate: prompt-lab/blocks/candidate_001
Source prompt: Bricscad_AgentAI_V2/resources/prompts/system_prompt_blocks.txt
Benchmark: Benchmark_09_InsertBlock_Extended
Baseline score: 84.00%
Failed tests analyzed: 4
Changes proposed: 3

Files:
- system_prompt_blocks.candidate.txt
- diff.patch
- rationale.md
- review_checklist.md
- manifest.json
```

### 6.4 Komenda `inspect`

Pokazuje podsumowanie kandydata bez otwierania plików ręcznie.

```powershell
dotnet run --project PromptCandidateOptimizer -- inspect `
  --candidate prompt-lab/blocks/candidate_001
```

### 6.5 Komenda `compare`

Porównuje dwóch kandydatów albo kandydata z promptem źródłowym.

```powershell
dotnet run --project PromptCandidateOptimizer -- compare `
  --before prompt-lab/blocks/candidate_001 `
  --after prompt-lab/blocks/candidate_002
```

### 6.6 Komenda `promote`

Opcjonalna komenda do ręcznej promocji. Domyślnie wyłączona albo wymagająca flagi `--confirm`.

```powershell
dotnet run --project PromptCandidateOptimizer -- promote `
  --candidate prompt-lab/blocks/candidate_003 `
  --target Bricscad_AgentAI_V2/resources/prompts/system_prompt_blocks.txt `
  --confirm
```

W MVP można tej komendy nie implementować. Jeśli powstanie, musi:

- pokazać diff,
- wymagać `--confirm`,
- zapisać backup,
- odmówić działania, jeśli kandydat nie ma manifestu,
- odmówić działania, jeśli kandydat nie był oznaczony jako ręcznie zaakceptowany.

### 6.7 Komenda `create-job`

Tworzy job dla BricsCAD Benchmark Lab Workera.

```powershell
dotnet run --project PromptCandidateOptimizer -- create-job `
  --candidate prompt-lab/blocks/candidate_001 `
  --benchmark Bricscad_AgentAI_V2/tests/Benchmark_09_InsertBlock_Extended.json `
  --profile CadBlocksProfile `
  --provider gemma-4-12b-qat `
  --target-score 90 `
  --jobs-root prompt-lab/jobs
```

Wynik:

```text
prompt-lab/jobs/pending/job_20260610_001.json
```

### 6.8 Komenda `record-result`

Aktualizuje manifest kandydata po zakończeniu joba przez BricsCAD.

```powershell
dotnet run --project PromptCandidateOptimizer -- record-result `
  --candidate prompt-lab/blocks/candidate_001 `
  --full-report prompt-lab/blocks/candidate_001/bricscad-results/Benchmark_09_FULL.json `
  --errors-report prompt-lab/blocks/candidate_001/bricscad-results/Benchmark_09_ERRORS.json
```

Ta komenda pozwala zewnętrznemu agentowi zdecydować, czy target został osiągnięty, czy trzeba wygenerować kolejnego kandydata.

---

## 7. Format plików kandydata

### 7.1 `system_prompt_blocks.candidate.txt`

Pełna kopia promptu z proponowanymi zmianami.

Nazwa powinna zachowywać źródłowy kontekst:

```text
system_prompt_blocks.candidate.txt
system_prompt_math.candidate.txt
system_prompt_geometry.candidate.txt
```

### 7.2 `diff.patch`

Unified diff między promptem źródłowym a kandydatem.

Przykład:

```diff
--- system_prompt_blocks.txt
+++ system_prompt_blocks.candidate.txt
@@ -104,3 +104,7 @@
 Zasady kanoniczne dla Foreach w profilu blokow:
 - W Foreach + InsertBlock z Items=[A1,B2,C3]: Action MUSI miec BlockName={item}.
+
+Dodatkowe zasady dla InsertBlock po analizie Benchmark_09:
+- Gdy użytkownik wymienia 2+ bloki lub punkty, pierwszy wybór to Foreach, nie seria osobnych InsertBlock.
+- Pipeline z utworzeniem, edycją i wstawieniem bloku powinien zachować kolejność: CreateBlock, EditBlock, InsertBlock/EditAttributes.
```

### 7.3 `rationale.md`

Zawiera uzasadnienie zmian.

Minimalna struktura:

```markdown
# Candidate rationale

## Source
- Profile: CadBlocksProfile
- Source prompt: ...
- Benchmark report: ...
- Baseline score: 84.00%

## Failed tests analyzed
| Test | Category | Difficulty | Classification | Decision |
|------|----------|------------|----------------|----------|
| 14 | InsertBlockDynamicAttributes | D3 | prompt_gap | Add Foreach rule |

## Proposed changes
1. Added Foreach preference rule for multi-item InsertBlock requests.
2. Added pipeline ordering rule for CreateBlock/EditBlock/InsertBlock workflows.

## Not changed
- Test 6 looks like possible benchmark_bug because the validator may over-constrain TargetVariable formatting.

## Regression risks
- Foreach rule may over-trigger for simple repeated insertions where separate calls are acceptable.
- Pipeline rule may bias model toward unnecessary EditAttributes.
```

### 7.4 `review_checklist.md`

Checklist dla człowieka przed uruchomieniem benchmarku.

```markdown
# Review checklist

- [ ] Diff jest mały i zrozumiały.
- [ ] Zmiany dotyczą failed tests z raportu.
- [ ] Brak zmian w promptach produkcyjnych.
- [ ] Brak duplikatów istniejących reguł.
- [ ] Brak reguł sprzecznych z aktualnym kontraktem narzędzi.
- [ ] Kandydat nie przekracza limitu długości promptu.
- [ ] Uruchomiono benchmark w BricsCAD z PromptOverridePath.
- [ ] Porównano wynik z baseline.
```

### 7.5 `classified_failures.json`

Maszynowo czytelny wynik klasyfikacji.

```json
{
  "failures": [
    {
      "testId": 14,
      "testName": "InsertBlock_AttributeName_WithItem",
      "category": "InsertBlockDynamicAttributes",
      "difficulty": 3,
      "classification": "prompt_gap",
      "confidence": 0.78,
      "failedRules": [
        "Model powinien uzyc Foreach"
      ],
      "recommendedAction": "Add a compact Foreach preference rule for multi-item InsertBlock requests."
    }
  ]
}
```

### 7.6 `manifest.json`

Manifest jest kontraktem audytowym.

```json
{
  "schemaVersion": 1,
  "candidateId": "candidate_001",
  "createdAt": "2026-06-10T18:30:00+02:00",
  "profileName": "CadBlocksProfile",
  "sourcePromptPath": "Bricscad_AgentAI_V2/resources/prompts/system_prompt_blocks.txt",
  "candidatePromptPath": "prompt-lab/blocks/candidate_001/system_prompt_blocks.candidate.txt",
  "fullReportPath": "Bricscad_AgentAI_V2/tests/...FULL.json",
  "errorsReportPath": "Bricscad_AgentAI_V2/tests/...ERRORS.json",
  "benchmarkName": "Benchmark_09_InsertBlock_Extended",
  "modelName": "google/gemma-4-12b-qat",
  "baselineScore": 84.0,
  "failedTestsCount": 4,
  "proposedChangesCount": 3,
  "status": "candidate_created",
  "productionPromptModified": false,
  "labRun": {
    "enabled": false,
    "targetScore": null,
    "lastScore": null,
    "lastJobId": null,
    "lastResultPath": null
  }
}
```

### 7.7 `bricscad-job.json`

Opcjonalny plik joba generowany dla trybu laboratoryjnego.

```json
{
  "schemaVersion": 1,
  "jobId": "job_20260610_001",
  "runMode": "optimizer_lab",
  "benchmarkPath": "Bricscad_AgentAI_V2/tests/Benchmark_09_InsertBlock_Extended.json",
  "profileName": "CadBlocksProfile",
  "providerId": "gemma-4-12b-qat",
  "promptOverridePath": "prompt-lab/blocks/candidate_001/system_prompt_blocks.candidate.txt",
  "candidateId": "candidate_001",
  "outputRoot": "prompt-lab/blocks/candidate_001/bricscad-results",
  "targetScore": 90.0,
  "saveToUserBenchmarkHistory": false
}
```

Ten plik jest kontraktem między zewnętrznym agentem / optimizerem a uruchomionym BricsCAD-em.

---

## 8. Integracja z BricsCAD

Optimizer sam nie wykonuje narzędzi CAD i nie dubluje benchmark engine poza BricsCAD. Potrzebna jest mała zmiana w BricsCAD V2: benchmark musi umieć użyć promptu z pliku kandydata tylko dla jednego przebiegu oraz opcjonalnie działać jako izolowany worker laboratoryjny.

### 8.1 `BenchmarkRunOptions`

Zamiast rozbudowywać `RunBenchmarkAsync` kolejnymi parametrami, lepiej wprowadzić obiekt opcji:

```csharp
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
}
```

Docelowa sygnatura:

```csharp
public async Task<BenchmarkConfig> RunBenchmarkAsync(
    string jsonFilePath,
    BenchmarkRunOptions options,
    CancellationToken ct = default)
```

Dla kompatybilności można zostawić starą sygnaturę jako wrapper.

### 8.2 Wymagana funkcja: `PromptOverridePath`

W miejscu ładowania system promptu:

```csharp
string systemPromptContent;

if (!string.IsNullOrWhiteSpace(promptOverridePath))
{
    systemPromptContent = File.ReadAllText(promptOverridePath, Encoding.UTF8);
}
else
{
    systemPromptContent = ToolConfigManager.LoadEffectivePromptForProfile(options.ProfileName);
}
```

### 8.3 Ważne ograniczenie

`PromptOverridePath` zastępuje tylko treść promptu systemowego. Nie zmienia:

- profilu,
- listy narzędzi,
- tagów narzędzi,
- konfiguracji providera,
- `ToolConfigManager`,
- plików w `resources/prompts`.

To jest kluczowe, bo chcemy testować tylko różnicę w prompt content, bez mieszania innych zmiennych.

### 8.4 Tryb `optimizer_lab`

Tryb laboratoryjny służy wyłącznie do automatycznego pomiaru skuteczności kandydatów promptu.

W tym trybie:

```json
{
  "runMode": "optimizer_lab",
  "saveToUserBenchmarkHistory": false,
  "outputRoot": "prompt-lab/blocks/candidate_001/bricscad-results"
}
```

BricsCAD zapisuje raporty do `OutputRoot`, a nie do standardowego katalogu wyników użytkownika.

Przykład struktury:

```text
prompt-lab/blocks/candidate_001/
├── system_prompt_blocks.candidate.txt
├── manifest.json
└── bricscad-results/
    ├── Benchmark_09_InsertBlock_Extended_FULL.json
    ├── Benchmark_09_InsertBlock_Extended_ERRORS.json
    └── run.log
```

Raport powinien zawierać metadane:

```json
{
  "RunMetadata": {
    "RunMode": "optimizer_lab",
    "PromptOverridePath": "prompt-lab/blocks/candidate_001/system_prompt_blocks.candidate.txt",
    "CandidateId": "candidate_001",
    "JobId": "job_20260610_001",
    "OutputRoot": "prompt-lab/blocks/candidate_001/bricscad-results",
    "SaveToUserBenchmarkHistory": false
  }
}
```

Jeżeli nie chcemy od razu zmieniać modelu raportu, te dane można tymczasowo zapisać w `RunMetadata.Comment`. Docelowo lepiej dodać jawne pola.

### 8.5 BricsCAD Benchmark Lab Worker

Lab Worker to lekki mechanizm po stronie uruchomionego BricsCAD-a, który odbiera joby z katalogu i wykonuje benchmarki w trybie laboratoryjnym.

Proponowana kolejka:

```text
prompt-lab/jobs/
├── pending/
├── running/
├── done/
└── failed/
```

Przepływ:

1. Zewnętrzny agent lub `PromptCandidateOptimizer create-job` zapisuje job w `pending`.
2. BricsCAD Lab Worker przenosi job do `running`.
3. Worker waliduje `runMode == optimizer_lab`.
4. Worker waliduje, że `PromptOverridePath` istnieje.
5. Worker uruchamia benchmark z `BenchmarkRunOptions`.
6. Worker zapisuje raporty do `OutputRoot`.
7. Worker przenosi job do `done` albo `failed`.

Lab Worker powinien być jawnie włączany przez użytkownika, np. osobnym przyciskiem w UI:

```text
Start Benchmark Lab Worker
Stop Benchmark Lab Worker
```

Domyślnie worker jest wyłączony.

### 8.6 UI w `AutoBenchmarkControl`

Minimalne UI:

- checkbox: `Użyj promptu z pliku dla tego benchmarku`,
- textbox: ścieżka do pliku kandydata,
- button: `Wybierz...`,
- label ostrzegawczy: `Prompt produkcyjny nie zostanie zmieniony`.

Podczas benchmarku `RunMetadata.Comment` albo nowe pole powinno zapisać informację:

```text
PromptOverridePath=prompt-lab/blocks/candidate_001/system_prompt_blocks.candidate.txt
```

Jeżeli nie chcemy zmieniać modeli raportu, można dopisać to do `RunMetadata.Comment`.

Dodatkowy panel dla trybu laboratoryjnego:

- checkbox: `Włącz Benchmark Lab Worker`,
- textbox: `JobsRoot`, domyślnie `prompt-lab/jobs`,
- textbox: `LabOutputRoot`, opcjonalnie tylko informacyjne,
- label statusu: `pending/running/done/failed`,
- ostrzeżenie: `Tryb laboratoryjny zapisuje wyniki poza historią benchmarków użytkownika`.

---

## 9. Logika klasyfikacji błędów

### 9.1 Dane wejściowe

Optimizer czyta z raportów:

- `RunMetadata.BenchmarkName`,
- `RunMetadata.ModelName`,
- `RunMetadata.ProfileName`,
- `RunMetadata.GlobalScore`,
- `Tests[].Passed`,
- `Tests[].Category`,
- `Tests[].Difficulty`,
- `Tests[].TestName`,
- `Tests[].UserPrompt`,
- `Tests[].RecordedToolCalls`,
- `Tests[].ValidationRules`,
- `Tests[].FailedRulesErrors`.

### 9.2 Heurystyki MVP

| Sygnał | Klasyfikacja domyślna |
|--------|------------------------|
| Walidator oczekuje jednego formatu punktu, a narzędzie akceptuje wiele formatów | `benchmark_bug` |
| Failed rule dotyczy `AnyArgumentMatch` na złożonym JSON | `benchmark_bug` |
| Model użył nieistniejącego narzędzia | `model_bug` |
| Model pominął wymagane `SelectEntities` mimo jasnego promptu | `model_bug` albo `prompt_gap` |
| Kilka failed tests z tej samej kategorii pokazuje brak tej samej zasady | `prompt_gap` |
| Błąd dotyczy kolejności pipeline | `prompt_gap` |
| Błąd dotyczy niejasnego schematu argumentów | `schema_gap` |

### 9.3 Zasada ostrożności

Jeżeli klasyfikacja ma niską pewność, optimizer nie powinien zmieniać promptu. Powinien dopisać do `rationale.md`:

```text
Manual review required: unclear whether this is prompt_gap or benchmark_bug.
```

---

## 10. Strategia generowania zmian

### 10.1 Styl zmian

Zmiany powinny być:

- addytywne,
- krótkie,
- konkretne,
- blisko istniejącej sekcji tematycznej,
- powiązane z narzędziem i scenariuszem,
- napisane językiem istniejącego promptu.

### 10.2 Czego unikać

Optimizer nie powinien:

- przepisywać całego promptu,
- usuwać istniejących reguł bez bardzo mocnego uzasadnienia,
- dodawać ogólników typu "zawsze działaj poprawnie",
- wzmacniać promptu pod błędny benchmark,
- dublować istniejących zasad,
- zwiększać promptu bez limitu.

### 10.3 Wstawianie zmian

Preferowana kolejność:

1. Znajdź istniejącą sekcję dla narzędzia, np. `InsertBlock`, `EditBlock`, `Foreach`.
2. Jeżeli sekcja istnieje, dodaj 1-3 punkty na końcu tej sekcji.
3. Jeżeli sekcji nie ma, dodaj małą sekcję `Dodatkowe zasady po benchmarku <name>`.
4. Nie mieszaj zasad różnych narzędzi w jednej sekcji.

---

## 11. Iteracje, ranking kandydatów i pętla agenta

Po ręcznym benchmarku na kandydacie użytkownik uruchamia optimizer ponownie z nowym raportem.

W trybie laboratoryjnym ten sam krok wykonuje zewnętrzny agent po wykryciu joba w `done`.

Docelowo można dodać:

```powershell
dotnet run --project PromptCandidateOptimizer -- record-result `
  --candidate prompt-lab/blocks/candidate_001 `
  --full-report path/to/new_FULL.json `
  --errors-report path/to/new_ERRORS.json
```

To aktualizuje `manifest.json`:

```json
{
  "status": "benchmarked",
  "candidateScore": 88.0,
  "deltaScore": 4.0,
  "regressions": [
    {
      "testId": 3,
      "previous": "PASS",
      "current": "FAIL"
    }
  ],
  "newPasses": [
    {
      "testId": 14,
      "previous": "FAIL",
      "current": "PASS"
    }
  ]
}
```

Ranking kandydatów powinien brać pod uwagę:

- wzrost global score,
- liczbę regresji,
- liczbę nowych PASS,
- długość promptu,
- liczbę zmian,
- czy poprawione zostały testy D4/D5.

### 11.1 Pętla zewnętrznego agenta LLM

Zewnętrzny agent może działać według algorytmu:

```text
targetScore = 90
maxIterations = 10

for i in 1..maxIterations:
    read last FULL/ERRORS report
    run PromptCandidateOptimizer suggest
    run PromptCandidateOptimizer create-job
    wait until BricsCAD Lab Worker moves job to done/failed
    run PromptCandidateOptimizer record-result
    read candidate score
    if score >= targetScore:
        stop and mark candidate as target_reached
    if regressions are too high:
        stop or branch from previous candidate
```

Warunek stopu nie powinien opierać się tylko na `GlobalScore`. Dobrze dodać:

- maksymalną liczbę regresji,
- minimalną poprawę względem poprzedniej iteracji,
- limit długości promptu,
- limit liczby zmian na iterację,
- manual review gate przed promocją.

---

## 12. Testowanie

### 12.1 Testy jednostkowe

Wymagane testy:

- `BenchmarkReportReader` czyta raport FULL i ERRORS.
- `FailureExtractor` poprawnie wyciąga failed tests.
- `FailureClassifier` rozpoznaje podstawowe wzorce.
- `ProductionPromptGuard` blokuje zapis do `resources/prompts`.
- `CandidateStore` tworzy katalog `candidate_NNN`.
- `DiffGenerator` generuje stabilny diff.
- `CandidateGenerator` tworzy pełny prompt kandydata.

### 12.2 Testy integracyjne bez BricsCAD

Testy na zapisanych raportach:

- wejście: historyczny raport `_ERRORS.json`,
- wejście: aktualny `system_prompt_blocks.txt`,
- wynik: kompletny katalog kandydata,
- asercja: produkcyjny prompt bez zmian.

### 12.3 Test ręczny w BricsCAD

Scenariusz akceptacyjny:

1. Wygeneruj kandydata z raportu Benchmark_09.
2. Otwórz diff i rationale.
3. W BricsCAD wybierz `CadBlocksProfile`.
4. Włącz `PromptOverridePath`.
5. Wskaż `system_prompt_blocks.candidate.txt`.
6. Uruchom benchmark.
7. Sprawdź, że `resources/prompts/system_prompt_blocks.txt` nie zmienił się.
8. Porównaj wynik z baseline.

### 12.4 Test laboratoryjny z job queue

Scenariusz akceptacyjny:

1. Wygeneruj kandydata.
2. Wygeneruj `bricscad-job.json`.
3. Skopiuj albo zapisz job do `prompt-lab/jobs/pending`.
4. Uruchom BricsCAD Benchmark Lab Worker.
5. Sprawdź, że job przechodzi przez `running` do `done`.
6. Sprawdź, że raporty zapisano w `candidate_NNN/bricscad-results`.
7. Sprawdź, że standardowy katalog `Bricscad_AgentAI_V2/tests/<model>/` nie dostał nowych plików z tego runu.
8. Sprawdź, że raport ma `RunMode = optimizer_lab`.
9. Sprawdź, że prompt produkcyjny nie zmienił się.

---

## 13. Definition of Done

MVP jest gotowe, gdy:

- [ ] `suggest` tworzy katalog `candidate_NNN`.
- [ ] Kandydat zawiera pełny plik promptu.
- [ ] Kandydat zawiera `diff.patch`, `rationale.md`, `review_checklist.md`, `classified_failures.json`, `manifest.json`.
- [ ] Optimizer nie zapisuje do `Bricscad_AgentAI_V2/resources/prompts/`.
- [ ] Klasyfikacja rozróżnia co najmniej `benchmark_bug`, `model_bug`, `prompt_gap`, `schema_gap`, `unclear`.
- [ ] BricsCAD benchmark potrafi użyć `PromptOverridePath`.
- [ ] BricsCAD Benchmark Lab Worker potrafi wykonać job z `prompt-lab/jobs/pending`.
- [ ] Tryb `optimizer_lab` zapisuje wyniki wyłącznie do `OutputRoot`.
- [ ] Tryb `optimizer_lab` nie zaśmieca standardowej historii benchmarków użytkownika.
- [ ] Raport benchmarku zawiera informację, że użyto prompt override.
- [ ] Raport laboratoryjny zawiera `RunMode`, `CandidateId`, `JobId`, `PromptOverridePath`, `OutputRoot`.
- [ ] Test ręczny potwierdza, że prompt produkcyjny pozostaje bez zmian.
- [ ] Test laboratoryjny potwierdza, że zewnętrzny agent może wykonać cykl candidate → job → report → record-result.
- [ ] Dokumentacja opisuje pełną pętlę: raport → kandydat → review → benchmark override → decyzja człowieka.

---

## 14. Rekomendowana kolejność wdrożenia

### Faza 1: Candidate generator bez LLM

Pierwszy wariant może działać heurystycznie:

- czyta failed tests,
- grupuje po kategorii,
- tworzy `classified_failures.json`,
- generuje `rationale.md`,
- kopiuje prompt bez zmian albo dodaje tylko ręcznie zakodowane sugestie dla znanych kategorii.

Cel: zbudować bezpieczny format i workflow.

### Faza 2: BricsCAD `PromptOverridePath` i `BenchmarkRunOptions`

Dodać jednorazowy prompt override do benchmarku.

To jest najważniejszy element integracyjny, bo pozwala testować kandydatów bez dotykania produkcji.

### Faza 3: Izolowany `optimizer_lab`

Dodać:

- `OutputRoot`,
- `RunMode`,
- `CandidateId`,
- `JobId`,
- `SaveToUserBenchmarkHistory = false`,
- zapis raportów laboratoryjnych poza standardową historią użytkownika.

### Faza 4: BricsCAD Benchmark Lab Worker

Dodać obsługę kolejki:

```text
prompt-lab/jobs/pending
prompt-lab/jobs/running
prompt-lab/jobs/done
prompt-lab/jobs/failed
```

Worker ma być domyślnie wyłączony i jawnie uruchamiany przez użytkownika.

### Faza 5: LLM-assisted candidate generation

Dopiero po stabilnym formacie można dodać LLM jako redaktora promptu.

LLM dostaje:

- prompt źródłowy,
- failed tests,
- klasyfikację,
- limity zmian,
- instrukcję: nie zmieniaj produkcji, generuj tylko kandydata.

LLM nie dostaje prawa zapisu poza katalogiem kandydata.

### Faza 6: Pętla zewnętrznego agenta

Dodać komendy `create-job`, `record-result` i dokumentację automatycznej pętli do target score.

### Faza 7: Porównywanie iteracji

Dodać `record-result`, ranking kandydatów i wykrywanie regresji.

### Faza 8: Ręczna promocja

Opcjonalnie dodać `promote --confirm`, ale dopiero po kilku cyklach pracy, kiedy format kandydata będzie sprawdzony.

---

## 15. Dlaczego to jest lepsze od autonomicznego runnera

Ten wariant:

- nie dubluje benchmark engine poza BricsCAD,
- nie ryzykuje rozjazdu tool schemas,
- nie miesza zmian promptu z konfiguracją narzędzi,
- daje pełną audytowalność,
- pozwala człowiekowi kontrolować promowanie zmian,
- nadal automatyzuje najbardziej żmudną część: analizę raportu i przygotowanie kandydata.

To jest bezpieczniejszy układ: optimizer może być kreatywny, ale tylko w laboratorium. Produkcja pozostaje pod kontrolą użytkownika.
