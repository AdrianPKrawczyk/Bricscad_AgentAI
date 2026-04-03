# Plik 5: Protokoły Pamięci i Operacji (AI Memory Protocol)

Niniejszy dokument określa zasady, według których Agent AI musi zarządzać wiedzą o projekcie w trakcie jego realizacji. Ma to na celu zapewnienie ciągłości pracy, nawet jeśli sesja zostanie przerwana lub zresetowana.

## 1. Zarządzanie plikiem `memory.md` (Dziennik Budowy)

Plik `memory.md` w katalogu głównym projektu jest **jedynym oficjalnym rejestrem stanu prac**. Agent ma obowiązek aktualizować go po każdym zakończonym podzadaniu.

### Struktura pliku `memory.md`:
* **`## [LOG_DATETIME]`**: Każdy wpis musi mieć znacznik czasu.
* **`### [ZREALIZOWANO]`**: Lista konkretnych zmian w kodzie (np. „Stworzono klasę `LlmClient.cs` w folderze Core”).
* **`### [STAN_SYSTEMU]`**: Krótki opis techniczny (np. „Klient HTTP łączy się z portem 1234, ale nie obsługuje jeszcze błędów 404”).
* **`### [BLOKADY / PROBLEMY]`**: Opis napotkanych trudności (np. „Błąd referencji do `brxmgd.dll` – wymaga ręcznego sprawdzenia ścieżki”).
* **`### [KOLEJNY_KROK]`**: Precyzyjne zadanie do wykonania w następnej kolejności (zgodnie z `3_Migration_Plan.md`).

**Zasada aktualizacji:** Przed każdą odpowiedzią kończącą sesję, Agent musi wykonać `WRITE` do tego plika.

## 2. Zarządzanie plikiem `System_Blueprint.md` (Baza Wiedzy V2)

Podczas gdy `memory.md` to historia, `System_Blueprint.md` to **aktualny opis techniczny nowej wersji**. Jeśli Agent podejmie decyzję o architekturze, musi ją tam zapisać.

### Obowiązkowe sekcje w `System_Blueprint.md`:
* **Kontrakt Tool Calling**: Dokładny format JSON, jaki model LLM musi wygenerować.
* **Rejestr Narzędzi**: Lista aktualnie zaimplementowanych klas w folderze `src/Tools/` wraz z ich opisami.
* **Logika Mapowania**: Opis tego, jak `tool_calls.arguments` są zamieniane na obiekty C#.

## 3. Kompendium Wiedzy o V1 (Quick Reference dla Agenta)

Aby Agent nie musiał za każdym razem analizować tysięcy linii kodu z folderu `Bricscad_AgentAI`, poniżej znajduje się „ściąga” z kluczowych mechanizmów wersji V1, które musi rozumieć:

### Logika Komunikacji V1:
* **Główna klasa**: `AgentCommand.cs`.
* **Proces**: Tekst -> `systemPrompt` -> Odpowiedź LLM z tagami `[ACTION:]` lub `[SELECT:]` -> Regex (parsowanie) -> `TagValidator.cs`.
* **Problem**: Brak determinizmu. LLM często gubi nawiasy klamrowe w JSONach wewnątrz tagów.

### Logika CAD (Do zachowania):
* **Magiczny Wrapper**: Metoda `WykonajWCADAsync<T>` w `AgentCommand.cs` używa `doc.SendStringToExecute("_AGENT_RUN_TOOL\n")`. Jest to krytyczne dla synchronizacji wątków (UI vs CAD).
* **Zaznaczanie**: Metoda `WykonajInteligentneZaznaczenie` w `AgentCommand.cs` wykonuje iterację po bazie danych DWG (`Transaction`, `BlockTableRecord`) i sprawdza właściwości obiektów za pomocą Refleksji C#.
* **Pamięć i Zmienne**: Klasa `AgentMemory.cs` przechowuje zmienne `@Nazwa`, które są wstrzykiwane do komend przed ich wykonaniem.
* **Kalkulator**: Klasa `RpnCalculator.cs` obsługuje jednostki (np. `_m`, `_kg`) i stałe fizyczne (np. `#PI`), chroniąc precyzję obliczeń.

## 4. Wytyczne dot. Zachowania Agenta
1.  **ZAKAZ HALUCYNACJI API (RAG REFERENCE)**: Jeśli nie masz absolutnej pewności co do istnienia klasy, metody lub właściwości w BricsCAD API (np. wahasz się, czy użyć `TextString` czy `Contents` dla danego obiektu), **masz bezwzględny obowiązek** sprawdzić to w plikach referencyjnych w folderze `Bricscad_AgentAI/`. 
    Do dyspozycji masz dwie bazy danych – korzystaj z nich w następującej kolejności:
    * **`BricsCAD_API_Quick.txt` (Priorytet 1):** Szukaj tutaj najpierw. Jest to wyselekcjonowana baza najczęściej używanych obiektów (Entity) BricsCADa, w której właściwości są opisane najbardziej szczegółowo.
    * **`BricsCAD_API_V22.txt` (Priorytet 2):** Użyj tego pliku jako fallbacku (opcji zapasowej), jeśli klasa nie istnieje w pliku Quick. Znajduje się tu większość obiektów BricsCADa (obecnie bez elementów Mechanical i BIM).
    * *Uwaga:* Projektuj kod odczytu tych plików w sposób elastyczny, ponieważ w przyszłości w folderze może pojawić się trzeci plik z kompletną, globalną bazą wszystkich obiektów.
2.  **KODOWANIE**: Nowy kod w V2 musi być czysty, udokumentowany i korzystać z nowoczesnych bibliotek (np. `System.Text.Json` dla .NET 4.8).
3.  **PRACA SELEKTYWNA**: Przy kopiowaniu logiki z V1, Agent musi odrzucić wszystko co dotyczy `Regex` i `TagValidator`, zostawiając tylko „mięso” czyli operacje na bazie danych CAD.