using System.Diagnostics;
using System.Linq;
using Bricscad_AgentAI_V2.Tools;

namespace Bricscad_AgentAI_V2.Tests.Tools
{
    public static class MetricVisionToolTests
    {
        public static void RunTests()
        {
            TestCaptureMetricVisionAreaSchema();
            TestScanMetricVisionDrawingSchema();
            TestQueryVisionScanIndexSchema();
            TestDiagnoseMetricVisionGraphicsSystemSchema();
            System.Console.WriteLine("Pomyslnie zakonczono testy MetricVision tools.");
        }

        private static void TestCaptureMetricVisionAreaSchema()
        {
            var tool = new CaptureMetricVisionAreaTool();
            var schema = tool.GetToolSchema();
            Debug.Assert(schema.Function.Name == "CaptureMetricVisionArea", "Niewlasciwa nazwa narzedzia CaptureMetricVisionArea");
            Debug.Assert(schema.Function.Parameters.Required.Contains("MinX"), "MinX powinien byc wymagany");
            Debug.Assert(schema.Function.Parameters.Required.Contains("MinY"), "MinY powinien byc wymagany");
            Debug.Assert(schema.Function.Parameters.Required.Contains("MaxX"), "MaxX powinien byc wymagany");
            Debug.Assert(schema.Function.Parameters.Required.Contains("MaxY"), "MaxY powinien byc wymagany");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("Profile"), "Brakuje parametru Profile");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("UseExperimentalOffscreen"), "Brakuje parametru UseExperimentalOffscreen");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("AllowScreenFallback"), "Brakuje parametru AllowScreenFallback");
        }

        private static void TestScanMetricVisionDrawingSchema()
        {
            var tool = new ScanMetricVisionDrawingTool();
            var schema = tool.GetToolSchema();
            Debug.Assert(schema.Function.Name == "ScanMetricVisionDrawing", "Niewlasciwa nazwa narzedzia ScanMetricVisionDrawing");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("Scope"), "Brakuje parametru Scope");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("AnalyzeNow"), "Brakuje parametru AnalyzeNow");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("UseExperimentalOffscreen"), "Brakuje parametru UseExperimentalOffscreen");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("AllowScreenFallback"), "Brakuje parametru AllowScreenFallback");
            Debug.Assert(schema.Function.Parameters.Properties["Profile"].Enum.Contains("OcrLabels"), "Profile powinien zawierac OcrLabels");
        }

        private static void TestQueryVisionScanIndexSchema()
        {
            var tool = new QueryVisionScanIndexTool();
            var schema = tool.GetToolSchema();
            Debug.Assert(schema.Function.Name == "QueryVisionScanIndex", "Niewlasciwa nazwa narzedzia QueryVisionScanIndex");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("ScanId"), "Brakuje parametru ScanId");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("Query"), "Brakuje parametru Query");
        }

        private static void TestDiagnoseMetricVisionGraphicsSystemSchema()
        {
            var tool = new DiagnoseMetricVisionGraphicsSystemTool();
            var schema = tool.GetToolSchema();
            Debug.Assert(schema.Function.Name == "DiagnoseMetricVisionGraphicsSystem", "Niewlasciwa nazwa narzedzia DiagnoseMetricVisionGraphicsSystem");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("Stage"), "Brakuje parametru Stage");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("Resolution"), "Brakuje parametru Resolution");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("MinX"), "Brakuje parametru MinX");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("MinY"), "Brakuje parametru MinY");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("MaxX"), "Brakuje parametru MaxX");
            Debug.Assert(schema.Function.Parameters.Properties.ContainsKey("MaxY"), "Brakuje parametru MaxY");
            Debug.Assert(schema.Function.Parameters.Properties["Stage"].Enum.Contains("GraphicsManager"), "Stage powinien zawierac GraphicsManager");
        }
    }
}
