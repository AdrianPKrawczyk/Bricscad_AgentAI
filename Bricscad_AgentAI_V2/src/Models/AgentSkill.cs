using System.Collections.Generic;

namespace Bricscad_AgentAI_V2.Models
{
    public class AgentSkill
    {
        public string Id { get; set; }
        public string Category { get; set; } = "Uncategorized";
        public string Description { get; set; } = "";
        public List<string> Tags { get; set; } = new List<string>();
        public string Content { get; set; } = "";
    }
}
