# BricsCAD Agent AI V2: Tools Technical Reference

Ten dokument zawiera pełną specyfikację techniczną (Tool Calling) dla 20 narzędzi systemowych dostępnych w wersji **GOLD**. Każde narzędzie posiada unikalny schemat JSON, który silnik Agenta przesyła do modelu LLM.

---

## 🏗️ Zarządzanie Obiektami i Blokami

### 1. `CreateObject`
Tworzy nową geometrię i teksty w rysunku.
- **Parametry**: `EntityType` (Line, Circle, DBText, MText, MLeader), `Layer`, `StartPoint`, `EndPoint`, `Center`, `Radius`, `Position`, `Text`, `Height`, `Rotation`, `ArrowPoint`, `LandingPoint`.
- **Funkcje specjalne**: Obsługuje słowo kluczowe `AskUser` dla interakcji z użytkownikiem oraz formuły `RPN:` dla obliczeń matematycznych.

### 2. `InsertBlock`
Wstawia instancję bloku (`BlockReference`).
- **Parametry**: `BlockName`, `InsertionPoint`, `Scale`, `Rotation`, `Attributes` (lista obiektów `Tag/Value`).
- **Automatyzacja**: Wykonuje synchronizację atrybutów po wstawieniu.

### 3. `CreateBlock`
Tworzy nową definicję bloku (`BlockTableRecord`) z aktualnie zaznaczonych elementów.
- **Parametry**: `BlockName`, `BasePoint`, `DeleteOriginals`.
- **Mechanizm**: Wykonuje `DeepCloneObjects` dla zachowania integralności danych.

### 4. `EditBlock`
Modyfikuje obiekty **wewnątrz** definicji bloku (wpływa na wszystkie wystąpienia).
- **Parametry**: `Target` (Selection/ByName), `BlockName`, `Recursive`, `RemoveDimensions`, `FindText`, `ReplaceText`, `Modifications`, `Filters`.
- **Zastosowanie**: Masowa zmiana warstw, kolorów lub tekstów ukrytych głęboko w strukturze bloków.

### 5. `EditAttributes`
Edytuje wartości atrybutów w konkretnych instancjach bloków.
- **Parametry**: `Action` (Read/Update), `Attributes` (Tag/Value), `SaveAs`.
- **Kluczowa funkcja**: Pozwala pobrać dane z atrybutów do pamięci podręcznej Agenta.

### 6. `ListBlocks`
Zwraca listę nazw wszystkich dostępnych bloków w rysunku.
- **Parametry**: `SaveAs`.
- **Filtry**: Ignoruje bloki anonimowe, XREF-y i layouty.

---

## 🔍 Analiza i Selekcja

### 7. `SelectEntities`
Inteligentna wyszukiwarka obiektów. Fundament pracy Agenta.
- **Parametry**: `Mode` (New, Add, Remove, Clear), `Scope` (Model, Blocks), `EntityType`, `Conditions` (lista słowników `Prop/Op/Val`).
- **Operatory**: `==`, `!=`, `>`, `<`, `>=`, `<=`, `contains`, `in`.

### 8. `InspectEntity`
Zwraca surowy zrzut danych JSON o konkretnym obiekcie (Handle lub ActiveSelection).
- **Parametry**: `EntityHandle`.

### 9. `GetPropertiesTool`
Skanuje obiekty w zaznaczeniu i wyciąga ich kluczowe cechy geometryczne.
- **Parametry**: `Mode` (Lite, Full).
- **Wynik**: Raport tekstowy o położeniu, długościach, polach powierzchni.

### 10. `ReadPropertyTool`
Pobiera jedną, konkretną właściwość i zapisuje ją do zmiennej.
- **Parametry**: `Property` (np. `Length`, `Center.X`), `SaveAs`.

### 11. `ReadTextSampleTool`
Pobiera reprezentatywną próbkę tekstów z zaznaczenia (DBText, MText, MLeader).
- **Mechanizm**: Próbkowanie statystyczne (sqrt) zapobiega przepełnieniu okna kontekstowego (Context Window).

### 12. `AnalyzeSelectionTool`
Wykonuje agregację danych w pamięci.
- **Parametry**: `Mode` (CountTypes, ListUniqueValues), `TargetProperty`, `SaveAs`.
- **Zastosowanie**: "Ile mam linii?", "Wypisz wszystkie użyte warstwy w tym detalu".

---

## 🔧 Modyfikacja i Tekst

### 13. `ModifyProperties`
Masowa edycja właściwości obiektów w zaznaczeniu.
- **Parametry**: `Modifications` (Prop/Val).
- **Tarcza Anty-Halucynacyjna**: Każda właściwość jest walidowana pod kątem poprawności dla danego typu obiektu przed próbą zapisu.

### 14. `TextEditTool`
Zaawansowana edycja treści i formatowania RTF.
- **Parametry**: `Mode` (Append, Prepend, Replace, FormatHighlight, ClearFormatting), `FindText`, `ReplaceWith`, `ColorIndex`, `IsBold`.
- **Funkcja**: Pozwala podświetlać błędy lub dodawać prefiksy masowo.

### 15. `ManageLayers`
Zarządzanie tabelą warstw.
- **Parametry**: `Action` (Create, Modify, Delete), `LayerName` (obsługuje maski `*`), `ColorIndex`, `IsOff`, `IsFrozen`, `IsLocked`, `Linetype`.

---

## 📊 Narzędzia Pomocnicze i Systemowe

### 16. `Foreach`
Pomaga LLM w iterowaniu po listach zapisanych w pamięci `@Variables`.
- **Parametry**: `TargetVariable`, `Separator`, `Action` (List, Count).

### 17. `ManageAnnoScales`
Obsługa skal opisowych (Annotative).
- **Parametry**: `Action` (Add, Remove, Read), `ScaleName`, `DisableAnnotative`, `SaveAs`.

### 18. `ExecuteMacro`
Uruchamia predefiniowane skrypty lub własne polecenia LISP/RPN.
- **Parametry**: `MacroName`, `CustomCommand`.
- **Makra GOLD**: `CleanDrawings`, `ResetLayers`, `ZoomExtents`.

### 19. `UserInput`
Interaktywne pobieranie danych od człowieka.
- **Parametry**: `PromptMessage`, `InputType` (String, Integer, Double, Point), `SaveAs`.

### 20. `UserChoice`
Wyświetla menu wyboru w linii komend BricsCAD.
- **Parametry**: `PromptMessage`, `Options` (lista), `SaveAs`.

---

## 💡 Pamięć Agenta (Context)

Agent korzysta z dwóch rodzajów pamięci:
1. **ActiveSelection**: Lista `ObjectId` obiektów zaznaczonych na rysunku.
2. **Variables (cache)**: Słownik klucz-wartość (np. `@MojaDlugosc`), do którego można zapisywać wyniki narzędzi i wstrzykiwać je jako parametry w następnych krokach za pomocą symbolu `$`.

> [!NOTE]
> Wszystkie narzędzia zwracają raport tekstowy, który model LLM interpretuje, aby podjąć decyzję o kolejnym kroku lub zakończeniu zadania.
