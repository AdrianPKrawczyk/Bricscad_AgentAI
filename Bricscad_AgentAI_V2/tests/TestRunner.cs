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
                SelectEntitiesToolTests.RunTests();
                Bricscad_AgentAI_V2.Tests.UI.LatexToUnicodeConverterTests.RunTests();
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
