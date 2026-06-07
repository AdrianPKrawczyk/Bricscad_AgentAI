# BricsCAD Agent AI V2: Profesjonalny PodrÄ™cznik UĹĽytkownika (v2.20.4 GOLD)

Witaj w wersji **GOLD** systemu Bielik AI V2. Niniejszy podrÄ™cznik zostaĹ‚ przygotowany dla inĹĽynierĂłw i projektantĂłw BricsCAD, ktĂłrzy chcÄ… w peĹ‚ni wykorzystaÄ‡ potencjaĹ‚ sztucznej inteligencji zintegrowanej bezpoĹ›rednio z silnikiem CAD.

---

## đź§  1. Architektura PamiÄ™ci i Stanu

Zrozumienie sposobu, w jaki Agent "myĹ›li" i przechowuje dane, jest kluczowe dla budowania zaawansowanych scenariuszy pracy.

### 1.1. PamiÄ™Ä‡ Zaznaczenia (ActiveSelection)
W przeciwieĹ„stwie do standardowych poleceĹ„ CAD, Agent posiada **pamiÄ™Ä‡ selekcji**, ktĂłra persists (utrzymuje siÄ™) miÄ™dzy kolejnymi zapytaniami.
- **Automatyzacja selekcji**: KaĹĽdy nowo utworzony obiekt (np. liniÄ…, blokiem) jest automatycznie dodawany do pamiÄ™ci.
- **WydajnoĹ›Ä‡**: DziÄ™ki temu moĹĽesz wydaÄ‡ polecenie "Narysuj okrÄ…g", a w nastÄ™pnym kroku napisaÄ‡ po prostu "ZmieĹ„ jego kolor na czerwony" â€“ Agent wie dokĹ‚adnie, o ktĂłry obiekt chodzi, bez koniecznoĹ›ci ponownego wskazywania go na ekranie.
- **ZarzÄ…dzanie**: PamiÄ™ciÄ… sterujÄ… narzÄ™dzia `SelectEntities` (dodawanie/odejmowanie) oraz polecenie gĹ‚osowe/tekstowe "Odznacz wszystko" (czyĹ›ci stan).

### 1.2. Zmienne Sesji (@Variables)
Agent moĹĽe wyekstrahowaÄ‡ dane z rysunku i zapisaÄ‡ je w nazwanych "szufladkach".
- **Zapisywanie**: NarzÄ™dzia takie jak `ReadProperty` lub `AnalyzeSelection` pozwalajÄ… zapisaÄ‡ wynik pod aliasem (np. `@SumaPowierzchni`).
- **Wstrzykiwanie**: MoĹĽesz wymusiÄ‡ uĹĽycie zmiennej w nastÄ™pnym kroku, uĹĽywajÄ…c symbolu `$`.
- **PrzykĹ‚ad**: *"Odczytaj dĹ‚ugoĹ›Ä‡ tej linii jako @L, a potem narysuj okrÄ…g o promieniu $L"*.

---

## đź§® 2. Silnik Obliczeniowy RPN (Reverse Polish Notation)

Agent V2 posiada wbudowany procesor matematyczny dziaĹ‚ajÄ…cy w Odwrotnej Notacji Polskiej. Pozwala to na wykonywanie obliczeĹ„ bezpoĹ›rednio na geometrii.

### 2.1. Zmienne Dynamiczne `$OLD_...`
Podczas modyfikacji wĹ‚aĹ›ciwoĹ›ci (`ModifyProperties`), Agent automatycznie udostÄ™pnia starÄ… wartoĹ›Ä‡ obiektu pod specjalnym prefiksem.

| Zmienna | Opis | PrzykĹ‚ad uĹĽycia w prompt |
| :--- | :--- | :--- |
| `$OLD_RADIUS` | Obecny promieĹ„ okrÄ™gu/Ĺ‚uku | *"ZwiÄ™ksz promieĹ„ o 1.5 raza"* (Agent: `$OLD_RADIUS 1.5 *`) |
| `$OLD_HEIGHT` | Obecna wysokoĹ›Ä‡ tekstu | *"Zmniejsz teksty o 2 jednostki"* (Agent: `$OLD_HEIGHT 2 -`) |
| `$OLD_LENGTH` | Obecna dĹ‚ugoĹ›Ä‡ linii/polilinii | *"WydĹ‚uĹĽ o 10%"* (Agent: `$OLD_LENGTH 1.1 *`) |
| `$OLD_LAYER` | Obecna nazwa warstwy | Wykorzystywane w logice warunkowej. |

### 2.2. PrzykĹ‚ady FormuĹ‚ RPN
- **Skalowanie**: `RPN: $OLD_RADIUS 2 *` (Podwojenie promienia).
- **PrzesuniÄ™cie**: `RPN: $OLD_X 100 +` (PrzesuniÄ™cie o 100 jednostek w osi X).
- **ZĹ‚oĹĽone**: `RPN: 10 20 + 5 *` (Wynik: 150).

> [!TIP]
> JeĹ›li chcesz mieÄ‡ pewnoĹ›Ä‡, ĹĽe Agent uĹĽyje obliczeĹ„, napisz: *"Zastosuj formuĹ‚Ä™ RPN: [twoje dziaĹ‚anie]"*.

### 2.3. Interfejs CLI dla Kalkulatora (v2.20.4)
MoĹĽesz korzystaÄ‡ z mocy obliczeniowej Agenta bezpoĹ›rednio w linii poleceĹ„ BricsCAD.

- **`RPN`**: Interaktywny tryb obliczeĹ„ z **podglÄ…dem stosu na ĹĽywo**.
    - **Wstrzykiwanie**: Po zakoĹ„czeniu (pusty Enter), wynik trafia do aktywnego polecenia BricsCAD.
    - **Unit-Clean**: System automatycznie przelicza jednostki dĹ‚ugoĹ›ci (np. `1_m` -> `1000` dla rysunku w mm) i usuwa sufiks jednostki przed wstrzykniÄ™ciem.
- **`CALC`**: PÄ™tla obliczeniowa (tylko odczyt). Idealna do szybkich przeliczeĹ„ bez wpĹ‚ywania na historiÄ™ poleceĹ„ CAD.
- **`STOS`**: WyĹ›wietla aktualnÄ…, peĹ‚nÄ… zawartoĹ›Ä‡ stosu matematycznego zapisanÄ… w rysunku.

### 2.4. TrwaĹ‚oĹ›Ä‡ Stosu (DWG Persistence)
Stos Agenta jest zapisywany wewnÄ…trz pliku `.dwg` w sĹ‚owniku NOD (`BIELIK_RPN_STACK`). Dane sÄ… odĹ›wieĹĽane po kaĹĽdej operacji, co gwarantuje ich bezpieczeĹ„stwo.

---

## đźŹ˘ 3. Zaawansowane ZarzÄ…dzanie Blokami i Atrybutami

W wersji GOLD rozrĂłĹĽniamy dwa krytyczne tryby pracy z blokami:

### 3.1. Edycja Definicji (Globalna) - `EditBlock`
Modyfikuje "matrycÄ™" bloku. Zmiana tutaj wpĹ‚ywa na **wszystkie** wystÄ…pienia danego bloku w caĹ‚ym rysunku.
- **Zastosowanie**: Zmiana koloru linii wewnÄ…trz symbolu, usuniÄ™cie zbÄ™dnej geometrii z definicji.
- **Opcja `Recursive`**: Pozwala Agentowi wejĹ›Ä‡ gĹ‚Ä™biej w zagnieĹĽdĹĽone bloki.

### 3.2. Edycja AtrybutĂłw (Lokalna) - `EditAttributes`
Modyfikuje tylko dane tekstowe (atrybuty) w **konkretnej instancji** bloku na rysunku.
- **Zastosowanie**: Numeracja pomieszczeĹ„, wypeĹ‚nianie tabliczek rysunkowych, zmiana opisu bez zmiany wyglÄ…du bloku.

- **Akcja `SetCurrent`**: Dedykowany, bezpieczny sposĂłb na przeĹ‚Ä…czenie aktywnej warstwy roboczej.

---

## đź“ 4. Edycja Precyzyjna (Wymiary i Teksty)

W wersji **v2.14.0** system przeszedĹ‚ na model **Separation of Concerns**. Oznacza to, ĹĽe skomplikowane obiekty majÄ… swoje dedykowane, potÄ™ĹĽniejsze narzÄ™dzia.

### 4.1. Edycja WymiarĂłw - `DimensionEdit`
Zamiast ogĂłlnych wĹ‚aĹ›ciwoĹ›ci, uĹĽywaj dedykowanego narzÄ™dzia do "anatomii" wymiaru.
- **Tekst**: MoĹĽesz nadpisaÄ‡ wartoĹ›Ä‡ lub wrĂłciÄ‡ do pomiaru (wpisujÄ…c pusty tekst).
- **Skala**: Zmieniaj `OverallScale` by powiÄ™kszyÄ‡ teksty i strzaĹ‚ki bez zmiany stylu.
- **Stylizacja**: NiezaleĹĽne kolory dla tekstu, linii gĹ‚Ăłwnej i linii pomocniczych.
- **Grot StrzaĹ‚ki**: Wybieraj predefiniowane bloki (np. `_ARCHTICK` dla kreski).

### 4.3. Rozszerzone dane (XData) - `ReadXData`
Agent posiada teraz narzÄ™dzie do "gĹ‚Ä™bokiej inspekcji" metadanych ukrytych w obiektach DWG.
- **Zastosowanie**: Odczytywanie danych z zewnÄ™trznych systemĂłw (np. ERP, GIS) zapisanych jako XData.
- **Filtrowanie**: MoĹĽesz poprosiÄ‡ o dane konkretnej aplikacji: *"Odczytaj XData dla aplikacji 'MY_BIM_APP'"*.
- **PamiÄ™Ä‡**: MoĹĽesz zapisaÄ‡ te metadane do zmiennej i uĹĽyÄ‡ ich w formule RPN.

---

## đź¤ť 5. Interakcja i Konsultacje (Tryb Hybrydowy)

Agent nie musi zgadywaÄ‡ â€“ moĹĽe zapytaÄ‡ Ciebie o zdanie.

### 5.1. SĹ‚owo kluczowe `AskUser`
UĹĽywaj go, gdy chcesz wskazaÄ‡ coĹ› myszkÄ… w trakcie pracy Agenta.
- *"Narysuj liniÄ™ od AskUser do 100,100"*.
- Agent przeĹ‚Ä…czy fokus na BricsCAD i poprosi CiÄ™ o klikniÄ™cie punktu.

### 5.2. Konsultacje w linii komend
Agent moĹĽe wywoĹ‚aÄ‡ interaktywne zapytania:
- **String/Value**: *"Podaj nazwÄ™ inwestora"*.
- **Choice**: WyĹ›wietli listÄ™ opcji w pasku poleceĹ„ (np. `[Stal/Drewno/Beton]`). MoĹĽesz wybraÄ‡ opcjÄ™ klikniÄ™ciem.

---

## đźŹ—ď¸Ź 6. Zaawansowane Scenariusze (Workflows)

Oto przykĹ‚ady Ĺ‚aĹ„cuchĂłw dziaĹ‚aĹ„, ktĂłre pokazujÄ… peĹ‚nÄ… moc wersji GOLD:

### Scenariusz A: Raportowanie i Modyfikacja
> *"ZnajdĹş wszystkie polilinie na warstwie 'OBRYS', odczytaj ich powierzchnie i zapisz do zmiennej @Pola. JeĹ›li powierzchnia jest wiÄ™ksza niĹĽ 100, zmieĹ„ kolor polilinii na czerwony, a w jej Ĺ›rodku ciÄ™ĹĽkoĹ›ci wstaw tekst 'ALARM' o wysokoĹ›ci RPN: $OLD_AREA 0.01 *"*

### Scenariusz B: Standaryzacja Warstw
> *"Pobierz listÄ™ wszystkich warstw w rysunku. Dla kaĹĽdej warstwy zaczynajÄ…cej siÄ™ od 'TEMP_', przenieĹ› znajdujÄ…ce siÄ™ na niej obiekty na warstwÄ™ 'ARCH_STARE', a nastÄ™pnie usuĹ„ puste warstwy 'TEMP_*'. Na koniec ustaw przezroczystoĹ›Ä‡ wszystkich warstw 'ARCH_*' na 50% i ustaw gruboĹ›Ä‡ linii na 0.13mm."*

### Scenariusz C: Inteligentna Blokowa Numeracja
> *"Zaznacz bloki o nazwie 'POMIESZCZENIE'. Pobierz ich atrybut 'NUMER'. UĹĽyj pÄ™tli, aby przesortowaÄ‡ je i zmieniÄ‡ atrybut 'STATUS' na 'WERYFIKACJA' dla tych, ktĂłrych numer jest parzysty."*

---

## đźŹ·ď¸Ź 7. Optymalizacja Kontekstu (Semantic Tool Routing)

W wersji **v2.7 GOLD** wprowadziliĹ›my system inteligentnego sterowania zestawem narzÄ™dzi przy uĹĽyciu tagĂłw (Hashtags). Pozwala to na drastyczne przyspieszenie reakcji Agenta i unikniÄ™cie pomyĹ‚ek w zĹ‚oĹĽonych rysunkach.

### 7.1. Czym sÄ… tagi narzÄ™dzi?
Zamiast wysyĹ‚aÄ‡ wszystkie 20+ narzÄ™dzi przy kaĹĽdym zapytaniu, moĹĽesz wskazaÄ‡ Agentowi, w jakim obszarze ma pracowaÄ‡.
- **#core** (Zawsze aktywne): Podstawowe rysowanie, wybieranie, pÄ™tle i zmienne.
- **#bloki**: Wszystko co dotyczy definicji, atrybutĂłw i wstawiania blokĂłw.
- **#warstwy**: ZarzÄ…dzanie warstwami.
- **#tekst**: Edycja tekstĂłw i skal opisowych.
- **#makro**: WywoĹ‚ywanie predefiniowanych procedur.
- **#all**: Odblokowuje peĹ‚ny zestaw wszystkich dostÄ™pnych narzÄ™dzi.

### 7.2. AutouzupeĹ‚nianie (Autocomplete)
W polu wprowadzania tekstu wpisz znak `#`, a pojawi siÄ™ lista dostÄ™pnych kategorii. MoĹĽesz nawigowaÄ‡ strzaĹ‚kami i zatwierdziÄ‡ wybĂłr klawiszem `Enter` lub `Tab`.

### 7.3. PrzykĹ‚ad uĹĽycia w praktyce
- *"Wypisz wszystkie bloki w tym rysunku #bloki"* â€“ Agent zaĹ‚aduje tylko narzÄ™dzia do blokĂłw, co zmniejsza ryzyko halucynacji.
- *"ZmieĹ„ kolor linii na czerwony"* â€“ Nie musisz dodawaÄ‡ tagĂłw dla podstawowych zadaĹ„ (narzÄ™dzia `#core` sÄ… zawsze aktywne).

### 7.4. Agentic Fallback (Samoleczenie)
JeĹ›li zapomnisz o tagu, a Agent uzna, ĹĽe potrzebuje narzÄ™dzi z innej grupy (np. prosisz o warstwy bez tagu `#warstwy`), system posiada mechanizm **Agentic Fallback**. AI automatycznie "poprosi" o dostÄ™p do brakujÄ…cej puli narzÄ™dzi i wykona zadanie w nastÄ™pnym kroku.

---

> [!IMPORTANT]
> Bielik V2 GOLD to system deterministyczny. UĹĽywajÄ…c precyzyjnych narzÄ™dzi i tagĂłw, masz gwarancjÄ™ 100% powtarzalnoĹ›ci wynikĂłw.

### Scenariusz D: Automatyczne Szyki i Sekwencje (NOWOĹšÄ† v2.6.8)
Agent potrafi teraz generowaÄ‡ skomplikowane ukĹ‚ady geometryczne bez Twojej pomocy w liczeniu wspĂłĹ‚rzÄ™dnych.
> *"Narysuj szyk 10 sĹ‚upĂłw (blok 'SLUP_A') zaczynajÄ…c od punktu 0,0 i przesuwajÄ…c kaĹĽdy o 500 jednostek w prawo."*

W tym scenariuszu Agent uĹĽywa narzÄ™dzia `Foreach` z moduĹ‚em `GenerateSequence`:
1. Generuje listÄ™ 10 punktĂłw (0,0; 500,0; 1000,0...).
2. Dla kaĹĽdego punktu wywoĹ‚uje `InsertBlock`, podstawiajÄ…c wygenerowany punkt pod parametr `Position`.

---

## đź”„ 7. Generator CiÄ…gĂłw (Foreach)

NarzÄ™dzie `Foreach` staĹ‚o siÄ™ potÄ™ĹĽnym procesorem danych przestrzennych i logicznych.
- **Sequence Generator**: Pozwala na tworzenie liniowych szykĂłw punktĂłw.
- **Tag `{item}`**: SĹ‚uĹĽy jako miejsce podstawienia wygenerowanej wartoĹ›ci (pozycji lub elementu z listy).
- **Tag `{index}`**: Wstawia numer bieĹĽÄ…cej iteracji (liczony od 1).
- **RPN & ToolName**: MoĹĽesz uĹĽywaÄ‡ kalkulatora RPN wewnÄ…trz akcji oraz wywoĹ‚ywaÄ‡ inne narzÄ™dzia niĹĽ `CreateObject` (np. `ManageLayers`) poprzez dodanie klucza `"ToolName": "..."` w szablonie JSON.

> [!TIP]
> PrzykĹ‚ad zaawansowany: `{"EntityType": "DBText", "Text": "RPN: 'Nr ' {index} CONCAT"}` wstawi teksty "Nr 1", "Nr 2" itd.

---

## âš™ď¸Ź 9. Dataset Studio (Data Flywheel) - NOWOĹšÄ† v2.10.0

Aby system stawaĹ‚ siÄ™ coraz mÄ…drzejszy, wprowadziliĹ›my mechanizm **Data Flywheel**. Pozwala on na przechwytywanie Twoich interakcji z Agentem i zapisywanie ich jako "ZĹ‚ote Standardy" dla przyszĹ‚ych sesji treningowych modelu.

### 9.1. Przechwytywanie Sesji
Po kaĹĽdej zakoĹ„czonej pÄ™tli myĹ›lowej (ReAct), system automatycznie przesyĹ‚a snapshot rozmowy do zakĹ‚adki **"đź’ľ Dataset Studio"**.

### 9.2. Edycja i Zapis
1. PrzejdĹş do zakĹ‚adki **Dataset Studio**.
2. Wybierz sesjÄ™ z listy po lewej stronie.
3. **Context Slicer (âś‚ď¸Ź)**: DomyĹ›lnie zaznaczona opcja "Izoluj polecenie" wycina z historii konwersacji tylko ostatnie zadanie (Turn). Pozwala to na unikniÄ™cie "zanieczyszczenia" danych treningowych poprzednimi tematami. Odznacz tÄ™ opcjÄ™, jeĹ›li chcesz zapisaÄ‡ peĹ‚nÄ…, wieloetapowÄ… sesjÄ™ (Multi-turn).
4. W edytorze po prawej zobaczysz wynikowy kod JSON. MoĹĽesz go dowolnie edytowaÄ‡.
5. Kliknij **"đź’ľ Zapisz ZĹ‚oty Standard do JSONL"**.

Dane sÄ… dopisywane do pliku `Agent_Training_Data_v2_DO_TRENINGU.jsonl` w folderze wtyczki. Plik ten moĹĽe byÄ‡ bezpoĹ›rednio uĹĽyty do fine-tuningu modeli OpenAI oraz OpenSource.

---

## đź› ď¸Ź 8. Diagnostyka i WydajnoĹ›Ä‡

- **Pasek HUD**: Sprawdzaj na dole okna czatu, czy Agent jest poĹ‚Ä…czony z modelem LLM.
- **TrimHistory**: Przy bardzo dĹ‚ugich sesjach Agent automatycznie "zapomina" najstarsze, techniczne logi, aby zachowaÄ‡ szybkoĹ›Ä‡ reakcji (nie tracÄ…c przy tym pamiÄ™ci o Twoich zmiennych `@`).
- **Logi NarzÄ™dzi**: JeĹ›li coĹ› nie dziaĹ‚a, otwĂłrz zakĹ‚adkÄ™ "Logi NarzÄ™dzi" â€“ zobaczysz tam dokĹ‚adnie, jaki JSON zostaĹ‚ wysĹ‚any i co odpowiedziaĹ‚ BricsCAD.
- **ZakĹ‚adka Debug (đź›)**: Zaawansowane narzÄ™dzie diagnostyczne. Pozwala Ĺ›ledziÄ‡ komunikacjÄ™ na linii Agent -> C# -> Silnik BricsCAD (zdarzenia bazy Teigha). UĹĽywaj jej, gdy narzÄ™dzia "udajÄ…", ĹĽe coĹ› zrobiĹ‚y, ale zmiany nie sÄ… widoczne na ekranie.

---
> [!IMPORTANT]
> **BezpieczeĹ„stwo**: Agent V2 wykonuje wiÄ™kszoĹ›Ä‡ operacji wewnÄ…trz transakcji. JeĹ›li wystÄ…pi bĹ‚Ä…d krytyczny, system sprĂłbuje wycofaÄ‡ zmiany (Rollback), aby nie uszkodziÄ‡ rysunku.

---

## đź‘ď¸Ź 11. MoĹĽliwoĹ›ci Wizyjne (Multimodal Vision) - NOWOĹšÄ† v2.20.8

Wersja **v2.20.8 GOLD** wprowadza obsĹ‚ugÄ™ modeli multimodalnych (VLM). Agent moĹĽe teraz "widzieÄ‡" TwĂłj rysunek, co pozwala na analizÄ™ elementĂłw, ktĂłre nie sÄ… natywnie rozpoznawalne (np. raster, PDF, skomplikowane symbole graficzne).

### 11.1. Przechwytywanie obszaru - `CaptureVisionArea`
NarzÄ™dzie to pozwala na wskazanie okna w BricsCAD, ktĂłre zostanie "uwiecznione" i przesĹ‚ane do Agenta.
- **DziaĹ‚anie**: Program wykonuje `ZoomWindow` do wskazanego obszaru, odczekuje na odĹ›wieĹĽenie grafiki i wykonuje zrzut ekranu wysokiej jakoĹ›ci.
- **Przetwarzanie**: Obraz jest automatycznie doĹ‚Ä…czany do Twojego zapytania jako wiadomoĹ›Ä‡ uĹĽytkownika z zakodowanym obrazem (standard OpenAI Vision).

### 11.2. Polecenia CLI dla Wizji
MoĹĽesz wywoĹ‚ywaÄ‡ funkcje wizyjne bezpoĹ›rednio z paska poleceĹ„ BricsCAD:

- **`SKAN`**: Szybkie przechwytywanie. Pozwala wskazaÄ‡ obszar i zapisuje obraz do folderu tymczasowego. Idealne, gdy chcesz najpierw coĹ› zaznaczyÄ‡, a potem zapytaÄ‡ Agenta.
- **`AI_VISION`**: PeĹ‚ny proces analityczny. 
    1. Wskazujesz obszar.
    2. Wpisujesz pytanie (np. "Odczytaj tabelkÄ™ z tego rysunku").
    3. Agent automatycznie otrzymuje obraz i Twoje pytanie, a nastÄ™pnie zwraca odpowiedĹş.

> [!IMPORTANT]
> **PrywatnoĹ›Ä‡ i PamiÄ™Ä‡**: Wszystkie zrzuty ekranu (`AgentVision_*.jpg`) sÄ… przechowywane w systemowym folderze `%TEMP%`. Agent automatycznie czyĹ›ci ten folder przy kaĹĽdym uruchomieniu wtyczki, aby nie zajmowaÄ‡ miejsca na dysku.

### 11.3. Kiedy uĹĽywaÄ‡ Wizji?
- **OCR Tabel**: Szybkie przepisywanie danych z tabel tekstowych, ktĂłre sÄ… "rozbitymi" liniami/tekstem.
- **WyjaĹ›nianie BĹ‚Ä™dĂłw**: PokaĹĽ Agentowi fragment rysunku i zapytaj: "Dlaczego te kreskowania nachodzÄ… na siebie?".
- **Inwentaryzacja**: Analiza podkĹ‚adĂłw rastrowych (skanĂłw) w celu zliczenia symboli.

---

*Wersja Systemu: v2.20.8 GOLD | BricsCAD Agent AI Project*

---

## đźš€ 10. Recepty (System DrogowskazĂłw) - NOWOĹšÄ† v2.16.0

System receptur pozwala na tworzenie "skrĂłtĂłw myĹ›lowych" dla Agenta. Zamiast tĹ‚umaczyÄ‡ mu za kaĹĽdym razem jak ma coĹ› narysowaÄ‡, moĹĽesz stworzyÄ‡ recepturÄ™ wywoĹ‚ywanÄ… specjalnym znakiem `$`.

### 10.1. Jak uĹĽywaÄ‡ znaku `$`?
Wpisz `$` a nastÄ™pnie nazwÄ™ wyzwalacza, aby "pokazaÄ‡" Agentowi jak ma wykonaÄ‡ dane zadanie.
- PrzykĹ‚ad: *"ZrĂłb to uĹĽywajÄ…c $kopiuj_warstwe"*
- Agent zobaczy TwojÄ… zapisanÄ… wczeĹ›niej instrukcjÄ™ oraz poprawny ciÄ…g wywoĹ‚aĹ„ narzÄ™dzi, co gwarantuje 100% precyzji.

### 10.2. Tworzenie PrzepisĂłw (Capture)
Najszybszym sposobem na stworzenie przepisu jest przechwycenie udanej sesji:
1. Pracuj z Agentem w zakĹ‚adce **Aktualna sesja**, aĹĽ osiÄ…gniesz poĹĽÄ…dany efekt.
2. Kliknij przycisk **"âś¨ PrzechwyÄ‡ jako Przepis"**.
3. System automatycznie przeniesie CiÄ™ do zakĹ‚adki **Recepty**, parsujÄ…c sesjÄ™ i wyciÄ…gajÄ…c z niej same wywoĹ‚ania narzÄ™dzi.
4. Nadaj przepisowi nazwÄ™ (np. `duplikuj_osie`) i kliknij **Zapisz**.

### 10.3. Tworzenie od podstaw (v2.18.0)
MoĹĽesz teraz tworzyÄ‡ recepty bez interakcji z Agentem:
1. PrzejdĹş do zakĹ‚adki **Recepty**.
2. Kliknij **"âž• Nowa Recepta"** i podaj nazwÄ™ wyzwalacza.
3. Wpisz instrukcjÄ™ JSON lub uzupeĹ‚nij przepis korzystajÄ…c z **Tool Sandboxa**.

### 10.4. Integracja z Tool Sandboxem
Podczas testowania narzÄ™dzi w Sandboxie moĹĽesz w dowolnej chwili wysĹ‚aÄ‡ skonfigurowane wywoĹ‚anie do biblioteki receptur:
1. Kliknij **"âś¨ WyĹ›lij do Recepty"** w Sandboxie.
2. Wybierz z menu istniejÄ…cÄ… receptÄ™ (program dopisze narzÄ™dzie na koĹ„cu sekwencji) lub stwĂłrz nowÄ….

### 10.5. Kategoryzacja Receptur
W edytorze receptur moĹĽesz zaznaczyÄ‡ kategorie narzÄ™dzi (np. `#warstwy`, `#bloki`), ktĂłre majÄ… zostaÄ‡ automatycznie zaĹ‚adowane do pamiÄ™ci podrÄ™cznej Agenta w momencie uĹĽycia przepisu. Eliminuje to potrzebÄ™ rÄ™cznego wpisywania tagĂłw przy kaĹĽdym zapytaniu.

### 10.6. Natychmiastowe Wykonanie ($trigger$) - v2.17.0
Wersja 2.17.0 wprowadza "Tryb Makra", ktĂłry pozwala na wykonanie przepisu bez angaĹĽowania sztucznej inteligencji.
- **SkĹ‚adnia**: Zamknij nazwÄ™ triggera w dwa znaki dolara, np. `$duplikuj_warstwe$`.
- **DziaĹ‚anie**: Program od razu wykona zapisanÄ… sekwencjÄ™, co jest idealne dla czÄ™sto powtarzanych, pewnych czynnoĹ›ci technicznych.

### 10.7. Eksport do ZĹ‚otego Standardu (v2.19.0)
JeĹ›li Twoja recepta dziaĹ‚a idealnie, moĹĽesz jÄ… "ozĹ‚ociÄ‡", czyli dodaÄ‡ jako idealny przykĹ‚ad treningowy do bazy wiedzy AI:
1. W zakĹ‚adce **Recepty** wybierz przepis i kliknij **"âś¨ ZĹ‚oty Standard"**.
2. Podaj zapytanie uĹĽytkownika (np. "Wstaw okno i osie").
3. Opcjonalnie dodaj dodatkowe tagi (kategorie narzÄ™dzi), ktĂłre AI powinno mieÄ‡ w pamiÄ™ci podczas nauki tego przykĹ‚adu.
4. Program skompiluje peĹ‚ny rekord JSONL (System Prompt + NarzÄ™dzia + Konwersacja) i dopisze go do pliku treningowego.

### 10.8. Testowanie i Debugowanie Receptur
W zakĹ‚adce **Recepty** znajdziesz dwa tryby weryfikacji:
1. **đź§Ş Testuj w Sandboxie**: PrzesyĹ‚a wybrany krok receptury do Tool Sandboxa. JeĹ›li przepis ma wiele krokĂłw, program zapyta CiÄ™, ktĂłry z nich chcesz przetestowaÄ‡.
2. **đźš€ Testuj SekwencjÄ™**: Uruchamia caĹ‚y przepis natychmiast w BricsCAD. W przypadku bĹ‚Ä™du w skĹ‚adni JSON lub bĹ‚Ä™dnego dziaĹ‚ania narzÄ™dzia, system wyĹ›wietli szczegĂłĹ‚owy log z diagnozÄ… i sugestiÄ… poprawki.

---

## đź•µď¸Ź 12. Rewident (MĂłzg QA) i Automatyczne Testowanie (v2.28.3 GOLD)

Z myĹ›lÄ… o pisaniu i testowaniu nowych narzÄ™dzi (skilli i recept) oraz weryfikacji kodu C#, udostÄ™pniono profil **MĂłzgu QA** (`AuditorProfile`). Ten wyspecjalizowany agent-inĹĽynier operuje w trybie izolowanym, z peĹ‚nym dostÄ™pem do odczytu kodu ĹşrĂłdĹ‚owego systemu oraz potÄ™ĹĽnym arsenaĹ‚em do diagnostyki i komunikacji.

### 12.1. Testy Statyczne (RunToolTest)
MĂłzg QA nie musi rÄ™cznie wchodziÄ‡ do interfejsu testowego. Posiada narzÄ™dzie `RunToolTest`, ktĂłre pozwala mu w Ĺ›rodowisku pamiÄ™ci przygotowaÄ‡ wirtualny Ĺ‚adunek (JSON) i wstrzyknÄ…Ä‡ go do Orkiestratora. 
W przypadku awarii kodu C# (np. bĹ‚Ä…d `NullReferenceException`), testowany proces jest przechwytywany i bezpiecznie zatrzymywany, a sam MĂłzg bada "StackTrace", dowiadujÄ…c siÄ™, co i w ktĂłrej linii kodu zawiodĹ‚o.
*Zawsze stosuje flagÄ™ testowÄ… `__DryRun: true` lub `__MockResponse` dla bezpieczeĹ„stwa rysunku uĹĽytkownika.*

### 12.2. Raportowanie BĹ‚Ä™dĂłw (WriteQAReport)
Po zakoĹ„czonym audycie i zlokalizowaniu usterki, MĂłzg QA tworzy czytelny, sformatowany raport w formacie Markdown z analizÄ… bĹ‚Ä™du. Wykorzystuje do tego narzÄ™dzie `WriteQAReport`, zapisujÄ…ce dokumenty w folderze `Autotesty/Reports/`, co zostawia trwaĹ‚y Ĺ›lad z przeprowadzonych testĂłw.

### 12.3. PoĹ‚Ä…czenie z ZewnÄ™trznym Deweloperem (Antigravity AI)
MĂłzg QA ma zakaz samodzielnego przepisywania wraĹĽliwych plikĂłw ĹşrĂłdĹ‚owych C# w czasie pracy BricsCADa. JeĹ›li zdiagnozuje usterkÄ™, ktĂłra wymaga modyfikacji rdzenia systemu, wysyĹ‚a oficjalne zgĹ‚oszenie za pomocÄ… narzÄ™dzia `DelegateTaskToAntigravity`.
- System generuje bilet/raport w formacie Markdown w folderze `Autotesty/TasksForAntigravity/`.
- ZewnÄ™trzny Agent Kodowania (np. Antigravity AI) moĹĽe bezpiecznie wejĹ›Ä‡ w ten folder, przeczytaÄ‡ wytyczne MĂłzgu i nanieĹ›Ä‡ profesjonalne poprawki w repozytorium uĹĽytkownika.
- Eliminuje to ryzyko niekontrolowanego zawieszenia interfejsu BricsCAD i zachowuje peĹ‚ny "Separation of Concerns".

> [!TIP]
> JeĹ›li z poziomu czatu w BricsCAD natrafisz na jakiĹ› niewyjaĹ›niony bĹ‚Ä…d, po prostu napisz GĹ‚Ăłwnemu Agentowi (Supervisor): *"PoproĹ› MĂłzg o przetestowanie polecenia InsertBlockTool, bo przestaĹ‚o dziaĹ‚aÄ‡"*. MĂłzg sprawdzi kod, odpali symulacjÄ™, napisze raport i zgĹ‚osi do Antigravity potrzebÄ™ naprawy!


## Zarządzanie Skryptami LISP
Bielik V2 potrafi generować, walidować i zapisywać skrypty AutoLISP bezpośrednio do Bazy Wiedzy. Dzięki nowej architekturze Actor-Critic każdy generowany kod posiada wbudowany mechanizm Self-Healing (nadpisana funkcja *error*), który w razie awarii bezpiecznie przywraca środowisko CAD (m.in. OSMODE, CMDECHO) oraz powiadamia agenta o błędzie. Użytkownik ma dostęp do bazy skryptów w specjalnej zakładce 'Skrypty LISP' wewnątrz okna Knowledge Base.

