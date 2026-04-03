---
name: project-compiler
description: Obsługa kompilacji projektu C# przez terminal Bash. Używaj po każdej zmianie w kodzie, aby zweryfikować poprawność składniową.
---
# Instrukcja Kompilacji V2

Twoim zadaniem jest zapewnienie, że kod w folderze `Bricscad_AgentAI_V2` zawsze się kompiluje.

## 1. Komenda kompilacji
Używaj nowoczesnego interfejsu .NET CLI:
`dotnet build Bricscad_AgentAI_V2/src/Bricscad_AgentAI_V2.csproj`

## 2. Obsługa błędów (Loop-to-Fix)
Jeśli kompilacja zwróci błąd (Exit Code != 0):
1. Przeanalizuj wyjście z terminala (Error List).
2. Zlokalizuj plik i linię błędu.
3. Napraw błąd (np. brakujący namespace, literówka w metodzie BricsCAD API).
4. Ponów kompilację aż do uzyskania statusu "Build succeeded".

## 3. Ograniczenia środowiska
Jeśli komenda `dotnet` nie jest dostępna, spróbuj zlokalizować `msbuild.exe` używając komendy `find` lub zapytaj użytkownika o ścieżkę do zmiennych środowiskowych Visual Studio.