using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Bricscad_AgentAI_V2.Tools
{
    /// <summary>
    /// Narzędzie do iteracji po listach elementów lub generowania sekwencji (np. współrzędnych dla szyków).
    /// </summary>
    public class ForeachTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "Foreach",
                    Description = "Umożliwia iterację po listach lub generowanie ciągów wektorów (Sequence Generator) dla wielokrotnych operacji CAD.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "TargetVariable", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa zmiennej w pamięci Agenta (@Variables), z której pobierana jest lista (opcjonalne)."
                                }
                            },
                            {
                                "Items", new ToolParameter
                                {
                                    Type = "array",
                                    Items = new JObject { ["type"] = "string" },
                                    Description = "Jawna lista elementów (np. Handles). Jeśli generujesz szyk wektorowy, zostaw to pole puste i użyj 'GenerateSequence'."
                                }
                            },
                            {
                                "GenerateSequence", new ToolParameter
                                {
                                    Type = "object",
                                    Properties = new Dictionary<string, ToolParameter>
                                    {
                                        ["StartVector"] = new ToolParameter { Type = "string", Description = "Punkt początkowy (np. '0,0,0')." },
                                        ["OffsetVector"] = new ToolParameter { Type = "string", Description = "Wektor przesunięcia dla każdej iteracji (np. '100,0,0')." },
                                        ["Count"] = new ToolParameter { Type = "integer", Description = "Liczba elementów do wygenerowania." }
                                    },
                                    Description = "Generator punktów dla szyków. Wygenerowane współrzędne zostaną podstawione pod tag '{item}' w parametrach kolejnych narzędzi (np. 'Center': '{item}')."
                                }
                            },
                            {
                                "Separator", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Separator dla TargetVariable (domyślnie ' | ')."
                                }
                            },
                            {
                                "Action", new ToolParameter
                                {
                                    Type = "string",
                                    Enum = new List<string> { "List", "Count" },
                                    Description = "Tryb: 'List' (lista elementów) lub 'Count' (tylko liczba)."
                                }
                            }
                        }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            List<string> finalItems = new List<string>();

            // 1. Sprawdź Generator Sequence
            if (args["GenerateSequence"] != null && args["GenerateSequence"].HasValues)
            {
                var seq = args["GenerateSequence"];
                string startStr = seq["StartVector"]?.ToString() ?? "0,0,0";
                string offsetStr = seq["OffsetVector"]?.ToString() ?? "0,0,0";
                int count = (int)(seq["Count"] ?? 1);

                double[] start = ParseCoords(startStr);
                double[] offset = ParseCoords(offsetStr);

                for (int i = 0; i < count; i++)
                {
                    double x = start[0] + (offset[0] * i);
                    double y = start[1] + (offset[1] * i);
                    double z = start[2] + (offset[2] * i);

                    finalItems.Add(string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4}", x, y, z));
                }
            }
            // 2. Sprawdź jawną listę Items
            else if (args["Items"] != null && args["Items"].Type == JTokenType.Array)
            {
                var array = (JArray)args["Items"];
                foreach (var item in array) finalItems.Add(item.ToString());
            }
            // 3. Sprawdź zmienną z pamięci
            else if (args["TargetVariable"] != null)
            {
                string targetVar = args["TargetVariable"].ToString();
                string separator = args["Separator"]?.ToString() ?? " | ";
                if (AgentMemoryState.Variables.ContainsKey(targetVar))
                {
                    string rawValue = AgentMemoryState.Variables[targetVar];
                    if (!string.IsNullOrEmpty(rawValue))
                    {
                        finalItems.AddRange(rawValue.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
                                                   .Select(e => e.Trim())
                                                   .Where(e => !string.IsNullOrEmpty(e)));
                    }
                }
            }

            if (finalItems.Count == 0) return "WYNIK: Brak elementów do przetworzenia.";

            string action = args["Action"]?.ToString() ?? "List";
            if (action.Equals("Count", StringComparison.OrdinalIgnoreCase))
                return $"WYNIK: Wygenerowano/pobrano {finalItems.Count} elementów.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"WYNIK: Lista {finalItems.Count} elementów:");
            for (int i = 0; i < finalItems.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {finalItems[i]}");
            }
            return sb.ToString();
        }

        private double[] ParseCoords(string s)
        {
            var parts = s.Split(',').Select(p => p.Trim()).ToArray();
            double[] res = new double[3];
            for (int i = 0; i < 3; i++)
            {
                if (i < parts.Length && double.TryParse(parts[i].Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                    res[i] = val;
                else
                    res[i] = 0;
            }
            return res;
        }
    }
}
