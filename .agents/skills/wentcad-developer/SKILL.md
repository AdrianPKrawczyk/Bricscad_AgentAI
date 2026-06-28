---
name: wentcad-developer
description: Zasady pracy nad niezależną wtyczką WentCad dla BricsCAD. Używaj przy każdej zmianie kodu, architektury, dokumentacji, testów, tool-i Agenta lub kontraktów danych związanych z folderem WentCad, komendami WENTCAD, NOD/XData WENTCAD_* albo integracją WentCad z Bielik.DrukWidoki i Bricscad_AgentAI_V2.
---

# WentCad Developer

## Cel Umiejętności

Prowadź prace nad `WentCad` jako nad osobną wtyczką BricsCAD, a nie jako modułem Agenta. Zachowuj niezależną kompilację, własny model danych i luźną integrację przez DWG/NOD/XData oraz komendy BricsCAD.

## Szybki Workflow

1. Przeczytaj aktualny stan projektu:
   - `WentCad/memory.md`
   - `WentCad/README.md`
   - `WentCad/WentCad.csproj`
2. Przy zmianach kodu BricsCAD użyj także skill-a `bricscad-expert`.
3. Przy zmianach tool-i Agenta utrzymuj zasadę: Agent nie referencjonuje `WentCad.dll`.
4. Przed edycją sprawdź istniejące wzorce w:
   - `WentCad/Core`
   - `WentCad/UI`
   - `Bielik.DrukWidoki`
   - `Bricscad_AgentAI_V2/src/Tools/WentCad`
5. Po zmianach kompiluj co najmniej:
   - `dotnet build WentCad\WentCad.csproj`
6. Jeśli zmieniasz `Bricscad_AgentAI_V2`, użyj:
   - `powershell -ExecutionPolicy Bypass -File build.ps1` w folderze `Bricscad_AgentAI_V2`
7. Jeżeli build Agenta kończy się na kopiowaniu DLL, sprawdź czy BricsCAD nie trzyma załadowanych bibliotek.

## Zasady Architektury

- `WentCad` musi działać bez uruchomionego Agenta.
- `WentCad` nie może mieć referencji do `Bricscad_AgentAI_V2`.
- `Bricscad_AgentAI_V2` nie może mieć referencji do `WentCad.dll`.
- Wspólnym kontraktem są dane DWG i plik `.wentcad`, nie klasy C#.
- `.wentcad` jest źródłem prawdy; NOD i XData są indeksem CAD oraz trwałymi powiązaniami z geometrią.
- `Bielik.DrukWidoki` jest czytany, nie modyfikowany.
- Pierwszy etap WentCad obejmuje wentylację, pomieszczenia, kondygnacje i podstawowy bilans. IFC i WATT są etapami późniejszymi.
- WATT v1 obejmuje detekcję ścian i okien jako model `Thermal` w `.wentcad`; pełne obliczenia strat/zysków są osobnym etapem.
- Tool-e Agenta dla WATT (`ReadWentCadEnvelope`, `ScanWentCadEnvelope`, `UpdateWentCadWall`, `UpdateWentCadWindow`) również nie mogą referencjonować `WentCad.dll`.

## Kiedy Czytać Referencję

Przeczytaj `references/development-guide.md`, gdy zadanie dotyczy:

- nowych komend BricsCAD,
- zmian modelu `.wentcad`,
- NOD lub XData `WENTCAD_*`,
- modelu `Thermal.Walls`, `Thermal.Windows`, `Thermal.Settings`,
- NOD `WENTCAD_WALLS` / `WENTCAD_WINDOWS` albo XData `WENTCAD_WINDOW`,
- skanowania pomieszczeń,
- bilansu wentylacji,
- integracji z `Bielik.DrukWidoki`,
- tool-i Agenta `ReadWentCad*` lub `RunWentCadCommand`,
- testowania lub planowania kolejnego etapu.

## Minimalna Checklista Przed Finalizacją

- Czy `WentCad` nadal kompiluje się jako osobna DLL?
- Czy nie dodano twardej referencji między `WentCad` i `Bricscad_AgentAI_V2`?
- Czy zapis danych zachowuje kontrakt `.wentcad -> NOD -> XData`?
- Czy operacje DWG używają transakcji i `DocumentLock` tam, gdzie trzeba?
- Czy zmiana nie narusza formatu `BIELIK_DRUK_WIDOKI`?
- Czy wynik jest opisany w `WentCad/memory.md` lub dokumentacji, jeśli zmienia filozofię projektu?
