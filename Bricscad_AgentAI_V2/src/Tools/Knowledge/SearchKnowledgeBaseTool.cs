using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.Knowledge
{
    public class SearchKnowledgeBaseTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "SearchKnowledgeBase",
                    Description = "Przeszukuje bazę wiedzy w poszukiwaniu dostępnych formuł inżynierskich i makr. Zwraca informacje o ID skryptu i wymaganych parametrach.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Query", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Słowo kluczowe do wyszukania w bazie formuł i makr (opcjonalnie)."
                                }
                            }
                        },
                        Required = new List<string>()
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string query = args["Query"]?.ToString()?.ToLower() ?? "";

            var formulas = DynamicFormulaManager.GetAvailableFormulas();
            var macros = MacroManager.GetAvailableMacros();

            var result = new System.Text.StringBuilder();
            result.AppendLine("--- WYNIKI WYSZUKIWANIA BAZY WIEDZY ---");

            var matchedFormulas = string.IsNullOrEmpty(query) ? formulas : formulas.Where(f => f.ToLower().Contains(query));
            if (matchedFormulas.Any())
            {
                result.AppendLine("\n[FORMUŁY INŻYNIERSKIE (Roslyn)]:");
                foreach (var f in matchedFormulas)
                {
                    result.AppendLine($"- ID: {f} (Do uruchomienia przez ExecuteFormula)");
                }
            }

            var matchedMacros = string.IsNullOrEmpty(query) ? macros : macros.Where(m => m.ToLower().Contains(query) || MacroManager.GetMacro(m).Description.ToLower().Contains(query));
            if (matchedMacros.Any())
            {
                result.AppendLine("\n[MAKRA (JSON)]:");
                foreach (var m in matchedMacros)
                {
                    var macro = MacroManager.GetMacro(m);
                    result.AppendLine($"- ID: {macro.Id} | Opis: {macro.Description} (Do uruchomienia przez ExecuteMacro)");
                }
            }

            if (!matchedFormulas.Any() && !matchedMacros.Any())
            {
                return "Brak wyników w bazie wiedzy dla zapytania: " + query;
            }

            return result.ToString();
        }

        public List<string> Examples => null;
    }
}
