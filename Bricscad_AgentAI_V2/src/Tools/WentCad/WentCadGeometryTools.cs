using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bricscad.ApplicationServices;
using Newtonsoft.Json.Linq;
using Teigha.Colors;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    internal static class WentCadGeometryTools
    {
        public const string FloorRegionLayer = "_WENTCAD_KONDYGNACJE";
        public const string FloorRegionRegApp = "WENTCAD_FLOOR_REGION";

        public static List<JObject> BuildRectangle(double minX, double minY, double maxX, double maxY)
        {
            return new List<JObject>
            {
                Point(minX, minY),
                Point(maxX, minY),
                Point(maxX, maxY),
                Point(minX, maxY)
            };
        }

        public static List<JObject> ReadPoints(JToken token)
        {
            var result = new List<JObject>();
            if (!(token is JArray array)) return result;
            foreach (var item in array)
            {
                if (item is JObject obj)
                {
                    result.Add(Point(ToDouble(obj["X"], 0), ToDouble(obj["Y"], 0)));
                }
                else if (item is JArray pair && pair.Count >= 2)
                {
                    result.Add(Point(ToDouble(pair[0], 0), ToDouble(pair[1], 0)));
                }
            }
            return result;
        }

        public static ObjectId CreateOrUpdateFloorRegionPolyline(Document doc, string floorId, IList<JObject> points, string existingHandle)
        {
            ObjectId resultId = ObjectId.Null;
            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                EnsureLayer(doc.Database, tr, FloorRegionLayer, 3, false, false);
                EnsureRegApp(doc.Database, tr, FloorRegionRegApp);

                Polyline polyline = null;
                ObjectId existingId;
                if (TryGetObjectId(doc.Database, existingHandle, out existingId))
                {
                    polyline = tr.GetObject(existingId, OpenMode.ForWrite) as Polyline;
                    if (polyline != null)
                    {
                        while (polyline.NumberOfVertices > 0)
                        {
                            polyline.RemoveVertexAt(polyline.NumberOfVertices - 1);
                        }
                    }
                }

                if (polyline == null)
                {
                    polyline = new Polyline();
                    var blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    var model = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                    resultId = model.AppendEntity(polyline);
                    tr.AddNewlyCreatedDBObject(polyline, true);
                }
                else
                {
                    resultId = polyline.ObjectId;
                }

                for (int i = 0; i < points.Count; i++)
                {
                    polyline.AddVertexAt(i, new Point2d(ToDouble(points[i]["X"], 0), ToDouble(points[i]["Y"], 0)), 0, 0, 0);
                }
                polyline.Closed = true;
                polyline.Layer = FloorRegionLayer;
                polyline.Color = Color.FromColorIndex(ColorMethod.ByAci, 3);
                polyline.XData = new ResultBuffer(
                    new TypedValue((short)DxfCode.ExtendedDataRegAppName, FloorRegionRegApp),
                    new TypedValue((short)DxfCode.ExtendedDataAsciiString, floorId ?? ""));

                tr.Commit();
            }
            return resultId;
        }

        public static ScanResult ScanRooms(Document doc, JObject project, JObject floor, JObject args)
        {
            string boundaryLayer = Clean(args["BoundaryLayer"]) ?? Clean(project["DetectionMapping"]?["BoundaryLayer"]);
            string tagLayer = Clean(args["TagLayer"]) ?? Clean(project["DetectionMapping"]?["TagLayer"]);
            string numberAttribute = Clean(args["NumberAttribute"]) ?? Clean(project["DetectionMapping"]?["NumberAttribute"]);
            string nameAttribute = Clean(args["NameAttribute"]) ?? Clean(project["DetectionMapping"]?["NameAttribute"]);
            string heightAttribute = Clean(args["HeightAttribute"]) ?? Clean(project["DetectionMapping"]?["HeightAttribute"]);
            string areaAttribute = Clean(args["AreaAttribute"]) ?? Clean(project["DetectionMapping"]?["AreaAttribute"]);

            var scan = new ScanResult();
            if (string.IsNullOrWhiteSpace(boundaryLayer) || string.IsNullOrWhiteSpace(tagLayer))
            {
                scan.Messages.Add("Brak BoundaryLayer albo TagLayer.");
                return scan;
            }

            var region = ReadPoints(floor["Region"]);
            if (region.Count < 3)
            {
                region = ReadPoints(args["RegionPoints"]);
            }
            if (region.Count < 3 && args["MinX"] != null && args["MinY"] != null && args["MaxX"] != null && args["MaxY"] != null)
            {
                region = BuildRectangle(ToDouble(args["MinX"], 0), ToDouble(args["MinY"], 0), ToDouble(args["MaxX"], 0), ToDouble(args["MaxY"], 0));
            }
            if (region.Count < 3)
            {
                scan.Messages.Add("Brak regionu kondygnacji. Ustaw RegionPoints albo MinX/MinY/MaxX/MaxY.");
                return scan;
            }

            var boundaries = new List<BoundaryData>();
            var tags = new List<TagData>();
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                var blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId entId in model)
                {
                    var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                    if (ent == null || ent.IsErased) continue;

                    if (string.Equals(ent.Layer, boundaryLayer, StringComparison.OrdinalIgnoreCase) && ent is Polyline polyline)
                    {
                        if (!polyline.Closed || polyline.NumberOfVertices < 3)
                        {
                            continue;
                        }

                        var points = PolylineToPoints(polyline);
                        var centroid = Centroid(points);
                        if (!PointInPolygon(centroid, region)) continue;
                        boundaries.Add(new BoundaryData
                        {
                            Handle = polyline.Handle.ToString(),
                            Points = points,
                            Area = PolygonArea(points)
                        });
                    }
                    else if (string.Equals(ent.Layer, tagLayer, StringComparison.OrdinalIgnoreCase) && ent is BlockReference blockReference)
                    {
                        var position = new Point2d(blockReference.Position.X, blockReference.Position.Y);
                        if (!PointInPolygon(position, region)) continue;
                        tags.Add(new TagData
                        {
                            Handle = blockReference.Handle.ToString(),
                            Position = position,
                            Attributes = ReadAttributes(tr, blockReference)
                        });
                    }
                }
                tr.Commit();
            }

            scan.Boundaries = boundaries.Count;
            scan.Tags = tags.Count;

            string floorId = Clean(floor["FloorId"]);
            double floorHeight = ToDouble(floor["HeightNet"], 3.0);
            var usedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var boundary in boundaries)
            {
                var matchedTags = tags.Where(t => PointInPolygon(t.Position, boundary.Points)).ToList();
                if (matchedTags.Count == 0)
                {
                    scan.UnmatchedBoundaries++;
                    continue;
                }

                var tag = matchedTags[0];
                usedTags.Add(tag.Handle);

                var roomArgs = new JObject
                {
                    ["FloorId"] = floorId,
                    ["Number"] = GetAttribute(tag, numberAttribute, ""),
                    ["Name"] = GetAttribute(tag, nameAttribute, ""),
                    ["BoundaryHandle"] = boundary.Handle,
                    ["TagHandle"] = tag.Handle,
                    ["Area"] = ReadDouble(GetAttribute(tag, areaAttribute, ""), boundary.Area),
                    ["Height"] = ReadDouble(GetAttribute(tag, heightAttribute, ""), floorHeight)
                };

                string supply = FirstSystem(project, "SUPPLY");
                string exhaust = FirstSystem(project, "EXHAUST");
                if (!string.IsNullOrWhiteSpace(supply)) roomArgs["SupplySystemId"] = supply;
                if (!string.IsNullOrWhiteSpace(exhaust)) roomArgs["ExhaustSystemId"] = exhaust;

                var room = UpsertRoomByBoundaryOrNumber(project, floorId, boundary.Handle, roomArgs);
                scan.Rooms.Add(room);
                scan.Matched++;
                if (matchedTags.Count > 1)
                {
                    scan.Messages.Add($"Obrys {boundary.Handle} zawiera {matchedTags.Count} metki; uzyto pierwszej ({tag.Handle}).");
                }
            }

            scan.UnmatchedTags = tags.Count(t => !usedTags.Contains(t.Handle));
            return scan;
        }

        public static JObject Point(double x, double y)
        {
            return new JObject { ["X"] = x, ["Y"] = y };
        }

        public static JArray ToJArray(IEnumerable<JObject> points)
        {
            var array = new JArray();
            foreach (var point in points)
            {
                array.Add(new JObject { ["X"] = ToDouble(point["X"], 0), ["Y"] = ToDouble(point["Y"], 0) });
            }
            return array;
        }

        public static double ToDouble(JToken token, double fallback)
        {
            if (token == null || token.Type == JTokenType.Null) return fallback;
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                try { return token.Value<double>(); }
                catch { return fallback; }
            }

            string raw = token.ToString().Trim();
            double value;
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out value)) return value;
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return value;
            raw = raw.Replace(',', '.');
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        public static string Clean(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            string value = token.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static JObject UpsertRoomByBoundaryOrNumber(JObject project, string floorId, string boundaryHandle, JObject roomArgs)
        {
            var rooms = WentCadProjectStore.ObjectMap(project, "Rooms");
            JObject room = rooms.Properties()
                .Select(p => p.Value as JObject)
                .FirstOrDefault(r => string.Equals(Clean(r?["BoundaryHandle"]), boundaryHandle, StringComparison.OrdinalIgnoreCase));

            if (room == null)
            {
                string number = Clean(roomArgs["Number"]);
                if (!string.IsNullOrWhiteSpace(number))
                {
                    room = rooms.Properties()
                        .Select(p => p.Value as JObject)
                        .FirstOrDefault(r =>
                            string.Equals(Clean(r?["FloorId"]), floorId, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(Clean(r?["Number"]), number, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (room != null)
            {
                roomArgs["RoomId"] = Clean(room["RoomId"]);
            }
            return WentCadProjectStore.UpsertRoom(project, roomArgs);
        }

        private static string FirstSystem(JObject project, string type)
        {
            return (project["Systems"] as JArray)?
                .Children<JObject>()
                .FirstOrDefault(s => string.Equals(Clean(s["Type"]), type, StringComparison.OrdinalIgnoreCase))?["SystemId"]?
                .ToString();
        }

        private static string GetAttribute(TagData tag, string key, string fallback)
        {
            if (string.IsNullOrWhiteSpace(key)) return fallback ?? "";
            return tag.Attributes.TryGetValue(key, out string value) ? value : (fallback ?? "");
        }

        private static double ReadDouble(string value, double fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            string normalized = value.Replace("m2", "").Replace("m²", "").Replace("m", "").Replace(" ", "").Replace(",", ".");
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? parsed : fallback;
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

        private static Dictionary<string, string> ReadAttributes(Transaction tr, BlockReference blockReference)
        {
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId attId in blockReference.AttributeCollection)
            {
                var att = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                if (att == null || att.IsErased) continue;
                attrs[att.Tag] = att.IsMTextAttribute ? att.MTextAttribute.Text : att.TextString;
            }
            return attrs;
        }

        private static Point2d Centroid(IList<JObject> pts)
        {
            if (pts == null || pts.Count == 0) return new Point2d();
            return new Point2d(pts.Average(p => ToDouble(p["X"], 0)), pts.Average(p => ToDouble(p["Y"], 0)));
        }

        private static double PolygonArea(IList<JObject> pts)
        {
            if (pts == null || pts.Count < 3) return 0;
            double area = 0;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                double xi = ToDouble(pts[i]["X"], 0);
                double yi = ToDouble(pts[i]["Y"], 0);
                double xj = ToDouble(pts[j]["X"], 0);
                double yj = ToDouble(pts[j]["Y"], 0);
                area += (xj + xi) * (yj - yi);
            }
            return Math.Abs(area / 2.0);
        }

        private static bool PointInPolygon(Point2d point, IList<JObject> polygon)
        {
            if (polygon == null || polygon.Count < 3) return true;
            bool inside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = ToDouble(polygon[i]["X"], 0);
                double yi = ToDouble(polygon[i]["Y"], 0);
                double xj = ToDouble(polygon[j]["X"], 0);
                double yj = ToDouble(polygon[j]["Y"], 0);
                bool intersect = ((yi > point.Y) != (yj > point.Y)) &&
                                 (point.X < (xj - xi) * (point.Y - yi) / (yj - yi) + xi);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        private static void EnsureLayer(Database db, Transaction tr, string layerName, short colorIndex, bool locked, bool plottable)
        {
            var table = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (table.Has(layerName)) return;
            table.UpgradeOpen();
            var record = new LayerTableRecord
            {
                Name = layerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex),
                IsLocked = locked,
                IsPlottable = plottable
            };
            table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        private static void EnsureRegApp(Database db, Transaction tr, string appName)
        {
            var table = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (table.Has(appName)) return;
            table.UpgradeOpen();
            var record = new RegAppTableRecord { Name = appName };
            table.Add(record);
            tr.AddNewlyCreatedDBObject(record, true);
        }

        private static bool TryGetObjectId(Database db, string handleString, out ObjectId id)
        {
            id = ObjectId.Null;
            if (string.IsNullOrWhiteSpace(handleString)) return false;
            if (!long.TryParse(handleString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long value)) return false;
            try
            {
                id = db.GetObjectId(false, new Handle(value), 0);
                return !id.IsNull && !id.IsErased;
            }
            catch
            {
                return false;
            }
        }

        private class BoundaryData
        {
            public string Handle { get; set; }
            public List<JObject> Points { get; set; }
            public double Area { get; set; }
        }

        private class TagData
        {
            public string Handle { get; set; }
            public Point2d Position { get; set; }
            public Dictionary<string, string> Attributes { get; set; }
        }
    }

    internal class ScanResult
    {
        public int Boundaries { get; set; }
        public int Tags { get; set; }
        public int Matched { get; set; }
        public int UnmatchedBoundaries { get; set; }
        public int UnmatchedTags { get; set; }
        public List<string> Messages { get; } = new List<string>();
        public List<JObject> Rooms { get; } = new List<JObject>();
    }
}
