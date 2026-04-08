using System;
using System.Collections.Generic;
using System.Linq;
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
    public class AnalyzeSelectionTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "AnalyzeSelectionTool",
                    Description = "Służy do analizy i agregacji obiektów w pamięci (ActiveSelection). Wykonuje zliczanie typów obiektów lub wyciąga unikalne wartości konkretnej właściwości bez powtórzeń.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Mode", new ToolParameter 
                                { 
                                    Type = "string", 
                                    Description = "Tryb analizy.",
                                    Enum = new List<string> { "CountTypes", "ListUniqueValues" }
                                }
                            },
                            {
                                "TargetProperty", new ToolParameter 
                                { 
                                    Type = "string", 
                                    Description = "Nazwa badanej właściwości (wymagane tylko dla Mode: ListUniqueValues, np. 'Layer', 'Color')." 
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
                return "BŁĄD: Brak zaznaczonych obiektów w pamięci. Selektuj obiekty przed wywołaniem analizy.";
            }

            string mode = args["Mode"]?.ToString() ?? "CountTypes";
            string targetProp = args["TargetProperty"]?.ToString();
            string saveAs = args["SaveAs"]?.ToString();

            if (mode == "ListUniqueValues" && string.IsNullOrEmpty(targetProp))
            {
                return "BŁĄD: W trybie ListUniqueValues musisz podać parametr TargetProperty.";
            }

            try
            {
                string resultMessage = "";
                string memoryValue = "";

                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    if (mode == "CountTypes")
                    {
                        var counts = new Dictionary<string, int>();
                        foreach (var id in ids)
                        {
                            Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;
                            string typeName = ent.GetType().Name;
                            if (counts.ContainsKey(typeName)) counts[typeName]++;
                            else counts[typeName] = 1;
                        }

                        var summary = string.Join(", ", counts.Select(kv => $"{kv.Value}x {kv.Key}"));
                        resultMessage = $"WYNIK ANALIZY (Łącznie {ids.Length} obiektów): {summary}";
                        memoryValue = summary;
                    }
                    else if (mode == "ListUniqueValues")
                    {
                        var uniqueValues = new HashSet<string>();
                        foreach (var id in ids)
                        {
                            Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent == null) continue;

                            try
                            {
                                object val = GetNestedProperty(ent, targetProp);
                                if (val != null)
                                {
                                    uniqueValues.Add(FormatValue(val));
                                }
                            }
                            catch { /* Skip errors for individual objects in unique listing */ }
                        }

                        var sortedValues = uniqueValues.ToList();
                        sortedValues.Sort();
                        var count = sortedValues.Count;
                        var joined = string.Join(", ", sortedValues);
                        resultMessage = $"WYNIK: Znaleziono unikalnych wartości właściwości '{targetProp}' ({count}): {joined}";
                        memoryValue = string.Join(" | ", sortedValues);
                    }

                    tr.Commit();
                }

                if (!string.IsNullOrEmpty(saveAs))
                {
                    AgentMemoryState.Variables[saveAs] = memoryValue;
                    resultMessage = $"ZAPISANO W PAMIĘCI JAKO: @{saveAs}\n{resultMessage}";
                }

                return resultMessage;
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY ANALIZY: {ex.Message}";
            }
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
        public List<string> Examples => null;
    }
}
