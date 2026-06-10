---
name: prompt-candidate-optimizer
description: Workflow automatycznej pętli Prompt Candidate Optimizer dla BricsCAD_AgentAI_V2. Używaj, gdy agent ma tworzyć lub modyfikować laboratoryjne kopie promptu, generować joby benchmarkowe, uruchamiać BricsCAD Benchmark Lab przez COM, monitorować wyniki w prompt-lab, rejestrować score i iterować do targetu bez ruszania promptów produkcyjnych.
---

# Prompt Candidate Optimizer

Ten skill opisuje bezpieczną pętlę:

```text
raport benchmarku -> kandydat promptu -> job labowy -> BricsCAD przez COM -> raport -> manifest -> kolejna iteracja
```

Najważniejsza zasada: **nigdy nie edytuj `Bricscad_AgentAI_V2/resources/prompts/*.txt` podczas optymalizacji**. Edytuj wyłącznie pliki w `prompt-lab/blocks/candidate_NNN/`.

## Elementy Systemu

- CLI: `PromptCandidateOptimizer/PromptCandidateOptimizer.csproj`
- Produkcyjne prompty: `Bricscad_AgentAI_V2/resources/prompts/`
- Katalog labowy: `prompt-lab/`
- Kandydaci: `prompt-lab/blocks/candidate_NNN/`
- Kolejka jobów: `prompt-lab/jobs/pending|running|done|failed`
- Wyniki labowe: `prompt-lab/blocks/candidate_NNN/bricscad-results/`
- Komenda BricsCAD: `AGENT_BENCHMARK_LAB_ONCE`
- Tryb raportu: `RunMode = optimizer_lab`

## Standardowa Pętla

### 1. Znajdź ostatni raport wejściowy

Dla pierwszej iteracji użyj ręcznego raportu z:

```powershell
Bricscad_AgentAI_V2/tests/<model>/*FULL*.json
Bricscad_AgentAI_V2/tests/<model>/*ERRORS*.json
```

Dla kolejnych iteracji użyj raportu poprzedniego kandydata:

```powershell
prompt-lab/blocks/candidate_NNN/bricscad-results/*FULL*.json
prompt-lab/blocks/candidate_NNN/bricscad-results/*ERRORS*.json
```

### 2. Utwórz kandydata

```powershell
$full = (Get-ChildItem -LiteralPath prompt-lab\blocks\candidate_001\bricscad-results -Filter *FULL*.json | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
$errors = (Get-ChildItem -LiteralPath prompt-lab\blocks\candidate_001\bricscad-results -Filter *ERRORS*.json | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName

dotnet run --project PromptCandidateOptimizer\PromptCandidateOptimizer.csproj -- suggest `
  --profile CadBlocksProfile `
  --source prompt-lab\blocks\candidate_001\system_prompt_blocks.candidate.txt `
  --full-report $full `
  --errors-report $errors `
  --out prompt-lab\blocks `
  --max-changes 5
```

Jeśli źródłem jest już kandydat, nazwa pliku może wyjść jako `system_prompt_blocks.candidate.candidate.txt`. To jest akceptowalne, o ile job wskazuje właściwy plik.

### 3. Edytuj tylko prompt laboratoryjny

Możesz ręcznie zmienić tylko plik w nowym katalogu kandydata, np.:

```text
prompt-lab/blocks/candidate_002/system_prompt_blocks.candidate.candidate.txt
```

Dodawaj małe, celowane reguły. Nie rób szerokich zmian stylistycznych. Po ręcznej edycji dopisz krótką notatkę do `rationale.md` w sekcji `Manual lab edit`.

### 4. Utwórz job dla BricsCAD

```powershell
dotnet run --project PromptCandidateOptimizer\PromptCandidateOptimizer.csproj -- create-job `
  --candidate prompt-lab\blocks\candidate_002 `
  --benchmark Bricscad_AgentAI_V2\tests\Benchmark_09_InsertBlock_Extended.json `
  --benchmark Bricscad_AgentAI_V2\tests\Benchmark_08_CreateBlock.json `
  --benchmark Bricscad_AgentAI_V2\tests\Benchmark_10_EditAttributes.json `
  --profile CadBlocksProfile `
  --provider gemma-4-12b-qat `
  --target-score 90 `
  --jobs-root prompt-lab\jobs
```

Preferowany tryb optymalizacji promptu to kilka benchmarkow w jednym jobie. Pojedynczy JSON jest dobry do szybkiej diagnozy, ale latwo przeucza prompt pod jeden przypadek.

Alternatywnie zapisz liste benchmarkow w pliku tekstowym albo JSON array i uzyj:

```powershell
dotnet run --project PromptCandidateOptimizer\PromptCandidateOptimizer.csproj -- create-job `
  --candidate prompt-lab\blocks\candidate_002 `
  --benchmarks-file prompt-lab\benchmark-sets\cad-blocks-core.txt `
  --profile CadBlocksProfile `
  --provider gemma-4-12b-qat `
  --target-score 90 `
  --jobs-root prompt-lab\jobs
```

Sprawdź, czy job ma absolutne ścieżki:

```powershell
Get-Content -LiteralPath prompt-lab\jobs\pending\job_*.json
```

Job wielobenchmarkowy powinien miec `benchmarkPaths` z lista sciezek. `benchmarkPath` zostaje tylko dla kompatybilnosci wstecznej i wskazuje pierwszy benchmark.

### 5. Uruchom BricsCAD benchmark przez COM

BricsCAD musi być uruchomiony i plugin musi być załadowany. Potem wyślij komendę przez COM:

```powershell
$app = [Runtime.InteropServices.Marshal]::GetActiveObject('BricscadApp.AcadApplication')
$doc = $app.ActiveDocument
$cmd = "AGENT_BENCHMARK_LAB_ONCE`nD:\GitHub\Bricscad_AgentAI\prompt-lab\jobs`n"
$doc.SendCommand($cmd)
```

Nie proś użytkownika o ręczne wpisanie komendy, jeśli COM działa. Ręczne uruchomienie jest tylko fallbackiem.

### 6. Monitoruj kolejkę

```powershell
Get-ChildItem -LiteralPath prompt-lab\jobs -Recurse | Select-Object FullName,Length,LastWriteTime
```

Interpretacja:

- `pending`: job jeszcze niepodjęty.
- `running`: BricsCAD aktualnie wykonuje benchmark; czekaj.
- `done`: benchmark zakończony, czytaj raporty.
- `failed`: czytaj `*.error.txt`.

Jeśli job jest w `running`, czekaj i sprawdzaj ponownie:

```powershell
Start-Sleep -Seconds 30
```

### 7. Odczytaj wynik

```powershell
$summary = (Get-ChildItem -LiteralPath prompt-lab\blocks\candidate_002\bricscad-results -Filter *_SUMMARY.json | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
$j = Get-Content -LiteralPath $summary -Raw | ConvertFrom-Json
[pscustomobject]@{
  Score = $j.weightedGlobalScore
  Passed = $j.passedTests
  Total = $j.totalTests
  RunMode = $j.runMode
  CandidateId = $j.candidateId
  JobId = $j.jobId
  PromptOverridePath = $j.promptOverridePath
}
```

Wynik labowy musi mieć:

```text
RunMode = optimizer_lab
CandidateId = candidate_NNN
PromptOverridePath = prompt-lab/...
```

### 8. Zarejestruj wynik w manifeście

```powershell
dotnet run --project PromptCandidateOptimizer\PromptCandidateOptimizer.csproj -- record-result `
  --candidate prompt-lab\blocks\candidate_002 `
  --results-root prompt-lab\blocks\candidate_002\bricscad-results
```

`record-result --results-root` zapisuje w manifiescie score wazony po wszystkich testach ze zbiorczego summary. Stary tryb `--full-report` zostaje dla jednego benchmarku.

### 9. Analizuj oble

```powershell
$e = (Get-ChildItem -LiteralPath prompt-lab\blocks\candidate_002\bricscad-results -Filter *ERRORS*.json | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
$j = Get-Content -LiteralPath $e -Raw | ConvertFrom-Json
foreach ($t in $j.Tests) {
  [pscustomobject]@{
    Id = $t.Id
    D = $t.Difficulty
    Category = $t.Category
    TestName = $t.TestName
    Errors = ($t.FailedRulesErrors -join ' | ')
    Calls = (($t.RecordedToolCalls | ForEach-Object { $_.ToolName }) -join ',')
  }
}
```

Klasyfikuj przed zmianą promptu:

- `benchmark_bug`: walidator zbyt restrykcyjny; nie maskuj promptem.
- `prompt_gap`: brak jasnej reguły; można dodać małą regułę.
- `model_bug`: model ignoruje jasne zasady; dodaj negatywną regułę tylko jeśli wzorzec powtarzalny.
- `schema_gap`: niejasny kontrakt narzędzia; preferuj poprawę benchmarku/schematu.
- `unclear`: nie zmieniaj promptu bez review.

## Automatyczna Pętla Do Targetu

Powtarzaj:

```text
1. read last report
2. suggest next candidate
3. optionally edit lab prompt
4. create-job
5. SendCommand via COM
6. wait for done/failed
7. read *_SUMMARY.json and record-result --results-root
8. compare score/regressions
```

Zatrzymaj pętlę, gdy:

- `score >= target-score`, albo
- pojawia się dużo regresji, albo
- wynik stoi w plateau przez 2 iteracje, albo
- oble wyglądają na `benchmark_bug`.

## Bezpieczniki

- Nigdy nie zapisuj do `Bricscad_AgentAI_V2/resources/prompts/`.
- Nigdy nie kopiuj kandydata do produkcji bez jawnej prośby użytkownika.
- Wyniki automatyczne muszą lądować w `prompt-lab/blocks/candidate_NNN/bricscad-results/`.
- Nie mieszaj wyników labowych z `Bricscad_AgentAI_V2/tests/<model>/`.
- Jeśli COM nie działa, sprawdź proces `bricscad` i ProgID `BricscadApp.AcadApplication`.
- Jeśli job trafia do `failed`, najpierw czytaj `prompt-lab/jobs/failed/*.error.txt`.

Przy optymalizacji szerokiej job powinien uzywac `benchmarkPaths`, aby mierzyc prompt szerzej niz pojedynczy JSON.

## Znane Problemy

- Jeżeli job zawiera ścieżki względne, BricsCAD może szukać plików względem własnego katalogu roboczego. Nowe joby powinny mieć absolutne ścieżki.
- Sandbox może blokować `dotnet run` przez `NuGet.Config`; w razie błędu uruchom z eskalacją.
- `candidate.candidate.txt` w nazwie jest kosmetyczne; ważne, aby `bricscad-job.json` wskazywał istniejący plik.
- Szerokie reguły promptu mogą naprawić jedne testy i zepsuć inne. Preferuj małe iteracje.
