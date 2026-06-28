using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class ScanWentCadEnvelopeTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ScanWentCadEnvelope",
                    Description = "Nieinteraktywnie wykrywa sciany WATT i okna WentCad dla kondygnacji z obrysow pomieszczen, warstwy scian i warstwy/blokow okien. Zapisuje .wentcad oraz NOD WENTCAD_WALLS/WENTCAD_WINDOWS.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "FloorId", new ToolParameter { Type = "string", Description = "Id kondygnacji. Wymagane, jesli nie podano FloorName." } },
                            { "FloorName", new ToolParameter { Type = "string", Description = "Nazwa kondygnacji." } },
                            { "WallLayer", new ToolParameter { Type = "string", Description = "Warstwa scian pomocniczych, np. WC_TEST_SCIANY." } },
                            { "WindowLayer", new ToolParameter { Type = "string", Description = "Warstwa okien, np. WC_TEST_OKNA." } },
                            { "WindowBlockNamePattern", new ToolParameter { Type = "string", Description = "Fragment nazwy bloku okna." } },
                            { "WindowLabelLayer", new ToolParameter { Type = "string", Description = "Warstwa blokow opisow okien." } },
                            { "WindowLabelBlockNamePattern", new ToolParameter { Type = "string", Description = "Fragment nazwy bloku opisu okna." } },
                            { "WindowWidthAttribute", new ToolParameter { Type = "string", Description = "Atrybut szerokosci. Domyslnie WIDTH." } },
                            { "WindowHeightAttribute", new ToolParameter { Type = "string", Description = "Atrybut wysokosci. Domyslnie HEIGHT." } },
                            { "WindowSillAttribute", new ToolParameter { Type = "string", Description = "Atrybut parapetu. Domyslnie SILL." } },
                            { "MaxInteriorWallDistance", new ToolParameter { Type = "number", Description = "Maksymalny dystans miedzy obrysami pomieszczen dla sciany wewnetrznej. Dla LISPa testowego z przerwa 12 uzyj 12.5." } },
                            { "MaxExteriorConfirmDistance", new ToolParameter { Type = "number", Description = "Maksymalny dystans potwierdzenia sciany zewnetrznej." } },
                            { "MaxWindowSnapDistance", new ToolParameter { Type = "number", Description = "Maksymalny dystans przypiecia okna do sciany zewnetrznej." } }
                        }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{\"FloorName\":\"Parter\",\"WallLayer\":\"WC_TEST_SCIANY\",\"WindowLayer\":\"WC_TEST_OKNA\"}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";
            args = args ?? new JObject();
            var project = WentCadProjectStore.LoadOrCreate(doc);
            var floor = FindFloor(project, args);
            if (floor == null) return "Error: Nie znaleziono kondygnacji.";
            var settings = (JObject)WentCadProjectStore.Thermal(project)["Settings"];
            foreach (string key in new[] { "WallLayer", "WindowLayer", "WindowBlockNamePattern", "WindowLabelLayer", "WindowLabelBlockNamePattern", "WindowWidthAttribute", "WindowHeightAttribute", "WindowSillAttribute" })
            {
                if (args[key] != null) settings[key] = WentCadProjectStore.Clean(args[key]) ?? "";
            }
            foreach (string key in new[] { "MaxInteriorWallDistance", "MaxExteriorConfirmDistance", "MaxWindowSnapDistance" })
            {
                if (args[key] != null) settings[key] = ToDouble(args[key], ToDouble(settings[key], 0));
            }

            string floorId = WentCadProjectStore.Clean(floor["FloorId"]);
            var scan = Scan(doc, project, floor, settings);
            WentCadProjectStore.Save(doc, project);
            WentCadProjectStore.SaveContractToDwg(doc, project, true);
            return new JObject
            {
                ["FloorId"] = floorId,
                ["FloorName"] = floor["Name"],
                ["Walls"] = scan.Walls,
                ["ExternalWalls"] = scan.ExternalWalls,
                ["InternalWalls"] = scan.InternalWalls,
                ["UnresolvedWalls"] = scan.UnresolvedWalls,
                ["WindowCandidates"] = scan.WindowCandidates,
                ["WindowsAssigned"] = scan.WindowsAssigned,
                ["Messages"] = new JArray(scan.Messages),
                ["Summary"] = WentCadProjectStore.BuildSummary(project, WentCadProjectStore.GetProjectPath(doc))
            }.ToString(Formatting.Indented);
        }

        private static ScanEnvelopeResult Scan(Document doc, JObject project, JObject floor, JObject settings)
        {
            string floorId = WentCadProjectStore.Clean(floor["FloorId"]);
            var result = new ScanEnvelopeResult();
            var rooms = WentCadProjectStore.ObjectMap(project, "Rooms").Properties()
                .Select(p => p.Value as JObject)
                .Where(r => string.Equals(WentCadProjectStore.Clean(r?["FloorId"]), floorId, StringComparison.OrdinalIgnoreCase))
                .Where(r => !string.IsNullOrWhiteSpace(WentCadProjectStore.Clean(r?["BoundaryHandle"])))
                .ToList();
            var roomSegments = new List<Segment>();
            var wallHints = new List<Segment>();
            var windowCandidates = new List<WindowCandidate>();
            var region = WentCadGeometryTools.ReadPoints(floor["Region"]);

            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var room in rooms)
                {
                    ObjectId id;
                    if (!TryGetObjectId(doc.Database, WentCadProjectStore.Clean(room["BoundaryHandle"]), out id)) continue;
                    var poly = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                    if (poly == null || !poly.Closed || poly.NumberOfVertices < 3) continue;
                    var points = PolylineToPoints(poly);
                    for (int i = 0; i < points.Count; i++)
                    {
                        roomSegments.Add(new Segment
                        {
                            Room = room,
                            Index = i,
                            P1 = points[i],
                            P2 = points[(i + 1) % points.Count],
                            Handle = WentCadProjectStore.Clean(room["BoundaryHandle"])
                        });
                    }
                }

                var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId entId in model)
                {
                    var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                    if (ent == null || ent.IsErased) continue;
                    string layer = ent.Layer ?? "";
                    if (Matches(layer, settings["WallLayer"]?.ToString()) && ent is Line line)
                    {
                        wallHints.Add(new Segment { P1 = Point(line.StartPoint.X, line.StartPoint.Y), P2 = Point(line.EndPoint.X, line.EndPoint.Y) });
                    }
                    else if (Matches(layer, settings["WindowLayer"]?.ToString()))
                    {
                        AddWindowCandidate(tr, ent, settings, windowCandidates);
                    }
                }
                tr.Commit();
            }

            var walls = new JObject();
            foreach (var segment in roomSegments)
            {
                var adjacent = roomSegments.Where(s => s.Room != segment.Room)
                    .Select(s => Match(segment, s))
                    .Where(m => m.Overlap > 0.01 && m.Distance <= ToDouble(settings["MaxInteriorWallDistance"], 0.6))
                    .OrderBy(m => m.Distance)
                    .FirstOrDefault();
                string kind = adjacent.Other != null ? "INTERNAL" : "UNRESOLVED";
                string adjacentRoomId = adjacent.Other != null ? WentCadProjectStore.Clean(adjacent.Other.Room["RoomId"]) : "";
                double thickness = adjacent.Other != null ? adjacent.Distance : 0;
                double confidence = adjacent.Other != null ? 0.85 : 0.35;
                string message = "";
                if (adjacent.Other == null && wallHints.Any(h => Match(segment, h).Overlap > 0.01 && DistanceMid(segment, h) <= ToDouble(settings["MaxExteriorConfirmDistance"], 1.2)))
                {
                    kind = "EXTERNAL";
                    thickness = 0.35;
                    confidence = 0.9;
                }
                else if (adjacent.Other == null)
                {
                    message = "Brak sasiada i potwierdzenia na warstwie scian.";
                }

                string wallId = "wall-" + WentCadProjectStore.Clean(segment.Room["RoomId"]) + "-" + segment.Index;
                walls[wallId] = new JObject
                {
                    ["WallId"] = wallId,
                    ["FloorId"] = floorId,
                    ["RoomId"] = segment.Room["RoomId"],
                    ["Kind"] = kind,
                    ["P1"] = segment.P1,
                    ["P2"] = segment.P2,
                    ["Length"] = Math.Round(Distance(segment.P1, segment.P2), 3),
                    ["Thickness"] = thickness,
                    ["Azimuth"] = Math.Round(Azimuth(segment), 2),
                    ["AdjacentRoomId"] = adjacentRoomId,
                    ["SourceRoomBoundaryHandle"] = segment.Handle,
                    ["Confidence"] = confidence,
                    ["Message"] = message
                };
            }

            var windows = new JObject();
            var externalWalls = walls.Properties().Select(p => p.Value as JObject).Where(w => WentCadProjectStore.Clean(w?["Kind"]) == "EXTERNAL").ToList();
            result.WindowCandidates = windowCandidates.Count;
            foreach (var candidate in windowCandidates)
            {
                var best = externalWalls.Select(w => Project(candidate.Point, w)).Where(p => p.Distance <= ToDouble(settings["MaxWindowSnapDistance"], 1.0)).OrderBy(p => p.Distance).FirstOrDefault();
                if (best == null)
                {
                    result.Messages.Add("Okno " + candidate.Handle + ": nie znaleziono sciany zewnetrznej.");
                    continue;
                }
                double width = candidate.Width > 0 ? candidate.Width : ToDouble(settings["DefaultWindowWidth"], 1.2);
                double height = candidate.Height > 0 ? candidate.Height : ToDouble(settings["DefaultWindowHeight"], 1.5);
                string windowId = "win-" + candidate.Handle;
                windows[windowId] = new JObject
                {
                    ["WindowId"] = windowId,
                    ["FloorId"] = best.Wall["FloorId"],
                    ["RoomId"] = best.Wall["RoomId"],
                    ["WallId"] = best.Wall["WallId"],
                    ["BlockHandle"] = candidate.Handle,
                    ["LabelHandle"] = "",
                    ["BlockName"] = candidate.Name,
                    ["Width"] = width,
                    ["Height"] = height,
                    ["SillHeight"] = candidate.Sill,
                    ["Area"] = Math.Round(width * height, 3),
                    ["Placement"] = Math.Round(best.T * ToDouble(best.Wall["Length"], 0), 3),
                    ["Confidence"] = candidate.Width > 0 && candidate.Height > 0 ? 0.9 : 0.65,
                    ["Message"] = candidate.Message
                };
                result.WindowsAssigned++;
            }

            var thermal = WentCadProjectStore.Thermal(project);
            ReplaceFloorMap(WentCadProjectStore.ObjectMap(thermal, "Walls"), floorId, "FloorId", walls);
            ReplaceFloorMap(WentCadProjectStore.ObjectMap(thermal, "Windows"), floorId, "FloorId", windows);
            result.Walls = walls.Count;
            result.ExternalWalls = walls.Properties().Count(p => WentCadProjectStore.Clean(p.Value["Kind"]) == "EXTERNAL");
            result.InternalWalls = walls.Properties().Count(p => WentCadProjectStore.Clean(p.Value["Kind"]) == "INTERNAL");
            result.UnresolvedWalls = walls.Properties().Count(p => WentCadProjectStore.Clean(p.Value["Kind"]) == "UNRESOLVED");
            return result;
        }

        private static JObject FindFloor(JObject project, JObject args)
        {
            string floorId = WentCadProjectStore.Clean(args["FloorId"]);
            string floorName = WentCadProjectStore.Clean(args["FloorName"]);
            return WentCadProjectStore.ObjectMap(project, "Floors").Properties()
                .Select(p => p.Value as JObject)
                .FirstOrDefault(f =>
                    (!string.IsNullOrWhiteSpace(floorId) && string.Equals(WentCadProjectStore.Clean(f?["FloorId"]), floorId, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrWhiteSpace(floorName) && string.Equals(WentCadProjectStore.Clean(f?["Name"]), floorName, StringComparison.OrdinalIgnoreCase)));
        }

        private static void AddWindowCandidate(Transaction tr, Entity ent, JObject settings, List<WindowCandidate> candidates)
        {
            if (ent is BlockReference br)
            {
                var attrs = ReadAttributes(tr, br);
                double width = ReadAttr(attrs, settings["WindowWidthAttribute"]?.ToString(), 0);
                double height = ReadAttr(attrs, settings["WindowHeightAttribute"]?.ToString(), 0);
                candidates.Add(new WindowCandidate
                {
                    Handle = br.Handle.ToString(),
                    Name = br.Name,
                    Point = new JObject { ["X"] = br.Position.X, ["Y"] = br.Position.Y },
                    Width = Normalize(width),
                    Height = Normalize(height),
                    Sill = Normalize(ReadAttr(attrs, settings["WindowSillAttribute"]?.ToString(), ToDouble(settings["DefaultWindowSillHeight"], 0.9)))
                });
            }
            else if (ent is Line line)
            {
                candidates.Add(new WindowCandidate
                {
                    Handle = ent.Handle.ToString(),
                    Name = "Line",
                    Point = new JObject { ["X"] = (line.StartPoint.X + line.EndPoint.X) / 2.0, ["Y"] = (line.StartPoint.Y + line.EndPoint.Y) / 2.0 },
                    Width = Distance(Point(line.StartPoint.X, line.StartPoint.Y), Point(line.EndPoint.X, line.EndPoint.Y)),
                    Height = ToDouble(settings["DefaultWindowHeight"], 1.5),
                    Sill = ToDouble(settings["DefaultWindowSillHeight"], 0.9),
                    Message = "Okno wykryte z linii; wysokosc/parapet z domyslnych."
                });
            }
        }

        private static void ReplaceFloorMap(JObject target, string floorId, string floorField, JObject source)
        {
            foreach (var key in target.Properties().Where(p => string.Equals(WentCadProjectStore.Clean(p.Value[floorField]), floorId, StringComparison.OrdinalIgnoreCase)).Select(p => p.Name).ToList())
            {
                target.Remove(key);
            }
            foreach (var prop in source.Properties()) target[prop.Name] = prop.Value;
        }

        private static Projection Project(JObject point, JObject wall)
        {
            var p1 = wall["P1"] as JObject;
            var p2 = wall["P2"] as JObject;
            double vx = ToDouble(p2["X"], 0) - ToDouble(p1["X"], 0);
            double vy = ToDouble(p2["Y"], 0) - ToDouble(p1["Y"], 0);
            double len2 = vx * vx + vy * vy;
            if (len2 <= 0) return null;
            double t = ((ToDouble(point["X"], 0) - ToDouble(p1["X"], 0)) * vx + (ToDouble(point["Y"], 0) - ToDouble(p1["Y"], 0)) * vy) / len2;
            t = Math.Max(0, Math.Min(1, t));
            double x = ToDouble(p1["X"], 0) + t * vx;
            double y = ToDouble(p1["Y"], 0) + t * vy;
            return new Projection { Wall = wall, T = t, Distance = Math.Sqrt(Math.Pow(ToDouble(point["X"], 0) - x, 2) + Math.Pow(ToDouble(point["Y"], 0) - y, 2)) };
        }

        private static MatchResult Match(Segment a, Segment b)
        {
            if (!Parallel(a, b)) return new MatchResult();
            double vx = ToDouble(a.P2["X"], 0) - ToDouble(a.P1["X"], 0);
            double vy = ToDouble(a.P2["Y"], 0) - ToDouble(a.P1["Y"], 0);
            double len2 = vx * vx + vy * vy;
            if (len2 <= 0) return new MatchResult();
            double t1 = ((ToDouble(b.P1["X"], 0) - ToDouble(a.P1["X"], 0)) * vx + (ToDouble(b.P1["Y"], 0) - ToDouble(a.P1["Y"], 0)) * vy) / len2;
            double t2 = ((ToDouble(b.P2["X"], 0) - ToDouble(a.P1["X"], 0)) * vx + (ToDouble(b.P2["Y"], 0) - ToDouble(a.P1["Y"], 0)) * vy) / len2;
            double overlap = Math.Max(0, Math.Min(1, Math.Max(t1, t2)) - Math.Max(0, Math.Min(t1, t2))) * Distance(a.P1, a.P2);
            return new MatchResult { Other = b, Overlap = overlap, Distance = DistanceMid(a, b) };
        }

        private static bool Parallel(Segment a, Segment b)
        {
            double delta = Math.Abs(Angle(a) - Angle(b));
            delta = Math.Min(delta, 180 - delta);
            return delta <= 2.0;
        }

        private static double Angle(Segment s)
        {
            double angle = Math.Atan2(ToDouble(s.P2["Y"], 0) - ToDouble(s.P1["Y"], 0), ToDouble(s.P2["X"], 0) - ToDouble(s.P1["X"], 0)) * 180 / Math.PI;
            if (angle < 0) angle += 180;
            if (angle >= 180) angle -= 180;
            return angle;
        }

        private static double Azimuth(Segment s)
        {
            double angle = Math.Atan2(ToDouble(s.P2["Y"], 0) - ToDouble(s.P1["Y"], 0), ToDouble(s.P2["X"], 0) - ToDouble(s.P1["X"], 0)) * 180 / Math.PI;
            double azimuth = angle + 180;
            while (azimuth < 0) azimuth += 360;
            while (azimuth >= 360) azimuth -= 360;
            return azimuth;
        }

        private static double DistanceMid(Segment a, Segment b)
        {
            var mid = new JObject { ["X"] = (ToDouble(a.P1["X"], 0) + ToDouble(a.P2["X"], 0)) / 2.0, ["Y"] = (ToDouble(a.P1["Y"], 0) + ToDouble(a.P2["Y"], 0)) / 2.0 };
            return DistancePointToSegment(mid, b.P1, b.P2);
        }

        private static double DistancePointToSegment(JObject p, JObject v, JObject w)
        {
            double len2 = Math.Pow(ToDouble(v["X"], 0) - ToDouble(w["X"], 0), 2) + Math.Pow(ToDouble(v["Y"], 0) - ToDouble(w["Y"], 0), 2);
            if (len2 <= 0) return Distance(p, v);
            double t = ((ToDouble(p["X"], 0) - ToDouble(v["X"], 0)) * (ToDouble(w["X"], 0) - ToDouble(v["X"], 0)) + (ToDouble(p["Y"], 0) - ToDouble(v["Y"], 0)) * (ToDouble(w["Y"], 0) - ToDouble(v["Y"], 0))) / len2;
            t = Math.Max(0, Math.Min(1, t));
            return Distance(p, new JObject { ["X"] = ToDouble(v["X"], 0) + t * (ToDouble(w["X"], 0) - ToDouble(v["X"], 0)), ["Y"] = ToDouble(v["Y"], 0) + t * (ToDouble(w["Y"], 0) - ToDouble(v["Y"], 0)) });
        }

        private static List<JObject> PolylineToPoints(Polyline polyline)
        {
            var points = new List<JObject>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                var pt = polyline.GetPoint2dAt(i);
                points.Add(Point(pt.X, pt.Y));
            }
            return points;
        }

        private static Dictionary<string, string> ReadAttributes(Transaction tr, BlockReference br)
        {
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId attId in br.AttributeCollection)
            {
                var att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                if (att == null || att.IsErased) continue;
                attrs[att.Tag] = att.IsMTextAttribute ? att.MTextAttribute.Text : att.TextString;
            }
            return attrs;
        }

        private static double ReadAttr(Dictionary<string, string> attrs, string key, double fallback)
        {
            if (string.IsNullOrWhiteSpace(key)) return fallback;
            string value;
            return attrs.TryGetValue(key, out value) ? ToDouble(value, fallback) : fallback;
        }

        private static bool TryGetObjectId(Database db, string handle, out ObjectId id)
        {
            id = ObjectId.Null;
            long value;
            if (string.IsNullOrWhiteSpace(handle) || !long.TryParse(handle, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) return false;
            try { id = db.GetObjectId(false, new Handle(value), 0); return !id.IsNull && !id.IsErased; }
            catch { return false; }
        }

        private static bool Matches(string value, string pattern) => !string.IsNullOrWhiteSpace(pattern) && !string.IsNullOrWhiteSpace(value) && value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        private static JObject Point(double x, double y) => new JObject { ["X"] = x, ["Y"] = y };
        private static double Distance(JObject a, JObject b) => Math.Sqrt(Math.Pow(ToDouble(a["X"], 0) - ToDouble(b["X"], 0), 2) + Math.Pow(ToDouble(a["Y"], 0) - ToDouble(b["Y"], 0), 2));
        private static double Normalize(double v) => v > 500 ? v / 1000.0 : (v > 20 ? v / 100.0 : v);
        private static double ToDouble(JToken token, double fallback) => WentCadGeometryTools.ToDouble(token, fallback);
        private static double ToDouble(string raw, double fallback) => WentCadGeometryTools.ToDouble(raw == null ? null : JToken.FromObject(raw), fallback);

        private class Segment { public JObject Room; public int Index; public JObject P1; public JObject P2; public string Handle; }
        private class MatchResult { public Segment Other; public double Overlap; public double Distance; }
        private class Projection { public JObject Wall; public double T; public double Distance; }
        private class WindowCandidate { public string Handle; public string Name; public JObject Point; public double Width; public double Height; public double Sill; public string Message; }
        private class ScanEnvelopeResult
        {
            public int Walls;
            public int ExternalWalls;
            public int InternalWalls;
            public int UnresolvedWalls;
            public int WindowCandidates;
            public int WindowsAssigned;
            public List<string> Messages = new List<string>();
        }
    }
}
