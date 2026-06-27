# WentCad Development Guide

## Filozofia

`WentCad` ma być narzędziem projektowym działającym bezpośrednio w BricsCAD. Agent AI jest partnerem odczytowym i sterującym, ale nie fundamentem działania aplikacji. Kod powinien być projektowany tak, aby użytkownik mógł załadować `WentCad.dll`, otworzyć panel `WENTCAD` i pracować bez jakiegokolwiek procesu Agenta.

Najważniejsze napięcie projektowe: dane muszą być wystarczająco bogate dla bilansów i automatyzacji, ale jednocześnie stabilne w DWG. Dlatego model domenowy trzymamy w `.wentcad`, a DWG przechowuje indeks i trwałe powiązania z obiektami.

## Granice Projektów

`WentCad`

- Osobna wtyczka BricsCAD.
- Własne komendy, panel, modele, serwisy i reactor-y.
- Referencje do BricsCAD/Teigha, WPF/WinForms i Newtonsoft.Json są dozwolone.
- Referencja do `Bricscad_AgentAI_V2` jest zabroniona.

`Bricscad_AgentAI_V2`

- Może czytać NOD `WENTCAD_*`.
- Może wysyłać do BricsCAD bezpieczne komendy `WENTCAD` i `WENTCAD_SYNC`.
- Nie może ładować ani referencjonować `WentCad.dll`.
- Tool-e Agenta powinny działać nawet wtedy, gdy panel WentCad nie jest otwarty, o ile dane są zapisane w DWG.

`Bielik.DrukWidoki`

- Pozostaje niezależny.
- `WentCad` czyta NOD `BIELIK_DRUK_WIDOKI`.
- Nie zmieniaj formatu danych DrukWidoki z poziomu WentCad.
- Powiązanie zakresu DrukWidoki z kondygnacją zapisuj w modelu WentCad jako `DrukWidokiViewId`.

## Kontrakt Danych

Warstwy danych:

1. `.wentcad` obok DWG - źródło prawdy.
2. NOD w DWG - indeks i kontrakt dla Agenta oraz innych automatyzacji.
3. XData na geometrii - trwałe powiązanie obiektu CAD z encją domenową.

NOD:

- `WENTCAD_PROJECT`
- `WENTCAD_FLOORS`
- `WENTCAD_ROOMS`

XData:

- `WENTCAD_ROOM` na obrysach pomieszczeń.
- `WENTCAD_FLOOR_REGION` na własnych regionach kondygnacji.

Warstwa:

- `_WENTCAD_KONDYGNACJE` dla własnych regionów kondygnacji.

Nie zapisuj dużych, pełnych modeli w XData. XData ma zawierać identyfikatory i najważniejsze pola wynikowe. Pełny model trzymać w `.wentcad` i indeksować w NOD.

## Model Domenowy

Kondygnacja (`FloorDef`) powinna opisywać:

- `FloorId`,
- nazwę,
- kolejność,
- rzędną,
- wysokości,
- punkt bazowy,
- `DrukWidokiViewId`,
- własny `Region`.

Pomieszczenie (`RoomDef`) powinno opisywać:

- `RoomId`,
- `FloorId`,
- numer i nazwę,
- uchwyty obrysu i metki,
- system nawiewny i wywiewny,
- powierzchnię, wysokość i kubaturę,
- dane do bilansu,
- wyniki bilansu.

System (`SystemDef`) powinien opisywać:

- `SystemId`,
- nazwę,
- typ `SUPPLY` albo `EXHAUST`,
- kolor ACI,
- sumy przepływów.

## Praca Z DWG

Stosuj standardy BricsCAD .NET:

- Używaj `DocumentLock` przy modyfikacji aktywnego dokumentu.
- Używaj transakcji dla odczytu i zapisu DWG.
- Nie trzymaj długo otwartych obiektów DBObject.
- Reactory powinny zbierać zmiany i zapisywać je bezpiecznie na `Application.Idle`, a nie wykonywać rozbudowane operacje bezpośrednio w zdarzeniu modyfikacji.
- Nie zakładaj, że handle jest zawsze poprawny; obsługuj usunięte lub brakujące obiekty.

## UI

Panel `WENTCAD` powinien pozostać narzędziem roboczym, nie landing page.

Priorytety UI:

- szybkie skanowanie i korekta danych,
- czytelne tabele,
- minimalna liczba ozdobników,
- jawne zapisanie/synchronizacja,
- ergonomia pracy na aktywnym DWG.

Unikaj logiki DWG bezpośrednio w XAML/code-behind. Code-behind może wywoływać akcje ViewModelu, a operacje CAD powinny trafiać do `Core`, `Jigs`, `Reactors` albo komend.

## Skanowanie Pomieszczeń

Skanowanie v1:

- obrysy: zamknięte polilinie na wybranej warstwie,
- metki: bloki z atrybutami na wybranej warstwie,
- dopasowanie: punkt wstawienia metki wewnątrz obrysu,
- zakres: aktywna kondygnacja.

Obsługuj przypadki diagnostyczne:

- brak metki w obrysie,
- metka poza obrysem,
- wiele metek w jednym obrysie,
- niezamknięty obrys,
- brak regionu kondygnacji,
- nieistniejąca warstwa.

Nie koduj na sztywno CadProfi jako jedynego źródła. CadProfi ma być jedną z możliwych integracji przez mapowanie warstw, atrybutów i później parser jego XData.

## Bilans

Tryby v1:

- `AUTO_MAX` - większy wynik z higienicznego i ACH.
- `MANUAL` - ręczne przepływy.
- `HYGIENIC_ONLY` - osoby razy dawka.
- `ACH_ONLY` - kubatura razy ACH.

Po zmianie danych wejściowych przelicz:

- kubaturę,
- nawiew,
- wywiew,
- real ACH,
- bilans netto,
- sumy systemów.

Nie wprowadzaj w v1 pełnych strat/zysków ciepła ani WATT bez osobnego etapu projektowego. Jeśli zadanie dotyczy WATT, najpierw rozdziel je na model przegród, detekcję geometrii i kalkulator.

## Integracja Z Agentem

Tool-e Agenta powinny być małe i odseparowane:

- `ReadWentCadProject` - czyta NOD i zwraca JSON.
- `ReadWentCadRooms` - czyta pokoje, opcjonalnie filtruje po `FloorId`.
- `RunWentCadCommand` - wysyła tylko whitelisted komendy.

Nie importuj namespace `WentCad` w projekcie Agenta. Nie dodawaj ProjectReference ani Reference do DLL. Jeżeli potrzebujesz nowego pola dla Agenta, zapisz je w kontrakcie NOD albo XData.

## Kompilacja I Walidacja

Po zmianach w `WentCad`:

```powershell
dotnet build WentCad\WentCad.csproj
```

Po zmianach w `Bricscad_AgentAI_V2`:

```powershell
cd Bricscad_AgentAI_V2
powershell -ExecutionPolicy Bypass -File build.ps1
```

Nie traktuj `dotnet build Bricscad_AgentAI_V2\Bricscad_AgentAI_V2.csproj` jako wiarygodnego testu dla starego projektu .NET Framework, bo może fałszywie zgłaszać brak pakietów NuGet.

Jeżeli build kończy się błędem kopiowania DLL, sprawdź czy BricsCAD nie trzyma załadowanej biblioteki.

## Dokumentacja Przy Zmianach

Aktualizuj:

- `WentCad/memory.md`, gdy zmienia się stan projektu, decyzja architektoniczna albo następny krok.
- `WentCad/README.md`, gdy zmienia się komenda, kontrakt danych, workflow użytkownika albo zakres v1.
- Ten skill, gdy pojawi się nowa zasada pracy, której przyszły agent z czystym kontekstem nie powinien zgadywać.
