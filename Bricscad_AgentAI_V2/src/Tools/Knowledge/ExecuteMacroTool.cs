using System;
using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.Knowledge
{
    public class ExecuteMacroTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ExecuteMacro",
                    Description = "Uruchamia zdefiniowane makro JSON z bazy wiedzy na podstawie jego ID.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "MacroId", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "ID makra do uruchomienia (np. 'CzyszczenieRysunku')."
                                }
                            }
                        },
                        Required = new List<string> { "MacroId" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string macroId = args["MacroId"]?.ToString();
            
            if (string.IsNullOrWhiteSpace(macroId))
            {
                return "BŁĄD: Parametr MacroId jest wymagany.";
            }

            try
            {
                var context = new CadExecutionContext(doc);
                MacroManager.ExecuteMacro(macroId, context);
                
                string response = $"SUKCES: Wykonano makro '{macroId}'.";
                AgentTelemetry.ReportStatus(response);
                return response;
            }
            catch (Exception ex)
            {
                string error = $"BŁĄD WYKONYWANIA MAKRA '{macroId}': {ex.Message}";
                AgentTelemetry.ReportStatus(error);
                return error;
            }
        }

        public List<string> Examples => null;
    }
}
