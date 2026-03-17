using System;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Reflection;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Teigha.Runtime;
using Teigha.DatabaseServices;

namespace BricsCAD_Agent
{
    // UWAGA: Zakładam, że interfejs ITool i klasy ColorRedTool, SelectLinesTool masz zdefiniowane w innym pliku.
    public class Komendy
    {
        // Czekamy do 5 minut na myślenie AI (Think)
        private static readonly HttpClient client = new HttpClient() { Timeout = TimeSpan.FromMinutes(5) };
        private static List<string> historiaRozmowy = new List<string>();
        private static string wybranyModel = "qwen3.5-9b-instruct";

        // --- NOWE: Baza wiedzy wyciągnięta przez Pająka w Pythonie ---
        private static Dictionary<string, string> bazaWiedzyAPI = new Dictionary<string, string>();

        private List<ITool> tools = new List<ITool>
        {
            // new ColorRedTool(),
            // new SelectLinesTool()
        };

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

        // ==========================================
        // ZARZĄDZANIE PAMIĘCIĄ I ŚCIEŻKAMI
        // ==========================================

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

            string name = Path.GetFileName(docPath);
            if (name.StartsWith("Drawing", StringComparison.OrdinalIgnoreCase)) return null;

            string dir = Path.GetDirectoryName(docPath);
            string nameNoExt = Path.GetFileNameWithoutExtension(docPath);
            return Path.Combine(dir, nameNoExt + "_ai_memory.json");
        }

        private string GetBazaWiedzyPath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BricsCAD_Agent");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "BricsCAD_API_V22.txt");
        }

        private void WczytajBazeWiedzy(Editor ed)
        {
            string path = GetBazaWiedzyPath();
            bazaWiedzyAPI.Clear();

            if (!File.Exists(path))
            {
                ed.WriteMessage("\n[System]: Plik bazy wiedzy nie istnieje.");
                return;
            }

            try
            {
                // Czytamy CAŁY sklejony tekst na raz
                string calyTekst = File.ReadAllText(path);

                // Magiczny Regex: Wyciąga klucz i wartość ignorując braki enterów
                MatchCollection matches = Regex.Matches(calyTekst, @"([a-zA-Z0-9_]+)\|Klasa API:\s*(.*?)(?=(?:[a-zA-Z0-9_]+\|Klasa API:|$))", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match m in matches)
                {
                    string klucz = m.Groups[1].Value.Trim().ToLower();
                    string wartosc = "Klasa API: " + m.Groups[2].Value.Trim();
                    bazaWiedzyAPI[klucz] = wartosc;
                }

                ed.WriteMessage($"\n[System]: Załadowano do pamięci {bazaWiedzyAPI.Count} zoptymalizowanych klas BricsCAD API.");
            }
            catch { ed.WriteMessage("\n[System]: Błąd odczytu bazy wiedzy API."); }
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

        // ==========================================
        // PARSOWANIE I LM STUDIO
        // ==========================================

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

        // ==========================================
        // SILNIK ZAZNACZANIA REFLEKSYJNEGO
        // ==========================================

        private void WykonajInteligentneZaznaczenie(Document doc, string json)
        {
            Editor ed = doc.Editor;
            try
            {
                string entityTypeStr = Regex.Match(json, @"\""EntityType\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                var warunki = new List<(string Prop, string Op, string Val)>();

                MatchCollection matches = Regex.Matches(json, @"\""Property\""\s*:\s*\""([^\""]+)\"".*?\""Operator\""\s*:\s*\""([^\""]+)\"".*?\""Value\""\s*:\s*(\""[^\""]+\""|[^\s,}]+)", RegexOptions.Singleline | RegexOptions.IgnoreCase); foreach (Match m in matches)
                {
                    warunki.Add((m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value.Trim('\"')));
                }

                if (string.IsNullOrEmpty(entityTypeStr)) return;

                if (entityTypeStr.Equals("Clear", StringComparison.OrdinalIgnoreCase))
                {
                    ed.SetImpliedSelection(new ObjectId[0]);
                    ed.WriteMessage("\n[System]: Odznaczono obiekty.");
                    return;
                }

                string[] typyDoSzukania = entityTypeStr.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                ed.WriteMessage($"\n[System]: Szukam obiektów '{string.Join(" / ", typyDoSzukania)}' (Warunków: {warunki.Count})...");

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

                        if (warunki.Count > 0)
                        {
                            foreach (var warunek in warunki)
                            {
                                string rzeczywistaWlasciwosc = warunek.Prop;

                                if (ent is MText && rzeczywistaWlasciwosc.Equals("Height", StringComparison.OrdinalIgnoreCase))
                                {
                                    rzeczywistaWlasciwosc = "TextHeight";
                                }
                                else if (ent is Dimension && (rzeczywistaWlasciwosc.Equals("Height", StringComparison.OrdinalIgnoreCase) || rzeczywistaWlasciwosc.Equals("TextHeight", StringComparison.OrdinalIgnoreCase)))
                                {
                                    rzeczywistaWlasciwosc = "Dimtxt";
                                }

                                PropertyInfo propInfo = ent.GetType().GetProperty(rzeczywistaWlasciwosc, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                                if (propInfo == null) { spelniaWszystkie = false; break; }

                                object wartoscObiektu = propInfo.GetValue(ent);
                                if (wartoscObiektu == null) { spelniaWszystkie = false; break; }

                                string valStr = wartoscObiektu.ToString();
                                bool warunekSpelniony = false;

                                if (double.TryParse(valStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double valNum) &&
                                    double.TryParse(warunek.Val.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double warNum))
                                {
                                    switch (warunek.Op)
                                    {
                                        case "==": warunekSpelniony = Math.Abs(valNum - warNum) < 0.0001; break;
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
                                        case "contains": warunekSpelniony = valStr.IndexOf(warunek.Val, StringComparison.OrdinalIgnoreCase) >= 0; break;
                                    }
                                }

                                if (!warunekSpelniony) { spelniaWszystkie = false; break; }
                            }
                        }

                        if (spelniaWszystkie) znalezioneObiekty.Add(objId);
                    }
                    tr.Commit();
                }

                if (znalezioneObiekty.Count > 0)
                {
                    ed.SetImpliedSelection(znalezioneObiekty.ToArray());
                    ed.WriteMessage($"\n[Sukces]: Zaznaczono {znalezioneObiekty.Count} obiekt(ów)!");
                }
                else
                {
                    ed.WriteMessage("\n[System]: Nie znaleziono obiektów spełniających kryteria.");
                    ed.SetImpliedSelection(new ObjectId[0]);
                }
            }
            catch (System.Exception ex) { ed.WriteMessage($"\n[Błąd Zaznaczania C#]: {ex.Message}"); }
        }

        // ==========================================
        // GŁÓWNA PĘTLA AGENTA
        // ==========================================

        [CommandMethod("AGENT_START")]
        public void UruchomAgenta()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            historiaRozmowy.Clear();
            WczytajPamiec(doc);
            WczytajBazeWiedzy(ed); // Ładujemy wiedzę od Pająka!

            string systemPrompt = "Jesteś asystentem Bielik w BricsCAD. Odpowiadaj krótko. " +
                                    "Analizuj zadania w tagach <think>.\n\n" +
                                    "--- NARZĘDZIE RYSOWANIA [LISP: ...] ---\n" +
                                    "Jeśli użytkownik chce coś narysować lub zmienić, UŻYJ: [LISP: (command \"_KOMENDA\" ...) ].\n" +
                                    "ZASADY LISP:\n" +
                                    "1. ZAWSZE dodawaj podkreślnik przed komendą, np. \"_LINE\", \"_CIRCLE\", \"_MOVE\".\n" +
                                    "2. Komenda LINE musi być zakończona pustym stringiem, np: (command \"_LINE\" p1 p2 \"\").\n" +
                                    "3. Używaj współrzędnych jako stringów \"x,y\" lub list (list x y).\n\n" +
                                    "--- NARZĘDZIE WIEDZY [SEARCH: ...] i [SELECT: ...] ---\n" +
                                    "Jeśli nie znasz właściwości obiektu do zaznaczania, użyj [SEARCH: Klasa]. " +
                                    "Potem wygeneruj [SELECT: JSON_w_jednej_linii].\n\n" +
                                    "Pamiętaj: Możesz rysować i edytować WSZYSTKO przez [LISP]. Nigdy nie mów, że nie masz uprawnień.";

            if (historiaRozmowy.Count == 0 || !historiaRozmowy[0].Contains("KROK 1"))
            {
                historiaRozmowy.Insert(0, "{\"role\": \"system\", \"content\": \"" + SafeJson(systemPrompt) + "\"}");
                ed.WriteMessage($"\n--- Agent Bielik gotowy (Model: {wybranyModel}) ---");
            }

            while (true)
            {
                PromptStringOptions pso = new PromptStringOptions("\nCo robimy, Adrianie? (exit/reset/modele): ");
                pso.AllowSpaces = true;
                PromptResult pr = ed.GetString(pso);
                if (pr.Status != PromptStatus.OK) break;
                string userMsg = pr.StringResult.Trim();

                if (string.IsNullOrWhiteSpace(userMsg)) continue;
                if (userMsg.ToLower() == "exit") break;
                if (userMsg.ToLower() == "modele") { WybierzModela(); continue; }
                if (userMsg.ToLower() == "reset")
                {
                    ResetujPamiec(doc);
                    historiaRozmowy.Add("{\"role\": \"system\", \"content\": \"" + SafeJson(systemPrompt) + "\"}");
                    continue;
                }

                try
                {
                    historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + SafeJson(userMsg) + "\"}");

                    bool agentPotrzebujeDanych = true;

                    while (agentPotrzebujeDanych)
                    {
                        agentPotrzebujeDanych = false;
                        ed.WriteMessage("\nBielik myśli...");

                        string jsonBody = "{\"model\": \"" + wybranyModel + "\", \"messages\": [" + string.Join(",", historiaRozmowy) + "], \"temperature\": 0.1, \"stream\": false}";

                        var response = client.PostAsync("http://127.0.0.1:1234/v1/chat/completions", new StringContent(jsonBody, Encoding.UTF8, "application/json")).Result;
                        string rawContent = WyciagnijContentZJson(response.Content.ReadAsStringAsync().Result);

                        string aiMsg = rawContent;
                        if (aiMsg.Contains("</think>"))
                            aiMsg = aiMsg.Substring(aiMsg.LastIndexOf("</think>") + 8).Trim();

                        if (!string.IsNullOrEmpty(aiMsg))
                        {
                            // --- GILOTYNA KONTEKSTOWA ---
                            // Zamiast zapisywać całą odpowiedź (z potencjalną halucynacją), najpierw sprawdzamy co to jest!

                            if (aiMsg.Contains("[SEARCH:"))
                            {
                                int startSearch = aiMsg.IndexOf("[SEARCH:") + 8;
                                int endSearch = aiMsg.IndexOf("]", startSearch);
                                if (endSearch > startSearch)
                                {
                                    string szukanaKlasa = aiMsg.Substring(startSearch, endSearch - startSearch).Trim();

                                    // Zapisujemy do pamięci TYLKO sam tag. Odcinamy ewentualny bełkot wygenerowany po nim!
                                    historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + SafeJson($"[SEARCH: {szukanaKlasa}]") + "\"}");

                                    ed.WriteMessage($"\n[System]: Agent przeszukuje API dla hasła '{szukanaKlasa.ToLower()}'...");

                                    string wynikWyszukiwania = "Brak informacji w dokumentacji dla tej klasy.";
                                    if (bazaWiedzyAPI.ContainsKey(szukanaKlasa.ToLower()))
                                    {
                                        wynikWyszukiwania = bazaWiedzyAPI[szukanaKlasa.ToLower()];
                                    }

                                    string odpowiedzSystemu = $"[WYNIK DOKUMENTACJI API]: {wynikWyszukiwania} -> Wygeneruj komendę [SELECT: ...] bazując na tej wiedzy.";
                                    historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + SafeJson(odpowiedzSystemu) + "\"}");

                                    agentPotrzebujeDanych = true;
                                }
                            }
                            else if (aiMsg.Contains("[SELECT:"))
                            {
                                ed.WriteMessage("\n[Agent AI]: " + aiMsg);
                                historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + SafeJson(aiMsg) + "\"}");

                                // Kuloodporne wyciąganie JSON-a niezależnie od enterów
                                int startTag = aiMsg.IndexOf("[SELECT:");
                                int startJson = aiMsg.IndexOf("{", startTag);
                                int endJson = aiMsg.LastIndexOf("}");

                                if (startJson != -1 && endJson > startJson)
                                {
                                    string jsonStr = aiMsg.Substring(startJson, endJson - startJson + 1);
                                    WykonajInteligentneZaznaczenie(doc, jsonStr);
                                    ZapiszPamiec(doc);
                                }
                                else
                                {
                                    // Fallback np. dla samego [SELECT: Clear]
                                    int start = startTag + 8;
                                    int end = aiMsg.IndexOf("]", start);
                                    if (end > start)
                                    {
                                        WykonajInteligentneZaznaczenie(doc, aiMsg.Substring(start, end - start));
                                        ZapiszPamiec(doc);
                                    }
                                }
                            }
                            else if (aiMsg.Contains("[LISP:"))
                            {
                                ed.WriteMessage("\n[Agent AI]: " + aiMsg);
                                historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + SafeJson(aiMsg) + "\"}");

                                int start = aiMsg.IndexOf("[LISP:") + 6;
                                int end = aiMsg.IndexOf("]", start);
                                if (end > start)
                                {
                                    string lisp = aiMsg.Substring(start, end - start).Trim().Replace("`", "");
                                    ed.WriteMessage("\n[System]: Wykonywanie kodu AutoLISP...");
                                    ed.WriteMessage("\n[Debug LISP]: " + lisp); // To pokaże Ci w konsoli, co dokładnie AI wysyła do BricsCADa
                                    doc.SendStringToExecute(lisp + "\n", true, false, false);
                                    doc.SendStringToExecute("AGENT_START\n", true, false, false);
                                    return;
                                }
                            }
                            else
                            {
                                // Zwykła rozmowa
                                ed.WriteMessage("\n[Agent AI]: " + aiMsg);
                                historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + SafeJson(aiMsg) + "\"}");
                                ZapiszPamiec(doc);
                            }
                        }
                    }
                }
                catch (System.Exception ex) { ed.WriteMessage("\n[Błąd komunikacji]: " + ex.Message); }
            }
        }

        [CommandMethod("AGENT_CHECK_DB")]
        public void SprawdzBazeWiedzy()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // Upewniamy się, że baza jest załadowana
            if (bazaWiedzyAPI.Count == 0)
            {
                WczytajBazeWiedzy(ed);
            }

            ed.WriteMessage($"\n--- TESTER BAZY WIEDZY API ({bazaWiedzyAPI.Count} wczytanych klas) ---");

            PromptStringOptions pso = new PromptStringOptions("\nPodaj nazwę klasy do wyszukania (np. circle, mtext, dimension) lub 'exit': ");
            pso.AllowSpaces = false;

            while (true)
            {
                PromptResult pr = ed.GetString(pso);
                if (pr.Status != PromptStatus.OK) break;

                string szukane = pr.StringResult.Trim().ToLower();
                if (szukane == "exit") break;

                if (bazaWiedzyAPI.ContainsKey(szukane))
                {
                    ed.WriteMessage($"\n\n--- ZNALEZIONO KLASĘ: {szukane.ToUpper()} ---");

                    string tresc = bazaWiedzyAPI[szukane];

                    // Formatujemy "sklejkę" na ładny i czytelny wygląd dla inżyniera
                    tresc = tresc.Replace("Właściwości (Properties):", "\n[WŁAŚCIWOŚCI]:\n  - ");
                    tresc = tresc.Replace(", ", "\n  - ");
                    tresc = tresc.Replace("Opis:", "\n\n[OPIS]:");

                    ed.WriteMessage($"\n{tresc}\n");
                }
                else
                {
                    ed.WriteMessage($"\n[Brak wyników]: Nie znaleziono klucza '{szukane}' w załadowanej bazie do RAM.");
                }
            }
        }
    }
}