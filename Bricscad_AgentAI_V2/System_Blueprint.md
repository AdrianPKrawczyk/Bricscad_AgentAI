# System# Baza Wiedzy (System Blueprint)

## Architektura Tool Calling (V2)
Nowa architektura porzuca parsowanie Regex z V1 na rzecz wbudowanego wsparcia modeli LLM dla Tool Calling. System został ustanowiony wokół interfejsu `IToolV2` oraz klas `ToolModels`.

### 1. Kontrakt Narzędzi (`IToolV2`)
Narzędzia muszą implementować interfejs narzucający zwracanie definicji zgodnych z OpenAI wejściowo, oraz przyjmowanie zdeserializowanego JSON (`JObject`).

### 2. Typy Tool Parameter (`src/Models/ToolModels.cs`)
Każde polecenie udostępnia `ToolDefinition`, opisane modelem `FunctionSchema` z atrybutami (Dictionary string -> ToolParameter) z flagami enumów i wymaganych elementów wg struktury JSONSchema.

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