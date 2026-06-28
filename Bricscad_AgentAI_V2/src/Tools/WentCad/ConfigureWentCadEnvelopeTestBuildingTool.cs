using System;
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
                    Description = "Wykonuje one-shot test WATT dla standardowego budynku testowego WentCad z LISPa GEN_WENTCAD_TEST_BUILDING: ustawia minimalny projekt, skanuje pomieszczenia, sciany, okna i drzwi dla Parter i Pietro_1, warstwy WC_TEST_SCIANY/WC_TEST_OKNA/WC_TEST_DRZWI oraz atrybuty WIDTH/HEIGHT/SILL. To wlasciwe narzedzie dla promptow WATT; uzyj raz zamiast ConfigureWentCadTestBuilding i recznej petli narzedzi.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "RunRoomSetupFirst", new ToolParameter { Type = "boolean", Description = "Jesli true, najpierw ustawia minimalne dane testowe i uruchamia ScanWentCadRooms dla obu kondygnacji. Domyslnie true." } },
                            { "ProjectName", new ToolParameter { Type = "string", Description = "Opcjonalna nazwa projektu zapisywana w minimalnym setupie WATT. Domyslnie WentCad LISP Test Building." } }
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
            bool setup = args["RunRoomSetupFirst"] == null || !bool.TryParse(args["RunRoomSetupFirst"].ToString(), out bool parsed) || parsed;
            string projectName = WentCadProjectStore.Clean(args["ProjectName"]);
            if (string.IsNullOrWhiteSpace(projectName)) projectName = "WentCad LISP Test Building";
            var reports = new JArray();
            if (setup)
            {
                reports.Add(RunStep("EnsureWentCadEnvelopeTestSetup", null, () => EnsureTestSetup(doc, projectName)));

                var roomScanTool = new ScanWentCadRoomsTool();
                foreach (string floorName in new[] { "Parter", "Pietro_1" })
                {
                    reports.Add(RunStep("ScanWentCadRooms", floorName, () =>
                        roomScanTool.Execute(doc, new JObject
                        {
                            ["FloorName"] = floorName,
                            ["WriteContractToDwg"] = false
                        })));
                }
            }

            var scanTool = new ScanWentCadEnvelopeTool();
            foreach (string floorName in new[] { "Parter", "Pietro_1" })
            {
                reports.Add(RunStep("ScanWentCadEnvelope", floorName, () =>
                    scanTool.Execute(doc, new JObject
                    {
                        ["FloorName"] = floorName,
                        ["WallLayer"] = "WC_TEST_SCIANY",
                        ["WindowLayer"] = "WC_TEST_OKNA",
                        ["WindowLabelLayer"] = "WC_TEST_OPISY_OKIEN",
                        ["WindowWidthAttribute"] = "WIDTH",
                        ["WindowHeightAttribute"] = "HEIGHT",
                        ["WindowSillAttribute"] = "SILL",
                        ["DoorLayer"] = "WC_TEST_DRZWI",
                        ["DoorLabelLayer"] = "WC_TEST_OPISY_DRZWI",
                        ["DoorWidthAttribute"] = "WIDTH",
                        ["DoorHeightAttribute"] = "HEIGHT",
                        ["DoorSillAttribute"] = "SILL",
                        ["MaxInteriorWallDistance"] = 15.0,
                        ["MaxExteriorConfirmDistance"] = 15.0,
                        ["MaxWindowSnapDistance"] = 25.0,
                        ["WriteContractToDwg"] = false
                    })));
            }

            return new JObject
            {
                ["Reports"] = reports,
                ["Envelope"] = ParseToolResult("ReadWentCadEnvelope", null, new ReadWentCadEnvelopeTool().Execute(doc, new JObject()))
            }.ToString(Formatting.Indented);
        }

        private static string EnsureTestSetup(Document doc, string projectName)
        {
            var project = WentCadProjectStore.LoadOrCreate(doc);
            project["ProjectName"] = projectName;
            project["DetectionMapping"] = new JObject
            {
                ["BoundaryLayer"] = "WC_TEST_OBRYSY",
                ["TagLayer"] = "WC_TEST_METKI",
                ["NumberAttribute"] = "NR",
                ["NameAttribute"] = "NAZWA",
                ["HeightAttribute"] = "H",
                ["AreaAttribute"] = "POW"
            };

            var parter = WentCadProjectStore.UpsertFloor(project, new JObject
            {
                ["Name"] = "Parter",
                ["Order"] = 0,
                ["Elevation"] = 0.0,
                ["HeightNet"] = 3.0,
                ["HeightTotal"] = 3.5,
                ["BasePointX"] = -50.0,
                ["BasePointY"] = -50.0,
                ["BasePointZ"] = 0.0,
                ["BasePointDescription"] = "Punkt bazowy Parter (-50, -50), jednostki cm"
            });
            var pietro = WentCadProjectStore.UpsertFloor(project, new JObject
            {
                ["Name"] = "Pietro_1",
                ["Order"] = 1,
                ["Elevation"] = 3.5,
                ["HeightNet"] = 3.2,
                ["HeightTotal"] = 3.5,
                ["BasePointX"] = -50.0,
                ["BasePointY"] = 1250.0,
                ["BasePointZ"] = 3.5,
                ["BasePointDescription"] = "Punkt bazowy Pietro_1 (-50, 1250), jednostki cm"
            });

            parter["Region"] = WentCadGeometryTools.ToJArray(WentCadGeometryTools.BuildRectangle(-50, -50, 1850, 1050));
            pietro["Region"] = WentCadGeometryTools.ToJArray(WentCadGeometryTools.BuildRectangle(-50, 1250, 1850, 2350));
            parter["DrukWidokiViewId"] = null;
            pietro["DrukWidokiViewId"] = null;

            WentCadProjectStore.Save(doc, project);
            return new JObject
            {
                ["ProjectPath"] = WentCadProjectStore.GetProjectPath(doc),
                ["ProjectName"] = projectName,
                ["Floors"] = new JArray("Parter", "Pietro_1"),
                ["ContractWriteSkipped"] = true,
                ["Message"] = "Ustawiono minimalny projekt WATT bez wywolywania ConfigureWentCadTestBuildingTool."
            }.ToString(Formatting.Indented);
        }

        private static JObject RunStep(string toolName, string floorName, Func<string> action)
        {
            try
            {
                return ParseToolResult(toolName, floorName, action());
            }
            catch (Exception ex)
            {
                return new JObject
                {
                    ["Tool"] = toolName,
                    ["FloorName"] = floorName,
                    ["Ok"] = false,
                    ["Error"] = ex.Message
                };
            }
        }

        private static JObject ParseToolResult(string toolName, string floorName, string content)
        {
            try
            {
                var parsed = JObject.Parse(content ?? "{}");
                parsed["Tool"] = toolName;
                if (!string.IsNullOrWhiteSpace(floorName)) parsed["FloorName"] = floorName;
                parsed["Ok"] = true;
                return parsed;
            }
            catch
            {
                return new JObject
                {
                    ["Tool"] = toolName,
                    ["FloorName"] = floorName,
                    ["Ok"] = false,
                    ["Error"] = content ?? ""
                };
            }
        }
    }
}
