using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class ReadWentCadRoomsTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ReadWentCadRooms",
                    Description = "Odczytuje pomieszczenia WentCad z NOD WENTCAD_ROOMS oraz opcjonalnie filtruje je po FloorId.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "FloorId",
                                new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalny identyfikator kondygnacji. Gdy pusty, zwracane sa wszystkie pomieszczenia."
                                }
                            }
                        }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{}", "{\"FloorId\":\"...\"}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";

            string floorId = args?["FloorId"]?.ToString();
            JObject contract;
            using (doc.LockDocument())
            {
                contract = WentCadContractReader.Read(doc.Database);
            }

            var rooms = contract["Rooms"] as JArray ?? new JArray();
            if (!string.IsNullOrWhiteSpace(floorId))
            {
                var filtered = new JArray();
                foreach (var room in rooms)
                {
                    if (string.Equals(room["FloorId"]?.ToString(), floorId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        filtered.Add(room);
                    }
                }
                rooms = filtered;
            }

            var result = new JObject
            {
                ["Project"] = contract["Project"],
                ["Floors"] = contract["Floors"],
                ["Rooms"] = rooms
            };
            return result.ToString(Formatting.Indented);
        }
    }
}
