# Bricscad Agent AI V2 - Logi Pamięci

## Wstęp
Ten dokument służy jako zewnętrzna pamięć długotrwała dla modelu AI. Zawiera historię zmian, kluczowe decyzje architektoniczne oraz napotkane błędy.

## Historia Wersji (Log Zmian)
- v2.0.0: Inicjalna migracja do Function Calling (IToolV2).
- v2.1.0: Dodanie mechanizmu ReAct w LLMClient.
- v2.5.0: Implementacja `ForeachTool` i `RpnCalculator`.
- v2.6.0: Rozbudowa systemu Benchmarkingowego i UI Testera.
- v2.7.10 GOLD: Implementacja twardych Guardrails w `CreateObjectTool.cs`.
- v2.8.0 GOLD: Przejście na dynamiczną konfigurację narzędzi (`tools_config.json`, `ToolConfigManager`).
    - Usunięcie właściwości `ToolTags` z `IToolV2` i wszystkich narzędzi (21 plików).
    - Implementacja wzorca "AI Package Manager" w `RequestAdditionalToolsTool` (ListCategories/LoadCategory).
    - Dodanie zakładki "Tagi" w `AgentControl.cs` (UI do edycji `IsCore` i `Tags`).
    - Dynamiczne filtrowanie narzędzi w `ToolOrchestrator` na podstawie JSON.
- 2026-04-05: v2.6.8 GOLD [FOREACH+ SEQ] - Implementacja Sequence Generator w ForeachTool.cs, rozszerzenie ToolParameter o nested properties/items, testy i dokumentacja.
- 2026-04-05: v2.6.7 GOLD [BENCHMARK+ LGC] - Naprawa błędu LINQ w AutoBenchmarkEngine (ArgumentMatch), odblokowanie RecordedToolCalls w JSON.
- 2026-04-05: v2.6.6 GOLD [UI HOTFIX] - Rozdzielono etykiety HUD (lblStatus/lblStats), całkowity refaktoring AgentTesterControl (SplitContainer, JSON V1).
- v2.9.0 GOLD [EARLY EXIT] - Implementacja mechanizmu Client-Side Resolution (Tryb Szybki), przerywającego pętlę ReAct po udanych akcjach fizycznych (Create/Modify).
- v2.9.2 GOLD [FIX TOOL POOL] - Rozwiązanie problemu "Spirali Śmierci" (mismatch nazw API vs C#) i uodpornienie ładowania narzędzi #core w ToolOrchestrator.
- v2.10.0 GOLD [DATASET STUDIO] - Implementacja modułu Dataset Studio (Data Flywheel) do zbierania danych treningowych .jsonl. Refaktoring statystyk LLM na jednolity model LLMStats.
- v2.10.1 GOLD [BUILD HOTFIX] - Naprawa błędów kompilacji (CS1501, CS0246, CS0105) oraz czyszczenie nieużywanych pól w UI (CS0169).
- v2.10.2 GOLD [DATASET UX] - Naprawa ścieżki zapisu JSONL (Brak Uprawnień) oraz poprawki UX w Dataset Studio (formatowanie czasu ms -> s, czytelne etykiety).
- v2.10.3 GOLD [DOC SYNC] - Pełna synchronizacja System_Blueprint.md oraz dokumentacji w folderze /docs z aktualnym stanem V2.10.x.
- v2.11.0 GOLD [CONTEXT SLICER] - Implementacja inteligentnej "Krajalnicy" (Context Slicer) w Dataset Studio. Rozwiązanie problemu Context Poisoning przez izolację turnów (System + Last User + Responses). Głęboka kopia historii konwersacji w UI. Synchronizacja dokumentacji.
- v2.11.1 GOLD [TOOLS IN JSONL] - Dodanie tablicy "tools" do eksportu JSONL w Dataset Studio. Pełna zgodność z formatem OpenAI Fine-tuning dla Tool Calling.
- v2.11.2 GOLD [UI PERSISTENCE] - Naprawa układu Dataset Studio (widoczność statystyk, kolejność DockStyle.Fill). Implementacja UISettingsManager do trwałego zapamiętywania pozycji splittera (ui_settings.json).
- v2.11.3 GOLD [RPN UNIT STRIP] - Naprawa błędu double.TryParse w CreateObjectTool.cs. Wstrzyknięcie komend RPN (#UNITL CONVE UVAL) celem normalizacji wyników przed konwersją na typ numeryczny.
- v2.11.4 GOLD [RPN SMART SCALE] - Hotfix błędu rzutowania jednostek. Dodano inteligentne sprawdzanie Regex w CreateObjectTool.cs – konwersja do jednostek dokumentu zachodzi tylko wtedy, gdy wynik RPN zawiera sygnaturę literową (jednostkę). Zapobiega to błędnemu skalowaniu gołych współrzędnych.
- v2.11.5 GOLD [FOREACH INDEX] - Dodanie obsługi tagu {index} w ForeachTool.cs. Umożliwia to generowanie sekwencyjnego nazewnictwa (np. "Oś 1", "Oś 2") podczas operacji w pętli. Licznik iteracji startuje od 1.
- v2.11.6 GOLD [PROMPT EXPANSION] - Rozbudowa System Promptu w AgentControl.cs o instrukcje dla RPN (CONCAT, IFTE) oraz formatowanie nowej linii (\P) dla MText. Poprawia to zdolność modelu do generowania dynamicznych tekstów w pętlach.
- v2.11.7 GOLD [COLOR MAP] - Wstrzyknięcie mapy kolorów ACI (AutoCAD Color Index) oraz instrukcji TrueColor (RGB) do promptu systemowego. Ułatwia to modelowi poprawne wyszukiwanie i zamianę kolorów w rysunku.
- v2.11.8 GOLD [RGB SELECT] - Refaktoryzacja wydobywania kolorów w SelectEntitiesTool.cs. Wprowadzono pełną obsługę formatu RGB ("R,G,B") dla TrueColor oraz poprawne rzutowanie kolorów dziedziczonych z warstw, co umożliwia precyzyjne filtrowanie selekcji po kolorach innych niż ACI.

## Decyzje Architektoniczne
- **Semantic Tool Routing**: System dynamicznego dobierania narzędzi na podstawie tagów (#core, #bloki, itp.). Od v2.8.0 zarządzany przez `ToolConfigManager`.
- **Early Exit (Fast Mode)**: Mechanizm pozwalający Agentowi na zakończenie pętli po wykonaniu narzędzi akcji, jeśli wspierają one flagę `SupportsEarlyExit`. Drastyczna redukcja tokenów i czasu odpowiedzi.
- **AI Package Manager**: Model LLM samodzielnie odkrywa i ładuje pakiety narzędzi przez `RequestAdditionalToolsTool`.
- **Hard Guardrails**: Każde narzędzie jest odpowiedzialne za walidację swoich parametrów i zwracanie "Błędu Krytycznego" w celu przerwania halucynacji LLM.

## Rozwiązane Problemy (Bug Log)
- **UI Autocomplete**: Naprawiono przechwytywanie klawiszy Tab/Enter przez migrację do `ProcessCmdKey` w `AgentControl.cs`.
- **Build CS0111/CS0103**: Naprawiono błędy kompilacji po masowej refaktoryzacji (dodanie plików do .csproj oraz usunięcie duplikatu klasy w UserInputTool.cs).
- **Silent Name Mismatch (Death Spiral)**: Naprawiono błąd w v2.9.1, gdzie klucze `ToolConfigManager` korzystały z nazw klas C# zamiast API Names z `FunctionSchema`, co unieruchamiało mechanizm Early Exit i gubiło narzędzia #core.
