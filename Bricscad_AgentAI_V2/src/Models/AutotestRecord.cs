using System;

namespace Bricscad_AgentAI_V2.Models
{
    public class AutotestRecord
    {
        public string ToolName { get; set; }
        public int StaticTestsCount { get; set; }
        public int InteractiveTestsCount { get; set; }
        public string LastStatus { get; set; } // np. "Passed", "Failed", "Pending", "-"
        public DateTime LastTestDate { get; set; }
    }
}
