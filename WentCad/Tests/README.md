# WentCad Test Environment

Ten folder zawiera proste środowisko testowe AutoLISP do wygenerowania budynku testowego w aktywnym DWG BricsCAD.

## Pliki

- `generate_wentcad_test_building.lsp` - generator budynku z 2 kondygnacjami, obrysami pomieszczeń i metkami blokowymi.
- `TESTING.md` - pełna instrukcja ręcznego testowania przepływu WentCad na wygenerowanym modelu.

## Uruchomienie

1. Otwórz pusty rysunek DWG w BricsCAD.
2. Załaduj plik:

```text
APPLOAD
D:\GitHub\Bricscad_AgentAI\WentCad\Tests\generate_wentcad_test_building.lsp
```

3. Uruchom komendę:

```text
GEN_WENTCAD_TEST_BUILDING
```

## Dane Do Skanowania WentCad

W panelu `WENTCAD`, w zakładce `Pomieszczenia`, ustaw:

- warstwa obrysów: `WC_TEST_OBRYSY`
- warstwa metek: `WC_TEST_METKI`
- atrybut numeru: `NR`
- atrybut nazwy: `NAZWA`
- atrybut wysokości: `H`
- atrybut powierzchni: `POW`

## Kondygnacje

Generator tworzy dwa rzuty w model space:

- `Parter` w obszarze od około `0,0` do `60,35`
- `Pietro_1` w obszarze od około `0,80` do `60,115`

Na warstwie `_WENTCAD_KONDYGNACJE` tworzone są prostokątne regiony pomocnicze kondygnacji. W aktualnym v1 WentCad najlepiej dodać kondygnacje w panelu i narysować/przypisać regiony testowe zgodnie z tymi obszarami.

## Przypadki Testowe

Generator tworzy:

- 5 poprawnych pomieszczeń na parterze,
- 5 poprawnych pomieszczeń na piętrze,
- jedno pomieszczenie bez metki,
- jedną metkę poza obrysem,
- jeden niezamknięty obrys.

To pozwala sprawdzić normalne wykrywanie oraz komunikaty diagnostyczne skanera.

## Warstwy

- `WC_TEST_OBRYSY` - zamknięte obrysy pomieszczeń.
- `WC_TEST_METKI` - bloki metek `WC_ROOM_TAG`.
- `WC_TEST_SCIANY` - linie/obrysy ścian pomocniczych.
- `WC_TEST_OKNA` - okna testowe do przyszłych etapów WATT.
- `WC_TEST_OPISY` - opisy kondygnacji i uwagi.
- `_WENTCAD_KONDYGNACJE` - pomocnicze regiony kondygnacji.
