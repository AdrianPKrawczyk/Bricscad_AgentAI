# BricsCAD Agent AI V2: Commands Reference (v2.20.4 GOLD)

Poniższa lista zawiera wszystkie komendy zarejestrowane w systemie BricsCAD przez wtyczkę Agent AI V2.

---

## 🚀 Komendy Główne

### 1. `AGENT_V2`
Główna komenda uruchamiająca panel Agenta (Paletę).
- **Działanie**: Otwiera paletę boczną z interfejsem czatu, logami i nowym Dataset Studio.
- **Lokalizacja**: `Bricscad_AgentAI_V2.Core.AgentStartup`.

### 2. `AI_V2`
Przezroczysta (Transparent) wersja komendy Agenta.
- **Zaleta**: Może być wywoływana w trakcie trwania innych poleceń bez ich przerywania.

---

## 🔬 Laboratorium i Diagnostyka

### 3. `AGENT_BENCHMARK_V2`
Uruchamia panel automatycznych testów (Benchmark).
- **Działanie**: Automatycznie przełącza paletę na zakładkę Benchmark. Służy do weryfikacji modeli LLM na statycznych scenariuszach (Mock).

### 4. `AI_XDATA`
Ręczne wywołanie narzędzia ReadXData.
- **Działanie**: Pozwala na szybki podgląd metadanych XData zaznaczonych obiektów bezpośrednio w konsoli CAD.

### 5. `AI_SETXDATA`
Ręczne wywołanie narzędzia WriteXData.
- **Działanie**: Pozwala na zapisanie metadanych XData do zaznaczonych obiektów. Wymaga podania nazwy aplikacji oraz danych w formacie JSON.

### 6. `AGENT_TESTER_V2`
Uruchamia panel testera V1-compatible.
- **Działanie**: Pozwala na wysyłanie surowych zapytań JSON do orchestratora w celach debugowania schematów.

---

## 🧮 Komendy Kalkulatora RPN (v2.20.4)

### 5. `RPN`
Interaktywny kalkulator Odwrotnej Notacji Polskiej z trwałością w DWG.
- **Flaga**: Transparent (można wywołać jako `'RPN`).
- **Nowość**: Automatyczne wstrzykiwanie wyniku do linii poleceń CAD po naciśnięciu `Enter`.
- **Unit-Clean**: Automatycznie konwertuje jednostki długości na jednostki rysunku (`INSUNITS`).

### 6. `CALC`
Szybka pętla obliczeniowa (tylko odczyt).
- **Flaga**: Transparent.
- **Zastosowanie**: Wykonywanie obliczeń bez wstrzykiwania wyniku do bufora CAD.

### 7. `STOS`
Diagnostyczny podgląd stosu RPN.
- **Działanie**: Wypisuje ponumerowaną listę wszystkich elementów aktualnie znajdujących się na stosie w bieżącym rysunku.

---

## ⚙️ Komendy Wewnętrzne (Systemowe)

### 8. `AGENT_RUN_TOOL_V2`
Krytyczna komenda mechanizmu **Thread Safety**. Jest wywoływana niejawnie przez `LLMClient`.
- **Działanie**: Bezpiecznie wykonuje narzędzia CAD na głównym wątku BricsCAD, zapobiegając crashom przy asynchronicznym Tool Callingu.

---

## 🏷️ Skróty i Aliasy
Wtyczka obsługuje również aliasy LISP (jeśli zainstalowano paczkę LispConnect):
- `(v2)` -> skrót do `AGENT_V2`.
- `(v2_bench)` -> skrót do `AGENT_BENCHMARK_V2`.
