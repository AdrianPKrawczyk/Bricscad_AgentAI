using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Nadrzędny koordynator systemu. Utrzymuje Pamięć Globalną (Semantic History)
    /// i deleguje wyspecjalizowane zadania do Workerów (przy pomocy DelegateTaskTool).
    /// </summary>
    public class SupervisorOrchestrator
    {
        private readonly List<ChatMessage> _globalHistory = new List<ChatMessage>();
        private readonly LLMClient _client;

        public SupervisorOrchestrator(LLMClient client)
        {
            _client = client;
        }

        public void ClearHistory()
        {
            SessionManager.CurrentSession.Messages.Clear();
        }

        public List<ChatMessage> GetHistory() => SessionManager.CurrentSession.Messages;

        public async Task<AgentExecutionResult> ProcessInputAsync(object userContent, IExecutionContext context, string activeDwgPath = "")
        {
            var history = SessionManager.CurrentSession.Messages;

            if (history.Count == 0)
            {
                var profiles = ToolConfigManager.GetProfiles();
                string sysPrompt = "Jesteś Głównym Menedżerem (Supervisorem). Przeanalizuj problem i zidentyfikuj jakiego Agenta Eksperta wezwać. Nie wykonujesz pracy sam.";
                
                if (profiles.TryGetValue("SupervisorProfile", out var profile) && !string.IsNullOrEmpty(profile.SystemPromptFile))
                {
                    string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), profile.SystemPromptFile);
                    if (File.Exists(path))
                    {
                        sysPrompt = File.ReadAllText(path);
                    }
                }
                
                // RAG: Doklejamy notatkę do promptu systemowego
                string note = DrawingNoteManager.ReadNote(activeDwgPath);
                if (!string.IsNullOrWhiteSpace(note))
                {
                    sysPrompt += $"\n\n=== NOTATKA DLA RYSUNKU: {activeDwgPath} ===\n{note}\n=== KONIEC NOTATKI ===";
                }
                
                history.Add(new ChatMessage { Role = "system", Content = sysPrompt });
            }
            else
            {
                // Jeśli mamy już system prompt w historii, szukamy go i aktualizujemy notatkę jeśli to potrzebne
                var sysMsg = history.Find(m => m.Role == "system");
                if (sysMsg != null && sysMsg.Content is string sysContent)
                {
                    string note = DrawingNoteManager.ReadNote(activeDwgPath);
                    if (!string.IsNullOrWhiteSpace(note))
                    {
                        // Zabezpieczenie przed dublowaniem
                        if (!sysContent.Contains("=== NOTATKA DLA RYSUNKU:"))
                        {
                            sysMsg.Content = sysContent + $"\n\n=== NOTATKA DLA RYSUNKU: {activeDwgPath} ===\n{note}\n=== KONIEC NOTATKI ===";
                        }
                        else
                        {
                            // Podmiana notatki (uproszczone)
                            int startIdx = sysContent.IndexOf("=== NOTATKA DLA RYSUNKU:");
                            sysMsg.Content = sysContent.Substring(0, startIdx).TrimEnd() + $"\n\n=== NOTATKA DLA RYSUNKU: {activeDwgPath} ===\n{note}\n=== KONIEC NOTATKI ===";
                        }
                    }
                }
            }

            // Prefix dla Context Tracking
            object finalContent = userContent;
            if (userContent is string strContent && !string.IsNullOrWhiteSpace(activeDwgPath))
            {
                finalContent = $"[Kontekst: Aktywny plik to {activeDwgPath}]\n{strContent}";
            }

            history.Add(new ChatMessage { Role = "user", Content = finalContent, ActiveDocumentPath = activeDwgPath });
            
            // Trigger auto-naming w tle, jeśli mamy już co najmniej 2 wiadomości (np. system + user)
            SessionManager.TriggerAutoNaming(_client, SessionManager.CurrentSession);

            AgentExecutionResult result = await _client.SendMessageReActAsync(history, context, null, true, 10, "SupervisorProfile");
            
            SessionManager.SaveSession();
            return result;
        }
    }
}
