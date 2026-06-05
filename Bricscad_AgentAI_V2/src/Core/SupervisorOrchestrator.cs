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
            _globalHistory.Clear();
        }

        public List<ChatMessage> GetHistory() => _globalHistory;

        public async Task<AgentExecutionResult> ProcessInputAsync(object userContent, IExecutionContext context)
        {
            if (_globalHistory.Count == 0)
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
                
                _globalHistory.Add(new ChatMessage { Role = "system", Content = sysPrompt });
            }

            _globalHistory.Add(new ChatMessage { Role = "user", Content = userContent });

            // Supervisor always runs with SupervisorProfile
            AgentExecutionResult result = await _client.SendMessageReActAsync(_globalHistory, context, null, true, 10, "SupervisorProfile");
            return result;
        }
    }
}
