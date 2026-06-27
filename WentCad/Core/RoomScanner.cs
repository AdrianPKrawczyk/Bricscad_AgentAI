using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using WentCad.Models;

namespace WentCad.Core
{
    public class ScanResult
    {
        public int Boundaries { get; set; }
        public int Tags { get; set; }
        public int Matched { get; set; }
        public int UnmatchedBoundaries { get; set; }
        public int UnmatchedTags { get; set; }
        public List<string> Messages { get; set; } = new List<string>();
    }

    public static class RoomScanner
    {
        public static ScanResult ScanActiveFloor(Document doc, WentCadProject project, FloorDef floor)
        {
            var result = new ScanResult();
            var mapping = project.DetectionMapping ?? new RoomDetectionMapping();
            if (string.IsNullOrWhiteSpace(mapping.BoundaryLayer) || string.IsNullOrWhiteSpace(mapping.TagLayer))
            {
                result.Messages.Add("Ustaw warstwy obrysow i metek przed skanowaniem.");
                return result;
            }

            var region = ResolveFloorRegion(doc.Database, floor);
            var boundaries = new List<BoundaryData>();
            var tags = new List<TagData>();

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId entId in model)
                {
                    var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                    if (ent == null || ent.IsErased) continue;

                    if (string.Equals(ent.Layer, mapping.BoundaryLayer, StringComparison.OrdinalIgnoreCase) && ent is Polyline poly)
                    {
                        if (!poly.Closed || poly.NumberOfVertices < 3) continue;
                        var points = GeometryManager.PolylineToPoints(poly);
                        var centroid = Centroid(points);
                        if (!GeometryManager.PointInPolygon(centroid, region)) continue;
                        boundaries.Add(new BoundaryData
                        {
                            Handle = poly.Handle.ToString(),
                            Points = points,
                            Area = GeometryManager.PolygonArea(points)
                        });
                    }
                    else if (string.Equals(ent.Layer, mapping.TagLayer, StringComparison.OrdinalIgnoreCase) && ent is BlockReference br)
                    {
                        var pos = new Point2d(br.Position.X, br.Position.Y);
                        if (!GeometryManager.PointInPolygon(pos, region)) continue;
                        tags.Add(new TagData
                        {
                            Handle = br.Handle.ToString(),
                            Position = pos,
                            Attributes = ReadAttributes(tr, br)
                        });
                    }
                }
                tr.Commit();
            }

            result.Boundaries = boundaries.Count;
            result.Tags = tags.Count;

            var usedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var boundary in boundaries)
            {
                var matchedTags = tags.Where(t => GeometryManager.PointInPolygon(t.Position, boundary.Points)).ToList();
                if (matchedTags.Count == 0)
                {
                    result.UnmatchedBoundaries++;
                    continue;
                }

                var tag = matchedTags.First();
                usedTags.Add(tag.Handle);
                UpsertRoom(project, floor, boundary, tag, mapping);
                result.Matched++;
                if (matchedTags.Count > 1)
                {
                    result.Messages.Add($"Obrys {boundary.Handle} zawiera {matchedTags.Count} metki; uzyto pierwszej ({tag.Handle}).");
                }
            }

            result.UnmatchedTags = tags.Count(t => !usedTags.Contains(t.Handle));
            BalanceEngine.Recalculate(project);
            ProjectFileService.Save(doc, project);
            NodManager.SaveProjectIndex(doc, project);
            GeometryManager.WriteRoomXData(doc, project);
            return result;
        }

        private static void UpsertRoom(WentCadProject project, FloorDef floor, BoundaryData boundary, TagData tag, RoomDetectionMapping mapping)
        {
            var existing = project.Rooms.Values.FirstOrDefault(r => string.Equals(r.BoundaryHandle, boundary.Handle, StringComparison.OrdinalIgnoreCase));
            var room = existing ?? new RoomDef { RoomId = Guid.NewGuid().ToString() };
            room.FloorId = floor.FloorId;
            room.BoundaryHandle = boundary.Handle;
            room.TagHandle = tag.Handle;
            room.Number = GetAttr(tag, mapping.NumberAttribute, room.Number);
            room.Name = GetAttr(tag, mapping.NameAttribute, room.Name);
            room.Area = ReadDouble(GetAttr(tag, mapping.AreaAttribute, ""), boundary.Area);
            room.Height = ReadDouble(GetAttr(tag, mapping.HeightAttribute, ""), room.Height > 0 ? room.Height : floor.HeightNet);
            if (string.IsNullOrEmpty(room.SupplySystemId))
            {
                room.SupplySystemId = project.Systems.FirstOrDefault(s => s.Type == "SUPPLY")?.SystemId ?? "";
            }
            if (string.IsNullOrEmpty(room.ExhaustSystemId))
            {
                room.ExhaustSystemId = project.Systems.FirstOrDefault(s => s.Type == "EXHAUST")?.SystemId ?? "";
            }
            project.Rooms[room.RoomId] = room;
        }

        private static string GetAttr(TagData tag, string key, string fallback)
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

        private static List<PointDto> ResolveFloorRegion(Database db, FloorDef floor)
        {
            if (floor.Region != null && floor.Region.Count >= 3) return floor.Region;
            if (!string.IsNullOrWhiteSpace(floor.DrukWidokiViewId))
            {
                var view = NodManager.LoadDrukWidokiViews(db).FirstOrDefault(v => string.Equals(v.ViewId, floor.DrukWidokiViewId, StringComparison.OrdinalIgnoreCase));
                if (view?.Geometry != null && view.Geometry.Count >= 3) return view.Geometry;
            }
            return new List<PointDto>();
        }

        private static Point2d Centroid(IList<PointDto> pts)
        {
            if (pts == null || pts.Count == 0) return new Point2d();
            return new Point2d(pts.Average(p => p.X), pts.Average(p => p.Y));
        }

        private class BoundaryData
        {
            public string Handle { get; set; }
            public List<PointDto> Points { get; set; }
            public double Area { get; set; }
        }

        private class TagData
        {
            public string Handle { get; set; }
            public Point2d Position { get; set; }
            public Dictionary<string, string> Attributes { get; set; }
        }
    }
}
