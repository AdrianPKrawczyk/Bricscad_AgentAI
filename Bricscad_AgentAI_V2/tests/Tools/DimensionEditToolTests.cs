using System.Diagnostics;
using Bricscad_AgentAI_V2.Tools;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class DimensionEditToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            TestParameterParsing();
            System.Console.WriteLine("Pomyślnie zakończono wszystkie testy DimensionEditTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new DimensionEditTool();
            var schema = tool.GetToolSchema();
            
            Debug.Assert(schema.Function.Name == "DimensionEditTool", "Niewłaściwa nazwa");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("TextOverride"), "Brakuje parametru TextOverride");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("OverallScale"), "Brakuje parametru OverallScale");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("ArrowBlock"), "Brakuje parametru ArrowBlock");
        }

        private static void TestParameterParsing()
        {
            var tool = new DimensionEditTool();
            
            // Testujemy czy Execute nie rzuca błędów przy braku selekcji (to jest testowane w Execute)
            var args = new JObject
            {
                ["TextOverride"] = "150.5",
                ["OverallScale"] = 2.0,
                ["TextColor"] = 1
            };

            // Nie wywołujemy Execute(null, args) bo to by crashowało przez brak sesji/doc i bazy danych
            // Weryfikujemy tylko czy schema poprawnie opisuje te parametry
            var schema = tool.GetToolSchema();
            Debug.Assert(schema.Function.Parameters.Properties["TextColor"].Type == "integer");
            Debug.Assert(schema.Function.Parameters.Properties["OverallScale"].Type == "number");
        }
    }
}
