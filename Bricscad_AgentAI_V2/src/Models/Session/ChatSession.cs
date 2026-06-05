using System;
using System.Collections.Generic;

namespace Bricscad_AgentAI_V2.Models.Session
{
    public class ChatSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Description { get; set; } = "Nowa sesja";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public Dictionary<string, string> Blackboard { get; set; } = new Dictionary<string, string>();
    }
}
