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
                ManageViewportsToolTests.RunTests();
                PublishToPdfToolTests.RunTests();
                ReadFieldsToolTests.RunTests();
                ManageFieldsToolTests.RunTests();
                CadTextProfileTests.RunTests();
                Bricscad_AgentAI_V2.Tests.UI.LatexToUnicodeConverterTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.DynamicFormulaManagerTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.VisionOcrTilerTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.FileExtractorTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.Pc3ParserTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.GpdParserTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.UserMediaResolverTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.WorkValidatorTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.RewidentCircuitBreakerTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.RewidentRegressionTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.AuditorReportTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.AntiLoopDetectorTests.RunTests();
                Bricscad_AgentAI_V2.Tests.Core.LLMClientTests.RunTests();
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
