 **Rdzeń Architektoniczny (Główne Moduły Sterujące)** to absolutne serce aplikacji. 
 To w tych czterech klasach dzieje się największa "magia" związana z ujarzmianiem 
 asynchronicznej sztucznej inteligencji i zmuszaniem jej do bezpiecznej, synchronicznej 
 pracy na wrażliwej bazie danych programu inżynieryjnego.

Oto dogłębna, techniczna analiza każdego z tych głównych modułów linijka po linijce:


### 1. Mózg Operacji: `AgentCommand.cs`
Jest to najpotężniejszy i najbardziej złożony plik w całym projekcie. Pełni rolę serwera zarządzającego, rutera i tłumacza.

* **Gigantyczny System Prompt:** Na samym początku klasy znajduje się potężny zmienny string `systemPrompt`. To zbiór sztywnych reguł dla modelu językowego (LLM). Uczy on modela składni tagów, wyjaśnia różnice między kolorami `ByLayer` a ACI, tłumaczy działanie kalkulatora RPN oraz zawiera "Krytyczne Zasady Bezpieczeństwa" (np. zakaz zmyślania zaznaczeń, zakaz gadania tekstem, gdy używa się akcji). Dodatkowo kod dynamicznie wkleja do promptu aktualne jednostki fizyczne z rysunku CAD (`INSUNITS`), dając Agentowi pełną świadomość skali, w jakiej pracuje.
* **Główna Pętla Asynchroniczna (`ZapytajAgentaAsync`):** To tutaj program łączy się z portem `127.0.0.1:1234` (lokalny LM Studio) poprzez `HttpClient`. Pętla posiada system **Autokorekty (Chain of Thought)**. Jeśli np. model odpowie zwykłym tekstem zapominając o tagach, pętla nie crashuje, lecz przechwytuje odpowiedź, ukrywa ją przed użytkownikiem i wysyła reprymendę: *"[SYSTEM] BŁĄD KRYTYCZNY: Twoja odpowiedź nie zawierała żadnego tagu!"* wymuszając na modelu wygenerowanie nowej, poprawnej odpowiedzi w tle (do 2 prób naprawczych).
* **Magiczny Wrapper (`WykonajWCADAsync`):** Absolutny majstersztyk pozwalający ominąć twarde limity API BricsCADa. CAD wymaga, aby wszystkie zmiany w rysunku działy się na głównym wątku (UI). Czekanie na AI jest asynchroniczne (w tle). Kod zapisuje to, co Agent chce zrobić (np. `SELECT` czy `ModifyGeometry`) do globalnej zmiennej `AktualneZadanieCAD` w postaci lambdy, a następnie wpisuje do paska poleceń ukrytą komendę `_AGENT_RUN_TOOL`. Ta komenda, odpalając się w natywnym środowisku CAD, fizycznie wykonuje zakolejkowane zadanie i zwraca wynik do Taska przez `TaskCompletionSource`.
* **Silnik Zaznaczania (`WykonajInteligentneZaznaczenie`):** To tutaj działa tag `[SELECT]`. Parser rozbija JSON na warunki logiczne (`Property`, `Operator`, `Value`). Odczytuje cechy obiektów w locie używając Refleksji C# (`propInfo.GetValue`) i potrafi odcedzać halucynacje (np. mapując sztuczną właściwość `VisualColor` wprost na sprawdzenie `ColorIndex` we wsparciu o ustawienia warstwy `ByLayer`). Wyszukuje nawet obiekty zagnieżdżone w strukturze pod-bloków (`Scope: Blocks`).

---

### 2. Tarcza Ochronna Bazy Danych: `TagValidator.cs`
Sztuczna Inteligencja często zmyśla rzeczy, których nie wie (tzw. halucynacje). Jeśli model spróbowałby zmienić nieistniejącą właściwość obiektu, zcrashowałby BricsCADa. Ten plik zapobiega temu całkowicie.

* **Inicjalizacja Bazy Wiedzy (`LoadApiCache`):** Podczas pierwszego uruchomienia, kod wczytuje dwa słowniki (`BricsCAD_API_Quick.txt` i `V22.txt`). Używając wyrażeń regularnych (`Regex`) wyciąga stamtąd wszystkie możliwe Klasy (np. `Line`, `MText`) oraz podległe im właściwości, tworząc w pamięci superszybką tablicę mieszającą `HashSet`.
* **Walidacja Składni (`ValidateSequence`):** Kiedy Agent wysyła komendę `[ACTION]` lub `[SELECT]`, ZANIM wykona się jakikolwiek kod modyfikujący rysunek, tekst przechodzi przez ten walidator. 
    * Sprawdzane są zamknięcia nawiasów klamrowych `{}` i kwadratowych `[]` (chroni to przed ucięciem wyjścia JSON przez model).
    * Metoda `CheckProperty` sprawdza wyłuskane z JSONa intencje modelu. Jeśli Agent napisze `"Property": "GrubośćŚciany"`, walidator odrzuci to natychmiast i prześle przez pętlę w `AgentCommand` informację o błędzie z prośbą do LLM o naprawę, chroniąc silnik CAD.
    * Posiada również *WhiteListę* (Dozwolone parametry) specyficzną dla narzędzia zarządzania warstwami (`MANAGE_LAYERS`), upewniając się, że model nie zepsuje tabeli warstw.

---

### 3. Moduł Trwałego Kontekstu: `AgentMemory.cs`
Dzięki temu modułowi Agent potrafi wykonywać procesy, które łączą wiele narzędzi krok po kroku (np. Zaznacz -> Odczytaj -> Pamiętaj -> Użyj w obliczeniach).

* Klasa jest wybitnie krótka, ale genialna w swojej prostocie. Przechowuje zmienne w statycznym słowniku `Variables` (odpornym na wielkość liter).
* **Inteligentne Wstrzykiwanie (`InjectVariables`):** Kiedy Agent wywoła narzędzie, np. wstawienia tekstu o treści `"@MojaWarstwa"`, kod narzędzia najpierw przepuszcza ten string przez tę metodę. 
* **Zabezpieczenie Sortowaniem:** Autor zastosował sprytny trik: `Variables.Keys.OrderByDescending(k => k.Length)`. Sortuje on nazwy zmiennych od najdłuższej do najkrótszej. Dlaczego to krytyczne? Jeśli mielibyśmy w pamięci zmienne `@Promien` i `@PromienOkręgu`, zamiana tekstu z góry do dołu mogłaby nadpisać fragment większej zmiennej. Sortowanie od najdłuższego naprawia ten błąd logiki.

---

### 4. Połączenie z człowiekiem (UI): `AgentControl.cs`
To nie jest tylko okno czatu C# WinForms podpinane do palety BricsCAD. To wysoce zoptymalizowane narzędzie interakcji i tzw. Data-Harvestingu (Zbiórki Danych).

* **Organizacja kart (TabControl):** Interfejs dzieli się na zakładki: Czat z AI, Logi tagów (dawniej tryb deweloperski), DB Manager, Moje Makra oraz Ustawienia (zmiana motywu Dark/Light wpisywana na stałe do Rejestru Windows).
* **Pasek HUD (Heads-Up Display):** Metoda `UpdateStatsUI` aktualizuje pasek informacyjny na żywo, zbierając z pętli Agenta dane o zużytych tokenach (`promptTokens`, `completionTokens`), czasie myślenia modelu, i oblicza na tej podstawie rzeczywistą prędkość generowania w Tokenach na Sekundę (`t/s`).
* **System "Złotego Standardu" (Harvesting):** Kod nasłuchuje na Eventy głównego Agenta `Komendy.OnTagGenerated += CatchTagForTraining`. Gdy model LLM wygeneruje IDEALNIE poprawny tag po skomplikowanym poleceniu użytkownika, okno czatu w tle natychmiast formatuje to jako parę (Prompt użytkownika -> Odpowiedź Asystenta) i zapisuje w zakładce "Logi Tagów". Pozwala to deweloperowi na zapisanie tych perfekcyjnych zagrań do plików `.jsonl` i douczanie modelu w przyszłości.
* **Edytor z podświetlaniem składni (`ApplySyntaxHighlighting`):** Wbudowany prosty edytor w `RichTextBox`, który w czasie rzeczywistym używając wyrażeń Regex, koloruje składnię JSONa (klucze na niebiesko, wartości tekstowe na czerwono, nazwy tagów na fioletowo + pogrubienie), ułatwiając użytkownikowi odczytywanie skomplikowanych danych.