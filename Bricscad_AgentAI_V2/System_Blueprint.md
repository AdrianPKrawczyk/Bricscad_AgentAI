# System# Baza Wiedzy (System Blueprint)

## Architektura Tool Calling (V2)
Nowa architektura porzuca parsowanie Regex z V1 na rzecz wbudowanego wsparcia modeli LLM dla Tool Calling. System został ustanowiony wokół interfejsu `IToolV2` oraz klas `ToolModels`.

### 1. Kontrakt Narzędzi (`IToolV2`)
Narzędzia muszą implementować interfejs narzucający zwracanie definicji zgodnych z OpenAI wejściowo, oraz przyjmowanie zdeserializowanego JSON (`JObject`).

### 2. Typy Tool Parameter (`src/Models/ToolModels.cs`)
Każde polecenie udostępnia `ToolDefinition`, opisane modelem `FunctionSchema` z atrybutami (Dictionary string -> ToolParameter) z flagami enumów i wymaganych elementów wg struktury JSONSchema.

### 3. Mechanizm TrimHistory (`src/Core/LLMClient.cs`)
System automatycznie optymalizuje okno kontekstowe poprzez przycinanie długich odpowiedzi z narzędzi (rola `tool`). Jeśli wynik przekracza 500 znaków, jest skracany do 100 znaków z dodaniem informacji o liczbie usuniętych bajtów. Pozwala to na zachowanie spójności struktury Tool Calling przy jednoczesnej drastycznej redukcji zużycia tokenów w długich sesjach.

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
- `RpnCalculator.cs`: Silnik matematyczny obsługujący wyrażenia odwrotnej notacji polskiej, pozwalający na dynamiczne przeliczanie wartości (np. `$OLD_RADIUS 10 +`).

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
- `Modifications` (array, Required): Lista słowników z kluczami `Prop` (nazwa właściwości do edycji m.in. Layer, Color, Linetype, ConstantWidth, TextString, Height, Radius) i `Val` (nowa wartość. Może zawierać wyrażenia matematyczne przez prefiks `RPN:` tj. `RPN: $OLD_RADIUS 10 +`).

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
Kombajn do edycji i formatowania tekstów. Obsługuje `DBText` i `MText`. Pozwala na edycję treści (`Append`, `Prepend`, `Replace`) oraz zaawansowane formatowanie RTF dla `MText` (podświetlanie słów, czyszczenie formatu).

### ManageAnnoScalesTool
Narzędzie do zarządzania skalami opisowymi (Annotative Scales). Obsługuje dodawanie, usuwanie i odczytywanie skal dla kompatybilnych obiektów. Pozwala również na całkowite wyłączenie opisowości.

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

### ForeachTool
Narzędzie pomocnicze do "rozpakowywania" i analizy list elementów zapisanych w zmiennych Agenta (@Variables). Pozwala modelowi LLM na przejrzysty wgląd w dane przed iteracją.
**Parametry**:
- `SaveAs` (string, Optional): Nazwa zmiennej do zapisu próbek.
**Uwagi**: Wykorzystuje algorytm nieliniowego próbkowania (`sqrt(n)`, max 15). Pobiera czysty tekst (`MText.Text`) ignorując kody formatowania RTF. Wiele próbek w pamięci jest łączonych separatorem ` | `.