---
name: test-bricscad-tool
description: Użyj tej umiejętności, aby wygenerować kod testujący (Unit Tests / Integration Tests) dla nowo przeportowanych narzędzi BricsCAD V2 (np. walidacja ToolSchema i deserializacji JSON).
---
# Instrukcja Tworzenia Testów BricsCAD V2

Po zaimplementowaniu nowego narzędzia lub komponentu (np. `IToolV2`), masz obowiązek napisać dla niego testy. Ponieważ API BricsCAD (Teigha) jest trudne do testowania poza głównym procesem aplikacji, testy dzielimy na dwie kategorie.

## 1. Co MUSISZ testować (Logika Tool Calling)
Każde narzędzie w folderze `src/Tools/` musi posiadać odpowiadającą mu klasę testową (np. używając NUnit lub xUnit), która weryfikuje:
- **Poprawność JSON Schema:** Czy metoda `GetToolSchema()` zwraca poprawny i kompletny obiekt, który LLM zrozumie? Czy wszystkie właściwości `Required` są poprawnie oznaczone?
- **Deserializacja parametrów:** Stwórz sztuczny obiekt JSON (odpowiadający odpowiedzi LLM `tool_calls.arguments`) i przekaż go do początkowej logiki narzędzia. Sprawdź, czy narzędzie poprawnie odczytuje argumenty (np. czy nie wyrzuca wyjątku, gdy brakuje opcjonalnego parametru, jak `Color`).

## 2. Czego NIE MUSISZ testować (Silnik CAD)
- **Nie próbuj** mockować głębokich transakcji BricsCAD (`Transaction`, `OpenMode.ForWrite`, modyfikacja DWG) w zwykłych testach jednostkowych. To spowoduje błędy NullReferenceException. 
- Głęboka logika CAD będzie testowana za pomocą wewnętrznych skryptów w BricsCADzie (tak jak w pliku `@Bricscad_AgentAI/AutoBenchmark.cs` z wersji V1). Do metody `Execute(Document doc, ...)` w teście możesz przekazać po prostu `null` dla parametru `doc` i upewnić się tylko, że kod nie crashuje się przed etapem interakcji z BricsCADem.

## 3. Lokalizacja Testów
- Kod testów zapisuj w osobnym folderze wewnątrz projektu V2: `Bricscad_AgentAI_V2/tests/` (np. `Bricscad_AgentAI_V2/tests/Tools/ManageLayersToolTests.cs`).    