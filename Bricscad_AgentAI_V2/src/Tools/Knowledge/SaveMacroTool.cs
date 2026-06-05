using System;
using System.Collections.Generic;
using System.IO;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.Knowledge
{
    public class SaveMacroTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "SaveMacro",
                    Description = "Zapisuje nową sekwencję kroków jako trwałe Makro. Każdy krok to wywołanie innego narzędzia. Umożliwia grupowanie powtarzalnych czynności.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "MacroId", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Unikalna nazwa makra (np. 'CzyszczenieWarstw')."
                                }
                            },
                            {
                                "Description", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opis sekwencji działania."
                                }
                            },
                            {
                                "JsonSteps", new ToolParameter
                                {
                                    Type = "array",
                                    Description = "Tablica kroków w formacie zgodnym z MacroStep: [ { 'actionType': 'Narzędzie', 'parameters': { ... } } ].",
                                    Items = new JObject
                                    {
                                        ["type"] = "object",
                                        ["properties"] = new JObject
                                        {
                                            ["actionType"] = new JObject { ["type"] = "string" },
                                            ["parameters"] = new JObject { ["type"] = "object" }
                                        }
                                    }
                                }
                            }
                        },
                        Required = new List<string> { "MacroId", "JsonSteps" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string id = args["MacroId"]?.ToString();
            string description = args["Description"]?.ToString();
            
            if (string.IsNullOrWhiteSpace(id)) return "BŁĄD: Parametr MacroId jest wymagany.";
            
            var stepsToken = args["JsonSteps"];
            if (stepsToken == null || !(stepsToken is JArray stepsArray))
            {
                return "BŁĄD: JsonSteps musi być prawidłową tablicą JSON obiektów.";
            }

            try
            {
                List<MacroStep> steps = new List<MacroStep>();
                foreach (JObject stepToken in stepsArray)
                {
                    steps.Add(new MacroStep
                    {
                        ActionType = stepToken["actionType"]?.ToString(),
                        Parameters = stepToken["parameters"] as JObject ?? new JObject()
                    });
                }

                var macro = new MacroDefinition
                {
                    Id = id,
                    Description = description,
                    Steps = steps
                };

                string jsonOutput = JsonConvert.SerializeObject(macro, Formatting.Indented);
                MacroManager.SaveMacro(id, jsonOutput);
                MacroManager.LoadAllMacros();
                
                return $"SUKCES: Zapisano makro '{id}' zawierające {steps.Count} krok(ów).";
            }
            catch (Exception ex)
            {
                return $"BŁĄD PARSOWANIA LUB ZAPISU JSON: {ex.Message}";
            }
        }

        public List<string> Examples => null;
    }
}
