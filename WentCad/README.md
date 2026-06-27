# WentCad

`WentCad` to niezależna wtyczka BricsCAD dla projektów wentylacji. Jej pierwszym celem jest przeniesienie części logiki aplikacji webowej WENTCAD bezpośrednio do DWG: kondygnacje, przypisywanie pomieszczeń z obrysów i metek, systemy wentylacyjne, podstawowy bilans oraz wizualizacja przypisań.

Projekt jest osobną DLL, kompilowaną niezależnie od `Bricscad_AgentAI_V2`. Agent może z nią współpracować, ale nie jest wymagany do działania panelu.

## Zakres v1

Wersja v1 obejmuje:

- panel BricsCAD `WENTCAD`,
- obsługę kondygnacji,
- wykorzystanie zakresów z `Bielik.DrukWidoki`,
- własne regiony kondygnacji na warstwie `_WENTCAD_KONDYGNACJE`,
- skanowanie pomieszczeń z zamkniętych polilinii i bloków z atrybutami,
- zapis trwałego powiązania `RoomId` przez XData,
- systemy nawiewu/wywiewu,
- podstawowy bilans wentylacji,
- kolorowanie obrysów według systemu,
- eksport CSV,
- eksport IFC2x3 pomieszczeń jako `IfcSpace`,
- punkt bazowy kondygnacji do składania wielu rzutów w jednym układzie budynku.

Poza zakresem v1, ale przewidziane dalej:

- automatyczna detekcja ścian zewnętrznych/wewnętrznych,
- detekcja okien,
- bilanse strat i zysków ciepła,
- pełna integracja z logiką WATT.

## Kompilacja

Projekt:

```powershell
dotnet build WentCad\WentCad.csproj
```

Wynik:

```text
WentCad\bin\Debug\WentCad.dll
```

Wtyczkę można załadować w BricsCAD przez `NETLOAD`.

## Komendy BricsCAD

`WENTCAD`

Otwiera dokowalny panel `PaletteSet` z interfejsem WPF.

`WENTCAD_SYNC`

Synchronizuje dane projektu:

- zapisuje plik `.wentcad`,
- aktualizuje NOD w DWG,
- zapisuje XData na obrysach pomieszczeń.

`WENTCAD_DRAW_FLOOR_REGION`

Rysuje prostokątny własny region aktywnej kondygnacji. Region trafia na warstwę `_WENTCAD_KONDYGNACJE` i dostaje XData `WENTCAD_FLOOR_REGION`.

`WENTCAD_PICK_FLOOR_REGION`

Pozwala wskazać istniejącą zamkniętą polilinię jako region aktywnej kondygnacji. To jest preferowana ścieżka dla rzutów, które mają już prowadnice kondygnacji, np. z LISP testowego albo z innego narzędzia. Wybrana polilinia trafia na warstwę `_WENTCAD_KONDYGNACJE`, dostaje XData `WENTCAD_FLOOR_REGION`, a jej wierzchołki zapisują się w `.wentcad`.

`WENTCAD_PICK_BASE_POINT`

Pozwala wskazać punkt bazowy aktywnej kondygnacji, np. przecięcie osi `A-1`. Ten punkt powinien oznaczać ten sam fizyczny punkt budynku na każdej kondygnacji. Przy eksporcie IFC współrzędne pomieszczeń są liczone względem punktu bazowego swojej kondygnacji.

## Warstwy danych

WentCad używa trzech warstw danych.

### 1. Plik `.wentcad`

Plik `.wentcad` leży obok DWG i jest głównym źródłem prawdy.

Zawiera:

- `ProjectId`,
- kondygnacje,
- pomieszczenia,
- systemy wentylacyjne,
- ustawienia skanowania,
- pola bilansowe.

Dla niezapisanego DWG plik trafia do `%APPDATA%\WentCad\Untitled.wentcad`.

### 2. NOD w DWG

NOD przechowuje indeks i publiczny kontrakt CAD:

- `WENTCAD_PROJECT`
- `WENTCAD_FLOORS`
- `WENTCAD_ROOMS`

To z tej warstwy korzystają tool-e Agenta. Dzięki temu Agent nie musi ładować ani referencjonować DLL `WentCad`.

### 3. XData

`WENTCAD_ROOM` jest zapisywane na obrysach pomieszczeń.

Najważniejsze pola:

- `ProjectId`,
- `RoomId`,
- `FloorId`,
- `Number`,
- `Name`,
- `SupplySystemId`,
- `ExhaustSystemId`,
- `Area`,
- `Volume`,
- `SupplyFlow`,
- `ExhaustFlow`.

`WENTCAD_FLOOR_REGION` jest zapisywane na własnych poliliniach regionów kondygnacji i zawiera `FloorId`.

## Współpraca z Bielik.DrukWidoki

`WentCad` czyta NOD:

```text
BIELIK_DRUK_WIDOKI
```

Zakresy z `Bielik.DrukWidoki` mogą być użyte jako regiony kondygnacji. `WentCad` nie zmienia ich formatu. Powiązanie `ViewId -> FloorId` jest zapisywane wyłącznie w modelu `.wentcad` i we własnym NOD WentCad.

Jeżeli dla kondygnacji nie ma zakresu DrukWidoki, użytkownik może narysować własny region.

## Skanowanie pomieszczeń

Skanowanie działa w zakresie aktywnej kondygnacji:

- region DrukWidoki, jeżeli przypisano `DrukWidokiViewId`,
- własny region WentCad, jeżeli został narysowany,
- cały model, jeżeli regionu brak.

Użytkownik ustawia mapowanie:

- warstwa obrysów,
- warstwa metek,
- atrybut numeru,
- atrybut nazwy,
- opcjonalny atrybut wysokości,
- opcjonalny atrybut powierzchni.

Obsługiwane są:

- dowolne zamknięte polilinie jako obrysy,
- dowolne bloki z atrybutami jako metki.

Dopasowanie odbywa się przez położenie punktu wstawienia metki wewnątrz obrysu.

## Bilans v1

Silnik bilansu obsługuje tryby:

`AUTO_MAX`

Przyjmuje większą wartość z obliczeń higienicznych i ACH.

`MANUAL`

Używa ręcznie wpisanego nawiewu i wywiewu.

`HYGIENIC_ONLY`

Liczy przepływ z liczby osób i dawki na osobę.

`ACH_ONLY`

Liczy przepływ z kubatury i zadanej krotności wymian.

Dodatkowo obliczane są:

- kubatura,
- realne ACH,
- bilans netto,
- sumy systemów nawiewnych i wywiewnych.

## Integracja z Agentem

Agent V2 ma osobne tool-e w:

```text
Bricscad_AgentAI_V2/src/Tools/WentCad
```

Tool-e:

- `ReadWentCadProject` odczytuje projekt, kondygnacje i pomieszczenia z NOD,
- `ReadWentCadRooms` odczytuje pomieszczenia, opcjonalnie filtrowane po `FloorId`,
- `SetWentCadFloorRegion` ustawia region kondygnacji po punktach albo prostokącie bez klikania w panelu,
- `ScanWentCadRooms` skanuje obrysy i metki w regionie kondygnacji bez klikania w panelu oraz uzupełnia `BoundaryHandle`/`TagHandle`,
- `UpdateWentCadRoomByNumber` bezpiecznie aktualizuje istniejące pomieszczenie po `FloorName/FloorId + Number` i nie tworzy rekordów bez kondygnacji,
- `ConfigureWentCadTestBuilding` wykonuje pełny scenariusz testowy LISP w jednym wywołaniu, aby ominąć limit długiej pętli agenta,
- `RunWentCadCommand` wysyła do BricsCAD tylko jawnie dozwolone komendy `WENTCAD` i `WENTCAD_SYNC`.

Nie ma referencji projektowej ani binarnej z Agenta do `WentCad.dll`.

## Eksport IFC

Eksport IFC v1 tworzy plik `.ifc` obok pliku `.wentcad`.

Struktura IFC:

- `IfcProject`
- `IfcSite`
- `IfcBuilding`
- `IfcBuildingStorey`
- `IfcSpace`

Każde pomieszczenie z poprawnym obrysem polilinii jest eksportowane jako `IfcSpace` z bryłą `IfcExtrudedAreaSolid`. Wysokość bryły jest pobierana z wysokości pomieszczenia, a rzędna kondygnacji z pola `Elevation`.

Współrzędne XY są transformowane według zasady:

```text
X_ifc = X_dwg - BasePoint.X
Y_ifc = Y_dwg - BasePoint.Y
```

Dla budynku wielokondygnacyjnego należy wskazać na każdej kondygnacji ten sam fizyczny punkt bazowy, np. przecięcie osi `A-1`. Jeżeli rzuty kondygnacji są rozłożone obok siebie w jednym DWG, każdy z nich może mieć inny `BasePoint.X/Y`, ale po eksporcie ich geometria złoży się we wspólnym układzie.

Do każdego `IfcSpace` dopisywany jest `Pset_WentCad_Ventilation` z podstawowymi danymi:

- powierzchnia,
- wysokość,
- kubatura,
- nawiew,
- wywiew,
- real ACH,
- tryb obliczeń,
- identyfikatory systemów nawiewu i wywiewu.

## Najważniejsze pliki

- `WentCad.csproj` - osobny projekt DLL.
- `UI/PaletteSetManager.cs` - komendy BricsCAD i PaletteSet.
- `UI/MainView.xaml` - panel WPF.
- `Core/ProjectFileService.cs` - plik `.wentcad`.
- `Core/NodManager.cs` - zapis/odczyt NOD.
- `Core/GeometryManager.cs` - XData, warstwy, kolorowanie, geometria.
- `Core/RoomScanner.cs` - skanowanie pomieszczeń.
- `Core/BalanceEngine.cs` - podstawowy bilans.
- `Reactors/FloorRegionReactor.cs` - aktualizacja regionów kondygnacji po edycji.

## Uwagi projektowe

- XData WentCad nie zastępuje danych CadProfi. To osobny kontrakt aplikacji.
- W kolejnych etapach warto dodać parser istniejącego XData CadProfi, aby automatycznie rozpoznawać metki i obrysy tworzone przez CadProfi.
- Regiony kondygnacji powinny docelowo obsługiwać nie tylko prostokąty, ale dowolne polilinie.
- UI v1 jest techniczne i służy do walidacji przepływu danych. Następny krok to wygodniejsze selektory warstw, atrybutów, systemów i mapowania CadProfi.
