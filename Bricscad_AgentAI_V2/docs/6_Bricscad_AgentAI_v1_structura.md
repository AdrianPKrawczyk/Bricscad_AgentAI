Oto kompletne i uporządkowane zestawienie wszystkich modułów oraz systemów pobocznych tworzących architekturę wtyczki **Bricscad_AgentAI**, oparte na przeanalizowanym kodzie i strukturze projektu:

### 1. Rdzeń Architektoniczny (Główne Moduły Sterujące)
Są to absolutne fundamenty, na których opiera się logika działania Agenta.
* **Silnik AI i Komunikacji (`AgentCommand.cs`):** "Mózg" wtyczki. Buduje systemowy prompt, komunikuje się asynchronicznie z lokalnym serwerem LLM i posiada "Ruter", który odbiera tagi i deleguje je do odpowiednich narzędzi.
* **Inicjalizacja i Środowisko (`AgentStartupAndCLI.cs`):** Punkt wejścia do programu. Ładuje wtyczkę, rozgrzewa w tle model (VRAM) i rejestruje komendy startowe w konsoli BricsCAD (m.in. przezroczyste polecenie `'AI`).
* **Tarcza Ochronna (`TagValidator.cs`):** System weryfikacji anty-halucynacyjnej. Bada składnię JSON oraz sprawdza zgodność wywoływanych przez LLM klas/właściwości z natywnym API CADa.
* **Pamięć Podręczna (`AgentMemory.cs`):** Zarządza zmiennymi globalnymi i dba o wstrzykiwanie zapamiętanych danych (np. współrzędnych czy list warstw) bezpośrednio w kolejne komendy w locie.
* **Interfejs Użytkownika (`AgentControl.cs`):** Okno czatu z modelem (PaletteSet). Obsługuje wizualizację historii, kolorowanie składni i przyjmowanie zapytań od człowieka.

---

### 2. Zaawansowane Systemy Poboczne (Silniki Logiczne i Testowe)
Skomplikowane układy zbudowane w C#, z którymi Agent może współpracować (lub które służą do sprawdzania jego inteligencji).
* **Zaawansowany Kalkulator Inżynieryjny RPN (`RpnCalculator.cs`):** Potężny, zintegrowany silnik Odwrotnej Notacji Polskiej. Nie jest tylko kalkulatorem matematycznym, ale w pełni funkcjonalnym **Silnikiem Analizy Wymiarowej** (obsługuje układ SI, przeliczanie jednostek, potęgowanie np. metrów kwadratowych, offsety temperatur). Potrafi pobierać wymiary prosto z kliknięć na ekranie i zapisuje swój stan trwale w bazie DWG.
* **Menedżer Skryptów i Makr (`MacroManager.cs`, `UserMacroControl.cs`):** System pozwalający na zapisywanie, edycję (we wbudowanym edytorze kodu z kolorowaniem składni) i odtwarzanie gotowych sekwencji JSON. Zapisuje makra globalnie lub przypina je do konkretnego pliku DWG.
* **Studio Treningowe - Kreator Datasetów (`TrainingStudio.cs`):** Przechwytuje logikę i działania programisty wyklikującego polecenia w CAD, tworząc z nich idealnie sformatowane tagi (Złoty Standard), a następnie zapisuje je do plików `.jsonl`, co służy do douczania (Fine-tuningu) modelu. Zawiera też wbudowany bezpośredni parser skryptów LISP.
* **Ewaluator Ręczny (`AgentTesterControl.cs`):** Graficzny panel do wgrywania plików z pytaniami. Pozwala wysłać pytanie do modelu, odfiltrować z "łańcucha myślowego" czyste tagi komend, zmierzyć czas odpowiedzi i ocenić jakość predykcji poszczególnych modeli.
* **Auto-Sędzia / Benchmark (`AutoBenchmark.cs`):** Bezlitosny, wieloetapowy automat testujący LLM. Potrafi symulować środowisko CAD, wstrzykując modelowi spreparowane dane w trakcie konwersacji. Dokonuje twardej walidacji (m.in. sprawdzając jakość wygenerowanych przez AI wzorów inżynieryjnych poprzez wrzucenie ich we własny silnik RPN) i generuje sumaryczne raporty JSON.
* **Menedżer Bazy Danych (`DatasetManagerControl.cs`):** Okienkowy menedżer (wywoływany komendą `AGENT_DB_MANAGER`) służący do przeglądania i operacji na dużych plikach uczących (datasetach).

---

### 3. Moduły Narzędziowe (Tools - Ramiona Agenta)
Zbiór 25 zaimplementowanych klas (implementujących interfejs `ITool`), z których Agent korzysta używając tagów `[ACTION:]` oraz `[SELECT:]`.
* **Analityczne:** `AnalyzeSelectionTool`, `ReadTextSampleTool`, `GetPropertiesToolLite`, `GetPropertiesTool`, `ReadPropertyTool`, `ListUniqueTool`.
* **Geometria:** `CreateObjectTool`, `ModifyGeometryTool`, `SetPropertiesTool`.
* **Teksty:** `MTextFormatTool`, `MTextEditTool`, `TextEditTool`.
* **Bloki:** `CreateBlockTool`, `InsertBlockTool`, `ListBlocksTool`, `EditBlockTool`.
* **Warstwy:** `SearchLayersTool`, `ManageLayersTool`.
* **Skale opisowe:** `AddAnnoScaleTool`, `ReadAnnoScalesTool`, `RemoveAnnoScaleTool`.
* **Interakcje użytkownika i System:** `UserInputTool`, `UserChoiceTool`, `ForeachTool`, `SendCommandTool`, `CalculateRpnTool`.