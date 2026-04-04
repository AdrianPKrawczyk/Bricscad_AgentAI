# BricsCAD Agent AI V2: Command Reference

Niniejszy dokument zawiera listę wszystkich dostępnych komend wtyczki **Bielik AI V2 GOLD**. Komendy te można wpisywać bezpośrednio w linii poleceń BricsCAD po załadowaniu pliku `.dll`.

## Lista Komend

| Nazwa Komendy | Opis (PL) | Tryb (Flags) | Cel |
| :--- | :--- | :--- | :--- |
| **`AGENT_V2`** | Otwiera główny panel boczny Asystenta AI. | Standard | Uruchomienie interfejsu graficznego (Palety). |
| **`AI_V2`** | Szybkie zapytanie do Agenta z linii komend. | Transparent | Zadanie pytania bez konieczności klikania w panelu. |
| **`AGENT_BENCHMARK_V2`** | Otwiera panel automatycznego benchmarkingu. | Standard | Masowe testowanie modeli LLM na zestawach testowych. |

---

## Szczegółowy Opis

### 1. AGENT_V2
Główna komenda inicjalizująca środowisko. Jeśli panel (PaletteSet) nie był wcześniej utworzony, komenda go powołuje do życia, dodaje kontrolkę `AgentControl` i dokuje ją do prawej krawędzi ekranu.
- **Użycie**: Wpisz `AGENT_V2` i naciśnij Enter.

### 2. AI_V2
Komenda przeznaczona dla zaawansowanych użytkowników, którzy chcą szybko wydać polecenie Agentowi. 
- **Działanie**: Po wpisaniu komendy, BricsCAD poprosi o wpisanie zapytania. Tekst zostanie przesłany bezpośrednio do silnika LLM, a Agent zacznie pracę w tle.
- **Zaleta**: Dzięki flagi `Transparent`, komenda może być wywołana w trakcie działania innych poleceń BricsCAD (poprzedzona apostrofem, np. `'AI_V2`).

### 3. AGENT_BENCHMARK_V2
Dedykowana komenda dla inżynierów promptu i deweloperów. 
- **Działanie**: Otwiera paletę Agenta i automatycznie przełącza ją na trzecią zakładkę (**Auto-Benchmark**). Pozwala na wczytanie pliku JSON z testami i uruchomienie masowej walidacji.

---

## Jak załadować wtyczkę?

Aby korzystać z powyższych komend, wykonaj następujące kroki:
1. Uruchom BricsCAD.
2. Wpisz komendę `NETLOAD`.
3. Wskaż plik binarny: `Bricscad_AgentAI_V2.dll`.
4. Po pomyślnym załadowaniu, wpisz `AGENT_V2`, aby zobaczyć panel.

> [!TIP]
> Jeśli chcesz, aby wtyczka ładowała się automatycznie, dodaj ścieżkę do pliku `.dll` w ustawieniach "Manage Applications" (Appload) w BricsCAD.
