using System.Diagnostics;
using Bricscad_AgentAI_V2.Tools;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class ExtractRoomDataEntitiesToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            TestInstanceCreated();
            System.Console.WriteLine("Pomyślnie zakończono testy ExtractRoomDataEntitiesTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new ExtractRoomDataEntitiesTool();
            var schema = tool.GetToolSchema();

            Debug.Assert(schema.Function.Name == "ExtractRoomDataEntities", "Niewłaściwa nazwa narzędzia");
            Debug.Assert(schema.Function.Parameters.Required.Contains("boundaryLayer"), "Brak wymaganego 'boundaryLayer'");
            Debug.Assert(schema.Function.Parameters.Required.Contains("tagLayer"), "Brak wymaganego 'tagLayer'");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("saveAs"), "Brak parametru 'saveAs'");

            var boundaryLayer = schema.Function.Parameters.Properties["boundaryLayer"];
            Debug.Assert(boundaryLayer.Type == "string", "'boundaryLayer' musi być typu string");

            var tagLayer = schema.Function.Parameters.Properties["tagLayer"];
            Debug.Assert(tagLayer.Type == "string", "'tagLayer' musi być typu string");

            Debug.Assert(tool.Examples != null && tool.Examples.Count >= 2, "Wymagane minimum 2 przykłady użycia");
        }

        private static void TestInstanceCreated()
        {
            var tool = new ExtractRoomDataEntitiesTool();
            Debug.Assert(tool != null, "Nie udało się utworzyć instancji ExtractRoomDataEntitiesTool");
        }
    }
}
