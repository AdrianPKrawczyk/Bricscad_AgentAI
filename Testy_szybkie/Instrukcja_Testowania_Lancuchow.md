Oto szczegółowa instrukcja w formacie Markdown, która wyjaśnia, jak projektować testy wieloetapowe (łańcuchy) dla Agenta. Jest to kluczowy dokument, ponieważ testowanie łańcuchowe pozwala sprawdzić "pamięć operacyjną" modelu i jego zdolność do adaptacji na podstawie danych zwracanych przez system CAD.

Możesz zapisać ten plik jako `Instrukcja_Testowania_Lancuchow.md`.

---

# 🔗 Instrukcja Projektowania Testów Łańcuchowych (Multi-Turn)

Ten dokument opisuje, jak tworzyć zaawansowane scenariusze testowe w pliku JSON, które symulują pełną interakcję między użytkownikiem, Agentem AI a systemem BricsCAD.

## 1. Koncepcja Łańcucha (Chain)

W rzeczywistej pracy Agent rzadko wykonuje zadanie w jednym kroku. Typowy proces to:
1. **UserPrompt:** Użytkownik zadaje pytanie.
2. **AI Step 1:** Agent prosi o zaznaczenie obiektów (`SELECT`).
3. **CAD Response 1:** System CAD informuje, ile obiektów zaznaczono.
4. **AI Step 2:** Agent odczytuje właściwości (`READ_PROPERTY`).
5. **CAD Response 2:** System CAD zwraca wartości (np. promienie, pola).
6. **AI Step 3:** Agent wykonuje finalną akcję (`FOREACH`, `CREATE_OBJECT`).

---

## 2. Kluczowe pole: `SimulatedCADResponses`

To pole w pliku JSON udaje "głos" BricsCADa. Silnik benchmarku automatycznie przesyła te wiadomości do Agenta po każdym wygenerowanym przez niego tagu.

### Zasady działania:
* Liczba wiadomości w `SimulatedCADResponses` powinna odpowiadać liczbie kroków pośrednich.
* Każda wiadomość musi zaczynać się od słowa **`WYNIK:`**, aby Agent wiedział, że otrzymuje dane systemowe.

---

## 3. Walidacja sekwencji: `SequenceMatch`

Aby sprawdzić, czy model zachował poprawną logikę i nie próbował np. rysować przed zaznaczeniem obiektów, używamy reguły `SequenceMatch`.

* **Value:** Przecinkami oddzielone nazwy tagów, które muszą wystąpić w tej kolejności.
* **Przykład:** `"Value": "SELECT,READ_PROPERTY,FOREACH"`

---

## 4. Szablon Testu Łańcuchowego

Oto jak powinien wyglądać wzorcowy wpis w JSON dla zadania wieloetapowego:

```json
{
  "Id": 10,
  "Category": "ActionChains",
  "TestName": "Złożony opis polilinii",
  "UserPrompt": "Zaznacz zamknięte polilinie. Pobierz ich warstwy i pola powierzchni. Na koniec opisz każdą z nich tekstem na warstwie 'Opisy'.",
  "SimulatedCADResponses": [
    "WYNIK: Zaznaczono 3 obiekty.",
    "WYNIK: @Warstwy = '0' | 'Konstrukcja' | 'Nawiew' \nWYNIK: @Pola = 150.5 | 200.0 | 50.0"
  ],
  "ValidationRules": [
    {
      "RuleType": "SequenceMatch",
      "Value": "SELECT,READ_PROPERTY,FOREACH",
      "ErrorMessage": "Model pominął etap odczytu danych lub zaznaczania."
    },
    {
      "RuleType": "MustContain",
      "Value": "\"Layer\": \"Opisy\"",
      "ErrorMessage": "Model nie uwzględnił wymogu warstwy 'Opisy' w finalnym obiekcie."
    }
  ]
}
```

---

## 5. Dobre praktyki tworzenia łańcuchów

### A. Symulowanie "pamięci" (@zmienne)
W `SimulatedCADResponses` zawsze podawaj dane w formacie, który Agent rozumie (używając małpki `@` i separatora `|`).
* **Dobrze:** `WYNIK: @Promienie = 10 | 20 | 30`
* **Źle:** `Masz promienie 10, 20 i 30.` (Model może nie rozpoznać tego jako danych do pętli).

### B. Testowanie reakcji na błędy
Możesz symulować sytuację, w której CAD nic nie znalazł, aby sprawdzić czy Agent potrafi przeprosić lub zmienić strategię.
* **Prompt:** "Usuń wszystkie bloki 'Pompa'."
* **SimulatedCADResponse:** `WYNIK: Nie znaleziono obiektów spełniających kryteria.`
* **ValidationRule:** `MustContain: [MSG:` (Sprawdza, czy model odpowiedział tekstem, a nie tagiem akcji).

### C. Łączenie z RPN
W testach łańcuchowych warto dodawać regułę `EvaluateRPN` w ostatnim kroku, aby upewnić się, że dane wstrzyknięte w `SimulatedCADResponses` zostały poprawnie przetworzone matematycznie w `FOREACH`.

---



Z taką instrukcją jesteśmy gotowi do budowy **Bazy Testowej (Master Benchmark)**. Chcesz, abym zaproponował teraz listę 10-15 konkretnych scenariuszy (z podziałem na kategorie), które wypełnią Twój pierwszy duży plik testowy?