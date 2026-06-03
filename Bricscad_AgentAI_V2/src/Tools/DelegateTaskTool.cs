using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class DelegateTaskTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "DelegateTask",
                    Description = "Deleguje zadanie do wyspecjalizowanego Agenta Eksperta. Użyj tego, gdy zadanie wykracza poza twoje kompetencje. Po zakończeniu zadania, ekspert zwróci wynik.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "TargetProfile", new ToolParameter { Type = "string", Description = "Nazwa profilu docelowego eksperta (np. 'CadProfile', 'NotesProfile')." } },
                            { "TaskDescription", new ToolParameter { Type = "string", Description = "Szczegółowa instrukcja dla eksperta." } }
                        },
                        Required = new List<string> { "TargetProfile", "TaskDescription" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string targetProfile = args["TargetProfile"]?.ToString();
            string taskDescription = args["TaskDescription"]?.ToString();

            if (string.IsNullOrEmpty(targetProfile) || string.IsNullOrEmpty(taskDescription))
            {
                return "BŁĄD: Parametry 'TargetProfile' i 'TaskDescription' są wymagane.";
            }

            var profiles = ToolConfigManager.GetProfiles();
            if (!profiles.TryGetValue(targetProfile, out var profileConfig))
            {
                return $"BŁĄD: Nie znaleziono profilu '{targetProfile}'. Zawsze weryfikuj nazwę profilu.";
            }

            // 1. Ładowanie system promptu eksperta
            string systemPrompt = LoadSystemPrompt(profileConfig.SystemPromptFile);

            // 2. Przygotowanie izolowanej historii
            var localHistory = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = taskDescription }
            };

            // 3. Wstrzyknięcie zawartości Blackboard jako kontekstu
            var blackboardState = SharedMemoryState.GetAll();
            if (blackboardState.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== PAMIĘĆ WSPÓŁDZIELONA (BLACKBOARD) ===");
                foreach (var kvp in blackboardState)
                {
                    sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
                localHistory.Add(new ChatMessage { Role = "system", Content = sb.ToString() });
            }

            // 4. Inicjalizacja klienta i synchroniczne oczekiwanie (działamy w Task.Run w LLMClient)
            var client = new LLMClient(ToolOrchestrator.Instance);
            
            client.OnStatusUpdate += (msg) => {
                System.Diagnostics.Debug.WriteLine($"[{targetProfile}] {msg}");
                Bricscad_AgentAI_V2.UI.AgentControl.Instance?.UpdateStatusHUD($"[{targetProfile}] {msg}");
            };
            client.OnToolCallLogged += (log) => {
                Bricscad_AgentAI_V2.UI.AgentControl.Instance?.AppendToolLog($"--- [{targetProfile}] ---\n{log}");
            };
            client.OnStatsUpdate += (stats) => {
                // Opcjonalnie możemy agregować statystyki, na razie przekazujemy
                Bricscad_AgentAI_V2.UI.AgentControl.Instance?.UpdateStatsHUD(stats);
            };

            var context = new CadExecutionContext(doc);

            try
            {
                // Musimy zablokować wątek i poczekać na wynik z eksperta
                AgentExecutionResult result = client.SendMessageReActAsync(
                    conversationHistory: localHistory, 
                    context: context, 
                    initialTags: null, 
                    earlyExitEnabled: true, 
                    maxIterations: 10, 
                    profileName: targetProfile).GetAwaiter().GetResult();

                // --- DATASET STUDIO INTEGRATION FOR WORKER ---
                try
                {
                    var historySnapshot = new List<ChatMessage>(localHistory);
                    var toolsSnapshot = ToolOrchestrator.Instance.GetToolsPayloadForProfile(targetProfile);
                    Bricscad_AgentAI_V2.UI.AgentControl.Instance?.DatasetStudio.AddSessionRecord(
                        $"[{targetProfile}] {taskDescription}", 
                        historySnapshot, 
                        toolsSnapshot, 
                        client.LastStats
                    );
                }
                catch { }

                if (result.IsSuccess)
                {
                    return $"Zadanie zakończone przez '{targetProfile}'. Zwrócony wynik: {result.DisplayMessage}";
                }
                else
                {
                    return $"BŁĄD: '{targetProfile}' zgłosił awarię: {result.DisplayMessage}";
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY podczas delegowania do '{targetProfile}': {ex.Message}";
            }
        }

        private string LoadSystemPrompt(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return "Jesteś ekspertem zadanym przez system.";
            
            string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), filename);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
            return "Jesteś ekspertem zadanym przez system. (Nie znaleziono pliku promptu)";
        }

        public List<string> Examples => new List<string>
        {
            "{ \"TargetProfile\": \"CadProfile\", \"TaskDescription\": \"Narysuj okrąg o promieniu 50\" }"
        };
    }
}
