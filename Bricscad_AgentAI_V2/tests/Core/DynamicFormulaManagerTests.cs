using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Bricscad_AgentAI_V2.Core.DynamicSystems;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    public class DynamicFormulaManagerTests
    {
        public static void RunTests()
        {
            Console.WriteLine("\n[TEST] Uruchamianie DynamicFormulaManagerTests...");
            int passed = 0;
            int failed = 0;

            if (Test_DynamicKompilacja_Dodawanie()) passed++; else failed++;

            Console.WriteLine($"[TEST] Zakończono DynamicFormulaManagerTests. Udane: {passed}, Błędy: {failed}");
        }

        private static bool Test_DynamicKompilacja_Dodawanie()
        {
            Console.WriteLine("  -> Uruchamianie: Test_DynamicKompilacja_Dodawanie");
            
            // a) Utworzenie tymczasowego katalogu na skrypty
            string tempDir = Path.Combine(Path.GetTempPath(), "Bricscad_AgentAI_Tests", "Formulas");
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
            Directory.CreateDirectory(tempDir);

            // Zmiana CustomFormulasPath, żeby manager wskazywał na tempDir
            string originalPath = DynamicFormulaManager.CustomFormulasPath;
            DynamicFormulaManager.CustomFormulasPath = tempDir;

            try
            {
                // b) Utworzenie pliku Test_Dodawanie.csx
                string filePath = Path.Combine(tempDir, "Test_Dodawanie.csx");
                string code = "Length a = Length.Parse(Inputs[\"A\"]); Length b = Length.Parse(Inputs[\"B\"]); return (a+b).ToString();";
                File.WriteAllText(filePath, code);

                // c) Wywołanie LoadAndCompileAll()
                DynamicFormulaManager.LoadAndCompileAll();

                // d) Wywołanie formuły
                var inputs = new Dictionary<string, string> { { "A", "15 m" }, { "B", "25 m" } };
                // ExecuteFormula jest asynchroniczne, więc używamy Task.Run lub GetAwaiter
                string result = DynamicFormulaManager.ExecuteFormula("Test_Dodawanie", inputs).GetAwaiter().GetResult();

                // e) Weryfikacja asercją
                if (result == "40 m")
                {
                    Console.WriteLine("     [SUKCES] Formuła zwróciła prawidłowy wynik: 40 m");
                    return true;
                }
                else
                {
                    Console.WriteLine($"     [BŁĄD] Oczekiwano '40 m', otrzymano: '{result}'");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"     [BŁĄD] Wyjątek w teście: {ex.Message}");
                return false;
            }
            finally
            {
                // f) Posprzątanie (usunięcie katalogu tymczasowego)
                DynamicFormulaManager.CustomFormulasPath = originalPath;
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
