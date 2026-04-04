# BricsCAD Agent AI V2: Profesjonalny Podręcznik Użytkownika (v2.6.2 GOLD)

Witaj w wersji **GOLD** systemu Bielik AI V2. Niniejszy podręcznik został przygotowany dla inżynierów i projektantów BricsCAD, którzy chcą w pełni wykorzystać potencjał sztucznej inteligencji zintegrowanej bezpośrednio z silnikiem CAD.

---

## 🧠 1. Architektura Pamięci i Stanu

Zrozumienie sposobu, w jaki Agent "myśli" i przechowuje dane, jest kluczowe dla budowania zaawansowanych scenariuszy pracy.

### 1.1. Pamięć Zaznaczenia (ActiveSelection)
W przeciwieństwie do standardowych poleceń CAD, Agent posiada **pamięć selekcji**, która persists (utrzymuje się) między kolejnymi zapytaniami.
- **Automatyzacja selekcji**: Każdy nowo utworzony obiekt (np. linią, blokiem) jest automatycznie dodawany do pamięci.
- **Wydajność**: Dzięki temu możesz wydać polecenie "Narysuj okrąg", a w następnym kroku napisać po prostu "Zmień jego kolor na czerwony" – Agent wie dokładnie, o który obiekt chodzi, bez konieczności ponownego wskazywania go na ekranie.
- **Zarządzanie**: Pamięcią sterują narzędzia `SelectEntities` (dodawanie/odejmowanie) oraz polecenie głosowe/tekstowe "Odznacz wszystko" (czyści stan).

### 1.2. Zmienne Sesji (@Variables)
Agent może wyekstrahować dane z rysunku i zapisać je w nazwanych "szufladkach".
- **Zapisywanie**: Narzędzia takie jak `ReadProperty` lub `AnalyzeSelection` pozwalają zapisać wynik pod aliasem (np. `@SumaPowierzchni`).
- **Wstrzykiwanie**: Możesz wymusić użycie zmiennej w następnym kroku, używając symbolu `$`.
- **Przykład**: *"Odczytaj długość tej linii jako @L, a potem narysuj okrąg o promieniu $L"*.

---

## 🧮 2. Silnik Obliczeniowy RPN (Reverse Polish Notation)

Agent V2 posiada wbudowany procesor matematyczny działający w Odwrotnej Notacji Polskiej. Pozwala to na wykonywanie obliczeń bezpośrednio na geometrii.

### 2.1. Zmienne Dynamiczne `$OLD_...`
Podczas modyfikacji właściwości (`ModifyProperties`), Agent automatycznie udostępnia starą wartość obiektu pod specjalnym prefiksem.

| Zmienna | Opis | Przykład użycia w prompt |
| :--- | :--- | :--- |
| `$OLD_RADIUS` | Obecny promień okręgu/łuku | *"Zwiększ promień o 1.5 raza"* (Agent: `$OLD_RADIUS 1.5 *`) |
| `$OLD_HEIGHT` | Obecna wysokość tekstu | *"Zmniejsz teksty o 2 jednostki"* (Agent: `$OLD_HEIGHT 2 -`) |
| `$OLD_LENGTH` | Obecna długość linii/polilinii | *"Wydłuż o 10%"* (Agent: `$OLD_LENGTH 1.1 *`) |
| `$OLD_LAYER` | Obecna nazwa warstwy | Wykorzystywane w logice warunkowej. |

### 2.2. Przykłady Formuł RPN
- **Skalowanie**: `RPN: $OLD_RADIUS 2 *` (Podwojenie promienia).
- **Przesunięcie**: `RPN: $OLD_X 100 +` (Przesunięcie o 100 jednostek w osi X).
- **Złożone**: `RPN: 10 20 + 5 *` (Wynik: 150).

> [!TIP]
> Jeśli chcesz mieć pewność, że Agent użyje obliczeń, napisz: *"Zastosuj formułę RPN: [twoje działanie]"*.

---

## 🏢 3. Zaawansowane Zarządzanie Blokami i Atrybutami

W wersji GOLD rozróżniamy dwa krytyczne tryby pracy z blokami:

### 3.1. Edycja Definicji (Globalna) - `EditBlock`
Modyfikuje "matrycę" bloku. Zmiana tutaj wpływa na **wszystkie** wystąpienia danego bloku w całym rysunku.
- **Zastosowanie**: Zmiana koloru linii wewnątrz symbolu, usunięcie zbędnej geometrii z definicji.
- **Opcja `Recursive`**: Pozwala Agentowi wejść głębiej w zagnieżdżone bloki.

### 3.2. Edycja Atrybutów (Lokalna) - `EditAttributes`
Modyfikuje tylko dane tekstowe (atrybuty) w **konkretnej instancji** bloku na rysunku.
- **Zastosowanie**: Numeracja pomieszczeń, wypełnianie tabliczek rysunkowych, zmiana opisu bez zmiany wyglądu bloku.

---

## 📐 4. Skale Opisowe (Annotative)

Agent V2 rozumie mechanizm adnotacyjności BricsCAD.
- **Zarządzanie skalą**: Możesz polecić dodanie zestawu skal do tekstów (`ManageAnnoScales`).
- **`DisableAnnotative`**: Możesz masowo wyłączyć tryb opisowy dla obiektów, które mają stałą wielkość niezależną od skali wydruku.
- **Automatyzacja**: Agent potrafi odczytać aktualną skalę rysunku (np. 1:50) i dopasować do niej tworzone obiekty.

---

## 🤝 5. Interakcja i Konsultacje (Tryb Hybrydowy)

Agent nie musi zgadywać – może zapytać Ciebie o zdanie.

### 5.1. Słowo kluczowe `AskUser`
Używaj go, gdy chcesz wskazać coś myszką w trakcie pracy Agenta.
- *"Narysuj linię od AskUser do 100,100"*.
- Agent przełączy fokus na BricsCAD i poprosi Cię o kliknięcie punktu.

### 5.2. Konsultacje w linii komend
Agent może wywołać interaktywne zapytania:
- **String/Value**: *"Podaj nazwę inwestora"*.
- **Choice**: Wyświetli listę opcji w pasku poleceń (np. `[Stal/Drewno/Beton]`). Możesz wybrać opcję kliknięciem.

---

## 🏗️ 6. Zaawansowane Scenariusze (Workflows)

Oto przykłady łańcuchów działań, które pokazują pełną moc wersji GOLD:

### Scenariusz A: Raportowanie i Modyfikacja
> *"Znajdź wszystkie polilinie na warstwie 'OBRYS', odczytaj ich powierzchnie i zapisz do zmiennej @Pola. Jeśli powierzchnia jest większa niż 100, zmień kolor polilinii na czerwony, a w jej środku ciężkości wstaw tekst 'ALARM' o wysokości RPN: $OLD_AREA 0.01 *"*

### Scenariusz B: Standaryzacja Warstw
> *"Pobierz listę wszystkich warstw w rysunku. Dla każdej warstwy zaczynającej się od 'TEMP_', przenieś znajdujące się na niej obiekty na warstwę 'ARCH_STARE', a następnie usuń puste warstwy 'TEMP_*'."*

### Scenariusz C: Inteligentna Blokowa Numeracja
> *"Zaznacz bloki o nazwie 'POMIESZCZENIE'. Pobierz ich atrybut 'NUMER'. Użyj pętli, aby przesortować je i zmienić atrybut 'STATUS' na 'WERYFIKACJA' dla tych, których numer jest parzysty."*

---

## 🛠️ 7. Diagnostyka i Wydajność

- **Pasek HUD**: Sprawdzaj na dole okna czatu, czy Agent jest połączony z modelem LLM.
- **TrimHistory**: Przy bardzo długich sesjach Agent automatycznie "zapomina" najstarsze, techniczne logi, aby zachować szybkość reakcji (nie tracąc przy tym pamięci o Twoich zmiennych `@`).
- **Logi Narzędzi**: Jeśli coś nie działa, otwórz zakładkę "Logi Narzędzi" – zobaczysz tam dokładnie, jaki JSON został wysłany i co odpowiedział BricsCAD.

---
> [!IMPORTANT]
> **Bezpieczeństwo**: Agent V2 wykonuje większość operacji wewnątrz transakcji. Jeśli wystąpi błąd krytyczny, system spróbuje wycofać zmiany (Rollback), aby nie uszkodzić rysunku.

*Wersja Systemu: v2.6.2 GOLD | BricsCAD Agent AI Project*
