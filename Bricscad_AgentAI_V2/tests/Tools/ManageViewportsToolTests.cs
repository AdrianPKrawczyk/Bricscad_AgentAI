using System;
using System.Diagnostics;
using System.Linq;
using Bricscad_AgentAI_V2.Tools.Layout;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class ManageViewportsToolTests
    {
        public static void RunTests()
        {
            TestGetToolSchema();
            TestParseViewportScale();
            Console.WriteLine("Pomyslnie zakonczono testy ManageViewportsTool.");
        }

        private static void TestGetToolSchema()
        {
            var tool = new ManageViewportsTool();
            var schema = tool.GetToolSchema();

            Debug.Assert(schema.Function.Name == "ManageViewportsTool", "Niewlasciwa nazwa narzedzia.");
            Debug.Assert(schema.Function.Parameters.Required.Contains("Action"), "Brakuje wymaganego atrybutu 'Action'.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("ViewportHandle"), "Brak parametru ViewportHandle.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("ViewportIndex"), "Brak parametru ViewportIndex.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("ModelMinX"), "Brak parametru ModelMinX.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("Scale"), "Brak parametru Scale.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("AnnotationScale"), "Brak parametru AnnotationScale.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("NamedView"), "Brak parametru NamedView.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("FreezeLayers"), "Brak parametru FreezeLayers.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("ThawLayers"), "Brak parametru ThawLayers.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("ThawAllLayers"), "Brak parametru ThawAllLayers.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("ClipBoundaryHandle"), "Brak parametru ClipBoundaryHandle.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("ClipBoundaryPaperPoints"), "Brak parametru ClipBoundaryPaperPoints.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("RemoveNonRectClip"), "Brak parametru RemoveNonRectClip.");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("OverwriteUnlocked"), "Brak parametru OverwriteUnlocked.");
        }

        private static void TestParseViewportScale()
        {
            AssertScale("1:50", 0.02);
            AssertScale("1_100", 0.01);
            AssertScale("1/25", 0.04);
            AssertScale("0.02", 0.02);
            AssertScale("0,02", 0.02);

            double scale;
            Debug.Assert(!ManageViewportsTool.TryParseViewportScale("1:0", out scale), "Skala 1:0 powinna byc odrzucona.");
            Debug.Assert(!ManageViewportsTool.TryParseViewportScale("abc", out scale), "Tekst abc powinien byc odrzucony.");
        }

        private static void AssertScale(string input, double expected)
        {
            double scale;
            Debug.Assert(ManageViewportsTool.TryParseViewportScale(input, out scale), $"Skala '{input}' powinna byc poprawna.");
            Debug.Assert(Math.Abs(scale - expected) < 1e-9, $"Skala '{input}' ma wartosc {scale}, oczekiwano {expected}.");
        }
    }
}
