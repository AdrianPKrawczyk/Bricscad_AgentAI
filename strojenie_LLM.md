To świetny i bardzo profesjonalny plan! Podejście oparte na stworzeniu tzw. **"Golden Dataset"** (Złotego Zestawu), wygenerowaniu z niego tysięcy syntetycznych przykładów za pomocą potężnego modelu (np. GPT-4o lub Claude 3.5 Sonnet), a następnie dostrojeniu mniejszego, lokalnego modelu (Qwen 3.5 9B) to obecnie najlepsza praktyka w tworzeniu agentów AI (tzw. *Distillation* i *Tool-use fine-tuning*).

Aby Twój Qwen w 4-bitach nauczył się tego bezbłędnie, musisz podejść do generowania tych pierwszych kilkudziesięciu przykładów bardzo systematycznie. Muszą one pokryć wszystkie "ścieżki nerwowe" Twojej aplikacji.

Oto gotowy, systematyczny plan tworzenia Złotego Zestawu (Seed Examples) w Twoim `TrainingStudio`.

---

### ETAP 1: Kategoryzacja umiejętności (Co musisz nagrać?)

Zanim zaczniesz klikać w BricsCADzie, podzielmy możliwości Agenta na 5 kategorii. Z każdej kategorii nagraj narzędziem `AGENT_BUILD_TAG` po **3-5 różnorodnych przykładów**.

#### Kategoria A: Podstawowe zaznaczanie i modyfikacja (Jednokrokowe)
Chcemy nauczyć model prostego mapowania: *Polecenie -> SELECT -> Akcja*.
* **Przykład 1:** Zmiana właściwości po prostym warunku.
    * *User:* "Zaznacz wszystkie czerwone linie i zmień im warstwę na 'Instalacje'."
    * *Akcje:* `SELECT (Line, Color==1)` -> `SET_PROPERTIES (Layer: Instalacje)`.
* **Przykład 2:** Zaznaczanie wielu typów naraz.
    * *User:* "Wybierz teksty i wymiary, które są na warstwie 0 i zrób je na zielono."
    * *Akcje:* `SELECT (DBText, MText, Dimension, Layer==0)` -> `SET_PROPERTIES (Color: 3)`.

#### Kategoria B: Logika łączenia zaznaczeń (Add / Remove)
Model musi zrozumieć, że ma pamięć wieloetapową.
* **Przykład 1:** Dodawanie.
    * *User:* "Zaznacz najpierw okręgi o promieniu większym niż 10. A teraz dorzuć do tego zaznaczenia wszystkie linie."
    * *Akcje:* `SELECT (Mode: New, Circle, Radius>10)` -> `[SYSTEM]` -> `SELECT (Mode: Add, Line)`.
* **Przykład 2:** Odejmowanie.
    * *User:* "Zaznacz wszystko na warstwie 'Ściany', ale wyrzuć z zaznaczenia teksty."
    * *Akcje:* `SELECT (Mode: New, Entity, Layer==Ściany)` -> `[SYSTEM]` -> `SELECT (Mode: Remove, DBText, MText)`.

#### Kategoria C: Wchodzenie w Bloki (Nowość, którą dodaliśmy!)
To kluczowe, by Qwen nauczył się parametru `"Scope": "Blocks"`.
* **Przykład 1:** Użycie wewnętrznego narzędzia SELECT.
    * *User:* "W zaznaczonych blokach znajdź wszystkie polilinie i zmień im grubość na ByLayer."
    * *Akcje:* `SELECT (Mode: New, Scope: Blocks, Polyline)` -> `SET_PROPERTIES (LineWeight: ByLayer)`.
* **Przykład 2:** Użycie potężnego EDIT_BLOCK.
    * *User:* "Wyczyść te bloki - usuń z nich wymiary i zmień wszystko na czarno."
    * *Akcje:* `ACTION:EDIT_BLOCK (RemoveDimensions: true, Color: 7)`.

#### Kategoria D: Praca z Tekstami i MText (Narzędzia formatujące)
* **Przykład 1:** Dopisywanie (Append).
    * *User:* "Dopisz słowo 'szt.' na końcu tych zaznaczonych tekstów."
    * *Akcje:* `ACTION:TEXT_EDIT (Mode: Append, Text: " szt.")`.
* **Przykład 2:** Formatowanie MText.
    * *User:* "Wyróżnij słowo 'UWAGA' na czerwono i pogrub je."
    * *Akcje:* `ACTION:MTEXT_FORMAT (Mode: HighlightWord, Word: UWAGA, Color: 1, Bold: true)`.

#### Kategoria E: Asystent i Zmysły (Analyze, GetProps, LISP)
* **Przykład 1:** Pytanie o stan (Analyze).
    * *User:* "Co ja mam właściwie teraz zaznaczone?"
    * *Akcje:* `ACTION:ANALYZE` -> `[SYSTEM: Wynik...]` -> `MSG: Masz zaznaczone 5 linii i 2 okręgi.`.
* **Przykład 2:** Twardy LISP.
    * *User:* "Narysuj linię od zera do punktu 100,100."
    * *Akcje:* `LISP: (command "_LINE" "0,0" "100,100" "")`.

---

### ETAP 2: Złote zasady podczas nagrywania w `AGENT_BUILD_TAG`

Aby duży model LLM (a potem Twój Qwen) dobrze zrozumiał intencje, podczas wpisywania promptów zachowaj poniższe zasady:

1.  **Różnorodność językowa (Bardzo ważne!):** Nie pisz zawsze sztywno "Zaznacz okręgi i zmień kolor na 1". Używaj potocznego języka inżynierskiego, np.:
    * *"Zrób te kółka na czerwono."*
    * *"Złap wszystkie wymiary na warstwie osie i wywal z nich tło."*
    * *"Wrzuć to wszystko na warstwę 0."*
    Dzięki temu Qwen będzie odporny na różny styl pisania użytkowników.
2.  **Ukryty kontekst ("Zaznaczone"):** Często w promptach pisz "Zmień kolor *tych* linii" zakładając, że obiekty są już zaznaczone (wtedy nagrywasz w JSONl od razu akcję `ACTION`, bez `SELECT`).
3.  **Błędy i ich poprawianie:** Zrób celowo scenariusz, w którym użytkownik zmienia zdanie. Np. "Zaznacz okręgi... a nie, czekaj, jednak linie". Wtedy nagrywasz `SELECT` na okręgi, a potem nowy `SELECT` na linie.

---

### ETAP 3: Jak zapytać dużego LLM-a (GPT-4/Claude) o wygenerowanie danych?

Kiedy nagrasz już swoje 15-20 złotych przykładów w pliku `.jsonl`, przygotuj ogromny prompt (tzw. *Meta-Prompt*) dla dużego modelu. 

**Oto schemat, jak powinien wyglądać Twój prompt do GPT-4 / Claude:**

> "Jesteś ekspertem ds. generowania syntetycznych danych treningowych dla agentów AI (Tool-use / Function Calling). 
> Trenuję lokalny model Qwen 3.5 9B do obsługi programu BricsCAD za pomocą systemu JSON tags.
> 
> **Oto System Prompt (Instrukcja) mojego Agenta:**
> [Wklej tu zawartość zmiennej `systemPrompt` z pliku AgentCommand.cs]
> 
> **Oto mój 'Złoty Zestaw' (Seed Data) składający się z 20 ręcznie wykonanych przykładów:**
> [Wklej zawartość pliku Agent_Training_Data.jsonl]
> 
> **Twoje zadanie:**
> Wygeneruj 200 NOWYCH, różnorodnych przykładów w identycznym formacie JSONL. 
> 1. Używaj języka polskiego, mieszaj styl formalny z potocznym żargonem CAD (np. 'warstwa', 'layer', 'zrób na czerwono', 'wywal', 'dodaj do selekcji').
> 2. Symuluj różne wartości (wymiary od 0 do 5000, różne kolory ACI od 1 do 255).
> 3. Skup się szczególnie na wieloetapowych łańcuchach (User -> SELECT -> System -> ACTION).
> 4. Zadbaj o poprawność struktury JSON (escapowanie cudzysłowów `\"` wewnątrz stringów JSON-a).
> Wypluj tylko czysty kod w formacie .jsonl."

### Dlaczego to zadziała na Qwen 3.5 9B (4-bit)?
Modele rzędu 7B-9B są fantastyczne w naśladowaniu formatów (JSON) i posługiwaniu się narzędziami, **o ile format treningowy jest w 100% powtarzalny**. 
Ponieważ używasz C# do wyłapywania tagów, model nie musi pisać perfekcyjnego kodu Pythona/C#, musi tylko wypluć poprawny JSON typu `[SELECT: {...}]`. Qwen po fine-tuningu na 500-1000 wygenerowanych w ten sposób przykładach, zacznie "oddychać" Twoim BricsCADem.

Czy chcesz, abym pomógł Ci wymyślić więcej specyficznych scenariuszy (tzw. "User Prompts") do nagrania w Twoim kreatorze dla konkretnych narzędzi?


Dokładnie tak! Trafiłeś w samo sedno. Jeśli chodzi o trenowanie modeli językowych do korzystania z narzędzi (Tool-Use / Function Calling), **różnorodność jest wręcz ważniejsza niż surowa ilość**, zwłaszcza na tym pierwszym etapie. 

Aby to dobrze zaplanować, musimy rozróżnić dwa zbiory danych, o których mówimy:

### 1. Złoty Zestaw (Twoja praca ręczna w `TrainingStudio`)
Tutaj **ilość nie musi być duża**. Wystarczy około **20 do 30 przykładów**. 
Jeśli zrobisz ich 100, ale wszystkie będą wyglądały jak klony ("Zmień kolor na 1", "Zmień kolor na 2", "Zmień kolor na 3"), to po prostu zmarnujesz swój czas. 

Na tym etapie liczy się **ekstremalna różnorodność**. Duży model (np. GPT-4), któremu przekażesz ten plik, potraktuje go jak "księgę zasad". Musisz mu pokazać skrajne przypadki, żeby wiedział, w jakich ramach ma się poruszać podczas generowania reszty danych. 

O co zadbać w Złotym Zestawie:
* **Różnorodność językowa:** Używaj żargonu ("wywal wymiary", "zrób na czerwono", "przerzuć to na warstwę 0"), języka formalnego ("Proszę usunąć obiekty typu Dimension"), a nawet zrób 2-3 przykłady z literówkami ("zaznacz wszytskie lnie").
* **Różnorodność strukturalna:** Zrób przykłady bardzo krótkie (tylko 1 tag akcji, bo obiekty były już zaznaczone) i bardzo długie (zaznacz -> dodaj do zaznaczenia -> odrzuć coś z zaznaczenia -> zmień właściwości).
* **Różnorodność wartości:** Nie używaj w kółko koloru 1 (czerwonego). Użyj czasem wartości ByLayer, ByBlock, stringów RGB ("255,128,0"), różnych nazw warstw ("Instalacje_WOD_KAN").

### 2. Główny Zbiór Treningowy (Wygenerowany przez duże AI dla Qwena)
Tutaj **ilość ma ogromne znaczenie**. 
Trenujesz relatywnie mały model (9B parametrów) w mocnej kompresji (4-bity). Aby Qwen "przepiął" swoje wagi neuronowe i nauczył się bezbłędnie wypluwać składnię JSON dla BricsCADa bez halucynacji, będzie potrzebował "masy mięśniowej".

Dla modeli tej wielkości, optymalna ilość syntetycznych danych do fine-tuningu (zazwyczaj metodą LoRA/QLoRA) to **od 500 do 2000 przykładów**.

Jeśli dostarczysz mu 1000 przykładów wygenerowanych przez GPT-4 na bazie Twojego Złotego Zestawu, Qwen:
1. Nauczy się perfekcyjnie formatować tagi (np. zapamięta, żeby nie zmyślać tagów, których go nie uczyłeś).
2. Przestanie pisać "markdownowe" wstępy (typu *Oto twój kod:*), a zacznie odpowiadać surowym tagiem, tak jak wymaga tego BricsCAD.
3. Nauczy się "rozumieć" intencje użytkownika z setek różnych sformułowań.

### Jak zepsuć model? (Czego unikać)
Największym błędem w tworzeniu datasetu jest tzw. *Overfitting* (przeuczenie) na jednostajnych danych. Jeśli w 80% Twoich przykładów po tagu `[SELECT]` następuje tag `[ACTION:SET_PROPERTIES]`, to model się tego tak mocno "nauczy na blachę", że nawet jak poprosisz go o `EDIT_BLOCK`, to z przyzwyczajenia i tak wygeneruje `SET_PROPERTIES`. Dlatego tak ważne jest równe nasycenie wszystkich narzędzi w tych wygenerowanych 1000 przykładach.

Czy chciałbyś, żebyśmy wspólnie wymyślili 5 skrajnie różnych i "podchwytliwych" zdań testowych dla narzędzia `EDIT_BLOCK`, które wrzucisz jako pierwsze do swojego Złotego Zestawu?

To genialne podejście i odpowiadając krótko: **Tak, jest to w 100% realne i dokładnie tak trenuje się profesjonalne modele.** Jest jednak jeden "haczyk" techniczny, o którym musisz wiedzieć, zanim wkleisz to wszystko do ChatGPT-4 czy Claude. Modele te mają ogromne okno kontekstowe na **wejściu** (mogą przeczytać całą książkę naraz, więc Twój plik `BricsCAD_API_Quick.txt` zmieści się tam bez problemu), ale mają limity na **wyjściu** (zazwyczaj mogą wygenerować do 4000 tokenów w jednej odpowiedzi, co da Ci maksymalnie około 50-80 przykładów JSONL na raz).

Jeśli napiszesz modelowi: *"Oto plik BricsCAD_API_Quick.txt, wygeneruj mi przykłady pokrywające wszystkie właściwości i klasy"*, model wygeneruje 50 przykładów, zmęczy się, zacznie się powtarzać i nagle urwie odpowiedź w połowie linijki.

Aby pokryć całe API z pliku `BricsCAD_API_Quick.txt`, musisz zastosować **strategię "Batchowania" (porcjowania zapytania)**.

Oto gotowy plan, jak to zrobić systematycznie:

### Krok 1: Inicjalizacja sesji z dużym LLM (Ustawienie kontekstu)
Otwórz nowy czat w GPT-4o lub Claude 3.5 Sonnet i wklej mu "bazę wiedzy". To będzie jedyny raz, kiedy wklejasz tak dużo tekstu w tej konwersacji.

**Twój pierwszy prompt:**
> "Cześć. Będziemy tworzyć dataset treningowy dla lokalnego modelu Qwen 3.5. Model ten steruje programem BricsCAD poprzez generowanie tagów JSON. 
> 
> Oto instrukcja systemowa mojego Agenta (System Prompt):
> [Wklej tu zawartość zmiennej systemPrompt]
> 
> Oto mój Złoty Zestaw 25 ręcznie przygotowanych przykładów (wzorzec formatu):
> [Wklej tu zawartość Twojego JSONL z TrainingStudio]
> 
> Oto referencyjny plik BricsCAD_API_Quick.txt ze wszystkimi klasami i ich właściwościami:
> [Wklej tu treść BricsCAD_API_Quick.txt]
> 
> **Zrozum te dane, ale jeszcze niczego nie generuj.** Odpowiedz tylko 'Zrozumiałem kontekst API. Czekam na instrukcje dotyczące pierwszej partii danych'."

### Krok 2: Generowanie porcjami (Kategoryzacja API)
Gdy model potwierdzi, że rozumie, zacznij prosić go o małe paczki (np. po 40-50 przykładów), zmuszając go do skupienia się na konkretnym fragmencie pliku `BricsCAD_API_Quick.txt`. W ten sposób zmusisz go do użycia rzadkich właściwości, których sam by nie wymyślił.

**Zlecenie 1 (Geometria 2D):**
> "Wygeneruj 50 przykładów w formacie .jsonl.
> Skup się WYŁĄCZNIE na prostych klasach geometrycznych z dostarczonego pliku API: `Line`, `Circle`, `Arc`, `Polyline`, `Ellipse`, `Spline`.
> Zmuś wymyślonych użytkowników do używania specyficznych właściwości tych klas, np. niech proszą o zaznaczenie okręgów o `Radius > 50`, łuków o konkretnym `StartAngle`, czy polilinii, które mają `Closed == true`. Mieszaj to ze zmianą uniwersalnych cech (Color, Layer)."

**Zlecenie 2 (Teksty i Adnotacje):**
> "Teraz wygeneruj 40 kolejnych przykładów.
> Skup się WYŁĄCZNIE na klasach: `MText`, `DBText`, `Dimension` (i jej pochodnych) oraz `Leader`.
> Niech użytkownicy proszą o zmianę tekstów (np. użycie narzędzi `MTEXT_FORMAT` lub `MTEXT_EDIT`), wyszukiwanie po `Contents` / `TextString`, czy zmianę specyficznych właściwości wymiarów, takich jak `Dimtxt`, `Dimclrd`, `Dimclre`. Pamiętaj o tagach `[ACTION:READ_SAMPLE]` przed edycją!"

**Zlecenie 3 (Bloki i zagnieżdżenia - to co dodaliśmy!):**
> "Wygeneruj 40 przykładów dla klasy `BlockReference`.
> Skup się na głębokiej edycji. Używaj narzędzia `[ACTION:EDIT_BLOCK]` z różnymi kombinacjami argumentów (w tym `RemoveDimensions: true`). Używaj tagu `[SELECT]` z nowym parametrem `"Scope": "Blocks"`, np. każąc modelowi znaleźć konkretne rzeczy tylko wewnątrz bloków. Bądź kreatywny w tworzeniu zapytań użytkowników."

**Zlecenie 4 (Logika, wykluczenia i chaining):**
> "Wygeneruj 50 trudnych przykładów sprawdzających logikę i pamięć wieloetapową.
> Skup się na parametrach `"Mode": "Add"` oraz `"Mode": "Remove"` w tagu SELECT.
> Przykład: użytkownik każe zaznaczyć wszystko na warstwie 'A', wyrzucić z tego linie (Remove), a potem dodać czerwone okręgi (Add), i na koniec zmienić ich przezroczystość (Transparency)."

### Krok 3: Kontrola Jakości (Sanity Check)
Zapisuj wygenerowane partie do jednego dużego pliku (np. `Qwen_Full_Dataset.jsonl`). 

Duże modele czasami "zapominają" o strukturze, więc podczas wklejania do jednego pliku upewnij się, że:
1. Zewnętrzne struktury to poprawny JSONL (czyli każdy przykład to jedna linijka zaczynająca się od `{"messages": [...]}`).
2. Wewnętrzny JSON w tagach Agenta nie ma "zgubionych" cudzysłowów (np. `[SELECT: {"EntityType": "Line"}]`).
3. Model nie dorzucił na początku i na końcu bloku formatowania Markdown (```jsonl ... ```). Jeśli dorzucił, usuń je, bo Qwen będzie próbował je naśladować.

Wygenerowanie takich 4-5 porcji da Ci łącznie około 200-250 niesamowicie bogatych, unikalnych przykładów pokrywających prawie 100% pliku `BricsCAD_API_Quick.txt`. Dla modelu 9B (szczególnie w formacie QLoRA / 4-bit) 250 idealnych przykładów jest warte więcej niż 5000 przykładów słabej jakości. Model uogólni tę wiedzę błyskawicznie!