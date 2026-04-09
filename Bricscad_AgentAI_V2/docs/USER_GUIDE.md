# BricsCAD Agent AI V2: Profesjonalny Podręcznik Użytkownika (v2.20.4 GOLD)

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

### 2.3. Interfejs CLI dla Kalkulatora (v2.20.4)
Możesz korzystać z mocy obliczeniowej Agenta bezpośrednio w linii poleceń BricsCAD.

- **`RPN`**: Interaktywny tryb obliczeń z **podglądem stosu na żywo**.
    - **Wstrzykiwanie**: Po zakończeniu (pusty Enter), wynik trafia do aktywnego polecenia BricsCAD.
    - **Unit-Clean**: System automatycznie przelicza jednostki długości (np. `1_m` -> `1000` dla rysunku w mm) i usuwa sufiks jednostki przed wstrzyknięciem.
- **`CALC`**: Pętla obliczeniowa (tylko odczyt). Idealna do szybkich przeliczeń bez wpływania na historię poleceń CAD.
- **`STOS`**: Wyświetla aktualną, pełną zawartość stosu matematycznego zapisaną w rysunku.

### 2.4. Trwałość Stosu (DWG Persistence)
Stos Agenta jest zapisywany wewnątrz pliku `.dwg` w słowniku NOD (`BIELIK_RPN_STACK`). Dane są odświeżane po każdej operacji, co gwarantuje ich bezpieczeństwo.

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

- **Akcja `SetCurrent`**: Dedykowany, bezpieczny sposób na przełączenie aktywnej warstwy roboczej.

---

## 📐 4. Edycja Precyzyjna (Wymiary i Teksty)

W wersji **v2.14.0** system przeszedł na model **Separation of Concerns**. Oznacza to, że skomplikowane obiekty mają swoje dedykowane, potężniejsze narzędzia.

### 4.1. Edycja Wymiarów - `DimensionEdit`
Zamiast ogólnych właściwości, używaj dedykowanego narzędzia do "anatomii" wymiaru.
- **Tekst**: Możesz nadpisać wartość lub wrócić do pomiaru (wpisując pusty tekst).
- **Skala**: Zmieniaj `OverallScale` by powiększyć teksty i strzałki bez zmiany stylu.
- **Stylizacja**: Niezależne kolory dla tekstu, linii głównej i linii pomocniczych.
- **Grot Strzałki**: Wybieraj predefiniowane bloki (np. `_ARCHTICK` dla kreski).

### 4.3. Rozszerzone dane (XData) - `ReadXData`
Agent posiada teraz narzędzie do "głębokiej inspekcji" metadanych ukrytych w obiektach DWG.
- **Zastosowanie**: Odczytywanie danych z zewnętrznych systemów (np. ERP, GIS) zapisanych jako XData.
- **Filtrowanie**: Możesz poprosić o dane konkretnej aplikacji: *"Odczytaj XData dla aplikacji 'MY_BIM_APP'"*.
- **Pamięć**: Możesz zapisać te metadane do zmiennej i użyć ich w formule RPN.

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
> *"Pobierz listę wszystkich warstw w rysunku. Dla każdej warstwy zaczynającej się od 'TEMP_', przenieś znajdujące się na niej obiekty na warstwę 'ARCH_STARE', a następnie usuń puste warstwy 'TEMP_*'. Na koniec ustaw przezroczystość wszystkich warstw 'ARCH_*' na 50% i ustaw grubość linii na 0.13mm."*

### Scenariusz C: Inteligentna Blokowa Numeracja
> *"Zaznacz bloki o nazwie 'POMIESZCZENIE'. Pobierz ich atrybut 'NUMER'. Użyj pętli, aby przesortować je i zmienić atrybut 'STATUS' na 'WERYFIKACJA' dla tych, których numer jest parzysty."*

---

## 🏷️ 7. Optymalizacja Kontekstu (Semantic Tool Routing)

W wersji **v2.7 GOLD** wprowadziliśmy system inteligentnego sterowania zestawem narzędzi przy użyciu tagów (Hashtags). Pozwala to na drastyczne przyspieszenie reakcji Agenta i uniknięcie pomyłek w złożonych rysunkach.

### 7.1. Czym są tagi narzędzi?
Zamiast wysyłać wszystkie 20+ narzędzi przy każdym zapytaniu, możesz wskazać Agentowi, w jakim obszarze ma pracować.
- **#core** (Zawsze aktywne): Podstawowe rysowanie, wybieranie, pętle i zmienne.
- **#bloki**: Wszystko co dotyczy definicji, atrybutów i wstawiania bloków.
- **#warstwy**: Zarządzanie warstwami.
- **#tekst**: Edycja tekstów i skal opisowych.
- **#makro**: Wywoływanie predefiniowanych procedur.
- **#all**: Odblokowuje pełny zestaw wszystkich dostępnych narzędzi.

### 7.2. Autouzupełnianie (Autocomplete)
W polu wprowadzania tekstu wpisz znak `#`, a pojawi się lista dostępnych kategorii. Możesz nawigować strzałkami i zatwierdzić wybór klawiszem `Enter` lub `Tab`.

### 7.3. Przykład użycia w praktyce
- *"Wypisz wszystkie bloki w tym rysunku #bloki"* – Agent załaduje tylko narzędzia do bloków, co zmniejsza ryzyko halucynacji.
- *"Zmień kolor linii na czerwony"* – Nie musisz dodawać tagów dla podstawowych zadań (narzędzia `#core` są zawsze aktywne).

### 7.4. Agentic Fallback (Samoleczenie)
Jeśli zapomnisz o tagu, a Agent uzna, że potrzebuje narzędzi z innej grupy (np. prosisz o warstwy bez tagu `#warstwy`), system posiada mechanizm **Agentic Fallback**. AI automatycznie "poprosi" o dostęp do brakującej puli narzędzi i wykona zadanie w następnym kroku.

---

> [!IMPORTANT]
> Bielik V2 GOLD to system deterministyczny. Używając precyzyjnych narzędzi i tagów, masz gwarancję 100% powtarzalności wyników.

### Scenariusz D: Automatyczne Szyki i Sekwencje (NOWOŚĆ v2.6.8)
Agent potrafi teraz generować skomplikowane układy geometryczne bez Twojej pomocy w liczeniu współrzędnych.
> *"Narysuj szyk 10 słupów (blok 'SLUP_A') zaczynając od punktu 0,0 i przesuwając każdy o 500 jednostek w prawo."*

W tym scenariuszu Agent używa narzędzia `Foreach` z modułem `GenerateSequence`:
1. Generuje listę 10 punktów (0,0; 500,0; 1000,0...).
2. Dla każdego punktu wywołuje `InsertBlock`, podstawiając wygenerowany punkt pod parametr `Position`.

---

## 🔄 7. Generator Ciągów (Foreach)

Narzędzie `Foreach` stało się potężnym procesorem danych przestrzennych i logicznych.
- **Sequence Generator**: Pozwala na tworzenie liniowych szyków punktów.
- **Tag `{item}`**: Służy jako miejsce podstawienia wygenerowanej wartości (pozycji lub elementu z listy).
- **Tag `{index}`**: Wstawia numer bieżącej iteracji (liczony od 1).
- **RPN & ToolName**: Możesz używać kalkulatora RPN wewnątrz akcji oraz wywoływać inne narzędzia niż `CreateObject` (np. `ManageLayers`) poprzez dodanie klucza `"ToolName": "..."` w szablonie JSON.

> [!TIP]
> Przykład zaawansowany: `{"EntityType": "DBText", "Text": "RPN: 'Nr ' {index} CONCAT"}` wstawi teksty "Nr 1", "Nr 2" itd.

---

## ⚙️ 9. Dataset Studio (Data Flywheel) - NOWOŚĆ v2.10.0

Aby system stawał się coraz mądrzejszy, wprowadziliśmy mechanizm **Data Flywheel**. Pozwala on na przechwytywanie Twoich interakcji z Agentem i zapisywanie ich jako "Złote Standardy" dla przyszłych sesji treningowych modelu.

### 9.1. Przechwytywanie Sesji
Po każdej zakończonej pętli myślowej (ReAct), system automatycznie przesyła snapshot rozmowy do zakładki **"💾 Dataset Studio"**.

### 9.2. Edycja i Zapis
1. Przejdź do zakładki **Dataset Studio**.
2. Wybierz sesję z listy po lewej stronie.
3. **Context Slicer (✂️)**: Domyślnie zaznaczona opcja "Izoluj polecenie" wycina z historii konwersacji tylko ostatnie zadanie (Turn). Pozwala to na uniknięcie "zanieczyszczenia" danych treningowych poprzednimi tematami. Odznacz tę opcję, jeśli chcesz zapisać pełną, wieloetapową sesję (Multi-turn).
4. W edytorze po prawej zobaczysz wynikowy kod JSON. Możesz go dowolnie edytować.
5. Kliknij **"💾 Zapisz Złoty Standard do JSONL"**.

Dane są dopisywane do pliku `Agent_Training_Data_v2_DO_TRENINGU.jsonl` w folderze wtyczki. Plik ten może być bezpośrednio użyty do fine-tuningu modeli OpenAI oraz OpenSource.

---

## 🛠️ 8. Diagnostyka i Wydajność

- **Pasek HUD**: Sprawdzaj na dole okna czatu, czy Agent jest połączony z modelem LLM.
- **TrimHistory**: Przy bardzo długich sesjach Agent automatycznie "zapomina" najstarsze, techniczne logi, aby zachować szybkość reakcji (nie tracąc przy tym pamięci o Twoich zmiennych `@`).
- **Logi Narzędzi**: Jeśli coś nie działa, otwórz zakładkę "Logi Narzędzi" – zobaczysz tam dokładnie, jaki JSON został wysłany i co odpowiedział BricsCAD.
- **Zakładka Debug (🐛)**: Zaawansowane narzędzie diagnostyczne. Pozwala śledzić komunikację na linii Agent -> C# -> Silnik BricsCAD (zdarzenia bazy Teigha). Używaj jej, gdy narzędzia "udają", że coś zrobiły, ale zmiany nie są widoczne na ekranie.

---
> [!IMPORTANT]
> **Bezpieczeństwo**: Agent V2 wykonuje większość operacji wewnątrz transakcji. Jeśli wystąpi błąd krytyczny, system spróbuje wycofać zmiany (Rollback), aby nie uszkodzić rysunku.

*Wersja Systemu: v2.20.4 GOLD | BricsCAD Agent AI Project*

---

## 🚀 10. Recepty (System Drogowskazów) - NOWOŚĆ v2.16.0

System receptur pozwala na tworzenie "skrótów myślowych" dla Agenta. Zamiast tłumaczyć mu za każdym razem jak ma coś narysować, możesz stworzyć recepturę wywoływaną specjalnym znakiem `$`.

### 10.1. Jak używać znaku `$`?
Wpisz `$` a następnie nazwę wyzwalacza, aby "pokazać" Agentowi jak ma wykonać dane zadanie.
- Przykład: *"Zrób to używając $kopiuj_warstwe"*
- Agent zobaczy Twoją zapisaną wcześniej instrukcję oraz poprawny ciąg wywołań narzędzi, co gwarantuje 100% precyzji.

### 10.2. Tworzenie Przepisów (Capture)
Najszybszym sposobem na stworzenie przepisu jest przechwycenie udanej sesji:
1. Pracuj z Agentem w zakładce **Aktualna sesja**, aż osiągniesz pożądany efekt.
2. Kliknij przycisk **"✨ Przechwyć jako Przepis"**.
3. System automatycznie przeniesie Cię do zakładki **Recepty**, parsując sesję i wyciągając z niej same wywołania narzędzi.
4. Nadaj przepisowi nazwę (np. `duplikuj_osie`) i kliknij **Zapisz**.

### 10.3. Tworzenie od podstaw (v2.18.0)
Możesz teraz tworzyć recepty bez interakcji z Agentem:
1. Przejdź do zakładki **Recepty**.
2. Kliknij **"➕ Nowa Recepta"** i podaj nazwę wyzwalacza.
3. Wpisz instrukcję JSON lub uzupełnij przepis korzystając z **Tool Sandboxa**.

### 10.4. Integracja z Tool Sandboxem
Podczas testowania narzędzi w Sandboxie możesz w dowolnej chwili wysłać skonfigurowane wywołanie do biblioteki receptur:
1. Kliknij **"✨ Wyślij do Recepty"** w Sandboxie.
2. Wybierz z menu istniejącą receptę (program dopisze narzędzie na końcu sekwencji) lub stwórz nową.

### 10.5. Kategoryzacja Receptur
W edytorze receptur możesz zaznaczyć kategorie narzędzi (np. `#warstwy`, `#bloki`), które mają zostać automatycznie załadowane do pamięci podręcznej Agenta w momencie użycia przepisu. Eliminuje to potrzebę ręcznego wpisywania tagów przy każdym zapytaniu.

### 10.6. Natychmiastowe Wykonanie ($trigger$) - v2.17.0
Wersja 2.17.0 wprowadza "Tryb Makra", który pozwala na wykonanie przepisu bez angażowania sztucznej inteligencji.
- **Składnia**: Zamknij nazwę triggera w dwa znaki dolara, np. `$duplikuj_warstwe$`.
- **Działanie**: Program od razu wykona zapisaną sekwencję, co jest idealne dla często powtarzanych, pewnych czynności technicznych.

### 10.7. Eksport do Złotego Standardu (v2.19.0)
Jeśli Twoja recepta działa idealnie, możesz ją "ozłocić", czyli dodać jako idealny przykład treningowy do bazy wiedzy AI:
1. W zakładce **Recepty** wybierz przepis i kliknij **"✨ Złoty Standard"**.
2. Podaj zapytanie użytkownika (np. "Wstaw okno i osie").
3. Opcjonalnie dodaj dodatkowe tagi (kategorie narzędzi), które AI powinno mieć w pamięci podczas nauki tego przykładu.
4. Program skompiluje pełny rekord JSONL (System Prompt + Narzędzia + Konwersacja) i dopisze go do pliku treningowego.

### 10.8. Testowanie i Debugowanie Receptur
W zakładce **Recepty** znajdziesz dwa tryby weryfikacji:
1. **🧪 Testuj w Sandboxie**: Przesyła wybrany krok receptury do Tool Sandboxa. Jeśli przepis ma wiele kroków, program zapyta Cię, który z nich chcesz przetestować.
2. **🚀 Testuj Sekwencję**: Uruchamia cały przepis natychmiast w BricsCAD. W przypadku błędu w składni JSON lub błędnego działania narzędzia, system wyświetli szczegółowy log z diagnozą i sugestią poprawki.
