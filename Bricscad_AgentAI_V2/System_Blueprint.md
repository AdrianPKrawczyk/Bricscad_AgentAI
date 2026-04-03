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

## Rejestr Narzędzi (Registered Tools)

### SelectEntitiesTool
**Klasa**: `Bricscad_AgentAI_V2.Tools.SelectEntitiesTool`
**Cel**: Wyszukiwanie i filtrowanie obiektów w bazie danych DWG.
**Parametry**:
- `EntityType` (string, Required): Nazwa klasy (np. "Line") lub wildcard (np. "*Line").
- `Mode` (enum): "New", "Add", "Remove", "Clear". Określa sposób interakcji z `AgentMemoryState`.
- `Scope` (enum): "Model" (cała przestrzeń), "Blocks" (przeszukiwanie tylko wewnątrz uprzednio zaznaczonych bloków).
- `Conditions` (array of objects): Lista warunków filtrowania właściwości (`Prop`, `Op`, `Val`).