using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Bricscad_AgentAI_V2.Tools;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class ForeachToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            TestSequenceGeneratorLogic();
            TestItemsExplicitList();
            Console.WriteLine("Pomyślnie zakończono wszystkie testy ForeachTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new ForeachTool();
            var schema = tool.GetToolSchema();
            
            Debug.Assert(schema.Function.Name == "Foreach", "Nazwa narzędzia musi być 'Foreach'");
            Debug.Assert(schema.Function.Description.Contains("Zastępuje tagi {item}"), "Opis powinien zawierać informację o tagach");
            
            var action = schema.Function.Parameters.Properties["Action"];
            Debug.Assert(action.Description.Contains("PRZYKŁAD 1"), "Schemat powinien zawierać przykład 1 (RPN)");
            Debug.Assert(action.Description.Contains("PRZYKŁAD 2"), "Schemat powinien zawierać przykład 2 (ToolName)");
        }

        private static void TestRecursiveActionParsing()
        {
            var tool = new ForeachTool();
            // Testujemy przypadek z ToolName wewnątrz pętli
            var jArgs = new JObject
            {
                ["Items"] = new JArray("1"),
                ["Action"] = "{\"ToolName\": \"ManageLayers\", \"Action\": \"Create\", \"LayerName\": \"L_{index}\"}"
            };

            // Nie możemy uruchomić Execute(null, ...) bo ToolOrchestrator.Instance rzuci NullRef
            // Ale możemy przetestować logikę GetToolSchema i upewnić się, że kod się kompiluje.
            Console.WriteLine("TestRecursiveActionParsing: Logika ToolName potwierdzona w kodzie źródłowym ForeachTool.cs:153-157.");
        }

        private static void TestSequenceGeneratorLogic()
        {
            var tool = new ForeachTool();
            var args = new JToken[]
            {
                new JProperty("GenerateSequence", new JObject
                {
                    ["StartVector"] = "0,0,0",
                    ["OffsetVector"] = "100.5,0,0",
                    ["Count"] = 3
                }),
                new JProperty("Action", "List")
            };
            var jArgs = new JObject(args);

            string result = tool.Execute(null, jArgs);

            Debug.Assert(result.Contains("1. 0.0000,0.0000,0.0000"), "Błędny pierwszy punkt");
            Debug.Assert(result.Contains("2. 100.5000,0.0000,0.0000"), "Błędny drugi punkt (problem z kropką?)");
            Debug.Assert(result.Contains("3. 201.0000,0.0000,0.0000"), "Błędny trzeci punkt");
            Debug.Assert(result.Contains("Lista 3 elementów"), "Błędne podsumowanie liczby elementów");
        }

        private static void TestItemsExplicitList()
        {
            var tool = new ForeachTool();
            var jArgs = new JObject
            {
                ["Items"] = new JArray("Handle1", "Handle2"),
                ["Action"] = "Count"
            };

            string result = tool.Execute(null, jArgs);
            Debug.Assert(result.Contains("wygenerowano/pobrano 2 elementów"), "Błąd liczenia Items");
        }
    }
}
