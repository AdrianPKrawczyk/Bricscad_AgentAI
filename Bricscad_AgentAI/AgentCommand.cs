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
                ed.WriteMessage("\n[System]: Plik bazy wiedzy nie istnieje. Uruchom skrypt Pająka w Pythonie.");
                return;
            }

            try
            {
                string[] linie = File.ReadAllLines(path);
                foreach (string linia in linie)
                {
                    if (string.IsNullOrWhiteSpace(linia) || !linia.Contains("|")) continue;
                    string[] podzial = linia.Split(new char[] { '|' }, 2);
                    bazaWiedzyAPI[podzial[0].Trim().ToLower()] = podzial[1].Trim();
                }
                ed.WriteMessage($"\n[System]: Załadowano do pamięci {bazaWiedzyAPI.Count} klas BricsCAD API.");
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

                MatchCollection matches = Regex.Matches(json, @"\""Property\""\s*:\s*\""([^\""]+)\"".*?\""Operator\""\s*:\s*\""([^\""]+)\"".*?\""Value\""\s*:\s*(\""[^\""]+\""|[^\s,}]+)");
                foreach (Match m in matches)
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
                            "Analizuj zadania w tagach <think>. Przetłumacz polecenie użytkownika na angielskie nazwy klas BricsCAD API (np. elipsa -> Ellipse, wymiar -> Dimension).\n" +
                            "Masz do dyspozycji DWA KROKI, jeśli użytkownik prosi o wyszukiwanie/zaznaczanie:\n" +
                            "KROK 1: Jeśli nie znasz właściwości dla danej klasy, wygeneruj TYLKO tag [SEARCH: NazwaKlasyPoAngielsku]. Nie dodawaj po nim ŻADNEGO tekstu. System zwróci Ci dokumentację.\n" +
                            "KROK 2: Po otrzymaniu dokumentacji, wygeneruj tag [SELECT: JSON].\n" +
                            "Zasady [SELECT: ...]:\n" +
                            "- EntityType: Podaj klasę API obiektu.\n" +
                            "- Conditions: Używaj TYLKO właściwości otrzymanych z [SEARCH]. Podaj Property, Operator (==, >, <, >=, <=, Contains) i Value.\n" +
                            "- WSZYSTKIE obiekty: zostaw Conditions puste: []\n" +
                            "- ODZNACZANIE: [SELECT: {\"EntityType\": \"Clear\", \"Conditions\": []}]";

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

                                int start = aiMsg.IndexOf("[SELECT:") + 8;
                                int end = aiMsg.LastIndexOf("]");
                                if (end > start)
                                {
                                    string jsonStr = aiMsg.Substring(start, end - start).Trim();
                                    WykonajInteligentneZaznaczenie(doc, jsonStr);
                                    ZapiszPamiec(doc);
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
    }
}