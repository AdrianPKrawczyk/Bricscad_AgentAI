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
        // =========================================================

        public static AgentExecutionResult Success(string message, object data = null)
        {
            return new AgentExecutionResult { IsSuccess = true, DisplayMessage = message, InternalData = data };
        }

        public static AgentExecutionResult Failure(string message, object data = null)
        {
            return new AgentExecutionResult { IsSuccess = false, DisplayMessage = message, InternalData = data };
        }
    }
}
