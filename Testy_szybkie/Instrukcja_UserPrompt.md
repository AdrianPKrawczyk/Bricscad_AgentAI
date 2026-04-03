Ten dokument opisuje zasady tworzenia zapytań (`UserPrompt`) do zautomatyzowanych testów. Celem jest minimalizacja halucynacji i wymuszenie na modelu generowania wyłącznie technicznych tagów akcji.

## 1. Dlaczego standardowe pytania zawodzą?
Modele LLM (szczególnie mniejsze, jak 7B) domyślnie starają się być pomocne i rozmowne. W teście automatycznym każda dodatkowa sekcja `[MSG]` lub wyjaśnienie tekstowe jest traktowane jako szum, który może doprowadzić do oblania walidacji JSON lub RPN.

---

## 2. Anatomia Skutecznego Polecenia Testowego

Dobre polecenie testowe powinno składać się z trzech sekcji: **Zadanie**, **Parametry Techniczne** oraz **Restrykcje Formatowania**.

### Przykład struktury:
> **[Zadanie]** + **[Parametry]** + **[ZAKAZY/NAKAZY]**

### W praktyce:
`"Wygeneruj tag INSERT_BLOCK. Nazwa bloku: 'Zawór'. Punkt: 100,200. ZAKAZ używania tagu [MSG]. Odpowiedz wyłącznie tagiem [ACTION]."`

---

## 3. Złote Zasady "Hard Promptingu"

### A. Wymuszanie konkretnego narzędzia
Jeśli testujesz, czy model odróżnia wstawianie bloku od rysowania obiektu, wprost nazwij tag, którego oczekujesz.
* **Źle:** "Wstaw blok silnika w 0,0."
* **Dobrze:** "Wygeneruj tag **[ACTION:INSERT_BLOCK]** dla bloku 'Silnik' w punkcie 0,0."

### B. Kontrola zmiennych (Zasada $ITEM1)
Aby uniknąć błędów indeksowania (które widzieliśmy w testach RPN), narzuć modelowi nazwy zmiennych.
* **Instrukcja:** "Użyj zmiennej **$ITEM1** do obliczeń, nie używaj $ITEM2 ani innych indeksów."

### C. Blokada "Gadatliwości" (No-Chat Constraint)
To najważniejszy element testów automatycznych. Model musi wiedzieć, że każda litera poza tagiem jest błędem.
* **Frazy klucze do dopisania na końcu każdego testu:**
    * `ZAKAZ UŻYWANIA TAGU [MSG].`
    * `ODPOWIEDZ WYŁĄCZNIE CZYSTYM TAGIEM.`
    * `NIE WYJAŚNIAJ SWOJEGO DZIAŁANIA.`

---

## 4. Szablony dla Kategorii Testowych

### 1. Testy Wyboru Narzędzia (ToolSelection)
Sprawdzają, czy model stosuje reguły specjalne (np. zakaz `CREATE_OBJECT` dla bloków).
* **Prompt:** "Wstaw element 'Pompa' (BlockReference) w punkcie 10,10. Zastosuj się do reguły dotyczącej bloków. Odpowiedz tylko tagiem [ACTION]."

### 2. Testy Zmiennych i Łańcuchów (Variables/Chains)
Sprawdzają, czy model potrafi zapisać dane do pamięci.
* **Prompt:** "Zaznacz wszystkie linie. Odczytaj ich długości i zapisz je do zmiennej globalnej o nazwie **@MojeLinie**. Nie wykonuj dalszych akcji."

### 3. Testy Wiedzy o API (Logic)
Sprawdzają znajomość specyficznych operatorów (np. `IFEMPTY`).
* **Prompt:** "Zmień tekst zaznaczonych wymiarów. Użyj RPN: pobierz aktualny tekst, jeśli jest pusty zastąp go **'NOT_SET'** używając operatora **IFEMPTY**. Tylko tag [ACTION]."

---

## 5. Checklist przed dodaniem testu do JSON

Zanim dodasz test do pliku `Benchmark.json`, sprawdź:
1.  [ ] Czy w `UserPrompt` podałeś konkretną nazwę zmiennej do `MockData`?
2.  [ ] Czy dopisałeś zakaz używania `[MSG]`?
3.  [ ] Czy `ExpectedOutput` w regule RPN uwzględnia spacje (np. `' m2'` vs `'m2'`)?
4.  [ ] Czy polecenie nie jest zbyt złożone? (Lepiej zrobić dwa oddzielne testy niż jeden gigantyczny łańcuch).

---

### Pro Tip: Symulacja Kontekstu
Jeśli testujesz krok nr 3 w łańcuchu (np. pętlę `FOREACH`), użyj pola `SimulatedCADResponses` w JSON, aby "nakarmić" model danymi, które udają, że krok 1 i 2 już się odbyły. To pozwoli Ci testować logikę pętli bez konieczności przechodzenia przez cały proces zaznaczania za każdym razem.

---

Ta instrukcja pozwoli Ci budować powtarzalne testy, które faktycznie mierzą inteligencję modelu, a nie jego umiejętność prowadzenia pogawędek. Czy chcesz, abym przygotował teraz paczkę 10 takich "twardych" testów do Twojego nowego benchmarku?