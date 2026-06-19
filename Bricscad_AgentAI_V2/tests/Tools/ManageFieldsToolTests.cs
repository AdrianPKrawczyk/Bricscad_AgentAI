using System;
using System.Diagnostics;
using System.Linq;
using Bricscad_AgentAI_V2.Tools;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class ManageFieldsToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            TestRequiredActionParameter();
            TestAllActionsCovered();
            TestExamplesAreValidJson();
            TestFieldCodeValidation();
            TestNewFilterParameters();
            System.Console.WriteLine("Pomyślnie zakończono testy ManageFieldsTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new ManageFieldsTool();
            var schema = tool.GetToolSchema();

            Debug.Assert(schema != null, "Schema nie moze byc null.");
            Debug.Assert(schema.Function != null, "Function nie moze byc null.");
            Debug.Assert(schema.Function.Name == "ManageFields",
                "Nazwa narzedzia powinna byc ManageFields, a jest: " + schema.Function.Name);
            Debug.Assert(schema.Function.Parameters != null, "Parameters nie moga byc null.");
            Debug.Assert(schema.Function.Parameters.Required != null,
                "Required nie moze byc null.");
            Debug.Assert(schema.Function.Parameters.Properties != null,
                "Properties nie moga byc null.");
        }

        private static void TestRequiredActionParameter()
        {
            var tool = new ManageFieldsTool();
            var required = tool.GetToolSchema().Function.Parameters.Required;

            Debug.Assert(required.Contains("Action"),
                "Brak wymaganego parametru Action w schema ManageFields.");
            Debug.Assert(required.Count == 1,
                "ManageFields powinien miec dokladnie 1 wymagany parametr (Action), a ma: "
                + required.Count);
        }

        private static void TestAllActionsCovered()
        {
            var tool = new ManageFieldsTool();
            var actionParam = tool.GetToolSchema().Function.Parameters.Properties["Action"];

            Debug.Assert(actionParam.Enum != null && actionParam.Enum.Count == 5,
                "Action enum powinien miec dokladnie 5 wartosci (InsertField, RemoveField, " +
                "ReplaceFieldCode, ConvertToText, EvaluateAll).");

            var expected = new[]
            {
                "InsertField", "RemoveField", "ReplaceFieldCode",
                "ConvertToText", "EvaluateAll"
            };
            foreach (var exp in expected)
            {
                Debug.Assert(actionParam.Enum.Contains(exp),
                    "Action enum brakuje wartosci: " + exp);
            }
        }

        private static void TestExamplesAreValidJson()
        {
            var tool = new ManageFieldsTool();
            Debug.Assert(tool.Examples != null && tool.Examples.Count >= 4,
                "ManageFields powinien miec co najmniej 4 przyklady uzycia.");

            foreach (var example in tool.Examples)
            {
                try
                {
                    var parsed = JObject.Parse(example);
                    Debug.Assert(parsed["Action"] != null,
                        "Przyklad nie zawiera wymaganego pola Action: " + example);
                }
                catch (Exception ex)
                {
                    Debug.Assert(false,
                        "Przyklad nie jest poprawnym JSON-em: " + example + " -> " + ex.Message);
                }
            }
        }

        private static void TestFieldCodeValidation()
        {
            var tool = new ManageFieldsTool();

            bool foundValidCode = false;
            foreach (var example in tool.Examples)
            {
                if (example.Contains("%<") && example.Contains(">"))
                {
                    foundValidCode = true;
                    break;
                }
            }
            Debug.Assert(foundValidCode,
                "Przynajmniej jeden przyklad powinien zawierac poprawny kod pola %<...>.");
        }

        private static void TestNewFilterParameters()
        {
            // Po naprawie B1+B3+B4: ManageFields powinien miec filtry:
            // - MatchText (filtr po tresci encji)
            // - BlockNameFilter (filtr po nazwie bloku dla atrybutow)
            // - AttributeTagFilter (filtr po tagu atrybutu)
            var tool = new ManageFieldsTool();
            var props = tool.GetToolSchema().Function.Parameters.Properties;

            Debug.Assert(props.ContainsKey("MatchText"),
                "Brak parametru MatchText - umozliwia filtrowanie po tresci obiektu.");
            Debug.Assert(props.ContainsKey("BlockNameFilter"),
                "Brak parametru BlockNameFilter - umozliwia filtrowanie po nazwie bloku.");
            Debug.Assert(props.ContainsKey("AttributeTagFilter"),
                "Brak parametru AttributeTagFilter - umozliwia filtrowanie atrybutow po Tag.");

            // Targets powinien akceptowac "Selection" - dla BlockReference
            // w Selection powinien wejsc w atrybuty.
            var targetsParam = props["Targets"];
            Debug.Assert(targetsParam != null, "Brak Targets w schema.");
            Debug.Assert(targetsParam.Enum != null && targetsParam.Enum.Contains("Selection"),
                "Targets powinien zawierac 'Selection'.");

            // Przyklady powinny demonstrowac nowe filtry.
            bool hasMatchTextExample = tool.Examples.Any(e =>
                e.Contains("MatchText") || e.Contains("BlockNameFilter") || e.Contains("AttributeTagFilter"));
            Debug.Assert(hasMatchTextExample,
                "Przynajmniej jeden przyklad powinien demonstrowac MatchText/BlockNameFilter/AttributeTagFilter.");
        }
    }
}