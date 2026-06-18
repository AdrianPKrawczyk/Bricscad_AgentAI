# Prompty testowe - ManageViewportsTool advanced

Zestaw zaklada rysunek z layoutami `Arkusz1` i `Arkusz2` w formacie A3 oraz zaladowane srodowisko:

```lisp
(load "D:/GitHub/Bricscad_AgentAI/Bricscad_AgentAI_V2/tests/generate_viewport_test_model.lsp")
GEN_VIEWPORT_TEST_MODEL
```

LISP tworzy zakresy modelu, warstwy `A-MEBLE`, `A-OPIS`, `A-INSTALACJE` oraz widoki nazwane `VPT_ARKUSZ1_FULL`, `VPT_ARKUSZ2_FULL`, `VPT_ARKUSZ1_DETAIL`, `VPT_ARKUSZ2_DETAIL`.

## 1. Rozpoznanie

```text
Uzyj profilu CadLayoutProfile. Wylistuj layouty i sprawdz ustawienia layoutow "Arkusz1" oraz "Arkusz2".
```

Oczekiwane: agent uzywa `ListLayoutsTool`; potwierdza layouty papierowe i parametry A3.

```text
Uzyj profilu CadLayoutProfile. Wylistuj rzutnie papierowe na layoucie "Arkusz1".
```

Oczekiwane: lista rzutni albo informacja o braku rzutni poza systemowa.

## 2. Utworzenie rzutni z Window XY

```text
Uzyj profilu CadLayoutProfile. Na layoucie "Arkusz1" utworz rzutnie 360x250 mm ze srodkiem w punkcie 210,148.5. Pokaz zakres modelu od 0,0 do 18000,12500, ustaw skale 1:50, skale opisowa 1:50 i zablokuj rzutnie.
```

Oczekiwane: rzutnia pokazuje cala strefe `ARKUSZ1_WINDOW_0_0_18000_12500`; ramka jest na `_rzutnie`.

```text
Uzyj profilu CadLayoutProfile. Na layoucie "Arkusz2" utworz rzutnie 360x250 mm ze srodkiem w punkcie 210,148.5. Pokaz zakres modelu od 20000,0 do 38000,12500, ustaw skale 1:50, skale opisowa 1:50 i zablokuj rzutnie.
```

Oczekiwane: rzutnia pokazuje cala strefe `ARKUSZ2_WINDOW_20000_0_38000_12500`.

## 3. Widoki nazwane

```text
Uzyj profilu CadLayoutProfile. Na layoucie "Arkusz1" ustaw rzutnie nr 1 wedlug widoku nazwanego "VPT_ARKUSZ1_DETAIL". Jesli rzutnia jest zablokowana, odblokuj tymczasowo i zablokuj z powrotem.
```

Oczekiwane: rzutnia pokazuje detal z pierwszej strefy, mniej wiecej zakres 3000,2000 do 9000,6500.

```text
Uzyj profilu CadLayoutProfile. Na layoucie "Arkusz1" ustaw rzutnie nr 1 wedlug widoku nazwanego "VPT_ARKUSZ1_FULL", ale zachowaj skale 1:100. Jesli rzutnia jest zablokowana, odblokuj tymczasowo i zablokuj z powrotem.
```

Oczekiwane: agent przekazuje `NamedView="VPT_ARKUSZ1_FULL"` oraz `Scale="1:100"`; jawna skala doprecyzowuje widok nazwany.

## 4. Warstwy per rzutnia

```text
Uzyj profilu CadLayoutProfile. Na layoucie "Arkusz1" w rzutni nr 1 zamroz warstwy "A-MEBLE" i "A-OPIS" tylko w tej rzutni. Jesli rzutnia jest zablokowana, odblokuj tymczasowo i zablokuj z powrotem.
```

Oczekiwane: meble i opisy znikaja tylko w rzutni na `Arkusz1`; linie osi, siatka i instalacje zostaja widoczne.

```text
Uzyj profilu CadLayoutProfile. Wylistuj rzutnie papierowe na layoucie "Arkusz1" i pokaz, jakie warstwy sa zamrozone lokalnie.
```

Oczekiwane: wynik `List` zawiera `FrozenLayers=A-MEBLE, A-OPIS` albo te nazwy w innej kolejnosci.

```text
Uzyj profilu CadLayoutProfile. Na layoucie "Arkusz1" w rzutni nr 1 odmroz warstwe "A-OPIS" tylko w tej rzutni. Jesli rzutnia jest zablokowana, odblokuj tymczasowo i zablokuj z powrotem.
```

Oczekiwane: opisy wracaja, meble nadal sa ukryte.

```text
Uzyj profilu CadLayoutProfile. Na layoucie "Arkusz1" w rzutni nr 1 odmroz wszystkie warstwy zamrozone lokalnie. Jesli rzutnia jest zablokowana, odblokuj tymczasowo i zablokuj z powrotem.
```

Oczekiwane: `FrozenLayers=(brak)`, wszystkie elementy testowe widoczne.

## 5. Nieprostokatny clipping

```text
Uzyj profilu CadLayoutProfile. Na layoucie "Arkusz1" ustaw rzutni nr 1 nieprostokatny clipping z punktow papieru: 35,35; 385,35; 370,250; 230,265; 35,245. Jesli rzutnia jest zablokowana, odblokuj tymczasowo i zablokuj z powrotem.
```

Oczekiwane: rzutnia zostaje przycieta wielokatnie; `List` powinien pokazac `NonRectClipOn=True` i `ClipHandle`.

```text
Uzyj profilu CadLayoutProfile. Wylistuj rzutnie papierowe na layoucie "Arkusz1" i podaj handle granicy clippingu rzutni nr 1.
```

Oczekiwane: wynik zawiera `ClipHandle=<hex>`.

```text
Uzyj profilu CadLayoutProfile. Na layoucie "Arkusz1" usun nieprostokatny clipping z rzutni nr 1. Jesli rzutnia jest zablokowana, odblokuj tymczasowo i zablokuj z powrotem.
```

Oczekiwane: `NonRectClipOn=False`; rzutnia wraca do prostokatnego obrysu.

## 6. Test z istniejaca granica clippingu

Najpierw utworz polilinie w papierze recznie albo przez narzedzie CAD, a potem odczytaj jej handle. Gdy masz handle, uzyj promptu:

```text
Uzyj profilu CadLayoutProfile. Na layoucie "Arkusz1" ustaw rzutni nr 1 nieprostokatny clipping z istniejacej granicy o handle "WPISZ_HANDLE". Jesli rzutnia jest zablokowana, odblokuj tymczasowo i zablokuj z powrotem.
```

Oczekiwane: agent uzywa `ClipBoundaryHandle`; granica musi lezec w tej samej przestrzeni papieru co rzutnia.

## 7. Walidacja bledow

```text
Uzyj profilu CadLayoutProfile. Na layoucie "Arkusz1" ustaw rzutnie nr 1 wedlug widoku nazwanego "NIE_MA_TAKIEGO_WIDOKU".
```

Oczekiwane: czytelny blad, ze widok nazwany nie istnieje.

```text
Uzyj profilu CadLayoutProfile. Na layoucie "Arkusz1" w rzutni nr 1 zamroz warstwe "NIE_MA_TAKIEJ_WARSTWY".
```

Oczekiwane: czytelny blad, ze warstwa nie istnieje.

```text
Uzyj profilu CadLayoutProfile. Na layoucie "Arkusz1" ustaw rzutni nr 1 nieprostokatny clipping z punktow papieru: 35,35; 385,35.
```

Oczekiwane: blad walidacji, bo clipping z punktow wymaga minimum 3 punktow.

## 8. Publikacja kontrolna

```text
Uzyj profilu CadLayoutProfile. Opublikuj layouty "Arkusz1" i "Arkusz2" do pojedynczych plikow PDF w katalogu Z:/export/. Tryb SingleFiles. Dodaj na poczatku nazwy pdf "VPT-TEST-".
```

Oczekiwane: agent uzywa `PublishToPdfTool` z `OutputPdfPath="Z:/export/"` i `FileNamePrefix="VPT-TEST-"`. Powstaja pliki `Z:/export/VPT-TEST-Arkusz1.pdf` i `Z:/export/VPT-TEST-Arkusz2.pdf`, bez tworzenia folderu `VPT-TEST-`.
