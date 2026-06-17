using System.Diagnostics;
using Bricscad_AgentAI_V2.Core;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    public static class FileExtractorTests
    {
        public static void RunTests()
        {
            TestRepairPolishMojibake();
            TestRepairPolishMojibakeLeavesCleanText();
        }

        private static void TestRepairPolishMojibake()
        {
            string broken = "GMINA WROCĹAW, ul. WaĹ‚brzyska, ZawĂłr, RzÄ…pia";
            string repaired = FileExtractor.RepairPolishMojibake(broken);

            Debug.Assert(repaired.Contains("GMINA WROCŁAW"), "Powinien naprawic L z kreska.");
            Debug.Assert(repaired.Contains("Wałbrzyska"), "Powinien naprawic l z kreska.");
            Debug.Assert(repaired.Contains("Zawór"), "Powinien naprawic o z kreska.");
            Debug.Assert(repaired.Contains("Rząpia"), "Powinien naprawic a z ogonkiem.");
        }

        private static void TestRepairPolishMojibakeLeavesCleanText()
        {
            string clean = "GMINA WROCŁAW, ul. Wałbrzyska, Zawór, Rząpia";
            string repaired = FileExtractor.RepairPolishMojibake(clean);

            Debug.Assert(repaired == clean, "Czysty tekst nie powinien byc zmieniany.");
        }
    }
}
