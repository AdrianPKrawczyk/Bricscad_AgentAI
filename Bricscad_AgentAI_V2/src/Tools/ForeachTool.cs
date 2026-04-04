using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Bricscad_AgentAI_V2.Tools
{
    /// <summary>
    /// Narzędzie pomocnicze do "rozpakowywania" list elementów zapisanych w pamięci Agenta.
    /// Pomaga LLM w iterowaniu po danych (np. nazwach warstw, bloków, wartościach atrybutów).
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
                    Description = "Służy do analizy i rozpakowywania list elementów (string) zapisanych w pamięci podręcznej Agenta (@Variables).",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "TargetVariable", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa zmiennej w pamięci, z której chcesz pobrać listę (bez znaku @, np. 'UnikalneWarstwy')."
                                }
                            },
                            {
                                "Separator", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Znak lub ciąg znaków, którym rozdzielone są elementy w pamięci (domyślnie ' | ')."
                                }
                            },
                            {
                                "Action", new ToolParameter
                                {
                                    Type = "string",
                                    Enum = new List<string> { "List", "Count" },
                                    Description = "Tryb działania: 'List' (wyświetla wszystkie elementy) lub 'Count' (zwraca tylko ich liczbę)."
                                }
                            }
                        },
                        Required = new List<string> { "TargetVariable" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string targetVar = args["TargetVariable"]?.ToString();
            string separator = args["Separator"]?.ToString() ?? " | ";
            string action = args["Action"]?.ToString() ?? "List";

            if (string.IsNullOrEmpty(targetVar))
                return "BŁĄD: Parametr 'TargetVariable' nie może być pusty.";

            if (!AgentMemoryState.Variables.ContainsKey(targetVar))
                return $"BŁĄD: Zmienna @{targetVar} nie istnieje w pamięci Agenta.";

            string rawValue = AgentMemoryState.Variables[targetVar];
            if (string.IsNullOrEmpty(rawValue))
                return $"WYNIK: Zmienna @{targetVar} jest pusta.";

            // Podział na elementy z filtrowaniem pustych wpisów i usuwaniem białych znaków
            string[] elements = rawValue.Split(new[] { separator }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(e => e.Trim())
                                       .Where(e => !string.IsNullOrEmpty(e))
                                       .ToArray();

            if (action.Equals("Count", StringComparison.OrdinalIgnoreCase))
            {
                return $"WYNIK: Zmienna @{targetVar} zawiera {elements.Length} elementów.";
            }

            // Domyślnie tryb 'List'
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"WYNIK: Zmienna @{targetVar} zawiera {elements.Length} elementów:");
            for (int i = 0; i < elements.Length; i++)
            {
                sb.AppendLine($"{i + 1}. {elements[i]}");
            }

            return sb.ToString();
        }
    }
}
