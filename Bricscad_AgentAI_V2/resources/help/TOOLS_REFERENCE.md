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

### 11. `ReadXData` **[TAG: #data]**
Odczytuje rozszerzone dane (XData) z obiektów. Pozwala filtrować po nazwie aplikacji (`AppName`) i zapisywać wynik do zmiennej.

### 12. `WriteXData` **[TAG: #data]**
Zapisuje lub nadpisuje rozszerzone dane (XData) dla obiektów. Automatycznie rejestruje aplikację (RegApp).

### 13. `FindXData` **[TAG: #data]**
Skanuje zaznaczone obiekty lub wnętrze definicji bloku (rekurencyjnie) w poszukiwaniu elementów posiadających metadane XData.

### 14. `ReadTextSample` **[TAG: #tekst]**
Pobiera reprezentatywną próbkę treści z dużych zbiorów tekstów.

### 15. `CaptureVisionArea` **[IsCore: Tak]**
Pozwala na wykonanie zrzutu ekranu z wybranego obszaru rysunku. Służy jako wejście dla analizy Multimodalnej (VLM).

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

## 📐 Arkusze wydruku i Page Setup (Layout & Plot)

Narzędzia dedykowane do zarządzania arkuszami wydruku (Layouts), konfiguracją Page Setup (format papieru, drukarka, styl, skala), drukowaniem (PDF/DWF/PNG) oraz zarządzaniem stylami wydruku (CTB/STB). Dostępne przez dedykowany profil `CadLayoutProfile` (Supervisor deleguje pytania o layout/plot bezpośrednio do tego profilu).

### 24. `ListLayoutsTool` **[TAG: #layout, #wydruk] [Early Exit: Tak]**
Zwraca listę wszystkich arkuszy (Layout) w bieżącym rysunku wraz z metadanymi: nazwa, typ (Model/Arkusz), format papieru, urządzenie drukujące, styl wydruku, skala, obrót.

### 25. `ManageLayoutTool` **[TAG: #layout, #wydruk] [Early Exit: Tak]**
CRUD na arkuszach wydruku: `Create`, `Delete`, `Rename`, `Clone`, `SetCurrent`, `CopyFromTemplate` (import z DWT/DWG). Bezpieczna blokada usuwania layoutu `Model`.

### 26. `PageSetupTool` **[TAG: #layout, #wydruk, #pagesetup] [Early Exit: Tak]**
Konfiguracja Page Setup wybranego layoutu: format papieru (np. ISO A4), urządzenie drukujące, styl wydruku (CTB/STB), skala (standardowa lub własna), obrót, typ obszaru drukowania, plot origin, jednostki, shade plot, opcje lineweight/transparency/viewport borders. Waliduje dozwolone wartości enumeratorów i zwraca listę wartości przy błędzie.

### 27. `ImportLayoutTemplateTool` **[TAG: #layout, #template, #wydruk]**
Importuje layout (z Page Setup i/lub zawartością geometryczną) z zewnętrznego pliku DWG/DWT. Obsługuje opcje: tylko Page Setup, tylko entities, nadpisywanie istniejącego layoutu.

### 28. `BatchImportLayoutsTool` **[TAG: #layout, #template, #wydruk]**
Importuje wiele layoutow z zewnetrznego pliku DWG/DWT bez petli Foreach. Najpierw odczytuje arkusze w pliku zrodlowym, dopasowuje nazwy po dokladnej nazwie, fragmencie albo fuzzy, usuwa duplikaty i wykonuje kontrolowany import sekwencyjny.

### 29. `ExportLayoutTemplateTool` **[TAG: #layout, #template, #wydruk]**
Eksportuje wybrany layout do osobnego pliku DWT (szablon arkusza). Opcjonalnie kopiuje powiązane definicje bloków.

### 30. `PlotLayoutTool` **[TAG: #wydruk, #plot]**
Drukuje pojedynczy layout do PDF/DWF/PNG. Konfiguruje urządzenie wyjściowe (np. DWG To PDF.pc3), parametry skalowania, centrowania, shade plot. Generuje plik w tle przez komendę `-PLOT`.

### 31. `PublishToPdfTool` **[TAG: #wydruk, #publish, #pdf]**
Batch publish wielu layoutów. Tryb `MultiSheet` = jeden PDF z wszystkimi arkuszami. Tryb `SingleFiles` = osobny PDF per layout. Limit 50 layoutów w trybie MultiSheet. W `SingleFiles` parametr `OutputPdfPath` powinien wskazywac katalog docelowy, a opcjonalny `FileNamePrefix` dodaje prefiks do nazw plikow, np. `FileNamePrefix="VPT-TEST-"` tworzy `VPT-TEST-Arkusz1.pdf`.

### 32. `ManageViewportsTool` **[TAG: #layout, #viewport, #rzutnie, #wydruk]**
Zarzadzanie rzutniami papierowymi na layoutach: `List`, `Create`, `Modify`, `Delete`. Narzedzie uzywa natywnego API `Viewport`, bez komend CAD i bez dialogow. Obsluguje pozycje i rozmiar rzutni na arkuszu, zakres modelu Window XY (`ModelMinX/Y`, `ModelMaxX/Y`), widoki nazwane (`NamedView`), skale rzutni (`1:50`, `1_50`, `1/50`, `0.02`), skale opisowa, blokade, widocznosc, warstwe ramki i ukrywanie linii. Domyslnie nowe rzutnie trafiaja na warstwe `_rzutnie`; jesli jej brakuje, jest tworzona automatycznie z kolorem 200 i wylaczonym drukiem. Parametr `Layer` pozwala wskazac inna warstwe.

Opcje zaawansowane: `FreezeLayers`, `ThawLayers` i `ThawAllLayers` steruja zamrozeniem warstw tylko w konkretnej rzutni. `ClipBoundaryHandle` ustawia nieprostokatny clipping z istniejacej granicy papierowej, a `ClipBoundaryPaperPoints` tworzy nowa zamknieta polilinie clippingu z punktow arkusza. `RemoveNonRectClip=true` wylacza clipping nieprostokatny.

### 33. `PlotStyleTool` **[TAG: #wydruk, #plotstyle, #ctb] [Early Exit: Tak]**
Zarządzanie stylami wydruku (CTB/STB): listowanie dostępnych stylów, ładowanie z pliku (przez `_.PSETUPIN`), informacje o stylu, przypisywanie do layoutu (bieżącego, wszystkich lub wg nazwy). Bezpieczne blokady dla layoutu `Model`.

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

## SearchFileContentTool
**Typ:** Systemowe (Core)
**Uprawnienia:** AuditorProfile (do kodu), SupervisorProfile (do notatek i rysunk�w).
**Opis:** Narz�dzie dzia�aj�ce jak linuksowy grep. Szybko skanuje foldery w poszukiwaniu wyst�pie� tekstu w plikach bez �adowania ca�ych plik�w do kontekstu LLM. Posiada zabezpieczenia limituj�ce maksymaln� ilo�� wynik�w (zapobiega przepe�nieniu token�w) oraz ograniczenia na wychodzenie poza folder roboczy (Directory Traversal).
**Parametry:**
* DirectoryType *(wymagane)*: Okre�la punkt startowy wyszukiwania. Dozwolone: SourceCode (kod C# wtyczki) lub DrawingFolder (folder bie��cego dokumentu DWG).
* SearchQuery *(wymagane)*: Poszukiwana fraza tekstowa (wielko�� liter jest ignorowana).
* RelativePath *(opcjonalne)*: �cie�ka podfolderu, w kt�rym zaw�one zostanie wyszukiwanie.
* FileExtension *(opcjonalne)*: Filtr rozszerzenia, np. *.cs, *.md, *.txt.

---

## ManageSheetSetTool - Sheet Set Metadata i Custom Properties

Nowe akcje dla plikow `.dst`:

- `ListMetadata`: wypisuje ustawienia widoczne w UI Sheet Set Manager: nazwa, opis, sciezka DST, blok etykiety, bloki wywolan, liczba arkuszy, lokalizacja nowych arkuszy i wzor arkuszy.
- `SetMetadata`: zmienia natywne ustawienia zestawu: `Name`, `Description`, `NewSheetLocation`, `SheetCreationTemplatePath` + `SheetCreationTemplateLayout`, `LabelBlockPath` + `LabelBlockName`, `CalloutBlockPath` + `CalloutBlockName`.
- `ListProperties`: wypisuje wlasciwosci uzytkownika. Bez `PropertyScope` zwraca zakres `SheetSet` i `Sheet`.
- `SetProperty`: tworzy albo zmienia wlasciwosc uzytkownika.
- `RegisterPropertyName`: zapamietuje nazwy recznie utworzonych etykiet, gdy BricsCAD COM nie potrafi ich wyliczyc enumeratorem.

Parametry:

- `PropertyScope`: `SheetSet`, `Sheet` albo `SheetInstance`.
- `PropertyName`: nazwa/etykieta wlasciwosci.
- `PropertyNames`: lista nazw etykiet oddzielona przecinkami/srednikami albo tablica JSON; uzywana przy `ListProperties` i `RegisterPropertyName`.
- `PropertyValue`: wartosc albo domyslna wartosc.
- `SheetNumber`: wymagany tylko dla `SheetInstance`.

Uwaga: pola `ProjectNumber`, `ProjectName`, `ProjectPhase`, `ProjectMilestone` z sekcji "Kontrola Projektu" sa obslugiwane przez probe late-binding `Get/SetProject...`. Jesli BricsCAD COM odrzuci zapis, narzedzie zwroci ostrzezenie i nie zapisze ich jako zwyklych Custom Properties, zeby nie raportowac falszywego sukcesu w UI.

Mapowanie na UI BricsCAD:

- `SheetSet`: sekcja "Dostosuj Wlasciwosci Zestawu Arkuszy".
- `Sheet`: sekcja "Dostosuj Wlasciwosci Arkusza".
