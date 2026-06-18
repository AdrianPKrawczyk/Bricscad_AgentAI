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
                    Description = "Deleguje zadanie do wyspecjalizowanego Agenta Eksperta. UĹĽyj tego, gdy zadanie wykracza poza twoje kompetencje. Po zakoĹ„czeniu zadania, ekspert zwrĂłci wynik.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "TargetProfile", new ToolParameter { Type = "string", Description = "Nazwa profilu docelowego eksperta (np. 'CadProfile', 'NotesProfile')." } },
                            { "SelectionScopeLock", new ToolParameter { Type = "boolean", Description = "Ustaw true, gdy zadanie ma operowac wylacznie na aktualnie zaznaczonych/wybranych obiektach uzytkownika. Blokuje Workerowi zastapienie selekcji globalnym wyszukiwaniem po modelu." } },
                            { "TaskDescription", new ToolParameter { Type = "string", Description = "SzczegĂłĹ‚owa instrukcja dla eksperta." } }
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
                return "BĹÄ„D: Parametry 'TargetProfile' i 'TaskDescription' sÄ… wymagane.";
            }

            var profiles = ToolConfigManager.GetProfiles();
            if (!profiles.TryGetValue(targetProfile, out var profileConfig))
            {
                return $"BĹÄ„D: Nie znaleziono profilu '{targetProfile}'. Zawsze weryfikuj nazwÄ™ profilu.";
            }

            // 1. Ĺadowanie system promptu eksperta
            string systemPrompt = ToolConfigManager.LoadEffectivePromptForProfile(targetProfile);

            bool explicitSelectionScopeLock = args["SelectionScopeLock"]?.Value<bool>() ?? false;
            if (explicitSelectionScopeLock &&
                string.Equals(targetProfile, "CadLayoutProfile", StringComparison.OrdinalIgnoreCase) &&
                IsLayoutMutationTask(taskDescription))
            {
                explicitSelectionScopeLock = false;
                BielikLogger.LogInfo("[DelegateTask] Ignoring SelectionScopeLock=true for CadLayoutProfile layout/page-setup task.");
            }
            bool isSelectionScopedTask = explicitSelectionScopeLock || IsSelectionScopedTask(taskDescription);
            if (isSelectionScopedTask && AgentMemoryState.ActiveSelection.Length == 0)
            {
                return "BĹÄ„D: Zadanie dotyczy aktualnie zaznaczonych obiektĂłw, ale pamiÄ™Ä‡ Agenta nie zawiera ĹĽadnego zaznaczenia. Zaznacz obiekty ponownie albo najpierw zsynchronizuj SelectionSet.";
            }

            bool lockSelectionScope = isSelectionScopedTask;
            if (lockSelectionScope)
            {
                AgentMemoryState.LockSelectionScope();
            }

            // 2. Przygotowanie izolowanej historii
            var localHistory = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = taskDescription }
            };

            // 3. WstrzykniÄ™cie zawartoĹ›ci Blackboard jako kontekstu
            var blackboardState = SharedMemoryState.GetAll();
            if (blackboardState.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== DOSTÄPNE ZMIENNE W PAMIÄCI (BLACKBOARD) ===");
                sb.AppendLine("UĹĽyj odpowiedniego narzÄ™dzia (np. ReadFromBlackboardTool) lub wstrzykiwania zmiennych ($KLUCZ), aby odczytaÄ‡ peĹ‚ne wartoĹ›ci.");
                foreach (var kvp in blackboardState)
                {
                    sb.AppendLine($"- {kvp.Key} (DĹ‚ugoĹ›Ä‡: {kvp.Value?.Length ?? 0} znakĂłw)");
                }
                localHistory.Add(new ChatMessage { Role = "system", Content = sb.ToString() });
            }

            // 4. Inicjalizacja klienta i synchroniczne oczekiwanie (dziaĹ‚amy w Task.Run w LLMClient)
            var client = new LLMClient(ToolOrchestrator.Instance);
            
            client.OnStatusUpdate += (msg) => {
                System.Diagnostics.Debug.WriteLine($"[{targetProfile}] {msg}");
                AgentTelemetry.ReportStatus($"[{targetProfile}] {msg}");
            };
            client.OnToolCallLogged += (log) => {
                AgentTelemetry.ReportToolLog($"--- [{targetProfile}] ---\n{log}");
            };
            client.OnLoopLogged += (log) => {
                AgentTelemetry.ReportLoopLog(log);
            };
            client.OnStatsUpdate += (stats) => {
                AgentTelemetry.ReportStats(stats);
            };

            var context = new CadExecutionContext(doc);

            try
            {
                AgentTelemetry.ReportLoopLog($"[Supervisor -> {targetProfile}]\n{taskDescription}");
                // Musimy zablokowaÄ‡ wÄ…tek i poczekaÄ‡ na wynik z eksperta, chroniÄ…c gĹ‚Ăłwny wÄ…tek przed Deadlockiem
                AgentExecutionResult result = Task.Run(async () => {
                    return await client.SendMessageReActAsync(
                        conversationHistory: localHistory, 
                        context: context, 
                        initialTags: null, 
                        earlyExitEnabled: AgentMemoryState.EarlyExitEnabled,
                        maxIterations: 10, 
                        profileName: targetProfile);
                }).GetAwaiter().GetResult();

                // --- DATASET STUDIO INTEGRATION FOR WORKER ---
                try
                {
                    var historySnapshot = new List<ChatMessage>(localHistory);
                    var toolsSnapshot = ToolOrchestrator.Instance.GetToolsPayloadForProfile(targetProfile);
                    AgentTelemetry.ReportDatasetRecord(
                        $"[{targetProfile}] {taskDescription}", 
                        historySnapshot, 
                        toolsSnapshot, 
                        client.LastStats
                    );
                }
                catch { }

                if (result.IsSuccess)
                {
                    if (IsLikelyUnfulfilledLayoutMutation(targetProfile, taskDescription, result.DisplayMessage))
                    {
                        return $"BŁĄD: '{targetProfile}' nie wykonał zleconej modyfikacji. Zwrócił tylko listę layoutów zamiast użyć PageSetupTool/Foreach do zmiany ustawień. Powtórz delegowanie z jawnym nakazem wykonania PageSetupTool dla każdego arkusza.";
                    }
                    AgentTelemetry.ReportLoopLog($"[{targetProfile} -> Supervisor]\n{result.DisplayMessage}");
                    return $"Zadanie zakoĹ„czone przez '{targetProfile}'. ZwrĂłcony wynik: {result.DisplayMessage}";
                }
                else
                {
                    AgentTelemetry.ReportLoopLog($"[{targetProfile} -> Supervisor]\nBLAD: {result.DisplayMessage}");
                    return $"BĹÄ„D: '{targetProfile}' zgĹ‚osiĹ‚ awariÄ™: {result.DisplayMessage}";
                }
            }
            catch (Exception ex)
            {
                return $"BĹÄ„D KRYTYCZNY podczas delegowania do '{targetProfile}': {ex.Message}";
            }
            finally
            {
                if (lockSelectionScope)
                {
                    AgentMemoryState.UnlockSelectionScope();
                }
            }
        }

        private bool IsLikelyUnfulfilledLayoutMutation(string targetProfile, string taskDescription, string displayMessage)
        {
            if (!string.Equals(targetProfile, "CadLayoutProfile", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!IsLayoutMutationTask(taskDescription))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayMessage))
            {
                return true;
            }

            string result = displayMessage.ToLowerInvariant();
            bool isOnlyListResult =
                result.Contains("wynik: znaleziono") &&
                result.Contains("layout(ow)") &&
                !result.Contains("sukces: zastosowano") &&
                !result.Contains("wykonano") &&
                !result.Contains("pagesetup") &&
                !result.Contains("page setup");

            return isOnlyListResult;
        }

        private bool IsLayoutMutationTask(string taskDescription)
        {
            if (string.IsNullOrWhiteSpace(taskDescription)) return false;

            string text = taskDescription.ToLowerInvariant();
            bool hasActionVerb =
                text.Contains("ustaw") ||
                text.Contains("zmien") ||
                text.Contains("zmień") ||
                text.Contains("zastosuj") ||
                text.Contains("skonfiguruj") ||
                text.Contains("przypisz") ||
                text.Contains("ustawić") ||
                text.Contains("zmienić") ||
                text.Contains("ma byc") ||
                text.Contains("ma być");

            bool mentionsLayoutOrPlot =
                text.Contains("arkusz") ||
                text.Contains("layout") ||
                text.Contains("drukark") ||
                text.Contains("format papieru") ||
                text.Contains("styl wydruku") ||
                text.Contains("ctb") ||
                text.Contains("stb") ||
                text.Contains("page setup") ||
                text.Contains("pagesetup");

            return hasActionVerb && mentionsLayoutOrPlot;
        }

        private bool IsSelectionScopedTask(string taskDescription)
        {
            if (string.IsNullOrWhiteSpace(taskDescription)) return false;

            string text = taskDescription.ToLowerInvariant();
            return text.Contains("zaznaczon") ||
                   text.Contains("wybran") ||
                   text.Contains("activeselection") ||
                   text.Contains("selectionset") ||
                   text.Contains("aktualnym wybor") ||
                   text.Contains("obecnym wybor");
        }


        public List<string> Examples => new List<string>
        {
            "{ \"TargetProfile\": \"CadProfile\", \"TaskDescription\": \"Narysuj okrÄ…g o promieniu 50\" }"
        };
    }
}
