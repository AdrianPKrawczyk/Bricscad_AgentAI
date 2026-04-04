W **Bricscad_AgentAI**, agent dysponuje listą 25 narzędzi (implementujących interfejs `ITool`), które pozwalają mu na głęboką interakcję z programem CAD. 

Oto pełna lista narzędzi wraz z krótkim opisem ich działania, wyciągnięta z logiki inicjalizacyjnej oraz system promptu agenta:

### Narzędzia analityczne i odczytu (Skanowanie rysunku)
* **[ACTION:ANALYZE]** (`AnalyzeSelectionTool`) – Zwraca podsumowanie tego, co obecnie znajduje się w pamięci zaznaczenia. Agent używa tego przed edycją obiektów o niepewnych typach.
* **[ACTION:READ_SAMPLE]** (`ReadTextSampleTool`) – Odczytuje próbkę zawartości z zaznaczonych tekstów, co jest przydatne przed planowaną edycją lub podmianą słów.
* **[ACTION:GET_PROPERTIES_LITE]** (`GetPropertiesToolLite`) – Wykonuje szybki skan podstawowych właściwości zaznaczonych obiektów (np. Kolor, Warstwa).
* **[ACTION:GET_PROPERTIES]** (`GetPropertiesTool`) – Potężne narzędzie przeprowadzające głęboki skan (za pomocą mechanizmu Refleksji C#) wszystkich zaawansowanych parametrów z API CAD dostępnych dla zaznaczonego obiektu.
* **[ACTION:READ_PROPERTY]** (`ReadPropertyTool`) – Odczytuje pojedynczą, konkretną właściwość z zaznaczonych obiektów (wspiera również wirtualne parametry geometryczne jak `MidPoint`, `Length`, `Area`, `Volume` czy `Centroid`). Pozwala zapisać wynik do wewnętrznej pamięci agenta.
* **[ACTION:LIST_UNIQUE]** (`ListUniqueTool`) – Zwraca unikalne typy klas lub unikalne wartości konkretnej właściwości w zadanym obszarze (np. wybiera tylko unikalne warstwy z zaznaczenia).

### Narzędzia tworzenia i edycji geometrii
* **[ACTION:CREATE_OBJECT]** (`CreateObjectTool`) – Pozwala agentowi rysować obiekty w przestrzeni modelu. Obsługuje tworzenie linii, okręgów, tekstów (DBText, MText) oraz linii odniesienia (MLeader).
* **[ACTION:MODIFY_GEOMETRY]** (`ModifyGeometryTool`) – Służy do fizycznej edycji kształtu i położenia zaznaczonych obiektów. Posiada tryby pracy takie jak: `Erase` (usuń), `Move` (przesuń), `Copy` (kopiuj), `Rotate` (obróć) oraz `Scale` (skaluj).
* **[ACTION:SET_PROPERTIES]** (`SetPropertiesTool`) – Uniwersalne narzędzie do zmiany właściwości (np. zmiana koloru na czerwony lub przepięcie na inną warstwę) dla grupy zaznaczonych obiektów. Obsługuje kalkulator inżynieryjny RPN.

### Narzędzia edycji tekstów
* **[ACTION:MTEXT_FORMAT]** (`MTextFormatTool`) – Zmienia bogate formatowanie w tekstach wielowierszowych (MText). Pozwala na podświetlenie wybranego słowa na konkretny kolor, formatowanie całości lub czyszczenie formatowania.
* **[ACTION:MTEXT_EDIT]** (`MTextEditTool`) – Narzędzie modyfikujące samą treść tekstów MText (np. dodawanie słowa na końcu, na początku lub podmiana konkretnej frazy).
* **[ACTION:TEXT_EDIT]** (`TextEditTool`) – Analogiczne do MTEXT_EDIT, ale przystosowane do edytowania prostych tekstów jednowierszowych (DBText).

### Narzędzia do pracy z blokami
* **[ACTION:CREATE_BLOCK]** (`CreateBlockTool`) – Tworzy nową definicję bloku z obecnie zaznaczonych na rysunku obiektów.
* **[ACTION:INSERT_BLOCK]** (`InsertBlockTool`) – Wstawia fizyczne wystąpienie istniejącego bloku na rysunek w podanym punkcie.
* **[ACTION:LIST_BLOCKS]** (`ListBlocksTool`) – Wyciąga listę unikalnych nazw bloków występujących na rysunku lub w aktualnym zaznaczeniu.
* **[ACTION:EDIT_BLOCK]** (`EditBlockTool`) – Narzędzie pozwalające na edycję zawartości wewnątrz istniejących zaznaczonych bloków (np. zmiana koloru elementów składowych bloku, podmiana tekstów wewnątrz).

### Narzędzia zarządzania warstwami
* **[ACTION:SEARCH_LAYERS]** (`SearchLayersTool`) – Zautomatyzowana wyszukiwarka, która skanuje rysunek w poszukiwaniu warstw spełniających zadane kryteria (np. zaczynających się od konkretnego przedrostka).
* **[ACTION:MANAGE_LAYERS]** (`ManageLayersTool`) – Kompleksowe narzędzie do zarządzania warstwami. Posiada kilka trybów: `Create` (tworzenie), `Modify` (edycja parametrów takich jak kolor czy włączanie/zamrażanie), `Purge` (czyszczenie pustych warstw), `Delete` (usuwanie) oraz `Merge` (scalanie warstw).

### Narzędzia interakcji z użytkownikiem
* **[ACTION:USER_INPUT]** (`UserInputTool`) – Wywołuje okno promptu w BricsCAD i prosi użytkownika o ręczne wpisanie tekstu, wartości, lub wskazanie punktu (lub wielu punktów) za pomocą myszy.
* **[ACTION:USER_CHOICE]** (`UserChoiceTool`) – Wyświetla użytkownikowi interaktywną listę jednokrotnego lub wielokrotnego wyboru (np. z prośbą o wybranie warstwy).

### Narzędzia do skal opisowych (Annotative)
* **[ACTION:ADD_ANNO_SCALE]** (`AddAnnoScaleTool`) – Dodaje właściwość Annotative (opisową) i przypisuje do obiektu wybraną skalę.
* **[ACTION:READ_ANNO_SCALES]** (`ReadAnnoScalesTool`) – Odczytuje, jakie skale opisowe są przypisane do zaznaczonych obiektów (w trybie podsumowania lub szczegółowym).
* **[ACTION:REMOVE_ANNO_SCALE]** (`RemoveAnnoScaleTool`) – Narzędzie do usuwania przypisanych skal opisowych z obiektów.

### Systemowe narzędzia zaawansowane
* **[ACTION:FOREACH]** (`ForeachTool`) – Mechanizm pętli. Pozwala agentowi iterować po listach zachowanych w pamięci i wykonywać na nich inną wybraną akcję wielokrotnie (idealne np. do seryjnego tworzenia opisów na podstawie zebranych punktów i pól powierzchni).
* **[ACTION:SEND_TO_CMD]** (`SendCommandTool`) – Pozwala agentowi na bezpośrednie wstrzykiwanie tekstu lub wyniku obliczeń do paska poleceń CAD-a. Współpracuje z kalkulatorem `RPN` potrafiąc np. obliczyć wektor, zastosować zaokrąglenia i "wpisać" fizycznie wynik zamiast użytkownika, gdy ten korzysta z innej komendy (np. rysuje linię).