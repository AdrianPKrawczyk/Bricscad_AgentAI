# Zestaw Promptow Dla Subagenta WentCadProfile

Ten plik zawiera gotowe prompty testowe dla subagenta `WentCadProfile`, oparte na modelu generowanym przez:

```text
WentCad\Tests\generate_wentcad_test_building.lsp
```

Scenariusz zaklada aktywny DWG z wygenerowanym budynkiem testowym:

- obrysy: `WC_TEST_OBRYSY`,
- metki: `WC_TEST_METKI`,
- atrybut numeru: `NR`,
- atrybut nazwy: `NAZWA`,
- atrybut wysokosci: `H`,
- atrybut powierzchni: `POW`,
- jednostki DWG: `1 jednostka = 1 cm`,
- powierzchnie w metkach: m2,
- kondygnacja `Parter`: zakres prowadnicy okolo `-50,-50` do `1850,1050`,
- kondygnacja `Pietro_1`: zakres prowadnicy okolo `-50,1250` do `1850,2350`,
- diagnostyka: okolice `2050,-50` do `2950,950`.

## Jak Uzywac

Sa dwa warianty pracy:

1. Przez Supervisora: wklej prompt z sekcji `Prompt D0`, aby glowny agent delegowal zadanie do `WentCadProfile`.
2. Bezposrednio w subagencie: otworz czat profilu `WentCadProfile` i wklej prompty `W0` do `W10`.

Prompty sa napisane tak, aby subagent dzialal przez narzedzia:

- `ReadWentCadProject`,
- `ReadWentCadRooms`,
- `UpdateWentCadProject`,
- `ManageWentCadFloors`,
- `ManageWentCadRooms`,
- `UpdateWentCadRoomByNumber`,
- `ManageWentCadSystems`,
- `RecalculateWentCadBalance`,
- `RunWentCadCommand`,
- `ConfigureWentCadTestBuilding`,
- opcjonalnie `read_view_definitions`, `ReadXData`, `FindXData`.

## Prompt D0 - Delegacja Do WentCadProfile

Wklej do glownego Agenta/Supervisora:

```text
Deleguj zadanie do profilu WentCadProfile.

Zadanie:
Przeprowadz pelna obsluge testowego modelu WentCad wygenerowanego przez LISP WentCad\Tests\generate_wentcad_test_building.lsp. Najpierw odczytaj aktualny stan WentCad z DWG, potem skonfiguruj projekt testowy, mapowanie skanowania, kondygnacje, punkty bazowe, systemy wentylacyjne i bilans. Uzywaj wylacznie narzedzi WentCad oraz kontraktu .wentcad/NOD/XData, bez referencji do WentCad.dll. Na koncu zsynchronizuj projekt i zwroc raport: kondygnacje, liczba pomieszczen, systemy, sumy nawiewu/wywiewu, status NOD/XData i ewentualne braki.
```

## Prompt W0 - Audyt Stanu Poczatkowego

```text
Jestes WentCadProfile. Sprawdz aktywny DWG pod katem danych WentCad.

Wykonaj:
1. Odczytaj `ReadWentCadProject`.
2. Jesli projekt jest pusty albo nie zawiera kondygnacji/pomieszczen, nie traktuj tego jako blad - to moze byc stan przed konfiguracja.
3. Zwroc krotki raport:
   - czy istnieje `Project`,
   - ile jest `Floors`,
   - ile jest `Rooms`,
   - czy widac dane z NOD `WENTCAD_*`,
   - jakie sa nastepne kroki dla testu LISP.
```

## Prompt W1 - Otwarcie Panelu I Synchronizacja Startowa

```text
Otworz panel WentCad i wykonaj synchronizacje startowa.

Wykonaj:
1. Uruchom `RunWentCadCommand` z komenda `WENTCAD`.
2. Uruchom `RunWentCadCommand` z komenda `WENTCAD_SYNC`.
3. Odczytaj `ReadWentCadProject`.
4. Zwroc raport, czy panel powinien byc otwarty i czy NOD WentCad zostal zaktualizowany.
```

## Prompt W2 - Konfiguracja Projektu I Mapowania LISP

```text
Skonfiguruj projekt WentCad dla testowego budynku LISP.

Ustaw:
- ProjectName: `WentCad LISP Test Building`
- DetectionMapping:
  - BoundaryLayer: `WC_TEST_OBRYSY`
  - TagLayer: `WC_TEST_METKI`
  - NumberAttribute: `NR`
  - NameAttribute: `NAZWA`
  - HeightAttribute: `H`
  - AreaAttribute: `POW`

Wykonaj:
1. Uzyj `UpdateWentCadProject`.
2. Potem odczytaj `ReadWentCadProject`.
3. Zweryfikuj, ze mapowanie jest zapisane albo opisz, ktore pole nie zostalo zwrocone przez kontrakt NOD.
```

## Prompt W3 - Utworzenie Kondygnacji, Punktow Bazowych I Regionow

```text
Utworz albo zaktualizuj kondygnacje dla testowego budynku LISP.

Kondygnacja 1:
- Name: `Parter`
- Order: 0
- Elevation: 0
- HeightNet: 3.0
- HeightTotal: 3.5
- BasePointX: -50
- BasePointY: -50
- BasePointZ: 0
- BasePointDescription: `Punkt bazowy Parter: lewy dolny naroznik prowadnicy, X=-50, Y=-50, jednostki cm`

Kondygnacja 2:
- Name: `Pietro_1`
- Order: 1
- Elevation: 3.5
- HeightNet: 3.2
- HeightTotal: 3.5
- BasePointX: -50
- BasePointY: 1250
- BasePointZ: 0
- BasePointDescription: `Punkt bazowy Pietro_1: odpowiadajacy lewy dolny naroznik prowadnicy, X=-50, Y=1250, jednostki cm`

Wykonaj:
1. Uzyj `ManageWentCadFloors` dla `Parter`.
2. Uzyj `ManageWentCadFloors` dla `Pietro_1`.
3. Uzyj `SetWentCadFloorRegion` dla `Parter` z `MinX=-50`, `MinY=-50`, `MaxX=1850`, `MaxY=1050`.
4. Uzyj `SetWentCadFloorRegion` dla `Pietro_1` z `MinX=-50`, `MinY=1250`, `MaxX=1850`, `MaxY=2350`.
5. Nie pros uzytkownika o klikanie regionow, jezeli `SetWentCadFloorRegion` jest dostepny.
6. Odczytaj `ReadWentCadProject`.
7. Zweryfikuj, ze `Region` dla obu kondygnacji ma co najmniej 4 punkty.
8. Zwroc `FloorId` obu kondygnacji, bo beda potrzebne do kolejnych krokow.
```

## Prompt W4 - Weryfikacja Po Recznym Skanowaniu Z Panelu

Ten prompt stosuj po tym, jak regiony zostaly przypisane przez `SetWentCadFloorRegion` albo panel `WENTCAD`, a pomieszczenia zostaly przeskanowane przez `ScanWentCadRooms` albo panel.
Ten prompt niczego nie tworzy. Jezeli wynik zawiera `Rooms: []`, to uruchom prompt `W4A`, ktory wykonuje nieinteraktywne skanowanie.

```text
Sprawdz wynik skanowania pomieszczen WentCad dla modelu LISP.

Wykonaj:
1. Odczytaj `ReadWentCadProject`.
2. Odczytaj `ReadWentCadRooms`.
3. Znajdz kondygnacje `Parter` i `Pietro_1`.
4. Najpierw sprawdz, czy `Region` dla `Parter` i `Pietro_1` ma punkty. Jezeli ktorykolwiek `Region` jest pusty, napisz: `Brak regionu kondygnacji - najpierw uzyj WENTCAD_PICK_FLOOR_REGION albo przycisku Wybierz region, inaczej skan pomiesza rzuty`.
5. Sprawdz, czy `Parter` ma pomieszczenia:
   - `0.01` Wiatrolap, H=3.0, Pow=12.00
   - `0.02` Komunikacja, H=3.0, Pow=23.52
   - `0.03` Biuro, H=3.0, Pow=35.52
   - `0.04` Sala spotkan, H=3.0, Pow=47.04
   - `0.05` Open space, H=3.0, Pow=58.09
6. Sprawdz, czy `Pietro_1` ma pomieszczenia:
   - `1.01` Gabinet, H=3.2, Pow=12.00
   - `1.02` Pokoj pracy, H=3.2, Pow=23.52
   - `1.03` Archiwum, H=3.2, Pow=35.52
   - `1.04` Sala szkolen, H=3.2, Pow=47.04
   - `1.05` Socjal, H=3.2, Pow=58.09
7. Jezeli numery `1.xx` sa na `Parter`, a `Pietro_1` jest puste, rozpoznaj blad braku regionu podczas skanowania i nie przechodz do W7.
8. Nie tworz pomieszczen diagnostycznych `X.01` ani `X.02`.
9. Jezeli nie ma pomieszczen, jasno napisz: `W4 to tylko weryfikacja. Uruchom skan w panelu albo uzyj W5`.
10. Zwroc raport zgodnosci i liste brakow.
```

## Prompt W4A - Nieinteraktywne Skanowanie Pomieszczen

```text
Wykonaj nieinteraktywne skanowanie pomieszczen WentCad dla modelu LISP.

Wykonaj:
1. Odczytaj `ReadWentCadProject`.
2. Znajdz kondygnacje `Parter` i `Pietro_1`.
3. Jezeli `Region` kondygnacji jest pusty, najpierw ustaw:
   - `SetWentCadFloorRegion` dla `Parter`: `MinX=-50`, `MinY=-50`, `MaxX=1850`, `MaxY=1050`;
   - `SetWentCadFloorRegion` dla `Pietro_1`: `MinX=-50`, `MinY=1250`, `MaxX=1850`, `MaxY=2350`.
4. Uzyj `ScanWentCadRooms` dla `Parter`.
5. Uzyj `ScanWentCadRooms` dla `Pietro_1`.
6. Uruchom `RecalculateWentCadBalance` z `WriteRoomXData=true`.
7. Odczytaj `ReadWentCadRooms`.
8. Zwroc liczbe obrysow, metek, dopasowanych pomieszczen, liste pokoi oraz potwierdz, czy kazdy pokoj ma `BoundaryHandle` i `TagHandle`.
```

## Prompt W5 - Awaryjne Wypelnienie Pomieszczen Z Danych LISP

Ten prompt pozwala przetestowac tool-e Agenta nawet wtedy, gdy skanowanie z panelu nie zostalo jeszcze wykonane.

```text
Wypelnij pomieszczenia WentCad na podstawie znanych danych z LISP testowego. Najpierw odczytaj projekt i znajdz `FloorId` dla `Parter` oraz `Pietro_1`. Jezeli ich nie ma, utworz je zgodnie z promptem W3.

Przed upsertami odczytaj `ReadWentCadRooms`. Jezeli pokoj o wymaganym numerze juz istnieje na innej kondygnacji, aktualizuj ten konkretny `RoomId` i przenies go na wlasciwy `FloorId`; nie tworz drugiej kopii. Jezeli istnieja duplikaty tego samego numeru, preferuj rekord z `BoundaryHandle`/`TagHandle`, a rekord bez uchwytow zglos jako kandydat do usuniecia.

Dla kazdego nowego pomieszczenia uzyj `ManageWentCadRooms` z `Action=upsert`, ale zawsze z jednoznacznym `FloorId` oraz `Number`. Jezeli pomieszczenie zostalo juz zeskanowane, aktualizuj je przez `UpdateWentCadRoomByNumber` z `FloorName` i `Number`.

Parter:
- Number `0.01`, Name `Wiatrolap`, Area 12.00, Height 3.0
- Number `0.02`, Name `Komunikacja`, Area 23.52, Height 3.0
- Number `0.03`, Name `Biuro`, Area 35.52, Height 3.0
- Number `0.04`, Name `Sala spotkan`, Area 47.04, Height 3.0
- Number `0.05`, Name `Open space`, Area 58.09, Height 3.0

Pietro_1:
- Number `1.01`, Name `Gabinet`, Area 12.00, Height 3.2
- Number `1.02`, Name `Pokoj pracy`, Area 23.52, Height 3.2
- Number `1.03`, Name `Archiwum`, Area 35.52, Height 3.2
- Number `1.04`, Name `Sala szkolen`, Area 47.04, Height 3.2
- Number `1.05`, Name `Socjal`, Area 58.09, Height 3.2

Po upsertach:
1. Uruchom `RecalculateWentCadBalance`.
2. Odczytaj `ReadWentCadRooms`.
3. Zwroc raport z liczba pomieszczen na kazdej kondygnacji i wynikami nawiew/wywiew/kubatura.
```

## Prompt W6 - Systemy Wentylacyjne

```text
Skonfiguruj systemy wentylacyjne dla testu WentCad.

Utworz albo zaktualizuj:
- `N1`, Name `Nawiew parter`, Type `SUPPLY`, ColorIndex 5
- `W1`, Name `Wywiew parter`, Type `EXHAUST`, ColorIndex 1
- `N2`, Name `Nawiew pietro`, Type `SUPPLY`, ColorIndex 4
- `W2`, Name `Wywiew pietro`, Type `EXHAUST`, ColorIndex 6

Nastepnie przypisz:
- wszystkie pomieszczenia `0.xx` do `SupplySystemId=N1` i `ExhaustSystemId=W1`,
- wszystkie pomieszczenia `1.xx` do `SupplySystemId=N2` i `ExhaustSystemId=W2`.

Wykonaj:
1. `ManageWentCadSystems` dla kazdego systemu.
2. `UpdateWentCadRoomByNumber` dla potrzebnych aktualizacji pomieszczen po `FloorName + Number`; nie uzywaj `ManageWentCadRooms` po samym numerze.
3. `RecalculateWentCadBalance`.
4. Zwroc sumy systemow i liste pomieszczen z przypisanymi systemami.
```

## Prompt W7 - Test Trybow Bilansu

```text
Przetestuj rozne tryby bilansu na danych LISP.

Ustaw:
- `0.01` Wiatrolap: `AUTO_MAX`, Occupants 1, DosePerOccupant 30
- `0.02` Komunikacja: `ACH_ONLY`, IsTargetAchManual true, ManualTargetAch 1.5
- `0.03` Biuro: `HYGIENIC_ONLY`, Occupants 4, DosePerOccupant 30
- `0.04` Sala spotkan: `MANUAL`, ManualSupply 900, ManualExhaust 850
- `0.05` Open space: `AUTO_MAX`, Occupants 8, DosePerOccupant 30
- `1.05` Socjal: ActivityType `Pomieszczenie socjalne`

Wykonaj:
1. Odczytaj projekt i sprawdz, ze istnieja kondygnacje `Parter` i `Pietro_1`.
2. Zaktualizuj pomieszczenia przez `UpdateWentCadRoomByNumber` z `FloorName` i `Number`.
3. Uruchom `RecalculateWentCadBalance`.
4. Zwroc tabele: Number, Name, Mode, Volume, CalculatedSupply, CalculatedExhaust, RealAch, NetBalance.
5. W raporcie zwroc uwage, czy `MANUAL` uzywa wartosci recznych i czy `HYGIENIC_ONLY` zalezy od osob oraz dawki.
```

## Prompt W8 - Diagnostyka Negatywna

```text
Zweryfikuj, ze dane diagnostyczne z LISP nie zostaly potraktowane jako poprawne pomieszczenia WentCad.

W LISP istnieja przypadki:
- zamkniety obrys bez metki w rejonie `72,0` do `92,14`,
- metka `X.01` poza obrysem,
- niezamkniety obrys z metka `X.02`.

Wykonaj:
1. Odczytaj `ReadWentCadRooms`.
2. Sprawdz, czy nie istnieja pomieszczenia o numerach `X.01` ani `X.02`.
3. Sprawdz, czy liczba poprawnych pomieszczen testowych wynosi 10, jezeli wykonano pelny test parter + pietro.
4. Jezeli `X.01` albo `X.02` istnieja, zwroc ostrzezenie i zaproponuj usuniecie przez `ManageWentCadRooms` z `Action=delete`, ale nie usuwaj bez jawnej zgody uzytkownika.
```

## Prompt W9 - Synchronizacja, NOD I XData

```text
Zsynchronizuj projekt WentCad i sprawdz kontrakt CAD.

Wykonaj:
1. Uruchom `RecalculateWentCadBalance` z zapisem XData.
2. Uruchom `RunWentCadCommand` z `WENTCAD_SYNC`.
3. Odczytaj `ReadWentCadProject`.
4. Odczytaj `ReadWentCadRooms`.
5. Jesli pomieszczenia maja `BoundaryHandle`, sprawdz wybrane obrysy narzedziem `ReadXData` albo `FindXData` dla RegApp `WENTCAD_ROOM`.
6. Zwroc raport:
   - liczba kondygnacji,
   - liczba pomieszczen,
   - czy NOD zawiera `Project`, `Floors`, `Rooms`,
   - czy XData jest dostepne na obrysach z uchwytami,
   - czy sa pomieszczenia bez `BoundaryHandle`.
```

## Prompt W10 - Raport Koncowy Testu Od Poczatku Do Konca

```text
Przygotuj raport koncowy testu WentCad dla modelu LISP.

Raport ma zawierac:
1. Nazwe projektu i lokalizacje pliku `.wentcad`, jezeli jest dostepna w wyniku narzedzi.
2. Kondygnacje:
   - Parter: rzedna, wysokosc netto/calkowita, punkt bazowy X/Y, opis bazy.
   - Pietro_1: rzedna, wysokosc netto/calkowita, punkt bazowy X/Y, opis bazy.
3. Pomieszczenia:
   - liczba pomieszczen parteru,
   - liczba pomieszczen pietra,
   - lista numer/nazwa/powierzchnia/wysokosc/kubatura.
4. Bilans:
   - tryby obliczen,
   - nawiew,
   - wywiew,
   - Real ACH,
   - netto.
5. Systemy:
   - SystemId,
   - nazwa,
   - typ,
   - suma nawiewu,
   - suma wywiewu.
6. Diagnostyka:
   - czy `X.01` i `X.02` nie zostaly zapisane jako pomieszczenia,
   - czy sa braki `BoundaryHandle`,
   - czy XData `WENTCAD_ROOM` wymaga dodatkowej synchronizacji.
7. Rekomendacje:
   - co uzytkownik powinien jeszcze kliknac w panelu `WENTCAD`, jesli eksport CSV/IFC wymaga dzialania UI.

Najpierw uzyj `ReadWentCadProject` i `ReadWentCadRooms`, a jezeli bilans wyglada na nieaktualny, uruchom `RecalculateWentCadBalance`.
```

## Prompt W11 - WATT Sciany Zewnetrzne I Okna

```text
Przetestuj WATT v1 w WentCad na aktywnym DWG z modelem `GEN_WENTCAD_TEST_BUILDING`.

Zalozenia:
- sciany pomocnicze: `WC_TEST_SCIANY`
- okna: `WC_TEST_OKNA`
- opisy okien: `WC_TEST_OPISY_OKIEN`
- atrybuty okien: `WIDTH`, `HEIGHT`, `SILL`
- tolerancje testowe: sciana wewnetrzna `15`, sciana zewnetrzna `15`, przypiecie okna `25`
- kondygnacje: `Parter`, `Pietro_1`

Wykonaj:
1. Odczytaj `ReadWentCadProject` i `ReadWentCadRooms`.
2. Jesli pomieszczenia nie maja `BoundaryHandle`, najpierw uzyj `ConfigureWentCadTestBuilding`.
3. Uzyj `ConfigureWentCadEnvelopeTestBuilding`.
4. Odczytaj `ReadWentCadEnvelope`.
5. Zwroc raport:
   - liczba scian EXTERNAL/INTERNAL/UNRESOLVED dla kazdej kondygnacji,
   - liczba wykrytych okien i ile ma `RoomId` oraz `WallId`,
   - lista okien z wymiarami `Width/Height/SillHeight`,
   - przypadki ostrzezen z `Message`,
   - czy NOD `WENTCAD_WALLS` i `WENTCAD_WINDOWS` zwraca dane.
6. Jezeli widzisz okno bez przypisania, nie dopisuj go recznie bez uzasadnienia; opisz, czy lezy przy scianie wewnetrznej albo poza tolerancja.
```

## Prompt FULL - Jedno Polecenie Dla Calego Testu

Ten prompt jest dluzszy, ale pozwala uruchomic caly scenariusz w jednym zadaniu dla `WentCadProfile`.

```text
Przeprowadz pelny test WentCadProfile na aktywnym DWG z budynkiem LISP `GEN_WENTCAD_TEST_BUILDING`.

Zalozenia modelu:
- obrysy: `WC_TEST_OBRYSY`
- metki: `WC_TEST_METKI`
- atrybuty: `NR`, `NAZWA`, `H`, `POW`
- parter: prowadnica `-50,-50` do `1850,1050`, baza `-50,-50`, rzedna 0, H netto 3.0, H calk. 3.5
- pietro: prowadnica `-50,1250` do `1850,2350`, baza `-50,1250`, rzedna 3.5, H netto 3.2, H calk. 3.5

Kroki:
1. Odczytaj obecny stan przez `ReadWentCadProject`.
2. Jezeli aktywny DWG jest niezapisany i `ProjectPath` wypada jako `%APPDATA%\WentCad\Untitled.wentcad`, kontynuuj test danych, ale w raporcie koncowym oznacz to jako tryb awaryjny niezapisanego rysunku.
3. Uzyj `ConfigureWentCadTestBuilding` z `ProjectName=WentCad LISP Test Building`, `CleanupOrphans=true`, `DrawRegions=true`.
4. Uruchom `RunWentCadCommand` z `WENTCAD_SYNC`.
5. Odczytaj projekt i pomieszczenia ponownie.
6. Zwroc raport koncowy: kondygnacje, base pointy, liczba pomieszczen, systemy, sumy, tabela bilansu, diagnostyka `X.01`/`X.02`, status NOD/XData.
7. Uzyj `ConfigureWentCadEnvelopeTestBuilding`, a potem `ReadWentCadEnvelope`, zeby sprawdzic WATT.
8. W statusie NOD/XData nie pisz, ze XData zostalo zapisane na obrysach, jezeli `BoundaryHandle` jest pusty. Napisz wtedy, ze NOD/.wentcad sa zapisane, ale XData wymaga skanu lub uchwytow obrysow.
```
