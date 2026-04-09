using System.Diagnostics;
using System.Linq;
using Bricscad_AgentAI_V2.Tools;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class ReadXDataToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            System.Console.WriteLine("Pomyślnie zakończono testy ReadXDataTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new ReadXDataTool();
            var schema = tool.GetToolSchema();
            
            Debug.Assert(schema.Function.Name == "ReadXData", "Niewłaściwa nazwa narzędzia");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("AppName"), "Brakuje parametru AppName");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("SaveAs"), "Brakuje parametru SaveAs");
            
            Debug.Assert(tool.Examples != null, "Brakuje przykładów użycia");
            Debug.Assert(tool.Examples.Count > 0, "Lista przykładów jest pusta");
        }
    }
}
