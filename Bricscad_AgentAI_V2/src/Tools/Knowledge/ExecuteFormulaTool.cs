using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.Knowledge
{
    public class ExecuteFormulaTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ExecuteFormula",
                    Description = "Uruchamia skompilowaną dynamiczną formułę inżynierską (Roslyn) na podstawie jej FormulaId i podanych argumentów (łańcuchy znaków z jednostkami, np. '50 kg'). Wynik może zostać opcjonalnie zapisany na Blackboardzie. Inputs are strings containing values and units. You MUST pass quantities like '10 m', not raw numbers.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "FormulaId", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "ID uruchamianej formuły (np. 'Hvac_FlowRate')."
                                }
                            },
                            {
                                "Inputs", new ToolParameter
                                {
                                    Type = "object",
                                    Description = "Słownik (Klucz-Wartość), gdzie kluczem jest nazwa argumentu oczekiwanego przez formułę, a wartością łańcuch znaków z wartością i jednostką (np. { \"power_kW\": \"150.0 kW\", \"deltaT\": \"20.0 °C\" })."
                                }
                            },
                            {
                                "SaveToBlackboardKey", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna nazwa klucza, pod którym wynik zostanie zrzucony do pamięci Blackboard (SharedMemoryState)."
                                }
                            }
                        },
                        Required = new List<string> { "FormulaId", "Inputs" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string id = args["FormulaId"]?.ToString();
            if (string.IsNullOrWhiteSpace(id)) return "BŁĄD: Parametr FormulaId jest wymagany.";

            var inputsDict = new Dictionary<string, string>();
            if (args["Inputs"] is JObject inputsObj)
            {
                foreach (var prop in inputsObj.Properties())
                {
                    inputsDict[prop.Name] = prop.Value.ToString();
                }
            }

            try
            {
                // Ponieważ interfejs IToolV2 jest synchroniczny (Execute), wymuszamy zakończenie zadania.
                // W środowisku CAD blokowanie wątku może być inwazyjne, ale tutaj wykonujemy czyste operacje in-memory, 
                // kompilacja skryptu już się odbyła.
                string result = DynamicFormulaManager.ExecuteFormula(id, inputsDict).GetAwaiter().GetResult();
                
                string response = $"SUKCES: Formuła '{id}' zwróciła wynik: {result}";

                // Zapisz wynik na Blackboard jeśli zażądano
                string bbKey = args["SaveToBlackboardKey"]?.ToString();
                if (!string.IsNullOrWhiteSpace(bbKey))
                {
                    SharedMemoryState.Write(bbKey, result);
                    response += $"\nWynik został zapisany na Blackboardzie pod kluczem: '{bbKey}'.";
                }

                // Raportuj do UI
                AgentTelemetry.ReportStatus($"Wykonano formułę {id}. Wynik: {result}");

                return response;
            }
            catch (Exception ex)
            {
                return $"BŁĄD WYKONYWANIA FORMUŁY '{id}': {ex.Message}";
            }
        }

        public List<string> Examples => null;
    }
}
