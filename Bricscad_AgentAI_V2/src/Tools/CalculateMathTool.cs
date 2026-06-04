using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Bricscad_AgentAI_V2.Tools
{
    public class CalculateMathTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "CalculateMath",
                    Description = "Wykonuje obliczenia matematyczne i fizyczne w naturalnej notacji algebraicznej z obsługą jednostek SI i stałych fizycznych (#PI, #G).",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Expression", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Wyrażenie matematyczne, np. '( 5_cm * 4 ) / 2', '11.34_g/cm3 * 523.6_cm3', '5.94_kg * #G * 10_m'."
                                }
                            },
                            {
                                "TargetUnit", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalnie: jednostka docelowa do konwersji wyniku, np. 'cm3', 'kg', 'mm2'."
                                }
                            },
                            {
                                "SaveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalnie: nazwa zmiennej do zapisu wyniku (bez znaku @)."
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
            string targetUnit = args["TargetUnit"]?.ToString();
            string saveAs = args["SaveAs"]?.ToString();

            if (string.IsNullOrEmpty(expression))
                return "BŁĄD: Parametr 'Expression' jest pusty.";

            try
            {
                // Konwersja z naturalnej notacji na RPN
                string rpnExpression = RpnCalculator.ConvertInfixToRpn(expression);
                
                // Jeśli podano jednostkę docelową, dołącz komendę CONVE na końcu wyrażenia
                if (!string.IsNullOrEmpty(targetUnit))
                {
                    // Upewniamy się, że jednostka docelowa ma pojedyncze cudzysłowy (wymóg RPN CONVE)
                    string safeUnit = targetUnit.Replace("'", "").Replace("\"", "");
                    rpnExpression += $" '{safeUnit}' CONVE";
                }

                // Wykonaj obliczenia
                string result = RpnCalculator.Evaluate(rpnExpression, null, null, doc?.Editor);

                if (!string.IsNullOrEmpty(saveAs))
                {
                    // Dodaj STO aby zapisać zmienną tak jak w oryginalnym silniku (opcjonalne, ale zgodne z RPN)
                    // Zapisujemy bezpośrednio do pamięci agenta:
                    AgentMemoryState.Variables[saveAs] = result;
                    return $"WYNIK: {result} (zapisano w @{saveAs})";
                }

                return $"WYNIK: {result}";
            }
            catch (Exception ex)
            {
                return $"BŁĄD OBLICZEŃ: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string>
        {
            "Oblicz pole koła: CalculateMath(Expression='( 100_mm / 2 ) ^ 2 * #PI', TargetUnit='mm2')",
            "Oblicz masę: CalculateMath(Expression='11.34_g/cm3 * 523.6_cm3', TargetUnit='kg')",
            "Oblicz energię potencjalną: CalculateMath(Expression='5.94_kg * #G * 10_m')"
        };
    }
}
