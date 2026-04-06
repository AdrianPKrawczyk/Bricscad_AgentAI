using System;
using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    /// <summary>
    /// Narzędzie 'Agentic Fallback'. Pozwala modelowi LLM poprosić o dołączenie dodatkowych 
    /// pul narzędzi (tagów), jeśli uważa, że są mu potrzebne do wykonania zadania.
    /// </summary>
    public class RequestAdditionalToolsTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "RequestAdditionalTools",
                    Description = "Zarządza pakietami narzędzi. Pozwala odkryć dostępne kategorie lub załadować konkretną grupę narzędzi do bieżącej sesji.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { 
                                "Action", new ToolParameter 
                                { 
                                    Type = "string", 
                                    Description = "Akcja: 'ListCategories' (pobiera listę dostępnych grup) lub 'LoadCategory' (ładuje wybraną grupę)." 
                                } 
                            },
                            { 
                                "CategoryName", new ToolParameter 
                                { 
                                    Type = "string", 
                                    Description = "Nazwa kategorii do załadowania (wymagane tylko dla Action='LoadCategory'), np. '#bloki'." 
                                } 
                            }
                        },
                        Required = new List<string> { "Action" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string action = args["Action"]?.ToString() ?? "ListCategories";

            if (action.Equals("ListCategories", StringComparison.OrdinalIgnoreCase))
            {
                var categories = ToolConfigManager.GetAvailableCategories();
                if (!categories.Any())
                    return "INFO: W systemie nie ma obecnie zdefiniowanych żadnych dodatkowych kategorii (poza zestawem #core).";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Dostępne kategorie narzędzi (pakiety):");
                foreach (var cat in categories)
                {
                    var tools = ToolConfigManager.GetToolsInCategory(cat);
                    sb.AppendLine($"- '{cat}' (zawiera: {string.Join(", ", tools)})");
                }
                sb.AppendLine("\nAby uzyskać dostęp do wybranej grupy, wywołaj to narzędzie ponownie z parametrem Action='LoadCategory' i podaj CategoryName.");
                return sb.ToString();
            }

            if (action.Equals("LoadCategory", StringComparison.OrdinalIgnoreCase))
            {
                string category = args["CategoryName"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(category))
                    return "BŁĄD: Parametr 'CategoryName' jest wymagany dla akcji 'LoadCategory'.";

                // LLMClient przechwyci to wywołanie i doda tag do zestawu
                return $"SUKCES: Załadowano kategorię {category}. Narzędzia z tej grupy są teraz dostępne w Twoim arsenale. Możesz ich użyć w następnym kroku.";
            }

            return "BŁĄD: Nieznana akcja. Użyj 'ListCategories' lub 'LoadCategory'.";
        }
    }
}
