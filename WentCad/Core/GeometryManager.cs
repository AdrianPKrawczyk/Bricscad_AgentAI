using System;
using System.Collections.Generic;
using System.Globalization;
using Bricscad.ApplicationServices;
using Teigha.Colors;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using WentCad.Models;

namespace WentCad.Core
{
    public static class GeometryManager
    {
        public static void EnsureLayer(Database db, Transaction tr, string layerName, short colorIndex, bool locked = false, bool plottable = false)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(layerName))
            {
                lt.UpgradeOpen();
                var ltr = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex),
                    IsLocked = locked,
                    IsPlottable = plottable
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }

        public static void EnsureRegApp(Database db, Transaction tr, string appName)
        {
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (!rat.Has(appName))
            {
                rat.UpgradeOpen();
                var ratr = new RegAppTableRecord { Name = appName };
                rat.Add(ratr);
                tr.AddNewlyCreatedDBObject(ratr, true);
            }
        }

        public static ObjectId DrawFloorRegion(Document doc, FloorDef floor)
        {
            ObjectId polyId = ObjectId.Null;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                EnsureLayer(doc.Database, tr, WentCadConstants.FloorRegionLayer, 3, false, false);
                EnsureRegApp(doc.Database, tr, WentCadConstants.FloorRegionRegApp);

                var poly = new Polyline();
                for (int i = 0; i < floor.Region.Count; i++)
                {
                    poly.AddVertexAt(i, new Point2d(floor.Region[i].X, floor.Region[i].Y), 0, 0, 0);
                }
                poly.Closed = true;
                poly.Layer = WentCadConstants.FloorRegionLayer;
                poly.XData = new ResultBuffer(
                    new TypedValue((short)DxfCode.ExtendedDataRegAppName, WentCadConstants.FloorRegionRegApp),
                    new TypedValue((short)DxfCode.ExtendedDataAsciiString, floor.FloorId)
                );

                var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                polyId = btr.AppendEntity(poly);
                tr.AddNewlyCreatedDBObject(poly, true);
                tr.Commit();
            }
            return polyId;
        }

        public static void WriteRoomXData(Document doc, WentCadProject project)
        {
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                EnsureRegApp(doc.Database, tr, WentCadConstants.RoomRegApp);
                foreach (var room in project.Rooms.Values)
                {
                    if (!TryGetObjectId(doc.Database, room.BoundaryHandle, out ObjectId id)) continue;
                    var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                    if (ent == null) continue;

                    ent.XData = BuildRoomXData(project, room);
                }
                tr.Commit();
            }
        }

        public static void ColorRoomsBySystem(Document doc, WentCadProject project)
        {
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var room in project.Rooms.Values)
                {
                    if (!TryGetObjectId(doc.Database, room.BoundaryHandle, out ObjectId id)) continue;
                    var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                    if (ent == null) continue;

                    var sys = project.Systems.Find(s => string.Equals(s.SystemId, room.SupplySystemId, StringComparison.OrdinalIgnoreCase))
                              ?? project.Systems.Find(s => string.Equals(s.SystemId, room.ExhaustSystemId, StringComparison.OrdinalIgnoreCase));
                    if (sys != null)
                    {
                        ent.Color = Color.FromColorIndex(ColorMethod.ByAci, sys.ColorIndex);
                    }
                }
                tr.Commit();
            }
        }

        public static void WriteWindowXData(Document doc, WentCadProject project)
        {
            if (project?.Thermal?.Windows == null) return;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                EnsureRegApp(doc.Database, tr, WentCadConstants.WindowRegApp);
                foreach (var window in project.Thermal.Windows.Values)
                {
                    if (!TryGetObjectId(doc.Database, window.BlockHandle, out ObjectId id)) continue;
                    var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                    if (ent == null) continue;
                    ent.XData = BuildWindowXData(project, window);
                }
                tr.Commit();
            }
        }

        public static void DrawThermalOverlay(Document doc, WentCadProject project)
        {
            if (project?.Thermal?.Walls == null) return;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                EnsureLayer(doc.Database, tr, WentCadConstants.ThermalOverlayLayer, 1, false, false);
                EnsureLayer(doc.Database, tr, WentCadConstants.ThermalIssueLayer, 30, false, false);

                var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                foreach (var wall in project.Thermal.Walls.Values)
                {
                    if (!string.Equals(wall.Kind, "EXTERNAL", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(wall.Kind, "UNRESOLVED", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var line = new Line(new Point3d(wall.P1.X, wall.P1.Y, 0), new Point3d(wall.P2.X, wall.P2.Y, 0));
                    line.Layer = string.Equals(wall.Kind, "UNRESOLVED", StringComparison.OrdinalIgnoreCase)
                        ? WentCadConstants.ThermalIssueLayer
                        : WentCadConstants.ThermalOverlayLayer;
                    line.Color = Color.FromColorIndex(ColorMethod.ByAci, string.Equals(wall.Kind, "UNRESOLVED", StringComparison.OrdinalIgnoreCase) ? (short)30 : (short)1);
                    btr.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);
                }
                tr.Commit();
            }
        }

        public static bool TryGetObjectId(Database db, string handleString, out ObjectId id)
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

        public static List<PointDto> PolylineToPoints(Polyline poly)
        {
            var points = new List<PointDto>();
            for (int i = 0; i < poly.NumberOfVertices; i++)
            {
                var pt = poly.GetPoint2dAt(i);
                points.Add(new PointDto { X = pt.X, Y = pt.Y });
            }
            return points;
        }

        public static double PolygonArea(IList<PointDto> pts)
        {
            if (pts == null || pts.Count < 3) return 0;
            double area = 0;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                area += (pts[j].X + pts[i].X) * (pts[j].Y - pts[i].Y);
            }
            return Math.Abs(area / 2.0);
        }

        public static bool PointInPolygon(Point2d point, IList<PointDto> polygon)
        {
            if (polygon == null || polygon.Count < 3) return true;
            bool inside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = polygon[i].X, yi = polygon[i].Y;
                double xj = polygon[j].X, yj = polygon[j].Y;
                bool intersect = ((yi > point.Y) != (yj > point.Y)) &&
                                 (point.X < (xj - xi) * (point.Y - yi) / (yj - yi) + xi);
                if (intersect) inside = !inside;
            }
            return inside;
        }

        private static ResultBuffer BuildRoomXData(WentCadProject project, RoomDef room)
        {
            var values = new List<TypedValue>
            {
                new TypedValue((short)DxfCode.ExtendedDataRegAppName, WentCadConstants.RoomRegApp)
            };

            Add(values, "ProjectId", project.ProjectId.ToString());
            Add(values, "RoomId", room.RoomId);
            Add(values, "FloorId", room.FloorId);
            Add(values, "Number", room.Number);
            Add(values, "Name", room.Name);
            Add(values, "SupplySystemId", room.SupplySystemId);
            Add(values, "ExhaustSystemId", room.ExhaustSystemId);
            Add(values, "Area", room.Area.ToString("0.##", CultureInfo.InvariantCulture));
            Add(values, "Volume", room.Volume.ToString("0.##", CultureInfo.InvariantCulture));
            Add(values, "SupplyFlow", room.CalculatedSupply.ToString("0", CultureInfo.InvariantCulture));
            Add(values, "ExhaustFlow", room.CalculatedExhaust.ToString("0", CultureInfo.InvariantCulture));
            return new ResultBuffer(values.ToArray());
        }

        private static ResultBuffer BuildWindowXData(WentCadProject project, ThermalWindowDef window)
        {
            var values = new List<TypedValue>
            {
                new TypedValue((short)DxfCode.ExtendedDataRegAppName, WentCadConstants.WindowRegApp)
            };

            Add(values, "ProjectId", project.ProjectId.ToString());
            Add(values, "WindowId", window.WindowId);
            Add(values, "FloorId", window.FloorId);
            Add(values, "RoomId", window.RoomId);
            Add(values, "WallId", window.WallId);
            Add(values, "Width", window.Width.ToString("0.###", CultureInfo.InvariantCulture));
            Add(values, "Height", window.Height.ToString("0.###", CultureInfo.InvariantCulture));
            Add(values, "SillHeight", window.SillHeight.ToString("0.###", CultureInfo.InvariantCulture));
            Add(values, "Area", window.Area.ToString("0.###", CultureInfo.InvariantCulture));
            Add(values, "Confidence", window.Confidence.ToString("0.##", CultureInfo.InvariantCulture));
            return new ResultBuffer(values.ToArray());
        }

        private static void Add(List<TypedValue> values, string key, string val)
        {
            values.Add(new TypedValue((short)DxfCode.ExtendedDataAsciiString, key));
            values.Add(new TypedValue((short)DxfCode.ExtendedDataAsciiString, val ?? ""));
        }
    }
}
