using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Bricscad_AgentAI_V2.Models;
using System;

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

        public async Task<AgentExecutionResult> ProcessInputAsync(object userContent, IExecutionContext context, string activeDwgPath = "", bool earlyExitEnabled = true)
        {
            AgentMemoryState.EarlyExitEnabled = earlyExitEnabled;
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
            if (ShouldRequireFreshDelegation(finalContent))
            {
                history.Add(new ChatMessage
                {
                    Role = "system",
                    Content = "[SYSTEM GUARD] To jest nowe polecenie wykonawcze CAD. Nie wolno odpowiadac z pamieci ani na podstawie poprzedniego podobnego polecenia. Jesli uzytkownik prosi o ustawienie, zmiane, przypisanie albo zastosowanie parametrow w rysunku, uzyj DelegateTask i poczekaj na realny wynik narzedzia. Kopiuj nazwy plikow, style wydruku, layouty, drukarki i wartosci w cudzyslowach dokladnie z polecenia uzytkownika; nie skracaj np. 'CadProfi Color.ctb' do 'CadProfi.ctb'."
                });
            }
            SessionManager.SaveSession(); // Natychmiastowy zapis zapobiegajÄ…cy utracie pytania ("ucieĹ‚o moje pytanie")
            
            // Trigger auto-naming w tle, jeĹ›li mamy juĹĽ co najmniej 2 wiadomoĹ›ci (np. system + user)
            SessionManager.TriggerAutoNaming(_client, SessionManager.CurrentSession);

            AgentExecutionResult result = await _client.SendMessageReActAsync(history, context, null, earlyExitEnabled, 10, "SupervisorProfile");
            
            SessionManager.SaveSession();
            return result;
        }

        private static bool ShouldRequireFreshDelegation(object content)
        {
            string text = content?.ToString();
            if (string.IsNullOrWhiteSpace(text)) return false;

            string lower = text.ToLowerInvariant();
            bool hasActionVerb =
                lower.Contains("ustaw") ||
                lower.Contains("zmien") ||
                lower.Contains("zmień") ||
                lower.Contains("przypisz") ||
                lower.Contains("zastosuj") ||
                lower.Contains("skonfiguruj") ||
                lower.Contains("dodaj") ||
                lower.Contains("usun") ||
                lower.Contains("usuń") ||
                lower.Contains("narysuj") ||
                lower.Contains("utworz") ||
                lower.Contains("utwórz");

            bool touchesCadState =
                lower.Contains("arkusz") ||
                lower.Contains("layout") ||
                lower.Contains("drukark") ||
                lower.Contains("format") ||
                lower.Contains("ctb") ||
                lower.Contains("stb") ||
                lower.Contains("warstw") ||
                lower.Contains("blok") ||
                lower.Contains("tekst") ||
                lower.Contains("wymiar") ||
                lower.Contains("rysunk");

            return hasActionVerb && touchesCadState;
        }
    }
}
