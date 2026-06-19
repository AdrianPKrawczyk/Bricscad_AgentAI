using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Bricscad_AgentAI_V2.Tools;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class ReadFieldsToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            TestExamplesProvided();
            TestClassificationHeuristic();
            TestBinaryFieldDetection();
            TestExtractFieldsFromText();
            System.Console.WriteLine("Pomyślnie zakończono testy ReadFieldsTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new ReadFieldsTool();
            var schema = tool.GetToolSchema();

            Debug.Assert(schema != null, "Schema nie moze byc null.");
            Debug.Assert(schema.Function != null, "Function nie moze byc null.");
            Debug.Assert(schema.Function.Name == "ReadFields",
                "Nazwa narzedzia powinna byc ReadFields, a jest: " + schema.Function.Name);
            Debug.Assert(schema.Function.Parameters != null, "Parameters nie moga byc null.");
            Debug.Assert(schema.Function.Parameters.Properties != null, "Properties nie moga byc null.");

            // Opcjonalne parametry powinny istniec.
            var props = schema.Function.Parameters.Properties;
            Debug.Assert(props.ContainsKey("SaveAs"),
                "Brak opcjonalnego parametru SaveAs w schema.");
            Debug.Assert(props.ContainsKey("IncludeRawCode"),
                "Brak opcjonalnego parametru IncludeRawCode w schema.");
            Debug.Assert(props.ContainsKey("FilterByCategory"),
                "Brak opcjonalnego parametru FilterByCategory w schema.");

            // FilterByCategory powinien miec enum z kategoriami.
            var filter = props["FilterByCategory"];
            Debug.Assert(filter.Enum != null && filter.Enum.Count >= 5,
                "FilterByCategory powinien miec enum z co najmniej 5 kategoriami.");
            Debug.Assert(filter.Enum.Contains("SystemVariable"),
                "Enum powinien zawierac SystemVariable.");
            Debug.Assert(filter.Enum.Contains("DateTime"),
                "Enum powinien zawierac DateTime.");
            Debug.Assert(filter.Enum.Contains("Expression"),
                "Enum powinien zawierac Expression.");
            Debug.Assert(filter.Enum.Contains("BinaryField"),
                "Enum powinien zawierac BinaryField (forma %<\\_FldIdx).");
        }

        private static void TestExamplesProvided()
        {
            var tool = new ReadFieldsTool();
            Debug.Assert(tool.Examples != null && tool.Examples.Count >= 2,
                "Brakuje przykladow uzycia - ReadFields powinien miec co najmniej 2 przyklady JSON.");

            foreach (var example in tool.Examples)
            {
                try
                {
                    var parsed = JObject.Parse(example);
                    Debug.Assert(parsed != null,
                        "Przyklad nie sparsowal sie jako JObject: " + example);
                }
                catch (System.Exception ex)
                {
                    Debug.Assert(false,
                        "Przyklad nie jest poprawnym JSON-em: " + example + " -> " + ex.Message);
                }
            }
        }

        private static void TestClassificationHeuristic()
        {
            string classifyMethod = "ClassifyFieldCode";
            var method = typeof(ReadFieldsTool).GetMethod(classifyMethod,
                BindingFlags.NonPublic | BindingFlags.Static);
            Debug.Assert(method != null, "Nie znaleziono prywatnej metody ClassifyFieldCode w ReadFieldsTool.");

            var cases = new[]
            {
                new { code = "%<\\AcVar \"DWGNAME\">", expected = "SystemVariable" },
                new { code = "%<\\AcDate \\yyyy-MM-dd>", expected = "DateTime" },
                new { code = "%<\\AcExpr 10*5>", expected = "Expression" },
                new { code = "%<\\AcDim \\Width>", expected = "DimensionProperty" },
                new { code = "%<\\AcObjProp Object.\\Layer>", expected = "ObjectProperty" },
                new { code = "%<\\AcProp BlockRef.\\LengthAnnotation>", expected = "BlockAttributeProperty" },
                new { code = "%<\\AcFido \"SHEET\">", expected = "SheetSet" },
                new { code = "%<\\AcVar2 \"x\">", expected = "Unknown" },
                new { code = "plain text", expected = "Unknown" },
                new { code = "", expected = "Unknown" }
            };

            foreach (var c in cases)
            {
                string actual = (string)method.Invoke(null, new object[] { c.code });
                Debug.Assert(actual == c.expected,
                    "ClassifyFieldCode('" + c.code + "') zwrocilo '" + actual +
                    "', oczekiwano '" + c.expected + "'");
            }
        }

        private static void TestBinaryFieldDetection()
        {
            string classifyMethod = "ClassifyFieldCode";
            var method = typeof(ReadFieldsTool).GetMethod(classifyMethod,
                BindingFlags.NonPublic | BindingFlags.Static);

            // Forma _FldIdx - wystepuje w rysunkach DWG przed przeliczeniem
            // lub gdy pole ma niestandardowy ewaluator.
            var binaryCases = new[]
            {
                "%<\\\\_FldIdx 0ec",
                "%<\\\\_FldIdx 0>",
                "%<\\\\_FldIdx 05>",
                "%<\\\\_FldIdx"
            };

            foreach (var code in binaryCases)
            {
                string actual = (string)method.Invoke(null, new object[] { code });
                Debug.Assert(actual == "BinaryField",
                    "ClassifyFieldCode('" + code + "') powinno zwrocic 'BinaryField', a zwrocilo '" + actual + "'");
            }
        }

        private static void TestExtractFieldsFromText()
        {
            var method = typeof(ReadFieldsTool).GetMethod("ExtractFieldsFromText",
                BindingFlags.NonPublic | BindingFlags.Static);
            Debug.Assert(method != null, "Nie znaleziono metody ExtractFieldsFromText.");

            // Tekst z wieloma polami - powinny zostac wyciagniete wszystkie.
            string multi = "Projekt: %<\\AcVar \"DWGNAME\"> | Srodek: %<\\AcDate \\HH:mm> | Koniec";
            var result = method.Invoke(null, new object[] { multi }) as System.Collections.IList;
            Debug.Assert(result != null && result.Count == 2,
                "Multi-field text powinien zwrocic 2 pola, zwrocil: " +
                (result == null ? "null" : result.Count.ToString()));

            // Tekst z forma binarna.
            string binary = "Projekt: %<\\\\_FldIdx 0ec";
            var result2 = method.Invoke(null, new object[] { binary }) as System.Collections.IList;
            Debug.Assert(result2 != null && result2.Count == 1,
                "Binary field text powinien zwrocic 1 pole, zwrocil: " +
                (result2 == null ? "null" : result2.Count.ToString()));

            // Tekst bez pol.
            string plain = "Zwykly tekst bez pol CAD.";
            var result3 = method.Invoke(null, new object[] { plain }) as System.Collections.IList;
            Debug.Assert(result3 != null && result3.Count == 0,
                "Plain text powinien zwrocic 0 pol, zwrocil: " +
                (result3 == null ? "null" : result3.Count.ToString()));
        }
    }
}