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
    public class CreateObjectTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "CreateObject",
                    Description = "Tworzy obiekty CAD (Line, Circle, Text, MLeader).",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "EntityType", new ToolParameter { Type = "string", Description = "Typ: Line, Circle, DBText, MText, MLeader." } },
                            { "Layer", new ToolParameter { Type = "string", Description = "Warstwa docelowa." } },
                            { "SelectObject", new ToolParameter { Type = "boolean", Description = "Ustaw jako zaznaczenie (default: true)." } },
                            { "StartPoint", new ToolParameter { Type = "string", Description = "Punkt startowy (X,Y,Z). DOZWOLONY TYLKO dla obiektu Line. ABSOLUTNIE ZABRONIONE dla DBText, MText oraz Circle! Format: 'X,Y,Z'. KRYTYCZNE: Jeśli musisz wyliczyć którąś oś (np. znając długość linii), ZABRONIONE JEST liczenie w pamięci. Zamiast tego MUSISZ użyć kalkulatora dla tej osi, np. 'X, RPN: Y DŁ 100 +, Z'." } },
                            { "EndPoint", new ToolParameter { Type = "string", Description = "Punkt końcowy (x,y,z). Format: 'X,Y,Z'. KRYTYCZNE: Jeśli musisz wyliczyć którąś oś (np. dodając długość do StartPoint), ZABRONIONE JEST liczenie w pamięci. Użyj RPN, np. '50, RPN: 10 125.5 +, 0'." } },
                            { "Center", new ToolParameter { Type = "string", Description = "Punkt środkowy (X,Y,Z). WYMAGANY I DOZWOLONY TYLKO dla obiektu Circle. Format: 'X,Y,Z'. KRYTYCZNE: Jeśli musisz wyliczyć którąś oś (np. dodając offset), ZABRONIONE JEST liczenie w pamięci. Użyj RPN, np. 'RPN: X 100 +, Y, Z'." } },
                            { "Radius", new ToolParameter { Type = "string", Description = "Promień okręgu." } },
                            { "Position", new ToolParameter { Type = "string", Description = "Pozycja wstawienia. WYMAGANA I DOZWOLONA TYLKO dla tekstów. Format: 'X,Y,Z'. KRYTYCZNE: Jeśli musisz wyliczyć którąś oś (np. offset od innego punktu), ZABRONIONE JEST liczenie w pamięci. Użyj RPN, np. 'X, Y, RPN: Z 50 +'." } },
                            { "Text", new ToolParameter { Type = "string", Description = "Treść tekstu." } },
                            { "Height", new ToolParameter { Type = "string", Description = "Wysokość elementu." } },
                            { "Rotation", new ToolParameter { Type = "string", Description = "Obrót (stopnie)." } },
                            { "ArrowPoint", new ToolParameter { Type = "string", Description = "Punkt strzałki MLeader. Format: 'X,Y,Z'." } },
                            { "LandingPoint", new ToolParameter { Type = "string", Description = "Punkt tekstu MLeader. Format: 'X,Y,Z'." } }
                        },
                        Required = new List<string> { "EntityType" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;
            string entityType = args["EntityType"]?.ToString() ?? "";
            bool selectObject = args["SelectObject"]?.Type == JTokenType.Boolean ? (bool)args["SelectObject"] : true;

            try
            {
                Entity newEnt = null;
                string spatialInfo = "";

                if (entityType.Equals("Line", StringComparison.OrdinalIgnoreCase))
                {
                    Point3d sp = GetPoint(ed, args["StartPoint"]?.ToString(), "Start: ");
                    Point3d ep = GetPoint(ed, args["EndPoint"]?.ToString(), "Koniec: ");
                    newEnt = new Line(sp, ep);
                    Point3d mp = new Point3d((sp.X + ep.X) / 2.0, (sp.Y + ep.Y) / 2.0, (sp.Z + ep.Z) / 2.0);
                    spatialInfo = $"Punkty: Start={FormatPt(sp)}, Koniec={FormatPt(ep)}, Środek={FormatPt(mp)}";
                }
                else if (entityType.Equals("Circle", StringComparison.OrdinalIgnoreCase))
                {
                    if (args["EndPoint"] != null)
                        return "[BŁĄD] Okrąg nie posiada parametru EndPoint. Użyj 'Center' i 'Radius'.";

                    // Fallback: jeśli model użył 'Position' zamiast 'Center'
                    string centerInput = (args["Center"] ?? args["Position"])?.ToString();
                    Point3d cen = GetPoint(ed, centerInput, "Środek: ");
                    double rad = GetDouble(ed, args["Radius"]?.ToString(), "Promień: ", 1.0);
                    newEnt = new Circle(cen, Vector3d.ZAxis, rad);
                    double area = Math.PI * rad * rad;
                    spatialInfo = $"Center={FormatPt(cen)}, Radius={Math.Round(rad, 4)}, Area={Math.Round(area, 4)}";
                }
                else if (entityType.Equals("DBText", StringComparison.OrdinalIgnoreCase) || entityType.Equals("MText", StringComparison.OrdinalIgnoreCase))
                {
                    Point3d pos = GetPoint(ed, args["Position"]?.ToString(), "Pozycja: ");
                    
                    string txt = GetStringVal(ed, args["Text"]?.ToString(), "Tekst: ");
                    double h = GetDouble(ed, args["Height"]?.ToString(), "Wysokość: ", db.Textsize);
                    double rot = GetDouble(ed, args["Rotation"]?.ToString(), "Obrót: ", 0.0);

                    if (entityType.Equals("DBText", StringComparison.OrdinalIgnoreCase))
                        newEnt = new DBText { Position = pos, TextString = txt, Height = h, Rotation = rot };
                    else
                        newEnt = new MText { Location = pos, Contents = txt, TextHeight = h, Rotation = rot };
                    
                    spatialInfo = $"Position={FormatPt(pos)}, Wysokość={h}";
                }
                else if (entityType.Equals("MLeader", StringComparison.OrdinalIgnoreCase))
                {
                    Point3d arrow = GetPoint(ed, args["ArrowPoint"]?.ToString(), "Strzałka: ");
                    Point3d landing = GetPoint(ed, args["LandingPoint"]?.ToString(), "Tekst: ");
                    string txt = GetStringVal(ed, args["Text"]?.ToString(), "Opis: ");
                    double h = GetDouble(ed, args["Height"]?.ToString(), "Wysokość: ", 2.5);

                    MLeader ml = new MLeader();
                    ml.SetDatabaseDefaults();
                    ml.ContentType = ContentType.MTextContent;
                    ml.MText = new MText { Contents = txt, Height = h };
                    int lIdx = ml.AddLeader();
                    int liIdx = ml.AddLeaderLine(lIdx);
                    ml.AddFirstVertex(liIdx, arrow);
                    ml.AddLastVertex(liIdx, landing);
                    newEnt = ml;
                    spatialInfo = $"Strzałka={FormatPt(arrow)}, Tekst={FormatPt(landing)}";
                }

                if (newEnt != null)
                {
                    using (doc.LockDocument())
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                        string layer = args["Layer"]?.ToString();
                        if (!string.IsNullOrEmpty(layer)) try { newEnt.Layer = layer; } catch { }

                        btr.AppendEntity(newEnt);
                        tr.AddNewlyCreatedDBObject(newEnt, true);
                        ObjectId id = newEnt.Id;
                        string handle = newEnt.Handle.ToString();
                        tr.Commit();

                        if (selectObject)
                        {
                            AgentMemoryState.Update(new ObjectId[] { id });
                            ed.SetImpliedSelection(AgentMemoryState.ActiveSelection);
                        }
                        return $"SUKCES: Utworzono {entityType} (Handle: {handle}). {spatialInfo}";
                    }
                }
                return "BŁĄD: Nieobsługiwany typ obiektu.";
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
            return def;
        }

        private string GetStringVal(Editor ed, string input, string prompt)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string res = input.Trim();
            if (res.Contains("AskUser"))
            {
                var pr = ed.GetString("\n" + prompt);
                if (pr.Status == PromptStatus.OK) res = res.Replace("AskUser", pr.StringResult);
            }
            if (res.ToUpper().Contains("RPN:"))
            {
                int idx = res.ToUpper().IndexOf("RPN:");
                string rpnPart = res.Substring(idx + 4).Trim();
                res = RpnCalculator.Evaluate(rpnPart);
            }
            return res;
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
                    if (component.StartsWith("RPN:", StringComparison.OrdinalIgnoreCase))
                    {
                        string rpnExpr = component.Substring(4).Trim();
                        string rpnResult = RpnCalculator.Evaluate(rpnExpr);
                        if (double.TryParse(rpnResult.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                        {
                            coords[i] = val;
                        }
                    }
                    else if (double.TryParse(component.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                    {
                        coords[i] = val;
                    }
                }
            }
            return new Point3d(coords[0], coords[1], coords[2]);
        }

        private double ParseRpnDouble(string rpn, double def)
        {
            string res = RpnCalculator.Evaluate(rpn);
            if (double.TryParse(res.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double val)) return val;
            return def;
        }
    }
}
