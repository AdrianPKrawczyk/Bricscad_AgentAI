using System;

namespace Bricscad_AgentAI_V2.Models
{
    public class AgentExecutionResult
    {
        public bool IsSuccess { get; set; }
        public string DisplayMessage { get; set; }
        public object InternalData { get; set; }

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
