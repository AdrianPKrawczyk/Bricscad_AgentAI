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
                    Description = "Potężne narzędzie do pętli. Służy do masowego wywoływania INNEGO narzędzia (np. CreateObject, ManageLayers) na podstawie listy, szyku lub prostego licznika. Zastępuje tagi {item} (wartość z listy) i {index} (numer iteracji: 1, 2, 3...) wewnątrz szablonu Action.",
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
                                    Description = "Jawna lista elementów. Użyj np. [\"1\",\"2\",\"3\",\"4\"], jeśli nie generujesz geometrii, a potrzebujesz tylko wykonać pętlę 4 razy korzystając z tagu {index}."
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
                                    Description = "Generator współrzędnych dla szyków (zastępuje tag {item} w Action)."
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
                                    Description = "Szablon JSON wywołania narzędzia. Domyślnie wywołuje CreateObject. Aby wywołać inne narzędzie, dodaj 'ToolName'. Możesz łączyć tagi {index}/{item} z ewaluacją RPN!\nPRZYKŁAD 1 (Teksty i RPN): '{\"EntityType\": \"DBText\", \"Position\": \"{item}\", \"Text\": \"RPN: \\'Poziom +\\' {index} 50 * CONCAT\"}'\nPRZYKŁAD 2 (Tworzenie wielu warstw): '{\"ToolName\": \"ManageLayers\", \"Action\": \"Create\", \"LayerName\": \"KONDYGNACJA_{index}\", \"ColorIndex\": \"RPN: {index} 10 *\"}'"
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

            // WYKONANIE REKURENCYJNE (Action as JSON Template)
            if (action.Contains("{") && action.Contains("}"))
            {
                int successCount = 0;
                List<string> handles = new List<string>();
                List<string> errors = new List<string>();
                int loopIndex = 1;

                foreach (var item in finalItems)
                {
                    // ZMIANA: Podmieniamy zarówno {item} jak i {index}
                    string expandedAction = action.Replace("{item}", item)
                                                  .Replace("{index}", loopIndex.ToString());
                    try
                    {
                        JObject toolArgs = JObject.Parse(expandedAction);
                        
                        // [NOWY BLOK] Przechwytywanie i pre-ewaluacja RPN
                        foreach (var property in toolArgs.Properties().ToList())
                        {
                            if (property.Value.Type == JTokenType.String)
                            {
                                string valStr = property.Value.ToString();
                                if (valStr.StartsWith("RPN:", StringComparison.OrdinalIgnoreCase))
                                {
                                    string rpnExpr = valStr.Substring(4).Trim();
                                    string evaluated = RpnCalculator.Evaluate(rpnExpr);
                                    
                                    if (!evaluated.StartsWith("BŁĄD", StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Podmiana surowego stringa na wyliczony wynik
                                        toolArgs[property.Name] = evaluated;
                                    }
                                    else
                                    {
                                        errors.Add($"Błąd RPN w iteracji {loopIndex} dla '{property.Name}': {evaluated}");
                                    }
                                }
                            }
                        }
                        // [KONIEC NOWEGO BLOKU]

                        // SPRZĘGŁO GRAFICZNE: Wymuszamy brak interakcji z UI podczas iteracji
                        toolArgs["SelectObject"] = false;

                        // Wykrywanie narzędzia - domyślnie CreateObject jeśli jest EntityType
                        string targetTool = "CreateObject";
                        if (toolArgs["ToolName"] != null)
                        {
                            targetTool = toolArgs["ToolName"].ToString();
                            toolArgs.Remove("ToolName");
                        }

                        string res = ToolOrchestrator.Instance.ExecuteTool(targetTool, toolArgs, doc);
                        
                        if (res.StartsWith("SUKCES"))
                        {
                            successCount++;
                            // Próba wyciągnięcia Handle z logu (np. "Handle: 1A2B")
                            var match = System.Text.RegularExpressions.Regex.Match(res, @"Handle: ([A-Fa-f0-9]+)");
                            if (match.Success) handles.Add(match.Groups[1].Value);
                        }
                        else
                        {
                            errors.Add(res);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Błąd parsowania JSON dla elementu '{item}': {ex.Message}");
                    }
                    loopIndex++;
                }

                // Wymuszamy odświeżenie ekranu na końcu masowej operacji
                doc.Editor.UpdateScreen();

                StringBuilder summary = new StringBuilder();
                summary.Append($"SUKCES: Wykonano {successCount}/{finalItems.Count} operacji.");
                if (handles.Count > 0) summary.Append($" Uchwyty: {string.Join(", ", handles.Take(10))}{(handles.Count > 10 ? "..." : "")}");
                if (errors.Count > 0) summary.Append($" Błędy: {errors.Count} (ostatni: {errors.Last()})");
                return summary.ToString();
            }

            // TRYBY KLASYCZNE (List / Count)
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
