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
    public class Komendy
    {
        // Czekamy do 5 minut na myślenie AI (Think)
        private static readonly HttpClient client = new HttpClient() { Timeout = TimeSpan.FromMinutes(5) };
        private static List<string> historiaRozmowy = new List<string>();
        private static string wybranyModel = "qwen3.5-9b-instruct";

        private List<ITool> tools = new List<ITool> {
            new ColorRedTool(),
            new SelectLinesTool()
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

            // Bezpieczna zmiana rozszerzenia, niezależnie czy to .dwg, .DWG czy .dxf
            string dir = Path.GetDirectoryName(docPath);
            string nameNoExt = Path.GetFileNameWithoutExtension(docPath);
            return Path.Combine(dir, nameNoExt + "_ai_memory.json");
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

        [CommandMethod("AGENT_START")]
        public void UruchomAgenta()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            historiaRozmowy.Clear();
            WczytajPamiec(doc);

            string systemPrompt = "Jesteś Bielik, asystent inżyniera. Odpowiadaj krótko. " +
                "Analizuj zadania w tagach <think>. " +
                "RYSOWANIE: używaj [LISP: kod].\n" +
                "ZAZNACZANIE/WYSZUKIWANIE: używaj [SELECT: JSON].\n" +
                "Zasady dla [SELECT: ...]:\n" +
                "- EntityType: Typ obiektu API (np. Line, Polyline, MText, Text, Circle). Możesz podać kilka po przecinku, np. \"Text, MText\".\n" +
                "- Conditions: Używaj Property (np. Length, Height, Radius), Operator (==, >, <, >=, <=, Contains) i Value.\n" +
                "- Jeśli użytkownik prosi o wysokość tekstu, ZAWSZE używaj właściwości \"Height\".\n" +
                "- Jeśli użytkownik chce WSZYSTKIE obiekty z danego typu, zostaw Conditions puste: []\n" +
                "- Aby ODZNACZYĆ, użyj: [SELECT: {\"EntityType\": \"Clear\", \"Conditions\": []}]";

            if (historiaRozmowy.Count == 0 || !historiaRozmowy[0].Contains("system"))
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
                if (userMsg.ToLower() == "reset") { ResetujPamiec(doc); break; }

                ed.WriteMessage("\nBielik myśli...");

                try
                {
                    historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + SafeJson(userMsg) + "\"}");
                    string jsonBody = "{\"model\": \"" + wybranyModel + "\", \"messages\": [" + string.Join(",", historiaRozmowy) + "], \"temperature\": 0.1, \"stream\": false}";

                    var response = client.PostAsync("http://127.0.0.1:1234/v1/chat/completions", new StringContent(jsonBody, Encoding.UTF8, "application/json")).Result;
                    string rawContent = WyciagnijContentZJson(response.Content.ReadAsStringAsync().Result);

                    // --- CZYSTY FILTR ODPOWIEDZI ---
                    string aiMsg = rawContent;
                    if (aiMsg.Contains("</think>"))
                        aiMsg = aiMsg.Substring(aiMsg.LastIndexOf("</think>") + 8).Trim();

                    if (!string.IsNullOrEmpty(aiMsg))
                    {
                        ed.WriteMessage("\n[Agent AI]: " + aiMsg);
                        historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + SafeJson(aiMsg) + "\"}");
                        ZapiszPamiec(doc);

                        // --- SEJF KOMEND LISP ---
                        if (aiMsg.Contains("[LISP:"))
                        {
                            int start = aiMsg.IndexOf("[LISP:") + 6;
                            int end = aiMsg.IndexOf("]", start);
                            if (end > start)
                            {
                                string lisp = aiMsg.Substring(start, end - start).Trim();
                                // Czyścimy ewentualne znaki markdown (np. ` )
                                lisp = lisp.Replace("`", "");

                                ed.WriteMessage("\n[System]: Wykonywanie kodu AutoLISP...");
                                doc.SendStringToExecute(lisp + "\n", true, false, false);
                                doc.SendStringToExecute("AGENT_START\n", true, false, false);
                                return;
                            }
                        }
                        // --- INTELIGENTNE ZAZNACZANIE (REFLEKSJA) ---
                        else if (aiMsg.Contains("[SELECT:"))
                        {
                            int start = aiMsg.IndexOf("[SELECT:") + 8;
                            int end = aiMsg.LastIndexOf("]");
                            if (end > start)
                            {
                                string jsonStr = aiMsg.Substring(start, end - start).Trim();
                                WykonajInteligentneZaznaczenie(doc, jsonStr);
                                ZapiszPamiec(doc);
                            }
                        }
                        else
                        {
                            ZapiszPamiec(doc);
                        }
                    }
                }
                catch (System.Exception ex) { ed.WriteMessage("\n[Błąd]: " + ex.Message); }
            }
        }
        private void WykonajInteligentneZaznaczenie(Document doc, string json)
        {
            Editor ed = doc.Editor;
            try
            {
                string entityTypeStr = Regex.Match(json, @"\""EntityType\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                var warunki = new List<(string Prop, string Op, string Val)>();

                MatchCollection matches = Regex.Matches(json, @"\{\s*\""Property\""\s*:\s*\""([^\""]+)\""\s*,\s*\""Operator\""\s*:\s*\""([^\""]+)\""\s*,\s*\""Value\""\s*:\s*\""([^\""]+)\""\s*\}");
                foreach (Match m in matches)
                {
                    warunki.Add((m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value));
                }

                if (string.IsNullOrEmpty(entityTypeStr)) return;

                if (entityTypeStr.Equals("Clear", StringComparison.OrdinalIgnoreCase))
                {
                    ed.SetImpliedSelection(new ObjectId[0]);
                    ed.WriteMessage("\n[System]: Odznaczono obiekty.");
                    return;
                }

                // Rozdzielamy typy, jeśli Agent podał kilka (np. "Text, MText")
                string[] typyDoSzukania = entityTypeStr.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                ed.WriteMessage($"\n[System]: Szukam obiektów typu '{string.Join(" / ", typyDoSzukania)}' (Warunki: {warunki.Count})...");

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
                            if (nazwaTypuEnt.Equals(t, StringComparison.OrdinalIgnoreCase)) { typPasuje = true; break; }
                        }
                        if (!typPasuje) continue;

                        bool spelniaWszystkie = true;

                        if (warunki.Count > 0)
                        {
                            foreach (var warunek in warunki)
                            {
                                PropertyInfo propInfo = ent.GetType().GetProperty(warunek.Prop, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                                // ALIAS: Jeśli szukamy "Height" ale obiekt to "MText", użyj "TextHeight"
                                if (propInfo == null && warunek.Prop.Equals("Height", StringComparison.OrdinalIgnoreCase))
                                {
                                    propInfo = ent.GetType().GetProperty("TextHeight", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                                }

                                if (propInfo == null) { spelniaWszystkie = false; break; }

                                object wartoscObiektu = propInfo.GetValue(ent);
                                if (wartoscObiektu == null) { spelniaWszystkie = false; break; }

                                string valStr = wartoscObiektu.ToString();
                                bool warunekSpelniony = false;

                                if (double.TryParse(valStr, out double valNum) && double.TryParse(warunek.Val.Replace(".", ","), out double warNum))
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
                    else if (json[i] == '\\')
                    {
                        escaped = true;
                    }
                    else if (json[i] == '\"')
                    {
                        break;
                    }
                    else
                    {
                        sb.Append(json[i]);
                    }
                }
                return sb.ToString();
            }
            catch { return "Błąd parsowania odpowiedzi."; }
        }
    }
}