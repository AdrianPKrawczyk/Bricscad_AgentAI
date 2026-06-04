using System.Diagnostics;
using System.Collections.Generic;
using Bricscad_AgentAI_V2.Tools;
using Bricscad_AgentAI_V2.Core;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class CalculateMathToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            TestCalculationExecution();
            TestCalculationSaveAs();
            System.Console.WriteLine("Pomyślnie zakończono wszystkie testy CalculateMathTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new CalculateMathTool();
            var schema = tool.GetToolSchema();
            
            Debug.Assert(schema.Function.Name == "CalculateMath", "Niewłaściwa nazwa narzędzia");
            Debug.Assert(schema.Function.Parameters.Required.Contains("Expression"), "Brakuje wymaganego parametru Expression");
        }

        private static void TestCalculationExecution()
        {
            var tool = new CalculateMathTool();
            
            // Proste dodawanie
            var args1 = new JObject { ["Expression"] = "5 + 10" };
            string result1 = tool.Execute(null, args1);
            Debug.Assert(result1.Contains("15"), $"Oczekiwano 15, otrzymano: {result1}");

            // Z jednostkami (np. pole powierzchni)
            var args2 = new JObject { ["Expression"] = "10_m * 5_m" };
            string result2 = tool.Execute(null, args2);
            Debug.Assert(result2.Contains("50_m2"), $"Oczekiwano 50_m2, otrzymano: {result2}");

            // Stała fizyczna #G i konwersja do dżuli
            var args3 = new JObject { ["Expression"] = "5.94_kg * #G * 10_m" };
            string result3 = tool.Execute(null, args3);
            Debug.Assert(result3.Contains("_J"), $"Oczekiwano wyniku z jednostką dżuli (_J), otrzymano: {result3}");

            // Objętość kuli z konwersją na cm3: V = 4/3 * pi * r^3
            var args4 = new JObject { ["Expression"] = "( 4 / 3 ) * #PI * ( 5_cm ^ 3 )", ["TargetUnit"] = "cm3" };
            string result4 = tool.Execute(null, args4);
            Debug.Assert(result4.Contains("523.5987"), $"Oczekiwano ok. 523.5987_cm3, otrzymano: {result4}");
        }

        private static void TestCalculationSaveAs()
        {
            var tool = new CalculateMathTool();
            
            var args = new JObject 
            { 
                ["Expression"] = "100 / 2",
                ["SaveAs"] = "TestMathVal"
            };
            
            string result = tool.Execute(null, args);
            Debug.Assert(AgentMemoryState.Variables.ContainsKey("TestMathVal"), "Nie zapisano wartości w zmiennych");
            Debug.Assert(AgentMemoryState.Variables["TestMathVal"] == "50", $"Oczekiwano wartości 50, otrzymano: {AgentMemoryState.Variables["TestMathVal"]}");
        }
    }
}
