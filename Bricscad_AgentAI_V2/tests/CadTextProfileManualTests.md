# Testy manualne - Profil CadTextProfile (edycja DBText/MText/RTF/pola)

Zestaw poleceń do wklejania agentowi w zakładce **Agent-Czat** lub głównym
panelu agenta w celu weryfikacji działania profilu `CadTextProfile` w BricsCAD V22.

Profil `CadTextProfile` specjalizuje się w:
- edycji treści i formatowania (RTF) wolnych obiektów `DBText` i `MText`,
- wstawianiu/aktualizacji/usuwaniu pól CAD (`%<\Ac...>`) w tekstach,
- masowych operacjach na tekstach (`Replace`/`Append`/`Prepend` przez `ForeachTool`),
- identyfikacji tekstów po warstwie, nazwie, treści lub `XData`,
- generowaniu LISP-ów `replace_text_in_layers` dla bardzo dużych zbiorów.

Granice kompetencji:
- atrybuty w blokach (`AttributeReference`) → `CadBlocksProfile`,
- zapis `XData` (`WriteXData`, `BatchWriteXData`) → `CadMetadataProfile`,
- proste wstawienie 1-2 opisów przy rysowaniu → `CadGeometryProfile`.

---

## 0. Setup środowiska

W panelu **BricsCAD** wykonaj kolejno:

```
(load "ścieżka/do/Bricscad_AgentAI_V2/tests/generate_text_test_model.lsp")
(load "ścieżka/do/Bricscad_AgentAI_V2/tests/verify_text_tests.lsp")
GEN_TEXT_TESTS
```

Rysunek powinien zawierać:

| # | Typ | Zawartość | Warstwa |
|---|-----|-----------|---------|
| 1 | DBText | `Stara etykieta A` | Bielik_TestTekstow |
| 2 | DBText | `Data: %<\AcDate \yyyy-MM-dd>` | Bielik_TestTekstow |
| 3 | MText  | `Projekt: %<\AcVar "DWGNAME">` | Bielik_TestTekstow |
| 4 | MText  | `Linia 1 znacznik\PLinia 2 znacznik\PLinia 3 znacznik` (wieloliniowy) | Bielik_TestTekstow |
| 5 | MText  | `Opis A\POpis B\POpis C` | Bielik_TestTekstow |
| 6-35 | MText (30 szt.) | `[STARY] Opis 1` ... `[STARY] Opis 30` | Bielik_TestTekstow |
| 36 | MText (XData) | `Specjalny opis` + XData `Bielik_Test/Kontekst/Etykieta_36` | Bielik_TestTekstow |
| 37-41 | DBText (5 szt.) | `[STARy] Notatka 1` ... `[STARy] Notatka 5` (mała litera y) | Bielik_TestTekstow |

---

## 1. ReadTextSampleTool - próbka treści przed edycją

### 1.1. Zwykła próbka (bez `SaveAs`)

```
Pokaż mi próbkę tekstów z zaznaczenia.
```

**Oczekiwany rezultat:**
- Raport powinien zawierać fragmenty treści: `Stara etykieta A`, `Data: %<...>`,
  `Projekt: %<...>`, `[STARY] Opis 1`...`[STARY] Opis 30`, `[STARy] Notatka 1`...
- NIE powinien zwracać całej zawartości (chroni kontekst).

### 1.2. Zapis do pamięci z `SaveAs`

```
Zapisz próbkę tekstów do pamięci jako TekstyProbka.
```

**Oczekiwany rezultat:**
- Komunikat `ZAPISANO W PAMIECI JAKO @TekstyProbka`.
- W kolejnych zapytaniach odwołanie do `@TekstyProbka` zwraca zapisaną próbkę.

### 1.3. Wykrycie literary 'y' zamiast 'Y' w `[STARy]`

```
Wybierz wszystkie DBText ze znacznikiem [STARy] i wylistuj ich tresc.
```

**Oczekiwany rezultat:**
- W raporcie powinny pojawić się 5 DBText: `[STARy] Notatka 1`...`[STARy] Notatka 5`.
- Agent powinien zauważyć, że `FindText="[STARY]"` NIE zadziała (wielkość liter).

---

## 2. TextEditTool.Replace - prosta zamiana na małej selekcji

### 2.1. Replace na pojedynczym MText

```
Zamień tekst "Stara etykieta A" na "Nowa etykieta A".
```

**Oczekiwany rezultat:**
- Agent powinien zauważyć, że obiekt to DBText, nie MText.
- Po `SelectEntities` + `ReadTextSampleTool` → `TextEditTool(Mode=Replace, FindText="Stara etykieta A", ReplaceWith="Nowa etykieta A")`.
- Raport: `Zmodyfikowano 1 obiektów tekstowych`.
- W modelu: `Nowa etykieta A`.

### 2.2. Replace na MText z polem - BLOKADA

```
Zamień tekst "Projekt: " w MText "Projekt: %<\AcVar "DWGNAME">" na "Zlecenie: ".
```

**Oczekiwany rezultat:**
- W raporcie powinno pojawić się `[BLOKADA POLA] MText (ID: ...) zawiera pole CAD. Tryb 'Replace' nie zostanie wykonany.`
- Treść MText powinna pozostać `Projekt: %<\AcVar "DWGNAME">`.
- Liczba zmodyfikowanych obiektów = 0.
- Agent powinien zaproponować użycie `ManageFieldsTool.ConvertToText` lub
  `TextEditTool` z `AllowFieldOverride=true`.

### 2.3. Replace z `AllowFieldOverride=true`

```
Zamień tekst w MText z polem DWGNAME na "Zlecenie" - swiadomie niszcze pole.
```

**Oczekiwany rezultat:**
- Treść MText zostanie zastąpiona (zależnie od `FindText`).
- Pole CAD zostanie utracone (nieodwracalnie).
- W raporcie: brak ostrzeżenia o blokadzie pola, ale informacja o modyfikacji.

---

## 3. ForeachTool - masowa edycja tekstów (PRIORYTET 1)

### 3.1. Replace na 30 MText przez ForeachTool

```
Zamień [STARY] na [NOWY] we wszystkich 30 opisach.
```

**Oczekiwany rezultat:**
- Agent powinien użyć `ForeachTool(IterateSelection=true, Action={"ToolName":"TextEditTool","Mode":"Replace","FindText":"[STARY]","ReplaceWith":"[NOWY]"})`.
- LUB `SelectEntities(Scope=Model, EntityType="MText", Conditions.ContainsText="[STARY]")` + Foreach.
- Raport: `Zmodyfikowano 30 obiektów tekstowych`.
- W modelu: 30 MText z `[NOWY] Opis 1`...`[NOWY] Opis 30`.

### 3.2. Append sufiksu do 30 opisów

```
Dopisz _2026 do konca wszystkich 30 opisow [STARY] (teraz [NOWY]).
```

**Oczekiwany rezultat:**
- Foreach + `TextEditTool(Mode=Append, ReplaceWith="_2026")`.
- Raport: `Zmodyfikowano 30 obiektów tekstowych`.
- W modelu: 30 MText z `[NOWY] Opis 1_2026`...`[NOWY] Opis 30_2026`.

### 3.3. Prepend prefiksu

```
Dodaj prefix [AKTUALNE] do wszystkich opisow z _2026.
```

**Oczekiwany rezultat:**
- Foreach + `TextEditTool(Mode=Prepend, ReplaceWith="[AKTUALNE] ")`.
- W modelu: 30 MText z `[AKTUALNE] [NOWY] Opis N_2026`.

### 3.4. Replace z wariancją Items (różne wartości na obiekt)

```
Dla pierwszych 3 opisow ustaw tresc: "POZYCJA 1", "POZYCJA 2", "POZYCJA 3".
```

**Oczekiwany rezultat:**
- Foreach z `Items=["POZYCJA 1", "POZYCJA 2", "POZYCJA 3"]`.
- W `Action.ReplaceWith="{item}"`.
- W modelu: pierwsze 3 MText mają odpowiednie stałe wartości.

---

## 4. manage_lisps - masowa edycja >200 obiektów (PRIORYTET 2)

### 4.1. Szablon replace_text_in_layers

```
Zamień tekst "STARY" na "NOWY" we wszystkich tekstach na warstwie Bielik_TestTekstow.
```

**Oczekiwany rezultat:**
- Agent powinien rozważyć `manage_lisps` z `Action=generate_lisp`, `Template=replace_text_in_layers`.
- LUB użyć ForeachTool jeśli liczba obiektów < 200 (w naszym modelu jest 36, więc Foreach wystarczy).
- W naszym teście 30 MText ma `[STARY]`, więc po wywołaniu wszystkie powinny mieć `[NOWY]`.

### 4.2. MatchMode=exact vs contains

```
Zamień dokladnie "Opis" na "Element" (bez contains) na warstwie Bielik_TestTekstow.
```

**Oczekiwany rezultat:**
- W trybie `MatchMode="exact"` tylko dokładne dopasowanie `Opis` zostanie zmienione.
- W trybie `MatchMode="contains"` (domyślny) każdy tekst zawierający `Opis` zostanie zmieniony.
- W naszym modelu MText #5 ma `Opis A\POpis B\POpis C` - tu dokładne `Opis` nie istnieje,
  ale `Opis A`, `Opis B`, `Opis C` tak. W trybie exact nic się nie zmieni, w contains wszystkie trzy.

---

## 5. TextEditTool.FormatHighlight - RTF w MText

### 5.1. FormatHighlight na MText - sukces

```
Pokoloruj slowo "znacznik" na czerwono we wszystkich MText z warstwy Bielik_TestTekstow.
```

**Oczekiwany rezultat:**
- Foreach + `TextEditTool(Mode=FormatHighlight, FindText="znacznik", ReplaceWith="znacznik", ColorIndex=1)`.
- W modelu: MText z `{\C1;znacznik}` zamiast `znacznik` (MText #4 ma 3 wystąpienia).
- Raport: `Zmodyfikowano 1 obiektów tekstowych` (MText #4).

### 5.2. FormatHighlight na DBText - OSTRZEŻENIE

```
Pokoloruj slowo "Stara" na czerwono w DBText "Stara etykieta A".
```

**Oczekiwany rezultat:**
- W raporcie: `[OSTRZEŻENIE] Zignorowano obiekt DBText (ID: ...) w trybie FormatHighlight, ponieważ nie obsługuje on kodów RTF.`
- Treść DBText NIE zmodyfikowana.
- Liczba zmodyfikowanych obiektów = 0.

### 5.3. FormatHighlight z pogrubieniem

```
Pogrub i pokoloruj na czerwono slowo "Opis" w MText z lista opisow.
```

**Oczekiwany rezultat:**
- Foreach lub pojedyncze wywołanie z `Mode=FormatHighlight, FindText="Opis", IsBold=true, ColorIndex=1`.
- W modelu: `{\fArial|b1;\C1;Opis}` (lub podobny kod RTF).

---

## 6. TextEditTool.ClearFormatting - usuwanie RTF

### 6.1. ClearFormatting na MText z RTF

```
Wyczysc formatowanie RTF w MText "Linia 1 znacznik\PLinia 2 znacznik...".
```

**Oczekiwany rezultat:**
- `TextEditTool(Mode=ClearFormatting)`.
- Kody RTF (`\fArial|b1;`, `\C1;`) usunięte, ale `\P` (nowe linie) zachowane.
- W modelu: `Linia 1 znacznik\PLinia 2 znacznik\PLinia 3 znacznik` (bez dodatkowych kodów).

### 6.2. ClearFormatting na MText z polem - BLOKADA

```
Wyczysc formatowanie w MText "Projekt: %<\AcVar "DWGNAME">".
```

**Oczekiwany rezultat:**
- W raporcie: `[BLOKADA POLA] MText (ID: ...) zawiera pole CAD. Tryb ClearFormatting nie zostanie wykonany...`
- Treść MText NIE zmieniona.

### 6.3. ClearFormatting na DBText - OSTRZEŻENIE

```
Wyczysc formatowanie w DBText "Stara etykieta A".
```

**Oczekiwany rezultat:**
- W raporcie: `[OSTRZEŻENIE] Pominięto obiekt DBText (ID: ...) w trybie ClearFormatting, ponieważ nie zawiera on kodów RTF.`

---

## 7. ManageFieldsTool - pola CAD w tekstach

### 7.1. InsertField na końcu

```
Wstaw aktualna date na koncu tekstu "Specjalny opis".
```

**Oczekiwany rezultat:**
- Agent powinien zlokalizować MText #36 (z XData) po `FindXDataTool` lub po treści.
- `ManageFieldsTool(Action="InsertField", FieldCode="%<\\AcDate \\\"yyyy-MM-dd\\\">", Position="End", Targets=...)`.
- W modelu: `Specjalny opis%<\AcDate \yyyy-MM-dd>`.
- Po `_.REGEN` data zostanie obliczona.

### 7.2. ReadFields - inspekcja pól

```
Wylistuj wszystkie pola w zaznaczonych obiektach.
```

**Oczekiwany rezultat:**
- MText #3 (`%<\AcVar "DWGNAME">`), DBText #2 (`%<\AcDate \yyyy-MM-dd>`), MText #36 (data dodana w 7.1).
- 3 wpisy z `Category` (SystemVariable, DateTime).

### 7.3. ConvertToText

```
Zamien pola na zwykly tekst w MText "Projekt: %<\AcVar "DWGNAME">".
```

**Oczekiwany rezultat:**
- `ManageFieldsTool(Action="ConvertToText", Targets=...)`.
- W modelu: `Projekt: NAZWA_PLIKU_DWG` (wartość obliczona).
- Kod `%<` usunięty.

---

## 8. FindXDataTool - identyfikacja po XData

### 8.1. Wyszukanie po XData

```
Znajdz MText z XData "Bielik_Test" i pokaz jego tresc.
```

**Oczekiwany rezultat:**
- `FindXDataTool(AppName="Bielik_Test")` lub `ReadXDataTool` po selekcji.
- MText #36 `Specjalny opis` powinien być zidentyfikowany.
- Dodatkowe metadane z XData: `Kontekst`, `Etykieta_36`.

### 8.2. Użycie XData jako filtra

```
Wstaw pole z data w MText, ktory ma XData "Bielik_Test" (Etykieta_36).
```

**Oczekiwany rezultat:**
- Agent powinien najpierw `FindXDataTool`, potem `ManageFieldsTool` na znalezionym obiekcie.
- Identyczny efekt jak 7.1, ale pokazujący integrację XData → ManageFields.

---

## 9. ActiveSelection vs Scope=Model

### 9.1. Edycja tylko zaznaczonych

```
Mam zaznaczone 3 MText z [NOWY]. Zamien [NOWY] na [KONIEC] tylko w nich.
```

**Oczekiwany rezultat:**
- Agent powinien wykryć `SelectionScopeLock=true` w delegacji.
- W prompcie Worker powinien dostać instrukcję "operuj TYLKO na ActiveSelection".
- 3 MText zmienione, pozostałe 27 MText z `[NOWY]` nietknięte.

### 9.2. Edycja po filtrze warstwy

```
Zamien [NOWY] na [KONIEC] we wszystkich MText na warstwie Bielik_TestTekstow.
```

**Oczekiwany rezultat:**
- Brak `SelectionScopeLock` (user nie mówi o selekcji).
- `SelectEntities(Scope=Model, EntityType="MText", Layer="Bielik_TestTekstow", Conditions.ContainsText="[NOWY]")`.
- 30 MText zmienione.

---

## 10. Granica kompetencji - delegacja w górę

### 10.1. Atrybuty w bloku - delegacja do CadBlocksProfile

```
W bloku Bielik_Test ustaw atrybut OPIS na "test123".
```

**Oczekiwany rezultat:**
- CadTextProfile NIE MA `EditAttributes` w `AllowedTools`.
- Worker powinien zgłosić błąd architektury i poprosić o delegację w górę do Supervisora.
- Supervisor powinien przekierować do `CadBlocksProfile`.

### 10.2. Zapis XData - delegacja do CadMetadataProfile

```
Zapisz XData "Projekt=A" w MText "Specjalny opis".
```

**Oczekiwany rezultat:**
- CadTextProfile NIE MA `WriteXData`/`BatchWriteXData` w `AllowedTools`.
- Worker powinien zgłosić brak narzędzia i poprosić o delegację w górę.
- Supervisor → `CadMetadataProfile`.

---

## 11. Routing sygnałowy - test decyzji Supervisora

### 11.1. Fraza "tekst" + "wszystkie" → CadTextProfile

```
We wszystkich tekstach na warstwie Bielik_TestTekstow zamien "Opis" na "Element".
```

**Oczekiwany rezultat:**
- Supervisor rozpoznaje: "tekst", "wszystkich", "we wszystkich" + warstwa.
- Deleguje do `CadTextProfile` (NIE do geometry ani profile).

### 11.2. Fraza "koloruj" → CadTextProfile

```
Pokoloruj na czerwono slowo TEST we wszystkich MText.
```

**Oczekiwany rezultat:**
- Supervisor rozpoznaje: "koloruj", "MText", "RTF".
- Deleguje do `CadTextProfile`.

### 11.3. Fraza "wstaw pole" → CadTextProfile

```
Wstaw pole z dzisiejsza data w tekscie "Opis A".
```

**Oczekiwany rezultat:**
- Supervisor rozpoznaje: "pole", "wstaw pole", "data".
- Deleguje do `CadTextProfile`.

### 11.4. Fraza geometria + tekst → CadGeometryProfile (prosty opis)

```
Narysuj prostokat i wpisz w nim napis "Strefa A".
```

**Oczekiwany rezultat:**
- To 1-2 opisy przy rysowaniu → `CadGeometryProfile` (NIE CadTextProfile).
- Supervisor powinien rozpoznać, że to rysowanie + prosty opis.

---

## 12. Pipeline end-to-end (scenariusz realistyczny)

```
1. (load "generate_text_test_model.lsp")
2. GEN_TEXT_TESTS
3. ReadTextSampleTool                              # inwentaryzacja
4. TextEditTool(Replace, [STARY] -> [NOWY], Foreach)  # 30 MText zmienionych
5. TextEditTool(Append, _2026, Foreach)               # 30 MText zaktualizowanych
6. TextEditTool(FormatHighlight, Opis, red, Foreach)  # RTF w MText #5
7. ReadFields                                       # raport pol
8. ManageFieldsTool(InsertField, data, End, XData)  # pole w MText #36
9. TextEditTool(Replace, [NOWY] -> [KONIEC], Foreach)  # kolejna zamiana
10. Verify in BricsCAD: 30 MText z "[KONIEC] Opis N_2026", MText #5 z RTF, MText #36 z polem
```

---

## 13. Weryfikacja końcowa (asercje LISP)

Po wykonaniu wybranych scenariuszy wywołaj:

```
VERIFY_TEXT_TESTS
```

Procedura wypisze podsumowanie typu:

```
========================================
= WERYFIKACJA PROFILU CadTextProfile =
========================================
--- TextEditTool.Replace (z ForeachTool) ---
  [PASS] Liczba MText z [STARY] po Replace: 0 (powinno byc 0)
  [PASS] Liczba MText z [NOWY] po Replace: 30 (powinno byc >= 30)
--- TextEditTool.Append (z ForeachTool) ---
  [PASS] Liczba MText z _2026: 30 (powinno byc >= 30)
--- TextEditTool.FormatHighlight ---
  [PASS] Liczba MText z kodem \C: 1 (powinno byc > 0)
--- ReadTextSampleTool ---
  [PASS] Liczba DBText z [STARy]: 5 (powinno byc >= 5)
--- ManageFields.InsertField ---
  [PASS] MText 'Specjalny opis' ma na koncu kod %<\\AcDate
--- Ochrona pol CAD ---
  [PASS] MText z polem CAD jest nadal obecny (markery %<: 1)
--- Granica kompetencji (DBText vs MText) ---
  [PASS] DBText 'Stara etykieta A' nadal istnieje: 1 (powinno byc 1)
--- Kontekst XData ---
  [PASS] MText 'Specjalny opis' istnieje w modelu (XData obecne w strukturze)
========================================
= PODSUMOWANIE: 9 PASS / 0 FAIL (z 9 asercji)
========================================
```

Lista asercji:

| # | Asercja | Sprawdza |
|---|---------|----------|
| 1 | `TextEditTool.Replace` usunął `[STARY]` | Foreach + Replace |
| 2 | `TextEditTool.Replace` dodał `[NOWY]` | Foreach + Replace |
| 3 | `TextEditTool.Append` dodał `_2026` | Foreach + Append |
| 4 | `TextEditTool.FormatHighlight` dodał `\C` | RTF w MText |
| 5 | `ReadTextSampleTool` widzi `[STARy]` | Próbkowanie |
| 6 | `ManageFields.InsertField` dodał kod | Pola CAD |
| 7 | Pole `%<\AcVar "DWGNAME">` jest nienaruszone | Ochrona pól |
| 8 | `DBText` nie zmieniony przez `FormatHighlight` | Granica DBText/MText |
| 9 | MText z XData istnieje | Kontekst XData |

---

## 14. Diagnostyka problemów

### Agent wybiera geometry zamiast CadTextProfile

Jeśli supervisor kieruje zadania tekstowe do `CadGeometryProfile`:
1. Sprawdź, czy `system_prompt_supervisor.txt` zawiera wpis `CadTextProfile` w regułach delegacji.
2. Sprawdź, czy fraza "tekst" / "napis" / "MText" jest w wypowiedzi użytkownika.
3. Jeśli brak - dodaj do wypowiedzi: "edycja tekstu", "RTF", "pokoloruj tekst".

### TextEditTool.Replace nie działa mimo braku pól

1. Sprawdź czy obiekt to DBText, nie MText - DBText nie obsługuje wielu trybów.
2. Sprawdź czy `FindText` nie ma spacji wiodących/końcowych (BricsCAD wymaga dokładnego dopasowania).
3. Sprawdź czy `ReadTextSampleTool` zwrócił dokładnie tę treść, której szukasz.

### ForeachTool przetwarza tylko część obiektów

1. Sprawdź `IterateSelection=true` - wymaga, żeby `AgentMemoryState.ActiveSelection` było ustawione.
2. Jeśli nie ma selekcji - użyj `SelectEntities(Scope=Model, ...)` PRZED Foreach.
3. `ForeachTool` ma cap ~200 - dla większych zbiorów użyj `manage_lisps` `Template=replace_text_in_layers`.

### FormatHighlight nie daje efektu wizualnego

1. Sprawdź czy obiekt to MText (DBText → ostrzeżenie).
2. Sprawdź czy kolor ACI (1-255) istnieje w rysunku - kolory >255 mogą być niewidoczne.
3. Po edycji wywołaj `_.REGEN` (czasem potrzebne do odświeżenia widoku RTF).

### ManageFields.InsertField zwraca błąd składni pola

1. Sprawdź czy `FieldCode` ma escapowane cudzysłowy: `\"` w JSON.
2. Sprawdź czy nie ma spacji przed zamykającym `>`.
3. BricsCAD akceptuje: `%<\AcDate \yyyy-MM-dd>`, NIE: `%<\AcDate \yyyy-MM-dd >`.
4. Sprawdź pisownię kategorii: `AcVar`, `AcDate`, `AcExpr`, `AcObjProp`, `AcProp`, `AcFido`, `AcDim`.

### FindXDataTool nie zwraca oczekiwanego obiektu

1. Sprawdź czy obiekt faktycznie ma `XData` (niektóre importy DXF je tracą).
2. Sprawdź `AppName` - musi być dokładna nazwa aplikacji zarejestrowana w obiekcie.
3. Użyj `ReadXDataTool` na konkretnym `Handle` aby zobaczyć WSZYSTKIE XData obiektu.

---

## 15. Checklist wydania profilu (release checklist)

Przed uznaniem `CadTextProfile` za produkcyjny:

- [ ] Kompilacja: `build.ps1` bez błędów (5 pre-existujących warningów OK).
- [ ] Testy jednostkowe: `CadTextProfileTests.RunTests()` zielone.
- [ ] Setup środowiska: `GEN_TEXT_TESTS` działa bez błędów w BricsCAD V22.
- [ ] Scenariusze 1-12 przeszły manualnie (każdy ma `[PASS]` w `VERIFY_TEXT_TESTS`).
- [ ] Granica kompetencji (sekcja 10) zwraca prawidłowe błędy architektury.
- [ ] Routing sygnałowy (sekcja 11) kieruje do `CadTextProfile`, nie do geometry.
- [ ] Profile bloków i metadanych nie zostały zregresjonowane (ich testy nadal zielone).
- [ ] `USER_GUIDE.md` zaktualizowany o opis `CadTextProfile` (sekcja: Profile agentów).
- [ ] Benchmarki AutoBenchmark typu `EditText_*` wygenerowane i przetestowane (po tej weryfikacji).
