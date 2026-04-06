### Plik 4: `4_Directory_Structure.md` (Struktura i Prace Przygotowawcze)

Niniejszy dokument definiuje fizyczną i logiczną strukturę nowej wersji projektu. Agent AI ma obowiązek tworzyć pliki wyłącznie w obrębie struktury `V2`, traktując folder `V1` jako źródło wiedzy.

#### 1. Wizualizacja struktury katalogów
```text
AgentAI_Workspace/
│
├── memory.md                   # Centralny rejestr stanu prac (AI Log)
├── System_Blueprint.md         # Główna specyfikacja techniczna V2 (Źródło Prawdy)
│
├── Bricscad_AgentAI/           # [V1] WERSJA REFERENCYJNA (READ-ONLY)
│   └── ... (stary kod, tagi, regexy)
│
└── Bricscad_AgentAI_V2/        # [V2] TWOJE MIEJSCE PRACY
    ├── Bricscad_AgentAI_V2.sln
    ├── docs/                   # Dokumentacja projektowa (Wytyczne)
    │   ├── 1_PRD.md
    │   ├── 2_Current_Architecture.md
    │   ├── 3_Migration_Plan.md
    │   └── 4_Directory_Structure.md
    │
    ├── src/                    # Kod źródłowy aplikacji
    │   ├── AgentControl.cs          # Główny interfejs czatu i logów
    │   ├── AgentTesterControl.cs    # Interfejs do uruchamiania Benchmarków
    │   ├── DatasetStudioControl.cs  # Interfejs do zbierania danych treningowych (JSONL)
    │   ├── AutoBenchmarkControl.cs  # Panel laboratorium analitycznego
    │   ├── Core/               # Jądro systemu
    │   │   ├── LLMClient.cs    # Nowy klient HTTP (Tool Calling)
    │   │   ├── Orchestrator.cs # Zarządca narzędzi i mapowanie JSON
    │   │   └── CadWrapper.cs   # Obsługa wątków (_AGENT_RUN_TOOL)
    │   │
    │   ├── Tools/              # Implementacje IToolV2 (Modularne wtyczki)
    │   │   ├── Selection/      # Narzędzia typu Select
    │   │   ├── Geometry/       # Tworzenie i edycja kształtów
    │   │   └── Management/     # Warstwy, Bloki, Właściwości
    │   │
    │   ├── Models/             # Klasy DTO dla JSON Schema i Tool Calls
    │   ├── UI/                 # Kontrolki WinForms/WPF
    │   └── Utilities/          # Kalkulator RPN, Jednostki, Loggery
    │
    └── resources/              # Pliki API (BricsCAD_API_Quick.txt itp.)
```

#### 2. Pliki Śledzenia Postępu (Root Level)

Aby Agent AI nie gubił się w trakcie sesji, musi aktywnie korzystać z dwóch plików znajdujących się w głównym katalogu:

* **`memory.md` (Pamięć Operacyjna):**
    * **Cel:** Zapisywanie "gdzie skończyliśmy".
    * **Struktura:**
        * `[DONE]`: Lista zamkniętych zadań.
        * `[ACTIVE]`: Opis aktualnie pisanego modułu/funkcji.
        * `[NEXT]`: Następny logiczny krok z planu migracji.
        * `[CONTEXT]`: Specyficzne ID procesów lub nazwy zmiennych, które Agent musi zapamiętać między odpowiedziami.

* **`System_Blueprint.md` (Zamiast `architecture.md`):**
    * **Cel:** Definicja "Jak to właściwie działa". Jest to kontrakt techniczny między Agentem a Tobą.
    * **Zawartość:**
        * Opis formatu JSON Schema używanego dla narzędzi.
        * Mapowanie odpowiedzi LLM (`tool_calls`) na konkretne klasy w `src/Tools/`.
        * Specyfikacja obsługi błędów (co się dzieje, gdy LLM wyśle złe parametry).
        * Dokumentacja "Magicznego Wrappera" (Synchronizacja CAD).

#### 3. Prace Przygotowawcze (Instrukcja dla Agenta)

Agent AI przed napisaniem pierwszej linii kodu biznesowego musi wykonać następujące kroki:

1.  **Inicjalizacja repozytorium:** Stworzenie struktury folderów `V2`.
2.  **Konfiguracja Projektu:**
    * Stworzenie pliku `.csproj` dla .NET Framework 4.8.
    * Poprawne zlinkowanie referencji do DLL-ek BricsCAD (wykorzystując ścieżki z V1 jako wzór).
3.  **Analiza Kontraktu:** Przeczytanie `1_PRD.md` oraz `3_Migration_Plan.md` i wpisanie pierwszego wpisu do `memory.md` w sekcji `[ACTIVE]`.