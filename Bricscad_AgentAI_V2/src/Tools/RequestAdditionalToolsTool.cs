using System;
using System.Collections.Generic;
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
        public string[] ToolTags => new[] { "#core" };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "RequestAdditionalTools",
                    Description = "Prosi o dostęp do dodatkowych narzędzi (puli tematycznej) na podstawie tagów. Użyj, jeśli nie masz narzędzi potrzebnych do wykonania prośby użytkownika.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { 
                                "Tags", new ToolParameter 
                                { 
                                    Type = "array", 
                                    Description = "Tablica tagów do załadowania, np. ['#bloki', '#tekst']." 
                                } 
                            }
                        },
                        Required = new List<string> { "Tags" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            var tags = args["Tags"] as JArray;
            string tagList = tags != null ? string.Join(", ", tags) : "brak";
            
            // To narzędzie jest przechwytywane przez LLMClient. 
            // Zwracamy informację dla modelu, że narzędzia zostały doładowane.
            return $"SUKCES: Przestrzeń robocza została rozszerzona o narzędzia z grup: {tagList}. Możesz teraz ich użyć w następnym kroku.";
        }
    }
}
