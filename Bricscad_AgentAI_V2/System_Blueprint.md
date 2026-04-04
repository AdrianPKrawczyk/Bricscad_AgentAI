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
**Cel**: Pobieranie reprezentatywnej próbki tekstów z zaznaczenia (DBText, MText, MLeader). Chroni kontekst przed przepełnieniem.
**Parametry**:
- `SaveAs` (string, Optional): Nazwa zmiennej do zapisu próbek.
**Uwagi**: Wykorzystuje algorytm nieliniowego próbkowania (`sqrt(n)`, max 15). Pobiera czysty tekst (`MText.Text`) ignorując kody formatowania RTF. Wiele próbek w pamięci jest łączonych separatorem ` | `.