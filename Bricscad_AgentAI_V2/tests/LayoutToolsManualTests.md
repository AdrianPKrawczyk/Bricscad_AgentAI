# Testy manualne - Narzędzia Layout/Plot/Page Setup

Zestaw poleceń do wklejania agentowi w zakładce **Agent-Czat** lub głównym panelu agenta w celu weryfikacji działania nowych narzędzi layout/plot/page setup.

---

## 1. Wstępne rozpoznanie

```
Wylistuj wszystkie layouty w biezacym rysunku.
```

```
Pokaz mi ustawienia Page Setup dla layoutu A4-PION.
```

---

## 2. Page Setup

```
Ustaw layout A4-PION na papier ISO A4 pionowo, drukarke DWG To PDF, styl acad.ctb, plot type Layout, obrot Zero, skala 1:50.
```

```
W layoucie A4-PION ustaw PlotCentered na true, PlotPlotStyles na true, PlotPlotStyles na true. Ustaw tez ShadePlot na Wireframe.
```

```
Dla layoutu K1-A4 ustaw SkalaFit, OriginX=10, OriginY=10, CustomScaleNumerator=1, CustomScaleDenominator=20. Wycentruj na stronie.
```

```
Skonfiguruj Page Setup layoutu A4-PION: drukarka DWG To PDF.pc3, papier ISO_A4, styl acad.ctb, drukuj z plot styles i lineweights, hidden line off, viewport borders on.
```

---

## 3. Zarządzanie layoutami

```
Stworz nowy layout o nazwie "TEST-A4".
```

```
Stworz layout "A4-PION-2" jako kopie "A4-PION".
```

```
Zmien nazwe layoutu "TEST-A4" na "A4-PION-3".
```

```
Usun layout "TEST-A4".
```

```
Ustaw aktywny layout na "A4-PION".
```

---

## 4. Import szablonów arkuszy

```
Zaimportuj layout "A4-PION" z pliku C:/templates/standard.dwt. Jesli plik nie istnieje, powiedz.
```

```
Zaimportuj layout "ImportA4" z szablonu C:/Users/Adrian/Desktop/template.dwt jako nowy layout. Nadpisz jesli istnieje.
```

```
Importuj tylko Page Setup (bez geometrii) layoutu "TitleBlock" z C:/templates/layout-only.dwt.
```

---

## 5. Eksport szablonów

```
Eksportuj layout "A4-PION" do pliku C:/Users/Adrian/Desktop/a4-template.dwt.
```

```
Wyeksportuj layout "K1-A4" do pliku C:/Users/Adrian/Desktop/k1-template.dwt wraz z powiazanymi blokami.
```

---

## 6. Plotowanie

```
Wydrukuj layout "A4-PION" do PDF i zapisz w C:/export/projekt.pdf.
```

```
Wydrukuj aktywny layout do pliku C:/temp/wydruk.pdf jako PDF, dopasuj skale, wycentruj na stronie.
```

```
Wydrukuj layout A4 do DWF w C:/export/rysunek.dwf.
```

---

## 7. Publish do PDF (batch)

```
Opublikuj wszystkie layouty (oprocz Model) do jednego PDF w C:/export/komplet.pdf w trybie MultiSheet.
```

```
Opublikuj layouty "A4-PION" i "A4-KRAJOBRAZ" do pojedynczych plikow PDF w katalogu C:/export/. Tryb SingleFiles.
```

```
Opublikuj wszystkie layouty do PDF C:/Users/Adrian/Desktop/projekt-final.pdf z haslem "secret123".
```

---

## 8. Style wydruku (CTB/STB)

```
Wylistuj wszystkie dostepne style wydruku CTB i STB.
```

```
Sprawdz czy styl acad.ctb jest zaladowany.
```

```
Zaladuj styl wydruku z pliku C:/Users/Adrian/AppData/Roaming/Bricsys/BricsCAD/V22/pl/PlotStyles/acad.ctb.
```

```
Przypisz styl acad.ctb do wszystkich layoutow (oprocz Model).
```

```
Przypisz styl monochrome.ctb tylko do layoutu A4-PION.
```

---

## 9. Bezpieczenstwo - testowe "anti-blokady"

```
Usun layout Model.
```
Oczekiwany blad - Model nie moze byc usuniety.

```
Zaimportuj layout X z nieistniejacego pliku C:/nie-ma.dwt.
```
Oczekiwany blad - plik nie istnieje.

```
Ustaw PlotType na "Nieprawidlowa".
```
Oczekiwany blad - niedozwolona wartosc + lista dozwolonych.

```
Usun layout "NieistniejacyLayout".
```
Oczekiwany blad - layout nie istnieje.

---

## 10. Zlozone scenariusze

```
Sprawdz ustawienia Page Setup wszystkich layoutow. Wyswietl dla kazdego: papier, urzadzenie, styl, skale, obrot.
```

```
Dla kazdego layoutu ustaw styl monochrome.ctb i PlotCentered na true. Nie modyfikuj Model.
```

```
Stworz 3 nowe layouty "RZ-1", "RZ-2", "RZ-3" jako kopie "A4-PION", ustaw na nich styl acad.ctb. Na koniec wylistuj wszystkie layouty.
```

```
Zaimportuj layout TitleBlock z C:/templates/standard.dwt, ustaw na nim plot type Extents i scale 1:100, potem wydrukuj do PDF.
```

---

## 11. Format RPN (opcjonalnie - zaawansowane)

```
Ustaw OriginX na 12.5 mm dla layoutu A4-PION. OriginY na 25.4 mm.
```

Wartosci z prefixem `RPN:` sa wspierane w PageSetupTool (np. `RPN:Scale = 1/50`).

---

## Uwagi dla testujacego

- **Agent-Czat** - tu rozmawiasz z SubAgent (`SubAgentChatControl`), nie z glownym Supervisorem
- Polecenia trafiaja do glownego agenta, ktory moze delegowac do `CadLayoutProfile` jesli rozpozna intencje
- Jesli chcesz bezposrednio wymusic uzycie `CadLayoutProfile`, dodaj na poczatku: *Uzyj profilu CadLayoutProfile, ...*
- Pierwsze wywolanie po instalacji triggeruje automatyczne generowanie `tools_config.json` z nowymi narzedziami
- Profile widoczne w UI w zakladce **Agenci -> Przeglad**: `CadLayoutProfile` powinien byc na liscie po pierwszym uruchomieniu

---

## Checklist weryfikacji

- [ ] `ListLayoutsTool` - wylistowuje layouty z metadanymi
- [ ] `PageSetupTool` - zmienia Media/Device/Style/PlotType/PlotRotation/Scale
- [ ] `PageSetupTool` - walidacja bledow (PlotType, PlotRotation, StdScaleType)
- [ ] `ManageLayoutTool` - Create/Delete/Rename/Clone/SetCurrent
- [ ] `ManageLayoutTool` - CopyFromTemplate
- [ ] `ImportLayoutTemplateTool` - import z DWT/DWG
- [ ] `ExportLayoutTemplateTool` - eksport do DWT/DWG
- [ ] `PlotLayoutTool` - drukowanie pojedynczego layoutu
- [ ] `PublishToPdfTool` - tryb MultiSheet
- [ ] `PublishToPdfTool` - tryb SingleFiles
- [ ] `PlotStyleTool` - List/GetInfo
- [ ] `PlotStyleTool` - Load (przez PSETUPIN)
- [ ] `PlotStyleTool` - Assign do layoutu/wszystkich/ByName
- [ ] Blokada usuniecia Model
- [ ] Blokada nadpisania bez flagi
- [ ] Walidacja bledow z czytelnymi komunikatami
- [ ] Tagi w `tools_config.json` (sprawdzic w: %APPDATA%/Bricscad_AgentAI/tools_config.json)
- [ ] Profil `CadLayoutProfile` widoczny w UI