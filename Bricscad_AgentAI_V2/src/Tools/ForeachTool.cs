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
                    Description = "Potężne narzędzie do pętli. Służy do masowego wywoływania INNEGO narzędzia (np. CreateObject, ManageLayers, TextEditTool) na podstawie listy, szyku lub prostego licznika. Zastępuje tagi {item} (wartość z listy) i {index} (numer iteracji: 1, 2, 3...) wewnątrz szablonu Action.\n\nUWAGA v2.35.3: Jesli masz juz selekcje obiektow w AgentMemoryState.ActiveSelection (np. po SelectEntities) i chcesz wykonac mutujace narzedzie (np. TextEditTool) na KAZDYM z nich - ustaw IterateSelection=true. Nie musisz podawac Items ani TargetVariable.",
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
                                "IterateSelection", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Jesli true, iteruje po AgentMemoryState.ActiveSelection. Kazda iteracja ustawia {item} na Handle obiektu (hex) i wywoluje Action jako mutujace narzedzie (np. TextEditTool) na tym konkretnym obiekcie. Uzywaj tego do masowej edycji tekstow/warstw bez koniecznosci podawania Items."
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
                                    Description = "Szablon JSON wywołania narzędzia. Domyślnie wywołuje CreateObject. Aby wywołać inne narzędzie, dodaj 'ToolName'. Możesz łączyć tagi {index}/{item} z ewaluacją matematyki używając {MATH: wyrażenie}!\nPRZYKŁAD 1 (Teksty i Math): '{\"EntityType\": \"DBText\", \"Position\": \"{item}\", \"Text\": \"Poziom: {MATH: {index} * 50}\"}'\nPRZYKŁAD 2 (Tworzenie wielu warstw): '{\"ToolName\": \"ManageLayers\", \"Action\": \"Create\", \"LayerName\": \"KONDYGNACJA_{index}\", \"ColorIndex\": \"{MATH: {index} * 10}\"}'\nPRZYKŁAD 3 (Edycja tekstu na kazdym obiekcie z selekcji): '{\"ToolName\": \"TextEditTool\", \"Mode\": \"Replace\", \"FindText\": \"DN15\", \"ReplaceWith\": \"PP-stabi PN20 %%C25\"}' + ustaw IterateSelection=true."
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

            // 0. Iteruj po AgentMemoryState.ActiveSelection (v2.35.3 / BUG #6)
            // Kazdy element to Handle ObjectId (hex string) - Action moze uzyc {item}.
            // Specjalny tryb dla masowej edycji tekstu/warstwy bez podawania Items.
            if (args["IterateSelection"] != null && args["IterateSelection"].Value<bool>())
            {
                var selection = AgentMemoryState.ActiveSelection;
                if (selection == null || selection.Length == 0)
                {
                    return "WYNIK: Brak zaznaczonych obiektów w AgentMemoryState.ActiveSelection. Użyj najpierw SelectEntities.";
                }
                foreach (var id in selection)
                {
                    if (id.IsNull) continue;
                    finalItems.Add(id.Handle.Value.ToString("X"));
                }
            }
            // 1. Sprawdź Generator Sequence
            else if (args["GenerateSequence"] != null && args["GenerateSequence"].HasValues)
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

                // Fix v2.35.3 (BUG #6): Cap na liczbe iteracji zeby nie wpadnac w kolejna petle.
                // 200 wystarczy dla typowych rysunkow. Dla wiekszych - user musi podawac dane porcjami.
                const int maxIterations = 200;
                bool truncated = false;
                if (finalItems.Count > maxIterations)
                {
                    truncated = true;
                    finalItems = finalItems.Take(maxIterations).ToList();
                }

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
                                string evaluated = RpnCalculator.ProcessMathTemplates(valStr);

                                if (evaluated != valStr)
                                {
                                    if (!evaluated.StartsWith("BŁĄD", StringComparison.OrdinalIgnoreCase))
                                    {
                                        toolArgs[property.Name] = evaluated;
                                    }
                                    else
                                    {
                                        errors.Add($"Błąd MATH w iteracji {loopIndex} dla '{property.Name}': {evaluated}");
                                    }
                                }
                            }
                        }
                        // [KONIEC NOWEGO BLOKU]

                        // SPRZĘGŁO GRAFICZNE: Wymuszamy brak interakcji z UI podczas iteracji
                        toolArgs["SelectObject"] = false;
                        toolArgs["SuppressUI"] = true; // [NOWE] Zapobiega bombardowaniu wątku głównego przez pod-narzędzia i błędom eLockViolation

                        // Wykrywanie narzędzia - domyślnie CreateObject jeśli jest EntityType
                        string targetTool = "CreateObject";
                        if (toolArgs["ToolName"] != null)
                        {
                            targetTool = toolArgs["ToolName"].ToString();
                            toolArgs.Remove("ToolName");
                        }

                        // Fix v2.35.3 (BUG #6): Jesli iterujemy po selekcji, przekaz Handle
                        // jako TargetHandle, zeby narzedzie mutujace (np. TextEditTool)
                        // moglo odniesc sie do konkretnego obiektu zamiast do calego ActiveSelection.
                        if (args["IterateSelection"] != null && args["IterateSelection"].Value<bool>() &&
                            !toolArgs.ContainsKey("TargetHandle"))
                        {
                            toolArgs["TargetHandle"] = item;
                        }

                        string res = ToolOrchestrator.Instance.ExecuteTool(targetTool, toolArgs, new CadExecutionContext(doc));

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
                if (errors.Count > 0)
                {
                    string prefix = successCount == 0 ? "BLAD FOREACH" : "BLAD CZESCIOWY FOREACH";
                    summary.Append($"{prefix}: Wykonano {successCount}/{finalItems.Count} operacji.");
                }
                else
                {
                    summary.Append($"SUKCES: Wykonano {successCount}/{finalItems.Count} operacji.");
                }
                if (handles.Count > 0) summary.Append($" Uchwyty: {string.Join(", ", handles.Take(10))}{(handles.Count > 10 ? "..." : "")}");
                if (errors.Count > 0) summary.Append($" Błędy: {errors.Count} (ostatni: {errors.Last()})");
                if (truncated) summary.Append($" UWAGA: Iteracja zostala ograniczona do {maxIterations} elementow. Uzyj SelectEntities z mniejszym filtrem dla pozostalych.");
                // [NOWE] Pojedyncze odświeżenie interfejsu po zakończeniu wszystkich iteracji w pętli
                doc.SendStringToExecute("(princ) \n", true, false, false);

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
        public List<string> Examples => null;
    }
}

