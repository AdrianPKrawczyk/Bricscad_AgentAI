using System.Diagnostics;
using Bricscad_AgentAI_V2.Tools;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class BatchWriteXDataToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            System.Console.WriteLine("Pomyślnie zakończono testy BatchWriteXDataTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new BatchWriteXDataTool();
            var schema = tool.GetToolSchema();

            Debug.Assert(schema.Function.Name == "BatchWriteXData", "Niewłaściwa nazwa narzędzia");
            Debug.Assert(schema.Function.Parameters.Required.Contains("appName"), "Brak wymaganego 'appName'");
            Debug.Assert(schema.Function.Parameters.Required.Contains("entitiesData"), "Brak wymaganego 'entitiesData'");

            var appName = schema.Function.Parameters.Properties["appName"];
            Debug.Assert(appName.Type == "string", "'appName' musi być typu string");

            var entitiesData = schema.Function.Parameters.Properties["entitiesData"];
            Debug.Assert(entitiesData.Type == "array", "'entitiesData' musi być typu array");
            Debug.Assert(entitiesData.Items != null, "'entitiesData' musi mieć zdefiniowane 'items' (schemat obiektu)");

            Debug.Assert(tool.Examples != null && tool.Examples.Count >= 2, "Wymagane minimum 2 przykłady użycia");
        }
    }
}
