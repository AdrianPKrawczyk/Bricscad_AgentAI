# Instrukcja Testowania WentCad Na Modelu LISP

Ta instrukcja opisuje ręczny test `WentCad` w BricsCAD na modelu generowanym przez:

```text
WentCad\Tests\generate_wentcad_test_building.lsp
```

Celem testu jest sprawdzenie pierwszego przepływu v1:

- uruchomienie panelu `WENTCAD`,
- utworzenie kondygnacji,
- zdefiniowanie regionów kondygnacji,
- skanowanie obrysów i metek,
- zapis `.wentcad`,
- zapis NOD/XData,
- podstawowy bilans,
- eksport CSV.

## 1. Przygotowanie DLL

Z poziomu katalogu repo:

```powershell
dotnet build WentCad\WentCad.csproj
```

Oczekiwany wynik:

```text
WentCad\bin\Debug\WentCad.dll
```

Jeżeli DLL jest już załadowana w BricsCAD, zamknij BricsCAD przed ponowną kompilacją.

## 2. Przygotowanie DWG

1. Otwórz BricsCAD.
2. Utwórz nowy pusty rysunek.
3. Zapisz rysunek, np. jako:

```text
D:\tmp\WentCad_Test_Budynek.dwg
```

Zapis DWG przed testem jest ważny, bo `WentCad` tworzy plik `.wentcad` obok DWG.

## 3. Załadowanie Wtyczki WentCad

W BricsCAD uruchom:

```text
NETLOAD
```

Wskaż:

```text
D:\GitHub\Bricscad_AgentAI\WentCad\bin\Debug\WentCad.dll
```

Następnie uruchom:

```text
WENTCAD
```

Oczekiwany wynik:

- otwiera się panel `WentCad`,
- w panelu widoczna jest co najmniej jedna domyślna kondygnacja,
- obok DWG powstaje plik `.wentcad` po pierwszym zapisie/synchronizacji.

## 4. Wygenerowanie Budynku Testowego

Załaduj generator LISP przez:

```text
APPLOAD
D:\GitHub\Bricscad_AgentAI\WentCad\Tests\generate_wentcad_test_building.lsp
```

Uruchom:

```text
GEN_WENTCAD_TEST_BUILDING
```

Oczekiwany wynik w model space:

- rzut `Parter` w obszarze około `-3,-3` do `63,38`,
- rzut `Pietro_1` w obszarze około `-3,77` do `63,118`,
- diagnostyka po prawej stronie, około `72,0`,
- obrysy pomieszczeń na warstwie `WC_TEST_OBRYSY`,
- metki blokowe na warstwie `WC_TEST_METKI`,
- pomocnicze obrysy kondygnacji na warstwie `_WENTCAD_KONDYGNACJE`.

Uwaga: prostokąty na `_WENTCAD_KONDYGNACJE` z LISP-a są prowadnicami wizualnymi do momentu przypisania ich do kondygnacji. W panelu `WentCad` użyj `Wybierz region`, aby wskazać istniejący prostokąt; polilinia dostanie XData `WENTCAD_FLOOR_REGION`, a jej wierzchołki zostaną zapisane jako region kondygnacji.

## 5. Ustawienie Mapowania Pomieszczeń

W panelu `WENTCAD`, zakładka `Pomieszczenia`, ustaw:

| Pole | Wartość |
|---|---|
| Warstwa obrysów | `WC_TEST_OBRYSY` |
| Warstwa metek | `WC_TEST_METKI` |
| Atrybut numeru | `NR` |
| Atrybut nazwy | `NAZWA` |
| Atrybut wysokości | `H` |
| Atrybut powierzchni | `POW` |

Kliknij `Zapisz`.

## 6. Test Parteru

1. W zakładce `Kondygnacje` wybierz lub utwórz kondygnację `Parter`.
2. Ustaw orientacyjnie:
   - rzędna: `0.00`,
   - wysokość netto: `3.00`,
   - wysokość całkowita: `3.50`.
3. Kliknij `Wybierz region` i wskaż istniejący prostokąt prowadnicy parteru na warstwie `_WENTCAD_KONDYGNACJE`.
4. Alternatywnie kliknij `Rysuj region` i narysuj prostokąt obejmujący parter, najlepiej od około:

```text
-3,-3
63,38
```

5. Przejdź do zakładki `Pomieszczenia`.
6. Kliknij `Skanuj`.

Oczekiwany wynik:

- wykryte obrysy: `5`,
- wykryte metki: `5`,
- dopasowane pomieszczenia: `5`,
- w tabeli są pomieszczenia:
  - `0.01` Wiatrolap,
  - `0.02` Komunikacja,
  - `0.03` Biuro,
  - `0.04` Sala spotkan,
  - `0.05` Open space.

Po skanie kliknij `Zapisz` albo uruchom komendę:

```text
WENTCAD_SYNC
```

## 7. Test Piętra

1. W zakładce `Kondygnacje` dodaj kondygnację `Pietro_1`.
2. Ustaw orientacyjnie:
   - rzędna: `3.50`,
   - wysokość netto: `3.20`,
   - wysokość całkowita: `3.50`.
3. Kliknij `Wybierz region` i wskaż istniejący prostokąt prowadnicy piętra na warstwie `_WENTCAD_KONDYGNACJE`.
4. Alternatywnie kliknij `Rysuj region` i narysuj prostokąt obejmujący piętro, najlepiej od około:

```text
-3,77
63,118
```

5. W zakładce `Pomieszczenia` kliknij `Skanuj`.

Oczekiwany wynik:

- wykryte obrysy: `5`,
- wykryte metki: `5`,
- dopasowane pomieszczenia: `5`,
- w tabeli są pomieszczenia:
  - `1.01` Gabinet,
  - `1.02` Pokoj pracy,
  - `1.03` Archiwum,
  - `1.04` Sala szkolen,
  - `1.05` Socjal.

## 8. Test Diagnostyki Skanera

Ten test sprawdza przypadki błędne.

1. W zakładce `Kondygnacje` dodaj kondygnację `Diagnostyka`.
2. Kliknij `Rysuj region`.
3. Narysuj prostokąt obejmujący obszar diagnostyczny, np.:

```text
68,-3
112,43
```

4. W zakładce `Pomieszczenia` kliknij `Skanuj`.

Oczekiwany wynik:

- zamknięty obrys bez metki powinien być policzony jako obrys niedopasowany,
- metka `X.01` poza obrysem powinna być policzona jako metka niedopasowana,
- niezamknięty obrys nie powinien zostać potraktowany jako pomieszczenie,
- metka `X.02` znajdująca się przy niezamkniętym obrysie nie powinna utworzyć poprawnego pomieszczenia.

W zależności od aktualnej wersji komunikatów UI, wynik może pojawić się w pasku statusu panelu i/lub w oknie z uwagami skanowania.

## 9. Test Bilansu

1. Wybierz kondygnację z poprawnie zeskanowanymi pomieszczeniami.
2. Przejdź do zakładki `Bilans`.
3. Dla wybranych pomieszczeń ustaw różne tryby:

| Tryb | Co sprawdzić |
|---|---|
| `AUTO_MAX` | przepływ powinien być większą wartością z higienicznego i ACH |
| `MANUAL` | przepływ powinien użyć pól `ManualSupply` i `ManualExhaust` |
| `HYGIENIC_ONLY` | przepływ powinien zależeć od liczby osób i dawki |
| `ACH_ONLY` | przepływ powinien zależeć od kubatury i ACH |

4. Kliknij `Przelicz`.

Oczekiwany wynik:

- uzupełniona kubatura,
- uzupełniony nawiew i wywiew,
- uzupełnione `Real ACH`,
- uzupełniony bilans netto.

## 10. Test Systemów I Kolorowania

1. Przejdź do zakładki `Systemy`.
2. Sprawdź domyślne systemy `N1` i `W1`.
3. Dla kilku pomieszczeń zmień `SupplySystemId` lub `ExhaustSystemId` w tabeli pomieszczeń, jeśli dana kolumna jest edytowalna w aktualnej wersji UI.
4. Kliknij `Przelicz i koloruj`.

Oczekiwany wynik:

- sumy systemów aktualizują się,
- obrysy pomieszczeń zmieniają kolor zgodnie z systemem.

## 11. Test Zapisu `.wentcad`, NOD I XData

Po skanowaniu uruchom:

```text
WENTCAD_SYNC
```

Sprawdź:

1. Obok DWG istnieje plik:

```text
WentCad_Test_Budynek.wentcad
```

2. Po zapisaniu i ponownym otwarciu DWG panel `WENTCAD` odtwarza dane projektu.
3. Obrysy pomieszczeń mają XData `WENTCAD_ROOM`.

XData można sprawdzić Agentem narzędziem `ReadXData` albo innym narzędziem diagnostycznym BricsCAD.

## 12. Test Eksportu CSV

1. Przejdź do zakładki `Eksport`.
2. Kliknij `Eksportuj CSV`.

Oczekiwany wynik:

- obok pliku `.wentcad` powstaje plik `.rooms.csv`,
- CSV zawiera pomieszczenia z numerami, nazwami, powierzchnią, kubaturą i wynikami bilansu.

## 13. Test Punktu Bazowego I Eksportu IFC

1. Przejdź do zakładki `Kondygnacje`.
2. Dla `Parter` kliknij `Wskaz baze`.
3. Wskaż punkt odpowiadający wspólnemu punktowi budynku, np. lewy dolny narożnik prowadnicy parteru:

```text
-3,-3
```

4. Dla `Pietro_1` kliknij `Wskaz baze`.
5. Wskaż odpowiadający punkt na rzucie piętra:

```text
-3,77
```

6. Kliknij `Zapisz`.
7. Przejdź do zakładki `Eksport`.
8. Kliknij `Eksportuj IFC`.

Oczekiwany wynik:

- obok pliku `.wentcad` powstaje plik `.ifc`,
- plik zawiera `IfcBuildingStorey` dla kondygnacji,
- pomieszczenia są zapisane jako `IfcSpace`,
- parter i piętro po imporcie do przeglądarki IFC są nad sobą, a nie przesunięte o `80` jednostek w osi Y.

## 14. Test Integracji Z Agentem

Jeżeli `Bricscad_AgentAI_V2` jest załadowany:

1. Uruchom tool `ReadWentCadProject`.
2. Sprawdź, czy JSON zawiera:
   - `Project`,
   - `Floors`,
   - `Rooms`.
3. Uruchom tool `ReadWentCadRooms`.
4. Opcjonalnie uruchom `RunWentCadCommand` z:

```json
{ "Command": "WENTCAD_SYNC" }
```

Oczekiwany wynik:

- Agent odczytuje dane z NOD bez referencji do `WentCad.dll`,
- brak uruchomionego Agenta nie wpływa na działanie panelu `WENTCAD`.

## 15. Kryteria Zaliczania Testu

Test można uznać za zaliczony, jeżeli:

- `WENTCAD` otwiera panel,
- generator LISP tworzy model bez błędów,
- parter i piętro skanują po `5` poprawnych pomieszczeń,
- przypadki diagnostyczne nie tworzą fałszywych pomieszczeń,
- `WENTCAD_SYNC` zapisuje `.wentcad`, NOD i XData,
- bilans przelicza przepływy,
- eksport CSV tworzy plik wynikowy,
- eksport IFC tworzy plik wynikowy z `IfcSpace`,
- tool-e Agenta odczytują JSON z NOD, jeśli Agent jest załadowany.

## 16. Znane Ograniczenia v1

- Regiony kondygnacji z generatora LISP są wizualnymi prowadnicami, nie pełnym importem zakresów WentCad.
- Własny region v1 najlepiej tworzyć przez `Rysuj region` w panelu `WENTCAD`.
- Skaner v1 obsługuje zamknięte polilinie i bloki z atrybutami, ale nie analizuje jeszcze natywnego XData CadProfi.
- IFC v1 eksportuje pomieszczenia jako `IfcSpace`, ale nie eksportuje jeszcze ścian, okien, drzwi ani pełnych przegród WATT.
- WATT, detekcja ścian i detekcja okien są kolejnymi etapami.
