using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Core
{
    public class RunToolTestTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "RunToolTest",
                    Description = "URUCHAMIA test podanego narzędzia (IToolV2) z wstrzykniętymi argumentami JSON. Używaj tego narzędzia do testowania innych narzędzi. Narzędzie testowane wykona się naprawdę. Pamiętaj, aby do argumentów dodawać flagi __DryRun: true (dla akcji fizycznych) lub __MockResponse (dla interakcji), aby nie zepsuć środowiska i nie zablokować interfejsu.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "TargetToolName", new ToolParameter { Type = "string", Description = "Nazwa narzędzia do przetestowania, np. 'CreateObject', 'UserInput', 'SaveMacro'." } },
                            { "ArgumentsJson", new ToolParameter { Type = "string", Description = "Argumenty dla testowanego narzędzia w formacie JSON (jako string). Zawsze dodawaj __DryRun: true dla bezpieczeństwa." } }
                        },
                        Required = new List<string> { "TargetToolName", "ArgumentsJson" }
                    }
                }
            };
        }

        public string Execute(JObject arguments, IExecutionContext context)
        {
            string targetToolName = arguments["TargetToolName"]?.ToString();
            string argumentsJsonString = arguments["ArgumentsJson"]?.ToString();

            if (string.IsNullOrEmpty(targetToolName))
                return "[BŁĄD]: Brak nazwy narzędzia docelowego.";

            if (string.IsNullOrEmpty(argumentsJsonString))
                return "[BŁĄD]: Brak argumentów w formacie JSON.";

            JObject targetArguments;
            try
            {
                targetArguments = JObject.Parse(argumentsJsonString);
            }
            catch (Exception ex)
            {
                AutotestRegistry.UpdateRecord(targetToolName, isStatic: true, passed: false);
                return $"[BŁĄD PARSOWANIA JSON]: Podany string JSON jest nieprawidłowy: {ex.Message}";
            }

            try
            {
                // Wywołujemy narzędzie używając wbudowanego orkiestratora
                // Podajemy callerProfile jako "AuditorProfile", co pozwoli pominąć niektóre blokady
                string result = ToolOrchestrator.Instance.ExecuteTool(targetToolName, targetArguments, context, "AuditorProfile");

                bool passed = !result.Contains("BŁĄD KRYTYCZNY") && !result.Contains("[BŁĄD]");
                
                AutotestRegistry.UpdateRecord(targetToolName, isStatic: true, passed: passed);

                return $"[WYNIK TESTU {targetToolName}]:\n{result}";
            }
            catch (Exception ex)
            {
                AutotestRegistry.UpdateRecord(targetToolName, isStatic: true, passed: false);
                return $"[BŁĄD KRYTYCZNY (CRASH) w {targetToolName}]: {ex.Message}\nStackTrace:\n{ex.StackTrace}";
            }
        }
    }
}
