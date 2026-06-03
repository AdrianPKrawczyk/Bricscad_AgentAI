# Plan Wdrożenia: Architektura "Router-Worker" (Supervisor-Eksperci)

## 1. Architektura systemu po modernizacji (Docelowy Model Działania)

Aplikacja porzuca jednowątkowy monolit na rzecz hierarchicznego systemu agentowego sterowanego wzorcem przekazywania sterowania (Handoff) oraz współdzielonej tablicy pamięci (Blackboard).

### Główne komponenty i przepływ sterowania:

1. **Pamięć Globalna (Semantic History):** Przechowuje wyłącznie bezpośredni dialog między użytkownikiem a systemem (Role: `user`, `assistant`) oraz wysokopoziomowe podsumowania działań ekspertów. Pamięć ta jest odizolowana od surowych odpowiedzi API i logów CAD.
2. **Supervisor (Router):** Główny proces nasłuchujący. Jego instrukcja systemowa (System Prompt) definiuje go jako menedżera zadań. Nie posiada on narzędzi wykonawczych (np. `CreateObject`, `ReadXData`). Posiada wyłącznie **narzędzia delegujące** (np. `RouteToCadExpert`, `RouteToNotesManager`).
3. **Blackboard (Współdzielony Stan / Shared State):** Centralny, strukturyzowany obiekt w pamięci C# (np. `AgentMemoryState`), do którego dostęp mają wszyscy agenci. Służy do wymiany wyekstrahowanych danych (np. kubatury, wektorów, identyfikatorów polilinii) między niepowiązanymi agentami (np. CAD Expert i Notes Expert).
4. **Worker (Wyspecjalizowany Ekspert):** Egzemplarz maszyny stanu wywoływany przez Supervisora z jasno zdefiniowanym celem (tzw. `Task Prompt`). Otrzymuje własną, **odizolowaną i ulotną historię wiadomości**, swój unikalny System Prompt oraz ściśle określoną pulę narzędzi z `ToolConfigManager` (np. tylko tag `#cad` lub `#obliczenia`). Po wykonaniu zadania (zakończeniu lokalnej pętli ReAct) Worker zwraca wynik (DTO) i jest usuwany z pamięci, a jego lokalny szum narzędziowy nie zanieczyszcza Pamięci Globalnej.

---

## 2. Szczegółowy harmonogram migracji (Kroki implementacyjne)

### Krok 1: Wyabstrahowanie silnika ReAct (`LLMClient.cs`)

Obecna metoda `SendMessageReActAsync` jest ściśle związana z jednym konkretnym typem zadania i globalnym kontekstem CAD. Należy ją przekształcić w uniwersalny silnik wykonawczy (Worker Engine).

* **1.1. Dekapitalizacja zależności środowiskowych:** Usunięcie twardego argumentu `Document doc` z sygnatury `SendMessageReActAsync`. Kontekst środowiska (CAD, Pliki, Notatki) powinien być przekazywany przez abstrakcyjny interfejs `IExecutionContext` lub wstrzykiwany bezpośrednio do konkretnych narzędzi podczas ich inicjalizacji w `ToolOrchestrator`.
* **1.2. Usunięcie logiki "Agentic Fallback" z silnika:**
Z kodu należy usunąć dynamiczne dociąganie tagów (warunek na `RequestAdditionalTools`). Pula narzędzi (`List<ToolDefinition>`) musi być stała i wstrzykiwana na etapie inicjalizacji pętli przez Supervisora.
* **1.3. Zmiana zwracanego typu (Return Type):**
Zamiast zwracać surowy `string`, pętla musi zwracać obiekt strukturyzowany (np. `AgentExecutionResult`), zawierający pola: `IsSuccess` (bool), `DisplayMessage` (string dla użytkownika), oraz `InternalData` (obiekt przekazywany z powrotem do Supervisora).

### Krok 2: Implementacja Blackboard (Pamięć Współdzielona)

Należy zapewnić mechanizm transferu danych między izolowanymi pętlami LLM.

* **2.1. Klasa `SharedMemoryState`:**
Utworzenie globalnego kontenera słownikowego (Klucz-Wartość), np. `Dictionary<string, object>`, wstrzykiwanego do ramy działania agentów.
* **2.2. Narzędzia transferowe (I/O Memory Tools):**
Implementacja narzędzi w C#, które zostaną udostępnione każdemu Workerowi (oraz Supervisorowi):
* `WriteToBlackboard(key, value)` – np. zapisywanie wyników obliczeń hydraulicznych.
* `ReadFromBlackboard(key)` – np. pobieranie tych wyników przez agenta notatek.



### Krok 3: Restrukturyzacja zarządzania narzędziami (`ToolConfigManager.cs` i `ToolOrchestrator.cs`)

Zniesienie pojęcia "Globalnych Narzędzi Core" na rzecz przypisywania ról.

* **3.1. Rejestr Profili (Agent Profiles):**
Rozszerzenie modelu JSON konfiguracji. Zamiast płaskiej listy tagów, należy zdefiniować profile (np. `CadProfile`, `NotesProfile`, `SupervisorProfile`). Każdy profil mapuje na dedykowany zestaw narzędzi oraz ścieżkę do pliku tekstowego z System Promptem.
* **3.2. Izolacja Handoff:**
Narzędzia służące do interakcji z użytkownikiem (`UserInput`, `UserChoice`) mogą pozostać we współdzielonej puli, jednak specyficzne narzędzia (np. `CreateObject`) muszą być dostępne **wyłącznie** po zainicjalizowaniu pętli dla konkretnego profilu docelowego.

### Krok 4: Implementacja Supervisora (Nadrzędna pętla sterująca)

Stworzenie nowej klasy nadrzędnej (np. `SupervisorOrchestrator`), która przejmuje kontrolę nad sesją (zastępując obecne, bezpośrednie wywołanie `LLMClient` z warstwy UI).

* **4.1. Zarządzanie Pamięcią Globalną:**
Klasa `SupervisorOrchestrator` utrzymuje nadrzędną listę `List<ChatMessage>`. Otrzymuje wiadomości od użytkownika i wysyła je do LLM wywołanego z profilem `SupervisorProfile`.
* **4.2. Logika Handoff (Delegacji):**
Implementacja narzędzi dla Supervisora (np. `DelegateTask`). Zdefiniowanie schematu argumentów JSON: `{"TargetExpert": "CadExpert", "TaskDescription": "Narysuj prostokąt 10x10"}`.
* **4.3. Instancjonowanie sub-pętli:**
Gdy Supervisor wywołuje `DelegateTask`, system:
1. Zawiesza wątek Supervisora.
2. Inicjalizuje `LLMClient` z nowym `System Prompt` (dla CAD).
3. Tworzy czystą lokalną listę `List<ChatMessage>`, wstawiając jako wiadomości `user` zawartość `TaskDescription` oraz obecny zrzut z tablicy `Blackboard`.
4. Uruchamia zmienioną (z Kroku 1) pętlę ReAct.
5. Po zwróceniu wyniku (`AgentExecutionResult`), wynik ten trafia do Pamięci Globalnej jako wiadomość roli `tool` odpowiadająca na wywołanie `DelegateTask`, a Supervisor wznawia pracę i podsumowuje operację użytkownikowi.



### Krok 5: Optymalizacja UX i Early Exit

Dostosowanie komunikacji asynchronicznej z interfejsem graficznym.

* **5.1. Propagacja zdarzeń w górę (Event Bubbling):**
Delegaty aktualizujące UI (`OnStatusUpdate`, `OnToolCallLogged`) muszą prawidłowo obsługiwać zdarzenia z uruchamianych dynamicznie sub-pętli Workera i przekazywać je do głównego okna aplikacji bez kolizji wątków, jasno wskazując użytkownikowi, *który* ekspert aktualnie przetwarza dane (np. `[CAD Expert]: Uruchamiam SelectEntities...`).
* **5.2. Obsługa Early Exit w modelu kaskadowym:**
Jeśli Worker użyje narzędzia wspierającego "szybkie wyjście" (np. utworzy blok poprawnie), proces Workera kończy się natychmiast z flagą `Success`, przerywając własną pętlę ReAct. Supervisor musi przyjąć tę flagę i samodzielnie zdecydować, czy generuje krótkie powiadomienie do UI, czy płynnie przechodzi do następnego kroku zapisanego w swojej logice (np. wezwania kolejnego Workera bez odpytywania użytkownika).