using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using WentCad.Models;

namespace WentCad.Core
{
    public class ThermalScanResult
    {
        public int Walls { get; set; }
        public int ExternalWalls { get; set; }
        public int InternalWalls { get; set; }
        public int UnresolvedWalls { get; set; }
        public int WindowCandidates { get; set; }
        public int WindowsAssigned { get; set; }
        public List<string> Messages { get; } = new List<string>();
    }

    public static class ThermalScanner
    {
        private const double AngleToleranceDeg = 2.0;
        private const double MinOverlap = 0.01;

        public static ThermalScanResult ScanActiveFloor(Document doc, WentCadProject project, FloorDef floor)
        {
            var result = new ThermalScanResult();
            if (doc == null || project == null || floor == null) return result;
            EnsureThermal(project);

            var settings = project.Thermal.Settings ?? new ThermalSettings();
            var roomPolygons = LoadRoomPolygons(doc, project, floor, result);
            var wallHints = LoadWallHints(doc, floor, settings);
            var regionEdges = ToSegments(ResolveFloorRegion(doc.Database, floor), null, null);
            var walls = DetectWalls(roomPolygons, wallHints, regionEdges, settings, result);
            var windowCandidates = LoadWindowCandidates(doc, floor, settings);
            result.WindowCandidates = windowCandidates.Count;
            var windows = AssignWindows(walls, windowCandidates, settings, result);
            var horizontalPartitions = DetectHorizontalPartitions(project, floor);

            ReplaceFloorThermalData(project, floor.FloorId, walls, windows, horizontalPartitions);
            GeometryManager.WriteWindowXData(doc, project);
            GeometryManager.DrawThermalOverlay(doc, project);
            ProjectFileService.Save(doc, project);
            NodManager.SaveProjectIndex(doc, project);
            return result;
        }

        private static void EnsureThermal(WentCadProject project)
        {
            if (project.Thermal == null) project.Thermal = new ThermalModel();
            if (project.Thermal.Walls == null) project.Thermal.Walls = new Dictionary<string, ThermalWallDef>();
            if (project.Thermal.Windows == null) project.Thermal.Windows = new Dictionary<string, ThermalWindowDef>();
            if (project.Thermal.HorizontalPartitions == null) project.Thermal.HorizontalPartitions = new Dictionary<string, ThermalHorizontalDef>();
            if (project.Thermal.Settings == null) project.Thermal.Settings = new ThermalSettings();
        }

        private static List<RoomPolygon> LoadRoomPolygons(Document doc, WentCadProject project, FloorDef floor, ThermalScanResult result)
        {
            var rooms = project.Rooms.Values
                .Where(r => string.Equals(r.FloorId, floor.FloorId, StringComparison.OrdinalIgnoreCase))
                .Where(r => !string.IsNullOrWhiteSpace(r.BoundaryHandle))
                .ToList();
            var polygons = new List<RoomPolygon>();

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var room in rooms)
                {
                    if (!GeometryManager.TryGetObjectId(doc.Database, room.BoundaryHandle, out ObjectId id))
                    {
                        result.Messages.Add($"Pomieszczenie {room.Number}: brak obrysu {room.BoundaryHandle}.");
                        continue;
                    }

                    var poly = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                    if (poly == null || !poly.Closed || poly.NumberOfVertices < 3)
                    {
                        result.Messages.Add($"Pomieszczenie {room.Number}: obrys nie jest zamknieta polilinia.");
                        continue;
                    }

                    polygons.Add(new RoomPolygon
                    {
                        Room = room,
                        Points = GeometryManager.PolylineToPoints(poly)
                    });
                }
                tr.Commit();
            }

            return polygons;
        }

        private static List<Segment> LoadWallHints(Document doc, FloorDef floor, ThermalSettings settings)
        {
            var hints = new List<Segment>();
            if (string.IsNullOrWhiteSpace(settings.WallLayer)) return hints;
            var region = ResolveFloorRegion(doc.Database, floor);

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId entId in model)
                {
                    var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                    if (ent == null || ent.IsErased) continue;
                    if (!string.Equals(ent.Layer, settings.WallLayer, StringComparison.OrdinalIgnoreCase)) continue;

                    if (ent is Line line)
                    {
                        var mid = new Point2d((line.StartPoint.X + line.EndPoint.X) / 2.0, (line.StartPoint.Y + line.EndPoint.Y) / 2.0);
                        if (!GeometryManager.PointInPolygon(mid, region)) continue;
                        hints.Add(new Segment(ToPoint(line.StartPoint), ToPoint(line.EndPoint), null, null));
                    }
                    else if (ent is Polyline poly && poly.NumberOfVertices >= 2)
                    {
                        var points = GeometryManager.PolylineToPoints(poly);
                        hints.AddRange(ToSegments(points, null, null, poly.Closed));
                    }
                }
                tr.Commit();
            }

            return hints;
        }

        private static List<ThermalWallDef> DetectWalls(List<RoomPolygon> rooms, List<Segment> wallHints, List<Segment> regionEdges, ThermalSettings settings, ThermalScanResult result)
        {
            var allSegments = rooms.SelectMany(r => ToSegments(r.Points, r.Room, r.Room.BoundaryHandle)).ToList();
            var walls = new List<ThermalWallDef>();
            double maxInterior = Math.Max(0.01, settings.MaxInteriorWallDistance);
            double maxExterior = Math.Max(0.01, settings.MaxExteriorConfirmDistance);

            foreach (var segment in allSegments)
            {
                var neighbor = allSegments
                    .Where(s => s.Room != segment.Room)
                    .Select(s => MatchSegments(segment, s))
                    .Where(m => m.Overlaps && m.Distance <= maxInterior)
                    .OrderBy(m => m.Distance)
                    .ThenByDescending(m => m.Overlap)
                    .FirstOrDefault();

                string kind;
                string adjacentRoomId = "";
                double thickness = 0;
                double confidence;
                string message = "";

                if (neighbor != null)
                {
                    kind = "INTERNAL";
                    adjacentRoomId = neighbor.Other.Room?.RoomId ?? "";
                    thickness = neighbor.Distance;
                    confidence = thickness <= 0.001 ? 0.75 : 0.9;
                }
                else
                {
                    bool wallHint = wallHints.Any(h => MatchSegments(segment, h).Overlaps && DistanceMidToSegment(segment, h) <= maxExterior);
                    bool regionHint = regionEdges.Any(h => MatchSegments(segment, h).Overlaps && DistanceMidToSegment(segment, h) <= maxExterior);
                    if (wallHint || regionHint)
                    {
                        kind = "EXTERNAL";
                        thickness = wallHint
                            ? Math.Max(0, wallHints.Where(h => MatchSegments(segment, h).Overlaps).Select(h => DistanceMidToSegment(segment, h)).DefaultIfEmpty(0.35).Min())
                            : Math.Max(0, regionEdges.Where(h => MatchSegments(segment, h).Overlaps).Select(h => DistanceMidToSegment(segment, h)).DefaultIfEmpty(0.35).Min());
                        if (thickness < 0.05) thickness = 0.35;
                        confidence = wallHint ? 0.9 : 0.72;
                    }
                    else
                    {
                        kind = "UNRESOLVED";
                        confidence = 0.35;
                        message = "Brak sasiada i brak potwierdzenia warstwa scian albo regionem kondygnacji.";
                    }
                }

                walls.Add(new ThermalWallDef
                {
                    WallId = StableWallId(segment.Room.RoomId, segment.Index),
                    FloorId = segment.Room.FloorId,
                    RoomId = segment.Room.RoomId,
                    Kind = kind,
                    Code = WallCode(kind),
                    P1 = segment.P1,
                    P2 = segment.P2,
                    Length = Math.Round(CadLengthToMeters(SegmentLength(segment)), 3),
                    Thickness = Math.Round(NormalizeWallThickness(thickness), 3),
                    Azimuth = Math.Round(Azimuth(segment), 2),
                    AdjacentRoomId = adjacentRoomId,
                    SourceRoomBoundaryHandle = segment.SourceHandle,
                    Confidence = confidence,
                    Message = message
                });
            }

            result.Walls = walls.Count;
            result.ExternalWalls = walls.Count(w => w.Kind == "EXTERNAL");
            result.InternalWalls = walls.Count(w => w.Kind == "INTERNAL");
            result.UnresolvedWalls = walls.Count(w => w.Kind == "UNRESOLVED");
            return walls;
        }

        private static List<WindowCandidate> LoadWindowCandidates(Document doc, FloorDef floor, ThermalSettings settings)
        {
            var candidates = new List<WindowCandidate>();
            var labels = new List<WindowLabel>();
            var region = ResolveFloorRegion(doc.Database, floor);

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId entId in model)
                {
                    var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                    if (ent == null || ent.IsErased) continue;

                    if (ent is BlockReference br)
                    {
                        var blockName = GetBlockName(tr, br);
                        var pos = new Point2d(br.Position.X, br.Position.Y);
                        if (!GeometryManager.PointInPolygon(pos, region)) continue;

                        if (IsDoorEntity(ent.Layer, blockName, settings))
                        {
                            candidates.Add(ReadBlockOpening(tr, br, blockName, settings, "DOOR"));
                        }
                        else if (IsWindowEntity(ent.Layer, blockName, settings))
                        {
                            candidates.Add(ReadBlockOpening(tr, br, blockName, settings, DetectOpeningKind(blockName)));
                        }
                        else if (IsDoorLabel(ent.Layer, blockName, settings))
                        {
                            labels.Add(ReadOpeningLabel(tr, br, blockName, settings, "DOOR"));
                        }
                        else if (IsWindowLabel(ent.Layer, blockName, settings))
                        {
                            labels.Add(ReadOpeningLabel(tr, br, blockName, settings, DetectOpeningKind(blockName)));
                        }
                    }
                    else if (IsDoorEntity(ent.Layer, ent.GetType().Name, settings))
                    {
                        var candidate = ReadLinearOpening(ent, settings, "DOOR");
                        if (candidate != null && GeometryManager.PointInPolygon(candidate.Centroid, region)) candidates.Add(candidate);
                    }
                    else if (IsWindowEntity(ent.Layer, ent.GetType().Name, settings))
                    {
                        var candidate = ReadLinearOpening(ent, settings, "WINDOW");
                        if (candidate != null && GeometryManager.PointInPolygon(candidate.Centroid, region)) candidates.Add(candidate);
                    }
                }
                tr.Commit();
            }

            foreach (var candidate in candidates)
            {
                var label = labels
                    .Where(l => string.Equals(l.OpeningKind, candidate.OpeningKind, StringComparison.OrdinalIgnoreCase))
                    .Select(l => new { Label = l, Distance = Distance(candidate.Centroid, l.Position) })
                    .Where(x => x.Distance <= Math.Max(2.0, settings.MaxWindowSnapDistance * 2.0))
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault()?.Label;
                if (label == null) continue;
                candidate.LabelHandle = label.Handle;
                if (candidate.Width <= 0 && label.Width > 0) candidate.Width = label.Width;
                if (candidate.Height <= 0 && label.Height > 0) candidate.Height = label.Height;
                if (candidate.SillHeight <= 0 && label.SillHeight >= 0) candidate.SillHeight = label.SillHeight;
            }

            return candidates;
        }

        private static List<ThermalWindowDef> AssignWindows(List<ThermalWallDef> walls, List<WindowCandidate> candidates, ThermalSettings settings, ThermalScanResult result)
        {
            var externalWalls = walls.Where(w => w.Kind == "EXTERNAL").ToList();
            var windows = new List<ThermalWindowDef>();
            foreach (var candidate in candidates)
            {
                var searchableWalls = string.Equals(candidate.OpeningKind, "DOOR", StringComparison.OrdinalIgnoreCase)
                    ? walls.Where(w => w.Kind == "EXTERNAL" || w.Kind == "INTERNAL").ToList()
                    : externalWalls;
                var match = searchableWalls
                    .Select(w => ProjectToWall(candidate.Centroid, w))
                    .Where(p => p.Distance <= Math.Max(0.01, settings.MaxWindowSnapDistance))
                    .OrderBy(p => p.Distance)
                    .FirstOrDefault();
                if (match == null)
                {
                    string label = string.Equals(candidate.OpeningKind, "DOOR", StringComparison.OrdinalIgnoreCase) ? "Drzwi" : "Okno";
                    string target = string.Equals(candidate.OpeningKind, "DOOR", StringComparison.OrdinalIgnoreCase) ? "sciany" : "sciany zewnetrznej";
                    result.Messages.Add($"{label} {candidate.Handle}: nie znaleziono bliskiej {target}.");
                    continue;
                }

                bool isDoor = string.Equals(candidate.OpeningKind, "DOOR", StringComparison.OrdinalIgnoreCase);
                double width = candidate.Width > 0 ? candidate.Width : (isDoor ? settings.DefaultDoorWidth : settings.DefaultWindowWidth);
                double height = candidate.Height > 0 ? candidate.Height : (isDoor ? settings.DefaultDoorHeight : settings.DefaultWindowHeight);
                double sill = candidate.SillHeight >= 0 ? candidate.SillHeight : (isDoor ? settings.DefaultDoorSillHeight : settings.DefaultWindowSillHeight);
                double confidence = candidate.Width > 0 && candidate.Height > 0 ? 0.9 : 0.65;
                if (!string.IsNullOrWhiteSpace(candidate.LabelHandle)) confidence = Math.Max(confidence, 0.82);

                windows.Add(new ThermalWindowDef
                {
                    WindowId = StableWindowId(candidate.Handle),
                    FloorId = match.Wall.FloorId,
                    RoomId = match.Wall.RoomId,
                    WallId = match.Wall.WallId,
                    BlockHandle = candidate.Handle,
                    LabelHandle = candidate.LabelHandle,
                    BlockName = candidate.BlockName,
                    OpeningKind = candidate.OpeningKind,
                    Code = candidate.OpeningKind == "DOOR" ? "DRZ" : "OZ",
                    Width = Math.Round(width, 3),
                    Height = Math.Round(height, 3),
                    SillHeight = Math.Round(sill, 3),
                    Area = Math.Round(width * height, 3),
                    Placement = Math.Round(match.T * match.Wall.Length, 3),
                    Confidence = confidence,
                    Message = candidate.Message
                });
            }

            var deduplicated = DeduplicateWindows(windows, result);
            result.WindowsAssigned = deduplicated.Count;
            return deduplicated;
        }

        private static List<ThermalWindowDef> DeduplicateWindows(List<ThermalWindowDef> windows, ThermalScanResult result)
        {
            const double placementToleranceMeters = 0.05;
            var deduplicated = new List<ThermalWindowDef>();
            foreach (var window in windows.OrderBy(w => w.WallId).ThenBy(w => w.Placement))
            {
                int existingIndex = deduplicated.FindIndex(w =>
                    string.Equals(w.WallId, window.WallId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(w.OpeningKind, window.OpeningKind, StringComparison.OrdinalIgnoreCase) &&
                    Math.Abs(w.Placement - window.Placement) <= placementToleranceMeters);
                if (existingIndex < 0)
                {
                    deduplicated.Add(window);
                    continue;
                }

                var existing = deduplicated[existingIndex];
                var preferred = PreferWindow(existing, window);
                var rejected = ReferenceEquals(preferred, existing) ? window : existing;
                deduplicated[existingIndex] = preferred;
                result.Messages.Add($"Okno {rejected.WindowId}: pominieto duplikat w tym samym polozeniu sciany {rejected.WallId}.");
            }

            return deduplicated;
        }

        private static ThermalWindowDef PreferWindow(ThermalWindowDef first, ThermalWindowDef second)
        {
            int firstScore = WindowCandidateScore(first);
            int secondScore = WindowCandidateScore(second);
            if (secondScore > firstScore) return second;
            if (secondScore < firstScore) return first;
            if (second.Confidence > first.Confidence) return second;
            if (second.Area > first.Area) return second;
            return first;
        }

        private static int WindowCandidateScore(ThermalWindowDef window)
        {
            int score = 0;
            if (!IsLinearWindow(window.BlockName)) score += 10;
            if (!string.IsNullOrWhiteSpace(window.LabelHandle)) score += 2;
            if (window.Width > 0 && window.Height > 0) score += 1;
            return score;
        }

        private static bool IsLinearWindow(string blockName)
        {
            return string.Equals(blockName, "Line", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(blockName, "Polyline", StringComparison.OrdinalIgnoreCase);
        }

        private static List<ThermalHorizontalDef> DetectHorizontalPartitions(WentCadProject project, FloorDef floor)
        {
            var partitions = new List<ThermalHorizontalDef>();
            if (project?.Rooms == null || floor == null) return partitions;

            var floors = project.Floors.Values.OrderBy(f => f.Order).ThenBy(f => f.Name).ToList();
            int index = floors.FindIndex(f => string.Equals(f.FloorId, floor.FloorId, StringComparison.OrdinalIgnoreCase));
            bool isLowest = index <= 0;
            bool isHighest = index < 0 || index == floors.Count - 1;

            foreach (var room in project.Rooms.Values.Where(r => string.Equals(r.FloorId, floor.FloorId, StringComparison.OrdinalIgnoreCase)))
            {
                double area = Math.Round(room.Area, 3);
                if (area <= 0) continue;

                partitions.Add(new ThermalHorizontalDef
                {
                    PartitionId = StableHorizontalId(room.RoomId, "floor"),
                    FloorId = floor.FloorId,
                    RoomId = room.RoomId,
                    Kind = isLowest ? "FLOOR_GROUND" : "FLOOR_INTERIOR",
                    Code = isLowest ? "PG" : "StW",
                    Area = area,
                    Confidence = 0.8,
                    Message = isLowest ? "Podloga na gruncie wg najnizszej kondygnacji." : "Strop/podloga wewnetrzna wg sasiedniej kondygnacji ponizej."
                });

                partitions.Add(new ThermalHorizontalDef
                {
                    PartitionId = StableHorizontalId(room.RoomId, "ceiling"),
                    FloorId = floor.FloorId,
                    RoomId = room.RoomId,
                    Kind = isHighest ? "ROOF" : "CEILING_INTERIOR",
                    Code = isHighest ? "D" : "StW",
                    Area = area,
                    Confidence = 0.8,
                    Message = isHighest ? "Dach/strop zewnetrzny wg najwyzszej kondygnacji." : "Strop wewnetrzny wg sasiedniej kondygnacji powyzej."
                });
            }

            return partitions;
        }

        private static WindowCandidate ReadBlockOpening(Transaction tr, BlockReference br, string blockName, ThermalSettings settings, string openingKind)
        {
            var attrs = ReadAttributes(tr, br);
            bool isDoor = string.Equals(openingKind, "DOOR", StringComparison.OrdinalIgnoreCase);
            string widthAttribute = isDoor ? settings.DoorWidthAttribute : settings.WindowWidthAttribute;
            string heightAttribute = isDoor ? settings.DoorHeightAttribute : settings.WindowHeightAttribute;
            string sillAttribute = isDoor ? settings.DoorSillAttribute : settings.WindowSillAttribute;
            double defaultSill = isDoor ? settings.DefaultDoorSillHeight : settings.DefaultWindowSillHeight;
            double width = FirstPositive(
                ReadDynamic(br, new[] { "WIDTH", "SZEROKOSC", "B" }),
                ReadDouble(attrs, widthAttribute, 0),
                ReadDouble(attrs, "WIDTH", 0),
                ReadDouble(attrs, "B", 0),
                ParseDimension(blockName, true));
            double height = FirstPositive(
                ReadDynamic(br, new[] { "HEIGHT", "WYSOKOSC", "H" }),
                ReadDouble(attrs, heightAttribute, 0),
                ReadDouble(attrs, "HEIGHT", 0),
                ReadDouble(attrs, "H", 0),
                ParseDimension(blockName, false));
            double sill = FirstNonNegative(
                ReadDynamic(br, new[] { "SILL", "PARAPET", "HO" }, -1),
                ReadDouble(attrs, sillAttribute, -1),
                ReadDouble(attrs, "SILL", -1),
                ReadDouble(attrs, "HO", -1),
                defaultSill);

            if (width <= 0)
            {
                try
                {
                    var ext = br.GeometricExtents;
                    width = Math.Max(ext.MaxPoint.X - ext.MinPoint.X, ext.MaxPoint.Y - ext.MinPoint.Y);
                }
                catch { }
            }

            return new WindowCandidate
            {
                Handle = br.Handle.ToString(),
                BlockName = blockName,
                OpeningKind = isDoor ? "DOOR" : "WINDOW",
                Centroid = new Point2d(br.Position.X, br.Position.Y),
                Width = NormalizeMetric(width),
                Height = NormalizeMetric(height),
                SillHeight = NormalizeMetric(sill),
                Message = width <= 0 || height <= 0 ? "Uzyto wartosci domyslnych dla brakujacych wymiarow." : ""
            };
        }

        private static WindowLabel ReadOpeningLabel(Transaction tr, BlockReference br, string blockName, ThermalSettings settings, string openingKind)
        {
            var attrs = ReadAttributes(tr, br);
            bool isDoor = string.Equals(openingKind, "DOOR", StringComparison.OrdinalIgnoreCase);
            string widthAttribute = isDoor ? settings.DoorWidthAttribute : settings.WindowWidthAttribute;
            string heightAttribute = isDoor ? settings.DoorHeightAttribute : settings.WindowHeightAttribute;
            string sillAttribute = isDoor ? settings.DoorSillAttribute : settings.WindowSillAttribute;
            return new WindowLabel
            {
                Handle = br.Handle.ToString(),
                OpeningKind = isDoor ? "DOOR" : "WINDOW",
                Position = new Point2d(br.Position.X, br.Position.Y),
                Width = NormalizeMetric(FirstPositive(ReadDouble(attrs, widthAttribute, 0), ReadDouble(attrs, "WIDTH", 0), ReadDouble(attrs, "B", 0), ParseDimension(blockName, true))),
                Height = NormalizeMetric(FirstPositive(ReadDouble(attrs, heightAttribute, 0), ReadDouble(attrs, "HEIGHT", 0), ReadDouble(attrs, "H", 0), ParseDimension(blockName, false))),
                SillHeight = NormalizeMetric(FirstNonNegative(ReadDouble(attrs, sillAttribute, -1), ReadDouble(attrs, "SILL", -1), ReadDouble(attrs, "HO", -1), -1))
            };
        }

        private static WindowCandidate ReadLinearOpening(Entity ent, ThermalSettings settings, string openingKind)
        {
            Point2d p1;
            Point2d p2;
            if (ent is Line line)
            {
                p1 = new Point2d(line.StartPoint.X, line.StartPoint.Y);
                p2 = new Point2d(line.EndPoint.X, line.EndPoint.Y);
            }
            else if (ent is Polyline poly && poly.NumberOfVertices >= 2)
            {
                p1 = poly.GetPoint2dAt(0);
                p2 = poly.GetPoint2dAt(1);
            }
            else
            {
                return null;
            }

            bool isDoor = string.Equals(openingKind, "DOOR", StringComparison.OrdinalIgnoreCase);
            return new WindowCandidate
            {
                Handle = ent.Handle.ToString(),
                BlockName = ent.GetType().Name,
                OpeningKind = isDoor ? "DOOR" : "WINDOW",
                Centroid = new Point2d((p1.X + p2.X) / 2.0, (p1.Y + p2.Y) / 2.0),
                Width = CadLengthToMeters(Distance(p1, p2)),
                Height = isDoor ? settings.DefaultDoorHeight : settings.DefaultWindowHeight,
                SillHeight = isDoor ? settings.DefaultDoorSillHeight : settings.DefaultWindowSillHeight,
                Message = isDoor
                    ? "Drzwi wykryte z linii/polilinii; wysokosc z ustawien domyslnych."
                    : "Okno wykryte z linii/polilinii; wysokosc i parapet z ustawien domyslnych."
            };
        }

        private static void ReplaceFloorThermalData(WentCadProject project, string floorId, List<ThermalWallDef> walls, List<ThermalWindowDef> windows, List<ThermalHorizontalDef> horizontalPartitions)
        {
            foreach (var id in project.Thermal.Walls.Values.Where(w => w.FloorId == floorId).Select(w => w.WallId).ToList())
            {
                project.Thermal.Walls.Remove(id);
            }
            foreach (var id in project.Thermal.Windows.Values.Where(w => w.FloorId == floorId).Select(w => w.WindowId).ToList())
            {
                project.Thermal.Windows.Remove(id);
            }
            foreach (var id in project.Thermal.HorizontalPartitions.Values.Where(p => p.FloorId == floorId).Select(p => p.PartitionId).ToList())
            {
                project.Thermal.HorizontalPartitions.Remove(id);
            }
            foreach (var wall in walls) project.Thermal.Walls[wall.WallId] = wall;
            foreach (var window in windows) project.Thermal.Windows[window.WindowId] = window;
            foreach (var partition in horizontalPartitions) project.Thermal.HorizontalPartitions[partition.PartitionId] = partition;
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

        private static List<Segment> ToSegments(IList<PointDto> points, RoomDef room, string sourceHandle, bool closed = true)
        {
            var segments = new List<Segment>();
            if (points == null || points.Count < 2) return segments;
            int last = closed ? points.Count : points.Count - 1;
            for (int i = 0; i < last; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Count];
                if (Distance(p1, p2) <= 0.001) continue;
                segments.Add(new Segment(p1, p2, room, sourceHandle) { Index = i });
            }
            return segments;
        }

        private static SegmentMatch MatchSegments(Segment a, Segment b)
        {
            if (!Parallel(a, b)) return new SegmentMatch { Other = b };
            var av = new PointDto { X = a.P2.X - a.P1.X, Y = a.P2.Y - a.P1.Y };
            double len2 = av.X * av.X + av.Y * av.Y;
            if (len2 <= 0) return new SegmentMatch { Other = b };
            double t1 = ((b.P1.X - a.P1.X) * av.X + (b.P1.Y - a.P1.Y) * av.Y) / len2;
            double t2 = ((b.P2.X - a.P1.X) * av.X + (b.P2.Y - a.P1.Y) * av.Y) / len2;
            double start = Math.Max(0, Math.Min(t1, t2));
            double end = Math.Min(1, Math.Max(t1, t2));
            double overlap = Math.Max(0, end - start) * SegmentLength(a);
            return new SegmentMatch
            {
                Other = b,
                Overlaps = overlap >= MinOverlap,
                Overlap = overlap,
                Distance = DistanceMidToSegment(a, b)
            };
        }

        private static Projection ProjectToWall(Point2d point, ThermalWallDef wall)
        {
            double vx = wall.P2.X - wall.P1.X;
            double vy = wall.P2.Y - wall.P1.Y;
            double len2 = vx * vx + vy * vy;
            if (len2 <= 0) return null;
            double t = ((point.X - wall.P1.X) * vx + (point.Y - wall.P1.Y) * vy) / len2;
            t = Math.Max(0, Math.Min(1, t));
            double x = wall.P1.X + t * vx;
            double y = wall.P1.Y + t * vy;
            return new Projection
            {
                Wall = wall,
                T = t,
                Distance = Math.Sqrt(Math.Pow(point.X - x, 2) + Math.Pow(point.Y - y, 2))
            };
        }

        private static bool IsWindowEntity(string layer, string blockName, ThermalSettings settings)
        {
            return Matches(layer, settings.WindowLayer) || Matches(blockName, settings.WindowBlockNamePattern);
        }

        private static bool IsDoorEntity(string layer, string blockName, ThermalSettings settings)
        {
            return Matches(layer, settings.DoorLayer) || Matches(blockName, settings.DoorBlockNamePattern);
        }

        private static bool IsWindowLabel(string layer, string blockName, ThermalSettings settings)
        {
            return Matches(layer, settings.WindowLabelLayer) || Matches(blockName, settings.WindowLabelBlockNamePattern);
        }

        private static bool IsDoorLabel(string layer, string blockName, ThermalSettings settings)
        {
            return Matches(layer, settings.DoorLabelLayer) || Matches(blockName, settings.DoorLabelBlockNamePattern);
        }

        private static string DetectOpeningKind(string blockName)
        {
            var name = (blockName ?? "").ToUpperInvariant();
            if (name.Contains("DOOR") || name.Contains("DRZWI") || name.Contains("DRZ")) return "DOOR";
            return "WINDOW";
        }

        private static bool Matches(string value, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return false;
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Parallel(Segment a, Segment b)
        {
            double delta = Math.Abs(NormalizeAngle(a) - NormalizeAngle(b));
            delta = Math.Min(delta, 180 - delta);
            return delta <= AngleToleranceDeg;
        }

        private static double NormalizeAngle(Segment s)
        {
            double angle = Math.Atan2(s.P2.Y - s.P1.Y, s.P2.X - s.P1.X) * 180 / Math.PI;
            if (angle < 0) angle += 180;
            if (angle >= 180) angle -= 180;
            return angle;
        }

        private static double Azimuth(Segment s)
        {
            double angle = Math.Atan2(s.P2.Y - s.P1.Y, s.P2.X - s.P1.X) * 180 / Math.PI;
            double azimuth = angle + 180;
            while (azimuth < 0) azimuth += 360;
            while (azimuth >= 360) azimuth -= 360;
            return azimuth;
        }

        private static double DistanceMidToSegment(Segment a, Segment b)
        {
            var mid = new PointDto { X = (a.P1.X + a.P2.X) / 2.0, Y = (a.P1.Y + a.P2.Y) / 2.0 };
            return DistancePointToSegment(mid, b.P1, b.P2);
        }

        private static double DistancePointToSegment(PointDto p, PointDto v, PointDto w)
        {
            double len2 = Math.Pow(v.X - w.X, 2) + Math.Pow(v.Y - w.Y, 2);
            if (len2 <= 0) return Distance(p, v);
            double t = ((p.X - v.X) * (w.X - v.X) + (p.Y - v.Y) * (w.Y - v.Y)) / len2;
            t = Math.Max(0, Math.Min(1, t));
            return Distance(p, new PointDto { X = v.X + t * (w.X - v.X), Y = v.Y + t * (w.Y - v.Y) });
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

        private static string GetBlockName(Transaction tr, BlockReference br)
        {
            try
            {
                var btr = tr.GetObject(br.DynamicBlockTableRecord.IsNull ? br.BlockTableRecord : br.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                return btr?.Name ?? br.Name ?? "";
            }
            catch
            {
                return br.Name ?? "";
            }
        }

        private static double ReadDynamic(BlockReference br, IEnumerable<string> names, double fallback = 0)
        {
            try
            {
                foreach (DynamicBlockReferenceProperty prop in br.DynamicBlockReferencePropertyCollection)
                {
                    string propName = prop.PropertyName ?? "";
                    if (!names.Any(n => string.Equals(n, propName, StringComparison.OrdinalIgnoreCase))) continue;
                    return Convert.ToDouble(prop.Value, CultureInfo.InvariantCulture);
                }
            }
            catch { }
            return fallback;
        }

        private static double ReadDouble(Dictionary<string, string> attrs, string key, double fallback)
        {
            if (string.IsNullOrWhiteSpace(key)) return fallback;
            if (!attrs.TryGetValue(key, out string raw)) return fallback;
            return ParseNumber(raw, fallback);
        }

        private static double ParseDimension(string text, bool width)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            var m = Regex.Match(text, @"(\d+(?:[\.,]\d+)?)\s*[xX]\s*(\d+(?:[\.,]\d+)?)");
            if (!m.Success) return 0;
            return ParseNumber(m.Groups[width ? 1 : 2].Value, 0);
        }

        private static double ParseNumber(string raw, double fallback)
        {
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            string clean = raw.Replace("mm", "").Replace("cm", "").Replace("m", "").Replace(" ", "").Replace(",", ".");
            return double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : fallback;
        }

        private static double NormalizeMetric(double value)
        {
            if (value < 0) return value;
            if (value > 500) return value / 1000.0;
            if (value > 20) return value / 100.0;
            return value;
        }

        private static double CadLengthToMeters(double value)
        {
            return value / 100.0;
        }

        private static double NormalizeWallThickness(double value)
        {
            if (value <= 0) return 0;
            if (value < 1.0) return value;
            return CadLengthToMeters(value);
        }

        private static double FirstPositive(params double[] values) => values.FirstOrDefault(v => v > 0);

        private static double FirstNonNegative(params double[] values)
        {
            foreach (double value in values)
            {
                if (value >= 0) return value;
            }
            return -1;
        }

        private static string StableWallId(string roomId, int index) => $"wall-{roomId}-{index}";

        private static string StableWindowId(string handle) => $"win-{handle}";

        private static string StableHorizontalId(string roomId, string role) => $"hp-{roomId}-{role}";

        private static string WallCode(string kind)
        {
            if (string.Equals(kind, "EXTERNAL", StringComparison.OrdinalIgnoreCase)) return "SZ";
            if (string.Equals(kind, "INTERNAL", StringComparison.OrdinalIgnoreCase)) return "SW";
            return "?";
        }

        private static PointDto ToPoint(Point3d pt) => new PointDto { X = pt.X, Y = pt.Y };

        private static double SegmentLength(Segment s) => Distance(s.P1, s.P2);

        private static double Distance(PointDto a, PointDto b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

        private static double Distance(Point2d a, Point2d b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

        private class RoomPolygon
        {
            public RoomDef Room { get; set; }
            public List<PointDto> Points { get; set; }
        }

        private class Segment
        {
            public Segment(PointDto p1, PointDto p2, RoomDef room, string sourceHandle)
            {
                P1 = p1;
                P2 = p2;
                Room = room;
                SourceHandle = sourceHandle ?? "";
            }

            public int Index { get; set; }
            public PointDto P1 { get; }
            public PointDto P2 { get; }
            public RoomDef Room { get; }
            public string SourceHandle { get; }
        }

        private class SegmentMatch
        {
            public Segment Other { get; set; }
            public bool Overlaps { get; set; }
            public double Overlap { get; set; }
            public double Distance { get; set; }
        }

        private class WindowCandidate
        {
            public string Handle { get; set; }
            public string LabelHandle { get; set; }
            public string BlockName { get; set; }
            public string OpeningKind { get; set; } = "WINDOW";
            public Point2d Centroid { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public double SillHeight { get; set; } = -1;
            public string Message { get; set; }
        }

        private class WindowLabel
        {
            public string Handle { get; set; }
            public string OpeningKind { get; set; } = "WINDOW";
            public Point2d Position { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public double SillHeight { get; set; } = -1;
        }

        private class Projection
        {
            public ThermalWallDef Wall { get; set; }
            public double T { get; set; }
            public double Distance { get; set; }
        }
    }
}
