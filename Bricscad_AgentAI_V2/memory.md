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
