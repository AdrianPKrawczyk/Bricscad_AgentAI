
# Product Requirements Document (PRD): DRUK_WIDOKI

**Projekt:** Menedżer Zakresów i Widoków dla BricsCAD (Moduł Ekosystemu Bielik V2)

**Wersja:** 1.0.0

**Status:** Wersja Robocza (Draft Architektoniczny)

## 1. Cel Projektu

Stworzenie niezależnej, okienkowej aplikacji (wtyczki) do środowiska BricsCAD, umożliwiającej definiowanie, zarządzanie oraz graficzną reprezentację nazwanych obszarów roboczych (widoków, rzutów, przekrojów). Aplikacja ma stanowić "pomost danych" pomiędzy inżynierem a autonomicznym Agentem AI (profil `CadLayoutProfile`), dostarczając Agentowi ustrukturyzowanych instrukcji i granic geometrii niezbędnych do zautomatyzowanego generowania arkuszy wydruku (Layoutów).

## 2. Architektura Systemu

System opiera się na architekturze **"Data-Driven DWG"** z wzorcem **Dual-State** (oddzielenie źródła prawdy od reprezentacji graficznej).

* **Separacja procesów (Loose Coupling):** Aplikacja jest kompilowana jako niezależna biblioteka DLL (np. `Bielik.DrukWidoki.dll`). Nie posiada twardych referencji do kodu rdzenia Agenta V2.
* **Medium Komunikacyjne:** Współdzielonym "serwerem" danych dla obu aplikacji jest sam plik `.dwg`.
* **Wzorzec Dual-State:**
  * *Master Data (Baza):* Wszystkie definicje przechowywane są w ukrytym słowniku systemowym rysunku (NOD - Named Object Dictionary).
  * *Reprezentacja (Grafika):* Polilinie w przestrzeni modelu pełnią jedynie funkcję uchwytów (Grips) dla użytkownika.

## 3. Model Danych i Zapis (Persistence)

### 3.1. Źródło Prawdy (Named Object Dictionary)

Dane przechowywane są w NOD pod globalnym kluczem `BIELIK_DRUK_WIDOKI`. Zapis realizowany jest poprzez obiekty typu `XRecord`. Każdy wpis reprezentuje jeden widok i zawiera:

* `ViewId` (GUID) – unikalny identyfikator.
* `Name` (String) – nazwa widoku (np. "Rzut Parteru").
* `Type` (Enum/String) – kategoria (Rzut, Przekrój, Schemat, Detal, Inne).
* `Geometry` (Array of Points) – zrzucone współrzędne wierzchołków (X,Y) obszaru.
* `CustomVariables` (Dictionary<string, string>) – dynamiczny słownik zmiennych wstrzykiwanych do Agenta (np. `SkalaWydruku = "1:50"`, `Szablon = "A3_Poziom"`).

### 3.2. Reprezentacja Graficzna (Polilinie + XData)

Na dedykowanej, niedrukowalnej warstwie (np. `_BIELIK_ZAKRESY`) program generuje zamknięte polilinie. Do każdej polilinii doczepione są dane rozszerzone (XData) zarejestrowanej aplikacji (RegApp: `BIELIK_VIEW_DEF`), przechowujące wyłącznie `ViewId`.

* Rozciągnięcie polilinii przez użytkownika powoduje zdarzenie aktualizujące pole `Geometry` w NOD.

## 4. Interfejs Użytkownika (UI/UX)

* **Typ okna:** `PaletteSet` (Panel dokowalny / Modeless). Pozwala na jednoczesną pracę w przestrzeni modelu BricsCAD bez zamykania okna.
* **Układ Głównego Okna:**
  * *Panel Lewy (Lista):* Wykaz zdefiniowanych widoków. Posiada wskaźniki statusu (np. ⚠️ brak reprezentacji graficznej, jeśli polilinia została usunięta).
  * *Panel Prawy (Szczegóły):* Edycja nazwy, typu oraz `DataGridView` z listą zmiennych `Key=Value`.
* **Pasek Narzędzi (Toolbar):**
  * ➕ *Dodaj (Menu):* Z prostokąta / Z wieloboku / Z zaznaczonej polilinii.
  * 👁️ *Pokaż:* Wykonuje `Zoom Window` do granic widoku i podświetla polilinię.
  * 🔄 *Odtwórz:* Generuje nową polilinię na podstawie danych z NOD (jeśli została przypadkowo usunięta).
  * 📥 *Importuj:* Pobiera słownik `BIELIK_DRUK_WIDOKI` z innego, wskazanego pliku DWG (Cross-DWG Import).

## 5. Przypadki Użycia (Use Cases)

### UC1: Tworzenie widoku przez użytkownika

1. Użytkownik wybiera "Dodaj z prostokąta".
2. Program prosi o wskazanie 2 narożników w przestrzeni BricsCAD.
3. Program generuje nowy rekord w NOD.
4. Program rysuje polilinię na warstwie `_BIELIK_ZAKRESY` i przypisuje jej XData z wygenerowanym ViewId.
5. Zaktualizowana lista pojawia się w PaletteSet.

### UC2: Migracja międzybranżowa (Import z innego pliku)

1. Użytkownik w nowym pliku instalacji sanitarnej klika "Importuj".
2. Wskazuje plik `.dwg` z projektem architektonicznym.
3. Program w tle czyta bazę z pliku zewnętrznego, nadpisuje lokalny słownik w NOD i automatycznie wyrysowuje polilinie reprezentujące obszary w nowym pliku.

### UC3: Ręczna modyfikacja granic

1. Użytkownik za pomocą standardowych uchwytów (Grips) BricsCADa rozciąga polilinię reprezentującą widok.
2. Nasłuchiwacz zdarzeń (Database Event Listener) modyfikacji obiektów wyłapuje polilinię z XData.
3. Program aktualizuje wierzchołki w słowniku NOD.

## 6. Integracja z Agentem AI (Ekosystem Bielik V2)

Aby Agent AI mógł wykorzystać dane z DRUK_WIDOKI, w głównym projekcie `Bricscad_AgentAI_V2` zostanie zaprogramowane dedykowane narzędzie (Tool Calling).

* **Nazwa Narzędzia (V2):** `ReadViewDefinitionsTool` (w puli profilu `CadLayoutProfile`).
* **Działanie:** Odpytuje plik DWG, czyta zawartość NOD i zwraca wynik.
* **Kontrakt JSON (Odpowiedź z narzędzia do LLM):**

**JSON**

```
{
  "Views": [
    {
      "ViewName": "Rzut Parteru - Woda",
      "ViewType": "Rzut",
      "Bounds": {"MinX": 0, "MinY": 0, "MaxX": 1000, "MaxY": 500},
      "Center": {"X": 500, "Y": 250},
      "Variables": {
        "SkalaWydruku": "1:50",
        "FormatPapieru": "A3",
        "TabelkaBramka": "TAK"
      }
    }
  ]
}
```

* **Zasada działania Agenta (System Prompt Handoff):** Gdy użytkownik nakazuje:  *"Zrób arkusze ze wszystkich rzutów w menedżerze"* , Agent pobiera powyższy JSON, nadpisuje swoje wbudowane instrukcje na podstawie zawartości pola `Variables` i wykonuje seryjne pętle za pomocą `ManageViewportsTool` i `PageSetupTool`.

## 7. Harmonogram Wdrożenia

### Faza 1: Baza Danych i Rdzeń CAD (Backend)

* [ ] Inicjalizacja projektu Class Library (.NET Framework / .NET Core dla BricsCAD).
* [ ] Klasy modeli danych (DTO) dla widoków.
* [ ] Implementacja CRUD dla Named Object Dictionary (Zapis/Odczyt XRecord).
* [ ] Moduł generowania i zarządzania warstwą `_BIELIK_ZAKRESY` oraz operacje na XData.

### Faza 2: Interfejs Użytkownika (UI PaletteSet)

* [ ] Utworzenie okna dokowalnego (WPF/WinForms).
* [ ] Bindowanie danych z NOD do listy w UI.
* [ ] Obsługa zdarzeń GUI (podświetlanie, Zoomowanie - COM/Teigha Viewports).

### Faza 3: Zaawansowane Interakcje CAD

* [ ] Procedury Jig (rysowanie prostokąta/wieloboku przez użytkownika na żywo).
* [ ] Nasłuchiwacz zdarzeń (Reactor) do automatycznej aktualizacji NOD przy rozciąganiu polilinii.
* [ ] Funkcja importu (ReadDwgFile bez blokowania interfejsu).

### Faza 4: Most z Agentem V2

* [ ] Implementacja `ReadViewDefinitionsTool` z interfejsem `IToolV2` w kodzie źródłowym Agenta.
* [ ] Testy w Sandboksie Agenta.
* [ ] Zdefiniowanie reguł dla `CadLayoutProfile` w `system_prompt_layout.txt`.

*Koniec dokumentu.*
