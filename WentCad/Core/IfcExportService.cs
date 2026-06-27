using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using WentCad.Models;

namespace WentCad.Core
{
    public static class IfcExportService
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private const string IfcChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

        public static string ExportSpaces(Document doc, WentCadProject project)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (project == null) throw new ArgumentNullException(nameof(project));

            BalanceEngine.Recalculate(project);
            string basePath = ProjectFileService.GetProjectPath(doc);
            string ifcPath = Path.ChangeExtension(basePath, ".ifc");

            var boundaryPoints = LoadBoundaryPoints(doc, project);
            var builder = new IfcBuilder(project.ProjectName);
            builder.BuildProject(project, boundaryPoints);
            File.WriteAllText(ifcPath, builder.Build(), Encoding.UTF8);
            return ifcPath;
        }

        private static Dictionary<string, List<PointDto>> LoadBoundaryPoints(Document doc, WentCadProject project)
        {
            var result = new Dictionary<string, List<PointDto>>(StringComparer.OrdinalIgnoreCase);
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                foreach (var room in project.Rooms.Values)
                {
                    if (string.IsNullOrWhiteSpace(room.BoundaryHandle)) continue;
                    if (!GeometryManager.TryGetObjectId(doc.Database, room.BoundaryHandle, out var objectId)) continue;
                    var polyline = tr.GetObject(objectId, OpenMode.ForRead) as Polyline;
                    if (polyline == null || !polyline.Closed || polyline.NumberOfVertices < 3) continue;
                    result[room.RoomId] = GeometryManager.PolylineToPoints(polyline);
                }
                tr.Commit();
            }
            return result;
        }

        private static string Guid22()
        {
            byte[] bytes = Guid.NewGuid().ToByteArray();
            return Compress(bytes, 0, 1) +
                   Compress(bytes, 1, 3) +
                   Compress(bytes, 4, 3) +
                   Compress(bytes, 7, 3) +
                   Compress(bytes, 10, 3) +
                   Compress(bytes, 13, 3);
        }

        private static string Compress(byte[] bytes, int start, int count)
        {
            int value = 0;
            for (int i = 0; i < count; i++)
            {
                value = (value << 8) | bytes[start + i];
            }

            int chars = count == 3 ? 4 : 2;
            var sb = new StringBuilder(chars);
            for (int i = 0; i < chars; i++)
            {
                int shift = 6 * (chars - 1 - i);
                sb.Append(IfcChars[(value >> shift) & 0x3f]);
            }
            return sb.ToString();
        }

        private static string S(string value)
        {
            return (value ?? "").Replace("'", "''");
        }

        private static string N(double value, int digits = 3)
        {
            return value.ToString("0." + new string('#', digits), Inv);
        }

        private class IfcBuilder
        {
            private readonly List<string> _lines = new List<string>();
            private readonly string _projectName;
            private int _nextId = 100;

            public IfcBuilder(string projectName)
            {
                _projectName = string.IsNullOrWhiteSpace(projectName) ? "WentCad Project" : projectName;
            }

            public void BuildProject(WentCadProject project, Dictionary<string, List<PointDto>> boundaryPoints)
            {
                string ownerHistory = Add("IFCOWNERHISTORY($,$,$,.ADDED.,$,$,$,1700000000)");
                string lengthUnit = Add("IFCSIUNIT(*,.LENGTHUNIT.,$,.METRE.)");
                string areaUnit = Add("IFCSIUNIT(*,.AREAUNIT.,$,.SQUARE_METRE.)");
                string volumeUnit = Add("IFCSIUNIT(*,.VOLUMEUNIT.,$,.CUBIC_METRE.)");
                string unitAssignment = Add($"IFCUNITASSIGNMENT(({lengthUnit},{areaUnit},{volumeUnit}))");

                string dirZ = Add("IFCDIRECTION((0.,0.,1.))");
                string dirX = Add("IFCDIRECTION((1.,0.,0.))");
                string origin = Add("IFCCARTESIANPOINT((0.,0.,0.))");
                string axis = Add($"IFCAXIS2PLACEMENT3D({origin},{dirZ},{dirX})");
                string globalPlacement = Add($"IFCLOCALPLACEMENT($,{axis})");

                string ifcProject = Add($"IFCPROJECT('{Guid22()}',{ownerHistory},'{S(_projectName)}',$,$,$,$,($,$),{unitAssignment})");
                string site = Add($"IFCSITE('{Guid22()}',{ownerHistory},'Site',$,$,{globalPlacement},$,$,.ELEMENT.,(0,0,0,0),(0,0,0,0),0.,$,$)");
                string building = Add($"IFCBUILDING('{Guid22()}',{ownerHistory},'Building',$,$,{globalPlacement},$,$,.ELEMENT.,$,$,$)");
                Add($"IFCRELAGGREGATES('{Guid22()}',{ownerHistory},'Project-Site',$,{ifcProject},({site}))");
                Add($"IFCRELAGGREGATES('{Guid22()}',{ownerHistory},'Site-Building',$,{site},({building}))");

                var storeys = new List<string>();
                foreach (var floor in project.Floors.Values.OrderBy(f => f.Order).ThenBy(f => f.Name))
                {
                    string storey = BuildStorey(project, floor, boundaryPoints, ownerHistory, globalPlacement, dirZ, dirX);
                    if (!string.IsNullOrEmpty(storey)) storeys.Add(storey);
                }

                if (storeys.Count > 0)
                {
                    Add($"IFCRELAGGREGATES('{Guid22()}',{ownerHistory},'Building-Storeys',$,{building},({string.Join(",", storeys)}))");
                }
            }

            private string BuildStorey(WentCadProject project, FloorDef floor, Dictionary<string, List<PointDto>> boundaryPoints, string ownerHistory, string globalPlacement, string dirZ, string dirX)
            {
                double elevation = floor.Elevation;
                string ptStorey = Add($"IFCCARTESIANPOINT((0.,0.,{N(elevation)}))");
                string axisStorey = Add($"IFCAXIS2PLACEMENT3D({ptStorey},{dirZ},{dirX})");
                string placementStorey = Add($"IFCLOCALPLACEMENT({globalPlacement},{axisStorey})");
                string storey = Add($"IFCBUILDINGSTOREY('{Guid22()}',{ownerHistory},'{S(floor.Name)}',$,$,{placementStorey},$,$,.ELEMENT.,{N(elevation)})");

                var spaces = new List<string>();
                foreach (var room in project.Rooms.Values.Where(r => r.FloorId == floor.FloorId).OrderBy(r => r.Number))
                {
                    if (!boundaryPoints.TryGetValue(room.RoomId, out var points) || points.Count < 3) continue;
                    spaces.Add(BuildSpace(room, floor, points, ownerHistory, placementStorey, dirZ, dirX));
                }

                if (spaces.Count > 0)
                {
                    Add($"IFCRELAGGREGATES('{Guid22()}',{ownerHistory},'Storey-Spaces',$,{storey},({string.Join(",", spaces)}))");
                }
                return storey;
            }

            private string BuildSpace(RoomDef room, FloorDef floor, List<PointDto> points, string ownerHistory, string placementStorey, string dirZ, string dirX)
            {
                var pointIds = new List<string>();
                foreach (var point in points)
                {
                    double x = point.X - (floor.BasePoint?.X ?? 0);
                    double y = point.Y - (floor.BasePoint?.Y ?? 0);
                    pointIds.Add(Add($"IFCCARTESIANPOINT(({N(x)},{N(y)}))"));
                }

                string polyline = Add($"IFCPOLYLINE(({string.Join(",", pointIds)},{pointIds[0]}))");
                string profile = Add($"IFCARBITRARYCLOSEDPROFILEDEF(.AREA.,$,{polyline})");
                string localOrigin = Add("IFCCARTESIANPOINT((0.,0.,0.))");
                string solidAxis = Add($"IFCAXIS2PLACEMENT3D({localOrigin},{dirZ},{dirX})");
                string solid = Add($"IFCEXTRUDEDAREASOLID({profile},{solidAxis},{dirZ},{N(room.Height > 0 ? room.Height : floor.HeightNet)})");
                string representation = Add($"IFCSHAPEREPRESENTATION($,'Body','SweptSolid',({solid}))");
                string shape = Add($"IFCPRODUCTDEFINITIONSHAPE($,$,({representation}))");

                string spaceOrigin = Add("IFCCARTESIANPOINT((0.,0.,0.))");
                string spaceAxis = Add($"IFCAXIS2PLACEMENT3D({spaceOrigin},{dirZ},{dirX})");
                string spacePlacement = Add($"IFCLOCALPLACEMENT({placementStorey},{spaceAxis})");
                string space = Add($"IFCSPACE('{Guid22()}',{ownerHistory},'{S(room.Number)}','{S(room.Name)}','{S(room.ActivityType)}',{spacePlacement},{shape},$,.ELEMENT.,.INTERNAL.,$)");

                AddSpaceProperties(room, ownerHistory, space);
                return space;
            }

            private void AddSpaceProperties(RoomDef room, string ownerHistory, string space)
            {
                string pArea = Add($"IFCPROPERTYSINGLEVALUE('Area',$,IFCAREAMEASURE({N(room.Area, 2)}),$)");
                string pHeight = Add($"IFCPROPERTYSINGLEVALUE('Height',$,IFCLENGTHMEASURE({N(room.Height, 2)}),$)");
                string pVolume = Add($"IFCPROPERTYSINGLEVALUE('Volume',$,IFCVOLUMEMEASURE({N(room.Volume, 2)}),$)");
                string pSupply = Add($"IFCPROPERTYSINGLEVALUE('SupplyAirFlow',$,IFCREAL({N(room.CalculatedSupply, 2)}),$)");
                string pExhaust = Add($"IFCPROPERTYSINGLEVALUE('ExhaustAirFlow',$,IFCREAL({N(room.CalculatedExhaust, 2)}),$)");
                string pAch = Add($"IFCPROPERTYSINGLEVALUE('RealACH',$,IFCREAL({N(room.RealAch, 2)}),$)");
                string pMode = Add($"IFCPROPERTYSINGLEVALUE('CalculationMode',$,IFCLABEL('{S(room.CalculationMode)}'),$)");
                string pSupplySystem = Add($"IFCPROPERTYSINGLEVALUE('SupplySystemId',$,IFCLABEL('{S(room.SupplySystemId)}'),$)");
                string pExhaustSystem = Add($"IFCPROPERTYSINGLEVALUE('ExhaustSystemId',$,IFCLABEL('{S(room.ExhaustSystemId)}'),$)");
                string pset = Add($"IFCPROPERTYSET('{Guid22()}',{ownerHistory},'Pset_WentCad_Ventilation',$,({pArea},{pHeight},{pVolume},{pSupply},{pExhaust},{pAch},{pMode},{pSupplySystem},{pExhaustSystem}))");
                Add($"IFCRELDEFINESBYPROPERTIES('{Guid22()}',{ownerHistory},$,$,({space}),{pset})");
            }

            private string Add(string entity)
            {
                string id = "#" + _nextId++;
                _lines.Add(id + "= " + entity + ";");
                return id;
            }

            public string Build()
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", Inv);
                return "ISO-10303-21;\n" +
                       "HEADER;\n" +
                       "FILE_DESCRIPTION(('ViewDefinition [CoordinationView]'), '2;1');\n" +
                       $"FILE_NAME('WENTCAD_Export.ifc', '{timestamp}', ('WentCad User'), ('WentCad'), 'WentCad IFC Exporter', 'WentCad', '');\n" +
                       "FILE_SCHEMA(('IFC2X3'));\n" +
                       "ENDSEC;\n" +
                       "DATA;\n" +
                       string.Join("\n", _lines) +
                       "\nENDSEC;\n" +
                       "END-ISO-10303-21;\n";
            }
        }
    }
}
