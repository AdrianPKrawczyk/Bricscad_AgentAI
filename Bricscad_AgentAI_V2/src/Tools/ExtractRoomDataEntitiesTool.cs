using System;
using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ExtractRoomDataEntitiesTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ExtractRoomDataEntities",
                    Description = "Skanuje CAŁY rysunek (Model Space) w poszukiwaniu obrysów pomieszczeń (Polyline) na warstwie 'boundaryLayer' oraz metek (BlockReference z atrybutami) na warstwie 'tagLayer'. Dla KAŻDEJ pary (obrys, metka) wykonuje test geometryczny Point-in-Polygon (pozycja bloku vs. obrys polilinii). Zwraca JSON z trzema listami: 'Matched' (pary handle'ów + atrybuty — może być wiele metek na obrys), 'UnmatchedBoundaries' (obrysy bez żadnej metki — wskazówka dla Vision), 'UnmatchedTags' (metki na zewnątrz obrysów). UWAGA: Tylko odczyt, nie modyfikuje rysunku. Łuki w poliliniach traktowane są jako proste (wierzchołki).",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "boundaryLayer", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa warstwy z poliliniami-obrysami pomieszczeń (np. 'A-WALL-INT')."
                                }
                            },
                            {
                                "tagLayer", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa warstwy z blokami-metkami posiadającymi atrybuty (np. 'A-ROOM-TAG')."
                                }
                            },
                            {
                                "saveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna nazwa zmiennej (bez @), pod którą zostanie zapisany zwrócony JSON w pamięci agenta (np. 'RoomData')."
                                }
                            }
                        },
                        Required = new List<string> { "boundaryLayer", "tagLayer" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string boundaryLayer = args["boundaryLayer"]?.ToString();
            string tagLayer = args["tagLayer"]?.ToString();
            string saveAs = args["saveAs"]?.ToString();

            if (string.IsNullOrWhiteSpace(boundaryLayer) || string.IsNullOrWhiteSpace(tagLayer))
                return "BŁĄD: Parametry 'boundaryLayer' i 'tagLayer' są wymagane.";

            Database db = doc.Database;

            try
            {
                List<ObjectId> boundaryIds = new List<ObjectId>();
                List<ObjectId> tagIds = new List<ObjectId>();

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                    if (!lt.Has(boundaryLayer))
                    {
                        tr.Commit();
                        return $"BŁĄD: Warstwa '{boundaryLayer}' nie istnieje w rysunku.";
                    }
                    if (!lt.Has(tagLayer))
                    {
                        tr.Commit();
                        return $"BŁĄD: Warstwa '{tagLayer}' nie istnieje w rysunku.";
                    }

                    BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead);

                    foreach (ObjectId entId in modelSpace)
                    {
                        Entity ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                        if (ent == null || ent.IsErased) continue;

                        if (string.Equals(ent.Layer, boundaryLayer, StringComparison.OrdinalIgnoreCase) && ent is Polyline)
                        {
                            boundaryIds.Add(entId);
                        }
                        else if (string.Equals(ent.Layer, tagLayer, StringComparison.OrdinalIgnoreCase) && ent is BlockReference)
                        {
                            tagIds.Add(entId);
                        }
                    }

                    tr.Commit();
                }

                var boundaryData = new List<(string handle, Point2d[] vertices)>();
                var tagData = new List<(string handle, Point2d pos, Dictionary<string, string> attributes, string instanceName, string definitionName, string dynamicName)>();

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in boundaryIds)
                    {
                        Polyline pl = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                        if (pl == null || !pl.Closed || pl.NumberOfVertices < 3) continue;

                        Point2d[] verts = new Point2d[pl.NumberOfVertices];
                        for (int i = 0; i < pl.NumberOfVertices; i++)
                        {
                            verts[i] = pl.GetPoint2dAt(i);
                        }
                        boundaryData.Add((pl.Handle.ToString(), verts));
                    }

                    foreach (ObjectId id in tagIds)
                    {
                        BlockReference br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (br == null) continue;

                        Point2d pos = new Point2d(br.Position.X, br.Position.Y);
                        var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                        foreach (ObjectId attId in br.AttributeCollection)
                        {
                            AttributeReference attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                            if (attRef == null || attRef.IsErased) continue;

                            string tag = attRef.Tag;
                            string val = attRef.IsMTextAttribute ? attRef.MTextAttribute.Text : attRef.TextString;
                            if (!string.IsNullOrEmpty(tag))
                            {
                                attrs[tag] = val ?? "";
                            }
                        }

                        BlockTableRecord defBtr = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        BlockTableRecord dynBtr = br.DynamicBlockTableRecord != ObjectId.Null
                            ? tr.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord
                            : null;

                        string instanceName = br.Name ?? "";
                        string definitionName = defBtr?.Name ?? "";
                        string dynamicName = dynBtr?.Name ?? "";

                        tagData.Add((br.Handle.ToString(), pos, attrs, instanceName, definitionName, dynamicName));
                    }

                    tr.Commit();
                }

                var matched = new JArray();
                var matchedBoundaryHandles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var matchedTagHandles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var tag in tagData)
                {
                    foreach (var boundary in boundaryData)
                    {
                        if (PointInPolygon(tag.pos, boundary.vertices))
                        {
                            JObject pair = new JObject();
                            pair["BoundaryHandle"] = boundary.handle;
                            pair["TagHandle"] = tag.handle;
                            pair["BlockName"] = tag.instanceName;
                            pair["BlockDefinition"] = tag.definitionName;
                            pair["BlockDynamicName"] = tag.dynamicName;
                            pair["Attributes"] = JObject.FromObject(tag.attributes);
                            matched.Add(pair);

                            matchedBoundaryHandles.Add(boundary.handle);
                            matchedTagHandles.Add(tag.handle);
                        }
                    }
                }

                var unmatchedBoundaries = new JArray();
                foreach (var b in boundaryData)
                {
                    if (!matchedBoundaryHandles.Contains(b.handle))
                    {
                        unmatchedBoundaries.Add(b.handle);
                    }
                }

                var unmatchedTags = new JArray();
                foreach (var t in tagData)
                {
                    if (!matchedTagHandles.Contains(t.handle))
                    {
                        JObject tagInfo = new JObject();
                        tagInfo["TagHandle"] = t.handle;
                        tagInfo["BlockName"] = t.instanceName;
                        tagInfo["BlockDefinition"] = t.definitionName;
                        tagInfo["BlockDynamicName"] = t.dynamicName;
                        tagInfo["Attributes"] = JObject.FromObject(t.attributes);
                        unmatchedTags.Add(tagInfo);
                    }
                }

                JObject result = new JObject();
                result["Matched"] = matched;
                result["UnmatchedBoundaries"] = unmatchedBoundaries;
                result["UnmatchedTags"] = unmatchedTags;

                string jsonOutput = result.ToString(Newtonsoft.Json.Formatting.Indented);

                if (!string.IsNullOrEmpty(saveAs))
                {
                    AgentMemoryState.Variables[saveAs] = jsonOutput;
                }

                string summary = $"SUKCES: Przeanalizowano {boundaryData.Count} obrysów i {tagData.Count} metek. " +
                                 $"Matched: {matched.Count}, UnmatchedBoundaries: {unmatchedBoundaries.Count}, " +
                                 $"UnmatchedTags: {unmatchedTags.Count}." +
                                 (string.IsNullOrEmpty(saveAs) ? "" : $" Zapisano w @{saveAs}.");

                return summary + "\n" + jsonOutput;
            }
            catch (Exception ex)
            {
                return $"BŁĄD EKSTRAKCJI DANYCH POMIESZCZEŃ: {ex.Message}";
            }
        }

        private static bool PointInPolygon(Point2d point, Point2d[] polygon)
        {
            if (polygon == null || polygon.Length < 3) return false;

            bool inside = false;
            int n = polygon.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = polygon[i].X, yi = polygon[i].Y;
                double xj = polygon[j].X, yj = polygon[j].Y;

                bool intersect = ((yi > point.Y) != (yj > point.Y)) &&
                                 (point.X < (xj - xi) * (point.Y - yi) / (yj - yi) + xi);
                if (intersect)
                    inside = !inside;
            }
            return inside;
        }

        public List<string> Examples => new List<string>
        {
            "{\"boundaryLayer\":\"A-WALL-INT\",\"tagLayer\":\"A-ROOM-TAG\"}",
            "{\"boundaryLayer\":\"A-WALL-INT\",\"tagLayer\":\"A-ROOM-TAG\",\"saveAs\":\"RoomData\"}"
        };
    }
}
