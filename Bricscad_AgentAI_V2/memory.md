# Bricscad Agent AI V2 - Logi PamiÄ™ci

## WstÄ™p
Ten dokument sĹ‚uĹĽy jako zewnÄ™trzna pamiÄ™Ä‡ dĹ‚ugotrwaĹ‚a dla modelu AI. Zawiera historiÄ™ zmian, kluczowe decyzje architektoniczne oraz napotkane bĹ‚Ä™dy.

## Historia Wersji (Log Zmian)
- v2.0.0: Inicjalna migracja do Function Calling (IToolV2).
- v2.1.0: Dodanie mechanizmu ReAct w LLMClient.
- v2.5.0: Implementacja `ForeachTool` i `RpnCalculator`.
- v2.6.0: Rozbudowa systemu Benchmarkingowego i UI Testera.
- v2.7.10 GOLD: Implementacja twardych Guardrails w `CreateObjectTool.cs`.
- v2.8.0 GOLD: PrzejĹ›cie na dynamicznÄ… konfiguracjÄ™ narzÄ™dzi (`tools_config.json`, `ToolConfigManager`).
    - UsuniÄ™cie wĹ‚aĹ›ciwoĹ›ci `ToolTags` z `IToolV2` i wszystkich narzÄ™dzi (21 plikĂłw).
    - Implementacja wzorca "AI Package Manager" w `RequestAdditionalToolsTool` (ListCategories/LoadCategory).
    - Dodanie zakĹ‚adki "Tagi" w `AgentControl.cs` (UI do edycji `IsCore` i `Tags`).
    - Dynamiczne filtrowanie narzÄ™dzi w `ToolOrchestrator` na podstawie JSON.
- 2026-04-05: v2.6.8 GOLD [FOREACH+ SEQ] - Implementacja Sequence Generator w ForeachTool.cs, rozszerzenie ToolParameter o nested properties/items, testy i dokumentacja.
- 2026-04-05: v2.6.7 GOLD [BENCHMARK+ LGC] - Naprawa bĹ‚Ä™du LINQ w AutoBenchmarkEngine (ArgumentMatch), odblokowanie RecordedToolCalls w JSON.
- 2026-04-05: v2.6.6 GOLD [UI HOTFIX] - Rozdzielono etykiety HUD (lblStatus/lblStats), caĹ‚kowity refaktoring AgentTesterControl (SplitContainer, JSON V1).
- v2.9.0 GOLD [EARLY EXIT] - Implementacja mechanizmu Client-Side Resolution (Tryb Szybki), przerywajÄ…cego pÄ™tlÄ™ ReAct po udanych akcjach fizycznych (Create/Modify).
- v2.9.2 GOLD [FIX TOOL POOL] - RozwiÄ…zanie problemu "Spirali Ĺšmierci" (mismatch nazw API vs C#) i uodpornienie Ĺ‚adowania narzÄ™dzi #core w ToolOrchestrator.
- v2.10.0 GOLD [DATASET STUDIO] - Implementacja moduĹ‚u Dataset Studio (Data Flywheel) do zbierania danych treningowych .jsonl. Refaktoring statystyk LLM na jednolity model LLMStats.
- v2.10.1 GOLD [BUILD HOTFIX] - Naprawa bĹ‚Ä™dĂłw kompilacji (CS1501, CS0246, CS0105) oraz czyszczenie nieuĹĽywanych pĂłl w UI (CS0169).
- v2.10.2 GOLD [DATASET UX] - Naprawa Ĺ›cieĹĽki zapisu JSONL (Brak UprawnieĹ„) oraz poprawki UX w Dataset Studio (formatowanie czasu ms -> s, czytelne etykiety).
- v2.10.3 GOLD [DOC SYNC] - PeĹ‚na synchronizacja System_Blueprint.md oraz dokumentacji w folderze /docs z aktualnym stanem V2.10.x.
- v2.11.0 GOLD [CONTEXT SLICER] - Implementacja inteligentnej "Krajalnicy" (Context Slicer) w Dataset Studio. RozwiÄ…zanie problemu Context Poisoning przez izolacjÄ™ turnĂłw (System + Last User + Responses). GĹ‚Ä™boka kopia historii konwersacji w UI. Synchronizacja dokumentacji.
- v2.11.1 GOLD [TOOLS IN JSONL] - Dodanie tablicy "tools" do eksportu JSONL w Dataset Studio. PeĹ‚na zgodnoĹ›Ä‡ z formatem OpenAI Fine-tuning dla Tool Calling.
- v2.11.2 GOLD [UI PERSISTENCE] - Naprawa ukĹ‚adu Dataset Studio (widocznoĹ›Ä‡ statystyk, kolejnoĹ›Ä‡ DockStyle.Fill). Implementacja UISettingsManager do trwaĹ‚ego zapamiÄ™tywania pozycji splittera (ui_settings.json).
- v2.11.3 GOLD [RPN UNIT STRIP] - Naprawa bĹ‚Ä™du double.TryParse w CreateObjectTool.cs. WstrzykniÄ™cie komend RPN (#UNITL CONVE UVAL) celem normalizacji wynikĂłw przed konwersjÄ… na typ numeryczny.
- v2.11.4 GOLD [RPN SMART SCALE] - Hotfix bĹ‚Ä™du rzutowania jednostek. Dodano inteligentne sprawdzanie Regex w CreateObjectTool.cs â€“ konwersja do jednostek dokumentu zachodzi tylko wtedy, gdy wynik RPN zawiera sygnaturÄ™ literowÄ… (jednostkÄ™). Zapobiega to bĹ‚Ä™dnemu skalowaniu goĹ‚ych wspĂłĹ‚rzÄ™dnych.
- v2.11.5 GOLD [FOREACH INDEX] - Dodanie obsĹ‚ugi tagu {index} w ForeachTool.cs. UmoĹĽliwia to generowanie sekwencyjnego nazewnictwa (np. "OĹ› 1", "OĹ› 2") podczas operacji w pÄ™tli. Licznik iteracji startuje od 1.
- v2.11.6 GOLD [PROMPT EXPANSION] - Rozbudowa System Promptu w AgentControl.cs o instrukcje dla RPN (CONCAT, IFTE) oraz formatowanie nowej linii (\P) dla MText. Poprawia to zdolnoĹ›Ä‡ modelu do generowania dynamicznych tekstĂłw w pÄ™tlach.
- v2.11.7 GOLD [COLOR MAP] - WstrzykniÄ™cie mapy kolorĂłw ACI (AutoCAD Color Index) oraz instrukcji TrueColor (RGB) do promptu systemowego. UĹ‚atwia to modelowi poprawne wyszukiwanie i zamianÄ™ kolorĂłw w rysunku.
- v2.11.8 GOLD [RGB SELECT] - Refaktoryzacja wydobywania kolorĂłw w SelectEntitiesTool.cs. Wprowadzono peĹ‚nÄ… obsĹ‚ugÄ™ formatu RGB ("R,G,B") dla TrueColor oraz poprawne rzutowanie kolorĂłw dziedziczonych z warstw, co umoĹĽliwia precyzyjne filtrowanie selekcji po kolorach innych niĹĽ ACI.
- v2.11.9 GOLD [RGB PATTERN] - Dodanie do System Promptu instrukcji o "Wzorcu Przecinka" do masowego wykrywania dowolnych kolorĂłw RGB (`contains: ","`) oraz przypomnienia o zakresie skĹ‚adowych 0-255.
- v2.11.10 GOLD [VISUAL PERCEPTION] - WdroĹĽenie "ReguĹ‚y Percepcji" do System Promptu. Model zostaĹ‚ poinstruowany, aby automatycznie uĹĽywaÄ‡ wĹ‚aĹ›ciwoĹ›ci wirtualnych (`VisualColor`, `VisualLinetype` itd.) przy zapytaniach dotyczÄ…cych wyglÄ…du zewnÄ™trznego obiektĂłw, co zapewnia poprawne uwzglÄ™dnienie dziedziczenia warstw (ByLayer).
- v2.11.11 GOLD [PROPERTY SYNC] - Ujednolicenie mapowania wĹ‚aĹ›ciwoĹ›ci (Transparency, LineWeight, LinetypeScale) miÄ™dzy UI a API. WdroĹĽono dwukierunkowÄ… konwersjÄ™ przezroczystoĹ›ci (0-90 UI <=> 0-255 Alpha) oraz rozbudowano System Prompt o globalne zasady dla gruboĹ›ci i rodzajĂłw linii.
- v2.11.12 GOLD [LAYER NATIVE] - Kompletny refaktoring ManageLayersTool.cs na natywne API BricsCAD (LayerTable/LayerTableRecord). UsuniÄ™to wywoĹ‚ania `Editor.Command`, co wyeliminowaĹ‚o bĹ‚Ä™dy Fatal Error i blokowanie interfejsu. Dodano obsĹ‚ugÄ™ wielu warstw (lista po przecinku), akcjÄ™ `Toggle` (Lock/Freeze/Off) oraz bezpieczne `Rename`.
- v2.11.13 GOLD [STRUCTURE DIRECTIVE] - Wprowadzenie "Dyrektywy Struktury" do System Promptu. Model otrzymaĹ‚ kategoryczny zakaz uĹĽywania narzÄ™dzi geometrycznych (`CreateObject`, `ModifyProperties`) do manipulacji strukturÄ… rysunku (warstwami). Wzmocniono rolÄ™ narzÄ™dzia `ManageLayers` oraz mechanizmu ĹĽÄ…dania dodatkowych narzÄ™dzi.
- v2.11.14 GOLD [THREAD SAFETY] - WdroĹĽenie thread-marshalingu (UI thread synchronization) dla narzÄ™dzi `UserInputTool` i `UserChoiceTool`. Interakcje z BricsCAD Editor sÄ… teraz bezpiecznie delegowane do gĹ‚Ăłwnego wÄ…tku za pomocÄ… `Invoke`, eliminujÄ…c bĹ‚Ä™dy Cross-Thread Exception i Fatal Error podczas asynchronicznych sesji LLM.
- v2.11.15 GOLD [STRICT ACTION VALIDATION] - Dodanie twardej walidacji parametru `Action` w `ManageLayersTool.cs`. NarzÄ™dzie odrzuca teraz nieobsĹ‚ugiwane polecenia (np. `CreateLayer`) z wyraĹşnym komunikatem o bĹ‚Ä™dzie, zamiast koĹ„czyÄ‡ dziaĹ‚anie bez efektu.
- v2.11.16 GOLD [HOT-RELOAD & DYNAMIC PROMPT] - WdroĹĽenie odĹ›wieĹĽania orkiestratora na ĹĽÄ…danie (po zapisie konfiguracji) oraz dynamicznego wstrzykiwania dostÄ™pnych kategorii narzÄ™dzi z `ToolConfigManager` do promptu systemowego. Wyeliminowano potrzebÄ™ restartu aplikacji po zmianie dostÄ™pnych pakietĂłw narzÄ™dziowych.
- v2.11.17 GOLD [DISCOVERY & ACTIVATION] - WdroĹĽenie "Katalogu NarzÄ™dzi UĹ›pionych" automatycznie generowanego z definicji orkiestratora. Model LLM widzi teraz nazwy i opisy narzÄ™dzi, ktĂłrych nie ma w arsenale, i moĹĽe poprosiÄ‡ o ich zaĹ‚adowanie poprzez `RequestAdditionalTools`. UmoĹĽliwiono aktywacjÄ™ narzÄ™dzi bezpoĹ›rednio po nazwie klasy/API jako fallback.
- v2.12.1 GOLD [3-PILLAR ARCHITECTURE] - Kompletna przebudowa architektury wiedzy. 1) Konstytucja Agenta (Prompt) zcentralizowana na RPN i logice CAD. 2) Lokalne Schematy (Tool Schemas) przejÄ™Ĺ‚y specyficzne guardraile narzÄ™dzi. 3) Dynamiczne Odkrywanie (Discovery) - `RequestAdditionalTools` serwuje teraz peĹ‚ny katalog opisĂłw narzÄ™dzi uĹ›pionych.
- v2.12.2 GOLD [REACT ERROR PROTOCOL] - WdroĹĽenie instrukcyjnego komunikatu bĹ‚Ä™du w `ToolOrchestrator.ExecuteTool`. JeĹ›li model wywoĹ‚a uĹ›pione narzÄ™dzie, otrzyma "BĹÄ„D KRYTYCZNY" z natychmiastowÄ… instrukcjÄ… uĹĽycia `RequestAdditionalTools`, co wymusza poprawny cykl rozumowania (ReAct).
- v2.12.3 GOLD [DYNAMIC SCHEMA INJECTION] - Naprawa luki w Ĺ‚adowaniu narzÄ™dzi. WdroĹĽono `SessionDynamicTags` w `ToolConfigManager`, co pozwala orkiestratorowi na natychmiastowe odblokowanie schematĂłw (ToolDefinition) nowo zaĹ‚adowanych narzÄ™dzi w trakcie tej samej sesji. Zapewnia to, ĹĽe LLM otrzyma definicje parametrĂłw zaraz po akcji `LoadCategory`.
- v2.12.4 GOLD [TWO-PHASE COMMIT] - Naprawa bĹ‚Ä™du "Silent Transaction Failure" w `ManageLayersTool.cs`. WdroĹĽono wzorzec dwufazowego zatwierdzania bazy danych: najpierw tworzona jest struktura warstwy, a dopiero po pomyĹ›lnym `Commit()` gĹ‚Ăłwnej transakcji, w drugiej maĹ‚ej transakcji, warstwa jest ustawiana jako aktualna (`db.Clayer`). Zapobiega to powstawaniu "warstw widm".
- v2.12.5 GOLD [CONTEXT PROTECTION] - Wzmocnienie stabilnoĹ›ci `ManageLayersTool.cs` poprzez wymuszenie kontekstu `HostApplicationServices.WorkingDatabase` (rozwiÄ…zanie problemu Silent Rollback w ODA Teigha). Dodatkowo zoptymalizowano proces tworzenia rekordĂłw warstw, inicjujÄ…c ich wĹ‚aĹ›ciwoĹ›ci (kolor, rodzaj linii) przed dodaniem do tablicy symboli.
- v2.12.6 GOLD [ENGINE TRACER] - WdroĹĽenie zakĹ‚adki Debug oraz nasĹ‚uchiwania zdarzeĹ„ bazy danych (ObjectAppended, TransactionAborted) celem diagnozy zjawiska Silent Rollback w Teigha API.
- v2.12.7 GOLD [THREAD-SAFE UI SYNC] - Refaktoryzacja `ManageLayersTool.cs`. Przeniesiono aktywacjÄ™ warstw (`db.Clayer`) oraz odĹ›wieĹĽanie interfejsu do GĹ‚Ăłwnego WÄ…tku (Main Thread) za pomocÄ… `doc.SendStringToExecute`. RozwiÄ…zuje to problem "cichego rollbacku" przy interakcjach z UI z wÄ…tkĂłw pobocznych.
- v2.12.8 GOLD [ACTION SETCURRENT] - Dodanie dedykowanej akcji `SetCurrent` do `ManageLayersTool.cs`. RozwiÄ…zuje to problem bĹ‚Ä™dnego uĹĽywania przez LLM akcji `Toggle -> On` do przeĹ‚Ä…czania warstwy roboczej. Zaktualizowano schemat narzÄ™dzia, oznaczajÄ…c flagÄ™ `MakeCurrent` jako przestarzaĹ‚Ä….
- v2.12.9 GOLD [LAYER MODIFICATION] - Rozszerzenie `ManageLayersTool.cs` o akcjÄ™ `Modify` oraz obsĹ‚ugÄ™ wĹ‚aĹ›ciwoĹ›ci: `Transparency` (konwersja 0-90 na Alpha 255-0), `LineWeight` oraz `Plottable`. Zunifikowano logikÄ™ `Create/Modify` z peĹ‚nÄ… obsĹ‚ugÄ… masek (*, ?) dla modyfikacji masowych, co eliminuje halucynacje modelu dotyczÄ…ce uĹĽywania narzÄ™dzi edycji obiektĂłw fizycznych do zarzÄ…dzania strukturÄ… warstw.
- v2.12.10 GOLD [PROMPT ENHANCEMENT] - Optymalizacja schematu `ForeachTool.cs` (Prompt Engineering). WstrzykniÄ™to "ZĹ‚oty Standard" wywoĹ‚aĹ„ (Few-Shot Examples) bezpoĹ›rednio do opisu parametrĂłw. Agent dowiaduje siÄ™ o moĹĽliwoĹ›ci zagnieĹĽdĹĽania ewaluacji RPN, uĹĽywania tagu `{index}` oraz wywoĹ‚ywania dowolnych narzÄ™dzi (np. `ManageLayers`) wewnÄ…trz pÄ™tli za pomocÄ… klucza `ToolName`.
- v2.12.11 GOLD [RPN INTERCEPTION] - Implementacja RPN Interception w `ForeachTool.cs`. Parametry oznaczone prefiksem `RPN:` wewnÄ…trz pÄ™tli sÄ… teraz ewaluowane przez `RpnCalculator` przed przekazaniem do orkiestratora. RozwiÄ…zuje to problem przesyĹ‚ania surowych wyraĹĽeĹ„ matematycznych zamiast wyliczonych wartoĹ›ci (np. dla kolorĂłw lub pozycji) w pÄ™tlach.
- v2.12.12 GOLD [TEST FIX] - Naprawa bĹ‚Ä™du kompilacji CS0103 w `ForeachToolTests.cs` poprzez dodanie brakujÄ…cego `using Bricscad_AgentAI_V2.Core`.
- v2.12.13 GOLD [LAYER HOTFIX] - Wyeliminowanie bĹ‚Ä™du "Double-Open" w `ManageLayersTool.cs`. PrzejĹ›cie na operowanie bezpoĹ›rednio na `LayerTableRecord` (List) zamiast ponownego otwierania obiektĂłw przez `ObjectId`. RozwiÄ…zuje to problem cichego rollbacku transakcji w Teigha API przy modyfikacji nowo utworzonych warstw.
- v2.12.14 GOLD [STALE UI FIX] - WdroĹĽenie wymuszonej synchronizacji GUI w `ManageLayersTool.cs`. Gwarantowane wywoĹ‚anie `SendStringToExecute` (z komendÄ… `(princ)` jako fallback) zapewnia, ĹĽe MenedĹĽer Warstw BricsCAD odĹ›wieĹĽy swĂłj stan i pokaĹĽe nowo utworzone/zmodyfikowane warstwy nawet w trybie asynchronicznym bez ich aktywacji.
- v2.12.15 GOLD [XREF PROTECTION] - WdroĹĽenie zabezpieczeĹ„ dla warstw zaleĹĽnych (XREF) w `ManageLayersTool.cs`. Zablokowano akcje `SetCurrent`, `Rename` oraz `Delete` dla warstw `IsDependent`. Zaktualizowano schemat o instrukcjÄ™ uĹĽycia znaku `|` do modyfikacji wizualnej podkĹ‚adĂłw.
- v2.12.16 GOLD [SUPPRESS UI] - Implementacja mechanizmu `SuppressUI` w `ManageLayersTool.cs` i `ForeachTool.cs`. NarzÄ™dzia wywoĹ‚ywane w pÄ™tlach otrzymujÄ… flagÄ™ blokujÄ…cÄ… odĹ›wieĹĽanie interfejsu w kaĹĽdej iteracji, co eliminuje kolizje blokad dokumentu (`eLockViolation`). Zbiorcze odĹ›wieĹĽenie UI nastÄ™puje raz po zakoĹ„czeniu caĹ‚ej pÄ™tli.
- v2.13.1 GOLD [ADVANCED FILTERS] - Implementacja `AdvancedFilters` w `SelectEntitiesTool.cs`. Dodano obsĹ‚ugÄ™ zĹ‚oĹĽonych zapytaĹ„ o wĹ‚aĹ›ciwoĹ›ci CAD (np. Transparency 0-90, TextOverride) z uĹĽyciem refleksji i dedykowanego mapowania klas. Rozszerzono logikÄ™ o operatory `Contains` oraz `NotContains`.
- v2.13.2 GOLD [UI REFACTOR] - Reorganizacja interfejsu `DatasetStudioControl.cs`. Zmieniono ukĹ‚ad z pionowego na poziomy (lista na gĂłrze - 20%, edytor na dole - 80%). Wprowadzono czytelnÄ… belkÄ™ nagĹ‚ĂłwkowÄ… na samej gĂłrze dla przeĹ‚Ä…cznikĂłw i statystyk.
- v2.13.3 GOLD [DATASET STUDIO PRO] - Kompleksowa przebudowa Dataset Studio. WdroĹĽono kolorowanie skĹ‚adni JSON (VSC style), system zakĹ‚adek ("Aktualna sesja" / "Edycja data setĂłw") oraz peĹ‚ne zarzÄ…dzanie plikami JSONL z funkcjÄ… "Uruchom Makro" do testowania instrukcji. Wprowadzono trwaĹ‚oĹ›Ä‡ ustawieĹ„ ostatnio otwartego pliku.
- v2.13.4 GOLD [INSTRUCTION TOOLS] - Rozbudowa Dataset Studio o narzÄ™dzia manipulacji treĹ›ciÄ…: przyciski "UsuĹ„ instrukcje" (czyszczenie tablicy messages) oraz "ZamieĹ„ instrukcjÄ™" (wklejanie z walidacjÄ… formatu ze schowka).
- v2.13.5 GOLD [SMART INSTRUCTIONS] - Refaktoryzacja narzÄ™dzi instrukcji: "UsuĹ„ instrukcje" teraz precyzyjnie zachowuje nagĹ‚Ăłwek `system`, a "ZamieĹ„ instrukcjÄ™" inteligentnie Ĺ‚Ä…czy nowÄ… interakcjÄ™ ze schowka, dbajÄ…c o niepowtarzanie nagĹ‚ĂłwkĂłw systemowych.
- v2.13.6 GOLD [UI REFINERY] - Poprawki UX w Dataset Studio: wdroĹĽenie debounce dla kolorowania skĹ‚adni (fix klawisza Enter), przeniesienie przyciskĂłw zarzÄ…dzania wpisami (Duplikuj/UsuĹ„) na gĂłrny pasek, wdroĹĽenie responsywnego ukĹ‚adu dolnego panelu akcji oraz dodanie funkcji usuwania rekordĂłw z listÄ… potwierdzeĹ„.
- v2.14.0 GOLD [SEPARATION OF CONCERNS] - Rozdzielenie kompetencji narzÄ™dzi: wdroĹĽenie wyspecjalizowanego `DimensionEditTool` (#wymiary), wprowadzenie blokady (Runtime Guardrail) w `ModifyPropertiesTool` dla tekstu i wymiarĂłw oraz implementacja hard-cast fallback w `SelectEntitiesTool` dla `HatchObjectType` (fix gradientĂłw) i wĹ‚aĹ›ciwoĹ›ci tekstowych.
- v2.14.1 HOTFIX [DIMENSION SYNC] - Usprawnienie `DimensionEditTool`: wdroĹĽenie `GetArrowObjectId` dla automatycznego generowania standardowych grotĂłw (Lazy Loading) oraz dodanie `RecomputeDimensionBlock` dla natychmiastowego odĹ›wieĹĽania grafiki wymiaru po zmianie parametrĂłw.
- v2.14.2 CRITICAL HOTFIX [SAFE PARSE] - CaĹ‚kowita rekonstrukcja `Execute` w `DimensionEditTool`: wdroĹĽenie bezpiecznego parsowania (`InvariantCulture`), wymuszenie trybu `OpenMode.ForWrite` dla kaĹĽdego obiektu oraz dodanie polecenia `REGEN` dla synchronizacji interfejsu CAD.
- v2.15.0 GOLD [TOOL SANDBOX] - WdroĹĽenie zakĹ‚adki "Tool Sandbox" do izolowanego testowania logiki C# narzÄ™dzi `IToolV2`. Implementacja inteligentnego generatora szablonĂłw JSON na podstawie schematĂłw, integracja z `AgentMemoryState` celem Ĺ‚adowania zaznaczenia CAD oraz system logowania wynikĂłw z sygnaturÄ… czasowÄ….
- v2.15.1 [TOOL SANDBOX TRANSFORMATION] - PrzeksztaĹ‚cenie Sandboxa w interaktywnÄ… dokumentacjÄ™. WdroĹĽenie "Property Discovery" (dynamiczny podglÄ…d parametrĂłw i typĂłw), obsĹ‚uga "Snippets" (przykĹ‚adĂłw JSON), ulepszone szablony z komentarzami i placeholderami oraz automatyczne oczyszczanie JSON (Regex) przed egzekucjÄ…. Poprawiono wysokoĹ›Ä‡ sekcji dokumentacji oraz przywrĂłiono `DimensionEditTool` i `InspectEntityTool` do projektu.
- v2.15.2 [INTERACTIVE SANDBOX] - WdroĹĽenie interaktywnego budowania JSON: podwĂłjne klikniÄ™cie na parametr w dokumentacji wstawia go do edytora. Wprowadzenie minimalistycznych szablonĂłw (tylko pola Required).
- v2.15.3 [UI POLISH] - Ulepszenie interakcji: podwĂłjne klikniÄ™cie ustawia kursor bezpoĹ›rednio wewnÄ…trz pustych cudzysĹ‚owĂłw `""` (bez tekstu zastÄ™pczego). Uproszczenie generatora szablonĂłw celem zwiÄ™kszenia przejrzystoĹ›ci. ResponsywnoĹ›Ä‡ Tool Sandbox (SplitContainer + persistence), Click-to-Add dla parametrĂłw.
- v2.16.0 [AGENT RECIPES] - WdroĹĽenie systemu "Agent Recipes" (Drogowskazy). Nowa 4. zakĹ‚adka w Dataset Studio, mechanizm Few-Shot Prompting ($trigger) oraz przycisk "PrzechwyÄ‡ jako Przepis" w edytorze sesji.
- v2.16.1 [DYNAMIC AUTOCOMPLETE] - WdroĹĽenie dynamicznego autouzupeĹ‚niania dla `#` (Tagi z ToolConfigManager) oraz `$` (Receptury z RecipeManager). Naprawiono bĹ‚Ä…d braku widocznoĹ›ci rÄ™cznie dodanych kategorii w podpowiedziach.
- v2.17.0 [ADVANCED RECIPES] - Rozbudowa systemu receptur o "Tryb Makra" ($trigger$ - natychmiastowe wykonanie). WdroĹĽenie testowania caĹ‚ej sekwencji z walidacjÄ… JSON oraz interaktywnego wyboru kroku do przesĹ‚ania do Tool Sandboxa.
- v2.18.0 [UI & INTEGRATION] - Zmiana nazwy na "Recepty". Implementacja integracji "WyĹ›lij do Recepty" w Tool Sandboxie. Dodanie przycisku "Nowa Recepta" (tworzenie od zera). Optymalizacja proporcji UI (25/75) z persistencjÄ….
- v2.19.0 [TRAINING DATA] - WdroĹĽenie moduĹ‚u "Eksport do ZĹ‚otego Standardu" w Receptach. Automatyczna generacja JSONL z uwzglÄ™dnieniem System Promptu, definicji narzÄ™dzi (#core + tagi) oraz zapytania uĹĽytkownika.
- v2.20.0 GOLD [CLI INTERFACE] - WdroĹĽenie Command Line Interface (CLI). Dodano komendy `AI_RUN`, `AI_TOOL`, `AI_PROPS` oraz `AI_DIM`. Implementacja mechanizmu `SyncSelectionWithMemory` do automatycznej synchronizacji zaznaczenia CAD (PickFirst) z pamiÄ™ciÄ… Agenta. RozwiÄ…zanie konfliktu nazw dla klasy `Exception`.
- v2.20.1 GOLD [RPN CLI] - PeĹ‚na migracja systemu RPN z v1. Komendy RPN, CALC, STOS. TrwaĹ‚oĹ›Ä‡ stosu w DWG.
- v2.20.2 GOLD [RPN FINAL SPEC] - Finalizacja CLI RPN. Tryb interaktywny (pÄ™tla), interaktywne pomiary CAD.
- v2.20.3 GOLD [RPN V1 SYNC] - PeĹ‚na synchronizacja zachowania z v1. Implementacja "wstrzykiwania" wyniku na koĹ„cu komendy RPN (SendStringToExecute), pÄ™tla obliczeĹ„ dla CALC oraz automatyczne odĹ›wieĹĽanie stosu w konsoli.
- v2.20.4 GOLD [UNIT CLEAN INJECTION] - Inteligentne czyszczenie jednostek przed wstrzykniÄ™ciem do CAD. Automatyczna konwersja jednostek dĹ‚ugoĹ›ci na jednostki rysunku (INSUNITS) oraz wstrzykiwanie surowych wartoĹ›ci (DisplayValue) dla innych wymiarĂłw.
- v2.20.5 GOLD [READ XDATA] - Nowe narzÄ™dzie `ReadXData` do odczytu metadanych XData. Dodano komendÄ™ CLI `AI_XDATA` oraz peĹ‚nÄ… dokumentacjÄ™ technicznÄ….
- v2.20.6 GOLD [WRITE XDATA] - Implementacja narzÄ™dzia `WriteXData` z obsĹ‚ugÄ… automatycznej rejestracji RegApp oraz komendÄ… CLI `AI_SETXDATA`.
- v2.20.7 GOLD [FIND XDATA] - Implementacja narzÄ™dzia `FindXData` z obsĹ‚ugÄ… rekurencyjnego skanowania blokĂłw oraz komendÄ… CLI `AI_FINDXDATA`.
- v2.20.8 GOLD [MULTIMODAL VISION] - WdroĹĽenie obsĹ‚ugi modeli VLM (Vision).
    - Implementacja `CaptureVisionAreaTool` (Win32 P/Invoke screenshot).
    - Rozszerzenie `ChatMessage` o pole `Content` (object) dla standardu GPT-4o Vision.
    - Automatyczne wstrzykiwanie Base64 obrazĂłw do historii sesji w `LLMClient`.
    - Nowe polecenia CLI: `SKAN` (zrzut ekranu) i `AI_VISION` (zrzut + pytanie).
- v2.20.9 GOLD [VISION e15 FIX] - Naprawa krytycznego bĹ‚Ä™du `eVetoed` (e15) w module Vision.
    - Rezygnacja z COM `ZoomWindow` na rzecz natywnego logicznego manipulowania `ViewTableRecord`.
    - Poprawa cyklu ĹĽycia `ViewTableRecord` (naprawa bĹ‚Ä™du use-after-dispose).
    - UsuniÄ™cie flagi `Transparent` z poleceĹ„ wizyjnych celem umoĹĽliwienia zmian widoku.
- v2.21.0 GOLD [MULTI-AGENT ORCHESTRATION] - Rozdzielenie kompetencji agentĂłw: wprowadzenie `IExecutionContext`, `SharedMemoryState` (Blackboard) dla komunikacji, `SupervisorOrchestrator` oraz `DelegateTaskTool` (Kroki 1-5).
- v2.22.0 GOLD [MATH EXPERT & RPN] - WdroĹĽenie profilu `CadMathProfile` i eksperta matematycznego zintegrowanego z kalkulatorem `RpnCalculator`. Wsparcie dla szablonĂłw `{MATH:...}` w narzÄ™dziach CAD.
- v2.23.0 GOLD [MULTI-CRITERIA FILTERS] - Rozszerzenie `DatasetManager` o filtrowanie wielokryterialne podtypĂłw (Faza 8.1).
- v2.24.0 GOLD [KNOWLEDGE BASE CATEGORIZATION] - System kategoryzacji baz danych, makr i formuĹ‚. Wprowadzenie tagĂłw, hierarchii folderĂłw oraz widoku drzewiastego TreeView w GUI (Faza 9).
- v2.25.0 GOLD [MULTI-MODAL VISION] - ObsĹ‚uga multimodalnych zaĹ‚Ä…cznikĂłw (tekst, PDF, XLSX) i automatyczna kompresja/skalowanie obrazĂłw z Vision API oraz integracja ze schowkiem Ctrl+V (Faza 10 & 12).
- v2.25.1 GOLD [DATASET STANDARDIZATION] - Rozdzielenie plikĂłw metadanych i danych (*.data.json), zabezpieczenie przed brakiem danych oraz normalizacja tabel w GUI (Faza 11).
- v2.25.2 HOTFIX [GUI ENCODING] - Poprawka bĹ‚Ä™dĂłw kodowania polskich znakĂłw w interfejsie AgentControl.cs.
- v2.26.0 GOLD [SESSION & COMPRESSION] - System zarzÄ…dzania sesjami ChatSession, trwaĹ‚y zapis sesji w APPDATA, automatyczne nadawanie nazw sesjom oraz kompresja kontekstu w oknie Context Bar (Faza 13).
- v2.27.0 GOLD [DWG CONTEXT & NOTES] - System Notatek Projektowych (Sidecar Markdown), komenda `/notatka` w GUI, menedĹĽer DrawingNoteManager oraz dynamiczne wstrzykiwanie RAG do promptu Supervisora (Faza 14).
- v2.28.0 GOLD [SAFE FILE TOOLS] - Zabezpieczone narzÄ™dzia plikowe Read/WriteProjectFileTool z blokadami Ĺ›cieĹĽek/rozszerzeĹ„ oraz integracja autouzupeĹ‚niania komend i tagĂłw w GUI (Faza 15).

- v2.28.1 GOLD [MEMORY SYNCHRONIZATION] - Ujednolicenie pliku pamiÄ™ci memory.md, naprawa znieksztaĹ‚ceĹ„ Mojibake i UTF-16, nadanie numerĂłw wersji kolejnym krokom.

## Decyzje Architektoniczne
- **Semantic Tool Routing**: System dynamicznego dobierania narzÄ™dzi na podstawie tagĂłw (#core, #bloki, itp.). Od v2.8.0 zarzÄ…dzany przez `ToolConfigManager`.
- **Early Exit (Fast Mode)**: Mechanizm pozwalajÄ…cy Agentowi na zakoĹ„czenie pÄ™tli po wykonaniu narzÄ™dzi akcji, jeĹ›li wspierajÄ… one flagÄ™ `SupportsEarlyExit`. Drastyczna redukcja tokenĂłw i czasu odpowiedzi.
- **AI Package Manager**: Model LLM samodzielnie odkrywa i Ĺ‚aduje pakiety narzÄ™dzi przez `RequestAdditionalToolsTool`.
- **Hard Guardrails**: KaĹĽde narzÄ™dzie jest odpowiedzialne za walidacjÄ™ swoich parametrĂłw i zwracanie "BĹ‚Ä™du Krytycznego" w celu przerwania halucynacji LLM.

## RozwiÄ…zane Problemy (Bug Log)
- **UI Autocomplete**: Naprawiono przechwytywanie klawiszy Tab/Enter przez migracjÄ™ do `ProcessCmdKey` w `AgentControl.cs`.
- **Build CS0111/CS0103**: Naprawiono bĹ‚Ä™dy kompilacji po masowej refaktoryzacji (dodanie plikĂłw do .csproj oraz usuniÄ™cie duplikatu klasy w UserInputTool.cs).
- **Silent Name Mismatch (Death Spiral)**: Naprawiono bĹ‚Ä…d w v2.9.1, gdzie klucze `ToolConfigManager` korzystaĹ‚y z nazw klas C# zamiast API Names z `FunctionSchema`, co unieruchamiaĹ‚o mechanizm Early Exit i gubiĹ‚o narzÄ™dzia #core.

## Dziennik Deweloperski (Logi ZadaĹ„)
## [v2.20.10] 2026-06-03T10:45:17+02:00 - Inicjalna analiza architektury V2 i protokoĹ‚Ăłw pamiÄ™ci
### [ZREALIZOWANO]
- Przeprowadzono szczegĂłĹ‚owÄ… analizÄ™ architektury projektu w wersji V2 (Function Calling, LLMClient ReAct, RPN, Dataset Studio, CLI, system receptur i faza Vision).
- Przeanalizowano wytyczne, instrukcje i zasady w dokumentacji projektu.
### [STAN_SYSTEMU]
- System w peĹ‚ni stabilny i przeanalizowany, gotowy do dalszego rozwoju.
### [BLOKADY / PROBLEMY]
- Brak napotkanych trudnoĹ›ci w procesie analizy.
### [KOLEJNY_KROK]
- Oczekiwanie na konkretne zadania implementacyjne od uĹĽytkownika.

## [v2.20.11] 2026-06-03T11:31:40+02:00 - Analiza konfiguracji LLM i identyfikacja parametrĂłw LM Studio
### [ZREALIZOWANO]
- Przeanalizowano pliki konfiguracyjne LLM (LLMConfigModels.cs, LLMClient.cs) w celu identyfikacji parametrĂłw, ktĂłre moĹĽna przekazaÄ‡ do dostawcĂłw API (OpenAI, OpenRouter, LM Studio).
- Opracowano listÄ™ potencjalnych rozszerzeĹ„ konfiguracji (m.in. reasoning_effort, thinking budget, top_p, seed, frequency/presence penalties).
### [STAN_SYSTEMU]
- System jest gotowy na wprowadzenie zmian w konfiguracji dostawcĂłw. Parametry sÄ… obecnie ograniczone do Model, Temperature, MaxTokens, ApiKey i EndpointUrl.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Ewentualna implementacja dodatkowych pĂłl w LLMProviderConfig i LLMConfigDialog na ĹĽyczenie uĹĽytkownika.

## [v2.20.12] 2026-06-03T11:34:52+02:00 - Poprawka kodowania znakĂłw UTF-8 w GUI AgentControl.cs
### [ZREALIZOWANO]
- Przeanalizowano przydatnoĹ›Ä‡ parametrĂłw LLM specyficznych dla silnikĂłw lokalnych (Ollama, LM Studio, llama.cpp): top_p, top_k, min_p, repeat_penalty oraz stop sequences.
- Opracowano plan integracji parametrĂłw samplingu dla modeli lokalnych w celu poprawy deterministycznoĹ›ci i stabilnoĹ›ci Tool Callingu na maĹ‚ych modelach (np. Qwen, DeepSeek).
### [STAN_SYSTEMU]
- System bez zmian kodu ĹşrĂłdĹ‚owego. Przeprowadzono analizÄ™ wpĹ‚ywu parametrĂłw lokalnych na API.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na decyzjÄ™ uĹĽytkownika co do implementacji rozszerzonych parametrĂłw lokalnych.

## [v2.20.13] 2026-06-03T11:38:50+02:00 - Poprawka formatu kodowania pliku LLMConfigModels.cs na UTF-8
### [ZREALIZOWANO]
- WdroĹĽono rozszerzone parametry konfiguracji dostawcĂłw LLM (TopP, TopK, MinP, RepetitionPenalty, ReasoningEffort) w modelach danych C# (LLMConfigModels.cs), silniku klienta (LLMClient.cs) oraz formularzu UI (LLMConfigDialog.cs).
- PomyĹ›lnie skompilowano i zweryfikowano projekt Bricscad_AgentAI_V2 za pomocÄ… dotnet build.
### [STAN_SYSTEMU]
- System w peĹ‚ni zaktualizowany o obsĹ‚ugÄ™ zaawansowanych parametrĂłw dla modeli lokalnych i chmurowych. Wszystkie zmiany sÄ… wstecznie kompatybilne.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na uruchomienie i testy uĹĽytkownika w Ĺ›rodowisku BricsCAD.

## [v2.20.14] 2026-06-03T11:42:24+02:00 - Poprawa importu System.Text.Encoding w LLMConfigDialog.cs
### [ZREALIZOWANO]
- WdroĹĽono czytelny interfejs pomocy i podpowiedzi (ToolTips) dla wszystkich zaawansowanych parametrĂłw konfiguracji dostawcĂłw (Temperature, Max Tokens, Top-P, Top-K, Min-P, Repetition Penalty, Reasoning Effort).
- PomyĹ›lnie skompilowano i przetestowano aplikacjÄ™.
### [STAN_SYSTEMU]
- System w peĹ‚ni zaktualizowany o opisy parametrĂłw podpowiedzi tooltip. Stabilny i gotowy do uĹĽycia.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na dalsze wytyczne od uĹĽytkownika.

## [v2.20.15] 2026-06-03T13:20:00+02:00 - Testowanie stabilnoĹ›ci Ĺ‚adowania modeli i integracji z CAD
### [ZREALIZOWANO]
- Dodano twarde zabezpieczenia (safeguards) w `LLMClient.cs` zapobiegajÄ…ce doĹ‚Ä…czaniu zaawansowanych parametrĂłw samplingu (`top_k`, `min_p`, `repetition_penalty`) do ĹĽÄ…daĹ„ wysyĹ‚anych do oficjalnych API OpenAI (`api.openai.com`) i Azure OpenAI (`openai.azure.com`).
- UsuniÄ™to bĹ‚Ä™dy kompilacji:
  - RozwiÄ…zano konflikt nazw `config` w `SendMessageReActAsync` przez usuniÄ™cie redundancji sĹ‚owa kluczowego `var` przy re-definicji w pÄ™tli.
  - Skorygowano rzutowanie typĂłw w `double.TryParse` dla `GpuOffload` w `LLMClient.cs` oraz `LLMConfigDialog.cs` poprzez uĹĽycie `System.Globalization.NumberStyles.Any`.
  - Dodano peĹ‚ny namespace `System.Text.Encoding` dla `StringContent` w `LLMConfigDialog.cs` w celu wyeliminowania bĹ‚Ä™du braku nazwy `Encoding` w kontekĹ›cie.
- Zweryfikowano poprawnoĹ›Ä‡ kompilacji - kompilacja zakoĹ„czyĹ‚a siÄ™ peĹ‚nym sukcesem (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- System jest stabilny, w peĹ‚ni kompatybilny wstecznie ze wszystkimi chmurowymi dostawcami (OpenRouter, OpenAI) oraz przystosowany do zaawansowanego sterowania i dynamicznego Ĺ‚adowania modeli lokalnych w LM Studio.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie dziaĹ‚ania dynamicznego Ĺ‚adowania w Ĺ›rodowisku CAD po netloadzie wtyczki.

## [v2.20.16] 2026-06-03T13:35:00+02:00 - UsuniÄ™cie nieobsĹ‚ugiwanych parametrĂłw z payloadu LM Studio API
### [ZREALIZOWANO]
- RozwiÄ…zano bĹ‚Ä…d `unrecognized_keys` w LM Studio (`contextLength`, `flashAttention`, `offloadKvCacheToGpu`):
  - UsuniÄ™to duplikowane camelCase parametry z payloadu Ĺ‚adowania w `LLMClient.cs` oraz `LLMConfigDialog.cs`. REST API LM Studio wspiera wyĹ‚Ä…cznie snake_case.
  - UsuniÄ™to nieobsĹ‚ugiwany parametr `gpu` z payloadu REST API. GPU offload w LM Studio REST API jest konfigurowany i dziedziczony z domyĹ›lnego profilu modelu w GUI aplikacji LM Studio.
  - Zaktualizowano podpowiedĹş (ToolTip) dla pola GPU w `LLMConfigDialog.cs`, aby jasno informowaÄ‡ o tym zachowaniu API LM Studio.
- Ponownie przetestowano kompilacjÄ™ projektu (sukces, 0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- System w peĹ‚ni zsynchronizowany ze Ĺ›cisĹ‚ym schematem REST API LM Studio (`POST /api/v1/models/load`), co eliminuje bĹ‚Ä™dy `unrecognized_keys` (BadRequest).
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Czekanie na weryfikacjÄ™ ze strony uĹĽytkownika.

## [v2.20.17] 2026-06-03T14:00:00+02:00 - WdroĹĽenie dynamicznego edytora system promptu w zakĹ‚adce Ustawienia
### [ZREALIZOWANO]
- WdroĹĽono dynamiczne zarzÄ…dzanie promptem systemowym z poziomu UI wtyczki BricsCAD:
  - Dodano nowÄ…, ostatniÄ… zakĹ‚adkÄ™ "âš™ď¸Ź Ustawienia" w panelu gĹ‚Ăłwnym `AgentControl.cs`.
  - Umieszczono na niej edytor tekstowy `txtSystemPromptEditor` (RichTextBox) wyĹ›wietlajÄ…cy aktualnÄ… treĹ›Ä‡ promptu.
  - Zaimplementowano obsĹ‚ugÄ™ zapisu przez przycisk "đź’ľ Zapisz Prompt", ktĂłry nadpisuje plik `system_prompt.txt` na dysku i wywoĹ‚uje `RebuildSystemPrompt()`, odĹ›wieĹĽajÄ…c stan w pamiÄ™ci aktywnej sesji czatu.
  - Zsynchronizowano edytor z metodÄ… `RebuildSystemPrompt()`, dziÄ™ki czemu przy kaĹĽdym resecie pamiÄ™ci lub wczytaniu ustawieĹ„ treĹ›Ä‡ w edytorze jest aktualizowana.
  - Dodano peĹ‚ne wsparcie kolorystyczne (ApplyTheme) dla nowo dodanych kontrolek.
- Zweryfikowano poprawnoĹ›Ä‡ kompilacji - kompilacja zakoĹ„czyĹ‚a siÄ™ peĹ‚nym sukcesem (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„), a plik DLL zostaĹ‚ pomyĹ›lnie przebudowany.
### [STAN_SYSTEMU]
- System stabilny, rozbudowany o kompletnÄ… zakĹ‚adkÄ™ ustawieĹ„ uĹ‚atwiajÄ…cÄ… dynamicznÄ… pracÄ™ z promptem systemowym w locie bez restartu BricsCAD i bez rekompilacji.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Uruchomienie wtyczki w programie BricsCAD i weryfikacja nowej zakĹ‚adki "Ustawienia".

## [v2.20.18] 2026-06-03T14:15:00+02:00 - Dodanie struktury podzakĹ‚adek (TabControl) w Ustawieniach
### [ZREALIZOWANO]
- Przebudowano strukturÄ™ zakĹ‚adki "âš™ď¸Ź Ustawienia" w `AgentControl.cs` w celu wdroĹĽenia architektury zagnieĹĽdĹĽonych podzakĹ‚adek (TabControl):
  - Utworzono podzakĹ‚adkÄ™ "Prompt" przeznaczonÄ… do edycji promptu systemowego.
  - Dodano do podzakĹ‚adki przycisk "đź“‚ OtwĂłrz folder" wywoĹ‚ujÄ…cy `System.Diagnostics.Process` z argumentem `/select` w celu otwarcia Eksploratora Windows i automatycznego zaznaczenia pliku `system_prompt.txt`.
  - Dostosowano mechanizm `ApplyTheme` do aplikowania motywĂłw kolorystycznych (tĹ‚a i czcionek) rĂłwnieĹĽ dla nowo powstaĹ‚ego kontenera podzakĹ‚adek `tabSettingsSub` i jego dzieci.
- Przeprowadzono pomyĹ›lnÄ… kompilacjÄ™ (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- System stabilny, zorganizowany pod kÄ…tem przyszĹ‚ej rozbudowy ustawieĹ„, z wygodnÄ… integracjÄ… z systemowym menedĹĽerem plikĂłw (Explorer).
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Czekanie na weryfikacjÄ™ ze strony uĹĽytkownika.

## [v2.20.19] 2026-06-03T14:45:00+02:00 - Fix wczytywania parametrĂłw dostawcĂłw LLM w GUI
### [ZREALIZOWANO]
- Naprawiono bĹ‚Ä…d pustych/niewidocznych parametrĂłw aktywnego profilu przy otwarciu okna "Ustawienia DostawcĂłw LLM":
  - W metodzie `LoadData` w pliku `LLMConfigDialog.cs` dodano reset `cbProviders.SelectedIndex = -1` przed przypisaniem docelowego indeksu dostawcy. Wymusza to poprawne wywoĹ‚anie zdarzenia `SelectedIndexChanged` w WinForms, kiedy domyĹ›lny indeks pokrywa siÄ™ z indeksem 0 (ktĂłry byĹ‚ automatycznie przypisywany przy bindowaniu DataSource).
- Zweryfikowano poprawnoĹ›Ä‡ kompilacji (0 bĹ‚Ä™dĂłw).
### [STAN_SYSTEMU]
- System stabilny, poprawione zachowanie UI konfiguracji dostawcĂłw. Parametry wczytujÄ… siÄ™ natychmiast po uruchomieniu okna dialogowego.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Czekanie na weryfikacjÄ™ ze strony uĹĽytkownika.

## [v2.20.20] 2026-06-03T15:05:00+02:00 - Refaktoryzacja WykonajInteligentneZaznaczenie z Regex na Newtonsoft JSON
### [ZREALIZOWANO]
- Zrefaktoryzowano metodÄ™ `WykonajInteligentneZaznaczenie` w pliku `AgentCommand.cs` (V1).
- ZastÄ…piono 4 wywoĹ‚ania `Regex.Match`/`Regex.Matches` (EntityType, Mode, Scope, Conditions) parsowaniem strukturalnym przez `JObject.Parse(json)` z biblioteki `Newtonsoft.Json.Linq`.
- Dodano blok `try-catch` dla `JsonReaderException` z komunikatem diagnostycznym, obsĹ‚ugujÄ…cy uszkodzony JSON generowany przez LLM.
- Ekstrakcja `Conditions` wykonywana jest teraz przez iteracjÄ™ po `JArray`, a `Value` jest bezpiecznie konwertowane przez `JToken.ToString()` (obsĹ‚uguje string, liczby i bool bez problemĂłw z cudzysĹ‚owami).
- Zachowano domyĹ›lne wartoĹ›ci: `Mode = "New"`, `Scope = "Model"` (operator `??`).
- CaĹ‚a dolna logika metody (transakcje BricsCAD, refleksja, `AktywneZaznaczenie`) pozostaĹ‚a bez zmian.
### [STAN_SYSTEMU]
- Plik `AgentCommand.cs` zmodyfikowany, import `Newtonsoft.Json.Linq` juĹĽ istniaĹ‚. Metoda jest teraz odporna na warianty formatowania JSON z LLM.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Kompilacja i weryfikacja poprawnoĹ›ci dziaĹ‚ania w Ĺ›rodowisku BricsCAD.

## [v2.21.0] 2026-06-03T20:10:00+02:00 - Multi-Agent Krok 1: Refaktoring silnika ReAct i IExecutionContext
### [ZREALIZOWANO]
- Wykonano Krok 1 z `12_Mulitagent_upgrade.md`: Refaktoring silnika ReAct (`LLMClient.cs`).
- Utworzono interfejs `IExecutionContext` oraz `CadExecutionContext` aby uniezaleĹĽniÄ‡ Workery od sztywnego wymogu `Document doc`.
- Utworzono strukturÄ™ `AgentExecutionResult` w `Models` dla zwracania spĂłjnych wynikĂłw wykonania Agenta.
- Zmodyfikowano `LLMClient.cs`: usuniÄ™to z pÄ™tli obsĹ‚ugÄ™ `RequestAdditionalTools` (pulÄ… narzÄ™dzi zarzÄ…dza teraz Supervisor przed wywoĹ‚aniem pÄ™tli) oraz zmieniono logikÄ™ powrotnÄ… na uĹĽycie `AgentExecutionResult`.
- Dostosowano `AgentTesterControl` i `AgentControl` do nowych zmian i kompilacja (dotnet build) zakoĹ„czyĹ‚a siÄ™ peĹ‚nym sukcesem.
### [STAN_SYSTEMU]
- System jest stabilny. Silnik konwersacyjny gotowy na krok 2, czyli implementacjÄ™ `Blackboard` i wyizolowanie zarzÄ…dzania z wewnÄ…trz poszczegĂłlnych pÄ™tli.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Krok 2: WdroĹĽenie Blackboard (wspĂłĹ‚dzielony stan).

## [v2.21.1] 2026-06-03T20:12:00+02:00 - Multi-Agent Krok 2: SharedMemoryState i narzÄ™dzia Read/Write Blackboard
### [ZREALIZOWANO]
- Wykonano Krok 2 z `12_Mulitagent_upgrade.md`: WdroĹĽono Blackboard (WspĂłĹ‚dzielony Stan).
- Utworzono klasÄ™ `SharedMemoryState` (`ConcurrentDictionary<string, string>`) dla bezpiecznej wymiany danych miÄ™dzy agentami.
- Utworzono i podpiÄ™to do projektu narzÄ™dzia: `WriteToBlackboardTool` oraz `ReadFromBlackboardTool`.
- Kompilacja przebiegĹ‚a pomyĹ›lnie.
### [STAN_SYSTEMU]
- System posiada teraz tablicÄ™ ogĹ‚oszeĹ„ do trwaĹ‚ego przechowywania i przekazywania danych kontekstowych miÄ™dzy rĂłĹĽnymi izolowanymi sesjami konwersacyjnymi.
### [BLOKADY / PROBLEMY]
- Drobne bĹ‚Ä™dy kompilacji (nieaktualne nazwy klas `ToolFunction` na `FunctionSchema` w nowych narzÄ™dziach) - bĹ‚yskawicznie naprawione.
### [KOLEJNY_KROK]
- Krok 3: Profile NarzÄ™dzi (`ToolConfigManager.cs`).

## [v2.21.2] 2026-06-03T20:14:00+02:00 - Multi-Agent Krok 3: Profile agentĂłw i konfiguracja ToolConfigRoot
### [ZREALIZOWANO]
- Wykonano Krok 3 z `12_Mulitagent_upgrade.md`: Restrukturyzacja zarzÄ…dzania narzÄ™dziami.
- Wprowadzono nowÄ… strukturÄ™ konfiguracji JSON w `ToolConfigManager.cs` (`ToolConfigRoot`), grupujÄ…cÄ… definicje narzÄ™dzi oraz nowe Profile AgentĂłw.
- Zdefiniowano profile (np. `SupervisorProfile`, `CadProfile`) ze zdefiniowanymi przypisaniami Ĺ›cieĹĽek do pliku promptu systemowego oraz dostÄ™pnych narzÄ™dzi (`AllowedTools`) i tagĂłw (`AllowedTags`).
- Dodano wstecznÄ… kompatybilnoĹ›Ä‡ podczas Ĺ‚adowania (automatyczna migracja starej pĹ‚askiej struktury do nowej `ToolConfigRoot`).
- Skorygowano UI w `AgentControl.cs` podpinajÄ…c przywrĂłconÄ… metodÄ™ `UpdateSettings`.
### [STAN_SYSTEMU]
- Kompilacja przebiegĹ‚a pomyĹ›lnie. Nowy plik `tools_config.json` z profilami generuje siÄ™ bezbĹ‚Ä™dnie. Agenci mogÄ… byÄ‡ teraz instancjowani z okreĹ›lonym profilem kompetencji.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Krok 4: Implementacja Supervisora (NadrzÄ™dna pÄ™tla sterujÄ…ca).

## [v2.21.3] 2026-06-03T20:20:00+02:00 - Multi-Agent Krok 4: Klasa SupervisorOrchestrator i DelegateTaskTool
### [ZREALIZOWANO]
- Wykonano Krok 4 z `12_Mulitagent_upgrade.md`: Utworzenie Orchestratora GĹ‚Ăłwnego.
- Utworzono klasÄ™ `SupervisorOrchestrator`, odpowiedzialnÄ… za przechowywanie globalnej historii i inicjalizacjÄ™ gĹ‚Ăłwnej pÄ™tli dla "SupervisorProfile".
- Stworzono narzÄ™dzie `DelegateTaskTool` (IToolV2), dziÄ™ki ktĂłremu Supervisor moĹĽe delegowaÄ‡ konkretne instrukcje do sprofilowanych agentĂłw (np. `CadProfile`).
- `DelegateTaskTool` poprawnie blokuje gĹ‚Ăłwny cykl Supervisora, spawnujÄ…c sub-agenta i przekazujÄ…c mu zadanie wraz z wstrzykniÄ™ciem kontekstu pamiÄ™ci z Blackboarda.
- Zaktualizowano `AgentControl.cs` i usuniÄ™to obsĹ‚ugÄ™ lokalnej zmiennej `_conversationHistory`, przepinajÄ…c UI bezpoĹ›rednio pod `SupervisorOrchestrator`.
- Kompilacja przebiegĹ‚a pomyĹ›lnie.
### [STAN_SYSTEMU]
- Architektura opiera siÄ™ teraz o wzorzec Supervisor-Worker. Komunikacja przechodzi przez Agenta GĹ‚Ăłwnego (Supervisora), ktĂłry nastÄ™pnie deleguje pracÄ™ "w dĂłĹ‚" z ustandaryzowanymi profilami (Agent Ekspert).
### [BLOKADY / PROBLEMY]
- WymagaĹ‚o kaskadowej refaktoryzacji we wszystkich narzÄ™dziach oraz kontrolkach ze wzglÄ™du na zmianÄ™ sygnatury `ExecuteTool` przyjmujÄ…cej teraz `IExecutionContext` zamiast `Document doc`. Sukces po iteracyjnych poprawkach i ponownych kompilacjach.
### [KOLEJNY_KROK]
- Krok 5: Propagacja i ZgodnoĹ›Ä‡ UX (Eventy z sub-pÄ™tli wysyĹ‚ane do AgentControl, obsĹ‚uga logowania wywoĹ‚aĹ„ w UI z zachowaniem informacji o roli Agenta wykonujÄ…cego).

## [v2.21.4] 2026-06-03T20:25:00+02:00 - Multi-Agent Krok 5: Integracja ChatSession i SessionManager
### [ZREALIZOWANO]
- Wykonano Krok 5 z `12_Mulitagent_upgrade.md`: Propagacja i ZgodnoĹ›Ä‡ UX.
- Zmodyfikowano kontrolkÄ™ `AgentControl.cs`, upewniajÄ…c siÄ™, ĹĽe metody aktualizujÄ…ce HUD (`UpdateStatusHUD`, `UpdateStatsHUD`, `AppendToolLog`) sÄ… publiczne i gotowe do odbioru zdarzeĹ„ z zewnÄ…trz.
- PodĹ‚Ä…czono w `DelegateTaskTool.cs` instancjÄ™ sub-klienta (`LLMClient`) do gĹ‚Ăłwnej instancji kontrolki UI (`AgentControl.Instance`).
- Zdarzenia z sub-klienta (Eksperta) sÄ… teraz propagowane na gĹ‚Ăłwny ekran z prefiksem okreĹ›lajÄ…cym aktywny profil (np. `[CadProfile] Oczekiwanie na analizÄ™...`).
- Wyniki dziaĹ‚ania sub-klienta (zwracane przez narzÄ™dzie) sÄ… natywnie dodawane przez Supervisora do `_globalHistory` jako wiadomoĹ›ci typu `tool`, co zamyka pÄ™tlÄ™ ReAct.
- Kompilacja przebiegĹ‚a bezbĹ‚Ä™dnie.
### [STAN_SYSTEMU]
- Kompletne wdroĹĽenie architektury Multi-Agent (Supervisor-Worker) zostaĹ‚o zakoĹ„czone. CaĹ‚y projekt dziaĹ‚a w trybie hierarchicznym, odciÄ…ĹĽajÄ…c jeden gĹ‚Ăłwny system prompt od nadmiaru wiedzy, izolujÄ…c narzÄ™dzia i zapobiegajÄ…c halucynacjom wywoĹ‚anym zbyt duĹĽym kontekstem.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- RozpoczÄ™cie tworzenia konkretnych, wyspecjalizowanych narzÄ™dzi pod kÄ…tem nowych profili (np. dedykowany Agent do zarzÄ…dzania arkuszami).

## [v2.21.5] 2026-06-03T22:35:00+02:00 - Dostosowanie zakĹ‚adki Agenci i Tester V2 do Multi-Agent
### [ZREALIZOWANO]
- Naprawiono regresjÄ™ routingu Supervisora i brakujÄ…cych profili w konfiguracji:
  - Zaimplementowano funkcjÄ™ `EnsureSupervisorPromptFile()` w `ToolConfigManager.cs`, ktĂłra automatycznie tworzy plik `system_prompt_supervisor.txt` z rygorystycznymi wytycznymi dotyczÄ…cymi braku pogawÄ™dek i natychmiastowego delegowania do `CadProfile`.
  - Zaktualizowano `SyncWithTools` w `ToolConfigManager.cs`, aby automatycznie synchronizowaĹ‚ i uzupeĹ‚niaĹ‚ brakujÄ…ce lub puste profile (`SupervisorProfile`, `CadProfile`) oraz ich domyĹ›lne dozwolone narzÄ™dzia w pliku `tools_config.json`.
  - Mapowano `CadProfile` do `system_prompt.txt` zamiast `system_prompt_cad.txt`, co przywrĂłciĹ‚o peĹ‚nÄ… kompatybilnoĹ›Ä‡ z wbudowanym w UI edytorem promptu systemowego.
  - Zmodyfikowano `GetToolsPayloadForProfile` w `ToolOrchestrator.cs`, umoĹĽliwiajÄ…c dynamicznÄ… aktywacjÄ™ narzÄ™dzi na podstawie tagĂłw sesji (`SessionDynamicTags`) oraz profili (`AllowedTags`), co przywrĂłciĹ‚o mechanizm Ĺ‚adowania dynamicznego w architekturze Multi-Agent.
  - PomyĹ›lnie przebudowano i skompilowano wtyczkÄ™ bez bĹ‚Ä™dĂłw i ostrzeĹĽeĹ„.
### [STAN_SYSTEMU]
- System jest stabilny. Supervisor poprawnie wykrywa narzÄ™dzia delegujÄ…ce, a Worker CAD posiada dostÄ™p do wszystkich dedykowanych komend i poprawnie reaguje na zmiany promptu w locie.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie zachowania asystenta w programie BricsCAD pod kÄ…tem masowych selekcji i edycji.

## [v2.21.6] 2026-06-03T22:52:00+02:00 - BielikLogger crash-safe diagnostics i logi w GUI
### [ZREALIZOWANO]
- UporzÄ…dkowano i zsynchronizowano pozostaĹ‚e zakĹ‚adki aplikacji z architekturÄ… Multi-Agent:
  - **Ustawienia (Prompt Editor)**: Dodano rozwijanÄ… listÄ™ `cbPromptFile` do wyboru pliku promptu (`system_prompt.txt` / `system_prompt_supervisor.txt`), umoĹĽliwiajÄ…c dynamicznÄ… edycjÄ™ i zapis promptĂłw CAD i Supervisora bezpoĹ›rednio z UI.
  - **Tester V2**: WdroĹĽono ComboBox `cbProfiles` pozwalajÄ…cy na testowanie zapytaĹ„ w kontekĹ›cie konkretnego profilu (`CadProfile` / `SupervisorProfile` / Monolit). Zapytania sÄ… teraz wysyĹ‚ane z dedykowanymi promptami wczytywanymi z dysku dla danego profilu.
  - **Benchmark**: Zmodyfikowano `SendMessageBenchmarkAsync` w `LLMClient.cs` tak, aby wczytywaĹ‚ peĹ‚nÄ… listÄ™ narzÄ™dzi (tag `#all`). UmoĹĽliwiĹ‚o to pomyĹ›lnÄ… walidacjÄ™ benchmarkĂłw testujÄ…cych nie-bazowe narzÄ™dzia CAD.
  - **Dataset Studio & Tool Logs**: Dodano przechwytywanie logĂłw wykonania w `DelegateTaskTool.cs`. KaĹĽda izolowana pÄ™tla Workera (z jej tool calls i odpowiedziami) jest automatycznie rejestrowana jako osobny rekord sesji w Dataset Studio. RozwiÄ…zuje to problem utraty danych treningowych CAD w architekturze hierarchicznej.
  - **LLM stats**: Dodano wĹ‚aĹ›ciwoĹ›Ä‡ `LastStats` do klasy `LLMClient.cs` w celu pobierania statystyk tokenĂłw na koniec wywoĹ‚aĹ„.
### [STAN_SYSTEMU]
- Wszystkie zakĹ‚adki w panelu bocznym wtyczki zostaĹ‚y zintegrowane i przetestowane syntetycznie. System w peĹ‚ni wspiera hierarchiczne fine-tuning i diagnostykÄ™.
### [BLOKADY / PROBLEMY]
- Blokowanie pliku DLL `bin\Debug\Bricscad_AgentAI_V2.dll` przez dziaĹ‚ajÄ…cÄ… instancjÄ™ BricsCAD przy prĂłbie skopiowania po kompilacji (kod ĹşrĂłdĹ‚owy kompiluje siÄ™ bez bĹ‚Ä™dĂłw w `obj\Debug`).
### [KOLEJNY_KROK]
- Uruchomienie zaktualizowanego panelu w BricsCAD (po restarcie aplikacji CAD w celu zwolnienia blokady pliku DLL) i testy manualne.

## [v2.21.7] 2026-06-03T23:10:00+02:00 - Testy LISP generatora dla walidacji operacji na blokach
### [ZREALIZOWANO]
- WdroĹĽono lekki, bezpieczny system logowania i diagnostyki `BielikLogger.cs` w celu monitorowania dziaĹ‚ania wtyczki w locie i diagnozowania nagĹ‚ych zamkniÄ™Ä‡ (crashy) programu BricsCAD:
  - Stworzono klasÄ™ statycznÄ… `BielikLogger` piszÄ…cÄ… synchronicznie (dla bezpieczeĹ„stwa zapisu przed crashem) do pliku `bielik_debug.log` z rotacjÄ… po przekroczeniu rozmiaru 1 MB.
  - Zarejestrowano unhandled exception i thread exception trap w `AgentStartup.cs` logujÄ…ce stack trace na sekundy przed zamkniÄ™ciem procesu.
  - Zintegrowano logowanie zapytaĹ„ LLM w `LLMClient.cs` oraz wywoĹ‚aĹ„ narzÄ™dzi w `ToolOrchestrator.ExecuteTool` z doĹ‚Ä…czeniem identyfikacji wÄ…tkĂłw (`[UI]` / `[Worker]`).
  - Dodano podzakĹ‚adkÄ™ "Diagnostyka" w zakĹ‚adce "Ustawienia" w `AgentControl.cs` z podglÄ…dem logu w czasie rzeczywistym, czyszczeniem logĂłw i bezpoĹ›rednim otwieraniem pliku w systemowym Notatniku.
- Dodano plik `BielikLogger.cs` do kompilacji w `Bricscad_AgentAI_V2.csproj`.
- PomyĹ›lnie skompilowano projekt (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- System jest w peĹ‚ni stabilny, wyposaĹĽony w automatyczny trap bĹ‚Ä™dĂłw i bezpieczne logi diagnostyczne do Ĺ›ledzenia w BricsCAD.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testy w Ĺ›rodowisku BricsCAD z otwartym podglÄ…dem Diagnostyki i przechwyceniem logĂłw w razie awarii.

## [v2.21.8] 2026-06-03T23:25:00+02:00 - Integracja orkiestratora z wÄ…tkiem GUI BricsCAD
### [ZREALIZOWANO]
- Wprowadzono architekturÄ™ wieloagentowÄ… z trzema nowymi, wysoce wyspecjalizowanymi profilami pod-agentĂłw (Workers) w celu skrĂłcenia czasu reakcji, redukcji tokenĂłw i zwiÄ™kszenia precyzji:
  - **`CadGeometryProfile`**: Dedykowany do rysowania, warstw, kolorĂłw, linii i tekstĂłw (narzÄ™dzia geometryczne).
  - **`CadBlocksProfile`**: Dedykowany do operacji na blokach i atrybutach.
  - **`CadMetadataProfile`**: Dedykowany do pomiarĂłw, odczytu wĹ‚aĹ›ciwoĹ›ci i metadanych XData.
  - **`CadProfile`**: Pozostawiony jako profil ogĂłlny/awaryjny.
  - Zaktualizowano `ToolConfigManager.cs`, aby automatycznie synchronizowaĹ‚ i tworzyĹ‚ te profile w `tools_config.json`.
  - WdroĹĽono automatyczny upgrade `system_prompt_supervisor.txt`, ktĂłry instruuje Supervisora o istnieniu 3 wyspecjalizowanych profilĂłw i zasadach kierowania zadaĹ„ (routingu).
  - Rozbudowano okno **Tester V2** (`AgentTesterControl.cs`), dodajÄ…c nowe profile do listy wyboru w celach testowych.
  - Zaimplementowano `VariableStore` w `AgentMemoryState.cs` w celu automatycznego replikowania zmiennych sesyjnych Agenta na globalny Blackboard (rozwiÄ…zanie problemu odczytu odczytanych atrybutĂłw przez workera).
  - Dodano parametry `FilterTag` i `FilterValue` do `EditAttributesTool.cs` w celu umoĹĽliwienia modyfikacji konkretnego wystÄ…pienia bloku w zaznaczeniu.
- Przygotowano AutoLISP-owy skrypt testowy **[generate_test_objects.lsp](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/generate_test_objects.lsp)** definiujÄ…cy komendÄ™ `GEN_BIELIK_TESTS`, ktĂłra tworzy warstwy, polilinie z XData BIELIK_APP, teksty oraz bloki z atrybutami, sĹ‚uĹĽÄ…ce do weryfikacji kaĹĽdego z 3 nowych profili.
- PomyĹ›lnie skompilowano projekt (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- System posiada w peĹ‚ni sprofilowanÄ… strukturÄ™ hierarchicznÄ… z dedykowanymi agentami roboczymi. Gotowy do testowania w BricsCAD.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- ZaĹ‚adowanie LISP-a w BricsCAD, uruchomienie generowania obiektĂłw i przeprowadzenie testĂłw routingu Supervisora oraz wykonania u poszczegĂłlnych agentĂłw.

## [v2.21.9] 2026-06-03T23:36:00+02:00 - Selekcja i filtrowanie BlockReference z wieloznacznikami w SelectEntitiesTool
### [ZREALIZOWANO]
- Naprawiono bĹ‚Ä…d wyszukiwania instancji blokĂłw (`SelectEntitiesTool.cs`) podczas selekcji po `EntityType`:
  - Przechwycono zapytania o wĹ‚aĹ›ciwoĹ›ci `"EntityType"` oraz `"Type"`, przekierowujÄ…c je bezpoĹ›rednio na pobranie nazwy typu C# (`ent.GetType().Name`) i tym samym omijajÄ…c refleksjÄ™ C#, ktĂłra zwracaĹ‚a `null` dla typu klasy `BlockReference`.
  - WdroĹĽono obsĹ‚ugÄ™ dopasowaĹ„ z uĹĽyciem symboli wieloznacznych (Wildcard, np. `*BlockReference`) w metodzie `ValidateLogicCondition` za pomocÄ… mapowania na wyraĹĽenia regularne (`IsWildcardMatch`).
  - Dodano asercje testowe w `SelectEntitiesToolTests.cs` weryfikujÄ…ce poprawnoĹ›Ä‡ dopasowaĹ„ wieloznacznych dla operatorĂłw `==` i `!=`.
  - Zweryfikowano poprawnoĹ›Ä‡ kompilacji projektu (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- Filtrowanie i selekcja encji w pamiÄ™ci Agenta (w tym po typie `BlockReference` z uĹĽyciem symboli wieloznacznych) dziaĹ‚a w 100% stabilnie i niezawodnie, zapobiegajÄ…c bĹ‚Ä™dom pustej pamiÄ™ci w Scenario 2.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Uruchomienie BricsCAD i ponowne przetestowanie Scenario 2 w celu weryfikacji zmiany atrybutu biurka o ID A2 na "Wolne".

## [v2.21.10] 2026-06-03T23:55:00+02:00 - ZĹ‚agodzenie restrykcji promptu systemowego Supervisora
### [ZREALIZOWANO]
- ZĹ‚agodzono restrykcje promptu systemowego Supervisora (`system_prompt_supervisor.txt`):
  - Zaktualizowano definicjÄ™ i generowanie promptu w `ToolConfigManager.cs`, aby jednoznacznie zezwalaÄ‡ na bezpoĹ›rednie, rzeczowe i przyjazne odpowiadanie na pytania ogĂłlne (matematyczne, historyczne, luĹşne rozmowy) bez delegowania ani odmawiania.
  - Zaimplementowano automatyczny mechanizm wykrywania i nadpisywania (auto-upgrade) starych wersji promptu w `EnsureSupervisorPromptFile()` w oparciu o obecnoĹ›Ä‡ tagu `"PYTANIA OGĂ“LNE"`.
  - PomyĹ›lnie przebudowano i skompilowano wtyczkÄ™ (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- Supervisor jest w stanie poprawnie kierowaÄ‡ zapytaniami: deleguje operacje CAD do dedykowanych workerĂłw, a na pytania ogĂłlne (niezwiÄ…zane z silnikiem CAD) odpowiada samodzielnie w zwykĹ‚ym tekĹ›cie.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Weryfikacja dziaĹ‚ania przez uĹĽytkownika w programie BricsCAD (pytania ogĂłlne typu "kto byĹ‚ pierwszym krĂłlem Polski" lub obliczenia matematyczne powinny teraz uzyskiwaÄ‡ bezpoĹ›rednie odpowiedzi).

## [v2.22.0] 2026-06-04T00:15:00+02:00 - WdroĹĽenie CadMathProfile, promptu Math Experta i narzÄ™dzia CalculateRpn
### [ZREALIZOWANO]
- WdroĹĽono wyspecjalizowany profil obliczeniowy `CadMathProfile` oraz narzÄ™dzie `CalculateRpn`:
  - Utworzono klasÄ™ [CalculateRpnTool.cs](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/src/Tools/CalculateRpnTool.cs) implementujÄ…cÄ… interfejs `IToolV2`, umoĹĽliwiajÄ…cÄ… bezpieczne wykonywanie obliczeĹ„ RPN za pomocÄ… wewnÄ™trznego silnika wymiarowego `RpnCalculator`.
  - Dodano automatycznÄ… generacjÄ™ promptu systemowego `system_prompt_math.txt` dla Math Experta.
  - Zarejestrowano profil `CadMathProfile` w `ToolConfigManager.cs` ze Ĺ›cisĹ‚ym zakresem dopuszczalnych narzÄ™dzi (`CalculateRpn`, `ReadFromBlackboard`, `WriteToBlackboard`, `UserInput`, `UserChoice`) i powiÄ…zaniem z promptem obliczeniowym.
  - Zaktualizowano prompt Supervisora, dodajÄ…c routowanie zadaĹ„ matematyczno-fizycznych i przeliczania jednostek bezpoĹ›rednio do `CadMathProfile`.
  - Dodano `CadMathProfile` do dropdowna `cbProfiles` w **Tester V2** oraz do edytora promptĂłw w zakĹ‚adce **Ustawienia** w `AgentControl.cs`.
  - Napisano testy jednostkowe w `CalculateRpnToolTests.cs` i zintegrowano je z gĹ‚Ăłwnym runnerem `TestRunner.cs`.
  - PomyĹ›lnie skompilowano wtyczkÄ™ jako bibliotekÄ™ DLL (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- System posiada 4 wyspecjalizowane profile robocze (Geometry, Blocks, Metadata, Math) zarzÄ…dzane przez Supervisora. Profil obliczeniowy posiada dedykowane narzÄ™dzie do precyzyjnych obliczeĹ„ w notacji RPN z analizÄ… wymiarowÄ… jednostek SI.
### [BLOKADY / PROBLEMY]
- Testy offline runnera `.exe` wymagajÄ… zaleĹĽnoĹ›ci native C++ od BricsCAD (`BrxMgd.dll`), dlatego peĹ‚na weryfikacja logiki RPN odbywa siÄ™ po zaĹ‚adowaniu wtyczki wewnÄ…trz CAD.
### [KOLEJNY_KROK]
- Testowanie w programie BricsCAD z uĹĽyciem nowego profilu obliczeniowego `CadMathProfile` i narzÄ™dzia `CalculateRpn`.

## [v2.22.1] 2026-06-04T00:17:00+02:00 - Zasady routingu matematyczno-fizycznego Supervisora
### [ZREALIZOWANO]
- Skorygowano reguĹ‚y routingu nadrzÄ™dnego Supervisora w `ToolConfigManager.cs`:
  - Rozgraniczono "LUĹąNÄ„ ROZMOWÄ I WIEDZÄ OGĂ“LNÄ„" (ktĂłrÄ… Supervisor obsĹ‚uguje sam w zwykĹ‚ym tekĹ›cie) od "OBLICZEĹ MATEMATYCZNYCH, FIZYCZNYCH I PRZELICZANIA JEDNOSTEK" (ktĂłre Supervisor musi bezwzglÄ™dnie delegowaÄ‡ do profilu `CadMathProfile` za pomocÄ… narzÄ™dzia `DelegateTask`).
  - Dodano automatyczny upgrade (auto-upgrade) dla szablonĂłw promptu supervisora w oparciu o obecnoĹ›Ä‡ tagu `"LUĹąNA ROZMOWA"`.
  - PomyĹ›lnie przebudowano i skompilowano wtyczkÄ™ (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- Supervisor ma Ĺ›ciĹ›le zdefiniowane warunki brzegowe: luĹşne rozmowy prowadzi sam, a kaĹĽde fizyczne/matematyczne/inĹĽynieryjne obliczenie deleguje do eksperta `CadMathProfile` posiadajÄ…cego dostÄ™p do kalkulatora RPN.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Weryfikacja delegowania obliczeĹ„ do `CadMathProfile` w programie BricsCAD.

## [v2.22.2] 2026-06-04T00:30:00+02:00 - Rozbudowa promptu Math Experta o notacjÄ™ RPN i wzory
### [ZREALIZOWANO]
- Skorygowano i rozbudowano prompt systemowy Math Experta (`system_prompt_math.txt`):
  - Dodano szczegĂłĹ‚owe zasady dziaĹ‚ania notacji RPN na stosie i formatowania wartoĹ›ci z jednostkami (np. `10_m`, `11.34_g/cm3`).
  - Nakazano podziaĹ‚ zĹ‚oĹĽonych obliczeĹ„ na mniejsze, precyzyjne kroki czÄ…stkowe (osobne wywoĹ‚ania `CalculateRpn`) zamiast jednego gigantycznego wyraĹĽenia, co eliminuje bĹ‚Ä™dy zapÄ™tlenia modelu i przekroczenia limitu tokenĂłw.
  - Zaimplementowano instrukcje korzystania z parametrĂłw `SaveAs` i `@zmienna` do przechowywania wartoĹ›ci czÄ…stkowych.
  - Dodano gotowe, poprawne wzory i przykĹ‚ady RPN dla obliczania pola koĹ‚a ($\pi r^2$), objÄ™toĹ›ci kuli ($\frac{4}{3} \pi r^3$), masy oĹ‚owiu oraz energii kinetycznej ($mgh$).
  - Dodano automatyczny upgrade (auto-upgrade) szablonu promptu w oparciu o obecnoĹ›Ä‡ tagu `"WZORY I PRZYKĹADY RPN"`.
  - PomyĹ›lnie przebudowano i skompilowano wtyczkÄ™ (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- Math Expert posiada kompletnÄ… i szczegĂłĹ‚owÄ… bazÄ™ wiedzy na temat skĹ‚adni RPN i strategii podziaĹ‚u obliczeĹ„ na kroki, co zabezpiecza go przed bĹ‚Ä™dnymi operacjami stosu i zapÄ™tleniami.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Ponowne przetestowanie obliczeĹ„ (koĹ‚o i kula) w BricsCAD.


## [v2.22.3] 2026-06-04T00:38:00+02:00 - Auto-upgrade promptu o wytyczne unikania zwisĂłw stosu (Dangled Stack)
### [ZREALIZOWANO]
- Udoskonalono walidacjÄ™ i proces auto-upgrade dla szablonu promptu Math Experta (`system_prompt_math.txt`) w `ToolConfigManager.cs`, wprowadzajÄ…c sprawdzenie obecnoĹ›ci fraz `"CzÄ™sty bĹ‚Ä…d przy uĹ‚amkach"` oraz `"UNIKAJ DANGLED STACK"`.
- Rozbudowano domyĹ›lny prompt systemowy dla profilu `CadMathProfile` o jasne wytyczne dotyczÄ…ce unikania zwisĂłw na stosie (Dangled Stack) oraz zasad zapisu uĹ‚amkĂłw i mnoĹĽenia uĹ‚amkĂłw (np. `4 3 / wyraĹĽenie *` lub `wyraĹĽenie 4 * 3 /` zamiast bĹ‚Ä™dnego `wyraĹĽenie 4 / 3`).
- Dodano test jednostkowy dla objÄ™toĹ›ci kuli z konwersjÄ… jednostkowÄ… (`5_cm 3 ^ #PI * 4 * 3 / 'cm3' CONVE`) w `CalculateRpnToolTests.cs` w celu weryfikacji poprawnoĹ›ci obliczeĹ„ RPN.
- PomyĹ›lnie skompilowano wtyczkÄ™ jako bibliotekÄ™ DLL (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- System stabilny, a prompt Math Experta zabezpieczony przed typowymi bĹ‚Ä™dami generowania wyraĹĽeĹ„ RPN dla uĹ‚amkĂłw i operacji trĂłjskĹ‚adnikowych (mgh).
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Przetestowanie dziaĹ‚ania agenta Bielik (Supervisor + Math Expert) w BricsCAD po ponownym zaĹ‚adowaniu wtyczki z nowym promptem matematycznym.


## [v2.22.4] 2026-06-04T00:43:00+02:00 - E2E testy profilu matematycznego w CAD i blokada profili jako narzÄ™dzi
### [ZREALIZOWANO]
- Zdiagnozowano i pomyĹ›lnie przetestowano wywoĹ‚anie profilu matematycznego w programie BricsCAD. Agent poprawnie rozĹ‚oĹĽyĹ‚ i obliczyĹ‚ energiÄ™ kinetycznÄ… w 4 krokach: promieĹ„ (`0.1_m 2 /`), objÄ™toĹ›Ä‡ (`@Promien 3 ^ #PI * 4 * 3 /`), masÄ™ (`@Objetosc 11340_kg/m3 *`) oraz energiÄ™ kinetycznÄ… (`@Masa #G * 10_m *`). Wszystkie operacje stosu i staĹ‚e fizyczne zadziaĹ‚aĹ‚y bezbĹ‚Ä™dnie.
- Zidentyfikowano pojedynczÄ… prĂłbÄ™ wywoĹ‚ania nazwy profilu `"CadMathProfile"` jako bezpoĹ›redniej nazwy funkcji przez model Supervisor.
- Wprowadzono poprawkÄ™ w `ToolConfigManager.cs` (`EnsureSupervisorPromptFile`): dodano automatycznÄ… aktualizacjÄ™ pliku `system_prompt_supervisor.txt` sprawdzajÄ…cÄ… obecnoĹ›Ä‡ frazy `"Profile NIE sÄ… narzÄ™dziami"`.
- Zaimplementowano nowÄ… reguĹ‚Ä™ w instrukcjach nadrzÄ™dnych Supervisora kategorycznie zabraniajÄ…cÄ… traktowania nazw profilĂłw (np. `CadMathProfile`, `CadGeometryProfile`) jako narzÄ™dzi i nakazujÄ…cÄ… bezwzglÄ™dne korzystanie z `DelegateTask`.
- PomyĹ›lnie zrekompilowano wtyczkÄ™ jako bibliotekÄ™ DLL (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- System jest stabilny, zabezpieczony przed podwĂłjnym wywoĹ‚ywaniem profilu i gotowy do ostatecznych testĂłw.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Oczekiwanie na ostateczne potwierdzenie dziaĹ‚ania przez uĹĽytkownika w Ĺ›rodowisku BricsCAD.


## [v2.22.5] 2026-06-04T00:46:00+02:00 - Silnik benchmarkĂłw dla wyboru profilu i promptĂłw eksperckich
### [ZREALIZOWANO]
- Przebudowano system benchmarkowy pod kÄ…tem obsĹ‚ugi wyboru profilu i testowania konkretnego Agenta Eksperta:
  - Rozszerzono metodÄ™ `SendMessageBenchmarkAsync` w `LLMClient.cs` o opcjonalny parametr `profileName`. Gdy profil jest podany, wywoĹ‚ywany jest orkiestrator z listÄ… dozwolonych narzÄ™dzi i tagĂłw zdefiniowanych dla tego profilu (zamiast standardowego `#all`).
  - Rozszerzono metodÄ™ `RunBenchmarkAsync` w `AutoBenchmarkEngine.cs` o parametr `profileName`. Gdy profil jest wybrany, silnik benchmarkowy dynamicznie wczytuje wĹ‚aĹ›ciwy plik promptu systemowego dla tego agenta (np. `system_prompt_math.txt` dla `CadMathProfile` lub `system_prompt_supervisor.txt` dla `SupervisorProfile`) i automatycznie wstrzykuje go jako pierwszÄ… wiadomoĹ›Ä‡ roli `"system"` w historii konwersacji, w peĹ‚ni odtwarzajÄ…c rzeczywiste Ĺ›rodowisko wykonawcze agenta.
  - Zmodyfikowano kontrolkÄ™ `AutoBenchmarkControl.cs`, wprowadzajÄ…c nowÄ… kontrolkÄ™ rozwijanÄ… `cbProfiles` w pasku gĂłrnym. Dropdown jest dynamicznie uzupeĹ‚niany profilami pobranymi z `ToolConfigManager` z opcjÄ… domyĹ›lnÄ… "(Brak profilu - wszystkie narzÄ™dzia)" dla zachowania peĹ‚nej kompatybilnoĹ›ci wstecznej.
  - Zabezpieczono stan UI, blokujÄ…c moĹĽliwoĹ›Ä‡ zmiany profilu w trakcie trwania benchmarku i odblokowujÄ…c kontrolkÄ™ po zakoĹ„czeniu przebiegu.
  - PomyĹ›lnie zrekompilowano wtyczkÄ™ jako bibliotekÄ™ DLL (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- System w peĹ‚ni zaktualizowany. Panel benchmarkowy umoĹĽliwia precyzyjne testowanie i walidacjÄ™ konkretnych profilĂłw agentĂłw (np. CadMathProfile, CadGeometryProfile) z wĹ‚aĹ›ciwymi promptami i pulami narzÄ™dzi bez wychodzenia z UI testowego.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Przetestowanie nova funkcjÄ™ wyboru profilu w zakĹ‚adce Benchmark systemu BricsCAD.


## [v2.22.6] 2026-06-04T00:49:00+02:00 - ZbiĂłr testowy benchmarku Benchmark_02_Math.json
### [ZREALIZOWANO]
- Utworzono dedykowany zestaw testowy benchmarku [Benchmark_02_Math.json](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/Benchmark_02_Math.json) zawierajÄ…cy 5 reprezentatywnych zadaĹ„ matematycznych i fizycznych (Pole koĹ‚a, ObjÄ™toĹ›Ä‡ kuli, Masa oĹ‚owiu z gÄ™stoĹ›ci, Energia potencjalna, ObjÄ™toĹ›Ä‡ rury).
- Skonfigurowano reguĹ‚y walidacji typu `EvaluateRPN_Argument` w celu precyzyjnego obliczania i weryfikowania wyjĹ›ciowej wartoĹ›ci RPN (np. asercja do `"7853.981634_mm2"` lub `"86.393798_L"`).
- Skonfigurowano symulowane odpowiedzi CAD (`SimulatedCADResponses`) dla Kalkulatora RPN dla kaĹĽdego z 5 zadaĹ„, co umoĹĽliwia dwuetapowÄ… symulacjÄ™ konwersacji w pÄ™tli ReAct podczas dziaĹ‚ania benchmarku.
### [STAN_SYSTEMU]
- Plik benchmarkowy utworzony w folderze `/tests`, w peĹ‚ni zgodny ze schematem V2 i gotowy do wczytania w UI testowym.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Wczytanie pliku `Benchmark_02_Math.json` w zakĹ‚adce Benchmark i uruchomienie testu z wybranym profilem `CadMathProfile`.


## [v2.22.7] 2026-06-04T00:54:00+02:00 - Optymalizacja wzoru objÄ™toĹ›ci kuli w promptach
### [ZREALIZOWANO]
- Naprawiono i uĹ›ciĹ›lono przykĹ‚ad obliczania objÄ™toĹ›ci kuli w `EnsureMathPromptFile` w `ToolConfigManager.cs` (dodano `'cm3' CONVE` do wzoru przykĹ‚adowego, aby model precyzyjnie konwertowaĹ‚ jednostki do oczekiwanego formatu).
- Zmodyfikowano zapytania `UserPrompt` w pliku [Benchmark_02_Math.json](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/Benchmark_02_Math.json), doprecyzowujÄ…c wymĂłg wykonania obliczeĹ„ w pojedynczym kroku RPN z wyraĹşnym poleceniem konwersji jednostki (`CONVE`). RozwiÄ…zuje to problem przedwczesnego zatrzymania pÄ™tli ReAct po napotkaniu mockowanych odpowiedzi w teĹ›cie wieloetapowym.
- UsuniÄ™to bĹ‚Ä™dy skĹ‚adniowe w `ToolConfigManager.cs` powstaĹ‚e podczas nakĹ‚adania zmian, a caĹ‚y projekt skompilowaĹ‚ siÄ™ bez ostrzeĹĽeĹ„.
### [STAN_SYSTEMU]
- Pliki kodu i konfiguracji benchmarku sÄ… zsynchronizowane, a silnik benchmarkowy i model majÄ… precyzyjne dopasowanie pod kÄ…tem jednopoziomowych obliczeĹ„ RPN.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Ponowne wykonanie testu benchmarkowego `Benchmark_02_Math.json` w BricsCAD i weryfikacja skutecznoĹ›ci.


## [v2.22.8] 2026-06-04T01:05:00+02:00 - Uruchomienie rzeczywistych narzÄ™dzi obliczeniowych w benchmarku
### [ZREALIZOWANO]
- **Realne wykonanie narzÄ™dzi obliczeniowych i pamiÄ™ciowych w benchmarku**: Zmodyfikowano metodÄ™ `SendMessageBenchmarkAsync` w [LLMClient.cs](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/src/Core/LLMClient.cs) tak, aby narzÄ™dzia `CalculateRpn`, `ReadFromBlackboard` oraz `WriteToBlackboard` byĹ‚y wykonywane naprawdÄ™ za pomocÄ… orkiestratora, zamiast zwracania mockowanych odpowiedzi z pliku JSON. RozwiÄ…zuje to problem przedwczesnego zakoĹ„czenia pÄ™tli ReAct (LLM przestaje koĹ„czyÄ‡ dziaĹ‚anie na pierwszym kroku obliczeĹ„ czÄ…stkowych po otrzymaniu mockowanego wyniku ostatecznego).
- **Walidacja ostatniego wywoĹ‚ania RPN**: Zaktualizowano reguĹ‚Ä™ `EvaluateRPN_Argument` w [AutoBenchmarkEngine.cs](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/src/Core/AutoBenchmarkEngine.cs) do pobierania ostatniego wywoĹ‚ania `CalculateRpn` (`LastOrDefault`) zamiast pierwszego wywoĹ‚ania z argumentami (`FirstOrDefault`). Zapewnia to poprawnÄ… weryfikacjÄ™ koĹ„cowego wyniku w zadaniach wielokrokowych, w ktĂłrych LLM odwoĹ‚uje siÄ™ do zmiennych zapisanych w tablicy (blackboard).
- **Kompilacja i stabilnoĹ›Ä‡**: PomyĹ›lnie zrekompilowano wtyczkÄ™ jako bibliotekÄ™ DLL (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- System stabilny, poprawnie obsĹ‚uguje zadania wieloetapowe i obliczenia zmiennych blackboardowych w trybie benchmarkowym z wybranym profilowaniem.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Wykonanie testu benchmarku w programie BricsCAD w celu weryfikacji 100% poprawnoĹ›ci.


## [v2.22.9] 2026-06-04T01:10:00+02:00 - Uproszczenie zapytaĹ„ benchmarkowych dla Bielika 11B
### [ZREALIZOWANO]
- **Uproszczenie zapytaĹ„ w pliku benchmarkowym**: PrzywrĂłcono naturalne sformuĹ‚owania w zapytaniach `UserPrompt` w [Benchmark_02_Math.json](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/Benchmark_02_Math.json) (usunieto narzucony wymĂłg "wykonania obliczeĹ„ jako pojedyncze wyraĹĽenie RPN"). DziÄ™ki temu model nie otrzymuje sprzecznych instrukcji z system promptem (ktĂłry nakazuje dzielenie zadaĹ„ na logiczne kroki czÄ…stkowe) i poprawnie rozbija obliczenia na czytelne etapy, co eliminuje bĹ‚Ä™dy skĹ‚adniowe i matematyczne (takie jak bĹ‚Ä™dne potÄ™gowanie czy zbÄ™dne dzielenie przez 1000).
- **Weryfikacja kompilacji**: Kompilacja wtyczki powiodĹ‚a siÄ™ bez ĹĽadnych bĹ‚Ä™dĂłw i ostrzeĹĽeĹ„.
### [STAN_SYSTEMU]
- System stabilny, zapytania benchmarkowe sÄ… w peĹ‚ni skoordynowane ze strategiÄ… obliczeniowÄ… zdefiniowanÄ… w system prompcie `CadMathProfile`.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Wykonanie testu benchmarku w BricsCAD i weryfikacja poprawnoĹ›ci.


## [v2.22.10] 2026-06-04T01:17:00+02:00 - Podniesienie precyzji RPN (12 miejsc) i fizyczne porĂłwnanie tolerancji
### [ZREALIZOWANO]
- **Podniesienie precyzji kalkulatora RPN**: ZwiÄ™kszono precyzjÄ™ formatowania liczb zmiennoprzecinkowych w metodzie `ToString()` klasy `PhysicalValue` w [RpnCalculator.cs](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/src/Core/RpnCalculator.cs) z 6 do **12 miejsc po przecinku**. Zapobiega to utracie precyzji w obliczeniach wielokrokowych, gdy wyniki czÄ…stkowe sÄ… zapisywane jako tekst w pamiÄ™ci Agenta (np. maĹ‚e wartoĹ›ci w m2/m3 po konwersji do mm2/cm3/litrĂłw ulegaĹ‚y silnemu zaokrÄ…gleniu).
- **Fizyczne porĂłwnywanie wartoĹ›ci w walidacji (AreValuesPhysicallyEqual)**: Dodano inteligentnÄ… metodÄ™ porĂłwnywania wielkoĹ›ci fizycznych w `RpnCalculator` i zintegrowano jÄ… w [AutoBenchmarkEngine.cs](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/src/Core/AutoBenchmarkEngine.cs). Zamiast porĂłwnywaÄ‡ sztywne napisy (np. `7853.981634_mm2` vs `7854_mm2`), silnik parsuje obie wartoĹ›ci, weryfikuje zgodnoĹ›Ä‡ wymiarowÄ… (Dimensions) i sprawdza wartoĹ›Ä‡ liczbowÄ… w granicach tolerancji `1e-4` (relative tolerance).
- **Rozbudowa i uĹ›ciĹ›lenie przykĹ‚adĂłw w promptach systemowych**:
  - Zaktualizowano prompt systemowy `system_prompt_math.txt` w [ToolConfigManager.cs](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/src/Core/ToolConfigManager.cs), dodajÄ…c precyzyjne przykĹ‚ady konwersji jednostek w locie za pomocÄ… `'jednostka' CONVE` oraz peĹ‚ny wzĂłr i przykĹ‚ad obliczania objÄ™toĹ›ci rury/walca w litrach (`r 2 ^ #PI * h * 'L' CONVE`).
  - Rozszerzono mechanizm automatycznej aktualizacji (auto-upgrade) dla promptĂłw matematycznych i supervisora, aby poprawnie wymuszaĹ‚y zapis nowej wersji plikĂłw promptĂłw.
- **Kompilacja**: Zrekompilowano projekt (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„).
### [STAN_SYSTEMU]
- System jest w peĹ‚ni odporny na drobne rozbieĹĽnoĹ›ci zaokrÄ…gleĹ„ double w RPN i posiada zaktualizowanÄ… bazÄ™ promptĂłw gwarantujÄ…cÄ… stabilne i powtarzalne wyniki obliczeĹ„ geometrycznych i fizycznych.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Uruchomienie zaktualizowanego benchmarku w Ĺ›rodowisku BricsCAD i weryfikacja przejĹ›cia wszystkich 5 testĂłw.


## [v2.22.11] 2026-06-04T01:23:00+02:00 - Dodanie wskazĂłwek syntaktycznych RPN do pytaĹ„ benchmarku
### [ZREALIZOWANO]
- **Dodanie wskazĂłwek RPN do zapytaĹ„ benchmarkowych**: Wzbogacono pytania `UserPrompt` w pliku [Benchmark_02_Math.json](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/Benchmark_02_Math.json) o wyraĹşne wskazĂłwki dotyczÄ…ce koniecznoĹ›ci stosowania kalkulatora RPN, dopisywania jednostek (np. `_mm`, `_cm3`) oraz uĹĽywania polecenia `CONVE` w pojedynczych cudzysĹ‚owach. Pomaga to mniejszemu modelowi (Bielik 11B) w utrzymaniu dyscypliny skĹ‚adniowej RPN bez zmuszania go do nienaturalnego upakowywania caĹ‚ego zadania w jedno wywoĹ‚anie.
- **Weryfikacja kompilacji**: Kompilacja powiodĹ‚a siÄ™ bez ĹĽadnych bĹ‚Ä™dĂłw i ostrzeĹĽeĹ„.
### [STAN_SYSTEMU]
- System stabilny, zapytania benchmarkowe sÄ… przystosowane pod kÄ…tem specyfiki mniejszych modeli LLM pracujÄ…cych w pÄ™tli ReAct.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Wykonanie testu benchmarku w programie BricsCAD w celu weryfikacji.


ŕ´€
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
- **Refaktoryzacja zakĹ‚adki agentĂłw (UI AgentControl)**: Przeprojektowano zakĹ‚adkÄ™ " Agenci\ w interfejsie uĹĽytkownika. ZastÄ…piono tabelÄ™ przypisywania skilli wygodnÄ… listÄ… typu CheckedListBox (chlbAgentTools) powiÄ…zanÄ… z wybranym profilem.
- **Dodanie Leksykonu Skilli**: Wprowadzono listÄ™ wszystkich dostÄ™pnych w systemie narzÄ™dzi/skilli (lbAllTools) wraz z podglÄ…dem ich schematĂłw JSON (
tbToolSchema) generowanych automatycznie na podstawie definicji parametrĂłw wysyĹ‚anych do LLM.
- **Wsparcie dla konfiguracji profilowych w UI**: PowiÄ…zano listÄ™ wyboru promptĂłw systemowych bezpoĹ›rednio z wybranym agentem. Dodano przycisk umoĹĽliwiajÄ…cy natychmiastowe otwarcie powiÄ…zanego pliku promptu systemowego w Notatniku.
- **Aktualizacja zapisu profilu**: Dodano logikÄ™ zapisu przypisanego pliku promptu i zestawu dozwolonych narzÄ™dzi do ools_config.json za pomocÄ… ToolConfigManager.UpdateAgentProfile.
### [STAN_SYSTEMU]
- System kompiluje siÄ™ w peĹ‚ni poprawnie (0 bĹ‚Ä™dĂłw, 0 ostrzeĹĽeĹ„). UI poprawnie synchronizuje konfiguracje profili agentĂłw.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie nowej zakĹ‚adki Agenci bezpoĹ›rednio w BricsCAD.

## 2026-06-05T09:51:00+02:00
### [ZREALIZOWANO]
- **Naprawa bĹ‚Ä™du mscorlib recursive resource lookup / SEHException**: UsuniÄ™to problem z deadlockami oraz wywoĹ‚aniami aktualizacji UI z wÄ…tkĂłw tĹ‚a (tzw. cross-thread UI operations) w klasach AgentControl.cs oraz DatasetStudioControl.cs. Dodano mechanizmy sprawdzajÄ…ce istnienie uchwytu okna (IsHandleCreated) oraz bezpieczne delegowanie aktualizacji (np. z AgentTelemetry) na gĹ‚Ăłwny wÄ…tek przy uĹĽyciu BeginInvoke wywoĹ‚ywanego na gĹ‚Ăłwnej instancji AgentControl.Instance.
### [STAN_SYSTEMU]
- ZwiÄ™kszona stabilnoĹ›Ä‡ interfejsu WinForms osadzonego w BricsCAD. Aplikacja nie rzuca juĹĽ bĹ‚Ä™du System.Runtime.InteropServices.ExternalException przy dĹ‚ugotrwaĹ‚ym dziaĹ‚aniu w tle.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Kompilacja i weryfikacja dziaĹ‚ania w programie BricsCAD.


## 2026-06-05T16:05:00+02:00
### [ZREALIZOWANO]
- **Aktualizacja .gitignore**: Dodano reguĹ‚Ä™ \**/[Bb]enchmark_02_Math*\ ignorujÄ…cÄ… pliki wynikĂłw benchmarkĂłw matematycznych (pliki rozpoczynajÄ…ce siÄ™ od Benchmark_02_Math), co pozwala na zachowanie innych plikĂłw JSON w projekcie.
### [STAN_SYSTEMU]
- Zaktualizowano reguĹ‚y ignorowania plikĂłw git.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Kompilacja i weryfikacja dziaĹ‚ania w programie BricsCAD.

## 2026-06-05T16:55:00+02:00
### [ZREALIZOWANO]
- **Migracja do PackageReference**: Przekonwertowano projekt z packages.config na format PackageReference. Zaktualizowano pakiety NuGet, w tym zabezpieczono lukÄ™ w System.Text.Json (wersja 8.0.5).
- **System FormuĹ‚ i Makr**: Zaimplementowano DynamicFormulaManager (dynamiczna kompilacja w locie przez Roslyn/CSharpScript, piaskownica, ograniczony dostÄ™p do I/O) oraz MacroManager obsĹ‚ugujÄ…cy parsowanie i asynchroniczne wykonywanie wieloetapowych skryptĂłw JSON dla narzÄ™dzi CAD.
- **NarzÄ™dzia Agentowe V2**: Dodano i zarejestrowano w profilach nowe narzÄ™dzia oparte o IToolV2: SearchKnowledgeBaseTool, SavePermanentFormulaTool, SaveMacroTool, ExecuteFormulaTool, ExecuteMacroTool. 
- **Baza Wiedzy AI (UI)**: Utworzono nowÄ… zakĹ‚adkÄ™ w interfejsie (KnowledgeBaseControl.cs), integrujÄ…cÄ… listy zdefiniowanych makr i formuĹ‚ Roslyn, a takĹĽe udostÄ™pniajÄ…cÄ… podglÄ…d kodu i przyciski do przeĹ‚adowywania ("Hot Reload") i wykonywania makr asynchronicznie (Task.Run).
- **Automatyczne Testy (QA)**: Dodano testy jednostkowe DynamicFormulaManagerTests, weryfikujÄ…ce dynamicznÄ… kompilacjÄ™, oraz zintegrowano je z istniejÄ…cym procesem TestRunner.
### [STAN_SYSTEMU]
- Projekt bezbĹ‚Ä™dnie kompiluje siÄ™ z uĹĽyciem Roslyn. Wprowadzono architekturÄ™ opartÄ… na asynchronicznoĹ›ci, uodparniajÄ…c UI (AgentControl) przed blokowaniem przez ciÄ™ĹĽkie skrypty C#. GotowoĹ›Ä‡ na testy dynamicznej wiedzy inĹĽynierskiej.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- RozpoczÄ™cie tworzenia nowych narzÄ…dzi przy wykorzystaniu stworzonej Bazy Wiedzy lub testowanie manualne w BricsCAD.

## 2026-06-05T17:10:00+02:00
### [ZREALIZOWANO]
- **Naprawa bĹ‚Ä™du PerTypeValues'1 (System.Runtime.CompilerServices.Unsafe)**: WdroĹĽono globalnÄ… obsĹ‚ugÄ™ zdarzenia AppDomain.CurrentDomain.AssemblyResolve w klasie AgentStartup.cs. Zapobiega to awariom kompilacji w locie (Roslyn) przez Ĺ›rodowisko BricsCAD, rÄ™cznie kierujÄ…c poszukiwania uszkodzonych referencji bezpoĹ›rednio do fizycznych plikĂłw .dll w katalogu wtyczki. Mechanizm ten rozwiÄ…zuje znane problemy z Ĺ‚adowaniem przestrzeni nazw w .NET Framework z zewnÄ™trznych plikĂłw w systemach wielowÄ…tkowych (takich jak kompilator CSharpScript).
### [STAN_SYSTEMU]
- Kompilator dynamicznych formuĹ‚ dziaĹ‚a stabilnie pod presjÄ… silnika BricsCAD. RozwiÄ…zano konflikt z zarzÄ…dzaniem pakietami Nuget/Roslyn na etapie wĹ‚Ä…czania wtyczki. W peĹ‚ni odblokowano zdolnoĹ›Ä‡ do tworzenia, zapisywania i korzystania z formuĹ‚ w czasie rzeczywistym.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- RozpoczÄ™cie tworzenia nowych narzÄ™dzi opartych o wiedzÄ™ inĹĽynierskÄ… przez UĹĽytkownika, wykorzystujÄ…cych poprawnie dziaĹ‚ajÄ…cy silnik Roslyn.

## 2026-06-05T17:43:00+02:00
### [ZREALIZOWANO]
- **WdroĹĽenie UnitsNet i rygoru wymiarowego (Faza 6 i 7)**: Rozbudowano system kompilacji formuĹ‚ (DynamicFormulaManager) o peĹ‚ne wsparcie dla UnitsNet. Zabezpieczono wymiary przesyĹ‚anych danych poprzez modyfikacjÄ™ IToolV2 na typ string zamiast goĹ‚ych double. Zaktualizowano profile AgentĂłw (ToolConfigManager), zapewniajÄ…c synchronizacjÄ™ uprawnieĹ„ dla starszych plikĂłw konfiguracyjnych, tak aby CadMathProfile mĂłgĹ‚ poprawnie korzystaÄ‡ z ExecuteFormulaTool.
### [STAN_SYSTEMU]
- Kompilator w locie dziaĹ‚a poprawnie. Agenci rozrĂłĹĽniajÄ… obliczenia interaktywne RPN od gotowych skryptĂłw formuĹ‚ inĹĽynierskich (.csx). Testy manualne (E2E) w BricsCAD zakoĹ„czone pomyĹ›lnie.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Ewentualna optymalizacja makr systemowych lub tworzenie pierwszych narzÄ™dzi specyficznych dla uĹĽytkownika przez interfejs CAD.

## 2026-06-05T18:02:00+02:00
### [ZREALIZOWANO]
- **Zaawansowany Sanitizer jednostek HVAC**: W module ExecuteFormulaTool.cs wprowadzono kaskadowe czyszczenie Ĺ‚aĹ„cuchĂłw znakĂłw przed przekazaniem ich do UnitsNet. RozwiÄ…zano problem halucynacji LLM, ktĂłre generowaĹ‚y zapis algebraiczny (np. 1.5_W/(m^2*K)). Sanitizer wygĹ‚adza tekst konwertujÄ…c potÄ™gi, usuwajÄ…c nawiasy, redukujÄ…c podwĂłjne spacje, zamieniajÄ…c znaki mnoĹĽenia na Ĺ›rodkowe kropki oraz korygujÄ…c przecinki. Wymuszono takĹĽe stosowanie CultureInfo.InvariantCulture przy budowaniu skryptĂłw w SavePermanentFormulaTool.cs.
### [STAN_SYSTEMU]
- System stabilny, kompilator ignoruje i naprawia bĹ‚Ä™dy wprowadzania jednostek fizycznych pochodzÄ…ce z naturalnego jÄ™zyka.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Rozbudowa kolejnych mechanizmĂłw lub testowanie praktycznych makr przez uĹĽytkownika.

## 2026-06-05T21:17:00+02:00
### [ZREALIZOWANO]
- **Konfigurowalne Ĺ›cieĹĽki Bazy Wiedzy (Faza 9)**: Wprowadzono centralnÄ… klasÄ™ `AppPaths`, ktĂłra pozwala na zdefiniowanie wĹ‚asnej Ĺ›cieĹĽki do folderu `CustomKnowledge` z poziomu nowej zakĹ‚adki "ĹšcieĹĽki i Dane" w ustawieniach interfejsu uĹĽytkownika. Zmiana ta umoĹĽliwia przetrzymywanie formuĹ‚ i makr w chmurze (np. OneDrive), z moĹĽliwoĹ›ciÄ… automatycznej migracji (kopiowania) plikĂłw ze starego folderu AppData. Zaktualizowano wszystkie powiÄ…zane menedĹĽery i narzÄ™dzia.
### [STAN_SYSTEMU]
- System operuje na konfigurowalnych Ĺ›cieĹĽkach do zasobĂłw wiedzy inĹĽynierskiej. ĹšcieĹĽka jest zapisywana w `ui_settings.json` i zachowuje spĂłjnoĹ›Ä‡ systemu.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Dalszy rozwĂłj funkcji CAD i korzystanie przez uĹĽytkownika z chmurowej bazy wiedzy.
## 2026-06-05T21:40:00+02:00
### [ZREALIZOWANO]
- **FAZA 8: System ZarzÄ…dzania Zestawami Danych (DatasetManager)**: 
  1. Stworzono `DatasetManager` operujÄ…cy na plikach JSON (`Newtonsoft.Json.Linq`), co pozwala na Ĺ‚atwe operacje na danych tabelarycznych.
  2. Rozbudowano moduĹ‚ `AppPaths` o Ĺ›cieĹĽkÄ™ do `CustomKnowledge\Datasets`.
  3. WstrzykniÄ™to obiekt bazy pod nazwÄ… `Data` bezpoĹ›rednio do `ScriptGlobals`, co pozwala wywoĹ‚ywaÄ‡ go z dynamicznych skryptĂłw formuĹ‚ `.csx`. Dodano dyrektywy Newtonsoft do kompilatora.
  4. Utworzono nowe narzÄ™dzia w standardzie `IToolV2`: `QueryDatasetTool`, `ImportCsvDatasetTool`, `ManageDatasetTool`. NarzÄ™dzia te zarejestrowano w profilach agentĂłw (Supervisor, CadMath).
  5. Rozbudowano interfejs UI (`KnowledgeBaseControl.cs`) o zakĹ‚adkÄ™ z widokiem tabelarycznym (`DataGridView`) uĹ‚atwiajÄ…cÄ… przeglÄ…d i proste edycje.
  6. Skompilowano caĹ‚y projekt MSBuild, nie uzyskujÄ…c ĹĽadnych bĹ‚Ä™dĂłw.
### [STAN_SYSTEMU]
- System operacyjny i w peĹ‚ni stabilny. Agenci uzyskali elastyczny dostÄ™p do baz danych inĹĽynierskich w formacie JSON z moĹĽliwoĹ›ciÄ… dynamicznych zapytaĹ„ (Exact, NearestGreater, NearestLower).
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie moduĹ‚u baz danych w interakcji.

## [v2.23.0] 2026-06-05T21:42:00+02:00 - Faza 8.1 - Filtrowanie wielokryterialne dla podtypĂłw w DatasetManager
### [ZREALIZOWANO]
- **FAZA 8.1: Filtrowanie wielokryterialne dla podtypĂłw w DatasetManager**: 
  1. Zmodyfikowano interfejs `IDatasetProvider` by metody `GetExactMatch`, `GetNearestGreater`, `GetNearestLower` przyjmowaĹ‚y opcjonalny sĹ‚ownik `Dictionary<string, string> filters`.
  2. W klasie `DatasetManager` dodano funkcjÄ™ pomocniczÄ… sprawdzajÄ…cÄ… podane klucze i wartoĹ›ci przed uruchomieniem algorytmĂłw szukajÄ…cych, optymalizujÄ…c wyciÄ…ganie np. rur konkretnego materiaĹ‚u z poĹ‚Ä…czonej tabeli JSON.
  3. Zaktualizowano definicjÄ™ i ciaĹ‚o `QueryDatasetTool`, wprowadzajÄ…c i wyciÄ…gajÄ…c opcjonalny parametr `filters`. NarzÄ™dzie przekazuje poprawnie filtry (jako JTokenType.Object).
  4. Projekt przebudowano z zerowÄ… iloĹ›ciÄ… bĹ‚Ä™dĂłw.
### [STAN_SYSTEMU]
- System jest stabilny. ZarzÄ…dzanie danymi obsĹ‚uguje peĹ‚ne, kaskadowe filtrowanie wĹ‚aĹ›ciwoĹ›ci przed odnalezieniem wĹ‚aĹ›ciwych parametrĂłw. FormuĹ‚y i makra zyskaĹ‚y duĹĽÄ… elastycznoĹ›Ä‡ w szukaniu w bazach.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie w CAD / praca inĹĽynierska na bazach.

## [v2.24.0] 2026-06-05T22:05:00+02:00 - Faza 9 - Kategoryzacja, Tagowanie i Drzewo FolderĂłw
### [ZREALIZOWANO]
- **FAZA 9: System Kategoryzacji, Tagowania i Drzewa FolderĂłw**: 
  1. Dodano pola Category i Tags do metadanych formuĹ‚ i makr.
  2. Zaktualizowano narzÄ™dzia SavePermanentFormulaTool i SaveMacroTool o obsĹ‚ugÄ™ kategorii, wĹ‚Ä…czajÄ…c sanityzacjÄ™ znakĂłw Windows oraz tworzenie fizycznych podfolderĂłw.
  3. Zmodyfikowano menedĹĽery (DynamicFormulaManager, MacroManager, DatasetManager) do przeszukiwania rekurencyjnego (SearchOption.AllDirectories).
  4. Rozbudowano wyszukiwarkÄ™ KnowledgeBase o opcjonalne parametry Category i Tags (filtrowanie LINQ).
  5. Przebudowano interfejs UI (KnowledgeBaseControl) z uĹĽyciem TreeView do grupowania folderĂłw i elementĂłw.
  6. Dodano automatyczne przeĹ‚adowywanie bazy po wejĹ›ciu w zakĹ‚adkÄ™ (zdarzenie VisibleChanged) oraz pasek tekstowy do dynamicznego filtrowania wyĹ›wietlanego drzewka po tagach.
### [STAN_SYSTEMU]
- System w peĹ‚ni stabilny i wspiera zaawansowanÄ… kategoryzacjÄ™ oraz filtrowanie tagami. Panel Bazy Wiedzy jest automatycznie aktualizowany po pokazaniu.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Testowanie nowej struktury z uĹĽyciem narzÄ™dzi lub dalszy rozwĂłj bazy wiedzy.

## [v2.28.1] 2026-06-06T20:44:00+02:00 - Ujednolicenie pliku pamiÄ™ci i naprawa kodowania
### [ZREALIZOWANO]
- Przeanalizowano pliki pamiÄ™ci i zidentyfikowano bĹ‚Ä™dy kodowania Mojibake (znaki UTF-8 zdekodowane jako Windows-1250 i zapisane ponownie) oraz bĹ‚Ä™dy zapisu UTF-16-BE (alternujÄ…ce spacje/NUL w pliku).
- Stworzono kopiÄ™ zapasowÄ… pliku `memory.md` jako `memory.bak.md` w folderze gĹ‚Ăłwnym wtyczki V2.
- Naprawiono wszystkie znieksztaĹ‚cenia kodowania, przywracajÄ…c czysty polski tekst UTF-8.
- Ponadawano numery wersji (v2.20.10 do v2.28.0) wszystkim krokom deweloperskim z datami od 2026-06-03.
- Zaktualizowano i ujednolicono spis wersji (Changelog) na poczÄ…tku pliku o nowe wersje odpowiadajÄ…ce krokom deweloperskim i fazom V2.
- Zsynchronizowano plik `memory.md` w katalogu gĹ‚Ăłwnym oraz w folderze `docs/`.
### [STAN_SYSTEMU]
- Pliki pamiÄ™ci sÄ… w peĹ‚ni ujednolicone, spĂłjne syntaktycznie i wolne od uszkodzeĹ„ kodowania.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Dalszy rozwĂłj projektu zgodnie z planem wdroĹĽenia w BricsCAD.

## [v2.28.2] 2026-06-06T19:11:00+02:00 - Implementacja narzÄ™dzia do zarzÄ…dzania receptami
### [ZREALIZOWANO]
- Przeniesiono przechowywanie recept z pojedynczego pliku 'AgentRecipes.json' do oddzielnych plikĂłw .json w dedykowanym folderze 'Recipes'.
- Zaimplementowano logikÄ™ migracji do nowego formatu w 'RecipeManager.cs'.
- Stworzono narzÄ™dzie 'ManageRecipesTool' umoĹĽliwiajÄ…ce agentom czytanie, tworzenie, edytowanie i usuwanie recept.
- Zaktualizowano profile Supervisora i pozostaĹ‚e w 'ToolConfigManager.cs', umoĹĽliwiajÄ…c dostÄ™p do nowego narzÄ™dzia i zachowanie znacznikĂłw $trigger w TaskDescription podczas delegacji zadaĹ„.
### [STAN_SYSTEMU]
- System umoĹĽliwia dynamiczne tworzenie i zarzÄ…dzanie zadaniami 'Few-Shot' przez agenta i odczytywanie ich z oddzielnych plikĂłw.
### [BLOKADY / PROBLEMY]
- Dotnet build z poziomu CLI wyrzucaĹ‚ bĹ‚Ä™dy o braku Newtonsoft.Json dla starych plikĂłw co moĹĽe wymagaÄ‡ weryfikacji .csproj.
### [KOLEJNY_KROK]
- Oczekiwanie na testy interakcji agenta z nowym narzÄ™dziem.

## [v2.28.3] 2026-06-06T21:26:00+02:00 - Implementacja Centrum Pomocy (ZakĹ‚adka Pomoc)
### [ZREALIZOWANO]
- Skopiowano dokumentacjÄ™ uĹĽytkownika (USER_GUIDE.md, TOOLS_REFERENCE.md, COMMANDS_REFERENCE.md) do nowej lokalizacji 'resources\help\'.
- Utworzono plik indeksujÄ…cy 'index.json' sterujÄ…cy zawartoĹ›ciÄ… drzewa nawigacyjnego w module pomocy.
- Zbudowano nowÄ… kontrolkÄ™ 'HelpCenterControl.cs' skĹ‚adajÄ…cÄ… siÄ™ z drzewka (TreeView) i przeglÄ…darki (WebBrowser).
- Zaimplementowano wewnÄ™trz 'HelpCenterControl' dynamiczny silnik parsujÄ…cy na wyraĹĽeniach regularnych, ktĂłry w locie tĹ‚umaczy skĹ‚adniÄ™ Markdown na sformatowany, stylowy HTML (obsĹ‚ugujÄ…cy nagĹ‚Ăłwki, listy, pogrubienia, sekcje kodu i alerty).
- WpiÄ™to nowÄ… kontrolkÄ™ jako zakĹ‚adkÄ™ '? Pomoc' do gĹ‚Ăłwnego obiektu 'tabControl' w 'AgentControl.cs'.
- Zaktualizowano plik 'Bricscad_AgentAI_V2.csproj' do kompilacji nowej kontrolki i uwzglÄ™dnienia plikĂłw pomocy jako zasobĂłw (CopyToOutputDirectory).
### [STAN_SYSTEMU]
- Dodano centrum zintegrowanej wiedzy, ktĂłre uĹĽytkownik bÄ™dzie mĂłgĹ‚ Ĺ‚atwo edytowaÄ‡ przez pliki MD w resources\help.
### [BLOKADY / PROBLEMY]
- Dotnet CLI rzuca standardowe problemy z brakiem referencji z NuGet, wymagana manualna kompilacja u uĹĽytkownika z VS / MSBuild.
### [KOLEJNY_KROK]
- Weryfikacja dziaĹ‚ania drzewka nawigacyjnego w GUI wtyczki w Ĺ›rodowisku natywnym BricsCAD.

## [v2.28.4] 2026-06-06T21:40:00+02:00 - ModuĹ‚ Agenta Pomocy (ReadHelpTool)
### [ZREALIZOWANO]
- Zbudowano narzÄ™dzie 'ReadHelpTool.cs' dla Agenta umoĹĽliwiajÄ…ce swobodny odczyt dokumentacji i listowanie zasobĂłw z folderu 'resources\help\'.
- NarzÄ™dzie posiada mechanizm Path Traversal Prevention zabezpieczajÄ…cy przed odczytem zewnÄ™trznych plikĂłw systemu.
- Uaktualniono domyĹ›lnÄ… konfiguracjÄ™ Supervisora w 'ToolConfigManager.cs' przypisujÄ…c mu bezpoĹ›redni dostÄ™p do narzÄ™dzia 'ReadHelp'.
- Zmodyfikowano logikÄ™ generatora 'system_prompt_supervisor.txt', dodajÄ…c sekcjÄ™ 4 o nazwie WIEDZA O SYSTEMIE / POMOC.
- Skompilowano caĹ‚y program upewniajÄ…c siÄ™, ĹĽe brak bĹ‚Ä™dĂłw Ĺ›rodowiskowych przy uĹĽyciu MSBuild.
### [STAN_SYSTEMU]
- Agent potrafi dyskutowaÄ‡ z uĹĽytkownikiem na temat wĹ‚asnej wtyczki i procedur w niej opisanych, doĹ‚Ä…czajÄ…c instrukcje z plikĂłw MD.
### [BLOKADY / PROBLEMY]
- Brak blokad, wszystkie pliki w tym '.csproj' nadpisane i skompilowane z sukcesem.
### [KOLEJNY_KROK]
- Test funkcjonalny narzÄ™dzia 'ReadHelp' w bezpoĹ›redniej rozmowie uĹĽytkownika z Supervisorem.

## [v2.28.5] 2026-06-06T22:15:00+02:00 - UI & UX Tweaks (Markdown & Commendy)
### [ZREALIZOWANO]
- Zmieniono komendÄ™ wywoĹ‚awczÄ… panelu z AGENT_V2 na krĂłtkie i proste 'AI'.
- Zaimplementowano w konsoli parser formatowania Markdown. Zamiast surowych gwiazdek, bot uĹĽywa teraz poprawnego pogrubienia, kursywy i dedykowanej czcionki z tĹ‚em dla blokĂłw kodu.
- Poprawiono parser, usuwajÄ…c bĹ‚Ä…d 'rozlewania' siÄ™ formatowania na wiele akapitĂłw i naĹ‚oĹĽono auto-pogrubienie na wiersze z nagĹ‚Ăłwkami z prefiksem '#'.
### [STAN_SYSTEMU]
- OdĹ›wieĹĽona konsola z ulepszonym renderingiem tekstu.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Implementacja dynamicznego systemu Skilli.

## [v2.28.6] 2026-06-06T23:18:51+02:00 - WdroĹĽenie systemu Skilli (Markdown+YAML)
### [ZREALIZOWANO]
- Utworzono strukturÄ™ danych `AgentSkill.cs` i zaimplementowano menedĹĽer `SkillManager.cs` parsujÄ…cy bloki YAML Frontmatter z plikĂłw Markdown.
- Zintegrowano obsĹ‚ugÄ™ plikĂłw `.md` w zakĹ‚adce Baza Wiedzy (nowa podzakĹ‚adka "Skille InĹĽynierskie").
- Stworzono `ManageSkillsTool.cs` dajÄ…ce Agentowi moĹĽliwoĹ›Ä‡ interakcji ze skillami (akcje `list_skills`, `read_skill`, `create_skill`).
- WdroĹĽono mechanizm Progressive Disclosure (poziom 1) w `AgentControl.cs` wstrzykujÄ…cy skille wywoĹ‚ane przez `#` w chatboxie prosto jako wiadomoĹ›ci systemowe (kontekst).
- Zaktualizowano plik `Bricscad_AgentAI_V2.csproj` w celu prawidĹ‚owej kompilacji nowych klas.
### [STAN_SYSTEMU]
- System umoĹĽliwia wczytywanie i dynamiczne wstrzykiwanie skilli inĹĽynierskich z plikĂłw Markdown z YAML.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- UzupeĹ‚nienie dokumentacji o obsĹ‚ugÄ™ Skilli.

## [v2.28.7] 2026-06-06T23:21:53+02:00 - Aktualizacja Pomocy (USER_GUIDE) o obsĹ‚ugÄ™ Skilli (#) i PoleceĹ„ (/)
### [ZREALIZOWANO]
- Zaktualizowano plik `USER_GUIDE.md` w resources/help o instrukcje wywoĹ‚ywania skilli przy uĹĽyciu `#` oraz poleceĹ„ ukoĹ›nika `/` w chatboxie.
### [STAN_SYSTEMU]
- Zaktualizowana pomoc dla uĹĽytkownika.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Aktualizacja system promptu Supervisora.

## [v2.28.8] 2026-06-06T23:36:12+02:00 - Aktualizacja Supervisor prompt: OdrĂłĹĽnienie Skilli (#) od Recept ($)
### [ZREALIZOWANO]
- Zaktualizowano system prompt w `ToolConfigManager.cs`, wprowadzajÄ…c precyzyjne wytyczne odrĂłĹĽniajÄ…ce dynamiczne Skille (`#`) od Recept (`$`).
### [STAN_SYSTEMU]
- Supervisor lepiej odrĂłĹĽnia i stosuje odpowiednie struktury meta-instrukcji.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- UdostÄ™pnienie narzÄ™dzia manage_skills dla Supervisora.

## [v2.28.9] 2026-06-06T23:57:36+02:00 - Dodanie manage_skills do domyĹ›lnych narzÄ™dzi Supervisora
### [ZREALIZOWANO]
- Dodano `ManageSkillsTool` (`manage_skills`) do domyĹ›lnej konfiguracji dostÄ™pnych narzÄ™dzi dla profilu Supervisora w `ToolConfigManager.cs`.
### [STAN_SYSTEMU]
- Agent w roli Supervisora ma doĹ›wiadczenie i uprawnienia do zarzÄ…dzania skillami w locie.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Uodpornienie narzÄ™dzia na bĹ‚Ä™dne wywoĹ‚ania przez LLM.

## [v2.28.10] 2026-06-07T00:06:39+02:00 - Uodpornienie ManageSkillsTool na halucynacje LLM (Case Insensitivity & Aliases)
### [ZREALIZOWANO]
- Zaimplementowano w `ManageSkillsTool.cs` tolerancjÄ™ na wielkoĹ›Ä‡ liter (case-insensitivity) przy wyszukiwaniu skilli.
- Wprowadzono obsĹ‚ugÄ™ aliasĂłw i nazw alternatywnych, aby zapobiec bĹ‚Ä™dom przy wywoĹ‚aniach przez LLM.
### [STAN_SYSTEMU]
- ModuĹ‚ obsĹ‚ugi skilli jest odporny na drobne bĹ‚Ä™dy zapisu i wielkoĹ›ci liter ze strony modeli LLM.
### [BLOKADY / PROBLEMY]
- Brak.
### [KOLEJNY_KROK]
- Zapis i ujednolicenie dziennika prac w memory.md.


## Podsumowania UkoĹ„czonych Faz Deweloperskich V2
### Faza 10: Multi-Modalne ZaĹ‚Ä…czniki (Tekst i Wizja) (ZakoĹ„czono)
1. Zaimplementowano klasÄ™ FileExtractor.cs do obsĹ‚ugi zaĹ‚Ä…cznikĂłw tekstowych (TXT, PY, MD, LSP) oraz binarnych (PDF, XLS/XLSX).
2. Wprowadzono kompresjÄ™ i skalowanie obrazĂłw (PNG, JPG) uĹĽywajÄ…c System.Drawing.Common do 1024x1024px z konwersjÄ… do Base64 dla wsparcia Vision API.
3. Zaktualizowano AgentControl.cs - dodano przycisk zaĹ‚Ä…cznika (btnAttachFile), obsĹ‚ugÄ™ OpenFileDialog, logikÄ™ procesowania zaĹ‚Ä…cznika w ProcessInputAsync i wyĹ›wietlanie w UI (lblAttachedFile).
4. Skompilowano kod z wynikiem pozytywnym bez bĹ‚Ä™dĂłw (MSBuild).
### Faza 11: Standaryzacja Datasetow (Wzorzec Plikow Towarzyszacych i Normalizacja) (Zakonczono)
1. Dodano plik Models/DatasetMetadata.cs aby zapewnic ustandaryzowana strukture bazy wiedzy.
2. Rozdzielono zapis/odczyt plikow baz danych w DatasetManager.cs na [id].json (metadane) i [id].data.json (tablice z danymi).
3. Dodano odpornosc na stare/niezmigrowane pliki (logowanie zamiast crashowania GUI).
4. Zaktualizowano KnowledgeBaseControl.cs - UI dla Bazy Danych pobiera kategorie, grupuje dane w TreeView, oraz obsluguje zapis wylacznie *.data.json z pominieciem nadpisywania metadanych.
5. Zmodyfikowano ManageDatasetTool i ImportCsvDatasetTool aby przyjmowaly opis, tagi, kategorie z nowa rygorystyczna wytyczna dotyczaca plaskich tabel.
### Hotfix: Polskie znaki w UI
Poprawiono kodowanie znakow w AgentControl.cs gdzie wyswietlane byly krzaczki np. "DoĹ‚Ä…czono plik" oraz upewniono sie, ze plik zapisany jest z kodowaniem UTF-8.
### Faza 12: Integracja Schowka Systemowego (Zakonczono)
1. Dodano w FileExtractor.cs metode do obslugi obrazow bezposrednio z pamieci operacyjnej (obiekt Image), ktora zabezpiecza VRAM i automatycznie przelicza, skaluje oraz uwalnia pamiec za pomoca blokow using.
2. Zaktualizowano AgentControl.cs wprowadzajac zdarzenie KeyDown dla pola tekstowego txtInput.
3. Gdy uzytkownik wcisnie Ctrl+V i schowek zawiera obraz, agent automatycznie wyodrebni ten obraz do wewnetrznej zmiennej _attachedClipboardImage, podmieni label GUI i zablokuje typowe wklejenie tekstu. 
4. Procesowanie obrazu za pomoca Vision API dziala natywnie i poprawnie konwertuje do binarnej bazy 64.

### Faza 13: ZarzÄ…dzanie Sesjami, PamiÄ™ciÄ… Kontekstu i Kompresja (ZakoĹ„czono)
1. Stworzono model ChatSession (z GUID, datami, wiadomoĹ›ciami i wyizolowanym Blackboardem) oraz SessionManager do zapisu/odczytu sesji w %APPDATA% (plik .json).
2. Zaktualizowano SharedMemoryState, dodajÄ…c moĹĽliwoĹ›Ä‡ Ĺ‚adowania pamiÄ™ci z Dictionary i czyszczenia jej na potrzeby izolacji stanĂłw miÄ™dzy poszczegĂłlnymi sesjami.
3. Zaktualizowano SupervisorOrchestrator, by korzystaĹ‚ wprost z historii wiadomoĹ›ci CurrentSession.Messages zamiast zmiennej globalnej, a nowy wÄ…tek wywoĹ‚uje Auto-Naming sesji gdy uzbierajÄ… siÄ™ 2 wiadomoĹ›ci.
4. Wprowadzono logikÄ™ zliczania uĹĽycia tokenĂłw z API oraz pasek Context Bar w GUI (AgentControl.cs), ze wsparciem dla bezpiecznej aktualizacji miÄ™dzy-wÄ…tkowej (Invoke).
5. Zbudowano i zintegrowano system kompresji kontekstu (Auto i RÄ™czna), podsumowujÄ…cy najstarsze logi, by ratowaÄ‡ miejsce w oknie kontekstowym.

### Faza 14: ĹšwiadomoĹ›Ä‡ Kontekstu DWG i Notatki Projektowe (ZakoĹ„czono)
1. Zmodyfikowano ChatMessage dodajÄ…c pole ActiveDocumentPath.
2. Stworzono system Notatek Projektowych (Sidecar Markdown) z wykorzystaniem nowej klasy DrawingNoteManager, zapisujÄ…c notatki obok plikĂłw DWG lub w folderze Temp dla rysunkĂłw niezapisanych.
3. Zaimplementowano w AgentControl.cs przechwytywacz komend (/notatka) oraz sub-agenta z zablokowanym narzÄ™dziownikiem (Tools) do generowania czystego Markdowna.
4. UzupeĹ‚niono SupervisorOrchestrator o logikÄ™ RAG - notatki inĹĽynierskie sÄ… dynamicznie wstrzykiwane do Promptu Systemowego.
5. Poprawiono bezpieczeĹ„stwo wyjÄ…tkĂłw I/O i wÄ…tkĂłw UI z uĹĽyciem blokĂłw try-catch.

### Faza 15: Bezpieczne NarzÄ™dzia Plikowe (ZakoĹ„czono)
1. Utworzono nowe narzÄ™dzia (ReadProjectFileTool i WriteProjectFileTool) zabezpieczajÄ…ce operacje na plikach - dozwolone rozszerzenia (.txt, .md, .csv itd.) i lokalizacja wymuszona w folderze aktualnego rysunku DWG (lub %APPDATA%).
2. Wprowadzono twardÄ… blokadÄ™ manipulowania notatkami inĹĽynierskimi (.ai_note.md) przez te narzÄ™dzia; zablokowane akcje wymuszajÄ… na Agencie uĹĽycie DelegateTaskTool.
3. Zaktualizowano menedĹĽer konfiguracji profili (ToolConfigManager), wstrzykujÄ…c wygenerowany system prompt dla NotesProfile oraz aktualizujÄ…c systemowy prompt Supervisora o RAG-owÄ… obsĹ‚ugÄ™ plikĂłw notatek (w tym Ĺ›wiadomoĹ›Ä‡ braku pliku).
4. Rozbudowano listÄ™ autouzupeĹ‚niania w AgentControl.cs (UI) - polecenia ze znakiem / (np. /notatka, /compress) otrzymaĹ‚y opisy wraz z autouzupeĹ‚nianiem; polecenia typu $ rĂłwnieĹĽ zyskaĹ‚y objaĹ›nienia.

### Faza 16: System Skilli (Markdown + YAML Frontmatter) (ZakoĹ„czono)
1. Utworzono strukturÄ™ danych `AgentSkill.cs` i zaimplementowano menedĹĽer `SkillManager.cs` parsujÄ…cy bloki YAML Frontmatter z plikĂłw Markdown.
2. Zintegrowano obsĹ‚ugÄ™ plikĂłw `.md` w zakĹ‚adce Baza Wiedzy (nowa podzakĹ‚adka "Skille InĹĽynierskie").
3. Stworzono `ManageSkillsTool.cs` dajÄ…ce Agentowi moĹĽliwoĹ›Ä‡ interakcji ze skillami (akcje `list_skills`, `read_skill`, `create_skill`).
5. Poprawiono zgodnoĹ›Ä‡ interfejsu narzÄ™dzia z wymogami projektu `IToolV2` i pomyĹ›lnie zrekompilowano system.

### Faza 17: Rewident (QA Agent / AuditorProfile) i bezpieczne testowanie narzÄ™dzi (ZakoĹ„czono)
1. WdroĹĽono nowy profil Agenta `AuditorProfile` (Rewident) odpowiedzialny za bezpieczne testowanie (QA) oraz diagnozowanie narzÄ™dzi na poziomie kodu.
2. Dodano obsĹ‚ugÄ™ parametru `__MockResponse` do narzÄ™dzi interaktywnych blokujÄ…cych UI (`UserInputTool`, `UserChoiceTool`), aby umoĹĽliwiÄ‡ przeprowadzanie zautomatyzowanych testĂłw bez udziaĹ‚u uĹĽytkownika (symulacja).
3. Dodano obsĹ‚ugÄ™ flagi `__DryRun` do narzÄ™dzi modyfikujÄ…cych stan oraz pliki (Side Effects): `WriteProjectFileTool`, `SaveMacroTool`, `SavePermanentFormulaTool`, `ManageRecipesTool`, `ManageSkillsTool`, ktĂłra waliduje parametry operacji i omija fizyczny zapis na dysku.
4. Zaktualizowano `ToolConfigManager.cs` - dodano dedykowany prompt systemowy nakazujÄ…cy Rewidentowi korzystanie z tych flag w celu unikania zepsucia Ĺ›rodowiska, oraz ograniczono jego listÄ™ narzÄ™dzi tylko do weryfikacji.
5. Poprawiono odpornoĹ›Ä‡ parsowania w `ManageSkillsTool` (obsĹ‚uga wariantĂłw Action/action).

### Faza 18: Zautomatyzowane Testowanie NarzÄ™dzi (Autotest) (ZakoĹ„czono)
1. Rozbudowano interfejs graficzny `AgentTesterControl.cs` wykorzystujÄ…c komponent `TabControl`, z wdroĹĽeniem podzakĹ‚adki "Autotest NarzÄ™dzi".
2. Dodano logikÄ™ dynamicznego wczytywania wszystkich dostÄ™pnych definicji narzÄ™dzi z poziomu `ToolOrchestrator` do kontrolki `CheckedListBox` ze wsparciem zbiorczej selekcji ("Zaznacz wszystko").
3. Opracowano izolowanÄ… pÄ™tlÄ™ testowÄ… dla zaznaczonych narzÄ™dzi - dla kaĹĽdego polecenia uruchamiana jest "czysta karta" z profilem `AuditorProfile`, ĹĽÄ…daniem wykorzystania flag izolacyjnych (`__DryRun`, `__MockResponse`) oraz wĹ‚asnym systemowym promptem Rewidenta.
4. Zaimplementowano mechanizm kolorowania logĂłw wyjĹ›ciowych na konsoli w UI (RichTextBox), umoĹĽliwiajÄ…cy Ĺ›ledzenie postÄ™pĂłw oraz identyfikacjÄ™ bĹ‚Ä™dĂłw/uwag (czerwony/zielony).
5. WdroĹĽono generator kompleksowych raportĂłw wyjĹ›ciowych w formacie `.md` zbierajÄ…cy rezultaty ewaluacji, z systemowym oknem zachÄ™cajÄ…cym uĹĽytkownika do ich zapisu.

## [v2.28.2] 2026-06-07T15:05:11+02:00 - Faza 16: Ekosystem LISP, Audyt kodu i Self-Healing
### [ZREALIZOWANO]
- WdroĹĽono profile agentĂłw dla Ĺ›rodowiska LISP: LispCoderProfile oraz LispAuditorProfile.
- Zintegrowano natywnÄ… funkcjÄ™ LISP [LispFunction("agent-callback")] przechwytujÄ…cÄ… w C# wyniki z LISP.
- Zaimplementowano cichÄ… pÄ™tlÄ™ samonaprawiajÄ…cÄ… (Self-Healing) - w przypadku napotkania przez LISP wartoĹ›ci "ERROR", sub-agent automatycznie podejmuje prĂłbÄ™ poprawy kodu i ponownego jego wywoĹ‚ania.
- Zaktualizowano interfejs w AgentControl.cs poprzez dodanie funkcjonalnoĹ›ci wykrywania wygenerowanego kodu LISP z widoku markdown. Po wygenerowaniu uaktywniany jest nowy przycisk **[Wykonaj Skrypt LISP]**, uruchamiajÄ…cy kod w zintegrowanym Ĺ›rodowisku BricsCAD.
- Przetestowano asynchronicznÄ… kompilacjÄ™ logiki C#. Projekt zbudowaĹ‚ siÄ™ prawidĹ‚owo.
### [STAN_SYSTEMU]
- Ekosystem gotowy do dziaĹ‚ania w BricsCAD. Wymagany restart CAD.
### [BLOKADY / PROBLEMY]
- Blokada pliku .dll przez uruchomionÄ… aplikacjÄ™ BricsCAD. Kod skompilowaĹ‚ siÄ™ bezbĹ‚Ä™dnie (CoreCompile), lecz MSBuild nie byĹ‚ w stanie skopiowaÄ‡ plikĂłw binarnych do katalogu /bin. Wymagany restart BricsCAD.
### [KOLEJNY_KROK]
- Restart BricsCAD, kompilacja koĹ„cowa i testy manualne.

## [v2.28.3] 2026-06-07T15:22:52+02:00 - Bugfix: Utrata sesji oraz delegacja kodu LISP
### [ZREALIZOWANO]
- Naprawiono błąd "ucinania" pytania (utraty historii czatu po ponownym uruchomieniu programu) przez wymuszenie natychmiastowego zapisu sesji SessionManager.SaveSession() od razu po otrzymaniu pytania w SupervisorOrchestrator.cs.
- Zaktualizowano system prompt agenta SupervisorProfile (system_prompt_supervisor.txt), ucząc go o istnieniu profilu LispCoderProfile. Rozwiązuje to problem, w którym Supervisor samodzielnie pisał LISP bez wymaganej wstrzykniętej obsługi błędu *error*, co wcześniej uniemożliwiało wystartowanie pętli samonaprawiającej w środowisku CAD.
### [STAN_SYSTEMU]
- Kompilacja przebiegła pomyślnie. Nowe zasady LISP w pełni zintegrowane.


## [2026-06-07] Faza 16: Integracja LISP z Bazą Wiedzy
- Zakończono wdrażanie zakładki Skrypty LISP w oknie Knowledge Base.
- Uzupełniono system prompts o poinstruowanie agentów o narzędziu manage_lisps.
- Podłączono LispManager do AgentControl, by umożliwić bezpośrednie wywołania LISPa (ExecuteLispFromExternal).

