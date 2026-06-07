using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Bricscad_AgentAI_V2.Models.Session;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Core
{
    public static class QASessionManager
    {
        public static ChatSession CurrentSession { get; private set; }
        private static readonly string SessionsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bricscad_AgentAI", "QASessions");

        static QASessionManager()
        {
            if (!Directory.Exists(SessionsDirectory))
            {
                Directory.CreateDirectory(SessionsDirectory);
            }
            CreateNewSession();
        }

        public static void CreateNewSession()
        {
            CurrentSession = new ChatSession();
            
            // Wstrzyknięcie profilu systemowego QA
            string systemPrompt = LoadSystemPromptForProfile("AuditorProfile");
            if (string.IsNullOrEmpty(systemPrompt))
            {
                systemPrompt = "Jesteś ekspertem QA i Audytorem Kodu w BricsCAD_AgentAI. Twoim zadaniem jest testowanie narzędzi C#, analizowanie logów i przeglądanie struktury projektu. Masz pełny dostęp do kodu źródłowego całego systemu Bielik V2 (katalog src). Masz do dyspozycji narzędzia ListSourceFiles oraz ReadSourceCode do eksplorowania pętli agentowej, rdzenia programu i komunikacji z BricsCAD, a także narzędzia do zapisu raportów.";
            }
            CurrentSession.Messages.Add(new ChatMessage { Role = "system", Content = systemPrompt });
            
            SaveSession(CurrentSession);
        }

        private static string LoadSystemPromptForProfile(string profileName)
        {
            var profiles = ToolConfigManager.GetProfiles();
            if (profiles.TryGetValue(profileName, out var profile) && !string.IsNullOrEmpty(profile.SystemPromptFile))
            {
                string path = Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), profile.SystemPromptFile);
                if (File.Exists(path))
                {
                    return File.ReadAllText(path, System.Text.Encoding.UTF8);
                }
            }
            return null;
        }

        public static void LoadSession(string sessionId)
        {
            string path = Path.Combine(SessionsDirectory, $"{sessionId}.json");
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var session = JsonConvert.DeserializeObject<ChatSession>(json);
                if (session != null)
                {
                    CurrentSession = session;
                }
            }
        }

        public static void SaveSession(ChatSession session = null)
        {
            if (session == null) session = CurrentSession;
            if (session == null) return;
            
            if (session.Messages.Count == 0 && session.Description == "Nowa sesja") return;
            
            session.UpdatedAt = DateTime.Now;

            string path = Path.Combine(SessionsDirectory, $"{session.Id}.json");
            string json = JsonConvert.SerializeObject(session, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public static List<ChatSession> GetAllSessions()
        {
            var sessions = new List<ChatSession>();
            foreach (var file in Directory.GetFiles(SessionsDirectory, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var session = JsonConvert.DeserializeObject<ChatSession>(json);
                    if (session != null)
                    {
                        sessions.Add(session);
                    }
                }
                catch (Exception ex)
                {
                    BielikLogger.LogWarn($"Nie udało się załadować sesji QA {file}: {ex.Message}");
                }
            }
            return sessions.OrderByDescending(s => s.UpdatedAt).ToList();
        }

        public static void DeleteSession(string sessionId)
        {
            string path = Path.Combine(SessionsDirectory, $"{sessionId}.json");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            if (CurrentSession != null && CurrentSession.Id == sessionId)
            {
                CreateNewSession();
            }
        }

        public static void TriggerAutoNaming(LLMClient client, ChatSession session)
        {
            if (session == null || session.Messages.Count < 2) return;
            if (session.Description != "Nowa sesja") return;

            Task.Run(async () =>
            {
                try
                {
                    var msgs = new List<ChatMessage>
                    {
                        new ChatMessage { Role = "system", Content = "Podsumuj temat tej sesji QA w maksymalnie 3-4 słowach. Odpowiedz samym podsumowaniem, bez komentarza." },
                        new ChatMessage { Role = "user", Content = string.Join("\n", session.Messages.Where(m => m.Role != "system" && m.Content != null).Select(m => m.Content.ToString()).Take(3)) }
                    };
                    
                    var response = await client.SendMessageReActAsync(msgs, null, null, true, 1, "AuditorProfile");
                    if (response != null && response.IsSuccess && !string.IsNullOrEmpty(response.DisplayMessage))
                    {
                        session.Description = response.DisplayMessage.Trim('\"', '.', '\n', '\r');
                        SaveSession(session);
                    }
                }
                catch (Exception ex)
                {
                    BielikLogger.LogWarn($"Nie udało się nadać nazwy sesji QA: {ex.Message}");
                }
            });
        }
    }
}
