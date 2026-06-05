# SPECyfikacja: System Dynamicznych Formuł (Roslyn) i Makr (JSON) dla Architektury Multi-Agent

## 1. Cel i Docelowe Działanie Systemu

Celem wdrożenia jest rozszerzenie architektury `Supervisor-Worker` o system trwałego uczenia się i adaptacji. System przechodzi z modelu statycznych narzędzi (Hardcoded Tools) na model otwartej bazy wiedzy (Dynamic Knowledge Base).

**Po wdrożeniu system będzie działał następująco:**

1. **Trwała Pamięć Skryptowa:** Zamiast twardo kodować każdy nowy wzór w `Core`, model wygeneruje czysty kod C# dla algorytmu i zapisze go jako plik `.csx` na dysku.
2. **Kompilacja w locie (Hot-Reload):** Przy starcie aplikacji (lub na żądanie z UI), wtyczka użyje `Microsoft.CodeAnalysis.CSharp.Scripting` (Roslyn) do skompilowania plików `.csx` w pamięci RAM i zarejestruje je jako wywoływalne funkcje inżynierskie.
3. **Zarządzanie Makrami:** Sekwencje powtarzalnych operacji na Blackboardzie i wywołań narzędzi CAD będą zapisywane w formacie JSON i możliwe do ponownego odtworzenia.
4. **Warstwa Prezentacji (UI):** Użytkownik uzyska dostęp do dedykowanego panelu zarządzania (Zakładki w UI), gdzie będzie mógł przeglądać, edytować (w prostym edytorze) oraz ręcznie wywoływać zarówno dynamiczne formuły, jak i makra, bez konieczności angażowania agenta LLM.

---

## 2. Architektura Komponentów (Nowe Moduły)

### A. System Plików (File System)

* `[Katalog_Wtyczki]/CustomKnowledge/Formulas/` -> Przechowuje skrypty `.csx` (np. `Hvac_PressureLoss.csx`).
* `[Katalog_Wtyczki]/CustomKnowledge/Macros/` -> Przechowuje sekwencje `.json` (np. `CleanLayersAndExport.json`).

### B. Warstwa Core (Zarządcy)

* **`DynamicFormulaManager`**: Odpowiada za skanowanie katalogu `Formulas`, kompilację skryptów przez Roslyn, utrzymywanie indeksu `Dictionary<string, CompiledScript>` oraz bezpieczne wykonywanie (Sandbox).
* **`MacroManager`**: Odpowiada za deserializację plików JSON do obiektów sekwencyjnych i egzekucję kroków makra (w tym rzutowanie wyników na `SharedMemoryState` - Blackboard).

### C. Warstwa Interfejsu (UI)

* Nowa kontrolka `KnowledgeBaseControl` z dwiema głównymi zakładkami:
1. **Zakładka "Formuły Inżynierskie (Roslyn)"**: Lista dostępnych skryptów `.csx`, okno podglądu kodu (ReadOnly/Edit), przycisk "Przeładuj Skrypty" (Hot Reload).
2. **Zakładka "Makra (JSON)"**: Lista dostępnych makr z opisem kroków i przyciskiem "Wykonaj teraz".



### D. Warstwa Narzędzi (Tools dla HvacExpert / Supervisor)

1. `SearchKnowledgeBaseTool` -> Przeszukuje indeksy obu menedżerów i zwraca spis dostępnych formuł/makr pasujących do zapytania.
2. `SavePermanentFormulaTool` -> Przyjmuje nazwę, opis, argumenty i `string` z kodem C#, po czym zapisuje plik `.csx` i wywołuje zdarzenie przeładowania bazy.
3. `SaveMacroTool` -> Przyjmuje nazwę, opis i strukturę JSON sekwencji, zapisuje do pliku.
4. `ExecuteFormulaTool` -> Wykonuje skompilowany skrypt, przekazując mu słownik argumentów. Wynik może być automatycznie zrzucony na Blackboard.
5. `ExecuteMacroTool` -> Uruchamia menedżera makr dla konkretnego ID.

---

## 3. Plan Wdrożenia dla Agentów VS Code (Roadmap)

Kopiując poniższe instrukcje do Agenta VSC, podawaj je etapami (Faza 1, po jej ukończeniu Faza 2 itd.), aby uniknąć błędów kontekstu.

### FAZA 1: Przygotowanie Infrastruktury i Zależności

**Instrukcje dla Agenta VSC:**

1. Dodaj pakiety NuGet do projektu głównego: `Microsoft.CodeAnalysis.CSharp.Scripting` oraz (jeśli brakuje) `System.Text.Json`. Miej na uwadze kompatybilność z wersją .NET Framework / .NET Core używaną przez wtyczkę BricsCAD.
2. Utwórz w projekcie strukturę katalogów: `Core/DynamicSystems/`, `Tools/Knowledge/`, `UI/KnowledgeBase/`.
3. Dodaj mechanizm tworzenia folderów `CustomKnowledge/Formulas` i `CustomKnowledge/Macros` w katalogu %APPDATA% lub w ścieżce wykonawczej aplikacji podczas jej startu (`AgentStartup.cs`).

### FAZA 2: Budowa Silnika Kompilacji (Roslyn) i Makr

**Instrukcje dla Agenta VSC:**

1. W katalogu `Core/DynamicSystems/` utwórz klasę `DynamicFormulaManager`.
2. Zaimplementuj w niej metodę `LoadAndCompileAll()`, która ładuje pliki `.csx`.
* **KRYTYCZNE (Bezpieczeństwo):** Użyj `ScriptOptions.Default.WithImports("System", "System.Math", "System.Collections.Generic")`. Nie zezwalaj na importowanie modułów I/O (System.IO) wewnątrz skryptów agenta, aby zapobiec modyfikacjom dysku przez halucynacje LLM.
* Zdefiniuj klasę bazową dla skryptu (tzw. `Globals`), np. `public class ScriptGlobals { public Dictionary<string, double> Inputs; }`.


3. Zaimplementuj metodę `ExecuteFormula(string id, Dictionary<string, double> inputs)`, która wywołuje `RunAsync` na skompilowanym skrypcie.
4. Utwórz klasę `MacroManager` z modelami danych C# odzwierciedlającymi strukturę kroku makra (np. `MacroStep: ActionType, TargetId, Parameters`).

### FAZA 3: Integracja Narzędzi Agentowych (Tools)

**Instrukcje dla Agenta VSC:**

1. W katalogu `Tools/Knowledge/` utwórz klasy narzędzi implementujące `IToolV2`:
* `SearchKnowledgeBaseTool`
* `SavePermanentFormulaTool` (waliduje składnię przez próbę próbnej kompilacji przed zapisem na dysk!).
* `SaveMacroTool`
* `ExecuteFormulaTool` (po wykonaniu, wynik należy logować za pomocą `AgentTelemetry.ReportStatus`).


2. Zaktualizuj plik `ToolConfigManager` lub pliki profilów (`HvacProfile.json`), aby zarejestrować te nowe narzędzia w puli aktywnej bazy.

### FAZA 4: Rozbudowa Interfejsu Użytkownika (UI)

**Instrukcje dla Agenta VSC:**

1. W katalogu `UI/KnowledgeBase/` utwórz nową kontrolkę Windows Forms (lub WPF) `KnowledgeBaseControl.cs`.
2. Zaprojektuj `TabControl` z dwiema zakładkami: `Formuły (Roslyn)` oraz `Makra (JSON)`.
3. W zakładce Formuł dodaj:
* `ListBox` po lewej stronie z listą załadowanych formuł (pobieranych z `DynamicFormulaManager`).
* `RichTextBox` po prawej do wyświetlania kodu źródłowego C# wybranej formuły.
* Przycisk "Przeładuj Bazy" (podpięty pod `DynamicFormulaManager.LoadAndCompileAll()`).


4. Zintegruj `KnowledgeBaseControl` z głównym oknem aplikacji (np. jako nowa zakładka obok obecnego okna czatu/logów Agenta).

### FAZA 5: Testowanie Przepływu (QA - Quality Assurance)

**Instrukcje dla Agenta VSC:**

1. Napisz test jednostkowy w warstwie testów, który symuluje wywołanie `SavePermanentFormulaTool` z następującym kodem testowym: `return Inputs["A"] * Inputs["B"];`.
2. Następnie wywołaj `DynamicFormulaManager.LoadAndCompileAll()`.
3. Sprawdź asercją, czy `ExecuteFormula` z argumentami A=5, B=10 zwraca poprawnie wartość 50.

---

## 4. Oczekiwany Schemat (JSON) Definicji Skryptu Formuły

Agent LLM musi generować skrypty w jednolitym formacie. Narzędzie zapisu będzie oczekiwało ustrukturyzowanego obiektu przed zrzutem do `.csx`. Standard, którym należy uczyć model, wygląda tak:

**Kod wewnętrzny pliku (przykład: `Hvac_FlowRate.csx`):**

```csharp
// Wymagane wejścia: ["power_kW", "deltaT_C", "specific_heat_kJ_kgK"]
// Zwraca: Przepływ w m3/h

double power = Inputs["power_kW"];
double deltaT = Inputs["deltaT_C"];
double cp = Inputs["specific_heat_kJ_kgK"]; // dla wody zazwyczaj 4.18

// Obliczenie masy (kg/s) = kW / (cp * deltaT)
double massFlow = power / (cp * deltaT);

// Zamiana na m3/h (zakładając gęstość wody ~1000 kg/m3)
double volFlow = (massFlow * 3600) / 1000.0;

return volFlow;

```

*(Zmienna `Inputs` jest automatycznie dostępna w przestrzeni skryptu dzięki klasie `ScriptGlobals` przekazywanej przez silnik Roslyn).*
