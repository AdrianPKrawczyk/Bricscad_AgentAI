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

        private static readonly HttpClient client = new HttpClient() { Timeout = TimeSpan.FromMinutes(5) };
        private static List<string> historiaRozmowy = new List<string>();
        private static string wybranyModel = "qwen3.5-9b-instruct";


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
        new UserChoiceTool()

        };

        private static PaletteSet oknoAgenta = null;
        private static Bricscad_AgentAI.AgentControl interfejsAgenta = null;

        private static string systemPrompt = "Jesteś autonomicznym Agentem Bielik w BricsCAD. Steruj programem ZA POMOCĄ TAGÓW. NIE JESTEŚ chatbotem do pisania kodu w markdown!\n\n" +
                        "Analizuj zadania w 5 tagach <think>.\n\n" +
                        "MUSISZ odpowiedzieć jednym z tagów:\n" +
                        "1. [SEARCH: Klasa] - ZAWSZE używaj tego, gdy nie znasz dokładnej nazwy właściwości! ZAKAZ ZGADYWANIA. \"Pamiętaj, że wszystkie obiekty graficzne (Line, Circle, Text, MText, itp) dziedziczą po klasie bazowej Entity. Zatem każdy obiekt zawsze posiada właściwości: Właściwości: Layer (warstwa), ColorIndex (1-255), Linetype, Transparency (0-90), Visible (True/False), LineWeight\"\n" +
                        "2. [SELECT: {\"Mode\": \"New|Add|Remove\", \"Scope\": \"Model|Blocks\", \"EntityType\": \"Klasa1, Klasa2\", \"Conditions\": [{\"Property\": \"Prop\", \"Operator\": \"==\", \"Value\": \"wartość\"}]}] - do zaznaczania. Użyj \"Scope\": \"Blocks\", jeśli użytkownik prosi o znalezienie obiektów WEWNĄTRZ aktualnie zaznaczonych bloków (domyślnie to \"Model\"). Parametr Mode określa zachowanie: \"New\" (tworzy nowe zaznaczenie, nadpisuje obecne), \"Add\" (dodaje szukane obiekty do tego, co obecnie zaznaczone), \"Remove\" (odejmuje szukane obiekty z obecnego zaznaczenia). Aby zaznaczyć wiele typów naraz, wymieniaj je po przecinku (np. \"DBText, MText\"). JSON bez enterów!\n" +
                        "3. [LISP: (command \"_KOMENDA\" ...)] - do rysowania/edycji.\n" +
                        "4. [MSG: Twój tekst] - UŻYJ TEGO TAGU, aby odpowiedzieć na pytania użytkownika, ZWŁASZCZA po zebraniu danych narzędziami ANALYZE, READ_SAMPLE lub GET_PROPERTIES!\n" +
                        "5. [ACTION:TAG_NARZEDZIA {\"Argumenty\": \"JSON\"}] - do uruchamiania narzędzi na zaznaczonych obiektach.\n\n" +

                        "--- GLOBALNE ZASADY WŁAŚCIWOŚCI CAD (DOTYCZY WSZYSTKICH OBIEKTÓW) ---\n" +
                        "Zawsze stosuj ten uniwersalny słownik wartości, gdy użytkownik prosi o wyszukanie (SELECT), zmianę lub edycję (np. EDIT_BLOCK). Te właściwości dziedziczy każdy obiekt CAD (Entity). Zwróć szczególną uwagę na to, jak zapisuje się stan 'JakWarstwa' i 'JakBlok' w różnych właściwościach:\n" +
                        "1. Color (Kolor): Używaj liczb! 256 = JakWarstwa (ByLayer), 0 = JakBlok (ByBlock). Pozostałe to ACI: 1-czerwony, 2-żółty, 3-zielony, 4-cyjan, 5-niebieski, 6-magenta, 7-biały/czarny, 8-ciemnoszary, 9-jasnoszary. Dla formatu RGB użyj stringa, np. \"255,128,0\".\n" +
                        "2. LineWeight (Grubość linii): Używaj specjalnych liczb! -1 = JakWarstwa (ByLayer), -2 = JakBlok (ByBlock), -3 = Domyślna (Default). Konkretne grubości to setne części milimetra (np. wartość 25 oznacza 0.25 mm, a 30 to 0.30 mm).\n" +
                        "3. Linetype (Rodzaj linii): Używaj tekstu (string)! Słowa kluczowe to: \"ByLayer\" (JakWarstwa), \"ByBlock\" (JakBlok) oraz \"Continuous\" (Ciągła).\n" +
                        "4. Material (Materiał) i PlotStyleName (Styl wydruku): Używaj tekstu (string)! Słowa kluczowe to: \"ByLayer\" oraz \"ByBlock\".\n" +
                        "5. Layer (Warstwa): Wartość tekstowa (string). Domyślna, zerowa warstwa nazywa się po prostu \"0\".\n" +
                        "6. Transparency (Przezroczystość): Przyjmuje liczby od 0 (pełna widoczność/brak przezroczystości) do 90 (maksymalna przezroczystość).\n\n" +

                        "--- DOSTĘPNE NARZĘDZIA (Użyj NAJPIERW [SELECT] aby zaznaczyć obiekty!): ---\n" +

                        "Tag: [ACTION:MTEXT_FORMAT]\n" +
                        "Opis: Zmienia formatowanie MText.\n" +
                        "Argumenty: {\"Mode\": \"HighlightWord\"|\"FormatAll\"|\"ClearFormatting\", \"Word\": \"słowo\" (tylko dla HighlightWord),\"Color\": nr_koloru (indeks ACI od 1 do 255, np. 1-czerwony, 2-żółty, 3-zielony, 79-jasnozielony, itd.), \"Bold\": true/false}\n\n" +

                        "Tag: [ACTION:MTEXT_EDIT]\n" +
                        "Opis: Dodaje lub zamienia tekst w MText.\n" +
                        "Argumenty: {\"Mode\": \"Append\"|\"Prepend\"|\"Replace\", \"Text\": \"tekst do dodania\", \"FindText\": \"szukany\" (tylko dla Replace), \"Color\": nr_koloru (np. 6 dla fioletu), \"Underline\": true/false, \"Bold\": true/false, \"Italic\": true/false}\n\n" +

                        "Tag: [ACTION:TEXT_EDIT]\n" +
                        "Opis: Dodaje lub zamienia zawartość zwykłego TEXT (DBText). Nie obsługuje formatowania wewnątrz tekstu.\n" +
                        "Argumenty: {\"Mode\": \"Append\"|\"Prepend\"|\"Replace\", \"Text\": \"tekst do dodania\", \"FindText\": \"szukany\" (tylko Replace), \"Color\": nr_koloru (zmienia kolor całego obiektu)}\n\n" +

                        "Tag: [ACTION:ANALYZE]\n" +
                        "Opis: Zmysł wzroku Agenta. Użyj tego ZANIM zaczniesz edycję, gdy użytkownik każe Ci edytować 'zaznaczone obiekty', a Ty nie wiesz, czy są to obiekty typu TEXT czy MText. Zwraca podsumowanie tego, co obecnie znajduje się w pamięci zaznaczenia.\n" +
                        "Argumenty: {}\n\n" +

                        "Tag: [ACTION:READ_SAMPLE]\n" +
                        "Opis: Zmysł czytania Agenta. Użyj tego BEZWZGLĘDNIE ZANIM użyjesz narzędzi edycji tekstu (zwłaszcza trybu Replace), aby 'przeczytać' zawartość i zrozumieć strukturę zaznaczonych tekstów na rysunku. Pozwala to uniknąć błędów przy podmianie słów.\n" +
                        "Argumenty: {}\n\n" +

                        "Tag: [ACTION:GET_PROPERTIES_LITE]\n" +
                        "Opis: Szybki skan podstawowych właściwości zaznaczonych obiektów (np. Kolor, Warstwa, Rodzaj Linii). Używaj tego domyślnie, aby oszczędzać pamięć.\n" +
                        "Argumenty: Brak.\n\n" +

                        "Tag: [ACTION:GET_PROPERTIES]\n" +
                        "Opis: Głęboki skan wykorzystujący Refleksję. Zwraca absolutnie wszystkie dostępne właściwości dla maksymalnie 5 zaznaczonych obiektów. Używaj tylko wtedy, gdy potrzebujesz sprawdzić zaawansowane parametry geometryczne lub specyficzne ustawienia, których nie zwróciło narzędzie LITE.\n" +
                        "Argumenty: Brak.\n\n" +

                        "Tag: [ACTION:EDIT_BLOCK]\n" +
                        "Opis: Edytuje wnętrza zaznaczonych bloków (BlockReference) oraz ich atrybuty. Wszystkie parametry są opcjonalne, ale musisz podać co najmniej jeden do zmiany.\n" +
                        "Argumenty (wygeneruj poprawny JSON): Dostępne klucze to: \"Color\" (0-255), \"Layer\" (string nazwa warstwy), \"FilterColor\" (liczba całkowita), \"FindText\" (string do znalezienia), \"ReplaceText\" (string do zamiany), \"RemoveDimensions\" (true/false, podaj true aby usunąć wszystkie wymiary z bloków). \n" +
                        "Przykład 1 (tylko kolor): [ACTION:EDIT_BLOCK {\"Color\": 7}]\n" +
                        "Przykład 2 (usuń wymiary): [ACTION:EDIT_BLOCK {\"RemoveDimensions\": true}]\n" +
                        "UWAGA: Nie pytaj użytkownika o zgodę ani potwierdzenie parametrów! Jeśli użytkownik pisze 'zmień na czarny', po prostu od razu wygeneruj tag działania!\n\n" +

                        "Tag: [ACTION:SET_PROPERTIES]\n" +
                        "Opis: Uniwersalne narzędzie do zmiany właściwości zaznaczonych obiektów (np. Koloru, Warstwy, Rodzaju Linii, Skali, itp.).\n" +
                        "Argumenty: Zbuduj JSON z listą \"Properties\". Przykład: [ACTION:SET_PROPERTIES {\"Properties\": [{\"Property\": \"Layer\", \"Value\": \"Instalacje\"}, {\"Property\": \"Color\", \"Value\": 256}]}]\n\n" +

                         "Tag: [ACTION:SET_PROPERTIES]\n" +
                        "Opis: Uniwersalne narzędzie do zmiany właściwości. Domyślnie nadpisuje wartość. Jeśli użytkownik prosi o ZMODYFIKOWANIE wartości (np. \"podnieś Z o 200\"), użyj klucza \"Operator\": \"+\" (lub \"-\", \"*\"). Do skomplikowanych obliczeń użyj \"Operator\": \"RPN\" podając wyrażenie (np. \"Value\": \"2 * 50 +\").\n" +
                        "Przykład 1 (Nadpisanie): [ACTION:SET_PROPERTIES {\"Properties\": [{\"Property\": \"Layer\", \"Value\": \"Instalacje\"}]}]\n" +
                        "Przykład 2 (Dodawanie): [ACTION:SET_PROPERTIES {\"Properties\": [{\"Property\": \"Center.Z\", \"Operator\": \"+\", \"Value\": 200}]}]\n\n" +

                        "Tag: [ACTION:LIST_BLOCKS]\n" +
                        "Opis: Zwraca listę wszystkich unikalnych (niepowtarzających się) nazw bloków, które znajdują się w aktualnym zaznaczeniu. Narzędzie nie wymaga argumentów.\n" +
                        "Przykład użycia: [ACTION:LIST_BLOCKS]\n\n" +

                        "Tag: [ACTION:MODIFY_GEOMETRY]\n" +
                        "Opis: Fizyczna edycja kształtu i położenia zaznaczonych obiektów (Usuwanie, Przesuwanie, Kopiowanie, Obracanie, Skalowanie). Wymaga parametrów w JSON!\n" +
                        "Tryby (Mode) i ich wymagane argumenty:\n" +
                        " - \"Erase\" (Kasuje obiekty z rysunku): [ACTION:MODIFY_GEOMETRY {\"Mode\": \"Erase\"}]\n" +
                        " - \"Move\" (Przesuwa o wektor przesunięcia): [ACTION:MODIFY_GEOMETRY {\"Mode\": \"Move\", \"Vector\": \"(dX,dY,dZ)\"}]\n" +
                        " - \"Copy\" (Kopiuje obiekty przesuwając o wektor): [ACTION:MODIFY_GEOMETRY {\"Mode\": \"Copy\", \"Vector\": \"(100,0,0)\"}]\n" +
                        " - \"Rotate\" (Obraca wokół punktu BasePoint o kąt Angle w stopniach): [ACTION:MODIFY_GEOMETRY {\"Mode\": \"Rotate\", \"BasePoint\": \"(0,0,0)\", \"Angle\": 90}]\n" +
                        " - \"Scale\" (Skaluje z punktu bazowego o współczynnik Factor): [ACTION:MODIFY_GEOMETRY {\"Mode\": \"Scale\", \"BasePoint\": \"(0,0,0)\", \"Factor\": 0.5}]\n\n" +


                        "Tag: [ACTION:READ_PROPERTY]\n" +
                        "Opis: Zwraca konkretną, pojedynczą właściwość z zaznaczonych obiektów (np. współrzędne środka, promień, długość). Użyj tego, gdy potrzebujesz wyciągnąć precyzyjne dane (np. punkt 'Center'), aby użyć ich w kolejnych krokach jako punktów bazowych.\n" +
                        "Argumenty: [ACTION:READ_PROPERTY {\"Property\": \"Center\"}]\n\n" +

                        "Tag: [ACTION:ADD_ANNO_SCALE]\n" +
                        "Opis: Narzędzie służące do włączania właściwości opisowej (Annotative) i jednoczesnego przypisywania konkretnej skali (np. 1:50 lub 1:100) do zaznaczonych wymiarów, tekstów i bloków.\n" +
                        "Argumenty: [ACTION:ADD_ANNO_SCALE {\"Scale\": \"1:50\"}]\n\n" +

                       "Tag: [ACTION:READ_ANNO_SCALES]\n" +
                        "Opis: Odczytuje wszystkie skale opisowe (Annotative Scales) przypisane do zaznaczonych obiektów. Użyj przed modyfikacją, by sprawdzić jakie skale mają obiekty.\n" +
                        "Argumenty: Brak.\n\n" +

                        "Tag: [ACTION:READ_ANNO_SCALES]\n" +
                        "Opis: Odczytuje skale opisowe przypisane do zaznaczonych obiektów.\n" +
                        "Tryby: {\"Mode\": \"Summary\"} (krótkie podsumowanie dla LLM) lub {\"Mode\": \"Detailed\"} (lista każdego obiektu z osobna).\n" +
                        "Przykłady: [ACTION:READ_ANNO_SCALES {\"Mode\": \"Summary\"}]\n\n" +

                        "Tag: [ACTION:LIST_UNIQUE]\n" +
                        "Opis: Agreguje i zwraca listę unikalnych klas API (np. by sprawdzić, jakie obiekty są na rysunku) lub unikalnych wartości konkretnej właściwości (np. lista wszystkich nazw warstw, nazw bloków).\n" +
                        "Argument 'Target': \"Class\" (zwraca typy) lub \"Property\" (zwraca wartości).\n" +
                        "Argument 'Scope': \"Selection\" (domyślnie), \"Model\" (cały rysunek) lub \"Blocks\" (wnętrza wszystkich bloków).\n" +
                        "Przykład 1 (Klasy w modelu): [ACTION:LIST_UNIQUE {\"Target\": \"Class\", \"Scope\": \"Model\"}]\n" +
                        "Przykład 2 (Nazwy warstw w zaznaczeniu): [ACTION:LIST_UNIQUE {\"Target\": \"Property\", \"Scope\": \"Selection\", \"Property\": \"Layer\"}]\n\n" +

                        "Tag: [ACTION:USER_CHOICE]\n" +
                        "Opis: Wyświetla interaktywną listę jednokrotnego wyboru. Możesz podać własne opcje, LUB automatycznie poprosić C# o pobranie unikalnych wartości prosto z rysunku (zalecane, oszczędza tokeny).\n" +
                        "Przykład 1 (Własne opcje): [ACTION:USER_CHOICE {\"Question\": \"Czy usunąć?\", \"Options\": [\"Tak\", \"Nie\"]}]\n" +
                        "Przykład 2 (Auto-pobieranie z rysunku): [ACTION:USER_CHOICE {\"Question\": \"Wybierz warstwę:\", \"FetchTarget\": \"Property\", \"FetchScope\": \"Model\", \"FetchProperty\": \"Layer\"}]\n\n" +

                        "Tag: [ACTION:USER_INPUT]\n" +
                        "Opis: Prosi użytkownika o wpisanie zwykłego tekstu lub fizyczne wskazanie punktów na rysunku (np. by pobrać współrzędne do przesunięcia).\n" +
                        "Argument 'Type': \"String\" (tekst), \"Point\" (jeden punkt) lub \"Points\" (wiele punktów).\n" +
                        "Przykład: [ACTION:USER_INPUT {\"Type\": \"Point\", \"Prompt\": \"Wskaż środek obrotu\"}]\n\n" +
                        "WAŻNE - MYŚLENIE WEWNĘTRZNE: Do *każdego* tagu [ACTION] lub [SELECT] możesz opcjonalnie dodać parametr \"Comment\", aby zapisać w nim swój tok rozumowania, dlaczego podejmujesz daną akcję. Np. [ACTION:MODIFY_GEOMETRY {\"Mode\": \"Erase\", \"Comment\": \"Usuwam to, bo użytkownik prosił o czyszczenie\"}].\n\n" +



                        "User: Zaznacz linie dłuższe niż 50\n" +
                        "Bielik: [SELECT: {\"Mode\": \"New\", \"Scope\": \"Model\", \"EntityType\": \"Line\", \"Conditions\": [{\"Property\": \"Length\", \"Operator\": \">\", \"Value\": 50}]}]\n" +

                        "User: Znajdź wszystkie teksty wewnątrz tych bloków\n" +
                        "Bielik: [SELECT: {\"Mode\": \"New\", \"Scope\": \"Blocks\", \"EntityType\": \"DBText, MText\", \"Conditions\": []}]\n" +

                        "User: Znajdź linie, które nie zaczynają się w 0,0,0\n" +
                        "Bielik: [SELECT: {\"Mode\": \"New\", \"Scope\": \"Model\", \"EntityType\": \"Line\", \"Conditions\": [{\"Property\": \"StartPoint\", \"Operator\": \"!=\", \"Value\": \"(0,0,0)\"}]}]\n" +

                        "User: Zaznacz teksty z formatowaniem wewnętrznym\n" +
                        "Bielik: [SELECT: {\"Mode\": \"New\", \"Scope\": \"Model\", \"EntityType\": \"MText\", \"Conditions\": [{\"Property\": \"Contents\", \"Operator\": \"Contains\", \"Value\": \";\"}]}]\n" +

                        "User: Dodaj do zaznaczenia zielone linie\n" +
                        "Bielik: [SELECT: {\"Mode\": \"Add\", \"Scope\": \"Model\", \"EntityType\": \"Line\", \"Conditions\": [{\"Property\": \"Color\", \"Operator\": \"==\", \"Value\": 3}]}]\n" +

                        "User: Wyrzuć z zaznaczenia teksty wyższe niż 10\n" +
                        "Bielik: [SELECT: {\"Mode\": \"Remove\", \"Scope\": \"Model\", \"EntityType\": \"DBText\", \"Conditions\": [{\"Property\": \"Height\", \"Operator\": \">\", \"Value\": 10}]}]\n" +

                        "User: Zmień słowo PVC na czerwone w zaznaczonych tekstach\n" +
                        "Bielik: [ACTION:MTEXT_FORMAT {\"Mode\": \"HighlightWord\", \"Word\": \"PVC\", \"Color\": 1, \"Bold\": false}]\n\n" +

                        "--- ZASADY LISP (KRYTYCZNE): ---\n" +
                        "1. ZAWSZE dodawaj podkreślnik przed komendą: \"_LINE\", \"_CIRCLE\".\n" +
                        "2. Komenda LINE musi kończyć się pustym stringiem: (command \"_LINE\" p1 p2 \"\").\n\n" +

                        "--- KRYTYCZNE ZASADY BEZPIECZEŃSTWA: ---\n" +
                        "1. ZAKAZ ZMYŚLANIA ZAZNACZEŃ! Jeśli użytkownik prosi o 'zaznaczenie', 'dodanie do zaznaczenia' lub 'odjęcie', ZAWSZE musisz najpierw wygenerować tag [SELECT: ...]. Nigdy nie odpowiadaj [MSG: Zaznaczono...], jeśli w poprzednim kroku nie użyłeś tagu [SELECT: ...].\n" +
                        "2. ZAKAZ RYSOWANIA, GDY UŻYTKOWNIK CHCE ZAZNACZYĆ! Słowa 'dodaj do zaznaczenia' (add to selection) to komenda trybu Mode: Add w tagu [SELECT: ...]. NIE UŻYWAJ tagu [LISP:] do rysowania nowych obiektów, chyba że użytkownik wyraźnie napisze 'narysuj' (draw/create)!\n\n" +
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
        public static async Task<string> ZapytajAgentaAsync(string userMsg, Document doc, ObjectId[] przechwyconeZaznaczenie = null)
        {
            if (historiaRozmowy.Count == 0 || !historiaRozmowy[0].Contains("system"))
            {
                // Odczytujemy jednostkę w tle
                short insunits = Convert.ToInt16(Application.GetSystemVariable("INSUNITS"));
                string unitName = insunits == 4 ? "Milimetry (mm)" : insunits == 5 ? "Centymetry (cm)" : insunits == 6 ? "Metry (m)" : "Inne";

                // Doklejamy informację do promptu
                string zaktualizowanyPrompt = systemPrompt + $"\n\n[INFO SYSTEMOWE]: Aktualne jednostki otwartego rysunku to: {unitName}. ZAWSZE przeliczaj wymiary podane przez użytkownika na te jednostki przed wygenerowaniem tagu SELECT.";

                historiaRozmowy.Insert(0, "{\"role\": \"system\", \"content\": \"" + Komendy.SafeJson(zaktualizowanyPrompt) + "\"}");
            }

            // Zapobiega dodawaniu pustych wiadomości przy automatycznej rekurencji (chaining)
            if (!string.IsNullOrEmpty(userMsg))
            {
                historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + Komendy.SafeJson(userMsg) + "\"}");
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
                    historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + Komendy.SafeJson(aiMsg) + "\"}");

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
                        else if (aiMsg.Contains("[ACTION:") || aiMsg.Contains("[ANALYZE:") || aiMsg.Contains("[READ_SAMPLE:"))
                        {
                            foreach (var tool in new Komendy().tools)
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
                                    if (tool is MTextFormatTool mtextTool) wynikNarzedzia = mtextTool.Execute(doc, args);
                                    else if (tool is MTextEditTool mtextEditTool) wynikNarzedzia = mtextEditTool.Execute(doc, args);
                                    else if (tool is TextEditTool textEditTool) wynikNarzedzia = textEditTool.Execute(doc, args);
                                    else if (tool is AnalyzeSelectionTool analyzeTool) wynikNarzedzia = analyzeTool.Execute(doc, args);
                                    else if (tool is ReadTextSampleTool readTool) wynikNarzedzia = readTool.Execute(doc, args);
                                    else wynikNarzedzia = tool.Execute(doc);

                                    // Rekursja gdy mamy dane, których potrzebuje model
                                    if (wynikNarzedzia.StartsWith("WYNIK") || wynikNarzedzia.StartsWith("Pobrano"))
                                    {
                                        historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + Komendy.SafeJson($"Oto dane z narzędzia:\n{wynikNarzedzia}\n\nKontynuuj zadanie. UŻYJ TAGU [MSG: twoja odpowiedź].") + "\"}");
                                        return await ZapytajAgentaAsync("", doc, przechwyconeZaznaczenie);
                                    }

                                    return $"[Wykonano narzędzie {tool.ActionTag}]\n{aiMsg}";
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
        private string GetGlobalPath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BricsCAD_Agent");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "global_memory.json");
        }

        private string GetLocalPath(Document doc)
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

        private void ResetujPamiec(Document doc)
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

                // Nowość: Odczytujemy tryb (jeśli Agent nie poda, domyślnie to "New")
                string trybStr = "New";
                Match trybMatch = Regex.Match(json, @"\""Mode\""\s*:\s*\""([^\""]+)\""");
                if (trybMatch.Success) trybStr = trybMatch.Groups[1].Value;

                // --- NOWOŚĆ: Odczytujemy obszar poszukiwań (domyślnie Model, ale może być Blocks) ---
                string scopeStr = "Model";
                Match scopeMatch = Regex.Match(json, @"\""Scope\""\s*:\s*\""([^\""]+)\""");
                if (scopeMatch.Success) scopeStr = scopeMatch.Groups[1].Value;

                var warunki = new List<(string Prop, string Op, string Val)>();
                MatchCollection matches = Regex.Matches(json, @"\""Property\""\s*:\s*\""([^\""]+)\"".*?\""Operator\""\s*:\s*\""([^\""]+)\"".*?\""Value\""\s*:\s*(\""[^\""]+\""|[^\s,}]+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match m in matches)
                {
                    warunki.Add((m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value.Trim('\"')));
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
                // Mała modyfikacja loga, żeby pokazywał też obszar (Model czy Bloki)
                ed.WriteMessage($"\n[System]: Szukam '{string.Join("/", typyDoSzukania)}' (Tryb: {trybStr}, Obszar: {scopeStr}, Warunki: {warunki.Count})...");

                List<ObjectId> znalezioneObiekty = new List<ObjectId>();
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    // --- NOWOŚĆ: Lista miejsc (obszarów/bloków), które będziemy przeszukiwać ---
                    List<ObjectId> blokiDoPrzeszukania = new List<ObjectId>();

                    if (scopeStr.Equals("Blocks", StringComparison.OrdinalIgnoreCase) && AktywneZaznaczenie != null && AktywneZaznaczenie.Length > 0)
                    {
                        // 1. Zbieramy pierwsze, najwyższe warstwy bloków z zaznaczenia
                        foreach (ObjectId id in AktywneZaznaczenie)
                        {
                            Entity zaznaczonyEnt = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (zaznaczonyEnt is BlockReference br)
                            {
                                if (!blokiDoPrzeszukania.Contains(br.BlockTableRecord))
                                {
                                    blokiDoPrzeszukania.Add(br.BlockTableRecord);
                                }
                            }
                        }

                        // 2. NOWOŚĆ: Przeszukiwanie "w głąb" (rekurencja pętlowa).
                        // Pętla iteruje po liście, która sama rośnie, jeśli znajdzie kolejne bloki!
                        for (int i = 0; i < blokiDoPrzeszukania.Count; i++)
                        {
                            BlockTableRecord wewnetrznyBtr = (BlockTableRecord)tr.GetObject(blokiDoPrzeszukania[i], OpenMode.ForRead);
                            foreach (ObjectId wewnetrzneId in wewnetrznyBtr)
                            {
                                Entity wewnEnt = tr.GetObject(wewnetrzneId, OpenMode.ForRead) as Entity;
                                if (wewnEnt is BlockReference zagniezdzonyBr)
                                {
                                    // Jeśli znajdujemy blok w bloku, dorzucamy jego definicję do końca naszej listy!
                                    if (!blokiDoPrzeszukania.Contains(zagniezdzonyBr.BlockTableRecord))
                                    {
                                        blokiDoPrzeszukania.Add(zagniezdzonyBr.BlockTableRecord);
                                    }
                                }
                            }
                        }

                        if (blokiDoPrzeszukania.Count == 0)
                        {
                            ed.WriteMessage("\n[Uwaga]: Brak zaznaczonych bloków, by szukać wewnątrz nich.");
                        }
                    }
                    else
                    {
                        // Domyślnie szukamy po prostu w otwartym aktualnie oknie (Model/Arkusz/BEDIT)
                        blokiDoPrzeszukania.Add(doc.Database.CurrentSpaceId);
                    }

                    // --- NOWOŚĆ: Główna pętla iterująca po wszystkich wybranych obszarach ---
                    foreach (ObjectId spaceId in blokiDoPrzeszukania)
                    {
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);

                        // Oryginalna pętla przeglądająca obiekty
                        foreach (ObjectId objId in btr)
                        {
                            Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;

                            string nazwaTypuEnt = ent.GetType().Name;
                            bool typPasuje = false;

                            foreach (var t in typyDoSzukania)
                            {
                                string szukanyTyp = t.Trim();

                                // --- NOWOŚĆ: Jeśli Agent szuka "Entity", przepuść absolutnie każdy obiekt graficzny! ---
                                if (szukanyTyp.Equals("Entity", StringComparison.OrdinalIgnoreCase)) { typPasuje = true; break; }

                                if (szukanyTyp.Equals("Text", StringComparison.OrdinalIgnoreCase) && ent is DBText) { typPasuje = true; break; }
                                if (szukanyTyp.Equals("Dimension", StringComparison.OrdinalIgnoreCase) && ent is Dimension) { typPasuje = true; break; }
                                if (nazwaTypuEnt.Equals(szukanyTyp, StringComparison.OrdinalIgnoreCase)) { typPasuje = true; break; }
                            }
                            if (!typPasuje) continue;

                            bool spelniaWszystkie = true;
                            foreach (var warunek in warunki)
                            {
                                string rzeczywistaWlasciwosc = warunek.Prop;
                                bool sprawdzajWizualnie = false;

                                // --- ROZDZIELENIE WŁAŚCIWOŚCI SUROWYCH OD WIZUALNYCH ---
                                if (rzeczywistaWlasciwosc.Equals("VisualColor", StringComparison.OrdinalIgnoreCase))
                                {
                                    rzeczywistaWlasciwosc = "ColorIndex";
                                    sprawdzajWizualnie = true;
                                }
                                else if (rzeczywistaWlasciwosc.Equals("VisualLinetype", StringComparison.OrdinalIgnoreCase))
                                {
                                    rzeczywistaWlasciwosc = "Linetype";
                                    sprawdzajWizualnie = true;
                                }
                                // NOWOŚĆ: Szerokość linii
                                else if (rzeczywistaWlasciwosc.Equals("VisualLineWeight", StringComparison.OrdinalIgnoreCase))
                                {
                                    rzeczywistaWlasciwosc = "LineWeight";
                                    sprawdzajWizualnie = true;
                                }
                                // NOWOŚĆ: Przezroczystość
                                else if (rzeczywistaWlasciwosc.Equals("VisualTransparency", StringComparison.OrdinalIgnoreCase))
                                {
                                    rzeczywistaWlasciwosc = "Transparency";
                                    sprawdzajWizualnie = true;
                                }
                                else if (rzeczywistaWlasciwosc.Equals("Color", StringComparison.OrdinalIgnoreCase))
                                {
                                    rzeczywistaWlasciwosc = "ColorIndex";
                                }
                                // Tłumaczenia dla specyficznych klas
                                else if (ent is MText && rzeczywistaWlasciwosc.Equals("Height", StringComparison.OrdinalIgnoreCase)) rzeczywistaWlasciwosc = "TextHeight";
                                else if (ent is Dimension && (rzeczywistaWlasciwosc.Equals("Height", StringComparison.OrdinalIgnoreCase) || rzeczywistaWlasciwosc.Equals("TextHeight", StringComparison.OrdinalIgnoreCase))) rzeczywistaWlasciwosc = "Dimtxt";

                                // --- NOWOŚĆ: Pobieranie wartości przez refleksję z obsługą zagnieżdżeń (np. Center.Z) ---
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

                                // --- SPECJALNA OBSŁUGA PRZEZROCZYSTOŚCI ---
                                if (wartoscObiektu is Teigha.Colors.Transparency transp)
                                {
                                    if (transp.IsByAlpha) valStr = Math.Round((255.0 - transp.Alpha) / 255.0 * 100.0).ToString();
                                    else if (sprawdzajWizualnie && transp.IsByLayer)
                                    {
                                        try
                                        {
                                            LayerTableRecord ltr = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord;
                                            if (ltr != null && ltr.Transparency.IsByAlpha)
                                                valStr = Math.Round((255.0 - ltr.Transparency.Alpha) / 255.0 * 100.0).ToString();
                                            else valStr = "0";
                                        }
                                        catch { valStr = "0"; }
                                    }
                                    else if (!sprawdzajWizualnie && transp.IsByLayer) valStr = "ByLayer";
                                    else if (!sprawdzajWizualnie && transp.IsByBlock) valStr = "ByBlock"; // <--- NOWOŚĆ: dodano brakującą obsługę ByBlock
                                    else valStr = "0";
                                }

                                // --- SPECJALNA OBSŁUGA WŁAŚCIWOŚCI OPISOWEJ (ANNOTATIVE) ---
                                else if (wartoscObiektu is Teigha.DatabaseServices.AnnotativeStates annState)
                                {
                                    if (annState == Teigha.DatabaseServices.AnnotativeStates.True) valStr = "True";
                                    else valStr = "False"; // Złapie zarówno .False jak i ewentualne .NotApplicable
                                }
                                // Na wszelki wypadek, gdyby jakaś inna właściwość była czystym boolem:
                                else if (wartoscObiektu is bool bVal)
                                {
                                    valStr = bVal ? "True" : "False";
                                }
                                // --- SPECJALNA OBSŁUGA WSPÓŁRZĘDNYCH 3D ---
                                else if (wartoscObiektu is Teigha.Geometry.Point3d pt)
                                {
                                    valStr = $"({Math.Round(pt.X, 4)},{Math.Round(pt.Y, 4)},{Math.Round(pt.Z, 4)})".Replace(".0000", "").Replace(",0)", ",0,0)");
                                }

                                // --- SPECJALNA OBSŁUGA GRUBOŚCI LINII (LineWeight) ---
                                else if (wartoscObiektu is Teigha.DatabaseServices.LineWeight lw)
                                {
                                    // Zmiana: Musimy mapować ByLayer i ByBlock na liczby (-1, -2), bo tak uczy się LLM z promptu!
                                    if (lw == Teigha.DatabaseServices.LineWeight.ByLayer) valStr = "-1";
                                    else if (lw == Teigha.DatabaseServices.LineWeight.ByBlock) valStr = "-2";
                                    else if (lw == Teigha.DatabaseServices.LineWeight.ByLineWeightDefault) valStr = "-3";
                                    else valStr = ((int)lw).ToString();

                                    // Jeśli LLM użył VisualLineWeight i obiekt ma przypisane "Jak warstwa"
                                    if (sprawdzajWizualnie && lw == Teigha.DatabaseServices.LineWeight.ByLayer)
                                    {
                                        try
                                        {
                                            LayerTableRecord ltr = tr.GetObject(ent.LayerId, OpenMode.ForRead) as LayerTableRecord;
                                            if (ltr != null)
                                            {
                                                if (ltr.LineWeight == Teigha.DatabaseServices.LineWeight.ByLineWeightDefault) valStr = "-3";
                                                else if (ltr.LineWeight == Teigha.DatabaseServices.LineWeight.ByLayer) valStr = "-1"; // Na wypadek błędu bazy
                                                else valStr = ((int)ltr.LineWeight).ToString();
                                            }
                                        }
                                        catch { valStr = "-3"; }
                                    }
                                }


                                // --- ROZWIĄZYWANIE "JAK WARSTWA" DLA WŁAŚCIWOŚCI VISUAL ---
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
                                // --------------------------------------------------------

                                // Logika porównywania 
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
                                    }
                                }
                                if (!warunekSpelniony) { spelniaWszystkie = false; break; }
                            }
                            if (spelniaWszystkie) znalezioneObiekty.Add(objId);
                        }
                        
                    }

                    // =========================================================
                    // MAGIA ŁĄCZENIA / ODEJMOWANIA ZAZNACZEŃ
                    // =========================================================
                    List<ObjectId> aktywne = AktywneZaznaczenie != null ? AktywneZaznaczenie.ToList() : new List<ObjectId>();
                    List<ObjectId> koncowe = new List<ObjectId>();

                    if (trybStr.Equals("Add", StringComparison.OrdinalIgnoreCase))
                    {
                        koncowe.AddRange(aktywne); // Kopiujemy obecne
                        foreach (var id in znalezioneObiekty) if (!koncowe.Contains(id)) koncowe.Add(id); // Dodajemy nowe
                    }
                    else if (trybStr.Equals("Remove", StringComparison.OrdinalIgnoreCase))
                    {
                        koncowe.AddRange(aktywne); // Kopiujemy obecne
                        foreach (var id in znalezioneObiekty) koncowe.Remove(id); // Usuwamy te, które model odnalazł do usunięcia
                    }
                    else // Domyślnie "New"
                    {
                        koncowe = znalezioneObiekty; // Nadpisuje całkowicie
                    }

                    if (koncowe.Count > 0)
                    {
                        // 1. Zabezpieczenie przez ewentualnymi duplikatami po łączeniu list
                        koncowe = koncowe.Distinct().ToList();

                        // 2. Podświetlamy obiekty na ekranie TYLKO jeśli nie są wewnątrz bloku!
                        if (!scopeStr.Equals("Blocks", StringComparison.OrdinalIgnoreCase))
                        {
                            try { ed.SetImpliedSelection(koncowe.ToArray()); }
                            catch { } // Ciche ignorowanie, jeśli CAD zabroni podświetlić jakiś ukryty obiekt
                        }
                        else
                        {
                            ed.WriteMessage("\n[Info]: Zapisano obiekty z wnętrza bloków do pamięci Agenta (brak podświetlenia na ekranie).");
                        }

                        // 3. Niezależnie od błędu graficznego, ZAPISUJEMY obiekty w pamięci C# do dalszej edycji!
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
                if (userMsg.ToLower() == "reset") { ResetujPamiec(doc); historiaRozmowy.Add("{\"role\": \"system\", \"content\": \"" + SafeJson(systemPrompt) + "\"}"); continue; }

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
                                        if (tool is MTextFormatTool mtextTool) wynikNarzedzia = mtextTool.Execute(doc, args);
                                        else if (tool is MTextEditTool mtextEditTool) wynikNarzedzia = mtextEditTool.Execute(doc, args);
                                        else if (tool is TextEditTool textEditTool) wynikNarzedzia = textEditTool.Execute(doc, args);
                                        else if (tool is AnalyzeSelectionTool analyzeTool) wynikNarzedzia = analyzeTool.Execute(doc, args);
                                        else if (tool is ReadTextSampleTool readTool) wynikNarzedzia = readTool.Execute(doc, args);
                                        else wynikNarzedzia = tool.Execute(doc);

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