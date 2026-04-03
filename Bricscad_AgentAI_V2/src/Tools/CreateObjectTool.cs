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
                    Description = "Tworzy nowe obiekty geometryczne i tekstowe (Line, Circle, DBText, MText, MLeader). Obsługuje parametry dynamiczne i RPN.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "EntityType", new ToolParameter { Type = "string", Description = "Typ obiektu: 'Line', 'Circle', 'DBText', 'MText', 'MLeader'." } },
                            { "Layer", new ToolParameter { Type = "string", Description = "Nazwa warstwy docelowej." } },
                            { "SelectObject", new ToolParameter { Type = "boolean", Description = "Czy ustawić nowy obiekt jako aktywne zaznaczenie (default: true)." } },
                            { "StartPoint", new ToolParameter { Type = "string", Description = "Punkt początkowy (x,y,z) lub 'AskUser'." } },
                            { "EndPoint", new ToolParameter { Type = "string", Description = "Punkt końcowy (x,y,z) lub 'AskUser'." } },
                            { "Center", new ToolParameter { Type = "string", Description = "Środek okręgu (x,y,z) lub 'AskUser'." } },
                            { "Radius", new ToolParameter { Type = "string", Description = "Promień (liczba lub 'RPN:...') lub 'AskUser'." } },
                            { "Position", new ToolParameter { Type = "string", Description = "Pozycja tekstu (x,y,z) lub 'AskUser'." } },
                            { "Text", new ToolParameter { Type = "string", Description = "Treść tekstu. Może zawierać 'RPN:' dla obliczeń." } },
                            { "Height", new ToolParameter { Type = "string", Description = "Wysokość tekstu (liczba lub 'RPN:...') lub 'AskUser'." } },
                            { "Rotation", new ToolParameter { Type = "string", Description = "Obrót w stopniach (liczba lub 'RPN:...')." } },
                            { "ArrowPoint", new ToolParameter { Type = "string", Description = "Punkt strzałki MLeader lub 'AskUser'." } },
                            { "LandingPoint", new ToolParameter { Type = "string", Description = "Punkt tekstu MLeader lub 'AskUser'." } }
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

                if (entityType.Equals("Line", StringComparison.OrdinalIgnoreCase))
                {
                    Point3d sp = GetPoint(ed, args["StartPoint"]?.ToString(), "Start: ");
                    Point3d ep = GetPoint(ed, args["EndPoint"]?.ToString(), "Koniec: ");
                    newEnt = new Line(sp, ep);
                }
                else if (entityType.Equals("Circle", StringComparison.OrdinalIgnoreCase))
                {
                    Point3d cen = GetPoint(ed, args["Center"]?.ToString(), "Środek: ");
                    double rad = GetDouble(ed, args["Radius"]?.ToString(), "Promień: ", 1.0);
                    newEnt = new Circle(cen, Vector3d.ZAxis, rad);
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
                        tr.Commit();

                        if (selectObject)
                        {
                            AgentMemoryState.Update(new ObjectId[] { id });
                            ed.SetImpliedSelection(AgentMemoryState.ActiveSelection);
                        }
                        return $"SUKCES: Utworzono {entityType}. ObjectId: {id}";
                    }
                }
                return "BŁĄD: Nieobsługiwany typ obiektu.";
            }
            catch (Exception ex) { return $"BŁĄD: {ex.Message}"; }
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
            if (input.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
            {
                var pr = ed.GetDistance("\n" + prompt);
                return pr.Status == PromptStatus.OK ? pr.Value : def;
            }
            if (input.StartsWith("RPN:")) return ParseRpnDouble(input.Substring(4), def);
            if (double.TryParse(input.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double val)) return val;
            return def;
        }

        private string GetStringVal(Editor ed, string input, string prompt)
        {
            if (string.IsNullOrEmpty(input)) return "";
            string res = input;
            if (input.Contains("AskUser"))
            {
                var pr = ed.GetString("\n" + prompt);
                if (pr.Status == PromptStatus.OK) res = res.Replace("AskUser", pr.StringResult);
            }
            if (res.Contains("RPN:")) res = RpnCalculator.Evaluate(res.Substring(res.IndexOf("RPN:") + 4));
            return res;
        }

        private Point3d ParsePoint(string s)
        {
            s = s.Replace("(", "").Replace(")", "").Trim();
            string[] p = s.Split(',');
            double x = p.Length > 0 && double.TryParse(p[0].Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double px) ? px : 0;
            double y = p.Length > 1 && double.TryParse(p[1].Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double py) ? py : 0;
            double z = p.Length > 2 && double.TryParse(p[2].Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double pz) ? pz : 0;
            return new Point3d(x, y, z);
        }

        private double ParseRpnDouble(string rpn, double def)
        {
            string res = RpnCalculator.Evaluate(rpn);
            if (double.TryParse(res.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double val)) return val;
            return def;
        }
    }
}
