Ten dokument opisuje metodologię tworzenia zautomatyzowanych testów sprawdzających logikę **Reverse Polish Notation (RPN)** w module `AutoBenchmark`.

## 1. Cel testowania RPN
Testy RPN nie sprawdzają tylko, czy model "wie co to RPN", ale weryfikują:
1. **Składnię:** Czy model poprawnie układa operatory na stosie (np. `ABS` po liczbie).
2. **Adresowanie:** Czy model używa poprawnych zmiennych (np. `$ITEM1` vs `$ITEM2`).
3. **Formatowanie:** Czy model poprawnie łączy jednostki i zaokrąglenia.

---

## 2. Struktura testu w formacie JSON

Każdy test RPN musi zawierać regułę typu `EvaluateRPN`.

```json
{
  "Id": [numer],
  "Category": "RPN_Logic",
  "TestName": "[Krótka nazwa]",
  "UserPrompt": "[Instrukcja dla AI - patrz sekcja 3]",
  "ValidationRules": [
    {
      "RuleType": "EvaluateRPN",
      "MockData": {
        "$ITEM1": "wartość_testowa_1",
        "$ITEM2": "wartość_testowa_2"
      },
      "ExpectedOutput": "dokładny_oczekiwany_string_wynikowy",
      "ErrorMessage": "Komunikat w razie błędu"
    }
  ]
}
```

---

## 3. Konstruowanie polecenia (UserPrompt) - "Hard Prompting"

Modele klasy 7B mają tendencję do "gadania" o zadaniu zamiast jego wykonywania. Aby test był miarodajny, stosuj zasadę **wymuszenia formatu**:

### Zła praktyka:
> "Napisz wzór RPN na pole powierzchni dla $ITEM1."

### Dobra praktyka (Wzorzec):
> "Wygeneruj tag `[ACTION:CREATE_OBJECT]`. W parametrze 'Text' ułóż równanie RPN: weź zmienną **$ITEM1**, nałóż na nią **ABS**, zaokrąglij do **2 miejsc** i dodaj jednostkę **' m2'**. ZAKAZ UŻYWANIA TAGU [MSG]. Odpowiedz wyłącznie tagiem [ACTION]."

**Kluczowe elementy polecenia:**
* **Wskazanie zmiennej:** Zawsze wymuszaj konkretną nazwę (np. `$ITEM1`), aby uniknąć błędów indeksowania.
* **Zakaz tagu [MSG]:** Zapobiega halucynacjom, w których model tłumaczy co robi, zamiast generować kod.
* **Specyfikacja jednostki:** Określ, czy używasz `m2` czy `m²`.

---

## 4. Konfiguracja MockData i ExpectedOutput

To serce walidacji matematycznej.

1.  **MockData:** Tu definiujesz "wirtualne" dane z CAD. Jeśli w poleceniu kazałeś użyć `$ITEM1`, w MockData musi istnieć klucz `$ITEM1`.
2.  **ExpectedOutput:** To string, który **musi** zwrócić Twój silnik `RpnCalculator` po przetworzeniu wzoru AI.
    * *Uwaga na spacje:* Jeśli AI wygeneruje `' m2' CONCAT`, wynik to `25.00 m2`. Jeśli wygeneruje `'m2' CONCAT`, wynik to `25.00m2`. Bądź precyzyjny w oczekiwaniach.

---

## 5. Najczęstsze błędy i ich interpretacja

| Błąd w raporcie | Co to oznacza? | Jak naprawić? |
| :--- | :--- | :--- |
| `Nie znaleziono wzoru zaczynającego się od 'RPN:'` | Model wysłał tekst zamiast tagu akcji. | Zaostrz UserPrompt (DODAJ ZAKAZ [MSG]). |
| `Brak zmiennej: $ITEM2` | Model użył innej zmiennej niż podałeś w MockData. | Sprawdź, czy model nie "przesunął" indeksu. |
| `Błąd obliczeń. Oczekiwano 'X', a otrzymano 'Y'` | Logika RPN jest błędna (np. zła kolejność operatorów). | Sprawdź `GeneratedTag` w raporcie, by zobaczyć wzór. |
| `Błąd przy '...': Nieznana funkcja` | Model wymyślił operator, którego nie ma w `RpnCalculator.cs`. | Wzmocnij trening modelu lub dodaj operator do C#. |

---

## 6. Przykładowy kompletny test (do skopiowania)

```json
{
  "Id": 99,
  "Category": "RPN_Logic",
  "TestName": "Walidacja Ciśnienia",
  "UserPrompt": "Wygeneruj tag CREATE_OBJECT. W polu Text użyj RPN: zmienna $ITEM1, pomnóż przez 10, dodaj ' Pa' i odpowiedz TYLKO tagiem [ACTION].",
  "ValidationRules": [
    {
      "RuleType": "EvaluateRPN",
      "MockData": { "$ITEM1": "15" },
      "ExpectedOutput": "150 Pa",
      "ErrorMessage": "Błąd przeliczania ciśnienia."
    }
  ]
}
```

---

### Pro tip dla dewelopera:
Zawsze po nieudanym teście sprawdzaj pole `GeneratedTag` w pliku raportu. To tam znajdziesz dowód na to, czy model "płynął" w rozmowie, czy po prostu pomylił się w matematyce.

---

Co o tym sądzisz? Czy taka instrukcja jest dla Ciebie jasna, czy dodać jeszcze sekcję o testowaniu zmiennych globalnych (tych z `@`)?