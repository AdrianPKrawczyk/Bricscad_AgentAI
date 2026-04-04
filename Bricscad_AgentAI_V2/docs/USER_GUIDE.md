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

## Wskazówki
- Używaj przecinków do oddzielania współrzędnych (np. `10,20,0`).
- Możesz odwoływać się do zmiennych używając `@` (jeśli zostały wcześniej zdefiniowane).
