# Architektura Systemu BricsCAD Agent AI (V2 GOLD)

> [!NOTE]
> Ten dokument opisuje aktualną architekturę **V2 GOLD**. Poprzednia dokumentacja V1 znajduje się w rozdziale "Wiedza Domenowa V1" poniżej i służy wyłącznie jako referencja mechanizmów CAD.

## 1. Paradygmat V2: Tool Calling & ReAct

Wersja **V2 GOLD** porzuca parsowanie Regex na rzecz natywnego mechanizmu **OpenAI Tool Calling**. Pozwala to na pełną deterministyczność i brak błędów składniowych w komunikacji między LLM a C#.

### Główna Dokumentacja Projektowa:
Najbardziej aktualne szczegóły techniczne, schematy baz danych i narzędzi znajdują się w pliku:
👉 **[System_Blueprint.md](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/System_Blueprint.md)**

---

## 4. Moduły Systemowe (Usługi i Kontrolki)

System V2 GOLD jest modułowy. Kluczowe komponenty to:
- `ToolConfigManager.cs`: Dynamiczne zarządzanie konfiguracją narzędzi, tagami i flagą Early Exit.
- `RecipeManager.cs`: (NOWOŚĆ v2.16.0) Zarządzanie biblioteką przykładów Few-Shot (Agent Recipes).
- `EngineTracer.cs`: Diagnostyczny nasłuchiwacz niskopoziomowych zdarzeń silnika ODA Teigha. Izoluje i raportuje problemy z transakcjami dokumentu.
- `ToolOrchestrator.cs`: Zarządca pakietów narzędziowych, filtrujący prompt na podstawie zapotrzebowania modelu.
- `DatasetStudioControl.cs`: Centrum dowodzenia mechanizmem Dataset Flywheel i Agent Recipes.
- `AgentRecipeControl.cs`: Edytor i menedżer receptur (Drogowskazów).
- `ToolSandboxControl.cs`: Środowisko testowe do izolowanej walidacji narzędzi CAD.

## 2. Kluczowe Filtry i Prędkość (Early Exit)

W V2 wprowadzono dwa mechanizmy drastycznie obniżające koszty (tokeny) i czas odpowiedzi:
- **Semantic Tool Routing**: Agent dostaje tylko te narzędzia, których potrzebuje w danym momencie (np. tylko `#bloki`).
- **Early Exit (Fast Mode)**: Pętla ReAct jest przerywana natychmiast po udanej operacji CAD, bez ponownego odpytywania LLM o podsumowanie (Client-Side Resolution).

---

## 3. Pamięć i Stan (ActiveSelection)
Zasada działania pamięci obiektów pozostaje spójna z fundamentami V1:
System V1 jest wysoce zaawansowanym mostem (wrapperem) łączącym asynchroniczne środowisko zewnętrznego modelu językowego (LLM obsługiwanego przez serwer kompatybilny z OpenAI, np. LM Studio) z synchronicznym, opartym na transakcjach środowiskiem API programu BricsCAD.

Logika komunikacji nie opiera się na prostym modelu zapytanie-odpowiedź, lecz na **sterowaniu zdarzeniowym za pomocą pętli rekursywnej (Chain of Thought / ReAct)**. Model jest zmuszany przez System Prompt do generowania tekstowych "Tagów" (np. `[ACTION: ...]`), które kod C# parsuje za pomocą Wyrażeń Regularnych (Regex).

## 2. Pętla Komunikacji (Cykl Życia Zapytania)

1. **Iniekcja Kontekstu (System Prompt):** System buduje potężny string zawierający zasady, opis tagów oraz (co bardzo ważne) **dynamicznie wstrzykiwane jednostki aktualnego rysunku** (np. mm, metry), pobierane ze zmiennej systemowej `INSUNITS`.
2. **Asynchroniczne Żądanie HTTP:** Tekst wysyłany jest do lokalnego serwera API.
3. **Regex i Ekstrakcja:** Otrzymany tekst jest przeszukiwany pod kątem wystąpienia specyficznych nawiasów, np. `[ACTION:CREATE_OBJECT { ... }]`.
4. **Tarcza Anty-Halucynacyjna:** Wycięty Tag trafia do klasy `TagValidator.cs`.
5. **Wykonanie w CAD:** Jeśli Tag jest poprawny, C# mapuje go na konkretną klasę narzędzia (implementującą `ITool`) i wywołuje jej kod.
6. **Sprzężenie Zwrotne (Auto-Loop):** Jeśli narzędzie CAD zwróci wynik (np. pobrane właściwości z `GET_PROPERTIES`), C# automatycznie i niejawnie generuje kolejny prompt do modelu: `"Oto dane z narzędzia:\n{wynik}..."`, zmuszając LLM do analizy tych danych i wygenerowania kolejnego Tagu lub ostatecznej odpowiedzi w Tagu `[MSG: ...]`.

## 3. Kluczowe Mechanizmy CAD (DO ZACHOWANIA W V2!)

Poniższe 4 mechanizmy stanowią największą wartość inżynierską starego kodu. Tworząc nową architekturę (Tool Calling) w V2, musisz zaadaptować te rozwiązania.

### A. Magiczny Wrapper (Thread Safety)
* **Problem:** API BricsCAD (podobnie jak AutoCAD) jest bezwzględne – wszelkie transakcje i modyfikacje bazy danych DWG (np. `Transaction`, `OpenMode.ForWrite`) **muszą odbywać się na głównym wątku aplikacji CAD**. Tymczasem komunikacja z LLM odbywa się asynchronicznie (`await HttpClient.PostAsync`), co przerywa synchronizację z głównym wątkiem. Bezpośrednie wykonanie API CAD z poziomu tego Taska spowoduje natychmiastowy Crash BricsCADa.
* **Rozwiązanie w V1:** Zastosowano metodę `WykonajWCADAsync<T>`. Kod, który Agent chce wykonać, jest pakowany w delegat (`Func<object>`) i przypisywany do zmiennej globalnej. Następnie C# wysyła do paska poleceń ukrytą komendę: `doc.SendStringToExecute("_AGENT_RUN_TOOL\n", true, false, false);`. BricsCAD umieszcza tę komendę w swojej głównej kolejce FIFO, a gdy ją wykonuje (metoda z atrybutem `[CommandMethod("AGENT_RUN_TOOL")]`), bezpiecznie konsumuje przygotowany wcześniej delegat.

### B. Silnik Zaznaczania i Refleksja (`WykonajInteligentneZaznaczenie`)
* Zanim Agent cokolwiek zedytuje, musi to zaznaczyć (Tag `[SELECT]`).
* Metoda ta przyjmuje JSON z warunkami (np. `"Conditions": [{"Property": "Height", "Operator": ">", "Value": 10}]`).
* **Magia Refleksji:** System pobiera wszystkie obiekty w CAD i używa `System.Reflection.PropertyInfo`, aby dynamicznie odpytać obiekt (np. `DBText` lub `Line`) o właściwość przekazaną z LLM w postaci stringa (np. "Color", "Length").
* **Logika Domenowa:** Silnik zawiera inteligentne wyjątki (np. przelicza zapytania o `VisualColor`, sprawdzając czy kolor obiektu to "JakWarstwa", a następnie sprawdzając kolor przypisanej warstwy). 
* *Wniosek dla V2:* Ten kod jest bezcenny i musi zostać podpięty pod nowe narzędzie Tool Calling (np. `SelectEntitiesTool`).

### C. Kalkulator RPN (Odwrócona Notacja Polska) i Jednostki
* **Problem:** Modele LLM są słabe w matematyce przestrzennej i gubią się w jednostkach (np. podają wynik w milimetrach, gdy rysunek jest w metrach).
* **Rozwiązanie w V1:** Model ma rygorystyczny zakaz wykonywania obliczeń. Zamiast tego przygotowuje wyrażenia w notacji polskiej zaczynające się od `RPN: ` (np. `RPN: 10_m 2 /` co oznacza $10m / 2$). Zaimplementowany w C# parser RPN wykonuje ułamki i dba o bezbłędne konwersje wektorowe przed wstawieniem wartości na rysunek.

### D. Pamięć Agenta (Zmienne i Pętle)
* Klasa `AgentMemory.cs` działa jako schowek RAM (słownik C#).
* Narzędzia potrafią odczytywać wartości z rysunku i zapisywać je w pamięci pod przyjaznymi nazwami (np. `ZapiszJako: "SrodkiOkregow"`).
* Inne narzędzia (jak `[ACTION:FOREACH]`) iterują po tych listach, podmieniając specjalne klucze (np. `$ITEM1`) wewnątrz argumentów (np. seryjnie generując setki tekstów z obliczonym polem powierzchni w środkach figur geometrycznych). Mechanizm podmiany zmiennych (`InjectVariables`) musi zostać zachowany w V2, przed wykonaniem logiki konkretnego narzędzia.

## 4. Tarcza Anty-Halucynacyjna (`TagValidator.cs` i pliki `.txt`)
W V1 zaimplementowano system RAG (Retrieval-Augmented Generation) w połączeniu z walidatorem przed-wykonawczym.
* W folderze projektu znajdują się pliki `.txt` (np. `BricsCAD_API_Quick.txt` i `BricsCAD_API_V22.txt`), z których system ładuje do pamięci cache wszystkie legalne właściwości dla każdej klasy obiektów BricsCAD.
* Zanim kod C# wykona polecenie, `TagValidator` upewnia się, że np. klasa `Line` ma prawo posiadać właściwość `Volume` (jeśli nie, system odrzuca Tag z błędem do LLM: "Nieznana właściwość...").
* *Wniosek dla V2:* Ponieważ przechodzimy na Tool Calling, błędy składni (Regex/nawiasy) znikną. Jednak sam mechanizm sprawdzania istnienia właściwości w API nadal powinien być aplikowany na argumentach zdeserializowanych z obiektu JSON.