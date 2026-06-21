using System;
using System.Collections.Generic;
using System.Globalization;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace Bricscad_AgentAI_V2.Tools
{
    public class CreateSolidTool : IToolV2
    {
        public string[] ToolTags => new[] { "#3dmodeler", "#solid" };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "CreateSolid",
                    Description = "Tworzy obiekty bryłowe 3D (Solid3d) typu Box, Cylinder, Sphere, Cone, Wedge, Torus.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "SolidType", new ToolParameter { Type = "string", Description = "Typ bryły: Box, Cylinder, Sphere, Cone, Wedge, Torus." } },
                            { "Layer", new ToolParameter { Type = "string", Description = "Warstwa docelowa." } },
                            { "SelectObject", new ToolParameter { Type = "boolean", Description = "Ustaw jako zaznaczenie (default: true)." } },
                            { "Position", new ToolParameter { Type = "string", Description = "Punkt wstawienia (X,Y,Z). Wykorzystaj RPN dla matematyki np. 'MATH: X + 50, Y, Z'." } },
                            { "Length", new ToolParameter { Type = "string", Description = "Długość wzdłuż osi X. Używane dla: Box, Wedge." } },
                            { "Width", new ToolParameter { Type = "string", Description = "Szerokość wzdłuż osi Y. Używane dla: Box, Wedge." } },
                            { "Height", new ToolParameter { Type = "string", Description = "Wysokość wzdłuż osi Z. Używane dla: Box, Cylinder, Cone, Wedge." } },
                            { "Radius", new ToolParameter { Type = "string", Description = "Promień. Używane dla: Cylinder, Sphere, Cone, Torus (promień główny)." } },
                            { "Radius2", new ToolParameter { Type = "string", Description = "Promień przekroju. Używane dla: Torus (promień rurki)." } }
                        },
                        Required = new List<string> { "SolidType", "Position" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;
            string solidType = args["SolidType"]?.ToString() ?? "";
            bool selectObject = args["SelectObject"]?.Type == JTokenType.Boolean ? (bool)args["SelectObject"] : true;

            try
            {
                if (string.IsNullOrEmpty(solidType))
                    return "BŁĄD: Parametr 'SolidType' jest wymagany.";

                if (args["Position"] == null)
                    return "BŁĄD KRYTYCZNY: Brakuje parametru 'Position'. Punkt wstawienia jest bezwzględnie wymagany.";

                Point3d pos = GetPoint(ed, args["Position"]?.ToString(), "Pozycja: ");
                
                string spatialInfo = "";
                string handle = "";
                ObjectId id = ObjectId.Null;

                // 1. TWORZENIE PRZEZ COM (BEZ BLOKADY .NET!)
                // Musi być poza doc.LockDocument(), w przeciwnym razie COM rzuca "Nieprawidłowy wskaźnik"
                // Używamy Reflection (InvokeMember) zamiast dynamic, aby uniknąć E_POINTER przy przekazywaniu tablic double[]
                try
                {
                    object acadApp = Bricscad.ApplicationServices.Application.AcadApplication;
                    object comDoc = acadApp.GetType().InvokeMember("ActiveDocument", System.Reflection.BindingFlags.GetProperty, null, acadApp, null);
                    object mSpace = comDoc.GetType().InvokeMember("ModelSpace", System.Reflection.BindingFlags.GetProperty, null, comDoc, null);
                    object newComSolid = null;

                    double[] origin = new double[] { 0.0, 0.0, 0.0 };

                    if (solidType.Equals("Box", StringComparison.OrdinalIgnoreCase))
                    {
                        double l = GetDouble(ed, args["Length"]?.ToString(), "Długość (X): ", 1.0);
                        double w = GetDouble(ed, args["Width"]?.ToString(), "Szerokość (Y): ", 1.0);
                        double h = GetDouble(ed, args["Height"]?.ToString(), "Wysokość (Z): ", 1.0);
                        newComSolid = mSpace.GetType().InvokeMember("AddBox", System.Reflection.BindingFlags.InvokeMethod, null, mSpace, new object[] { origin, l, w, h });
                    }
                    else if (solidType.Equals("Cylinder", StringComparison.OrdinalIgnoreCase))
                    {
                        double r = GetDouble(ed, args["Radius"]?.ToString(), "Promień: ", 1.0);
                        double h = GetDouble(ed, args["Height"]?.ToString(), "Wysokość: ", 1.0);
                        newComSolid = mSpace.GetType().InvokeMember("AddCylinder", System.Reflection.BindingFlags.InvokeMethod, null, mSpace, new object[] { origin, r, h });
                    }
                    else if (solidType.Equals("Sphere", StringComparison.OrdinalIgnoreCase))
                    {
                        double r = GetDouble(ed, args["Radius"]?.ToString(), "Promień: ", 1.0);
                        newComSolid = mSpace.GetType().InvokeMember("AddSphere", System.Reflection.BindingFlags.InvokeMethod, null, mSpace, new object[] { origin, r });
                    }
                    else if (solidType.Equals("Cone", StringComparison.OrdinalIgnoreCase))
                    {
                        double r = GetDouble(ed, args["Radius"]?.ToString(), "Promień: ", 1.0);
                        double h = GetDouble(ed, args["Height"]?.ToString(), "Wysokość: ", 1.0);
                        newComSolid = mSpace.GetType().InvokeMember("AddCone", System.Reflection.BindingFlags.InvokeMethod, null, mSpace, new object[] { origin, r, h });
                    }
                    else if (solidType.Equals("Wedge", StringComparison.OrdinalIgnoreCase))
                    {
                        double l = GetDouble(ed, args["Length"]?.ToString(), "Długość (X): ", 1.0);
                        double w = GetDouble(ed, args["Width"]?.ToString(), "Szerokość (Y): ", 1.0);
                        double h = GetDouble(ed, args["Height"]?.ToString(), "Wysokość (Z): ", 1.0);
                        newComSolid = mSpace.GetType().InvokeMember("AddWedge", System.Reflection.BindingFlags.InvokeMethod, null, mSpace, new object[] { origin, l, w, h });
                    }
                    else if (solidType.Equals("Torus", StringComparison.OrdinalIgnoreCase))
                    {
                        double r1 = GetDouble(ed, args["Radius"]?.ToString(), "Promień główny: ", 2.0);
                        double r2 = GetDouble(ed, args["Radius2"]?.ToString(), "Promień rurki: ", 0.5);
                        newComSolid = mSpace.GetType().InvokeMember("AddTorus", System.Reflection.BindingFlags.InvokeMethod, null, mSpace, new object[] { origin, r1, r2 });
                    }
                    else
                    {
                        return $"BŁĄD: Nieobsługiwany typ bryły '{solidType}'.";
                    }
                    
                    if (newComSolid != null)
                    {
                        handle = newComSolid.GetType().InvokeMember("Handle", System.Reflection.BindingFlags.GetProperty, null, newComSolid, null).ToString();
                    }
                    else
                    {
                        return "BŁĄD KRYTYCZNY: Obiekt COM nie został utworzony.";
                    }
                }
                catch (Exception ex)
                {
                    return $"BŁĄD KRYTYCZNY (COM API): Nie udało się utworzyć bryły. Powód: {ex.Message}";
                }

                // 2. MODYFIKACJA WŁAŚCIWOŚCI I AKTUALIZACJA PAMIĘCI PRZEZ .NET
                using (doc.LockDocument())
                {
                    Handle hnd = new Handle(Convert.ToInt64(handle, 16));
                    
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        id = db.GetObjectId(false, hnd, 0);
                        Solid3d solid = tr.GetObject(id, OpenMode.ForWrite) as Solid3d;
                        if (solid != null)
                        {
                            solid.SetDatabaseDefaults();
                            
                            solid.TransformBy(Matrix3d.Displacement(pos - Point3d.Origin));
                            
                            string layer = args["Layer"]?.ToString();
                            if (!string.IsNullOrEmpty(layer)) try { solid.Layer = layer; } catch { }
                            
                            try 
                            {
                                spatialInfo = $"Volume={Math.Round(solid.MassProperties.Volume, 4).ToString(CultureInfo.InvariantCulture)}, Area={Math.Round(solid.Area, 4).ToString(CultureInfo.InvariantCulture)}, Centroid={FormatPt(solid.MassProperties.Centroid)}";
                            } catch { spatialInfo = "BŁĄD: Nie można było odczytać MassProperties"; }
                        }
                        tr.Commit();
                    }

                    if (selectObject && id != ObjectId.Null)
                    {
                        AgentMemoryState.Update(new ObjectId[] { id });
                        ed.SetImpliedSelection(AgentMemoryState.ActiveSelection);
                    }
                    return $"SUKCES: Utworzono bryłę {solidType} (Handle: {handle}). Pozycja bazowa={FormatPt(pos)}, {spatialInfo}";
                }
            }
            catch (Exception ex) { return $"BŁĄD: {ex.Message}"; }
        }

        private string FormatPt(Point3d pt)
        {
            return $"{Math.Round(pt.X, 3).ToString(CultureInfo.InvariantCulture)},{Math.Round(pt.Y, 3).ToString(CultureInfo.InvariantCulture)},{Math.Round(pt.Z, 3).ToString(CultureInfo.InvariantCulture)}";
        }

        private Point3d GetPoint(Editor ed, string input, string prompt)
        {
            if (string.IsNullOrEmpty(input)) return Point3d.Origin;
            if (input.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
            {
                var pr = ed.GetPoint("\n" + prompt);
                return pr.Status == PromptStatus.OK ? pr.Value : Point3d.Origin;
            }
            return ParsePoint(input);
        }

        private double GetDouble(Editor ed, string input, string prompt, double def)
        {
            if (string.IsNullOrEmpty(input)) return def;
            string trimmed = input.Trim();
            if (trimmed.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
            {
                var pr = ed.GetDistance("\n" + prompt);
                return pr.Status == PromptStatus.OK ? pr.Value : def;
            }
            if (trimmed.StartsWith("RPN:", StringComparison.OrdinalIgnoreCase)) return ParseRpnDouble(trimmed.Substring(4).Trim(), def);
            if (double.TryParse(trimmed.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double val)) return val;
            
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"[a-zA-Z_+\-*/^]"))
            {
                string parsedRpn = RpnCalculator.ConvertInfixToRpn(trimmed);
                return ParseRpnDouble(parsedRpn, def);
            }
            
            return def;
        }

        private Point3d ParsePoint(string s)
        {
            if (string.IsNullOrEmpty(s)) return Point3d.Origin;
            
            s = s.Replace("(", "").Replace(")", "").Trim();
            string[] p = s.Split(',');
            double[] coords = new double[3];

            for (int i = 0; i < 3; i++)
            {
                if (i < p.Length)
                {
                    string component = p[i].Trim();
                    if (component.StartsWith("MATH:", StringComparison.OrdinalIgnoreCase) || component.StartsWith("RPN:", StringComparison.OrdinalIgnoreCase))
                    {
                        string rpnResult = RpnCalculator.ProcessMathTemplates(component);
                        if (System.Text.RegularExpressions.Regex.IsMatch(rpnResult, @"[a-zA-Z]"))
                        {
                            string safeRpn = $"'{rpnResult}' #UNITL CONVE UVAL";
                            rpnResult = RpnCalculator.Evaluate(safeRpn);
                        }
                        if (double.TryParse(rpnResult.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                            coords[i] = val;
                    }
                    else if (double.TryParse(component.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                        coords[i] = val;
                    else if (System.Text.RegularExpressions.Regex.IsMatch(component, @"[a-zA-Z_+\-*/^]"))
                    {
                        string rpnExpr = RpnCalculator.ConvertInfixToRpn(component);
                        string rpnResult = RpnCalculator.Evaluate(rpnExpr);
                        if (System.Text.RegularExpressions.Regex.IsMatch(rpnResult, @"[a-zA-Z]"))
                        {
                            string safeRpn = $"'{rpnResult}' #UNITL CONVE UVAL";
                            rpnResult = RpnCalculator.Evaluate(safeRpn);
                        }
                        if (double.TryParse(rpnResult.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double val2))
                            coords[i] = val2;
                    }
                }
            }
            return new Point3d(coords[0], coords[1], coords[2]);
        }

        private double ParseRpnDouble(string rpn, double def)
        {
            string res = RpnCalculator.Evaluate(rpn);
            if (System.Text.RegularExpressions.Regex.IsMatch(res, @"[a-zA-Z]"))
            {
                string safeRpn = $"'{res}' #UNITL CONVE UVAL";
                res = RpnCalculator.Evaluate(safeRpn);
            }
            if (double.TryParse(res.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double val)) 
                return val;
            return def;
        }

        public List<string> Examples => null;
    }
}
