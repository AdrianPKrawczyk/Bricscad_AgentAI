using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Colors;

namespace Bricscad_AgentAI_V2.Tools
{
    public class GetPropertiesTool : IToolV2
    {
        public string[] ToolTags => new[] { "#core" };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "GetPropertiesTool",
                    Description = "Odczytuje właściwości i parametry fizyczne obiektów aktualnie znajdujących się w pamięci zaznaczenia (ActiveSelection).",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Mode", new ToolParameter 
                                { 
                                    Type = "string", 
                                    Description = "Tryb odczytu właściwości.",
                                    Enum = new List<string> { "Lite", "Full" }
                                }
                            }
                        },
                        Required = new List<string> { "Mode" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            var ids = AgentMemoryState.ActiveSelection;
            if (ids == null || ids.Length == 0)
            {
                return "BŁĄD: Brak zaznaczonych obiektów w pamięci. Użyj najpierw SelectEntitiesTool.";
            }

            string mode = args["Mode"]?.ToString() ?? "Lite";
            int limit = mode == "Full" ? Math.Min(ids.Length, 5) : Math.Min(ids.Length, 15);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"--- RAPORT WŁAŚCIWOŚCI ({mode.ToUpper()}) ---");
            sb.AppendLine($"Wykryto obiektów: {ids.Length}. Skanowanie: {limit}.");

            try
            {
                Database db = doc.Database;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    for (int i = 0; i < limit; i++)
                    {
                        Entity ent = tr.GetObject(ids[i], OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        sb.AppendLine($"\n[Obiekt {i + 1} / {limit}] Typ: {ent.GetType().Name}, Handle: {ent.Handle.ToString()}");
                        sb.AppendLine($"  -> Layer: {ent.Layer}, Color: {ent.ColorIndex}, Linetype: {ent.Linetype}");

                        if (mode == "Full")
                        {
                            PropertyInfo[] properties = ent.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                            foreach (PropertyInfo prop in properties)
                            {
                                if (prop.PropertyType.IsPrimitive || prop.PropertyType.IsEnum || 
                                    prop.PropertyType == typeof(string) || prop.PropertyType == typeof(Point3d) || 
                                    prop.PropertyType == typeof(Vector3d) || prop.PropertyType == typeof(Color) || 
                                    prop.PropertyType == typeof(LineWeight))
                                {
                                    try
                                    {
                                        object val = prop.GetValue(ent, null);
                                        string valStr = FormatValue(val);
                                        sb.AppendLine($"    - {prop.Name}: {valStr}");
                                    }
                                    catch { /* Ignoruj błędy odczytu pojedynczych propertisów */ }
                                }
                            }
                        }
                        else
                        {
                            // Tryb LITE
                            if (ent is Line line)
                                sb.AppendLine($"  -> Length: {Math.Round(line.Length, 3)}, StartPt: {FormatPt(line.StartPoint)}, EndPt: {FormatPt(line.EndPoint)}");
                            else if (ent is Circle circle)
                                sb.AppendLine($"  -> Radius: {Math.Round(circle.Radius, 3)}, Area: {Math.Round(circle.Area, 3)}, Center: {FormatPt(circle.Center)}");
                            else if (ent is DBText dbtext)
                                sb.AppendLine($"  -> Text: \"{dbtext.TextString}\", Height: {Math.Round(dbtext.Height, 3)}, Rotation: {Math.Round(dbtext.Rotation, 3)}");
                            else if (ent is MText mtext)
                                sb.AppendLine($"  -> Text: \"{mtext.Text}\", Height: {Math.Round(mtext.TextHeight, 3)}, Location: {FormatPt(mtext.Location)}");
                            else if (ent is BlockReference block)
                                sb.AppendLine($"  -> BlockName: {block.Name}, Position: {FormatPt(block.Position)}, Rotation: {Math.Round(block.Rotation, 3)}");
                            else if (ent is MLeader mleader)
                                sb.AppendLine($"  -> HasText: {mleader.ContentType == ContentType.MTextContent}, BlockContents: {mleader.ContentType == ContentType.BlockContent}");
                        }
                    }
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD ODCZYTU: {ex.Message}";
            }

            return sb.ToString();
        }

        private string FormatPt(Point3d pt)
        {
            return $"[{Math.Round(pt.X, 3)}, {Math.Round(pt.Y, 3)}, {Math.Round(pt.Z, 3)}]";
        }

        private string FormatValue(object val)
        {
            if (val == null) return "null";
            if (val is Point3d pt) return FormatPt(pt);
            if (val is Vector3d vec) return $"[{Math.Round(vec.X, 3)}, {Math.Round(vec.Y, 3)}, {Math.Round(vec.Z, 3)}]";
            if (val is double d) return Math.Round(d, 3).ToString();
            return val.ToString();
        }
    }
}
