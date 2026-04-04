# Narzędzia analityczne i odczytu (Skanowanie rysunku)
## Szczegółowy opis
Grupa Narzędzi analitycznych i odczytu (Skanowanie rysunku) stanowi "oczy" Agenta AI. Ponieważ LLM nie widzi interfejsu graficznego, te 6 narzędzi pozwala mu badać strukturę rysunku, odczytywać geometrię i rozumieć kontekst przed podjęciem jakichkolwiek działań edycyjnych.

### 1. Zliczanie typów obiektów (`AnalyzeSelectionTool.cs`)
* **Tag narzędzia:** `[ACTION:ANALYZE]`
* **Wymagane parametry:** Pusty JSON `{}`.
* **Jak to działa:** Narzędzie to odpowiada na pytanie "Co właściwie zaznaczyłem?". Agent używa go, gdy nie jest pewien, jakiego typu obiekty trafiły do jego pamięci `AktywneZaznaczenie`.
* **Logika pod maską:** Kod iteruje przez wszystkie zaznaczone obiekty (`ObjectId`), odczytuje ich typ przy pomocy `ent.GetType().Name` i zlicza ich wystąpienia przy pomocy słownika `Dictionary<string, int> licznik`.
* **Zwracany wynik:** Kompaktowe podsumowanie, np. `WYNIK ANALIZY (Łącznie 8 obiektów w pamięci): 5x Line, 3x Circle`.

### 2. Skanowanie zawartości tekstów (`ReadTextSampleTool.cs`)
* **Tag narzędzia:** `[ACTION:READ_SAMPLE]`
* **Wymagane parametry:** Pusty JSON `{}`.
* **Jak to działa:** Pozwala Agentowi "przeczytać" fizyczną treść tekstów (MText lub DBText) przed próbą ich edycji (np. zamianą wyrazów).
* **Bezpieczeństwo kontekstu LLM:** Aby zapobiec awarii wynikającej ze wstrzyknięcia tysięcy linii tekstu do promptu, autor zastosował świetny algorytm próbkowania. Maksymalna liczba odczytanych tekstów jest ograniczona do 15 sztuk, a próbka obliczana jest nieliniowo ze wzoru na pierwiastek z liczby zaznaczonych tekstów (`Math.Ceiling(Math.Sqrt(teksty.Count))`).
* **Równomierne próbkowanie:** Pętla nie pobiera tylko pierwszych 15 tekstów z brzegu, ale skacze po indeksach (`Math.Round(i * step)`), aby wyciągnąć reprezentatywne próbki z początku, środka i końca listy.

### 3. Szybki odczyt podstawowy (`GetPropertiesToolLite.cs`)
* **Tag narzędzia:** `[ACTION:GET_PROPERTIES_LITE]`
* **Jak to działa:** Skanuje obiekty bez użycia obciążającej pamięć Refleksji. Agent otrzymuje gotowy, ustrukturyzowany zbiór najważniejszych cech geometrii.
* **Logika pod maską:** Narzędzie analizuje maksymalnie pierwsze 15 obiektów z zaznaczenia. Zawsze wypisuje bazowe parametry: `Layer`, `ColorIndex`, `Linetype`, `LineWeight` oraz bezpiecznie sformatowaną `Transparency`. Następnie używa drabinki instrukcji `if/else`, aby dodać cechy unikalne dla poszczególnych typów, np.:
    * Dla `Line`: Długość, punkt początkowy i końcowy.
    * Dla `Circle`: Promień, Powierzchnia, Środek.
    * Dla `MLeader` (Odnośników): Sprawdza zawartość tekstową (`mleader.MText.Text`) lub raportuje obecność bloku.

### 4. Głęboki skan (Refleksja) (`GetPropertiesTool.cs`)
* **Tag narzędzia:** `[ACTION:GET_PROPERTIES]`
* **Jak to działa:** Potężne narzędzie inżynieryjne wykorzystujące "Refleksję" w języku C# do wyciągnięcia absolutnie wszystkich dostępnych i publicznych parametrów API z danego obiektu, w tym bardzo rzadkich zmiennych.
* **Ograniczenia i filtry bezpieczeństwa:** Skan jest limitowany do maksymalnie 5 pierwszych obiektów `Math.Min(ids.Length, 5)`. Co najważniejsze, kod filtruje wyciągnięte właściwości: wyrzuca złożone obiekty wskaźnikowe (zrozumiałe tylko dla kompilatora) i zostawia wyłącznie parametry, które LLM może przetworzyć (`IsPrimitive`, `IsEnum`, `string`, `Point3d`, `Vector3d`, `Color`, `LineWeight`). Zebrane właściwości są sortowane alfabetycznie.

### 5. Pojedynczy pobór danych i Pamięć (`ReadPropertyTool.cs`)
* **Tag narzędzia:** `[ACTION:READ_PROPERTY]`
* **Wymagane parametry:** JSON typu `{"Property": "Nazwa", "SaveAs": "NazwaZmiennej"}`.
* **Jak to działa:** Pozwala Agentowi wycelować precyzyjnie w jedną konkretną wartość na wszystkich zaznaczonych obiektach (np. pobierz wszystkie promienie). 
* **Mechanika Pamięci (AgentMemory):** Jeśli Agent prześle parametr `SaveAs`, kod zbierze odczytane wartości i zapisze je do globalnego słownika (`AgentMemory.Variables`) w formie oddzielonych separatorem tekstów. Zmienna ta jest wstrzykiwana później do innych tagów jako np. `@MojaZmienna`.
* **"Wirtualne Właściwości":** Kod narzędzia nie polega wyłącznie na surowym API CADa, lecz definiuje własne, ujednolicone parametry matematyczne (które natywnie w CADzie są odrębnymi metodami). Przykłady:
    * `MidPoint`: Oblicza punkt w połowie całkowitej długości każdej krzywej.
    * `Length`: Wykorzystuje bezpieczne wywołanie `GetDistanceAtParameter(EndParam)`.
    * `Area`: Uniwersalnie pobiera powierzchnię dla zamkniętych polilinii lub kreskowań (Hatch).
    * `Centroid` i `Volume`: Automatycznie wyciąga środek ciężkości oraz objętość z `MassProperties` brył `Solid3d`.
    * `Angle`: Wymusza pobranie tangensa kąta w środku krzywej (np. by pozycjonować napisy).
    * `Value`: Uniwersalnie wyciąga surowy tekst (bez ukrytego formatowania RTF) z obiektów `DBText` oraz `MText`.

### 6. Agregator Danych (`ListUniqueTool.cs`)
* **Tag narzędzia:** `[ACTION:LIST_UNIQUE]`
* **Wymagane parametry:** JSON w postaci np. `{"Target": "Class|Property", "Scope": "Selection|Model|Blocks|Database"}`.
* **Jak to działa:** Służy do odsiewania duplikatów. Zwraca unikalną listę klas (typów obiektów) lub unikalne wartości konkretnej właściwości w zadanym środowisku. LLM może to wykorzystać, gdy chce np. poprosić użytkownika o wybór jednej z 5 warstw, które realnie istnieją w pliku DWG, zamiast "zgadywać".
* **Potężne przeszukiwanie obszarów (Scope):** Cechą wyróżniającą to narzędzie jest to, że potrafi ominąć aktywne zaznaczenie i przeszukać:
    * `Model` - całą przestrzeń modelu (każdy narysowany obiekt).
    * `Blocks` - obiekty ukryte we wnętrzach każdego zdefiniowanego bloku (ignorując arkusze i bloki anonimowe).
    * `Database` - bezpośrednio skanuje niskopoziomowe tabele systemowe CADa (np. `LayerTable`), aby wyciągnąć 100% zdefiniowanych warstw, nawet jeśli nie ma na nich żadnego narysowanego obiektu.

# Narzędzia tworzenia i edycji geometrii
## Szczegółowy opis
Grupa **Narzędzi tworzenia i edycji geometrii** składa się z trzech potężnych klas, które pozwalają Agentowi AI na fizyczną ingerencję w przestrzeń rysunku. Umożliwiają one rysowanie nowych elementów, modyfikowanie ich kształtu/położenia oraz zaawansowaną edycję ich parametrów.

### 1. Rysowanie i generowanie obiektów (`CreateObjectTool.cs`)
* **Tag narzędzia:** `[ACTION:CREATE_OBJECT]`
* **Jak to działa:** Narzędzie służy do wstawiania nowych obiektów (tzw. Entity) do aktualnej przestrzeni modelu. Kod dzieli proces na dwie fazy, co pozwala na bezpieczną interakcję z użytkownikiem bez blokowania całego programu.
* **Faza 1 - Zbieranie danych (Bez blokady):** Zanim program założy tzw. `DocumentLock` (blokadę bazy danych CAD), zbiera wszystkie parametry. 
    * Narzędzie posiada unikalną funkcję `"AskUser"`. Jeśli LLM wstawi to słowo jako wartość parametru (np. zamiast współrzędnych punktu), kod C# zatrzyma się i wywoła polecenie `ed.GetPoint` lub `ed.GetString`, prosząc fizycznego użytkownika o kliknięcie myszką na ekranie lub wpisanie tekstu.
    * Parametry liczbowe (np. promień okręgu, rotacja, wysokość tekstu) oraz zawartość tekstów mogą być przetwarzane w locie przez kalkulator inżynieryjny, jeśli zaczynają się od przedrostka `"RPN:"` (`RpnCalculator.Evaluate`).
* **Faza 2 - Zapis do bazy:** Po zebraniu danych (i ewentualnych kliknięciach użytkownika), zakładana jest transakcja, a utworzony obiekt (Line, Circle, DBText, MText lub MLeader) trafia do bazy `CurrentSpaceId`. Opcjonalnie parametr `"SelectObject": true` może od razu zaznaczyć ten nowy obiekt w pamięci.
* **Inteligentne interpretacje:** Dla tekstów obsługiwane jest justowanie (`MiddleCenter`, `BottomCenter`). Dla okręgów LLM może podać promień (`Radius`) lub średnicę (`Diameter`), a kod automatycznie przeliczy średnicę dzieląc ją przez 2.0.

### 2. Edycja transformacji i fizyczna modyfikacja (`ModifyGeometryTool.cs`)
* **Tag narzędzia:** `[ACTION:MODIFY_GEOMETRY]`
* **Jak to działa:** Zmienia geometrię istniejących, zaznaczonych obiektów z użyciem niskopoziomowych macierzy transformacji (`Matrix3d`). Narzędzie to działa na wszystkich obiektach zebranych w pamięci `AktywneZaznaczenie` w pętli `foreach`.
* **Obsługiwane tryby (Mode):**
    * **`Erase` (Usuń):** Fizycznie usuwa obiekty (`ent.Erase()`) i zwalnia pamięć zaznaczenia Agenta.
    * **`Move` (Przesuń):** Wykorzystuje wektor 3D (`Vector3d`) i przesuwa obiekty macierzą przemieszczenia (`Matrix3d.Displacement`).
    * **`Copy` (Kopiuj):** Kloniuje obiekt w pamięci RAM (`ent.Clone()`), przesuwa klona o podany wektor i zapisuje go w bazie danych jako osobny element.
    * **`Rotate` (Obróć):** Wymaga punktu bazowego (`BasePoint`) oraz kąta. Kod C# dba o to, by automatycznie przeliczyć podane przez model stopnie na wymagane przez API radiany (`angleDeg * Math.PI / 180.0`), a następnie stosuje `Matrix3d.Rotation`.
    * **`Scale` (Skaluj):** Podobnie jak rotacja, wymaga punktu bazowego oraz mnożnika (`Factor`), używając `Matrix3d.Scaling`.
* **Bezpieczne parsowanie:** Kod wykorzystuje własne funkcje (np. `ParseVector`, `ParsePoint`), które są wysoce odporne na błędy LLM (takie jak pomieszanie kropek z przecinkami w zapisie dziesiętnym lub niepotrzebne nawiasy).

### 3. Zmiana parametrów i zaawansowana Refleksja (`SetPropertiesTool.cs`)
* **Tag narzędzia:** `[ACTION:SET_PROPERTIES]`
* **Jak to działa:** Jest to uniwersalny modyfikator wartości obiektów. W JSON-ie przyjmuje strukturę warunków zawierających nazwę właściwości (`Property`), docelową wartość (`Value`) i operator przypisania (`Operator`).
* **Kluczowe mechaniki pod maską:**
    * **Tłumacz natywny API:** Zamiast rzucać wszystko na Refleksję, narzędzie posiada ręcznie zoptymalizowane ścieżki dla głównych cech CAD. Na przykład tłumaczy `Color` z palety ACI lub stringa RGB (`255,0,0`) na `Teigha.Colors.Color`, a `LineWeight` oraz `Transparency` parsuje ze stanów typu `"ByLayer"` / `"ByBlock"` na enumeratory.
    * **Operatory matematyczne i RPN:** Narzędzie to nie tylko "nadpisuje" wartość. Jeśli podamy operator np. `+`, `-`, `*`, program odczyta aktualną wartość właściwości z obiektu i wykona działanie. Jeśli operatorem jest `RPN`, wartość zostanie policzona na zaawansowanym kalkulatorze wstrzykując obecną wartość do stosu.
    * **Zagnieżdżenia (punkty 3D):** Za pomocą C# Reflection narzędzie wspiera modyfikację konkretnej osi współrzędnych. Jeśli LLM wywoła `{"Property": "Position.Z", "Operator": "+", "Value": "100"}`, kod wyciągnie punkt, doda 100 tylko do osi Z i utworzy nowy strukturnie punkt `Point3d`, a następnie przypisze go z powrotem.
    * **Skale opisowe (Annotative):** Automatyzuje skomplikowany proces API BricsCADa przypinania skal opisowych (`AnnotationScale`) do obiektów poprzez odszukiwanie ich w tablicach systemowych (`ACDB_ANNOTATIONSCALES`).
    * **Strzałki wymiarowe (`Dimblk`):** Narzędzie posiada zaszyte obejście (workaround) API CAD, które podmienia strzałki za pomocą zmiany zmiennej systemowej aplikacji (`Bricscad.ApplicationServices.Application.SetSystemVariable`), odczytuje jej wskaźnik bazodanowy (`ObjectId`), i dopiero ten wskaźnik przypisuje do obiektu.
    * Po udanych zmianach kod wymusza na rysunku fizyczne przeładowanie ekranu `doc.Editor.Regen()`.

# Narzędzia edycji tekstów
## Szczegółowy opis
Grupa **Narzędzi edycji tekstów** stanowi wyspecjalizowany zestaw do manipulacji napisami na rysunku. 
W środowisku CAD teksty dzielą się na dwa zupełnie różne byty: proste, jednowierszowe teksty (`DBText`) oraz zaawansowane, wielowierszowe teksty formatowane (`MText`), które pod spodem używają znaczników podobnych do RTF (np. `{\C1;Tekst}` dla koloru czerwonego). Autor programu doskonale to rozróżnia, tworząc osobne narzędzia dla każdego z tych typów.
Oto szczegółowy opis mechaniki działania tych trzech narzędzi:

### 1. Zmiana formatowania tekstów wielowierszowych (`MTextFormatTool.cs`)
* **Tag narzędzia:** `[ACTION:MTEXT_FORMAT]`
* **Jak to działa:** Narzędzie to **nie zmienia** samych słów, lecz operuje wyłącznie na warstwie wizualnej tekstu `MText`. Jak widać w diagnostycznej komendzie `AGENT_INSPECT` oraz `AGENT_DEBUG_2`, obiekty `MText` posiadają właściwość `Contents` (przechowującą ukryte kody formatujące) oraz `Text` (czysty tekst). To narzędzie inteligentnie wstrzykuje lub usuwa znaczniki z właściwości `Contents`.
* **Tryby pracy (Mode):**
    * `HighlightWord` (Wyróżnij słowo): LLM podaje konkretne słowo w argumencie `"Word"`. Kod odszukuje to słowo w tekście i otacza je znacznikami koloru (`"Color"`) lub pogrubienia (`"Bold": true`). Idealne do zapytań typu: *"Zmień słowo PVC na czerwone"*.
    * `FormatAll` (Formatuj całość): Narzuca jednorodne formatowanie na cały blok tekstu.
    * `ClearFormatting` (Wyczyść formatowanie): Prawdopodobnie "czyści" ciąg `Contents` z nadmiarowych nawiasów klamrowych i kodów, przywracając tekstowi natywny wygląd zgodny z warstwą.

### 2. Edycja i podmiana zawartości MText (`MTextEditTool.cs`)
* **Tag narzędzia:** `[ACTION:MTEXT_EDIT]`
* **Jak to działa:** Pozwala na ingerencję w samą treść i dodawanie nowych informacji do istniejących opisów wielowierszowych. Co ciekawe, narzędzie potrafi **jednocześnie** modyfikować treść i nakładać na tę *nową* treść formatowanie w locie (argumenty: `Color`, `Underline`, `Bold`, `Italic`).
* **Tryby pracy (Mode):**
    * `Append`: Dokleja tekst na samym końcu zaznaczonych elementów `MText`.
    * `Prepend`: Dokleja tekst na samym początku.
    * `Replace`: Działa jak narzędzie "Znajdź i Zamień". Agent używa argumentu `"FindText"` (co ma znaleźć) oraz `"Text"` (na co ma zamienić). W połączeniu z tagiem `[ACTION:READ_SAMPLE]` (który pozwala modelowi najpierw odczytać próbkę tekstu przed zmianą), stanowi to potężne i bezpieczne narzędzie masowej korekty dokumentacji.

### 3. Edycja prostych tekstów (`TextEditTool.cs`)
* **Tag narzędzia:** `[ACTION:TEXT_EDIT]`
* **Jak to działa:** Narzędzie przeznaczone wyłącznie dla tradycyjnych, jednowierszowych tekstów z rodziny `DBText`. Ponieważ ten obiekt nie obsługuje ukrytych kodów (RTF), JSON argumentów dla tego narzędzia jest znacznie uboższy – **nie posiada** kluczy odpowiedzialnych za formatowanie (jak `Color`, `Bold` itp.). 
* **Tryby pracy (Mode):** Posiada te same, czysto modyfikujące tryby co jego bogatszy odpowiednik: `Append`, `Prepend` oraz `Replace`. Jeśli Agent chce zmienić wygląd `DBText`, musi użyć narzędzia ogólnego `[ACTION:SET_PROPERTIES]`, które podmieni główną warstwę wizualną obiektu.


# Narzędzia do pracy z blokami
## Szczegółowy opis 
Grupa **Narzędzi do pracy z blokami** pozwala na tworzenie, wstawianie, analizę oraz głęboką modyfikację tzw. definicji bloków. Blok w środowisku CAD to zgrupowany zbiór obiektów zachowujący się jak jeden element, co ułatwia zarządzanie powtarzalnymi częściami rysunku.

### 1. Tworzenie nowej definicji bloku (`CreateBlockTool.cs`)
* **Tag narzędzia:** `[ACTION:CREATE_BLOCK]`
* **Jak to działa:** Zamyka aktualnie zaznaczone w pamięci obiekty w nową definicję bloku (`BlockTableRecord`). 
* **Mechanika "AskUser":** Narzędzie to obsługuje interaktywny tryb wprowadzania danych. Agent zamiast samodzielnie wymyślać współrzędne punktu wstawienia (`BasePoint`) lub nazwę nowego bloku (`Name`), może przekazać argument `"AskUser"`. Program zatrzyma się wtedy, a BricsCAD poprosi użytkownika w pasku poleceń o wskazanie myszą punktu bazowego lub wpisanie pożądanej nazwy z klawiatury.

### 2. Fizyczne wstawianie bloków (`InsertBlockTool.cs`)
* **Tag narzędzia:** `[ACTION:INSERT_BLOCK]`
* **Zasada bezpieczeństwa dla LLM:** W system promptcie jest wyraźnie powiedziane (Krytyczne Zasady Bezpieczeństwa): *"Do wstawiania obiektów typu BlockReference ZAWSZE używaj tagu [ACTION:INSERT_BLOCK]. ZAKAZ używania CREATE_OBJECT do wstawiania bloków"*. Wynika to ze specyfiki API CADa, w którym wstawienie instrukcji bloku różni się od rysowania prostej geometrii.
* **Parametry wejściowe:** Wymaga podania nazwy istniejącego bloku (`Name`) oraz punktu docelowego wstawienia na rysunku (`Position`). Dodatkowo obsługuje parametry opcjonalne, takie jak obrócenie wystąpienia (`Rotation`), jego skala (`Scale`) czy przypisanie do konkretnej warstwy (`Layer`).

### 3. Skanowanie definicji bloków (`ListBlocksTool.cs`)
* **Tag narzędzia:** `[ACTION:LIST_BLOCKS]`
* **Jak to działa:** Zwraca listę unikalnych nazw bloków, filtrując zduplikowane wpisy. Agent używa tego narzędzia, gdy użytkownik zada pytanie typu *"Jakie bloki mam na rysunku?"* lub jeśli Agent potrzebuje sprawdzić dostępność danego bloku przed użyciem `INSERT_BLOCK`.
* **Tryb zakresu (Scope):**
    * `"Selection"` – Skanuje wyłącznie obecnie zaznaczone obiekty i wyłuskuje z nich nazwy zawartych referencji bloków.
    * `"Database"` – Przeczesuje bezpośrednio tabelę systemową CADa (`BlockTable`) w poszukiwaniu absolutnie wszystkich zapisanych definicji bloków w danym pliku DWG, nawet jeśli w danej chwili nie ma ich wstawionych fizycznie w przestrzeni rysunku.

### 4. Zaawansowana edycja wnętrza bloków (`EditBlockTool.cs`)
* **Tag narzędzia:** `[ACTION:EDIT_BLOCK]`
* **Jak to działa:** To niezwykle potężne narzędzie pozwalające Agentowi wejść do wewnątrz definicji wybranych bloków i przeprowadzić hurtową edycję ich składowych z pominięciem interfejsu graficznego (edycji BEDIT). Zmiany są od razu widoczne na wszystkich instancjach tego bloku na rysunku.
* **Możliwości parametryzacji w JSON:**
    * `"Color"` i `"Layer"` – Hurtowo zmienia kolor i warstwę dla wszystkich obiektów graficznych wewnątrz bloku (np. wymuszenie koloru 0 – `ByBlock`).
    * `"FilterColor"` – Pozwala edytować obiekty wewnętrzne selektywnie (np. zinterpretowane polecenie: *zmień we wnętrzu bloku tylko linie czerwone na zielone*).
    * `"FindText"` i `"ReplaceText"` – Mechanizm wyszukiwania i podmiany zagnieżdżonych głęboko tekstów bez "rozbijania" bloku poleceniem `EXPLODE`.
    * `"RemoveDimensions"` – Przyjmuje wartość logiczną (`true`/`false`) i z łatwością czyści zaznaczone bloki ze wszystkich wymiarów ukrytych w ich wnętrzu.

# Narzędzia zarządzania warstwami
## Szczegółowy opis
Grupa **Narzędzi zarządzania warstwami** składa się z dwóch mocno zintegrowanych narzędzi. 
Warstwy w programach CAD to kluczowy element organizacji rysunku. Autor aplikacji stworzył narzędzia, które pozwalają Agentowi AI nie tylko tworzyć i zmieniać warstwy, ale również automatycznie analizować tablice systemowe w poszukiwaniu odpowiednich nazw i porządkować plik (np. poprzez usuwanie i scalanie).

### 1. Automatyczna wyszukiwarka warstw (`SearchLayersTool.cs`)
* **Tag narzędzia:** `[ACTION:SEARCH_LAYERS]`
* **Jak to działa:** Jest to wyspecjalizowany skaner tabeli warstw (`LayerTable`). W przeciwieństwie do polecenia `SELECT` (które szuka obiektów na warstwach), to narzędzie filtruje *same definicje warstw* w bazie danych pliku DWG na podstawie nazw.
* **Kryteria wyszukiwania (Condition):** Obsługuje zaawansowane operatory dopasowania tekstu (string matching): `Contains` (zawiera), `StartsWith` (zaczyna się od), `EndsWith` (kończy się na) oraz `Equals` (równa się).
* **Mechanika Pamięci (`SaveAs`):** Prawdziwa siła tego narzędzia objawia się w obsłudze parametru `"SaveAs"`. Jeśli LLM wywoła to narzędzie np. po to, by znaleźć wszystkie warstwy z przedrostkiem "STARE_", może od razu zapisać wynik w pamięci globalnej Agenta (np. `@WarstwyDoUsuniecia`). Pozwala to na "łańcuchowanie" komend i późniejsze przekazanie tej listy do narzędzia usuwającego warstwy. W `AgentCommand.cs` widać, że program jest specjalnie poinstruowany, aby przechwytywać wyniki zaczynające się od prefiksu `[ZAPISANO...` pochodzącego z tego narzędzia, co wyzwala rekurencję (kontynuację pracy modelu).

### 2. Kombajn do zarządzania tabelą warstw (`ManageLayersTool.cs`)
* **Tag narzędzia:** `[ACTION:MANAGE_LAYERS]`
* **Jak to działa:** Jest to kompleksowe i bardzo niebezpieczne (z uwagi na moc) narzędzie do ingerencji w strukturę organizacyjną pliku. Aby zapobiec halucynacjom modelu LLM, w pliku `TagValidator.cs` znajduje się specjalna, dedykowana tylko dla tego narzędzia "biała lista" dozwolonych parametrów (kluczy w JSON): `"Mode", "Layer", "Layers", "NewName", "Color", "LineWeight", "Linetype", "Transparency", "IsOff", "IsFrozen", "IsLocked", "SourceLayers", "TargetLayer"`. Jakiekolwiek inne zmyślone argumenty zostaną natychmiast odrzucone z błędem w celu ochrony stabilności bazy danych.
* **Tryby pracy (Mode):**
    * `Create` (Twórz): Tworzy zupełnie nową warstwę. Wymaga podania nazwy `"Layer"`. Można opcjonalnie od razu nadać jej kolor, rodzaj linii, grubość czy przezroczystość.
    * `Modify` (Modyfikuj): Edytuje istniejącą warstwę. Poza zmianami wyglądu (np. `IsFrozen`, `IsLocked`), obsługuje potężny argument `"NewName"` (Nowa Nazwa), który **wspiera notację RPN**. Oznacza to, że LLM może dynamicznie przeliczać lub sklejać nową nazwę na podstawie starej (używając operatorów tekstowych jak `CONCAT` w kalkulatorze).
    * `Purge` (Czyść): Odpowiednik komendy PURGE w CAD. Automatycznie skanuje i usuwa nieużywane (puste) warstwy.
    * `Delete` (Usuń): Siłowe usunięcie konkretnych warstw. Wymaga podania listy celów w argumencie `"SourceLayers"`. Tutaj idealnie komponuje się ze zmienną z pamięci przechwyconą wcześniej przez narzędzie `SEARCH_LAYERS`.
    * `Merge` (Scalaj): Odpowiednik polecenia LAYMRG. Agreguje wiele starych warstw zdefiniowanych jako `"SourceLayers"`, przenosi całą znajdującą się na nich geometrię na warstwę docelową zdefiniowaną w kluczu `"TargetLayer"`, po czym usuwa stare warstwy z systemu.

# Narzędzia interakcji z użytkownikiem
## Szczegółowy opis
Grupa **Narzędzi interakcji z użytkownikiem** służy do przełamywania bariery między autonomicznym działaniem LLM a potrzebą decyzji ze strony człowieka. 

Gdy Agent napotyka na brakujące dane (np. nie wie, jak wysoką linię narysować lub na jaką warstwę przenieść obiekty), zamiast zgadywać lub generować błędy, może wywołać te narzędzia, aby zawiesić swoje działanie i wprost zapytać o to użytkownika BricsCADa.

### 1. Pobieranie wartości lub wskazań z ekranu (`UserInputTool.cs`)
* **Tag narzędzia:** `[ACTION:USER_INPUT]`
* **Jak to działa:** Służy do poproszenia użytkownika o proste dane wpisywane z klawiatury lub o fizyczne kliknięcie na rysunku. 
* **Tryby wprowadzania (Type):**
    * `"String"` – Program wyświetla okienko lub zapytanie w konsoli i czeka, aż użytkownik wpisze dowolny tekst (np. *"Podaj wysokość tekstu"*).
    * `"Point"` – Wymusza na użytkowniku wskazanie myszką jednego konkretnego punktu (współrzędnych X,Y,Z) na rysunku CAD.
    * `"Points"` – Pozwala użytkownikowi na wskazanie całej ścieżki lub zbioru punktów (wielokrotne klikanie).
* **Komunikat (`Prompt`):** Agent przekazuje w tym parametrze treść pytania, które ma się wyświetlić człowiekowi.
* **Kluczowa mechanika Pamięci (`SaveAs`):** Odpowiedź użytkownika nie przepada. Używając parametru `"SaveAs": "NazwaZmiennej"`, wskazany punkt lub wpisany tekst zostaje zapisany bezpośrednio do globalnego słownika zmiennych (zdefiniowanego w `AgentMemory.cs`). W kolejnym kroku Agent może użyć tej danej wstawiając symbol `@NazwaZmiennej` do narzędzi rysujących.

### 2. Wyświetlanie list wyboru (`UserChoiceTool.cs`)
* **Tag narzędzia:** `[ACTION:USER_CHOICE]`
* **Jak to działa:** To bardziej zaawansowana forma interakcji. Zamiast prosić użytkownika o ręczne wpisanie tekstu (co zawsze niesie ryzyko literówki), Agent wyświetla interaktywną listę jednokrotnego lub wielokrotnego wyboru.
* **Dynamiczne pobieranie opcji (Fetch API):** Z promptu systemowego wynika, że to narzędzie potrafi najpierw przeskanować rysunek, aby wygenerować listę opcji dla użytkownika. Używając argumentów takich jak `"FetchTarget": "Property"` oraz `"FetchProperty": "Layer"`, kod automatycznie wyciąga unikalne nazwy warstw z obszaru `"FetchScope": "Model"` i to z nich buduje menu dla człowieka.
* **Integracja:** Podobnie jak w przypadku `USER_INPUT`, wybrana z listy przez człowieka opcja (np. docelowa nazwa warstwy) jest zapisywana do pamięci RAM przy pomocy parametru `"SaveAs"` (np. jako `@WybranaWarstwa`), by Agent w kolejnym kroku nałożył tę warstwę na wybrane wcześniej obiekty.

# Narzędzia do skal opisowych (Annotative)
## Szczegółowy opis
Grupa **Narzędzi do skal opisowych (Annotative)** służy do zautomatyzowanego zarządzania skalowaniem obiektów.  Skale opisowe w systemach CAD są technicznie dość skomplikowane, ponieważ nie są zwykłą właściwością tekstową czy liczbową, lecz ukrytymi "Kontekstami Obiektu" (`ObjectContext`), zarządzanymi przez tablice słownikowe bazy danych. Autor wtyczki wyciągnął tę złożoność z API bezpośrednio do trzech dedykowanych, zrozumiałych dla LLM narzędzi.

### 1. Dodawanie skal opisowych (`AddAnnoScaleTool.cs`)
* **Tag narzędzia:** `[ACTION:ADD_ANNO_SCALE]`
* **Wymagane parametry:** Narzędzie oczekuje wywołania z argumentem w formacie JSON: `{"Scale": "1:50"}`.
* **Jak to działa:** Służy do błyskawicznego nadawania obiektom (np. tekstom, wymiarom, blokom) właściwości Annotative oraz przypisywania im konkretnej skali zdefiniowanej w rysunku.
* **Logika pod maską:** Jak widać na podstawie bliźniaczego kodu zarządzającego właściwościami w `SetPropertiesTool.cs`, przypisanie skali wymaga precyzyjnej operacji na bazach danych BricsCADa:
    1. Wymuszenie sprzętowego stanu opisowości na obiekcie: `ent.Annotative = AnnotativeStates.True`.
    2. Zainicjowanie menedżera kontekstów rysunku: `doc.Database.ObjectContextManager`.
    3. Odnalezienie systemowej kolekcji skal opisowych, która jest schowana pod twardo zakodowaną nazwą słownika `"ACDB_ANNOTATIONSCALES"`.
    4. Weryfikacja, czy dana skala w ogóle w tym rysunku występuje, i jeśli tak – podpięcie jej do zaznaczonego elementu za pomocą metody `ent.AddContext(nowaSkala)`.

### 2. Odczyt przypisanych skal (`ReadAnnoScalesTool.cs`)
* **Tag narzędzia:** `[ACTION:READ_ANNO_SCALES]`
* **Jak to działa:** Narzędzie czysto analityczne. Skanuje zaznaczone w pamięci obiekty i sprawdza, jakie skale są do nich aktualnie "podpięte". Pozwala to Agentowi zweryfikować stan rysunku przed wprowadzeniem jakichkolwiek modyfikacji.
* **Tryby odczytu:** Zgodnie z instrukcją systemową w `AgentCommand.cs`, narzędzie posiada dwa tryby zwracania informacji, którymi LLM może żonglować:
    * `"Summary"` (Podsumowanie) – kompresuje wynik, zwracając prawdopodobnie ogólną informację w stylu: *"Na 10 obiektach znaleziono łącznie skale: 1:50, 1:100"*. Jest to świetne zabezpieczenie przeciwko zapchaniu kontekstu (tokenów) Agenta niepotrzebnymi danymi.
    * `"Detailed"` (Szczegółowy) – pełny raport, wypisujący precyzyjnie przydział poszczególnych skal dla absolutnie każdego sprawdzanego obiektu (do debugowania).

### 3. Usuwanie i czyszczenie skal (`RemoveAnnoScaleTool.cs`)
* **Tag narzędzia:** `[ACTION:REMOVE_ANNO_SCALE]`
* **Jak to działa:** Umożliwia selektywne odpinanie nieużywanych skal opisowych od zaznaczonych obiektów (redukując przy tym bałagan w pliku DWG, gdyż nadmiarowe skale powielają geometrię tekstów i wymiarów w tle).
* **Mechanika oczyszczania:** Sądząc po analogicznej rutynie odpinania skal zaimplementowanej dodatkowo w `SetPropertiesTool.cs`, proces ten obsługuje dwa scenariusze:
    * Celowane usunięcie konkretnej skali z kontekstu obiektu wywołaniem `ent.RemoveContext(skalaDoUsuniecia)`.
    * Parametr twardego resetu `"All"`. Narzędzie pobiera główną/aktualną skalę obszaru modelu (`occ.CurrentContext`), a następnie iteruje pętlą przez wszystkie inne skale ukryte w obiekcie, odpinając je. Dzięki temu obiekt traci śmieciowe skale uboczne, ale pozostaje mu jedna poprawna skala, co zabezpiecza przed przypadkowym "zniknięciem" tekstu czy wymiaru z ekranu.

# Systemowe narzędzia zaawansowane
## Szczegółowy opis
Grupa **Systemowych narzędzi zaawansowanych** pełni funkcję łącznika między logiką programistyczną a tradycyjnym wierszem poleceń AutoCADa/BricsCADa:

### 1. Pętle i masowe przetwarzanie (`ForeachTool.cs` - *oczekuje na kod*)
* **Tag narzędzia:** `[ACTION:FOREACH]`
* **Ogólna zasada działania:** Wprowadza programowanie strukturalne do logiki Agenta. Pozwala modelowi LLM iterować po listach zapisanych wcześniej w pamięci podręcznej programu (`AgentMemory`).
* **Przewidywana mechanika:** Jeśli w pamięci znajduje się np. lista 10 współrzędnych ukryta pod zmienną `@Punkty`, Agent wywołując to narzędzie może przekazać instrukcję: *"Dla każdego elementu w tej liście, uruchom narzędzie CREATE_OBJECT i wstaw w tym miejscu blok"*. Znacząco oszczędza to tokeny i czas modelu, ponieważ LLM nie musi wypisywać 10 osobnych komend wstawiania – robi to za niego pętla w C#.

### 2. Bezpośrednia interakcja z wierszem poleceń (`SendCommandTool.cs` - *oczekuje na kod*)
* **Tag narzędzia:** `[ACTION:SEND_TO_CMD]`
* **Ogólna zasada działania:** Pomost pomiędzy nowoczesnym API w C# a starym, klasycznym wierszem poleceń (Command Line) w CADzie. 
* **Zastosowanie z RPN:** Narzędzie to posiada ścisłą integrację z wbudowanym w program kalkulatorem Odwrotnej Notacji Polskiej (`RpnCalculator`). Agent może użyć tego tagu do wykonania skomplikowanych obliczeń matematycznych w tle, a następnie "wstrzyknięcia" samego wyniku liczbowego (jako ciągu znaków) prosto do paska poleceń – dokładnie tak, jakby wpisał to fizycznie użytkownik na klawiaturze.

**Aby uzyskać precyzyjną i dogłębną analizę (taką jak wyżej), wklej proszę zawartość plików `ForeachTool.cs` oraz `SendCommandTool.cs`.**