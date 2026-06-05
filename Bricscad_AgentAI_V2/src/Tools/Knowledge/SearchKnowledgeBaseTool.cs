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
                            },
                            {
                                "categoryFilter", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna kategoria zawężająca wyniki (np. 'HVAC/Wentylacja')."
                                }
                            },
                            {
                                "tagsFilter", new ToolParameter
                                {
                                    Type = "array",
                                    Description = "Opcjonalna lista tagów, które musi posiadać skrypt (np. ['przepływ'])."
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
            string categoryFilter = args["categoryFilter"]?.ToString()?.ToLower();
            List<string> tagsFilter = new List<string>();
            
            if (args["tagsFilter"] is JArray arr)
            {
                tagsFilter = arr.Select(t => t.ToString().ToLower()).ToList();
            }

            var formulas = DynamicFormulaManager.GetAvailableFormulas();
            var macros = MacroManager.GetAvailableMacros();

            var result = new System.Text.StringBuilder();
            result.AppendLine("--- WYNIKI WYSZUKIWANIA BAZY WIEDZY ---");

            var matchedFormulas = formulas.Where(f => 
            {
                var meta = DynamicFormulaManager.GetMetadata(f);
                if (meta == null) return string.IsNullOrEmpty(query) || f.ToLower().Contains(query);
                
                bool matchQuery = string.IsNullOrEmpty(query) || f.ToLower().Contains(query) || meta.Description.ToLower().Contains(query);
                bool matchCategory = string.IsNullOrEmpty(categoryFilter) || meta.Category.ToLower().Contains(categoryFilter);
                bool matchTags = !tagsFilter.Any() || tagsFilter.All(t => meta.Tags.Any(mt => mt.ToLower() == t));
                
                return matchQuery && matchCategory && matchTags;
            });

            if (matchedFormulas.Any())
            {
                result.AppendLine("\n[FORMUŁY INŻYNIERSKIE (Roslyn)]:");
                foreach (var f in matchedFormulas)
                {
                    var meta = DynamicFormulaManager.GetMetadata(f);
                    string cat = meta?.Category ?? "Uncategorized";
                    result.AppendLine($"- ID: {f} | Kat: {cat} (Do uruchomienia przez ExecuteFormula)");
                }
            }

            var matchedMacros = macros.Where(m => 
            {
                var macro = MacroManager.GetMacro(m);
                if (macro == null) return false;

                bool matchQuery = string.IsNullOrEmpty(query) || m.ToLower().Contains(query) || macro.Description.ToLower().Contains(query);
                bool matchCategory = string.IsNullOrEmpty(categoryFilter) || macro.Category.ToLower().Contains(categoryFilter);
                bool matchTags = !tagsFilter.Any() || tagsFilter.All(t => macro.Tags.Any(mt => mt.ToLower() == t));

                return matchQuery && matchCategory && matchTags;
            });

            if (matchedMacros.Any())
            {
                result.AppendLine("\n[MAKRA (JSON)]:");
                foreach (var m in matchedMacros)
                {
                    var macro = MacroManager.GetMacro(m);
                    result.AppendLine($"- ID: {macro.Id} | Kat: {macro.Category} | Opis: {macro.Description} (Do uruchomienia przez ExecuteMacro)");
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
