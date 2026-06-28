using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class UpdateWentCadWallTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "UpdateWentCadWall",
                    Description = "Aktualizuje istniejaca sciane WATT WentCad w .wentcad po WallId: typ, grubość, pewnosc albo komunikat. Nie wymaga DLL WentCad.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "WallId", new ToolParameter { Type = "string", Description = "Id sciany do aktualizacji." } },
                            { "Kind", new ToolParameter { Type = "string", Description = "EXTERNAL, INTERNAL albo UNRESOLVED." } },
                            { "Thickness", new ToolParameter { Type = "number", Description = "Grubosc sciany w metrach." } },
                            { "Confidence", new ToolParameter { Type = "number", Description = "Pewnosc 0..1." } },
                            { "Message", new ToolParameter { Type = "string", Description = "Uwagi diagnostyczne." } }
                        },
                        Required = new List<string> { "WallId" }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{\"WallId\":\"wall-...-0\",\"Kind\":\"EXTERNAL\",\"Confidence\":0.95}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();
            var project = WentCadProjectStore.LoadOrCreate(doc);
            var walls = WentCadProjectStore.ObjectMap(WentCadProjectStore.Thermal(project), "Walls");
            string wallId = WentCadProjectStore.Clean(args["WallId"]);
            var wall = walls[wallId] as JObject;
            if (wall == null) return "Error: Nie znaleziono WallId.";
            Patch(wall, args, "Kind");
            Patch(wall, args, "Message");
            if (args["Thickness"] != null) wall["Thickness"] = ToDouble(args["Thickness"], ToDouble(wall["Thickness"], 0));
            if (args["Confidence"] != null) wall["Confidence"] = ToDouble(args["Confidence"], ToDouble(wall["Confidence"], 0));
            WentCadProjectStore.Save(doc, project);
            WentCadProjectStore.SaveContractToDwg(doc, project, true);
            return wall.ToString(Formatting.Indented);
        }

        private static void Patch(JObject target, JObject source, string name)
        {
            if (source[name] != null) target[name] = WentCadProjectStore.Clean(source[name]) ?? "";
        }

        private static double ToDouble(JToken token, double fallback)
        {
            double value;
            return token != null && double.TryParse(token.ToString().Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : fallback;
        }
    }
}
