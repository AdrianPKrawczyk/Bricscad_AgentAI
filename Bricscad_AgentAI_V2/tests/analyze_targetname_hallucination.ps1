#requires -Version 5.0
<#
.SYNOPSIS
    Szczegolowa analiza halucynacji TargetName w raportach benchmarku.

.DESCRIPTION
    Analizuje wszystkie raporty benchmarku i wykrywa przypadki halucynacji
    parametru 'TargetName' (zamiast oczekiwanego 'BlockName') w wywolaniach
    EditBlock. Generuje statystyki per model i per kategoria.

.PARAMETER BenchmarkPattern
    Wzorzec nazwy pliku benchmarku (np. "Benchmark_07*").

.PARAMETER GroupByModel
    Grupuj wyniki per model (domyslnie true).

.EXAMPLE
    .\analyze_targetname_hallucination.ps1
    .\analyze_targetname_hallucination.ps1 -BenchmarkPattern "Benchmark_07*"
#>
param(
    [string]$BenchmarkPattern = "Benchmark_07*",
    [bool]$GroupByModel = $true
)

$ErrorActionPreference = "Stop"
$testsDir = Join-Path (Get-Location) "Bricscad_AgentAI_V2\tests"

if (-not (Test-Path -LiteralPath $testsDir)) {
    Write-Error "Nie znaleziono katalogu: $testsDir"
}

$reports = Get-ChildItem -LiteralPath $testsDir -Recurse -Filter "$BenchmarkPattern*FULL*.json" -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -gt (Get-Date).AddDays(-1) } |
    Sort-Object LastWriteTime -Descending

if ($reports.Count -eq 0) {
    Write-Host "[BRAK] Nie znaleziono raportow z ostatnich 24h dla wzorca: $BenchmarkPattern" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "=== ANALIZA HALUCYNACJI TargetName w raportach benchmarku ===" -ForegroundColor Cyan
Write-Host "Wzorzec: $BenchmarkPattern"
Write-Host "Raportow z ostatnich 24h: $($reports.Count)"
Write-Host ""

$globalStats = @{
    TotalReports = 0
    TotalTests = 0
    TestsWithHallucination = 0
    HallucinationsByModel = @{}
    HallucinationsByTest = @{}
    HallucinationsByCategory = @{}
}

foreach ($report in $reports) {
    $content = Get-Content -LiteralPath $report.FullName -Raw -Encoding UTF8
    try {
        $json = $content | ConvertFrom-Json
    } catch {
        Write-Host "[ERR] $($report.Name) - nieprawidlowy JSON, pomijam" -ForegroundColor Red
        continue
    }

    $model = $json.RunMetadata.ModelName
    $profile = $json.RunMetadata.ProfileName
    $score = $json.RunMetadata.GlobalScore
    $date = $json.RunMetadata.RunDate

    $globalStats.TotalReports++

    if (-not $globalStats.HallucinationsByModel.ContainsKey($model)) {
        $globalStats.HallucinationsByModel[$model] = @{
            Reports = 0
            Tests = 0
            WithHallucination = 0
            TestsByNumber = @{}
        }
    }
    $modelStats = $globalStats.HallucinationsByModel[$model]
    $modelStats.Reports++

    foreach ($test in $json.Tests) {
        $globalStats.TotalTests++
        $modelStats.Tests++

        $hasHallucination = $false
        $toolCalls = $test.RecordedToolCalls
        if ($null -ne $toolCalls) {
            foreach ($call in $toolCalls) {
                if ($null -ne $call.Arguments -and
                    $call.Arguments.PSObject.Properties.Name -contains "TargetName") {
                    $hasHallucination = $true
                    break
                }
            }
        }

        if ($hasHallucination) {
            $globalStats.TestsWithHallucination++
            $modelStats.WithHallucination++

            $testName = $test.TestName
            if (-not $globalStats.HallucinationsByTest.ContainsKey($testName)) {
                $globalStats.HallucinationsByTest[$testName] = 0
            }
            $globalStats.HallucinationsByTest[$testName]++

            $category = $test.Category
            if (-not $globalStats.HallucinationsByCategory.ContainsKey($category)) {
                $globalStats.HallucinationsByCategory[$category] = 0
            }
            $globalStats.HallucinationsByCategory[$category]++

            $testNumber = $test.Id
            if (-not $modelStats.TestsByNumber.ContainsKey($testNumber)) {
                $modelStats.TestsByNumber[$testNumber] = 0
            }
            $modelStats.TestsByNumber[$testNumber]++
        }
    }
}

# Raport glowny
Write-Host "=== PODSUMOWANIE ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Lacznie raportow: $($globalStats.TotalReports)"
Write-Host "Lacznie testow: $($globalStats.TotalTests)"
Write-Host "Testow z halucynacja TargetName: $($globalStats.TestsWithHallucination)" `
    -ForegroundColor $(if ($globalStats.TestsWithHallucination -gt 0) { "Red" } else { "Green" })
$hallucPct = if ($globalStats.TotalTests -gt 0) {
    [math]::Round(($globalStats.TestsWithHallucination / $globalStats.TotalTests) * 100, 1)
} else { 0 }
Write-Host "Procent halucynacji: $hallucPct%"
Write-Host ""

# Per model
Write-Host "=== PER MODEL ===" -ForegroundColor Cyan
foreach ($model in ($globalStats.HallucinationsByModel.Keys | Sort-Object)) {
    $ms = $globalStats.HallucinationsByModel[$model]
    $pct = if ($ms.Tests -gt 0) {
        [math]::Round(($ms.WithHallucination / $ms.Tests) * 100, 1)
    } else { 0 }
    $color = if ($pct -gt 50) { "Red" } elseif ($pct -gt 10) { "Yellow" } else { "Green" }
    Write-Host ""
    Write-Host "Model: $model" -ForegroundColor White
    Write-Host "  Raportow: $($ms.Reports), testow: $($ms.Tests)"
    Write-Host "  Halucynacji: $($ms.WithHallucination) ($pct%)" -ForegroundColor $color
    if ($ms.TestsByNumber.Count -gt 0) {
        Write-Host "  Testy z halucynacja (po numerze):"
        $sortedTests = $ms.TestsByNumber.Keys | Sort-Object
        foreach ($tn in $sortedTests) {
            $count = $ms.TestsByNumber[$tn]
            Write-Host "    Test #$tn : $count razy" -ForegroundColor Yellow
        }
    }
}

Write-Host ""
Write-Host "=== PER KATEGORIA ===" -ForegroundColor Cyan
foreach ($cat in ($globalStats.HallucinationsByCategory.Keys | Sort-Object)) {
    $count = $globalStats.HallucinationsByCategory[$cat]
    Write-Host "  $cat : $count halucynacji" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== PER TEST (TOP) ===" -ForegroundColor Cyan
$topTests = $globalStats.HallucinationsByTest.Keys | Sort-Object { $globalStats.HallucinationsByTest[$_] } -Descending | Select-Object -First 10
foreach ($tn in $topTests) {
    $count = $globalStats.HallucinationsByTest[$tn]
    Write-Host "  $tn : $count halucynacji" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== WNIOSEK ===" -ForegroundColor Cyan
if ($globalStats.TestsWithHallucination -eq 0) {
    Write-Host "Brak halucynacji TargetName w analizowanych raportach." -ForegroundColor Green
} else {
    $hallucModels = $globalStats.HallucinationsByModel.GetEnumerator() | Where-Object { $_.Value.WithHallucination -gt 0 }
    if ($hallucModels.Count -eq 1) {
        Write-Host "Halucynacja TargetName dotyczy TYLKO modelu: $($hallucModels[0].Key)" -ForegroundColor Yellow
        Write-Host "Pozostale modele nie wykazuja tego problemu." -ForegroundColor Green
    } else {
        Write-Host "Halucynacja TargetName dotyczy $($hallucModels.Count) modeli." -ForegroundColor Red
    }

    if ($globalStats.HallucinationsByTest.Count -gt 0 -and $globalStats.HallucinationsByTest.Count -le 5) {
        Write-Host "Halucynacja ograniczona do $($globalStats.HallucinationsByTest.Count) testow - mozliwe do precyzyjnego prompt-fix." -ForegroundColor Yellow
    } elseif ($globalStats.HallucinationsByTest.Count -gt 5) {
        Write-Host "Halucynacja w $($globalStats.HallucinationsByTest.Count) roznych testach - problem systemowy, wymaga walidacji w C#." -ForegroundColor Red
    }
}
