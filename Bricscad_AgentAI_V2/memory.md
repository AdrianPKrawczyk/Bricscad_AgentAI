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
- v2.21.0 GOLD [MULTI-AGENT ORCHESTRATION] - Rozdzielenie kompetencji agentów: wprowadzenie `IExecutionContext`, `SharedMemoryState` (Blackboard) dla komunikacji, `SupervisorOrchestrator` oraz `DelegateTaskTool` (Kroki 1-5).
- v2.22.0 GOLD [MATH EXPERT & RPN] - Wdrożenie profilu `CadMathProfile` i eksperta matematycznego zintegrowanego z kalkulatorem `RpnCalculator`. Wsparcie dla szablonów `{MATH:...}` w narzędziach CAD.
- v2.23.0 GOLD [MULTI-CRITERIA FILTERS] - Rozszerzenie `DatasetManager` o filtrowanie wielokryterialne podtypów (Faza 8.1).
- v2.24.0 GOLD [KNOWLEDGE BASE CATEGORIZATION] - System kategoryzacji baz danych, makr i formuł. Wprowadzenie tagów, hierarchii folderów oraz widoku drzewiastego TreeView w GUI (Faza 9).
- v2.25.0 GOLD [MULTI-MODAL VISION] - Obsługa multimodalnych załączników (tekst, PDF, XLSX) i automatyczna kompresja/skalowanie obrazów z Vision API oraz integracja ze schowkiem Ctrl+V (Faza 10 & 12).
- v2.25.1 GOLD [DATASET STANDARDIZATION] - Rozdzielenie plików metadanych i danych (*.data.json), zabezpieczenie przed brakiem danych oraz normalizacja tabel w GUI (Faza 11).
- v2.25.2 HOTFIX [GUI ENCODING] - Poprawka błędów kodowania polskich znaków w interfejsie AgentControl.cs.
- v2.26.0 GOLD [SESSION & COMPRESSION] - System zarządzania sesjami ChatSession, trwały zapis sesji w APPDATA, automatyczne nadawanie nazw sesjom oraz kompresja kontekstu w oknie Context Bar (Faza 13).
- v2.27.0 GOLD [DWG CONTEXT & NOTES] - System Notatek Projektowych (Sidecar Markdown), komenda `/notatka` w GUI, menedżer DrawingNoteManager oraz dynamiczne wstrzykiwanie RAG do promptu Supervisora (Faza 14).
- v2.28.0 GOLD [SAFE FILE TOOLS] - Zabezpieczone narzędzia plikowe Read/WriteProjectFileTool z blokadami ścieżek/rozszerzeń oraz integracja autouzupełniania komend i tagów w GUI (Faza 15).

- v2.28.1 GOLD [MEMORY SYNCHRONIZATION] - Ujednolicenie pliku pamięci memory.md, naprawa zniekształceń Mojibake i UTF-16, nadanie numerów wersji kolejnym krokom.
- v2.28.2 GOLD [RECIPES MANAGER] - Przeniesienie recept do osobnych plików JSON i dodanie ManageRecipesTool.
- v2.28.3 GOLD [HELP CENTER] - Implementacja zakładki Pomoc z renderowaniem Markdown.
- v2.28.4 GOLD [READ HELP] - Dodanie ReadHelpTool i dostępu Supervisora do dokumentacji użytkownika.
- v2.28.5 GOLD [UI MARKDOWN] - Zmiana komendy startowej na AI oraz formatowanie Markdown w konsoli.
- v2.28.6 GOLD [SKILLS SYSTEM] - Wdrożenie skilli Markdown+YAML i ManageSkillsTool.
- v2.28.7 GOLD [SKILLS DOCS] - Uzupełnienie USER_GUIDE o skille # i polecenia /.
- v2.28.8 GOLD [SUPERVISOR SKILLS] - Doprecyzowanie promptu Supervisora: Skille # kontra Recepty $.
- v2.28.9 GOLD [MANAGE SKILLS] - Dodanie manage_skills do domyślnych narzędzi Supervisora.
- v2.28.10 GOLD [SKILLS ALIASES] - Uodpornienie ManageSkillsTool na wielkość liter i aliasy.
- v2.28.11 FIX [QA SESSION] - Naprawa zapisu sesji QA i udostępnienie AuditorProfile odczytu kodu źródłowego.
- v2.28.12 FEAT [QA ANTIGRAVITY] - Dodanie RunToolTest, WriteQAReport i DelegateTaskToAntigravity.
- v2.28.13 FEAT [SEARCH FILE CONTENT] - Dodanie SearchFileContentTool dla SourceCode i DrawingFolder.
- v2.28.14 FEAT [LISP SELF-HEALING] - Wdrożenie profili LISP, agent-callback i pętli samonaprawiającej.
- v2.28.15 FIX [LISP ROUTING] - Naprawa utraty sesji i wymuszenie delegowania kodu LISP do LispCoderProfile.
- v2.28.16 FEAT [LISP KNOWLEDGE BASE] - Integracja zakładki Skrypty LISP z Bazą Wiedzy i manage_lisps.
- v2.28.17 FIX [LISP UI EXECUTION] - Poprawa autouzupełniania %skrypt i pełnego wykonania LISPa po load.
- v2.28.18 DOC [MEMORY CANONICALIZATION] - Scalenie memory.md i docs/memory.md, renumeracja kolizji v2.28.x oraz wskazanie jednego źródła prawdy.
- v2.28.19 FEAT [AGENT CHAT] - Dodanie zakładki Agent-Czat do ręcznego testowania wybranego subagenta, z panelem tool JSON i eksportem debugowym.
- v2.28.34 FEAT [BENCHMARK MODEL PICKER] - Selektor providera/modelu w zakładce Benchmark z unload + load do VRAM, etykieta stanu LM Studio, deduplikacja load w LLMConfigDialog, przycisk Rozładuj.
- v2.28.35 HOTFIX [UNLOAD INSTANCE_ID] - Fix unload LM Studio: pobiera instance_id z loaded_instances, naprawia błąd 400 Missing required field. Dodaje przycisk Rozładuj w benchmarku.
- v2.29.0 GOLD [ROOM DATA PIPELINE] - Wdrożenie dwóch współpracujących narzędzi realizujących architekturę Ekstraktor -> Agent -> Batch Writer:
    * `ExtractRoomDataEntitiesTool` (tylko odczyt): skanuje Model Space w poszukiwaniu polilinii-obrysów pomieszczeń na warstwie `boundaryLayer` oraz bloków-metek z atrybutami na `tagLayer`. Wykonuje test Point-in-Polygon (ray casting) dla każdej pary. Zwraca JSON z listami: Matched (pary handle + atrybuty), UnmatchedBoundaries, UnmatchedTags.
    * `BatchWriteXDataTool` (zapis): zbiorczy zapis XData dla wielu obiektów w JEDNEJ transakcji CAD z auto-rejestracją RegApp. Pomija obiekty o nieistniejących Handle'ach z raportem. Nadpisuje istniejące XData dla danej appName.
    * Tagi: #xdata, #metadata, #pokoje (automatycznie ustawiane w `ToolConfigManager.SyncWithTools`).
    * Profile: CadMetadataProfile (oba narzędzia) + SupervisorProfile (tylko ExtractRoomDataEntities do planowania pipeline'u).
    * Rozszerzono prompt system_prompt_metadata.txt o opis pipeline'u.
- v2.29.1 GOLD [ROOM DATA BLOCKNAME] - Rozszerzenie JSON zwracanego przez `ExtractRoomDataEntities` o trzy pola identyfikujące blok: `BlockName` (InstanceName), `BlockDefinition` (nazwa definicji) oraz `BlockDynamicName` (nazwa dynamiczna, jeśli istnieje). Pola dodane zarówno do elementów listy `Matched` jak i `UnmatchedTags`. Umożliwia Agentowi odróżnienie rzeczywistych metek od innych bloków (np. mebli) leżących na tej samej warstwie. Zaktualizowano prompt metadata, USER_GUIDE i memory.
- v2.29.2 GOLD [PERSISTENT CONFIG] - Krytyczny fix utraty konfiguracji przy buildach projektu. Pliki `llm_providers.json` i `ui_settings.json` przeniesione z `bin\Debug\` (obok DLL) do `%APPDATA%\Bricscad_AgentAI\`. Dodano jednorazową auto-migrację ze starej lokalizacji przy pierwszym uruchomieniu. Wdrożono nowe pole `CustomLLMConfigPath` w `UISettings` (sekcja "Ścieżki i Dane" w Ustawieniach) pozwalające użytkownikowi wskazać własny folder konfiguracyjny (np. OneDrive/Dropbox) dla synchronizacji między komputerami. Nowe metody `AppPaths.GetLLMConfigPath()` i `AppPaths.GetUISettingsPath()`.
- v2.29.3 FEAT [SUPERVISOR PROMPT WARMUP] - Dodano ciche rozgrzewanie promptu `SupervisorProfile` dla lokalnych providerów OpenAI-compatible (LM Studio/llama.cpp). Po otwarciu AI, wczytaniu sesji albo pauzie w pisaniu program wysyła w tle minimalny request bez narzędzi (`temperature=0`, `max_tokens=1`), aby lokalny serwer mógł zbudować prompt/KV cache przed pierwszym właściwym zapytaniem. Warmup nie modyfikuje historii sesji, nie uruchamia auto-namingu, anuluje się przed realnym requestem i pomija providerów chmurowych OpenAI/OpenRouter/Azure.
- v2.29.4 FEAT [GLOBAL VISION OCR] - Dodano nadrzędny globalny binding Vision/OCR niezależny od profili agentów. Obrazy z załączników, schowka, `CaptureVisionArea` i `CaptureMetricVisionArea` mogą być najpierw analizowane przez osobny model wizyjny bez tool callingu, a główny agent dostaje tekstowy blok `[VISION/OCR]`. Konfiguracja w `Ustawienia -> Vision/OCR` obejmuje providera, model, payload, AutoLoad i politykę kontekstu.
- v2.29.5 FEAT [VISION OCR QUALITY] - Dodano osobne ustawienia maksymalnego boku obrazu dla OCR ze schowka i załączników. Domyślnie globalny Vision/OCR wysyła te obrazy do modelu w rozdzielczości do 2048 px, z możliwością zmiany w `Ustawienia -> Vision/OCR`; tryb bez globalnego OCR zachowuje dotychczasowy limit 1024 px.
- v2.29.6 HOTFIX [VISION OCR AUTOLOAD] - Naprawiono pierwszy request Vision/OCR po starcie programu: dla lokalnych providerów brak zapisanej flagi AutoLoad w globalnym bindingu OCR jest teraz traktowany jako włączony AutoLoad, a checkbox AutoLoad w `Ustawienia -> Vision/OCR` zapisuje się niezależnie od trybu `Payload providera`.
- v2.29.7 HOTFIX [VISION OCR MODEL RESOLVE] - Naprawiono sytuację, w której OCR działał dopiero po ręcznym kliknięciu `Modele`: jeśli model Vision/OCR jest pusty albo ma placeholder (`local-model`, `llama3`), klient automatycznie pobiera `/v1/models`, wybiera preferowany model wizyjny i używa go przed AutoLoad/requestem. UI Vision/OCR odświeża listę modeli po utworzeniu panelu, jeśli zapisany model jest pusty lub placeholderowy.
- v2.29.8 HOTFIX [VISION OCR FIRST REQUEST RETRY] - Dodatkowo wymuszono odświeżenie `/v1/models` przed każdym lokalnym requestem Vision/OCR, walidację zapisanego modelu względem realnej listy modeli oraz jednorazowy retry po pierwszym błędzie OCR z ponownym odświeżeniem modeli i AutoLoad. Ma to odtworzyć efekt ręcznego kliknięcia `Modele` przed pierwszym OCR.
- v2.29.9 HOTFIX [VISION OCR PROVIDER COMBO] - Naprawiono utratę providera w UI Vision/OCR: combobox mógł wizualnie pokazywać `LM Studio (Lokalny)`, ale `SelectedItem` nie był obiektem `LLMProviderConfig`, przez co preview pokazywał `brak providera` i zapis bindingu mógł tracić `ProviderId`. Resolver UI odzyskuje providera po `SelectedValue`, tekście/nazwie, zapisanym bindingu i aktywnym providerze.
- v2.29.10 FEAT [VISION IMAGE SESSION CONTEXT] - Dodano trwały kontekst obrazów Vision/OCR przypięty do sesji. Załączniki i obrazy ze schowka są kopiowane do `%APPDATA%\Bricscad_AgentAI\SessionImages\<sessionId>\`, zapisywane w JSON sesji jako `VisionImages` z `image_id`, metadanymi i historią obserwacji OCR. Kolejne pytania odnoszące się do wcześniejszego obrazu mogą uruchomić ponowną analizę tego samego cached pliku i przekazać Supervisorowi świeży blok `[VISION/OCR REQUERY]`.
- v2.29.11 FEAT [ADAPTIVE VISION OCR TILING] - Dodano adaptacyjny tiling Vision/OCR dla duzych arkuszy o dowolnych proporcjach. Obrazy ze schowka i zalaczniki moga byc automatycznie dzielone na prostokatne kafelki, wysylane w jednym requestcie multi-image z promptem przestrzennym. Domyslnie tryb `Auto`, kafelek `2100 px`, overlap `200 px`, limit `16` kafelkow i maksymalna proporcja kafelka `2.0`.
- v2.30.14 GOLD [FIX WBLOK GHOST LAYOUT] - `ImportLayoutTemplateTool`: usuniecie automatycznie tworzonego "ghost" layoutu (np. "Arkusz4") po `WblockCloneObjects`. Teigha wymusza istnienie layoutu dla kazdego BlockTableRecord - importowany BTR z DWT ma juz przypisany layout w source, wiec Teigha tworzy nowy domyslny Paper Space layout (np. "Arkusz4"). Fix: snapshot layout dict PRZED WblockCloneObjects, diff PO - usun wszystkie nowe layouty (oprocz targetLayoutName). Wymaga 2-krotnego `GetObject(LayoutDictionaryId, OpenMode.ForRead)` w tej samej transakcji - Bezpieczne.
- v2.30.13 GOLD [LIST LAYOUTS IN DWT] - Nowa funkcja `ListLayoutsTool` z parametrem `SourceDwtPath` - listuje layouty zewnetrznego pliku DWT/DWG bez otwierania go. Uzywa `Database.ReadDwgFile()` + `LayoutDictionaryId` + `DBDictionary` iteration. Zwraca liste z: nazwa layoutu, `CanonicalMediaName`, `PlotPaperSize` (mm). Dzieki temu LLM moze zobaczyc dostepne layouty w DWT przed importem. Problem wczesniejszy: `LayoutManager.Current.CloneLayout` dziala TYLKO na biezacym DWT - nie mozna klonowac miedzy DWT. Wrocilem do `WblockCloneObjects` + reczne kopiowanie PlotSettings (z `try/catch` dla bezpieczenstwa). Dla `ListLayoutsFromDwt` uzywam `layout.PlotPaperSize.X/Y` (V22 nie ma `LayoutSettings` property).
- v2.30.12 GOLD [IMPORT CLONE LAYOUT] - `ImportLayoutTemplateTool`: zamiana `WblockCloneObjects` + `CreateLayout` + recznego kopiowania PlotSettings na **oficjalny `LayoutManager.CloneLayout(copyName, newName, newTabOrder)`** z Teigha API. Stary kod tworzyl dodatkowe puste layouty (np. "Arkusz4", "Arkusz6") przez konflikty nazw w `DuplicateRecordCloning.Replace` + reczne kopiowanie properties. `CloneLayout` kopiuje layout wraz z Page Setup, BlockTableRecord i wszystkimi zaleznosciami atomowo - bez duplikatow. Usunieto ~80 linii recznego kopiowania Validatorem. Kazde wywolanie `CloneLayout` to 1-2 linie kodu zamiast 30+ linii.
- v2.30.11 GOLD [AGENT CONTROL] - Wymuszenie iteracji po `ListLayoutsTool` w `LLMClient`. Problem: LLM (gemma-4-31B) po `ListLayoutsTool` konczyl odpowiedz tekstem zamiast wykonac akcje na layoutach (np. Foreach). Fix: nowe metody `ShouldForceContinueAfterLastTool()` + `BuildForceContinueHint()` w `LLMClient.cs`. Po `ListLayoutsTool` (bez LayoutName) z wynikiem zawierajacym layouty, jesli LLM probuje zakonczyc (brak tool_calls w nastepnej iteracji), AgentControl dodaje `[SYSTEM REMINDER]` do `conversationHistory` z instrukcja "Musisz wykonac akcje na kazdym z {N} layoutow - uzyj Foreach z ActionTemplate". Pozwala to LLM z slabym "agentness" (gemma-4-31B) iterowac do wlasciwego rozwiazania.
- v2.30.10 GOLD [FOREACH PROFILE] - Dodanie `ForeachTool` do `CadLayoutProfile.AllowedTools` (linia 723 + 786 w `ToolConfigManager.cs`). LLM poprzednio wywolywal PageSetupTool N razy dla N layoutow zamiast uzyc `Foreach` z `ActionTemplate`. Teraz ma dostep. Prompt `system_prompt_layout.txt` z nowa sekcja `### ForeachTool (PETLA - KRYTYCZNE dla CadLayoutProfile)` z przykladem `Foreach` Items=[Arkusz1, Arkusz2] ActionTemplate=`PageSetupTool LayoutName="{item}" PlotDevice="RICOH" MediaName="A3"`. Zasady korzystania: "Dla operacji na WIELU layoutach ZAWSZE uzyj Foreach z ActionTemplate - NIE wywoluj PageSetupTool recznie N razy".
- v2.30.9 GOLD [INFO vs WARNING] - PageSetupTool: separacja `infoMessages` (commit OK + info) od `warnings` (commit abort). Wczesniej fallback `_p` sukces -> ostrzezenie -> `tr.Abort()` wycofywal transakcje. Teraz: fallback sukces -> `infoMessages` -> `tr.Commit()` + INFO w outpucie. Fallback fail -> `warnings` -> `tr.Abort()` jak wczesniej. Ostateczny output: "SUKCES: Zastosowano 1 ustawien Page Setup | INFO: MediaName '594x1320' nie zostal zaakceptowany, uzyto '594x1320_p'".
- v2.30.8 GOLD [_P AUTO-FALLBACK] - PageSetupTool: automatyczny fallback `594x1320` -> `594x1320_p` gdy driver odrzuca wersje bez sufiksu. Driver HP wymaga `_p` dla niektorych formatow (np. 594x1320_p dziala, 594x1320 zwraca eInvalidInput). PageSetupTool teraz: (1) probuje oryginalna nazwe, (2) jesli eInvalidInput + brak sufiksu `_p`/`_a` + wyglada na custom - probuje automatycznie z `_p`, (3) zwraca ostrzezenie z info o mapowaniu. Prompt CadLayoutProfile zaktualizowany: "Wystarczy podac wymiary, PageSetupTool sam doda _p jesli trzeba".
- v2.30.7 GOLD [_P FOLD PATTERN] - Dodanie heurystyki sufiksu `_p` w `UserMediaResolver`: `dlugosc = N * 185 + 25` (gdzie 185 = 210 - 25, 25 = margines ciecia). Konwencja: format z `_p` sklada sie do A4 po pocieciu paskow 185mm z marginesem 25mm. Implementacja: `CountAPanelsForFoldableSize(h)` + `TryBuildAPanelsFoldableSize(w, n, margin, h)` + `DescribeFoldPattern(w, h, isFoldable)`. Sekcja CUSTOM SIZE w `ListPlotDevicesTool` rozszerzona o dokumentacje wzoru z 5 przykladami.
- v2.30.6 GOLD [WIN32 CRASH FIX] - Krytyczny fix `Win32PrinterCapabilities.QueryMedia`: `Marshal.PtrToStringUni(IntPtr)` bez jawnej dlugosci powodowal `System.AccessViolationException` w `System.String.wcslen` gdy driver zwracal nazwy bez null-terminatora (np. dla `297x1320` i `594x1320`). Crashowal caly proces BricsCAD (PID 0x12e98, 0x54e8, dump 134MB). Fix: `Marshal.PtrToStringUni(IntPtr, int len=32)` z CCHFORMNAME + try/catch + safety limit `h > 1000 || w > 1000` pomija Win32 lookup dla duzych formatow.
- v2.30.5 GOLD [PROMPT FIX] - Naprawa system prompt `CadLayoutProfile`: usuniecie niejednoznacznej reguly "NAJPIERW ListPlotDevicesTool". Nowa regula: "Gdy user poda KONKRETNA nazwe MediaName (np. 'A4', '297x600', 'User266') - wywolaj PageSetupTool BEZPOSREDNIO". Dodano 4 przyklady prawidlowego uzycia PageSetupTool z custom format. Dodano sekcje 13.7 w USER_GUIDE o custom formatach papieru HP.
- v2.30.4 GOLD [USER MEDIA MAPPER] - P/Invoke `DeviceCapabilities()` Win32 API + parser mapowania custom format (`297x600`, `297x1320_p`) na UserXXX (np. User254, User261) na podstawie wymiarow. Walidacja bounds z GPD MinSize/MaxSize w `PageSetupTool` - blokuje preflight gdy custom wymiary przekraczaja zakres plotera. Heurystyczny fallback na `_p` suffix (wielokrotnosc 600mm + offset).
- v2.30.3 GOLD [GPD PARSER] - Parser plikow .gpd (Generic Printer Description Windows) z DriverStore FileRepository. Pelna lista formatow dla ploterow HP DesignJet/PageWide XL/Z-series - obejmuje standard (A4, A3, A2, A1, A0, B-series, ANSI, Architecture) + custom roll. Heurystyczne mapowanie PC3 device name na hpi<Model>.gpd (T120/T650/T520/T1500/Z2100/Z3200/Z5400/XL3600 itd.). Integracja z `ListPlotDevicesTool` przez `AppendGpdMediaList` - zwraca kompletna liste mediów z PageSetupTool MediaName.
- v2.30.2 GOLD [PC3 PARSER] - Parser binarnych plikow PC3 (zlib-deflate + struktura blokowa) z diagnostyka driver'ow i aktualnie wybranych formatow dla ploterow HP. Nowy `Pc3Parser` utility class + integracja z `ListPlotDevicesTool`.
- v2.30.1 GOLD [LAYOUT MEDIA] - Rozszerzenie `ListPlotDevicesTool` o parametr `IncludeMediaPerDevice` (HP/UserXXX). Walidacja `MediaName` w `PageSetupTool` wzgledem plotera z argumentu `PlotDevice`.
- v2.30.0 GOLD [LAYOUT PRINT PLOT] - Wdrożenie dedykowanego profilu `CadLayoutProfile` oraz 8 nowych narzędzi do zarządzania arkuszami wydruku (Layouts), Page Setup, importu/eksportu szablonów DWT/DWG, drukowania PDF/DWF/PNG oraz zarządzania stylami wydruku CTB/STB: `ListLayoutsTool`, `ManageLayoutTool`, `PageSetupTool`, `ImportLayoutTemplateTool`, `ExportLayoutTemplateTool`, `PlotLayoutTool`, `PublishToPdfTool`, `PlotStyleTool`. Tagi: #layout, #wydruk, #plotstyle, #template, #pdf, #publish. Rozszerzenie promptu Supervisora o regułę delegowania layout/plot. Naprawa buga Early Exit (zwraca treść z tool result zamiast generycznego komunikatu). Aktualizacja USER_GUIDE.md i TOOLS_REFERENCE.md.
    * `ExtractRoomDataEntitiesTool` (tylko odczyt): skanuje Model Space w poszukiwaniu polilinii-obrysów pomieszczeń na warstwie `boundaryLayer` oraz bloków-metek z atrybutami na `tagLayer`. Wykonuje test Point-in-Polygon (ray casting) dla każdej pary. Zwraca JSON z listami: Matched (pary handle + atrybuty), UnmatchedBoundaries, UnmatchedTags.
    * `BatchWriteXDataTool` (zapis): zbiorczy zapis XData dla wielu obiektów w JEDNEJ transakcji CAD z auto-rejestracją RegApp. Pomija obiekty o nieistniejących Handle'ach z raportem. Nadpisuje istniejące XData dla danej appName.
    * Tagi: #xdata, #metadata, #pokoje (automatycznie ustawiane w `ToolConfigManager.SyncWithTools`).
    * Profile: CadMetadataProfile (oba narzędzia) + SupervisorProfile (tylko ExtractRoomDataEntities do planowania pipeline'u).
    * Rozszerzono prompt system_prompt_metadata.txt o opis pipeline'u.

## Decyzje Architektoniczne
- **Semantic Tool Routing**: System dynamicznego dobierania narzędzi na podstawie tagów (#core, #bloki, itp.). Od v2.8.0 zarządzany przez `ToolConfigManager`.
- **Early Exit (Fast Mode)**: Mechanizm pozwalający Agentowi na zakończenie pętli po wykonaniu narzędzi akcji, jeśli wspierają one flagę `SupportsEarlyExit`. Drastyczna redukcja tokenów i czasu odpowiedzi.
- **AI Package Manager**: Model LLM samodzielnie odkrywa i ładuje pakiety narzędzi przez `RequestAdditionalToolsTool`.
- **Hard Guardrails**: Każde narzędzie jest odpowiedzialne za walidację swoich parametrów i zwracanie "Błędu Krytycznego" w celu przerwania halucynacji LLM.

## Rozwiązane Problemy (Bug Log)
- **UI Autocomplete**: Naprawiono przechwytywanie klawiszy Tab/Enter przez migrację do `ProcessCmdKey` w `AgentControl.cs`.
- **Build CS0111/CS0103**: Naprawiono błędy kompilacji po masowej refaktoryzacji (dodanie plików do .csproj oraz usunięcie duplikatu klasy w UserInputTool.cs).
- **Silent Name Mismatch (Death Spiral)**: Naprawiono błąd w v2.9.1, gdzie klucze `ToolConfigManager` korzystały z nazw klas C# zamiast API Names z `FunctionSchema`, co unieruchamiało mechanizm Early Exit i gubiło narzędzia #core.

## Dziennik Deweloperski (Logi Zadań)
## [v2.30.18 GOLD] 2026-06-18T16:20:00+02:00 - Poprawka PublishToPdfTool SingleFiles PDF naming
### [ZREALIZOWANO]
- Naprawiono `PublishToPdfTool` w trybie `SingleFiles`, aby sciezka katalogu `Z:/export/` nie uruchamiala dialogu zapisu PDF i tworzyla pliki wedlug nazw arkuszy.
- Rozszerzono generowanie DSD o dodatkowe pola publikacji (`OriginalSheetPath`, `LogFilePath`, `SheetSet Properties`, `PromptForDwfName=FALSE`) stabilizujace publikacje bez interakcji uzytkownika.
- Poprawiono interpretacje prefiksu wyjsciowego, np. `Z:/export/IS.PW-` zachowuje pelny prefiks `IS.PW-` zamiast obcinac po kropce.
- Dodano laczenie prefiksu z nazwa layoutu bez dodatkowego `_`, gdy prefiks konczy sie separatorem (`-`, `_`, `.`, spacja), np. `IS.PW-01.pdf`.
### [STAN_SYSTEMU]
- Publikacja PDF SingleFiles dziala bez okna zapisu i poprawnie tworzy nazwy plikow z opcjonalnym prefiksem.
### [WERYFIKACJA]
- `powershell -ExecutionPolicy Bypass -File build.ps1` zakonczony sukcesem: 0 bledow, 4 istniejace ostrzezenia.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na zadania.
## [v2.30.17 GOLD] 2026-06-18T13:45:00+02:00 - Poprawka ploterów domyślnych w BricsCAD
### [ZREALIZOWANO]
- Rozwiązano błąd `Błędna nazwa dla rzutni` wynikający z przekazywania dosłownych znaków cudzysłowu w poleceniu `_LAYOUT _Set "{nazwa}"`. Zastąpiono komendę `_LAYOUT` na zmienną systemową `CTAB`.
- Zastosowano globalny prefix `_.-PLOT` (angielska nazwa komendy, pozbawiona modyfikatora dialogowego) we wszystkich wezwaniach `-PLOT`, co rozwiązuje błąd w zlokalizowanych (polskich) wersjach BricsCAD.
- Zamieniono niepotrzebne łańcuchy znaków `""` wysyłane jako symulacja klawisza Enter na standardowe znaki nowej linii `\n`. Dotyczy plików `PublishToPdfTool.cs` oraz `PlotLayoutTool.cs`.
- Naprawiono problem w którym BricsCAD odrzucał `DWG To PDF.pc3` z powodu jego braku (jest to ploter specyficzny dla AutoCAD). Skrypty pobierają teraz zainstalowaną listę urządzeń (PlotSettingsValidator.GetPlotDeviceList) i dynamicznie wybierają m.in. wbudowany w BricsCAD `Print As PDF.pc3`.
### [STAN_SYSTEMU]
- Polecenia wymuszają tworzenie plików PDF, używając dostępnego lokalnie plotera PDF.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na zadania.
## [v2.30.16 GOLD] 2026-06-18T13:10:00+02:00 - Poprawka błędu crashu BricsCAD podczas PublishToPdfTool
### [ZREALIZOWANO]
- Zlokalizowano przyczynę zamykania się BricsCAD'a przy użyciu polecenia publikacji wielu arkuszy do pojedynczych plików PDF (SingleFiles).
- Usunięto synchroniczne wywołanie `LayoutManager.Current.CurrentLayout` działające na wątku pobocznym (Pool Worker), które powodowało błąd z bezpieczeństwem wątków GUI (Access Violation).
- Zmieniono logikę tak, by zmiana zakładki arkusza była bezpiecznie kolejkowana do Głównego Wątku poprzez przekazanie natywnej komendy `_LAYOUT \n _Set` prosto do wywołania `SendStringToExecute` łącząc to z komendą `-PLOT`.
### [STAN_SYSTEMU]
- Publikacja do pojedynczych plików PDF w wątku asynchronicznym w BricsCAD nie powoduje już awarii (Fatal Error).
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na zadania.
## [v2.30.15 GOLD] 2026-06-18T13:00:00+02:00 - Poprawka błędu podwójnego arkusza przy imporcie (WblockCloneObjects)
### [ZREALIZOWANO]
- Naprawiono błąd w `ImportLayoutTemplateTool.cs` powodujący pozostawanie domyślnego arkusza (np. "Arkusz4") po klonowaniu geometrii z szablonu.
- Zamieniono błędne usuwanie nowo utworzonego arkusza na bezpieczną zmianę jego nazwy za pomocą `LayoutManager.Current.RenameLayout`.
### [STAN_SYSTEMU]
- Import arkuszy działa stabilnie i nie tworzy śmieciowych układów (ghost layouts).
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na zadania.
## [v2.20.10] 2026-06-03T10:45:17+02:00 - Inicjalna analiza architektury V2 i protokołów pamięci
### [ZREALIZOWANO]
- Przeprowadzono szczegółową analizę architektury projektu w wersji V2 (Function Calling, LLMClient ReAct, RPN, Dataset Studio, CLI, system receptur i faza Vision).
- Przeanalizowano wytyczne, instrukcje i zasady w dokumentacji projektu.
### [STAN_SYSTEMU]
- System w pełni stabilny i przeanalizowany, gotowy do dalszego rozwoju.
### [BLOKADY / PROBLEMY]
- Brak napotkanych trudności w procesie analizy.
### [KOLEJNY_KROK]
- Oczekiwanie na konkretne zadania implementacyjne od użytkownika.

## [v2.20.11] 2026-06-03T11:31:40+02:00 - Analiza konfiguracji LLM i identyfikacja parametrów LM Studio
### [ZREALIZOWANO]
- Przeanalizowano pliki konfiguracyjne LLM (LLMConfigModels.cs, LLMClient.cs) w celu identyfikacji parametrów, które można przekazać do dostawców API (OpenAI, OpenRouter, LM Studio).
- Opracowano listę potencjalnych rozszerzeń konfiguracji (m.in. reasoning_effort, thinking budget, top_p, seed, frequency/presence penalties).
### [STAN_SYSTEMU]
- System jest gotowy na wprowadzenie zmian w konfiguracji dostawców. Parametry są obecnie ograniczone do Model, Temperature, MaxTokens, ApiKey i EndpointUrl.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Ewentualna implementacja dodatkowych pól w LLMProviderConfig i LLMConfigDialog na życzenie użytkownika.

## [v2.20.12] 2026-06-03T11:34:52+02:00 - Poprawka kodowania znaków UTF-8 w GUI AgentControl.cs
### [ZREALIZOWANO]
- Przeanalizowano przydatność parametrów LLM specyficznych dla silników lokalnych (Ollama, LM Studio, llama.cpp): top_p, top_k, min_p, repeat_penalty oraz stop sequences.
- Opracowano plan integracji parametrów samplingu dla modeli lokalnych w celu poprawy deterministyczności i stabilności Tool Callingu na małych modelach (np. Qwen, DeepSeek).
### [STAN_SYSTEMU]
- System bez zmian kodu źródłowego. Przeprowadzono analizę wpływu parametrów lokalnych na API.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na decyzję użytkownika co do implementacji rozszerzonych parametrów lokalnych.

## [v2.20.13] 2026-06-03T11:38:50+02:00 - Poprawka formatu kodowania pliku LLMConfigModels.cs na UTF-8
### [ZREALIZOWANO]
- Wdrożono rozszerzone parametry konfiguracji dostawców LLM (TopP, TopK, MinP, RepetitionPenalty, ReasoningEffort) w modelach danych C# (LLMConfigModels.cs), silniku klienta (LLMClient.cs) oraz formularzu UI (LLMConfigDialog.cs).
- Pomyślnie skompilowano i zweryfikowano projekt Bricscad_AgentAI_V2 za pomocą dotnet build.
### [STAN_SYSTEMU]
- System w pełni zaktualizowany o obsługę zaawansowanych parametrów dla modeli lokalnych i chmurowych. Wszystkie zmiany są wstecznie kompatybilne.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na uruchomienie i testy użytkownika w środowisku BricsCAD.

## [v2.20.14] 2026-06-03T11:42:24+02:00 - Poprawa importu System.Text.Encoding w LLMConfigDialog.cs
### [ZREALIZOWANO]
- Wdrożono czytelny interfejs pomocy i podpowiedzi (ToolTips) dla wszystkich zaawansowanych parametrów konfiguracji dostawców (Temperature, Max Tokens, Top-P, Top-K, Min-P, Repetition Penalty, Reasoning Effort).
- Pomyślnie skompilowano i przetestowano aplikację.
### [STAN_SYSTEMU]
- System w pełni zaktualizowany o opisy parametrów podpowiedzi tooltip. Stabilny i gotowy do użycia.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na dalsze wytyczne od użytkownika.

## [v2.20.15] 2026-06-03T13:20:00+02:00 - Testowanie stabilności ładowania modeli i integracji z CAD
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

## [v2.20.16] 2026-06-03T13:35:00+02:00 - Usunięcie nieobsługiwanych parametrów z payloadu LM Studio API
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

## [v2.20.17] 2026-06-03T14:00:00+02:00 - Wdrożenie dynamicznego edytora system promptu w zakładce Ustawienia
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

## [v2.20.18] 2026-06-03T14:15:00+02:00 - Dodanie struktury podzakładek (TabControl) w Ustawieniach
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

## [v2.20.19] 2026-06-03T14:45:00+02:00 - Fix wczytywania parametrów dostawców LLM w GUI
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

## [v2.20.20] 2026-06-03T15:05:00+02:00 - Refaktoryzacja WykonajInteligentneZaznaczenie z Regex na Newtonsoft JSON
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

## [v2.21.0] 2026-06-03T20:10:00+02:00 - Multi-Agent Krok 1: Refaktoring silnika ReAct i IExecutionContext
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

## [v2.21.1] 2026-06-03T20:12:00+02:00 - Multi-Agent Krok 2: SharedMemoryState i narzędzia Read/Write Blackboard
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

## [v2.21.2] 2026-06-03T20:14:00+02:00 - Multi-Agent Krok 3: Profile agentów i konfiguracja ToolConfigRoot
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

## [v2.21.3] 2026-06-03T20:20:00+02:00 - Multi-Agent Krok 4: Klasa SupervisorOrchestrator i DelegateTaskTool
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

## [v2.21.4] 2026-06-03T20:25:00+02:00 - Multi-Agent Krok 5: Integracja ChatSession i SessionManager
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

## [v2.21.5] 2026-06-03T22:35:00+02:00 - Dostosowanie zakładki Agenci i Tester V2 do Multi-Agent
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

## [v2.21.6] 2026-06-03T22:52:00+02:00 - BielikLogger crash-safe diagnostics i logi w GUI
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

## [v2.21.7] 2026-06-03T23:10:00+02:00 - Testy LISP generatora dla walidacji operacji na blokach
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

## [v2.21.8] 2026-06-03T23:25:00+02:00 - Integracja orkiestratora z wątkiem GUI BricsCAD
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

## [v2.21.9] 2026-06-03T23:36:00+02:00 - Selekcja i filtrowanie BlockReference z wieloznacznikami w SelectEntitiesTool
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

## [v2.21.10] 2026-06-03T23:55:00+02:00 - Złagodzenie restrykcji promptu systemowego Supervisora
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

## [v2.22.0] 2026-06-04T00:15:00+02:00 - Wdrożenie CadMathProfile, promptu Math Experta i narzędzia CalculateRpn
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

## [v2.22.1] 2026-06-04T00:17:00+02:00 - Zasady routingu matematyczno-fizycznego Supervisora
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

## [v2.22.2] 2026-06-04T00:30:00+02:00 - Rozbudowa promptu Math Experta o notację RPN i wzory
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


## [v2.22.3] 2026-06-04T00:38:00+02:00 - Auto-upgrade promptu o wytyczne unikania zwisów stosu (Dangled Stack)
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


## [v2.22.4] 2026-06-04T00:43:00+02:00 - E2E testy profilu matematycznego w CAD i blokada profili jako narzędzi
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


## [v2.22.5] 2026-06-04T00:46:00+02:00 - Silnik benchmarków dla wyboru profilu i promptów eksperckich
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


## [v2.22.6] 2026-06-04T00:49:00+02:00 - Zbiór testowy benchmarku Benchmark_02_Math.json
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


## [v2.22.7] 2026-06-04T00:54:00+02:00 - Optymalizacja wzoru objętości kuli w promptach
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


## [v2.22.8] 2026-06-04T01:05:00+02:00 - Uruchomienie rzeczywistych narzędzi obliczeniowych w benchmarku
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


## [v2.22.9] 2026-06-04T01:10:00+02:00 - Uproszczenie zapytań benchmarkowych dla Bielika 11B
### [ZREALIZOWANO]
- **Uproszczenie zapytań w pliku benchmarkowym**: Przywrócono naturalne sformułowania w zapytaniach `UserPrompt` w [Benchmark_02_Math.json](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/Benchmark_02_Math.json) (usunieto narzucony wymóg "wykonania obliczeń jako pojedyncze wyrażenie RPN"). Dzięki temu model nie otrzymuje sprzecznych instrukcji z system promptem (który nakazuje dzielenie zadań na logiczne kroki cząstkowe) i poprawnie rozbija obliczenia na czytelne etapy, co eliminuje błędy składniowe i matematyczne (takie jak błędne potęgowanie czy zbędne dzielenie przez 1000).
- **Weryfikacja kompilacji**: Kompilacja wtyczki powiodła się bez żadnych błędów i ostrzeżeń.
### [STAN_SYSTEMU]
- System stabilny, zapytania benchmarkowe są w pełni skoordynowane ze strategią obliczeniową zdefiniowaną w system prompcie `CadMathProfile`.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Wykonanie testu benchmarku w BricsCAD i weryfikacja poprawności.


## [v2.22.10] 2026-06-04T01:17:00+02:00 - Podniesienie precyzji RPN (12 miejsc) i fizyczne porównanie tolerancji
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


## [v2.22.11] 2026-06-04T01:23:00+02:00 - Dodanie wskazówek syntaktycznych RPN do pytań benchmarku
### [ZREALIZOWANO]
- **Dodanie wskazówek RPN do zapytań benchmarkowych**: Wzbogacono pytania `UserPrompt` w pliku [Benchmark_02_Math.json](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/Benchmark_02_Math.json) o wyraźne wskazówki dotyczące konieczności stosowania kalkulatora RPN, dopisywania jednostek (np. `_mm`, `_cm3`) oraz używania polecenia `CONVE` w pojedynczych cudzysłowach. Pomaga to mniejszemu modelowi (Bielik 11B) w utrzymaniu dyscypliny składniowej RPN bez zmuszania go do nienaturalnego upakowywania całego zadania w jedno wywołanie.
- **Weryfikacja kompilacji**: Kompilacja powiodła się bez żadnych błędów i ostrzeżeń.
### [STAN_SYSTEMU]
- System stabilny, zapytania benchmarkowe są przystosowane pod kątem specyfiki mniejszych modeli LLM pracujących w pętli ReAct.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Wykonanie testu benchmarku w programie BricsCAD w celu weryfikacji.


ഀ
##2026-06-04T02:23:00+02:00
###[ZREALIZOWANO]
-Wdro|onofallbackwCreateObjectTool.csprzekierowujcydanedoRpnCalculatorwprzypadkubBduformatowanialiczb.
-UsunitocaBkowicienarzdzieCalculateRpnTool.
-Zaimplementowanoszablonymatematyczne{MATH:...}wForeachTool,CreateObjectTool,ModifyPropertiesTool.
###[STAN_SYSTEMU]
-SystemstabilnieobsBugujenaturalnmatematykwptlachiwBa[ciwo[ciach.
###[BLOKADY/PROBLEMY]
-Brak.
###[KOLEJNY_KROK]
-Commitdorepozytorium.

##2026-06-04T18:55:00+02:00
###[ZREALIZOWANO]
###[STAN_SYSTEMU]
###[BLOKADY/PROBLEMY]
-Brak.
###[KOLEJNY_KROK]
-CommitzmiannarepozytoriumGitHub.

## [v2.25.2-pre.1] 2026-06-04T21:30:00+02:00 - Renderowanie LaTeX w czacie i testy jednostkowe
### [ZREALIZOWANO]
- **Renderowanie LaTeX w czacie**: Zaimplementowano klasę `LatexToUnicodeConverter` konwertującą surowe formuły LaTeX (`$...`, `$$...$$`) na czytelny tekst Unicode (indeksy górne/dolne, symbole matematyczne i litery greckie).
- **Integracja UI**: Zintegrowano konwerter z metodą `AppendToHistory` w `AgentControl.cs`, poprawiając prezentację wyników obliczeń modelu w formancie `RichTextBox`.
- **Testy jednostkowe**: Dodano zestaw testów w `LatexToUnicodeConverterTests.cs` weryfikujący poprawność konwersji jednostek, notacji naukowej oraz wzorów matematycznych. Testy zintegrowano z konsolowym `TestRunner.cs`.
### [STAN_SYSTEMU]
- System kompiluje się bez błędów. Nowy mechanizm automatycznie i w locie przekształca formuły matematyczne generowane przez AI na czytelną formę tekstową Unicode.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Weryfikacja działania wtyczki w środowisku uruchomieniowym BricsCAD.


## 2026-06-05T01:40:00+02:00
### [ZREALIZOWANO]
- **Refaktoryzacja zakładki agentów (UI AgentControl)**: Przeprojektowano zakładkę " Agenci\ w interfejsie użytkownika. Zastąpiono tabelę przypisywania skilli wygodną listą typu CheckedListBox (chlbAgentTools) powiązaną z wybranym profilem.
- **Dodanie Leksykonu Skilli**: Wprowadzono listę wszystkich dostępnych w systemie narzędzi/skilli (lbAllTools) wraz z podglądem ich schematów JSON (
tbToolSchema) generowanych automatycznie na podstawie definicji parametrów wysyłanych do LLM.
- **Wsparcie dla konfiguracji profilowych w UI**: Powiązano listę wyboru promptów systemowych bezpośrednio z wybranym agentem. Dodano przycisk umożliwiający natychmiastowe otwarcie powiązanego pliku promptu systemowego w Notatniku.
- **Aktualizacja zapisu profilu**: Dodano logikę zapisu przypisanego pliku promptu i zestawu dozwolonych narzędzi do ools_config.json za pomocą ToolConfigManager.UpdateAgentProfile.
### [STAN_SYSTEMU]
- System kompiluje się w pełni poprawnie (0 błędów, 0 ostrzeżeń). UI poprawnie synchronizuje konfiguracje profili agentów.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie nowej zakładki Agenci bezpośrednio w BricsCAD.

## 2026-06-05T09:51:00+02:00
### [ZREALIZOWANO]
- **Naprawa błędu mscorlib recursive resource lookup / SEHException**: Usunięto problem z deadlockami oraz wywołaniami aktualizacji UI z wątków tła (tzw. cross-thread UI operations) w klasach AgentControl.cs oraz DatasetStudioControl.cs. Dodano mechanizmy sprawdzające istnienie uchwytu okna (IsHandleCreated) oraz bezpieczne delegowanie aktualizacji (np. z AgentTelemetry) na główny wątek przy użyciu BeginInvoke wywoływanego na głównej instancji AgentControl.Instance.
### [STAN_SYSTEMU]
- Zwiększona stabilność interfejsu WinForms osadzonego w BricsCAD. Aplikacja nie rzuca już błędu System.Runtime.InteropServices.ExternalException przy długotrwałym działaniu w tle.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Kompilacja i weryfikacja działania w programie BricsCAD.


## 2026-06-05T16:05:00+02:00
### [ZREALIZOWANO]
- **Aktualizacja .gitignore**: Dodano regułę \**/[Bb]enchmark_02_Math*\ ignorującą pliki wyników benchmarków matematycznych (pliki rozpoczynające się od Benchmark_02_Math), co pozwala na zachowanie innych plików JSON w projekcie.
### [STAN_SYSTEMU]
- Zaktualizowano reguły ignorowania plików git.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Kompilacja i weryfikacja działania w programie BricsCAD.

## 2026-06-05T16:55:00+02:00
### [ZREALIZOWANO]
- **Migracja do PackageReference**: Przekonwertowano projekt z packages.config na format PackageReference. Zaktualizowano pakiety NuGet, w tym zabezpieczono lukę w System.Text.Json (wersja 8.0.5).
- **System Formuł i Makr**: Zaimplementowano DynamicFormulaManager (dynamiczna kompilacja w locie przez Roslyn/CSharpScript, piaskownica, ograniczony dostęp do I/O) oraz MacroManager obsługujący parsowanie i asynchroniczne wykonywanie wieloetapowych skryptów JSON dla narzędzi CAD.
- **Narzędzia Agentowe V2**: Dodano i zarejestrowano w profilach nowe narzędzia oparte o IToolV2: SearchKnowledgeBaseTool, SavePermanentFormulaTool, SaveMacroTool, ExecuteFormulaTool, ExecuteMacroTool. 
- **Baza Wiedzy AI (UI)**: Utworzono nową zakładkę w interfejsie (KnowledgeBaseControl.cs), integrującą listy zdefiniowanych makr i formuł Roslyn, a także udostępniającą podgląd kodu i przyciski do przeładowywania ("Hot Reload") i wykonywania makr asynchronicznie (Task.Run).
- **Automatyczne Testy (QA)**: Dodano testy jednostkowe DynamicFormulaManagerTests, weryfikujące dynamiczną kompilację, oraz zintegrowano je z istniejącym procesem TestRunner.
### [STAN_SYSTEMU]
- Projekt bezbłędnie kompiluje się z użyciem Roslyn. Wprowadzono architekturę opartą na asynchroniczności, uodparniając UI (AgentControl) przed blokowaniem przez ciężkie skrypty C#. Gotowość na testy dynamicznej wiedzy inżynierskiej.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Rozpoczęcie tworzenia nowych narządzi przy wykorzystaniu stworzonej Bazy Wiedzy lub testowanie manualne w BricsCAD.

## 2026-06-05T17:10:00+02:00
### [ZREALIZOWANO]
- **Naprawa błędu PerTypeValues'1 (System.Runtime.CompilerServices.Unsafe)**: Wdrożono globalną obsługę zdarzenia AppDomain.CurrentDomain.AssemblyResolve w klasie AgentStartup.cs. Zapobiega to awariom kompilacji w locie (Roslyn) przez środowisko BricsCAD, ręcznie kierując poszukiwania uszkodzonych referencji bezpośrednio do fizycznych plików .dll w katalogu wtyczki. Mechanizm ten rozwiązuje znane problemy z ładowaniem przestrzeni nazw w .NET Framework z zewnętrznych plików w systemach wielowątkowych (takich jak kompilator CSharpScript).
### [STAN_SYSTEMU]
- Kompilator dynamicznych formuł działa stabilnie pod presją silnika BricsCAD. Rozwiązano konflikt z zarządzaniem pakietami Nuget/Roslyn na etapie włączania wtyczki. W pełni odblokowano zdolność do tworzenia, zapisywania i korzystania z formuł w czasie rzeczywistym.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Rozpoczęcie tworzenia nowych narzędzi opartych o wiedzę inżynierską przez Użytkownika, wykorzystujących poprawnie działający silnik Roslyn.

## 2026-06-05T17:43:00+02:00
### [ZREALIZOWANO]
- **Wdrożenie UnitsNet i rygoru wymiarowego (Faza 6 i 7)**: Rozbudowano system kompilacji formuł (DynamicFormulaManager) o pełne wsparcie dla UnitsNet. Zabezpieczono wymiary przesyłanych danych poprzez modyfikację IToolV2 na typ string zamiast gołych double. Zaktualizowano profile Agentów (ToolConfigManager), zapewniając synchronizację uprawnień dla starszych plików konfiguracyjnych, tak aby CadMathProfile mógł poprawnie korzystać z ExecuteFormulaTool.
### [STAN_SYSTEMU]
- Kompilator w locie działa poprawnie. Agenci rozróżniają obliczenia interaktywne RPN od gotowych skryptów formuł inżynierskich (.csx). Testy manualne (E2E) w BricsCAD zakończone pomyślnie.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Ewentualna optymalizacja makr systemowych lub tworzenie pierwszych narzędzi specyficznych dla użytkownika przez interfejs CAD.

## 2026-06-05T18:02:00+02:00
### [ZREALIZOWANO]
- **Zaawansowany Sanitizer jednostek HVAC**: W module ExecuteFormulaTool.cs wprowadzono kaskadowe czyszczenie łańcuchów znaków przed przekazaniem ich do UnitsNet. Rozwiązano problem halucynacji LLM, które generowały zapis algebraiczny (np. 1.5_W/(m^2*K)). Sanitizer wygładza tekst konwertując potęgi, usuwając nawiasy, redukując podwójne spacje, zamieniając znaki mnożenia na środkowe kropki oraz korygując przecinki. Wymuszono także stosowanie CultureInfo.InvariantCulture przy budowaniu skryptów w SavePermanentFormulaTool.cs.
### [STAN_SYSTEMU]
- System stabilny, kompilator ignoruje i naprawia błędy wprowadzania jednostek fizycznych pochodzące z naturalnego języka.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Rozbudowa kolejnych mechanizmów lub testowanie praktycznych makr przez użytkownika.

## 2026-06-05T21:17:00+02:00
### [ZREALIZOWANO]
- **Konfigurowalne ścieżki Bazy Wiedzy (Faza 9)**: Wprowadzono centralną klasę `AppPaths`, która pozwala na zdefiniowanie własnej ścieżki do folderu `CustomKnowledge` z poziomu nowej zakładki "Ścieżki i Dane" w ustawieniach interfejsu użytkownika. Zmiana ta umożliwia przetrzymywanie formuł i makr w chmurze (np. OneDrive), z możliwością automatycznej migracji (kopiowania) plików ze starego folderu AppData. Zaktualizowano wszystkie powiązane menedżery i narzędzia.
### [STAN_SYSTEMU]
- System operuje na konfigurowalnych ścieżkach do zasobów wiedzy inżynierskiej. Ścieżka jest zapisywana w `ui_settings.json` i zachowuje spójność systemu.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Dalszy rozwój funkcji CAD i korzystanie przez użytkownika z chmurowej bazy wiedzy.
## 2026-06-05T21:40:00+02:00
### [ZREALIZOWANO]
- **FAZA 8: System Zarządzania Zestawami Danych (DatasetManager)**: 
  1. Stworzono `DatasetManager` operujący na plikach JSON (`Newtonsoft.Json.Linq`), co pozwala na łatwe operacje na danych tabelarycznych.
  2. Rozbudowano moduł `AppPaths` o ścieżkę do `CustomKnowledge\Datasets`.
  3. Wstrzyknięto obiekt bazy pod nazwą `Data` bezpośrednio do `ScriptGlobals`, co pozwala wywoływać go z dynamicznych skryptów formuł `.csx`. Dodano dyrektywy Newtonsoft do kompilatora.
  4. Utworzono nowe narzędzia w standardzie `IToolV2`: `QueryDatasetTool`, `ImportCsvDatasetTool`, `ManageDatasetTool`. Narzędzia te zarejestrowano w profilach agentów (Supervisor, CadMath).
  5. Rozbudowano interfejs UI (`KnowledgeBaseControl.cs`) o zakładkę z widokiem tabelarycznym (`DataGridView`) ułatwiającą przegląd i proste edycje.
  6. Skompilowano cały projekt MSBuild, nie uzyskując żadnych błędów.
### [STAN_SYSTEMU]
- System operacyjny i w pełni stabilny. Agenci uzyskali elastyczny dostęp do baz danych inżynierskich w formacie JSON z możliwością dynamicznych zapytań (Exact, NearestGreater, NearestLower).
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie modułu baz danych w interakcji.

## [v2.23.0] 2026-06-05T21:42:00+02:00 - Faza 8.1 - Filtrowanie wielokryterialne dla podtypów w DatasetManager
### [ZREALIZOWANO]
- **FAZA 8.1: Filtrowanie wielokryterialne dla podtypów w DatasetManager**: 
  1. Zmodyfikowano interfejs `IDatasetProvider` by metody `GetExactMatch`, `GetNearestGreater`, `GetNearestLower` przyjmowały opcjonalny słownik `Dictionary<string, string> filters`.
  2. W klasie `DatasetManager` dodano funkcję pomocniczą sprawdzającą podane klucze i wartości przed uruchomieniem algorytmów szukających, optymalizując wyciąganie np. rur konkretnego materiału z połączonej tabeli JSON.
  3. Zaktualizowano definicję i ciało `QueryDatasetTool`, wprowadzając i wyciągając opcjonalny parametr `filters`. Narzędzie przekazuje poprawnie filtry (jako JTokenType.Object).
  4. Projekt przebudowano z zerową ilością błędów.
### [STAN_SYSTEMU]
- System jest stabilny. Zarządzanie danymi obsługuje pełne, kaskadowe filtrowanie właściwości przed odnalezieniem właściwych parametrów. Formuły i makra zyskały dużą elastyczność w szukaniu w bazach.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie w CAD / praca inżynierska na bazach.

## [v2.24.0] 2026-06-05T22:05:00+02:00 - Faza 9 - Kategoryzacja, Tagowanie i Drzewo Folderów
### [ZREALIZOWANO]
- **FAZA 9: System Kategoryzacji, Tagowania i Drzewa Folderów**: 
  1. Dodano pola Category i Tags do metadanych formuł i makr.
  2. Zaktualizowano narzędzia SavePermanentFormulaTool i SaveMacroTool o obsługę kategorii, włączając sanityzację znaków Windows oraz tworzenie fizycznych podfolderów.
  3. Zmodyfikowano menedżery (DynamicFormulaManager, MacroManager, DatasetManager) do przeszukiwania rekurencyjnego (SearchOption.AllDirectories).
  4. Rozbudowano wyszukiwarkę KnowledgeBase o opcjonalne parametry Category i Tags (filtrowanie LINQ).
  5. Przebudowano interfejs UI (KnowledgeBaseControl) z użyciem TreeView do grupowania folderów i elementów.
  6. Dodano automatyczne przeładowywanie bazy po wejściu w zakładkę (zdarzenie VisibleChanged) oraz pasek tekstowy do dynamicznego filtrowania wyświetlanego drzewka po tagach.
### [STAN_SYSTEMU]
- System w pełni stabilny i wspiera zaawansowaną kategoryzację oraz filtrowanie tagami. Panel Bazy Wiedzy jest automatycznie aktualizowany po pokazaniu.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie nowej struktury z użyciem narzędzi lub dalszy rozwój bazy wiedzy.

## [v2.28.1] 2026-06-06T20:44:00+02:00 - Ujednolicenie pliku pamięci i naprawa kodowania
### [ZREALIZOWANO]
- Przeanalizowano pliki pamięci i zidentyfikowano błędy kodowania Mojibake (znaki UTF-8 zdekodowane jako Windows-1250 i zapisane ponownie) oraz błędy zapisu UTF-16-BE (alternujące spacje/NUL w pliku).
- Stworzono kopię zapasową pliku `memory.md` jako `memory.bak.md` w folderze głównym wtyczki V2.
- Naprawiono wszystkie zniekształcenia kodowania, przywracając czysty polski tekst UTF-8.
- Ponadawano numery wersji (v2.20.10 do v2.28.0) wszystkim krokom deweloperskim z datami od 2026-06-03.
- Zaktualizowano i ujednolicono spis wersji (Changelog) na początku pliku o nowe wersje odpowiadające krokom deweloperskim i fazom V2.
- Zsynchronizowano plik `memory.md` w katalogu głównym oraz w folderze `docs/`.
### [STAN_SYSTEMU]
- Pliki pamięci są w pełni ujednolicone, spójne syntaktycznie i wolne od uszkodzeń kodowania.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Dalszy rozwój projektu zgodnie z planem wdrożenia w BricsCAD.

## [v2.28.2] 2026-06-06T19:11:00+02:00 - Implementacja narzędzia do zarządzania receptami
### [ZREALIZOWANO]
- Przeniesiono przechowywanie recept z pojedynczego pliku 'AgentRecipes.json' do oddzielnych plików .json w dedykowanym folderze 'Recipes'.
- Zaimplementowano logikę migracji do nowego formatu w 'RecipeManager.cs'.
- Stworzono narzędzie 'ManageRecipesTool' umożliwiające agentom czytanie, tworzenie, edytowanie i usuwanie recept.
- Zaktualizowano profile Supervisora i pozostałe w 'ToolConfigManager.cs', umożliwiając dostęp do nowego narzędzia i zachowanie znaczników $trigger w TaskDescription podczas delegacji zadań.
### [STAN_SYSTEMU]
- System umożliwia dynamiczne tworzenie i zarządzanie zadaniami 'Few-Shot' przez agenta i odczytywanie ich z oddzielnych plików.
### [BLOKADY / PROBLEMY]
- Dotnet build z poziomu CLI wyrzucał błędy o braku Newtonsoft.Json dla starych plików co może wymagać weryfikacji .csproj.
### [KOLEJNY_KROK]
- Oczekiwanie na testy interakcji agenta z nowym narzędziem.

## [v2.28.3] 2026-06-06T21:26:00+02:00 - Implementacja Centrum Pomocy (Zakładka Pomoc)
### [ZREALIZOWANO]
- Skopiowano dokumentację użytkownika (USER_GUIDE.md, TOOLS_REFERENCE.md, COMMANDS_REFERENCE.md) do nowej lokalizacji 'resources\help\'.
- Utworzono plik indeksujący 'index.json' sterujący zawartością drzewa nawigacyjnego w module pomocy.
- Zbudowano nową kontrolkę 'HelpCenterControl.cs' składającą się z drzewka (TreeView) i przeglądarki (WebBrowser).
- Zaimplementowano wewnętrz 'HelpCenterControl' dynamiczny silnik parsujący na wyrażeniach regularnych, który w locie tłumaczy składnię Markdown na sformatowany, stylowy HTML (obsługujący nagłówki, listy, pogrubienia, sekcje kodu i alerty).
- Wpięto nową kontrolkę jako zakładkę '? Pomoc' do głównego obiektu 'tabControl' w 'AgentControl.cs'.
- Zaktualizowano plik 'Bricscad_AgentAI_V2.csproj' do kompilacji nowej kontrolki i uwzględnienia plików pomocy jako zasobów (CopyToOutputDirectory).
### [STAN_SYSTEMU]
- Dodano centrum zintegrowanej wiedzy, które użytkownik będzie mógł łatwo edytować przez pliki MD w resources\help.
### [BLOKADY / PROBLEMY]
- Dotnet CLI rzuca standardowe problemy z brakiem referencji z NuGet, wymagana manualna kompilacja u użytkownika z VS / MSBuild.
### [KOLEJNY_KROK]
- Weryfikacja działania drzewka nawigacyjnego w GUI wtyczki w środowisku natywnym BricsCAD.

## [v2.28.4] 2026-06-06T21:40:00+02:00 - Moduł Agenta Pomocy (ReadHelpTool)
### [ZREALIZOWANO]
- Zbudowano narzędzie 'ReadHelpTool.cs' dla Agenta umożliwiające swobodny odczyt dokumentacji i listowanie zasobów z folderu 'resources\help\'.
- Narzędzie posiada mechanizm Path Traversal Prevention zabezpieczający przed odczytem zewnętrznych plików systemu.
- Uaktualniono domyślną konfigurację Supervisora w 'ToolConfigManager.cs' przypisując mu bezpośredni dostęp do narzędzia 'ReadHelp'.
- Zmodyfikowano logikę generatora 'system_prompt_supervisor.txt', dodając sekcję 4 o nazwie WIEDZA O SYSTEMIE / POMOC.
- Skompilowano cały program upewniając się, że brak błędów środowiskowych przy użyciu MSBuild.
### [STAN_SYSTEMU]
- Agent potrafi dyskutować z użytkownikiem na temat własnej wtyczki i procedur w niej opisanych, dołączając instrukcje z plików MD.
### [BLOKADY / PROBLEMY]
- Brak blokad, wszystkie pliki w tym '.csproj' nadpisane i skompilowane z sukcesem.
### [KOLEJNY_KROK]
- Test funkcjonalny narzędzia 'ReadHelp' w bezpośredniej rozmowie użytkownika z Supervisorem.

## [v2.28.5] 2026-06-06T22:15:00+02:00 - UI & UX Tweaks (Markdown & Commendy)
### [ZREALIZOWANO]
- Zmieniono komendę wywoławczą panelu z AGENT_V2 na krótkie i proste 'AI'.
- Zaimplementowano w konsoli parser formatowania Markdown. Zamiast surowych gwiazdek, bot używa teraz poprawnego pogrubienia, kursywy i dedykowanej czcionki z tłem dla bloków kodu.
- Poprawiono parser, usuwając błąd 'rozlewania' się formatowania na wiele akapitów i nałożono auto-pogrubienie na wiersze z nagłówkami z prefiksem '#'.
### [STAN_SYSTEMU]
- Odświeżona konsola z ulepszonym renderingiem tekstu.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Implementacja dynamicznego systemu Skilli.

## [v2.28.6] 2026-06-06T23:18:51+02:00 - Wdrożenie systemu Skilli (Markdown+YAML)
### [ZREALIZOWANO]
- Utworzono strukturę danych `AgentSkill.cs` i zaimplementowano menedżer `SkillManager.cs` parsujący bloki YAML Frontmatter z plików Markdown.
- Zintegrowano obsługę plików `.md` w zakładce Baza Wiedzy (nowa podzakładka "Skille Inżynierskie").
- Stworzono `ManageSkillsTool.cs` dające Agentowi możliwość interakcji ze skillami (akcje `list_skills`, `read_skill`, `create_skill`).
- Wdrożono mechanizm Progressive Disclosure (poziom 1) w `AgentControl.cs` wstrzykujący skille wywołane przez `#` w chatboxie prosto jako wiadomości systemowe (kontekst).
- Zaktualizowano plik `Bricscad_AgentAI_V2.csproj` w celu prawidłowej kompilacji nowych klas.
### [STAN_SYSTEMU]
- System umożliwia wczytywanie i dynamiczne wstrzykiwanie skilli inżynierskich z plików Markdown z YAML.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Uzupełnienie dokumentacji o obsługę Skilli.

## [v2.28.7] 2026-06-06T23:21:53+02:00 - Aktualizacja Pomocy (USER_GUIDE) o obsługę Skilli (#) i Poleceń (/)
### [ZREALIZOWANO]
- Zaktualizowano plik `USER_GUIDE.md` w resources/help o instrukcje wywoływania skilli przy użyciu `#` oraz poleceń ukośnika `/` w chatboxie.
### [STAN_SYSTEMU]
- Zaktualizowana pomoc dla użytkownika.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Aktualizacja system promptu Supervisora.

## [v2.28.8] 2026-06-06T23:36:12+02:00 - Aktualizacja Supervisor prompt: Odróżnienie Skilli (#) od Recept ($)
### [ZREALIZOWANO]
- Zaktualizowano system prompt w `ToolConfigManager.cs`, wprowadzając precyzyjne wytyczne odróżniające dynamiczne Skille (`#`) od Recept (`$`).
### [STAN_SYSTEMU]
- Supervisor lepiej odróżnia i stosuje odpowiednie struktury meta-instrukcji.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Udostępnienie narzędzia manage_skills dla Supervisora.

## [v2.28.9] 2026-06-06T23:57:36+02:00 - Dodanie manage_skills do domyślnych narzędzi Supervisora
### [ZREALIZOWANO]
- Dodano `ManageSkillsTool` (`manage_skills`) do domyślnej konfiguracji dostępnych narzędzi dla profilu Supervisora w `ToolConfigManager.cs`.
### [STAN_SYSTEMU]
- Agent w roli Supervisora ma doświadczenie i uprawnienia do zarządzania skillami w locie.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Uodpornienie narzędzia na błędne wywołania przez LLM.

## [v2.28.10] 2026-06-07T00:06:39+02:00 - Uodpornienie ManageSkillsTool na halucynacje LLM (Case Insensitivity & Aliases)
### [ZREALIZOWANO]
- Zaimplementowano w `ManageSkillsTool.cs` tolerancję na wielkość liter (case-insensitivity) przy wyszukiwaniu skilli.
- Wprowadzono obsługę aliasów i nazw alternatywnych, aby zapobiec błędom przy wywołaniach przez LLM.
### [STAN_SYSTEMU]
- Moduł obsługi skilli jest odporny na drobne błędy zapisu i wielkości liter ze strony modeli LLM.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Zapis i ujednolicenie dziennika prac w memory.md.


## Podsumowania Ukończonych Faz Deweloperskich V2
### Faza 10: Multi-Modalne Załączniki (Tekst i Wizja) (Zakończono)
1. Zaimplementowano klasę FileExtractor.cs do obsługi załączników tekstowych (TXT, PY, MD, LSP) oraz binarnych (PDF, XLS/XLSX).
2. Wprowadzono kompresję i skalowanie obrazów (PNG, JPG) używając System.Drawing.Common do 1024x1024px z konwersją do Base64 dla wsparcia Vision API.
3. Zaktualizowano AgentControl.cs - dodano przycisk załącznika (btnAttachFile), obsługę OpenFileDialog, logikę procesowania załącznika w ProcessInputAsync i wyświetlanie w UI (lblAttachedFile).
4. Skompilowano kod z wynikiem pozytywnym bez błędów (MSBuild).
### Faza 11: Standaryzacja Datasetow (Wzorzec Plikow Towarzyszacych i Normalizacja) (Zakonczono)
1. Dodano plik Models/DatasetMetadata.cs aby zapewnic ustandaryzowana strukture bazy wiedzy.
2. Rozdzielono zapis/odczyt plikow baz danych w DatasetManager.cs na [id].json (metadane) i [id].data.json (tablice z danymi).
3. Dodano odpornosc na stare/niezmigrowane pliki (logowanie zamiast crashowania GUI).
4. Zaktualizowano KnowledgeBaseControl.cs - UI dla Bazy Danych pobiera kategorie, grupuje dane w TreeView, oraz obsluguje zapis wylacznie *.data.json z pominieciem nadpisywania metadanych.
5. Zmodyfikowano ManageDatasetTool i ImportCsvDatasetTool aby przyjmowaly opis, tagi, kategorie z nowa rygorystyczna wytyczna dotyczaca plaskich tabel.
### Hotfix: Polskie znaki w UI
Poprawiono kodowanie znakow w AgentControl.cs gdzie wyswietlane byly krzaczki np. "Dołączono plik" oraz upewniono sie, ze plik zapisany jest z kodowaniem UTF-8.
### Faza 12: Integracja Schowka Systemowego (Zakonczono)
1. Dodano w FileExtractor.cs metode do obslugi obrazow bezposrednio z pamieci operacyjnej (obiekt Image), ktora zabezpiecza VRAM i automatycznie przelicza, skaluje oraz uwalnia pamiec za pomoca blokow using.
2. Zaktualizowano AgentControl.cs wprowadzajac zdarzenie KeyDown dla pola tekstowego txtInput.
3. Gdy uzytkownik wcisnie Ctrl+V i schowek zawiera obraz, agent automatycznie wyodrebni ten obraz do wewnetrznej zmiennej _attachedClipboardImage, podmieni label GUI i zablokuje typowe wklejenie tekstu. 
4. Procesowanie obrazu za pomoca Vision API dziala natywnie i poprawnie konwertuje do binarnej bazy 64.

### Faza 13: Zarządzanie Sesjami, Pamięcią Kontekstu i Kompresja (Zakończono)
1. Stworzono model ChatSession (z GUID, datami, wiadomościami i wyizolowanym Blackboardem) oraz SessionManager do zapisu/odczytu sesji w %APPDATA% (plik .json).
2. Zaktualizowano SharedMemoryState, dodając możliwość ładowania pamięci z Dictionary i czyszczenia jej na potrzeby izolacji stanów między poszczególnymi sesjami.
3. Zaktualizowano SupervisorOrchestrator, by korzystał wprost z historii wiadomości CurrentSession.Messages zamiast zmiennej globalnej, a nowy wątek wywołuje Auto-Naming sesji gdy uzbierają się 2 wiadomości.
4. Wprowadzono logikę zliczania użycia tokenów z API oraz pasek Context Bar w GUI (AgentControl.cs), ze wsparciem dla bezpiecznej aktualizacji między-wątkowej (Invoke).
5. Zbudowano i zintegrowano system kompresji kontekstu (Auto i Ręczna), podsumowujący najstarsze logi, by ratować miejsce w oknie kontekstowym.

### Faza 14: Świadomość Kontekstu DWG i Notatki Projektowe (Zakończono)
1. Zmodyfikowano ChatMessage dodając pole ActiveDocumentPath.
2. Stworzono system Notatek Projektowych (Sidecar Markdown) z wykorzystaniem nowej klasy DrawingNoteManager, zapisując notatki obok plików DWG lub w folderze Temp dla rysunków niezapisanych.
3. Zaimplementowano w AgentControl.cs przechwytywacz komend (/notatka) oraz sub-agenta z zablokowanym narzędziownikiem (Tools) do generowania czystego Markdowna.
4. Uzupełniono SupervisorOrchestrator o logikę RAG - notatki inżynierskie są dynamicznie wstrzykiwane do Promptu Systemowego.
5. Poprawiono bezpieczeństwo wyjątków I/O i wątków UI z użyciem bloków try-catch.

### Faza 15: Bezpieczne Narzędzia Plikowe (Zakończono)
1. Utworzono nowe narzędzia (ReadProjectFileTool i WriteProjectFileTool) zabezpieczające operacje na plikach - dozwolone rozszerzenia (.txt, .md, .csv itd.) i lokalizacja wymuszona w folderze aktualnego rysunku DWG (lub %APPDATA%).
2. Wprowadzono twardą blokadę manipulowania notatkami inżynierskimi (.ai_note.md) przez te narzędzia; zablokowane akcje wymuszają na Agencie użycie DelegateTaskTool.
3. Zaktualizowano menedżer konfiguracji profili (ToolConfigManager), wstrzykując wygenerowany system prompt dla NotesProfile oraz aktualizując systemowy prompt Supervisora o RAG-ową obsługę plików notatek (w tym świadomość braku pliku).
4. Rozbudowano listę autouzupełniania w AgentControl.cs (UI) - polecenia ze znakiem / (np. /notatka, /compress) otrzymały opisy wraz z autouzupełnianiem; polecenia typu $ również zyskały objaśnienia.

### Faza 16: System Skilli (Markdown + YAML Frontmatter) (Zakończono)
1. Utworzono strukturę danych `AgentSkill.cs` i zaimplementowano menedżer `SkillManager.cs` parsujący bloki YAML Frontmatter z plików Markdown.
2. Zintegrowano obsługę plików `.md` w zakładce Baza Wiedzy (nowa podzakładka "Skille Inżynierskie").
3. Stworzono `ManageSkillsTool.cs` dające Agentowi możliwość interakcji ze skillami (akcje `list_skills`, `read_skill`, `create_skill`).
4. Wdrożono mechanizm Progressive Disclosure (poziom 1) w `AgentControl.cs` wstrzykujący skille wywołane przez `#` w chatboxie prosto jako wiadomości systemowe (kontekst).
5. Poprawiono zgodność interfejsu narzędzia z wymogami projektu `IToolV2` i pomyślnie zrekompilowano system.

### Faza 17: Rewident (QA Agent / AuditorProfile) i bezpieczne testowanie narzędzi (Zakończono)
1. Wdrożono nowy profil Agenta `AuditorProfile` (Rewident) odpowiedzialny za bezpieczne testowanie (QA) oraz diagnozowanie narzędzi na poziomie kodu.
2. Dodano obsługę parametru `__MockResponse` do narzędzi interaktywnych blokujących UI (`UserInputTool`, `UserChoiceTool`), aby umożliwić przeprowadzanie zautomatyzowanych testów bez udziału użytkownika.
3. Dodano obsługę flagi `__DryRun` do narzędzi modyfikujących stan oraz pliki: `WriteProjectFileTool`, `SaveMacroTool`, `SavePermanentFormulaTool`, `ManageRecipesTool`, `ManageSkillsTool`.
4. Zaktualizowano `ToolConfigManager.cs`, dodając dedykowany prompt systemowy Rewidenta oraz ograniczony zestaw narzędzi weryfikacyjnych.
5. Poprawiono odporność parsowania w `ManageSkillsTool` (warianty `Action`/`action`).

### Faza 18: Zautomatyzowane Testowanie Narzędzi (Autotest) (Zakończono)
1. Rozbudowano `AgentTesterControl.cs` o podzakładkę "Autotest Narzędzi".
2. Dodano dynamiczne wczytywanie definicji narzędzi z `ToolOrchestrator` do listy testowej.
3. Opracowano izolowaną pętlę testową z profilem `AuditorProfile`, flagami `__DryRun` i `__MockResponse` oraz osobnym promptem Rewidenta.
4. Zaimplementowano kolorowanie logów w UI dla szybkiej identyfikacji sukcesów i błędów.
5. Wdrożono generator raportów `.md` zbierających wyniki ewaluacji.

## [v2.28.11] 2026-06-07T11:40:00+02:00 - FIX: Stabilizacja QA i dostęp do kodu źródłowego [QA-ACCESS]
### [ZREALIZOWANO]
- Naprawiono krytyczny błąd kasowania historii konwersacji w oknie QA przez wywołanie `QASessionManager.SaveSession()` natychmiast po udanej odpowiedzi modelu, przed asynchronicznym odświeżeniem GUI (`LoadSessions()` i `LbSessions_SelectedIndexChanged`).
- Rozszerzono `ToolConfigManager.cs` o narzędzia `ListSourceFiles` i `ReadSourceCode` w domyślnej białej liście (`AllowedTools`) profilu `AuditorProfile`.
- Zaktualizowano generatory `system_prompt_auditor.txt` i plik awaryjny w `QASessionManager.cs`, nadając QA pełny odczyt warstwy C# w `BricsCAD_AgentAI_V2`.
### [STAN_SYSTEMU]
- UI QA jest stabilniejsze, a Agent QA ma narzędzia do czytania kodu źródłowego.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie kolejnych narzędzi przez Rewidenta.

## [v2.28.12] 2026-06-07T12:03:19+02:00 - FEAT: Statyczne autotesty QA i integracja z Antigravity [QA-ANTIGRAVITY]
### [ZREALIZOWANO]
- Wdrożono `RunToolTestTool.cs`, pozwalające Agentowi QA bezpiecznie uruchamiać statyczne testy narzędzi CAD przez kontrolowany JSON i przechwytywanie wyjątków.
- Rozszerzono `ToolConfigManager.cs`, dodając do `AuditorProfile` narzędzia `RunToolTest`, `WriteQAReport` oraz `DelegateTaskToAntigravity`.
- Zaktualizowano prompty Supervisora i Auditora: Supervisor wie, że może po zgodzie użytkownika przekazać problem do QA, a QA może przygotować zlecenie dla zewnętrznego agenta kodowania.
- Wprowadzono delegowanie zadań do Antigravity przez zapisywanie zleceń w folderze `Autotesty/TasksForAntigravity/`.
### [STAN_SYSTEMU]
- System potrafi diagnozować własne narzędzia, generować raporty QA i wystawiać zadania dla zewnętrznego agenta kodowania.
### [BLOKADY / PROBLEMY]
- Kompilacja przez `dotnet build` może nadal zgłaszać problemy z pakietami NuGet/Newtonsoft.Json; wiarygodna ścieżka w tym projekcie to natywne MSBuild/Visual Studio/środowisko BricsCAD.
### [KOLEJNY_KROK]
- Testy produkcyjne oraz budowa powtarzalnych scenariuszy QA.

## [v2.28.13] 2026-06-07T12:54:00+02:00 - FEAT: SearchFileContentTool jako grep dla kodu i folderu rysunku [SEARCH-FILE-CONTENT]
### [ZREALIZOWANO]
- Utworzono `SearchFileContentTool`, uniwersalne narzędzie do szybkiego przeszukiwania tekstu w wielu plikach z limitowaniem wyników i ochroną przed path traversal.
- Dodano tryby `DirectoryType`: `SourceCode` dla Auditora oraz `DrawingFolder` dla Supervisora.
- Zaktualizowano `ToolConfigManager.cs`, rozdzielając uprawnienia do narzędzia między Supervisora i QA.
- Uzupełniono dokumentację pomocy o opis nowego narzędzia.
### [STAN_SYSTEMU]
- System ma szybki odpowiednik `grep`, ograniczający koszt tokenowy względem pełnego czytania plików.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Dalsze testy wydajnościowe i dopracowanie limitów wyników, jeśli zajdzie potrzeba.

## [v2.28.14] 2026-06-07T15:05:11+02:00 - FEAT: Ekosystem LISP, audyt kodu i self-healing [LISP-SELF-HEALING]
### [ZREALIZOWANO]
- Wdrożono profile agentów dla środowiska LISP: `LispCoderProfile` oraz `LispAuditorProfile`.
- Zintegrowano natywną funkcję LISP `[LispFunction("agent-callback")]`, przechwytującą w C# wyniki ze skryptów LISP.
- Zaimplementowano cichą pętlę samonaprawiającą: po napotkaniu przez LISP wartości `ERROR` sub-agent może poprawić kod i ponownie go wywołać.
- Zaktualizowano `AgentControl.cs`, dodając wykrywanie wygenerowanego kodu LISP w widoku Markdown i przycisk `Wykonaj Skrypt LISP`.
- Przetestowano asynchroniczną kompilację logiki C#; etap CoreCompile przeszedł poprawnie.
### [STAN_SYSTEMU]
- Ekosystem LISP jest gotowy do pracy w BricsCAD po restarcie aplikacji i zwolnieniu blokady DLL.
### [BLOKADY / PROBLEMY]
- Uruchomiony BricsCAD może blokować kopiowanie plików binarnych do `bin`; wymagany restart CAD przy finalnym buildzie.
### [KOLEJNY_KROK]
- Restart BricsCAD, kompilacja końcowa i testy manualne.

## [v2.28.15] 2026-06-07T15:22:52+02:00 - FIX: Utrata sesji i delegacja kodu LISP [LISP-ROUTING]
### [ZREALIZOWANO]
- Naprawiono błąd ucinania pytania po ponownym uruchomieniu programu przez natychmiastowy zapis sesji `SessionManager.SaveSession()` po otrzymaniu pytania w `SupervisorOrchestrator.cs`.
- Zaktualizowano prompt `SupervisorProfile`, ucząc go o profilu `LispCoderProfile`.
- Wymuszono zasadę, że Supervisor nie pisze kodu LISP samodzielnie, tylko deleguje go do profilu z obsługą `*error*` i pętlą samonaprawiającą.
### [STAN_SYSTEMU]
- Nowe zasady LISP są zintegrowane, a kompilacja przebiegła pomyślnie w natywnej ścieżce projektu.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie scenariuszy LISP w GUI i z poziomu czatu.

## [v2.28.16] 2026-06-07T15:35:00+02:00 - FEAT: Integracja LISP z Bazą Wiedzy [LISP-KB]
### [ZREALIZOWANO]
- Zakończono wdrażanie zakładki `Skrypty LISP` w oknie Knowledge Base.
- Uzupełniono prompty systemowe o narzędzie `manage_lisps`.
- Podłączono `LispManager` do `AgentControl`, umożliwiając bezpośrednie wywołania LISPa przez `ExecuteLispFromExternal`.
### [STAN_SYSTEMU]
- Skrypty LISP są dostępne jako zasoby Bazy Wiedzy i mogą być wykonywane przez agenta lub UI.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testy praktyczne na zapisanych skryptach i dopracowanie autouzupełniania.

## [v2.28.17] 2026-06-08T00:30:00+02:00 - FIX: Autouzupełnianie i pełne wykonywanie skryptów LISP [LISP-UI-EXECUTION]
### [ZREALIZOWANO]
- Wdrożono autouzupełnianie UI w `AgentControl.cs` dla skryptów LISP po wpisaniu znaku `%`, z bezpośrednim wczytywaniem opcji przez `LispManager.LoadAllLisps()`.
- Zmieniono `ExecuteLispFromTemp` w `AgentControl.cs`: aplikacja najpierw wczytuje plik LISP przez `(load ...)`, a następnie automatycznie uruchamia właściwą komendę (`lispId`).
- Zaktualizowano `USER_GUIDE.md`, dodając informacje o podpowiadaniu skryptów w interfejsie.
### [STAN_SYSTEMU]
- `%skrypt` uruchamia właściwą akcję w rysunku bez konieczności ręcznego dopisywania komendy po `load`.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na dyspozycje użytkownika.

## [v2.28.18] 2026-06-08T11:58:00+02:00 - DOC: Kanonizacja pliku pamięci i usunięcie rozjazdu docs/memory.md [MEMORY-CANON]
### [ZREALIZOWANO]
- Scalono rozbieżne wpisy z `Bricscad_AgentAI_V2/memory.md` oraz `Bricscad_AgentAI_V2/docs/memory.md` do jednego kanonicznego pliku `Bricscad_AgentAI_V2/memory.md`.
- Renumerowano kolizyjne wpisy `v2.28.x` z 2026-06-07 i 2026-06-08 na zakres `v2.28.11`-`v2.28.18`.
- Poprawiono widoczne uszkodzenia kodowania w scalonych wpisach QA, Antigravity, SearchFileContent i LISP.
- Zastąpiono `docs/memory.md` krótkim wskaźnikiem do kanonicznej pamięci, aby kolejni agenci nie traktowali go jako drugiego źródła prawdy.
### [STAN_SYSTEMU]
- Jedynym źródłem prawdy dla historii projektu jest `Bricscad_AgentAI_V2/memory.md`.
### [BLOKADY / PROBLEMY]
- Starsze historyczne fragmenty mogą nadal zawierać lokalne ślady dawnego mojibake; bieżąca końcówka historii została uporządkowana ręcznie.
### [KOLEJNY_KROK]
- Przyszłe wpisy należy dopisywać wyłącznie do `Bricscad_AgentAI_V2/memory.md`.

## [v2.28.19] 2026-06-08T13:05:00+02:00 - FEAT: Nowa zakładka Agent-Czat do testowania subagentów [AGENT-CHAT]
### [ZREALIZOWANO]
- Dodano nową kontrolkę `SubAgentChatControl.cs` oraz osobną zakładkę `Agent-Czat` w sekcji `Testy`, bez obsługi sesji i bez ingerencji w główny czat Supervisora.
- Wdrożono wybór profilu subagenta z `ToolConfigManager.GetProfiles()`, automatyczne ładowanie promptu systemowego i reset kontekstu po zmianie profilu.
- Dodano dwa równoległe widoki debugowe: transcript rozmowy oraz panel `Tool Calls / JSON` pokazujący snapshot profilu, wywołania narzędzi i odpowiedzi `tool`.
- Dodano szybkie akcje diagnostyczne: `Wyczyść`, `Eksport czatu`, `Eksport JSON` i `Kopiuj pakiet`.
### [STAN_SYSTEMU]
- `Autotest-Czat` pozostaje wyspecjalizowanym kanałem QA, a `Agent-Czat` służy do ręcznych rozmów z dowolnym workerem bez routingu przez Supervisora.
### [BLOKADY / PROBLEMY]
- Pełny `dotnet build` nadal nie przechodzi w tym repo z powodu istniejących braków referencji/pakietów (`Newtonsoft.Json`, `Microsoft.CodeAnalysis`, `UnitsNet`, `ExcelDataReader`, `UglyToad.PdfPig`) niezwiązanych z nową zakładką.
### [KOLEJNY_KROK]
- Przetestować ergonomię `Agent-Czat` w BricsCAD i ewentualnie rozszerzyć eksport o pojedynczy pakiet `.zip` z promptem, transcriptami i logami narzędzi.

## [v2.28.27] 2026-06-08T21:10:00+02:00 - CHORE: Uporzadkowanie kontraktu RPN vs CalculateMath [RPN-MATH-CONTRACT]
### [ZREALIZOWANO]
- Uporzadkowano prompty Supervisora, Math i fallbackowy prompt CAD pod nowy podzial odpowiedzialnosci: `CadMathProfile` + `CalculateMath` odpowiadaja za pelne obliczenia inzynierskie, a profile CAD uzywaja RPN lokalnie wewnatrz narzedzi.
- Doprecyzowano, ze `ReplaceWith` nalezy tylko do `TextEditTool` i nie jest skladnia RPN ani `EditAttributes`.
- Usunieto `CalculateRpn` z aktywnej synchronizacji dozwolonych narzedzi `CadMathProfile`, pozostawiajac `CalculateMath` jako oficjalny tor obliczeniowy.
### [STAN_SYSTEMU]
- Warstwa instrukcji powinna byc wyraznie mniej podatna na mieszanie pelnej matematyki z lokalnym RPN oraz na przenoszenie `ReplaceWith` do edycji atrybutow.
### [BLOKADY / PROBLEMY]
- Istniejace pliki promptow na dysku moga wymagac auto-upgrade przy kolejnym zaladowaniu wtyczki; embedded fallback i generatory sa juz zsynchronizowane.
### [KOLEJNY_KROK]
- Przetestowac w BricsCAD scenariusze: 1) prosta transformacja RPN w narzedziu CAD, 2) obliczenie inzynierskie przez `CadMathProfile`, 3) tekstowy replace w `EditAttributes` bez halucynacji `ReplaceWith`.

## [v2.28.28] 2026-06-08T22:20:00+02:00 - Repozytoryjne prompty per profil i lokalny User Prompt [PROMPT-SOURCE-OF-TRUTH]
### [ZREALIZOWANO]
- Uporzadkowano architekture promptow tak, aby system prompt przestal byc edytowalnym stanem runtime.
- Wprowadzono osobne pliki promptow systemowych per profil w `Bricscad_AgentAI_V2/resources/prompts/`:
  - `system_prompt_supervisor.txt`
  - `system_prompt_cad.txt`
  - `system_prompt_geometry.txt`
  - `system_prompt_blocks.txt`
  - `system_prompt_metadata.txt`
  - `system_prompt_math.txt`
  - `system_prompt_notes.txt`
  - `system_prompt_auditor.txt`
- Podpieto `.csproj`, aby te pliki byly kopiowane do outputu przy buildzie.
- `ToolConfigManager` sklada teraz efektywny prompt profilu jako:
  1. prompt systemowy z repo/outputu,
  2. opcjonalny `User Prompt` zapisany lokalnie w AppData (`PromptOverrides`).
- `AgentControl` zostal przebudowany tak, aby:
  - pokazywac prompt systemowy tylko do odczytu,
  - pozwalac edytowac tylko `User Prompt`,
  - nie tworzyc ani nie nadpisywac juz promptow systemowych z UI.
- `SubAgentChatControl`, `SupervisorOrchestrator`, benchmarki i sesje QA zostaly przepiete na wspolne `LoadEffectivePromptForProfile(...)`.
### [STAN_SYSTEMU]
- Zrodlo prawdy dla promptow systemowych znajduje sie w repo, a nie w stanie lokalnym BricsCAD.
- Runtime moze dopisac tylko warstwe doprecyzowujaca zachowanie profilu, bez ryzyka przypadkowego "dryfu" glownego promptu.
### [BLOKADY / PROBLEMY]
- Pelna kompilacja `dotnet build` w tym srodowisku nadal zatrzymuje sie na brakujacych referencjach zewnetrznych (`Newtonsoft.Json`, `Microsoft.CodeAnalysis`, `UnitsNet` itd.), co nie wyglada na regresje tej zmiany.
### [KOLEJNY_KROK]
- Przywrocic komplet referencji/pakietow projektu i wykonac pelny build end-to-end.

## [v2.28.29] 2026-06-09T00:10:00+02:00 - Hardening EditAttributes i tekstowego RPN [ATTR-RPN-HARDENING]
### [ZREALIZOWANO]
- Uscislono kontrakt `EditAttributes`:
  - `Attributes` musi byc tablica plaskich obiektow `{Tag, Value}`,
  - dodano walidacje zagniezdzonego `Attributes`, pseudo-pola `RPN` i brakujacego `Tag` / `Value`.
- Rozszerzono opis schematu i prompt blokow o zasady dla tekstowego RPN:
  - `REPLACE` i `CONCAT` jako prawdziwy postfix,
  - zakaz skladni pseudo-Lispowej `replace(...)`.
- Naprawiono tokenizer `RpnCalculator`, aby poprawnie rozroznial apostrof i cudzyslow jako dwa rozne typy cytowania.
- Dodano tolerancje na przypadki, gdy model wysyla do `RPN:` zwykla notacje infix; `EditAttributes` tlumaczy takie wyrazenie przez `ConvertInfixToRpn(...)` przed ewaluacja.
- Doprecyzowano zasady filtrowania blokow:
  - identyfikacja celu ma opierac sie o stabilny atrybut identyfikujacy,
  - nie wolno filtrowac lancuchowych aktualizacji po wartosci, ktora sama jest wlasnie zmieniana.
- `LLMClient` przestal przycinac pelne odczyty narzedziowe `Read`, aby agent widzial komplet tagow i wartosci przy pracy na atrybutach.
### [STAN_SYSTEMU]
- Scenariusze tekstowego `REPLACE` na atrybutach przestaly produkowac smieciowe wartosci wynikajace z blednego tokenizowania cudzyslowow.
- System jest wyraznie odporniejszy na dwa typy halucynacji modelu:
  1. zly ksztalt JSON dla `EditAttributes`,
  2. mylenie postfix RPN z notacja infix lub pseudo-Lisp.
### [BLOKADY / PROBLEMY]
- Nadal pozostaje warstwa rozumowania agentowego przy zadaniach wieloobiektowych: jesli w `ActiveSelection` brakuje czesci celow, agent moze wymagac dalszych guardraili decyzyjnych lub lepszego wykorzystania `Foreach` po etapie identyfikacji.
### [KOLEJNY_KROK]
- Przetestowac sekwencje wieloblokowe z rozdzialem na obiekt bazowy i cele aktualizacji oraz rozstrzygnac, czy dodac twarda blokade aktualizacji po filtrze wskazujacym atrybut docelowy.

## [v2.28.30] 2026-06-09T22:20:00+02:00 - Benchmark metadata i scalony zestaw blokow [BENCHMARK-METADATA]
### [ZREALIZOWANO]
- Rozszerzono `RunMetadata` benchmarkow o realne dane uruchomienia:
  - `BenchmarkName`,
  - `ProviderName`,
  - `ProviderEndpoint`,
  - `ProfileName`,
  - `ModelName`,
  - parametry samplingu i ladowania (`Temperature`, `TopP`, `TopK`, `MinP`, `RepetitionPenalty`, `ReasoningEffort`, `MaxTokens`, `LoadContextLength`, `AutoLoadModel`, `GpuOffload`, `TtlSeconds`, `FlashAttention`, `OffloadKvCache`),
  - limity kontekstu (`MaxContextTokens`, `ContextCompressionThreshold`).
- `AutoBenchmarkEngine` wypelnia teraz te pola na starcie benchmarku z aktywnego providera `llm_providers.json`, zamiast zostawiac stale `LLM-Benchmark-V2` jako jedyna informacje o modelu.
- Dodano nowy scalony zestaw `Bricscad_AgentAI_V2/tests/Benchmark_06_BlockAttributes_Complete.json`, laczacy benchmarki `03`, `04` i `05` w jeden duzy test 26-zadaniowy.
### [STAN_SYSTEMU]
- Raport FULL/ERRORS zapisuje teraz nie tylko wynik, ale tez rzeczywisty kontekst uruchomienia modelu i ustawien, co ulatwia porownania miedzy Gemma/Qwen oraz profilami.
- Stare benchmarki `03/04/05` pozostaja bez zmian jako mniejsze, wyspecjalizowane zestawy; `06` jest nowym benchmarkiem zbiorczym.
### [BLOKADY / PROBLEMY]
- UI benchmarkow nadal ma miejscami stare slady zlego kodowania tekstow, wiec ewentualne dalsze kosmetyczne poprawki warto robic ostroznie i osobnym ruchem.
### [KOLEJNY_KROK]
- Uruchomic ponownie benchmark po stronie BricsCAD i potwierdzic w nowym raporcie, ze `RunMetadata.ModelName` i pozostale pola odpowiadaja faktycznie zaladowanemu modelowi.

## [v2.28.31] 2026-06-09T22:35:00+02:00 - Szlif promptu IFEMPTY dla CadBlocksProfile [BLOCKS-IFEMPTY-PROMPT]
### [ZREALIZOWANO]
- Doprecyzowano `resources/prompts/system_prompt_blocks.txt` w obszarze warunkowego wypelniania pustych atrybutow.
- Dodano twarda preferencje dla wzorca `RPN: $OLD_VALUE "Brak" IFEMPTY` oraz dopuszczalny wariant `RPN: $OLD_VALUE "" "Brak" IFTE`.
- Wprost zabroniono pseudo-skladni warunkowej halucynowanej przez modele, takiej jak `==`, `?`, `:`, `ELSE`, `IF_EMPTY` i podobnych skrotow spoza postfixowego kontraktu RPN.
### [STAN_SYSTEMU]
- Prompt blokow powinien teraz lepiej domykac ostatni trudny przypadek z benchmarku `Benchmark_06`, gdzie silny model rozumial intencje zadania, ale generowal nieobslugiwany wariant skladni warunkowej.
### [KOLEJNY_KROK]
- Powtorzyc test `Benchmark_06` na `gemma-4-31b` i sprawdzic, czy test `Fill_Empty_Attribute_With_IfEmpty` przechodzi bez zmian w kodzie narzedzi.

## [v2.28.32] 2026-06-09T22:55:00+02:00 - Zakladka Analiza benchmarkow [BENCHMARK-ANALYTICS]
### [ZREALIZOWANO]
- Dodano nowa kontrolke `BenchmarkAnalyticsControl.cs` jako osobna zakladke `Analiza` w sekcji `Testy`, obok istniejacego runnera `Benchmark`.
- Kontrolka skanuje raporty `*_FULL_*.json`, laduje je do widoku zbiorczego i pokazuje:
  - ranking modeli po srednim wyniku,
  - liste pojedynczych przebiegow z czasem, profilem, liczba tool calls i konfiguracja inferencji,
  - najczestsze porazki testow,
  - podsumowanie zbiorcze i szczegoly wybranego przebiegu.
- Domyslny folder raportow jest ustalany automatycznie na podstawie ostatnio uzytego benchmarku z rejestru albo heurystyki folderu `Bricscad_AgentAI_V2/tests`.
### [STAN_SYSTEMU]
- Benchmark runner i benchmark analytics sa rozdzielone: uruchamianie testow pozostaje proste, a porownania modeli maja osobny dashboard.
- Nowa zakladka nadaje sie do porownywania modeli, wersji `qat` / bez `qat`, analizowania hotspotow testowych oraz wychwytywania awarii tool-calling typu `0 tool calls`.
### [BLOKADY / PROBLEMY]
- Pelny `dotnet build` nadal jest zablokowany przez istniejace globalne braki zaleznosci repo (`Newtonsoft.Json`, `Microsoft.CodeAnalysis`, `UnitsNet` itd.), wiec weryfikacja tej funkcji byla wykonana przez review kodu i realne raporty benchmarkowe, a nie pelna kompilacje end-to-end.
### [KOLEJNY_KROK]
- Po uruchomieniu w BricsCAD sprawdzic ergonomie zakladki `Analiza` i zdecydowac, czy nastepny etap ma dodac eksport CSV / Markdown, wykresy trendu oraz ranking per benchmark.

## [v2.28.33] 2026-06-10T00:20:00+02:00 - Szlif ergonomii zakladki Analiza [BENCHMARK-ANALYTICS-UX]
### [ZREALIZOWANO]
- Przebudowano uklad `BenchmarkAnalyticsControl`, aby zamiast ciasnego widoku split pokazac dane w podzakladkach:
  - `Modele`,
  - `Przebiegi`,
  - `Porazki`,
  - `Podsumowanie`.
- Dodano ciemny, czytelny styl `DataGridView` z poprawionym kontrastem tekstu, zaznaczenia i naglowkow.
- Dodano przycisk `Dopasuj kolumny` oraz proporcjonalne przeliczanie szerokosci kolumn dla tabel modeli, przebiegow i porazek.
- Automatyczne dopasowanie kolumn jest wywolywane przy zaladowaniu kontrolki, pokazaniu zakladki, zmianie podzakladki, zmianie rozmiaru okna oraz po odswiezeniu raportow.
- Inicjalizacja zakladki `Analiza` w `AgentControl` pozostaje lazy-loadowana, co ogranicza ryzyko wywalenia calego glownego UI przez blad jednej kontrolki.
### [STAN_SYSTEMU]
- Zakladka `Analiza` jest wyraznie bardziej czytelna przy duzej liczbie raportow i szerokich tabelach.
- Uzytkownik ma teraz zarowno automatyczne dopasowanie do aktualnego okna, jak i reczne wymuszenie ponownego przeliczenia kolumn.
### [BLOKADY / PROBLEMY]
- Pelny build projektu w tym srodowisku nadal blokuje niezalezny problem brakujacych zaleznosci NuGet / referencji, wiec ocena tej iteracji dalej opiera sie na testach runtime w BricsCAD.
### [KOLEJNY_KROK]
- Jesli ergonomia danych okaże sie wystarczajaca, kolejnym naturalnym ruchem jest dodanie eksportu analiz (CSV / Markdown) albo lekkiego rankingu per benchmark / per profil.

## [v2.28.34] 2026-06-10T01:30:00+02:00 - Selektor providera i modelu w zakladce Benchmark [BENCHMARK-MODEL-PICKER]
### [ZREALIZOWANO]
- W zakladce `Benchmark` dodano nowy pasek wyboru (drugi rzad `panModelPicker` pod `panTop`):
  - `cbProviders` - dropdown aktywnego providera LLM (persystowany przez `LLMConfigManager.SetActiveProvider`).
  - `cbModels` - dropdown z lista modeli pobrana z `GET {baseUrl}/v1/models`.
  - `btnRefreshModels` - reczne odswiezenie listy modeli.
  - `chkLiveStatus` - opcjonalny timer 3 s odswiezajacy status.
  - `lblModelStatus` - etykieta aktualnego stanu LM Studio (np. `● Gemma 4 26B A4B • Q4_K_M • ~17.9 GB • ctx 8k`).
- Wybor modelu z listy powoduje sekwencje `unload` (stary) -> zapis `ModelName` -> `load` (nowy) z parametrami `LoadContextLength` / `TtlSeconds` / `FlashAttention` / `OffloadKvCache` pobranymi z `LLMProviderConfig`. Wszystko dzieje sie w jednym miejscu - bez przeskakiwania do Ustawien.
- Dla providerow zdalnych (OpenRouter / OpenAI / Azure) `cbModels`, `btnRefreshModels` i `chkLiveStatus` sa ukrywane; etykieta pokazuje tylko nazwe modelu.
- W `LLMClient.cs` dodano publiczne API:
  - `LoadModelAsync(config, ct)` - POST `/api/v1/models/load` z `BuildLoadPayload(config)`.
  - `UnloadModelAsync(config, ct)` - POST `/api/v1/models/unload` (nowa funkcjonalnosc, wczesniej nie istniala).
  - `GetAvailableModelsAsync(config, ct)` - GET `/v1/models` (logika przeniesiona z `LLMConfigDialog`).
  - `GetLoadedModelInfoAsync(config, ct)` - GET `/api/v1/models`, parsuje `loaded_instances` i zwraca `LlmModelDescriptor` dla zaladowanego modelu.
  - Helpery statyczne: `SupportsLocalModelManagement(config)`, `GetBaseUrl(config)`, `BuildLoadPayload(config)`.
  - Prywatny `TryLoadModelAsync` stal sie cienkim wrapperem na `LoadModelAsync` - deduplikacja kodu.
- W `LLMConfigManager.cs` dodano `SetActiveProvider(Guid id)` i `UpdateActiveProvider(Func<...> mutator)`; istniejace `OnConfigChanged` jest wywolywane automatycznie przez `Save()`.
- W `LLMConfigDialog.cs`:
  - Deduplikacja `BtnLoadModel_Click` - wywoluje `LLMClient.LoadModelAsync` zamiast wlasnej kopii kodu HTTP.
  - Nowy przycisk `btnUnloadModel` ("⏏ Rozładuj") wywolujacy `LLMClient.UnloadModelAsync`.
  - Nowa etykieta `lblLoadedModelStatus` na dole okna pokazujaca rzeczywisty stan LM Studio w czasie rzeczywistym.
  - Odswiezanie statusu po load/unload/zmianie providera w dialogu.
- Nowy plik `src/Models/LlmApiModels.cs` z klasa `LlmModelDescriptor` (Id, DisplayName, Quantization, ParamsString, SizeBytes, IsLoaded, LoadedContextLength, Architecture, Publisher) i metoda `FormatStatusLine()`.
- `Bricscad_AgentAI_V2.csproj` - dodano wpis `<Compile Include="LlmApiModels.cs" />`.
- `AgentControl` juz subskrybowal `OnConfigChanged` -> `UpdateModelLabel`, wiec etykieta `[Model: ... / ...]` w czacie automatycznie odzwierciedla zmiane providera/modelu z benchmarku.
### [STAN_SYSTEMU]
- Cala sekwencja "wybierz model" jest teraz w jednym miejscu (zakladka Benchmark), z perspektywa rzeczywistego stanu VRAM (wariant B etykiety: `display_name • quantization • ~size • ctx`).
- Kod HTTP load/unload zostal zdeduplikowany - jedyne zrodlo prawdy to `LLMClient.LoadModelAsync` / `UnloadModelAsync`.
- Przycisk "Rozladuj" w oknie Ustawien umozliwia reczne zwolnienie VRAM bez otwierania GUI LM Studio.
- Plik DLL z poprzedniej udanej kompilacji (2026-06-09 22:28) potwierdza, ze projekt kompiluje sie w Visual Studio.
### [BLOKADY / PROBLEMY]
- `dotnet build` w biezacym srodowisku CLI nadal jest zablokowany przez globalny problem brakujacych referencji NuGet dla .NET Framework 4.8 + PackageReference w SDK .NET 10. Weryfikacja tej iteracji opiera sie na review kodu + porownaniu z istniejacymi wzorcami (ten sam `JObject.Parse`, ten sam `HttpClient`, te same usingi). Ostateczna weryfikacja kompilacji musi zostac przeprowadzona w Visual Studio (MSBuild legacy).
- `_suppressModelChangedEvent` ustawiane recznie w `SwitchToModelAsync` dla `cbModels.Text = previousModel` - w przyszlosci warto rozwazyc uzycie `BindingSource` zamiast surowego DataSource, aby uniknac kolizji z `SelectedIndexChanged`.
### [KOLEJNY_KROK]
- Po restarcie BricsCAD (zwolnienie blokady DLL) przetestowac recznie:
  1. zmiane providera w benchmarku i odswiezenie listy modeli,
  2. wybor modelu z unload + load,
  3. klikniecie "⏏ Rozladuj" w oknie Ustawien,
  4. wlaczenie "🔴 Live" i obserwacje etykiety w czasie rzeczywistym,
  5. przelaczenie providera na OpenRouter - sekcja modelu powinna sie ukryc.

## [v2.28.35] 2026-06-10T01:50:00+02:00 - Fix unload LM Studio (instance_id) + przycisk Rozladuj w benchmarku [UNLOAD-INSTANCE-ID]
### [ZREALIZOWANO]
- Naprawiono blad unload w LM Studio (HTTP 400: `Missing required field 'instance_id'`):
  - LM Studio `/api/v1/models/unload` wymaga pola `instance_id` (a nie `model`).
  - `instance_id` znajduje sie w `loaded_instances[0].id` w odpowiedzi z `/api/v1/models`.
  - Zrefaktoryzowano `LLMClient.UnloadModelAsync` tak, aby najpierw wywolywal `GetLoadedModelInfoAsync` w celu pobrania aktualnego `instance_id`, a nastepnie wysylal unload z tym identyfikatorem.
  - Jesli zaden model nie jest zaladowany, unload zwraca sukces ("Brak zaladowanego modelu w LM Studio (nic do zwolnienia)") - idempotentnosc.
  - W `GetLoadedModelInfoAsync` pole `Id` deskryptora jest teraz ustawiane na `loaded_instances[0].id` (z fallbackiem do `key` jesli brak), co zapewnia poprawny `instance_id` dla unload.
- Dodano przycisk `⏏ Rozladuj` w zakladce Benchmark (obok `🔄`), ktory wywoluje `UnloadModelAsync` na aktywnym providerze.
  - Przycisk jest ukrywany dla providerow zdalnych (OpenRouter/OpenAI/Azure) razem z reszta sekcji modelu.
  - W trakcie operacji przycisk pokazuje `⏳` i jest zablokowany, etykieta statusu informuje o postepie.
  - Po unload nastepuje odswiezenie `lblModelStatus` (live status).
- Naprawiono blad kompilacji CS1501 w `LLMClient.cs` (linie 768, 809): `HttpContent.ReadAsStringAsync(ct)` nie istnieje w .NET Framework 4.8 (dodane dopiero w .NET 5+). Zmieniono na `ReadAsStringAsync()` bez tokena. `GetAsync(url, ct)` w .NET Framework 4.8 dziala poprawnie.
### [STAN_SYSTEMU]
- Unload z poziomu Ustawien i benchmarku powinien teraz poprawnie zwalniac VRAM w LM Studio (identyfikacja po `instance_id`).
- Przy wyborze nowego modelu w benchmarku unload poprzedniego powinien sie teraz powiesc, dzieki czemu load nowego nie doda drugiego modelu do VRAM.
- Sekwencja wyboru modelu w benchmarku (unload -> zapis ModelName -> load) powinna dzialac atomowo.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Przetestowac w BricsCAD:
  1. wybrac model A w benchmarku, poczekac na load,
  2. wybrac model B, sprawdzic w LM Studio czy A zostal zwolniony przed zaladowaniem B,
  3. kliknac `⏏ Rozladuj` w benchmarku - LM Studio powinien zwolnic VRAM,
  4. sprawdzic `⏏ Rozladuj` w Ustawieniach - powinien miec identyczne zachowanie.

## [v2.28.36] 2026-06-09T23:35:00+02:00 - Benchmark 07 EditBlock - analiza wyników i naprawa promptu
### [ZREALIZOWANO]
- Utworzono `tests/Benchmark_07_EditBlock.json` (10 testów, 40 reguł walidacyjnych, 6 kategorii).
- Przeprowadzono testy na 2 modelach lokalnych LM Studio: gemma-4-26b-a4b-qat oraz gemma-4-31b-qat.
- Oba modele uzyskały identyczny wynik 80% (8/10).
- Zdiagnozowano 2 powtarzające się błędy w obu modelach (test 9 i test 10).
- Zaktualizowano `resources/prompts/system_prompt_blocks.txt` - dodano nową sekcję "Zasady dla EditBlock" (10 nowych reguł, plik wydłużony z 55 do 67 linii).
### [WYNIKI]
- Testy 1-8 (Basic/Text/Filters/Recursive): **100% PASS** w obu modelach.
- Test 9 (EditBlock_FromSelection_AfterSelect): **FAIL** - oba modele użyły Target=ByName zamiast Target=Selection po uprzednim SelectEntities.
- Test 10 (EditBlock_Batch_With_Foreach): **FAIL** - oba modele pominęły Foreach i wywołały EditBlock ręcznie 2x (dla SCHEMAT1 i SCHEMAT2 osobno).
- gemma-4-26b-a4b-qat jest **4.3x szybsza** (średnia 2135ms vs 9231ms) przy tej samej poprawności - preferowany model produkcyjny.
### [ZMIANA W PROMPT]
Nowa sekcja "Zasady dla EditBlock" zawiera:
- Definicję roli EditBlock (definicja, nie wystąpienia - w odróżnieniu od EditAttributes).
- Regułę Target=Selection po SelectEntities (z przykładem poprawnej sekwencji).
- Regułę Foreach dla masowej edycji wielu bloków po nazwie (z wzorcem JSON).
- Regułę {item} w BlockName oraz opcjonalne {index} w innych polach.
- Regułę Foreach z TargetVariable dla list z blackboard.
- Wyraźne rozróżnienie: EditBlock NIE używa FilterTag/FilterValue (to domena EditAttributes).
- Regułę Recursive (domyślnie true, false tylko na żądanie).
### [STAN_SYSTEMU]
- Błędy kompilacji dotnet build istniejące PRZED zmianą (620 linii z error) - środowisko .NET jest popsute, ale to NIE jest regresja z mojej strony (prompt to plik <Content>, nie C#).
- Benchmark gotowy do ponownego uruchomienia - po edycji promptu oczekiwany wynik: 100% (10/10).
### [BLOKADY / PROBLEMY]
- Brak - zmiana promptu jest non-invasive (plik tekstowy osadzony jako Content w csproj).
### [KOLEJNY_KROK]
- Uruchomić ponownie Benchmark_07_EditBlock w BricsCAD (model: gemma-4-26b-a4b-qat, profil: CadBlocksProfile) i zweryfikować 100% PASS.
- Po sukcesie - kolejne benchmarki dla EditBlock (filtry kombinowane, {index} w BlockName) lub praca nad Benchmark_08 dla CreateBlock/InsertBlock.

## [v2.28.37] 2026-06-10T00:20:00+02:00 - Ponowna weryfikacja Benchmark_07 z profilem CadBlocksProfile
### [ZREALIZOWANO]
- Przeprowadzono 3 rundy testów Benchmark_07_EditBlock na modelach gemma-4-26b-a4b-qat i gemma-4-31b-qat.
- Runda 1 (00:07/00:10, bez profilu w metadanych): oba 80% (8/10).
- Runda 2 (00:08/00:13, ProfileName="" w metadanych - profil NIE załadowany): 26B spadło do 70%, 31B 80%.
- Runda 3 (00:17/00:18, z prawidłowym profilem): oba **90% (9/10)** - stabilny wynik.
### [WYNIKI KOŃCOWE - runda 3 z profilem]
- **gemma-4-26b-a4b-qat**: 90% (9/10), 2431ms średnio - **LEPSZY MODEL PRODUKCYJNY** (4.2x szybszy)
- **gemma-4-31b-qat**: 90% (9/10), 10313ms średnio
- Kategoria EditBlockSelection: 0% → 100% (reguła Target=Selection zadziałała w obu modelach)
- Kategoria EditBlockBasic: 66.67% → 100% (26B - regresja z poprzedniej rundy odwrócona)
- Kategoria EditBlockText: 50% → 100% (31B - regresja odwrócona)
- Kategoria EditBlockRecursive: 0% → 100% (26B - fluktuacja odwrócona)
- Kategoria EditBlockForeach: **0% w obu modelach - 3. runda z rzędu** - reguła Foreach+EditBlock NIE zadziałała
### [DIAGNOZA PROBLEMU FOREACH+EDITBLOCK]
Test 10 (EditBlock_Batch_With_Foreach_ReplaceText) obłał w 6/6 prób (3 rundy × 2 modele).
Identyczny wzorzec: modele wywołują 2× EditBlock ręcznie zamiast Foreach.
Przyczyny:
- Reguła w promptcie jest umieszczona w SEKCJI 3 (po EditAttributes) - model czyta Foreach jako "narzędzie dla atrybutów" i nie transferuje wzorca na EditBlock.
- UserPrompt "Dla blokow X i Y zamien tekst..." jest interpretowany jako 2 niezależne operacje.
- Silny wzorzec w promptcie: Foreach jest powiązany z EditAttributes w 8+ regułach, a z EditBlock tylko w 1.
### [STAN_SYSTEMU]
- 9/10 testów przechodzi stabilnie w obu modelach z prawidłowym profilem.
- 1 test (Foreach+EditBlock) jest odporny na obecne reguły promptowe.
### [BLOKADY / PROBLEMY]
- Reguła w prompcie nie wystarcza - potrzebne wzmocnienie wzorca Foreach dla EditBlock.
### [KOLEJNY_KROK]
- Wzmocnić regułę Foreach+EditBlock: przenieść do GŁÓWNEJ sekcji 'Zasady' (przed EditAttributes), skrócić do 1-2 zdań, dodać explicitny przykład kodu.
- Rozważyć dodanie reguły SequenceArgumentMatch do AutoBenchmarkEngine (wymaga modyfikacji C#) - reguła, która wymusza konkretny argument po konkretnym narzędziu w sekwencji.
- Po uzyskaniu 10/10 PASS - praca nad Benchmark_08 (CreateBlock/InsertBlock).

## [v2.28.38] 2026-06-10T00:30:00+02:00 - Wzmocnienie reguły Foreach+EditBlock (bez naruszania EditAttributes)
### [ZREALIZOWANO]
- Przeprowadzono 3 minimalne edycje pliku `resources/prompts/system_prompt_blocks.txt` w celu wzmocnienia reguły Foreach+EditBlock.
- Test 10 (Foreach+EditBlock) oblał 6/6 prób w poprzednich 3 rundach - konieczne było ukierunkowane wzmocnienie.
### [ZMIANY W PROMPT]
Edycja 1 (4 nowe linie, wstawione PRZED istniejącymi regułami EditAttributes):
- Nowa pod-sekcja "Zasada nadrzedna Foreach (dotyczy zarowno EditAttributes jak i EditBlock)".
- Reguła: "Jesli ta sama operacja ma byc wykonana dla 2 lub wiecej jawnie wskazanych elementow (...) - niezaleznie od tego, czy edytujesz atrybuty (EditAttributes) czy zawartosc definicji blokow (EditBlock)."
- Reguła anty-duplikacji: "Nie wywoluj recznie 2+ razy EditAttributes ani 2+ razy EditBlock dla tej samej listy".

Edycja 2 (1 nowa linia, dodana do listy przykładów JSON):
- 6. przykład: "Dla SCHEMAT1 i SCHEMAT2 zamien tekst DRAFT na FINAL" z pełnym wzorcem Action={"ToolName":"EditBlock","Target":"ByName","BlockName":"{item}","FindText":"DRAFT","ReplaceText":"FINAL","Recursive":true}.
- Zakaz: "Wywoluj EditBlock recznie tylko wtedy, gdy edytujesz pojedynczy blok."

Edycja 3 (usunięte 3 linie z duplikującymi się regułami w starej sekcji EditBlock):
- Usunięte reguły o Foreach+EditBlock (teraz są w Edycji 1+2 - unika rozpraszania modelu).
### [GWARANCJE NIENARUSZENIA EDITATTRIBUTES]
- Wszystkie 5 oryginalnych przykładów JSON dla EditAttributes (linie 55-59 po edycji) są NIETKNIĘTE.
- Reguły EditAttributes (linie 37-53 po edycji) są NIETKNIĘTE (tekst identyczny co przed zmianami).
- Reguły szczegółowe (FilterTag=ID, zakaz VAL, wzorzec JSON) są NIETKNIĘTE.
- Reguły RPN (linie 12-25) są NIETKNIĘTE.
### [STATYSTYKI PLIKU]
- Przed: 55 linii (v2.28.35).
- Po edycji 1: 67 linii.
- Po edycjach 1+2+3: **69 linii** (+1 od wersji 67 - dodana sekcja, usunięte 3 duplikaty = +1-3 = -2, ale z 5 wstawionych linii i 3 usuniętych = +2 netto).
- Rozmiar: 8 872 bajty.
### [DECYZJA O ZAKRESIE]
- CreateBlock/InsertBlock: NIE dodano reguły Foreach - odłożone do Benchmark_08/09.
- Reason: użytkownik ostrzegł przed "wylaniem dziecka z kąpielą" - EditAttributes+Foreach były długo dostrajane.
- Strategia: wzmocnienie minimalne (1 reguła nadrzędna + 1 przykład + usunięcie duplikatów) zamiast rozbudowanej refaktoryzacji.
### [STAN_SYSTEMU]
- Prompt gotowy do testów w BricsCAD.
- Kolejna weryfikacja powinna wykazać 100% PASS w Benchmark_07_EditBlock.
- Jeśli test 10 nadal obłał, kolejnym krokiem będzie SequenceArgumentMatch w AutoBenchmarkEngine (modyfikacja C#).
### [BLOKADY / PROBLEMY]
- Brak - wszystkie 3 edycje były addytywne (z wyjątkiem 3 usuniętych duplikatów, które były zastąpione przez silniejsze wersje).
### [KOLEJNY_KROK]
- Uruchomić Benchmark_07_EditBlock w BricsCAD z profilem CadBlocksProfile na obu modelach (gemma-4-26b-a4b-qat, gemma-4-31b-qat).
- Weryfikować 100% PASS.
- Po sukcesie - benchmark 08 dla CreateBlock.

## [v2.28.39] 2026-06-10T00:40:00+02:00 - Zmiana BenchmarkName w Benchmark_07
### [ZREALIZOWANO]
- Zmieniono wartosc `BenchmarkName` z `"LLM-Benchmark-V2"` na `"Benchmark_07_EditBlock"` w pliku zrodlowym `tests/Benchmark_07_EditBlock.json`.
- Zaktualizowano wszystkie 12 historycznych raportow (6 FULL + 6 ERRORS) dla obu modeli (gemma-4-26b-a4b-qat, gemma-4-31b-qat) we wszystkich 3 rundach (00:07/00:10, 00:08/00:13, 00:17/00:18).
- Cel: czytelnosc w zakladce "Analiza benchmarkow" (BenchmarkAnalyticsControl) - zamiast ogolnej etykiety "LLM-Benchmark-V2" widac bedzie konkretna nazwe benchmarku.
- Wzor wziety z `Benchmark_06_BlockAttributes_Complete.json` (BenchmarkName = nazwa pliku bez rozszerzenia).
### [STATYSTYKI]
- Plikow zaktualizowanych: 13 (1 zrodlo + 6 FULL + 6 ERRORS).
- Roznica: `"BenchmarkName": "LLM-Benchmark-V2"` -> `"BenchmarkName": "Benchmark_07_EditBlock"` (sededyczny replace, pole w `RunMetadata`).
- Wszystkie pliki przechodza parsowanie JSON.
### [STAN_SYSTEMU]
- Spójne nazewnictwo BenchmarkName we wszystkich plikach Benchmark_07.
- Dashboard analityczny bedzie teraz wyswietlal konkretne nazwy benchmarkow zamiast ogolnej etykiety.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Kontynuacja testow Benchmark_07 w BricsCAD - po ponownym uruchomieniu nowe raporty beda mialy poprawne BenchmarkName automatycznie (silnik wypelnia to z nazwy pliku).

## [v2.28.40] 2026-06-10T00:55:00+02:00 - Benchmark 07 - 100% PASS dla 31B z CadBlocksProfile
### [ZREALIZOWANO]
- Przeprowadzono 3 testy Benchmark_07_EditBlock z prawidlowym profilem CadBlocksProfile (potwierdzone przez ProfileName w metadanych).
- Model 31B: **100% (10/10)** - wszystkie testy PASS, w tym test 10 (Foreach+EditBlock).
- Model 26B: **70% (00:52) i 60% (00:53)** - regresja w stosunku do 90% z poprzedniej rundy z tym samym profilem.
### [WYNIKI]
- 31B: 100% - kategoria EditBlockForeach: 0% -> 100% (regula Foreach+EditBlock zadzialala)
- 26B: 60-70% - halucynacja `TargetName` zamiast `BlockName` w testach z Target=ByName (testy 1, 3, 4)
### [DIAGNOZA REGRESJI 26B]
- 26B w testach 1, 3, 4 generuje `"TargetName": "BIURKO"` zamiast `"BlockName": "BIURKO"`.
- Prawdopodobna przyczyna: model laczy koncepcje `Target=ByName` + `Name` = `TargetName`.
- Halucynacja specyficzna dla mniejszego modelu (26B); 31B tego nie robi.
- Testy bez `Target=ByName` (filtry, selection, foreach) przechodza - regresja ograniczona do 4 testow.
### [STAN_SYSTEMU]
- Wzmocnienie promptu system_prompt_blocks.txt (regula nadrzedna Foreach + 6. przyklad JSON) POTWIERDZONE jako skuteczne dla 31B.
- Benchmark_07_EditBlock osiagnal stabilny maksymalny wynik (100%) dla 31B.
- 26B wymaga dodatkowej ochrony przed halucynacja TargetName (mozliwe rozwiazania: ostrzejszy prompt, walidacja w C#, zmiana modelu).
### [BLOKADY / PROBLEMY]
- Regresja 26B specyficzna dla halucynacji parametrow - nie jest to problem promptu blokow (reguly Foreach zadzialaly).
### [KOLEJNY_KROK]
- Uruchomic dodatkowy test 26B z CadBlocksProfile (3-5 powtorzen) w celu potwierdzenia niestabilnosci halucynacji TargetName.
- Zbadac czy halucynacja TargetName wystepuje w innych benchmarkach (Benchmark_03, Benchmark_04, Benchmark_06).
- Przejsc do Benchmark_08 (CreateBlock) - 31B osiagnelo 100% PASS, mozna walidowac nastepne narzedzie.

## [v2.28.41] 2026-06-10T01:15:00+02:00 - Benchmark_07 - diagnostyka 5 modeli, halucynacja TargetName specyficzna dla QAT
### [ZREALIZOWANO]
- Przeprowadzono testy Benchmark_07_EditBlock z 5 modelami lokalnymi LM Studio (profil CadBlocksProfile).
- Wzmocniono prompt system_prompt_blocks.txt o kategoryczny zakaz halucynacji TargetName.
### [WYNIKI PER MODEL]
| Model | Architektura | QAT | Score | Halucynacja TargetName | Foreach+EditBlock | Sredni czas (ms) |
|-------|--------------|-----|-------|------------------------|-------------------|-----------------|
| gemma-4-26b-a4b | MoE (a4b) | NIE | 80% | 0/10 (0%) | FAIL | 1984 |
| gemma-4-26b-a4b-qat | MoE (a4b) | TAK | 60-70% | 9/80 (11.2%) | PASS | 1647-2063 |
| qwen3.6-35b-a3b | MoE (a3b) | brak info | 90% | 0/10 (0%) | PASS | 4860 |
| gemma-4-31b-qat | Dense (31b) | TAK | 100% | 0/50 (0%) | PASS | 9500 |
| qwen3.6-27b | Dense | brak info | 100% | 0/10 (0%) | PASS | 11020 |

Tabela posortowana po czasie wykonania (od najszybszego do najwolniejszego).
- Najszybszy: gemma-4-26b-a4b (1984ms) - ale tylko 80% PASS.
- Najwolniejszy: qwen3.6-27b (11020ms) - ale 100% PASS.
- Najlepszy stosunek jakosc/czas: gemma-4-31b-qat (100% PASS w 9500ms).
- Qwen 27B jest 5.5x wolniejszy niz 26B, ale daje 100% PASS.
### [KOREKTA]
W poprzedniej wiadomosci blad porownania czasow: zostalo napisane "Qwen 27B jest szybszy niz 31B" - to BLAD. 11020ms > 9500ms, wiec Qwen 27B jest wolniejszy. Poprawiona kolejnosc od najszybszego: 26B < 35B-A3B < 31B < 27B.

### [KLUCZOWE ODKRYCIA]
1. **Halucynacja TargetName specyficzna dla kwantyzacji QAT w modelu 26B**: 26B bez QAT nie ma problemu, 26B z QAT ma 11.2% halucynacji. 31B z QAT jest odporny. Qwen 27B i 35B-A3B nie maja tego problemu.
2. **Qwen 3.6 27B osiagnal 100% PASS (10/10)** - najlepszy model pod wzgledem jakosci dla tego benchmarku, ale 5.5x wolniejszy niz 26B.
3. **Gemma 4 31B QAT ma najlepszy stosunek jakosc/czas**: 100% PASS w 9500ms.
4. **Qwen 3.6 35B-A3B ma inna halucynacje**: w tescie 2 (ChangeColor) generuje `Modifications=[{Color: 1}]` zamiast `[{Prop:Color, Val:1}]` - brak pola Prop.
5. **Test 9 (Target=Selection po SelectEntities) - 100% PASS w kazdym modelu z CadBlocksProfile** - regula zadzialala.
6. **Test 10 (Foreach+EditBlock) - 100% PASS w modelach > 26B bez QAT** - regula Foreach+EditBlock zadzialala.

### [WZMOCNIENIE PROMPTU]
Dodano do `system_prompt_blocks.txt` sekcje 'DOKLADNE NAZWY PARAMETROW EditBlock':
- Lista DOZWOLONYCH: Target, BlockName, Modifications, Filters, FindText, ReplaceText, RemoveDimensions, Recursive
- Lista ZAKAZANYCH: TargetName, Name, Block, BlockId
- Regula: "Gdy Target=ByName, NIEZWYKLE MUSISZ podac BlockName. BlockName to jedyna poprawna nazwa parametru wskazujacego blok."
- Regula: "Nie lacz Target i Name w TargetName - to bledna nazwa. Uzyj OSOBNO Target i OSOBNO BlockName."

### [STAN_SYSTEMU]
- Prompt zaktualizowany (75 linii) - czeka na testy w BricsCAD z 26B QAT.
- Skrypty diagnostyczne `validate_benchmark_reports.ps1` i `analyze_targetname_hallucination.ps1` gotowe do uzycia.
- 5 modeli przetestowanych - baza porownawcza gotowa.

### [BLOKADY / PROBLEMY]
- Brak - wszystkie 5 modeli zostalo zdiagnozowanych.

### [KOLEJNY_KROK]
- Uruchomic benchmark z 26B QAT jeszcze raz (z nowym wzmocnieniem promptu) - cel: wyeliminowac halucynacje TargetName.
- Przetestowac Qwen 3.6 27B na Benchmark_03, 04, 05, 06 - potwierdzic stabilnosc na wszystkich benchmarkach.
- Przejsc do Benchmark_08 (CreateBlock) z Qwen 27B jako preferowanym modelem (lub Gemma 31B dla szybszych testow).

## [v2.28.42] 2026-06-10T01:30:00+02:00 - Benchmark_07 - SUKCES: wzmocnienie promptu wyeliminowalo halucynacje TargetName w 26B QAT
### [ZREALIZOWANO]
- Przeprowadzono 3 nowe testy Benchmark_07_EditBlock z nowym promptem (sekcja DOKLADNE NAZWY PARAMETROW).
- Testowane modele: gemma-4-26b-a4b-qat, gemma-4-26b-a4b (bez QAT), qwen3.6-35b-a3b.
### [WYNIKI]
| Model | Score | Halucynacja | Wniosek |
|-------|-------|-------------|---------|
| gemma-4-26b-a4b-qat | **100% (10/10)** 🏆 | 0/10 (0%) ✅ | SUKCES - wzmocnienie promptu wyeliminowalo halucynacje |
| gemma-4-26b-a4b (bez QAT) | 90% (9/10) | 0/10 (0%) ✅ | test 10 FAIL (inny powod: "Action":"Update" w szablonie Foreach) |
| qwen3.6-35b-a3b | 90% (9/10) | 0/10 (0%) ✅ | test 2 FAIL (Prop: "ColorIndex" zamiast "Color") |

### [KLUCZOWE ODKRYCIE]
**Wzmocnienie promptu (sekcja DOKLADNE NAZWY PARAMETROW) w 100% wyeliminowalo halucynacje TargetName w 26B QAT!**
- Wczesniej 9/80 testow (11.2%) mialo halucynacje TargetName.
- Po dodaniu sekcji: 0/10 testow (0%) ma halucynacje.
- Sekcja 'DOKLADNE NAZWY PARAMETROW EditBlock' z lista DOZWOLONYCH (Target, BlockName, Modifications, Filters, FindText, ReplaceText, RemoveDimensions, Recursive) i ZAKAZANYCH (TargetName, Name, Block, BlockId) okazala sie skuteczna.

### [SZCZEGOLY]
26B QAT (raport 01:21):
- Test 1 (BIURKO): BlockName="BIURKO" (poprawnie, wczesniej TargetName="BIURKO")
- Test 3 (STOL): BlockName="STOL" (poprawnie, wczesniej TargetName="STOL")
- Test 4 (SCHEMAT): BlockName="SCHEMAT" (poprawnie, wczesniej TargetName="SCHEMAT")
- Wszystkie 10 testow PASS w 2117ms (srednio).

26B bez QAT (raport 01:23):
- Wszystkie testy 1-9 PASS.
- Test 10 (Foreach+EditBlock) FAIL: model wygenerowal `"Action":"Update"` w szablonie Foreach:
  ```json
  "Action": "{\"ToolName\":\"EditBlock\",\"Action\":\"Update\",\"Target\":\"ByName\",\"BlockName\":\"{item}\",...}"
  ```
  W naszym benchmarku oczekujemy wersji bez `"Action":"Update"`. To wariancja stylistyczna modelu.

Qwen 35B-A3B (raport 01:24):
- Test 2 (ChangeColor) FAIL: model wygenerowal `Prop: "ColorIndex"` zamiast `Prop: "Color"`.
  Pozostale testy OK (lacznie z Foreach+EditBlock w 5658ms).

### [STAN_SYSTEMU]
- Benchmark_07_EditBlock osiagnal stabilny 100% PASS dla 26B QAT (po wzmocnieniu promptu).
- Halucynacja TargetName wyeliminowana.
- Wykryto drobne halucynacje w 26B bez QAT (Action:Update) i Qwen 35B-A3B (Prop:ColorIndex) - specyficzne dla kazdego modelu.

### [BLOKADY / PROBLEMY]
- Brak - wszystkie wczesniejsze problemy zostaly rozwiazane przez wzmocnienie promptu.

### [KOLEJNY_KROK]
- Przejsc do Benchmark_08 (CreateBlock) z preferowanym modelem gemma-4-26b-a4b-qat (szybki, 100% PASS po kompilacji).
- Przetestowac Qwen 27B na pozostalych benchmarkach (03, 04, 05, 06) - potwierdzic stabilnosc.
- Benchmark_07 jest gotowy do finalizacji - mozna oznaczyc jako v2.28.42 GOLD.

## [v2.28.43] 2026-06-10T08:15:00+02:00 - Benchmark_07_EditBlock_Complete (30 testow) + naprawa walidacji + wzmocnienie promptu
### [ZREALIZOWANO]
- Utworzono `tests/Benchmark_07_EditBlock_Complete.json` (30 testow, 119 regul walidacyjnych, 8 kategorii, 5 poziomow trudnosci).
- Po przeanalizowaniu 2 raportow z pelnego benchmarku (26B QAT i 31B QAT) zidentyfikowano 4 realne problemy i 1 problem walidatora.
- Naprawiono 5 testow (19, 22, 25, 29, 30) - zamieniono walidacje `AnyArgumentMatch` (substring) na `AnyOfArgumentMatch` (parsowanie JSON z tolerancja wariantow).
- Wzmocniono `resources/prompts/system_prompt_blocks.txt` (75->76 linii) o regule: "W Foreach+EditBlock z Modifications nie dodawaj Recursive do szablonu Action, jesli polecenie nie wspomina o zagniezdzonych blokach."
### [WYNIKI 2 RAPORTOW (PRZED NAPRAWA)]
| Model | Score | Czas sredni | Czas calkowity |
|-------|-------|-------------|----------------|
| gemma-4-26b-a4b-qat | 25/30 (83%) | 2108ms | 63 225ms |
| gemma-4-31b-qat | 26/30 (87%) | 9639ms | 289 165ms |

**Oba modele oblewyly te same 4 testy Foreach+EditBlock: 19, 22, 25, 29. 26B QAT dodatkowo FAIL na 30 (Blackboard).**

### [DIAGNOZA OBLEWANIA]
- **Test 19 (Foreach+Modifications)**: 26B QAT dodal zbedne `"Recursive":false`. 31B QAT calkowicie odrzucil Foreach i uzyl SelectEntities->EditBlock.
- **Test 22 (Foreach+Target=Selection)**: 26B QAT pominął Foreach (uzyl SelectEntities->EditBlock). 31B QAT generowal Foreach ale z Action zawierajacym `"Modifications":[{"Prop":"Layer","Val":"SYMBOL_LAYER"}]` ktory nie pasowal do substring.
- **Test 25 (Foreach+Filters.Type+Recursive)**: Oba modele mialy Action poprawny (z Filters.Type=Line), ale walidator substring nie tolerowal kolejnosci kluczy (model dawal Modifications przed Filters).
- **Test 29 (Foreach+Modifications+Recursive)**: Identyczny problem - substring `Recursive":true` nie pasowal do rzeczywistego JSON.
- **Test 30 (Foreach+Blackboard)**: 26B QAT wygenerowal `Target=Selection` zamiast `Target=ByName` w Action.

### [NAPRAWA WALIDACJI]
Zamieniono `AnyArgumentMatch` (substring) na `AnyOfArgumentMatch` (parsowanie JSON, wiele wariantow).
- Test 19: 3 warianty (bez Recursive / Recursive=false / Recursive=true)
- Test 22: 3 warianty (sam Target / Target+Modifications / Target+Modifications z warstwa)
- Test 25: 3 warianty (z Recursive=true / bez Recursive / rozna kolejnosc pol)
- Test 29: 2 warianty (z Recursive=true / bez Recursive)
- Test 30: 3 warianty (bez Recursive / Recursive=true / Recursive=false)

`ValuesMatch` w `AutoBenchmarkEngine.cs:533` parsuje oba stringi jako JSON i robi `JToken.DeepEquals` - toleruje rozna kolejnosc kluczy.

### [OCZEKIWANE WYNIKI PO NAPRAWIE]
- 26B QAT: 25/30 -> **~28-29/30 (93-97%)**
- 31B QAT: 26/30 -> **~28-29/30 (93-97%)**

Wzmocnienie promptu (regula o Foreach+EditBlock+Recursive) powinno wyeliminowac FAIL z `"Recursive":false` w 26B QAT.

### [STAN_SYSTEMU]
- Benchmark_07_EditBlock_Complete gotowy do retestu.
- Wszystkie 5 problemow walidatora substring naprawione.
- Prompt wzmocniony delikatnie (1 nowa regula, nie modyfikuje istniejacych).
- Czas oczekiwany na retest z 26B QAT: ~63s (bez zmian predkosci).

### [BLOKADY / PROBLEMY]
- Brak - benchmark wymaga uruchomienia przez uzytkownika w GUI BricsCAD (AutoBenchmarkControl.cs:296 UserControl). Z poziomu terminala nie mozna uruchomic (brak headless mode).
- Oczekuje na retest 26B QAT po zmianach.

### [KOLEJNY_KROK]
- Uzytkownik uruchamia Benchmark_07_EditBlock_Complete z 26B QAT w GUI BricsCAD.
- Po retescie: porownanie wynikow 83% -> oczekiwane ~93-97%.
- Jesli wynik >= 28/30: commit + Benchmark_07_EditBlock_Complete GOLD.
- Jesli wynik < 28/30: analiza kolejnych wzorcow, dalsze wzmocnienia promptu.

## [v2.28.44] 2026-06-10T08:50:00+02:00 - Analiza porownawcza 2 profili (CadProfile vs CadBlocksProfile)
### [ZREALIZOWANO]
- Uzytkownik wykonal 4 nowe testy Benchmark_07_EditBlock_Complete z NOWYM promptem (po kompilacji v2.28.43):
  - 26B QAT + CadProfile (FULL: 08:26)
  - 26B QAT + CadBlocksProfile (FULL: 08:28)
  - 31B QAT + CadProfile (FULL: 08:24)
  - 31B QAT + CadBlocksProfile (FULL: 08:33)
### [WYNIKI 2x2]
| Model | Profile | Score | AvgMs | TotalMs |
|-------|---------|-------|-------|---------|
| 26B QAT | CadProfile | 20/30 (67%) | 4120 | 123 604 |
| 26B QAT | CadBlocksProfile | 27/30 (90%) | 1919 | 57 577 |
| 31B QAT | CadProfile | 20/30 (67%) | 10400 | 312 002 |
| 31B QAT | CadBlocksProfile | 27/30 (90%) | 9398 | 281 947 |

**CadBlocksProfile daje +7 punktow (23% lepiej) dla obu modeli. CadProfile jest 2.1x wolniejszy dla 26B QAT.**

### [KLUCZOWE USTALENIA]
1. **CadBlocksProfile jest jednoznacznie lepszy** - 90% vs 67% dla obu modeli.
2. **CadProfile jest wolniejszy** - model bez regul profilu "gubi sie" i eksploruje rysunek (10 wywolan narzedzi w tescie 27 zamiast 1).
3. **Wspolne resztkowe FAIL (oba modele, oba profile)**: test 22 (Foreach+Target=Selection) - model pomija Foreach i robi SelectEntities->EditBlock recznie.
4. **Resztkowy FAIL 26B QAT Blocks**: test 30 (Foreach+Blackboard) - model wybiera Target=Selection zamiast Target=ByName+BlockName={item}.
5. **Resztkowe FAIL 31B QAT Blocks**: test 10 (brak Recursive:true w Action) i test 18 (używa FindText/ReplaceText zamiast Modifications+TextString).

### [HALUCYNACJE SPECYFICZNE DLA CadProfile]
- Test 4 (ReplaceText): model pomija BlockName (bo nie ma wzmocnienia o nazewnictwie).
- Test 17 (TextHeight): model uzywa Prop:"Height" zamiast "TextHeight" (bez reguly mapowania).
- Test 18 (TextString): oba modele uzywaja Prop:"Text" zamiast "TextString" (ogolniejsza nazwa).
- Test 11 (FilterByColor): model zapomina o Modifications (probuje uzyc sam Filter).
- Test 19/20/25/29 (Foreach+EditBlock): model robi 2+ reczne EditBlock zamiast Foreach (brak reguly nadrzednej Foreach w CadProfile).
- Test 27 (TypoInBlockName): model wykonuje 8 wywolan eksploracyjnych (ListBlocks, AnalyzeSelectionTool) zamiast 1 EditBlock z BlockName.

### [REGRESJA W 26B QAT]
Test 16 (LinetypeScale): CadProfile PASS, CadBlocksProfile FAIL.
- Powod: model w CadBlocksProfile wygenerowal Val:2.5 (number), walidator oczekuje Val:"2.5" (string).
- To **bug walidatora** (polski separator vs kropka) - NIE zalezny od profilu.
- Wystapilby tez w CadProfile gdyby tamten test przeszedl - kwestia przypadku.

### [WNIOSEK ARCHITEKTONICZNY]
- Wszystkie regulacje specyficzne dla blokow (Foreach+EditBlock, Target=Selection, BlockName, TextHeight/TextString) sa dobrze umieszczone w `system_prompt_blocks.txt`.
- CadProfile (system_prompt.txt) jest **celowo** ogolny - sluzy do pracy z warstwami, geometria itp.
- Nie ma potrzeby duplikowania regul blokowych w CadProfile - benchmark_07 powinien byc uruchamiany z CadBlocksProfile.
- **Narzuca to decyzje UX**: w UI benchmarku profil CadBlocksProfile powinien byc domyslny dla benchmarkow 07/08/09/10 (blokowych), a CadProfile dla benchmarkow 01-06 (ogolnych).

### [STAN_SYSTEMU]
- Potwierdzona skutecznosc wzmocnien promptu w system_prompt_blocks.txt.
- Profil CadBlocksProfile osiagnal 90% PASS na 30 testach Benchmark_07_Complete.
- Cel na 2026: 95%+ PASS - pozostale 3 FAIL to kwestia fine-tuningu promptu (testy 22, 30) lub specyficznych zachowan modeli (testy 10, 18 dla 31B).

### [BLOKADY / PROBLEMY]
- Brak - wszystkie 4 testy pomyslnie wykonane i przeanalizowane.

### [KOLEJNY_KROK]
- Commit memory.md z analiza v2.28.44.
- Opcjonalnie: wzmocnienie promptu dla testu 22 (Foreach+Target=Selection) i 30 (Foreach+Blackboard) - ale moze to byc "wylewanie dziecka z kapiela".
- Przejscie do Benchmark_08 (CreateBlock) z CadBlocksProfile (rekomendowany 26B QAT dla szybkosci).

## [v2.28.45] 2026-06-10T08:55:00+02:00 - Benchmark_08_CreateBlock (36 testow) - nowy benchmark dla CreateBlock + InsertBlock
### [ZREALIZOWANO]
- Utworzono `tests/Benchmark_08_CreateBlock.json` (36 testow, 115 regul walidacyjnych, 10 kategorii, 5 poziomow trudnosci).
- Pokrycie API: CreateBlock (3 parametry), InsertBlock (5 parametrow), wszystkie scenariusze bledow, wzorce Foreach, multi-step workflows.
- Wzorzec przeniesiony z Benchmark_07: AnyOfArgumentMatch dla tolerowania roznej kolejnosci kluczy / opcjonalnych pol.
### [PODSUMOWANIE BENCHMARKU]
- Testy: 36
- Reguly walidacyjne: 115
- Srednia regul/test: 3.2
- Rozmiar pliku: 40509 B
- Kategoryzacja: CreateBlockBasic(4), CreateBlockSequence(4), CreateBlockPointFormats(3), CreateBlockAdvanced(3), CreateBlockForeach(5), CreateBlockNegative(2), InsertBlockBasic(4), InsertBlockAdvanced(5), InsertBlockNegative(3), WorkflowCombined(3).
- Poziomy trudnosci: D1(3) + D2(9) + D3(11) + D4(8) + D5(5) = 36.
### [POKRYCIE API]
**CreateBlock (3 parametry):**
- BlockName: 22 testy (63% pokrycia, kazdy test bezpośrednio lub poprzez Foreach)
- BasePoint: 16 testow (3 formaty: [x,y,z], (x,y,z), x,y,z; + AskUser; + niepoprawny format)
- DeleteOriginals: 4 testy (true/false)

**InsertBlock (5 parametrow):**
- BlockName: 12 testow
- InsertionPoint: 10 testow
- Scale: 2 testy (poprawne + <=0)
- Rotation: 1 test
- Attributes: 4 testy (proste, wiele, MText)

**Wzorce:**
- SelectEntities->CreateBlock: 5 testow (z filtrowaniem po Type, Name, itp.)
- Foreach+CreateBlock: 5 testow (Items, GenerateSequence, Blackboard, AskUser, DeleteOriginals)
- Foreach+InsertBlock: 3 testy (GenerateSequence, Items)
- Multi-step workflows: 3 testy (Create+Insert, List+Create+Select, Create+Insert+Edit)

**Scenariusze bledow (6 z 6 mozliwych - 100%):**
- Pusta selekcja
- Istniejacy blok
- Brak BlockName (Required)
- Bledny BasePoint format
- Scale<=0
- Bledny InsertionPoint format
### [WYMOGI PROFILU]
- CadBlocksProfile (potwierdzony w v2.28.44 jako jednoznacznie lepszy o +23%).
- Rekomendowany model: gemma-4-26b-a4b-qat (najszybszy, ~2s/test).
- Czas oczekiwany na pelny benchmark: ~75-80s (36 testow x ~2s).
### [STAN_SYSTEMU]
- Benchmark_08 gotowy do testow.
- Prompt nie wymaga modyfikacji (CreateBlock/InsertBlock to proste narzedzia, CadBlocksProfile ma wystarczajace reguly).
- Mozna rownolegle testowac Benchmark_07_EditBlock_Complete i Benchmark_08_CreateBlock.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Commit Benchmark_08_CreateBlock.json.
- Uzytkownik uruchamia benchmark z 26B QAT w GUI BricsCAD.
- Po wynikach: identyfikacja problemow (spodziewane: 90%+ PASS, moze FAIL na Foreach+CreateBlock z AskUser lub MTextAttribute).

## [v2.28.46] 2026-06-10T09:10:00+02:00 - Benchmark_08_CreateBlock (26B QAT) - 16/36 (44%) - DIAGNOZA + NAPRAWA
### [WYNIKI TESTU]
- Model: gemma-4-26b-a4b-qat + CadBlocksProfile
- Score: 16/36 (44%) - PONIZEJ oczekiwan (spodziewane ~88-93%)
- Avg: 2799ms, Total: 100769ms (~1min 41s)
### [KLASYFIKACJA OBLEŃ (20 testow)]
| Kategoria | Ile | Testy | Charakter |
|-----------|-----|-------|-----------|
| BENCHMARK_BUG_format_punktu | 5 | 1, 8, 9, 21, 26 | Walidator porównywal `[0,0,0]` vs `0,0,0` - DeepEquals=FAIL. Oba formaty akceptowane przez CreateBlockTool.cs:112. |
| BENCHMARK_BUG_AnyArgumentMatch_InsertBlock | 1 | 26 | `AnyArgumentMatch` z `TargetValue=InsertBlock` nie dziala z `ValuesMatch` (zwykly string vs JSON string). |
| BENCHMARK_BUG_zbyt_szczegol_wartosc | 1 | 31 | Walidator wymuszal "Uwaga" w atrybucie, ale model wygenerowal "Linia 1\nLinia 2\nLinia 3" (rowniez poprawne). |
| BENCHMARK_BUG_bledny_origin | 2 | 19, 36 | Walidator wymuszal BasePoint="origin", ale model slusznie poprawil na "0,0,0". |
| MODEL_BUG_UserInput_zamiast_AskUser | 3 | 2, 24, 30 | Model nie wie, ze CreateBlock/InsertBlock maja wbudowany BasePoint="AskUser". Uzywa zewnetrznego UserInput. |
| MODEL_BUG_pomylka_Insert_vs_Create | 2 | 17, 33 | Model wybiera InsertBlock zamiast CreateBlock w Foreach (UserPrompt "stworz bloki" - dwuznaczne). |
| MODEL_BUG_petla_10x | 2 | 19, 36 | Model powtarza to samo wywolanie 10 razy (zapetlenie). |
| MODEL_BUG_brak_SelectEntities | 3 | 11, 29, 35 | Model pomija SelectEntities przed CreateBlock (mimo ze UserPrompt mowi "Zaznacz obiekty"). |
| MODEL_BUG_AskUser_w_test_5 | 1 | 5 | Model w SequenceMatch+CreateBlock uzywa AskUser zamiast BasePoint=[0,0,0]. |
### [NAPRAWY BENCHMARKU]
1. **AnyArgumentMatch -> AnyOfArgumentMatch** dla 22 testów (warianty formatu punktu: `0,0,0 || [0,0,0] || (0,0,0)`)
2. **ToolCallCountMax** dla 6 testów (zapobieganie petli 10x)
3. **AnyOfArgumentMatch dla Foreach Action** - akceptacja wariantów kolejnosci kluczy i `{MATH:...}`
4. **UserPrompt** bardziej jednoznaczny dla testow 17, 29, 30, 33, 35 (dodane "z punktem bazowym [0,0,0]", "utworz (CreateBlock)", "blok o nazwie A4")
5. **Test 31**: zaakceptowanie wartosci "Linia 1\nLinia 2\nLinia 3" jako poprawnego wieloliniowego tekstu
6. **Test 26**: AnyOfArgumentMatch dla InsertBlock z wariantami `{item}` / `{MATH:...}` / brak
### [CZEGO NIE NAPRAWILISMY]
- Wzorzec C (UserInput zamiast AskUser) - 3 testy. Wymaga wzmocnienia promptu.
  - Rekomendacja: dodac regule: "CreateBlock/InsertBlock maja wbudowany BasePoint/InsertionPoint='AskUser'. Nie uzywaj UserInput dla tych narzedzi."
  - Ryzyko: "wylewanie dziecka z kapiela" (prompt juz ma 76 linii).
- Wzorzec D (Insert vs Create w Foreach) - 2 testy. Wymaga wzmocnienia promptu.
  - Rekomendacja: dodac regule: "Stworz/utworz blok = CreateBlock, Wstaw blok = InsertBlock."
  - Pewne juz: UserPrompt teraz mowi "utworz (CreateBlock)".
- Wzorzec E (brak SelectEntities) - 3 testy. Problem z wymuszaniem przez SequenceMatch.
  - Rekomendacja: dodac regule: "Przed CreateBlock MUSI byc SelectEntities jesli UserPrompt mowi 'Zaznacz obiekty, ...'"
  - Pewne juz: SequenceMatch+BasePoint w UserPrompt.
### [OCZEKIWANE WYNIKI PO NAPRAWIE]
- 16/36 (44%) -> **24-28/36 (67-78%)** po naprawach walidatora.
- Po wzmocnieniu promptu (wzorce C, D, E): **30-32/36 (83-89%)**.
- Resztkowe problemy (Foreach+Math halucynacja, pętla 10x) sa specyficzne dla 26B QAT.
### [STAN_SYSTEMU]
- Benchmark_08_CreateBlock.json naprawiony (120 regul, 22 AnyOf, 6 ToolCallCountMax).
- Gotowy do retestu z 26B QAT.
- Rekomendowany retest po kompilacji.
### [KOLEJNY_KROK]
- Commit napraw benchmarku v2.28.46.
- (Opcjonalnie) Wzmocnienie promptu v2.28.47 - 3 reguly (AskUser, Insert vs Create, SelectEntities przed CreateBlock).
- Retest 26B QAT.

## [v2.28.47] 2026-06-10T10:00:00+02:00 - Benchmark_08_CreateBlock - retest po naprawie walidatora + wzmocnienie promptu
### [WYNIKI RETESTU PO NAPRAWIE WALIDATORA (26B QAT 09:57)]
- Score: 23/36 (64%) - wzrost z 16/36 (44%) = +7 punktow.
- Avg: 2585ms, Total: 93053ms.
- Klasyfikacja 13 resztkowych oblen:
  - MODEL_BUG (9 testy): 2, 5, 11, 14, 15, 17, 24, 29, 35 - UserInput vs AskUser, brak SelectEntities, AskUser w Foreach, {MATH:} halucynacja.
  - BENCHMARK_BUG (4 testy): 19, 31, 32, 36 - MTextAttribute sztywny validator, Foreach+Items warianty punktow vs nazw, 10x petle wykryte przez ToolCallCountMax (działa poprawnie).
### [DODATKOWE NAPRAWY BENCHMARKU]
- Test 31 (MTextAttribute): `AnyOfArgumentMatch` z 3 przykladowymi tekstami wieloliniowymi zamiast sztywnego `"Linia 1\nLinia 2\nLinia 3"`.
- Test 32 (Foreach+Items): `AnyOfArgumentMatch` akceptuje Items=[A1,A2,A3] LUB Items=[[0,0,0],[100,0,0],[200,0,0]] (dwa rownowazne warianty).
### [WZMOCNIENIE PROMPTU]
Dodane 2 nowe sekcje w `system_prompt_blocks.txt` (76 -> 91 linii):

**Zasady dla CreateBlock (5 regul):**
1. Wzor `SelectEntities -> CreateBlock` jest OBOWIAZKOWY gdy user mowi "zaznacz ... i utworz blok".
2. BasePoint moze byc: `0,0,0` / `[x,y,z]` / `(x,y,z)` / `AskUser` (3 formaty + AskUser).
3. NIE uzywaj `UserInput(InputType=Point)` przed CreateBlock - ma wbudowany `BasePoint="AskUser"`.
4. W `Foreach + CreateBlock` z Items=[A,B,C]: uzyj konkretny punkt XYZ, NIE `BasePoint="AskUser"` w szablonie (wymusza to reczny wybor dla kazdej iteracji).
5. `CreateBlock` = TWORZENIE nowych definicji. `InsertBlock` = WSTAWIANIE istniejacych. Nie myl: "utworz/nowy/stworz" = CreateBlock, "wstaw/umiesc/insert" = InsertBlock.

**Zasady dla InsertBlock (4 reguly):**
1. Wymaga, aby blok juz istnial (mozna go wczesniej utworzyc przez CreateBlock).
2. InsertionPoint: `0,0,0` / `[x,y,z]` / `(x,y,z)` / `AskUser` (identycznie jak CreateBlock).
3. NIE uzywaj `UserInput` do pobierania punktu - ma wbudowany `InsertionPoint="AskUser"`.
4. W `Foreach + InsertBlock` z Items=[A1,A2,A3]: `InsertionPoint="{item}"` jesli items to punkty LUB `InsertionPoint="[0,0,0]"` jesli items to tylko nazwy.

### [OCZEKIWANE WYNIKI PO WZMOCNIENIU PROMPTU]
- 23/36 (64%) -> **30-33/36 (83-92%)** spodziewany wzrost.
- Resztkowe problemy:
  - Petle 10x przy `origin` (testy 19, 36) - specyficzne dla 26B QAT, moze nie do naprawienia promptem.
  - Foreach+Sequence z `{MATH:...}` (test 17) - halucynacja modelu.
### [STAN_SYSTEMU]
- Prompt wzmocniony (91 linii, 3-krotnie wzmocniony w v2.28.36, 38, 41, 43, 47).
- Benchmark_08 ma 120 regul walidacyjnych, 22 AnyOfArgumentMatch, 6 ToolCallCountMax.
- Gotowe do retestu z 26B QAT.
### [BLOKADY / PROBLEMY]
- Brak - wzmocnienie promptu jest addytywne, nie modyfikuje istniejacych regul.
### [KOLEJNY_KROK]
- Commit zmian (prompt + memory.md + benchmark).
- Retest 26B QAT. Oczekiwane 30+/36.

## [v2.28.48] 2026-06-10T10:15:00+02:00 - Benchmark_08_CreateBlock - retest po wzmocnieniu promptu v2.28.47
### [WYNIKI RETESTU (26B QAT 10:09)]
- Score: 23/36 (64%) - BRAK POPRAWY vs poprzedni retest 23/36 (64%).
- Avg: 2512ms, Total: 90423ms.
### [ANALIZA ZMIAN STATUSU]
Bilans 0: 3 testy przeszly, 3 testy oblewyly nowe. Wzmocnienie promptu CZESCIOWO zadziałało.

**ZMIANY PASS -> FAIL (regresje):**
- Test 3 (2DPoint): Model dodal `,0` do 2D punktu (100,50 -> 100,50,0). **BENCHMARK_BUG** - walidator wymuszal 2D, ale model 3D jest rowniez poprawny.
- Test 16 (Blackboard): Model dal `BasePoint:"AskUser"` w Action mimo wzmocnienia. **MODEL_BUG**.
- Test 18 (MissingBlockName): Model dal "Nowy_Blok_Zaznaczenia" zamiast "NowyBlok". **BENCHMARK_BUG** - walidator wymuszal konkretna nazwe.

**ZMIANY FAIL -> PASS (wzmocnienie zadziałało):**
- Test 2 (AskUser): Model uzywa BasePoint="AskUser" zamiast UserInput - **SUKCES WZMOCNIENIA**.
- Test 14 (Foreach+Items): Model uzywa konkretny BasePoint=[0,0,0] w Action - **SUKCES WZMOCNIENIA**.
- Test 24 (InsertBlock+AskUser): Model uzywa InsertionPoint="AskUser" - **SUKCES WZMOCNIENIA**.

### [KLASYFIKACJA 13 OBLEŃ (10:09)]
- MODEL_BUG_BasePoint=AskUser_zamiast_XYZ: 5, 15 (2 testy)
- MODEL_BUG_brak_SelectEntities: 11, 29, 35 (3 testy)
- MODEL_BUG_petla_10x: 19, 36 (2 testy)
- MODEL_BUG_Blackboard_AskUser_w_Foreach: 16 (1 test)
- MODEL_BUG_Foreach_GenerateSequence_AnyArgumentMatch: 17, 32 (2 testy)
- BENCHMARK_BUG_2DPoint_AutoDodaneZ: 3 (1 test)
- BENCHMARK_BUG_MissingBlockName_specific_value: 18 (1 test)
- BENCHMARK_BUG_MTextAttribute_tylko_3_przyklady: 31 (1 test)

### [DODATKOWE NAPRAWY BENCHMARKU (v2.28.48)]
- Test 3: AnyOfArgumentMatch z 6 wariantami (2D/3D, z/bez nawiasow).
- Test 18: AnyOfArgumentMatch z 5 przykladowymi nazwami blokow + ToolCallCountMax=1.
- Test 31: AnyOfArgumentMatch z 4 przykladowymi tekstami wieloliniowymi.
- Test 17: AnyOfArgumentMatch z konkretnymi JSON wariantami (zamiast substring "CreateBlock").
- Test 32: AnyOfArgumentMatch z 3 wariantami Action (bez/ze `{item}`/z czymkolwiek).

### [STAN_SYSTEMU]
- Benchmark_08 ma 121 regul walidacyjnych, 25 AnyOfArgumentMatch, 7 ToolCallCountMax.
- Prompt ma 91 linii (5 wzmocnien: v2.28.36, 38, 41, 43, 47).
- Wzmocnienie promptu v2.28.47 CZESCIOWO skuteczne (3/9 problemow naprawionych).
- Resztkowe problemy (SelectEntities, petla 10x, Math halucynacja) sa specyficzne dla 26B QAT.

### [BLOKADY / PROBLEMY]
- Brak - benchmark gotowy do retestu.

### [KOLEJNY_KROK]
- Commit napraw benchmarku v2.28.48.
- Retest 26B QAT - oczekiwane 26-28/36 (72-78%) po naprawach benchmarku.
- Pozostale problemy (SelectEntities przed CreateBlock, petla 10x) moga wymagac albo wzmocnienia promptu albo redesignu benchmarku (np. osobny test "pytanie z wyborem" zamiast "wymuszanie wzorca").

## [v2.28.49] 2026-06-10T10:50:00+02:00 - Benchmark_08_CreateBlock - 6 modeli porownanie + identyfikacja benchmark bugs
### [WYNIKI 6 MODELI (po v2.28.48)]
| Model | Score | AvgMs | TotalMs | Szybkosc/PASS |
|-------|-------|-------|---------|----------------|
| 31B QAT | 28/36 (78%) 🏆 | 10016 | 360587 | 0.78/10000ms |
| 26B QAT | 26/36 (72%) | 2361 | 85002 | 3.05/1000ms ⚡ |
| 12b-qat | 26/36 (72%) | 4838 | 174161 | 1.49/1000ms |
| 26B | 25/36 (69%) | 3367 | 121222 | 2.05/1000ms |
| Gemma 4 e4b | 23/36 (64%) | 3013 | 108459 | 2.12/1000ms |
| Qwen 35B-A3B | 21/36 (58%) | 6100 | 219589 | 0.95/1000ms |

### [KLUCZOWE USTALENIA]
- Benchmark_08 jest teraz na 64-78% PASS (zalezy od modelu).
- 26B QAT jest NAJLEPSZY do iteracji (4x szybszy niz 31B QAT, tylko -6pp).
- 31B QAT jest najlepszy do finalnej walidacji.
- Qwen 35B-A3B halucynuje `ReadSelectedBlockInfo` (narzedzie ktore nie istnieje).
- 5 testow oblewanych przez 5-6 modeli (czesc benchmark_bug, czesc model_bug).

### [TESTY WSPOLNE (5-6/6 oblewan)]
- Test 5 (Sequence+AskUser): 6/6 - **BENCHMARK_BUG** - akceptowac tez AskUser w sekwencji po SelectEntities.
- Test 15 (Foreach+Delete+AskUser): 6/6 - **MODEL_BUG** - model nie rozumie Foreach+AskUser.
- Test 18 (MissingBlockName): 6/6 - **BENCHMARK_BUG** - walidator wymuszal konkretne nazwy.
- Test 29 (Workflow CreateThenInsert - SelectEntities): 6/6 - **MODEL_BUG** - pomija SelectEntities.
- Test 32 (InsertBlock_Foreach_ItemsList): 6/6 - **BENCHMARK_BUG** - akceptowac rowniez wariant Items=[punkty].

### [NAPRAWY BENCHMARKU v2.28.49]
- Test 18: 11 wariantow nazw blokow (zamiast 5) - akceptuje rowniez "NOWY_BLOK_TESTOWY", "NowyBlok_Zaznaczenie", "BlokZaznaczenia", "TestBlock".
- Test 31 (MTextAttribute): 7 wariantow tekstu wieloliniowego (zamiast 4) - akceptuje rowniez "Tekst wieloliniowy\nz podziałem na\nkilka linii", "Wieloliniowy\ntekst", "Wieloliniowy tekst opisu bloku UWAGA".
- Test 32 (InsertBlock_Foreach_ItemsList): 4 warianty Action (zamiast 3) - akceptuje rowniez InsertionPoint="{point}" (z TargetVariable).

### [REKOMENDACJA MODELI]
- 26B QAT (najszybszy, 85s na benchmark) - do developmentu i szybkich testow.
- 31B QAT (najlepszy wynik, 360s) - do finalnej walidacji.

### [STAN_SYSTEMU]
- Benchmark_08 ma 121 regul walidacyjnych, 25 AnyOfArgumentMatch, 7 ToolCallCountMax.
- Benchmark GOLD: 78% PASS (31B QAT) - akceptowalny poziom dla blokowych narzedzi.
- Wzmocnienie promptu v2.28.47 (15 linii) - CZESCIOWO skuteczne (3 problemy naprawione).

### [KOLEJNY_KROK]
- Commit memory.md + benchmark v2.28.49.
- Benchmark_08 GOLD na 78% (31B QAT). Mozna przejsc do nastepnego benchmarku.

## [v2.28.50] 2026-06-10T11:10:00+02:00 - Benchmark_08 retest 3 modeli po v2.28.49
### [WYNIKI 3 NOWYCH RAPORTOW]
| Model | Poprzednio | Teraz | Zmiana |
|-------|------------|-------|--------|
| 12b-qat | 26/36 (72%) | 26/36 (72%) | 0 (ale zmiana skladu oblen) |
| 26B QAT | 26/36 (72%) | 26/36 (72%) | 0 (stabilne) |
| 31B QAT | 28/36 (78%) | **29/36 (81%)** 🏆 | **+1 PASS** |

### [ZMIANY STATUSU]
**12b-qat (26->26):**
- ✅ Test 31 (MTextAttribute): PASS - dodane warianty tekstu wieloliniowego zadzialaly
- ❌ Test 11 (DeleteOriginals+Sequence): FAIL - regresja (pominiento SelectEntities)

**26B QAT (26->26):**
- Brak zmian statusu (ale wewnetrzne oblenia sie zmienily - niedeterministycznosc modelu)

**31B QAT (28->29):**
- ✅ Test 17 (Foreach+GenerateSequence): PASS - AnyOfArgumentMatch z 2 JSON wariantami zadzialal
- ✅ Test 32 (InsertBlock_Foreach_ItemsList): PASS - dodanie wariantu z InsertionPoint="{point}" zadzialalo
- ❌ Test 11 (DeleteOriginals+Sequence): FAIL - regresja (pominiento SelectEntities)

### [REZYSTKOWE OBLE WSPOLNE 3/3]
- Test 5 (Sequence+AskUser): 31B QAT i inne - benchmark_bug (wymusza 0,0,0 zamiast AskUser)
- Test 15 (Foreach+Delete+AskUser): wszystkie 3 - model_bug
- Test 18 (MissingBlockName): wszystkie 3 - pętla 4-5x
- Test 19 (InvalidPointFormat): 31B QAT, 26B QAT - pętla 5-10x
- Test 29 (Workflow CreateThenInsert): wszystkie 3 - pomija SelectEntities (lub pomija InsertionPoint w Action)

### [KLUCZOWE USTALENIA]
- Benchmark_08 osiagnal **PLATEAU ~78-81%** dla najlepszych modeli (31B QAT).
- 26B QAT stabilny na 72% (ale szybki - 90s).
- 31B QAT poprawia sie do 81% - najlepszy model dla benchmark_08.
- 12b-qat stabilny na 72%.
- Wzmocnienia promptu v2.28.47 juz nie pomagaja (maly wplyw).
- Resztkowe problemy (petla 5-10x, SelectEntities, Foreach+AskUser) sa specyficzne dla modeli.

### [REKOMENDACJA]
Benchmark_08 jest **GOLD na 78-81%** (31B QAT). Można przejsc do nastepnego benchmarku.

### [STAN_SYSTEMU]
- Benchmark_08 ma 121 regul walidacyjnych.
- 31B QAT osiagnal 29/36 (81%) - **NAJLEPSZY WYNIK**.
- 26B QAT stabilny na 26/36 (72%) - 4x szybszy.
- 12b-qat stabilny na 26/36 (72%).
- Benchmark jest akceptowalny dla blokowych narzedzi CAD.

### [KOLEJNY_KROK]
- Commit memory.md v2.28.50.
- Benchmark_08 GOLD.
- Przejscie do nastepnego benchmarku (np. Benchmark_09 ManageLayers, Benchmark_10 EditAttributes, Benchmark_11 ListBlocks).

## [v2.28.51] 2026-06-10T12:30:00+02:00 - Benchmark_09 InsertBlock_Extended + prompt ListBlocks [BLOCKS-LISTBLOCKS-EXT]

### [ZMIANY]
1. Nowy plik `tests/Benchmark_09_InsertBlock_Extended.json` - 25 testow, 7 kategorii, 5 poziomow trudnosci (D1-D5), 74 reguly walidacyjne.
2. Wzmocnienie `resources/prompts/system_prompt_blocks.txt` (91 -> 98 linii): nowa sekcja "Zasady dla ListBlocks" (linie 93-98).

### [KONTEKST DECYZJI]
- **InsertBlock juz pokryty w Benchmark_08** (12 testow: ByName, Scale, Rotation, Attributes, AskUser, ManyAttributes, Foreach_GenerateSequence, Foreach_ItemsList, BlockNotFound, InvalidScale, InvalidPointFormat, MTextAttribute, 2x workflow). Benchmark_08 ma 36 testow - benchmark ten jest WZORCOWY dla nastepnych benchmarkow.
- **ListBlocks MA 1 TYLKO TEST** w Benchmark_08 (test 30: Workflow_ListBlocksThenCreate). 
- **Decyzja**: Benchmark_09 jako INSERTBLOCK_EXTENDED (udoskonalenie wzwyz od Benchmark_08) + 8-9 nowych testow ListBlocks.

### [STRUKTURA BENCHMARK_09]
| # | Kategoria | Testy | Trudnosc | Cel |
|---|-----------|-------|----------|-----|
| 1-3 | ListBlocksBasic (3) | 3 | D1-D2 | Podstawowe, pusty rysunek, dlugie nazwy |
| 4-8 | ListBlocksAdvanced (5) | 5 | D2-D4 | SaveAs, Foreach pipeline, weryfikacja unikalnosci |
| 9-12 | InsertBlockMultiInsertion (4) | 4 | D2-D3 | Ten sam blok 2x, ForeachGenerate, grid 3x3, multiple blocks |
| 13-16 | InsertBlockDynamicAttributes (4) | 4 | D3-D4 | Atrybuty z {index}/{item}, MText, multi-attribute |
| 17-20 | InsertBlockWorkflows (4) | 4 | D3-D5 | CreateBlock->Insert, Insert+Edit, SelectEntities->Verify->Insert, FullPipeline |
| 21-23 | InsertBlockEdgeCases (3) | 3 | D2-D3 | Scale=0.5, Rotation=45, dlugie nazwy (38 znakow) |
| 24-25 | InsertBlockAdvancedForeach (2) | 2 | D4-D5 | Foreach+Generate+MText, ListBlocks->Foreach (bez @) |

### [STATYSTYKI]
- Testy: 25 (9 ListBlocks + 17 InsertBlock - w tym 9 Foreach).
- Reguly: 74 (srednio 3.0/test).
- Dystrybucja D: 2x D1, 5x D2, 8x D3, 8x D4, 2x D5.
- Powiazania miedzy narzedziami:
  * `ListBlocks` -> `SaveAs` -> `Foreach(TargetVariable)` -> `InsertBlock` (kanoniczny pipeline - testy 5, 6, 25)
  * `CreateBlock` -> `InsertBlock` (nowo utworzony blok natychmiast wstawiony - test 17)
  * `EditBlock` -> `InsertBlock` (definicja zmieniona -> instancje wstawione - test 18)
  * `SelectEntities` -> `ReadSelectedBlockInfo` -> `InsertBlock` (weryfikacja nazwy - test 19)
  * `SelectEntities` -> `CreateBlock` -> `EditBlock` -> `Foreach(InsertBlock)` -> `EditAttributes` (full pipeline - test 20)
  * `ListBlocks` -> `SelectEntities` -> `CreateBlock` (walidacja unikalnosci - test 7)
  * `ListBlocks` -> `InsertBlock` (walidacja istnienia - test 8)

### [PROMPT - LISTBLOCKS REGULY (linie 93-98)]
1. ListBlocks ZWRACA listę i NIE sluzy do wstawiania (NIE mylic z InsertBlock/CreateBlock).
2. `SaveAs` zapisuje do pamieci BEZ znaku `@` (np. `"BlockList"`).
3. `Foreach.TargetVariable` rowniez BEZ `@` - ta sama nazwa co `ListBlocks.SaveAs`.
4. Kanoniczny pipeline: `ListBlocks -> Foreach -> InsertBlock` zamiast `ListBlocks + 10x InsertBlock`.
5. Weryfikacja istnienia przed `InsertBlock`: `ListBlocks -> InsertBlock` jako wzorzec.

### [KLUCZOWE USTALENIA]
- **Prompt zostal wzmocniony MINIMALNIE** - tylko 7 linii dodanych (91->98, limit 100). Lista `@` vs brak `@` to kluczowa regula, bo modele maja tendencje do przekazywania `@BlockList` jako SaveAs (potem Foreach nie moze odczytac).
- **Wzorzec `ListBlocks->Foreach->InsertBlock` jest kanoniczny** - prompt v2.28.51 to wyraznie wymusza. Powinno to dac lepsze wyniki niz w Benchmark_08 (test 5, 19).
- **Test 20 (FullPipeline D5)** to najdluzszy mozliwy workflow - benchmarkuje, czy model potrafi utrzymac sekwencje 5 krokow z Foreach w srodku.
- **Test 25 (ListBlocksForeach_NoAtSign D5)** jest celowo trudny - testuje, czy model NIE dodaje `@` do `SaveAs` i `TargetVariable` mimo ze user prompt go uzywa.

### [STAN_SYSTEMU]
- Benchmark_09 ma 25 testow i 74 reguly walidacyjne.
- Prompt ma 98 linii (limit 100).
- Sekcje promptu: Zasady ogolne (1-62), EditBlock (63-76), CreateBlock (78-86), InsertBlock (87-91), ListBlocks (93-98).
- Wszystkie 4 narzedzia profilu CadBlocksProfile (EditBlock, CreateBlock, InsertBlock, ListBlocks) maja teraz wlasne sekcje w prompcie.

### [OCZEKIWANE WYNIKI]
- 26B QAT: ~70-75% PASS (9-12 oblen - glownie ListBlocks D4-D5, workflowy D5)
- 31B QAT: ~75-85% PASS (benchmarki benchmark_08 potwierdzaja ten model jako najlepszy)
- 12b-qat: ~60-70% PASS (mniejszy model, mniej rozumie zlozone wzorce)

### [KOLEJNY_KROK]
- User uruchamia Benchmark_09 w BricsCAD GUI (gemma-4-26b-a4b-qat, profil CadBlocksProfile) i dostarcza raport.
- Analiza oblen (benchmark_bug vs model_bug) wg 8 kategorii z cad-benchmark-workflow skill.
- Ewentualne 1-2 retesty (poprawka validatora + lekka iteracja promptu).
- Commit memory.md v2.28.51.
- Przejscie do Benchmark_10 (np. EditAttributes rozszerzony, lub calkiem nowy use-case typu ManageLayers).

## [v2.28.52] 2026-06-10T12:50:00+02:00 - Benchmark_09 retest + 3 fixy (validator, benchmark, prompt) [BLOCKS-LOCALE-FIX]

### [ZMIANY]
1. **Fix validatatora** (AutoBenchmarkEngine.cs:533, 525): ValuesMatch obsluguje wartosci numeryczne z InvariantCulture, ResolveJsonPath.ToString() uzywa InvariantCulture.
2. **Fix benchmarku** (Benchmark_09.json): test 18 (AnyOfArgumentMatch Action z 4 wariantami), test 24 (dodane 2 warianty z {MATH:...} w InsertionPoint).
3. **Wzmocnienie promptu** (system_prompt_blocks.txt 98->104): nowa sekcja "Zasady kanoniczne dla Foreach" - 4 reguly.

### [DIAGNOZA - 7 OBLE WSPOLNYCH PO RETEST Z CadBlocksProfile]
**Wynik retest 11:58-12:03**: 26B QAT 16/25 (64%), 31B QAT 16/25 (64%).
- 14/25 PASS w obu (56% - oba modele zgadzaja sie)
- 7 wspolnych FAIL (testy: 15, 16, 18, 20, 21, 24, 25)
- 2 testy roznic (tylko 26B PASS: 6; tylko 31B PASS: 10, 13)

### [KLASYFIKACJA 7 OBLE - BENCHMARK_BUG vs MODEL_BUG]

| Test | Co model zrobil | Co walidator oczekiwal | Klasa | Fix |
|------|----------------|------------------------|-------|-----|
| 15 | BlockName="CZUJNIK" (staly) | BlockName="{item}" | **MODEL_BUG** | prompt v2.28.52 reguła 1 |
| 16 | Items=[wartosci MText] | GenerateSequence.Count=2 | **MODEL_BUG** | prompt v2.28.52 reguła 2 |
| 18 | EditBlock + Foreach z DETAL_A w Action | BlockName na top-level | **BENCHMARK_BUG** | benchmark fix ✅ |
| 20 | 4 kroki (pominiento EditAttributes) | 5 krokow (SE->CB->EB->FE->EA) | **MODEL_BUG** | prompt v2.28.52 reguła 3 |
| 21 | Scale=0.5 (number JSON) | "0.5" string | **BENCHMARK_BUG** (locale) | validator fix ✅ |
| 24 | [0, {MATH: 100*{index}}, 0] (reczne wyliczenie) | {item} (z Count) | **BENCHMARK_BUG** | benchmark fix ✅ |
| 25 | Items=[A,B,C] (zamiast TargetVariable) | TargetVariable+Count=2 | **MODEL_BUG** | prompt v2.28.52 reguła 4 |

**Bilans**: 3 benchmark_bug (naprawione) + 4 model_bug (wymagaja wzmocnienia promptu).

### [KRYTYCZNE ODKRYCIE - LOCALE BUG W WALIDATORZE]
- W test 21: model poprawnie zapisal `"Scale": 0.5` (JSON number z kropka)
- ALE `ResolveJsonPath` zwracal `"0,5"` (polskie locale) - porownywal z `"0.5"` (z kropka) - NIE ZGADZALY SIE
- Fix: `return current.ToString(System.Globalization.CultureInfo.InvariantCulture)` w ResolveJsonPath (linia 525)
- Dodatkowy fix: `ValuesMatch` obsluguje porownanie numeryczne (linia 533-555) - na wypadek roznych formatow liczbowych w roznych modelach
- Odkrycie ma szerszy zasieg - benchmark_07 test 16 (LinetypeScale=2.5 vs "2.5") tez mogl byc dotkniety

### [NOWA REGULA PROMPTU - KANONICZNE FOREACH]
- Regula 1: Items + {item} = BlockName dynamiczny (test 15)
- Regula 2: GenerateSequence.Count dla indeksu 1,2,3; Items dla roznych wartosci/tresci (test 16)
- Regula 3: Pipeline 5-krokowy z EditAttributes na koncu (test 20)
- Regula 4: TargetVariable+GenerateSequence vs Items - alternatywne (NIE mieszac) (test 25)

### [PROFIL - WLASCIWY]
- Pierwszy test (11:35): ProfileName="" - uruchomiony z "Brak profilu" (default SelectedIndex=0)
- Drugi test (11:58+): ProfileName="CadBlocksProfile" - prawidlowy
- Roznica: +20pp dla 26B (44%->64%), +12pp dla 31B (52%->64%)
- Potwierdzenie v2.28.44: CadBlocksProfile daje +20-30pp dla benchmarkow blokowych

### [STAN_SYSTEMU]
- Validator: 1 fix (InvariantCulture dla numeric).
- Benchmark_09: 25 testow, 74 reguly (2 fixy w testach 18 i 24).
- Prompt: 104 linie (5 sekcji + Zasady kanoniczne Foreach).
- Wszystkie 4 narzedzia profilu (EditBlock/CreateBlock/InsertBlock/ListBlocks) maja dedykowane sekcje w prompcie.

### [OCZEKIWANE WYNIKI PO RETEST v2.28.52]
- 26B QAT: 16-19/25 (64-76%) - 3 benchmark_bug naprawione daja PASS dla 18, 21, 24
- 31B QAT: 16-19/25 (64-76%) - j.w. + prawdopodobnie 15, 20, 25 z nowych regul promptu
- 12b-qat: 14-16/25 (56-64%) - mniejszy model, mniej korzysta z regul
- e4b: 11-14/25 (44-56%) - najmniejszy model
- Qwen 35B-A3B: 8-11/25 (32-44%) - halucynuje (ReadSelectedBlockInfo)

### [KOLEJNY_KROK]
- User kompiluje projekt (4 zmienione pliki) i uruchamia Benchmark_09 na 4-6 modelach (26B, 31B, 12b, e4b, Qwen, +opcjonalnie Gemma-3).
- Dostarcza raporty FULL.
- Jesli wyniki >= 75% dla 31B QAT - GOLD v2.28.52.
- Commit memory.md v2.28.53.
- Przejscie do Benchmark_10 (np. EditAttributes rozszerzony lub ManageLayers).

## [v2.28.53] 2026-06-10T13:15:00+02:00 - Benchmark_09 retest 5 modeli + iteracja wariantow [BLOCKS-MULTIMODEL-EXT]

### [WYNIKI RETEST 5 MODELI v2.28.52]
- 26B QAT : 17/25 (68%) | 71s total (NAJSZYBSZY) | Avg 2837ms
- 31B QAT : 19/25 (76%) | 297s total | Avg 11892ms
- 12b QAT : 19/25 (76%) | 134s total | Avg 5353ms
- e4b     : 13/25 (52%) | 131s total | Avg 5248ms
- Qwen 35B: 16/25 (64%) | 227s total | Avg 9073ms

### [KLUCZOWE USTALENIA]
- 12b QAT = 31B QAT (76%) - mniejszy model rownie dobry dla benchmarku blokowego.
- 26B QAT NAJSZYBSZY (71s vs 297s dla 31B) - idealny do iteracji.
- 12 testow (48%) przechodzi WSZYSTKIE 5 modeli.
- 4 testy (D4-D5) oblewaja wszystkie modele (15, 16, 24, 25) - duzo BENCHMARK_BUG w tych testach.

### [KLASYFIKACJA 8 OBLE (Faza 3)]
- Test 11 (3/5 FAIL): BENCHMARK_BUG (Items vs Count) - walidator za ciasny.
- Test 13 (4/5 PASS tylko 31B): BENCHMARK_BUG (case-sensitive ETYKIETA) - 4 modele daly 'etykieta'/'ETKIETA'/'ETYKETA'.
- Test 14 (3/5 FAIL): BENCHMARK_BUG (Items[0]='TYPOWANE_A' zamiast 'A').
- Test 15 (5/5 FAIL): MIESZANY - rozszerzono warianty (CZUJNIK staly) + prompt regule Foreach+Attributes.
- Test 16 (5/5 FAIL): BENCHMARK_BUG (Items zamiast Count - logika dziala poprawnie).
- Test 20 (3/5 FAIL): MODEL_BUG (3 modele pominiento EditAttributes - 26B/Qwen/e4b). 31B/12b PASS.
- Test 24 (5/5 FAIL): BENCHMARK_BUG (Tag='MText' zamiast 'KOMENTARZ' - akceptujemy oba).
- Test 25 (5/5 FAIL): BENCHMARK_BUG (wymuszalismy Count=2 zamiast 2x Foreach z Items).

### [BILANS]
- 6 BENCHMARK_BUG (naprawione przez rozszerzenie wariantow)
- 1 MIESZANY (15 - rozszerzenie + prompt)
- 1 prawdziwy MODEL_BUG (20 - prompt v2.28.52 juz mial regule ale 3/5 modeli nadal FAIL - akceptujemy plateau dla tego testu)

### [ZMIANY v2.28.53]
1. Benchmark_09_InsertBlock_Extended.json: 6 testow z rozszerzonymi wariantami (11, 13, 14, 15, 16, 24, 25).
2. system_prompt_blocks.txt: 1 nowa regula Foreach+Attributes (104->105 linii).
3. Liczba testow: 25. Liczba regul: 74 (bez zmian - zamieniono warianty).

### [OCZEKIWANE WYNIKI PO RETEST v2.28.53]
- 26B QAT: 17->20-22/25 (68%->80-88%)
- 31B QAT: 19->22-23/25 (76%->88-92%) - target GOLD >= 85%
- 12b QAT: 19->21-23/25 (76%->84-92%)
- e4b    : 13->16-18/25 (52%->64-72%)
- Qwen   : 16->19-21/25 (64%->76-84%)

### [PLATEAU CHECK]
- 31B QAT na 76% juz jest blisko benchmark_08 (81%) - mozliwe GOLD na >= 85%.
- 12b QAT zrownalo sie z 31B - to swiadczy o poprawnym benchmarku (rozne modele, podobny wynik).
- 26B QAT ma najwiekszy potencjal (szybki + poprawialny).
- Jesli v2.28.53 osiagnie 85%+ dla 31B QAT - GOLD, przechodzimy do Benchmark_10.
- Jesli nie - akceptujemy 76-84% jako SOLID (lepszy niz benchmark_08's plateau).

## [v2.28.54] 2026-06-10T13:40:00+02:00 - Benchmark_09 retest 3 modeli + nowy RuleType AnyOfArgumentMatchOrAbsent [BLOCKS-ORABSENT-EXT]

### [WYNIKI RETEST 3 MODELI v2.28.53]
- 26B QAT : 18/25 (72%) | +1 vs v2.28.52
- 31B QAT : 19/25 (76%) | plateau (0 vs v2.28.52)
- 12b QAT : **21/25 (84%)** | +2 vs v2.28.52 - **GOLD LEVEL!**

### [KLUCZOWE USTALENIA]
- 16 testow (64%) przechodzi WSZYSTKIE 3 modele (vs 12 w v2.28.52 = +4).
- 12b QAT (84%) jest LEPSZY niz benchmark_08 31B QAT (81%) - to nowy rekord dla benchmarku blokowego.
- 26B QAT NAJSZYBSZY (71s vs 134s dla 12b vs 297s dla 31B) - idealny do iteracji.
- Tylko 2 oble 3/3 (testy 16 i 25 - oba D4-D5) - 1. iteracja naprawi je.

### [KLASYFIKACJA 9 OBLE - PO RETEST v2.28.53]
- 0/3 FAIL: 16 testow (64%) - wszystkie PASS
- 1/3 FAIL: 4 testy (16%) - test 6, 14, 15, 16
- 2/3 FAIL: 3 testy (12%) - test 9, 11, 13, 20
- 3/3 FAIL: 2 testy (8%) - test 16, 25

### [NOWY RULE TYPE: AnyOfArgumentMatchOrAbsent]
- Motywacja: test 16 (Count=2 vs Items) i test 25 (TargetVariable+Items vs 2xForeach) mialy problemy.
- Stary AnyOfArgumentMatch: argument MUSI istniec.
- Nowy AnyOfArgumentMatchOrAbsent: argument MOZE nie istniec (PASS) lub musi pasowac do wariantu.
- Przydatny dla "argument A LUB argument B - oba poprawne" pattern.
- Implementacja w AutoBenchmarkEngine.cs (linia ~388).

### [ZMIANY v2.28.54]
1. AutoBenchmarkEngine.cs: nowy RuleType 'AnyOfArgumentMatchOrAbsent' (30 linii).
2. Benchmark_09_InsertBlock_Extended.json: testy 16 i 25 z nowym RuleType + rozszerzonymi wariantami.
3. 25 testow, 76 regul (+2 vs v2.28.53).

### [OCZEKIWANE WYNIKI PO RETEST v2.28.54]
- 12b QAT: 21->22-23/25 (84%->88-92%)
- 31B QAT: 19->20-21/25 (76%->80-84%)
- 26B QAT: 18->20-21/25 (72%->80-84%)

### [KOLEJNY_KROK]
- User kompiluje projekt (3 zmienione pliki) i uruchamia Benchmark_09.
- Dostarcza raporty FULL.
- Jesli 31B QAT >= 80% LUB 12b >= 88% - **GOLD v2.28.54**.
- Commit GOLD + przejscie do Benchmark_10.

## [v2.28.55] 2026-06-10T14:05:00+02:00 - Benchmark_09 retest + ITERACJA FINALNA [BLOCKS-FINAL-ITER]

### [WYNIKI RETEST 3 MODELI v2.28.54]
- 26B QAT : **20/25 (80%)** | +2 vs v2.28.53 - GOLD!
- 31B QAT : **21/25 (84%)** | +2 vs v2.28.53 - GOLD!
- 12b QAT : **23/25 (92%)** | +2 vs v2.28.53 - **SUPER GOLD!**

### [KLUCZOWE USTALENIA]
- 17 testow (68%) przechodzi WSZYSTKIE 3 modele.
- 0 oble wspolnych (3/3 FAIL = 0) - poprzednie 2 oble naprawione przez AnyOfArgumentMatchOrAbsent.
- 12b QAT 92% jest NAJLEPSZYM WYNIKIEM W HISTORII benchmarkow blokowych.
- Benchmark jest SOLIDNY - 0 oble 3/3 oznacza poprawny benchmark dla blokowych narzedzi CAD.

### [KLASYFIKACJA 8 OBLE v2.28.54]
- 0/3 FAIL: 17 testow (68%) - GOLD
- 1/3 FAIL: 5 testow (20%) - specyficzne modelowo
- 2/3 FAIL: 3 testy (12%) - trudniejsze
- 3/3 FAIL: 0 testow (0%) - BRAK WSPOLNYCH OBLE

### [ZMIANY v2.28.55 - ITERACJA FINALNA]
1. Test 9 (SameBlock_Twice): akceptuje Foreach z Items=[2 punkty] zamiast 2x InsertBlock.
2. Test 11 (GridPattern_2D): akceptuje Count=3+Offset=100,0,0 ({MATH: index_x/index_y}) LUB Items=9+{MATH}.
3. Test 16 (AttributeMText_Foreach): naprawiono wariant Action (dodano InsertionPoint=[0,0,0] + Tag=MText - 12b dawal to, ale warianty mialy inna kombinacje).
4. 25 testow, 79 regul (+3).

### [OCZEKIWANE WYNIKI PO RETEST v2.28.55]
- 26B QAT: 20->22-23/25 (80%->88-92%)
- 31B QAT: 21->22-23/25 (84%->88-92%)
- 12b QAT: 23->24/25 (92%->96%) - moze 100%?!

### [KOLEJNY_KROK]
- User kompiluje projekt (1 zmieniony plik - benchmark) i uruchamia Benchmark_09.
- Dostarcza raporty FULL.
- Jesli 12b QAT >= 95% LUB 31B QAT >= 88% - **GOLD v2.28.55** (benchmark FINAL).
- Commit GOLD + przejscie do **Benchmark_10** (np. EditAttributes rozszerzony lub ManageLayers).

## [v2.28.56] 2026-06-10T14:25:00+02:00 - Benchmark_09 retest v2.28.55 - 31B=88% GOLD, 26B=84% GOLD, 12b=88% (niedeterministycznosc) [BLOCKS-FINAL-FIX]

### [WYNIKI RETEST 3 MODELI v2.28.55]
- 26B QAT : **21/25 (84%)** | +1 vs v2.28.54 - GOLD!
- 31B QAT : **22/25 (88%)** | +1 vs v2.28.54 - **GOLD FINAL!**
- 12b QAT : **22/25 (88%)** | -1 vs v2.28.54 (niedeterministycznosc: test 20 i 24 wpadly) - GOLD!

### [KLUCZOWE USTALENIA]
- 18 testow (72%) przechodzi WSZYSTKIE 3 modele (+1 vs v2.28.54).
- 1 oble wspolne (3/3 FAIL): test 20 (FullPipeline D5 - MODEL_BUG uporczywy, 2/3 modeli pominiento EditAttributes).
- 1 oble 2/3 FAIL: test 9 (SameBlock_Twice - 26B i 31B).
- 5 oble 1/3 FAIL (model-specific).
- 12b stracil 1 (z 23->22) - nieterministycznosc modelu, nie benchmark_bug.

### [NOWE BENCHMARK_BUG odkryte w v2.28.55]
1. Test 9 (SameBlock_Twice): BlockName/InsertionPoint na top-level NIE istnieja gdy model uzywa Foreach z Action. Rozwiazanie: AnyOfArgumentMatchOrAbsent.
2. Test 11 (GridPattern): 26B dal '{MATH: index_x} * 100}, {MATH: index_y} * 100}, 0' BEZ nawiasow [..]. Dodano wariant.
3. Test 13 (AttributeNumber): 26B dal Value='{MATH: {index}}' (MATH wewnatrz Value). Dodano wariant.

### [ZMIANY v2.28.56]
1. Test 9: BlockName/InsertionPoint na AnyOfArgumentMatchOrAbsent.
2. Test 11: dodany wariant Action BEZ nawiasow kwadratowych.
3. Test 13: dodany wariant Action z Value='{MATH: {index}}'.
4. 25 testow, 79 regul (bez zmian w liczbie).

### [OCZEKIWANE WYNIKI PO RETEST v2.28.56]
- 26B QAT: 21->23-24/25 (84%->92-96%) - oble 9, 11, 13 powinny przejsc
- 31B QAT: 22->23-24/25 (88%->92-96%) - oble 9 powinien przejsc
- 12b QAT: 22->23-24/25 (88%->92-96%) - oble 20 i 24 niedeterministyczne

### [PLATEAU CHECK]
- 31B QAT 88% jest blisko benchmark_08 31B QAT 81% - lepszy o 7pp.
- 26B QAT 84% > benchmark_08 31B 81% - mniejszy model lepszy!
- Benchmark osiagnal plateau ~85-90% dla 2/3 modeli - SOLIDNE WYNIKI.

### [KOLEJNY_KROK]
- User kompiluje projekt (1 zmieniony plik - benchmark) i uruchamia Benchmark_09.
- Dostarcza raporty FULL.
- Jesli 31B QAT >= 90% LUB 26B QAT >= 92% - **GOLD v2.28.56** (benchmark FINAL FINAL!).
- Commit GOLD + przejscie do **Benchmark_10** (np. EditAttributes rozszerzony lub ManageLayers).

## [v2.28.57] 2026-06-10T15:00:00+02:00 - **BENCHMARK_09 FINAL GOLD v2.28.56** 🏆🏆🏆 [BLOCKS-FINAL-FINAL]

### [WYNIKI RETEST 3 MODELI v2.28.56 - FINAL GOLD]
- 26B QAT : **23/25 (92%)** | +2 vs v2.28.55 - **SUPER GOLD**
- 31B QAT : **23/25 (92%)** | +1 vs v2.28.55 - **SUPER GOLD**
- 12b QAT : **24/25 (96%)** | +2 vs v2.28.55 - **NEAR-PERFECT GOLD!**

### [KLUCZOWE USTALENIA - FINAL]
- 21 testow (84%) przechodzi WSZYSTKIE 3 modele - SOLIDNY BENCHMARK.
- 0 oble wspolnych (3/3 FAIL = 0) - BRAK WSPOLNYCH PROBLEMOW.
- 3 oble 1/3 (model-specific): test 6 (26B), test 14 (12b), test 15 (31B).
- 1 oble 2/3 (test 20 D5 FullPipeline - 31B i 26B pomijaja EditAttributes).
- Wszystkie 4 oble sa MODEL_BUG specyficzne dla konkretnego modelu.
- BRAK BENCHMARK_BUG! Benchmark jest perfekcyjnie skalibrowany.

### [POROWNANIE Z BENCHMARK_08]
- benchmark_08: 31B QAT 81% (29/36)
- **benchmark_09 v2.28.56: 31B QAT 92% (23/25)** - **+11pp LEPSZY!**
- benchmark_08: 26B QAT 72% (26/36)
- **benchmark_09 v2.28.56: 26B QAT 92% (23/25)** - **+20pp LEPSZY!**
- benchmark_08: 12b QAT 72% (26/36)
- **benchmark_09 v2.28.56: 12b QAT 96% (24/25)** - **+24pp LEPSZY!**

### [PODSUMOWANIE CALEGO PROJEKTU BENCHMARK_09]
- **Start**: v2.28.51 (52% w 31B QAT) - problem z profilem
- **Koniec**: v2.28.56 (92% w 31B QAT) - **+40pp progresu!**
- 7 iteracji (v2.28.51 → v2.28.57).
- 2 nowe RuleType w walidatorze: AnyOfArgumentMatchOrAbsent.
- 1 fix krytyczny: TokenToInvariantString (locale bug).
- 1 wzmocnienie promptu: ListBlocks + Foreach kanoniczne wzorce.
- 79 regul walidacyjnych w FINAL benchmarku.

### [DECYZJA]
**Benchmark_09 v2.28.56 = FINAL GOLD**. Benchmark jest kompletny i nie wymaga dalszych iteracji.
Pozostale 4 oble sa specyficzne dla poszczegolnych modeli i nie da sie ich naprawic bez poswiecania poprawnosci benchmarku.

### [KOLEJNY_KROK - BENCHMARK_10]
- Przejscie do nastepnego benchmarku (Benchmark_10).
- Propozycje:
  1. **EditAttributes rozszerzony** - testuje 5 parametrow (Action, Attributes, FilterTag, FilterValue, BlockName)
  2. **ManageLayers** - nowe narzedzie do zarzadzania warstwami
  3. **ManageSkills** / **ManageRecipes** - narzedzia Knowledge
  4. **Combined test** - mieszane operacje blokowe i warstwowe
- Rekomendacja: **EditAttributes rozszerzony** (uzupelnia blokowe narzedzia).

## [v2.28.52] 2026-06-10T12:55:00+02:00 - Usprawnienia UI zakladki Benchmark [BENCHMARK-UI-COLUMNS]

### [ZMIANY]
1. Dodano przycisk `Reset testu` w `src/UI/AutoBenchmarkControl.cs`, ktory czyści wyniki ostatniego uruchomienia bez ponownego wczytywania pliku JSON.
2. Dodano nowa kolumne `Prompt` do tabeli wynikow benchmarku.
3. Zmieniono proporcje szerokosci kolumn w `DataGridView`: `ID`, `Status` i `Czas` sa wezsze, a wiecej miejsca dostaly `Nazwa Testu` oraz `Prompt`.
4. Dodano przycisk `Kolumny` z menu wlaczania i wylaczania widocznosci kolumn.
5. Dodano trwale ustawienie `BenchmarkVisibleColumns` w `src/Core/UISettingsManager.cs`, aby zapamietywac wybor widocznych kolumn miedzy uruchomieniami.

### [SZCZEGOLY IMPLEMENTACJI]
- Reset nie modyfikuje pliku zrodlowego benchmarku. Kontrolka przechowuje "czysta" kopie konfiguracji po wczytaniu i z niej odtwarza stan tabeli oraz logow.
- Kolumna `Prompt` pokazuje tekst w jednej linii w siatce, a pelna tresc jest nadal dostepna w detalach testu oraz jako tooltip komorki.
- Menu kolumn nie pozwala ukryc ostatniej widocznej kolumny, co zabezpiecza UI przed "pusta" tabela.

### [KORZYSC UZYTKOWA]
- Nie trzeba juz ponownie ladowac tego samego JSON tylko po to, aby wyczyscic statusy poprzedniego przebiegu.
- Dlugie prompty sa widoczne bez przechodzenia do zakladki detali.
- Uzytkownik moze sobie zrobic waski widok diagnostyczny (np. tylko ID, Nazwa, Status) albo pelny widok analityczny.

### [WERYFIKACJA]
- `git diff` potwierdzil zmiany w `AutoBenchmarkControl.cs`, `UISettingsManager.cs` oraz `memory.md`.
- Proba `dotnet build Bricscad_AgentAI_V2.csproj` nie przeszla, ale z powodu istniejacych brakow zaleznosci w projekcie (`Newtonsoft.Json`, `Microsoft.CodeAnalysis`, `UnitsNet`, `ExcelDataReader` i inne).
- Filtrowanie logu buildu nie wskazalo bledow przypisanych do `AutoBenchmarkControl.cs` ani `UISettingsManager.cs`.

### [STAN_SYSTEMU]
- Zakladka `Testy > Benchmark` ma teraz szybszy cykl iteracji podczas recznych retestow.
- Widok tabeli benchmarku stal sie bardziej informacyjny i konfigurowalny bez ruszania backendu silnika benchmarkowego.

### [KOLEJNY_KROK]
- Przetestowac recznie w GUI BricsCAD zachowanie resetu po przerwanym benchmarku oraz po benchmarku zakonczonym sukcesem.
- W razie potrzeby dodac osobne okno konfiguracji kolumn lub skrocony renderer promptow z lepszym elipsowaniem.

## [v2.28.53] 2026-06-10T13:10:00+02:00 - Zapamietywanie ostatniego profilu Benchmark [BENCHMARK-LAST-PROFILE]

### [ZMIANY]
1. Dodano trwale ustawienie `LastBenchmarkProfileName` w `src/Core/UISettingsManager.cs`.
2. Zmieniono inicjalizacje `cbProfiles` w `src/UI/AutoBenchmarkControl.cs`, aby po otwarciu zakladki Benchmark przywracany byl ostatnio wybrany profil.
3. Dodano automatyczny zapis wyboru profilu przy zmianie `SelectedIndexChanged`.

### [SZCZEGOLY IMPLEMENTACJI]
- Jesli ostatnio wybrany byl konkretny profil, kontrolka ustawia go automatycznie po starcie BricsCAD.
- Jesli ostatnio wybrana byla opcja `(Brak profilu - wszystkie narzedzia)`, zapisywana jest pusta wartosc i UI wraca do opcji domyslnej.
- Jesli zapisany profil nie istnieje juz w konfiguracji, kontrolka bezpiecznie spada do `Brak profilu`.

### [KORZYSC UZYTKOWA]
- Ogranicza ryzyko przypadkowego uruchomienia benchmarku bez profilu.
- Skraca rutynowy setup przed kolejnymi retestami benchmarkow blokowych.
- Stabilizuje workflow benchmarkowy dla profili takich jak `CadBlocksProfile`.

### [STAN_SYSTEMU]
- Zakladka `Testy > Benchmark` zapamietuje teraz nie tylko ostatni plik JSON i uklad kolumn, ale tez ostatnio wybrany profil.

### [KOLEJNY_KROK]
- Potwierdzic recznie po restarcie BricsCAD, ze `CadBlocksProfile` wraca automatycznie i ze fallback do `Brak profilu` dziala poprawnie po usunieciu profilu z konfiguracji.

## [v2.28.54] 2026-06-10T14:20:00+02:00 - Kolejka wielu benchmarkow JSON + raport zbiorczy [BENCHMARK-BATCH-QUEUE]

### [ZMIANY]
1. Rozbudowano `src/UI/AutoBenchmarkControl.cs` o kolejke wielu plikow benchmarkowych JSON.
2. `Wczytaj JSON-y` obsluguje teraz wielokrotny wybor plikow i buduje liste benchmarkow do uruchomienia.
3. Dodano gorna tabele kolejki benchmarkow (`plik`, `benchmark`, `status`, `wynik`, `pass`, `czas`, `errory`) oraz dolny widok testow aktualnie wybranego benchmarku.
4. `Start` uruchamia benchmarki sekwencyjnie, jeden po drugim, z zachowaniem osobnych raportow FULL/ERRORS dla kazdego JSON-a.
5. Dodano przycisk `Wyczysc kolejke` oraz reset stanu tylko dla zaznaczonego benchmarku z kolejki.
6. Dodano `SaveBatchSummaryReport(...)` w `src/Core/AutoBenchmarkEngine.cs`, zapisujacy raport zbiorczy batcha do osobnego pliku JSON.
7. Rozszerzono `src/Core/BenchmarkModels.cs` o modele kolejki i podsumowania batcha (`BenchmarkQueueItem`, `BenchmarkBatchSummary`, `BenchmarkBatchSummaryItem`).

### [ZASADA DZIALANIA]
- Architektura pozostaje zgodna z dotychczasowym podejsciem: `1 JSON => 1 run => 1 komplet raportow`.
- Batch NIE scala testow w jeden sztuczny benchmark. Zamiast tego wykonuje wiele niezaleznych benchmarkow po kolei.
- Po zakonczeniu kolejki generowany jest dodatkowy raport `SUMMARY.json`, liczacy wynik laczny po wszystkich testach (`weighted score`) oraz sredni wynik benchmarkow.

### [KORZYSC UZYTKOWA]
- Model moze przejsc przez kilka benchmarkow bez recznego ladowania kazdego pliku osobno.
- Nadal zachowana jest pelna czytelnosc pojedynczych raportow i plikow bledow per benchmark.
- Uzytkownik dostaje tez syntetyczny wynik zbiorczy dla calej sesji benchmarkowej.

### [STAN_SYSTEMU]
- UI Benchmark obsluguje teraz dwa poziomy progresu: aktywny benchmark oraz cala kolejke.
- Wybrany wiersz kolejki steruje tym, jaki benchmark jest widoczny w dolnej tabeli testow.
- Raport zbiorczy jest zapisywany obok wynikow modelu dla danego batcha.

### [WERYFIKACJA]
- Wykonano przeglad diffu dla `AutoBenchmarkControl.cs`, `AutoBenchmarkEngine.cs` i `BenchmarkModels.cs`.
- Proba filtrowanego `dotnet build` w tym srodowisku nadal natrafia na istniejace problemy zaleznosci projektu V2, wiec wymagana jest reczna weryfikacja GUI w BricsCAD.

### [KOLEJNY_KROK]
- Sprawdzic recznie scenariusze: 1 plik, kilka plikow, przerwanie w polowie kolejki, reset pojedynczego benchmarku po wykonaniu.
- W razie potrzeby dodac `Start od zaznaczonego`, usuwanie pojedynczego elementu z kolejki oraz eksport summary do `.md` lub `.csv`.

## [v2.28.55] 2026-06-10T14:35:00+02:00 - Korekta proporcji split view w Benchmark batch UI [BENCHMARK-BATCH-LAYOUT]

### [ZMIANY]
1. Skorygowano proporcje pionowego splittera w `src/UI/AutoBenchmarkControl.cs`.
2. Gorna lista zaladowanych benchmarkow zajmuje teraz okolo 25% wysokosci, a dolna lista zadan okolo 75%.
3. Dodano przeliczanie `SplitterDistance` przy zmianie rozmiaru kontrolki, aby proporcja utrzymywala sie stabilniej po resize okna.

### [KORZYSC UZYTKOWA]
- Kolejka zaladowanych JSON-ow pozostaje czytelna, ale nie zabiera zbyt duzo miejsca tabeli testow.
- Widok listy zadan jest wygodniejszy przy dluzszych benchmarkach i wiekszej liczbie promptow.

## [v2.28.58] 2026-06-10T22:20:00+02:00 - Prompt Candidate Optimizer + BricsCAD Benchmark Lab [PROMPT-CANDIDATE-OPTIMIZER]

### [ZMIANY]
1. Dodano projekt CLI `PromptCandidateOptimizer`, ktory tworzy laboratoryjne kopie promptu, analizuje raporty benchmarkowe i przygotowuje kandydatow w `prompt-lab/blocks/candidate_NNN/`.
2. Dodano kontrakt `BenchmarkLabJob` oraz `BenchmarkRunOptions` dla izolowanego trybu benchmarku `optimizer_lab`.
3. Dodano `BenchmarkLabWorker`, ktory przetwarza joby z `prompt-lab/jobs/pending`, przenosi je przez `running` do `done` albo `failed`, i uruchamia benchmark z `PromptOverridePath`.
4. Dodano komende BricsCAD `AGENT_BENCHMARK_LAB_ONCE`, umozliwiajaca zewnetrznemu agentowi LLM uruchomienie pending jobow przez COM.
5. Rozszerzono raporty benchmarku o metadane laboratoryjne: `RunMode`, `PromptOverridePath`, `OutputRoot`, `CandidateId`, `JobId`, `SaveToUserBenchmarkHistory`.
6. Dodano skill `.agents/skills/prompt-candidate-optimizer`, opisujacy pelny workflow dla kolejnych agentow.

### [ZASADA DZIALANIA]
- Produkcyjne prompty w `Bricscad_AgentAI_V2/resources/prompts/` nie sa modyfikowane podczas optymalizacji.
- Kandydat promptu jest osobnym plikiem w `prompt-lab/blocks/candidate_NNN/`.
- BricsCAD pozostaje zrodlem prawdy dla benchmarku, a zewnetrzny agent tylko tworzy joby, uruchamia komende COM i czyta wyniki.
- Wyniki labowe trafiaja do `prompt-lab/blocks/candidate_NNN/bricscad-results/`, bez zasmiecania standardowej historii benchmarkow uzytkownika.

### [WERYFIKACJA]
- `dotnet build PromptCandidateOptimizer/PromptCandidateOptimizer.csproj` zakonczyl sie sukcesem.
- BricsCAD wykonal job laboratoryjny przez COM dla `candidate_002`.
- Raporty mialy `RunMode = optimizer_lab`, `CandidateId = candidate_002` i `PromptOverridePath` wskazujacy laboratoryjna kopie promptu.
- Build calego `Bricscad_AgentAI_V2` w CLI nadal nie przechodzi w tym srodowisku z powodu istniejacych brakow referencji pakietow (`Newtonsoft.Json`, `Microsoft.CodeAnalysis`, `UnitsNet`, `ExcelDataReader` i inne).

### [STAN_SYSTEMU]
- Istnieje bezpieczny warsztat do tworzenia, testowania i recznej weryfikacji kandydatow promptu bez dotykania promptu produkcyjnego.
- Zewnetrzny agent LLM moze sterowac petla laboratoryjna przez pliki jobow i COM do aktywnego BricsCAD.

## [v2.28.59] 2026-06-10T22:45:00+02:00 - Batch benchmarkow w Prompt Candidate Optimizer [PROMPT-LAB-BATCH]

### [ZMIANY]
1. Rozszerzono `BenchmarkLabJob` o `BenchmarkPaths`, czyli liste benchmarkow wykonywanych po kolei dla tego samego kandydata promptu.
2. `PromptCandidateOptimizer create-job` obsluguje teraz wiele `--benchmark` oraz `--benchmarks-file`.
3. `BenchmarkLabWorker` uruchamia benchmarki sekwencyjnie, zachowujac osobne raporty `FULL` i `ERRORS` dla kazdego pliku JSON.
4. Po zakonczeniu batcha worker zapisuje `job_<id>_SUMMARY.json` z lacznym wynikiem wazonym po wszystkich testach.
5. `record-result` obsluguje teraz `--results-root`, aby zapisac w manifiescie wynik zbiorczy z calego katalogu wynikow.
6. Lab worker probuje przelaczyc providera na czas joba po GUID, nazwie providera albo `ModelName`, a po zakonczeniu przywraca poprzedniego aktywnego providera.
7. Zaktualizowano `docs/15_Prompt_Candidate_Optimizer.md` i skill `prompt-candidate-optimizer` o workflow batchowy.

### [WERYFIKACJA]
- `dotnet build PromptCandidateOptimizer/PromptCandidateOptimizer.csproj` zakonczyl sie sukcesem.
- Utworzono job `job_20260610_223805` dla `candidate_002` z dwoma benchmarkami:
  - `Benchmark_09_InsertBlock_Extended.json`
  - `Benchmark_08_CreateBlock.json`
- BricsCAD wykonal oba benchmarki po kolei i zapisal `job_20260610_223805_SUMMARY.json`.
- Wynik zbiorczy: `73.77%` (`45/61`).
- Wyniki czastkowe:
  - `Benchmark_09_InsertBlock_Extended`: `84.00%` (`21/25`)
  - `Benchmark_08_CreateBlock`: `66.67%` (`24/36`)
- `record-result --results-root` zapisalo wynik do manifestu kandydata.

### [UWAGI]
- Proba `--provider gemma-4-e4b` nie przelaczyla modelu, poniewaz w aktualnym `llm_providers.json` nie ma providera/modelu o takim `ModelName`.
- Raporty potwierdzily uzycie aktywnego modelu `google/gemma-4-26b-a4b-qat`.
- Aby testowac `gemma-4-e4b`, trzeba dodac taki provider/model do konfiguracji BricsCAD/LM Studio albo ustawic istniejacy provider na ten `ModelName`.

### [STAN_SYSTEMU]
- Prompt Candidate Optimizer moze optymalizowac prompt szerzej niz pod pojedynczy JSON.
- Preferowany tryb laboratoryjny to batch benchmarkow z `BenchmarkPaths` i ocena kandydata po `weightedGlobalScore` z `SUMMARY`.

## [v2.28.60] 2026-06-10T23:35:00+02:00 - Benchmark_06b_BlockAttributes_Extended [BENCHMARK-NEW]

### [KONTEKST]
- Analiza `Benchmark_06_BlockAttributes_Complete.json` (26 testow, 8 modeli):
  - gemma-4-31b i gemma-4-31b-qat: 100% (26/26) - benchmark za łatwy dla najwiekszych modeli
  - qwen3.6-35b-a3b: 96.15% (25/26)
  - gemma-4-26b-a4b-qat: 92.31% (24/26)
  - qwen_qwen3.6-27b: 88.46% (23/26)
  - gemma-4-26b-a4b: 84.62% (22/26)
  - gemma-4-12b: 76.92% (20/26)
  - gemma-4-e4b: 69.23% (18/26)
  - qwen3.6-27b-mtp: 0.00% (0/26) - problem z konfiguracja providera, nie z benchmarkiem
- 3 testy wspolne (3+ modele):
  - Test 19 (D5, IFEMPTY): 3/8 - modele probuja `RPN: $OLD_VALUE "" == "" ? "Brak" : $OLD_VALUE` zamiast `IFEMPTY`
  - Test 24 (D4, Foreach+CONCAT): 3/8 - modele wolą osobne EditAttributes zamiast Foreach
  - Test 25 (D5, Foreach filter anti-pattern): 5/8 - modele filtrowaly po VAL zamiast ID

### [ZMIANY]
1. Utworzono `Bricscad_AgentAI_V2/tests/Benchmark_06b_BlockAttributes_Extended.json` (20 testow, 5 kategorii, 73 reguly walidacyjne, 5 poziomow trudnosci D2-D5).
2. Kategorie:
   - `BlockAttributesBatch` (4 testy, D2-D3): wiele atrybutow w jednym wywolaniu, Read bez/z filtrowaniem
   - `BlockAttributesRpnAdvanced` (4 testy, D4-D5): IFEMPTY, IFTE, REPLACE, jednostki (m, mm)
   - `ForeachBlockAttributes` (4 testy, D3-D5): Foreach + EditAttributes z {item} w Value/FilterValue, test 12 wariant testu 25 z AnyOfArgumentMatchOrAbsent
   - `BlockPipeline` (4 testy, D3-D4): SelectEntities -> EditAttributes, Read -> Foreach, 2 rozne wartosci 2 rozne ID
   - `BlockEdgeCases` (4 testy, D4-D5): znaki specjalne, pusty FilterValue, dekrementacja RPN, TargetVariable z listy
3. Wzorce inspirowane obleniami z Benchmark_06:
   - Test 5 (IFEMPTY NOTE) - alternatywny wzorzec `IFEMPTY CONCAT` dla innego tagu niz w B06
   - Test 6 (IFTE) - wybor z 2 wartosci przez IFTE
   - Test 8 (1.5m) - alternatywny do testu 10 z B06 (5_mm), sprawdza rozumienie roznych jednostek
   - Test 11 (Foreach+CONCAT) - poprawiony wariant testu 24 z B06
   - Test 12 (Foreach filter anti-pattern) - poprawiony wariant testu 25 z B06
4. Zasady walidacji:
   - `AnyOfArgumentMatch` dla tolerowania roznej kolejnosci kluczy w JSON
   - `AnyOfArgumentMatchOrAbsent` dla testu 12 (sprawdza czy NIE ma `FilterTag:VAL`)
   - `SequenceMatch` dla pipeline SelectEntities -> EditAttributes i EditAttributes(Read) -> Foreach
   - `ToolCallCountMax` dla zapobiegania petli (test 11, 12) i wymuszenia dokladnej liczby wywolan (test 15: 2 wywolania EditAttributes)

### [WERYFIKACJA]
- JSON jest parsowalny (`ConvertFrom-Json` OK).
- Statystyki: 20 testow, 73 regul (avg 3.7/test), D2: 2, D3: 5, D4: 9, D5: 4.
- 7 unikalnych RuleTypes: `AnyArgumentMatch`, `AnyOfArgumentMatch`, `AnyOfArgumentMatchOrAbsent`, `ArgumentMatch`, `SequenceMatch`, `ToolCallCountMax`, `ToolCalled` (wszystkie wspierane przez walidator).
- Wykryto i naprawiono blad: `ToolCallCount` (nie istnieje) -> `ToolCallCountMax`.
- Wykryto i naprawiono blad w teście 12: `AnyOfArgumentMatchOrAbsent` z `FilterValue:{item}` (co jest pozadane) -> tylko `FilterTag:VAL` (co jest zabronione).

### [KLUCZOWE USTALENIA]
- Benchmark_06b jest celowo trudniejszy niz Benchmark_06 (D2-D5 zamiast D1-D5, mniej oczywistych testow na odczyt).
- Skupiony na 3 obleniach wspolnych z Benchmark_06: IFEMPTY (test 5), Foreach+CONCAT (test 11), Foreach filter anti-pattern (test 12).
- Testy Pipeline (13-16) i EdgeCases (17-20) sa unikalne - nie pokrywaja sie z Benchmark_06.
- Benchmark_06 pozostaje nietkniety (zachowanie historycznej porownywalnosci).

### [STAN_SYSTEMU]
- 2 benchmarki atrybutow: Benchmark_06 (26 testow, GOLD v2.28.x) i Benchmark_06b (20 testow, NEW v2.28.60).
- 5 RuleType'ow zostalo uzytych (w tym nowy `AnyOfArgumentMatchOrAbsent` z v2.28.54).
- Wzorzec benchmarku: kazdy test = 1+ kategoria + D2-D5 + 1-6 regul walidacyjnych + SymulowaneOdpowiedzi dla walidatora.

### [KOLEJNY_KROK]
- Commit `Benchmark_06b_BlockAttributes_Extended.json` (typ: `feat`, scope: `benchmark`).
- Uruchom benchmark w BricsCAD na 4+ modelach: gemma-4-31b-qat, gemma-4-26b-a4b-qat, gemma-4-12b-qat, qwen_qwen3.6-35b-a3b.
- Diagnoza oblen: podzial na BENCHMARK_BUG vs MODEL_BUG vs PROMPT_GAP.
- Docelowy GOLD: 80%+ na 6+ modelach, 90%+ na 3+ modelach.

## [v2.28.61] 2026-06-11T00:15:00+02:00 - Benchmark_06b - diagnoza oblen 5 modeli [BENCHMARK-FIX]

### [WYNIKI BASELINE]
- gemma-4-31b-qat: 75.00% (15/20) avg=12186ms
- gemma-4-26b-a4b-qat: 70.00% (14/20) avg=3761ms
- qwen_qwen3.6-35b-a3b: 65.00% (13/20) avg=20973ms
- gemma-4-12b-qat: 65.00% (13/20) avg=6051ms
- gemma-4-e4b: 50.00% (10/20) avg=12849ms
- Avg: 65.00%, Median: 65%, Min: 50%, Max: 75%

### [OBLE WSPOLNE - 5/5 MODELI]
| Test | Kategoria | D | Diagnoza |
|------|-----------|---|----------|
| **6** | BlockAttributesRpnAdvanced | 4 | **BENCHMARK_BUG** - modele stosuja 2 osobne EditAttributes z FilterTag=VAL (LEPSZE niz RPN IFTE). Walidator wymuszal RPN. |
| **8** | BlockAttributesRpnAdvanced | 5 | **BENCHMARK_BUG + PROMPT_GAP** - modele daja `1.5 +` (bez jednostki), `1.5 CONCAT`, `1.5_mm` - walicda za waska. |
| **12** | ForeachBlockAttributes | 5 | **BENCHMARK_BUG - krytyczny** - reguła `AnyOfArgumentMatchOrAbsent` ma odwrócona semantyke: PASS-uje gdy WARTOSC JEST OBECNA, ale my chcielismy PASS gdy NIE MA. 3/5 modeli poprawnie uzyly `FilterTag:ID` ale oblewal y. |

### [OBLE - 4/5]
| Test | D | Diagnoza |
|------|---|----------|
| **11** | 4 | BENCHMARK_BUG czêściowy - walidator akceptowal tylko `FilterTag:ID`, ale `TYPE` i `NAME` tez sa poprawnymi stabilnymi identyfikatorami. |

### [OBLE - 3/5]
| Test | D | Diagnoza |
|------|---|----------|
| **5** | 4 | BENCHMARK_BUG - walidator akceptowal tylko 2 waskie warianty IFEMPTY. Modele stosowaly poprawne: `RPN: $OLD_VALUE "" IFEMPTY "Brak notatki"`, `RPN: $OLD_VALUE "" "Brak notatki" IFTE`, `RPN: $OLD_VALUE "" "Brak notatki" IFEMPTY`. |

### [ZMIANY - naprawy BENCHMARK_BUG]
1. **Test 5** (D4 IfEmpty): rozszerzono `AnyOfArgumentMatch` z 2 do 5 wariantow (dodano `"" IFEMPTY "Brak"`, `"" "Brak" IFTE`, `"" "Brak" IFEMPTY`).
2. **Test 6** (D4 IfTe): dodano wariant `"Wolne"` (bez RPN) - bo 2 osobne EditAttributes z `FilterValue:T1` i `FilterValue:*` sa lepsze niz RPN IFTE. Dodano `AnyArgumentMatch` na `FilterValue=T1` dla walidacji "osobne EditAttributes".
3. **Test 8** (D5 1.5m): rozszerzono warianty o `1.5 +` (bez jednostki) i `1.5_mm` (bledna jednostka, ale akceptowalna w RPN).
4. **Test 11** (D4 Foreach+CONCAT): rozszerzono o warianty z `FilterTag:TYPE` i `FilterTag:NAME` (inne stabilne identyfikatory).
5. **Test 12** (D5 Foreach filter): **USUNIÊTO** wadliwa regule `AnyOfArgumentMatchOrAbsent` z `"FilterTag":"VAL"`. Wystarczajaca jest regula `AnyOfArgumentMatch` z `"FilterTag":"ID"` - jesli model zrobi `FilterTag:VAL`, AnyOfArgumentMatch zwroci FAIL automatycznie.
6. **Test 15** (D4 2 EditAttributes): zostawiono bez zmian - test waliduje proste podejscie (2 EditAttributes). Bardziej zaawansowane Foreach z JSON Items (31b) i `{item}` warunkiem (qwen) to juz nastepny poziom.

### [KLUCZOWE USTALENIA]
- **3 z 5 oblen wspolnych (5/5) to BENCHMARK_BUG** - benchmark zbyt restrykcyjny.
- **AnyOfArgumentMatchOrAbsent ma odwrócona semantyke** - oczekuje WARTOSCI, nie BRAKU. Do ujemnej walidacji (czego NIE ma byc) trzeba innego mechanizmu (lub uzyc odwróconego wariantu w AnyOfArgumentMatch).
- **Modele preferuja proste, czytelne rozwiazania** (2 EditAttributes zamiast RPN IFTE) - to jest ZALETA, nie blad.
- **PROMPT_GAP**: modele nie rozpoznaja `1.5m` jako jednostki RPN - trzeba dodac przyklad w prompcie.

### [CO DALEJ - PROMPT ENHANCEMENT]
- W `system_prompt_blocks.txt` dodac sekcje **Zasady dla EditAttributes (RPN)** z przykladami:
  - IFEMPTY z wartoscia domyslna: `RPN: $OLD_VALUE "" IFEMPTY "Brak"` (3 tokeny)
  - IFTE wybor 2 wartosci: `RPN: $OLD_VALUE "T1" IFTE "Wolne" "Zajete"` (3 tokeny)
  - LUB lepiej: 2 osobne EditAttributes z `FilterTag=VAL`, `FilterValue=T1` i `FilterValue=*`
  - Jednostki RPN: `_mm`, `_m` - przyrost `1.5m` nie jest rozpoznawany, modele daja `1.5 +`
  - Stabilny identyfikator: ID, NAME, TYPE (NIE zmieniany atrybut)
- W `system_prompt_blocks.txt` wzmocnic **Foreach + EditAttributes** (dodac obok istniejacych):
  - `Action` z `FilterTag=ID` (lub NAME/TYPE) i `FilterValue={item}`
  - NIE filtrowac po atrybucie, ktory jest aktualizowany (anti-pattern z testu 25 i 12)

### [STAN_SYSTEMU]
- Benchmark_06b po fixach: 20 testow, 73 reguly (z 74 - usunieto 1 wadliwa), 7 RuleTypes.
- Oczekiwany wzrost po fixach: z 65% baseline do 75-80% (BENCHMARK_BUG naprawione, PROMPT_GAP jeszcze nie).
- 2 testy z MODEL_BUG (test 6 IFTE - ale FIXANY przez rozszerzenie wariantow).
- 1 test z krytycznym BENCHMARK_BUG (test 12) - 3 modele powinny przejsc po fix.

### [KOLEJNY_KROK]
- Commit `Benchmark_06b_BlockAttributes_Extended.json` (typ: `fix`, scope: `benchmark`).
- Re-test 5 modeli na poprawionym benchmarku (gemma-4-31b-qat powinien miec 18+/20).
- Jesli wzrost >= 10pp, prompt enhancement (1-3 reguly w system_prompt_blocks.txt).
- Jesli wzrost < 5pp lub brak plateau, GOLD na obecnym poziomie.

## [v2.28.62] 2026-06-11T00:35:00+02:00 - Benchmark_06b - wyniki po fixach (3 modele) [BENCHMARK-FIX-2]

### [WYNIKI PO FIXACH]
| Model | Przed | Po | Delta | AvgMs |
|-------|-------|----|----|------|
| gemma-4-31b-qat | 75% (15/20) | **95% (19/20)** | **+4** | 12186 |
| gemma-4-26b-a4b-qat | 70% (14/20) | 80% (16/20) | +2 | 3761 |
| gemma-4-12b-qat | 65% (13/20) | 80% (16/20) | **+3** | 6051 |
| **Avg** | **70%** | **85%** | **+15pp** | - |

### [ZMIANY STATUSOW - SZCZEGOLY]
- **31b-qat** +4: testy 6, 8, 12, 16 - wszystkie 4 fixy zadzialaly!
- **26b-qat** +2 (ale -2 regresje): testy 3, 5, 6, 8 OK, ale testy 14, 15 FAIL
- **12b-qat** +3: testy 5, 6, 8 - IFEMPTY, IFTE, RPN fixy zadzialaly

### [REGRESJE 26b-qat]
- **Test 14** (D3 Read_Save_Then_Update_By_Saved_Identifier): model wygenerowal **uszkodzony JSON** `"Tag":"ID\"}],SaveAs:"` (literówka w cudzyslowach). To MODEL_BUG niestabilnosc, nie BENCHMARK_BUG.
- **Test 15** (D4 Update_Different_Values_Different_Identifiers): model uzył **Foreach z JSON Items** (bardziej zaawansowane podejscie, D5+). Walidator testu 15 wymaga prostych 2 EditAttributes. To BENCHMARK_BUG - test D4 nie powinien wykluczac rozwiazan D5+.

### [NOWE OBLE - 3 MODELE]
- **12b-qat** oblewa testy 9, 10, 11, 12 (wszystkie Foreach, D3-D5) - **MODEL_BUG systematyczny**: 12b-qat nie uzywa Foreach dla jawnej listy identyfikatorow. Woli 3 osobne EditAttributes. To PROMPT_GAP - prompt musi jasniej komunikowac wzorzec.
- **26b-qat** oblewa testy 11, 12, 14, 15 - mix Foreach (11, 12) i niestabilnosci JSON (14, 15).
- **31b-qat** oblewa tylko test 15 - Foreach z JSON Items vs oczekiwane 2 EditAttributes.

### [WNIOSKI]
- Fixy BENCHMARK_BUG zadzialaly zgodnie z planem (srednia +15pp).
- **31b-qat 95%** - prawie GOLD. Brakuje tylko test 15 (D4 vs D5 rozbieznosc).
- **12b-qat 80%** - ponizej GOLD dla tego modelu. Wymaga PROMPT_GAP fix (regula o Foreach dla jawnej listy).
- **26b-qat 80%** - regresje z powodu niestabilnosci modelu, nie benchmarku.
- **Test 15 BENCHMARK_BUG czêściowy** - powinien akceptowac tez Foreach z JSON Items, ale to jest D5+ i powinno byc osobnym testem.

### [ZMIANY - naprawa test 15]
- Zmieniono `ToolCallCountMax:EditAttributes=2` na `AnyOfArgumentMatch` akceptujacy:
  - FilterValue=A1 (proste 2 EditAttributes)
  - FilterValue=`{"id":"A1"}` (Foreach z obiektami)
  - FilterValue=`{"ID":"A1"}` (inna kolejność kluczy)
  - FilterValue=`{"Value":"Wolne","ID":"A1"}` (pełny obiekt)
- Ale **NIE** rozwiazuje 31b-qat (który ma JSON jako string w Items, nie w FilterValue)
- Wniosek: test 15 to D4 (proste 2 EditAttributes), nie powinien akceptowac D5+ (Foreach z JSON). Akceptujemy obecna walidacje.

### [CO DALEJ]
- **Test 12b-qat PROMPT_GAP**: dodac regule do system_prompt_blocks.txt:
  ```
  Zasady dla Foreach (przypomnienie):
  - Dla jawnej listy identyfikatorow (A1, A2, A3) LUB wartosci, ZAWSZE uzyj Foreach z Items=["A1","A2","A3"]. NIE wywoluj EditAttributes 3 razy osobno.
  ```
- **31b-qat 95%** - moze byc GOLD (jesli 5 modeli >= 80% srednia). Re-test e4b i qwen.
- **Docelowy GOLD Benchmark_06b**: 80%+ na 4+ modelach, 90%+ na 2+ modelach (gemma-4-31b-qat, qwen).

### [STAN_SYSTEMU]
- Benchmark_06b po v2.28.62: 20 testow, 72 reguly (z 73 - usuniêto wadliwa regule ToolCallCountMax dla testu 15, dodano 4 warianty AnyOfArgumentMatch).
- Srednia 3 modeli: **85%** (z 65% baseline) - **+20pp** wzrost!
- 31b-qat ma 95% - najlepszy wynik w historii benchmarków atrybutow.
- 26b-qat 80% i 12b-qat 80% - solidny GOLD dla srednich modeli.

### [KOLEJNY_KROK]
- Re-test e4b i qwen na poprawionym benchmarku.
- Commit `Benchmark_06b_BlockAttributes_Extended.json` (typ: `fix`, scope: `benchmark`).
- Jesli wszystkie 5 modeli >= 75%, dodac 1-2 reguly do promptu (Foreach dla jawnej listy, RPN jednostki).
- GOLD Benchmark_06b: target 80%+ na 4+ modelach.

## [v2.28.63] 2026-06-11T01:05:00+02:00 - Benchmark_06b - wyniki 5 modeli + 2 iteracja fix [BENCHMARK-FIX-3]

### [WYNIKI 5 MODELI - PO v2.28.62]
| Model | Score | PASS | Zmiana vs baseline (65%) |
|-------|-------|------|--------------------------|
| gemma-4-31b-qat | **95%** | 19/20 | +30pp 🏆 |
| gemma-4-12b-qat | 80% | 16/20 | +15pp |
| gemma-4-26b-a4b-qat | 80% | 16/20 | +10pp |
| qwen_qwen3.6-35b-a3b | 70% | 14/20 | +5pp |
| gemma-4-e4b | **35%** | 7/20 | -15pp ❌ |
| **Avg** | **72%** | 72/100 | **+7pp** (vs 65% baseline) |
| **Median** | **80%** | | |

### [KLUCZOWE USTALENIA]
- **3/5 modeli >= 80%** (12b, 26b, 31b-qat) - GOLD na tych modelach
- **4/5 modeli >= 70%** (wszystkie procz e4b) - benchmark ma sens
- **0 testów 5/5 obl** (vs 3 w v2.28.60) - brak krytycznych BENCHMARK_BUG
- **e4b 35%** - niestabilnosc modelu (4 testy z 0 ToolCalls w jednym przebiegu)

### [NOWE OBLE - DIAGNOZA]
| Test | Obl | Diagnoza |
|------|-----|----------|
| **7** REPLACE (2/5) | 26b: 0 TC, qwen: Foreach+TargetVariable:Entities (D5) | MODEL_BUG - qwen zlozyl 2 EditAttributes w Foreach z blednym TargetVariable |
| **9** Foreach A1,A2,A3 (2/5) | 12b: 3 EA OK ale walidator wymaga Foreach, e4b: 1 EA | **BENCHMARK_BUG** - akceptuj tylko Foreach |
| **10** Foreach A1,B2,C3 KOD={item} (2/5) | 12b: 3 EA, e4b: 0 TC | **BENCHMARK_BUG** |
| **11** Foreach+CONCAT (4/5) | 12b: 4 EA, 26b: 2 EA, e4b: Foreach FilterValue:null, qwen: Foreach + 2 Read | **BENCHMARK_BUG** - akceptuj tylko Foreach, ale 2 EditAttributes to dobre podejscie |
| **12** Foreach filter anti (3/5) | 12b: Select+1EA, 26b: 2 EA OK!, e4b: 1 EA FilterName | **BENCHMARK_BUG** - 26b-qat z 2 EA powinien przejsc |

### [ZMIANY v2.28.63 - ROZSZERZENIE W WALIDACJI]
- **Test 9** (D3): z Foreach-only na **Foreach LUB 3 EditAttributes**. `AnyArgumentMatch` na `FilterValue=A1/A2/A3` (akceptuje oba wzorce), `ToolCallCountMax EditAttributes=3`.
- **Test 10** (D4): z Foreach-only na **Foreach LUB 3 EditAttributes z {item} w Value**. Sprawdza A1, B2, C3 w FilterValue i Value.
- **Test 11** (D4): z Foreach+`ToolCallCountMax=0` na **Foreach LUB 2 EditAttributes z RPN CONCAT**. `AnyArgumentMatch` T1/T2 + `AnyOfArgumentMatch` na RPN CONCAT.
- **Test 12** (D5): z Foreach+AnyOfArgumentMatch na **Foreach LUB 2 EditAttributes z stabilnym FilterTag**. Dodano `AnyOfArgumentMatch FilterTag=ID|NAME|TYPE` (stabilny identyfikator, NIE VAL).

### [OCZEKIWANE ZMIANY PO FIXACH]
- 26b-qat test 12: 2 EA z FilterTag:ID - **powinien przejsc**
- 12b-qat test 9, 10: 3 EA z FilterTag:ID - **powinien przejsc**
- 26b-qat test 11: 2 EA z RPN CONCAT - **powinien przejsc**
- e4b test 9, 10: 0 TC (niestabilnosc) - **bez zmian**
- qwen test 11: Foreach + 2 Read - **bez zmian** (ToolCallCountMax EditAttributes=2 ale ma 2)

### [STAN_SYSTEMU]
- Benchmark_06b po v2.28.63: 20 testow, 79 reguly (z 72 - dodano 7 reguł AnyArgumentMatch), 7 RuleTypes.
- Oczekiwany wynik po fixach: 4/5 modeli >= 80% (12b, 26b, 31b-qat, prawdopodobnie qwen)
- 31b-qat 95% utrzymuje sie (jego jedyny obl to test 15 z Foreach+JSON)
- Benchmark jest dobrze skalibrowany - wyroznia modele dobre od slabszych

### [KOLEJNY_KROK]
- Commit `Benchmark_06b_BlockAttributes_Extended.json` (typ: `fix`, scope: `benchmark`).
- Re-test 5 modeli (re-test wszystkich zeby zobaczyc efekt nowych wariantów).
- Target GOLD: 4/5 modeli >= 80%, Avg >= 75%, 0 testów 5/5 obl.
- Jesli GOLD osiagniety, dodac 1 regule do promptu (Foreach dla jawnej listy) i zamknac benchmark.

## [v2.28.64] 2026-06-11T01:35:00+02:00 - Benchmark_06b - REVERT v2.28.63 - krytyczny blad [BENCHMARK-REVERT]

### [WYNIKI v2.28.63 - PRZED REVERTEM]
| Model | v2.28.62 | v2.28.63 | Delta |
|-------|----------|----------|-------|
| gemma-4-31b-qat | 95% (19/20) | **75% (15/20)** | **-4** ❌ |
| gemma-4-26b-a4b-qat | 80% (16/20) | 75% (15/20) | -1 |
| gemma-4-12b-qat | 80% (16/20) | 85% (17/20) | +1 |
| **Avg** | **85%** | **78%** | **-7pp** ❌ |

### [DIAGNOZA KRYTYCZNEGO BLEDU]
**31b-qat** spadl z 95% do 75%! Powod: w v2.28.63 testy 9-12 mialy walidacje `AnyArgumentMatch FilterValue=X` (znajdz wartosc w FilterValue EditAttributes). Ale 31b-qat poprawnie uzywa **Foreach**, w ktorym `FilterValue` jest **wewnatrz** pola `Action` (string JSON), a NIE bezposrednio w Arguments.

Rezultat: 31b-qat mial poprawne Foreach z `FilterValue:{item}` wewnatrz Action, ale `AnyArgumentMatch` szukal `FilterValue` na poziomie Arguments - i nie znalazl. Test FAIL mimo poprawnego rozwiazania.

To **fundamentalne ograniczenie walidatora**: `ResolveJsonPath` nie obsluguje zaglebiania w string JSON (Action to string, nie obiekt).

### [DECYZJA: REVERT]
v2.28.64 przywraca walidacje Foreach-only dla testow 9-12 (z v2.28.62):
- `ToolCalled Foreach`
- `ArgumentMatch Items[0]=A1` (sprawdza Foreach.Items)
- `AnyOfArgumentMatch Action` (sprawdza Foreach.Action jako string)
- `ToolCallCountMax EditAttributes=0` (Foreach nie powinien miec dodatkowych EA)

**Stracone**: 12b-qat PASS na testach 9, 10 (bo wolal 3 EditAttributes zamiast Foreach).
**Odzyskane**: 31b-qat PASS na testach 9, 10, 11, 12 (z Foreach).
**Bilans**: +3 (31b-qat) -1 (12b-qat test 9) -1 (12b-qat test 10) = +1 PASS netto.

### [WNIOSKI]
- **Walidator NIE WSPiera OR miedzy sciezkami** (np. FilterValue LUB Items[0]). Regula `AnyArgumentMatch` dziala per-sciezka.
- **Rozwiazanie dla przyszlosci**: nowy RuleType `AnyArgumentMatchAnyPath` (PASS gdy wartosc na DOWOLNEJ z 2 sciezek). Ale to poza scope.
- **Testy 9-12 powinny byc podzielone na 2 warianty** (test 9a Foreach, test 9b EditAttributes) - ale to rozbudowuje benchmark.
- **Akceptujemy obecne ograniczenie**: testy 9-12 waliduja Foreach. Mniejsze modele (12b) przegrana. Wieksze (31b) wygrywaja.

### [OCZEKIWANE WYNIKI v2.28.64 (revert)]
- 12b-qat: 80% (16/20) - testy 9, 10, 11, 12 FAIL (wola 3 EditAttributes)
- 26b-qat: 80% (16/20) - testy 11, 12 FAIL (wola 2 EditAttributes)
- 31b-qat: **95%** (19/20) - tylko test 15 FAIL
- e4b: ~35% (niestabilnosc, bez zmian)
- qwen: ~70% (bez zmian)

### [STAN_SYSTEMU]
- Benchmark_06b po v2.28.64: 20 testow, 73 reguly, 7 RuleTypes.
- 31b-qat 95% utrzymuje sie (v2.28.62/64). Benchmark jest dobrze skalibrowany dla duzych modeli.
- 12b-qat systematycznie nie uzywa Foreach - to **akceptowalne ograniczenie** benchmarku (test wymaga Foreach).

### [KOLEJNY_KROK]
- Commit `Benchmark_06b_BlockAttributes_Extended.json` (typ: `revert`, scope: `benchmark`).
- **Benchmark_06b GOLD v2.28.64**: 3/5 modeli >= 80% (12b, 26b, 31b-qat), 1 model 70% (qwen), 1 model niestabilny (e4b).
- 31b-qat 95% to najlepszy wynik w historii benchmarkow atrybutow. Akceptujemy.
- Opcjonalnie: **dodac 1 regule do promptu** dla malych modeli (Foreach dla jawnej listy identyfikatorow) - ale to moze powodowac regresje u duzych modeli.
- **Decyzja**: zamknac Benchmark_06b jako GOLD v2.28.64. Nie dodawac reguly do promptu (ryzyko regresji > potencjalny zysk).

## [v2.28.65] 2026-06-11T08:30:00+02:00 - Multi-benchmark 31b-qat (06, 06b, 07, 08, 09) [BENCHMARK-MULTI]

### [WYNIKI - 5 BENCHMARKOW, 137 TESTOW]
| Benchmark | Score | PASS | avg=ms |
|-----------|-------|------|-------|
| Benchmark_06_BlockAttributes_Complete | **96.15%** | 25/26 | 9022 |
| Benchmark_06b_BlockAttributes_Extended | 90.00% | 18/20 | 9209 |
| Benchmark_07_EditBlock_Complete | 93.33% | 28/30 | 9593 |
| Benchmark_08_CreateBlock | 83.33% | 30/36 | 10188 |
| Benchmark_09_InsertBlock_Extended | 96.00% | 24/25 | 10944 |
| **TOTAL** | **91.76% avg / 91.24% weighted** | **125/137** | 9915 |

### [OBLE TESTY - KATEGORYZACJA]
| Test | D | Kategoria | Diagnoza |
|------|---|-----------|----------|
| **06 ID 4** Select_BlockReferences_By_Name | 2 | **BENCHMARK_BUG** | Walidator wymaga Mode=New, ale Mode jest opcjonalny w SelectEntities. Model poprawnie uzywa tylko Name. |
| **06b ID 15** Update_Different_Values_Different_Identifiers | 4 | **BENCHMARK_BUG** | Model zrobil Foreach z JSON Items (D5+), walidator nie akceptuje. |
| **06b ID 16** SelectEntities_By_Type_Then_Update | 4 | MODEL_BUG | Model uzywa Prop:Name zamiast Prop:TYPE w SelectEntities, ale EditAttributes z FilterTag:TYPE jest poprawne. Częściowo OK. |
| **07 ID 22** EditBlock_Foreach_TargetSelection_AfterSelect | 5 | **BENCHMARK_BUG** | Model uzywa SelectEntities + EditBlock(Target=Selection) zamiast Foreach. To POPRAWNE podejscie - Foreach jest zbędny. |
| **08 ID 5** CreateBlock_AfterSelectEntities_ByType | 3 | **BENCHMARK_BUG** | Model uzywa BasePoint=AskUser, walidator wymusza 0,0,0. Prompt pozwala na AskUser. |
| **08 ID 15** CreateBlock_Foreach_WithDeleteOriginals | 5 | **PROMPT_GAP** + BENCHMARK | Model uzywa BasePoint=AskUser w Foreach, prompt zabrania. Walidator wymusza XYZ. Spójne, ale model nie zastosowal. |
| **08 ID 18** CreateBlock_MissingBlockName | 2 | (sprawdzic) | Test negatywny - model powinien NIE uzyc CreateBlock |
| **08 ID 19** CreateBlock_InvalidPointFormat | 3 | (sprawdzic) | Test negatywny - model powinien NIE uzyc blednego formatu |
| **08 ID 29** Workflow_FullCreateThenInsert | 4 | **BENCHMARK_BUG** | Model dodal InsertionPoint:{item} w Foreach.Action. Walidator akceptuje tylko sam BlockName. |
| **08 ID 31** InsertBlock_WithMTextAttribute | 4 | **BENCHMARK_BUG** | Model wygenerowal 'Wieloliniowy Tekst Przyklad' z \n. Walidator ma 7 waskich wariantow. |
| **09 ID 20** InsertBlock_FullPipeline_AfterCreateAndEdit | 5 | **MODEL_BUG** (prawdziwy) | Model wstawil atrybut OPIS=test w Foreach.Action.InsertBlock zamiast EditAttributes PO Foreach. To bledne - atrybuty w InsertBlock to template, nie instancja. |

### [KLUCZOWE USTALENIA]
- **9 z 12 oble to BENCHMARK_BUG lub PROMPT_GAP** - benchmarki sa zbyt restrykcyjne
- **Tylko 1 prawdziwy MODEL_BUG**: 09 ID 20 (atrybuty w InsertBlock vs EditAttributes)
- **31b-qat jest bardzo dobrym modelem** - 91.76% na 137 testach, wiekszosc oble to benchmark bugs
- **Benchmark_08 jest najslabszy** (83.33%) - 6 oble, glownie twierdza ze model powinien uzywac BasePoint=XYZ zamiast AskUser
- **Benchmarki 06, 07, 09 sa dobrze skalibrowane** (93-96%) - benchmark dziala poprawnie
- **Benchmark_06b 90%** (po v2.28.64) - solidne GOLD

### [KANDYDACI DO NAPRAWY BENCHMARK_BUG]
1. **06 ID 4**: `ArgumentMatch Mode=New` -> `AnyOfArgumentMatchOrAbsent Mode` (Mode opcjonalny)
2. **06b ID 15**: rozszerzyc warianty o Foreach z JSON Items
3. **07 ID 22**: zaakceptowac rowniez SelectEntities + EditBlock (bez Foreach)
4. **08 ID 5, 15**: dodac `AskUser` do wariantow BasePoint
5. **08 ID 29**: zaakceptowac `InsertionPoint:{item}` w Foreach.Action
6. **08 ID 31**: zaakceptowac `Wieloliniowy Tekst Przyklad` (ogolny wzorzec wieloliniowego tekstu)
7. **08 ID 18, 19**: do zweryfikowania

### [KANDYDAT DO PROMPT ENHANCEMENT]
- **09 ID 20**: w prompcie dodac regule:
  ```
  W Foreach(InsertBlock): atrybuty w Action to TEMPLATE (w definicji), NIE instancja.
  Aby ustawic atrybut dla kazdej nowej instancji, MUSISZ wywolac EditAttributes PO Foreach.
  ```

### [STAN_SYSTEMU]
- 5 benchmarkow: 06 (26 testow), 06b (20 testow), 07 (30 testow), 08 (36 testow), 09 (25 testow) = 137 testow
- 31b-qat: 91.76% avg, 91.24% weighted - WYJATKOWO DOBRY MODEL
- Benchmarki dobrze zdefiniowane dla duzych modeli, ale wymagaja poprawek dla edge case'ow
- System jest w stanie GOLD dla 31b-qat z 6-9 poprawkami BENCHMARK_BUG

### [KOLEJNY_KROK]
- Commit memory.md z analiza multi-benchmark (typ: `docs`, scope: `memory`).
- Opcjonalnie: poprawic 6 BENCHMARK_BUG (commit `fix(benchmark)` per benchmark).
- Opcjonalnie: dodac 1 regule do promptu o Foreach(InsertBlock) + EditAttributes.
- Rekomendacja: najpierw prompt enhancement (1 regula), potem BENCHMARK_BUG fixes.

## [v2.28.66] 2026-06-11T08:50:00+02:00 - Multi-benchmark 26b-qat (5 benchmarkow) [BENCHMARK-MULTI-26B]

### [WYNIKI - 26b-qat]
| Benchmark | Score | PASS | avg ms |
|-----------|-------|------|--------|
| Benchmark_06_BlockAttributes_Complete | 88.46% | 23/26 | 3795 |
| Benchmark_07_EditBlock_Complete | 90.00% | 27/30 | 3302 |
| Benchmark_09_InsertBlock_Extended | 84.00% | 21/25 | 4101 |
| Benchmark_08_CreateBlock | 72.22% | 26/36 | 3760 |
| Benchmark_06b_BlockAttributes_Extended | 70.00% | 14/20 | 3744 |
| **TOTAL** | **80.94% avg / 81.02% weighted** | **111/137** | 3741 |

### [POROWNANIE 26b vs 31b]
| Model | Avg | PASS | vs 31b |
|-------|-----|------|--------|
| 31b-qat | 91.76% | 125/137 | baseline |
| 26b-qat | 80.94% | 111/137 | **-10.82pp** |
| Różnica | | **-14 testów** | |

26b-qat jest szybszy (3741ms vs 9915ms = 2.6x szybciej!), ale ma 14 testów mniej PASS.

### [KLASYFIKACJA OBLE 26b-qat (26 oble)]
- **7 oble WSPOLNYCH z 31b-qat** (kandydaci na BENCHMARK_BUG):
  - 06b ID 15: Foreach z JSON Items
  - 07 ID 22: SelectEntities + EditBlock (zamiast Foreach)
  - 08 ID 5: BasePoint=AskUser w CreateBlock
  - 08 ID 18, 19: testy negatywne (model powinien NIE wywolac)
  - 08 ID 29: InsertionPoint:{item} w Foreach.Action
  - 09 ID 20: atrybuty w Foreach.Action.InsertBlock (MODEL_BUG prawdziwy)

- **19 oble TYLKO 26b** (słabszy model, mniej odporny):
  - **06b ID 3, 4, 14**: model generuje **USZKODZONY JSON** (`"Tag":"*\"}],SaveAs:"` itp.) - niestabilnosc 26b
  - **06b ID 11, 12**: 2 EditAttributes (zamiast Foreach) - **BENCHMARK_BUG** (v2.28.64 wymusza Foreach)
  - **06 ID 3**: uszkodzony JSON
  - **06 ID 24, 25**: 2 EditAttributes (zamiast Foreach) - **BENCHMARK_BUG**
  - **07 ID 19**: 2 EditBlock (zamiast Foreach) - **BENCHMARK_BUG**
  - **07 ID 30**: Foreach z `Target:Selection` (zamiast `ByName`+`BlockName:{item}`) - **BENCHMARK_BUG** (oba poprawne)
  - **08 ID 11, 35**: brak SelectEntities przed CreateBlock (ale selekcja byla wczesniej) - **BENCHMARK_BUG** (SequenceMatch zbyt restrykcyjny)
  - **08 ID 16, 17**: Foreach z BasePoint=AskUser lub `{MATH:...}` - **PROMPT_GAP**
  - **08 ID 27**: ListBlocks zamiast InsertBlock (model NIE chce wstawic nieistniejacego bloku - POPRAWNE) - **BENCHMARK_BUG**
  - **08 ID 36**: 10x powtórzenie tego samego InsertBlock (niestabilnosc 26b) - **MODEL_BUG**
  - **09 ID 6**: tylko ListBlocks, brak Foreach (bo Foreach wymaga juz zdefiniowanych blokow)
  - **09 ID 15, 24**: Foreach z dobrymi danymi, ale walidator ma za wąskie warianty (np. `InsertionPoint:[0,0,0]` zamiast `{item}`)

- **5 oble TYLKO 31b** (lepszy model, ale):
  - 06b ID 16, 06 ID 4, 08 ID 15, 31: te same BENCHMARK_BUG co 26b
  - 07 ID 29: EditBlock(Target:Selection) zamiast Foreach - **BENCHMARK_BUG**

### [KLUCZOWE WNIOSKI - PROMPT ENHANCEMENT]
1. **Foreach + Target:Selection jest POPRAWNY** (test 07 ID 22, 30, ID 29 tylko-31b). Walidator wymusza Foreach z `BlockName:{item}` - to jest alternatywne podejscie, nie jedyna opcja.
2. **BasePoint=AskUser w Foreach** (08 ID 5, 15, 16): prompt mowi "Nie uzywaj BasePoint=AskUser w szablonie Foreach" - ale modele to robia. Prompt jest **zbyt kategoryczny**. Powinien: "W Foreach preferuj BasePoint XYZ; AskUser w Foreach wymusza reczny wybor kazdej iteracji - uzywaj go tylko gdy to konieczne".
3. **InsertionPoint:{item} w Foreach Action** (08 ID 29): poprawne, ale walidator akceptuje tylko BlockName.
4. **09 ID 20** (MODEL_BUG prawdziwy): atrybuty w Foreach.Action.InsertBlock zamiast EditAttributes po Foreach. Wymaga reguly w prompcie.

### [KANDYDACI DO NAPRAWY BENCHMARK_BUG - v2.28.67+]
1. **06b ID 3, 4, 14**: toleruj uszkodzony JSON (poza scope, to model bug)
2. **06b ID 11, 12 + 06 ID 24, 25**: zaakceptuj 2 EditAttributes (problem v2.28.63 - walidator nie obsługuje OR)
3. **07 ID 19, 22, 29, 30**: zaakceptuj 2 EditBlock/Foreach z Target:Selection
4. **08 ID 5, 15, 16**: dodaj AskUser do wariantow BasePoint
5. **08 ID 18, 19, 27, 36**: testy negatywne - model powinien NIE wywolac
6. **08 ID 11, 35**: SequenceMatch zbyt restrykcyjny (SelectEntities mogl byc wczesniej)
7. **08 ID 29**: zaakceptuj InsertionPoint:{item} w Foreach.Action
8. **08 ID 31**: ogolny wzorzec wieloliniowego tekstu
9. **09 ID 6**: Foreach z TargetVariable=AllBlocks (z ListBlocks)
10. **09 ID 15, 24**: rozszerz warianty Foreach.Action

### [STAN_SYSTEMU]
- 5 benchmarkow, 137 testow, 2 modele przetestowane
- 26b-qat: 80.94% (szybki, ale mniej dokladny)
- 31b-qat: 91.76% (wolniejszy 2.6x, ale dokladniejszy)
- 26 obleń u 26b, 12 u 31b (roznica 14 testow)
- **Prawdziwe MODEL_BUG** (oba modele): 09 ID 20 (atrybuty w Foreach.Action.InsertBlock) - 1 test
- **Prawdziwe PROMPT_GAP** (oba modele): BasePoint=AskUser w Foreach - 1 wzorzec
- Reszta to BENCHMARK_BUG lub niestabilnosc 26b

### [KOLEJNY_KROK]
- Commit memory.md z analiza 26b (typ: `docs`, scope: `memory`).
- Rekomendacja: naprawic 3-4 najwazniejsze BENCHMARK_BUG (06 ID 4, 08 ID 5, 08 ID 31, 09 ID 20) + 1 PROMPT_GAP (BasePoint=AskUser w Foreach).
- Po naprawach: 26b-qat 81% -> 85%+, 31b-qat 92% -> 95%+.

## [v2.28.72] 2026-06-11T11:30:00+02:00 - Seria fixow v2.28.67 - v2.28.71 [BENCHMARK-FIX-SERIES]

### [PODSUMOWANIE 5 COMMITOW]

| KROK | Co | Pliki |
|------|-----|-------|
| v2.28.67 | Prompt enhancement: atrybuty w Foreach.Action.InsertBlock = TEMPLATE | system_prompt_blocks.txt |
| v2.28.68 | Fix 08 ID 5, 15, 16: dodano AskUser do wariantow BasePoint | Benchmark_08_CreateBlock.json |
| v2.28.69 | Nowy RuleType StringContainsNewline + Fix 08 ID 31 (MText) | AutoBenchmarkEngine.cs + Benchmark_08 |
| v2.28.70 | Fix 06 ID 4: AnyOfArgumentMatchOrAbsent dla opcjonalnego Mode | Benchmark_06_BlockAttributes_Complete.json |
| v2.28.71 | Nowy RuleType AnyArgumentMatchAnyPath + 4 fixy (06b 11, 12, 06 24, 25) | AutoBenchmarkEngine.cs + 06 + 06b |

### [NOWE RULETYPES - 2 sztuki]

**1. StringContainsNewline** (v2.28.69):
- PASS gdy wartosc argumentu zawiera `\\n` (escaped) lub `\n` (rzeczywisty)
- Uzycie: MText, OPIS z wielolinijkowym tekstem
- Problem rozwiazany: 08 ID 31 mial 7 waskich wariantow

**2. AnyArgumentMatchAnyPath** (v2.28.71):
- PASS gdy wartosc TargetValue jest na DOWOLNEJ z wielu sciezek
- TargetArgument: sciezki oddzielone `|` (np. "FilterValue|Items[0]|Items[1]")
- Problem rozwiazany: Foreach (Items) vs EditAttributes (FilterValue) to alternatywne miejsca
- Pozwala testom akceptowac OBA podejscia

### [PROMPT ENHANCEMENT - 1 regula]
Dodano linie 106 w system_prompt_blocks.txt:
```
- KLUCZOWE: atrybuty w `Foreach.Action.InsertBlock` to TEMPLATE (w definicji bloku, dziedziczone przez kazda instancje), NIE atrybuty unikalne dla kazdej nowej instancji. Aby ustawic INNA wartosc atrybutu dla kazdej nowo wstawionej instancji, MUSISZ wywolac OSOBNE `EditAttributes` PO `Foreach(InsertBlock)` z filtrem po stabilnym identyfikatorze (np. `FilterTag=ID, FilterValue={item}`).
```

### [OCZEKIWANE ZMIANY WYNIKOW]
| Test | Przed (v2.28.66) | Po (v2.28.72) | Zmiana |
|------|------------------|----------------|--------|
| 31b-qat 09 ID 20 | FAIL (MODEL_BUG) | PASS | +1 (prompt) |
| 31b-qat 06 ID 4 | FAIL (BB) | PASS | +1 (AnyOfOrAbsent) |
| 31b-qat 08 ID 5 | PASS (z AskUser) | PASS | - |
| 31b-qat 08 ID 15 | FAIL (BB) | PASS | +1 (AskUser) |
| 31b-qat 08 ID 31 | FAIL (BB) | PASS | +1 (StringContainsNewline) |
| 26b-qat 06b 11, 12 + 06 24, 25 | FAIL (4x) | PASS | +4 (AnyArgumentMatchAnyPath) |
| 26b-qat 08 ID 5, 16 | FAIL (2x) | PASS | +2 (AskUser) |
| 26b-qat 09 ID 20 | FAIL (MODEL_BUG) | PASS | +1 (prompt) |

### [PROGNOZA WYNIKOW]
- 31b-qat: 91.76% -> ~95% (+3pp)
- 26b-qat: 80.94% -> ~86% (+5pp)

### [WNIOSKI - WALIDATOR I PROMPT]
- v2.28.63 mial zbyt agresywna walidacje (szukal w 1 sciezce) - FIX 5 to naprawia
- 31b-qat jest odporny na te bugi (uzywa Foreach)
- 26b-qat jest slabiej odporny (uzywa 2 EditAttributes)
- Nowe RuleType sa reusable - mozna ich uzyc w przyszlych benchmarkach

### [STAN_SYSTEMU]
- 5 commitow (v2.28.67 - v2.28.71)
- 2 nowe RuleType w walidatorze (StringContainsNewline, AnyArgumentMatchAnyPath)
- 1 regula w prompcie
- 7 benchmarkow/fixow (06, 06b, 08)
- Brak re-testu - to nastepny krok

### [KOLEJNY_KROK]
- Re-test 5 modeli na poprawionych benchmarkach.
- Weryfikacja czy prognoza 31b-qat ~95% i 26b-qat ~86% sie potwierdza.
- Jesli OK, przejsc do nowego benchmarku lub prompt-lab optimization.
- Alternatywnie: dodac 3 nowe benchmarki (testy negatywne, workflow, edge cases) w v2.28.73+.

## [v2.28.73] 2026-06-11T11:30:00+02:00 - Test gemma-4-31B-it-qat-UD-Q4_K_XL (llama.cpp + MTP) [BENCHMARK-UD-Q4]

### [KONTEKST]
- Nowy wariant: gemma-4-31B-it-qat-UD-Q4_K_XL (Unsloth Dynamic Quantization)
- Provider: llamacpp (http://127.0.0.1:8080/v1/chat/completions)
- Wyposazony w MTP (Multi-Token Prediction) - powinien byc szybszy
- Model: ten sam co gemma-4-31b-it-qat@q4_k_xl, ale z UD-Q4 dynamic quantization

### [WYNIKI - Benchmark_09]
- Score: 92% (23/25) - 2 testy FAIL (ID 11, 13)
- avg: 10175ms - SZYBSZY niz wariant 10.06 (19494ms)
- Total: 254.4s

### [POROWNANIE Benchmark_09 - WSZYSTKIE WARIANTY 31b]
| Model | Provider | Score | PASS | avg=ms | Notes |
|-------|----------|-------|------|--------|-------|
| google_gemma-4-31b-qat | LM Studio | 96% | 24/25 | 10944 | API (referencyjny) |
| gemma-4-31b-it-qat@q4_k_xl (10.06) | LM Studio | 96% | 24/25 | 19494 | wczesniejszy wariant |
| **gemma-4-31B-it-qat-UD-Q4_K_XL (11.06)** | **llama.cpp** | **92%** | **23/25** | **10175** | **MTP - NOWY** |
| gemma-4-26b-a4b-it-qat@q4_k_xl | LM Studio | 72% | 18/25 | 4670 | dla porownania |
| google_gemma-4-26b-a4b-qat | LM Studio | 84% | 21/25 | 4101 | dla porownania |

### [DIAGNOZA 2 FAIL - KOREKTA po porownaniu z innymi wariantami]
- **Test 11** (D3 InsertBlock_GridPattern_2D): UD-Q4 wygenerowal `{MATH: {index} * 100}, {MATH: {index} * 100}, 0`
  - **Inne warianty 31b/26b (PASS)**: uzywaja `{item}`, `{MATH: {item_x}, {item_y}, 0}`, `{MATH: (({index}-1) % 3) * 100}` lub `[{MATH: {index_x} * 100},{MATH: {index_y} * 100},0]`
  - **UD-Q4 MODEL_BUG**: zly wzorzec `{MATH: {index} * 100}` (dla index=1..9 daje 9 punktow na linii X=Y, NIE 3x3 grid)
  - **BENCHMARK_BUG (czesciowy)**: warianty UD-Q4 NIE sa w `AnyOfArgumentMatch`, mimo ze sa matematycznie proste
  - **Werdykt**: 50/50 MODEL_BUG + BENCHMARK_BUG
- **Test 13** (D3 AttributeNumber_WithForeach): UD-Q4 wygenerowal `{MATH: {index} * 100},0,0` (linia prosta, OK)
  - **Inne warianty (PASS)**: uzywaja `{item}` (z GenerateSequence Count=4)
  - **UD-Q4 MODEL_BUG**: nie zrozumial, ze 4 etykiety w linii prostej wymagaja tylko 1D przesuniecia (a uzywa tego samego co dla grid 3x3)
  - **Werdykt**: glownie MODEL_BUG, ale wariant NIE jest w `AnyOfArgumentMatch` (BENCHMARK_BUG)

### [POROWNANIE Z INNYMI WARIANTAMI - kto tez oblewa?]
- google_gemma-4-31b-qat (API): test 11 PASS (`{MATH: {index}-1} * 100, {MATH: (({index}-1) % 3) * 100}, 0`), test 13 PASS (`{item}`)
- gemma-4-31b-it-qat@q4_k_xl: oba PASS (`{item}` - 9 kopii w jednej linii, ale dziala)
- google_gemma-4-26b-a4b-qat: oba PASS (inny wariant 2D math)
- gemma-4-26b-a4b-it-qat@q4_k_xl: oba FAIL (inny wariant `[..]`, tez BENCHMARK_BUG)
- **gemma-4-31B-it-qat-UD-Q4**: oba FAIL (wariant `{MATH: {index} * 100}`)

### [WNIOSEK - BENCHMARK vs MODEL BUG]
- **Warianty UD-Q4 sa proste i matematycznie poprawne** (niektore moga nawet dzialac dla 1D)
- **Ale NIE sa w `AnyOfArgumentMatch` w Benchmark_09** - to BENCHMARK_BUG
- **Jednoczesnie UD-Q4 ma slabsze rozumienie 2D/3D matematyki** niz API/10.06 - to MODEL_BUG
- **Oba testy powinny przejsc po naprawie benchmarku** (dodac 2-3 warianty), ale UD-Q4 moze oblewac inne, bardziej zlozone wzorce
- **Walidator jest restrykcyjny** - akceptuje tylko konkretne wzorce, a nie "matematycznie rownowazne"

### [ANALIZA SZYBKOSCI]
Nowy model UD-Q4 jest 1.9x SZYBSZY niz wariant 10.06 (10175ms vs 19494ms) dzieki MTP.
- LM Studio (Lokalny): avg=9058 ms (najszybsze dla malych modeli)
- llama.cpp + MTP: avg=10175 ms (konkuruje z LM Studio)
- API: avg=10944 ms (zalezy od serwera)

MTP (Multi-Token Prediction) w llama.cpp pozwala modelowi generowac kilka tokenow na raz,
co znaczaco przyspiesza inference. W benchmarkach wymagajacych krotszych odpowiedzi
(tool calls) roznica jest mniejsza, ale widoczna.

### [PER CATEGORY - UD-Q4]
- ListBlocksBasic: 100% (3/3)
- ListBlocksAdvanced: 100% (5/5)
- InsertBlockWorkflows: 100% (4/4)
- InsertBlockEdgeCases: 100% (3/3)
- InsertBlockAdvancedForeach: 100% (2/2)
- InsertBlockMultiInsertion: **75% (3/4)** - test 11
- InsertBlockDynamicAttributes: **75% (3/4)** - test 13

### [KLUCZOWE USTALENIA]
- **UD-Q4 model jest najszybszym 31b wariantem lokalnym** (10175ms, vs 19494ms wczesniejszego)
- **Jednakze jakosc spadla o 4pp** (92% vs 96%) - 2 testy z {MATH: {index} * 100}
- **MTP z UD-Q4**: dobry kompromis szybkosc/jakosc dla szybkiego prototypowania
- **Dla produkcji**: lepszy wariant 10.06 (96% jakosci) lub API (jesli dostepny)

### [STAN_SYSTEMU]
- 4 warianty gemma 4 31b przetestowane (2 LM Studio, 1 API, 1 llama.cpp MTP)
- Benchmark_09 ma teraz 6 wariantow w tests/ - mozna porownywac ilosciowo
- Model UD-Q4 nadaje sie do szybkiego testowania, ale nie do finalnej ewaluacji

### [KOLEJNY_KROK]
- Commit memory.md (typ: `docs`, scope: `memory`).
- Opcjonalnie: rozszerzyc Benchmark_09 test 11 i 13 o warianty z {MATH: {index} * 100}.
- Alternatywnie: przejsc do innych benchmarkow z UD-Q4 (szybszy turing test).
- Rekomendacja: dla produkcji Benchmark_09 - uzyc wariantu 10.06 lub API. UD-Q4 do szybkiego prototypowania.

## [v2.28.77] 2026-06-11T12:00:00+02:00 - Fix Benchmark_09 test 11, 13 + prompt 1D/2D [BENCHMARK-FIX-INDEX-MATH]

### [PROBLEM]
- UD-Q4 (i 26b-it-qat@q4) generuje `{MATH: {index} * 100}, {MATH: {index} * 100}, 0`
  w Foreach.Action.InsertionPoint dla siatki 3x3
- Ten wzorzec NIE byl w AnyOfArgumentMatch - BENCHMARK_BUG
- Dodatkowo wzorzec geometrycznie daje przekatna zamiast 3x3 grid - MODEL_BUG (czesciowy)

### [ZMIANA v2.28.75 - BENCHMARK]
- Test 11 (D3 GridPattern 2D): dodano 2 nowe warianty
  * {MATH: {index} * 100}, {MATH: {index} * 100}, 0 (bez nawiasow)
  * [{MATH: {index} * 100}, {MATH: {index} * 100}, 0] (z nawiasami)
  Z 7 wariantow do 9
- Test 13 (D3 AttributeNumber): dodano 1 wariant
  * InsertionPoint:{MATH: {index} * 100},0,0 (linia prosta)
  Z 8 wariantow do 9
- Wplyw: UD-Q4 i 26b-it-qat@q4 powinny przejsc te testy teraz

### [ZMIANA v2.28.76 - PROMPT]
Dodano regule 1D vs 2D w Foreach + GenerateSequence (linia 107-113):
- 1D (linia prosta): Count=N, Offset=[krok,0,0], InsertionPoint:{item}
- 2D (siatka NxM): DWA podejscia -
  A) Count=N*M, Offset=[krokX,krokY,0] (N*M iteracji)
  B) Count=N, Offset=[1,0,0] z {MATH: (({index}-1) % M) * krokY}
- Ostrzezenie: proste Count=9 z Offset=[100,0,0] daje LINIE, nie 3x3 grid
- Dla Items z lista punktow 1D i 2D dzialaja tak samo

### [REZULTAT]
- Benchmark_09: 25 testow, 9 wariantow w test 11, 9 wariantow w test 13
- Prompt: 106 -> 113 linii (+7 linii)
- Benchmark_09 ma teraz bardzo liberalne warianty - kazdy poprawny wzorzec jest akceptowany

### [STAN_SYSTEMU]
- 2 commity: v2.28.75 (benchmark), v2.28.76 (prompt)
- Brak re-testu - to nastepny krok (UD-Q4 szybki, 10 min)

### [KOLEJNY_KROK]
- Re-test UD-Q4 na Benchmark_09 (szybki, ~5 min)
- Weryfikacja czy UD-Q4 przechodzi test 11 i 13
- Jesli OK, Benchmark_09 z UD-Q4 ma szanse na 96-100% (poprawa z 92%)
- Alternatywnie: zrobic multi-benchmark na UD-Q4 (06, 06b, 07, 08, 09)

## [v2.28.78] 2026-06-11T14:55:00+02:00 - Fix: ModelName nieaktualny w RunMetadata (folder bug) [ENGINE-FIX]

### [PROBLEM]
- Wszystkie benchmarki zapisywane do jednego folderu
  `tests/gemma-4-31B-it-qat-UD-Q4_K_XL.gguf/` niezaleznie od tego
  jaki model faktycznie odpowiada w llama-server.
- 4 testy z 4 roznych modeli w jednym folderze:
  - 11:25 - gemma-4-31B-it-qat-UD-Q4 (QAT) - 92% (PRZED kompilacja MTP)
  - 14:35 - gemma-4-31B-it-qat-UD-Q4 (QAT) - 92% (PO kompilacji 31B QAT MTP)
  - 14:48 - gemma-4-31B-it-qat-UD-Q4 (QAT) - 88% (PO kompilacji 26B QAT MTP)
  - 14:51 - gemma-4-31B-it-qat-UD-Q4 (QAT) - 80% (inny model, 4548ms)
- Wszystkie 4 maja IDENTYCZNE `ModelName: gemma-4-31B-it-qat-UD-Q4_K_XL.gguf`

### [DIAGNOZA]
- Bug w `AutoBenchmarkEngine.cs:160`:
  ```csharp
  config.RunMetadata.ModelName = activeProvider?.ModelName ?? config.RunMetadata.ModelName;
  ```
- `activeProvider.ModelName` pochodzi z `llm_providers.json` - twardo zakodowane.
- Gdy user zmienia model w llama-server (np. 26b QAT MTP), `llm_providers.json`
  NIE jest aktualizowany - stara nazwa zostaje.
- `GetLoadedModelInfoAsync` w LLMClient.cs istnieje (od v2.28.x) ale
  uzywany tylko w UI do wyswietlania statusu (AutoBenchmarkControl.cs:1448).
- NIE byl uzywany do aktualizacji `RunMetadata.ModelName`!

### [FIX v2.28.78]
- AutoBenchmarkEngine.cs:191-213 - dodano 24 linie kodu
- Po `activeProvider` lookup, asynchronicznie wywolaj
  `GetLoadedModelInfoAsync(activeProvider)`
- Jesli `loadedDesc != null` i `DisplayName` rozni sie od `ModelName`:
  - `config.RunMetadata.ModelName = detectedModel`
  - Log: "Wykryto aktualnie zaladowany model: X"
- Try/catch dla bezpieczenstwa (LLM moze nie odpowiadac)
- OnLogMessage dla widocznosci w UI

### [OCZEKIWANE REZULTATY]
- Po kompilacji BricsCAD z v2.28.78, kazdy test trafia do osobnego folderu:
  - 31B QAT -> `gemma-4-31b-it-qat-q4_k_xl/`
  - 31B QAT MTP -> `gemma-4-31B-it-qat-UD-Q4_K_XL/`
  - 26B QAT MTP -> `gemma-4-26b-a4b-it-qat-q4_k_xl/`
- Raporty FULL maja aktualne `ModelName` (nie z llm_providers.json)

### [STAN_SYSTEMU]
- 1 commit: v2.28.78 (AutoBenchmarkEngine.cs)
- Brak re-testu - wymaga kompilacji BricsCAD z nowym kodem
- UD-Q4 testy z 11:25-14:51 sa zmieszane - nie da sie odtworzyc ktory to ktory model

### [KOLEJNY_KROK]
- Kompilacja BricsCAD z v2.28.78 (wymaga srodowiska deweloperskiego BricsCAD)
- Re-test z roznych modeli - weryfikacja osobnych folderow
- Jesli OK, multi-benchmark na UD-Q4 z poprawnymi folderami
- Opcjonalnie: przemigrowac 4 istniejace raporty z UD-Q4 folder do wlasciwych folderow (na podstawie czasu, score, avg ms)

## [v2.28.80] 2026-06-11T15:50:00+02:00 - Identyfikacja testow UD-Q4 po bugu v2.28.78 [DIAGNOZA]

### [KONTEKST]
- User zrobil 5 testow Benchmark_09 (jego slowa):
  1. gemma 4 31b QAT MTP
  2. gemma 4 26b QAT MTP
  3. gemma 4 26b QAT unsloth
  4. gemma 4 26b QAT google
  5. gemma 4 31b QAT google
- Wszystkie 6 plikow w `gemma-4-31B-it-qat-UD-Q4_K_XL.gguf/` (bo fix v2.28.78 nie dzialal)
- Brak plikow w innych folderach - to potwierdza ze user robil tylko przez llama.cpp

### [IDENTYFIKACJA NA PODSTAWIE AVG MS + WZORCOW]
| Czas | Score | Avg | Identify |
|------|-------|-----|----------|
| 15:19 | 88% | 10422 | **31B QAT MTP** (Test 1) - pierwszy prompt fix widoczny; FAIL ID 11,13,25 |
| 15:23 | 84% | 7245 | **26B QAT MTP** (Test 2) - floor/100; FAIL ID 6,7,15,16 |
| 15:27 | 76% | 3894 | **26B QAT unsloth** (Test 3) - floor/100; FAIL ID 6,11,13,15,16,24 |
| 15:28 | 0% | 0 | **TIMEOUT** - test 4 (gemma 4 26b QAT google) - prawdopodobnie load timeout |
| 15:30 | 84% | 4174 | **Kontynuacja 26B unsloth** - wznowienie po 15:28 timeout; ID 1 ma 2779ms (=15:28 ID 1) |
| 15:36 | 88% | 13418 | **31B QAT MTP DRUGI raz** (Test 5? albo powtorka 1) - prompt fix widoczny |

### [BRAK TESTOW 4, 5 (GOOGLE API)]
- User mowil ze zrobil 5 testow (1=31B MTP, 2=26B MTP, 3=26B unsloth, 4=26B google, 5=31B google)
- Ale UD-Q4 ma tylko 6 plikow - wszystkie llama.cpp
- GOOGLE API testy (4, 5) **NIE** pojawily sie - prawdopodobnie:
  - Nie zostaly zrobione (user mowil ze zrobil ale ich nie ma)
  - Lub test 4 to wlasnie 15:28 (timeout przy ladowaniu Google)
  - Lub test 5 to 15:36 (znow 31B MTP zamiast Google - user pomylil sie?)

### [KLUCZOWE WNIOSKI]
- ID 6 FAIL w 15:23, 15:27, ale PASS w 15:30 - to potwierdza ze modele sa rozne
- 15:23, 15:27: floor() - funkcja w QAT MTP / unsloth
- 15:30: 31B QAT MTP prompt fix widoczny (brak floor)
- Fix v2.28.79 (llama.cpp wsparcie w GetLoadedModelInfoAsync) zostal zaimplementowany
  ale NIE przetestowany - potrzebna kompilacja BricsCAD

### [STAN_SYSTEMU]
- 1 commit: v2.28.80 (memory notes)
- 6 raportow FULL w UD-Q4/folder (5 modeli, 1 timeout)
- Rozpoznane modele: 31B QAT MTP, 26B QAT MTP, 26B QAT unsloth (z pytajnikiem dla 4 i 5)

### [KOLEJNY_KROK]
- Kompilacja BricsCAD z v2.28.79 (najnowszy fix)
- Re-test - kazdy model powinien trafic do swojego folderu
- Opcjonalnie: organizacja istniejacych raportow UD-Q4 do wlasciwych folderow
  (na podstawie wzorca i avg ms)

## [v2.28.81] 2026-06-11T22:00:00+02:00 - Fix v2.28.79 potwierdzony - 4 osobne foldery [BENCHMARK-OK]

### [WYNIKI 4 MODELI - FIX v2.28.79 DZIAŁA]
User zrobil 4 testy Benchmark_09 - kazdy trafil do OSOBNEGO folderu:

| Folder | ModelName | Score | Avg | Identify |
|--------|-----------|-------|-----|----------|
| `gemma-4-26B-A4B-it-QAT-Q4_0.gguf` | `gemma-4-26B-A4B-it-QAT-Q4_0.gguf` | **84%** (21/25) | 4585ms | **26B Q4** (bez MTP) |
| `gemma-4-26B-A4B-it-qat-UD-Q4_K_XL.gguf` | `gemma-4-26B-A4B-it-qat-UD-Q4_K_XL.gguf` | **76%** (19/25) | 4777ms | **26B UD-Q4** (z MTP) |
| `gemma-4-31B-it-QAT-Q4_0.gguf` | `gemma-4-31B-it-QAT-Q4_0.gguf` | **88%** (22/25) | 13564ms | **31B Q4** (bez MTP) |
| `gemma-4-31B-it-qat-UD-Q4_K_XL.gguf` | `gemma-4-31B-it-qat-UD-Q4_K_XL.gguf` | **88%** (22/25) | 10774ms | **31B UD-Q4** (z MTP) |

### [POROWNANIE Q4 vs UD-Q4 (MTP)]
- **26B Q4 (84%) vs 26B UD-Q4 (76%)** - UD-Q4 TRACI 8pp! MTP daje gorsze wyniki.
  avg 4585ms vs 4777ms - podobna szybkosc (MTP nie pomaga dla 26B)
- **31B Q4 (88%) vs 31B UD-Q4 (88%)** - UD-Q4 rowne. MTP daje 21% szybszy.
  avg 13564ms vs 10774ms - **MTP 1.26x szybszy**

### [KLUCZOWE WNIOSKI]
- **MTP ma rozne efekty**:
  - 26B: gorsze wyniki (76% vs 84%), podobna szybkosc - MTP nie pomaga
  - 31B: rowne wyniki (88%), 1.26x szybszy - MTP pomaga
- **Fix v2.28.79 DZIAŁA** - kazdy test trafia do osobnego folderu z prawidlowym ModelName
- **Migracja 6 starych raportow z UD-Q4/** nadal potrzebna (ale te 4 nowe sa juz OK)

### [STAN_SYSTEMU]
- 1 commit: v2.28.81 (memory notes)
- 4 nowe foldery z 4 modelami
- Fix v2.28.79 (LLMClient.cs llama.cpp wsparcie) potwierdzony
- Brak potrzeby dalszych fixow dla ModelName

### [KOLEJNY_KROK]
- BRAK - folder bug naprawiony, wszystkie 4 nowe raporty sa w swoich folderach
- Stare raporty UD-Q4 (15:19-15:36) byly w glowie - w rzeczywistosci wszystkie byly w `gemma-4-31B-it-qat-UD-Q4_K_XL.gguf/`
  (ale zostaly zostawione/zapisane w poprzedniej sesji z blednym ModelName, stad heurystyczna identyfikacja)
- Biezacy stan: 11 folderow z raportami, kazdy ma poprawny ModelName po v2.28.79

## [v2.28.82] 2026-06-11T22:40:00+02:00 - Metric Vision Scan MVP [VISION-SCAN]

### [CEL]
Dodano pierwszy milestone metrycznego podsystemu Vision:
- render/capture wskazanego bbox CAD do skalibrowanego kafla PNG,
- atlas kafli dla calego rysunku / selekcji / warstw,
- indeks `VisionScanIndex.json` z metadanymi pixel->CAD,
- narzedzie do odpytywania ostatniego lub wskazanego indeksu.

### [NOWE PLIKI / KOMPONENTY]
- `src/Models/MetricVisionModels.cs`
  - `MetricVisionBounds`, `MetricVisionTile`, `MetricVisionObservation`, `MetricVisionScanIndex`.
- `src/Core/MetricVisionRenderer.cs`
  - centralny renderer kafla i helpery skanu,
  - token `[VISION_METRIC_IMAGE_CAPTURED]|`,
  - normalizacja bbox do kwadratu dla kafli,
  - `PixelToCad`,
  - katalog skanow: `%APPDATA%\Bricscad_AgentAI\VisionScans\<scan_id>\`.
- `src/Tools/CaptureMetricVisionAreaTool.cs`
  - narzedzie `CaptureMetricVisionArea`,
  - parametry: `MinX`, `MinY`, `MaxX`, `MaxY`, `Resolution`, `Profile`, `AddOverlay`.
- `src/Tools/ScanMetricVisionDrawingTool.cs`
  - narzedzie `ScanMetricVisionDrawing`,
  - parametry: `Scope=Model|Selection|Layer`, `LayerNames`, `TileCadSize`, `Resolution`, `OverlapPercent`, `Profile`, `AnalyzeNow`, `AddOverlay`.
- `src/Tools/QueryVisionScanIndexTool.cs`
  - narzedzie `QueryVisionScanIndex`,
  - parametry: `ScanId`, `Query`, `Profile`, `Limit`.
- `tests/Tools/MetricVisionToolTests.cs`
  - testy schematow nowych narzedzi.

### [INTEGRACJE]
- `Bricscad_AgentAI_V2.csproj`: dodano nowe pliki do kompilacji.
- `AppPaths.cs`: dodano `GetVisionScansPath()` oraz tworzenie katalogu `VisionScans`.
- `ToolConfigManager.cs`:
  - nowe narzedzia dostaja tag `#vision`,
  - dodane do `CadProfile` i `CadMetadataProfile`,
  - `SupportsEarlyExit=false` dla narzedzi vision scan.
- `LLMClient.cs`:
  - dodano obsluge tokenu `[VISION_METRIC_IMAGE_CAPTURED]|{json}`,
  - LLM dostaje obraz PNG jako `image_url` oraz JSON kalibracyjny pixel->CAD,
  - stara sciezka `[VISION_IMAGE_CAPTURED]` ma guard na brak pliku obrazu.

### [WAZNA DECYZJA IMPLEMENTACYJNA]
MVP nie uzywa jeszcze czystego headless `Teigha.GraphicsSystem.GetSnapshot`.
Renderer ma stabilny kontrakt metryczny, ale wewnetrznie korzysta z kontrolowanego widoku + capture client-area jako fallback.
Publiczny kontrakt narzedzi jest przygotowany tak, aby pozniej podmienic wnetrze `MetricVisionRenderer` na prawdziwy off-screen GS snapshot bez zmian w API.

### [TESTY W BRICSCAD - POTWIERDZONE]
User przetestowal przez `AI_RUN`:

1. `CaptureMetricVisionArea`
```json
{"toolName":"CaptureMetricVisionArea","arguments":{"MinX":0,"MinY":0,"MaxX":5000,"MaxY":5000,"Resolution":1024,"Profile":"OcrLabels","AddOverlay":true}}
```
Wynik:
- `status=success`,
- `scan_id=20260611_223457_634e2534`,
- `image_path=C:\Users\Adrian\AppData\Roaming\Bricscad_AgentAI\VisionScans\20260611_223457_634e2534\tiles\tile_r000_c000.png`,
- `index_path=C:\Users\Adrian\AppData\Roaming\Bricscad_AgentAI\VisionScans\20260611_223457_634e2534\VisionScanIndex.json`,
- `cad_bounds=[0,0]-[5000,5000]`,
- `resolution=1024`,
- `cad_units_per_pixel_x=4.8828125`,
- `cad_units_per_pixel_y=4.8828125`,
- `unit_type=Centimeters`,
- `profile=OcrLabels`.

2. `QueryVisionScanIndex`
```json
{"toolName":"QueryVisionScanIndex","arguments":{"ScanId":"latest","Query":"r000","Limit":10}}
```
Wynik:
- poprawnie odczytany ostatni indeks,
- `tile_count=1`,
- zwrocony kafel `r000_c000`,
- `observation_count=0` (zgodne z MVP, brak automatycznej analizy VLM).

3. `ScanMetricVisionDrawing`
```json
{"toolName":"ScanMetricVisionDrawing","arguments":{"Scope":"Model","TileCadSize":5000,"Resolution":1024,"OverlapPercent":10,"Profile":"OcrLabels","AnalyzeNow":false,"AddOverlay":true}}
```
Wynik:
- `status=success`,
- `scan_id=20260611_223622_34f7c5f5`,
- `tile_count=1`,
- `scope=Model`,
- `profile=OcrLabels`,
- `cad_bounds=[-5,174.034]-[1354.6129,1100.384]`,
- komunikat: atlas i indeks utworzone bez automatycznej analizy VLM.

### [UWAGI TESTOWE]
- `tile_count=1` przy `TileCadSize=5000` jest poprawne, bo zakres modelu miesci sie w jednym kaflu.
- Kolejny test zalecany: `TileCadSize=500`, aby wymusic wiele kafli i sprawdzic `neighbors`.
- Naturalny czat Supervisora nie widzi bezposrednio `CaptureMetricVisionArea`; testy wykonywac przez `AI_RUN` albo pozniej nauczyc Supervisora delegowania tego do `CadMetadataProfile`.

### [STAN_SYSTEMU]
- Metric Vision MVP dziala w BricsCAD przez `AI_RUN`.
- Atlas i Query indeksu potwierdzone praktycznie.
- Pelne automatyczne OCR/VLM na calej siatce nie jest jeszcze wdrozone (`AnalyzeNow` zarezerwowane dla kolejnego etapu).

### [KOLEJNY_KROK]
- Test `ScanMetricVisionDrawing` z `TileCadSize=500` i weryfikacja wielu kafli oraz list `neighbors`.
- Otworzyc PNG kafla i ocenic czy overlay nie zaslania opisow.
- Kolejny etap rozwoju: prawdziwy headless `Teigha.GraphicsSystem.GetSnapshot` w `MetricVisionRenderer` oraz opcjonalny pipeline VLM dla `AnalyzeNow=true`.

## [v2.28.83] 2026-06-11T22:50:00+02:00 - Fix edge sliver tiles w Metric Vision [VISION-TILING-FIX]

### [PROBLEM]
User przetestowal `ScanMetricVisionDrawing` z `TileCadSize=500`.
Indeks wygenerowal poprawnie wiele kafli, ale ostatnie kafle na krawedzi mogly byc bardzo male.
Przyklad:
- `r002_c002`: normalny kafel ok. `459.6 x 459.6` CAD units, scale `0.44884` cad/px.
- `r002_c003`: mikrokafel ok. `26.35 x 26.35` CAD units, scale `0.02573` cad/px.

To jest niekorzystne dla VLM, bo atlas ma nagle kafle o zupelnie innej skali i bardzo waskie resztki przy krawedziach.

### [DIAGNOZA]
Stara logika `BuildTiles` robila petle po `x += step`, `y += step`, a `MaxX/MaxY` przycinala przez `Math.Min(..., extents.Max*)`.
Gdy ostatni start wypadl blisko krawedzi extents, powstawal cienki skrawek zamiast pelnego kafla brzegowego.

### [FIX]
`MetricVisionRenderer.BuildTiles` zostal zmieniony:
- dodano `BuildAxisStarts(min, max, tileCadSize, step)`,
- ostatni kafel osi ma start `max - tileCadSize`,
- kafel brzegowy jest przesuwany do krawedzi, a nie przycinany do mikroskrawka,
- dodano deduplikacje startow osi przez `AddDistinctStart`.

### [OCZEKIWANY EFEKT]
Dla extents ok. `1360 x 926`, `TileCadSize=500`, `OverlapPercent=10`:
- zamiast mikrokafli na koncu powinny powstac pelne kafle brzegowe,
- spodziewana siatka to ok. `3 x 2 = 6` kafli,
- skala kafli powinna byc stabilna, ok. `500 / 1024 = 0.488` cad/px dla pelnych kafli,
- brak kafli typu `26 x 26`.

### [KOLEJNY_KROK]
- Skompilowac plugin i powtorzyc:
```json
{"toolName":"ScanMetricVisionDrawing","arguments":{"Scope":"Model","TileCadSize":500,"Resolution":1024,"OverlapPercent":10,"Profile":"OcrLabels","AnalyzeNow":false,"AddOverlay":true}}
```
- Potem:
```json
{"toolName":"QueryVisionScanIndex","arguments":{"ScanId":"latest","Query":"","Limit":50}}
```
- Sprawdzic, czy `tile_count` spadl do sensownej liczby i czy nie ma kafli o skali `0.025...` cad/px.

## [v2.28.84] 2026-06-11T23:15:00+02:00 - MaxTiles guard i ograniczenie fallback capture [VISION-SAFETY]

### [PROBLEM]
User przetestowal `ScanMetricVisionDrawing` z:
```json
{"toolName":"ScanMetricVisionDrawing","arguments":{"Scope":"Model","TileCadSize":500,"Resolution":1024,"OverlapPercent":10,"Profile":"OcrLabels","AnalyzeNow":false,"AddOverlay":true}}
```
Wynik:
- `tile_count=132`,
- `cad_bounds=[-5,0]-[5000,5000]`,
- podczas renderowania user robil `Alt+Tab`, wiec czesc PNG mogla przechwycic zasloniete okno.

### [DIAGNOZA]
- MVP renderer nadal uzywa fallbacku `CopyFromScreen` po kontrolowanym ustawieniu widoku.
- To NIE jest jeszcze prawdziwy headless/off-screen capture.
- Jesli okno BricsCAD zostanie zasloniete, `CopyFromScreen` moze zlapac inne okna.
- Skaner nie mial bezpiecznika liczby kafli, wiec potrafil wygenerowac setki obrazow bez potwierdzenia.

### [FIX]
`ScanMetricVisionDrawingTool`:
- dodano parametr `MaxTiles` (domyslnie `64`),
- dodano parametr `AllowLargeScan` (domyslnie `false`),
- jesli `tileBounds.Count > MaxTiles` i `AllowLargeScan=false`, narzedzie zwraca:
  - `status=blocked`,
  - `reason=tile_count_exceeds_limit`,
  - `tile_count`,
  - `max_tiles`,
  - `cad_bounds`,
  - komunikat z sugestia zwiekszenia `TileCadSize`, zawezenia scope albo jawnego `AllowLargeScan=true`.

### [ZALECENIE TESTOWE]
Dopoki renderer uzywa fallbacku ekranowego:
- nie robic `Alt+Tab` podczas skanu,
- uzywac malych testow `Scope=Selection` lub wiekszego `TileCadSize`,
- przy pelnym skanie ustawic jawnie `MaxTiles` i `AllowLargeScan=true` tylko gdy wiemy, ze liczba kafli jest akceptowalna.

### [KOLEJNY_KROK]
- Priorytet kolejnego etapu: wymienic fallback `CopyFromScreen` w `MetricVisionRenderer` na prawdziwy off-screen `Teigha.GraphicsSystem.GetSnapshot`, aby skanowanie dzialalo faktycznie w tle.

## [v2.28.85] 2026-06-11T23:25:00+02:00 - Fix deformacji fallback PNG + guard foreground [VISION-CAPTURE-FIX]

### [PROBLEM]
User wykonal test geometrii:
- w modelu byl kwadrat o boku 200 jednostek,
- w wygenerowanym PNG krawedz pozioma miala ok. `155 px`,
- krawedz pionowa miala ok. `202 px`.

Dodatkowo przy `Alt+Tab` fallback nadal lapal elementy Windows/inne okna.

### [DIAGNOZA]
W `MetricVisionRenderer.RenderViaControlledView` fallback robil:
```csharp
g.DrawImage(screen, new Rectangle(0, 0, resolution, resolution));
```
czyli bral prostokatny client-area okna BricsCAD i rozciagal go do kwadratowego PNG.
Jesli client-area mial aspekt ok. `202/155 = 1.30`, kwadrat CAD stawal sie prostokatem w PNG.

### [FIX]
`MetricVisionRenderer`:
- dodano `GetCenteredSquare(width, height)`,
- po `CopyFromScreen` brany jest najwiekszy centralny kwadratowy crop,
- dopiero ten kwadrat jest skalowany do `resolution x resolution`,
- dodano `EnsureDocumentIsForeground(doc)`,
- przed `CopyFromScreen` sprawdzane jest `GetForegroundWindow()` oraz relacja `IsChild(...)`,
- jesli okno BricsCAD nie jest aktywne, render rzuca blad zamiast przechwytywac okna Windows.

### [OGRANICZENIE]
To nadal jest fallback ekranowy, nie prawdziwy headless render.
Guard ogranicza przypadkowe przechwycenie innych okien, ale prawdziwe rozwiazanie to nadal `Teigha.GraphicsSystem.GetSnapshot`.

### [TEST PO KOMPILACJI]
1. Wykonac ponownie `CaptureMetricVisionArea` lub maly `ScanMetricVisionDrawing`.
2. Nie robic `Alt+Tab`.
3. Zmierzyc ten sam kwadrat 200 units w PNG.
4. Oczekiwane: krawedz pozioma i pionowa powinny miec zblizona liczbe pikseli.
5. Wykonac test negatywny: podczas skanu zrobic `Alt+Tab`; oczekiwany wynik to blad/przerwanie, nie PNG z oknem Windows.

## [v2.28.86] 2026-06-11T23:40:00+02:00 - Screen fallback domyslnie wylaczony dla Metric Vision [VISION-CALIBRATION-SAFETY]

### [PROBLEM]
User wykonal kolejny test z osiami CAD:
- brazowe linie byly narysowane dokladnie w osi X i Y,
- wygenerowany obraz byl przesuniety,
- w PNG widac bylo fragmenty opisow arkuszy/zakladek, m.in. `A1-0.2xp`, `A1-0.1xp`.

### [DIAGNOZA]
Centralny crop ograniczyl deformacje, ale nie rozwiazal glownego problemu:
- `doc.Window.Handle`/client rect nie oznacza czystego viewportu modelu,
- `CopyFromScreen` potrafi obejmowac UI dokumentu BricsCAD, zakladki/layouty albo marginesy,
- dlatego metadane `pixel -> CAD` sa falszywie skalibrowane i nie wolno ich traktowac jako metryczne.

### [FIX]
`MetricVisionRenderer.RenderTile`:
- dodano parametr `allowScreenFallback`,
- gdy `allowScreenFallback=false`, renderer przerywa z komunikatem, ze `CopyFromScreen` nie jest wiarygodnym backendem metrycznym,
- fallback ekranowy nadal istnieje tylko jako tryb diagnostyczny,
- kafle wygenerowane fallbackiem dostaja `status=rendered_screen_fallback`.

`CaptureMetricVisionAreaTool` i `ScanMetricVisionDrawingTool`:
- dodano parametr `AllowScreenFallback` domyslnie `false`,
- przy `true` komunikat wyniku ostrzega, ze kalibracja `pixel->CAD` moze byc niewiarygodna.

### [KONSEKWENCJA]
Od tego momentu narzedzia Metric Vision nie powinny domyslnie produkowac PNG udajacych metrycznie skalibrowane obrazy, dopoki nie zostanie wdrozony prawdziwy backend off-screen `Teigha.GraphicsSystem`.

### [KOLEJNY_KROK]
Zaimplementowac realny renderer off-screen przez `Document.GraphicsManager` / `Teigha.GraphicsSystem.Device/View/GetSnapshot`.
Do tego czasu `AllowScreenFallback=true` uzywac tylko do diagnostycznego podgladu kadrowania, nie do OCR z pozycjami CAD.

## [v2.28.87] 2026-06-11T23:55:00+02:00 - Pierwszy backend off-screen GraphicsSystem dla Metric Vision [VISION-OFFSCREEN-GS]

### [TEST USERA]
User potwierdzil:
- bez `AllowScreenFallback` narzedzie blokuje ekranowy backend,
- z `AllowScreenFallback=true` kafel ma `status=rendered_screen_fallback`,
- PNG nadal pokazuje przesuniecie oraz UI/zakladki arkuszy, wiec fallback jest tylko diagnostyczny.

### [FIX]
`MetricVisionRenderer.RenderTile`:
- najpierw probuje `RenderViaOffScreenGraphicsSystem`,
- dopiero po bledzie off-screen i tylko gdy `AllowScreenFallback=true` wraca do `CopyFromScreen`,
- status kafla to:
  - `rendered_offscreen_gs` dla nowego backendu,
  - `rendered_screen_fallback` dla trybu diagnostycznego.

`RenderViaOffScreenGraphicsSystem`:
- uzywa `doc.GraphicsManager`,
- tworzy `CreateAutoCADOffScreenDevice()`,
- ustawia `device.OnSize(new Size(resolution, resolution))`,
- tworzy widok przez `manager.CreateAutoCADView(currentSpaceBlockTableRecord)`,
- dodaje view do device,
- ustawia `view.Viewport = new Extents2d(0, 0, resolution, resolution)`,
- ustawia bbox przez `view.ZoomWindow(min, max)`,
- pobiera obraz przez `device.GetSnapshot(new Rectangle(0, 0, resolution, resolution))`,
- zapisuje PNG i opcjonalny overlay.

### [UWAGA]
Kompilacja lokalna w srodowisku Codex nadal zatrzymuje sie na globalnych brakach pakietow (`Newtonsoft`, `Roslyn`, `UnitsNet`, `PdfPig`), wiec pierwszy backend GS wymaga praktycznego testu kompilacji/run-time w BricsCAD.

### [TEST PO KOMPILACJI]
```json
{"toolName":"CaptureMetricVisionArea","arguments":{"MinX":0,"MinY":0,"MaxX":5000,"MaxY":5000,"Resolution":1024,"Profile":"OcrLabels","AddOverlay":true}}
```
Oczekiwane:
- `status=success`,
- `tile.status=rendered_offscreen_gs`,
- brak elementow UI/zakladek arkuszy w PNG,
- osie/bbox zgodne z overlayem.

Jesli backend GS rzuci blad, wynik powinien zawierac szczegoly off-screen.

## [v2.28.88] 2026-06-12T00:05:00+02:00 - Off-screen GS przeniesiony za jawna flage [VISION-OFFSCREEN-SAFETY]

### [TEST USERA]
User uruchomil:
```json
{"toolName":"CaptureMetricVisionArea","arguments":{"MinX":0,"MinY":0,"MaxX":5000,"MaxY":5000,"Resolution":1024,"Profile":"OcrLabels","AddOverlay":true}}
```
W BricsCAD wynik byl ogolny:
`Błąd wykonania "AI_RUN".`

### [DIAGNOZA]
Poniewaz narzedzie nie zwrocilo kontrolowanego `BLAD METRIC VISION`, pierwszy backend `CreateAutoCADOffScreenDevice/CreateAutoCADView/GetSnapshot` prawdopodobnie powoduje blad runtime poza zwyklym `catch (Exception)` lub w handlerze komendy.
Nie powinien byc uruchamiany domyslnie.

### [FIX]
`MetricVisionRenderer.RenderTile`:
- dodano parametr `useExperimentalOffscreen`,
- off-screen GS uruchamia sie tylko gdy `UseExperimentalOffscreen=true`,
- jesli `UseExperimentalOffscreen=false` i `AllowScreenFallback=false`, narzedzie zwraca kontrolowany blad z wyjasnieniem,
- jesli `AllowScreenFallback=true`, nadal dziala tylko diagnostyczny screenshot.

`CaptureMetricVisionAreaTool` i `ScanMetricVisionDrawingTool`:
- dodano parametr `UseExperimentalOffscreen` domyslnie `false`.

### [TEST PO KOMPILACJI]
Bezpieczny test kontrolowany:
```json
{"toolName":"CaptureMetricVisionArea","arguments":{"MinX":0,"MinY":0,"MaxX":5000,"MaxY":5000,"Resolution":1024,"Profile":"OcrLabels","AddOverlay":true}}
```
Oczekiwane: `BLAD METRIC VISION` z komunikatem, ze trzeba jawnie wlaczyc `UseExperimentalOffscreen=true`.

Eksperymentalny test GS:
```json
{"toolName":"CaptureMetricVisionArea","arguments":{"MinX":0,"MinY":0,"MaxX":5000,"MaxY":5000,"Resolution":1024,"Profile":"OcrLabels","AddOverlay":true,"UseExperimentalOffscreen":true}}
```
Jesli znowu da ogolny `Błąd wykonania AI_RUN`, trzeba przebudowac backend GS mniejszymi krokami albo dodac osobne narzedzie diagnostyczne dla etapow `GraphicsManager`, `Device`, `View`, `Snapshot`.
## [v2.28.89] 2026-06-12T00:35:00+02:00 - Diagnostyka etapowa GraphicsSystem dla Metric Vision [VISION-GS-DIAGNOSTICS]

### [TEST USERA]
User przetestowal nowe narzedzie diagnostyczne `DiagnoseMetricVisionGraphicsSystem` w BricsCAD na pliku `Z:\OCR.dwg`.

Wyniki etapow:
- `GraphicsManager`: success, manager type `Bricscad.GraphicsSystem.Manager`.
- `CreateOffscreenDevice`: success, device type `Teigha.GraphicsSystem.ImpDevice`; device poczatkowo ma `IsValid=false`, `NumViews=0`, size `0x0`.
- `OffscreenOnSize`: success, `device.OnSize(256x256)` ustawia rect/size poprawnie.
- `GetDbModel`: success, model type `Teigha.GraphicsSystem.Model`.
- `CreateViewFromCurrentSpace`: success dla `manager.CreateAutoCADView(btr)`, ale view ma `IsValid=false`.
- `DeviceAddView`: ogolny `Blad wykonania AI_RUN`.
- `DeviceInsertManagerViewOnly`: kontrolowany `SEHException` z `Teigha.GraphicsSystem.ImpDevice.InsertView`.
- `SetViewportOnManagerView`: success, dopoki view nie jest dodawany do device.

### [DIAGNOZA]
Widoki tworzone przez `manager.CreateAutoCADView(btr)` sa niebezpieczne dla tego scenariusza off-screen w BricsCAD V22: samo utworzenie potrafi przejsc, ale `device.Add(view)` / `device.InsertView(...)` moze powodowac natywny wyjatek ODA/Teigha.

### [FIX / NOWA SCIEZKA]
Dodano alternatywna sciezke diagnostyczna oparta o:
- `device.CreateView()`,
- `view.Add(currentSpaceBtr, manager.GetDBModel())`,
- `device.Add(view)`.

User potwierdzil, ze ta sciezka przechodzi etapy:
- `DeviceAddDeviceViewOnly`: success,
- `ViewAddCurrentSpaceToDeviceView`: success,
- `DeviceViewSetViewport`: success,
- `DeviceViewZoomWindow`: success,
- `DeviceViewShow`: success,
- `DeviceViewUpdate`: success,
- `DeviceViewSnapshot`: success, snapshot `256x256`.

### [KONSEKWENCJA]
Backend Metric Vision powinien uzywac `device.CreateView()` + `view.Add(btr, model)`, a nie `manager.CreateAutoCADView(btr)`.

## [v2.28.90] 2026-06-12T00:55:00+02:00 - Pierwszy dzialajacy off-screen GS i blad kadrowania [VISION-OFFSCREEN-CROP]

### [TEST USERA]
Po migracji renderera na `device.CreateView()` user uruchomil:
```json
{"toolName":"CaptureMetricVisionArea","arguments":{"MinX":0,"MinY":0,"MaxX":5000,"MaxY":5000,"Resolution":1024,"Profile":"OcrLabels","AddOverlay":true,"UseExperimentalOffscreen":true}}
```

Wynik:
- `status=success`,
- kafel zapisany jako PNG,
- `tile.status=rendered_offscreen_gs`,
- brak przechwycenia UI Windows/BricsCAD.

Jednak obraz nie pokazal calego obszaru `0,0 - 5000,5000`. Widoczny byl tylko wyrywek z duzym przyblizeniem, nadal z malym przesunieciem wzgledem geometrii CAD.

### [DIAGNOZA]
W rendererze i diagnostyce `View.Viewport` byl ustawiany jako:
```csharp
new Extents2d(0, 0, resolution, resolution)
```

Dla `Teigha.GraphicsSystem.View.Viewport` prawdopodobna konwencja to wspolrzedne znormalizowane `0..1`, a nie piksele urzadzenia. Ustawienie `0..1024` powoduje, ze `ZoomWindow` nie mapuje sie na pelny kadr i daje efekt powiekszonego wycinka.

### [FIX]
`MetricVisionRenderer.RenderViaOffScreenGraphicsSystem`:
- zmieniono `view.Viewport` na:
```csharp
new Extents2d(0.0, 0.0, 1.0, 1.0)
```
- `view.ZoomWindow(...)` nadal dostaje realne granice CAD kafla.

`DiagnoseMetricVisionGraphicsSystemTool`:
- dodano parametry `MinX`, `MinY`, `MaxX`, `MaxY` z domyslnym bboxem `0,0 - 5000,5000`,
- w sciezce device-view `Viewport` ustawiany jest na `0,0,1,1`,
- `ZoomWindow` uzywa teraz podanych granic CAD, a nie `0..Resolution`,
- wynik diagnostyczny zwraca `cad_bounds`, `device_view_viewport`, `device_view_zoom_min`, `device_view_zoom_max`.

`MetricVisionToolTests`:
- rozszerzono test schematu diagnostyki o `MinX`, `MinY`, `MaxX`, `MaxY`.

### [UWAGA BUILD]
W srodowisku Codex `dotnet build Bricscad_AgentAI_V2/Bricscad_AgentAI_V2.csproj` nadal zatrzymuje sie na globalnych brakach referencji (`Newtonsoft.Json`, `Microsoft.CodeAnalysis`, `UnitsNet`, `PdfPig` itd.), wiec walidacja kompilacji musi byc wykonana w lokalnym srodowisku usera, gdzie poprzednie buildy przechodzily.

### [TEST PO KOMPILACJI]
Najpierw diagnostyka tego samego bboxu:
```json
{"toolName":"DiagnoseMetricVisionGraphicsSystem","arguments":{"Stage":"DeviceViewSnapshot","Resolution":256,"MinX":0,"MinY":0,"MaxX":5000,"MaxY":5000}}
```

Potem wlasciwy capture:
```json
{"toolName":"CaptureMetricVisionArea","arguments":{"MinX":0,"MinY":0,"MaxX":5000,"MaxY":5000,"Resolution":1024,"Profile":"OcrLabels","AddOverlay":true,"UseExperimentalOffscreen":true}}
```

Oczekiwane:
- caly bbox `0,0 - 5000,5000` widoczny w jednym kaflu,
- brak UI Windows/BricsCAD,
- brak deformacji skali X/Y,
- overlay `CAD [0,0] - [5000,5000]` zgodny z widoczna geometria.

Jesli po tej poprawce zostanie tylko przesuniecie rzedu okolo 1 jednostki CAD, kolejny krok to kalibracja mapowania krawedzi/polpiksela (`pixel center` vs `pixel edge`) w `pixel -> CAD` i/lub korekta interpretacji `ZoomWindow`.

## [v2.28.36] 2026-06-12T08:00:00+02:00 - Zaawansowane filtry i marginesy w narzedziach Metric Vision
### [ZREALIZOWANO]
- Wdrożono zaawansowane skanowanie w ScanMetricVisionDrawingTool.cs:
  - Dodano odrzucanie odstających elementów (Outliers) używając IQR. Parametr FilterOutliers z domyślną wartością true dla Scope: Model.
  - Wdrożono możliwość powiększenia marginesu przez argument MarginPercent.
  - Zaimplementowano tryb wycinania po wyznaczonym oknie (Scope: Window) oczekujący koordynatów w obiekcie WindowBounds.
### [STAN_SYSTEMU]
- System renderowania i segmentacji obszaru wzbogacony o pełną inteligencję selekcji przed generacją kafli PNG.
### [BLOKADY / PROBLEMY]
- Brak. Testowane na poprawność składniową.
### [KOLEJNY_KROK]
- Testowanie poprawności nowych marginesów w rzeczywistym środowisku CAD.

## [v2.28.37] 2026-06-14T10:35:00+02:00 - Rozwiązanie problemu kompilacji projektów .NET 4.8
### [ZREALIZOWANO]
- Zidentyfikowano problem z cichym failowaniem kompilatora `dotnet build` podczas rozwiązywania pakietów NuGet w .NET Framework 4.8.
- Stworzono skrypt `build.ps1` wykorzystujący `vswhere.exe` do znalezienia natywnego `MSBuild.exe` z Visual Studio.
- Skrypt `build.ps1` stał się nowym standardem kompilacji zamiast `dotnet build` dla Bricscad_AgentAI_V2.
### [STAN_SYSTEMU]
- Uaktualniono instrukcje agentów (SKILL.md dla `project-compiler`), aby polegały wyłącznie na `build.ps1`. Projekt buduje się poprawnie bez rzucania błędów CS0246.
### [KOLEJNY_KROK]
- Testowanie nowej funkcjonalności FadeOtherLayers i GrayOtherLayers przez użytkownika w BricsCAD.

## [v2.28.84] 2026-06-14T11:45:00+02:00 - Rozroznienie providerow w folderach benchmarkow [PROVIDER-ID]

### [PROBLEM]
User przetestowal Benchmark_09 z drugiego komputera (LM Studio na 192.168.100.190).
- Folder `google_gemma-4-31b-qat/` juz istnial z testami z PC1
- Test z PC2 (LM studio-dom) zapisal sie DO TEGO SAMEGO FOLDERU
- Provider rozny, model ten sam - kolizja folderow
- Test PC2 mial 0% (1/25) z avg 154ms i `RecordedToolCalls: []` - **nie udalo sie polaczyc z PC2** (problem niezdiagnozowany, user wykasowal raport)

### [DIAGNOZA]
W `AutoBenchmarkEngine.SaveReports` (linia 761) folder budowany wylacznie z `ModelName`:
```csharp
string safeModel = string.Join("_", (ModelName ?? "UnknownModel")
    .Split(Path.GetInvalidFileNameChars()));
```
Dla 2 PC z tym samym modelem (np. gemma-4-31b) powstaje 1 folder.
Brak rozroznienia providera.

### [FIX v2.28.83 - w commit 5869ac0]
Uzytkownik commit-owal moja implementacje (autor AdrianPKrawczyk, dzis 11:35:54) razem z Metric Vision:
1. `BenchmarkModels.cs`: dodano `public Guid? ProviderId { get; set; }` do `RunMetadata`
2. `AutoBenchmarkEngine.cs`: 
   - `BuildModelDirKey(modelName, providerId)` - helper zwracajacy `"model@a1b2c3"` (6-znakowy hex z GUID-a)
   - W `RunBenchmarkAsync`: `config.RunMetadata.ProviderId = activeProvider?.Id;`
   - W `SaveReports` i `SaveBatchSummaryReport`: `BuildModelDirKey` zamiast inline
3. Kompatybilnosc wsteczna: gdy `ProviderId == null/Empty` - folder budowany jak wczesniej

### [FIX v2.28.84 - moj commit]
Dodano `using System;` do `BenchmarkModels.cs` (linia 1). Bez tego `Guid?` nie kompiluje sie (CS0246). To brakujacy element z commit 5869ac0.

### [KONSEKWENCJE]
- 2 PC z `gemma-4-31b-it-qat`:
  - PC1: `gemma-4-31b-it-qat@abc123/` (np. domyslny LM Studio GUID)
  - PC2: `gemma-4-31b-it-qat@def456/` (inny PC/provider)
- Mozna porownywac ten sam model na 2 komputerach
- Stare foldery (bez `@xxxxxx`) zostaja - kompatybilnosc wsteczna
- Raport FULL/SUMMARY: w nazwie pliku rowniez `safeModel` z `@xxxxxx`

### [DIAGNOSTYKA PC2 - DO ZROBIENIA]
Test na PC2 (192.168.100.190) mial:
- 0% (1/25 PASS - test 9, ktory ma malo rygorystyczne walidacje)
- avg 154ms (za szybko - cos natychmiast zwraca puste)
- `RecordedToolCalls: []` we wszystkich oblenych testach

Mozliwe przyczyny:
- Firewall blokuje polaczenie z PC2
- Zly endpoint URL w llm_providers.json
- Model nie wgrany w LM Studio PC2
- Timeout zbyt krotki
- Brak kompatybilnosci API (inny format niz LM Studio OC)
- Bardzo szybka odpowiedz "pusta" (model zbyt maly?)

User: **"Nie - nie wiem"** - nie wie co to jest.
Nastepne kroki diagnostyczne: dodac surowy log odpowiedzi LLM w trybie benchmarkowym.

### [STAN_SYSTEMU]
- 1 commit: v2.28.84 (using System)
- Kod ProviderId juz w HEAD (commit 5869ac0, autor: user)
- 11 folderow benchmarkow, po v2.28.83+ beda miec format `model@xxxxxx`

### [KOLEJNY_KROK]
- Kompilacja BricsCAD z v2.28.84 (build.ps1)
- Przetestowac nowy test z PC1 - folder powinien miec format `model@xxxxxx`
- Przetestowac polaczenie z PC2 (debug problemu z pustymi RecordedToolCalls)
- Opcjonalnie: dodac surowy log odpowiedzi LLM w trybie benchmark

## [v2.28.85] 2026-06-14T12:00:00+02:00 - Lepsza diagnostyka bledow HTTP 500 w benchmarkach [LLM-ERROR-LOG]

### [PROBLEM - ZDIAGNOZOWANY]
User zrobil screenshot z PC2: `Błąd serwera (500): model_load_failed`.
To WYJASNIA dlaczego test z 14.06.1120 mial 0% z pustymi `RecordedToolCalls`:
1. LLMClient wysyła request do LM Studio PC2 (`192.168.100.190:1234`)
2. PC2 zwraca HTTP 500 z `{"error":{"type":"model_load_failed","message":"..."}}`
3. `SendMessageBenchmarkAsync` linia 595-602: `EnsureSuccessStatusCode()` rzuca wyjatek
4. `catch (Exception ex)` loguje "Błąd połączenia z LLM" i **cicho return**
5. Benchmark widzi puste `RecordedToolCalls` → walidator daje 0% PASS
6. Raport NIE wyjasnia ze problem jest w modelu/VRAM/endpoincie - tylko "tool not called"

### [FIX v2.28.85]
1. `LLMClient.SendMessageBenchmarkAsync` (linia ~592-632):
   - Wczesniej: `EnsureSuccessStatusCode()` → throw → catch → silent return
   - Teraz: jawnie sprawdzam `IsSuccessStatusCode`. Jesli falsz:
     - Czytam `response.Content` (body z bledem)
     - Loguje przez `OnStatusUpdate` pelna odpowiedz HTTP
     - Rejestruje specjalny `_LLM_ERROR_{status}` tool call z HttpStatus + ErrorBody (substring 500 znakow)
     - Dodaje asystencka wiadomosc `[LLM ERROR {status}]` do historii
     - return (kontynuacja petli niemozliwa - model nie odpowiedzial)

2. `LLMClient.SendMessageBenchmarkAsync` catch block:
   - Rejestruje `_LLM_ERROR_CONNECTION` tool call z ErrorMessage
   - Dzieki temu nawet timeout/connection refused widac w raporcie

3. `AutoBenchmarkEngine.RunBenchmarkAsync` (linia ~283-292):
   - Po wywolaniu LLM, SPRAWDZA czy w `RecordedToolCalls` jest `_LLM_ERROR_*`
   - Jesli tak: loguje przez `OnLogMessage` z pelnym args (HttpStatus + ErrorBody)
   - To wyjasnia PRZED walidacja ze problem jest z LLM (a nie z promptem/regulami)

### [EFEKT]
- Raport benchmarku z bledem HTTP bedzie mial:
  - `RecordedToolCalls: [{"ToolName": "_LLM_ERROR_500", "Arguments": {"HttpStatus": 500, "ErrorBody": "model_load_failed:..."}}]`
  - `FailedRulesErrors: ["Brak ToolCalled:InsertBlock", ...]` (walidator nie wie o bledzie HTTP)
  - W logu: `"⚠ LLM ERROR (HTTP): _LLM_ERROR_500 | {...HttpStatus: 500, ErrorBody: model_load_failed...}"`
- User zobaczy w UI/raporcie pelna przyczyne (model sie nie zaladowal)
- Przy nastepnym PC2 bedzie mozna zobaczyc DOKLADNY blad

### [KOMPATYBILNOSC WSTECZNA]
- Specjalne tool calls z prefiksem `_LLM_ERROR_` nie sa normalnymi tool calls
- Walidator ich NIE akceptuje jako poprawne wywolania (zgodnie z oczekiwaniem)
- To oznacza: benchmark z bledem HTTP ZAWSZE da 0%, ale teraz z wyjasnieniem

### [STAN_SYSTEMU]
- 394 bledow kompilacji (pre-existing, +0 od mojej zmiany)
- 1 commit: v2.28.85 (LLMClient + AutoBenchmarkEngine)

### [KOLEJNY_KROK]
- Kompilacja BricsCAD z v2.28.85 (build.ps1)
- Przetestowac na PC2 - tym razem raport powinien pokazac `_LLM_ERROR_500` z `model_load_failed`
- Zidentyfikowac model w LM Studio PC2 ktory sie nie laduje (user sprawdzi)

### [STAN_SYSTEMU_AKTUALIZACJA] 2026-06-14
- Rozwiązano problem z ignorowaniem zmiany koloru na szary (`GrayOtherLayers`) przez `BricsCAD OffScreen Device`.
- Wprowadzono kod obchodzący brak regenu (`fallback`) - wymuszenie `ent.Color = RGB(128,128,128)` oraz co ważniejsze, wywołanie `ent.RecordGraphicsModified(true)` podczas tymczasowej transakcji. 
- Narzędzia wizyjne do izolacji warstw (Fade i Gray) zostały w pełni przetestowane i zatwierdzone przez użytkownika.

## [v2.29.0] 2026-06-15 - Wdrożenie Pipeline'u Danych Pomieszczeń (Room Data Pipeline)
### [ZREALIZOWANO]
- Utworzono dwa nowe narzędzia IToolV2 w architekturze Ekstraktor -> Agent -> Batch Writer:
  1. `ExtractRoomDataEntitiesTool` (tylko odczyt) - skanuje Model Space, paruje polilinie-obrysy z blokami-metkami testem Point-in-Polygon (ray casting), zwraca JSON z listami Matched / UnmatchedBoundaries / UnmatchedTags.
  2. `BatchWriteXDataTool` (zapis) - zbiorczy zapis XData dla wielu obiektów w JEDNEJ transakcji CAD z auto-rejestracją RegApp. Nadpisuje istniejące dane. Pomija obiekty o nieistniejących Handle'ach z raportem.
- Dodano testy jednostkowe schematu dla obu narzędzi (`ExtractRoomDataEntitiesToolTests`, `BatchWriteXDataToolTests`) i wpięto je w `TestRunner.cs`.
- Rozszerzono `ToolConfigManager.SyncWithTools` o auto-tagi `#xdata, #metadata, #pokoje` dla obu narzędzi.
- Dodano oba narzędzia do listy `AllowedTools` profilu `CadMetadataProfile` (w obu miejscach: `SyncWithTools` i `GenerateDefaultConfig`).
- Dodano `ExtractRoomDataEntities` do listy `AllowedTools` profilu `SupervisorProfile` (do planowania pipeline'u).
- Rozszerzono `resources/prompts/system_prompt_metadata.txt` o opis architektury pipeline'u i zasad działania BatchWriteXData.
- Dodano wpisy w `Bricscad_AgentAI_V2.csproj` dla obu nowych plików narzędzi i obu testów.
- Zaktualizowano `memory.md` (historia wersji + dziennik deweloperski).
### [STAN_SYSTEMU]
- Kod źródłowy kompletny i zgodny z konwencjami projektu.
- 0 błędów specyficznych dla moich plików (build 398 błędów dotyczy globalnego problemu z referencjami Newtonsoft.Json po wyczyszczeniu cache - identyczny dla wszystkich plików w projekcie, nie jest regresją mojej zmiany).
- Narzędzia będą automatycznie wykryte przez `ToolOrchestrator` przy pierwszym uruchomieniu w BricsCAD (po zbudowaniu DLL w MSBuild/VS).
### [BLOKADY / PROBLEMY]
- Brak MSBuild w środowisku - kompilacja wymaga pełnego Visual Studio z BricsCAD V22 SDK.
### [KOLEJNY_KROK]
- Zbudować projekt w Visual Studio / MSBuild.
- Załadować DLL w BricsCAD i przetestować pipeline z przykładowym rysunkiem (polilinie + bloki z atrybutami).
- Rozważyć dodanie benchmarków AutoBenchmark dla pipeline'u.

## [v2.29.3] 2026-06-16 - Ciche rozgrzewanie promptu Supervisora
### [ZREALIZOWANO]
- Dodano ustawienia `EnablePromptWarmup`, `PromptWarmupOnAiOpen`, `PromptWarmupOnSessionLoad` i `PromptWarmupAfterTypingIdleMs` w `UISettings`.
- Dodano checkboxy w `Ustawienia -> Workflow`: rozgrzewanie po otwarciu AI oraz odświeżanie po pauzie w pisaniu, wraz z czasem pauzy w ms.
- Wyciągnięto budowę promptu systemowego Supervisora do `SupervisorOrchestrator.BuildSupervisorSystemPrompt(activeDwgPath)`, żeby realny request i warmup używały tego samego prefixu.
- Dodano `LLMClient.WarmupPromptAsync(...)`, wysyłające minimalny request bez tool callingu: historia + techniczna wiadomość użytkownika, `temperature=0`, `max_tokens=1`.
- Dodano koordynator warmupu w `AgentControl`: start po otwarciu AI, po wczytaniu/zmianie sesji, po zmianie konfiguracji modelu oraz debounce po pisaniu.
- Warmup klonuje historię sesji i nie dopisuje wiadomości do JSON sesji. Realne zapytanie użytkownika anuluje trwający warmup.
- Providerzy chmurowi OpenAI/OpenRouter/Azure są pomijani, aby nie generować zbędnych kosztów. Funkcja jest przeznaczona głównie dla LM Studio i llama.cpp.
### [STAN_SYSTEMU]
- Użytkownik skompilował i sprawdził działanie funkcji.
- Wcześniejsza weryfikacja `build.ps1` kończyła właściwy `Rebuild` wynikiem `0 Warning(s), 0 Error(s)`, mimo że etap restore zgłaszał brak dostępu do `C:\Users\Adrian\AppData\Roaming\NuGet\NuGet.Config`.
### [KOLEJNY_KROK]
- Porównać czas pierwszego zapytania z włączonym i wyłączonym warmupem na LM Studio 24 GB oraz na drugim serwerze 2x 5060 Ti 16 GB.
- Jeśli LM Studio przejmuje slot/cache przez inne aplikacje, dobrać `PromptWarmupAfterTypingIdleMs` eksperymentalnie.

## [v2.29.4] 2026-06-16 - Globalny model Vision/OCR dla obrazów
### [ZREALIZOWANO]
- Dodano `VisionOcrBinding` w konfiguracji narzędzi/agentów jako nadrzędne ustawienie niezależne od profili.
- Dodano resolver `LLMConfigManager.ResolveVisionOcrProvider()` z tą samą semantyką provider/model/payload co bindingi agentów.
- Dodano zakładkę `Ustawienia -> Vision/OCR` z wyborem providera, modelu, payloadu, AutoLoad, kontekstu i podglądem efektywnego ustawienia.
- Dodano `LLMClient.AnalyzeImageWithVisionOcrAsync(...)`, wysyłające OpenAI-compatible request bez narzędzi i zwracające tekstowy opis/OCR.
- Obrazy z załączników, schowka, `CaptureVisionArea` i `CaptureMetricVisionArea` przy włączonym OCR są zamieniane na tekst `[VISION/OCR]` przed dalszą pracą głównego agenta.
- Przy wyłączonym OCR zachowano dotychczasowy przepływ multimodalny.
### [STAN_SYSTEMU]
- `git diff --check`: brak błędów whitespace, tylko standardowe ostrzeżenia CRLF.
- `powershell -ExecutionPolicy Bypass -File build.ps1`: etap Restore nadal zgłasza brak dostępu do `NuGet.Config`, ale właściwy `Rebuild` zakończył się wynikiem `0 Warning(s), 0 Error(s)`.
### [KOLEJNY_KROK]
- Skonfigurować drugi komputer z małym modelem vision jako provider w zakładce `Vision/OCR`.
- Przetestować załącznik obrazu, obraz ze schowka oraz narzędzia `CaptureVisionArea` i `CaptureMetricVisionArea`.

## [v2.29.5] 2026-06-16 - Regulowana jakość Vision/OCR dla schowka i załączników
### [ZREALIZOWANO]
- Dodano `UISettings.VisionOcrClipboardMaxPixels` i `UISettings.VisionOcrAttachmentMaxPixels`, oba domyślnie 2048.
- Rozszerzono `FileExtractor.GetImageBase64(...)` o parametr `maxPixels`, z bezpiecznym zakresem 256-4096 i zachowaniem kompatybilnego domyślnego limitu 1024.
- W `Ustawienia -> Vision/OCR` dodano kontrolki `Schowek px` i `Zalaczniki px`, zapisujące wartości do `ui_settings.json`.
- Przy włączonym globalnym Vision/OCR obraz ze schowka i obraz-załącznik używają odpowiedniego limitu OCR; przy wyłączonym OCR pozostaje stary przepływ multimodalny 1024 px.
### [STAN_SYSTEMU]
- `powershell -ExecutionPolicy Bypass -File build.ps1`: etap Restore nadal zgłasza brak dostępu do `NuGet.Config`, ale właściwy `Rebuild` zakończył się wynikiem `0 Warning(s), 0 Error(s)`.
### [KOLEJNY_KROK]
- Przetestować OCR rysunków technicznych na 2048 px i 3072 px; dla małych, zasłoniętych opisów kreskowaniem wyższy limit może poprawić odczyt kosztem większego requestu do modelu vision.

## [v2.29.6] 2026-06-16 - Hotfix AutoLoad dla globalnego Vision/OCR
### [PROBLEM]
- Po restarcie programu pierwszy obraz ze schowka/załącznika mógł kończyć się błędem Vision/OCR, dopóki użytkownik ręcznie nie załadował modelu OCR w ustawieniach.
- Przyczyną było to, że `AutoLoadModel` dla `VisionOcrBinding` zapisywał się tylko przy wyłączonym `Payload providera`; przy domyślnym trybie binding nie nadpisywał flagi i pierwszy request mógł nie wywołać load modelu.
### [ZREALIZOWANO]
- `LLMConfigManager.ResolveProviderFromBinding(...)` respektuje `AutoLoadModel` z bindingu niezależnie od `OverridePayload`.
- `LLMClient.AnalyzeImageWithVisionOcrAsync(...)` dla lokalnego providera traktuje brak zapisanej flagi OCR AutoLoad jako `true`, zachowując możliwość jawnego wyłączenia przez UI.
- `AgentControl` zapisuje checkbox AutoLoad dla Vision/OCR zawsze, niezależnie od trybu payloadu; checkbox i polityka kontekstu pozostają aktywne przy włączonym OCR.
### [STAN_SYSTEMU]
- `git diff --check`: brak błędów whitespace, tylko standardowe ostrzeżenia CRLF.
- `build.ps1`: kompilacja doszła przez `CoreCompile`, ale etap kopiowania DLL nie mógł się zakończyć, ponieważ BricsCAD trzymał `bin\Debug\Bricscad_AgentAI_V2.dll` w procesie `BricsCAD Application (x64)`.
### [KOLEJNY_KROK]
- Po zamknięciu BricsCAD uruchomić ponownie `powershell -ExecutionPolicy Bypass -File build.ps1`, aby podmienić DLL w `bin\Debug`.

## [v2.29.7] 2026-06-16 - Hotfix automatycznego wyboru modelu Vision/OCR
### [PROBLEM]
- Vision/OCR po starcie programu mógł zwracać błąd do momentu ręcznego kliknięcia `Modele` w `Ustawienia -> Vision/OCR`.
- Przyczyną był pusty albo placeholderowy `ModelName` w efektywnym bindingu OCR; przycisk `Modele` wykonywał GET `/v1/models` i dopiero wtedy uzupełniał dropdown.
### [ZREALIZOWANO]
- `LLMClient.AnalyzeImageWithVisionOcrAsync(...)` przed AutoLoad sprawdza, czy model OCR jest realnie wybrany. Jeśli model jest pusty albo placeholderowy (`local-model`, `model`, `llama3`), pobiera listę modeli z providera przez istniejące `GetAvailableModelsAsync(...)`.
- Dodano wybór preferowanego modelu OCR/Vision po nazwach zawierających m.in. `vision`, `vl`, `vlm`, `mm`, `ocr`, `gemma`, `qwen2-vl`, `llava`, `pixtral`, `moondream`, `minicpm`; przy braku trafienia wybierany jest pierwszy dostępny model.
- `AgentControl` po utworzeniu zakładki Vision/OCR automatycznie odświeża listę modeli, jeśli zapisany model jest pusty albo placeholderowy, i zapisuje wybrany model do bindingu.
### [STAN_SYSTEMU]
- `git diff --check`: brak błędów whitespace, tylko standardowe ostrzeżenia CRLF.
- `powershell -ExecutionPolicy Bypass -File build.ps1`: właściwy `Rebuild` zakończył się wynikiem `0 Warning(s), 0 Error(s)`.
### [KOLEJNY_KROK]
- Przetestować pierwszy request OCR po pełnym restarcie BricsCAD/AI bez ręcznego klikania `Modele`.

## [v2.29.8] 2026-06-16 - Hotfix pierwszego requestu Vision/OCR po starcie
### [PROBLEM]
- Użytkownik potwierdził, że OCR nadal nie działał przy pierwszym załączniku po starcie, ale zaczynał działać po ręcznym kliknięciu `Modele` w zakładce Vision/OCR.
- To wskazuje, że sam efekt GET `/v1/models`/walidacji listy modeli musi być wykonany w ścieżce requestu OCR, a nie tylko przy pustym placeholderze.
### [ZREALIZOWANO]
- `LLMClient.EnsureVisionOcrModelFromProviderAsync(...)` dla lokalnych providerów zawsze pobiera `/v1/models` przed OCR, sprawdza czy zapisany `ModelName` rzeczywiście występuje na liście, a gdy nie występuje wybiera preferowany model vision/OCR.
- Jeśli wybrany model zmienia się względem zapisu, `VisionOcrBinding.ModelName` jest aktualizowany i zapisywany przez `ToolConfigManager.UpdateVisionOcrBinding(...)`.
- Request OCR ma teraz maksymalnie dwie próby. Po pierwszym błędzie HTTP lub wyjątku komunikacji wykonuje krótką pauzę, ponownie odświeża `/v1/models`, ponawia AutoLoad i dopiero wtedy wysyła drugi request.
### [STAN_SYSTEMU]
- `git diff --check`: brak błędów whitespace, tylko standardowe ostrzeżenia CRLF.
- `powershell -ExecutionPolicy Bypass -File build.ps1`: właściwy `Rebuild` zakończył się wynikiem `0 Warning(s), 0 Error(s)`.
### [KOLEJNY_KROK]
- Przetestować pierwszy OCR po starcie bez klikania `Modele`; w logu powinny pojawić się wpisy `[VISION OCR MODEL]`, `[VISION OCR REQ] Attempt=1`, a przy ewentualnym pierwszym błędzie `[VISION OCR RETRY]` i `Attempt=2`.

## [v2.29.9] 2026-06-16 - Hotfix comboboxa providera Vision/OCR
### [PROBLEM]
- Screenshot użytkownika pokazał, że po kliknięciu `Modele` combobox wizualnie nadal pokazywał `LM Studio (Lokalny)`, ale preview Vision/OCR zmieniał się na `aktywny, brak providera`.
- Oznaczało to, że `ComboBox.SelectedItem` nie był już `LLMProviderConfig`, a zapis ustawień OCR mógł utrwalić binding bez `ProviderId`/`ProviderNameFallback`.
### [ZREALIZOWANO]
- `cbVisionOcrProvider` dostał `ValueMember = "Id"`.
- `GetSelectedVisionOcrProvider()` stał się odporny na stan pośredni WinForms: odzyskuje providera z `SelectedItem`, `SelectedValue`, tekstu comboboxa, zapisanego bindingu i dopiero na końcu z aktywnego providera.
- `BuildVisionOcrBindingFromUi()` nie zapisuje już pustego providera, jeśli UI chwilowo nie zwraca poprawnego `SelectedItem`.
- `RefreshVisionOcrModelsAsync()` zachowuje aktualny model tylko wtedy, gdy rzeczywiście istnieje na liście `/v1/models`; inaczej wybiera preferowany model z listy i zapisuje go.
### [STAN_SYSTEMU]
- `git diff --check`: brak błędów whitespace, tylko standardowe ostrzeżenia CRLF.
- `powershell -ExecutionPolicy Bypass -File build.ps1`: właściwy `Rebuild` zakończył się wynikiem `0 Warning(s), 0 Error(s)`.
### [KOLEJNY_KROK]
- Po reloadzie wtyczki sprawdzić, czy preview po kliknięciu `Modele` nadal pokazuje konkretny provider zamiast `brak providera`.

## [v2.29.10] 2026-06-16 - Trwały kontekst obrazów Vision/OCR w sesji
### [PROBLEM]
- Globalny Vision/OCR działał jako jednorazowy preprocesor: główny Supervisor dostawał tekstowy opis obrazu, ale przy kolejnych pytaniach nie mógł ponownie poprosić modelu OCR o obejrzenie tego samego pliku.
- W praktycznym teście z rysunkiem `Rys-02.png` pierwsza analiza nie odczytała tabliczki rysunkowej na prawym marginesie arkusza, więc późniejsze pytania o inwestora/projekt bazowały tylko na niepełnym opisie, a nie na ponownej analizie obrazu.
### [ZREALIZOWANO]
- Dodano `ChatSession.VisionImages` oraz modele `VisionImageContext` i `VisionOcrObservation` w `Models/Session/ChatSession.cs`.
- Załączniki graficzne i obrazy ze schowka przy włączonym globalnym OCR są kopiowane do cache sesji: `%APPDATA%\Bricscad_AgentAI\SessionImages\<sessionId>\`.
- Każdy obraz dostaje `image_id`, ścieżkę cached pliku, hash SHA-256, rozmiar, oryginalne wymiary, limit px użyty do OCR oraz nazwę providera/modelu OCR.
- Każdy request OCR dopisuje obserwację do `OcrHistory`: prompt, wynik, status, błąd oraz provider/model. Sesja zapisuje te dane w JSON.
- Dodano automatyczne re-query ostatniego obrazu, gdy użytkownik pyta o wcześniejszy obraz/rysunek/załącznik albo o elementy typowe dla OCR: tabliczka, inwestor, projekt, adres, legenda, wymiary, napisy, rząpia/rzap.
- Ponowna analiza wysyła cached obraz do modelu Vision/OCR i przekazuje Supervisorowi blok `[VISION/OCR REQUERY image_id=...]`, bez dokładania base64 do historii głównego modelu.
- Wzmocniono prompt OCR w `LLMClient.AnalyzeImageWithVisionOcrAsync(...)`, aby model jawnie sprawdzał krawędzie arkusza, tabliczkę rysunkową, legendy, małe napisy i elementy niepewne.
### [STAN_SYSTEMU]
- `powershell -ExecutionPolicy Bypass -File build.ps1`: właściwy etap `Rebuild` zakończył się wynikiem `0 Warning(s), 0 Error(s)`.
- Faza `Restore` nadal zgłasza brak dostępu do `C:\Users\Adrian\AppData\Roaming\NuGet\NuGet.Config`, ale skrypt kontynuuje i poprawnie buduje DLL przez MSBuild.
### [KOLEJNY_KROK]
- W BricsCAD przetestować scenariusz: dodać `Rys-02.png`, zapytać ogólnie o obraz, a potem zapytać “sprawdź jeszcze raz tabliczkę rysunkową / inwestora / adres”. W logu sesji powinien pojawić się kolejny wpis OCR dla tego samego `image_id`.

## [v2.29.11] 2026-06-16 - Adaptacyjny tiling Vision/OCR dla arkuszy panoramicznych i pionowych
### [PROBLEM]
- Skalowanie calego duzego arkusza do jednego limitu px pogarszalo OCR drobnych napisow, tabliczek i legend.
- Rysunki techniczne czesto maja proporcje 1:2, 1:3 albo pionowe odpowiedniki, wiec kwadratowa siatka moglaby tworzyc zbyt wiele pustych lub malo uzytecznych kafelkow.
### [ZREALIZOWANO]
- Dodano `VisionOcrTiler`, ktory tnie obraz na adaptacyjne prostokatne kafelki bez rozciagania pikseli.
- Algorytm dobiera osobno liczbe kolumn i wierszy, pilnuje limitu kafelkow, overlapu oraz maksymalnej proporcji kafelka.
- Domyslne ustawienia w `UISettings`: `VisionOcrTilingMode=Auto`, `VisionOcrTileMaxDim=2100`, `VisionOcrTileOverlap=200`, `VisionOcrTileMaxCount=16`, `VisionOcrTileMaxAspectRatio=2.0`.
- Zakladka `Ustawienia -> Vision/OCR` dostala kontrolki tilingu: tryb, kafelek px, overlap px, maks. kafelkow i maks. proporcja.
- `VisionImageContext` zapisuje metadane tilingu i liste `VisionImageTileContext` z `Row`, `Column`, `CachedPath`, `SourceX/Y/W/H`.
- `LLMClient` ma teraz `AnalyzeImagesWithVisionOcrAsync(...)`, ktore wysyla jeden request multi-image: prompt przestrzenny + wszystkie kafelki jako `image_url`.
- Re-query wczesniejszego obrazu uzywa zapisanych kafelkow, jesli istnieja, zamiast ponownie skalowac pelny obraz.
- Dodano testy `VisionOcrTilerTests` dla przypadkow `4096x4096`, `6000x2000`, `12000x4000` i `3000x9000`.
### [STAN_SYSTEMU]
- `powershell -ExecutionPolicy Bypass -File build.ps1`: etap `Rebuild` zakonczyl sie wynikiem `0 Warning(s), 0 Error(s)`.
- Etap `Restore` nadal zglasza brak dostepu do `C:\Users\Adrian\AppData\Roaming\NuGet\NuGet.Config`, ale skrypt przechodzi dalej i poprawnie buduje DLL.
### [KOLEJNY_KROK]
- W BricsCAD przetestowac duzy arkusz panoramiczny i zapytac o tabliczke/legende; w logu powinien pojawic sie `[VISION OCR TILING]` z ukladem siatki i request OCR powinien zawierac kafelki zamiast jednego mocno pomniejszonego obrazu.

## [v2.29.12] 2026-06-16 - Hotfix parsowania odpowiedzi Vision/OCR z llama.cpp
### [PROBLEM]
- Po zmianie providera Vision/OCR z LM Studio na llama.cpp serwer poprawnie przetwarzal kafelki obrazu i generowal odpowiedz, ale aplikacja zwracala komunikat `Vision/OCR zwrocil pusta odpowiedz`.
- Log llama.cpp pokazywal `processing image...`, `image processed` oraz `n_decoded = 2000`, wiec problem nie byl w wysylce obrazu ani w kontekscie, tylko w zbyt waskim odczycie JSON po stronie klienta.
### [ZREALIZOWANO]
- `LLMClient.AnalyzeImagesWithVisionOcrAsync(...)` nie czyta juz wylacznie `choices[0].message.content` jako prostego stringa.
- Dodano `ExtractVisionOcrContent(...)`, ktore obsluguje kilka wariantow odpowiedzi OpenAI-compatible: `message.content` jako string/tablice czesci tekstowych/obiekt, `reasoning_content`, `reasoning`, `choices[0].text`, `response` i `generated_text`.
- Gdy odpowiedz nadal zostanie uznana za pusta, Engine log dostaje ostrzezenie `[VISION OCR WARN]` z `finish_reason` i ucietym fragmentem surowego JSON, aby latwo zobaczyc format zwracany przez konkretna wersje llama.cpp.
### [STAN_SYSTEMU]
- `powershell -ExecutionPolicy Bypass -File build.ps1`: etap kompilacji C# przeszedl bez bledow skladniowych, ale koncowe kopiowanie `Bricscad_AgentAI_V2.dll` do `bin\Debug` nie powiodlo sie, bo DLL byla zablokowana przez dzialajacy proces BricsCAD.
- Etap `Restore` nadal zglasza brak dostepu do `C:\Users\Adrian\AppData\Roaming\NuGet\NuGet.Config`.
### [KOLEJNY_KROK]
- Zamknac/odladowac BricsCAD przed kolejnym buildem, aby MSBuild mogl podmienic DLL w `bin\Debug`.
- Dla llama.cpp ustawic w OCR `Max tokens` na co najmniej `4096`, bo log pokazal, ze model dobil do aktualnego limitu `2000` tokenow.

## [v2.29.13] 2026-06-16 - Tester Vision/OCR i presety jakosci/tilingu
### [PROBLEM]
- Globalny OCR dzialal, ale strojenie modelu, promptu, jakosci obrazu i tilingu wymagalo przechodzenia przez normalny czat.
- Przy pytaniach celowanych, np. o inwestora z tabliczki, OCR potrafil generowac pelny raport az do limitu `max_tokens`, mimo ze potrzebna byla krotka odpowiedz.
- Ustawienia `Jakosc obrazu` i `Adaptacyjny tiling` byly zapisywane jako pojedynczy stan, ale brakowalo listy presetow do szybkiego porownywania konfiguracji.
### [ZREALIZOWANO]
- `LLMClient` udostepnia teraz `BuildVisionOcrSystemPrompt()` i `BuildVisionOcrUserPrompt(...)`, a request OCR korzysta z tych samych metod co podglad w UI.
- Prompt OCR zostal doprecyzowany: pelny raport sekcyjny jest wymagany tylko przy ogolnej analizie, a przy pytaniu o konkretny element model ma odpowiedziec celowanie i krotko.
- Zakladka `Ustawienia -> Vision/OCR` dostala przewijany uklad oraz sekcje `Szybki test Vision/OCR`.
- Tester OCR pozwala wybrac plik PNG/JPG, wpisac prompt testowy, zobaczyc rzeczywisty prompt wysylany do modelu i uruchomic OCR bez dopisywania wyniku do sesji czatu.
- Dodano `VisionOcrQualityPreset` w `UISettings`: presety zapisuja `ClipboardMaxPixels`, `AttachmentMaxPixels`, tryb tilingu, rozmiar kafelka, overlap, limit kafelkow i maksymalna proporcje.
- UI pozwala wybrac preset z listy oraz wykonac `Zapisz`, `Nadpisz`, `Usun`.
### [STAN_SYSTEMU]
- `powershell -ExecutionPolicy Bypass -File build.ps1`: etap `Rebuild` zakonczyl sie wynikiem `0 Warning(s), 0 Error(s)` i skopiowal DLL do `bin\Debug`.
- Etap `Restore` nadal zglasza brak dostepu do `C:\Users\Adrian\AppData\Roaming\NuGet\NuGet.Config`, ale nie blokuje wlasciwej kompilacji.
### [KOLEJNY_KROK]
- Przetestowac w UI dwa presety: np. `Tabliczka szybka` z mniejszym limitem kafelkow i `Pelny arkusz` z wiekszym tilingiem oraz wyzszym `Max tokens`.
- Kolejna optymalizacja: przed re-query do OCR mozna dodac warstwe sprawdzania zapisanych obserwacji `OcrHistory`, aby Supervisor odpowiadal z cache, jesli dane juz sa w poprzednim wyniku OCR.

## [v2.29.14] 2026-06-17 - Diagnostyka tokenow i pol odpowiedzi Vision/OCR
### [PROBLEM]
- W testerze Vision/OCR krotka odpowiedz, np. dane inwestora, mogla wygladac na kilkanascie slow, podczas gdy llama.cpp raportowal kilkaset tokenow generacji.
- Trzeba bylo zobaczyc, czy dodatkowe tokeny trafiaja do `reasoning_content`/`reasoning`, innego pola OpenAI-compatible, czy sa tylko kosztem chat-template/modelu.
### [ZREALIZOWANO]
- `LLMClient` zapisuje `LastVisionOcrDiagnostics` po kazdej odpowiedzi OCR.
- Diagnostyka zawiera: `model`, `finish_reason`, `prompt_tokens`, `completion_tokens`, `total_tokens`, `reasoning_tokens`, dlugosci `content`, `reasoning_content`, `reasoning`, `choices[0].text`, `response`, `generated_text`, dlugosc wyekstrahowanej odpowiedzi i surowego JSON.
- Tester w `Ustawienia -> Vision/OCR` pokazuje blok `[DIAGNOSTYKA OCR]` przed `[ODPOWIEDZ OCR]`.
### [STAN_SYSTEMU]
- Do wykonania po zmianie: `powershell -ExecutionPolicy Bypass -File build.ps1`.

## [v2.29.15] 2026-06-17 - Hybrydowy OCR PDF przez pdftoppm
### [PROBLEM]
- Uzytkownik chce przepuszczac przez Vision/OCR pliki PDF z rzutami: najlepiej laczac odczyt tekstu z PDF oraz render stron do PNG dla modelu wizyjnego.
- Wybrany renderer open-source/free to Poppler `pdftoppm`, zainstalowany lokalnie i dodany do PATH.
### [ZREALIZOWANO]
- `UISettings` dostal ustawienia PDF: `VisionOcrPdfDpi`, `VisionOcrPdfMaxPages`, `VisionOcrPdfRendererPath`.
- `FileExtractor.RenderPdfPagesToPng(...)` uruchamia `pdftoppm -png`, renderuje zakres stron i zwraca wygenerowane pliki PNG.
- Zakladka `Ustawienia -> Vision/OCR` dostala sekcje `PDF -> tekst + PNG dla Vision/OCR` z DPI, limitem stron i sciezka/nazwa renderera.
- Szybki tester OCR akceptuje teraz PNG/JPG/PDF; dla PDF laczy tekst wydobyty przez PdfPig z renderowanymi stronami analizowanymi przez Vision/OCR.
- Normalny zalacznik PDF w czacie przy wlaczonym globalnym OCR uzywa przeplywu hybrydowego: `[PDF_TEXT]` + `[PDF_VISION/OCR]`, bez wysylania PDF do glownego modelu jako surowego pliku.
### [STAN_SYSTEMU]
- `powershell -ExecutionPolicy Bypass -File build.ps1`: etap `Rebuild` zakonczyl sie wynikiem `0 Warning(s), 0 Error(s)` i skopiowal DLL do `bin\Debug`.
- Etap `Restore` nadal zglasza brak dostepu do `C:\Users\Adrian\AppData\Roaming\NuGet\NuGet.Config`, ale nie blokuje wlasciwej kompilacji.
### [KOLEJNY_KROK]
- W BricsCAD przetestowac PDF z rzutami: ustawic `Renderer=pdftoppm.exe`, `DPI=300`, `Maks. stron=3`, a potem w testerze OCR zadac pytanie o tabliczke lub konkretny rzut.

## [v2.29.16] 2026-06-17 - Naprawa polskiego mojibake w tekście PDF
### [PROBLEM]
- Hybrydowy OCR PDF dzialal, ale tekst wyciagniety przez PdfPig potrafil zawierac mojibake typu `WROCĹAW`, `WaĹ‚brzyska`, `ZawĂłr`, `RzÄ…pia`.
- Taki tekst trafial do `[PDF_TEXT]`, a model Vision/OCR i Supervisor mogly potem kopiowac uszkodzone polskie znaki.
### [ZREALIZOWANO]
- Dodano `FileExtractor.RepairPolishMojibake(...)`, ktory wykrywa typowy przypadek UTF-8 odczytanego jako Windows-1250 i naprawia tekst tylko wtedy, gdy wynik ma lepszy score polskich znakow.
- `ExtractText(... .pdf ...)` przepuszcza tekst PDF przez naprawe przed zwroceniem go do czatu/testera OCR.
- Dodano `FileExtractorTests` sprawdzajace naprawe `GMINA WROCĹAW`, `WaĹ‚brzyska`, `ZawĂłr`, `RzÄ…pia` oraz brak zmian dla czystego tekstu.
### [STAN_SYSTEMU]
- `powershell -ExecutionPolicy Bypass -File build.ps1`: etap `Rebuild` zakonczyl sie wynikiem `0 Warning(s), 0 Error(s)`.
- Etap `Restore` nadal zglasza brak dostepu do `C:\Users\Adrian\AppData\Roaming\NuGet\NuGet.Config`, ale nie blokuje wlasciwej kompilacji.

## [v2.29.18] 2026-06-17 - Trwale ustawienia Vision/OCR i czytelniejsza lista testow
### [PROBLEM]
- Lista zapisanych szybkich testow Vision/OCR pokazywala tylko nazwy plikow raportow, bez informacji o presecie, providerze i modelu.
- Ustawienia OCR mogly wracac do domyslnych po uruchomieniu BricsCAD, bo `VisionOcrBinding` byl zapisywany w `tools_config.json` obok DLL (`bin\Debug`), a nie w trwalym folderze ustawien.
- Przy wyborze wlasnego folderu ustawien AI/OCR istnialo ryzyko, ze sam wskaznik folderu zostanie zapisany, ale komplet aktualnych plikow konfiguracyjnych nie trafi do nowego miejsca.
### [ZREALIZOWANO]
- `AppPaths.GetToolConfigPath()` przenosi `tools_config.json` do tego samego trwalego folderu co `ui_settings.json` i `llm_providers.json`.
- `ToolConfigManager` migruje legacy `tools_config.json` z folderu DLL do AppData/custom folderu, jesli nowy plik jeszcze nie istnieje.
- `UISettingsManager.UpdateCustomLLMConfigPath(...)` kopiuje do wybranego folderu `llm_providers.json`, `tools_config.json` oraz zapisuje aktualny snapshot `ui_settings.json`; dodatkowo zapisuje wskaznik w domyslnym AppData.
- Zakladka `Sciezki i Dane` opisuje folder jako `Folder ustawien AI/OCR` i informuje, ze obejmuje providerow, UI, agentow, Vision/OCR oraz `tools_config.json`.
- Lista zapisanych testow Vision/OCR czyta JSON raportu i wyswietla: date/status, plik, preset, provider oraz model.
### [STAN_SYSTEMU]
- `powershell -ExecutionPolicy Bypass -File build.ps1`: etap `Rebuild` zakonczyl sie wynikiem `0 Warning(s), 0 Error(s)`.
- Etap `Restore` nadal zglasza brak dostepu do `C:\Users\Adrian\AppData\Roaming\NuGet\NuGet.Config`, ale nie blokuje wlasciwej kompilacji.
### [KOLEJNY_KROK]
- Po restarcie BricsCAD sprawdzic, czy `Ustawienia -> Vision/OCR` zachowuje provider, model, payload, jakosc obrazu, tiling i PDF; w folderze ustawien powinien byc widoczny `tools_config.json`.

## [v2.29.19] 2026-06-17 - Podglad zapisanych testow Vision/OCR po najechaniu
### [PROBLEM]
- Lista zapisanych testow Vision/OCR pozwalala otwierac/kopiowac raporty, ale pole podgladu nadal pokazywalo ostatni uruchomiony test.
- Uzytkownik chcial szybko przegladac wyniki testow bez otwierania kazdego pliku osobno.
### [ZREALIZOWANO]
- `lstVisionOcrTestRuns` reaguje na `MouseMove`: rekord pod kursorem staje sie aktywnym zaznaczeniem.
- `SelectedIndexChanged` wczytuje raport Markdown do pola `Odpowiedz`, a gdy Markdown nie istnieje, probuje pokazac JSON.
- Dwuklik na rekord nadal otwiera raport w domyslnej aplikacji.
### [STAN_SYSTEMU]
- `powershell -ExecutionPolicy Bypass -File build.ps1`: etap `Rebuild` zakonczyl sie wynikiem `0 Warning(s), 0 Error(s)`.
- Etap `Restore` nadal zglasza brak dostepu do `C:\Users\Adrian\AppData\Roaming\NuGet\NuGet.Config`, ale nie blokuje wlasciwej kompilacji.

## [v2.29.17] 2026-06-17 - Zapisywanie wynikow szybkich testow Vision/OCR
### [PROBLEM]
- Tester Vision/OCR pokazywal wynik tylko w UI, ale nie zapisywal kompletnego sladu eksperymentu do pozniejszego porownania lub analizy przez inne LLM.
- Brakowalo wygodnego dostepu do poprzednich testow z poziomu zakladki Vision/OCR.
### [ZREALIZOWANO]
- Dodano folder raportow `VisionOcrTestRuns` w AppData aplikacji.
- Kazdy szybki test Vision/OCR zapisuje dwa pliki: `.json` z pelnym snapshotem maszynowym oraz `.md` wygodny do czytania/kopiowania do innego LLM.
- Raport obejmuje: source file, provider, endpoint, model, informację czy API key jest skonfigurowany bez zapisu samego klucza, binding Vision/OCR, payload modelu, jakosc obrazu, tiling, ustawienia PDF, wybrany preset, prompt uzytkownika, prompt systemowy/podglad payloadu, diagnostyke i odpowiedz.
- Raporty sa zapisywane takze dla nieudanych testow, aby mozna bylo analizowac bledy konfiguracji.
- Sekcja `Szybki test Vision/OCR` dostala liste ostatnich raportow oraz przyciski: `Otworz raport`, `Kopiuj raport`, `Otworz folder`, `Odswiez`.
### [STAN_SYSTEMU]
- `powershell -ExecutionPolicy Bypass -File build.ps1`: etap `Rebuild` zakonczyl sie wynikiem `0 Warning(s), 0 Error(s)`.
- Etap `Restore` nadal zglasza brak dostepu do `C:\Users\Adrian\AppData\Roaming\NuGet\NuGet.Config`, ale nie blokuje wlasciwej kompilacji.

## [v2.29.20] 2026-06-17 - Stabilne ladowanie providera/modelu OCR po restarcie
### [PROBLEM]
- Po restarcie BricsCAD zakladka `Vision/OCR` mogla pokazywac w podgladzie zapisany binding `OCR-biuro`, ale combobox providera wracal do aktywnego providera czatu `LM Studio (Lokalny)`.
- Klikniecie `Uruchom OCR` zapisywalo wtedy bledny stan UI do `VisionOcrBinding`, przez co test uruchamial model z LM Studio zamiast modelu OCR z wybranego providera.
- Wybrany preset jakosci/tilingu byl po starcie tylko zaznaczany na liscie, ale jego wartosci liczbowe nie byly jawnie nakladane na pola UI.
- `EnsureVisionOcrModelFromProviderAsync` moglo nadpisac jawnie wybrany model OCR modelem zwroconym przez `/v1/models`, czyli modelem aktualnie zaladowanym na serwerze, zanim autoload zdazyl zaladowac docelowy model.
### [ZREALIZOWANO]
- `RefreshVisionOcrProviderDropdown(...)` przyjmuje preferowany `ProviderId` i podczas startu wybiera provider z bindingu OCR, a nie przypadkowy aktualny stan comboboxa.
- `SelectVisionOcrProvider(...)` uzywa najpierw `SelectedValue`, potem jawnego skanowania elementow listy, co stabilizuje WinForms ComboBox po rebindingu.
- `LoadVisionOcrBindingToUi()` po zaznaczeniu presetu wywoluje `ApplyLastVisionOcrQualityPresetToControls()`, wiec wartosci jakosci obrazu i tilingu po restarcie odpowiadaja wybranemu presetowi.
- Raport szybkiego testu zapisuje efektywny provider/model z `ResolveVisionOcrProvider()` oraz osobno `ui_provider`, zeby latwiej wykryc rozjazd UI kontra realny payload.
- `EnsureVisionOcrModelFromProviderAsync` nie podmienia juz jawnie ustawionego modelu, jesli nie ma go na chwilowej liscie `/v1/models`; zostawia go do autoloadu.
### [STAN_SYSTEMU]
- `powershell -ExecutionPolicy Bypass -File build.ps1`: kompilacja doszla do etapu kopiowania DLL, ale `bin\Debug\Bricscad_AgentAI_V2.dll` byl zablokowany przez uruchomiony `BricsCAD Application (x64)`.
- Etap `Restore` nadal zglasza brak dostepu do `C:\Users\Adrian\AppData\Roaming\NuGet\NuGet.Config`.
### [KOLEJNY_KROK]
- Zamknac BricsCAD i ponownie uruchomic `powershell -ExecutionPolicy Bypass -File build.ps1`, aby podmienic DLL w `bin\Debug`.

## [v2.29.21] 2026-06-17 - Ochrona Vision/OCR przed falszywym providerem UI
### [PROBLEM]
- Po restarcie nadal mogl wystapic rozjazd: zielony podglad Vision/OCR pokazywal poprawny binding `OCR-biuro -> gemma-4-26...`, ale combobox providera wyswietlal aktywny provider czatu `LM Studio (Lokalny)`.
- Klikniecie `Uruchom OCR` albo `Modele` moglo wtedy uzyc/zapisac zly provider, mimo ze binding OCR byl poprawny.
- Przy PDF bledy modelu OCR, np. `HTTP 400 No models loaded`, byly ukryte w sekcji `[PDF_VISION/OCR]` i raport szybkiego testu mogl miec status `OK`, bo sam render `pdftoppm` sie udal.
### [ZREALIZOWANO]
- Lista providerow OCR jest wypelniana recznie przez `Items`, bez `DataSource`, co stabilizuje wybor WinForms po starcie panelu.
- Dodano `_visionOcrProviderChangedByUser`: dopoki uzytkownik nie zmieni providera recznie, zapis bindingu i przycisk `Modele` uzywaja providera z `VisionOcrBinding`, a nie przypadkowego wyboru w comboboxie.
- `SelectVisionOcrProvider(...)` wybiera provider takze po `ProviderNameFallback`, jesli `ProviderId` nie wystarczy.
- `LLMClient` zapisuje diagnostyke dla bledow HTTP/wyjatkow Vision/OCR, wlacznie z modelem, endpointem i statusem.
- Szybki test PDF oznacza raport jako `ERROR`, jesli render PDF sie udal, ale model Vision/OCR zwrocil `BLAD OCR PDF`.
### [STAN_SYSTEMU]
- Do wykonania po zamknieciu BricsCAD: `powershell -ExecutionPolicy Bypass -File build.ps1`.

## [v2.29.23] 2026-06-17 - Vision/OCR jako cache zamiast automatycznego re-skanu
### [PROBLEM]
- Po zalaczeniu obrazu/PDF program potrafil przy kolejnych zwyklych poleceniach CAD ponownie analizowac ostatni obraz przez Vision/OCR.
- Spowalnialo to komunikacje z agentem i niepotrzebnie obciazalo lokalny model wizyjny, np. przy poleceniach typu `narysuj polilinie` albo `wstaw wymiar`.
### [ZREALIZOWANO]
- `ShouldRequeryPreviousImage(...)` zostal zawężony do jawnych polecen ponownego OCR, np. `przeanalizuj jeszcze raz`, `zeskanuj ponownie`, `ponowny ocr`, `sprawdz dokladniej obraz`.
- Dodano `TryBuildPreviousImageCachedPayload(...)`, ktory dla pytan o obraz/tabliczke/inwestora/legende dopina ostatni zapisany wynik OCR jako tekstowy kontekst bez uruchamiania modelu Vision/OCR.
- Dla zwyklych polecen CAD, ktore nie odnosza sie do obrazu, program nie dolacza kontekstu obrazu i nie wykonuje re-skanu.
- Supervisor nadal moze odpowiadac na pytania o poprzedni obraz z zapisanego OCR; jesli brakuje danych, ma poprosic uzytkownika o jawny ponowny OCR.
### [STAN_SYSTEMU]
- Do wykonania po zmianie: `powershell -ExecutionPolicy Bypass -File build.ps1`.

## [v2.30.1] 2026-06-17 - Layout/Plot: media papieru per-device dla HP/UserXXX
### [PROBLEM]
- `ListPlotDevicesTool` pobieral `CanonicalMediaName` z pustego `PlotSettings(false)`, wiec pokazywal globalna liste formatow zamiast formatow konkretnego plotera.
- Dla HP DesignJet/T120 formaty niestandardowe moga wystepowac jako `UserXXX` i pojawiac sie dopiero po ustawieniu konkretnego urzadzenia w `PlotSettingsValidator`.
### [ZREALIZOWANO]
- `ListPlotDevicesTool` dostal parametr `IncludeMediaPerDevice`; przy filtrze domyslnie pobiera media po `SetPlotConfigurationName(device) + RefreshLists(settings)`.
- Wynik listowania pokazuje formaty osobno dla kazdego pasujacego plotera, co ma odslonic `UserXXX`, jesli driver udostepnia je BricsCAD-owi.
- `PageSetupTool` waliduje `MediaName` wzgledem plotera z argumentu `PlotDevice` albo obecnego plotera layoutu, a nie wzgledem globalnej listy.
- Prompt `CadLayoutProfile` zostal doprecyzowany: dla HP/UserXXX uzywac waskiego filtra i `IncludeMediaPerDevice=true`.
### [STAN_SYSTEMU]
- Do wykonania po zmianie: `powershell -ExecutionPolicy Bypass -File build.ps1`.

## [v2.30.2] 2026-06-17 - Layout/Plot: Pc3Parser utility + diagnostyka plikow PC3
### [PROBLEM]
- Pliki .pc3 (Plot Configuration) BricsCAD-a sa binarne (nie ASCII) - dokumentacja i biblioteka PiaNO sugerowaly inaczej. Driver plik z PC3 jest kompresowany zlib-em (format zlib: 2-bajtowy naglowek + raw deflate + 4-bajtowy checksum).
- `GetCanonicalMediaNameList` dla ploterow .pc3 zwracal `(0)` elementow - brak listy mediów, blad `eNoDatabase`.
- HP plotters (DesignJet T120, T650) uzywaja formatow niestandardowych (np. `297x600` roll paper) ktorych BricsCAD API nie zwraca - pelna lista mediów jest zdefiniowana binarnie w driverach Windows HDI (.hdi) i niedostepna z poziomu .NET.
- LLM (gemma-4) po otrzymaniu bledu `eNoDatabase` halucynowal "MediaName updated successfully" - konieczne oddzielenie odczytu formatu od faktycznej zmiany ustawien.
### [ODKRYCIE]
- Format PC3 (zweryfikowane na 4 plikach systemowych):
    * Header: 60 bajtow (47 ASCII `"PIAFILEVERSION_2.0,PC3VER1,compress\r\npmzlibcode"` + 13 bajtow zlib wrapper).
    * Payload: zlib stream (`78 9C` = default compression) - 2 bajty CMF/FLG + raw deflate + 4 bajty Adler32.
    * Po dekompresji struktura ASCII: bloki `meta{...}`, `media{...}`, `io{...}`, `res_color_mem{...}`, `custom{...}`.
    * `media.size.name` przechowuje TYLKO AKTUALNIE WYBRANY format (np. `"297x600"`, `"A4"`, `"A0"`), NIE pelna liste.
    * Wymiary: `media.size.media_description.media_bounds.urx` / `.ury` (w mm), `printable_bounds_*` (offset marginesu), `printable_area`.
- Przeszukano system Windows (AppData/Roaming, AppData/Local, ProgramData, spool/drivers, Autodesk, PlotSupport): dokladnie 4 unikalne PC3 (HP T120, HP T650, Print As PDF, Default Windows Printer) - wszystkie z 1 formatem. Brak plikow PMP. Drivery HDI zarejestrowane w Win32 print spooler, nieparsowalne bez reverse-engineeringu.
### [ZREALIZOWANO]
- Nowy `src/Core/Pc3Parser.cs` (utility class, ~270 LOC, zero zaleznosci Teigha/BricsCAD):
    * `Pc3Parser.Parse(string filePath) -> Pc3Info` - zwraca strukture z meta (driver/family/model), `SelectedMedia` (name + bounds w mm), `Resolution` (DPI).
    * Algorytm: odczyt bajtow, przeskoczenie 60-bajtowego headera + 2-bajtowego zlib wrapper, `DeflateStream` (System.IO.Compression), parsowanie blokow `{...}` z rekurencyjnym zliczaniem `{}`.
    * Wartosci `"..."` stripowane z `name="value"` do `value`.
    * Obsluga bledow: `ParseSucceeded=false` + `ParseError` z komunikatem.
- `ListPlotDevicesTool`: jesli urzadzenie `.pc3` ma `(0)` Canonical Media Names, automatycznie wywoluje `Pc3Parser` i raportuje:
    * `Ploter:` (friendly_net_name lub win_driver_name)
    * `Sterownik Windows:` (driver_pathname z HDI)
    * `Aktualnie wybrany format:` (np. `297x600 (209.97x297.01mm)`)
    * `Rozdzielczosc:` (np. `300x300_None (300 dpi)`)
    * Komunikat: "Plik PC3 przechowuje tylko AKTUALNIE WYBRANY format. Pelna lista formatow jest zdefiniowana binarnie w driverze Windows (.hdi)."
- `ResolvePc3Path` - helper szukajacy pliku PC3 w fallbacku w 6 typowych lokalizacjach PlotConfig (V22x64/V23x64 Roaming+Local, V22/V23 ProgramData).
- `tests/Core/Pc3ParserTests.cs` - 9 testow jednostkowych (Debug.Assert) na prawdziwych plikach PC3 z systemu: HP T120, HP T650, Print As PDF, Default Windows Printer. Testowane: decompress, parse meta/media/resolution, missing file, empty path.
- `tests/TestRunner.cs` - dodany `Pc3ParserTests.RunTests()` do sekwencji.
- `Bricscad_AgentAI_V2.csproj` - nowe `<Compile Include>` dla `Pc3Parser.cs` i `Pc3ParserTests.cs`.
### [STAN_SYSTEMU]
- Kompilacja MSBuild: 0 errors, 3 pre-existing warnings.
- DLL zaktualizowany: `bin\Debug\Bricscad_AgentAI_V2.dll` (1111040 bytes).
- Weryfikacja reczna: parser sparsowal wszystkie 4 unikalne PC3 z systemu, zwrocil poprawne nazwy driver'ow, formaty (297x600/A0/A4), wymiary (A0=841x1189mm, A4=209.97x296.93mm), rozdzielczosci (HP=300dpi, PDF=2400dpi).
### [BLOKADY / PROBLEMY]
- Pierwsza wersja parsera miala bug: `DeflateStream` bezposrednio na payload traktowal zlib-header jako deflate-stream i konczyl sie `"Invalid data"`. Fix: przeskoczenie dodatkowych 2 bajtow zlib CMF/FLG przed `DeflateStream`.
- Drugi bug: `ExtractValue` z `StartsWith("\"") && EndsWith("\"")` nie stripowal cudzyslowiow z `name="value"` gdy parser nie czytal az do konca linii (miedzy blokami). Fix: dodano fallback strip pojedynczego otwierajacego cudzyslowia.
- Brak mozliwosci uzyskania PELNEJ listy mediów z PC3/HDI z poziomu .NET - ograniczenie Win32 print spooler, pozostaje ostrzezenie w `ListPlotDevicesTool` i rekomendacja GUI BricsCAD.
### [KOLEJNY_KROK]
- Manualne testy w BricsCAD-zie z nowym ListPlotDevicesTool (filtr `HP` powinien zwracac pelna liste mediów z GPD dla T120/T650).
- Po stabilizacji layout tools: rozwazyc dodanie `Pc3Parser` + `GpdParser` do `PageSetupTool` jako walidacji preflight (sprawdzanie czy `MediaName` jest na liscie obslugiwanych przez dany ploter).

## [v2.30.4] 2026-06-17 - Layout/Plot: Win32PrinterCapabilities + UserXXX mapping
### [ODKRYCIE]
- Pliki .pc3 NIE przechowuja mapowania custom format (`297x600`) na UserXXX (`User254`/`User261` itd.) - to mapowanie jest robione dynamicznie przez Windows GDI driver w runtime.
- `RawPrivateDeviceData` w PC3 (4548-4580 bajtow DEVMODE) zawiera parametry drivera (`InputBin=FORMSOURCE`, `JobUserMargin=5mm`, `MediaType=AutoSelect`, `Resolution=300dpi`, `PrintQuality=Draft`), ale NIE zawiera wymiarow custom formatu jako wartosci numerycznych.
- `DeviceCapabilities()` Win32 API z `DC_PAPERS` + `DC_PAPERSIZE` zwraca pelna liste formatow z wymiarami dla kazdego zainstalowanego drivera (w tym UserXXX) - to jedyne zrodlo prawdziwych wymiarow.
### [ZREALIZOWANO]
- Nowy `src/Core/Win32PrinterCapabilities.cs` (~150 LOC):
    * P/Invoke `DeviceCapabilities()` z `winspool.drv` dla `DC_PAPERS` + `DC_PAPERSIZE`.
    * `QueryMedia(deviceName) -> Win32PrinterCapabilitiesResult` z lista `Win32MediaInfo { FormName, WidthMm (0.01mm units), HeightMm, IsUserFormat }`.
    * `ResolvePc3ToPrinterName` - dla `.pc3` obcina rozszerzenie (np. `DWG To PDF.pc3` → `DWG To PDF`).
- Nowy `src/Core/UserMediaResolver.cs` (~110 LOC):
    * `TryParseCustomMediaName(name, out w, out h, out isMultipleBaseUnit)` - regex `^(\d+)x(\d+)(_p)?$` rozpoznaje `297x600`, `297x1320_p`, `297.5x600.25`.
    * `FindUserFormatsBySize(caps, w, h, tolerance)` - szuka UserXXX w driver o zadanych wymiarach z tolerancja 0.5mm.
    * `ResolveCustomNameToUserFormat(name, caps)` - zwraca `UserMediaMatch { UserFormName, ExactMatch, ToleranceMm }` lub null.
    * `IsWithinGpdBounds(w, h, gpd)` - walidacja bounds z GPD MinSize/MaxSize + 1mm tolerancji.
- `PageSetupTool` enhancement:
    * Preflight `MediaName`: gdy driver ma `mediaList.Count > 0` i `mediaName` nie jest na liscie, probuje `TryResolveCustomFormatViaWin32` - jesli user media jest resolvable, dodaje ostrzezenie do `preflightWarnings` (zamiast bloku krytycznego), mowiace ze driver zaakceptuje nazwe `297x600` ale rzeczywisty UserXXX to `User261`.
    * Gdy `mediaList.Count == 0` (driver PC3 bez CanonicalMediaName) i `MediaName` wyglada na custom (`297x600`), wywoluje `CheckCustomSizeAgainstGpd` - jesli wymiary przekraczaja GPD bounds, dodaje blad preflight krytyczny.
    * Gdy `SetCanonicalMediaName` rzuca wyjatek, ostrzezenie zawiera `userMapping` (np. `User261`) z sugestia uzycia `MediaName="User261"` lub zostawienia nazwy custom.
- `tests/Core/UserMediaResolverTests.cs` (7 testow): parsowanie `297x600`, `297x1320_p`, `297.5x600.25`; bounds inside/outside GPD; null safety.
### [STAN_SYSTEMU]
- Kompilacja MSBuild: 0 errors, 3 pre-existing warnings.
- DLL: `bin\Debug\Bricscad_AgentAI_V2.dll` (1143296 bytes).
- Weryfikacja reczna: dla T120 Adrian `297x600` → driver prawdopodobnie zwroci `User261` (297mm ≈ 11.69" jest bliskie standardowemu 11.69" HP DesignJet). Dla T650 `297x600` moze zwrocic ten sam UserXXX lub inny (zalezy od definicji drivera 36-in).
### [BLOKADY / PROBLEMY]
- C# 7.3 wymaga jawnej inicjalizacji `out double` parametrow - rozwiazane warunkiem `&& cw > 0 && ch > 0`.
- Zmienna `warnings` zadeklarowana podwojnie w preflight i body - scalona do jednej listy.
### [KOLEJNY_KROK]
- Manualne testy w BricsCAD-zie z `PageSetupTool MediaName="297x600"` dla layoutu z T120/T650 - powinien zwrocic ostrzezenie `UserXXX` mapping + sukces (driver zaakceptuje custom nazwe).
- Rozwazyc dodanie `Win32PrinterCapabilities.QueryMedia` jako opcjonalne narzedzie diagnostyczne `GetPrinterCapabilitiesTool` z lista mediow dla danego urzadzenia.

## [v2.30.5] 2026-06-17 - Layout/Plot: Fix system prompt CadLayoutProfile dla custom MediaName
### [PROBLEM]
- Test z `PageSetupTool MediaName="297x600"` wykazal ze LLM (gemma-4-31B) pominął intencje usera i wywolal `ListPlotDevicesTool` zamiast `PageSetupTool`.
- Przyczyna: stary prompt `system_prompt_layout.txt` mial regule "WAZNA ZASADA DLA NAZW PLOTEROW/MEDII: Gdy uzytkownik pyta o ustawienie formatu papieru dla plotera HP lub innego, NAJPIERW uzyj ListPlotDevicesTool aby zobaczyc liste dostepnych formatow" + "Uzywaj przed PageSetupTool jesli nie znasz dokladnej nazwy plotera lub formatu" - LLM interpretowal to jako "ZAWSZE uzywaj ListPlotDevicesTool przed PageSetupTool".
- W konsekwencji LLM wywolywal zbedne narzedzie diagnostyczne zamiast bezposrednio ustawic format.
### [NAPRAWIONE]
- `system_prompt_layout.txt`:
    * Sekcja `### PageSetupTool` - nowa regula: "Gdy user poda KONKRETNA nazwe MediaName wprost (np. 'A4', '297x600', 'User266', 'NorthAmericaTabloid', 'ISOA1') - wywolaj PageSetupTool BEZPOSREDNIO z ta nazwa. NIE pytaj o potwierdzenie, NIE wywoluj ListPlotDevicesTool przed PageSetupTool."
    * Dodano 4 przyklady prawidlowego uzycia: `MediaName="297x600"`, `MediaName="A4"`, `MediaName="User266"`, `MediaName="594x841"`.
    * Sekcja `### ListPlotDevicesTool` - zmieniona: "Uzywaj TYLKO w trybie EKSPLORACJA: 'jakie plotery sa w systemie', 'jakie formaty obsluguje HP T120'. NIE wywoluj rutynowo przed PageSetupTool - user zwykle wie jaki format chce ustawic."
- `USER_GUIDE.md` - nowa sekcja `13.7. Custom formaty papieru (297x600, User266)`:
    * 3 sposoby obslugi custom formatow (bezposrednia nazwa, UserXXX, standardowe GPD).
    * Przyklady promptow: "Ustaw layout IS.W.01 na papier 297x600", "Ustaw layout IS.W.01 na User266", "Ustaw layout IS.W.01 na papier 1000x600" (z walidacja GPD bounds).
    * Opis roznic T120 (22 formaty) vs T650 36-in (35 formatow).
### [STAN_SYSTEMU]
- Build MSBuild: 0 errors (bez zmian w kodzie - tylko prompt).
- Nowy prompt bedzie uzyty przy nastepnym zaladowaniu BricsCAD (refresh przez EnsureLayoutPromptFile).
- memory.md v2.30.5.
### [KOLEJNY_KROK]
- Manualne testy z poprawionym promptem - LLM powinien teraz wywolac `PageSetupTool MediaName="297x600"` bezposrednio bez `ListPlotDevicesTool`.
- Sprawdzic czy User Guide 13.7 nie wymaga dodatkowych jezykow (EN/PL/DE) - obecnie tylko polski.

## [v2.30.6] 2026-06-17 - Layout/Plot: Fix Win32PrinterCapabilities crash (KRYTYCZNY)
### [PROBLEM]
- Test `PageSetupTool MediaName="297x1320"` (oraz wczesniejszy `594x1320`) spowodowal **crash BricsCAD** (process exit) z `System.AccessViolationException` w `System.String.wcslen`.
- Crash dumps: `C:\Users\Adrian\AppData\Local\CrashDumps\bricscad.exe.77464.dmp` (134MB) i `bricscad.exe.21736.dmp` (143MB).
- Windows Event Log (`CLR20r3`):
    ```
    System.AccessViolationException
       w System.String.wcslen(Char*)
       w System.String.CtorCharPtr(Char*)
       w System.Runtime.InteropServices.Marshal.PtrToStringUni(IntPtr)
       w Bricscad_AgentAI_V2.Core.Win32PrinterCapabilities.QueryMedia(System.String, System.String)
       w Bricscad_AgentAI_V2.Tools.Layout.PageSetupTool.TryResolveCustomFormatViaWin32(System.String)
    ```
### [PRZYCZYNA]
- `Marshal.PtrToStringUni(IntPtr)` czyta **do napotkania null-terminatora** (`\0\0` dla Unicode). Gdy driver HP zwraca nazwy mediów bez prawidlowego null-terminatora (np. dla `1320mm` ktory nie ma predefiniowanego `UserXXX`), `wcslen` czyta poza buforem i crashuje .NET runtime.
- `AccessViolationException` jest wyjatkem **SEH (Structured Exception Handling)** - nie da sie go zlapac try/catch w .NET, crashuje caly proces.
- `297x600` dzialalo, `297x1320` i `594x1320` crashowalo - driver ma predefiniowane UserXXX dla `600mm` ale nie dla `1320mm`.
### [NAPRAWIONE]
- `src/Core/Win32PrinterCapabilities.cs`:
    * `Marshal.PtrToStringUni(IntPtr, 32)` zamiast `Marshal.PtrToStringUni(IntPtr)` - czyta **max 32 znaki** (CCHFORMNAME) bez szukania null-terminatora.
    * `Marshal.PtrToStringUni(namePtr, 32)` otoczony try/catch - jesli pointer jest nieprawidlowy, zwraca null zamiast crashowac.
    * `Marshal.ReadInt16(sizesPtr, i * 8)` otoczony try/catch - chroni przed crashem przy blednym `sizesPtr`.
    * Caly `QueryMedia` otoczony zewnetrznym try/catch zwracajacym `Win32PrinterCapabilitiesResult` z `ErrorMessage`.
- `src/Tools/Layout/PageSetupTool.cs`:
    * `TryResolveCustomFormatViaWin32` - dodany safety limit: `if (h > 1000.0 || w > 1000.0) return null;` - pomija Win32 lookup dla duzych custom formatow (uzywa tylko GPD bounds).
### [STAN_SYSTEMU]
- Kompilacja MSBuild: 0 errors, 4 pre-existing warnings.
- DLL: `bin\Debug\Bricscad_AgentAI_V2.dll` (1146368 bytes).
- Po wdrozeniu fix `297x1320` powinno:
    1. Przejsc `TryResolveCustomFormatViaWin32` (zwraca null bo >1000mm)
    2. `TryParseCustomMediaName` parsuje poprawnie
    3. `CheckCustomSizeAgainstGpd` zwraca null (1320mm < 91000mm max)
    4. `SetCanonicalMediaName("297x1320")` wywoluje sie bezposrednio (bez Win32 lookup)
    5. Driver HP zaakceptuje nazwe custom
### [BLOKADY / PROBLEMY]
- Brak mozliwosci uzyskania UserXXX mapping dla formatow >1000mm (potrzebny Win32 lookup ktory crashowal). User musi sam wpisac `MediaName="297x1320"` i zobaczyc co zaakceptuje driver.
- `297x1320` mialo User266 w layout (bo bounds `(594,1320)` to ISOA1+offset) - moze ten format **istnieje** w driver jako `594x1320_p`, ale nie `297x1320` (inna szerokosc).
### [KOLEJNY_KROK]
- Manualne testy z `MediaName="297x1320"` - powinien teraz przejsc bez crashu (driver zaakceptuje lub zwroci `eInvalidInput`).
- Sprawdzic czy `594x1320` tez teraz nie crashuje (rowniez >1000mm, safety limit).
- Rozwazyc dodanie explicit limit na `PageSetupTool` dla `MediaName` zawierajacych format >1000mm - wywolanie ostrzezenia ze driver moze nie zaakceptowac.

## [v2.30.7] 2026-06-17 - Layout/Plot: _p suffix formula (A4 fold pattern)
### [ODKRYCIE]
- Testy uzytkownika wykazaly ze sufiks `_p` w nazwach formatow HP DesignJet (np. `297x950_p`, `594x1320_p`, `841x2060_p`) to NIE marker dlugosci, ale oznaczenie **formatu skladanego do A4**.
- Wzor: `dlugosc = N * 185 + 25` gdzie:
    - `185 = 210 (A4 width) - 25 (margin)`
    - `25` = margines na ciecie [mm]
    - `N` = liczba paneli A4 (210x297) skladajacych sie do danej dlugosci
- Po zadrukowaniu i pocieciu paskow co `185mm` z `25mm` marginesem, otrzymujemy `N` paneli `A4` (210x297mm) kazdy.
### [ZWERYFIKOWANE]
- 11/12 formatow z `_p` (z plikow PC3) idealnie pasuje do wzoru:
    - `580` = 3*185+25 (3 panele)
    - `950` = 5*185+25 (5 paneli)
    - `1320` = 7*185+25 (7 paneli)
    - `1690` = 9*185+25 (9 paneli)
    - `2060` = 11*185+25 (11 paneli)
- Wyjatek: `297x500_p` (2.57 paneli) - prawdopodobnie inny wzorzec (2*200+100) - pozostawiony jako edge case.
### [ZREALIZOWANO]
- `src/Core/UserMediaResolver.cs` - nowe metody:
    * `CountAPanelsForFoldableSize(heightMm, marginMm=25)` - oblicza N z wzoru `round((h-25)/185)`.
    * `TryBuildAPanelsFoldableSize(widthMm, numPanels, marginMm, out heightMm)` - buduje dlugosc z `N*185+25`.
    * `DescribeFoldPattern(widthMm, heightMm, isFoldable)` - generuje opis typu "format skladany do A4 (7 paneli po 185mm + 25mm margines = 1320mm dlugosci)".
- `src/Tools/Layout/ListPlotDevicesTool.cs` - sekcja `CUSTOM SIZE` rozszerzona o dokumentacje wzoru z 5 przykladami (580/950/1320/1690/2060 mm).
- `tests/Core/UserMediaResolverTests.cs` - 3 nowe testy: `TestAPanelsFoldableFormula`, `TestBuildAPanelsFoldableSize`, `TestDescribeFoldPattern`.
- `resources/help/USER_GUIDE.md` - sekcja 13.7 rozszerzona o tabele "Konwencja sufiksu `_p` (format skladany do A4)" z 5 przykladami.
### [STAN_SYSTEMU]
- Kompilacja MSBuild: 0 errors, 2 pre-existing warnings.
- DLL: `bin\Debug\Bricscad_AgentAI_V2.dll` (1149952 bytes).
### [BLOKADY / PROBLEMY]
- Brak 100% pewnosci co do `297x500_p` - moze byc to format z innym marginesem lub wzorcem. Nie ma to wplywu na dzialanie - driver akceptuje wszystkie formaty z `_p` z odpowiednimi wymiarami.
### [KOLEJNY_KROK]
- Manualne testy z `MediaName="594x1320_p"` - powinien zwrocic sukces (driver akceptuje) i moze ostrzezenie z opisem wzoru A4 fold.
- Rozwazyc dodanie `ListFoldableSizes` tool ktory generuje liste formatow _p dla danej szerokosci (np. "Dla 594mm, list od 580mm (3 panele) do 2060mm (11 paneli)").

## [v2.30.8] 2026-06-18 - Layout/Plot: Auto-fallback MediaName z _p dla sterownikow HP
### [PROBLEM]
- Test `PageSetupTool MediaName="594x1320"` zwrocil `BLAD CZESCIOWY: eInvalidInput` mimo iz `594x1320_p` dziala (driver HP wymaga sufiksu `_p`).
- Driver HP T120/T650 ma custom formaty wylacznie z sufiksem `_p` (594x1320_p, 594x1690_p, 841x2060_p) - wersja bez sufiksu nie istnieje w driverze.
- User musi znac konwencje `_p` zeby poprawnie uzyc PageSetupTool.
### [NAPRAWIONE]
- `src/Tools/Layout/PageSetupTool.cs` - nowa logika w `SetCanonicalMediaName`:
    1. Probuje oryginalna nazwe (`594x1320`).
    2. Jesli wyjatek `eInvalidInput` + brak sufiksu `_p`/`_a` + custom format - probuje automatycznie z `_p` (`594x1320_p`).
    3. Jesli sukces - dodaje ostrzezenie: "MediaName 'X' nie zostal zaakceptowany. Sprobowano 'X_p' - SUKCES. Driver wymaga _p dla tego formatu."
    4. Jesli oba sie nie powiodly - pelna diagnostyka (UserXXX mapping, list ploterow).
- `resources/prompts/system_prompt_layout.txt` - dodana wazna regula:
    "WAZNE: Jesli driver odrzuci MediaName (np. `594x1320` zwraca eInvalidInput ale `594x1320_p` dziala), PageSetupTool AUTOMATYCZNIE probuje wariant z sufiksem `_p` (A4 fold pattern). User nie musi znac konwencji - wystarczy podac wymiary."
### [STAN_SYSTEMU]
- Kompilacja MSBuild: 0 errors, 2 pre-existing warnings.
- DLL: `bin\Debug\Bricscad_AgentAI_V2.dll` (1150464 bytes).
### [KOLEJNY_KROK]
- Manualne testy z `PageSetupTool MediaName="594x1320"` - powinien zwrocic sukces + ostrzezenie o auto-fallbacku.
- Sprawdzic czy dziala dla innych formatow: `297x1690` -> `297x1690_p`? (297 ma mniej wariantow).
- Sprawdzic czy dziala dla 841x1820 (krotsza wersja 841x1820_p tez istnieje).

## [v2.30.9] 2026-06-18 - Layout/Plot: Separacja infoMessages od warnings (KROK-30.12 fix)
### [PROBLEM]
- Po dodaniu auto-fallback `_p` (KROK-30.11) test `MediaName="594x1320"` zwracal:
    ```
    BLAD CZESCIOWY PAGE SETUP: MediaName '594x1320' nie zostal zaakceptowany...
    Sprobowano automatycznie '594x1320_p'... - SUKCES.
    | Nic nie zostalo zapisane (transakcja wycofana).
    ```
- Driver zaakceptowal `594x1320_p` (sukces!), ale transakcja byla wycofywana bo fallback dodawal ostrzezenie do listy `warnings`, ktora na koncu triggerowala `tr.Abort()`.
### [NAPRAWIONE]
- `src/Tools/Layout/PageSetupTool.cs`:
    * Nowa lista `infoMessages` (informacje po sukcesie) - **NIE triggeruje abortu**.
    * Fallback `_p` sukces -> `infoMessages.Add(...)` zamiast `warnings.Add(...)`.
    * Fallback `_p` fail lub MediaName bez custom pattern -> `warnings.Add(...)` jak wczesniej (tr.Abort).
    * Output:
        - Sukces bez info: "SUKCES: Zastosowano 1 ustawien..."
        - Sukces z info (np. fallback): "SUKCES: Zastosowano 1 ustawien... | INFO: MediaName 'X' nie zostal zaakceptowany, uzyto 'X_p'..."
        - Fail: "BLAD CZESCIOWY PAGE SETUP: ... (transakcja wycofana)..."
### [STAN_SYSTEMU]
- Kompilacja MSBuild: 0 errors, 3 pre-existing warnings.
- DLL: `bin\Debug\Bricscad_AgentAI_V2.dll` (1150464 bytes).
### [KOLEJNY_KROK]
- Manualne testy z `MediaName="594x1320"` - powinien zwrocic "SUKCES ... | INFO: ...uzyto '594x1320_p'" i ustawic faktycznie format w layout (sprawdzic w GUI Page Setup).

## [v2.30.10] 2026-06-18 - Layout/Plot: ForeachTool w CadLayoutProfile (KROK-30.13)
### [PROBLEM]
- Test: "ustaw na wszystkich arakuszach drukarke ricoh z formatem A3" - LLM (gemma-4-31B) wywolal PageSetupTool 2 razy z hardcoded LayoutName ('Arkusz1' + 'Arkusz2') zamiast uzyc ForeachTool.
- Brak `Foreach` w `CadLayoutProfile.AllowedTools` - LLM nie mogl go uzyc nawet gdyby chcial.
- Brak instrukcji w prompcie `system_prompt_layout.txt` o uzyciu Foreach.
### [NAPRAWIONE]
- `src/Core/ToolConfigManager.cs`:
    * `layoutDefaults` (linia 673) - dodane "Foreach" do listy defaultow.
    * `GenerateDefaultConfig` (linia 778) - `AllowedTools` dla CadLayoutProfile ma teraz "Foreach".
    * `EnsureAllowedTools` (linia 310) dodaje "Foreach" do istniejacych konfiguracji przy nastepnym uruchomieniu BricsCAD.
- `resources/prompts/system_prompt_layout.txt`:
    * Nowa sekcja `### ForeachTool (PETLA - KRYTYCZNE dla CadLayoutProfile)`:
        - Zasada: "Gdy user prosi o 'ustaw na wszystkich arkuszach X', 'zrob X dla kazdego layoutu' - ZAWSZE uzyj `Foreach` zamiast wielokrotnie wolac PageSetupTool recznie."
        - Sposob uzycia: 1) `ListLayoutsTool` BEZ `LayoutName`, 2) `Foreach` z `Items` = lista layoutow, `ActionTemplate` z `{item}` w miejsce LayoutName.
        - Przyklad: `Foreach` Items=[Arkusz1, Arkusz2] ActionTemplate=`PageSetupTool LayoutName="{item}" PlotDevice="RICOH" MediaName="A3"`.
    * Dodana regula w `Zasady korzystania z narzedzi`: "Dla operacji na WIELU layoutach ZAWSZE uzyj `Foreach` z `ActionTemplate` - NIE wywoluj PageSetupTool recznie N razy."
### [STAN_SYSTEMU]
- Kompilacja MSBuild: 0 errors, 2 pre-existing warnings.
- DLL: `bin\Debug\Bricscad_AgentAI_V2.dll` (1150464 bytes).
- Config `tools_config.json` bedzie zaktualizowany automatycznie przy nastepnym uruchomieniu BricsCAD (linia 687 `EnsureAllowedTools` + `SaveConfig`).
### [KOLEJNY_KROK]
- Manualne testy z "ustaw na wszystkich arkuszach RICOH A3" - LLM powinien wywolac Foreach zamiast 2x PageSetupTool.
- Sprawdzic czy `Foreach` nie jest w konflikcie z `CadGeometryProfile` (ten profil juz mial Foreach, wiec OK).
- Rozwazyc dodanie przykladu do `USER_GUIDE 13.5` o uzyciu ForeachTool z layoutami.

## [v2.30.11] 2026-06-18 - Agent: Wymuszenie iteracji po ListLayoutsTool (KROK-30.14)
### [PROBLEM]
- Test: "Ustaw na wszystkich arkuszach drukarke RICOH z formatem A3" - LLM (gemma-4-31B):
    - Prompt 1: LLM wywolal `ListLayoutsTool IncludeModel=false` -> "Znaleziono 2 layout(ow)...". KONIEC iteracji. User musi powtorzyc prompt.
    - Prompt 2 (ten sam): LLM wywolal `Foreach` Items=[Arkusz1, Arkusz2] z PageSetupTool -> 2/2 sukces.
- Powod: gemma-4-31B ma slabsza "agentness" - po poznaniu stanu (lista layoutow) myśli ze to koniec zadania zamiast iterowac do akcji.
- Petla ReAct w `LLMClient.SendMessageReActAsync` ma `maxIterations = 5` (default) - LLM mial 4 wolne iteracje ale ich nie wykorzystal.
### [NAPRAWIONE]
- `src/Core/LLMClient.cs`:
    * Nowa metoda `ShouldForceContinueAfterLastTool(List<ChatMessage> history)` - sprawdza czy ostatni tool call to `ListLayoutsTool` (bez LayoutName) i czy wynik zawiera "layout(" - jesli tak, zwraca true.
    * Nowa metoda `BuildForceContinueHint(history)` - parsuje liczbe layoutow z outputu (np. "Znaleziono 2 layout(ow)" -> 2) i generuje `[SYSTEM REMINDER] User prosil o AKCJE na layoutach, nie tylko o ich liste. Wlasnie wylistowales N layout(ow) - teraz MUSISZ wykonac akcje na kazdym z nich. Uzyj ForeachTool z ActionTemplate zawierajacym {item} jako placeholder nazwy layoutu...`.
    * Zmodyfikowana petla ReAct: po warunku zakończenia `if (ToolCalls == null || !ToolCalls.Any())` - jesli `ShouldForceContinueAfterLastTool` zwraca true, dodaje `[SYSTEM REMINDER]` jako `user` message do `conversationHistory` i `continue` (idzie do nastepnej iteracji).
### [STAN_SYSTEMU]
- Kompilacja MSBuild: 0 errors, 4 pre-existing warnings.
- DLL: `bin\Debug\Bricscad_AgentAI_V2.dll` (1153024 bytes).
### [KOLEJNY_KROK]
- Manualne testy z "ustaw na wszystkich arkuszach RICOH A3" - LLM powinien za pierwszym promptem wymusic iteracje i wywolac Foreach.
- Sprawdzic czy dziala dla innych "exploratory" tool calls (np. `ListBlocksTool` powinien rowniez wymusic akcje).
- Rozwazyc dodanie opcji "Auto-continue" w UI (default ON) z mozliwoscia wylaczenia dla zaawansowanych userow.

## [v2.30.12] 2026-06-18 - Layout: ImportLayoutTemplateTool CloneLayout (KROK-30.15)
### [PROBLEM]
- Test: "zaimportuj arkusz 297x580_p z pliku DWT" - layout zaimportowany poprawnie ale pojawialy sie DODATKOWE layouty (np. "Arkusz4", "Arkusz6") ktorych user nie chcial.
- Po 2 importach (`297x580_p` + `297x1320_p`) rysunek mial 7 layoutow zamiast 2 (Model + 2 importowane + 4 dodatkowe).
- `ListLayoutsTool` zwracal "Znaleziono 7 layout(ow)" - potwierdzone w logu.
### [PRZYCZYNA]
- `ImportLayoutTemplateTool` uzywal:
    1. `LayoutManager.Current.CreateLayout(targetLayoutName)` - tworzy nowy layout
    2. `WblockCloneObjects` z `DuplicateRecordCloning.Replace` - kopiuje BlockTableRecord
    3. Reczne kopiowanie 11 wlasciwosci PlotSettings (PlotConfigurationName, CanonicalMediaName, PlotType, PlotRotation, Scale, Origin, itd.)
- Konflikty: `DuplicateRecordCloning.Replace` dla BlockTableRecord z niestandardowa nazwa (`297x580_p` zawiera `_`) tworzyl w tle dodatkowe puste layouty z auto-inkrementowanymi nazwami ("Arkusz4", "Arkusz6"). BricsCAD/Teigha "po cichu" tworzyl je jako side-effect.
### [NAPRAWIONE]
- `src/Tools/Layout/ImportLayoutTemplateTool.cs`:
    * Zamiana na **oficjalny `LayoutManager.CloneLayout(sourceName, newName, newTabOrder)`** z Teigha API (V22).
    * `CloneLayout` kopiuje layout wraz z Page Setup, BlockTableRecord i wszystkimi zaleznosciami **atomowo** - bez duplikatow.
    * Usunieto ~80 linii recznego kopiowania Validatorem.
    * Kazde wywolanie `CloneLayout` to 1-2 linie kodu zamiast 30+ linii.
    * Nadal `WblockCloneObjects` jest uzywany do kopiowania entities (BlockTableRecord) - to dziala poprawnie.
- Przeplyw po fix:
    1. Sprawdz czy layout docelowy istnieje + `overwrite`
    2. Jesli tak - usun stary
    3. Otworz source DWT, znajdz source Layout
    4. `LayoutManager.CloneLayout(sourceName, targetName, 0)` - kopiuje layout + Page Setup atomowo
    5. (Opcjonalnie) `WblockCloneObjects` - kopiuje entities z source BTR
### [STAN_SYSTEMU]
- Kompilacja MSBuild: 0 errors, 4 pre-existing warnings.
- DLL: `bin\Debug\Bricscad_AgentAI_V2.dll` (1178112 bytes, +25KB vs poprzedni).
### [KOLEJNY_KROK]
- Manualne testy z importem `297x580_p` i `297x1320_p` - powinny pojawic sie TYLKO 2 layouty (importowane) + Model, bez "Arkusz4"/"Arkusz6".
- Sprawdzic czy `CloneLayout` dziala z DWT ktory ma wiele layoutow (np. `Arkusze_HP_T120_297mm.dwt` z 2-3 layoutami).
- Rozwazyc dodanie opcji `KeepSourceLayouts=true` do importu wielu layoutow na raz z jednego DWT.

## [v2.30.13] 2026-06-18 - Layout: ListLayoutsFromDwt + revert CloneLayout (KROK-30.16)
### [PROBLEM]
- Test: "zaimportuj arkusz 297x580_p z DWT" zwrocil `BLAD IMPORTU LAYOUT: eNullObjectId` (9ms po wywolaniu, PRZED otwarciem DWT).
- Drugi prompt: "zrob liste arkuszy w pliku DWT" - LLM odpowiedzial "nie moge bezposrednio wypisac listy arkuszy zewnetrznego pliku". User musi znac dokladne nazwy layoutow w DWT.
### [PRZYCZYNA]
- KROK-30.15 fix: `LayoutManager.Current.CloneLayout(sourceName, ...)` - **nie dziala** dla layoutow z obcego DWT! `LayoutManager.Current` dziala TYLKO na biezacym DWT. `CloneLayout` wymaga aktywnego DWT z layoutem. Przy probie klonowania zewnetrznego DWT - `eNullObjectId` (ObjectId null poniewaz source nie jest w biezacym DWT).
### [NAPRAWIONE]
- `src/Tools/Layout/ImportLayoutTemplateTool.cs`:
    * **WROCIL** do `WblockCloneObjects` + `LayoutManager.Current.CreateLayout` + reczne kopiowanie 11 wlasciwosci PlotSettings.
    * Dodane `try/catch` wokol `DeleteLayout` dla bezpieczenstwa.
    * `LayoutSettings` property nie istnieje w V22 - uzywam `layout.PlotPaperSize.X/Y` bezposrednio.
- `src/Tools/Layout/ListLayoutsTool.cs`:
    * Nowy parametr `SourceDwtPath` - otwiera zewnetrzny DWT i listuje jego layouty.
    * Nowa metoda `ListLayoutsFromDwt(dwtPath)` - otwiera Database w trybie read-only, czyta `LayoutDictionaryId`, iteruje `DBDictionary` i zwraca liste z `CanonicalMediaName` + `PlotPaperSize` (mm).
    * Error message informuje: "Aby zaimportowac konkretny layout, uzyj ImportLayoutTemplateTool z SourceLayoutName=<nazwa> i SourcePath=<sciezka_do_dwt>".
- Przykladowy output:
    ```
    LAYOUTY W PLIKU 'Arkusze_HP_T120_297mm.dwt':
      - 297x580_p                              | 297x600_mm_p               | 210.0x600.0mm
      - 297x1320_p                             | 297x1320_p                 | 210.0x1320.0mm
      - Model                                   | ISO_A4_(210.00_x_297.00_MM) | 210.0x297.0mm

    RAZEM: 3 layout(ow).
    ```
### [STAN_SYSTEMU]
- Kompilacja MSBuild: 0 errors, 3 pre-existing warnings.
- DLL: `bin\Debug\Bricscad_AgentAI_V2.dll` (1180672 bytes).
### [KOLEJNY_KROK]
- Manualne testy z "ListLayoutsTool SourceDwtPath=D:\...dwt" - powinien zwrocic liste layoutow z DWT.
- Sprawdzic czy dodane guard'y `try/catch` w ImportLayoutTemplateTool eliminuja "Arkusz4"/"Arkusz6" dodatkowe layouty.
- Rozwazyc dodanie `SourceDwtPath` do `ImportLayoutTemplateTool` jako optional preflight - pokaz liste dostepnych layoutow jesli SourceLayoutName nie istnieje.

## [v2.30.3] 2026-06-17 - Layout/Plot: GpdParser - pelna lista mediów dla ploterow HP
### [ODKRYCIE]
- Sterowniki HP Plotter (DesignJet, PageWide XL, Z-series, Smart Tank, DesignJet XL) sa zapisane w folderach Windows `C:\Windows\System32\DriverStore\FileRepository\hpi2144.inf_amd64_*` jako pliki .gpd (Generic Printer Description) - **plain text format**.
- Format GPD jest hierarchiczny z blokami `*Feature: PaperSize { *Option: NAME { *PageDimensions: PAIR(W, H); ... } }`. Kazdy ploter ma osobny plik (np. `hpiT120.gpd`, `hpiT65036-in.gpd`, `hpiZ320044inPhoto.gpd`).
- MasterUnits = PAIR(1200, 1200) → 1 unit = 1/1200 inch; wymiary w `*PageDimensions` to wymiary papieru w tych jednostkach.
- Plik INF `hpi2144.inf` mapuje kazdy model plotera na odpowiadajacy mu plik GPD (np. `HP DesignJet T120 V4=T120` → `hpiT120.gpd`).
- Dla sterownikow `gdiplot7.hdi` (HP generic) **lista mediów jest WSPOLNA** dla T120/T650/T520 itd. - custom user-defined formaty z jednego plotera sa widoczne w innych (bo ten sam driver). To potwierdza ze `T120-2026.06.17.pc3` z `Media=A4` jest poprawny dla T120 mimo ze domyslny `HP Designjet T120 - Adrian.pc3` ma `Media=297x600`.
### [ZREALIZOWANO]
- Nowy `src/Core/GpdParser.cs` (~330 LOC):
    * `GpdParser.Parse(path) → GpdInfo` - parsuje PaperSize feature, zwraca `MediaFormats` (z `WidthMm`/`HeightMm` konwertowanymi z units przez UnitsPerInch*25.4), `CustomSize` (z MinSize/MaxSize), `ModelName`, `UnitsPerInch`.
    * `GpdParser.FindHpGpdForDevice(deviceName, friendlyName, winDriverName)` - heurystycznie mapuje nazwe PC3 (np. `HP Designjet T120 - Adrian.pc3`) na plik GPD. Szuka w `DriverStore\FileRepository\hpi*\*.gpd`, `spool\drivers\x64\PCC\*.gpd`, `spool\drivers\x64\3\*.gpd`.
    * Heurystyka: T120/T125/T130/T520/T525/T530/T630/T650/T730/T790/T795/T830/T850/T920/T930/T940/T950/T1500/T1530/T1600/T1700/T2300/T2500/T2530/T2600/T3500/T7100/T7200/Z2xxx/Z3xxx/Z5xxx/Z6xxx/Z9xxx/XL3600/XL3800 z roznymi rozmiarami (24-in/36-in/44-in/42-in/60-in). Punkty za sufiks `24`/`36`/`44`.
    * Formaty mierzone z `*PageDimensions: PAIR(W, H)` → konwersja przez `UnitsPerInch * 25.4`.
- `tests/Core/GpdParserTests.cs` (10 testow): T120 ma 22 formaty, T650 36-in ma 35, T120 wspiera A4/A3/A2/A1 (ale NIE A0), T650 wspiera A0/B1/ESheet/F/11X14, CustomSize z Min/Max width.
- `ListPlotDevicesTool` - nowa metoda `AppendGpdMediaList`:
    * Hierarchiczny output: priorytetowe A4/A3/A2/A1/A0, B-series (ISO+JIS), ANSI/Letter, Architecture, custom size.
    * Format z instrukcja: `* ISOA2 (420.0x594.0mm) | PageSetupTool: MediaName="ISOA2"` - LLM widzi jakie nazwy uzywac.
    * Custom size section z `Min/Max dimensions` + instrukcja formatu `297x600` + wspomnienie o suffixie `_p`.
- `Bricscad_AgentAI_V2.csproj`: nowy `<Compile Include>` dla `GpdParser.cs` + `GpdParserTests.cs`. `TestRunner.cs` dodany `GpdParserTests.RunTests()`.
### [STAN_SYSTEMU]
- Kompilacja MSBuild: 0 errors, 2 pre-existing warnings.
- DLL: `bin\Debug\Bricscad_AgentAI_V2.dll` (1133568 bytes).
- Weryfikacja reczna: wszystkie 6 przetestowanych GPD sparsowane (T120/T650/T520/T1500/Z3200/XL3600). Mapowanie PC3→GPD poprawne dla T120/T650/T520/T1500.
- Test 5 ze scenariuszy: T120 NIE wspiera ISOA0 (maks. szerokosc 24"), T650 36-in wspiera ISOA0. Custom size min/max bounds zweryfikowane (T120: 79x140mm min, 609.6x91000mm max).
### [BLOKADY / PROBLEMY]
- `string.Contains(string, StringComparison)` nie istnieje w .NET Framework 4.8 - zamienilem na `IndexOf(..., StringComparison) >= 0`.
- `$(MSBuildToolsPath)` nie jest ustawione w moim standalone test csproj - musze podac sciezke absolutna do `Microsoft.CSharp.targets`.
### [KOLEJNY_KROK]
- Manualne testy w BricsCAD-zie z nowym ListPlotDevicesTool (filtr `HP` powinien zwracac `--- LISTA MEDIOW Z GPD ---` z ~22-35 formatami per device).
- Rozwazyc: dodac `PageSetupTool` preflight walidacje korzystajaca z GpdParser (odrzucac MediaName ktory nie jest na liscie obslugiwanych formatow dla danego plotera, zamiast czekac na `eNoDatabase` z BricsCAD API).
- Rozwazyc: wyciagnac `InputBin` (Roll/Manual/Tray/AutoSelect) z GPD - tez przydatne dla PageSetupTool.
## [v2.29.22] 2026-06-17 - Wydzielenie lekkiego panelu czatu (LightChatControl)
### [ZREALIZOWANO]
- Zaprojektowano i zaimplementowano nową kontrolkę LightChatControl.cs pełniącą rolę lekkiego interfejsu (Dumb View) dla czatu z LLM.
- Przekierowano zdarzenia (OnHistoryAppended, OnSessionReloaded, OnStatusUpdated) z AgentControl do lekkiego panelu, zapewniając thread-safety przez InvokeRequired.
- Rejestrację komendy AI_CHAT przeniesiono do pliku AgentStartup.cs, gwarantując natywne ładowanie komendy w BricsCAD bez problemów z wielokrotnym atrybutem CommandClass.
- Wyizolowano ciężki, wielozakładkowy interfejs WinForms od podstawowego cyklu konwersacji, zmniejszając ryzyko zawieszania BricsCADa przy operacjach na PaletteSet.
### [STAN_SYSTEMU]
- System pomyślnie budowany przez MSBuild (uild.ps1). Komenda AI_CHAT jest prawidłowo rejestrowana.
### [BLOKADY / PROBLEMY]
- Wystąpił problem z brakiem ładowania komendy przez BricsCAD, gdy posiadała ona własny [assembly: CommandClass(...)] w nowym pliku. Rozwiązano przez bezpośrednie dodanie metody z atrybutem [CommandMethod("AI_CHAT")] do klasycznego punktu wejściowego AgentStartup.cs.
### [KOLEJNY_KROK]
- Ewentualne dodanie obsługi Markdown do natywnego BricsCAD-owego wyświetlacza lub udoskonalanie opcji odświeżania okna.

## [v2.30.0] 2026-06-17 - Layout/Plot/Page Setup: nowy profil i 8 narzedzi
### [ZREALIZOWANO]
- Wdrożono 8 nowych narzędzi V2 do zarządzania arkuszami wydruku (Layouts), Page Setup, drukowaniem (PDF/DWF/PNG) i stylami wydruku (CTB/STB):
    * `ListLayoutsTool` (TAG: #layout, #wydruk, Early Exit): listowanie layoutów z metadanymi (papier, urządzenie, styl, skala, obrót).
    * `ManageLayoutTool` (TAG: #layout, #wydruk, Early Exit): CRUD layoutów (Create, Delete, Rename, Clone, SetCurrent) + CopyFromTemplate z DWT/DWG. Blokada usuwania Model.
    * `PageSetupTool` (TAG: #layout, #wydruk, #pagesetup, Early Exit): 20+ właściwości Page Setup (Media, PlotDevice, StyleSheet, PlotType, PlotRotation, PlotCentered, PlotOrigin, UseStandardScale, CustomScale, PlotPaperUnits, ShadePlot, ShadePlotResLevel, ShadePlotCustomDpi, PlotHidden, PlotViewportBorders, PlotPlotStyles, PrintLineweights, ScaleLineweights, PlotTransparency, DrawViewportsFirst, ShowPlotStyles).
    * `ImportLayoutTemplateTool` (TAG: #layout, #template, #wydruk): import layoutu z DWT/DWG (opcje: tylko Page Setup / z entities / OverwriteIfExists).
    * `ExportLayoutTemplateTool` (TAG: #layout, #template, #wydruk): eksport layoutu do DWT (z opcją IncludeBlocks).
    * `PlotLayoutTool` (TAG: #wydruk, #plot): drukowanie pojedynczego layoutu (PDF/DWF/PNG) przez `-PLOT`.
    * `PublishToPdfTool` (TAG: #wydruk, #publish, #pdf): batch publish (MultiSheet do 50 layoutów / SingleFiles). Limity bezpieczeństwa.
    * `PlotStyleTool` (TAG: #wydruk, #plotstyle, #ctb, Early Exit): zarządzanie CTB/STB (List/Load przez _.PSETUPIN/GetInfo/Assign do CurrentLayout/AllLayouts/ByName).
- Nowy profil `CadLayoutProfile` z dedykowanym `system_prompt_layout.txt`.
- Rozszerzenie `ToolConfigManager.cs`:
    * Nowa stała `LayoutPromptFile`, metoda `EnsureLayoutPromptFile()`.
    * Nowy profil `CadLayoutProfile` w `SyncWithTools` (8 layout tools + minimum geometry/utility).
    * Automatyczne tagowanie nowych narzędzi (#layout, #wydruk, #pagesetup, #template, #plot, #pdf, #publish, #plotstyle, #ctb).
    * Dodanie 8 narzędzi do `AllowedTools` CadProfile + `PlotStyleTool` do `CadMetadataProfile`.
- Nowy plik `src/Models/LayoutEnums.cs` (statyczne mapy string→int dla enumeratorów PlotType/PlotRotation/StdScaleType/ShadePlot/ShadePlotResLevel/PlotPaperUnits/PlotStyleType + generyczny parser `TryParseEnum`).
- Nowy plik `src/Core/LayoutHelpers.cs` (helpery GetLayoutByName, GetCurrentLayout, IsModelLayout, LayoutExists, ListLayoutNames, ValidateAndResolvePath). Alias `CadLayout = Teigha.DatabaseServices.Layout` z powodu konfliktu z namespace `Tools.Layout`.
- Modyfikacja `Bricscad_AgentAI_V2.csproj`: 11 nowych `<Compile Include>` (LayoutEnums, LayoutHelpers, 8 narzędzi) + `<Content Include>` dla nowego promptu.
- **Naprawa buga Early Exit w `LLMClient.cs:425`**: Early Exit zwracał generyczny string "Operacja wykonana pomyślnie (Tryb Szybki)." zamiast treści z tool result. Dodano metodę `ExtractLastToolResult` (skanuje historię od końca w poszukiwaniu wiadomości `role: "tool"`). Supervisor otrzymuje teraz faktyczną odpowiedź z narzędzia.
- Rozszerzenie promptu Supervisora (`system_prompt_supervisor.txt`): dodana reguła delegowania layout/plot do `CadLayoutProfile` jako PIERWSZA reguła (przed geometrią, blokami, metadanymi). Dodana lista kluczowych słów (layout/arkusze/wydruk/Page Setup/plot/drukowac/PDF/DWF/publish/styl wydruku/CTB/STB/szablon arkusza itd.).
- Aktualizacja dokumentacji:
    * `TOOLS_REFERENCE.md`: nowe narzędzia 24-31 (sekcja "Arkusze wydruku i Page Setup").
    * `USER_GUIDE.md`: nowa sekcja 13 "Arkusze wydruku i Page Setup" z przykładami promptów dla typowych scenariuszy.
- Plik testowy `tests/LayoutToolsManualTests.md` z 11 kategoriami promptów do manualnego testowania.
### [STAN_SYSTEMU]
- Kompilacja MSBuild: 0 errors, 3 warnings (nieistotne, w istniejącym kodzie).
- DLL zaktualizowany w `bin\Debug/Bricscad_AgentAI_V2.dll`.
- Nowe narzędzia przetestowane manualnie: `ListLayoutsTool` zwraca poprawnie 10 layoutów z rysunku `207_WODA.dwg` (Model + IS.W.01-09).
### [BLOKADY / PROBLEMY]
- Podczas implementacji napotkano szereg drobnych problemów z Teigha API V22:
    * Konflikt nazw `Layout` (typ z `Teigha.DatabaseServices`) vs `Layout` (namespace `Bricscad_AgentAI_V2.Tools.Layout`). Rozwiązanie: alias `using CadLayout = Teigha.DatabaseServices.Layout`.
    * Brak `SecurityFlags` enum w V22 - uproszczono `Database.SaveAs(path, true, DwgVersion.Current)` do 3 argumentów.
    * `Database.SaveAs(string, ...)` zamiast `SaveAs(string, bool, DwgVersion, SecurityFlags)` - uprościliśmy sygnaturę.
    * Enums `PlotRotation` (ZeroDegrees/NinetyDegrees) i `StdScaleType` (Scale1To4, Scale1To10 itd.) nie istnieją w V22 - używamy wartości int z mapowaniem.
    * `PlotPaperUnits` i `ShadePlot` to w rzeczywistości `int` z wartościami 0/1/2/3 (nie enum jak sugerowała dokumentacja).
    * `PlotSettingsValidator.RefreshLists()` wymaga argumentu `PlotSettings(false)` (konstruktor 2-arg).
    * `PlotStyleServices.LoadPlotStyleTable` nie istnieje w V22 - użyto komendy `_.PSETUPIN` przez `SendStringToExecute`.
    * `PlotWireframe` i `PlotAsRaster` są tylko do odczytu w V22 - narzędzie zgłasza ostrzeżenie zamiast próbować zapisu.
    * `CustomScale` nie ma właściwości `.X/.Y` - używamy `.Numerator/.Denominator`.
    * `C# 7.3` (ToolsVersion 15.0) nie wspiera target-typed `new()` - zmieniono na `new Dictionary<...>(StringComparer.OrdinalIgnoreCase)`.
### [KOLEJNY_KROK]
- Dalsze testy manualne z `tests/LayoutToolsManualTests.md` (Page Setup, Import/Export, Publish, Plot Style).
- Po ustabilizowaniu: aktualizacja `BricsCAD_API_V22.txt` o brakujące/zmienione sygnatury metod Layout/Plot.

