# Product Requirements Document (PRD) - BricsCAD Agent AI V2

## 1. Cel i Wizja Projektu (Executive Summary)
Projekt **BricsCAD Agent AI V2** to całkowity refaktoring architektoniczny istniejącej wtyczki do programu BricsCAD (pisanej w C# .NET Framework 4.8). 
Głównym celem V2 jest zmiana paradygmatu komunikacji z modelem LLM: przejście z niedeterministycznego generowania tekstu (i jego parsowania przez wyrażenia regularne Regex) na ustrukturyzowany standard **Function Calling (Tool Calling)** obsługiwany natywnie przez nowoczesne API (np. OpenAI API format, LM Studio, Ollama). 

Oczekiwany rezultat to wtyczka działająca szybciej (niższy czas inferencji TTFT - Time To First Token), zużywająca drastycznie mniej tokenów w oknie kontekstowym oraz gwarantująca 100% niezawodność parsowania parametrów narzędzi.

## 2. Diagnoza Problemów V1 (Dlaczego robimy refaktor)
Agent AI musi zrozumieć, *czego* unikamy w nowej wersji. Poniżej lista błędów architektonicznych V1, które zostają zbanowane w V2:

* **Problem 1: Zapychanie Kontekstu (Token Bloat).** * *Stan w V1:* `systemPrompt` zawiera całą dokumentację tekstową (jak używać narzędzi, jakie są parametry) połączoną z wstrzykiwanymi przykładami z plików `.jsonl`. Generuje to tysiące tokenów przy KAŻDYM zapytaniu do modelu.
    * *Skutek:* Wolne działanie lokalnych modeli LLM, szybkie wyczerpywanie okna kontekstowego.
* **Problem 2: Kruchość parsowania (Regex Fragility).**
    * *Stan w V1:* Model jest zmuszany do odpowiadania ciągiem znaków z wbudowanym formatem, np. `[ACTION:NAZWA_NARZEDZIA {"JSON"}]`. Kod C# próbuje to "wyciąć" za pomocą Regex (`Regex.Match`).
    * *Skutek:* Brak nawiasu, przypadkowa nowa linia, czy użycie nieprawidłowego cudzysłowu przez LLM całkowicie zawiesza wykonanie komendy.
* **Problem 3: Monolityczna Walidacja.**
    * *Stan w V1:* Zamiast polegać na strukturze, system próbuje leczyć halucynacje po fakcie za pomocą skomplikowanej klasy `TagValidator.cs`, która liczy nawiasy klamrowe i sprawdza logikę tekstu.
* **Problem 4: Sztywne złączenie (Tight Coupling).**
    * *Stan w V1:* Nowe narzędzie wymaga implementacji w kodzie oraz jednoczesnej aktualizacji hardkodowanego tekstu w prompcie systemowym.

## 3. Architektura Docelowa V2 (Kluczowe Wymagania)
Agent AI realizujący ten projekt musi rygorystycznie trzymać się poniższych założeń projektowych:

### 3.1. Standard Komunikacji: Tool Calling / Function Calling
* Cała logika wymuszania tagów zostaje porzucona.
* **Klient HTTP:** Warstwa komunikacji (np. metoda wysyłająca żądanie do API) musi wysyłać zapytania POST zgodne ze standardem OpenAI, zawierające pole `tools` z tablicą obiektów JSON Schema definiujących dostępne wtyczki C#.
* **Prompt Systemowy:** Ma zostać zredukowany do minimum (ok. 100-200 tokenów). Powinien opisywać jedynie "Personę" (kim jest agent) oraz wstrzykiwać globalne reguły CAD (np. jednostki i kalkulator RPN). Resztę wiedzy o narzędziach model czerpie z bloku `tools`.

### 3.2. Nowy interfejs `IToolV2` (Dynamiczna Rejestracja)
* Stary interfejs `ITool` (przyjmujący stringa) idzie do kosza.
* Nowy interfejs (lub klasa abstrakcyjna) musi wymuszać implementację dwóch kluczowych elementów:
    1.  `GetToolSchema()` - metoda zwracająca ustrukturyzowany obiekt (np. zserializowany JSON Schema), który opisuje nazwę narzędzia, jego cel i twardo zdefiniowane parametry.
    2.  `Execute(Document doc, Dictionary<string, object> arguments)` - metoda wykonawcza, do której trafiają już zdeserializowane i bezpieczne parametry z LLM.

### 3.3. Tool Orchestrator (Zarządca Narzędzi)
* Wymagany jest nowy mechanizm (klasa np. `ToolOrchestrator`), który przy starcie aplikacji automatycznie buduje listę dostępnych narzędzi, pobiera ich schematy i przygotowuje ładunek `tools` do wysyłki.
* Po otrzymaniu odpowiedzi od LLM zawierającej `tool_calls`, `ToolOrchestrator` ma za zadanie zdeserializować argumenty JSON, znaleźć odpowiednią klasę narzędzia i wywołać jej metodę `Execute`.

### 3.4. Co MUSI Zostać z V1 (Core Logic)
Choć zmieniamy "mózg" (komunikację z LLM), "ręce" (wykonywanie w CAD) pozostają te same:
* **"Magiczny Wrapper" (Synchronizacja Wątków):** Wywołania API BricsCADa MUSZĄ ostatecznie trafiać do ukrytej komendy `_AGENT_RUN_TOOL` (`doc.SendStringToExecute`). BricsCAD C# API zcrashuje się, jeśli transakcje (Transaction) i modyfikacje (OpenMode.ForWrite) będą wykonywane w locie na wątkach asynchronicznych generowanych przez HttpClient.
* **Logika Domenowa CAD:** Narzędzia takie jak `CreateObjectTool` czy mechanizm zaznaczania przestrzennego (stare `[SELECT]`) mają ogromną wartość. Należy skopiować ich logikę transakcyjną, wymieniając jedynie sposób, w jaki przyjmują dane.
* **Silnik RPN:** Zabezpieczenie kalkulacji matematycznych i jednostek fizycznych poprzez notację polską to kluczowe zabezpieczenie inżynierskie. Musi zostać w pełni zachowane.

## 4. Wytyczne dla Agenta Przekształcającego Kod (Migration Rules)
* **ZASADA READ-ONLY:** Agent ma całkowity zakaz tworzenia, usuwania i modyfikowania jakichkolwiek plików w oryginalnym folderze `Bricscad_AgentAI`. Folder ten jest wyłącznie biblioteką wiedzy.
* **MIEJSCE PRACY:** Wszelki nowy kod C# ma być tworzony w folderze `Bricscad_AgentAI_V2`.
* **BIBLIOTEKA JSON:** Do obsługi nowego standardu Tool Calling należy użyć natywnej biblioteki parsowania (np. `Newtonsoft.Json` lub wbudowanych klas `.NET`, jeśli są dostępne w używanej wersji), aby zagwarantować bezbłędną deserializację z API LLM. Żadnych Regexów do wyciągania JSON-ów z tekstu.

## 5. Kryteria Akceptacji (Definition of Done)
1. Nowa aplikacja V2 łączy się z serwerem LM Studio i wysyła poprawne żądanie zawierające pole `tools` (sprawdzone w logach serwera API).
2. LLM przestaje generować "potok myśli" w tekście z wymyślonymi tagami, a zamiast tego natywnie odpala strukturę `tool_calls`.
3. Klasa `ToolOrchestrator` potrafi bezbłędnie zdeserializować `tool_calls` i przekazać argumenty w formie mocno typowanej (np. `int Color`, `string LayerName`) do wybranego narzędzia w C#.
4. Kod BricsCAD (transakcje, rysowanie) uruchamia się na głównym wątku i wykonuje powierzone zadanie na pliku DWG bez zawieszania programu.