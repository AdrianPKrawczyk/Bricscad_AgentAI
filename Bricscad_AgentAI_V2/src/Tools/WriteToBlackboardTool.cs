using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class WriteToBlackboardTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "WriteToBlackboard",
                    Description = "Zapisuje informacje w Pamięci Współdzielonej (Blackboard). Używaj do przekazywania istotnych danych (np. zmierzonych odległości, identyfikatorów) do głównego Supervisora lub innych narzędzi.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "Key", new ToolParameter { Type = "string", Description = "Klucz pod którym dane zostaną zapisane (np. 'LastMeasuredDistance', 'WallLayerName')." } },
                            { "Value", new ToolParameter { Type = "string", Description = "Wartość do zapisania." } }
                        },
                        Required = new List<string> { "Key", "Value" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string key = args["Key"]?.ToString();
            string value = args["Value"]?.ToString();

            if (string.IsNullOrEmpty(key)) return "BŁĄD: Parametr 'Key' nie może być pusty.";

            SharedMemoryState.Write(key, value);
            return $"Zapisano pomyślnie na tablicy (Blackboard) pod kluczem '{key}'.";
        }

        public List<string> Examples => new List<string>
        {
            "{ \"Key\": \"MeasuredDistance\", \"Value\": \"150.5\" }"
        };
    }
}
