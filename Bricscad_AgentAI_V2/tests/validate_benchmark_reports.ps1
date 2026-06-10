#requires -Version 5.0
<#
.SYNOPSIS
    Waliduje raporty benchmarku - sprawdza profil, wynik, halucynacje parametrow.

.DESCRIPTION
    Skrypt diagnostyczny do szybkiej weryfikacji raportow benchmarku V2.
    Domyslnie analizuje Benchmark_07_EditBlock.

.PARAMETER BenchmarkPattern
    Wzorzec nazwy pliku benchmarku (np. "Benchmark_07*", "Benchmark_06*").

.PARAMETER ExpectedProfile
    Oczekiwany profil w raporcie (np. "CadBlocksProfile"). Jesli pusty - nie sprawdza.

.PARAMETER TopN
    Liczba najnowszych raportow do analizy (domyslnie 5).

.EXAMPLE
    .\validate_benchmark_reports.ps1
    .\validate_benchmark_reports.ps1 -BenchmarkPattern "Benchmark_07*"
    .\validate_benchmark_reports.ps1 -ExpectedProfile "CadBlocksProfile" -TopN 10
#>
param(
    [string]$BenchmarkPattern = "Benchmark_07*",
    [string]$ExpectedProfile = "",
    [int]$TopN = 5
)

$ErrorActionPreference = "Stop"
$testsDir = Join-Path (Get-Location) "Bricscad_AgentAI_V2\tests"

if (-not (Test-Path -LiteralPath $testsDir)) {
    Write-Error "Nie znaleziono katalogu: $testsDir"
}

$reports = Get-ChildItem -LiteralPath $testsDir -Recurse -Filter "$BenchmarkPattern*FULL*.json" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First $TopN

if ($reports.Count -eq 0) {
    Write-Host "[BRAK] Nie znaleziono raportow pasujacych do wzorca: $BenchmarkPattern" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "=== DIAGNOSTYKA RAPORTOW BENCHMARKU ===" -ForegroundColor Cyan
Write-Host "Wzorzec: $BenchmarkPattern"
Write-Host "Oczekiwany profil: $(if ($ExpectedProfile) { $ExpectedProfile } else { '(dowolny)' })"
Write-Host "Raportow do analizy: $($reports.Count)"
Write-Host ""

$allOk = $true
foreach ($report in $reports) {
    $content = Get-Content -LiteralPath $report.FullName -Raw -Encoding UTF8
    try {
        $json = $content | ConvertFrom-Json
    } catch {
        Write-Host "[ERR] $($report.Name) - nieprawidlowy JSON" -ForegroundColor Red
        $allOk = $false
        continue
    }

    $bm = $json.RunMetadata.BenchmarkName
    $model = $json.RunMetadata.ModelName
    $profile = $json.RunMetadata.ProfileName
    $score = $json.RunMetadata.GlobalScore
    $date = $json.RunMetadata.RunDate

    $profileOk = $true
    $profileColor = "Green"
    if ($ExpectedProfile -and $profile -ne $ExpectedProfile) {
        $profileOk = $false
        $profileColor = "Red"
        $allOk = $false
    } elseif (-not $profile) {
        $profileColor = "Yellow"
    }

    # Sprawdz halucynacje TargetName
    $hasHallucination = $false
    foreach ($test in $json.Tests) {
        foreach ($call in $test.RecordedToolCalls) {
            if ($call.Arguments.PSObject.Properties.Name -contains "TargetName") {
                $hasHallucination = $true
                break
            }
        }
        if ($hasHallucination) { break }
    }

    $hallucColor = if ($hasHallucination) { "Yellow" } else { "Green" }
    $hallucLabel = if ($hasHallucination) { "HALUCYNACJA" } else { "OK" }

    $statusColor = "Green"
    if ($score -lt 100) { $statusColor = "Yellow" }
    if ($score -lt 80) { $statusColor = "Red" }

    $line = "  {0:HH:mm}  {1}  Model={2}  Profile={3}  Score={4}%  Hallucination={5}" -f `
        $report.LastWriteTime, $date, $model, $profile, $score, $hallucLabel

    Write-Host $line -ForegroundColor $statusColor
    if (-not $profileOk) {
        Write-Host "         [UWAGA] Oczekiwany profil '$ExpectedProfile', ale raport ma '$profile'" -ForegroundColor $profileColor
    }
    if ($hasHallucination) {
        Write-Host "         [UWAGA] Wykryto parametr 'TargetName' - mozliwa halucynacja modelu" -ForegroundColor $hallucColor
    }
}

Write-Host ""
if ($allOk) {
    Write-Host "[OK] Wszystkie raporty zgodne z oczekiwanym profilem." -ForegroundColor Green
    exit 0
} else {
    Write-Host "[UWAGA] Wykryto niezgodnosci - sprawdz szczegoly powyzej." -ForegroundColor Yellow
    exit 2
}
