using System;
using System.Diagnostics;
using Bricscad_AgentAI_V2.Core;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    public static class Pc3ParserTests
    {
        private static readonly string[] TestPc3Files = new[]
        {
            @"C:\Users\Adrian\AppData\Roaming\Bricsys\BricsCAD\V22x64\pl_PL\PlotConfig\HP Designjet T120 - Adrian.pc3",
            @"C:\Users\Adrian\AppData\Roaming\Bricsys\BricsCAD\V22x64\pl_PL\PlotConfig\HP DesignJet T650 36-in.pc3",
            @"C:\Users\Adrian\AppData\Roaming\Bricsys\BricsCAD\V22x64\pl_PL\PlotConfig\Print As PDF.pc3",
            @"C:\Users\Adrian\AppData\Roaming\Bricsys\BricsCAD\V22x64\pl_PL\PlotConfig\Default Windows System Printer.pc3"
        };

        public static void RunTests()
        {
            TestDecompressesAndParsesHpT120();
            TestDecompressesAndParsesHpT650();
            TestDecompressesAndParsesPrintAsPdf();
            TestDecompressesAndParsesDefaultPrinter();
            TestReturnsErrorForMissingFile();
            TestReturnsErrorForEmptyPath();
            TestDetectsWinDriverName();
            TestDetectsMediaDimensions();
            TestDetectsResolution();
        }

        private static void TestDecompressesAndParsesHpT120()
        {
            var info = Pc3Parser.Parse(TestPc3Files[0]);
            Debug.Assert(info.ParseSucceeded,
                "HP T120 PC3 powinien zdekompresowac i sparsowac. Blad: " + info.ParseError);
            Debug.Assert(info.SelectedMedia != null,
                "HP T120 powinien miec wybrany format (297x600 roll).");
            Debug.Assert(info.SelectedMedia != null && info.SelectedMedia.Name != null && info.SelectedMedia.Name.Contains("297"),
                "HP T120 media name powinno zawierac '297'. Faktycznie: " +
                (info.SelectedMedia != null ? info.SelectedMedia.Name : "(null)"));
            Debug.Assert(info.WinDriverName != null && info.WinDriverName.Contains("T120"),
                "HP T120 win_driver_name powinien zawierac 'T120'. Faktycznie: " + info.WinDriverName);
            Debug.Assert(info.DriverPath != null && info.DriverPath.Contains("gdiplot"),
                "HP T120 driver_pathname powinien byc 'gdiplot7.hdi'. Faktycznie: " + info.DriverPath);
        }

        private static void TestDecompressesAndParsesHpT650()
        {
            var info = Pc3Parser.Parse(TestPc3Files[1]);
            Debug.Assert(info.ParseSucceeded,
                "HP T650 PC3 powinien zdekompresowac i sparsowac. Blad: " + info.ParseError);
            Debug.Assert(info.SelectedMedia != null && info.SelectedMedia.MediaBoundsUry > 900.0,
                "HP T650 ma roll 297x600 -> bounds ury > 900mm. Faktycznie: " +
                (info.SelectedMedia != null ? info.SelectedMedia.MediaBoundsUry.ToString() : "(null)"));
            Debug.Assert(info.WinDriverName != null && info.WinDriverName.Contains("T650"),
                "HP T650 win_driver_name powinien zawierac 'T650'. Faktycznie: " + info.WinDriverName);
        }

        private static void TestDecompressesAndParsesPrintAsPdf()
        {
            var info = Pc3Parser.Parse(TestPc3Files[2]);
            Debug.Assert(info.ParseSucceeded,
                "Print As PDF PC3 powinien zdekompresowac i sparsowac. Blad: " + info.ParseError);
            Debug.Assert(info.SelectedMedia != null && info.SelectedMedia.Name == "A0",
                "Print As PDF domyslnie ma A0. Faktycznie: " +
                (info.SelectedMedia != null ? info.SelectedMedia.Name : "(null)"));
            Debug.Assert(info.SelectedMedia != null &&
                         Math.Abs(info.SelectedMedia.MediaBoundsUrx - 841.0) < 1.0,
                "A0 width powinno byc ~841mm. Faktycznie: " +
                (info.SelectedMedia != null ? info.SelectedMedia.MediaBoundsUrx.ToString() : "(null)"));
            Debug.Assert(info.SelectedMedia != null &&
                         Math.Abs(info.SelectedMedia.MediaBoundsUry - 1189.0) < 1.0,
                "A0 height powinno byc ~1189mm. Faktycznie: " +
                (info.SelectedMedia != null ? info.SelectedMedia.MediaBoundsUry.ToString() : "(null)"));
        }

        private static void TestDecompressesAndParsesDefaultPrinter()
        {
            var info = Pc3Parser.Parse(TestPc3Files[3]);
            Debug.Assert(info.ParseSucceeded,
                "Default Windows System Printer PC3 powinien zdekompresowac i sparsowac. Blad: " + info.ParseError);
            Debug.Assert(info.SelectedMedia != null && info.SelectedMedia.Name == "A4",
                "Default printer ma A4. Faktycznie: " +
                (info.SelectedMedia != null ? info.SelectedMedia.Name : "(null)"));
        }

        private static void TestReturnsErrorForMissingFile()
        {
            var info = Pc3Parser.Parse(@"C:\non_existent_path\nonexistent.pc3");
            Debug.Assert(!info.ParseSucceeded,
                "Parsowanie nieistniejacego pliku powinno zwrocic ParseSucceeded=false.");
            Debug.Assert(!string.IsNullOrEmpty(info.ParseError),
                "Parsowanie nieistniejacego pliku powinno zwrocic ParseError.");
        }

        private static void TestReturnsErrorForEmptyPath()
        {
            var info = Pc3Parser.Parse(null);
            Debug.Assert(!info.ParseSucceeded, "Parsowanie pustej sciezki powinno zwrocic ParseSucceeded=false.");
            Debug.Assert(!string.IsNullOrEmpty(info.ParseError),
                "Parsowanie pustej sciezki powinno zwrocic ParseError.");
        }

        private static void TestDetectsWinDriverName()
        {
            var info = Pc3Parser.Parse(TestPc3Files[0]);
            Debug.Assert(info.ParseSucceeded && !string.IsNullOrEmpty(info.WinDriverName),
                "HP T120 powinien miec win_driver_name (np. 'HP DesignJet T120 V4'). Faktycznie: " + info.WinDriverName);
            Debug.Assert(info.ParseSucceeded && !string.IsNullOrEmpty(info.FriendlyNetName),
                "HP T120 powinien miec friendly_net_name. Faktycznie: " + info.FriendlyNetName);
        }

        private static void TestDetectsMediaDimensions()
        {
            var info = Pc3Parser.Parse(TestPc3Files[2]);
            Debug.Assert(info.ParseSucceeded && info.SelectedMedia != null &&
                         info.SelectedMedia.MediaBoundsUrx > 0 && info.SelectedMedia.MediaBoundsUry > 0,
                "Print As PDF powinien miec wymiary bounds > 0. Faktycznie: " +
                (info.SelectedMedia != null
                    ? (info.SelectedMedia.MediaBoundsUrx + "x" + info.SelectedMedia.MediaBoundsUry)
                    : "(null)"));
        }

        private static void TestDetectsResolution()
        {
            var info = Pc3Parser.Parse(TestPc3Files[0]);
            Debug.Assert(info.ParseSucceeded && info.Resolution != null,
                "HP T120 powinien miec sekcje resolution.");
            Debug.Assert(info.Resolution != null && info.Resolution.PhysResolutionX > 0,
                "HP T120 rozdzielczosc X powinna byc > 0. Faktycznie: " +
                (info.Resolution != null ? info.Resolution.PhysResolutionX.ToString() : "(null)"));
        }
    }
}