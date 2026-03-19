using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad.Windows;
using System;
using System.Collections.Generic;
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

        // --- GLOBALNA PAMIĘĆ ZAZNACZENIA ---
        public static ObjectId[] OstatnieZaznaczenie = new ObjectId[0];
        private static bool isSelectionHooked = false;

        // --- Dwie bazy danych (Tiered Storage) ---
        private static Dictionary<string, string> bazaQuick = new Dictionary<string, string>();
        private static Dictionary<string, string> bazaFull = new Dictionary<string, string>();

        private List<ITool> tools = new List<ITool>

        {
        new MTextFormatTool(),
        new MTextEditTool()
        };

        private static PaletteSet oknoAgenta = null;
        private static Bricscad_AgentAI.AgentControl interfejsAgenta = null;

        private static string systemPrompt = "Jesteś autonomicznym Agentem Bielik w BricsCAD. Steruj programem ZA POMOCĄ TAGÓW. NIE JESTEŚ chatbotem do pisania kodu w markdown!\n\n" +
                                "Analizuj zadania w tagach <think>.\n\n" +
                                "MUSISZ odpowiedzieć jednym z 5 tagów:\n" +
                                "1. [SEARCH: Klasa] - ZAWSZE używaj tego, gdy nie znasz dokładnej nazwy właściwości! ZAKAZ ZGADYWANIA.\n" +
                                "2. [SELECT: {\"EntityType\": \"Klasa\", \"Conditions\": [{\"Property\": \"Prop\", \"Operator\": \"==|>|<|>=|<=|!=|Contains\", \"Value\": \"wartość\"}]}] - do zaznaczania (JSON bez enterów!).\n" +
                                "3. [LISP: (command \"_KOMENDA\" ...)] - do rysowania/edycji.\n" +
                                "4. [MSG: Twój tekst] - UŻYJ TEGO TAGU, gdy użytkownik zada Ci pytanie (np. 'dlaczego?').\n" +
                                "5. [ACTION:TAG_NARZEDZIA {\"Argumenty\": \"JSON\"}] - do uruchamiania narzędzi na zaznaczonych obiektach.\n\n" +
                                "--- ZASADY LISP (KRYTYCZNE): ---\n" +
                                "1. ZAWSZE dodawaj podkreślnik przed komendą: \"_LINE\", \"_CIRCLE\".\n" +
                                "2. Komenda LINE musi kończyć się pustym stringiem: (command \"_LINE\" p1 p2 \"\").\n\n" +
                                "--- DOSTĘPNE NARZĘDZIA (Użyj NAJPIERW [SELECT] aby zaznaczyć obiekty!): ---\n" +
                                "Tag: [ACTION:MTEXT_FORMAT]\n" +
                                "Opis: Zmienia formatowanie MText.\n" +
                                "Argumenty: {\"Mode\": \"HighlightWord\"|\"FormatAll\"|\"ClearFormatting\", \"Word\": \"słowo\" (tylko dla HighlightWord),\"Color\": nr_koloru (indeks ACI od 1 do 255, np. 1-czerwony, 2-żółty, 3-zielony, 79-jasnozielony, itd.), \"Bold\": true/false}\n\n" +
                                "Tag: [ACTION:MTEXT_EDIT]\n" +
                                "Opis: Dodaje lub zamienia tekst w MText.\n" +
                                "Argumenty: {\"Mode\": \"Append\"|\"Prepend\"|\"Replace\", \"Text\": \"tekst do dodania\", \"FindText\": \"szukany\" (tylko dla Replace), \"Color\": nr_koloru (np. 6 dla fioletu), \"Underline\": true/false}\n\n" +
                                "--- PRZYKŁADY ZACHOWANIA: ---\n" +
                                "User: Zaznacz linie dłuższe niż 50\n" +
                                "Bielik: [SELECT: {\"EntityType\": \"Line\", \"Conditions\": [{\"Property\": \"Length\", \"Operator\": \">\", \"Value\": 50}]}]\n" +
                                "User: Znajdź linie, które nie zaczynają się w 0,0,0\n" +
                                "Bielik: [SELECT: {\"EntityType\": \"Line\", \"Conditions\": [{\"Property\": \"StartPoint\", \"Operator\": \"!=\", \"Value\": \"(0,0,0)\"}]}]\n" +
                                "User: Zaznacz teksty z formatowaniem wewnętrznym\n" +
                                "Bielik: [SELECT: {\"EntityType\": \"MText\", \"Conditions\": [{\"Property\": \"Contents\", \"Operator\": \"Contains\", \"Value\": \";\"}]}]\n" +
                                "User: Zmień słowo PVC na czerwone w zaznaczonych tekstach\n" +
                                "Bielik: [ACTION:MTEXT_FORMAT {\"Mode\": \"HighlightWord\", \"Word\": \"PVC\", \"Color\": 1, \"Bold\": false}]\n\n" +
                                "ZROZUMIANO. BĘDĘ ODPOWIADAŁ TYLKO TAGAMI.";

        [CommandMethod("AGENT_UI")]
        public void UruchomInterfejsAgenta()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;

            // PODSŁUCH ZAZNACZENIA: Działa w tle i zapisuje wszystko co klikniesz!
            if (!isSelectionHooked)
            {
                doc.ImpliedSelectionChanged += (s, e) =>
                {
                    PromptSelectionResult res = doc.Editor.SelectImplied();
                    if (res.Status == PromptStatus.OK && res.Value != null && res.Value.Count > 0)
                    {
                        OstatnieZaznaczenie = res.Value.GetObjectIds();
                    }
                    // Zauważ: Jeśli wynik jest zerowy (bo np. kliknąłeś w czat), 
                    // celowo NIE CZYŚCIMY pamięci. Agent zachowuje "ostatnio widziane" obiekty.
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
        public static async Task<string> ZapytajAgentaAsync(string userMsg, Document doc, ObjectId[] przechwyconeZaznaczenie = null)
        {
            if (historiaRozmowy.Count == 0 || !historiaRozmowy[0].Contains("system"))
            {
                historiaRozmowy.Insert(0, "{\"role\": \"system\", \"content\": \"" + new Komendy().SafeJson(systemPrompt) + "\"}");
            }

            historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + new Komendy().SafeJson(userMsg) + "\"}");

            try
            {
                string jsonBody = "{\"model\": \"" + wybranyModel + "\", \"messages\": [" + string.Join(",", historiaRozmowy) + "], \"temperature\": 0.1, \"stream\": false}";
                var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await client.PostAsync("http://127.0.0.1:1234/v1/chat/completions", content);
                string aiMsg = new Komendy().WyciagnijContentZJson(await response.Content.ReadAsStringAsync());

                if (aiMsg.Contains("</think>")) aiMsg = aiMsg.Substring(aiMsg.LastIndexOf("</think>") + 8).Trim();

                if (!string.IsNullOrEmpty(aiMsg))
                {
                    historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + new Komendy().SafeJson(aiMsg) + "\"}");

                    using (DocumentLock docLock = doc.LockDocument())
                    {
                        // --- GENIALNY TRIK CZ. 2: Wstrzykujemy zaznaczenie z powrotem do BricsCADa! ---
                        if (przechwyconeZaznaczenie != null && przechwyconeZaznaczenie.Length > 0)
                        {
                            doc.Editor.SetImpliedSelection(przechwyconeZaznaczenie);
                        }

                        if (aiMsg.Contains("[SELECT:"))
                        {
                            int start = aiMsg.IndexOf("{", aiMsg.IndexOf("[SELECT:"));
                            int end = aiMsg.LastIndexOf("}");
                            if (start != -1 && end > start)
                            {
                                int zaznaczoneLiczba = new Komendy().WykonajInteligentneZaznaczenie(doc, aiMsg.Substring(start, end - start + 1));
                                return $"[Zaznaczono {zaznaczoneLiczba} obiektów]\n{aiMsg}";
                            }
                        }
                        else if (aiMsg.Contains("[ACTION:"))
                        {
                            foreach (var tool in new Komendy().tools)
                            {
                                // GENIALNY TRIK: Usuwamy zamykający nawias z nazwy narzędzia (czyli szukamy "[ACTION:MTEXT_FORMAT" zamiast "[ACTION:MTEXT_FORMAT]")
                                string bazaTagu = tool.ActionTag.Replace("]", "");

                                if (aiMsg.Contains(bazaTagu))
                                {
                                    string args = "";
                                    int startArgs = aiMsg.IndexOf("{", aiMsg.IndexOf(bazaTagu));
                                    int endArgs = aiMsg.LastIndexOf("}");

                                    if (startArgs != -1 && endArgs > startArgs)
                                    {
                                        args = aiMsg.Substring(startArgs, endArgs - startArgs + 1);
                                    }

                                    if (tool is MTextFormatTool mtextTool)
                                    {
                                        mtextTool.Execute(doc, args);
                                    }
                                    else
                                    {
                                        tool.Execute(doc);
                                    }

                                    return $"[Wykonano narzędzie {tool.ActionTag}]\n{aiMsg}";
                                }
                            }
                        }
                        // Jeśli model wysłał LISP (wymaga wywołania po zwolnieniu blokady)
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
                    } // Tu kończy się blokada dokumentu (Unlock)

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
        private string SafeJson(string text)
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

            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BricsCAD_Agent");
            string pathQuick = Path.Combine(folder, "BricsCAD_API_Quick.txt");
            string pathFull = Path.Combine(folder, "BricsCAD_API_V22.txt");

            int LoadFile(string path, Dictionary<string, string> dict)
            {
                if (!File.Exists(path)) return 0;
                try
                {
                    string content = File.ReadAllText(path);
                    // NOWY REGEX: Łapie klucz przed '|' i całą resztę aż do następnego klucza
                    var matches = Regex.Matches(content, @"([^|\r\n]+)\|(.*?)(?=(?:[^|\r\n]+\||$))",
                                  RegexOptions.Singleline | RegexOptions.IgnoreCase);

                    foreach (Match m in matches)
                    {
                        string klucz = m.Groups[1].Value.Trim().ToLower();
                        string wartosc = m.Groups[2].Value.Trim();
                        dict[klucz] = wartosc;
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
        private int WykonajInteligentneZaznaczenie(Document doc, string json)
        {
            Editor ed = doc.Editor;
            try
            {
                string entityTypeStr = Regex.Match(json, @"\""EntityType\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                var warunki = new List<(string Prop, string Op, string Val)>();

                MatchCollection matches = Regex.Matches(json, @"\""Property\""\s*:\s*\""([^\""]+)\"".*?\""Operator\""\s*:\s*\""([^\""]+)\"".*?\""Value\""\s*:\s*(\""[^\""]+\""|[^\s,}]+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match m in matches)
                {
                    warunki.Add((m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value.Trim('\"')));
                }

                if (string.IsNullOrEmpty(entityTypeStr)) return 0; // <-- POPRAWKA: return 0 zamiast return

                if (entityTypeStr.Equals("Clear", StringComparison.OrdinalIgnoreCase))
                {
                    ed.SetImpliedSelection(new ObjectId[0]);
                    ed.WriteMessage("\n[System]: Odznaczono obiekty.");
                    return 0; // <-- POPRAWKA: return 0 zamiast return
                }

                string[] typyDoSzukania = entityTypeStr.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                ed.WriteMessage($"\n[System]: Szukam '{string.Join("/", typyDoSzukania)}' (Warunki: {warunki.Count})...");

                List<ObjectId> znalezioneObiekty = new List<ObjectId>();
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                    foreach (ObjectId objId in btr)
                    {
                        Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;
                        string nazwaTypuEnt = ent.GetType().Name;
                        bool typPasuje = false;
                        foreach (var t in typyDoSzukania)
                        {
                            string szukanyTyp = t.Trim();
                            if (szukanyTyp.Equals("Text", StringComparison.OrdinalIgnoreCase) && ent is DBText) { typPasuje = true; break; }
                            if (szukanyTyp.Equals("Dimension", StringComparison.OrdinalIgnoreCase) && ent is Dimension) { typPasuje = true; break; }
                            if (nazwaTypuEnt.Equals(szukanyTyp, StringComparison.OrdinalIgnoreCase)) { typPasuje = true; break; }
                        }
                        if (!typPasuje) continue;

                        bool spelniaWszystkie = true;
                        foreach (var warunek in warunki)
                        {
                            string rzeczywistaWlasciwosc = warunek.Prop;
                            if (ent is MText && rzeczywistaWlasciwosc.Equals("Height", StringComparison.OrdinalIgnoreCase)) rzeczywistaWlasciwosc = "TextHeight";
                            else if (ent is Dimension && (rzeczywistaWlasciwosc.Equals("Height", StringComparison.OrdinalIgnoreCase) || rzeczywistaWlasciwosc.Equals("TextHeight", StringComparison.OrdinalIgnoreCase))) rzeczywistaWlasciwosc = "Dimtxt";

                            System.Reflection.PropertyInfo propInfo = ent.GetType().GetProperty(rzeczywistaWlasciwosc, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                            if (propInfo == null) { spelniaWszystkie = false; break; }
                            object wartoscObiektu = propInfo.GetValue(ent);
                            if (wartoscObiektu == null) { spelniaWszystkie = false; break; }

                            string valStr = wartoscObiektu.ToString();

                            // --- TŁUMACZ ZŁOŻONYCH OBIEKTÓW CAD ---
                            if (wartoscObiektu is Teigha.Colors.Transparency transp)
                            {
                                if (transp.IsByAlpha)
                                {
                                    double procent = (255.0 - transp.Alpha) / 255.0 * 100.0;
                                    valStr = Math.Round(procent).ToString();
                                }
                                else
                                {
                                    valStr = "0";
                                }
                            }
                            else if (wartoscObiektu is Teigha.Geometry.Point3d pt)
                            {
                                valStr = $"({Math.Round(pt.X, 4)},{Math.Round(pt.Y, 4)},{Math.Round(pt.Z, 4)})".Replace(".0000", "").Replace(",0)", ",0,0)");
                            }
                            // ----------------------------------------

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
                    tr.Commit();
                }

                if (znalezioneObiekty.Count > 0)
                {
                    ed.SetImpliedSelection(znalezioneObiekty.ToArray());
                    OstatnieZaznaczenie = znalezioneObiekty.ToArray(); // <-- ZAPIS DLA AI
                    ed.WriteMessage($"\n[Sukces]: Zaznaczono {znalezioneObiekty.Count} obiekt(ów)!");
                    return znalezioneObiekty.Count;
                }
                else
                {
                    ed.WriteMessage("\n[System]: Nie znaleziono obiektów spełniających kryteria.");
                    ed.SetImpliedSelection(new ObjectId[0]);
                    OstatnieZaznaczenie = new ObjectId[0]; // <-- CZYSZCZENIE PAMIĘCI
                    return 0;
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n[Błąd Zaznaczania C#]: {ex.Message}");
                return 0; // <-- POPRAWKA: Zabezpieczenie bloku catch (CS0161)
            }
        }

        // --- GŁÓWNA PĘTLA AGENTA ---
        [CommandMethod("AGENT_START")]
        public void UruchomAgenta()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            historiaRozmowy.Clear();
            WczytajPamiec(doc);
            WczytajBazyWiedzy(ed); // Poprawione wywołanie
                    
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
                            // --- LOGIKA SEARCH (Poprawiona) ---
                            if (aiMsg.Contains("[SEARCH:"))
                            {
                                // 1. NAJPIERW zapisujemy to, co powiedział Agent (żeby nie miał luki w pamięci)
                                historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + SafeJson(aiMsg) + "\"}");

                                // 2. Zwiększamy licznik
                                licznikWyszukiwan++;

                                // 3. Sprawdzamy bezpiecznik
                                if (licznikWyszukiwan > 2)
                                {
                                    ed.WriteMessage("\n[System]: Przerwano nieskończoną pętlę wyszukiwania Agenta.");
                                    historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"[SYSTEM]: Przekroczono limit wyszukiwań. ZAKAZ wyszukiwania. Użyj tego co już wiesz lub zapytaj użytkownika przez [MSG].\"}");

                                    agentPotrzebujeDanych = true;
                                    continue; // Przerywamy TEN obieg, Agent wymyśla nową odpowiedź
                                }

                                // 4. Jeśli bezpiecznik nie wybił, normalnie szukamy w bazie
                                string szukanaKlasa = aiMsg.Substring(aiMsg.IndexOf("[SEARCH:") + 8).Split(']')[0].Trim().ToLower();
                                string wynik = "";

                                if (bazaQuick.ContainsKey(szukanaKlasa)) wynik = bazaQuick[szukanaKlasa];
                                else if (bazaFull.ContainsKey(szukanaKlasa)) wynik = bazaFull[szukanaKlasa];
                                else wynik = "Brak definicji.";

                                // 5. Odsyłamy wynik z bazy
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

                                // --- MAGIA ŁAŃCUCHOWANIA ZADAŃ (CHAINING) ---
                                // Informujemy Agenta o wyniku i zmuszamy go do kontynuowania myślenia!
                                string sysOdp = $"[SYSTEM]: Pomyślnie zaznaczono {zaznaczoneLiczba} obiekt(ów). Jeśli to koniec zadania, odpowiedz [MSG: Gotowe]. Jeśli miałeś w planach użyć narzędzia [ACTION], wygeneruj je TERAZ.";
                                historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + SafeJson(sysOdp) + "\"}");

                                agentPotrzebujeDanych = true; // NIE PRZERYWAMY PĘTLI! Agent działa dalej.
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
                        else if (aiMsg.Contains("[ACTION:"))
                        {
                            ed.WriteMessage("\n[Agent AI Używa Narzędzia]: " + aiMsg);
                            historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + SafeJson(aiMsg) + "\"}");

                            // Szukamy odpowiedniego narzędzia na liście
                            foreach (var tool in tools)
                            {
                                if (aiMsg.Contains(tool.ActionTag))
                                {
                                    // Wyciągamy argumenty JSON (jeśli są)
                                    string args = "";
                                    int startArgs = aiMsg.IndexOf("{", aiMsg.IndexOf(tool.ActionTag));
                                    int endArgs = aiMsg.LastIndexOf("}");
                                    if (startArgs != -1 && endArgs > startArgs)
                                    {
                                        args = aiMsg.Substring(startArgs, endArgs - startArgs + 1);
                                    }

                                    // Uruchamiamy narzędzie! (zakładam, że zaktualizowałeś ITool o metodę z dwoma parametrami)
                                    // Jeśli nie, rzutujemy na nasz konkretny typ:
                                    if (tool is MTextFormatTool mtextTool)
                                    {
                                        mtextTool.Execute(doc, args);
                                    }
                                    else
                                    {
                                        tool.Execute(doc);
                                    }
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