using System;
using System.Collections.Generic;
using System.IO;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.Knowledge
{
    public class ReadKnowledgeTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ReadKnowledgeTool",
                    Description = "Użyj tego narzędzia, aby przeczytać kod źródłowy i wymogi istniejącej formuły inżynierskiej lub makra, zanim spróbujesz ich użyć lub zmodyfikować. Zwraca pełny kod CSX i metadane JSON.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Id", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "ID formuły lub makra."
                                }
                            },
                            {
                                "Type", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Typ zasobu: 'formula' lub 'macro'."
                                }
                            }
                        },
                        Required = new List<string> { "Id", "Type" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string id = args["Id"]?.ToString();
            string type = args["Type"]?.ToString()?.ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type))
            {
                return "BŁĄD: Parametry Id oraz Type są wymagane.";
            }

            if (type == "formula")
            {
                string formulasPath = AppPaths.GetFormulasPath();
                string csxPath = Path.Combine(formulasPath, $"{id}.csx");
                string jsonPath = Path.Combine(formulasPath, $"{id}.json");

                if (!File.Exists(csxPath))
                {
                    return $"BŁĄD: Formuła '{id}' nie istnieje w bazie.";
                }

                string result = $"--- FORMULA METADATA (JSON) ---\n";
                if (File.Exists(jsonPath))
                    result += File.ReadAllText(jsonPath) + "\n";
                else
                    result += "Brak pliku metadanych.\n";

                result += $"--- FORMULA CODE (CSX) ---\n";
                result += File.ReadAllText(csxPath);

                return result;
            }
            else if (type == "macro")
            {
                string macrosPath = AppPaths.GetMacrosPath();
                string jsonMacroPath = Path.Combine(macrosPath, $"{id}.json");
                if (File.Exists(jsonMacroPath))
                {
                    string result = $"--- MACRO CONTENT (JSON) ---\n";
                    result += File.ReadAllText(jsonMacroPath);
                    return result;
                }
                return $"BŁĄD: Makro '{id}' nie istnieje w bazie.";
            }

            return $"BŁĄD: Nieznany typ '{type}'. Obsługiwane typy to 'formula' i 'macro'.";
        }

        public List<string> Examples => null;
    }
}
