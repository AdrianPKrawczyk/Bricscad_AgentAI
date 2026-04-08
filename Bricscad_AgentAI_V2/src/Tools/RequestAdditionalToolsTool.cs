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
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("SUKCES: Oto katalog uśpionych narzędzi w systemie BricsCAD (niewidocznych w Twoim podstawowym arsenale):");
                
                foreach (var tool in ToolOrchestrator.Instance.GetRegisteredTools())
                {
                    var schema = tool.GetToolSchema()?.Function;
                    if (schema == null) continue;

                    var settings = ToolConfigManager.GetSettings(schema.Name);
                    if (settings != null && !settings.IsCore) // Pokazujemy tylko te poza Core
                    {
                        string tags = string.IsNullOrEmpty(settings.Tags) ? "BRAK_TAGU" : settings.Tags;
                        sb.AppendLine($"- Narzędzie '{schema.Name}' (Tagi: {tags}) -> Opis: {schema.Description}");
                    }
                }
                
                sb.AppendLine("\nZASADA: Aby uzyskać dostęp do narzędzia z powyższej listy, wywołaj mnie ponownie (RequestAdditionalTools) z parametrem Action='LoadCategory' i podaj w CategoryName dokładnie nazwę tego narzędzia (np. 'ManageLayers').");
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
        public List<string> Examples => null;
    }
}

