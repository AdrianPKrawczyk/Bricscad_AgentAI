using System.Diagnostics;
using System.Linq;
using Bricscad_AgentAI_V2.Tools;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class ModifyPropertiesToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            System.Console.WriteLine("Pomyślnie zakończono testy ModifyPropertiesTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new ModifyPropertiesTool();
            var schema = tool.GetToolSchema();
            
            Debug.Assert(schema.Function.Name == "ModifyProperties", "Niewłaściwa nazwa narzędzia.");
            Debug.Assert(schema.Function.Parameters.Required.Contains("Modifications"), "Brakuje wymaganego atrybutu 'Modifications'.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("Modifications"), "Schema nie posiada atrybutu wejściowego 'Modifications'.");
        }
    }
}
