# BricsCAD Agent AI V2: Tools Technical Reference (v2.8.0 GOLD)

Ten dokument zawiera pełną specyfikację techniczną dla wszystkich narzędzi systemowych dostępnych w wersji **GOLD**. Od wersji **v2.8.0** wprowadzono **Dynamiczną Konfigurację**, która pozwala na zarządzanie zestawem narzędzi bez edycji kodu źródłowego.

---

## 🏷️ System Konfiguracji (ToolConfigManager)
Dostępność narzędzi nie jest już na stałe zapisana w kodzie (Hardcoded). System korzysta z pliku `tools_config.json`, którym można zarządzać przez zakładkę **Tagi** w interfejsie Agenta.

- **IsCore**: Narzędzia podstawowe, zawsze wysyłane do modelu (np. `CreateObject`, `SelectEntities`).
- **Tags**: Kategorie tematyczne (np. `#bloki`, `#warstwy`). Narzędzia z tych grup są ładowane dynamicznie, gdy użytkownik użyje hashtaga lub gdy Agent ich zażąda.

---

## 🏢 Narzędzia Zarządzania (AI Package Manager)

### 1. `RequestAdditionalTools` **[IsCore: Tak]**
Mechanizm **Agentic Fallback** wykonany we wzorcu Menedżera Pakietów. Pozwala Agentowi samodzielnie odkryć dostępne możliwości i doładować brakujące pule narzędzi w trakcie pętli ReAct.

- **Parametry**: 
  - `Action`: `ListCategories` (pobiera listę tagów i opis ich zawartości) lub `LoadCategory` (ładuje grupę).
  - `CategoryName`: Nazwa tagu (np. `#bloki`).
- **Logika**: 
  1. Agent wywołuje `ListCategories`, aby sprawdzić co potrafi (jeśli nie ma potrzebnych narzędzi).
  2. Agent wybiera kategorię i wywołuje `LoadCategory`.
  3. `LLMClient` dodaje tag do aktywnej sesji i ponawia zapytanie z nowymi narzędziami.

---

## 🧱 Obiekty i Bloki

### 2. `CreateObject` **[IsCore: Tak]**
Tworzy nową geometrię i teksty w rysunku.
- **Parametry**: `EntityType`, `Layer`, `StartPoint`, `EndPoint`, `Center`, `Radius`, `Position`, `Text`, `Height`, `Rotation`, `ArrowPoint`, `LandingPoint`.
- **Guardrails**: Zwraca twarde błędy przy braku wymaganych punktów dla konkretnych typów obiektów.

### 3. `InsertBlock` **[TAG: #bloki]**
Wstawia instancję bloku.
- **Parametry**: `BlockName`, `InsertionPoint`, `Scale`, `Rotation`, `Attributes`.

### 4. `CreateBlock` **[TAG: #bloki]**
Tworzy nową definicję bloku z zaznaczonych elementów.

### 5. `EditBlock` **[TAG: #bloki]**
Modyfikuje obiekty wewnątrz definicji bloku (używa `Recursive` dla zagnieżdżeń).

### 6. `EditAttributes` **[TAG: #bloki]**
Edytuje wartości atrybutów (lokalnie).

### 7. `ListBlocks` **[TAG: #bloki]**
Zwraca listę nazw wszystkich dostępnych bloków.

---

## 🔍 Analiza i Selekcja

### 8. `SelectEntities` **[IsCore: Tak]**
Inteligentna wyszukiwarka obiektów. Wspiera filtry geometryczne i bazodanowe.

### 9. `InspectEntity` **[IsCore: Tak]**
Pobiera surowy zrzut danych JSON obiektu (na podstawie Handle).

### 10. `ReadPropertyTool` / `GetPropertiesTool` **[IsCore: Tak]**
Wyciąga konkretne cechy geometryczne do zmiennych Agenta.

### 11. `AnalyzeSelectionTool` **[IsCore: Tak]**
Wykonuje agregację danych (Count, ListUnique).

### 12. `ReadTextSampleTool` **[TAG: #tekst]**
Pobiera reprezentatywną próbkę tekstów z zaznaczenia.

---

## 🔧 Modyfikacja i Warstwy

### 13. `ModifyProperties` **[IsCore: Tak]**
Masowa edycja właściwości obiektów (Walidacja przez `PropertyValidator`).

### 14. `TextEditTool` **[TAG: #tekst]**
Zaawansowana edycja treści (Append, Prepend, Replace).

### 15. `ManageLayers` **[TAG: #warstwy]**
Zarządzanie tabelą warstw (Create, Modify, Delete).

### 16. `ManageAnnoScales` **[TAG: #tekst]**
Obsługa skal opisowych (Annotative).

---

## 🔄 Pętle i Automatyzacja

### 17. `Foreach` **[IsCore: Tak]**
Iterowanie po listach. Wspiera `Sequence Generator` dla masowych operacji.

### 18. `ExecuteMacro` **[TAG: #makro]**
Uruchamianie predefiniowanych procedur lub kodu LISP.

### 19. `UserInput` / `UserChoice` **[IsCore: Tak]**
Interakcja z użytkownikiem w linii komend BricsCAD.
