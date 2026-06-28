# WentCad Memory

Aktualny stan: 2026-06-27

## Cel projektu

`WentCad` jest niezależną wtyczką BricsCAD do pracy z pomieszczeniami, kondygnacjami, systemami wentylacji i podstawowym bilansem wentylacyjnym. Projekt jest kompilowany osobno od `Bricscad_AgentAI_V2` i działa bez uruchomionego Agenta.

## Decyzje architektoniczne

- Projekt leży w `D:\GitHub\Bricscad_AgentAI\WentCad`.
- DLL jest niezależna od `Bricscad_AgentAI_V2`.
- Integracja z Agentem odbywa się wyłącznie przez dane w DWG i komendy BricsCAD.
- `.wentcad` obok DWG jest głównym źródłem prawdy.
- NOD w DWG jest indeksem/kontraktem CAD:
  - `WENTCAD_PROJECT`
  - `WENTCAD_FLOORS`
  - `WENTCAD_ROOMS`
- XData:
  - `WENTCAD_ROOM` na obrysach pomieszczeń
  - `WENTCAD_FLOOR_REGION` na własnych regionach kondygnacji
- `WentCad` czyta `BIELIK_DRUK_WIDOKI`, ale nie zmienia formatu `Bielik.DrukWidoki`.

## Implementacja v1

- Projekt: `WentCad/WentCad.csproj`, `net48`, WPF + WinForms, x64.
- Komendy:
  - `WENTCAD` otwiera panel.
  - `WENTCAD_SYNC` zapisuje `.wentcad`, NOD i XData.
  - `WENTCAD_DRAW_FLOOR_REGION` rysuje własny zakres kondygnacji.
  - `WENTCAD_PICK_FLOOR_REGION` przypisuje istniejącą zamkniętą polilinię jako zakres kondygnacji.
- Panel WPF ma zakładki:
  - Kondygnacje
  - Pomieszczenia
  - Systemy
  - Bilans
  - WATT
  - Eksport
- Reactor `FloorRegionReactor` aktualizuje geometrię kondygnacji po edycji polilinii regionu.
- Skanowanie pomieszczeń działa po wybranej kondygnacji, warstwie obrysów, warstwie metek i mapowaniu atrybutów.
- Bilans obsługuje tryby:
  - `AUTO_MAX`
  - `MANUAL`
  - `HYGIENIC_ONLY`
  - `ACH_ONLY`
- Eksport v1: CSV.
- UI: zakładki `Pomieszczenia` i `Bilans` mają własne menu wyboru kondygnacji spięte z `SelectedFloor`, więc nie trzeba wracać do zakładki `Kondygnacje`, aby zmienić aktualny zakres danych.
- UI: zakładka `Bilans` ma pełny widok tabelaryczny z danymi pomieszczenia, parametrami obliczeniowymi, transferami, wynikowym nawiewem/wywiewem, real ACH, bilansem netto i systemami.
- IFC v1: dodano eksport `IfcProject -> IfcSite -> IfcBuilding -> IfcBuildingStorey -> IfcSpace`; obrysy pomieszczeń są wyciągane jako `IfcExtrudedAreaSolid`.
- Punkt bazowy: każda kondygnacja ma `BasePoint.X/Y` i `BasePointDescription`; opis generowany przez `WENTCAD_PICK_BASE_POINT` zawiera współrzędne, np. `Punkt bazowy X=-3, Y=77`; eksport IFC odejmuje punkt bazowy kondygnacji od współrzędnych obrysów, aby rzuty rozłożone w DWG mogły złożyć się pionowo w IFC.
- WATT v1: dodano model `Thermal.Walls`, `Thermal.Windows`, `Thermal.Settings`, NOD `WENTCAD_WALLS`/`WENTCAD_WINDOWS` oraz XData `WENTCAD_WINDOW`.
- WATT v1: zakładka `WATT` skanuje ściany jako segmenty obrysów pomieszczeń, klasyfikuje `INTERNAL` przez sąsiedztwo pomieszczeń i `EXTERNAL` przez potwierdzenie warstwą ścian albo regionem.
- WATT v1: okna są wykrywane z bloków albo linii/polilinii na warstwie okien, a następnie przypisywane do najbliższej ściany zewnętrznej.
- Wizualizacja WATT: zewnętrzne i nierozstrzygnięte segmenty są rysowane jako overlay na warstwach `_WENTCAD_PRZEGRODY` i `_WENTCAD_PRZEGRODY_UWAGI`.
- UI: zakładka `Struktura` pokazuje read-only model budynku jako projekcję danych `.wentcad`: kondygnacje -> pomieszczenia -> przegrody -> okna. Nie dodaje nowego schematu danych; korzysta z `Rooms`, `Thermal.Walls` i `Thermal.Windows`.
- WATT: dodano `Thermal.HorizontalPartitions` dla poziomych przegród `PG`, `StW`, `D` oraz pola `ConstructionId/ConstructionName` przy ścianach, otworach i przegrodach poziomych. Zakładka `Struktura` pokazuje kody `SZ`, `SW`, `OZ`, `DRZ`, `PG`, `StW`, `D`.
- WATT: drzwi sa wykrywane analogicznie do okien (`DoorLayer`, wzorzec bloku, opisy i atrybuty wymiarowe), maja kod `DRZ` i moga byc przypisane do scian zewnetrznych albo wewnetrznych.
- UI WATT: przycisk `Skanuj WATT - caly budynek` skanuje wszystkie kondygnacje, a zakladka WATT pokazuje takze `PG`, `StW` i `D`.

## Most Agenta

W `Bricscad_AgentAI_V2/src/Tools/WentCad` dodano tool-e bez referencji do DLL `WentCad`:

- `ReadWentCadProject`
- `ReadWentCadRooms`
- `UpdateWentCadProject`
- `ManageWentCadFloors`
- `SetWentCadFloorRegion`
- `ScanWentCadRooms`
- `UpdateWentCadRoomByNumber`
- `ConfigureWentCadTestBuilding`
- `ManageWentCadRooms`
- `ManageWentCadSystems`
- `RecalculateWentCadBalance`
- `RunWentCadCommand`
- `ReadWentCadEnvelope`
- `ScanWentCadEnvelope`
- `UpdateWentCadWall`
- `UpdateWentCadWindow`
- `ConfigureWentCadEnvelopeTestBuilding`

Tool-e czytają NOD lub wysyłają bezpieczne komendy `WENTCAD`/`WENTCAD_SYNC`.
`SetWentCadFloorRegion` i `ScanWentCadRooms` pozwalają agentowi wykonać pełny test CAD bez interaktywnego klikania regionów/skanowania w panelu.

`UpdateWentCadRoomByNumber` jest preferowana sciezka aktualizacji bilansu po skanie, bo wymaga `FloorName/FloorId + Number` i nie tworzy rekordow bez kondygnacji.
`ConfigureWentCadTestBuilding` wykonuje caly scenariusz testowy LISP w jednym wywolaniu, aby uniknac przerwania przez limit dlugiej petli agenta.
`ScanWentCadEnvelope` i `ConfigureWentCadEnvelopeTestBuilding` realizuja WATT bez referencji do `WentCad.dll`; czytaja geometrie DWG oraz zapisują `.wentcad`/NOD/XData.

## Weryfikacja

- `dotnet build WentCad\WentCad.csproj` przechodzi poprawnie.
- `Bricscad_AgentAI_V2/build.ps1` kompiluje źródła, ale pełny rebuild może zatrzymać się na kopiowaniu DLL, jeśli BricsCAD ma załadowane:
  - `Bricscad_AgentAI_V2.dll`
  - `Bielik.DrukWidoki.dll`

To jest blokada pliku przez proces BricsCAD, nie błąd kodu WentCad.

## Środowisko testowe

- Dodano `WentCad/Tests/generate_wentcad_test_building.lsp`.
- Dodano `WentCad/Tests/AGENT_PROMPTS.md` z gotowymi promptami dla `WentCadProfile`, prowadzacymi subagenta przez konfiguracje, kondygnacje, pomieszczenia, systemy, bilans, synchronizacje i raport koncowy.
- Dodano `WentCad/Tests/TESTING.md` z ręczną procedurą testowania panelu, skanowania, bilansu, zapisu, CSV i integracji z Agentem.
- Komenda LISP: `GEN_WENTCAD_TEST_BUILDING`.
- Generator tworzy dwa rzuty kondygnacji, warstwy testowe, obrysy pomieszczeń, blok metki `WC_ROOM_TAG` z atrybutami `NR`, `NAZWA`, `H`, `POW`, okna pomocnicze oraz przypadki diagnostyczne.
- Generator tworzy też blok okna `WC_WINDOW` z atrybutami `WIDTH`, `HEIGHT`, `SILL` oraz blok opisu okna `WC_WINDOW_LABEL` na warstwie `WC_TEST_OPISY_OKIEN`.
- Mapowanie dla panelu WentCad:
  - obrysy: `WC_TEST_OBRYSY`
  - metki: `WC_TEST_METKI`
  - numer: `NR`
  - nazwa: `NAZWA`
  - wysokość: `H`
  - powierzchnia: `POW`
- Mapowanie WATT:
  - ściany: `WC_TEST_SCIANY`
  - okna: `WC_TEST_OKNA`
  - opisy okien: `WC_TEST_OPISY_OKIEN`
  - szerokość: `WIDTH`
  - wysokość: `HEIGHT`
  - parapet: `SILL`

## Następne kroki

1. Przetestować `NETLOAD` dla `WentCad\bin\Debug\WentCad.dll` w BricsCAD.
2. Sprawdzić komendę `WENTCAD` i przełączanie aktywnego DWG.
3. Przetestować odczyt zakresów `BIELIK_DRUK_WIDOKI`.
4. Zrobić test skanowania pomieszczeń na realnym rysunku CadProfi/BricsCAD.
5. Doprecyzować mapowanie metek CadProfi i istniejących XData CadProfi.
6. Rozbudować UI o wygodny wybór warstw/atrybutów z listy DWG.
7. Rozszerzyć IFC o ściany, okna, drzwi i przegródki WATT.
8. Rozbudować WATT o typy przegród, biblioteki U/g i obliczenia strat/zysków ciepła.
