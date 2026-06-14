---
name: project-compiler
description: Obsługa kompilacji projektu C# przez terminal Bash. Używaj po każdej zmianie w kodzie, aby zweryfikować poprawność składniową.
---
# Instrukcja Kompilacji V2

Twoim zadaniem jest zapewnienie, że kod w folderze `Bricscad_AgentAI_V2` zawsze się kompiluje.

## 1. Komenda kompilacji
Używaj utworzonego skryptu `build.ps1` zlokalizowanego w katalogu głównym projektu (np. `Bricscad_AgentAI_V2/build.ps1`):
`powershell -ExecutionPolicy Bypass -File build.ps1`

**Uwaga krytyczna:** Polecenie `dotnet build` użyte w tym środowisku w kontekście .NET Framework 4.8 posiada błąd: gdy składnia jest poprawna, kompilator często cicho failuje na rozwiązywaniu referencji NuGet (`CS0246` dla Newtonsoft.Json itp.). `build.ps1` automatycznie odnajduje prawdziwy `MSBuild.exe` dla Visual Studio i wykonuje prawidłowy build.

## 2. Obsługa błędów (Loop-to-Fix)
Jeśli kompilacja zwróci błąd (Exit Code != 0):
1. Przeanalizuj wyjście z terminala PowerShell.
2. Zlokalizuj plik i linię błędu (np. `CS1513`, `CS0103`).
3. Napraw błąd w kodzie źródłowym.
4. Ponów uruchomienie `build.ps1` aż do uzyskania statusu "Kompilacja powiodła się. Liczba błędów: 0".

## 3. Ograniczenia środowiska
Nie używaj `dotnet build` dla solucji `Bricscad_AgentAI_V2.sln`. Jeśli `build.ps1` zawiedzie, upewnij się, że użyto `vswhere.exe` do zlokalizowania `MSBuild.exe`, tak jak jest to zaimplementowane w skrypcie.