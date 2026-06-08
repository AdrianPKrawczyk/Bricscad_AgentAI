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
    public static class SessionManager
    {
        public static ChatSession CurrentSession { get; private set; }
        private static readonly string SessionsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bricscad_AgentAI", "Sessions");

        static SessionManager()
        {
            if (!Directory.Exists(SessionsDirectory))
            {
                Directory.CreateDirectory(SessionsDirectory);
            }
            int mode = UISettingsManager.Settings.AIStartupBehavior;
            if (mode == 0) // Ładuj poprzednią
            {
                var sessions = GetAllSessions();
                if (sessions.Count > 0)
                {
                    LoadSession(sessions.First().Id);
                }
                else
                {
                    CreateNewSession();
                }
            }
            else if (mode == 1) // Twórz nową
            {
                CreateNewSession();
            }
            else // Wybór manualny (nie zapisujemy jej od razu)
            {
                CurrentSession = new ChatSession();
                SharedMemoryState.Clear();
            }
        }

        public static void CreateNewSession()
        {
            CurrentSession = new ChatSession();
            SaveSession(CurrentSession);
            SharedMemoryState.Clear(); // Pusty blackboard na start
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
                    // Skonfiguruj istniejący stan tablicy dla załadowanej sesji
                    SharedMemoryState.LoadFromDictionary(session.Blackboard);
                }
            }
        }

        public static void SaveSession(ChatSession session = null)
        {
            if (session == null) session = CurrentSession;
            if (session == null) return;
            
            // Blokujemy zapisywanie całkowicie pustej domyślnej sesji na starcie programu
            if (session.Messages.Count == 0 && session.Description == "Nowa sesja") return;
            
            session.UpdatedAt = DateTime.Now;
            // Odśwież blackboard przed zapisem
            session.Blackboard = SharedMemoryState.GetAll();

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
                    BielikLogger.LogWarn($"Nie udało się załadować sesji {file}: {ex.Message}");
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
            // Nazywamy tylko raz (jesli domyslna)
            if (session.Description != "Nowa sesja") return;

            Task.Run(async () =>
            {
                try
                {
                    var msgs = new List<ChatMessage>
                    {
                        new ChatMessage { Role = "system", Content = "Podsumuj temat tej rozmowy w maksymalnie 3-4 słowach. Odpowiedz samym podsumowaniem, bez komentarza." },
                        new ChatMessage { Role = "user", Content = string.Join("\n", session.Messages.Where(m => m.Role != "system" && m.Content != null).Select(m => m.Content.ToString()).Take(3)) }
                    };
                    
                    var response = await client.SendMessageReActAsync(msgs, null, null, true, 1, "SupervisorProfile");
                    if (response != null && response.IsSuccess && !string.IsNullOrEmpty(response.DisplayMessage))
                    {
                        session.Description = response.DisplayMessage.Trim('\"', '.', '\n', '\r');
                        SaveSession(session);
                    }
                }
                catch (Exception ex)
                {
                    BielikLogger.LogWarn($"Nie udało się nadać nazwy sesji: {ex.Message}");
                }
            });
        }
    }
}
