using System;
using System.Diagnostics;
using Bricscad_AgentAI_V2.UI;

namespace Bricscad_AgentAI_V2.Tests.UI
{
    public static class LatexToUnicodeConverterTests
    {
        public static void RunTests()
        {
            TestSimpleUnits();
            TestScientificNotation();
            TestGreekAndMathSymbols();
            TestComplexExpressions();
            Console.WriteLine("Pomyślnie zakończono wszystkie testy LatexToUnicodeConverter.");
        }

        private static void TestSimpleUnits()
        {
            string input1 = @"$523,6\text{ mm}^3$";
            string expected1 = "523,6 mm³";
            string result1 = LatexToUnicodeConverter.Convert(input1);
            Debug.Assert(result1 == expected1, $"Oczekiwano: '{expected1}', Otrzymano: '{result1}'");

            string input2 = @"$10,10\text{ g}$";
            string expected2 = "10,10 g";
            string result2 = LatexToUnicodeConverter.Convert(input2);
            Debug.Assert(result2 == expected2, $"Oczekiwano: '{expected2}', Otrzymano: '{result2}'");

            string input3 = @"$0,99\text{ J}$";
            string expected3 = "0,99 J";
            string result3 = LatexToUnicodeConverter.Convert(input3);
            Debug.Assert(result3 == expected3, $"Oczekiwano: '{expected3}', Otrzymano: '{result3}'");
        }

        private static void TestScientificNotation()
        {
            string input = @"$5,236 \times 10^{-7}\text{ m}^3$";
            string expected = "5,236 × 10⁻⁷ m³";
            string result = LatexToUnicodeConverter.Convert(input);
            Debug.Assert(result == expected, $"Oczekiwano: '{expected}', Otrzymano: '{result}'");
        }

        private static void TestGreekAndMathSymbols()
        {
            string input = @"$\alpha + \beta \approx \pi$";
            string expected = "α + β ≈ π";
            string result = LatexToUnicodeConverter.Convert(input);
            Debug.Assert(result == expected, $"Oczekiwano: '{expected}', Otrzymano: '{result}'");
        }

        private static void TestComplexExpressions()
        {
            string input1 = @"$\frac{a}{b}$";
            string expected1 = "(a)/(b)";
            string result1 = LatexToUnicodeConverter.Convert(input1);
            Debug.Assert(result1 == expected1, $"Oczekiwano: '{expected1}', Otrzymano: '{result1}'");

            string input2 = @"$\sqrt{x}$";
            string expected2 = "√(x)";
            string result2 = LatexToUnicodeConverter.Convert(input2);
            Debug.Assert(result2 == expected2, $"Oczekiwano: '{expected2}', Otrzymano: '{result2}'");

            string input3 = @"Objętość kuli wynosi $523,6\text{ mm}^3$ (czyli $5,236 \times 10^{-7}\text{ m}^3$).";
            string expected3 = "Objętość kuli wynosi 523,6 mm³ (czyli 5,236 × 10⁻⁷ m³).";
            string result3 = LatexToUnicodeConverter.Convert(input3);
            Debug.Assert(result3 == expected3, $"Oczekiwano: '{expected3}', Otrzymano: '{result3}'");
        }
    }
}
