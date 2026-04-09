using System.Diagnostics;
using System.Linq;
using Bricscad_AgentAI_V2.Tools;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class WriteXDataToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            System.Console.WriteLine("Pomyślnie zakończono testy WriteXDataTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new WriteXDataTool();
            var schema = tool.GetToolSchema();
            
            Debug.Assert(schema.Function.Name == "WriteXData", "Niewłaściwa nazwa narzędzia");
            Debug.Assert(schema.Function.Parameters.Required.Contains("AppName"), "AppName powinien być wymagany");
            Debug.Assert(schema.Function.Parameters.Required.Contains("Entries"), "Entries powinno być wymagane");
            
            Debug.Assert(tool.Examples != null, "Brakuje przykładów użycia");
        }
    }
}
