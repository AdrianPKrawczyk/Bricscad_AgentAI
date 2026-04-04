# Podręcznik Użytkownika BricsCAD Agent AI (V2)

## Wprowadzenie
Witaj w nowej wersji Agenta AI dla BricsCAD! System V2 oferuje większą stabilność, precyzję oraz zaawansowane funkcje obliczeniowe.

## Kluczowe Funkcje

### 1. Wyszukiwanie Obiektów (SelectEntities)
Umożliwia inteligentne filtrowanie i zaznaczanie obiektów istniejących na rysunku. Zawsze działa w powiązaniu z "Pamięcią Agenta", pozwalając na swobodne zarządzanie zaznaczonymi elementami.
- **Co robi:** Znajduje elementy o konkretnych klasach (np. teksty, linie) i właściwościach (kolor, warstwa).
- **Jak o to zapytać:** "Zaznacz wszystkie linie na warstwie 'OSIE'" lub "Dodaj do zaznaczenia teksty o wysokości 2.5".
- **Wymagane dane:** Zazwyczaj musisz określić jakiego typu elementu szukasz (np. "wszystkie obiekty", "*Line", "Tekst").

### 2. Tworzenie Obiektów (CreateObject)
Agent potrafi rysować podstawowe elementy graficzne. Możesz poprosić go o:
- **Linie:** "Narysuj linię od 0,0 do 100,100"
- **Okręgi:** "Stwórz okrąg w punkcie 50,50 o promieniu 20"
- **Teksty (MText/DBText):** "Napisz 'Projekt łazienki' w punkcie 10,10"
- **Multileadery:** "Dodaj opis 'Przyłącze wody' ze strzałką w 0,0 i tekstem obok"

### 3. Modyfikacja Właściwości (ModifyProperties)
Możesz zmieniać właściwości ujętych w pamięci/wyselekcjonowanych elementów wspierając zaawansowane zależności. Pamiętaj, że wpierw musisz zaznaczyć docelową grupę obiektów za pomocą komendy z pkt 1, bądź polecenie zostanie zaaplikowane na świeżo narysowanym obiekcie.
- **Co robi:** Zmienia parametry wybranych wcześniej obiektów na podane w prompt. W przypadku pomyłek pomija niezgodne pojęcia (np Promień dla Linii).
- **Jak o to zapytać:** "Zmień kolor na niebieski a warstwę na Instalacje". "Obróć tekst o 15 stopni".
- **Wykorzystanie matematyki na starych wartościach (RPN):** Kiedy chcesz zmienić obecną liczbę np. o 10. Powiedz "Zwiększ promień zaznaczonych okręgów o 10" lub opisz mu z użyciem frazy "Użyj RPN dla starego promienia $OLD_RADIUS dodając 10" by go poinstruować. Np. "Zmień grubość wszystkich linii by była o 2.5 mniejsza od obecnej". Przykładowo Agent stworzy wyrażenie `RPN: $OLD_LINEWEIGHT 2.5 -`.

### 4. Zarządzanie Strukturą (Warstwy)
Narzędzie `ManageLayers` pozwala na pełną kontrolę nad warstwami projektu, w tym operacje masowe dzięki użyciu gwiazdki (*).
- **Tworzenie:** "Stwórz nową warstwę KONSTRUKCJA o kolorze czerwonym".
- **Modyfikacja masowa:** "Zmień kolor wszystkich warstw zaczynających się od INST_ na niebieski" (Agent użyje maski `INST_*`).
- **Usuwanie:** "Usuń warstwę POMOCNICZA". Jeśli warstwa zawiera obiekty, Agent poinformuje Cię o tym, zamiast usuwać je na siłę.
- **Zasady:** Warstwy "0" oraz "Defpoints" są chronione i nie mogą zostać usunięte.

### 5. Silnik Obliczeniowy RPN
Możesz wykonywać obliczenia bezpośrednio w poleceniach używając prefiksu `RPN:`. Format ten używa Odwrotnej Notacji Polskiej (liczby idą przed operatorem).

**Przykłady:**
- "Narysuj okrąg o promieniu RPN: 5 5 +" (stworzy okrąg o promieniu 10)
- "Dodaj tekst 'Wynik: RPN: 100 2 /'" (wstawi tekst 'Wynik: 50.0')

### 6. Interakcja AskUser
Jeśli nie znasz współrzędnych lub wymiarów, możesz kazać Agentowi zapytać Ciebie o nie na rysunku.
- **Przykład:** "Narysuj okrąg w centrum AskUser" -> Agent zatrzyma się i poprosi Cię o kliknięcie punktu w BricsCAD.

### 7. Pamięć Agenta i Automatyczne Zaznaczanie
Każdy nowy obiekt, który stworzysz, zostaje automatycznie dodany do "pamięci podręcznej" Agenta.
- **Flow pracy:** 
  1. Ty: "Narysuj kwadrat z linii"
  2. Agent: (Rysuje 4 linie i zaznacza je)
  3. Ty: "Zmień kolor na czerwony" -> Agent wie, że chodzi o te linie, które przed chwilą narysował.

### 8. Makra i Skrypty Automatyzacji
Agent potrafi wywoływać gotowe procedury oraz interpretować skrypty LISP.
- **Wywołanie makra:** "Uruchom czyszczenie rysunku" lub "Odpierwszuj warstwy" (Agent użyje makr `CleanDrawings` lub `ResetLayers`).
- **Skrypty LISP:** "Napisz i uruchom lisp, który zamieni wszystkie okręgi na kwadraty".
- **Błędy:** Jeśli skrypt ma błąd składni, Agent otrzyma raport z BricsCAD i poinformuje Cię o tym, co poszło nie tak.

### 9. Odczyt i Analiza Właściwości
Agent potrafi "czytać" i podsumowywać właściwości (takie jak kolor, warstwa, geometria) zaznaczonych obiektów z poziomu pamięci.
- **Zarządzanie Atrybutami (Dane dynamiczne):** "Wypełnij numerację pomieszczeń w tabelce od 101" lub "Odczytaj cenę z atrybutu CENA tych bloków" (Agent użyje `EditAttributesTool`). Pozwala to na precyzyjną edycję konkretnych wstawień bloków bez modyfikowania ich definicji (np. każde pomieszczenie może mieć inny numer).
- **Biblioteka Bloków:** "Pokaż jakie boki mamy w tym rysunku" (Agent użyje `ListBlocksTool`), "Wstaw blok 'Biurko' w punkcie 0,0" (Agent użyje `InsertBlockTool`) lub "Stwórz blok o nazwie 'Meble' z zaznaczenia" (Agent użyje `CreateBlockTool`). Agent panuje nad pełnym cyklem życia bloku: od odczytu dostępnych definicji, przez tworzenie własnych, aż po ich wstawianie z automatycznym wypełnianiem atrybutów.
- **Modyfikacja Wnętrza Bloku (Geometria):** "Zmień kolor wszystkich linii na czerwony w bloku 'Rama_Okna'" lub "Usuń wymiary z wnętrza tego bloku" (Agent użyje `EditBlockTool`). To unikalna funkcja pozwalająca na masową edycję definicji bloków bez ich rozbijania, co zachowuje strukturę i porządek w rysunku.
- **Odczyt bazowy:** "Jakie parametry mają te zaznaczone linie?" (Agent zwróci podstawowe informacje o maksymalnie 15 obiektach używając `GetPropertiesTool`).
- **Precyzyjny odczyt i Pamięć:** "Odczytaj długość tych linii i zapisz jako @Dlugosci" (Agent użyje `ReadPropertyTool`). Ta funkcja pozwala na wyciągnięcie konkretnej wartości (np. pola powierzchni, punktu środkowego) i zapamiętanie jej do późniejszego wykorzystania w obliczeniach RPN.
- **Edycja i Formatowanie Tekstu:** "Zamień słowo 'Projekt' na 'Budowa' in zaznaczeniu" lub "Podświetl na czerwono i pogrub słowo 'BŁĄD' w tekstach" (Agent użyje `TextEditTool`). Narzędzie to potrafi masowo modyfikować treść rysunku oraz nakładać formatowanie RTF na obiekty `MText`, co pozwala na wizualne raportowanie błędów bezpośrednio w CAD.

### 10. Interakcja i Konsultacje
Agent nie zawsze musi działać w pełni automatycznie. Dzięki nowym mechanizmom kontroli, może "zatrzymać się" i poprosić Cię o pomoc lub decyzję:
- **Pytania o dane:** Agent może zapytać w linii komend: "[KONSULTACJA AI] Podaj szerokość ściany:". Możesz wtedy wpisać wartość, która zostanie przekazana do dalszych obliczeń (np. do RPN).
- **Wskazywanie na ekranie:** Agent może poprosić: "[KONSULTACJA AI] Wskaż punkt bazowy dla otworu:". Wskaż punkt myszką na rysunku, a Agent odczyta jego współrzędne.
- **Wybór z opcji:** Jeśli Agent ma kilka wariantów działania, wyświetli je w linii komend (np. `[Stal/PCV/Miedź]`). Możesz kliknąć w opcję lub wpisać jej nazwę.
- **Przerwanie (ESC):** Zawsze możesz przerwać oczekiwanie Agenta klawiszem ESC – AI otrzyma wtedy sygnał o anulowaniu i przejdzie do kolejnego zadania lub zakończy pracę.

### 11. Przetwarzanie List i Pamięć
Agent potrafi zapamiętywać wyniki swoich działań (zmienne `@Nazwa`) i analizować je jako listy:
- **Rozpakowywanie list:** Jeśli Agent pobierze listę warstw lub bloków, może użyć `ForeachTool`, aby wyświetlić Ci je w formie czytelnej, numerowanej listy. Dzięki temu łatwiej jest wydać polecenie dotyczące konkretnego elementu z długiego spisu.
- **Zliczanie:** Możesz zapytać: "Ile unikalnych warstw jest w tym rysunku?", a Agent użyje trybu `Count`, aby podać Ci samą liczbę.
- **Skale Opisowe (Annotative):** "Dodaj skalę 1:50 do tych wymiarów" lub "Wyłącz opisowość dla tych tekstów" (Agent użyje `ManageAnnoScalesTool`). Dzięki temu Agent panuje nad widocznością i wielkością opisów w różnych rzutniach (viewports), co jest kluczowe przy przygotowywaniu arkuszy (layouts).
- **Agregacja i Statystyka:** "Zlicz typy obiektów w zaznaczeniu" lub "Pokaż unikalne warstwy w tym rysunku" (Agent użyje `AnalyzeSelectionTool`). To potężny kombajn do szybkiego przeglądu dużych zbiorów danych bez konieczności czytania każdego obiektu z osobna.
- **Próbkowanie tekstu:** "Pobierz próbkę tekstów z tego obszaru" (Agent użyje `ReadTextSampleTool`). Narzędzie to "wyskubie" reprezentatywne błędy/opisy z dużej liczby tekstów, co pozwala Agentowi zrozumieć kontekst opisu rysunku (np. numery działek, nazwy pomieszczeń) bez ryzyka przepełnienia pamięci.
- **Właściwości wirtualne:** Możesz pytać o rzeczy, których nie ma wprost we właściwościach CAD, np. `MidPoint` (środek linii/łuku) czy `Centroid` (środek ciężkości bryły).

## Interfejs Agenta (V2 GOLD)
Interfejs został zoptymalizowany pod kątem szybkości i diagnostyki:
- **Pasek HUD:** Na dole okienka czatu znajdziesz informację o aktualnie używanym modelu AI (np. Gemini 3 Flash lub local-model).
- **Logi Narzędzi:** W zakładce "Logi Narzędzi" widzisz surowe dane JSON przesyłane do Agenta. Użyj przycisku **"Kopiuj do schowka"**, aby szybko przesłać logi do pomocy technicznej w razie błędu.
- **Asynchroniczność:** Możesz swobodnie przesuwać widok w BricsCAD, podczas gdy Agent analizuje Twoje zapytanie.

## Wskazówki
- Używaj przecinków do oddzielania współrzędnych (np. `10,20,0`).
- Możesz odwoływać się do zmiennych używając `@` (jeśli zostały wcześniej zdefiniowane).
- System automatycznie pilnuje długości rozmowy, przycinając długie dane w tle (**TrimHistory**), co zapewnia stabilność przy bardzo długiej pracy.

## Dokumentacja Referencyjna
Dla zaawansowanych użytkowników i administratorów przygotowaliśmy szczegółowe katalogi techniczne:
- [Katalog Poleceń CAD (BricsCAD Commands)](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/docs/COMMANDS_REFERENCE.md) – Lista komend do wpisania w pasku poleceń.
- [Encyklopedia Narzędzi AI (AI Tools Reference)](file:///d:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/docs/TOOLS_REFERENCE.md) – Szczegółowa specyfikacja możliwości technicznych Agenta.

