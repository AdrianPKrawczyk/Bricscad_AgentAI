using System;
using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    public class RunWentCadCommandTool : IToolV2
    {
        private static readonly HashSet<string> AllowedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "WENTCAD",
            "WENTCAD_SYNC",
            "WENTCAD_DRAW_FLOOR_REGION",
            "WENTCAD_PICK_FLOOR_REGION",
            "WENTCAD_PICK_BASE_POINT"
        };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "RunWentCadCommand",
                    Description = "Uruchamia bezpieczna, jawnie dozwolona komende WentCad w aktywnym BricsCAD, np. WENTCAD, WENTCAD_SYNC albo interaktywne komendy wyboru regionu/punktu.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Command",
                                new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Dozwolona komenda WentCad.",
                                    Enum = new List<string> { "WENTCAD", "WENTCAD_SYNC", "WENTCAD_DRAW_FLOOR_REGION", "WENTCAD_PICK_FLOOR_REGION", "WENTCAD_PICK_BASE_POINT" }
                                }
                            }
                        },
                        Required = new List<string> { "Command" }
                    }
                }
            };
        }

        public List<string> Examples => new List<string> { "{\"Command\":\"WENTCAD_SYNC\"}" };

        public string Execute(Document doc, JObject args)
        {
            if (doc == null) return "Error: Brak aktywnego dokumentu.";

            string command = args?["Command"]?.ToString();
            if (string.IsNullOrWhiteSpace(command) || !AllowedCommands.Contains(command))
            {
                return "Error: Dozwolone sa tylko komendy WENTCAD, WENTCAD_SYNC, WENTCAD_DRAW_FLOOR_REGION, WENTCAD_PICK_FLOOR_REGION i WENTCAD_PICK_BASE_POINT.";
            }

            doc.SendStringToExecute(command + "\n", true, false, false);
            return $"Wyslano komende {command}.";
        }
    }
}
