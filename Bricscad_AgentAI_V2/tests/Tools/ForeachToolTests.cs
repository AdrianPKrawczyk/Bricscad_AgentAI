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
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("GenerateSequence"), "Brak pola 'GenerateSequence' w schemacie");
            
            var genSeq = schema.Function.Parameters.Properties["GenerateSequence"];
            Debug.Assert(genSeq.Type == "object", "GenerateSequence musi być typu object");
            Debug.Assert(genSeq.Properties.ContainsKey("StartVector"), "Brak pola StartVector");
            Debug.Assert(genSeq.Properties.ContainsKey("OffsetVector"), "Brak pola OffsetVector");
            Debug.Assert(genSeq.Properties.ContainsKey("Count"), "Brak pola Count");
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
