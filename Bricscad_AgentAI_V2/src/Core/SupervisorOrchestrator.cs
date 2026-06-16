using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// NadrzÄ™dny koordynator systemu. Utrzymuje PamiÄ™Ä‡ GlobalnÄ… (Semantic History)
    /// i deleguje wyspecjalizowane zadania do WorkerĂłw (przy pomocy DelegateTaskTool).
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

        public string BuildSupervisorSystemPrompt(string activeDwgPath = "")
        {
            string sysPrompt = "Jestes Glownym Menedzerem (Supervisorem). Przeanalizuj problem i zidentyfikuj najlepszego Agenta Eksperta. Nie wykonujesz pracy sam.";
            string loadedPrompt = ToolConfigManager.LoadEffectivePromptForProfile("SupervisorProfile");
            if (!string.IsNullOrWhiteSpace(loadedPrompt))
            {
                sysPrompt = loadedPrompt;
            }

            string note = DrawingNoteManager.ReadNote(activeDwgPath);
            if (string.IsNullOrWhiteSpace(note))
            {
                note = "[Brak notatki. Uzytkownik nie stworzyl jeszcze notatki inzynierskiej dla tego rysunku. Mozesz ja wygenerowac delegujac zadanie do agenta NotesProfile uzywajac narzedzia DelegateTask, albo powiedziec uzytkownikowi o komendzie /notatka]";
            }

            return sysPrompt + $"\n\n=== NOTATKA DLA RYSUNKU: {activeDwgPath} ===\n{note}\n=== KONIEC NOTATKI ===";
        }

        public async Task<AgentExecutionResult> ProcessInputAsync(object userContent, IExecutionContext context, string activeDwgPath = "")
        {
            var history = SessionManager.CurrentSession.Messages;

            if (history.Count == 0)
            {
                history.Add(new ChatMessage { Role = "system", Content = BuildSupervisorSystemPrompt(activeDwgPath) });
            }
            else
            {
                // JeĹ›li mamy juĹĽ system prompt w historii, szukamy go i aktualizujemy notatkÄ™ jeĹ›li to potrzebne
                var sysMsg = history.Find(m => m.Role == "system");
                if (sysMsg != null && sysMsg.Content is string sysContent)
                {
                    string note = DrawingNoteManager.ReadNote(activeDwgPath);
                    if (string.IsNullOrWhiteSpace(note))
                    {
                        note = "[Brak notatki. UĹĽytkownik nie stworzyĹ‚ jeszcze notatki inĹĽynierskiej dla tego rysunku. MoĹĽesz jÄ… wygenerowaÄ‡ delegujÄ…c zadanie do agenta NotesProfile uĹĽywajÄ…c narzÄ™dzia DelegateTask, albo powiedzieÄ‡ uĹĽytkownikowi o komendzie /notatka]";
                    }
                    
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

            // Prefix dla Context Tracking
            object finalContent = userContent;
            if (userContent is string strContent && !string.IsNullOrWhiteSpace(activeDwgPath))
            {
                finalContent = $"[Kontekst: Aktywny plik to {activeDwgPath}; ActiveSelection zawiera {AgentMemoryState.ActiveSelection.Length} obiekt(ow)]\n{strContent}";
            }

            history.Add(new ChatMessage { Role = "user", Content = finalContent, ActiveDocumentPath = activeDwgPath });
            SessionManager.SaveSession(); // Natychmiastowy zapis zapobiegajÄ…cy utracie pytania ("ucieĹ‚o moje pytanie")
            
            // Trigger auto-naming w tle, jeĹ›li mamy juĹĽ co najmniej 2 wiadomoĹ›ci (np. system + user)
            SessionManager.TriggerAutoNaming(_client, SessionManager.CurrentSession);

            AgentExecutionResult result = await _client.SendMessageReActAsync(history, context, null, true, 10, "SupervisorProfile");
            
            SessionManager.SaveSession();
            return result;
        }
    }
}
