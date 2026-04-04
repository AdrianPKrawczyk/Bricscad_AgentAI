using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Colors;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ReadPropertyTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ReadPropertyTool",
                    Description = "Odczytuje jedną, konkretną właściwość (np. Length, Area, Color, Layer) ze wszystkich obiektów w aktywnym zaznaczeniu. Pozwala na zapisanie wyniku do pamięci podręcznej Agenta jako zmiennej.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Property", new ToolParameter 
                                { 
                                    Type = "string", 
                                    Description = "Nazwa właściwości (np. Length, Area, Center, Height, MidPoint, Centroid, Angle, Value)." 
                                }
                            },
                            {
                                "SaveAs", new ToolParameter 
                                { 
                                    Type = "string", 
                                    Description = "Opcjonalna nazwa zmiennej (bez @), pod którą wynik zostanie zapisany w pamięci Agenta." 
                                }
                            }
                        },
                        Required = new List<string> { "Property" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            var ids = AgentMemoryState.ActiveSelection;
            if (ids == null || ids.Length == 0)
            {
                return "BŁĄD: Brak zaznaczonych obiektów w pamięci. Selektuj obiekty przed wywołaniem tego narzędzia.";
            }

            if (!args.TryGetValue("Property", out var propToken) || propToken == null)
            {
                return "BŁĄD: Nie podano nazwy właściwości (Property).";
            }

            string propName = propToken.ToString();
            string saveAs = args.TryGetValue("SaveAs", out var saveToken) ? saveToken?.ToString() : null;

            List<string> results = new List<string>();
            List<string> rawValues = new List<string>();

            try
            {
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in ids)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        try
                        {
                            object val = GetVirtualOrReflectionProperty(ent, propName);

                            if (val != null)
                            {
                                string formatted = FormatValue(val);
                                results.Add($"- Obiekt [{ent.GetType().Name}]: {formatted}");
                                rawValues.Add(formatted);
                            }
                            else
                            {
                                results.Add($"- Obiekt [{ent.GetType().Name}]: Brak właściwości '{propName}'");
                            }
                        }
                        catch (Exception ex)
                        {
                            results.Add($"- Obiekt [{ent.GetType().Name}]: BŁĄD ODCZYTU ({ex.Message})");
                        }
                    }
                    tr.Commit();
                }

                string report = $"WYNIK ODCZYTU WŁAŚCIWOŚCI '{propName}':\n" + string.Join("\n", results);

                if (!string.IsNullOrEmpty(saveAs) && rawValues.Count > 0)
                {
                    string combined = string.Join(" | ", rawValues);
                    AgentMemoryState.Variables[saveAs] = combined;
                    report = $"ZAPISANO W PAMIĘCI JAKO: @{saveAs}\n{report}";
                }

                return report;
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY NARZĘDZIA: {ex.Message}";
            }
        }

        private object GetVirtualOrReflectionProperty(Entity ent, string propName)
        {
            // 1. WIRTUALNE WŁAŚCIWOŚCI (Zgodnie z logiką V1)
            
            // MidPoint
            if (propName.Equals("MidPoint", StringComparison.OrdinalIgnoreCase) && ent is Curve curveMid)
            {
                try { return curveMid.GetPointAtDist(curveMid.GetDistanceAtParameter(curveMid.EndParam) / 2.0); } catch { }
            }
            
            // Length
            if (propName.Equals("Length", StringComparison.OrdinalIgnoreCase) && ent is Curve curveLen)
            {
                try { return curveLen.GetDistanceAtParameter(curveLen.EndParam); } catch { }
            }
            
            // Area
            if (propName.Equals("Area", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (ent is Curve curveArea && curveArea.Closed) return curveArea.Area;
                    if (ent is Hatch hatch) return hatch.Area;
                }
                catch { }
            }
            
            // Volume
            if (propName.Equals("Volume", StringComparison.OrdinalIgnoreCase) && ent is Solid3d solidVol)
            {
                try { return solidVol.MassProperties.Volume; } catch { }
            }
            
            // Centroid
            if (propName.Equals("Centroid", StringComparison.OrdinalIgnoreCase) && ent is Solid3d solidCent)
            {
                try { return solidCent.MassProperties.Centroid; } catch { }
            }
            
            // Angle
            if (propName.Equals("Angle", StringComparison.OrdinalIgnoreCase) && ent is Curve curveAng)
            {
                try
                {
                    Vector3d dir = curveAng.GetFirstDerivative(curveAng.GetParameterAtDistance(curveAng.GetDistanceAtParameter(curveAng.EndParam) / 2.0));
                    return Math.Atan2(dir.Y, dir.X);
                }
                catch { }
            }
            
            // Value (Text)
            if (propName.Equals("Value", StringComparison.OrdinalIgnoreCase))
            {
                if (ent is DBText dbText) return dbText.TextString;
                if (ent is MText mText) return mText.Text;
            }

            // 2. REFLEKSJA (Z obsługą zagnieżdżeń np. Position.X)
            return GetNestedProperty(ent, propName);
        }

        private object GetNestedProperty(object obj, string propPath)
        {
            if (obj == null || string.IsNullOrEmpty(propPath)) return null;

            string[] parts = propPath.Split('.');
            object current = obj;

            foreach (string part in parts)
            {
                if (current == null) return null;

                PropertyInfo propInfo = current.GetType().GetProperty(part, 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                if (propInfo == null) return null;

                try
                {
                    current = propInfo.GetValue(current, null);
                }
                catch
                {
                    return null;
                }
            }

            return current;
        }

        private string FormatValue(object val)
        {
            if (val == null) return "null";

            if (val is Point3d pt)
                return $"({pt.X.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)},{pt.Y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)},{pt.Z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)})";

            if (val is double dbl)
                return dbl.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

            if (val is Color col)
                return col.ColorIndex.ToString();

            return val.ToString();
        }
    }
}
