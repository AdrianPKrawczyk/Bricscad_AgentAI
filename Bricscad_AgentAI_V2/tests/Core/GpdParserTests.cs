using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Bricscad_AgentAI_V2.Core;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    public static class GpdParserTests
    {
        private static readonly string[] HpGpdRoots = new[]
        {
            @"C:\Windows\System32\DriverStore\FileRepository\hpi2144.inf_amd64_8c03221ca287856e",
            @"C:\Windows\System32\DriverStore\FileRepository\hpi2144.inf_amd64_a0912b8464ff4055",
            @"C:\Windows\System32\DriverStore\FileRepository\hpi2144.inf_amd64_ca9208ec948f1417"
        };

        public static void RunTests()
        {
            TestParseT120Gpd();
            TestParseT650Gpd();
            TestParseT520Gpd();
            TestParseReturnsErrorForMissingFile();
            TestT120HasA4A3A2A1();
            TestT650HasA0();
            TestCustomSizeIsPresent();
            TestCustomSizeDimensions();
            TestFindHpGpdForT120();
            TestFindHpGpdForT650();
            TestDimensionsAreCorrect();
        }

        private static string FindGpd(params string[] fileNames)
        {
            foreach (string root in HpGpdRoots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (string name in fileNames)
                {
                    string candidate = Path.Combine(root, name);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            return null;
        }

        private static void TestParseT120Gpd()
        {
            string path = FindGpd("hpiT120.gpd");
            Debug.Assert(path != null, "hpiT120.gpd not found in DriverStore.");
            if (path == null) return;

            var info = GpdParser.Parse(path);
            Debug.Assert(info.ParseSucceeded, "T120 GPD should parse: " + info.ParseError);
            Debug.Assert(info.ModelName != null && info.ModelName.Contains("T120"),
                "T120 model name should contain T120: " + info.ModelName);
            Debug.Assert(info.MediaFormats.Count > 15,
                "T120 should have many formats. Got: " + info.MediaFormats.Count);
        }

        private static void TestParseT650Gpd()
        {
            string path = FindGpd("hpiT65036-in.gpd");
            Debug.Assert(path != null, "hpiT65036-in.gpd not found.");
            if (path == null) return;

            var info = GpdParser.Parse(path);
            Debug.Assert(info.ParseSucceeded, "T650 GPD should parse: " + info.ParseError);
            Debug.Assert(info.MediaFormats.Count > 30,
                "T650 36-in should have 30+ formats. Got: " + info.MediaFormats.Count);
        }

        private static void TestParseT520Gpd()
        {
            string path = FindGpd("hpiT52036in.gpd");
            Debug.Assert(path != null, "hpiT52036in.gpd not found.");
            if (path == null) return;

            var info = GpdParser.Parse(path);
            Debug.Assert(info.ParseSucceeded, "T520 GPD should parse: " + info.ParseError);
            Debug.Assert(info.CustomSize != null, "T520 should have custom size defined.");
        }

        private static void TestParseReturnsErrorForMissingFile()
        {
            var info = GpdParser.Parse(@"C:\non_existent\nonexistent.gpd");
            Debug.Assert(!info.ParseSucceeded, "Missing GPD file should fail.");
            Debug.Assert(!string.IsNullOrEmpty(info.ParseError), "Missing GPD file should have error message.");
        }

        private static void TestT120HasA4A3A2A1()
        {
            string path = FindGpd("hpiT120.gpd");
            if (path == null) return;
            var info = GpdParser.Parse(path);
            var names = info.MediaFormats.Select(m => m.OptionName).ToHashSet();

            Debug.Assert(names.Contains("A4"), "T120 should support A4.");
            Debug.Assert(names.Contains("ISOA3"), "T120 should support ISOA3.");
            Debug.Assert(names.Contains("ISOA2"), "T120 should support ISOA2.");
            Debug.Assert(names.Contains("ISOA1"), "T120 should support ISOA1.");

            var a4 = info.MediaFormats.First(m => m.OptionName == "A4");
            Debug.Assert(Math.Abs(a4.WidthMm - 210.0) < 1.0, "A4 width should be ~210mm. Got: " + a4.WidthMm);
            Debug.Assert(Math.Abs(a4.HeightMm - 297.0) < 1.0, "A4 height should be ~297mm. Got: " + a4.HeightMm);
        }

        private static void TestT650HasA0()
        {
            string path = FindGpd("hpiT65036-in.gpd");
            if (path == null) return;
            var info = GpdParser.Parse(path);

            Debug.Assert(info.MediaFormats.Any(m => m.OptionName == "ISOA0"),
                "T650 36-in should support ISOA0 (unlike T120).");
            var a0 = info.MediaFormats.First(m => m.OptionName == "ISOA0");
            Debug.Assert(Math.Abs(a0.WidthMm - 841.0) < 1.0, "A0 width should be ~841mm. Got: " + a0.WidthMm);
            Debug.Assert(Math.Abs(a0.HeightMm - 1189.0) < 1.0, "A0 height should be ~1189mm. Got: " + a0.HeightMm);
        }

        private static void TestCustomSizeIsPresent()
        {
            string path = FindGpd("hpiT120.gpd", "hpiT65036-in.gpd");
            Debug.Assert(path != null, "T120 or T650 GPD should be available.");
            if (path == null) return;
            var info = GpdParser.Parse(path);
            Debug.Assert(info.CustomSize != null,
                "HP plotters should have CustomSize defined for roll paper.");
            Debug.Assert(info.MediaFormats.Any(m => m.OptionName == "CUSTOMSIZE"),
                "HP plotters should expose CUSTOMSIZE option.");
        }

        private static void TestCustomSizeDimensions()
        {
            string path = FindGpd("hpiT120.gpd");
            if (path == null) return;
            var info = GpdParser.Parse(path);

            Debug.Assert(info.CustomSize.MaxWidthMm > 500.0,
                "T120 max custom width should be > 500mm (24 inch roll). Got: " + info.CustomSize.MaxWidthMm);
            Debug.Assert(info.CustomSize.MinWidthMm < 100.0,
                "T120 min custom width should be < 100mm. Got: " + info.CustomSize.MinWidthMm);
        }

        private static void TestFindHpGpdForT120()
        {
            string found = GpdParser.FindHpGpdForDevice("HP Designjet T120 - Adrian.pc3", null, "HP DesignJet T120 V4");
            Debug.Assert(found != null && Path.GetFileName(found).Equals("hpiT120.gpd", StringComparison.OrdinalIgnoreCase),
                "FindHpGpdForDevice should find hpiT120.gpd for T120 device. Got: " +
                (found != null ? Path.GetFileName(found) : "(null)"));
        }

        private static void TestFindHpGpdForT650()
        {
            string found = GpdParser.FindHpGpdForDevice("HP DesignJet T650 36-in.pc3", null, "HP DesignJet T650 36-in V4");
            Debug.Assert(found != null && Path.GetFileName(found).Equals("hpiT65036-in.gpd", StringComparison.OrdinalIgnoreCase),
                "FindHpGpdForDevice should find hpiT65036-in.gpd for T650 device. Got: " +
                (found != null ? Path.GetFileName(found) : "(null)"));
        }

        private static void TestDimensionsAreCorrect()
        {
            string path = FindGpd("hpiT120.gpd");
            if (path == null) return;
            var info = GpdParser.Parse(path);

            var a1 = info.MediaFormats.First(m => m.OptionName == "ISOA1");
            Debug.Assert(Math.Abs(a1.WidthMm - 594.0) < 1.0, "A1 width should be ~594mm. Got: " + a1.WidthMm);
            Debug.Assert(Math.Abs(a1.HeightMm - 841.0) < 1.0, "A1 height should be ~841mm. Got: " + a1.HeightMm);

            var letter = info.MediaFormats.First(m => m.OptionName == "LETTER");
            Debug.Assert(Math.Abs(letter.WidthMm - 215.9) < 0.5, "LETTER width should be ~215.9mm (8.5\"). Got: " + letter.WidthMm);
            Debug.Assert(Math.Abs(letter.HeightMm - 279.4) < 0.5, "LETTER height should be ~279.4mm (11\"). Got: " + letter.HeightMm);
        }
    }
}