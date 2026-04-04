# Encyklopedia Narzędzi AI (V2 GOLD)

Ten dokument zawiera techniczną specyfikację narzędzi (Tools), których asystent Bielik AI używa do interakcji z BricsCAD. Model LLM wybiera te narzędzia automatycznie w pętli ReAct.

---

## 1. SelectEntities
**Nazwa systemowa:** `SelectEntities`  
**Opis:** Wyszukuje i filtruje obiekty w rysunku na podstawie zadanych kryteriów (typ, warstwa, kolor). Automatycznie aktualizuje `ActiveSelection` w pamięci Agenta.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `Type` | `string` | Nie | Typ obiektu (np. "Line", "Circle", "Polyline"). |
| `Layer` | `string` | Nie | Nazwa warstwy lub maska (np. "Wiany-*"). |
| `ColorToIndex` | `integer` | Nie | Wskaźnik koloru AutoCAD (ACI). |

### Zwracany wynik
Tablica uchwytów (Handles) znalezionych obiektów lub komunikat o braku dopasowań.

---

## 2. CreateObject
**Nazwa systemowa:** `CreateObject`  
**Opis:** Tworzy nowe obiekty geometryczne w BricsCAD. Obsługuje notację RPN dla parametrów liczbowych.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `ObjectType` | `string` | **Tak** | Typ obiektu: "Line", "Circle", "Text", "MLeader". |
| `Points` | `array` | **Tak** | Lista punktów `[x,y,z]` lub instrukcja "AskUser". |
| `Properties` | `object` | Nie | Słownik właściwości (np. `Radius`, `TextString`, `Height`). Akceptuje RPN. |

### Zwracany wynik
Komunikat o sukcesie z podaniem Handle nowego obiektu. Obiekt jest automatycznie zaznaczany.

---

## 3. ModifyProperties
**Nazwa systemowa:** `ModifyProperties`  
**Opis:** Zmienia właściwości obiektów znajdujących się w `ActiveSelection`.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `Properties` | `object` | **Tak** | Słownik właściwości do zmiany (np. `Color`, `Layer`, `Radius`). Wspiera RPN oraz zmienne `@OLD_...`. |

### Zwracany wynik
Zestawienie zmodyfikowanych obiektów lub lista błędów (np. brak właściwości w danym typie obiektu).

---

## 4. ManageLayers
**Nazwa systemowa:** `ManageLayers`  
**Opis:** Zarządzanie warstwami w rysunku (tworzenie, edycja, usuwanie).

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `Action` | `string` | **Tak** | Akcja: "Create", "Modify", "Delete". |
| `LayerName` | `string` | **Tak** | Nazwa warstwy (obsługuje maski `*` dla Modify/Delete). |
| `Properties` | `object` | Nie | Nowe właściwości (Color, IsLocked, IsFrozen itp.). |

### Zwracany wynik
Status operacji. Narzędzie blokuje usuwanie warstw systemowych ("0", "Defpoints") i aktualnej.

---

## 5. ExecuteMacro
**Nazwa systemowa:** `ExecuteMacro`  
**Opis:** Uruchamia predefiniowane makra lub surowe skrypty LISP/CAD.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `MacroName` | `string` | Nie | Nazwa makra z bazy (np. "CleanDrawings"). |
| `CustomCommand` | `string` | Nie | Surowy kod LISP lub komenda BricsCAD. |

### Zwracany wynik
Status wykonania skryptu lub raport o błędzie składniowym z interpretera.

---

## 6. InspectEntity
**Nazwa systemowa:** `InspectEntity`  
**Opis:** Pobiera komplet danych o konkretnym obiekcie dla Agenta.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `EntityHandle` | `string` | Nie | Handle obiektu. Jeśli brak, sprawdza pierwszy element z `ActiveSelection`. |

### Zwracany wynik
Struktura JSON zawierająca typ obiektu, warstwę, geometrię (punkty, promienie) oraz podstawowe atrybuty wizualne.

---

## 7. GetProperties
**Nazwa systemowa:** `GetPropertiesTool`  
**Opis:** Czyta i podsumowuje parametry oraz właściwości obiektów bezpośrednio z `ActiveSelection`. Zwraca skrócone dane lub szczegółowe zestawienie geometryczne i fizyczne.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `Mode` | `string` | **Tak** | Konfiguruje tryb ekstrakcji. `"Lite"` - lekki tryb o mniejszym zużyciu kontekstu (do 15 obiektów, tylko podstawowe dane); `"Full"` - pełny tryb z geometrią i detalami fizycznymi obiektu (ograniczony zazwyczaj do 5 obiektów dla oszczędności tokenów LLM). |

### Zwracany wynik
Sformatowany łańcuch tekstowy zawierający listę właściwości dla każdego z obiektów z `ActiveSelection`. Zwraca błąd, jeśli brak jest zaznaczonych elementów, lub powiadomienie o osiągnięciu limitu liczby analizowanych obiektów w danym trybie. LLM powinien używać tego narzędzia bez przekazywania długich zestawów ID, delegując zarządzanie zaznaczeniem do odpowiednich narzędzi Select.

---

## 8. ReadProperty
**Nazwa systemowa:** `ReadPropertyTool`  
**Opis:** Odczytuje jedną wybraną właściwość (np. `Length`, `MidPoint`, `Color`) ze wszystkich obiektów w `ActiveSelection`. Pozwala zapisać wynik do nazwanej zmiennej w pamięci Agenta (np. `@MojaWartosc`).

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `Property` | `string` | **Tak** | Nazwa właściwości. Wspiera właściwości natywne (np. `Layer`), zagnieżdżone (np. `Position.X`) oraz wirtualne (`MidPoint`, `Length`, `Area`, `Volume`, `Centroid`, `Angle`, `Value`). |
| `SaveAs` | `string` | Nie | Nazwa zmiennej (bez @), pod którą wynik zostanie zapisany w pamięci Agenta. |

### Zwracany wynik
Lista odczytanych wartości dla każdego obiektu. Jeśli użyto `SaveAs`, zwraca również potwierdzenie zapisu w formacie `ZAPISANO W PAMIĘCI JAKO: @Nazwa`. Wiele wartości jest łączonych separatorem ` | `.

---

## 9. AnalyzeSelection
**Nazwa systemowa:** `AnalyzeSelectionTool`  
**Opis:** Agreguje i statystycznie analizuje obiekty w `ActiveSelection`. Pozwala na zliczanie typów obiektów lub wyciąganie unikalnych wartości właściwości.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `Mode` | `string` | **Tak** | Tryb pracy: `"CountTypes"` (zlicza ile jest linii, okręgów itp.) lub `"ListUniqueValues"` (wyciąga niepowtarzalne wartości danej właściwości). |
| `TargetProperty` | `string` | Nie | Nazwa właściwości do analizy (wymagane tylko dla trybu `"ListUniqueValues"`, np. `"Layer"`, `"Color"`, `"Linetype"`). |
| `SaveAs` | `string` | Nie | Nazwa zmiennej (bez @), pod którą wynik zostanie zapisany w pamięci Agenta. |

### Zwracany wynik
Raport tekstowy z wynikami analizy. W trybie `ListUniqueValues` unikalne elementy są sortowane alfabetycznie i łączone przecinkami (w pamięci Agenta separatorem `" | "`).

---

## 10. ReadTextSample
**Nazwa systemowa:** `ReadTextSampleTool`  
**Opis:** Pobiera reprezentatywną próbkę treści tekstowych z obiektów `DBText`, `MText` oraz `MLeader` w zaznaczeniu. Idealne do szybkiego zapoznania się z zawartością opisową bez ryzyka przepełnienia kontekstu LLM.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `SaveAs` | `string` | Nie | Nazwa zmiennej (bez @), pod którą próbki zostaną zapisane w pamięci Agenta (połączone separatorem `" | "`). |

### Zwracany wynik
Lista wybranych próbek tekstowych (maksymalnie 15). Algorytm dobiera teksty równomiernie z całego zbioru (początek, środek, koniec), co daje statystycznie poprawny wgląd w dane. Treści są oczyszczone z kodów formatowania RTF.
