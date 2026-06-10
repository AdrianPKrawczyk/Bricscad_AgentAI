---
name: cad-benchmark-workflow
description: Kompletny workflow tworzenia, uruchamiania i ulepszania benchmarkow AutoBenchmark dla narzedzi CAD (EditBlock, CreateBlock, InsertBlock, EditAttributes, ManageLayers) w projekcie Bricscad_AgentAI_V2. Uzywaj przy kazdym nowym benchmarku JSON lub poprawie istniejacego. Obejmuje analize API, kategoryzacje testow D1-D5, klasyfikacje oblen (benchmark_bug vs model_bug), naprawe walidatorow (AnyOfArgumentMatch, ToolCallCountMax) i wzmocnienie promptu systemowego.
---

# Workflow Benchmarkow CAD - Bricscad_AgentAI_V2

Ten skill opisuje kompletny proces tworzenia benchmarku dla narzedzi CAD (obecnie bloki: EditBlock, CreateBlock, InsertBlock; w przyszlosci atrybuty, warstwy, listy blokow) i iteracyjnego poprawiania wynikow.

## Kontekst projektu

- **Projekt**: `Bricscad_AgentAI_V2/` (C# .NET 4.8 + WinForms + Teigha)
- **Benchmarki**: `tests/Benchmark_NN_<Name>.json` (patrz `Benchmark_07_EditBlock.json`, `Benchmark_08_CreateBlock.json`)
- **Silnik**: `src/Core/AutoBenchmarkEngine.cs` - `BenchmarkConfig`, `RunBenchmarkAsync()`
- **Profile**: `src/Core/ToolConfigManager.cs` - mapowanie profil -> plik promptu (CadProfile / CadBlocksProfile)
- **Prompty**: `resources/prompts/system_prompt.txt` (ogolny) i `resources/prompts/system_prompt_blocks.txt` (blokowy)
- **Raporty**: `tests/<model_dir>/Benchmark_NN_<Name>_<Model>_FULL_<timestamp>.json` + `_ERRORS_<timestamp>.json`
- **Pamiec**: `Bricscad_AgentAI_V2/memory.md` - logi v2.28.x z diagnozami

## 7-Fazowy Workflow

### Faza 1: Przygotowanie benchmarku (zrodlowy JSON)

**Krok 1.1: Analiza API narzedzia**
- Otworz `src/Tools/<ToolName>Tool.cs` i przeanalizuj:
  - Parametry schematu (GetToolSchema -> Properties, Required)
  - Logike Execute (walidacja, typowe wzorce, scenariusze bledow)
  - Formaty wejscia (np. punkt 3D: `0,0,0` / `[x,y,z]` / `(x,y,z)` / `AskUser`)

**Krok 1.2: Sprawdz pokrycie API przez istniejace benchmarki**
```bash
# Szukaj czy benchmark juz istnieje
ls Bricscad_AgentAI_V2/tests/Benchmark_*<ToolName>*.json
```
- Jesli istnieje - przejdz do Fazy 6 (Retest)
- Jesli nie - projektuj od zera

**Krok 1.3: Kategoryzacja testow (5 poziomow trudnosci)**

| D | Trudnosc | Przyklady |
|---|----------|-----------|
| 1 | Latwy | Pojedyncze wywolanie z oczywistymi parametrami |
| 2 | Sredni | Wiele argumentow, opcjonalne parametry |
| 3 | Wymagajacy | Sekwencje (SelectEntities -> EditBlock), filtry, scenariusze bledow |
| 4 | Trudny | Foreach+EditBlock, WorkflowCombined (3+ kroki) |
| 5 | Ekspercki | Foreach+GenerateSequence, MTextAttribute, multi-step workflows |

**Krok 1.4: Utworz plik benchmarku**
- Wzor: `Bricscad_AgentAI_V2/tests/Benchmark_NN_<ToolName>.json`
- Uzyj 30+ testow (minimum 20), 8-10 kategorii, ~120 regul walidacyjnych
- **Waliduj JSON** po utworzeniu:
```powershell
$json = Get-Content -LiteralPath "tests/Benchmark_NN_<Name>.json" -Raw
$obj = $json | ConvertFrom-Json  # musi przejsc bez Exception
Write-Host ("Tests: {0}, Rules: {1}" -f $obj.Tests.Count, (($obj.Tests | ForEach-Object { $_.ValidationRules.Count } | Measure-Object -Sum).Sum))
```

**Wzorzec ValidationRules** (z `Benchmark_07` i `Benchmark_08`):
- `ToolCalled` - czy model uzyc konkretnego narzedzia
- `ArgumentMatch` - konkretna wartosc argumentu (sciezka JSON np. `Modifications[0].Prop`)
- `AnyArgumentMatch` - substring (dla prostych sprawdzen, ale ma ograniczenia)
- `AnyOfArgumentMatch` - wiele wariantow JSON (parsowane jako JToken.DeepEquals) - **PREFEROWANE**
- `SequenceMatch` - kolejnosc wywolan narzedzi (np. "SelectEntities,EditBlock")
- `ToolCallCountMax` - ograniczenie liczby wywolan (zapobieganie petli)

### Faza 2: Pierwszy test (baseline)

**Krok 2.1: Kompilacja projektu**
- (Wymaga srodowiska z BricsCAD; user musi sam skompilowac)
- W terminalu nie mozna uruchomic GUI benchmarku - **user musi go uruchomic w BricsCAD**

**Krok 2.2: User uruchamia benchmark w GUI BricsCAD**
- W UI: `AutoBenchmarkControl` -> Wczytaj JSON -> Wybierz profil `CadBlocksProfile` -> Wybierz model (np. `gemma-4-26b-a4b-qat`) -> Start
- Benchmark zapisuje raport w `tests/<model_dir>/`

**Krok 2.3: Wczytaj raport i zsumuj**
```powershell
$f = "Bricscad_AgentAI_V2\tests\<model_dir>\Benchmark_NN_<Name>_<Model>_FULL_<timestamp>.json"
$j = Get-Content -LiteralPath $f -Raw | ConvertFrom-Json
$passed = ($j.Tests | Where-Object { $_.Passed }).Count
$totalTime = 0; foreach ($t in $j.Tests) { $totalTime += [int]$t.ExecutionTimeMs }
Write-Host ("PASS: {0}/{1}  Avg: {2}ms  Total: {3}ms" -f $passed, $j.Tests.Count, [int]($totalTime/$j.Tests.Count), $totalTime)
```

### Faza 3: Klasyfikacja oblen

**Krok 3.1: Wylistuj oble testy z FailedRulesErrors**
```powershell
foreach ($t in $j.Tests) {
    if (-not $t.Passed) {
        Write-Host ("Test {0} D{1}: {2}" -f $t.Id, $t.Difficulty, $t.TestName)
        foreach ($c in $t.RecordedToolCalls) {
            $argStr = ($c.Arguments | ConvertTo-Json -Compress -Depth 5)
            Write-Host ("    -> {0}({1})" -f $c.ToolName, $argStr)
        }
        foreach ($err in $t.FailedRulesErrors) {
            Write-Host ("  - {0}" -f $err)
        }
    }
}
```

**Krok 3.2: Kategoryzuj w kategorie**

| Kategoria | Co sprawdzic |
|-----------|--------------|
| **BENCHMARK_BUG_format_punktu** | Walidator porownuje `"[0,0,0]"` vs `"0,0,0"` ale `ParsePoint` akceptuje oba. Napraw: `AnyOfArgumentMatch` z 3-6 wariantami. |
| **BENCHMARK_BUG_zbyt_szczegol** | Walidator wymuszal konkretna wartosc (np. "Uwaga") ale model dal poprawna ale inna (np. "To jest tekst\n..."). Napraw: rozszerz warianty. |
| **BENCHMARK_BUG_AnyArgumentMatch** | `AnyArgumentMatch` z substring ("CreateBlock") vs JSON string - nie dziala z `ValuesMatch`. Napraw: `AnyOfArgumentMatch` z konkretnymi JSON wariantami. |
| **MODEL_BUG_halucynacja_narzedzia** | Model wywoluje nieistniejace narzedzia (np. `ReadSelectedBlockInfo`). Trudne do naprawienia. |
| **MODEL_BUG_brak_SelectEntities** | UserPrompt mowi "Zaznacz..." ale model pomija SelectEntities. Wymaga wzmocnienia promptu. |
| **MODEL_BUG_UserInput_vs_AskUser** | Model uzywa `UserInput(InputType=Point)` zamiast `BasePoint="AskUser"`. Wymaga wzmocnienia promptu. |
| **MODEL_BUG_petla_10x** | Model powtarza to samo wywolanie 10x. Wychwycone przez `ToolCallCountMax`. |
| **MODEL_BUG_Foreach_Math** | Model halucynuje `{MATH: {item_x}, {item_y}, 0}` (nieistniejace placeholdery). |

### Faza 4: Naprawa benchmarku

**Napraw BENCHMARK_BUG (szybkie wygrane) - przed wzmocnieniem promptu!**

Zasada: **Najpierw poleruj benchmark, potem prompt**. Kazde wzmocnienie promptu to ryzyko regresji gdzie indziej.

**Krok 4.1: Napraw `AnyArgumentMatch` -> `AnyOfArgumentMatch` dla tolerowania roznej kolejnosci kluczy**

Wzor:
```json
// PRZED (zbyt restrykcyjne):
{"RuleType": "AnyArgumentMatch", "TargetArgument": "Action", "TargetValue": "CreateBlock"}

// PO (akceptuje warianty):
{"RuleType": "AnyOfArgumentMatch", "TargetArgument": "Action", "TargetValue": "{\"ToolName\":\"CreateBlock\",\"BlockName\":\"{item}\"} || {\"ToolName\":\"CreateBlock\",\"BasePoint\":\"{item}\"}"}
```

**Krok 4.2: Napraw format punktu - akceptuj 6 wariantow**
```json
{"RuleType": "AnyOfArgumentMatch", "TargetArgument": "BasePoint", "TargetValue": "0,0,0 || [0,0,0] || (0,0,0) || 0,0 || [0,0] || (0,0)"}
```

**Krok 4.3: Napraw bledny origin (model poprawia sam)**
```json
{"RuleType": "AnyOfArgumentMatch", "TargetArgument": "BasePoint", "TargetValue": "origin || 0,0,0 || [0,0,0]"}
```

**Krok 4.4: Dodaj ToolCallCountMax dla zapobiegania petli**
```json
{"RuleType": "ToolCallCountMax", "TargetValue": "CreateBlock", "ExpectedOutput": "1", "ErrorMessage": "BLAD: Model powinien wywolac CreateBlock dokladnie 1 raz, a nie powtarzac w petli."}
```

**Krok 4.5: Waliduj JSON po kazdej edycji**
```powershell
$json = Get-Content -LiteralPath "<path>" -Raw
$obj = $json | ConvertFrom-Json  # musi przejsc
```

### Faza 5: Wzmocnienie promptu (delikatnie, addytywnie)

**KROK 5.1: Sprawdz obecny prompt**
```bash
wc -l Bricscad_AgentAI_V2/resources/prompts/system_prompt_blocks.txt
```

**KROK 5.2: Dodaj 1-3 reguly (NIE wiecej) na koncu pliku**

Wzorzec (z v2.28.47):
```
Zasady dla CreateBlock (tworzenie NOWEJ definicji bloku z zaznaczenia):
- CreateBlock tworzy nowa definicje bloku z AKTUALNIE zaznaczonych obiektow (ActiveSelection). Przed wywolaniem CreateBlock MUSI byc SelectEntities lub obiekty musza byc juz zaznaczone.
- Parametr BasePoint moze byc: 0,0,0 / [x,y,z] / (x,y,z) / AskUser.
- NIE uzywaj osobnego narzedzia UserInput(InputType=Point) przed CreateBlock - to zbedne. CreateBlock ma wbudowany mechanizm BasePoint="AskUser".
- W Foreach + CreateBlock z Items=[A,B,C]: w Action uzyj BasePoint jako konkretny punkt XYZ (np. "[0,0,0]"). Nie uzywaj BasePoint="AskUser" w szablonie Foreach.
- CreateBlock sluzy do TWORZENIA nowych definicji blokow. InsertBlock sluzy do WSTAWIANIA istniejacych definicji. Nie myl tych dwoch operacji.
```

**KROK 5.3: Zasady bezpieczenstwa**
- NIE modyfikuj istniejacych regul (zawsze dodawaj nowe)
- NIE przekraczaj 100 linii w prompcie (kazda regula to ryzyko regresji)
- NIE ruszaj `CadProfile` (system_prompt.txt) - tam nie powinno byc regul blokowych
- Po kazdym wzmocnieniu: testy 2-3 modeli i sprawdzenie czy nie ma regresji

### Faza 6: Retest i plateau check

**Krok 6.1: User wykonuje kompilacje i retest**
- Kompilacja projektu (wymaga srodowiska z BricsCAD)
- Ponowne uruchomienie benchmarku z **3+ roznymi modelami** (np. 26B QAT, 31B QAT, 12b-qat)

**Krok 6.2: Porownanie raportow (stary vs nowy)**
```powershell
# Diff zmian statusu
$old = Get-Content -LiteralPath $oldReport -Raw | ConvertFrom-Json
$new = Get-Content -LiteralPath $newReport -Raw | ConvertFrom-Json
foreach ($t in $new.Tests) {
    $oldTest = $old.Tests | Where-Object { $_.Id -eq $t.Id }
    if ($oldTest.Passed -ne $t.Passed) {
        $change = if ($t.Passed) { "PASS" } else { "FAIL" }
        Write-Host ("  Test {0,-2} D{1} {2} : {3}" -f $t.Id, $t.Difficulty, $t.TestName, $change)
    }
}
```

**Krok 6.3: Identyfikuj wspolne oble (5+/6 modeli)**
- Jesli oblewane przez 5+/6 modeli - prawdopodobnie benchmark_bug
- Jesli oblewane przez 1-2 modeli - specyficzny model_bug

**Krok 6.4: Sprawdz plateau**
- Jesli 2 kolejne retesty daja ten sam wynik (+/-1) - **plateau osiagniete**
- W takim przypadku - **przejdz do nastepnego benchmarku** zamiast polerowac dalej

### Faza 7: Commit + memory.md

**Krok 7.1: Struktura commit message**
```
<type>(<scope>): <subject> [KROK-v2.28.XX]

- Zmiana 1
- Zmiana 2
- Zmiana 3
```

Typy: `feat` (nowy benchmark), `fix` (naprawa benchmarku/promptu), `docs` (memory.md)

**Krok 7.2: Wpis w memory.md**
```markdown
## [v2.28.XX] 2026-06-10THH:MM:SS+02:00 - Benchmark_NN_<Name> - <opis>
### [WYNIKI]
| Model | Score | AvgMs | TotalMs |
|-------|-------|-------|---------|
| 31B QAT | 28/36 (78%) | 10016 | 360587 |
| 26B QAT | 26/36 (72%) | 2361 | 85002 |

### [KLUCZOWE USTALENIA]
- Benchmark osiagnal X% PASS.
- Rezydualne oble wspolne: testy Y, Z.

### [STAN_SYSTEMU]
- Benchmark_NN ma X regul walidacyjnych.
- Prompt ma Y linii.

### [KOLEJNY_KROK]
- Commit memory.md + benchmark.
- Benchmark_NN GOLD.
- Przejscie do Benchmark_(NN+1).
```

**Krok 7.3: Konwencja commitow**
- Conventional Commits (feat, fix, docs)
- Scope: `benchmark`, `prompt`, `memory`
- Numeracja KROK: v2.28.XX (inkrementalna)
- 1 commit = 1 logiczna zmiana (np. 1 benchmark_bug fix, 1 prompt enhancement)

## Specyficzne wzorce dla blokow (EditBlock/CreateBlock/InsertBlock)

### Wymuszanie Foreach (testy D4-D5)
- UserPrompt musi wyraznie mowic "dla X1, X2, X3 wywolaj Foreach"
- Walidator: `AnyOfArgumentMatch` Action z 3 wariantami kolejnosci kluczy
- Walidator: `ToolCallCountMax` CreateBlock/EditBlock/InsertBlock = 0 (model nie powinien wywolywac bezposrednio poza Foreach)

### Wymuszanie SelectEntities przed CreateBlock
- UserPrompt musi mowic "Zaznacz obiekty, nastepnie..."
- Walidator: `SequenceMatch` "SelectEntities,CreateBlock"
- Prompt: regula "SelectEntities MUSI byc przed CreateBlock gdy user mowi 'Zaznacz...'"

### Unikanie halucynacji TargetName (EditBlock)
- Wazna regula w prompcie (system_prompt_blocks.txt:71-76):
  ```
  DOKLADNE NAZWY PARAMETROW EditBlock:
  - DOZWOLONE: Target, BlockName, Modifications, Filters, FindText, ReplaceText, RemoveDimensions, Recursive
  - ZAKAZANE: TargetName, Name, Block, BlockId
  ```

### Tolerancja na rozne formaty
- Punkt: `0,0,0` / `[0,0,0]` / `(x,y,z)` - wszystkie akceptowane przez `ParsePoint`
- BlockName: moze zawierac `-`, `_`, `.` (BricsCAD akceptuje)
- Foreach Items: albo `[A1,A2,A3]` (nazwy) albo `[[0,0,0],[100,0,0]]` (punkty) - oba rownowazne

## Kiedy NIE uzywac tego skilla

- Przy prostych zmianach kodu C# (uzyj `bricscad-expert`)
- Przy commitowaniu zmian (uzyj `git-expert`)
- Przy kompilacji projektu (uzyj `project-compiler`)
- Przy pisaniu testow jednostkowych (uzyj `test-bricscad-tool`)
- Przy aktualizacji USER_GUIDE (uzyj `user-doc-manager`)

## Wzorcowe benchmarki (reference)

| Benchmark | Testy | Status | Lekcje |
|-----------|-------|--------|--------|
| Benchmark_07_EditBlock.json | 10 | GOLD 100% (26B QAT) | Bazowy, za prosty |
| Benchmark_07_EditBlock_Complete.json | 30 | GOLD 90% (CadBlocks) | Rozszerzony, AnyOfArgumentMatch |
| Benchmark_08_CreateBlock.json | 36 | GOLD 81% (31B QAT) | 6 modeli przetestowanych, plateau |

## Checklist przed GOLD

- [ ] Benchmark ma 20+ testow
- [ ] Benchmark ma 8-10 kategorii (EditBlockBasic, EditBlockFilters, EditBlockForeach, EditBlockNegative, itp.)
- [ ] Benchmark ma 5 poziomow trudnosci (D1-D5)
- [ ] Validator uzywa `AnyOfArgumentMatch` (nie `AnyArgumentMatch` z substring) gdziekolwiek to mozliwe
- [ ] Validator ma `ToolCallCountMax` dla testow z `origin` (zapobieganie petli)
- [ ] Prompt wzmocniony o reguly specyficzne dla narzedzia (NIE w `CadProfile`!)
- [ ] Benchmark przetestowany 3+ modelami
- [ ] Wynik 80%+ dla 31B QAT (top model)
- [ ] Wszystkie BENCHMARK_BUG naprawione
- [ ] memory.md zaktualizowany z wpisem v2.28.XX
- [ ] Commit z tytulem `[KROK-v2.28.XX]`
