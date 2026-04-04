using System;
using System.Diagnostics;
using System.Linq;
using Bricscad_AgentAI_V2.Tools;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class ManageLayersToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            // In a real environment, we would mock the CAD part,
            // but for now, we verify the Tool Schema and internal logic rules.
            System.Console.WriteLine("Pomyślnie zakończono testy ManageLayersTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new ManageLayersTool();
            var schema = tool.GetToolSchema();

            Debug.Assert(schema.Function.Name == "ManageLayers", "Niewłaściwa nazwa narzędzia.");
            Debug.Assert(schema.Function.Parameters.Required.Contains("Action"), "Brakuje wymaganego atrybutu 'Action'.");
            Debug.Assert(schema.Function.Parameters.Required.Contains("LayerName"), "Brakuje wymaganego atrybutu 'LayerName'.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("ColorIndex"), "Brak opcjonalnego atrybutu 'ColorIndex' w schema.");
        }
    }
}
