using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class SetWentCadFloorRegionTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "SetWentCadFloorRegion",
                    Description = "Nieinteraktywnie ustawia region kondygnacji WentCad po punktach albo prostokacie MinX/MinY/MaxX/MaxY. Opcjonalnie tworzy/aktualizuje polilinie regionu w DWG z XData WENTCAD_FLOOR_REGION.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "FloorId", new ToolParameter { Type = "string", Description = "Id kondygnacji. Wymagane, jesli nie podano FloorName." } },
                            { "FloorName", new ToolParameter { Type = "string", Description = "Nazwa kondygnacji, np. Parter. Uzywana, gdy FloorId jest puste." } },
                            { "RegionPoints", new ToolParameter { Type = "array", Description = "Lista punktow regionu [{X,Y}, ...] albo [[x,y], ...].", Items = new JObject { ["type"] = "object" } } },
                            { "MinX", new ToolParameter { Type = "number", Description = "Minimalne X prostokata regionu." } },
                            { "MinY", new ToolParameter { Type = "number", Description = "Minimalne Y prostokata regionu." } },
                            { "MaxX", new ToolParameter { Type = "number", Description = "Maksymalne X prostokata regionu." } },
                            { "MaxY", new ToolParameter { Type = "number", Description = "Maksymalne Y prostokata regionu." } },
                            { "DrawPolyline", new ToolParameter { Type = "boolean", Description = "Czy utworzyc/aktualizowac polilinie regionu w DWG. Domyslnie true." } },
                            { "ExistingRegionHandle", new ToolParameter { Type = "string", Description = "Opcjonalny handle istniejacej polilinii regionu do aktualizacji." } }
                        }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{\"FloorName\":\"Parter\",\"MinX\":-3,\"MinY\":-3,\"MaxX\":63,\"MaxY\":38}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();
            var project = WentCadProjectStore.LoadOrCreate(doc);
            var floors = WentCadProjectStore.ObjectMap(project, "Floors");

            string floorId = WentCadProjectStore.Clean(args["FloorId"]);
            string floorName = WentCadProjectStore.Clean(args["FloorName"]);
            JObject floor = null;
            if (!string.IsNullOrWhiteSpace(floorId))
            {
                floor = floors[floorId] as JObject;
            }
            if (floor == null && !string.IsNullOrWhiteSpace(floorName))
            {
                foreach (var prop in floors.Properties())
                {
                    var candidate = prop.Value as JObject;
                    if (string.Equals(WentCadProjectStore.Clean(candidate?["Name"]), floorName, System.StringComparison.OrdinalIgnoreCase))
                    {
                        floor = candidate;
                        floorId = WentCadProjectStore.Clean(candidate["FloorId"]);
                        break;
                    }
                }
            }
            if (floor == null) return "Error: Nie znaleziono kondygnacji po FloorId/FloorName.";

            var points = WentCadGeometryTools.ReadPoints(args["RegionPoints"]);
            if (points.Count < 3 && args["MinX"] != null && args["MinY"] != null && args["MaxX"] != null && args["MaxY"] != null)
            {
                points = WentCadGeometryTools.BuildRectangle(
                    WentCadGeometryTools.ToDouble(args["MinX"], 0),
                    WentCadGeometryTools.ToDouble(args["MinY"], 0),
                    WentCadGeometryTools.ToDouble(args["MaxX"], 0),
                    WentCadGeometryTools.ToDouble(args["MaxY"], 0));
            }
            if (points.Count < 3) return "Error: Region musi miec co najmniej 3 punkty albo komplet MinX/MinY/MaxX/MaxY.";

            floor["Region"] = WentCadGeometryTools.ToJArray(points);
            floor["DrukWidokiViewId"] = null;

            bool draw = args["DrawPolyline"] == null || args["DrawPolyline"].Value<bool>();
            string handle = "";
            if (draw)
            {
                ObjectId id = WentCadGeometryTools.CreateOrUpdateFloorRegionPolyline(doc, floorId, points, WentCadProjectStore.Clean(args["ExistingRegionHandle"]));
                if (!id.IsNull)
                {
                    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        handle = ent?.Handle.ToString() ?? "";
                        tr.Commit();
                    }
                }
            }

            WentCadProjectStore.Save(doc, project);
            WentCadProjectStore.SaveContractToDwg(doc, project, false);
            return new JObject
            {
                ["FloorId"] = floorId,
                ["FloorName"] = floor["Name"],
                ["RegionPointCount"] = points.Count,
                ["RegionHandle"] = handle,
                ["Summary"] = WentCadProjectStore.BuildSummary(project, WentCadProjectStore.GetProjectPath(doc))
            }.ToString(Formatting.Indented);
        }
    }
}
