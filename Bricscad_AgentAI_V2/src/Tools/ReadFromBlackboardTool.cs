using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ReadFromBlackboardTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ReadFromBlackboard",
                    Description = "Odczytuje informacje zapisane w Pamięci Współdzielonej (Blackboard) przez innych agentów/narzędzia.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "Key", new ToolParameter { Type = "string", Description = "Klucz do odczytania. Zostaw puste, aby pobrać całą zawartość Blackboard." } }
                        }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string key = args["Key"]?.ToString();

            if (string.IsNullOrEmpty(key))
            {
                var all = SharedMemoryState.GetAll();
                if (all.Count == 0) return "Blackboard jest pusty.";
                
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Zawartość Blackboard:");
                foreach(var kvp in all)
                {
                    sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
                return sb.ToString();
            }

            string value = SharedMemoryState.Read(key);
            if (value == null)
            {
                return $"Brak danych dla klucza '{key}' na Blackboard.";
            }

            return $"Wartość dla klucza '{key}': {value}";
        }

        public List<string> Examples => new List<string>
        {
            "{ \"Key\": \"MeasuredDistance\" }",
            "{ }"
        };
    }
}
