using System;
using Bricscad_AgentAI_V2.Tests.Tools;

namespace Bricscad_AgentAI_V2.Tests
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                ForeachToolTests.RunTests();
                ReadXDataToolTests.RunTests();
                WriteXDataToolTests.RunTests();
                FindXDataToolTests.RunTests();
                ExtractRoomDataEntitiesToolTests.RunTests();
                BatchWriteXDataToolTests.RunTests();
                MetricVisionToolTests.RunTests();
                SelectEntitiesToolTests.RunTests();
                Bricscad_AgentAI_V2.Tests.UI.LatexToUnicodeConverterTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.DynamicFormulaManagerTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.VisionOcrTilerTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.FileExtractorTests.RunTests();
                Console.WriteLine("WSZYSTKIE TESTY ZAKOŃCZONE SUKCESEM.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BŁĄD TESTÓW: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }
    }
}
