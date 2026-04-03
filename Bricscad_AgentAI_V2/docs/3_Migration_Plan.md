# Plan Migracji (Migration Roadmap) - BricsCAD Agent AI V2

Ten dokument to szczegółowy plan działania dla Agenta AI dokonującego refaktoryzacji. Twoim zadaniem jest realizacja poniższych etapów w sposób sekwencyjny. Pamiętaj: kod z folderu `Bricscad_AgentAI` (V1) służy tylko do wglądu. Twórz nowy kod w `Bricscad_AgentAI_V2`.

## ETAP 1: Inicjalizacja i Nowy Kontrakt Narzędzi (Core Interfaces)
**Cel:** Stworzenie fundamentów pod architekturę wtyczkową opartą na Tool Calling.

1. **Utworzenie projektu:** Utwórz projekt biblioteki klas (Class Library) w C# (.NET Framework 4.8) w folderze V2. Podepnij biblioteki Teigha/BricsCAD (`brxmgd.dll`, `td_mgd.dll` - tak jak w V1).
2. **Definicja Schematu:** Stwórz klasy modeli C# reprezentujące strukturę wymaganą przez API LLM (np. `FunctionCallSchema`, `ToolDefinition`, `ToolCall`).
3. **Nowy Interfejs `IToolV2`:**
   * W V1 narzędzia implementowały `string Execute(Document doc, string jsonArgs)`. To jest przestarzałe.
   * W V2 zdefiniuj interfejs (lub abstrakcyjną klasę bazową), który narzuca:
     * `ToolDefinition GetToolSchema()` - Zwraca strukturę JSON Schema opisującą nazwę narzędzia, opis i parametry (typy, opisy, wymagane pola).
     * `string Execute(Document doc, System.Text.Json.JsonElement args)` (lub `Dictionary<string, object>`) - Metoda wykonująca logikę na podstawie już **zdeserializowanych** parametrów.

## ETAP 2: Tool Orchestrator i Klient HTTP (Nowy "Mózg")
**Cel:** Wymiana silnika opartego na Regex na solidnego klienta API z natywną obsługą `tools`.

1. **Stworzenie `ToolOrchestrator.cs`:**
   * Napisz klasę, która podczas startu wtyczki (lub przy inicjalizacji) skanuje projekt i rejestruje wszystkie dostępne klasy implementujące `IToolV2`.
   * Klasa ta musi posiadać metodę generującą pełną listę narzędzi (tablicę obiektów `tools`) gotową do wysłania w formacie JSON do LLM.
2. **Nowy Klient LLM (`LLMClient.cs`):**
   * Przepisz logikę wysyłania zapytania POST do serwera (np. lokalnego LM Studio na porcie 1234).
   * Żądanie musi zawierać krótki System Prompt, historię rozmowy oraz wygenerowaną tablicę `tools`.
3. **Pętla Reakcyjna (ReAct Loop):**
   * Po otrzymaniu odpowiedzi od LLM, system nie szuka już tagów w tekście.
   * Jeśli odpowiedź zawiera pole `tool_calls`, `ToolOrchestrator` ma:
     a) Rozpoznać nazwę wywołanej funkcji.
     b) Zdeserializować argumenty JSON podane przez LLM.
     c) Uruchomić metodę `Execute` odpowiedniej wtyczki C#.
     d) Dodać wynik działania wtyczki do historii (jako wiadomość o roli `tool`) i automatycznie wysłać kolejne zapytanie do LLM, aby ten dokończył zadanie.

## ETAP 3: Portowanie "Oczu Agenta" (Zaznaczanie)
**Cel:** Adaptacja logiki tagu `[SELECT]` na pełnoprawne narzędzie.
*W V1 zaznaczanie było wbudowane na sztywno w główną pętlę (`AgentCommand.cs`). W V2 musi stać się jednym z narzędzi w Orkiestratorze.*

1. **Stworzenie `SelectEntitiesTool.cs`:**
   * Skopiuj cenną logikę BricsCAD z metody `WykonajInteligentneZaznaczenie` (V1). 
   * Zwróć szczególną uwagę na zachowanie fragmentów z `System.Reflection.PropertyInfo` oraz transakcji (`Transaction`, `GetObject`).
2. **Definicja Schematu dla Select:**
   * Zdefiniuj w `GetToolSchema()` parametry narzędzia, które w V1 były w ukrytym JSON-ie: `Mode` (New, Add, Remove), `Scope` (Model, Blocks), `EntityType` (String) oraz `Conditions` (tablica obiektów: Property, Operator, Value).
3. **Usunięcie parsowania:**
   * Zastąp stare regexy (które wycinały właściwości i operatory) bezpiecznym odczytem z obiektu JSON dostarczonego przez wywołanie funkcji (Tool Call).

## ETAP 4: Portowanie "Rąk Agenta" (Narzędzia Wykonawcze)
**Cel:** Przeniesienie kluczowych akcji z folderu V1 do V2, odcinając je od tagów.

Dla każdego z poniższych narzędzi (i pozostałych z V1) powtórz proces: a) Utwórz klasę w V2 implementującą `IToolV2`, b) Zdefiniuj jej twardy JSON Schema dla LLM, c) Skopiuj logikę CAD, d) Usuń Regexy.
1. **`CreateObjectTool`:** Zdefiniuj schemat z opcjonalnymi parametrami (np. `Position`, `Text`, `Radius`, `StartPoint`). Skopiuj logikę rysowania używającą BricsCAD API.
2. **`ManageLayersTool`:** Przenieś logikę z zachowaniem rygoru (Tworzenie, Usuwanie, Łączenie warstw).
3. **`SetPropertiesTool`:** Przenieś uniwersalną "zmieniarkę" właściwości. **KRYTYCZNE:** Upewnij się, że "Magiczny Wrapper" (wykorzystujący `doc.SendStringToExecute("_AGENT_RUN_TOOL\n", ...)`) jest poprawnie wdrożony, by chronić te modyfikacje na głównym wątku CAD!
4. **`CalculateRpnTool` / Logika RPN:** Przenieś kod kalkulatora w całości. LLM nadal musi z niego korzystać poprzez wstrzykiwanie wartości typu `RPN: 10_m 2 /` do argumentów funkcji.

## ETAP 5: UI i Czyszczenie (Odrzucenie Długu Technologicznego)
**Cel:** Połączenie nowego "mózgu" z interfejsem użytkownika i usunięcie przestarzałego kodu.

1. **Podpięcie do `AgentControl.cs`:**
   * Skopiuj interfejs użytkownika z V1 do V2.
   * Zaktualizuj metodę podpiętą pod przycisk "Wyślij", by wywoływała nowy `LLMClient` zamiast starego `AgentCommand.ZapytajAgentaAsync`.
   * Czat powinien teraz wyświetlać jedynie komunikaty z roli `assistant` (właściwe odpowiedzi tekstowe) oraz komunikaty systemowe (sukces wykonania narzędzia).
2. **Eksmisja `TagValidator.cs`:**
   * Ponieważ standard Tool Calling opiera się na twardych schematach JSON wspieranych przez API, stary system zliczania nawiasów jest zbędny. Nie portuj go.
   * Zamiast tego, stwórz w `ToolOrchestrator` prosty mechanizm: jeśli LLM wyśle parametr, który nie istnieje w definicji `GetToolSchema()`, Orkiestrator ma automatycznie zwrócić do LLM odpowiedź o roli `tool` z błędem: *"Błąd: Użyto nieznanego parametru X. Popraw wywołanie."*   