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
