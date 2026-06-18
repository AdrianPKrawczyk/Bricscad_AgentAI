using System;
using System.Diagnostics;
using System.Linq;
using Bricscad_AgentAI_V2.Tools.Layout;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class PublishToPdfToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            Console.WriteLine("Pomyslnie zakonczono testy PublishToPdfTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new PublishToPdfTool();
            var schema = tool.GetToolSchema();

            Debug.Assert(schema.Function.Name == "PublishToPdfTool", "Niewlasciwa nazwa narzedzia.");
            Debug.Assert(schema.Function.Parameters.Required.Contains("OutputPdfPath"), "Brakuje wymaganego atrybutu OutputPdfPath.");
            Debug.Assert(schema.Function.Parameters.Required.Contains("Mode"), "Brakuje wymaganego atrybutu Mode.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("LayoutNames"), "Brak parametru LayoutNames.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("FileNamePrefix"), "Brak parametru FileNamePrefix.");
        }
    }
}
