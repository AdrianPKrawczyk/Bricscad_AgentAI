using System.Diagnostics;
using System.Linq;
using Bricscad_AgentAI_V2.Tools;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class FindXDataToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            System.Console.WriteLine("Pomyślnie zakończono testy FindXDataTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new FindXDataTool();
            var schema = tool.GetToolSchema();
            
            Debug.Assert(schema.Function.Name == "FindXData", "Niewłaściwa nazwa narzędzia");
            Debug.Assert(schema.Function.Parameters.Required.Contains("Mode"), "Mode powinien być wymagany");
            
            Debug.Assert(tool.Examples != null && tool.Examples.Count > 0, "Brakuje przykładów użycia");
        }
    }
}
