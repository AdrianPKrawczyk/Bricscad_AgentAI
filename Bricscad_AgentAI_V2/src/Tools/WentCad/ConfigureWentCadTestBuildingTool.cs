using System;
using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class ConfigureWentCadTestBuildingTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ConfigureWentCadTestBuilding",
                    Description = "Jednym wywolaniem konfiguruje standardowy budynek testowy WentCad z LISPa GEN_WENTCAD_TEST_BUILDING: mapping, kondygnacje, regiony, skan pomieszczen, systemy N1/W1/N2/W2, tryby bilansu, przeliczenie oraz NOD/XData.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "ProjectName", new ToolParameter { Type = "string", Description = "Nazwa projektu. Domyslnie WentCad LISP Test Building." } },
                            { "BoundaryLayer", new ToolParameter { Type = "string", Description = "Warstwa obrysow. Domyslnie WC_TEST_OBRYSY." } },
                            { "TagLayer", new ToolParameter { Type = "string", Description = "Warstwa metek. Domyslnie WC_TEST_METKI." } },
                            { "NumberAttribute", new ToolParameter { Type = "string", Description = "Atrybut numeru. Domyslnie NR." } },
                            { "NameAttribute", new ToolParameter { Type = "string", Description = "Atrybut nazwy. Domyslnie NAZWA." } },
                            { "HeightAttribute", new ToolParameter { Type = "string", Description = "Atrybut wysokosci. Domyslnie H." } },
                            { "AreaAttribute", new ToolParameter { Type = "string", Description = "Atrybut powierzchni. Domyslnie POW." } },
                            { "DrawRegions", new ToolParameter { Type = "boolean", Description = "Czy rysowac/aktualizowac polilinie regionow kondygnacji. Domyslnie true." } },
                            { "CleanupOrphans", new ToolParameter { Type = "boolean", Description = "Czy usunac pomieszczenia bez FloorId i bez uchwytow obrysu/metki. Domyslnie true." } }
                        }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{\"ProjectName\":\"WentCad LISP Test Building\"}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();
            var project = WentCadProjectStore.LoadOrCreate(doc);

            project["ProjectName"] = WentCadProjectStore.Clean(args["ProjectName"]) ?? "WentCad LISP Test Building";
            var mapping = project["DetectionMapping"] as JObject ?? new JObject();
            mapping["BoundaryLayer"] = WentCadProjectStore.Clean(args["BoundaryLayer"]) ?? "WC_TEST_OBRYSY";
            mapping["TagLayer"] = WentCadProjectStore.Clean(args["TagLayer"]) ?? "WC_TEST_METKI";
            mapping["NumberAttribute"] = WentCadProjectStore.Clean(args["NumberAttribute"]) ?? "NR";
            mapping["NameAttribute"] = WentCadProjectStore.Clean(args["NameAttribute"]) ?? "NAZWA";
            mapping["HeightAttribute"] = WentCadProjectStore.Clean(args["HeightAttribute"]) ?? "H";
            mapping["AreaAttribute"] = WentCadProjectStore.Clean(args["AreaAttribute"]) ?? "POW";
            project["DetectionMapping"] = mapping;

            bool cleanup = args["CleanupOrphans"] == null || args["CleanupOrphans"].Value<bool>();
            int removedOrphans = cleanup ? RemoveOrphanRooms(project) : 0;

            JObject parter = WentCadProjectStore.UpsertFloor(project, new JObject
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
            JObject pietro = WentCadProjectStore.UpsertFloor(project, new JObject
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

            var parterRegion = WentCadGeometryTools.BuildRectangle(-50, -50, 1850, 1050);
            var pietroRegion = WentCadGeometryTools.BuildRectangle(-50, 1250, 1850, 2350);
            parter["Region"] = WentCadGeometryTools.ToJArray(parterRegion);
            pietro["Region"] = WentCadGeometryTools.ToJArray(pietroRegion);
            parter["DrukWidokiViewId"] = null;
            pietro["DrukWidokiViewId"] = null;

            bool drawRegions = args["DrawRegions"] == null || args["DrawRegions"].Value<bool>();
            var regionHandles = new JObject();
            if (drawRegions)
            {
                regionHandles["Parter"] = CreateRegionAndReadHandle(doc, WentCadProjectStore.Clean(parter["FloorId"]), parterRegion);
                regionHandles["Pietro_1"] = CreateRegionAndReadHandle(doc, WentCadProjectStore.Clean(pietro["FloorId"]), pietroRegion);
            }

            var parterScan = WentCadGeometryTools.ScanRooms(doc, project, parter, new JObject());
            var pietroScan = WentCadGeometryTools.ScanRooms(doc, project, pietro, new JObject());

            UpsertSystem(project, "N1", "Nawiew 1", "SUPPLY", 5);
            UpsertSystem(project, "W1", "Wywiew 1", "EXHAUST", 1);
            UpsertSystem(project, "N2", "Nawiew 2", "SUPPLY", 5);
            UpsertSystem(project, "W2", "Wywiew 2", "EXHAUST", 5);

            AssignFloorRooms(project, WentCadProjectStore.Clean(parter["FloorId"]), "N1", "W1");
            AssignFloorRooms(project, WentCadProjectStore.Clean(pietro["FloorId"]), "N2", "W2");

            SafeUpdate(project, "Parter", "0.01", new JObject { ["CalculationMode"] = "AUTO_MAX" });
            SafeUpdate(project, "Parter", "0.02", new JObject { ["CalculationMode"] = "ACH_ONLY", ["IsTargetAchManual"] = true, ["ManualTargetAch"] = 1.5 });
            SafeUpdate(project, "Parter", "0.03", new JObject { ["CalculationMode"] = "HYGIENIC_ONLY", ["Occupants"] = 4, ["DosePerOccupant"] = 30.0 });
            SafeUpdate(project, "Parter", "0.04", new JObject { ["CalculationMode"] = "MANUAL", ["ManualSupply"] = 900.0, ["ManualExhaust"] = 850.0 });
            SafeUpdate(project, "Parter", "0.05", new JObject { ["CalculationMode"] = "AUTO_MAX", ["Occupants"] = 8 });
            SafeUpdate(project, "Pietro_1", "1.05", new JObject { ["ActivityType"] = "Pomieszczenie socjalne" });

            WentCadProjectStore.Recalculate(project);
            WentCadProjectStore.Save(doc, project);
            WentCadProjectStore.SaveContractToDwg(doc, project, true);

            var diagnostics = BuildDiagnostics(project);
            return new JObject
            {
                ["ProjectPath"] = WentCadProjectStore.GetProjectPath(doc),
                ["RemovedOrphanRooms"] = removedOrphans,
                ["RegionHandles"] = regionHandles,
                ["Scan"] = new JObject
                {
                    ["Parter"] = BuildScanSummary(parterScan),
                    ["Pietro_1"] = BuildScanSummary(pietroScan)
                },
                ["Diagnostics"] = diagnostics,
                ["Summary"] = WentCadProjectStore.BuildSummary(project, WentCadProjectStore.GetProjectPath(doc)),
                ["Systems"] = project["Systems"]
            }.ToString(Formatting.Indented);
        }

        private static int RemoveOrphanRooms(JObject project)
        {
            var rooms = WentCadProjectStore.ObjectMap(project, "Rooms");
            var remove = rooms.Properties()
                .Where(p =>
                {
                    var room = p.Value as JObject;
                    return room != null &&
                           string.IsNullOrWhiteSpace(WentCadProjectStore.Clean(room["FloorId"])) &&
                           string.IsNullOrWhiteSpace(WentCadProjectStore.Clean(room["BoundaryHandle"])) &&
                           string.IsNullOrWhiteSpace(WentCadProjectStore.Clean(room["TagHandle"]));
                })
                .Select(p => p.Name)
                .ToList();
            foreach (string key in remove) rooms.Remove(key);
            return remove.Count;
        }

        private static string CreateRegionAndReadHandle(Document doc, string floorId, IList<JObject> points)
        {
            ObjectId id = WentCadGeometryTools.CreateOrUpdateFloorRegionPolyline(doc, floorId, points, null);
            if (id.IsNull) return "";
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                string handle = ent?.Handle.ToString() ?? "";
                tr.Commit();
                return handle;
            }
        }

        private static void UpsertSystem(JObject project, string id, string name, string type, int color)
        {
            WentCadProjectStore.UpsertSystem(project, new JObject
            {
                ["SystemId"] = id,
                ["Name"] = name,
                ["Type"] = type,
                ["ColorIndex"] = color
            });
        }

        private static void AssignFloorRooms(JObject project, string floorId, string supplySystemId, string exhaustSystemId)
        {
            foreach (JObject room in WentCadProjectStore.ObjectMap(project, "Rooms").Properties().Select(p => p.Value).OfType<JObject>())
            {
                if (!string.Equals(WentCadProjectStore.Clean(room["FloorId"]), floorId, StringComparison.OrdinalIgnoreCase)) continue;
                room["SupplySystemId"] = supplySystemId;
                room["ExhaustSystemId"] = exhaustSystemId;
            }
        }

        private static void SafeUpdate(JObject project, string floorName, string number, JObject patch)
        {
            patch["FloorName"] = floorName;
            patch["Number"] = number;
            WentCadProjectStore.UpdateExistingRoomByNumber(project, patch);
        }

        private static JObject BuildScanSummary(ScanResult scan)
        {
            return new JObject
            {
                ["Boundaries"] = scan.Boundaries,
                ["Tags"] = scan.Tags,
                ["Matched"] = scan.Matched,
                ["UnmatchedBoundaries"] = scan.UnmatchedBoundaries,
                ["UnmatchedTags"] = scan.UnmatchedTags,
                ["Messages"] = new JArray(scan.Messages)
            };
        }

        private static JObject BuildDiagnostics(JObject project)
        {
            var rooms = WentCadProjectStore.ObjectMap(project, "Rooms").Properties().Select(p => p.Value).OfType<JObject>().ToList();
            int orphans = rooms.Count(r => string.IsNullOrWhiteSpace(WentCadProjectStore.Clean(r["FloorId"])));
            int withoutBoundary = rooms.Count(r => string.IsNullOrWhiteSpace(WentCadProjectStore.Clean(r["BoundaryHandle"])));
            var duplicateKeys = rooms
                .Where(r => !string.IsNullOrWhiteSpace(WentCadProjectStore.Clean(r["FloorId"])) && !string.IsNullOrWhiteSpace(WentCadProjectStore.Clean(r["Number"])))
                .GroupBy(r => WentCadProjectStore.Clean(r["FloorId"]) + "|" + WentCadProjectStore.Clean(r["Number"]), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            return new JObject
            {
                ["Rooms"] = rooms.Count,
                ["OrphanRooms"] = orphans,
                ["RoomsWithoutBoundary"] = withoutBoundary,
                ["DuplicateFloorNumberKeys"] = new JArray(duplicateKeys)
            };
        }
    }
}
