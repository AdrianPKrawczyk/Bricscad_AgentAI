using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class ConfigureWentCadEnvelopeTestBuildingTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ConfigureWentCadEnvelopeTestBuilding",
                    Description = "Wykonuje zbiorczy skan WATT dla standardowego budynku testowego WentCad z LISPa: Parter i Pietro_1, warstwy WC_TEST_SCIANY/WC_TEST_OKNA oraz atrybuty WIDTH/HEIGHT/SILL.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "RunRoomSetupFirst", new ToolParameter { Type = "boolean", Description = "Jesli true, najpierw uruchamia ConfigureWentCadTestBuilding. Domyslnie false." } }
                        }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{\"RunRoomSetupFirst\":true}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();
            bool setup = args["RunRoomSetupFirst"] != null && bool.TryParse(args["RunRoomSetupFirst"].ToString(), out bool parsed) && parsed;
            var reports = new JArray();
            if (setup)
            {
                reports.Add(JObject.Parse(new ConfigureWentCadTestBuildingTool().Execute(doc, new JObject { ["ProjectName"] = "WentCad LISP Test Building" })));
            }

            var scanTool = new ScanWentCadEnvelopeTool();
            foreach (string floorName in new[] { "Parter", "Pietro_1" })
            {
                string json = scanTool.Execute(doc, new JObject
                {
                    ["FloorName"] = floorName,
                    ["WallLayer"] = "WC_TEST_SCIANY",
                    ["WindowLayer"] = "WC_TEST_OKNA",
                    ["WindowLabelLayer"] = "WC_TEST_OPISY_OKIEN",
                    ["WindowWidthAttribute"] = "WIDTH",
                    ["WindowHeightAttribute"] = "HEIGHT",
                    ["WindowSillAttribute"] = "SILL",
                    ["MaxInteriorWallDistance"] = 15.0,
                    ["MaxExteriorConfirmDistance"] = 15.0,
                    ["MaxWindowSnapDistance"] = 25.0
                });
                reports.Add(JObject.Parse(json));
            }

            return new JObject
            {
                ["Reports"] = reports,
                ["Envelope"] = JObject.Parse(new ReadWentCadEnvelopeTool().Execute(doc, new JObject()))
            }.ToString(Formatting.Indented);
        }
    }
}
