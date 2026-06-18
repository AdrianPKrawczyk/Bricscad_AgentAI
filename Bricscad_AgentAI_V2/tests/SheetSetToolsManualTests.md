# Testy manualne narzedzi Sheet Set Manager (SSM)

## Wymagania wstepne

1. Uruchom BricsCAD z pustym rysunkiem.
2. Zaladuj skrypt `Bricscad_AgentAI_V2/tests/generate_sheetset_test_model.lsp`, np. przez `APPLOAD`.
3. Uruchom w linii komend: `GEN_SHEETSET_TEST_MODEL`.
4. Sprawdz, czy powstaly pliki:
   - `C:\SheetSetTests\Architektura.dwg`
   - `C:\SheetSetTests\Konstrukcja.dwg`

## Test 1: Tworzenie i przeglad pustego zestawu

Prompt 1A:
```text
Uzyj profilu CadLayoutProfile. Utworz nowy zestaw arkuszy o nazwie "Projekt1" i zapisz go w pliku "C:\SheetSetTests\Projekt1.dst".
```

Oczekiwany tool call:
```json
{
  "Action": "Create",
  "DstFilePath": "C:\\SheetSetTests\\Projekt1.dst",
  "Name": "Projekt1"
}
```

Prompt 1B:
```text
Uzyj profilu CadLayoutProfile. Wypisz krotko aktualna zawartosc zestawu arkuszy "C:\SheetSetTests\Projekt1.dst".
```

Oczekiwany tool call:
```json
{
  "Action": "List",
  "DstFilePath": "C:\\SheetSetTests\\Projekt1.dst"
}
```

Oczekiwany rezultat: zestaw istnieje, a lista jest pusta albo zawiera tylko naglowek zestawu bez arkuszy.

## Test 2: Tworzenie podgrup

Prompt 2A:
```text
Uzyj profilu CadLayoutProfile. W zestawie arkuszy "C:\SheetSetTests\Projekt1.dst" dodaj podgrupe "Architektura".
```

Prompt 2B:
```text
Uzyj profilu CadLayoutProfile. W zestawie arkuszy "C:\SheetSetTests\Projekt1.dst" dodaj podgrupe "Konstrukcja".
```

Oczekiwany rezultat: dwa wywolania `ManageSheetSetTool` z `Action="CreateSubset"`.

## Test 3: Dodawanie arkuszy z DWG

Prompt 3A:
```text
Uzyj profilu CadLayoutProfile. Do zestawu "C:\SheetSetTests\Projekt1.dst" dodaj arkusz z pliku "C:\SheetSetTests\Architektura.dwg", layout "A1_Rzut_Parteru".
```

Prompt 3B:
```text
Uzyj profilu CadLayoutProfile. Do zestawu "C:\SheetSetTests\Projekt1.dst" dodaj arkusz z pliku "C:\SheetSetTests\Architektura.dwg", layout "A2_Rzut_Pietra".
```

Prompt 3C:
```text
Uzyj profilu CadLayoutProfile. Do zestawu "C:\SheetSetTests\Projekt1.dst" dodaj arkusz z pliku "C:\SheetSetTests\Konstrukcja.dwg", layout "K1_Zbrojenie".
```

Prompt 3D:
```text
Uzyj profilu CadLayoutProfile. Do zestawu "C:\SheetSetTests\Projekt1.dst" dodaj arkusz z pliku "C:\SheetSetTests\Konstrukcja.dwg", layout "K2_Fundamenty".
```

Oczekiwany tool call dla kazdego promptu:
```json
{
  "Action": "AddSheet",
  "DstFilePath": "C:\\SheetSetTests\\Projekt1.dst",
  "SourceDwgPath": "C:\\SheetSetTests\\Architektura.dwg",
  "SourceLayoutName": "A1_Rzut_Parteru"
}
```

## Test 4: Zmiana numeracji i nazw

Prompt 4A:
```text
Uzyj profilu CadLayoutProfile. W zestawie "C:\SheetSetTests\Projekt1.dst" arkusz o numerze "A1_Rzut_Parteru" przenumeruj na "A-01".
```

Prompt 4B:
```text
Uzyj profilu CadLayoutProfile. W zestawie "C:\SheetSetTests\Projekt1.dst" arkusz o numerze "A-01" zmien nazwe na "Rzut Parteru".
```

Oczekiwany rezultat: `SheetSetSheetTool` z akcjami `Renumber` oraz `Rename`.

## Test 5: Listowanie koncowe

Prompt:
```text
Uzyj profilu CadLayoutProfile. Podaj krotkie drzewo zawartosci pliku "C:\SheetSetTests\Projekt1.dst": podgrupy i arkusze.
```

Oczekiwany rezultat: `ManageSheetSetTool Action="List"` i zwiezly wynik bez dlugiego komentarza.

## Uwaga o limicie tokenow

Jesli subagent zaczyna generowac dluga odpowiedz zamiast wolac narzedzie, uzyj krotszej formy:

```text
Uzyj CadLayoutProfile i wywolaj ManageSheetSetTool: Action=List, DstFilePath="C:\SheetSetTests\Projekt1.dst".
```
