# System Baza Wiedzy (System Blueprint)

## Architektura Tool Calling (V2)
Nowa architektura porzuca parsowanie Regex z V1 na rzecz wbudowanego wsparcia modeli LLM dla Tool Calling. System został ustanowiony wokół interfejsu `IToolV2` oraz klas `ToolModels`.

### 1. Kontrakt Narzędzi (`IToolV2`)
Narzędzia muszą implementować interfejs narzucający zwracanie definicji zgodnych z OpenAI wejściowo, oraz przyjmowanie zdeserializowanego JSON (`JObject`).

### 2. Typy Tool Parameter (`src/Models/ToolModels.cs`)
Każde polecenie udostępnia `ToolDefinition`, opisane modelem `FunctionSchema` z atrybutami (Dictionary string -> ToolParameter) z flagami enumów i wymaganych elementów wg struktury JSONSchema.

### 3. Mechanizm TrimHistory (`src/Core/LLMClient.cs`)
System automatycznie optymalizuje okno kontekstowe poprzez przycinanie długich odpowiedzi z narzędzi (rola `tool`). Jeśli wynik przekracza 500 znaków, jest skracany do 100 znaków z dodaniem informacji o liczbie usuniętych bajtów. Pozwala to na zachowanie spójności struktury Tool Calling przy jednoczesnej drastycznej redukcji zużycia tokenów w długich sesjach.

### 4. Dynamiczne Zarządzanie Kontekstem (Semantic Tool Routing)
Wprowadzone w **v2.7.0 GOLD**, pozwala na selektywne ładowanie narzędzi do promptu LLM w zależności od potrzeb.
- **Tagowanie Narzędzi**: Każde narzędzie deklaruje `ToolTags` (np. `#bloki`, `#warstwy`). Narzędzia `#core` są ładowane zawsze.
- **Pre-procesing (AgentControl.cs)**: Tekst użytkownika jest skanowany pod kontem hashtagów. Znalezione tagi są wycinane z wiadomości i przekazywane jako `initialTags` do `SendMessageReActAsync`.
- **Filtrowanie (ToolOrchestrator.cs)**: Metoda `GetToolsPayload(requestedTags)` zwraca tylko narzędzia pasujące do zestawu: `#core` OR `requestedTags` (lub wszystkie, jeśli podano `#all`).
- **Agentic Fallback**: Jeśli model potrzebuje dodatkowych narzędzi, wywołuje `RequestAdditionalTools`. `LLMClient` przechwytuje to wywołanie, aktualizuje lokalny zbiór tagów i w następnej iteracji pętli ReAct przesyła rozszerzony zestaw narzędzi.
- **Early Exit (Fast Mode)**: Wprowadzone w **v2.9.0**, drastycznie redukuje latencję przy prostych operacjach.
    - **Mechanizm**: Jeśli globalny przełącznik `chkEarlyExit` jest włączony, system C# analizuje wywołania narzędzi w bieżącej iteracji.
    - **Warunki**: Pętla ReAct zostaje przerwana (Client-Side Resolution), jeśli:
        1. Wszystkie wywołane narzędzia mają flagę `SupportsEarlyExit` ustawioną na `true` w `tools_config.json`.
        2. Wszystkie narzędzia zwróciły wynik niebędący błędem.
        - **Korzyść**: Eliminuje "pusty" przejazd do LLM tylko po to, by usłyszeć "Zrobione".

### 5. Monitorowanie Wydajności (`LLMStats`)
Zunifikowany model statystyk pozwala na spójne raportowanie wydajności we wszystkich kontrolek UI (`AgentControl`, `AgentTesterControl`, `DatasetStudioControl`).
- **Model**: `TotalTimeMs`, `PromptTokens`, `CompletionTokens`, `TotalTokens`, `TokensPerSecond`.
- **Aproksymacja**: Przy braku natywnej obsługi tokenów przez lokalne API OpenSource, system stosuje przelicznik 4 znaki = 1 token.

### 6. Specjalizacja Narzędzi i Hard-Cast (v2.14.0)
W celu uniknięcia "konfliktu narzędzi" oraz błędów refleksji w silniku Teigha, wprowadzono dwa mechanizmy:
- **Separation of Concerns**: Narzędzia uniwersalne (np. `ModifyPropertiesTool`) są celowo ograniczane do właściwości wspólnych (`Entity`), podczas gdy skomplikowane obiekty (Wymiary, Teksty) obsługiwane są przez dedykowane klasy (np. `DimensionEditTool`, `TextEditTool`).
- **Hard-Cast Fallback**: W `SelectEntitiesTool.cs`, mechanizm refleksji jest poprzedzony jawnym rzutowaniem typu (`if (ent is Hatch hatch) ...`). Pozwala to na dostęp do właściwości "niewidocznych" dla standardowej refleksji, takich jak `HatchObjectType`.

## Zarządzanie Stanem (State)

### AgentMemoryState (`src/Core/AgentMemoryState.cs`)
Klasa statyczna przechowująca stan sesji Agenta. Izoluje kolekcję `ActiveSelection` (tablica `ObjectId[]`), co pozwala na:
- **Update(ids)**: Nadpisanie całego zbioru.
- **Append(ids)**: Unikalne dołączanie idków do zbioru.
- **Remove(ids)**: Odejmowanie idków ze zbioru.
- **Clear()**: Resetowanie pamięci.

### Variables (`AgentMemoryState.Variables`)
Słownik `Dictionary<string, string>` przechowujący zmienne sesji (prefix `@`).
- **InjectVariables(input)**: Automatycznie zamienia wystąpienia `@nazwa` na wartości ze słownika przed przekazaniem do narzędzi lub silnika RPN.

### Core Services
- `AgentMemoryState.cs`: Przechowuje globalny stan zaznaczenia (`ActiveSelection`) oraz słownik zmiennych (`Variables`). Elementy te są wstrzykiwane do argumentów narzędzi przed ich wykonaniem.
- `PropertyValidator.cs`: Tarcza anty-halucynacyjna. Skanuje pliki bazy wiedzy API BricsCAD i weryfikuje poprawność atrybutów dla danej klasy obiektu. Chroni przed błędami refleksji.
- `RpnCalculator.cs`: Silnik matematyczny obsługujący wyrażenia odwrotnej notacji polskiej z trwałością sesyjną. Wspiera fizyczną analizę wymiarową (UnitEngine) oraz inteligentne wstrzykiwanie wyników do CAD.
- `ToolConfigManager.cs`: Dynamiczne zarządzanie konfiguracją narzędzi, tagami i flagą Early Exit.
- `EngineTracer.cs`: Diagnostyczny nasłuchiwacz niskopoziomowych zdarzeń silnika ODA Teigha. Izoluje i raportuje problemy z transakcjami dokumentu.
- `ToolOrchestrator.cs`: Zarządca pakietów narzędziowych, filtrujący prompt na podstawie zapotrzebowania modelu.

## Rejestr Narzędzi (Registered Tools)

### SelectEntitiesTool
**Klasa**: `Bricscad_AgentAI_V2.Tools.SelectEntitiesTool`
**Cel**: Wyszukiwanie i filtrowanie obiektów w bazie danych DWG.
**Parametry**:
- `EntityType` (string, Required): Nazwa klasy (np. "Line") lub wildcard (np. "*Line").
- `Mode` (enum): "New", "Add", "Remove", "Clear". Określa sposób interakcji z `AgentMemoryState`.
- `Scope` (enum): "Model", "Blocks".
- `Conditions` (array): Warunki filtrowania (`Prop`, `Op`, `Val`).

### CreateObjectTool
**Klasa**: `Bricscad_AgentAI_V2.Tools.CreateObjectTool`
**Cel**: Tworzenie nowych obiektów CAD z obsługą RPN i interakcji z użytkownikiem.
**Parametry**:
- `EntityType` (string, Required): "Line", "Circle", "DBText", "MText", "MLeader".
- `Layer` (string): Docelowa warstwa.
- `SelectObject` (boolean): Czy dodać nowy obiekt do `ActiveSelection` (Auto-Selection).
- `StartPoint`, `EndPoint`, `Center`, `Position`, `ArrowPoint`, `LandingPoint` (string): Współrzędne (x,y,z) lub "AskUser".
- `Radius`, `Height`, `Rotation`, `Text` (string): Wartości liczbowe/tekstowe wspierające prefix "RPN:".

### ModifyPropertiesTool
**Klasa**: `Bricscad_AgentAI_V2.Tools.ModifyPropertiesTool`
**Cel**: Edycja cech obiektów w pamięci `ActiveSelection`. Zapewnia wsparcie dla iniekcji zmiennych `$OLD_...` i obliczeń matematycznych.
**Parametry**:
- `Modifications` (array, Required): Lista słowników z kluczami `Prop` (nazwa właściwości do edycji m.in. Layer, Color, Linetype, ConstantWidth) i `Val` (nowa wartość).
- **Zasady Bezpieczeństwa (v2.14.0)**: Narzędzie posiada blokadę `forbiddenProps`. Próba edycji właściwości `Text`, `DimensionText`, `Contents`, `Dimscale` itp. skutkuje błędem i wymuszeniem użycia narzędzi specjalistycznych.

### ManageLayersTool
**Klasa**: `Bricscad_AgentAI_V2.Tools.ManageLayersTool`
**Cel**: Zarządzanie warstwami (Create, Modify, Delete) z obsługą masek i blokadami bezpieczeństwa.
**Parametry**:
- `Action` (string, Required): "Create", "Modify", "Delete".
- `LayerName` (string, Required): Nazwa lub maska (np. "INST_*").
- `ColorIndex` (int): Kolor ACI.
- `IsOff`, `IsFrozen`, `IsLocked` (boolean): Stany warstwy.
- `Linetype` (string): Rodzaj linii.
**Zasady Bezpieczeństwa**:
- Blokada usuwania warstw "0" oraz "Defpoints".
- Blokada usuwania warstwy aktualnej (Clayer).
- Automatyczne zgłaszanie błędów przy próbie usunięcia warstwy zawierającej obiekty.

### ExecuteMacroTool
**Klasa**: `Bricscad_AgentAI_V2.Tools.ExecuteMacroTool`
**Cel**: Uruchamianie złożonych procedur (makr), skryptów LISP oraz poleceń CAD.
**Parametry**:
- `MacroName` (string): Nazwa zdefiniowanego makra (np. "CleanDrawings", "ResetLayers", "ZoomExtents").
- `CustomCommand` (string): Własny kod LISP lub ciąg poleceń do wykonania bezpośrednio w BricsCAD.
**Uwagi**: Narzędzie raportuje sukces wystawienia komendy lub błąd składni, pozwalając na inteligentną reakcję Agenta w pętli ReAct.

### GetPropertiesTool
**Klasa**: `Bricscad_AgentAI_V2.Tools.GetPropertiesTool`
**Cel**: Odczyt i ekstrakcja fizycznych/graficznych właściwości (kolor, warstwa, geometria) obiektów z bazy DWG.
**Parametry**:
- `Mode` (string, Required): "Lite" lub "Full". Ogranicza limit obiektów i ilość zwracanych właściwości (np. Lite do 15 obiektów, Full do 5 obiektów ze szczegółową geometrią).
**Uwagi**: Narzędzie działa wyłącznie na pamięci bieżących obiektów ustawionych w `AgentMemoryState.ActiveSelection`. LLM nie przekazuje list ID, bazuje na automatycznym dostępie do wyselekcjonowanej bazy.

### ReadPropertyTool
**Klasa**: `Bricscad_AgentAI_V2.Tools.ReadPropertyTool`
**Cel**: Odczyt konkretnej właściwości (natywnej lub wirtualnej) z obiektów w zaznaczeniu z opcją zapisu do pamięci Agenta.
**Parametry**:
- `Property` (string, Required): Nazwa właściwości (np. `Length`, `Area`, `MidPoint`, `Angle`, `Position.X`).
- `SaveAs` (string, Optional): Nazwa zmiennej (np. `MojaDlugosc`), pod którą wynik zostanie zapisany w `AgentMemoryState.Variables`.
**Uwagi**: Wspiera wirtualne właściwości (`MidPoint`, `Centroid`, `Volume`, `Angle`, `Value`) oraz zagnieżdżoną refleksję. W przypadku zapisu wielu obiektów, wartości są łączone separatorem ` | `.

### AnalyzeSelectionTool
**Klasa**: `Bricscad_AgentAI_V2.Tools.AnalyzeSelectionTool`
**Cel**: Agregacja i statystyka obiektów w pamięci `ActiveSelection`. Zastępuje V1 Analyze i ListUnique.
**Parametry**:
- `Mode` (enum, Required): `CountTypes` (zliczanie wystąpień klas) lub `ListUniqueValues` (wykaz unikalnych wartości właściwości).
- `TargetProperty` (string): Wymagane dla trybu `ListUniqueValues` (np. `Layer`, `Color`).
- `SaveAs` (string, Optional): Nazwa zmiennej do zapisu wyniku.
**Uwagi**: W trybie `ListUniqueValues` wartości w pamięci są łączone operatorem ` | `. Tryb `CountTypes` zapisuje pełny string raportu. Braki parametrów są obsługiwane przez komunikaty błędów bez przerywania transakcji.

### ReadTextSampleTool
**Klasa**: `Bricscad_AgentAI_V2.Tools.ReadTextSampleTool`
Służy do inteligentnego próbkowania treści tekstowych z dużych zbiorów obiektów, chroniąc kontekst LLM przed przepełnieniem.

### TextEditTool
**Klasa**: `Bricscad_AgentAI_V2.Tools.TextEditTool`
Kombajn do edycji i formatowania tekstów. Obsługuje `DBText` i `MText`. Pozwala na edycję treści (`Append`, `Prepend`, `Replace`) oraz zaawansowane formatowanie RTF dla `MText` (podświetlanie słów, czyszczenie formatu).

### DimensionEditTool
**Klasa**: `Bricscad_AgentAI_V2.Tools.DimensionEditTool`
**Cel**: Precyzyjna edycja anatomii wymiarów.
**Parametry**: `TextOverride`, `OverallScale`, `ArrowBlock`, `TextColor`, `DimLineColor`, `ExtLineColor`.
**Uwagi**: Obsługuje mapowanie nazw bloków strzałek (np. `_ARCHTICK`) oraz bezpośrednią modyfikację zmiennych stylu (DimVars).

### EditBlockTool
Zaawansowane narzędzie do chirurgicznej edycji wnętrza bloku (`BlockTableRecord`). Pozwala na modyfikację geometrii, tekstów i właściwości obiektów wewnątrz definicji bloku, co skutkuje aktualizacją wszystkich jego wystąpień. Obsługuje rekurencję i filtrowanie.

### EditAttributesTool
Narzędzie do precyzyjnego zarządzania atrybutami (dynamicznymi tekstami) w instancjach bloków. Pozwala na masowy odczyt wartości oraz ich aktualizację (Update) z obsługą wstrzykiwania zmiennych i parametrów RPN.

### ListBlocksTool
Narzędzie do odczytu biblioteki bloków. Zwraca czystą listę definicji (BlockTableRecord), filtrując szum systemowy (anonimowe, Layouty, XREFy).

### InsertBlockTool
Narzędzie do wstawiania nowych wystąpień bloków z automatyczną synchronizacją atrybutów i opcjonalnym wypełnianiem ich wartości podczas wstawiania.

### CreateBlockTool
Narzędzie do tworzenia nowych definicji bloków z aktualnie zaznaczonych obiektów (ActiveSelection) przy użyciu klonowania głębokiego (DeepClone).

### UserInputTool
Narzędzie umożliwiające Agentowi zadawanie pytań użytkownikowi bezpośrednio w linii komend BricsCAD. Obsługuje pobieranie tekstu, liczb oraz punktów na ekranie.

### UserChoiceTool
Narzędzie do wyświetlania listy opcji (słów kluczowych) do wyboru przez użytkownika. Automatycznie zarządza ograniczeniami API BricsCAD (brak spacji w Keywords).

### RequestAdditionalTools
**Klasa**: `Bricscad_AgentAI_V2.Tools.RequestAdditionalToolsTool`
**Cel**: Mechanizm dynamicznego ładowania nowych kategorii narzędzi (Package Manager).
**Parametry**:
- `CategoryName` (string): Nazwa kategorii do dociągnięcia do promptu (np. `layers`, `blocks`, `all`).

### InspectEntityTool
**Klasa**: `Bricscad_AgentAI_V2.Tools.InspectEntityTool`
**Cel**: Pobiera surowe właściwości DXF i atrybuty geonetryczne konkretnego obiektu.
**Parametry**:
- `EntityHandle` (string): Opcjonalny uchwyt (Handle) obiektu. Jeśli brak, sprawdza pierwszy element z `ActiveSelection`.

### ForeachTool
Narzędzie pomocnicze do "rozpakowywania" i analizy list elementów zapisanych w zmiennych Agenta (@Variables). Pozwala modelowi LLM na przejrzysty wgląd w dane przed iteracją.

---

## Mechanizm Data Flywheel (Dataset Studio)

Wprowadzone w **v2.10.0**, umożliwia ciągłe doskonalenie modelu poprzez zbieranie "Złotych Standardów" (Golden Standards) bezpośrednio podczas pracy inżynierskiej.

### Snapshotting
Po każdym udanym zakończeniu pętli ReAct (w `AgentControl.cs`), system wykonuje snapshot historii `ChatML`.
- **Deserializacja**: Historia jest czyszczona z pustych pól (`NullValueHandling.Ignore`).
- **Pamięć sesji**: Snapshot trafia do `DatasetStudioControl`, gdzie użytkownik może go zweryfikować.

### Context Slicer (✂️ Krajalnica)
Wprowadzone w **v2.11.0**, rozwiązuje problem *Context Poisoning* w danych treningowych. 
- **Logika**: Pozwala na izolację pojedynczego "turnu" (Sytem + Ostatni User + Tool Responses + Assistant Answer).
- **Deep Copy**: Snapshoty w pamięci są izolowane od bieżącej sesji czatu.

### JSONL Storage
- **Format**: Standard OpenAI Fine-tuning (Messages JSON structure).
- **Zapis**: Każda sesja to jedna linijka w pliku `.jsonl`.
- **Lokalizacja**: Folder wtyczki (`Assembly.Location`), nazwa pliku: `Agent_Training_Data_v2_DO_TRENINGU.jsonl`.

---

## Ekosystem Testowy (AutoBenchmark V2)

### Architektura
Izolowane laboratorium analityczne do walidacji modeli LLM. Celowo **nie zawiera** mechanizmu eksportu do JSONL — stanowi wyizolowany Test Set (zapobiega Data Leakage).

### Przepływ
```
User Prompt → [PRAWDZIWY LLM] → tool_calls
                                     ↓
                    [Benchmark] przechwytuje wywołanie
                                     ↓
                    SimulatedCADResponses[ToolName] → wiadomość roli "tool"
                                     ↓
                    [PRAWDZIWY LLM] kontynuuje / kończy
```

### Componenty
#### `AutoBenchmarkEngine.cs` (Silnik)
- **Pre-flight Schema Check** — instancjonuje wszystkie `IToolV2`, weryfikuje `Name` i `Description`. Jeden błąd = halt całego benchmarku.
- **Memory Sandbox** — czyści `AgentMemoryState.Variables` i `ActiveSelection` przed każdym testem; wstrzykuje `MockMemoryVariables`.
- **Walidator (Auto-Sędzia)** — weryfikuje poprawność wywołań na podstawie reguł (ToolCalled, ArgumentMatch, SequenceMatch).

---

## 7. System Agent Recipes (Drogowskazy)

Wprowadzone w **v2.16.0**, stanowi mechanizm wstrzykiwania przykładów Few-Shot do kontekstu LLM.

### 7.1. Mechanizm Triggerów ($)
System monitoruje wiadomości użytkownika pod kątem prefiksu `$`.
- **Wykrywanie**: `LLMClient.PreProcessRecipes` używa Regex `\$(\w+)` do identyfikacji triggerów.
- **Wstrzykiwanie**: Jeśli trigger zostanie odnaleziony w `AgentRecipes.json`, system wstrzykuje wiadomości instruktażowe do historii konwersacji.

### 7.2. Capture & persistencja
- **RecipeManager**: Statyczna klasa zarządzająca plikiem `AgentRecipes.json`. Obsługuje operacje CRUD na recepturach.
- **CaptureSessionAsRecipe**: Metoda w `DatasetStudioControl` parsująca JSONL bieżącej sesji w celu wyekstrahowania wyłącznie pomyślnych wywołań narzędzi.

---

## 8. Silnik RPN V2 (v2.20.4 GOLD)

Zmigrowany i rozbudowany silnik obliczeniowy zapewniający deterministyczną matematykę inżynierską.

### 8.1. Trwałość w DWG (NOD)
Dane stosu są przechowywane w **Named Object Dictionary** pod kluczem `BIELIK_RPN_STACK` jako obiekt `Xrecord`.
- **Zapis**: Wywoływany atomowo po każdej udanej operacji w CLI lub narzędziu.
- **Odczyt**: Inicjalizowany przy starcie komend `RPN`/`CALC` lub wywołaniu przez Agenta.

### 8.2. Interaktywne CLI i Pętla CAD
Komendy `RPN` i `CALC` wykorzystują `Editor.GetString` w pętli `while(true)`. 
- **Flags.Transparent**: Pozwala na wywoływanie kalkulatora znakami `'RPN` wewnątrz innych poleceń.
- **Stack Preview**: Przed każdym zapytaniem o dane, system odświeża i wyświetla 6 poziomów stosu w konsoli CAD.

### 8.3. Inteligentne Wstrzykiwanie (Injection)
Mechanizm `GetTopAsRawCadValue()` rozwiązuje problem niezgodności typów między kalkulatorem a linią poleceń CAD.
- **Unit Detection**: Pobiera `INSUNITS` aktywnego rysunku.
- **Auto-Conversion**: Jeśli na stosie jest wartość o wymiarze długości (`L=1`), zostaje przeliczona na jednostki rysunku (np. metry -> milimetry).
- **Raw Value Fallback**: Dla innych wymiarów (pole, masa), wstrzykiwana jest surowa wartość liczbowa (`DisplayValue`), ułatwiając wprowadzanie parametrów do poleceń CAD.
- **Formatting**: Wynik jest zawsze przekazywany z kropką jako separatorem (`InvariantCulture`), co zapobiega błędom w lokalizacjach nie-anglojęzycznych.