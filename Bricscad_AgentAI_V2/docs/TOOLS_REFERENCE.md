# BricsCAD Agent AI V2: Tools Technical Reference (v2.10.x GOLD)

Ten dokument zawiera pełną specyfikację techniczną dla wszystkich narzędzi systemowych dostępnych w wersji **GOLD**. Od wersji **v2.8.0** wprowadzono **Dynamiczną Konfigurację**, która pozwala na zarządzanie zestawem narzędzi bez edycji kodu źródłowego.

---

## 🏷️ System Konfiguracji (ToolConfigManager)
Dostępność narzędzi nie jest już na stałe zapisana w kodzie (Hardcoded). System korzysta z pliku `tools_config.json`, którym można zarządzać przez zakładkę **Tagi** w interfejsie Agenta.

- **IsCore**: Narzędzia podstawowe, zawsze wysyłane do modelu (np. `CreateObject`, `SelectEntities`).
- **Tags**: Kategorie tematyczne (np. `#bloki`, `#warstwy`). Narzędzia z tych grup są ładowane dynamicznie.
- **Early Exit**: Flaga określająca, czy narzędzie pozwala na natychmiastowe zakończenie pętli ReAct po poprawnym wykonaniu (Client-Side Resolution).

---

## 🏢 Narzędzia Zarządzania (AI Package Manager)

### 1. `RequestAdditionalTools` **[IsCore: Tak]**
Mechanizm **Agentic Fallback**. Pozwala Agentowi samodzielnie odkryć dostępne możliwości i doładować brakujące pule narzędzi.

### 2. `ListBlocks` **[TAG: #bloki]**
Zwraca listę nazw wszystkich dostępnych definicji bloków w rysunku.

---

## 🧱 Geometria i Tworzenie

### 3. `CreateObject` **[IsCore: Tak] [Early Exit: Tak]**
Tworzy nową geometrię (Line, Circle, Text, MText, MLeader). Wspiera RPN i autozaznaczanie.

### 4. `InsertBlock` **[TAG: #bloki] [Early Exit: Tak]**
Wstawia instancję bloku z obsługą atrybutów.

### 5. `CreateBlock` **[TAG: #bloki] [Early Exit: Tak]**
Tworzy nową definicję bloku z aktualnie zaznaczonych obiektów.

---

## 🔍 Selekcja i Inspekcja

### 6. `SelectEntities` **[IsCore: Tak]**
Główny silnik wyszukiwania. Filtruje obiekty po typach, warstwach i właściwościach. Wspiera `AdvancedFilters` dla skomplikowanych zapytań (np. `Transparency > 50`, `HatchObjectType == 1` - Gradienci, `TextOverride` zawiera tagi formatowania). Od wersji **v2.14.0** posiada mechanizm Hard-Cast Fallback dla obiektów Hatch i Dimension.

### 7. `InspectEntity` **[IsCore: Tak]**
Pobiera szczegółowy zrzut DXF/Properties dla konkretnego Handle lub pierwszego elementu z zaznaczenia.

### 8. `AnalyzeSelection` **[IsCore: Tak]**
Agreguje dane: liczy wystąpienia typów lub wykazuje unikalne wartości właściwości.

### 9. `ReadProperty` **[IsCore: Tak]**
Odczytuje konkretną właściwość i opcjonalnie zapisuje ją do zmiennej Agenta (`@Variable`).

### 10. `GetProperties` **[IsCore: Tak]**
Pobiera zestaw właściwości dla wszystkich obiektów w `ActiveSelection` (tryb Lite/Full).

### 11. `ReadTextSample` **[TAG: #tekst]**
Pobiera reprezentatywną próbkę treści z dużych zbiorów tekstów.

---

## 🔧 Edycja i Moduły

### 12. `ModifyProperties` **[IsCore: Tak] [Early Exit: Tak]**
Zmienia właściwości wspólne (Layer, Color, itp.) obiektów w pamięci. Wspiera `$OLD_...` i RPN. **UWAGA (v2.14.0)**: Narzędzie zablokowane dla właściwości tekstowych (`Text`, `Contents`) oraz wymiarowych (`DimensionText`, `Dimscale`). Do tych celów użyj `TextEdit` lub `DimensionEdit`.

### 13. `EditBlock` **[TAG: #bloki]**
Edytuje geometrię WEWNĄTRZ definicji bloku (globalnie).

### 14. `EditAttributes` **[TAG: #bloki]**
Zmienia wartości atrybutów w instancjach bloków.

### 15. `TextEdit` **[TAG: #tekst]**
Modyfikuje treść tekstów (Append, Prepend, Replace, RTF).

### 16. `ManageLayers` **[TAG: #warstwy] [Early Exit: Tak]**
Tworzy, modyfikuje lub usuwa warstwy.

### 17. `ManageAnnoScales` **[TAG: #tekst]**
Zarządza skalami adnotacyjnymi dla obiektów opisowych.

### 18. `InspectEntityTool` (alias `InspectEntity`)
Zapewnia wgląd w niskopoziomowe dane obiektu.

---

## 📐 Wymiarowanie i Detale

### 19. `DimensionEdit` **[TAG: #wymiary]**
Specjalistyczne narzędzie do modyfikacji anatomii wymiarów. Obsługuje:
- `TextOverride`: Nadpisanie tekstu lub powrót do pomiaru.
- `OverallScale`: Skala globalna tekstu i strzałek.
- `ArrowBlock`: Wybór grota strzałki (np. `_ARCHTICK`, `_DOT`).
- `TextColor`, `DimLineColor`, `ExtLineColor`: Kolory elementów wymiaru.

---

## 🔄 Pętle i Interakcja

### 20. `Foreach` **[IsCore: Tak]**
Iteruje po listach. Wspiera wbudowany `Sequence Generator` do generowania ciągów (np. punktów).

### 21. `ExecuteMacro` **[TAG: #makro] [Early Exit: Tak]**
Uruchamia predefiniowane skrypty, makra lub surowy kod LISP.

### 22. `UserInput` **[IsCore: Tak]**
Zadaje pytanie użytkownikowi (Text, Double, Point) w linii komend CAD.

### 23. `UserChoice` **[IsCore: Tak]**
Prezentuje listę słów kluczowych do wyboru przez użytkownika.

---

## 🛠️ Narzędzia Deweloperskie (Development Tools)

### Tool Sandbox (ToolSandboxControl)
Interaktywne środowisko testowe dostępne w **Dataset Studio**. Pozwala na:
- **Izolowane wywołania**: Uruchamianie `Execute(JObject args)` bezpośrednio na klasach narzędzi.
- **Auto-szablony**: Automatyczne generowanie struktury argumentów JSON na podstawie `ToolDefinition`.
- **Zarzędzanie Pamięcią**: Ręczne ładowanie zaznaczenia z CAD do `AgentMemoryState`.
- **Diagnostyka**: Logowanie wyników i błędów z pełnym śladem stosu i sygnaturą czasową.

---

## ✨ Receptury i Wzorce (Agent Recipes)

Od wersji **v2.16.0** każde narzędzie może być częścią zapisanego "Przepisu" (Drogowskazu).
- **Trigger $**: Umożliwia wywołanie sekwencji narzędzi jednym poleceniem.
- **Few-Shot Prompting**: Receptury są wstrzykiwane jako przykłady `tool_calls`, co pomaga Agentowi zrozumieć poprawne parametry i kolejność wywołań w specyficznym kontekście inżynierskim.
- **Kategoryzacja**: Wybranie przepisu może automatycznie załadować powiązane kategorie narzędzi (#bloki, #wymiary itp.), zapewniając, że Agent ma dostęp do wymaganego "zestawu instrumentów".
