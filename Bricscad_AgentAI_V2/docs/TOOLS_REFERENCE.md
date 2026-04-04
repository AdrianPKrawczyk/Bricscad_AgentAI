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
| `Modifications` | `array` | **Tak** | Lista obiektów zawierających klucze `Prop` (nazwa właściwości) oraz `Val` (nowa wartość lub wyrażenie RPN). |

### Uwagi i Zabezpieczenia (API Shield)
Narzędzie posiada wbudowaną **tarczę anty-halucynacyjną (PropertyValidator)**. Każda próba modyfikacji właściwości (np. `Radius`) jest weryfikowana pod kątem dopasowania do klasy obiektu (np. `Line` vs `Circle`). Jeśli właściwość nie istnieje w oficjalnym API BricsCAD, modyfikacja zostanie pominięta, a Agent otrzyma stosowne ostrzeżenie w logach.

### Zwracany wynik
Summary z liczbą zmodyfikowanych obiektów oraz listą ewentualnych ostrzeżeń z walidatora (jeśli model próbował użyć nieprawidłowych nazw właściwości).

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

---

## 11. TextEditTool
**Nazwa systemowa:** `TextEditTool`  
**Opis:** Uniwersalne narzędzie do edycji treści oraz formatowania wizualnego (RTF) obiektów `DBText` i `MText`. Pozwala na masową podmianę tekstów, dopełnianie treści oraz wizualne podświetlanie fraz.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `Mode` | `string` | **Tak** | Tryb: `Append`, `Prepend`, `Replace`, `FormatHighlight`, `ClearFormatting`. |
| `FindText` | `string` | Nie | Tekst do znalezienia (wymagany dla `Replace` i `FormatHighlight`). |
| `ReplaceWith` | `string` | Nie | Nowa treść (dla `Append`, `Prepend`, `Replace`). |
| `ColorIndex` | `int` | Nie | Indeks koloru ACI dla `FormatHighlight` (domyślnie 1 - czerwony). |
| `IsBold` | `bool` | Nie | Czy pogrubić tekst w trybie `FormatHighlight`. |

### Zabezpieczenia i Obsługa Typów
- **MText**: Pełne wsparcie dla wszystkich trybów, w tym formatowania RTF.
- **DBText**: Wspiera tylko edycję treści (`Append`, `Prepend`, `Replace`). Próba użycia trybów RTF (`FormatHighlight`, `ClearFormatting`) zakończy się ostrzeżeniem w logach.
- **Newline Preservation**: W trybie `ClearFormatting` narzędzie inteligentnie zachowuje znaki nowej linii (`\P`), usuwając jedynie kolory, czcionki i style.

### Zwracany wynik
Raport o liczbie zmodyfikowanych obiektów oraz spis ostrzeżeń (np. o obiektach `DBText` pominiętych w operacjach RTF).

---

## 12. ManageAnnoScalesTool
**Nazwa systemowa:** `ManageAnnoScales`  
**Opis:** Zarządza skalami opisowymi (Annotative Scales) dla kompatybilnych obiektów. Pozwala na dodawanie nowej skali, usuwanie istniejącej, odczytywanie listy przypisanych skal oraz całkowite wyłączanie opisowości.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `Action` | `string` | **Tak** | Akcja: `Add`, `Remove`, `Read`. |
| `ScaleName` | `string` | Nie* | Nazwa skali (np. "1:50"). Wymagana dla `Add` i `Remove`. |
| `DisableAnnotative` | `bool` | Nie | Jeśli `true` (kompatybilne z `Action: Remove`), całkowicie wyłącza opisowość obiektu. |
| `SaveAs` | `string` | Nie | Tylko dla `Read`: Nazwa zmiennej do zapisu listy skal. |

### Zabezpieczenia i Obsługa Typów
- **Kompatybilność**: Narzędzie działa na: `Dimension`, `MText`, `DBText`, `BlockReference`, `Leader`, `MLeader`, `Hatch`.
- **Block Definitions**: Przy dodawaniu skali do `BlockReference`, narzędzie automatycznie włącza opisowość w definicji bloku (`BlockTableRecord`), co zapobiega błędom API.
- **Error Handling**: Jeśli podana skala nie istnieje w rysunku, narzędzie zwraca błąd zamiast przerywać działanie dla pozostałych obiektów.

### Zwracany wynik
Podsumowanie wykonanych akcji, np.: "WYNIK (Add): Dodano skalę '1:100' do 5 obiektów." lub lista skal w trybie `Read`.

---

## 13. EditBlockTool
**Nazwa systemowa:** `EditBlock`  
**Opis:** Modyfikuje geometrię i właściwości obiektów wewnątrz definicji bloku (`BlockTableRecord`). Zmiany są trwałe i wpływają na wszystkie wstawienia tego bloku w rysunku.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `Target` | `string` | **Tak** | `Selection` (bloki z zaznaczenia) lub `ByName` (blok po nazwie). |
| `BlockName` | `string` | Nie* | Nazwa bloku (wymagana dla `ByName`). |
| `Recursive` | `bool` | Nie | Czy edytować bloki zagnieżdżone (domyślnie `true`). |
| `RemoveDimensions` | `bool` | Nie | Jeśli `true`, usuwa wymiary z wnętrza bloku. |
| `FindText` | `string` | Nie | Tekst do wyszukania wewnątrz bloku. |
| `ReplaceText` | `string` | Nie | Tekst na zamianę (wymaga `FindText`). |
| `Modifications` | `array` | Nie | Lista zmian właściwości: `[{"Prop": "Layer", "Val": "RED"}]`. |
| `Filters` | `object` | Nie | Filtry obiektów wewnątrz: `{"Type": "Line", "Color": 1}`. |

### Zabezpieczenia i Eksploatacja
- **API Shield**: Każda próba zmiany właściwości (np. `Color`, `Layer`) jest weryfikowana przez `PropertyValidator`.
- **Ochrona bazy**: Narzędzie odmawia edycji XREF-ów oraz arkuszy (Layouts), chroniąc strukturę pliku DWG.
- **Odświeżanie**: Automatycznie wywołuje `RecordGraphicsModified` na wszystkich wystąpieniach bloku oraz aktualizuje bloki anonimowe (dynamiczne).

### Zwracany wynik
Raport o liczbie zmodyfikowanych elementów wewnątrz definicji, np.: "WYNIK: Zmodyfikowano 12 obiektów wewnątrz definicji bloku 'DETAL_A'."

---

## 14. EditAttributesTool
**Nazwa systemowa:** `EditAttributes`  
**Opis:** Zarządza atrybutami (dynamicznymi tekstami) w konkretnych wystąpieniach bloków (`BlockReference`). Pozwala na odczyt wartości oraz ich synchronizację lub aktualizację.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `Action` | `string` | **Tak** | `Read` (odczyt) lub `Update` (aktualizacja). |
| `Attributes` | `array` | Nie* | Lista obiektów `{"Tag": "...", "Value": "..."}`. Wymagana dla `Update`. |
| `SaveAs` | `string` | Nie | Tylko dla `Read`: Nazwa zmiennej do zapisu wyniku. |

### Funkcjonalność i Integracja
- **IsMTextAttribute**: Narzędzie automatycznie wykrywa atrybuty wielowierszowe i zarządza ich treścią poprzez właściwość `MTextAttribute`, zapewniając czysty tekst bez zbędnych kodów RTF przy zachowaniu formatowania blokowego.
- **Variable Injection**: Tryb `Update` wspiera mechanizm `$ZMIENNA` oraz wyrażenia `RPN:`, co pozwala na automatyczne numerowanie lub przeliczanie wartości atrybutów w zaznaczeniu.
- **Raportowanie**: W przypadku braku wskazanego Tagu w bloku, narzędzie zwraca ostrzeżenie, kontynuując pracę dla pozostałych obiektów.

### Zwracany wynik
Podsumowanie akcji, np.: "WYNIK (Update): Zaktualizowano 5 atrybutów w 2 blokach." lub zagregowany ciąg tekstowy w trybie `Read`.

---

## 15. ListBlocksTool
**Nazwa systemowa:** `ListBlocks`  
**Opis:** Zwraca listę unikalnych nazw definicji bloków dostępnych w bieżącym rysunku.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `SaveAs` | `string` | Nie | Nazwa zmiennej do zapisu listy bloków. |

### Zwracany wynik
Posortowana alfabetycznie lista nazw bloków, np.: "WYNIK: Znaleziono 3 bloki: Drzwi_90, Okno_120, Stol_Biurko."

---

## 16. InsertBlockTool
**Nazwa systemowa:** `InsertBlock`  
**Opis:** Wstawia nową instancję bloku (`BlockReference`) do aktualnej przestrzeni (Model/Layout).

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `BlockName` | `string` | **Tak** | Nazwa bloku do wstawienia. |
| `InsertionPoint` | `point/string` | **Tak** | Współrzędne `[x,y,z]` lub `"AskUser"`. |
| `Scale` | `number` | Nie | Skala (domyślnie 1.0). |
| `Rotation` | `number` | Nie | Obrót w **stopniach** (domyślnie 0.0). |
| `Attributes` | `array` | Nie | Lista wartości atrybutów: `[{"Tag": "T1", "Value": "V1"}]`. |

### Zwracany wynik
Potwierdzenie wstawienia wraz ze współrzędnymi punktu.

---

## 17. CreateBlockTool
**Nazwa systemowa:** `CreateBlock`  
**Opis:** Tworzy nową definicję bloku (`BlockTableRecord`) z obiektów znajdujących się w aktualnym zaznaczeniu Agenta.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `BlockName` | `string` | **Tak** | Nazwa dla nowej definicji bloku. |
| `BasePoint` | `point/string` | **Tak** | Punkt bazowy `[x,y,z]` lub `"AskUser"`. |
| `DeleteOriginals` | `bool` | Nie | Czy usunąć obiekty źródłowe (domyślnie `false`). |

### Zabezpieczenia
- **Overwrite Protection**: Narzędzie zwraca błąd, jeśli blok o podanej nazwie już istnieje.
- **DeepClone**: Obiekty są kopiowane bezpiecznie wraz ze wszystkimi właściwościami.

### Zwracany wynik
Podsumowanie utworzenia bloku wraz z liczbą skopiowanych obiektów.

---

## 18. UserInputTool
**Nazwa systemowa:** `UserInput`  
**Opis:** Zatrzymuje pętlę Agenta i prosi użytkownika o podanie wartości (tekst, liczba, punkt) w linii komend BricsCAD.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `PromptMessage` | `string` | **Tak** | Komunikat dla użytkownika. |
| `InputType` | `string` | **Tak** | `String`, `Integer`, `Double`, `Point`. |
| `SaveAs` | `string` | Nie | Nazwa zmiennej do zapisu wyniku. |

### Zwracany wynik
Wpisana wartość lub komunikat `[ANULOWANO]`, jeśli użytkownik przerwał operację (ESC).

---

## 19. UserChoiceTool
**Nazwa systemowa:** `UserChoice`  
**Opis:** Wyświetla listę opcji (Keywords) do wyboru przez użytkownika.

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `PromptMessage` | `string` | **Tak** | Komunikat dla użytkownika. |
| `Options` | `array` | **Tak** | Lista dostępnych opcji (strings). |
| `SaveAs` | `string` | Nie | Nazwa zmiennej do zapisu wyboru. |

### Uwagi Techniczne
- **Auto-Cleaning**: Spacje w nazwach opcji są automatycznie zamieniane na podkreślenia (`_`), aby spełnić wymogi API BricsCAD.
- **Focus**: Narzędzie automatycznie przywraca fokus do okna rysunku przed wyświetleniem promptu.

---

## 20. ForeachTool
**Nazwa systemowa:** `Foreach`  
**Opis:** Pomocnicze narzędzie do strukturyzacji i analizy list elementów zapisanych w pamięci Agenta jako połączone ciągi znaków (np. po operacjach `ListBlocks`, `ListLayers` czy `ReadTextSample`).

### Parametry Wejściowe
| Parametr | Typ | Wymagany | Opis |
|----------|-----|----------|------|
| `TargetVariable` | `string` | **Tak** | Nazwa zmiennej w pamięci (bez @). |
| `Separator` | `string` | Nie | Znak rozdzielający (domyślnie `" | "`). |
| `Action` | `string` | Nie | `List` (domyślnie) lub `Count`. |

### Zwracany wynik
Przejrzysta, numerowana lista elementów lub liczba pozycji, co ułatwia modelowi LLM planowanie dalszych kroków iteracji.
