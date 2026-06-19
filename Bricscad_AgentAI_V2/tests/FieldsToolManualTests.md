# Testy manualne - Narzędzia pól CAD (ReadFields, ManageFields)

Zestaw poleceń do wklejania agentowi w zakładce **Agent-Czat** lub głównym
panelu agenta w celu weryfikacji działania narzędzi obsługi pól CAD w BricsCAD V22.

> **Konwencja pól DWG:**
> - `%<\AcVar "NAZWA">` — zmienna systemowa (np. `DWGNAME`, `SAVENAME`)
> - `%<\AcDate \format>` — data/godzina (np. `\yyyy-MM-dd`, `\HH:mm`)
> - `%<\AcExpr wyrazenie>` — wyrażenie arytmetyczne (np. `10*5`, `1+2`)
> - `%<\AcDim \Właściwość>` — właściwość wymiaru
> - `%<\AcObjProp Object.\Warstwa>` — właściwość obiektu
>
> **Ważne:** BricsCAD V22 może wyświetlać pola w dwóch formach:
> - **Czytelna** (`%<\AcVar "DWGNAME">`) — po `_.REGEN`.
> - **Binarna** (`%<\_FldIdx 0ec>`) — przed `_.REGEN` lub gdy pole ma
>   niestandardowy ewaluator. `ReadFields` rozpoznaje obie formy;
>   w formie binarnej kategoria to `BinaryField`.

---

## 0. Setup środowiska

W panelu **BricsCAD** wykonaj kolejno:

```
(load "ścieżka/do/Bricscad_AgentAI_V2/tests/generate_fields_test_model.lsp")
(load "ścieżka/do/Bricscad_AgentAI_V2/tests/verify_fields_tests.lsp")
GEN_FIELDS_TESTS
```

Rysunek powinien zawierać:

| # | Typ | Zawartość |
|---|-----|-----------|
| 1 | MText | `Projekt: %<\AcVar "DWGNAME">` |
| 2 | DBText | `Data: %<\AcDate \yyyy-MM-dd>` |
| 3 | MText | `Wynik: %<\AcExpr 10*5>` |
| 4 | MText | `Poczatek: %<\AcVar "DWGNAME"> | Srodek: %<\AcDate \HH:mm> | Koniec` |
| 5 | MText | `Ten tekst NIE zawiera pol CAD.` |
| 6 | BlockRef `Bielik_Pole_Tytul` x2 | atrybut MText `TYTUL` z `%<\AcVar "DWGNAME">` |
| 7 | BlockRef `Bielik_Pole_Etykieta` | atrybut DBText `NUMER` bez pól |

---

## 1. ReadFields - inspekcja pól w zaznaczeniu

### 1.1. Wykrywanie kategorii pól

```
Wylistuj wszystkie pola CAD w zaznaczonych obiektach.
```

**Oczekiwany rezultat:**
- Powinny zostać wykryte obiekty 1, 2, 3, 4 i oba `Bielik_Pole_Tytul` (6).
- Obiekt 5 (czysty tekst) powinien być pominięty.
- Każdy wpis powinien zawierać: `EntityType`, `EntityId`, `Category`, `Code`, `ResolvedValue`.

### 1.2. Filtrowanie po kategorii

```
Pokaz tylko pola kategorii DateTime.
```

**Oczekiwany rezultat:**
- Powinien pojawić się tylko obiekt 2 (DBText z `%<\AcDate \yyyy-MM-dd>`).
- Inne kategorie powinny być odfiltrowane.

### 1.3. Zapis raportu do pamięci

```
Wylistuj wszystkie pola i zapisz raport jako FieldsSummary.
```

**Oczekiwany rezultat:**
- W odpowiedzi powinien pojawić się wiersz `ZAPISANO W PAMIECI JAKO @FieldsSummary`.
- Kolejne wywołanie z `@FieldsSummary` (np. w prompcie) powinno odwołać się do tej listy.

### 1.4. Inspekcja atrybutów bloku

```
Wylistuj pola w zaznaczonych instancjach bloku Bielik_Pole_Tytul.
```

(Wcześniej: `SelectEntities(Name="Bielik_Pole_Tytul")`)

**Oczekiwany rezultat:**
- Każda instancja powinna raportować pole `%<\AcVar "DWGNAME">` w atrybucie `TYTUL`.
- `Bielik_Pole_Etykieta` nie powinien być w raporcie (atrybut DBText bez pól).

---

## 2. ManageFields.InsertField

### 2.1. Wstawianie pola na końcu tekstu

```
Wstaw pole z aktualną datą na końcu tekstu "Ten tekst NIE zawiera pol CAD.".
```

(`SelectEntities` na obiekcie 5)

**Oczekiwany rezultat:**
- Tekst MText powinien teraz wyglądać: `Ten tekst NIE zawiera pol CAD.%<\AcDate \yyyy-MM-dd>`.
- Raport powinien zwrócić `Wstawiono pole w 1 obiektach`.
- Po komendzie `_.REGEN` wartość pola powinna zostać obliczona (wyświetli się data).

### 2.2. Wstawianie pola na początku

```
Dodaj pole z nazwą pliku na początku tekstu "Wynik: %<\AcExpr 10*5>".
```

**Oczekiwany rezultat:**
- Tekst MText: `%<\AcVar "DWGNAME">Wynik: %<\AcExpr 10*5>`.
- Oba kody pól (istniejący i nowy) powinny być obecne.

### 2.3. Wstawianie pola w atrybut bloku

```
Dla wszystkich instancji bloku Bielik_Pole_Etykieta wstaw pole z nazwą arkusza w atrybucie NUMER.
```

(Wcześniej: `SelectEntities(Name="Bielik_Pole_Etykieta")`)

**Oczekiwany rezultat:**
- Atrybut DBText zostanie zastąpiony tekstem z kodem pola.
- W raporcie powinno pojawić się ostrzeżenie o DBText (jednoliniowy, brak RTF).

### 2.4. Wstawianie pola w atrybut stały

```
Dla wszystkich instancji bloku Bielik_Pole_Tytul wstaw pole w atrybucie TYTUL.
```

**Oczekiwany rezultat:**
- W raporcie powinno pojawić się ostrzeżenie `Atrybut stale ... pominieto`.
- Atrybut nie powinien zostać zmodyfikowany.
- Aby wymusić edycję, powtór z parametrem `IncludeConstantAttributes=true`.

### 2.5. Wstawianie pola z filtrem MatchText

```
Wstaw aktualną datę na końcu tekstu, który zawiera "Wynik:".
```

(Poprzednio: zaznacz kilka MText — `Wynik: ...`, `Projekt: ...`, itp.)

**Oczekiwany rezultat:**
- Zmodyfikowany powinien być tylko MText zawierający `Wynik:`.
- Raport: `Wstawiono pole w 1 obiektach (pominieto N).`
- W raporcie powinna pojawić się linia `Filtrowano po MatchText='Wynik:'.`

### 2.6. Wstawianie pola do konkretnego atrybutu bloku (po tagu)

```
Wstaw nazwę arkusza w atrybucie NUMER bloku Bielik_Pole_Etykieta.
```

(Poprzednio: `SelectEntities(Name="Bielik_Pole_Etykieta")`)

```
ManageFields(
  Action="InsertField",
  FieldCode="%<\\AcFido \"SHEET\">",
  AttributeTagFilter="NUMER",
  BlockNameFilter="Bielik_Pole_Etykieta",
  Targets="Selection"
)
```

**Oczekiwany rezultat:**
- Zmodyfikowany powinien być tylko atrybut `NUMER` w instancji `Bielik_Pole_Etykieta`.
- Raport: `Wstawiono pole w 0 obiektach tekstowych i 1 atrybutach blokow`.
- Pozostałe atrybuty (np. w innych blokach) powinny być pominięte.

---

### 3.1. Konwersja pojedynczego pola

```
Zamień wszystkie pola w zaznaczonym tekście "Wynik: %<\AcExpr 10*5>" na zwykły tekst.
```

**Oczekiwany rezultat:**
- Tekst MText powinien wyglądać: `Wynik: 50` (wartość obliczona).
- `%<\AcExpr>` powinno zniknąć.
- Raport: `Zamieniono pola na tekst w 1 obiektach`.

### 3.2. Konwersja z wieloma polami

```
Zamień wszystkie pola na zwykły tekst w MText z wieloma polami.
```

(Obiekt 4)

**Oczekiwany rezultat:**
- `%<\AcVar>` i `%<\AcDate>` powinny zniknąć.
- Rozdzielacz `|` i otaczający tekst powinny pozostać.

### 3.3. Konwersja w atrybucie bloku

```
Zamień pola na tekst w atrybucie TYTUL instancji Bielik_Pole_Tytul.
```

**Oczekiwany rezultat:**
- Pole `%<\AcVar "DWGNAME">` w atrybucie powinno zostać zastąpione aktualną nazwą pliku.
- Wartość atrybutu powinna być widoczna jako zwykły tekst (bez `%<`).

---

## 4. ManageFields.RemoveField

### 4.1. Usuwanie wszystkich pól z obiektu

```
Usuń wszystkie pola z MText "Projekt: %<\AcVar "DWGNAME">".
```

**Oczekiwany rezultat:**
- Tekst MText: `Projekt: ` (bez `%<`).
- Raport: `Usunieto pole/pola w 1 obiektach`.
- Liczba markerów `%<` w tekście = 0.

### 4.2. Usuwanie pól z wieloma obiektami

```
Usuń wszystkie pola ze wszystkich tekstów w bieżącym layout.
```

(`Targets: AllTextOnLayout`)

**Oczekiwany rezultat:**
- Wszystkie MText/DBText z `%<` powinny zostać oczyszczone.
- Raport: `Usunieto pole/pola w N obiektach`.
- Brak pól zostanie potwierdzony przez `ReadFields` (puste `"Nie wykryto zadnych pol CAD"`).

---

## 5. ManageFields.ReplaceFieldCode

### 5.1. Zamiana kodu pola

```
Zamień kod pola w MText "Projekt: ..." z %<\AcVar "DWGNAME"> na %<\AcVar "SAVENAME">.
```

**Oczekiwany rezultat:**
- Tekst MText: `Projekt: %<\AcVar "SAVENAME">`.
- Po `_.REGEN` wartość pola powinna odpowiadać nazwie zapisu, nie nazwie pliku.

---

## 6. ManageFields.EvaluateAll

### 6.1. Wymuszenie przeliczenia wszystkich pól

```
Wymuś przeliczenie wszystkich pól w rysunku.
```

(`Action: EvaluateAll`)

**Oczekiwany rezultat:**
- Raport: `Wyslano komende _.REGEN do wymuszenia przeliczenia pol`.
- Po chwili wszystkie pola w rysunku powinny wyświetlić aktualne wartości.

---

## 7. Blokady w TextEditTool

### 7.1. Próba nadpisania tekstu z polem

```
Zamień tekst "Projekt: %<\AcVar "DWGNAME">" na "Projekt: TEST".
```

(`TextEditTool` z `Mode: Replace`, `FindText: "Projekt:"`, `ReplaceWith: "Projekt: TEST"`)

**Oczekiwany rezultat:**
- W raporcie powinno pojawić się `[BLOKADA POLA] MText (ID: ...) zawiera pole CAD`.
- Treść MText powinna pozostać niezmieniona.
- Liczba zmodyfikowanych obiektów = 0.

### 7.2. Objeście blokady

```
Zamień tekst "Projekt: %<\AcVar "DWGNAME">" na "Projekt: TEST" i zgadzam się na zniszczenie pola.
```

(`TextEditTool` z `Mode: Replace` + `AllowFieldOverride: true`)

**Oczekiwany rezultat:**
- Treść MText zostanie zastąpiona na `Projekt: TEST`.
- Pole CAD zostanie utracone (nieodwracalnie).

### 7.3. ClearFormatting z polem

```
Wyczyść formatowanie tekstu "Projekt: %<\AcVar "DWGNAME">".
```

(`TextEditTool` z `Mode: ClearFormatting`)

**Oczekiwany rezultat:**
- W raporcie powinno pojawić się `[BLOKADA POLA] ... ClearFormatting nie zostanie wykonany`.
- Formatowanie nie zostanie zmienione.

---

## 8. Blokady w EditBlockTool

### 8.1. EditBlock na bloku z atrybutem zawierającym pole

```
W bloku Bielik_Pole_Tytul znajdz tekst "Projekt" i zamien na "Zlecenie".
```

(`EditBlock` z `Target: ByName`, `BlockName: "Bielik_Pole_Tytul"`, `FindText: "Projekt"`, `ReplaceText: "Zlecenie"`)

**Oczekiwany rezultat:**
- W raporcie powinno pojawić się `[BLOKADA POLA] MText (ID: ...) w bloku 'Bielik_Pole_Tytul'`.
- Atrybut TYTUL nie zostanie zmodyfikowany.
- Liczba zmodyfikowanych obiektów = 0.

### 8.2. EditBlock z AllowFieldOverride

```
W bloku Bielik_Pole_Tytul znajdz tekst "Projekt" i zamien na "Zlecenie" - swiadomie niszcze pole.
```

(`EditBlock` z `AllowFieldOverride: true`)

**Oczekiwany rezultat:**
- Atrybut TYTUL zostanie zmieniony na `Zlecenie projektu: ...`.
- Pole CAD zostanie utracone.

---

## 9. Pipeline end-to-end (scenariusz realistyczny)

```
1. (load "generate_fields_test_model.lsp")
2. GEN_FIELDS_TESTS
3. ReadFields                         # inwentaryzacja - raport powinien zawierac 6 obiektow z polami
4. ReadFields(SaveAs="MojRaport")     # zapis do pamieci
5. ManageFields(InsertField, "%<\AcVar "DWGNAME">", End, Selection)
6. ReadFields                         # powinien wykryc 2x kazde pole w obiekcie 5
7. ManageFields(ConvertToText, Selection)
8. ReadFields                         # obiekt 5 powinien zniknac z raportu
9. ManageFields(EvaluateAll)
10. Verify in BricsCAD: wszystkie pola maja aktualne wartosci
```

---

## 10. Weryfikacja końcowa (asercje LISP)

Po wykonaniu wybranych scenariuszy wywołaj:

```
VERIFY_FIELDS_TESTS
```

Procedura wypisze podsumowanie typu:

```
========================================
= PODSUMOWANIE: 9 PASS / 1 FAIL (z 10 asercji)
========================================
```

Lista asercji:

| # | Asercja | Sprawdza |
|---|---------|----------|
| 1 | `ReadFields` wykrywa `SystemVariable` | Kategoria `%<\AcVar>` |
| 2 | `ReadFields` wykrywa `DateTime` | Kategoria `%<\AcDate>` |
| 3 | `ReadFields` wykrywa `Expression` | Kategoria `%<\AcExpr>` |
| 4 | `ReadFields` ignoruje czysty tekst | Kontrola negatywna |
| 5 | `ManageFields.InsertField` Position=End | Dodaje kod na końcu |
| 6 | `ManageFields.InsertField` Position=Start | Dodaje kod na początku |
| 7 | `ManageFields.ConvertToText` | Usuwa kody `%<` |
| 8 | `ManageFields.RemoveField` | Usuwa kody `%<` |
| 9 | `TextEditTool` chroni pola | Kandydat do blokady obecny |
| 10 | Suma końcowa | Podsumowanie |

---

## 11. Diagnostyka problemów

### Narzędzie nie widzi pól mimo ich obecności

1. Sprawdź czy tekst nie został wstawiony jako RTF z kodami pol
   wyłączonymi (`FIELDDISPLAY=0`).
2. Sprawdź czy pole nie jest osadzone w atrybucie `Constant=true` —
   ManageFields pomija takie atrybuty domyślnie.
3. Wywołaj `ReadFields` bez filtru kategorii, aby zobaczyć wszystkie
   wykryte pola.

### Pole po `InsertField` ma złą wartość

1. Sprawdź czy nie użyto `ConvertToText` wcześniej — wtedy pole jest
   już "zamrożone" jako tekst.
2. Wywołaj `ManageFields.EvaluateAll` aby wymusić `_.REGEN`.
3. Sprawdź czy zmienna systemowa (np. `DWGNAME`) istnieje w rysunku
   (`SETVAR DWGNAME ...`).

### TextEditTool zgłasza blokadę, ale pole już nie istnieje

To znaczy, że obiekt ma ustawioną flagę `HasFields=true`, ale
`getTextWithFieldCodes()` nie zwraca kodu. Taki stan może wystąpić po
importach DXF z innych CAD-ów. Użyj `AllowFieldOverride=true` aby
obejść blokadę, lub najpierw `ManageFields.ConvertToText`.

### `ResolvedValue` jest puste mimo obecności pola

To znaczy, że pole jest w formie binarnej (`%<\_FldIdx>`) i **nie zostało przeliczone**
przez BricsCAD. Rozwiązania:

1. Sprawdź status w raporcie `ReadFields` — `status=NotYetEvaluated` potwierdza brak REGEN.
2. Wywołaj `ManageFields(EvaluateAll)` — wysyła `_.REGEN`, po czym wartości powinny się pojawić.
3. Po REGEN ponownie wywołaj `ReadFields`.

### `ManageFields` modyfikuje wszystkie obiekty z zaznaczenia, a chciałem tylko jeden

To znaczy, że brakuje filtra `MatchText`. Dodaj parametr:

```
ManageFields(
  Action="InsertField",
  FieldCode="%<\\AcDate \\yyyy-MM-dd>",
  Position="End",
  MatchText="Wynik:"
)
```

Ten wzorzec modyfikuje tylko obiekty, których treść zawiera `"Wynik:"`.
Podobnie `BlockNameFilter` i `AttributeTagFilter` pozwalają precyzyjnie
wskazać atrybut bloku (np. `NUMER` w bloku `Bielik_Pole_Etykieta`).

### `ManageFields` ignoruje atrybuty w bloku

Domyślnie `ManageFields` wchodzi w atrybuty `BlockReference` (poprzez
`AttributeCollection`). Atrybuty stałe (`Constant=true`) są pomijane
— ustaw `IncludeConstantAttributes=true` aby je modyfikować.
Narzędzie nie wchodzi w atrybuty zagnieżdżonych bloków (rekurencja
wymaga `Recursive` jak w `EditBlock`, nie zaimplementowane).