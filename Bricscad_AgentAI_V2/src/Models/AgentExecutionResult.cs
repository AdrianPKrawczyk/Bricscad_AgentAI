using System;
using System.Collections.Generic;

namespace Bricscad_AgentAI_V2.Models
{
    public class AgentExecutionResult
    {
        public bool IsSuccess { get; set; }
        public string DisplayMessage { get; set; }
        public object InternalData { get; set; }

        // ============== AUDITOR (Chain of Evidence) ==============
        public int MutationsDetected { get; set; }
        public int RollbacksDetected { get; set; }
        public List<string> EvidenceHandles { get; set; } = new List<string>();

        public bool HasChainOfEvidence => EvidenceHandles != null && EvidenceHandles.Count > 0;
        public bool HadRollback => RollbacksDetected > 0;

        // Fix v2.35.2 (BUG #3): Czy w historii Workera jest wywolanie narzedzia mutujacego.
        // Wykorzystywane przez RewidentAuditService jako NIEZALEZNY sygnal mutacji
        // (DisplayMessage moze zostac przeklamany przez LLM - skomponowany tekst
        // odpowiedzi, nie surowy wynik narzedzia).
        public bool HasMutatingToolCall { get; set; }
        public string LastMutatingToolName { get; set; }
        public List<string> MutatingToolNames { get; set; } = new List<string>();
        // =========================================================

        public static AgentExecutionResult Success(string message, object data = null)
        {
            return new AgentExecutionResult { IsSuccess = true, DisplayMessage = message, InternalData = data };
        }

        /// <summary>
        /// Fix v2.35.2 (BUG #3): Wzbogacona fabryka - automatycznie wykrywa czy w historii
        /// rozmowy zostalo uzyte jakies narzedzie mutujace i ustawia HasMutatingToolCall.
        /// Przekaz pusta liste - helper sam przeskanuje conversationHistory.
        /// </summary>
        public static AgentExecutionResult SuccessWithHistory(string message, List<ChatMessage> conversationHistory, object data = null)
        {
            var result = new AgentExecutionResult { IsSuccess = true, DisplayMessage = message, InternalData = data };
            PopulateMutationEvidenceFromHistory(result, conversationHistory);
            return result;
        }

        public static AgentExecutionResult Failure(string message, object data = null)
        {
            return new AgentExecutionResult { IsSuccess = false, DisplayMessage = message, InternalData = data };
        }

        /// <summary>
        /// Fix v2.35.3 (BUG #5): Failure z instrukcja jak wyjsc z petli.
        /// Wykorzystywane przez Anti-Loop detektor w LLMClient.
        /// </summary>
        public static AgentExecutionResult FailureWithHint(string message, object data = null)
        {
            return new AgentExecutionResult
            {
                IsSuccess = false,
                DisplayMessage = message,
                InternalData = data,
                LastMutatingToolName = "[ANTI-LOOP]"
            };
        }

        // Fix v2.35.2 (BUG #3): Skanuj historie w poszukiwaniu wywolan narzedzi mutujacych.
        // NIEZALEZNE od DisplayMessage (ktory moze zostac sklamany przez LLM).
        // Worker moze napisac "Zrobilem to!" ale my widzimy ze NIE wywolal zadnego
        // narzedzia mutujacego - wowczas HasMutatingToolCall = false.
        public static void PopulateMutationEvidenceFromHistory(AgentExecutionResult result, List<ChatMessage> conversationHistory)
        {
            if (result == null) return;
            if (conversationHistory == null) return;

            // Mutujace narzedzia - zrodlo prawdy niezalezne od LLM
            var mutatingTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CreateObject", "ModifyProperties", "ManageLayers", "InsertBlock", "CreateBlock",
                "EditBlock", "EditAttributes", "TextEditTool", "DimensionEditTool", "ManageAnnoScales",
                "WriteXData", "BatchWriteXData", "PageSetupTool", "PlotStyleTool", "ManageLayoutTool",
                "ImportLayoutTemplateTool", "ExportLayoutTemplateTool", "PlotLayoutTool", "PublishToPdfTool",
                "ManageViewportsTool", "ManageSheetSetTool", "SheetSetSheetTool"
            };

            foreach (var msg in conversationHistory)
            {
                if (msg?.ToolCalls == null) continue;
                foreach (var call in msg.ToolCalls)
                {
                    string name = call?.Function?.Name;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (mutatingTools.Contains(name))
                    {
                        if (!result.MutatingToolNames.Contains(name))
                        {
                            result.MutatingToolNames.Add(name);
                        }
                        result.LastMutatingToolName = name;
                    }
                }
            }

            result.HasMutatingToolCall = result.MutatingToolNames.Count > 0;
        }
    }
}
