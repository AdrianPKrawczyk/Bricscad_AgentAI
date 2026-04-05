# BricsCAD Agent AI V2: Tools Technical Reference (v2.7.0 GOLD)

Ten dokument zawiera pełną specyfikację techniczną dla wszystkich narzędzi systemowych dostępnych w wersji **GOLD**. Od wersji **v2.7.0** wprowadzono **Semantic Tool Routing**, który pozwala optymalizować pracę Agenta poprzez przydzielanie narzędzi do konkretnych pul tematycznych (Tagi).

---

## 🏷️ System Tagowania (Semantic Routing)
Każde narzędzie należy do co najmniej jednej kategorii. Tagi te są używane przez użytkownika (poprzez `#`) lub przez Agenta (poprzez `RequestAdditionalTools`) do dynamicznego zarządzania kontekstem.

- **#core**: Narzędzia podstawowe (zawsze dostępne).
- **#bloki**: Zarządzanie definicjami i instancjami bloków.
- **#warstwy**: Tabela warstw i ich właściwości.
- **#tekst**: Edycja treści, formatowania i skal opisowych.
- **#makro**: Automatyzacja złożonych procedur.

---

## 🏢 Zarządzanie Obiektami i Blokami

### 1. `CreateObject` **[Tag: #core]**
Tworzy nową geometrię i teksty w rysunku.
- **Parametry**: `EntityType` (Line, Circle, DBText, MText, MLeader), `Layer`, `StartPoint`, `EndPoint`, `Center`, `Radius`, `Position`, `Text`, `Height`, `Rotation`, `ArrowPoint`, `LandingPoint`.
- **Funkcje specjalne**: Obsługuje słowo kluczowe `AskUser` oraz formuły `RPN:` dla obliczeń.

### 2. `InsertBlock` **[Tag: #bloki]**
Wstawia instancję bloku (`BlockReference`).
- **Parametry**: `BlockName`, `InsertionPoint`, `Scale`, `Rotation`, `Attributes` (lista `Tag/Value`).

### 3. `CreateBlock` **[Tag: #bloki]**
Tworzy nową definicję bloku (`BlockTableRecord`) z zaznaczonych elementów.
- **Parametry**: `BlockName`, `BasePoint`, `DeleteOriginals`.

### 4. `EditBlock` **[Tag: #bloki]**
Modyfikuje obiekty **wewnątrz** definicji bloku (globalnie).
- **Parametry**: `BlockName`, `Recursive`, `FindText`, `ReplaceText`, `Modifications`.

### 5. `EditAttributes` **[Tag: #bloki]**
Edytuje wartości atrybutów w konkretnych instancjach bloków (lokalnie).
- **Parametry**: `Action` (Read/Update), `Attributes` (Tag/Value).

### 6. `ListBlocks` **[Tag: #bloki]**
Zwraca listę nazw wszystkich dostępnych bloków (pomija anonimowe i XREF).
- **Parametry**: `SaveAs`.

---

## 🔍 Analiza i Selekcja

### 7. `SelectEntities` **[Tag: #core]**
Fundament pracy Agenta. Inteligentna wyszukiwarka obiektów.
- **Parametry**: `Mode` (New, Add, Remove, Clear), `EntityType`, `Conditions`.

### 8. `InspectEntity` **[Tag: #core]**
Zwraca surowy zrzut danych JSON o konkretnym obiekcie (na podstawie Handle).
- **Parametry**: `EntityHandle`.

### 9. `ReadPropertyTool` **[Tag: #core]**
Pobiera jedną, konkretną właściwość (np. `Length`) i zapisuje ją do zmiennej.
- **Parametry**: `Property`, `SaveAs`.

### 10. `GetPropertiesTool` **[Tag: #core]**
Skanuje obiekty w zaznaczeniu i wyciąga cechy geometryczne.
- **Parametry**: `Mode` (Lite, Full).

### 11. `AnalyzeSelectionTool` **[Tag: #core]**
Wykonuje agregację danych (Count, ListUnique).
- **Parametry**: `Mode`, `TargetProperty`, `SaveAs`.

### 12. `ReadTextSampleTool` **[Tag: #tekst]**
Pobiera reprezentatywną próbkę tekstów z zaznaczenia.
- **Mechanizm**: Próbkowanie statystyczne zapobiegające przepełnieniu tokenów.

---

## 🔧 Modyfikacja i Tekst

### 13. `ModifyProperties` **[Tag: #core]**
Masowa edycja właściwości obiektów w zaznaczeniu.
- **Parametry**: `Modifications` (Prop/Val). Walidacja przez `PropertyValidator`.

### 14. `TextEditTool` **[Tag: #tekst]**
Zaawansowana edycja treści i formatowania RTF.
- **Parametry**: `Mode` (Append, Prepend, Replace, Format), `FindText`, `ReplaceWith`.

### 15. `ManageLayers` **[Tag: #warstwy]**
Zarządzanie tabelą warstw (Create, Modify, Delete).
- **Parametry**: `Action`, `LayerName`, `ColorIndex`, `IsOff`, `IsFrozen`, `IsLocked`.

### 16. `ManageAnnoScales` **[Tag: #tekst]**
Obsługa skal opisowych (Annotative).
- **Parametry**: `Action`, `ScaleName`, `DisableAnnotative`.

---

## 📊 Pętle i System (Agentic)

### 17. `Foreach` **[Tag: #core]**
Iterowanie po listach zapisanych w pamięci `@Variables`.
- **Parametry**: `Items`, `Action`, `Generator`.

### 18. `ExecuteMacro` **[Tag: #makro]**
Uruchamianie predefiniowanych procedur lub własnego kodu LISP.
- **Parametry**: `MacroName`, `CustomCommand`.

### 19. `UserInput` / `UserChoice` **[Tag: #core]**
Interakcja z użytkownikiem w pasku poleceń BricsCAD.
- **Parametry**: `Prompt`, `Choices`.

### 20. `RequestAdditionalTools` **[Tag: #core]** **(NOWOŚĆ)**
Mechanizm **Agentic Fallback**. Pozwala Agentowi samodzielnie doładować brakujące pule narzędzi w trakcie pętli myślenia.
- **Parametry**: `Tags` (Tablica stringów, np. `["#bloki", "#tekst"]`).
- **Zastosowanie**: Samoleczenie w przypadku braku precyzyjnych tagów od użytkownika.
