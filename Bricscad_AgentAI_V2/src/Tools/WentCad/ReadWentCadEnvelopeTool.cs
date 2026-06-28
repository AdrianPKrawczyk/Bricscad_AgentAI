using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class ReadWentCadEnvelopeTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ReadWentCadEnvelope",
                    Description = "Odczytuje sciany WATT i okna WentCad z NOD WENTCAD_WALLS/WENTCAD_WINDOWS. Nie wymaga referencji do DLL WentCad.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "FloorId", new ToolParameter { Type = "string", Description = "Opcjonalny filtr kondygnacji." } },
                            { "RoomId", new ToolParameter { Type = "string", Description = "Opcjonalny filtr pomieszczenia." } }
                        }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{\"FloorId\":\"...\"}", "{}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();
            string floorId = WentCadProjectStore.Clean(args["FloorId"]);
            string roomId = WentCadProjectStore.Clean(args["RoomId"]);
            var contract = WentCadContractReader.Read(doc.Database);
            var walls = (contract["Walls"] as JArray) ?? new JArray();
            var windows = (contract["Windows"] as JArray) ?? new JArray();

            bool Keep(JToken token)
            {
                if (!string.IsNullOrWhiteSpace(floorId) && !string.Equals(WentCadProjectStore.Clean(token["FloorId"]), floorId, System.StringComparison.OrdinalIgnoreCase)) return false;
                if (!string.IsNullOrWhiteSpace(roomId) && !string.Equals(WentCadProjectStore.Clean(token["RoomId"]), roomId, System.StringComparison.OrdinalIgnoreCase)) return false;
                return true;
            }

            return new JObject
            {
                ["Project"] = contract["Project"],
                ["Walls"] = new JArray(walls.Where(Keep)),
                ["Windows"] = new JArray(windows.Where(Keep))
            }.ToString(Formatting.Indented);
        }
    }
}
