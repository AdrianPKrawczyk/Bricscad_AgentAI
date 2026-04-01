using Bricscad.ApplicationServices;
using System.Text.RegularExpressions;

namespace BricsCAD_Agent
{
    public class SendCommandTool : ITool
    {
        public string ActionTag => "[ACTION:SEND_TO_CMD]";
        public string Description => "Wysyła czysty tekst lub wyliczoną liczbę bezpośrednio do paska poleceń. Obsługuje kalkulator RPN (np. RPN: 2 2 +).";

        public string Execute(Document doc, string argsJson = "")
        {
            // Łapiemy wartość tekstową lub liczbową
            string val = Regex.Match(argsJson, @"\""Value\""\s*:\s*(\""[^\""]+\""|[-0-9.]+)").Groups[1].Value.Trim('\"');

            if (!string.IsNullOrEmpty(val))
            {
                // Obliczanie RPN
                if (val.StartsWith("RPN:", System.StringComparison.OrdinalIgnoreCase))
                {
                    try { val = RpnCalculator.Evaluate(val.Substring(4).Trim()); }
                    catch (System.Exception ex) { return $"BŁĄD KALKULATORA RPN: {ex.Message}"; }
                }

                // Zamieniamy przecinki na kropki (CAD wymaga kropek dziesiętnych)
                val = val.Replace(",", ".");

                // Wstrzykujemy wartość + Enter do konsoli CADa!
                doc.SendStringToExecute(val + "\n", true, false, false);

                // --- POPRAWKA: Dodano prefiks WYNIK: ---
                return $"WYNIK: Wysłano '{val}' do paska poleceń.";
            }
            return "BŁĄD: Brak parametru Value.";
        }

        public string Execute(Document doc) => Execute(doc, "");
    }
}