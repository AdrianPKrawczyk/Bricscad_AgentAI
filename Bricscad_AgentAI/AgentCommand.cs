using System;
using System.Net.Http;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Teigha.Runtime;

namespace BricsCAD_Agent
{
    public class Komendy
    {
        private static readonly HttpClient client = new HttpClient();
        private static List<string> historiaRozmowy = new List<string>();

        // Domyślny model - możesz go zmienić komendą AGENT_MODELS
        private static string wybranyModel = "qwen3.5-9b-instruct";

        // Rejestr dostępnych narzędzi CAD
        private List<ITool> tools = new List<ITool> {
            new ColorRedTool(),
            new SelectLinesTool()
        };

        // --- ŚCIEŻKI PAMIĘCI ---
        private string GetGlobalPath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BricsCAD_Agent");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return Path.Combine(folder, "global_memory.json");
        }

        private string GetLocalPath(Document doc)
        {
            string docPath = doc.Database.Filename;
            if (string.IsNullOrEmpty(docPath) || docPath.StartsWith("Drawing", StringComparison.OrdinalIgnoreCase)) return null;
            return docPath.Replace(".dwg", "_ai_memory.json");
        }

        // --- KOMENDA: WYBÓR MODELU ---
        [CommandMethod("AGENT_MODELS")]
        public void WybierzModela()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            try
            {
                ed.WriteMessage("\nSprawdzam dostępne modele w LM Studio...");
                var response = client.GetAsync("http://127.0.0.1:1234/v1/models").Result;
                string json = response.Content.ReadAsStringAsync().Result;

                List<string> modele = ParsujListeModeli(json);
                if (modele.Count == 0)
                {
                    ed.WriteMessage("\n[Błąd]: Brak załadowanych modeli w LM Studio.");
                    return;
                }

                PromptKeywordOptions pko = new PromptKeywordOptions("\nWybierz model AI do testów:");
                foreach (string m in modele) pko.Keywords.Add(m);

                pko.AllowNone = true;
                PromptResult pr = ed.GetKeywords(pko);

                if (pr.Status == PromptStatus.OK)
                {
                    wybranyModel = pr.StringResult;
                    ed.WriteMessage($"\n[System]: Aktywny model ustawiony na: **{wybranyModel}**");
                }
            }
            catch
            {
                ed.WriteMessage("\n[Błąd]: Nie można pobrać listy. Upewnij się, że LM Studio działa na porcie 1234.");
            }
        }

        // --- KOMENDA GŁÓWNA: START AGENTA ---
        [CommandMethod("AGENT_START")]
        public void UruchomAgenta()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            historiaRozmowy.Clear();
            WczytajPamiec(doc);

            string listaNarzedzi = string.Join(", ", tools.Select(t => $"{t.Description} ({t.ActionTag})"));
            string systemPrompt = "Jesteś Bielik, inteligentny asystent Adriana w BricsCAD. " +
                                  "Odpowiadaj po polsku, krótko i rzeczowo. Używaj imienia Adrian. " +
                                  "NIE używaj tagów <think> w finalnej odpowiedzi. " +
                                  "Tagi ACTION dodawaj tylko na końcu, gdy faktycznie wykonujesz zadanie. " +
                                  "Dostępne akcje: " + listaNarzedzi;

            if (historiaRozmowy.Count == 0 || !historiaRozmowy[0].Contains("system"))
            {
                historiaRozmowy.Insert(0, "{\"role\": \"system\", \"content\": \"" + systemPrompt + "\"}");
                ed.WriteMessage($"\n--- Agent Bielik gotowy (Model: {wybranyModel}) ---");
            }
            else
            {
                ed.WriteMessage($"\n--- Agent Bielik: Witaj ponownie Adrian! (Model: {wybranyModel}) ---");
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
                if (userMsg.ToLower() == "reset") { ResetujPamiec(doc); break; }

                ed.WriteMessage("\nBielik pracuje...");

                try
                {
                    historiaRozmowy.Add("{\"role\": \"user\", \"content\": \"" + userMsg.Replace("\"", "\\\"") + "\"}");
                    string jsonBody = "{\"model\": \"" + wybranyModel + "\", \"messages\": [" + string.Join(",", historiaRozmowy) + "], \"temperature\": 0.1, \"stream\": false}";

                    var response = client.PostAsync("http://127.0.0.1:1234/v1/chat/completions",
                                   new StringContent(jsonBody, Encoding.UTF8, "application/json")).Result;

                    string resRaw = response.Content.ReadAsStringAsync().Result;
                    string rawContent = WyciagnijContentZJson(resRaw);

                    // FILTR MYŚLI (Usuwamy sekcję <think> dla modeli Qwen 3.5 / DeepSeek)
                    string aiMsg = rawContent;
                    if (aiMsg.Contains("</think>"))
                    {
                        aiMsg = aiMsg.Substring(aiMsg.IndexOf("</think>") + 8).Trim();
                    }

                    if (!string.IsNullOrEmpty(aiMsg))
                    {
                        ed.WriteMessage("\n[Agent AI]: " + aiMsg);
                        historiaRozmowy.Add("{\"role\": \"assistant\", \"content\": \"" + aiMsg.Replace("\"", "\\\"") + "\"}");
                        ZapiszPamiec(doc);

                        // Wykonanie akcji CAD (tylko z "czystej" wiadomości)
                        foreach (var tool in tools)
                        {
                            if (aiMsg.ToUpper().Contains(tool.ActionTag.ToUpper())) tool.Execute(doc);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    ed.WriteMessage("\n[Błąd]: " + ex.Message);
                }
            }
        }

        // --- FUNKCJE POMOCNICZE ---

        private void ZapiszPamiec(Document doc)
        {
            try
            {
                string data = string.Join(Environment.NewLine, historiaRozmowy);
                File.WriteAllText(GetGlobalPath(), data);
                string localPath = GetLocalPath(doc);
                if (localPath != null) File.WriteAllText(localPath, data);
            }
            catch { }
        }

        private void WczytajPamiec(Document doc)
        {
            try
            {
                string globalPath = GetGlobalPath();
                string localPath = GetLocalPath(doc);
                string path = (localPath != null && File.Exists(localPath)) ? localPath : globalPath;

                if (File.Exists(path))
                {
                    string[] lines = File.ReadAllLines(path);
                    foreach (var line in lines) if (!string.IsNullOrWhiteSpace(line)) historiaRozmowy.Add(line);
                }
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
            List<string> modele = new List<string>();
            try
            {
                int pos = 0;
                while ((pos = json.IndexOf("\"id\":", pos)) != -1)
                {
                    int start = json.IndexOf("\"", pos + 5) + 1;
                    int end = json.IndexOf("\"", start);
                    modele.Add(json.Substring(start, end - start));
                    pos = end;
                }
            }
            catch { }
            return modele;
        }

        private string WyciagnijContentZJson(string json)
        {
            try
            {
                int keyIdx = json.IndexOf("\"content\"");
                if (keyIdx == -1) return "";
                int start = json.IndexOf("\"", keyIdx + 9) + 1;
                StringBuilder sb = new StringBuilder();
                for (int i = start; i < json.Length; i++)
                {
                    if (json[i] == '\\' && json[i + 1] == '\"') { sb.Append('\"'); i++; }
                    else if (json[i] == '\"') break;
                    else sb.Append(json[i]);
                }
                return sb.ToString().Replace("\\n", "\n");
            }
            catch { return ""; }
        }
    }
}