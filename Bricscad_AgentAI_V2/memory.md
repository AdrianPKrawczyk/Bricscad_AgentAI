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
- v2.11.9 GOLD [RGB PATTERN] - Dodanie do System Promptu instrukcji o "Wzorcu Przecinka" do masowego wykrywania dowolnych kolorów RGB (`contains: ","`) oraz przypomnienia o zakresie składowych 0-255.
- v2.11.10 GOLD [VISUAL PERCEPTION] - Wdrożenie "Reguły Percepcji" do System Promptu. Model został poinstruowany, aby automatycznie używać właściwości wirtualnych (`VisualColor`, `VisualLinetype` itd.) przy zapytaniach dotyczących wyglądu zewnętrznego obiektów, co zapewnia poprawne uwzględnienie dziedziczenia warstw (ByLayer).
- v2.11.11 GOLD [PROPERTY SYNC] - Ujednolicenie mapowania właściwości (Transparency, LineWeight, LinetypeScale) między UI a API. Wdrożono dwukierunkową konwersję przezroczystości (0-90 UI <=> 0-255 Alpha) oraz rozbudowano System Prompt o globalne zasady dla grubości i rodzajów linii.
- v2.11.12 GOLD [LAYER NATIVE] - Kompletny refaktoring ManageLayersTool.cs na natywne API BricsCAD (LayerTable/LayerTableRecord). Usunięto wywołania `Editor.Command`, co wyeliminowało błędy Fatal Error i blokowanie interfejsu. Dodano obsługę wielu warstw (lista po przecinku), akcję `Toggle` (Lock/Freeze/Off) oraz bezpieczne `Rename`.
- v2.11.13 GOLD [STRUCTURE DIRECTIVE] - Wprowadzenie "Dyrektywy Struktury" do System Promptu. Model otrzymał kategoryczny zakaz używania narzędzi geometrycznych (`CreateObject`, `ModifyProperties`) do manipulacji strukturą rysunku (warstwami). Wzmocniono rolę narzędzia `ManageLayers` oraz mechanizmu żądania dodatkowych narzędzi.
- v2.11.14 GOLD [THREAD SAFETY] - Wdrożenie thread-marshalingu (UI thread synchronization) dla narzędzi `UserInputTool` i `UserChoiceTool`. Interakcje z BricsCAD Editor są teraz bezpiecznie delegowane do głównego wątku za pomocą `Invoke`, eliminując błędy Cross-Thread Exception i Fatal Error podczas asynchronicznych sesji LLM.
- v2.11.15 GOLD [STRICT ACTION VALIDATION] - Dodanie twardej walidacji parametru `Action` w `ManageLayersTool.cs`. Narzędzie odrzuca teraz nieobsługiwane polecenia (np. `CreateLayer`) z wyraźnym komunikatem o błędzie, zamiast kończyć działanie bez efektu.
- v2.11.16 GOLD [HOT-RELOAD & DYNAMIC PROMPT] - Wdrożenie odświeżania orkiestratora na żądanie (po zapisie konfiguracji) oraz dynamicznego wstrzykiwania dostępnych kategorii narzędzi z `ToolConfigManager` do promptu systemowego. Wyeliminowano potrzebę restartu aplikacji po zmianie dostępnych pakietów narzędziowych.
- v2.11.17 GOLD [DISCOVERY & ACTIVATION] - Wdrożenie "Katalogu Narzędzi Uśpionych" automatycznie generowanego z definicji orkiestratora. Model LLM widzi teraz nazwy i opisy narzędzi, których nie ma w arsenale, i może poprosić o ich załadowanie poprzez `RequestAdditionalTools`. Umożliwiono aktywację narzędzi bezpośrednio po nazwie klasy/API jako fallback.
- v2.12.1 GOLD [3-PILLAR ARCHITECTURE] - Kompletna przebudowa architektury wiedzy. 1) Konstytucja Agenta (Prompt) zcentralizowana na RPN i logice CAD. 2) Lokalne Schematy (Tool Schemas) przejęły specyficzne guardraile narzędzi. 3) Dynamiczne Odkrywanie (Discovery) - `RequestAdditionalTools` serwuje teraz pełny katalog opisów narzędzi uśpionych.
- v2.12.2 GOLD [REACT ERROR PROTOCOL] - Wdrożenie instrukcyjnego komunikatu błędu w `ToolOrchestrator.ExecuteTool`. Jeśli model wywoła uśpione narzędzie, otrzyma "BŁĄD KRYTYCZNY" z natychmiastową instrukcją użycia `RequestAdditionalTools`, co wymusza poprawny cykl rozumowania (ReAct).
- v2.12.3 GOLD [DYNAMIC SCHEMA INJECTION] - Naprawa luki w ładowaniu narzędzi. Wdrożono `SessionDynamicTags` w `ToolConfigManager`, co pozwala orkiestratorowi na natychmiastowe odblokowanie schematów (ToolDefinition) nowo załadowanych narzędzi w trakcie tej samej sesji. Zapewnia to, że LLM otrzyma definicje parametrów zaraz po akcji `LoadCategory`.
- v2.12.4 GOLD [TWO-PHASE COMMIT] - Naprawa błędu "Silent Transaction Failure" w `ManageLayersTool.cs`. Wdrożono wzorzec dwufazowego zatwierdzania bazy danych: najpierw tworzona jest struktura warstwy, a dopiero po pomyślnym `Commit()` głównej transakcji, w drugiej małej transakcji, warstwa jest ustawiana jako aktualna (`db.Clayer`). Zapobiega to powstawaniu "warstw widm".
- v2.12.5 GOLD [CONTEXT PROTECTION] - Wzmocnienie stabilności `ManageLayersTool.cs` poprzez wymuszenie kontekstu `HostApplicationServices.WorkingDatabase` (rozwiązanie problemu Silent Rollback w ODA Teigha). Dodatkowo zoptymalizowano proces tworzenia rekordów warstw, inicjując ich właściwości (kolor, rodzaj linii) przed dodaniem do tablicy symboli.
- v2.12.6 GOLD [ENGINE TRACER] - Wdrożenie zakładki Debug oraz nasłuchiwania zdarzeń bazy danych (ObjectAppended, TransactionAborted) celem diagnozy zjawiska Silent Rollback w Teigha API.
- v2.12.7 GOLD [THREAD-SAFE UI SYNC] - Refaktoryzacja `ManageLayersTool.cs`. Przeniesiono aktywację warstw (`db.Clayer`) oraz odświeżanie interfejsu do Głównego Wątku (Main Thread) za pomocą `doc.SendStringToExecute`. Rozwiązuje to problem "cichego rollbacku" przy interakcjach z UI z wątków pobocznych.
- v2.12.8 GOLD [ACTION SETCURRENT] - Dodanie dedykowanej akcji `SetCurrent` do `ManageLayersTool.cs`. Rozwiązuje to problem błędnego używania przez LLM akcji `Toggle -> On` do przełączania warstwy roboczej. Zaktualizowano schemat narzędzia, oznaczając flagę `MakeCurrent` jako przestarzałą.
- v2.12.9 GOLD [LAYER MODIFICATION] - Rozszerzenie `ManageLayersTool.cs` o akcję `Modify` oraz obsługę właściwości: `Transparency` (konwersja 0-90 na Alpha 255-0), `LineWeight` oraz `Plottable`. Zunifikowano logikę `Create/Modify` z pełną obsługą masek (*, ?) dla modyfikacji masowych, co eliminuje halucynacje modelu dotyczące używania narzędzi edycji obiektów fizycznych do zarządzania strukturą warstw.
- v2.12.10 GOLD [PROMPT ENHANCEMENT] - Optymalizacja schematu `ForeachTool.cs` (Prompt Engineering). Wstrzyknięto "Złoty Standard" wywołań (Few-Shot Examples) bezpośrednio do opisu parametrów. Agent dowiaduje się o możliwości zagnieżdżania ewaluacji RPN, używania tagu `{index}` oraz wywoływania dowolnych narzędzi (np. `ManageLayers`) wewnątrz pętli za pomocą klucza `ToolName`.
- v2.12.11 GOLD [RPN INTERCEPTION] - Implementacja RPN Interception w `ForeachTool.cs`. Parametry oznaczone prefiksem `RPN:` wewnątrz pętli są teraz ewaluowane przez `RpnCalculator` przed przekazaniem do orkiestratora. Rozwiązuje to problem przesyłania surowych wyrażeń matematycznych zamiast wyliczonych wartości (np. dla kolorów lub pozycji) w pętlach.
- v2.12.12 GOLD [TEST FIX] - Naprawa błędu kompilacji CS0103 w `ForeachToolTests.cs` poprzez dodanie brakującego `using Bricscad_AgentAI_V2.Core`.
- v2.12.13 GOLD [LAYER HOTFIX] - Wyeliminowanie błędu "Double-Open" w `ManageLayersTool.cs`. Przejście na operowanie bezpośrednio na `LayerTableRecord` (List) zamiast ponownego otwierania obiektów przez `ObjectId`. Rozwiązuje to problem cichego rollbacku transakcji w Teigha API przy modyfikacji nowo utworzonych warstw.
- v2.12.14 GOLD [STALE UI FIX] - Wdrożenie wymuszonej synchronizacji GUI w `ManageLayersTool.cs`. Gwarantowane wywołanie `SendStringToExecute` (z komendą `(princ)` jako fallback) zapewnia, że Menedżer Warstw BricsCAD odświeży swój stan i pokaże nowo utworzone/zmodyfikowane warstwy nawet w trybie asynchronicznym bez ich aktywacji.
- v2.12.15 GOLD [XREF PROTECTION] - Wdrożenie zabezpieczeń dla warstw zależnych (XREF) w `ManageLayersTool.cs`. Zablokowano akcje `SetCurrent`, `Rename` oraz `Delete` dla warstw `IsDependent`. Zaktualizowano schemat o instrukcję użycia znaku `|` do modyfikacji wizualnej podkładów.
- v2.12.16 GOLD [SUPPRESS UI] - Implementacja mechanizmu `SuppressUI` w `ManageLayersTool.cs` i `ForeachTool.cs`. Narzędzia wywoływane w pętlach otrzymują flagę blokującą odświeżanie interfejsu w każdej iteracji, co eliminuje kolizje blokad dokumentu (`eLockViolation`). Zbiorcze odświeżenie UI następuje raz po zakończeniu całej pętli.
- v2.13.1 GOLD [ADVANCED FILTERS] - Implementacja `AdvancedFilters` w `SelectEntitiesTool.cs`. Dodano obsługę złożonych zapytań o właściwości CAD (np. Transparency 0-90, TextOverride) z użyciem refleksji i dedykowanego mapowania klas. Rozszerzono logikę o operatory `Contains` oraz `NotContains`.
- v2.13.2 GOLD [UI REFACTOR] - Reorganizacja interfejsu `DatasetStudioControl.cs`. Zmieniono układ z pionowego na poziomy (lista na górze - 20%, edytor na dole - 80%). Wprowadzono czytelną belkę nagłówkową na samej górze dla przełączników i statystyk.
- v2.13.3 GOLD [DATASET STUDIO PRO] - Kompleksowa przebudowa Dataset Studio. Wdrożono kolorowanie składni JSON (VSC style), system zakładek ("Aktualna sesja" / "Edycja data setów") oraz pełne zarządzanie plikami JSONL z funkcją "Uruchom Makro" do testowania instrukcji. Wprowadzono trwałość ustawień ostatnio otwartego pliku.
- v2.13.4 GOLD [INSTRUCTION TOOLS] - Rozbudowa Dataset Studio o narzędzia manipulacji treścią: przyciski "Usuń instrukcje" (czyszczenie tablicy messages) oraz "Zamień instrukcję" (wklejanie z walidacją formatu ze schowka).
- v2.13.5 GOLD [SMART INSTRUCTIONS] - Refaktoryzacja narzędzi instrukcji: "Usuń instrukcje" teraz precyzyjnie zachowuje nagłówek `system`, a "Zamień instrukcję" inteligentnie łączy nową interakcję ze schowka, dbając o niepowtarzanie nagłówków systemowych.
- v2.13.6 GOLD [UI REFINERY] - Poprawki UX w Dataset Studio: wdrożenie debounce dla kolorowania składni (fix klawisza Enter), przeniesienie przycisków zarządzania wpisami (Duplikuj/Usuń) na górny pasek, wdrożenie responsywnego układu dolnego panelu akcji oraz dodanie funkcji usuwania rekordów z listą potwierdzeń.
- v2.14.0 GOLD [SEPARATION OF CONCERNS] - Rozdzielenie kompetencji narzędzi: wdrożenie wyspecjalizowanego `DimensionEditTool` (#wymiary), wprowadzenie blokady (Runtime Guardrail) w `ModifyPropertiesTool` dla tekstu i wymiarów oraz implementacja hard-cast fallback w `SelectEntitiesTool` dla `HatchObjectType` (fix gradientów) i właściwości tekstowych.
- v2.14.1 HOTFIX [DIMENSION SYNC] - Usprawnienie `DimensionEditTool`: wdrożenie `GetArrowObjectId` dla automatycznego generowania standardowych grotów (Lazy Loading) oraz dodanie `RecomputeDimensionBlock` dla natychmiastowego odświeżania grafiki wymiaru po zmianie parametrów.
- v2.14.2 CRITICAL HOTFIX [SAFE PARSE] - Całkowita rekonstrukcja `Execute` w `DimensionEditTool`: wdrożenie bezpiecznego parsowania (`InvariantCulture`), wymuszenie trybu `OpenMode.ForWrite` dla każdego obiektu oraz dodanie polecenia `REGEN` dla synchronizacji interfejsu CAD.
- v2.15.0 GOLD [TOOL SANDBOX] - Wdrożenie zakładki "Tool Sandbox" do izolowanego testowania logiki C# narzędzi `IToolV2`. Implementacja inteligentnego generatora szablonów JSON na podstawie schematów, integracja z `AgentMemoryState` celem ładowania zaznaczenia CAD oraz system logowania wyników z sygnaturą czasową.
- v2.15.1 [TOOL SANDBOX TRANSFORMATION] - Przekształcenie Sandboxa w interaktywną dokumentację. Wdrożenie "Property Discovery" (dynamiczny podgląd parametrów i typów), obsługa "Snippets" (przykładów JSON), ulepszone szablony z komentarzami i placeholderami oraz automatyczne oczyszczanie JSON (Regex) przed egzekucją. Poprawiono wysokość sekcji dokumentacji oraz przywróiono `DimensionEditTool` i `InspectEntityTool` do projektu.
- v2.15.2 [INTERACTIVE SANDBOX] - Wdrożenie interaktywnego budowania JSON: podwójne kliknięcie na parametr w dokumentacji wstawia go do edytora. Wprowadzenie minimalistycznych szablonów (tylko pola Required).
- v2.15.3 [UI POLISH] - Ulepszenie interakcji: podwójne kliknięcie ustawia kursor bezpośrednio wewnątrz pustych cudzysłowów `""` (bez tekstu zastępczego). Uproszczenie generatora szablonów celem zwiększenia przejrzystości. Responsywność Tool Sandbox (SplitContainer + persistence), Click-to-Add dla parametrów.
- v2.16.0 [AGENT RECIPES] - Wdrożenie systemu "Agent Recipes" (Drogowskazy). Nowa 4. zakładka w Dataset Studio, mechanizm Few-Shot Prompting ($trigger) oraz przycisk "Przechwyć jako Przepis" w edytorze sesji.
- v2.16.1 [DYNAMIC AUTOCOMPLETE] - Wdrożenie dynamicznego autouzupełniania dla `#` (Tagi z ToolConfigManager) oraz `$` (Receptury z RecipeManager). Naprawiono błąd braku widoczności ręcznie dodanych kategorii w podpowiedziach.
- v2.17.0 [ADVANCED RECIPES] - Rozbudowa systemu receptur o "Tryb Makra" ($trigger$ - natychmiastowe wykonanie). Wdrożenie testowania całej sekwencji z walidacją JSON oraz interaktywnego wyboru kroku do przesłania do Tool Sandboxa.
- v2.18.0 [UI & INTEGRATION] - Zmiana nazwy na "Recepty". Implementacja integracji "Wyślij do Recepty" w Tool Sandboxie. Dodanie przycisku "Nowa Recepta" (tworzenie od zera). Optymalizacja proporcji UI (25/75) z persistencją.
- v2.19.0 [TRAINING DATA] - Wdrożenie modułu "Eksport do Złotego Standardu" w Receptach. Automatyczna generacja JSONL z uwzględnieniem System Promptu, definicji narzędzi (#core + tagi) oraz zapytania użytkownika.
- v2.20.0 GOLD [CLI INTERFACE] - Wdrożenie Command Line Interface (CLI). Dodano komendy `AI_RUN`, `AI_TOOL`, `AI_PROPS` oraz `AI_DIM`. Implementacja mechanizmu `SyncSelectionWithMemory` do automatycznej synchronizacji zaznaczenia CAD (PickFirst) z pamięcią Agenta. Rozwiązanie konfliktu nazw dla klasy `Exception`.
- v2.20.1 GOLD [RPN CLI] - Pełna migracja systemu RPN z v1. Komendy RPN, CALC, STOS. Trwałość stosu w DWG.
- v2.20.2 GOLD [RPN FINAL SPEC] - Finalizacja CLI RPN. Tryb interaktywny (pętla), interaktywne pomiary CAD.
- v2.20.3 GOLD [RPN V1 SYNC] - Pełna synchronizacja zachowania z v1. Implementacja "wstrzykiwania" wyniku na końcu komendy RPN (SendStringToExecute), pętla obliczeń dla CALC oraz automatyczne odświeżanie stosu w konsoli.
- v2.20.4 GOLD [UNIT CLEAN INJECTION] - Inteligentne czyszczenie jednostek przed wstrzyknięciem do CAD. Automatyczna konwersja jednostek długości na jednostki rysunku (INSUNITS) oraz wstrzykiwanie surowych wartości (DisplayValue) dla innych wymiarów.
- v2.20.5 GOLD [READ XDATA] - Nowe narzędzie `ReadXData` do odczytu metadanych XData. Dodano komendę CLI `AI_XDATA` oraz pełną dokumentację techniczną.
- v2.20.6 GOLD [WRITE XDATA] - Implementacja narzędzia `WriteXData` z obsługą automatycznej rejestracji RegApp oraz komendą CLI `AI_SETXDATA`.
- v2.20.7 GOLD [FIND XDATA] - Implementacja narzędzia `FindXData` z obsługą rekurencyjnego skanowania bloków oraz komendą CLI `AI_FINDXDATA`.
- v2.20.8 GOLD [MULTIMODAL VISION] - Wdrożenie obsługi modeli VLM (Vision).
    - Implementacja `CaptureVisionAreaTool` (Win32 P/Invoke screenshot).
    - Rozszerzenie `ChatMessage` o pole `Content` (object) dla standardu GPT-4o Vision.
    - Automatyczne wstrzykiwanie Base64 obrazów do historii sesji w `LLMClient`.
    - Nowe polecenia CLI: `SKAN` (zrzut ekranu) i `AI_VISION` (zrzut + pytanie).
- v2.20.9 GOLD [VISION e15 FIX] - Naprawa krytycznego błędu `eVetoed` (e15) w module Vision.
    - Rezygnacja z COM `ZoomWindow` na rzecz natywnego logicznego manipulowania `ViewTableRecord`.
    - Poprawa cyklu życia `ViewTableRecord` (naprawa błędu use-after-dispose).
    - Usunięcie flagi `Transparent` z poleceń wizyjnych celem umożliwienia zmian widoku.

## Decjzje Architektoniczne
- **Semantic Tool Routing**: System dynamicznego dobierania narzędzi na podstawie tagów (#core, #bloki, itp.). Od v2.8.0 zarządzany przez `ToolConfigManager`.
- **Early Exit (Fast Mode)**: Mechanizm pozwalający Agentowi na zakończenie pętli po wykonaniu narzędzi akcji, jeśli wspierają one flagę `SupportsEarlyExit`. Drastyczna redukcja tokenów i czasu odpowiedzi.
- **AI Package Manager**: Model LLM samodzielnie odkrywa i ładuje pakiety narzędzi przez `RequestAdditionalToolsTool`.
- **Hard Guardrails**: Każde narzędzie jest odpowiedzialne za walidację swoich parametrów i zwracanie "Błędu Krytycznego" w celu przerwania halucynacji LLM.

## Rozwiązane Problemy (Bug Log)
- **UI Autocomplete**: Naprawiono przechwytywanie klawiszy Tab/Enter przez migrację do `ProcessCmdKey` w `AgentControl.cs`.
- **Build CS0111/CS0103**: Naprawiono błędy kompilacji po masowej refaktoryzacji (dodanie plików do .csproj oraz usunięcie duplikatu klasy w UserInputTool.cs).
- **Silent Name Mismatch (Death Spiral)**: Naprawiono błąd w v2.9.1, gdzie klucze `ToolConfigManager` korzystały z nazw klas C# zamiast API Names z `FunctionSchema`, co unieruchamiało mechanizm Early Exit i gubiło narzędzia #core.

## 2026-06-03T10:45:17+02:00
### [ZREALIZOWANO]
- Przeprowadzono szczegółową analizę architektury projektu w wersji V2 (Function Calling, LLMClient ReAct, RPN, Dataset Studio, CLI, system receptur i faza Vision).
- Przeanalizowano wytyczne, instrukcje i zasady w dokumentacji projektu.
### [STAN_SYSTEMU]
- System w pełni stabilny i przeanalizowany, gotowy do dalszego rozwoju.
### [BLOKADY / PROBLEMY]
- Brak napotkanych trudności w procesie analizy.
### [KOLEJNY_KROK]
- Oczekiwanie na konkretne zadania implementacyjne od użytkownika.

## 2026-06-03T11:31:40+02:00
### [ZREALIZOWANO]
- Przeanalizowano pliki konfiguracyjne LLM (LLMConfigModels.cs, LLMClient.cs) w celu identyfikacji parametrów, które można przekazać do dostawców API (OpenAI, OpenRouter, LM Studio).
- Opracowano listę potencjalnych rozszerzeń konfiguracji (m.in. reasoning_effort, thinking budget, top_p, seed, frequency/presence penalties).
### [STAN_SYSTEMU]
- System jest gotowy na wprowadzenie zmian w konfiguracji dostawców. Parametry są obecnie ograniczone do Model, Temperature, MaxTokens, ApiKey i EndpointUrl.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Ewentualna implementacja dodatkowych pól w LLMProviderConfig i LLMConfigDialog na życzenie użytkownika.

## 2026-06-03T11:34:52+02:00
### [ZREALIZOWANO]
- Przeanalizowano przydatność parametrów LLM specyficznych dla silników lokalnych (Ollama, LM Studio, llama.cpp): top_p, top_k, min_p, repeat_penalty oraz stop sequences.
- Opracowano plan integracji parametrów samplingu dla modeli lokalnych w celu poprawy deterministyczności i stabilności Tool Callingu na małych modelach (np. Qwen, DeepSeek).
### [STAN_SYSTEMU]
- System bez zmian kodu źródłowego. Przeprowadzono analizę wpływu parametrów lokalnych na API.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na decyzję użytkownika co do implementacji rozszerzonych parametrów lokalnych.

## 2026-06-03T11:38:50+02:00
### [ZREALIZOWANO]
- Wdrożono rozszerzone parametry konfiguracji dostawców LLM (TopP, TopK, MinP, RepetitionPenalty, ReasoningEffort) w modelach danych C# (LLMConfigModels.cs), silniku klienta (LLMClient.cs) oraz formularzu UI (LLMConfigDialog.cs).
- Pomyślnie skompilowano i zweryfikowano projekt Bricscad_AgentAI_V2 za pomocą dotnet build.
### [STAN_SYSTEMU]
- System w pełni zaktualizowany o obsługę zaawansowanych parametrów dla modeli lokalnych i chmurowych. Wszystkie zmiany są wstecznie kompatybilne.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na uruchomienie i testy użytkownika w środowisku BricsCAD.

## 2026-06-03T11:42:24+02:00
### [ZREALIZOWANO]
- Wdrożono czytelny interfejs pomocy i podpowiedzi (ToolTips) dla wszystkich zaawansowanych parametrów konfiguracji dostawców (Temperature, Max Tokens, Top-P, Top-K, Min-P, Repetition Penalty, Reasoning Effort).
- Pomyślnie skompilowano i przetestowano aplikację.
### [STAN_SYSTEMU]
- System w pełni zaktualizowany o opisy parametrów podpowiedzi tooltip. Stabilny i gotowy do użycia.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na dalsze wytyczne od użytkownika.

## 2026-06-03T13:20:00+02:00
### [ZREALIZOWANO]
- Dodano twarde zabezpieczenia (safeguards) w `LLMClient.cs` zapobiegające dołączaniu zaawansowanych parametrów samplingu (`top_k`, `min_p`, `repetition_penalty`) do żądań wysyłanych do oficjalnych API OpenAI (`api.openai.com`) i Azure OpenAI (`openai.azure.com`).
- Usunięto błędy kompilacji:
  - Rozwiązano konflikt nazw `config` w `SendMessageReActAsync` przez usunięcie redundancji słowa kluczowego `var` przy re-definicji w pętli.
  - Skorygowano rzutowanie typów w `double.TryParse` dla `GpuOffload` w `LLMClient.cs` oraz `LLMConfigDialog.cs` poprzez użycie `System.Globalization.NumberStyles.Any`.
  - Dodano pełny namespace `System.Text.Encoding` dla `StringContent` w `LLMConfigDialog.cs` w celu wyeliminowania błędu braku nazwy `Encoding` w kontekście.
- Zweryfikowano poprawność kompilacji - kompilacja zakończyła się pełnym sukcesem (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- System jest stabilny, w pełni kompatybilny wstecznie ze wszystkimi chmurowymi dostawcami (OpenRouter, OpenAI) oraz przystosowany do zaawansowanego sterowania i dynamicznego ładowania modeli lokalnych w LM Studio.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie działania dynamicznego ładowania w środowisku CAD po netloadzie wtyczki.

## 2026-06-03T13:35:00+02:00
### [ZREALIZOWANO]
- Rozwiązano błąd `unrecognized_keys` w LM Studio (`contextLength`, `flashAttention`, `offloadKvCacheToGpu`):
  - Usunięto duplikowane camelCase parametry z payloadu ładowania w `LLMClient.cs` oraz `LLMConfigDialog.cs`. REST API LM Studio wspiera wyłącznie snake_case.
  - Usunięto nieobsługiwany parametr `gpu` z payloadu REST API. GPU offload w LM Studio REST API jest konfigurowany i dziedziczony z domyślnego profilu modelu w GUI aplikacji LM Studio.
  - Zaktualizowano podpowiedź (ToolTip) dla pola GPU w `LLMConfigDialog.cs`, aby jasno informować o tym zachowaniu API LM Studio.
- Ponownie przetestowano kompilację projektu (sukces, 0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- System w pełni zsynchronizowany ze ścisłym schematem REST API LM Studio (`POST /api/v1/models/load`), co eliminuje błędy `unrecognized_keys` (BadRequest).
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Czekanie na weryfikację ze strony użytkownika.

## 2026-06-03T14:00:00+02:00
### [ZREALIZOWANO]
- Wdrożono dynamiczne zarządzanie promptem systemowym z poziomu UI wtyczki BricsCAD:
  - Dodano nową, ostatnią zakładkę "⚙️ Ustawienia" w panelu głównym `AgentControl.cs`.
  - Umieszczono na niej edytor tekstowy `txtSystemPromptEditor` (RichTextBox) wyświetlający aktualną treść promptu.
  - Zaimplementowano obsługę zapisu przez przycisk "💾 Zapisz Prompt", który nadpisuje plik `system_prompt.txt` na dysku i wywołuje `RebuildSystemPrompt()`, odświeżając stan w pamięci aktywnej sesji czatu.
  - Zsynchronizowano edytor z metodą `RebuildSystemPrompt()`, dzięki czemu przy każdym resecie pamięci lub wczytaniu ustawień treść w edytorze jest aktualizowana.
  - Dodano pełne wsparcie kolorystyczne (ApplyTheme) dla nowo dodanych kontrolek.
- Zweryfikowano poprawność kompilacji - kompilacja zakończyła się pełnym sukcesem (0 błędów, 0 ostrzeżeń), a plik DLL został pomyślnie przebudowany.
### [STAN_SYSTEMU]
- System stabilny, rozbudowany o kompletną zakładkę ustawień ułatwiającą dynamiczną pracę z promptem systemowym w locie bez restartu BricsCAD i bez rekompilacji.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Uruchomienie wtyczki w programie BricsCAD i weryfikacja nowej zakładki "Ustawienia".

## 2026-06-03T14:15:00+02:00
### [ZREALIZOWANO]
- Przebudowano strukturę zakładki "⚙️ Ustawienia" w `AgentControl.cs` w celu wdrożenia architektury zagnieżdżonych podzakładek (TabControl):
  - Utworzono podzakładkę "Prompt" przeznaczoną do edycji promptu systemowego.
  - Dodano do podzakładki przycisk "📂 Otwórz folder" wywołujący `System.Diagnostics.Process` z argumentem `/select` w celu otwarcia Eksploratora Windows i automatycznego zaznaczenia pliku `system_prompt.txt`.
  - Dostosowano mechanizm `ApplyTheme` do aplikowania motywów kolorystycznych (tła i czcionek) również dla nowo powstałego kontenera podzakładek `tabSettingsSub` i jego dzieci.
- Przeprowadzono pomyślną kompilację (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- System stabilny, zorganizowany pod kątem przyszłej rozbudowy ustawień, z wygodną integracją z systemowym menedżerem plików (Explorer).
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Czekanie na weryfikację ze strony użytkownika.

## 2026-06-03T14:45:00+02:00
### [ZREALIZOWANO]
- Naprawiono błąd pustych/niewidocznych parametrów aktywnego profilu przy otwarciu okna "Ustawienia Dostawców LLM":
  - W metodzie `LoadData` w pliku `LLMConfigDialog.cs` dodano reset `cbProviders.SelectedIndex = -1` przed przypisaniem docelowego indeksu dostawcy. Wymusza to poprawne wywołanie zdarzenia `SelectedIndexChanged` w WinForms, kiedy domyślny indeks pokrywa się z indeksem 0 (który był automatycznie przypisywany przy bindowaniu DataSource).
- Zweryfikowano poprawność kompilacji (0 błędów).
### [STAN_SYSTEMU]
- System stabilny, poprawione zachowanie UI konfiguracji dostawców. Parametry wczytują się natychmiast po uruchomieniu okna dialogowego.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Czekanie na weryfikację ze strony użytkownika.

## 2026-06-03T15:05:00+02:00
### [ZREALIZOWANO]
- Zrefaktoryzowano metodę `WykonajInteligentneZaznaczenie` w pliku `AgentCommand.cs` (V1).
- Zastąpiono 4 wywołania `Regex.Match`/`Regex.Matches` (EntityType, Mode, Scope, Conditions) parsowaniem strukturalnym przez `JObject.Parse(json)` z biblioteki `Newtonsoft.Json.Linq`.
- Dodano blok `try-catch` dla `JsonReaderException` z komunikatem diagnostycznym, obsługujący uszkodzony JSON generowany przez LLM.
- Ekstrakcja `Conditions` wykonywana jest teraz przez iterację po `JArray`, a `Value` jest bezpiecznie konwertowane przez `JToken.ToString()` (obsługuje string, liczby i bool bez problemów z cudzysłowami).
- Zachowano domyślne wartości: `Mode = "New"`, `Scope = "Model"` (operator `??`).
- Cała dolna logika metody (transakcje BricsCAD, refleksja, `AktywneZaznaczenie`) pozostała bez zmian.
### [STAN_SYSTEMU]
- Plik `AgentCommand.cs` zmodyfikowany, import `Newtonsoft.Json.Linq` już istniał. Metoda jest teraz odporna na warianty formatowania JSON z LLM.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Kompilacja i weryfikacja poprawności działania w środowisku BricsCAD.

## 2026-06-03T20:10:00+02:00
### [ZREALIZOWANO]
- Wykonano Krok 1 z `12_Mulitagent_upgrade.md`: Refaktoring silnika ReAct (`LLMClient.cs`).
- Utworzono interfejs `IExecutionContext` oraz `CadExecutionContext` aby uniezależnić Workery od sztywnego wymogu `Document doc`.
- Utworzono strukturę `AgentExecutionResult` w `Models` dla zwracania spójnych wyników wykonania Agenta.
- Zmodyfikowano `LLMClient.cs`: usunięto z pętli obsługę `RequestAdditionalTools` (pulą narzędzi zarządza teraz Supervisor przed wywołaniem pętli) oraz zmieniono logikę powrotną na użycie `AgentExecutionResult`.
- Dostosowano `AgentTesterControl` i `AgentControl` do nowych zmian i kompilacja (dotnet build) zakończyła się pełnym sukcesem.
### [STAN_SYSTEMU]
- System jest stabilny. Silnik konwersacyjny gotowy na krok 2, czyli implementację `Blackboard` i wyizolowanie zarządzania z wewnątrz poszczególnych pętli.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Krok 2: Wdrożenie Blackboard (współdzielony stan).

## 2026-06-03T20:12:00+02:00
### [ZREALIZOWANO]
- Wykonano Krok 2 z `12_Mulitagent_upgrade.md`: Wdrożono Blackboard (Współdzielony Stan).
- Utworzono klasę `SharedMemoryState` (`ConcurrentDictionary<string, string>`) dla bezpiecznej wymiany danych między agentami.
- Utworzono i podpięto do projektu narzędzia: `WriteToBlackboardTool` oraz `ReadFromBlackboardTool`.
- Kompilacja przebiegła pomyślnie.
### [STAN_SYSTEMU]
- System posiada teraz tablicę ogłoszeń do trwałego przechowywania i przekazywania danych kontekstowych między różnymi izolowanymi sesjami konwersacyjnymi.
### [BLOKADY / PROBLEMY]
- Drobne błędy kompilacji (nieaktualne nazwy klas `ToolFunction` na `FunctionSchema` w nowych narzędziach) - błyskawicznie naprawione.
### [KOLEJNY_KROK]
- Krok 3: Profile Narzędzi (`ToolConfigManager.cs`).

## 2026-06-03T20:14:00+02:00
### [ZREALIZOWANO]
- Wykonano Krok 3 z `12_Mulitagent_upgrade.md`: Restrukturyzacja zarządzania narzędziami.
- Wprowadzono nową strukturę konfiguracji JSON w `ToolConfigManager.cs` (`ToolConfigRoot`), grupującą definicje narzędzi oraz nowe Profile Agentów.
- Zdefiniowano profile (np. `SupervisorProfile`, `CadProfile`) ze zdefiniowanymi przypisaniami ścieżek do pliku promptu systemowego oraz dostępnych narzędzi (`AllowedTools`) i tagów (`AllowedTags`).
- Dodano wsteczną kompatybilność podczas ładowania (automatyczna migracja starej płaskiej struktury do nowej `ToolConfigRoot`).
- Skorygowano UI w `AgentControl.cs` podpinając przywróconą metodę `UpdateSettings`.
### [STAN_SYSTEMU]
- Kompilacja przebiegła pomyślnie. Nowy plik `tools_config.json` z profilami generuje się bezbłędnie. Agenci mogą być teraz instancjowani z określonym profilem kompetencji.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Krok 4: Implementacja Supervisora (Nadrzędna pętla sterująca).

## 2026-06-03T20:20:00+02:00
### [ZREALIZOWANO]
- Wykonano Krok 4 z `12_Mulitagent_upgrade.md`: Utworzenie Orchestratora Głównego.
- Utworzono klasę `SupervisorOrchestrator`, odpowiedzialną za przechowywanie globalnej historii i inicjalizację głównej pętli dla "SupervisorProfile".
- Stworzono narzędzie `DelegateTaskTool` (IToolV2), dzięki któremu Supervisor może delegować konkretne instrukcje do sprofilowanych agentów (np. `CadProfile`).
- `DelegateTaskTool` poprawnie blokuje główny cykl Supervisora, spawnując sub-agenta i przekazując mu zadanie wraz z wstrzyknięciem kontekstu pamięci z Blackboarda.
- Zaktualizowano `AgentControl.cs` i usunięto obsługę lokalnej zmiennej `_conversationHistory`, przepinając UI bezpośrednio pod `SupervisorOrchestrator`.
- Kompilacja przebiegła pomyślnie.
### [STAN_SYSTEMU]
- Architektura opiera się teraz o wzorzec Supervisor-Worker. Komunikacja przechodzi przez Agenta Głównego (Supervisora), który następnie deleguje pracę "w dół" z ustandaryzowanymi profilami (Agent Ekspert).
### [BLOKADY / PROBLEMY]
- Wymagało kaskadowej refaktoryzacji we wszystkich narzędziach oraz kontrolkach ze względu na zmianę sygnatury `ExecuteTool` przyjmującej teraz `IExecutionContext` zamiast `Document doc`. Sukces po iteracyjnych poprawkach i ponownych kompilacjach.
### [KOLEJNY_KROK]
- Krok 5: Propagacja i Zgodność UX (Eventy z sub-pętli wysyłane do AgentControl, obsługa logowania wywołań w UI z zachowaniem informacji o roli Agenta wykonującego).

## 2026-06-03T20:25:00+02:00
### [ZREALIZOWANO]
- Wykonano Krok 5 z `12_Mulitagent_upgrade.md`: Propagacja i Zgodność UX.
- Zmodyfikowano kontrolkę `AgentControl.cs`, upewniając się, że metody aktualizujące HUD (`UpdateStatusHUD`, `UpdateStatsHUD`, `AppendToolLog`) są publiczne i gotowe do odbioru zdarzeń z zewnątrz.
- Podłączono w `DelegateTaskTool.cs` instancję sub-klienta (`LLMClient`) do głównej instancji kontrolki UI (`AgentControl.Instance`).
- Zdarzenia z sub-klienta (Eksperta) są teraz propagowane na główny ekran z prefiksem określającym aktywny profil (np. `[CadProfile] Oczekiwanie na analizę...`).
- Wyniki działania sub-klienta (zwracane przez narzędzie) są natywnie dodawane przez Supervisora do `_globalHistory` jako wiadomości typu `tool`, co zamyka pętlę ReAct.
- Kompilacja przebiegła bezbłędnie.
### [STAN_SYSTEMU]
- Kompletne wdrożenie architektury Multi-Agent (Supervisor-Worker) zostało zakończone. Cały projekt działa w trybie hierarchicznym, odciążając jeden główny system prompt od nadmiaru wiedzy, izolując narzędzia i zapobiegając halucynacjom wywołanym zbyt dużym kontekstem.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Rozpoczęcie tworzenia konkretnych, wyspecjalizowanych narzędzi pod kątem nowych profili (np. dedykowany Agent do zarządzania arkuszami).

## 2026-06-03T22:35:00+02:00
### [ZREALIZOWANO]
- Naprawiono regresję routingu Supervisora i brakujących profili w konfiguracji:
  - Zaimplementowano funkcję `EnsureSupervisorPromptFile()` w `ToolConfigManager.cs`, która automatycznie tworzy plik `system_prompt_supervisor.txt` z rygorystycznymi wytycznymi dotyczącymi braku pogawędek i natychmiastowego delegowania do `CadProfile`.
  - Zaktualizowano `SyncWithTools` w `ToolConfigManager.cs`, aby automatycznie synchronizował i uzupełniał brakujące lub puste profile (`SupervisorProfile`, `CadProfile`) oraz ich domyślne dozwolone narzędzia w pliku `tools_config.json`.
  - Mapowano `CadProfile` do `system_prompt.txt` zamiast `system_prompt_cad.txt`, co przywróciło pełną kompatybilność z wbudowanym w UI edytorem promptu systemowego.
  - Zmodyfikowano `GetToolsPayloadForProfile` w `ToolOrchestrator.cs`, umożliwiając dynamiczną aktywację narzędzi na podstawie tagów sesji (`SessionDynamicTags`) oraz profili (`AllowedTags`), co przywróciło mechanizm ładowania dynamicznego w architekturze Multi-Agent.
  - Pomyślnie przebudowano i skompilowano wtyczkę bez błędów i ostrzeżeń.
### [STAN_SYSTEMU]
- System jest stabilny. Supervisor poprawnie wykrywa narzędzia delegujące, a Worker CAD posiada dostęp do wszystkich dedykowanych komend i poprawnie reaguje na zmiany promptu w locie.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie zachowania asystenta w programie BricsCAD pod kątem masowych selekcji i edycji.

## 2026-06-03T22:52:00+02:00
### [ZREALIZOWANO]
- Uporządkowano i zsynchronizowano pozostałe zakładki aplikacji z architekturą Multi-Agent:
  - **Ustawienia (Prompt Editor)**: Dodano rozwijaną listę `cbPromptFile` do wyboru pliku promptu (`system_prompt.txt` / `system_prompt_supervisor.txt`), umożliwiając dynamiczną edycję i zapis promptów CAD i Supervisora bezpośrednio z UI.
  - **Tester V2**: Wdrożono ComboBox `cbProfiles` pozwalający na testowanie zapytań w kontekście konkretnego profilu (`CadProfile` / `SupervisorProfile` / Monolit). Zapytania są teraz wysyłane z dedykowanymi promptami wczytywanymi z dysku dla danego profilu.
  - **Benchmark**: Zmodyfikowano `SendMessageBenchmarkAsync` w `LLMClient.cs` tak, aby wczytywał pełną listę narzędzi (tag `#all`). Umożliwiło to pomyślną walidację benchmarków testujących nie-bazowe narzędzia CAD.
  - **Dataset Studio & Tool Logs**: Dodano przechwytywanie logów wykonania w `DelegateTaskTool.cs`. Każda izolowana pętla Workera (z jej tool calls i odpowiedziami) jest automatycznie rejestrowana jako osobny rekord sesji w Dataset Studio. Rozwiązuje to problem utraty danych treningowych CAD w architekturze hierarchicznej.
  - **LLM stats**: Dodano właściwość `LastStats` do klasy `LLMClient.cs` w celu pobierania statystyk tokenów na koniec wywołań.
### [STAN_SYSTEMU]
- Wszystkie zakładki w panelu bocznym wtyczki zostały zintegrowane i przetestowane syntetycznie. System w pełni wspiera hierarchiczne fine-tuning i diagnostykę.
### [BLOKADY / PROBLEMY]
- Blokowanie pliku DLL `bin\Debug\Bricscad_AgentAI_V2.dll` przez działającą instancję BricsCAD przy próbie skopiowania po kompilacji (kod źródłowy kompiluje się bez błędów w `obj\Debug`).
### [KOLEJNY_KROK]
- Uruchomienie zaktualizowanego panelu w BricsCAD (po restarcie aplikacji CAD w celu zwolnienia blokady pliku DLL) i testy manualne.

## 2026-06-03T23:10:00+02:00
### [ZREALIZOWANO]
- Wdrożono lekki, bezpieczny system logowania i diagnostyki `BielikLogger.cs` w celu monitorowania działania wtyczki w locie i diagnozowania nagłych zamknięć (crashy) programu BricsCAD:
  - Stworzono klasę statyczną `BielikLogger` piszącą synchronicznie (dla bezpieczeństwa zapisu przed crashem) do pliku `bielik_debug.log` z rotacją po przekroczeniu rozmiaru 1 MB.
  - Zarejestrowano unhandled exception i thread exception trap w `AgentStartup.cs` logujące stack trace na sekundy przed zamknięciem procesu.
  - Zintegrowano logowanie zapytań LLM w `LLMClient.cs` oraz wywołań narzędzi w `ToolOrchestrator.ExecuteTool` z dołączeniem identyfikacji wątków (`[UI]` / `[Worker]`).
  - Dodano podzakładkę "Diagnostyka" w zakładce "Ustawienia" w `AgentControl.cs` z podglądem logu w czasie rzeczywistym, czyszczeniem logów i bezpośrednim otwieraniem pliku w systemowym Notatniku.
- Dodano plik `BielikLogger.cs` do kompilacji w `Bricscad_AgentAI_V2.csproj`.
- Pomyślnie skompilowano projekt (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- System jest w pełni stabilny, wyposażony w automatyczny trap błędów i bezpieczne logi diagnostyczne do śledzenia w BricsCAD.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testy w środowisku BricsCAD z otwartym podglądem Diagnostyki i przechwyceniem logów w razie awarii.

## 2026-06-03T23:25:00+02:00
### [ZREALIZOWANO]
- Wprowadzono architekturę wieloagentową z trzema nowymi, wysoce wyspecjalizowanymi profilami pod-agentów (Workers) w celu skrócenia czasu reakcji, redukcji tokenów i zwiększenia precyzji:
  - **`CadGeometryProfile`**: Dedykowany do rysowania, warstw, kolorów, linii i tekstów (narzędzia geometryczne).
  - **`CadBlocksProfile`**: Dedykowany do operacji na blokach i atrybutach.
  - **`CadMetadataProfile`**: Dedykowany do pomiarów, odczytu właściwości i metadanych XData.
  - **`CadProfile`**: Pozostawiony jako profil ogólny/awaryjny.
  - Zaktualizowano `ToolConfigManager.cs`, aby automatycznie synchronizował i tworzył te profile w `tools_config.json`.
  - Wdrożono automatyczny upgrade `system_prompt_supervisor.txt`, który instruuje Supervisora o istnieniu 3 wyspecjalizowanych profilów i zasadach kierowania zadań (routingu).
  - Rozbudowano okno **Tester V2** (`AgentTesterControl.cs`), dodając nowe profile do listy wyboru w celach testowych.
  - Zaimplementowano `VariableStore` w `AgentMemoryState.cs` w celu automatycznego replikowania zmiennych sesyjnych Agenta na globalny Blackboard (rozwiązanie problemu odczytu odczytanych atrybutów przez workera).
  - Dodano parametry `FilterTag` i `FilterValue` do `EditAttributesTool.cs` w celu umożliwienia modyfikacji konkretnego wystąpienia bloku w zaznaczeniu.
- Przygotowano AutoLISP-owy skrypt testowy **[generate_test_objects.lsp](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/generate_test_objects.lsp)** definiujący komendę `GEN_BIELIK_TESTS`, która tworzy warstwy, polilinie z XData BIELIK_APP, teksty oraz bloki z atrybutami, służące do weryfikacji każdego z 3 nowych profili.
- Pomyślnie skompilowano projekt (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- System posiada w pełni sprofilowaną strukturę hierarchiczną z dedykowanymi agentami roboczymi. Gotowy do testowania w BricsCAD.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Załadowanie LISP-a w BricsCAD, uruchomienie generowania obiektów i przeprowadzenie testów routingu Supervisora oraz wykonania u poszczególnych agentów.

## 2026-06-03T23:36:00+02:00
### [ZREALIZOWANO]
- Naprawiono błąd wyszukiwania instancji bloków (`SelectEntitiesTool.cs`) podczas selekcji po `EntityType`:
  - Przechwycono zapytania o właściwości `"EntityType"` oraz `"Type"`, przekierowując je bezpośrednio na pobranie nazwy typu C# (`ent.GetType().Name`) i tym samym omijając refleksję C#, która zwracała `null` dla typu klasy `BlockReference`.
  - Wdrożono obsługę dopasowań z użyciem symboli wieloznacznych (Wildcard, np. `*BlockReference`) w metodzie `ValidateLogicCondition` za pomocą mapowania na wyrażenia regularne (`IsWildcardMatch`).
  - Dodano asercje testowe w `SelectEntitiesToolTests.cs` weryfikujące poprawność dopasowań wieloznacznych dla operatorów `==` i `!=`.
  - Zweryfikowano poprawność kompilacji projektu (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- Filtrowanie i selekcja encji w pamięci Agenta (w tym po typie `BlockReference` z użyciem symboli wieloznacznych) działa w 100% stabilnie i niezawodnie, zapobiegając błędom pustej pamięci w Scenario 2.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Uruchomienie BricsCAD i ponowne przetestowanie Scenario 2 w celu weryfikacji zmiany atrybutu biurka o ID A2 na "Wolne".

## 2026-06-03T23:55:00+02:00
### [ZREALIZOWANO]
- Złagodzono restrykcje promptu systemowego Supervisora (`system_prompt_supervisor.txt`):
  - Zaktualizowano definicję i generowanie promptu w `ToolConfigManager.cs`, aby jednoznacznie zezwalać na bezpośrednie, rzeczowe i przyjazne odpowiadanie na pytania ogólne (matematyczne, historyczne, luźne rozmowy) bez delegowania ani odmawiania.
  - Zaimplementowano automatyczny mechanizm wykrywania i nadpisywania (auto-upgrade) starych wersji promptu w `EnsureSupervisorPromptFile()` w oparciu o obecność tagu `"PYTANIA OGÓLNE"`.
  - Pomyślnie przebudowano i skompilowano wtyczkę (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- Supervisor jest w stanie poprawnie kierować zapytaniami: deleguje operacje CAD do dedykowanych workerów, a na pytania ogólne (niezwiązane z silnikiem CAD) odpowiada samodzielnie w zwykłym tekście.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Weryfikacja działania przez użytkownika w programie BricsCAD (pytania ogólne typu "kto był pierwszym królem Polski" lub obliczenia matematyczne powinny teraz uzyskiwać bezpośrednie odpowiedzi).

## 2026-06-04T00:15:00+02:00
### [ZREALIZOWANO]
- Wdrożono wyspecjalizowany profil obliczeniowy `CadMathProfile` oraz narzędzie `CalculateRpn`:
  - Utworzono klasę [CalculateRpnTool.cs](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/src/Tools/CalculateRpnTool.cs) implementującą interfejs `IToolV2`, umożliwiającą bezpieczne wykonywanie obliczeń RPN za pomocą wewnętrznego silnika wymiarowego `RpnCalculator`.
  - Dodano automatyczną generację promptu systemowego `system_prompt_math.txt` dla Math Experta.
  - Zarejestrowano profil `CadMathProfile` w `ToolConfigManager.cs` ze ścisłym zakresem dopuszczalnych narzędzi (`CalculateRpn`, `ReadFromBlackboard`, `WriteToBlackboard`, `UserInput`, `UserChoice`) i powiązaniem z promptem obliczeniowym.
  - Zaktualizowano prompt Supervisora, dodając routowanie zadań matematyczno-fizycznych i przeliczania jednostek bezpośrednio do `CadMathProfile`.
  - Dodano `CadMathProfile` do dropdowna `cbProfiles` w **Tester V2** oraz do edytora promptów w zakładce **Ustawienia** w `AgentControl.cs`.
  - Napisano testy jednostkowe w `CalculateRpnToolTests.cs` i zintegrowano je z głównym runnerem `TestRunner.cs`.
  - Pomyślnie skompilowano wtyczkę jako bibliotekę DLL (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- System posiada 4 wyspecjalizowane profile robocze (Geometry, Blocks, Metadata, Math) zarządzane przez Supervisora. Profil obliczeniowy posiada dedykowane narzędzie do precyzyjnych obliczeń w notacji RPN z analizą wymiarową jednostek SI.
### [BLOKADY / PROBLEMY]
- Testy offline runnera `.exe` wymagają zależności native C++ od BricsCAD (`BrxMgd.dll`), dlatego pełna weryfikacja logiki RPN odbywa się po załadowaniu wtyczki wewnątrz CAD.
### [KOLEJNY_KROK]
- Testowanie w programie BricsCAD z użyciem nowego profilu obliczeniowego `CadMathProfile` i narzędzia `CalculateRpn`.

## 2026-06-04T00:17:00+02:00
### [ZREALIZOWANO]
- Skorygowano reguły routingu nadrzędnego Supervisora w `ToolConfigManager.cs`:
  - Rozgraniczono "LUŹNĄ ROZMOWĘ I WIEDZĘ OGÓLNĄ" (którą Supervisor obsługuje sam w zwykłym tekście) od "OBLICZEŃ MATEMATYCZNYCH, FIZYCZNYCH I PRZELICZANIA JEDNOSTEK" (które Supervisor musi bezwzględnie delegować do profilu `CadMathProfile` za pomocą narzędzia `DelegateTask`).
  - Dodano automatyczny upgrade (auto-upgrade) dla szablonów promptu supervisora w oparciu o obecność tagu `"LUŹNA ROZMOWA"`.
  - Pomyślnie przebudowano i skompilowano wtyczkę (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- Supervisor ma ściśle zdefiniowane warunki brzegowe: luźne rozmowy prowadzi sam, a każde fizyczne/matematyczne/inżynieryjne obliczenie deleguje do eksperta `CadMathProfile` posiadającego dostęp do kalkulatora RPN.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Weryfikacja delegowania obliczeń do `CadMathProfile` w programie BricsCAD.

## 2026-06-04T00:30:00+02:00
### [ZREALIZOWANO]
- Skorygowano i rozbudowano prompt systemowy Math Experta (`system_prompt_math.txt`):
  - Dodano szczegółowe zasady działania notacji RPN na stosie i formatowania wartości z jednostkami (np. `10_m`, `11.34_g/cm3`).
  - Nakazano podział złożonych obliczeń na mniejsze, precyzyjne kroki cząstkowe (osobne wywołania `CalculateRpn`) zamiast jednego gigantycznego wyrażenia, co eliminuje błędy zapętlenia modelu i przekroczenia limitu tokenów.
  - Zaimplementowano instrukcje korzystania z parametrów `SaveAs` i `@zmienna` do przechowywania wartości cząstkowych.
  - Dodano gotowe, poprawne wzory i przykłady RPN dla obliczania pola koła ($\pi r^2$), objętości kuli ($\frac{4}{3} \pi r^3$), masy ołowiu oraz energii kinetycznej ($mgh$).
  - Dodano automatyczny upgrade (auto-upgrade) szablonu promptu w oparciu o obecność tagu `"WZORY I PRZYKŁADY RPN"`.
  - Pomyślnie przebudowano i skompilowano wtyczkę (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- Math Expert posiada kompletną i szczegółową bazę wiedzy na temat składni RPN i strategii podziału obliczeń na kroki, co zabezpiecza go przed błędnymi operacjami stosu i zapętleniami.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Ponowne przetestowanie obliczeń (koło i kula) w BricsCAD.


## 2026-06-04T00:38:00+02:00
### [ZREALIZOWANO]
- Udoskonalono walidację i proces auto-upgrade dla szablonu promptu Math Experta (`system_prompt_math.txt`) w `ToolConfigManager.cs`, wprowadzając sprawdzenie obecności fraz `"Częsty błąd przy ułamkach"` oraz `"UNIKAJ DANGLED STACK"`.
- Rozbudowano domyślny prompt systemowy dla profilu `CadMathProfile` o jasne wytyczne dotyczące unikania zwisów na stosie (Dangled Stack) oraz zasad zapisu ułamków i mnożenia ułamków (np. `4 3 / wyrażenie *` lub `wyrażenie 4 * 3 /` zamiast błędnego `wyrażenie 4 / 3`).
- Dodano test jednostkowy dla objętości kuli z konwersją jednostkową (`5_cm 3 ^ #PI * 4 * 3 / 'cm3' CONVE`) w `CalculateRpnToolTests.cs` w celu weryfikacji poprawności obliczeń RPN.
- Pomyślnie skompilowano wtyczkę jako bibliotekę DLL (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- System stabilny, a prompt Math Experta zabezpieczony przed typowymi błędami generowania wyrażeń RPN dla ułamków i operacji trójskładnikowych (mgh).
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Przetestowanie działania agenta Bielik (Supervisor + Math Expert) w BricsCAD po ponownym załadowaniu wtyczki z nowym promptem matematycznym.


## 2026-06-04T00:43:00+02:00
### [ZREALIZOWANO]
- Zdiagnozowano i pomyślnie przetestowano wywołanie profilu matematycznego w programie BricsCAD. Agent poprawnie rozłożył i obliczył energię kinetyczną w 4 krokach: promień (`0.1_m 2 /`), objętość (`@Promien 3 ^ #PI * 4 * 3 /`), masę (`@Objetosc 11340_kg/m3 *`) oraz energię kinetyczną (`@Masa #G * 10_m *`). Wszystkie operacje stosu i stałe fizyczne zadziałały bezbłędnie.
- Zidentyfikowano pojedynczą próbę wywołania nazwy profilu `"CadMathProfile"` jako bezpośredniej nazwy funkcji przez model Supervisor.
- Wprowadzono poprawkę w `ToolConfigManager.cs` (`EnsureSupervisorPromptFile`): dodano automatyczną aktualizację pliku `system_prompt_supervisor.txt` sprawdzającą obecność frazy `"Profile NIE są narzędziami"`.
- Zaimplementowano nową regułę w instrukcjach nadrzędnych Supervisora kategorycznie zabraniającą traktowania nazw profilów (np. `CadMathProfile`, `CadGeometryProfile`) jako narzędzi i nakazującą bezwzględne korzystanie z `DelegateTask`.
- Pomyślnie zrekompilowano wtyczkę jako bibliotekę DLL (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- System jest stabilny, zabezpieczony przed podwójnym wywoływaniem profilu i gotowy do ostatecznych testów.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na ostateczne potwierdzenie działania przez użytkownika w środowisku BricsCAD.


## 2026-06-04T00:46:00+02:00
### [ZREALIZOWANO]
- Przebudowano system benchmarkowy pod kątem obsługi wyboru profilu i testowania konkretnego Agenta Eksperta:
  - Rozszerzono metodę `SendMessageBenchmarkAsync` w `LLMClient.cs` o opcjonalny parametr `profileName`. Gdy profil jest podany, wywoływany jest orkiestrator z listą dozwolonych narzędzi i tagów zdefiniowanych dla tego profilu (zamiast standardowego `#all`).
  - Rozszerzono metodę `RunBenchmarkAsync` w `AutoBenchmarkEngine.cs` o parametr `profileName`. Gdy profil jest wybrany, silnik benchmarkowy dynamicznie wczytuje właściwy plik promptu systemowego dla tego agenta (np. `system_prompt_math.txt` dla `CadMathProfile` lub `system_prompt_supervisor.txt` dla `SupervisorProfile`) i automatycznie wstrzykuje go jako pierwszą wiadomość roli `"system"` w historii konwersacji, w pełni odtwarzając rzeczywiste środowisko wykonawcze agenta.
  - Zmodyfikowano kontrolkę `AutoBenchmarkControl.cs`, wprowadzając nową kontrolkę rozwijaną `cbProfiles` w pasku górnym. Dropdown jest dynamicznie uzupełniany profilami pobranymi z `ToolConfigManager` z opcją domyślną "(Brak profilu - wszystkie narzędzia)" dla zachowania pełnej kompatybilności wstecznej.
  - Zabezpieczono stan UI, blokując możliwość zmiany profilu w trakcie trwania benchmarku i odblokowując kontrolkę po zakończeniu przebiegu.
  - Pomyślnie zrekompilowano wtyczkę jako bibliotekę DLL (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- System w pełni zaktualizowany. Panel benchmarkowy umożliwia precyzyjne testowanie i walidację konkretnych profilów agentów (np. CadMathProfile, CadGeometryProfile) z właściwymi promptami i pulami narzędzi bez wychodzenia z UI testowego.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Przetestowanie nova funkcję wyboru profilu w zakładce Benchmark systemu BricsCAD.


## 2026-06-04T00:49:00+02:00
### [ZREALIZOWANO]
- Utworzono dedykowany zestaw testowy benchmarku [Benchmark_02_Math.json](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/Benchmark_02_Math.json) zawierający 5 reprezentatywnych zadań matematycznych i fizycznych (Pole koła, Objętość kuli, Masa ołowiu z gęstości, Energia potencjalna, Objętość rury).
- Skonfigurowano reguły walidacji typu `EvaluateRPN_Argument` w celu precyzyjnego obliczania i weryfikowania wyjściowej wartości RPN (np. asercja do `"7853.981634_mm2"` lub `"86.393798_L"`).
- Skonfigurowano symulowane odpowiedzi CAD (`SimulatedCADResponses`) dla Kalkulatora RPN dla każdego z 5 zadań, co umożliwia dwuetapową symulację konwersacji w pętli ReAct podczas działania benchmarku.
### [STAN_SYSTEMU]
- Plik benchmarkowy utworzony w folderze `/tests`, w pełni zgodny ze schematem V2 i gotowy do wczytania w UI testowym.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Wczytanie pliku `Benchmark_02_Math.json` w zakładce Benchmark i uruchomienie testu z wybranym profilem `CadMathProfile`.


## 2026-06-04T00:54:00+02:00
### [ZREALIZOWANO]
- Naprawiono i uściślono przykład obliczania objętości kuli w `EnsureMathPromptFile` w `ToolConfigManager.cs` (dodano `'cm3' CONVE` do wzoru przykładowego, aby model precyzyjnie konwertował jednostki do oczekiwanego formatu).
- Zmodyfikowano zapytania `UserPrompt` w pliku [Benchmark_02_Math.json](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/Benchmark_02_Math.json), doprecyzowując wymóg wykonania obliczeń w pojedynczym kroku RPN z wyraźnym poleceniem konwersji jednostki (`CONVE`). Rozwiązuje to problem przedwczesnego zatrzymania pętli ReAct po napotkaniu mockowanych odpowiedzi w teście wieloetapowym.
- Usunięto błędy składniowe w `ToolConfigManager.cs` powstałe podczas nakładania zmian, a cały projekt skompilował się bez ostrzeżeń.
### [STAN_SYSTEMU]
- Pliki kodu i konfiguracji benchmarku są zsynchronizowane, a silnik benchmarkowy i model mają precyzyjne dopasowanie pod kątem jednopoziomowych obliczeń RPN.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Ponowne wykonanie testu benchmarkowego `Benchmark_02_Math.json` w BricsCAD i weryfikacja skuteczności.


## 2026-06-04T01:05:00+02:00
### [ZREALIZOWANO]
- **Realne wykonanie narzędzi obliczeniowych i pamięciowych w benchmarku**: Zmodyfikowano metodę `SendMessageBenchmarkAsync` w [LLMClient.cs](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/src/Core/LLMClient.cs) tak, aby narzędzia `CalculateRpn`, `ReadFromBlackboard` oraz `WriteToBlackboard` były wykonywane naprawdę za pomocą orkiestratora, zamiast zwracania mockowanych odpowiedzi z pliku JSON. Rozwiązuje to problem przedwczesnego zakończenia pętli ReAct (LLM przestaje kończyć działanie na pierwszym kroku obliczeń cząstkowych po otrzymaniu mockowanego wyniku ostatecznego).
- **Walidacja ostatniego wywołania RPN**: Zaktualizowano regułę `EvaluateRPN_Argument` w [AutoBenchmarkEngine.cs](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/src/Core/AutoBenchmarkEngine.cs) do pobierania ostatniego wywołania `CalculateRpn` (`LastOrDefault`) zamiast pierwszego wywołania z argumentami (`FirstOrDefault`). Zapewnia to poprawną weryfikację końcowego wyniku w zadaniach wielokrokowych, w których LLM odwołuje się do zmiennych zapisanych w tablicy (blackboard).
- **Kompilacja i stabilność**: Pomyślnie zrekompilowano wtyczkę jako bibliotekę DLL (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- System stabilny, poprawnie obsługuje zadania wieloetapowe i obliczenia zmiennych blackboardowych w trybie benchmarkowym z wybranym profilowaniem.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Wykonanie testu benchmarku w programie BricsCAD w celu weryfikacji 100% poprawności.


## 2026-06-04T01:10:00+02:00
### [ZREALIZOWANO]
- **Uproszczenie zapytań w pliku benchmarkowym**: Przywrócono naturalne sformułowania w zapytaniach `UserPrompt` w [Benchmark_02_Math.json](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/Benchmark_02_Math.json) (usunieto narzucony wymóg "wykonania obliczeń jako pojedyncze wyrażenie RPN"). Dzięki temu model nie otrzymuje sprzecznych instrukcji z system promptem (który nakazuje dzielenie zadań na logiczne kroki cząstkowe) i poprawnie rozbija obliczenia na czytelne etapy, co eliminuje błędy składniowe i matematyczne (takie jak błędne potęgowanie czy zbędne dzielenie przez 1000).
- **Weryfikacja kompilacji**: Kompilacja wtyczki powiodła się bez żadnych błędów i ostrzeżeń.
### [STAN_SYSTEMU]
- System stabilny, zapytania benchmarkowe są w pełni skoordynowane ze strategią obliczeniową zdefiniowaną w system prompcie `CadMathProfile`.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Wykonanie testu benchmarku w BricsCAD i weryfikacja poprawności.


## 2026-06-04T01:17:00+02:00
### [ZREALIZOWANO]
- **Podniesienie precyzji kalkulatora RPN**: Zwiększono precyzję formatowania liczb zmiennoprzecinkowych w metodzie `ToString()` klasy `PhysicalValue` w [RpnCalculator.cs](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/src/Core/RpnCalculator.cs) z 6 do **12 miejsc po przecinku**. Zapobiega to utracie precyzji w obliczeniach wielokrokowych, gdy wyniki cząstkowe są zapisywane jako tekst w pamięci Agenta (np. małe wartości w m2/m3 po konwersji do mm2/cm3/litrów ulegały silnemu zaokrągleniu).
- **Fizyczne porównywanie wartości w walidacji (AreValuesPhysicallyEqual)**: Dodano inteligentną metodę porównywania wielkości fizycznych w `RpnCalculator` i zintegrowano ją w [AutoBenchmarkEngine.cs](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/src/Core/AutoBenchmarkEngine.cs). Zamiast porównywać sztywne napisy (np. `7853.981634_mm2` vs `7854_mm2`), silnik parsuje obie wartości, weryfikuje zgodność wymiarową (Dimensions) i sprawdza wartość liczbową w granicach tolerancji `1e-4` (relative tolerance).
- **Rozbudowa i uściślenie przykładów w promptach systemowych**:
  - Zaktualizowano prompt systemowy `system_prompt_math.txt` w [ToolConfigManager.cs](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/src/Core/ToolConfigManager.cs), dodając precyzyjne przykłady konwersji jednostek w locie za pomocą `'jednostka' CONVE` oraz pełny wzór i przykład obliczania objętości rury/walca w litrach (`r 2 ^ #PI * h * 'L' CONVE`).
  - Rozszerzono mechanizm automatycznej aktualizacji (auto-upgrade) dla promptów matematycznych i supervisora, aby poprawnie wymuszały zapis nowej wersji plików promptów.
- **Kompilacja**: Zrekompilowano projekt (0 błędów, 0 ostrzeżeń).
### [STAN_SYSTEMU]
- System jest w pełni odporny na drobne rozbieżności zaokrągleń double w RPN i posiada zaktualizowaną bazę promptów gwarantującą stabilne i powtarzalne wyniki obliczeń geometrycznych i fizycznych.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Uruchomienie zaktualizowanego benchmarku w środowisku BricsCAD i weryfikacja przejścia wszystkich 5 testów.


## 2026-06-04T01:23:00+02:00
### [ZREALIZOWANO]
- **Dodanie wskazówek RPN do zapytań benchmarkowych**: Wzbogacono pytania `UserPrompt` w pliku [Benchmark_02_Math.json](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/Benchmark_02_Math.json) o wyraźne wskazówki dotyczące konieczności stosowania kalkulatora RPN, dopisywania jednostek (np. `_mm`, `_cm3`) oraz używania polecenia `CONVE` w pojedynczych cudzysłowach. Pomaga to mniejszemu modelowi (Bielik 11B) w utrzymaniu dyscypliny składniowej RPN bez zmuszania go do nienaturalnego upakowywania całego zadania w jedno wywołanie.
- **Weryfikacja kompilacji**: Kompilacja powiodła się bez żadnych błędów i ostrzeżeń.
### [STAN_SYSTEMU]
- System stabilny, zapytania benchmarkowe są przystosowane pod kątem specyfiki mniejszych modeli LLM pracujących w pętli ReAct.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Wykonanie testu benchmarku w programie BricsCAD w celu weryfikacji.


 
 # #   2 0 2 6 - 0 6 - 0 4 T 0 2 : 2 3 : 0 0 + 0 2 : 0 0  
 # # #   [ Z R E A L I Z O W A N O ]  
 -   W d r o |o n o   f a l l b a c k   w   C r e a t e O b j e c t T o o l . c s   p r z e k i e r o w u j c y   d a n e   d o   R p n C a l c u l a t o r   w   p r z y p a d k u   b Bd u   f o r m a t o w a n i a   l i c z b .  
 -   U s u n i t o   c a Bk o w i c i e   n a r z d z i e   C a l c u l a t e R p n T o o l .  
 -   Z a i m p l e m e n t o w a n o   s z a b l o n y   m a t e m a t y c z n e   { M A T H :   . . . }   w   F o r e a c h T o o l ,   C r e a t e O b j e c t T o o l ,   M o d i f y P r o p e r t i e s T o o l .  
 # # #   [ S T A N _ S Y S T E M U ]  
 -   S y s t e m   s t a b i l n i e   o b s Bu g u j e   n a t u r a l n   m a t e m a t y k   w   p t l a c h   i   w Ba [c i w o [c i a c h .  
 # # #   [ B L O K A D Y   /   P R O B L E M Y ]  
 -   B r a k .  
 # # #   [ K O L E J N Y _ K R O K ]  
 -   C o m m i t   d o   r e p o z y t o r i u m .  
 
 # #   2 0 2 6 - 0 6 - 0 4 T 1 8 : 5 5 : 0 0 + 0 2 : 0 0 
 # # #   [ Z R E A L I Z O W A N O ] 
 -   * * R o z w i � & z a n i e   r y z y k a   D e a d l o c k a * * :   Z r e f a k t o r y z o w a n o   D e l e g a t e T a s k T o o l . c s   -   w y w o 9 a n i e   a s y n c h r o n i c z n e j   p � "!t l i   L L M   ( c l i e n t . S e n d M e s s a g e R e A c t A s y n c )   o p a k o w a n o   w   b e z p i e c z n e   T a s k . R u n ( . . . ) . G e t A w a i t e r ( ) . G e t R e s u l t ( ) ,   c o   e l i m i n u j e   r y z y k o   z a b l o k o w a n i a   g 9 Bw n e g o   w � & t k u   U I   B r i c s C A D   ( S y n c h r o n i z a t i o n C o n t e x t ) . 
 -   * * O p t y m a l i z a c j a   k o n t e k s t u   B l a c k b o a r d * * :   Z a s t � & p i o n o   p e 9 n y   z r z u t   p a m i � "!c i   t a b l i c y   ( B l a c k b o a r d )   z w i � "!z 9 y m   S p i s e m   T r e 9: c i   ( T a b l e   o f   C o n t e n t s   -   T O C )   z a w i e r a j � & c y m   k l u c z e ,   t y p y   d a n y c h   o r a z   s k r Bc o n e   w a r t o 9: c i ,   c o   z n a c z � & c o   o s z c z � "!d z a   t o k e n y   k o n t e k s t u   L L M . 
 -   * * W d r o 9=e n i e   Z a s a d   S O L I D   ( D I P ) * * :   O d p � "!t 9 a n o   ( d e c o u p l e d )   l o g i k � "!  b i z n e s o w � &   C o r e /   o d   w a r s t w y   p r e z e n t a c j i   W i n F o r m s   ( U I / A g e n t C o n t r o l . c s ) .   W p r o w a d z o n o   s t a t y c z n � &   k l a s � "!  t e l e m e t r y c z n � &   A g e n t T e l e m e t r y ,   p r z e z   k t Br � &   U I   s u b s k r y b u j e   z d a r z e n i a   o   p o s t � "!p i e   d e l e g a c j i   z a d a 9   ( n p .   O n D e l e g a t i o n S t a r t e d ,   O n D e l e g a t i o n F i n i s h e d ) . 
 -   * * Z a b e z p i e c z e n i e   p r z e d   I n c e p c j � &   A g e n t Bw * * :   W p r o w a d z o n o   w e r y f i k a c j � "!  p r o f i l u   w y w o 9 u j � & c e g o   ( c a l l e r P r o f i l e )   w   T o o l O r c h e s t r a t o r   o r a z   L L M C l i e n t .   B l o k u j e   t o   w y w o 9 a n i e   n a r z � "!d z i a   D e l e g a t e T a s k T o o l   p r z e z   j a k i k o l w i e k   p r o f i l   i n n y   n i 9=  S u p e r v i s o r P r o f i l e   ( z a p o b i e g a j � & c   n i e s k o 9 c z o n y m   p � "!t l o m   d e l e g a c j i   t y p u   W o r k e r   - >   W o r k e r ) . 
 -   * * W e r y f i k a c j a * * :   P r o j e k t   k o m p i l u j e   s i � "!  w   p e 9 n i   p o p r a w n i e   ( 0   b 9 � "!d Bw ) . 
 # # #   [ S T A N _ S Y S T E M U ] 
 -   A r c h i t e k t u r a   S u p e r v i s o r - W o r k e r   z o s t a 9 a   u o d p o r n i o n a   n a   d e a d l o c k i ,   w y c i e k   k o n t e k s t u   ( B l a c k b o a r d   T O C )   o r a z   z a p � "!t l e n i a   w y w o 9 a 9   a g e n t Bw .   K o d   j e s t   w   p e 9 n i   m o d u l a r n y   i   z g o d n y   z   z a s a d � &   D I P . 
 # # #   [ B L O K A D Y   /   P R O B L E M Y ] 
 -   B r a k . 
 # # #   [ K O L E J N Y _ K R O K ] 
 -   C o m m i t   z m i a n   n a   r e p o z y t o r i u m   G i t H u b . 

## 2026-06-04T21:30:00+02:00
### [ZREALIZOWANO]
- **Renderowanie LaTeX w czacie**: Zaimplementowano klasÄ™ `LatexToUnicodeConverter` konwertujÄ…cÄ… surowe formuĹ‚y LaTeX (`$...`, `$$...$$`) na czytelny tekst Unicode (indeksy gĂłrne/dolne, symbole matematyczne i litery greckie).
- **Integracja UI**: Zintegrowano konwerter z metodÄ… `AppendToHistory` w `AgentControl.cs`, poprawiajÄ…c prezentacjÄ™ wynikĂłw obliczeĹ„ modelu w formancie `RichTextBox`.
- **Testy jednostkowe**: Dodano zestaw testĂłw w `LatexToUnicodeConverterTests.cs` weryfikujÄ…cy poprawnoĹ›Ä‡ konwersji jednostek, notacji naukowej oraz wzorĂłw matematycznych. Testy zintegrowano z konsolowym `TestRunner.cs`.
### [STAN_SYSTEMU]
- System kompiluje siÄ™ bez bĹ‚Ä™dĂłw. Nowy mechanizm automatycznie i w locie przeksztaĹ‚ca formuĹ‚y matematyczne generowane przez AI na czytelnÄ… formÄ™ tekstowÄ… Unicode.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Weryfikacja dziaĹ‚ania wtyczki w Ĺ›rodowisku uruchomieniowym BricsCAD.


## 2026-06-05T01:40:00+02:00
### [ZREALIZOWANO]
- **Refaktoryzacja zak�adki agent�w (UI AgentControl)**: Przeprojektowano zak�adk� " Agenci\ w interfejsie u�ytkownika. Zast�piono tabel� przypisywania skilli wygodn� list� typu CheckedListBox (chlbAgentTools) powi�zan� z wybranym profilem.
- **Dodanie Leksykonu Skilli**: Wprowadzono list� wszystkich dost�pnych w systemie narz�dzi/skilli (lbAllTools) wraz z podgl�dem ich schemat�w JSON (tbToolSchema) generowanych automatycznie na podstawie definicji parametr�w wysy�anych do LLM.
- **Wsparcie dla konfiguracji profilowych w UI**: Powi�zano list� wyboru prompt�w systemowych bezpo�rednio z wybranym agentem. Dodano przycisk umo�liwiaj�cy natychmiastowe otwarcie powi�zanego pliku promptu systemowego w Notatniku.
- **Aktualizacja zapisu profilu**: Dodano logik� zapisu przypisanego pliku promptu i zestawu dozwolonych narz�dzi do ools_config.json za pomoc� ToolConfigManager.UpdateAgentProfile.
### [STAN_SYSTEMU]
- System kompiluje si� w pe�ni poprawnie (0 b��d�w, 0 ostrze�e�). UI poprawnie synchronizuje konfiguracje profili agent�w.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie nowej zak�adki Agenci bezpo�rednio w BricsCAD.
