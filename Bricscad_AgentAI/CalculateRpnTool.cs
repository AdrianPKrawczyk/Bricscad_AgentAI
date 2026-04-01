using Bricscad.ApplicationServices;
using System.Text.RegularExpressions;

namespace BricsCAD_Agent
{
    public class CalculateRpnTool : ITool
    {
        public string ActionTag => "[ACTION:CALC_RPN]";
        public string Description => "Wykonuje obliczenia matematyczne i fizyczne używając Odwrotnej Notacji Polskiej (RPN). Obsługuje jednostki SI.";

        public string Execute(Document doc, string argsJson = "")
        {
            // Łapiemy wyrażenie z JSON-a
            string expr = Regex.Match(argsJson, @"\""Expression\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;

            if (!string.IsNullOrEmpty(expr))
            {
                try
                {
                    // Uruchamiamy silnik! Przekazujemy null dla Editora, bo Agent nie potrafi "klikać" na ekranie (DL/DX)
                    string result = RpnCalculator.Evaluate(expr, null, null, null);

                    // Prefix WYNIK: wymusi na Agencie odczytanie tej wartości w pętli czatu
                    return $"WYNIK: {result}";
                }
                catch (System.Exception ex)
                {
                    return $"BŁĄD KALKULATORA: {ex.Message}";
                }
            }
            return "BŁĄD: Brak parametru Expression. Użyj formatu {\"Expression\": \"10_m 2_s /\"}";
        }

        public string Execute(Document doc) => Execute(doc, "");
    }
}