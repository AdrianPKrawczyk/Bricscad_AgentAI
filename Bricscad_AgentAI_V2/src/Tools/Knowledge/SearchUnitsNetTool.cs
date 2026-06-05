using System;
using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using UnitsNet;

namespace Bricscad_AgentAI_V2.Tools.Knowledge
{
    public class SearchUnitsNetTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "SearchUnitsNetTool",
                    Description = "Użyj tego narzędzia, aby przeszukać dostępne klasy w bibliotece UnitsNet dla określonej wielkości fizycznej. Zwraca poprawne nazwy klas oraz przykłady użycia do parsowania. Wywołaj to za każdym razem, gdy nie wiesz jak UnitsNet nazywa daną wielkość (np. podaj 'velocity', 'viscosity', 'temperature', 'force').",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "SearchQuery", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Hasło do wyszukania w języku angielskim (np. 'speed', 'temperature', 'force', 'energy')."
                                }
                            }
                        },
                        Required = new List<string> { "SearchQuery" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string query = args["SearchQuery"]?.ToString()?.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(query))
            {
                return "BŁĄD: Parametr 'SearchQuery' jest wymagany.";
            }

            try
            {
                var assembly = typeof(UnitsNet.Length).Assembly;
                var quantityTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(UnitsNet.IQuantity).IsAssignableFrom(t));

                var matches = quantityTypes
                    .Where(t => t.Name.ToLowerInvariant().Contains(query))
                    .OrderBy(t => t.Name.Length)
                    .Take(10)
                    .ToList();

                if (matches.Count == 0)
                {
                    return $"Brak wyników w UnitsNet dla hasła '{query}'. Spróbuj podać inny synonim (w języku angielskim).";
                }

                string result = $"Znaleziono następujące klasy w UnitsNet dla hasła '{query}':\n\n";
                foreach (var t in matches)
                {
                    result += $"- {t.Name}\n";
                    result += $"  Przykład w kodzie CSX: var val = UnitsNet.{t.Name}.Parse(Inputs[\"param\"], System.Globalization.CultureInfo.InvariantCulture);\n\n";
                }

                return result;
            }
            catch (Exception ex)
            {
                return $"BŁĄD WEWNĘTRZNY narzędzia SearchUnitsNetTool: {ex.Message}";
            }
        }

        public List<string> Examples => null;
    }
}
