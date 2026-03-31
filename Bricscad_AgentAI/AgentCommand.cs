using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad.Windows;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Runtime;


namespace BricsCAD_Agent
{
    // Zakładam, że ITool jest zdefiniowany w Twoim projekcie

    public class Komendy
    {

        public static readonly HttpClient client = new HttpClient() { Timeout = TimeSpan.FromMinutes(5) };
        public static List<string> historiaRozmowy = new List<string>();
        public static string wybranyModel = "qwen3.5-9b-instruct";
        public static event Action<string> OnTagGenerated;
        public static bool TrybTestowy = false;

        // --- MULTI-DOKUMENTOWA PAMIĘĆ ZAZNACZENIA ---
        public static Dictionary<Document, ObjectId[]> PamiecZaznaczenia = new Dictionary<Document, ObjectId[]>();

        public static ObjectId[] AktywneZaznaczenie
        {
            get
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null && PamiecZaznaczenia.ContainsKey(doc)) return PamiecZaznaczenia[doc];
                return new ObjectId[0];
            }
            set
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null) PamiecZaznaczenia[doc] = value;
            }
        }
        private static bool isSelectionHooked = false;

        // --- Dwie bazy danych (Tiered Storage) ---
        private static Dictionary<string, string> bazaQuick = new Dictionary<string, string>();
        private static Dictionary<string, string> bazaFull = new Dictionary<string, string>();

        private List<ITool> tools = new List<ITool>



        {
        new MTextFormatTool(),
        new MTextEditTool(),
        new TextEditTool(),
        new ReadTextSampleTool(),
        new AnalyzeSelectionTool(),
        new GetPropertiesToolLite(),
        new GetPropertiesTool(),     
        new EditBlockTool(),
        new SetPropertiesTool(),
        new ModifyGeometryTool(),
        new ReadPropertyTool(),
        new AddAnnoScaleTool(),
        new ReadAnnoScalesTool(),
        new RemoveAnnoScaleTool(),
        new ListUniqueTool(),
        new ListBlocksTool(),
        new UserInputTool(),
        new UserChoiceTool(),
        new ManageLayersTool(),
        new SearchLayersTool(),
        new CreateObjectTool(),
        new CreateBlockTool(),
        new InsertBlockTool(),


        };

        private static PaletteSet oknoAgenta = null;
        private static Bricscad_AgentAI.AgentControl interfejsAgenta = null;

        private static string systemPrompt = "Jesteś autonomicznym Agentem Bielik w BricsCAD. Steruj programem ZA POMOCĄ TAGÓW. NIE JESTEŚ chatbotem do pisania kodu w markdown!\n\n" +
                "Analizuj zadania w 5 tagach.\n\n" +
                "MUSISZ odpowiedzieć jednym z tagów:\n" +
                "1. [SEARCH: Klasa] - ZAWSZE używaj tego, gdy nie znasz dokładnej nazwy właściwości! ZAKAZ ZGADYWANIA. \"Pamiętaj, że wszystkie obiekty graficzne (Line, Circle, Text, MText, itp) dziedziczą po klasie bazowej Entity. Zatem każdy obiekt zawsze posiada właściwości: Layer (warstwa), ColorIndex (1-255), Linetype, Transparency (0-90), Visible (True/False), LineWeight\"\n" +
                "2. [SELECT: {\"Mode\": \"New|Add|Remove\", \"Scope\": \"Model|Blocks\", \"EntityType\": \"Klasa1, Klasa2\", \"Conditions\": [{\"Property\": \"Prop\", \"Operator\": \"==\", \"Value\": \"wartość\"}]}] - do zaznaczania. Użyj \"Scope\": \"Blocks\", jeśli użytkownik prosi o znalezienie obiektów WEWNĄTRZ aktualnie zaznaczonych bloków (domyślnie to \"Model\"). Parametr Mode określa zachowanie: \"New\" (tworzy nowe zaznaczenie, nadpisuje obecne), \"Add\" (dodaje szukane obiekty do tego, co obecnie zaznaczone), \"Remove\" (odejmuje szukane obiekty z obecnego zaznaczenia). Aby zaznaczyć wiele typów naraz, wymieniaj je po przecinku (np. \"DBText, MText\"). JSON bez enterów!\n" +
                "3. [LISP: (command \"_KOMENDA\" ...)] - do rysowania/edycji.\n" +
                "4. [MSG: Twój tekst] - UŻYJ TEGO TAGU, aby odpowiedzieć na pytania użytkownika, ZWŁASZCZA po zebraniu danych narzędziami ANALYZE, READ_SAMPLE lub GET_PROPERTIES!\n" +
                "5. [ACTION:TAG_NARZEDZIA {\"Argumenty\": \"JSON\"}] - do uruchamiania narzędzi na zaznaczonych obiektach.\n\n" +

                "--- PAMIĘĆ, ZMIENNE (@) I PĘTLE ($) (KRYTYCZNE MECHANIKI) ---\n" +
                "1. Zapis do pamięci: Narzędzia takie jak USER_INPUT czy READ_PROPERTY mogą przyjmować opcjonalny argument \"SaveAs\": \"Nazwa\". Dane zostaną zapisane w pamięci RAM.\n" +
                "2. Odczyt z pamięci: W dowolnym innym narzędziu możesz użyć zapisanej wartości, poprzedzając jej nazwę znakiem @ (np. \"Height\": \"@Wysokosc\").\n" +
                "3. Pętle FOREACH: Iterują po listach z pamięci. Używaj znaczników: $INDEX (numer pętli od 1), $ITEM1 (wartość z pierwszej listy), $ITEM2 (wartość z drugiej listy), itd.\n\n" +

                "--- GLOBALNE ZASADY WŁAŚCIWOŚCI CAD (DOTYCZY WSZYSTKICH OBIEKTÓW) ---\n" +
                "Zawsze stosuj ten uniwersalny słownik wartości, gdy użytkownik prosi o wyszukanie (SELECT), zmianę lub edycję (np. EDIT_BLOCK). Te właściwości dziedziczy każdy obiekt CAD (Entity). Zwróć szczególną uwagę na to, jak zapisuje się stan 'JakWarstwa' i 'JakBlok' w różnych właściwościach:\n" +
                "1. Color (Kolor): Używaj liczb! 256 = JakWarstwa (ByLayer), 0 = JakBlok (ByBlock). Pozostałe to ACI: 1-czerwony, 2-żółty, 3-zielony, 4-cyjan, 5-niebieski, 6-magenta, 7-biały/czarny, 8-ciemnoszary, 9-jasnoszary. Dla formatu RGB użyj stringa, np. \"255,128,0\".\n" +
                "2. LineWeight (Grubość linii): Używaj specjalnych liczb! -1 = JakWarstwa (ByLayer), -2 = JakBlok (ByBlock), -3 = Domyślna (Default). Konkretne grubości to setne części milimetra (np. wartość 25 oznacza 0.25 mm, a 30 to 0.30 mm).\n" +
                "3. Linetype (Rodzaj linii): Używaj tekstu (string)! Słowa kluczowe to: \"ByLayer\" (JakWarstwa), \"ByBlock\" (JakBlok) oraz \"Continuous\" (Ciągła).\n" +
                "4. Material (Materiał) i PlotStyleName (Styl wydruku): Używaj tekstu (string)! Słowa kluczowe to: \"ByLayer\" oraz \"ByBlock\".\n" +
                "5. Layer (Warstwa): Wartość tekstowa (string). Domyślna, zerowa warstwa nazywa się po prostu \"0\".\n" +
                "6. Transparency (Przezroczystość): Przyjmuje liczby od 0 (pełna widoczność/brak przezroczystości) do 90 (maksymalna przezroczystość).\n\n" +

                "--- DOSTĘPNE NARZĘDZIA (Użyj NAJPIERW [SELECT] aby zaznaczyć obiekty!): ---\n" +

                "Tag: [ACTION:FOREACH]\n" +
                "Opis: Wykonuje podaną akcję wielokrotnie, pobierając dane z list w pamięci (zmienne @). Idealne do seryjnego tworzenia opisów (CREATE_OBJECT).\n" +
                "Argumenty: \"Iterable\" (wymień nazwy zmiennych oddzielone przecinkami, np. \"@Srodki, @Dlugosci\"), \"Action\" (nazwa tagu docelowego, np. \"CREATE_OBJECT\"), \"TemplateArgs\" (parametry akcji, używaj $ITEM1, $ITEM2, $INDEX).\n" +
                "Przykład: [ACTION:FOREACH {\"Iterable\": \"@Srodki, @Pola\", \"Action\": \"CREATE_OBJECT\", \"TemplateArgs\": {\"EntityType\": \"DBText\", \"Position\": \"$ITEM1\", \"Text\": \"RPN: 'Pole: ' $ITEM2 2 ROUND CONCAT\", \"Height\": 25}}]\n\n" +

                "Tag: [ACTION:USER_INPUT]\n" +
                "Opis: Prosi użytkownika o wpisanie zwykłego tekstu lub wskazanie punktów na rysunku.\n" +
                "Argumenty: \"Type\": \"String\" (tekst), \"Point\" (jeden punkt) lub \"Points\" (wiele punktów), \"Prompt\" (wiadomość dla użytkownika), \"SaveAs\" (opcjonalna nazwa zmiennej do zapisu w pamięci, bez @).\n" +
                "Przykład: [ACTION:USER_INPUT {\"Type\": \"String\", \"Prompt\": \"Podaj wysokość:\", \"SaveAs\": \"Wys\"}]\n\n" +

                "Tag: [ACTION:READ_PROPERTY]\n" +
                "Opis: Odczytuje pojedynczą właściwość z zaznaczonych obiektów (przydatne do pętli FOREACH). Obsługuje unikalne wirtualne parametry geometryczne, bez względu na typ obiektu!\n" +
                "Obsługiwane uniwersalne właściwości API: \"MidPoint\" (środek linii/polilinii/łuku), \"Length\" (długość krzywej), \"Area\" (powierzchnia zamkniętych figur), \"Volume\", \"Centroid\", \"StartPoint\", \"EndPoint\", \"Center\".\n" +
                "Argumenty: \"Property\" (nazwa właściwości), \"SaveAs\" (nazwa zmiennej do zapisu na liście, bez @).\n" +
                "Przykład: [ACTION:READ_PROPERTY {\"Property\": \"MidPoint\", \"SaveAs\": \"Srodki\"}]\n\n" +

                "Tag: [ACTION:CREATE_OBJECT]\n" +
                "Opis: Rysuje obiekty w przestrzeni rysunku.\n" +
                "Argumenty: \"EntityType\" (obsługiwane: \"Line\", \"Circle\", \"DBText\", \"MText\", \"MLeader\").\n" +
                "WAŻNE: Dla parametrów tekstowych (Text, Position) możesz użyć słowa \"AskUser\", aby program zapytał użytkownika. Możesz też łączyć to ze zwykłym tekstem, np. \"Text\": \"Powierzchnia:\\\\PAskUser\" (wypełniacz tekstu).\n" +
                "Opcjonalne justowanie dla tekstów: dodaj \"MiddleCenter\": \"true\" lub \"BottomCenter\": \"true\".\n" +
                "Opcjonalna rotacja: \"Rotation\": (kąt w radianach lub stopniach zależnie od zapytania użytkownika).\n" +
                "Znacznik nowej linii: w MText i MLeader używaj podwójnego ukośnika: \\\\P (np. \"Góra:\\\\PDół\").\n" +
                " - Dla Line: \"StartPoint\", \"EndPoint\".\n" +
                " - Dla Circle: \"Center\", \"Diameter\".\n" +
                " - Dla DBText/MText: \"Position\", \"Text\", \"Height\".\n" +
                " - Dla MLeader: \"ArrowPoint\", \"LandingPoint\", \"Text\", \"Height\".\n" +
                "Przykład: [ACTION:CREATE_OBJECT {\"EntityType\": \"MLeader\", \"ArrowPoint\": \"AskUser\", \"LandingPoint\": \"AskUser\", \"Text\": \"AskUser\", \"Height\": 25}]\n\n" +

                "Tag: [ACTION:SET_PROPERTIES]\n" +
                "Opis: Uniwersalne narzędzie do zmiany właściwości (Koloru, Warstwy, itp.).\n" +
                "Operator RPN: Zaawansowany kalkulator stosowy. Dostępne operatory: +, -, *, /, ^, SQRT, SIN, COS, ROUND, ABS (wartość bezwzględna), SWAP, DUP, DROP, CONCAT (łączy teksty), REPLACE, SUBSTR, UPPER, LOWER.\n" +
                "Przykład RPN (Odległość od 0 i absolutna powierzchnia): \"RPN: $ITEM2 ABS 2 ROUND CONCAT ' m²' CONCAT\"\n\n" +

                "Tag: [ACTION:MTEXT_FORMAT]\n" +
                "Opis: Zmienia formatowanie MText.\n" +
                "Argumenty: {\"Mode\": \"HighlightWord\"|\"FormatAll\"|\"ClearFormatting\", \"Word\": \"słowo\", \"Color\": nr_koloru (1-255), \"Bold\": true/false}\n\n" +

                "Tag: [ACTION:MTEXT_EDIT]\n" +
                "Opis: Dodaje lub zamienia tekst w MText.\n" +
                "Argumenty: {\"Mode\": \"Append\"|\"Prepend\"|\"Replace\", \"Text\": \"tekst do dodania\", \"FindText\": \"szukany\", \"Color\": nr_koloru, \"Underline\": true/false, \"Bold\": true/false, \"Italic\": true/false}\n\n" +

                "Tag: [ACTION:TEXT_EDIT]\n" +
                "Opis: Dodaje lub zamienia zawartość zwykłego TEXT (DBText).\n" +
                "Argumenty: {\"Mode\": \"Append\"|\"Prepend\"|\"Replace\", \"Text\": \"tekst do dodania\", \"FindText\": \"szukany\"}\n\n" +

                "Tag: [ACTION:ANALYZE]\n" +
                "Opis: Zwraca podsumowanie tego, co obecnie znajduje się w pamięci zaznaczenia. Użyj ZANIM zaczniesz edycję z niepewnymi typami.\n\n" +

                "Tag: [ACTION:READ_SAMPLE]\n" +
                "Opis: Czyta zawartość zaznaczonych tekstów przed edycją (np. Replace).\n\n" +

                "Tag: [ACTION:GET_PROPERTIES_LITE]\n" +
                "Opis: Szybki skan podstawowych właściwości (Kolor, Warstwa, itp.).\n\n" +

                "Tag: [ACTION:GET_PROPERTIES]\n" +
                "Opis: Głęboki skan (Refleksja) wszystkich zaawansowanych parametrów z API CAD.\n\n" +

                "Tag: [ACTION:EDIT_BLOCK]\n" +
                "Opis: Edytuje wnętrza zaznaczonych bloków.\n" +
                "Dostępne klucze (w JSON): \"Color\" (0-255), \"Layer\", \"FilterColor\", \"FindText\", \"ReplaceText\", \"RemoveDimensions\" (true/false).\n\n" +

                "Tag: [ACTION:LIST_BLOCKS]\n" +
                "Opis: Zwraca listę unikalnych nazw bloków.\n" +
                "Argument 'Scope': \"Selection\" lub \"Database\".\n\n" +

                "Tag: [ACTION:MODIFY_GEOMETRY]\n" +
                "Opis: Fizyczna edycja kształtu i położenia (Mode: \"Erase\", \"Move\", \"Copy\", \"Rotate\", \"Scale\").\n" +
                "Argumenty zależne od Mode: \"Vector\", \"BasePoint\", \"Angle\", \"Factor\".\n\n" +

                "Tag: [ACTION:ADD_ANNO_SCALE]\n" +
                "Opis: Dodaje właściwość Annotative z podaną skalą.\n" +
                "Argumenty: [ACTION:ADD_ANNO_SCALE {\"Scale\": \"1:50\"}]\n\n" +

                "Tag: [ACTION:READ_ANNO_SCALES]\n" +
                "Opis: Odczytuje przypisane skale. Tryby: \"Summary\" lub \"Detailed\".\n\n" +

                "Tag: [ACTION:LIST_UNIQUE]\n" +
                "Opis: Zwraca unikalne typy klas lub wartości danej właściwości.\n" +
                "Argumenty: 'Target': \"Class\" lub \"Property\". 'Scope': \"Selection\", \"Model\", \"Blocks\", \"Database\".\n\n" +

                "Tag: [ACTION:USER_CHOICE]\n" +
                "Opis: Wyświetla interaktywną listę jednokrotnego/wielokrotnego wyboru.\n" +
                "Przykład z auto-pobieraniem z rysunku: [ACTION:USER_CHOICE {\"Question\": \"Wybierz warstwę:\", \"FetchTarget\": \"Property\", \"FetchScope\": \"Model\", \"FetchProperty\": \"Layer\", \"SaveAs\": \"WybranaWarstwa\"}]\n\n" +

                "Tag: [ACTION:SEARCH_LAYERS]\n" +
                "Opis: Automatyczna wyszukiwarka warstw (Condition: Contains, StartsWith, EndsWith, Equals). Obsługuje \"SaveAs\" do zapisania w pamięci wyników.\n\n" +

                "Tag: [ACTION:MANAGE_LAYERS]\n" +
                "Opis: Potężne zarządzanie warstwami. Tryby (Mode): \"Create\", \"Modify\", \"Purge\", \"Delete\", \"Merge\".\n" +
                " - Dla Modify/Create: Wymaga \"Layer\" (nazwa warstwy). Opcjonalne: \"NewName\" (wspiera RPN!), \"Color\", \"LineWeight\", \"Linetype\", \"IsOff\", \"IsFrozen\", \"IsLocked\", \"Transparency\".\n" +
                " - Dla Delete: Wymaga podania listy \"SourceLayers\": [\"W1\", \"W2\"].\n" +
                " - Dla Merge: Wymaga \"SourceLayers\" oraz docelowej warstwy \"TargetLayer\".\n" +
                "Przykład modyfikacji: [ACTION:MANAGE_LAYERS {\"Mode\": \"Modify\", \"Layer\": \"_HCR\", \"NewName\": \"STARE__HCR\", \"Color\": 1}]\n\n" +
                "Tag: [ACTION:CREATE_BLOCK]\n" +
                "Opis: Tworzy nowy blok z zaznaczonych obiektów.\n" +
                "Argumenty: \"Name\" (nazwa), \"BasePoint\". Możesz użyć \"AskUser\".\n\n" +

                "Tag: [ACTION:INSERT_BLOCK]\n" +
                "Opis: Wstawia fizycznie blok na rysunek.\n" +
                "Argumenty: \"Name\", \"Position\". Opcjonalnie: \"Scale\", \"Rotation\", \"Layer\", \"SelectObject\".\n\n" +

                "--- PRZYKŁADOWE ROZMOWY (ZASADA DZIAŁANIA): ---\n" +
                "User: Wyrzuć z zaznaczenia teksty wyższe niż 10\n" +
                "Bielik: [SELECT: {\"Mode\": \"Remove\", \"Scope\": \"Model\", \"EntityType\": \"DBText\", \"Conditions\": [{\"Property\": \"Height\", \"Operator\": \">\", \"Value\": 10}]}]\n\n" +

                "User: Zaznacz linie, które nie zaczynają się w (0,0,0)\n" +
                "Bielik: [SELECT: {\"Mode\": \"New\", \"Scope\": \"Model\", \"EntityType\": \"Line\", \"Conditions\": [{\"Property\": \"StartPoint\", \"Operator\": \"!=\", \"Value\": \"(0,0,0)\"}]}]\n\n" +

                "User: Zmień słowo PVC na czerwone w zaznaczonych tekstach\n" +
                "Bielik: [ACTION:MTEXT_FORMAT {\"Mode\": \"HighlightWord\", \"Word\": \"PVC\", \"Color\": 1, \"Bold\": false}]\n\n" +

                "--- KRYTYCZNE ZASADY BEZPIECZEŃSTWA: ---\n" +
                "1. ZAKAZ ZMYŚLANIA ZAZNACZEŃ! ZAWSZE użyj [SELECT: ...], zanim cokolwiek edytujesz lub odczytasz za pomocą ACTION.\n" +
                "2. ZAKAZ RYSOWANIA, GDY UŻYTKOWNIK CHCE ZAZNACZYĆ! Słowa 'dodaj do zaznaczenia' (add to selection) to komenda [SELECT: ... {\"Mode\": \"Add\"}].\n" +
                "3. Komentuj swoje intencje! Dodawaj opcjonalny parametr \"Comment\": \"Twój komentarz\" do każdego JSONa w tagach SELECT i ACTION, by wyjaśnić swój proces myślowy.\n\n" +
                "ZROZUMIANO. BĘDĘ ODPOWIADAŁ TYLKO TAGAMI.";


        [CommandMethod("AGENT_UI")]
        public void UruchomInterfejsAgenta()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;

            // PODSŁUCH ZAZNACZENIA: Działa w tle i zapisuje wszystko co klikniesz!
            // PODSŁUCH ZAZNACZENIA (MULTI-DOCUMENT): Działa w tle na każdym rysunku!
            if (!isSelectionHooked)
            {
                System.EventHandler podsluch = (s, e) =>
                {
                    Document aktualnyDoc = Application.DocumentManager.MdiActiveDocument;
                    if (aktualnyDoc == null) return;
                    PromptSelectionResult res = aktualnyDoc.Editor.SelectImplied();
                    if (res.Status == PromptStatus.OK && res.Value != null && res.Value.Count > 0)
                    {
                        AktywneZaznaczenie = res.Value.GetObjectIds();
                    }
                };

                // 1. Podpinamy pod wszystkie ZAKTUALNIE otwarte rysunki
                foreach (Document d in Application.DocumentManager)
                {
                    d.ImpliedSelectionChanged += podsluch;
                }

                // 2. Podpinamy pod każdy NOWY rysunek, który utworzysz/otworzysz w przyszłości
                Application.DocumentManager.DocumentCreated += (s, e) =>
                {
                    e.Document.ImpliedSelectionChanged += podsluch;
                };

                isSelectionHooked = true;
            }

            if (oknoAgenta == null)
            {
                // Tworzymy główne okno dokowalne
                oknoAgenta = new PaletteSet("Agent Bielik AI");
                oknoAgenta.Style = PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu | PaletteSetStyles.ShowAutoHideButton;
                oknoAgenta.MinimumSize = new System.Drawing.Size(300, 500);

                // Tworzymy nasz interfejs z przyciskami (ten z pliku AgentControl.cs)
                interfejsAgenta = new Bricscad_AgentAI.AgentControl();

                // Dodajemy interfejs do palety
                oknoAgenta.Add("Czat z AI", interfejsAgenta);

                // --- NOWA ZAKŁADKA TESTOWANIA MODELI ---
                AgentTesterControl testerAgenta = new AgentTesterControl();
                oknoAgenta.Add("Benchmarking LLM", testerAgenta);
            }

            // Pokazujemy paletę na ekranie
            oknoAgenta.Visible = true;

            // Wczytujemy bazy, jeśli są puste
            if (bazaQuick.Count == 0 && bazaFull.Count == 0) WczytajBazyWiedzy(doc.Editor);
        }

        // Nowy asynchroniczny "mózg" Agenta
        // Nowy asynchroniczny "mózg" Agenta
        // =========================================================================================
        // NOWY ASYNCHRONICZNY MÓZG AGENTA (DLA OKIENKA)
        // =========================================================================================
        public static async Task<string> ZapytajAgentaAsync(string userMsg, Document doc, ObjectId[] przechwyconeZaznaczenie = null, int licznikNapraw = 0)
        {
            if (historiaRozmowy.Count == 0 || !historiaRozmowy[0].Contains("system"))
            {
                // Odczytujemy jednostkę w tle
                short insunits = Convert.ToInt16(Application.GetSystemVariable("INSUNITS"));
                string unitName = insunits == 4 ? "Milimetry (mm)" : insunits == 5 ? "Centymetry (cm)" : insunits == 6 ? "Metry (m)" : "Inne";

                // Doklejamy informację do promptu
                string zaktualizowanyPrompt = systemPrompt + $"\n\n[INFO SYSTEMOWE]: Aktualne jednostki otwartego rysunku to: {unitName}. ZAWSZE przeliczaj wymiary podane przez użytkownika na te jednostki przed wygenerowaniem tagu SELECT.";

                // 1. Dodajemy główne zasady gry (System Prompt)
                historiaRozmowy.Insert(0, "{\"role\": \"system\", \"content\": \"" + Komendy.SafeJson(zaktualizowanyPrompt) + "\"}");

                // 2. ŁADUJEMY PRZYKŁADY Z PLIKU DYSKOWEGO DO PAMIĘCI
                WczytajPrzykladyTreningowe();
            }

            // Zapobiega dodawaniu pustych wiadomości przy automatycznej rekurencji (chaining)
            if (!string.IsNullOrEmpty(userMsg))
            {
                // --- POPRAWKA 1: CONTEXT INJECTION ---
                // Doklejamy przypomnienie, by Agent trzymał się roli, niezależnie od tego o co pyta użytkownik.
                string przypomnienie = "\\n\\n[SYSTEM]: Pamiętaj, jesteś Agentem BricsCAD. ZAWSZE odpowiadaj używając tagów (np. [MSG: ...], [SELECT: ...], [ACTION: ...]). NIGDY nie używaj czystego tekstu.";

                historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + Komendy.SafeJson(userMsg + przypomnienie) + "\"}");
            }

            try
            {
                string jsonBody = "{\"model\": \"" + wybranyModel + "\", \"messages\": [" + string.Join(",", historiaRozmowy) + "], \"temperature\": 0.1, \"stream\": false}";
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("http://127.0.0.1:1234/v1/chat/completions", content);
                string aiMsg = new Komendy().WyciagnijContentZJson(await response.Content.ReadAsStringAsync());

                if (aiMsg.Contains("</think>")) aiMsg = aiMsg.Substring(aiMsg.LastIndexOf("</think>") + 8).Trim();

                if (!string.IsNullOrEmpty(aiMsg))
                {
                    // --- POPRAWKA 2: BLOKADA ZATRUWANIA KONTEKSTU ---
                    // Sprawdzamy czy w wypowiedzi modelu jest jakikolwiek znany tag
                    bool maTag = aiMsg.Contains("[ACTION:") || aiMsg.Contains("[SELECT:") || aiMsg.Contains("[MSG:") ||
                                 aiMsg.Contains("[SEARCH:") || aiMsg.Contains("[LISP:") || aiMsg.Contains("[ANALYZE") ||
                                 aiMsg.Contains("[READ_SAMPLE");

                    if (!maTag)
                    {
                        // Jeśli model napisał zwykły tekst bez tagu:
                        if (licznikNapraw >= 2)
                        {
                            doc.Editor.WriteMessage($"\n[Tarcza AI]: Model odmawia współpracy w formacie tagów.");
                            return $"[BŁĄD KRYTYCZNY] Agent przestał używać tagów i zaczął pisać zwykłym tekstem:\n{aiMsg}";
                        }

                        doc.Editor.WriteMessage($"\n[Tarcza AI]: Zablokowano odpowiedź bez tagu (Ochrona Persony). Wymuszam poprawę...");

                        string reprymenda = "[SYSTEM] BŁĄD KRYTYCZNY: Twoja odpowiedź nie zawierała żadnego tagu! Złamałeś zasady Systemu. Musisz użyć [MSG: twój tekst] do komunikacji ze mną lub [ACTION: ...]/[SELECT: ...] do pracy w CAD. Wygeneruj odpowiedź ponownie, trzymając się rygorystycznie formatu.";

                        // Dodajemy reprymendę do historii (ale NIE dodajemy błędnej wypowiedzi modelu!)
                        historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + Komendy.SafeJson(reprymenda) + "\"}");

                        // Wywołujemy rekurencję z podbitym licznikiem napraw
                        return await ZapytajAgentaAsync("", doc, przechwyconeZaznaczenie, licznikNapraw + 1);
                    }

                    // Jeśli odpowiedź MA poprawny tag, DOPIERO TERAZ dodajemy ją na stałe do historii rozmowy!
                    historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + Komendy.SafeJson(aiMsg) + "\"}");

                    // =======================================================================
                    // --- 🛡️ NIEWIDZIALNA TARCZA: SPRAWDZAMY SKŁADNIĘ ZANIM DOTKNIEMY CADA ---
                    // =======================================================================
                    if (aiMsg.Contains("[ACTION:") || aiMsg.Contains("[SELECT:"))
                    {
                        List<string> syntaxErrors = TagValidator.ValidateSequence(aiMsg);
                        if (syntaxErrors.Count > 0)
                        {
                            // BEZPIECZNIK: Przerywamy po 2 nieudanych próbach naprawy tego samego tagu
                            if (licznikNapraw >= 2)
                            {
                                doc.Editor.WriteMessage($"\n[Tarcza AI]: Przerwano samonaprawę (zbyt wiele nieudanych prób).");
                                return $"[BŁĄD KRYTYCZNY] Agent nie potrafił wygenerować poprawnego tagu po 3 próbach.\nOstatni wadliwy tag: {aiMsg}";
                            }

                            doc.Editor.WriteMessage($"\n[Tarcza AI]: Zablokowano wadliwy tag. Wymuszam samonaprawę w tle (Próba {licznikNapraw + 1})...");

                            string systemFeedback = $"[SYSTEM] Twój wygenerowany tag zawiera błędy. Nie został wykonany:\n" +
                                                    $"{string.Join("\n", syntaxErrors)}\n" +
                                                    $"Przeanalizuj swój błąd, upewnij się, że używasz właściwych nazw parametrów i wygeneruj CAŁKOWICIE NOWY, poprawny tag.";

                            historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + Komendy.SafeJson(systemFeedback) + "\"}");

                            // Wymuszamy samonaprawę, zwiększając licznik!
                            return await ZapytajAgentaAsync("", doc, przechwyconeZaznaczenie, licznikNapraw + 1);
                        }
                        // Jeśli tag nie miał błędów, strzelamy nim natychmiast do zakładki Trening!
                        OnTagGenerated?.Invoke(aiMsg);
                    }
                    // =======================================================================

                    // 1. OBSŁUGA WYSZUKIWANIA W BAZIE (SEARCH)
                    if (aiMsg.Contains("[SEARCH:"))
                    {
                        string szukanaKlasa = aiMsg.Substring(aiMsg.IndexOf("[SEARCH:") + 8).Split(']')[0].Trim().ToLower();
                        string wynik = "";

                        // Zbieramy wiedzę z obu baz naraz!
                        if (bazaQuick.ContainsKey(szukanaKlasa))
                            wynik += "[QUICK INFO]: " + bazaQuick[szukanaKlasa] + "\n";

                        if (bazaFull.ContainsKey(szukanaKlasa))
                            wynik += "[FULL API]: " + bazaFull[szukanaKlasa];

                        if (string.IsNullOrEmpty(wynik))
                            wynik = "Brak definicji dla tej klasy.";

                        string odpowiedzSystemu = $"[DOKUMENTACJA]: {wynik}\n\n[SYSTEM]: Otrzymałeś dokumentację. Kontynuuj zadanie używając tagu [SELECT: ] lub odpowiedz użytkownikowi [MSG: ].";
                        historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + Komendy.SafeJson(odpowiedzSystemu) + "\"}");

                        return await ZapytajAgentaAsync("", doc, przechwyconeZaznaczenie);
                    }

                    // Blokada dokumentu dla operacji modyfikujących CAD
                    using (DocumentLock docLock = doc.LockDocument())
                    {
                        if (przechwyconeZaznaczenie != null && przechwyconeZaznaczenie.Length > 0)
                        {
                            doc.Editor.SetImpliedSelection(przechwyconeZaznaczenie);
                        }

                        // 2. OBSŁUGA ZAZNACZANIA (SELECT)
                        if (aiMsg.Contains("[SELECT:"))
                        {
                            int start = aiMsg.IndexOf("{", aiMsg.IndexOf("[SELECT:"));
                            int end = aiMsg.LastIndexOf("}");
                            if (start != -1 && end > start)
                            {
                                int zaznaczoneLiczba = Komendy.WykonajInteligentneZaznaczenie(doc, aiMsg.Substring(start, end - start + 1));

                                string sysOdp = $"[SYSTEM]: Pomyślnie zaznaczono {zaznaczoneLiczba} obiekt(ów). Jeśli masz wykonać akcję na zaznaczeniu użyj [ACTION: ], w przeciwnym razie opisz wynik za pomocą [MSG: ].";
                                historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + Komendy.SafeJson(sysOdp) + "\"}");

                                return await ZapytajAgentaAsync("", doc, AktywneZaznaczenie);
                            }
                        }
                        // 3. OBSŁUGA NARZĘDZI (ACTION) ORAZ (ANALYZE / READ_SAMPLE)
                        // ZMIANA: Usunięto dwukropki, aby łapało czyste [ANALYZE] i [READ_SAMPLE]
                        else if (aiMsg.Contains("[ACTION:") || aiMsg.Contains("[ANALYZE") || aiMsg.Contains("[READ_SAMPLE"))
                        {
                            // --- PRZERWANIE W TRYBIE TESTOWYM (BENCHMARKING) ---
                            // Blokujemy WYŁĄCZNIE narzędzia, które proszą użytkownika o kliknięcie/wpisanie w CAD, 
                            // aby nie zawiesić interfejsu testowego. Pozostałe akcje (rysowanie, edycja) zostaną wykonane!
                            if (TrybTestowy && (aiMsg.Contains("USER_INPUT") || aiMsg.Contains("USER_CHOICE")))
                            {
                                return aiMsg;
                            }

                            foreach (var tool in new Komendy().tools)
                            {
                                // Pozbywamy się nawiasu zamykającego i dwukropka do celów szukania
                                string pelnySzukany = tool.ActionTag.Replace("]", ""); // np. "[ACTION:ANALYZE"
                                string krotkiSzukany = tool.ActionTag.Replace("[ACTION:", "[").Replace("]", ""); // np. "[ANALYZE"

                                // Sprawdzamy czy wygenerowany tekst zawiera fragment szukanego narzędzia
                                if (aiMsg.Contains(pelnySzukany) || aiMsg.Contains(krotkiSzukany))
                                {
                                    string wynikNarzedzia = "";

                                    // --- ZABEZPIECZENIE: WYCIĄGAMY TYLKO CZYSTY JSON Z TAGU ---
                                    string args = "";
                                    int startArgs = -1;

                                    if (aiMsg.Contains(pelnySzukany))
                                        startArgs = aiMsg.IndexOf("{", aiMsg.IndexOf(pelnySzukany));
                                    else if (aiMsg.Contains(krotkiSzukany))
                                        startArgs = aiMsg.IndexOf("{", aiMsg.IndexOf(krotkiSzukany));

                                    int endArgs = aiMsg.LastIndexOf("}");

                                    if (startArgs != -1 && endArgs > startArgs)
                                    {
                                        args = aiMsg.Substring(startArgs, endArgs - startArgs + 1);
                                    }

                                    // Odpalamy narzędzie przez Refleksję, ale tym razem dajemy mu tylko parametry (args)
                                    var methodInfo = tool.GetType().GetMethod("Execute", new Type[] { typeof(Document), typeof(string) });
                                    if (methodInfo != null)
                                    {
                                        wynikNarzedzia = (string)methodInfo.Invoke(tool, new object[] { doc, args });
                                    }
                                    else
                                    {
                                        wynikNarzedzia = tool.Execute(doc);
                                    }
                                    {
                                        historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + Komendy.SafeJson($"Oto dane z narzędzia:\n{wynikNarzedzia}\n\nKontynuuj zadanie. UŻYJ TAGU [MSG: twoja odpowiedź].") + "\"}");
                                        return await ZapytajAgentaAsync("", doc, przechwyconeZaznaczenie);
                                    }

                                    //return $"[Wykonano narzędzie {tool.ActionTag}]\n{aiMsg}";
                                }
                            }
                        }
                        // 4. OBSŁUGA SKRYPTÓW LISP
                        else if (aiMsg.Contains("[LISP:"))
                        {
                            int start = aiMsg.IndexOf("[LISP:") + 6;
                            int end = aiMsg.IndexOf("]", start);
                            if (end > start)
                            {
                                string lisp = aiMsg.Substring(start, end - start).Trim().Replace("`", "");
                                doc.SendStringToExecute(lisp + "\n", true, false, false);
                            }
                        }
                    }

                    return aiMsg;
                }
            }
            catch (System.Exception ex)
            {
                return "BŁĄD KOMUNIKACJI: " + ex.Message;
            }

            return "Agent nic nie odpowiedział.";
        }


        // --- PANCERNY ENKODER JSON ---
        public static string SafeJson(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in text)
            {
                switch (c)
                {
                    case '\"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // --- ŚCIEŻKI I PAMIĘĆ ---
        public static string GetGlobalPath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BricsCAD_Agent");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "global_memory.json");
        }

        public static string GetLocalPath(Document doc)
        {
            string docPath = doc.Database.Filename;
            if (string.IsNullOrEmpty(docPath)) return null;
            string dir = Path.GetDirectoryName(docPath);
            string nameNoExt = Path.GetFileNameWithoutExtension(docPath);
            return Path.Combine(dir, nameNoExt + "_ai_memory.json");
        }

        // --- ŁADOWANIE BAZ WIEDZY (Poprawione) ---
        private void WczytajBazyWiedzy(Editor ed)
        {
            bazaQuick.Clear();
            bazaFull.Clear();

            // --- NOWY SPOSÓB: Ścieżka do folderu z wtyczką (DLL) ---
            string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string folder = System.IO.Path.GetDirectoryName(assemblyPath);

            string pathQuick = System.IO.Path.Combine(folder, "BricsCAD_API_Quick.txt");
            string pathFull = System.IO.Path.Combine(folder, "BricsCAD_API_V22.txt");

            int LoadFile(string path, Dictionary<string, string> dict)
            {
                if (!File.Exists(path)) return 0;
                try
                {
                    string content = File.ReadAllText(path, Encoding.UTF8);
                    var matches = Regex.Matches(content, @"\b([a-zA-Z0-9_]+)\|(.*?)(?=\b[a-zA-Z0-9_]+\||$)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    // BIAŁA LISTA najpopularniejszych klas graficznych chroniąca Agenta przed przeładowaniem
                    string[] bLista = { "Entity", "Line", "Polyline", "Polyline2d", "Polyline3d", "Arc", "Circle", "Ellipse", "Spline", "DBPoint", "Xline", "Ray", "Hatch", "Region", "Wipeout", "MText", "DBText", "Dimension", "AlignedDimension", "RotatedDimension", "RadialDimension", "DiametricDimension", "ArcDimension", "LineAngularDimension2", "Point3AngularDimension", "Leader", "MLeader", "Solid3d", "Surface", "Solid", "Trace", "PolyFaceMesh", "PolygonMesh", "Face", "BlockReference" };
                    var hsBialaLista = new HashSet<string>(bLista, StringComparer.OrdinalIgnoreCase);

                    foreach (Match m in matches)
                    {
                        string klucz = m.Groups[1].Value.Trim().ToLower();
                        if (klucz.Contains(" ") || klucz.Length > 35) continue;

                        if (hsBialaLista.Contains(klucz))
                        {
                            string wartosc = m.Groups[2].Value.Trim();
                            dict[klucz] = wartosc;
                        }
                    }
                    return dict.Count;
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage($"\n[Błąd]: {Path.GetFileName(path)}: {ex.Message}");
                    return 0;
                }
            }

            int qCount = LoadFile(pathQuick, bazaQuick);
            int fCount = LoadFile(pathFull, bazaFull);
            ed.WriteMessage($"\n[System]: Załadowano bazy API (Quick: {qCount}, Full: {fCount})");
        }

        private void ZapiszPamiec(Document doc)
        {
            try
            {
                string data = string.Join(Environment.NewLine, historiaRozmowy);
                File.WriteAllText(GetGlobalPath(), data);
                string lp = GetLocalPath(doc);
                if (lp != null) File.WriteAllText(lp, data);
            }
            catch { }
        }

        private void WczytajPamiec(Document doc)
        {
            try
            {
                string gp = GetGlobalPath(); string lp = GetLocalPath(doc);
                string p = (lp != null && File.Exists(lp)) ? lp : gp;
                if (File.Exists(p)) historiaRozmowy.AddRange(File.ReadAllLines(p));
            }
            catch { }
        }

        public static void ResetujPamiec(Document doc)
        {
            historiaRozmowy.Clear();
            if (File.Exists(GetGlobalPath())) File.Delete(GetGlobalPath());
            string lp = GetLocalPath(doc);
            if (lp != null && File.Exists(lp)) File.Delete(lp);
            doc.Editor.WriteMessage("\n[System]: Pamięć wyczyszczona.");
        }

        // --- LM STUDIO MODELS ---
        private List<string> ParsujListeModeli(string json)
        {
            List<string> l = new List<string>();
            int p = 0;
            while ((p = json.IndexOf("\"id\":", p)) != -1)
            {
                int s = json.IndexOf("\"", p + 5) + 1;
                int e = json.IndexOf("\"", s);
                l.Add(json.Substring(s, e - s)); p = e;
            }
            return l;
        }

        private string WyciagnijContentZJson(string json)
        {
            try
            {
                int idx = json.IndexOf("\"content\":");
                if (idx == -1) return "";
                int start = json.IndexOf("\"", idx + 10) + 1;
                StringBuilder sb = new StringBuilder();
                bool escaped = false;
                for (int i = start; i < json.Length; i++)
                {
                    if (escaped)
                    {
                        if (json[i] == 'n') sb.Append('\n');
                        else if (json[i] == '\"') sb.Append('\"');
                        else if (json[i] == '\\') sb.Append('\\');
                        escaped = false;
                    }
                    else if (json[i] == '\\') escaped = true;
                    else if (json[i] == '\"') break;
                    else sb.Append(json[i]);
                }
                return sb.ToString();
            }
            catch { return "Błąd parsowania odpowiedzi."; }
        }

        [CommandMethod("AGENT_MACROS")]
        public void PokazMakraUzytkownika()
        {
            UruchomInterfejsAgenta(); // Wywoła paletę
                                      // Przełączamy od razu na zakładkę "Moje Makra" (indeks 1, jeśli "Czat z AI" to 0)
            oknoAgenta.Activate(1);
        }

        [CommandMethod("AGENT_SAVE_MACRO")]
        public void ZapiszTagJakoMakro()
        {
            Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // 1. Zamiast prosić w pasku poleceń, używamy okienka WinForms odpornego na znaki ENTER (Nowe linie)
            string resTag = PokazOkienkoWprowadzania("Wklej cały wygenerowany tag lub sekwencję (np. [SELECT:...]):", "Wprowadź kod Makra", true);
            if (string.IsNullOrWhiteSpace(resTag)) return;

            string resName = PokazOkienkoWprowadzania("Podaj przyjazną nazwę, pod którą makro pojawi się na liście:", "Nazwa Makra", false);
            if (string.IsNullOrWhiteSpace(resName)) return;

            // 2. Zapytanie w CADzie tylko o cel zapisu (proste kliknięcie)
            PromptKeywordOptions pko = new PromptKeywordOptions("\nGdzie zapisać to makro? [Globalnie/Rysunek]: ");
            pko.Keywords.Add("Globalnie");
            pko.Keywords.Add("Rysunek");
            pko.Keywords.Default = "Globalnie";

            var resScope = ed.GetKeywords(pko);
            if (resScope.Status != PromptStatus.OK) return;

            string path = "";
            if (resScope.StringResult == "Globalnie")
            {
                path = MacroManager.GlobalMacrosPath;
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            }
            else
            {
                path = MacroManager.GetLocalMacrosPath(doc);
                if (string.IsNullOrEmpty(path))
                {
                    ed.WriteMessage("\nBŁĄD: Musisz najpierw zapisać ten rysunek (DWG) na dysku, aby używać makr lokalnych!");
                    return;
                }
            }

            MacroManager.SaveMacro(path, resName, resTag);
            ed.WriteMessage($"\n[SUKCES] Makro '{resName}' zapisane w: {System.IO.Path.GetFileName(path)}!");
        }

        // Metoda pomocnicza dla Agenta - Bezpieczne wprowadzanie danych
        private string PokazOkienkoWprowadzania(string zacheta, string tytul, bool wielolinijkowe)
        {
            using (System.Windows.Forms.Form form = new System.Windows.Forms.Form())
            {
                form.Text = tytul;
                form.Width = 500;
                form.Height = wielolinijkowe ? 300 : 150;
                form.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                form.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                System.Windows.Forms.Label lbl = new System.Windows.Forms.Label() { Left = 10, Top = 10, Width = 460, Text = zacheta };
                System.Windows.Forms.TextBox txt = new System.Windows.Forms.TextBox() { Left = 10, Top = 35, Width = 460 };

                if (wielolinijkowe)
                {
                    txt.Multiline = true;
                    txt.Height = 170;
                    txt.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
                }

                System.Windows.Forms.Button btnOk = new System.Windows.Forms.Button() { Text = "Zatwierdź", Left = 300, Width = 85, DialogResult = System.Windows.Forms.DialogResult.OK };
                btnOk.Top = wielolinijkowe ? 220 : 70;
                System.Windows.Forms.Button btnCancel = new System.Windows.Forms.Button() { Text = "Anuluj", Left = 390, Width = 80, DialogResult = System.Windows.Forms.DialogResult.Cancel };
                btnCancel.Top = btnOk.Top;

                form.Controls.Add(lbl);
                form.Controls.Add(txt);
                form.Controls.Add(btnOk);
                form.Controls.Add(btnCancel);
                form.AcceptButton = wielolinijkowe ? null : btnOk;
                form.CancelButton = btnCancel;

                if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK) return txt.Text;
                return null;
            }
        }

        [CommandMethod("AGENT_MODELS")]
        public void WybierzModela()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            try
            {
                var response = client.GetAsync("http://127.0.0.1:1234/v1/models").Result;
                List<string> modele = ParsujListeModeli(response.Content.ReadAsStringAsync().Result);
                if (modele.Count == 0) return;
                PromptKeywordOptions pko = new PromptKeywordOptions("\nWybierz model AI:");
                foreach (string m in modele) pko.Keywords.Add(m);
                PromptResult pr = ed.GetKeywords(pko);
                if (pr.Status == PromptStatus.OK)
                {
                    wybranyModel = pr.StringResult;
                    ed.WriteMessage($"\n[System]: Aktywny model: {wybranyModel}");
                }
            }
            catch { ed.WriteMessage("\n[Błąd]: Brak połączenia z LM Studio."); }
        }

        // --- SILNIK ZAZNACZANIA ---
        public static int WykonajInteligentneZaznaczenie(Document doc, string json)
        {
            Editor ed = doc.Editor;
            try
            {
                string entityTypeStr = Regex.Match(json, @"\""EntityType\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;

                string trybStr = "New";
                Match trybMatch = Regex.Match(json, @"\""Mode\""\s*:\s*\""([^\""]+)\""");
                if (trybMatch.Success) trybStr = trybMatch.Groups[1].Value;

                string scopeStr = "Model";
                Match scopeMatch = Regex.Match(json, @"\""Scope\""\s*:\s*\""([^\""]+)\""");
                if (scopeMatch.Success) scopeStr = scopeMatch.Groups[1].Value;

                var warunki = new List<(string Prop, string Op, string Val)>();

                // PANCERNY REGEX: Oddziela wartości w cudzysłowach od wartości bez cudzysłowów (liczbowych i logicznych)
                MatchCollection matches = Regex.Matches(json, @"\""Property\""\s*:\s*\""([^\""]+)\"".*?\""Operator\""\s*:\s*\""([^\""]+)\"".*?\""Value\""\s*:\s*(?:\""([^\""]*)\""|([^\""\s,}]+))", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match m in matches)
                {
                    string prop = m.Groups[1].Value;
                    string op = m.Groups[2].Value;

                    // Łapiemy cokolwiek regex znalazł
                    string val = !string.IsNullOrEmpty(m.Groups[3].Value) ? m.Groups[3].Value : m.Groups[4].Value;

                    // CZYSTKA TOTALNA: Usuwamy z nazwy wszelkie backslashe (\) oraz same znaki cudzysłowów (") !
                    val = val.Replace("\\", "").Replace("\"", "").Trim();

                    warunki.Add((prop, op, val));
                }

                if (string.IsNullOrEmpty(entityTypeStr)) return 0;

                if (entityTypeStr.Equals("Clear", StringComparison.OrdinalIgnoreCase))
                {
                    ed.SetImpliedSelection(new ObjectId[0]);
                    AktywneZaznaczenie = new ObjectId[0];
                    ed.WriteMessage("\n[System]: Odznaczono obiekty.");
                    return 0;
                }

                string[] typyDoSzukania = entityTypeStr.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                ed.WriteMessage($"\n[System]: Szukam '{string.Join("/", typyDoSzukania)}' (Tryb: {trybStr}, Obszar: {scopeStr}, Warunki: {warunki.Count})...");

                // --- WYPISZ DEBUGOWANIE WARUNKÓW DLA UŻYTKOWNIKA ---
                foreach (var w in warunki)
                {
                    ed.WriteMessage($"\n  -> Zrozumiano warunek: [{w.Prop}] {w.Op} '{w.Val}'");
                }
                // ---------------------------------------------------

                List<ObjectId> znalezioneObiekty = new List<ObjectId>();
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    List<ObjectId> blokiDoPrzeszukania = new List<ObjectId>();

                    if (scopeStr.Equals("Blocks", StringComparison.OrdinalIgnoreCase) && AktywneZaznaczenie != null && AktywneZaznaczenie.Length > 0)
                    {
                        foreach (ObjectId id in AktywneZaznaczenie)
                        {
                            Entity zaznaczonyEnt = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (zaznaczonyEnt is BlockReference br && !blokiDoPrzeszukania.Contains(br.BlockTableRecord))
                                blokiDoPrzeszukania.Add(br.BlockTableRecord);
                        }

                        for (int i = 0; i < blokiDoPrzeszukania.Count; i++)
                        {
                            BlockTableRecord wewnetrznyBtr = (BlockTableRecord)tr.GetObject(blokiDoPrzeszukania[i], OpenMode.ForRead);
                            foreach (ObjectId wewnetrzneId in wewnetrznyBtr)
                            {
                                Entity wewnEnt = tr.GetObject(wewnetrzneId, OpenMode.ForRead) as Entity;
                                if (wewnEnt is BlockReference zagniezdzonyBr && !blokiDoPrzeszukania.Contains(zagniezdzonyBr.BlockTableRecord))
                                    blokiDoPrzeszukania.Add(zagniezdzonyBr.BlockTableRecord);
                            }
                        }

                        if (blokiDoPrzeszukania.Count == 0)
                            ed.WriteMessage("\n[Uwaga]: Brak zaznaczonych bloków, by szukać wewnątrz nich.");
                    }
                    else
                    {
                        blokiDoPrzeszukania.Add(doc.Database.CurrentSpaceId);
                    }

                    foreach (ObjectId spaceId in blokiDoPrzeszukania)
                    {
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);

                        foreach (ObjectId objId in btr)
                        {
                            Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;

                            string nazwaTypuEnt = ent.GetType().Name;
                            bool typPasuje = false;

                            foreach (var t in typyDoSzukania)
                            {
                                string szukanyTyp = t.Trim();

                                if (szukanyTyp.Equals("Entity", StringComparison.OrdinalIgnoreCase)) { typPasuje = true; break; }
                                if (szukanyTyp.Equals("Text", StringComparison.OrdinalIgnoreCase) && ent is DBText) { typPasuje = true; break; }
                                if (szukanyTyp.Equals("Dimension", StringComparison.OrdinalIgnoreCase) && ent is Dimension) { typPasuje = true; break; }

                                // --- NOWOŚĆ: OBSŁUGA WILDCARDÓW (gwiazdki) np. *Line zaznaczy Line i Polyline ---
                                if (szukanyTyp.StartsWith("*") && nazwaTypuEnt.EndsWith(szukanyTyp.Substring(1), StringComparison.OrdinalIgnoreCase)) { typPasuje = true; break; }

                                if (nazwaTypuEnt.Equals(szukanyTyp, StringComparison.OrdinalIgnoreCase)) { typPasuje = true; break; }
                            }
                            if (!typPasuje) continue;

                            bool spelniaWszystkie = true;
                            foreach (var warunek in warunki)
                            {
                                string rzeczywistaWlasciwosc = warunek.Prop;
                                bool sprawdzajWizualnie = false;

                                if (rzeczywistaWlasciwosc.Equals("VisualColor", StringComparison.OrdinalIgnoreCase))
                                { rzeczywistaWlasciwosc = "ColorIndex"; sprawdzajWizualnie = true; }
                                else if (rzeczywistaWlasciwosc.Equals("VisualLinetype", StringComparison.OrdinalIgnoreCase))
                                { rzeczywistaWlasciwosc = "Linetype"; sprawdzajWizualnie = true; }
                                else if (rzeczywistaWlasciwosc.Equals("VisualLineWeight", StringComparison.OrdinalIgnoreCase))
                                { rzeczywistaWlasciwosc = "LineWeight"; sprawdzajWizualnie = true; }
                                else if (rzeczywistaWlasciwosc.Equals("VisualTransparency", StringComparison.OrdinalIgnoreCase))
                                { rzeczywistaWlasciwosc = "Transparency"; sprawdzajWizualnie = true; }
                                else if (rzeczywistaWlasciwosc.Equals("Color", StringComparison.OrdinalIgnoreCase))
                                { rzeczywistaWlasciwosc = "ColorIndex"; }
                                else if (ent is MText && rzeczywistaWlasciwosc.Equals("Height", StringComparison.OrdinalIgnoreCase)) rzeczywistaWlasciwosc = "TextHeight";
                                else if (ent is Dimension && (rzeczywistaWlasciwosc.Equals("Height", StringComparison.OrdinalIgnoreCase) || rzeczywistaWlasciwosc.Equals("TextHeight", StringComparison.OrdinalIgnoreCase))) rzeczywistaWlasciwosc = "Dimtxt";

                                string[] zagniezdzenia = rzeczywistaWlasciwosc.Split('.');
                                object wartoscObiektu = ent;
                                System.Reflection.PropertyInfo propInfo = null;

                                foreach (string czesc in zagniezdzenia)
                                {
                                    if (wartoscObiektu == null) break;
                                    propInfo = wartoscObiektu.GetType().GetProperty(czesc, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                                    if (propInfo != null)
                                    {
                                        wartoscObiektu = propInfo.GetValue(wartoscObiektu);
                                    }
                                    else
                                    {
                                        wartoscObiektu = null;
                                        break;
                                    }
                                }

                                if (wartoscObiektu == null) { spelniaWszystkie = false; break; }

                                string valStr = wartoscObiektu.ToString();

                                if (wartoscObiektu is Teigha.Colors.Transparency transp)
                                {
                                    if (transp.IsByAlpha) valStr = Math.Round((255.0 - transp.Alpha) / 255.0 * 100.0).ToString();
                                    else if (sprawdzajWizualnie && transp.IsByLayer)
                                    {
                                        try
                                        {
                                            LayerTableRecord ltr = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord;
                                            if (ltr != null && ltr.Transparency.IsByAlpha) valStr = Math.Round((255.0 - ltr.Transparency.Alpha) / 255.0 * 100.0).ToString();
                                            else valStr = "0";
                                        }
                                        catch { valStr = "0"; }
                                    }
                                    else if (!sprawdzajWizualnie && transp.IsByLayer) valStr = "ByLayer";
                                    else if (!sprawdzajWizualnie && transp.IsByBlock) valStr = "ByBlock";
                                    else valStr = "0";
                                }
                                else if (wartoscObiektu is Teigha.DatabaseServices.AnnotativeStates annState)
                                {
                                    valStr = annState == Teigha.DatabaseServices.AnnotativeStates.True ? "True" : "False";
                                }
                                else if (wartoscObiektu is bool bVal)
                                {
                                    valStr = bVal ? "True" : "False";
                                }
                                else if (wartoscObiektu is Teigha.Geometry.Point3d pt)
                                {
                                    valStr = $"({Math.Round(pt.X, 4)},{Math.Round(pt.Y, 4)},{Math.Round(pt.Z, 4)})".Replace(".0000", "").Replace(",0)", ",0,0)");
                                }
                                else if (wartoscObiektu is Teigha.DatabaseServices.LineWeight lw)
                                {
                                    if (lw == Teigha.DatabaseServices.LineWeight.ByLayer) valStr = "-1";
                                    else if (lw == Teigha.DatabaseServices.LineWeight.ByBlock) valStr = "-2";
                                    else if (lw == Teigha.DatabaseServices.LineWeight.ByLineWeightDefault) valStr = "-3";
                                    else valStr = ((int)lw).ToString();

                                    if (sprawdzajWizualnie && lw == Teigha.DatabaseServices.LineWeight.ByLayer)
                                    {
                                        try
                                        {
                                            LayerTableRecord ltr = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord;
                                            if (ltr != null)
                                            {
                                                if (ltr.LineWeight == Teigha.DatabaseServices.LineWeight.ByLineWeightDefault) valStr = "-3";
                                                else if (ltr.LineWeight == Teigha.DatabaseServices.LineWeight.ByLayer) valStr = "-1";
                                                else valStr = ((int)ltr.LineWeight).ToString();
                                            }
                                        }
                                        catch { valStr = "-3"; }
                                    }
                                }

                                if (sprawdzajWizualnie)
                                {
                                    if (rzeczywistaWlasciwosc == "ColorIndex" && valStr == "256")
                                    {
                                        try
                                        {
                                            LayerTableRecord ltr = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord;
                                            if (ltr != null) valStr = ltr.Color.ColorIndex.ToString();
                                        }
                                        catch { }
                                    }
                                    else if (rzeczywistaWlasciwosc.Equals("Linetype", StringComparison.OrdinalIgnoreCase) && valStr.Equals("ByLayer", StringComparison.OrdinalIgnoreCase))
                                    {
                                        try
                                        {
                                            LayerTableRecord ltr = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord;
                                            if (ltr != null)
                                            {
                                                LinetypeTableRecord lttr = tr.GetObject(ltr.LinetypeObjectId, OpenMode.ForRead) as LinetypeTableRecord;
                                                if (lttr != null) valStr = lttr.Name;
                                            }
                                        }
                                        catch { }
                                    }
                                }

                                // BEZPIECZNE PORÓWNANIE WARTOŚCI
                                bool warunekSpelniony = false;
                                if (double.TryParse(valStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double valNum) &&
                                    double.TryParse(warunek.Val.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double warNum))
                                {
                                    switch (warunek.Op)
                                    {
                                        case "==": warunekSpelniony = Math.Abs(valNum - warNum) < 0.0001; break;
                                        case "!=": warunekSpelniony = Math.Abs(valNum - warNum) >= 0.0001; break;
                                        case ">": warunekSpelniony = valNum > warNum; break;
                                        case "<": warunekSpelniony = valNum < warNum; break;
                                        case ">=": warunekSpelniony = valNum >= warNum; break;
                                        case "<=": warunekSpelniony = valNum <= warNum; break;
                                    }
                                }
                                else
                                {
                                    switch (warunek.Op.ToLower())
                                    {
                                        case "==": warunekSpelniony = valStr.Equals(warunek.Val, StringComparison.OrdinalIgnoreCase); break;
                                        case "!=": warunekSpelniony = !valStr.Equals(warunek.Val, StringComparison.OrdinalIgnoreCase); break;
                                        case "contains": warunekSpelniony = valStr.IndexOf(warunek.Val, StringComparison.OrdinalIgnoreCase) >= 0; break;
                                        case "in":
                                            string[] mozliweWartosci = warunek.Val.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                            foreach (string mw in mozliweWartosci)
                                            {
                                                if (valStr.Equals(mw.Trim(), StringComparison.OrdinalIgnoreCase))
                                                {
                                                    warunekSpelniony = true;
                                                    break;
                                                }
                                            }
                                            break;
                                    }
                                }
                                if (!warunekSpelniony) { spelniaWszystkie = false; break; }
                            }
                            if (spelniaWszystkie) znalezioneObiekty.Add(objId);
                        }

                    }

                    List<ObjectId> aktywne = AktywneZaznaczenie != null ? AktywneZaznaczenie.ToList() : new List<ObjectId>();
                    List<ObjectId> koncowe = new List<ObjectId>();

                    if (trybStr.Equals("Add", StringComparison.OrdinalIgnoreCase))
                    {
                        koncowe.AddRange(aktywne);
                        foreach (var id in znalezioneObiekty) if (!koncowe.Contains(id)) koncowe.Add(id);
                    }
                    else if (trybStr.Equals("Remove", StringComparison.OrdinalIgnoreCase))
                    {
                        koncowe.AddRange(aktywne);
                        foreach (var id in znalezioneObiekty) koncowe.Remove(id);
                    }
                    else
                    {
                        koncowe = znalezioneObiekty;
                    }

                    if (koncowe.Count > 0)
                    {
                        koncowe = koncowe.Distinct().ToList();

                        if (!scopeStr.Equals("Blocks", StringComparison.OrdinalIgnoreCase))
                        {
                            try { ed.SetImpliedSelection(koncowe.ToArray()); }
                            catch { }
                        }
                        else
                        {
                            ed.WriteMessage("\n[Info]: Zapisano obiekty z wnętrza bloków do pamięci Agenta (brak podświetlenia na ekranie).");
                        }

                        AktywneZaznaczenie = koncowe.ToArray();
                        ed.WriteMessage($"\n[Sukces]: Aktywne zaznaczenie w pamięci: {koncowe.Count} obiekt(ów)!");
                        return koncowe.Count;
                    }
                    else
                    {
                        ed.WriteMessage("\n[System]: Wynik zaznaczenia jest pusty.");
                        ed.SetImpliedSelection(new ObjectId[0]);
                        AktywneZaznaczenie = new ObjectId[0];

                        tr.Commit();
                        return 0;
                    }
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[Błąd Zaznaczania C#]: {ex.Message}");
                return 0;
            }
        }

        // --- GŁÓWNA PĘTLA AGENTA ---
        [CommandMethod("AGENT_START")]
        // =========================================================================================
        // STARSZA PĘTLA KONSOLOWA AGENTA (Z NAPRAWIONYM ŁAŃCUCHOWANIEM NARZĘDZI)
        // =========================================================================================
        [CommandMethod("AGENT_START")]
        public void UruchomAgenta()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            historiaRozmowy.Clear();
            WczytajPamiec(doc);
            WczytajBazyWiedzy(ed);

            if (historiaRozmowy.Count == 0 || !historiaRozmowy[0].Contains("system"))
            {
                historiaRozmowy.Insert(0, "{\"role\": \"system\", \"content\": \"" + SafeJson(systemPrompt) + "\"}");
                WczytajPrzykladyTreningowe(); // <--- DODANO ŁADOWANIE
                ed.WriteMessage($"\n--- Agent Bielik gotowy ({wybranyModel}) ---");
            }

            while (true)
            {
                PromptStringOptions pso = new PromptStringOptions("\nCo robimy? (exit/reset/modele): ");
                pso.AllowSpaces = true;
                PromptResult pr = ed.GetString(pso);
                if (pr.Status != PromptStatus.OK) break;
                string userMsg = pr.StringResult.Trim();

                if (userMsg.ToLower() == "exit") break;
                if (userMsg.ToLower() == "modele") { WybierzModela(); continue; }
                if (userMsg.ToLower() == "reset")
                {
                    ResetujPamiec(doc);
                    historiaRozmowy.Add("{\"role\": \"system\", \"content\": \"" + SafeJson(systemPrompt) + "\"}");
                    WczytajPrzykladyTreningowe(); // <--- DODANO ŁADOWANIE PO RESECIE
                    continue;
                }

                try
                {
                    historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + SafeJson(userMsg) + "\"}");
                    int licznikWyszukiwan = 0;
                    bool agentPotrzebujeDanych = true;

                    while (agentPotrzebujeDanych)
                    {
                        agentPotrzebujeDanych = false;
                        ed.WriteMessage("\nBielik myśli...");
                        string jsonBody = "{\"model\": \"" + wybranyModel + "\", \"messages\": [" + string.Join(",", historiaRozmowy) + "], \"temperature\": 0.1, \"stream\": false}";
                        var response = client.PostAsync("http://127.0.0.1:1234/v1/chat/completions", new StringContent(jsonBody, Encoding.UTF8, "application/json")).Result;
                        string aiMsg = WyciagnijContentZJson(response.Content.ReadAsStringAsync().Result);
                        if (aiMsg.Contains("</think>")) aiMsg = aiMsg.Substring(aiMsg.LastIndexOf("</think>") + 8).Trim();

                        if (!string.IsNullOrEmpty(aiMsg))
                        {
                            if (aiMsg.Contains("[SEARCH:"))
                            {
                                historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + SafeJson(aiMsg) + "\"}");
                                licznikWyszukiwan++;
                                if (licznikWyszukiwan > 2)
                                {
                                    ed.WriteMessage("\n[System]: Przerwano nieskończoną pętlę wyszukiwania Agenta.");
                                    historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"[SYSTEM]: Przekroczono limit wyszukiwań. ZAKAZ wyszukiwania. Użyj tego co już wiesz lub zapytaj użytkownika przez [MSG].\"}");
                                    agentPotrzebujeDanych = true;
                                    continue;
                                }

                                string szukanaKlasa = aiMsg.Substring(aiMsg.IndexOf("[SEARCH:") + 8).Split(']')[0].Trim().ToLower();
                                string wynik = "";

                                // Zbieramy wiedzę z obu baz naraz!
                                if (bazaQuick.ContainsKey(szukanaKlasa))
                                    wynik += "[QUICK INFO]: " + bazaQuick[szukanaKlasa] + "\n";

                                if (bazaFull.ContainsKey(szukanaKlasa))
                                    wynik += "[FULL API]: " + bazaFull[szukanaKlasa];

                                if (string.IsNullOrEmpty(wynik))
                                    wynik = "Brak definicji dla tej klasy.";

                                string odpowiedzSystemu = $"[DOKUMENTACJA]: {wynik}";
                                historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + SafeJson(odpowiedzSystemu) + "\"}");
                                agentPotrzebujeDanych = true;
                            }
                            else if (aiMsg.Contains("[SELECT:"))
                            {
                                ed.WriteMessage("\n[Agent]: " + aiMsg);
                                historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + SafeJson(aiMsg) + "\"}");

                                int start = aiMsg.IndexOf("{", aiMsg.IndexOf("[SELECT:"));
                                int end = aiMsg.LastIndexOf("}");
                                int zaznaczoneLiczba = 0;

                                if (start != -1 && end > start)
                                {
                                    zaznaczoneLiczba = WykonajInteligentneZaznaczenie(doc, aiMsg.Substring(start, end - start + 1));
                                    ZapiszPamiec(doc);
                                }

                                string sysOdp = $"[SYSTEM]: Pomyślnie zaznaczono {zaznaczoneLiczba} obiekt(ów). Jeśli to koniec zadania, odpowiedz [MSG: Gotowe]. Jeśli miałeś w planach użyć narzędzia [ACTION], wygeneruj je TERAZ.";
                                historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + SafeJson(sysOdp) + "\"}");
                                agentPotrzebujeDanych = true;
                            }
                            else if (aiMsg.Contains("[LISP:"))
                            {
                                ed.WriteMessage("\n[Agent]: " + aiMsg);
                                historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + SafeJson(aiMsg) + "\"}");
                                int start = aiMsg.IndexOf("[LISP:") + 6;
                                int end = aiMsg.IndexOf("]", start);
                                if (end > start)
                                {
                                    string lisp = aiMsg.Substring(start, end - start).Trim().Replace("`", "");
                                    doc.SendStringToExecute(lisp + "\n", true, false, false);
                                    doc.SendStringToExecute("AGENT_START\n", true, false, false);
                                    return;
                                }
                            }
                            else if (aiMsg.Contains("[MSG:"))
                            {
                                int start = aiMsg.IndexOf("[MSG:") + 5;
                                int end = aiMsg.LastIndexOf("]");
                                if (end > start)
                                {
                                    ed.WriteMessage("\n[Bielik Mówi]: " + aiMsg.Substring(start, end - start).Trim());
                                }
                                else
                                {
                                    ed.WriteMessage("\n[Bielik Mówi]: " + aiMsg);
                                }
                                historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + SafeJson(aiMsg) + "\"}");
                                ZapiszPamiec(doc);
                            }
                            // OBSŁUGA NARZĘDZI W TRYBIE KONSOLOWYM
                            else if (aiMsg.Contains("[ACTION:") || aiMsg.Contains("[ANALYZE:") || aiMsg.Contains("[READ_SAMPLE:"))
                            {
                                ed.WriteMessage("\n[Agent AI Używa Narzędzia]: " + aiMsg);
                                historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + SafeJson(aiMsg) + "\"}");

                                foreach (var tool in tools)
                                {
                                    string pelnyTag = tool.ActionTag;
                                    string krotkiTag = tool.ActionTag.Replace("[ACTION:", "[").Replace("]", ":");

                                    if (aiMsg.Contains(pelnyTag.Replace("]", "")) || aiMsg.Contains(krotkiTag))
                                    {
                                        string args = "";
                                        int startArgs = -1;

                                        if (aiMsg.Contains(pelnyTag.Replace("]", "")))
                                            startArgs = aiMsg.IndexOf("{", aiMsg.IndexOf(pelnyTag.Replace("]", "")));
                                        else if (aiMsg.Contains(krotkiTag))
                                            startArgs = aiMsg.IndexOf("{", aiMsg.IndexOf(krotkiTag));

                                        int endArgs = aiMsg.LastIndexOf("}");

                                        if (startArgs != -1 && endArgs > startArgs)
                                        {
                                            args = aiMsg.Substring(startArgs, endArgs - startArgs + 1);
                                        }

                                        string wynikNarzedzia = "";

                                        // Sprawdzamy, czy dane narzędzie posiada wbudowaną metodę Execute przyjmującą parametry (Document, string)
                                        var methodInfo = tool.GetType().GetMethod("Execute", new Type[] { typeof(Document), typeof(string) });

                                        if (methodInfo != null)
                                        {
                                            // Jeśli tak, przekazujemy dokument oraz cały wygenerowany tag (aiMsg)
                                            // (Zauważyłem, że w TestTag też tak robisz, a narzędzia radzą sobie wyciągając JSON z ciągu)
                                            wynikNarzedzia = (string)methodInfo.Invoke(tool, new object[] { doc, aiMsg });
                                        }
                                        else
                                        {
                                            // Fallback - jeśli narzędzie działa bez argumentów tekstowych (czyste doc)
                                            wynikNarzedzia = tool.Execute(doc);
                                        }

                                        if (wynikNarzedzia.StartsWith("WYNIK") || wynikNarzedzia.StartsWith("Pobrano"))
                                        {
                                            historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + SafeJson($"Oto dane z narzędzia:\n{wynikNarzedzia}\n\nKontynuuj zadanie. UŻYJ TAGU [MSG: twoja odpowiedź].") + "\"}");
                                            agentPotrzebujeDanych = true; // Pozwala pętli uruchomić zapytanie do modelu ponownie bez awarii
                                        }
                                        break;
                                    }
                                }
                                ZapiszPamiec(doc);
                            }
                            else
                            {
                                ed.WriteMessage("\n[Agent]: " + aiMsg);
                                historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + SafeJson(aiMsg) + "\"}");
                                ZapiszPamiec(doc);
                            }
                        }
                    }
                }
                catch (System.Exception ex) { ed.WriteMessage("\n[Błąd]: " + ex.Message); }
            }
        }

        // --- TESTER BAZY (Poprawiony) ---
        [CommandMethod("AGENT_CHECK_DB")]
        public void SprawdzBazeWiedzy()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            if (bazaQuick.Count == 0 && bazaFull.Count == 0) WczytajBazyWiedzy(ed);

            PromptResult pr = ed.GetString("\nPodaj klasę (np. circle): ");
            if (pr.Status != PromptStatus.OK) return;
            string szukane = pr.StringResult.Trim().ToLower();

            string tresc = "";
            if (bazaQuick.TryGetValue(szukane, out tresc)) ed.WriteMessage("\n[Źródło: QUICK]");
            else if (bazaFull.TryGetValue(szukane, out tresc)) ed.WriteMessage("\n[Źródło: FULL]");

            if (!string.IsNullOrEmpty(tresc))
                ed.WriteMessage($"\n{tresc.Replace(", ", "\n  - ").Replace("Właściwości", "\n[WŁASNOŚCI]").Replace("Opis", "\n[OPIS]")}");
            else
                ed.WriteMessage("\nNie znaleziono w bazach.");
        }

        [CommandMethod("AGENT_INSPECT")]
        public void InspectMText()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            PromptEntityOptions peo = new PromptEntityOptions("\nWybierz sformatowany MText do inspekcji: ");
            peo.SetRejectMessage("\nTo nie jest MText!");
            peo.AddAllowedClass(typeof(MText), true);

            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                MText mt = tr.GetObject(per.ObjectId, OpenMode.ForRead) as MText;
                ed.WriteMessage($"\n--- INSPEKCJA MTEXT ---");
                ed.WriteMessage($"\nSurowa treść (Contents): {mt.Contents}");
                ed.WriteMessage($"\nCzysty tekst (Text): {mt.Text}");
                ed.WriteMessage($"\n-----------------------");
            }
        }

        // ==========================================================
        // --- ŁADOWANIE PLIKU TRENINGOWEGO .JSONL (In-Context Learning) ---
        // ==========================================================
        private static void WczytajPrzykladyTreningowe()
        {
            try
            {
                // Szukamy pliku Agent_Training_Data.jsonl w tym samym folderze co wtyczka DLL
                string assemblyPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string folder = System.IO.Path.GetDirectoryName(assemblyPath);
                string pathJsonl = System.IO.Path.Combine(folder, "Agent_Example_Data.jsonl");

                if (System.IO.File.Exists(pathJsonl))
                {
                    string[] lines = System.IO.File.ReadAllLines(pathJsonl, Encoding.UTF8);
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        // Naprawiony, bezpieczny Regex dla C# (poprawne cudzysłowy)
                        MatchCollection matches = Regex.Matches(line, @"\{\s*""role""\s*:\s*""(user|assistant)""\s*,\s*""content""\s*:\s*"".*?(?<!\\)""\s*\}", RegexOptions.Singleline);

                        foreach (Match m in matches)
                        {
                            historiaRozmowy.Add(m.Value);
                        }
                    }
                }
            }
            catch { } // Ciche ignorowanie błędu
        }


        // ==========================================================
        // NARZĘDZIA DEBUGOWANIA (DIAGNOSTYKA)
        // ==========================================================

        [CommandMethod("AGENT_DEBUG_1")]
        public void DebugZaznaczenia()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            ed.WriteMessage("\n\n--- DIAGNOSTYKA ZAZNACZENIA ---");

            // 1. Sprawdzamy SelectImplied (zaznaczenie aktywne)
            PromptSelectionResult sel1 = ed.SelectImplied();
            if (sel1.Status == PromptStatus.OK)
            {
                ed.WriteMessage($"\n[SelectImplied]: Wykryto {sel1.Value.Count} obiektów.");
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in sel1.Value.GetObjectIds())
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        ed.WriteMessage($"\n  -> Typ: {ent.GetType().Name}, ID: {id.Handle}");
                    }
                    tr.Commit();
                }
            }
            else
            {
                ed.WriteMessage("\n[SelectImplied]: BRAK AKTYWNEGO ZAZNACZENIA.");
            }

            // 2. Sprawdzamy SelectPrevious (zaznaczenie poprzednie)
            PromptSelectionResult sel2 = ed.SelectPrevious();
            if (sel2.Status == PromptStatus.OK)
            {
                ed.WriteMessage($"\n[SelectPrevious]: Zapisano w pamięci {sel2.Value.Count} obiektów.");
            }
            else
            {
                ed.WriteMessage("\n[SelectPrevious]: PAMIĘĆ PUSTA.");
            }
            ed.WriteMessage("\n-------------------------------\n");
        }

        [CommandMethod("AGENT_TEST_TAG")]
        public void TestTag()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // 1. Prosimy użytkownika o wklejenie tagu w linii poleceń
            PromptStringOptions pso = new PromptStringOptions("\nWklej surowy tag wygenerowany przez LLM (np. [SELECT: ...]): ");
            pso.AllowSpaces = true; // Pozwala na spacje wewnątrz JSON-a!

            PromptResult pr = ed.GetString(pso);
            if (pr.Status != PromptStatus.OK) return;

            string wklejonyTag = pr.StringResult;

            try
            {
                // 2. Symulujemy zachowanie głównej pętli Agenta
                if (wklejonyTag.Contains("[SELECT:"))
                {
                    ed.WriteMessage("\n[TEST] Uruchamiam silnik zaznaczania...\n");
                    int wynik = WykonajInteligentneZaznaczenie(doc, wklejonyTag);
                    ed.WriteMessage($"\n[TEST ZAKOŃCZONY] Zaznaczono obiektów: {wynik}");
                }
                else if (wklejonyTag.Contains("[ACTION:EDIT_BLOCK"))
                {
                    ed.WriteMessage("\n[TEST] Uruchamiam narzędzie EDIT_BLOCK...\n");
                    EditBlockTool tool = new EditBlockTool();
                    string wynik = tool.Execute(doc, wklejonyTag);
                    ed.WriteMessage($"\n[TEST ZAKOŃCZONY] {wynik}");
                }
                else if (wklejonyTag.Contains("[ACTION:GET_PROPERTIES"))
                {
                    ed.WriteMessage("\n[TEST] Uruchamiam narzędzie GET_PROPERTIES...\n");
                    GetPropertiesTool tool = new GetPropertiesTool();
                    string wynik = tool.Execute(doc, wklejonyTag);
                    ed.WriteMessage($"\n[TEST ZAKOŃCZONY]\n{wynik}");
                }
                else if (wklejonyTag.Contains("[ACTION:MTEXT_FORMAT"))
                {
                    ed.WriteMessage("\n[TEST] Uruchamiam narzędzie MTEXT_FORMAT...\n");
                    MTextFormatTool tool = new MTextFormatTool();
                    string wynik = tool.Execute(doc, wklejonyTag);
                    ed.WriteMessage($"\n[TEST ZAKOŃCZONY] {wynik}");
                }
                else if (wklejonyTag.Contains("[ACTION:MTEXT_EDIT"))
                {
                    ed.WriteMessage("\n[TEST] Uruchamiam narzędzie MTEXT_EDIT...\n");
                    MTextEditTool tool = new MTextEditTool();
                    string wynik = tool.Execute(doc, wklejonyTag);
                    ed.WriteMessage($"\n[TEST ZAKOŃCZONY] {wynik}");
                }
                else if (wklejonyTag.Contains("[ACTION:TEXT_EDIT"))
                {
                    ed.WriteMessage("\n[TEST] Uruchamiam narzędzie TEXT_EDIT...\n");
                    TextEditTool tool = new TextEditTool();
                    string wynik = tool.Execute(doc, wklejonyTag);
                    ed.WriteMessage($"\n[TEST ZAKOŃCZONY] {wynik}");
                }

                else
                {
                    ed.WriteMessage("\n[BŁĄD TESTU] Wklejony tekst nie zawiera znanego tagu (SELECT, ACTION:EDIT_BLOCK itp.).");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[BŁĄD KRYTYCZNY W TESTOWANYM KODZIE]: {ex.Message}");
            }
        }


        [CommandMethod("AGENT_DEBUG_2")]
        public void DebugTwardeFormatowanie()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            PromptEntityOptions peo = new PromptEntityOptions("\nWybierz jeden tekst MText do brutalnego testu formatowania: ");
            peo.SetRejectMessage("\nMusisz wybrać MText.");
            peo.AddAllowedClass(typeof(MText), true);

            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return;

            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    MText mt = tr.GetObject(per.ObjectId, OpenMode.ForWrite) as MText;

                    ed.WriteMessage("\n\n--- DIAGNOSTYKA MTEXT ---");
                    ed.WriteMessage($"\n[PRZED] Contents (RTF): {mt.Contents}");
                    ed.WriteMessage($"\n[PRZED] Czysty Tekst: {mt.Text}");

                    // Twarde wymuszenie koloru czerwonego (1)
                    try
                    {
                        // Próbujemy zapisać to dokładnie tak, jak robi to nasz system AI
                        string testCode = "{\\C1;" + mt.Text + "}";
                        mt.Contents = testCode;
                        mt.RecordGraphicsModified(true);

                        ed.WriteMessage($"\n[PO] Contents (RTF): {mt.Contents}");
                        ed.WriteMessage("\n[WYNIK]: Operacja zapisu API powiodła się.");
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n[BŁĄD ZAPISU API]: {ex.Message}");
                    }

                    tr.Commit();
                }
            }
            ed.WriteMessage("\n-------------------------\n");


        }
    }
}