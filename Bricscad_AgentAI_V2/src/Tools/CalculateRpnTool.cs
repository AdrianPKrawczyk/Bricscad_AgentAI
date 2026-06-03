using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Bricscad_AgentAI_V2.Tools
{
    public class CalculateRpnTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "CalculateRpn",
                    Description = "Wykonuje zaawansowane obliczenia matematyczne i fizyczne w notacji RPN z obsługą wymiarowości jednostek SI, stałych fizycznych (#PI, #G) oraz funkcji (SQRT, ROUND, ABS, CONVE).",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Expression", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Wyrażenie RPN do obliczenia, np. '5_cm 4 *', '11.34_g/cm3 523.6_cm3 *', '5.94_kg #G * 10_m *'."
                                }
                            },
                            {
                                "SaveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalnie: nazwa zmiennej do zapisu wyniku w pamięci globalnej (bez znaku @)."
                                }
                            }
                        },
                        Required = new List<string> { "Expression" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string expression = args["Expression"]?.ToString();
            string saveAs = args["SaveAs"]?.ToString();

            if (string.IsNullOrEmpty(expression))
                return "BŁĄD: Parametr 'Expression' jest pusty.";

            try
            {
                // Wykonaj obliczenia
                string result = RpnCalculator.Evaluate(expression, null, null, doc?.Editor);

                if (!string.IsNullOrEmpty(saveAs))
                {
                    AgentMemoryState.Variables[saveAs] = result;
                    return $"WYNIK (CalculateRpn): {result} (zapisano w @{saveAs})";
                }

                return $"WYNIK (CalculateRpn): {result}";
            }
            catch (Exception ex)
            {
                return $"BŁĄD OBLICZEŃ RPN: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string>
        {
            "Oblicz masę z objętości i gęstości: CalculateRpn(Expression='11.34_g/cm3 523.6_cm3 *')",
            "Przelicz 10 metrów na centymetry: CalculateRpn(Expression='10_m \"cm\" CONVE')",
            "Oblicz energię potencjalną (m*g*h): CalculateRpn(Expression='5.94_kg #G * 10_m *')"
        };
    }
}
