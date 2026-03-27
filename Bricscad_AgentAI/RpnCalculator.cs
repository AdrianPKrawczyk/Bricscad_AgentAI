using System;
using System.Collections.Generic;

namespace BricsCAD_Agent
{
    public class RpnCalculator
    {
        /// <summary>
        /// Wykonuje operacje w notacji RPN. 
        /// Zakłada, że na samym dnie stosu zawsze leży "obecna wartość" właściwości obiektu.
        /// </summary>
        public static double Evaluate(double currentValue, string rpnExpression)
        {
            if (string.IsNullOrWhiteSpace(rpnExpression))
                return currentValue;

            // Inicjalizujemy stos obecną wartością z BricsCADa
            Stack<double> stack = new Stack<double>();
            stack.Push(currentValue);

            // Dzielimy ciąg znaków po spacjach
            string[] tokens = rpnExpression.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string token in tokens)
            {
                // Jeśli token jest liczbą, wrzuć na stos
                if (double.TryParse(token.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double number))
                {
                    stack.Push(number);
                }
                else
                {
                    string op = token.ToUpper();

                    // Obsługa funkcji jednoargumentowych (wymagają 1 liczby na stosie)
                    if (IsUnaryOperation(op))
                    {
                        if (stack.Count < 1) throw new Exception($"Błąd RPN: Brak argumentu dla funkcji '{op}'.");
                        double a = stack.Pop();
                        stack.Push(PerformUnaryOperation(op, a));
                        continue;
                    }

                    // Obsługa klasycznych operatorów dwuargumentowych (wymagają 2 liczb na stosie)
                    if (stack.Count < 2) throw new Exception($"Błąd RPN: Brakuje argumentów dla operatora '{op}'.");

                    double right = stack.Pop();
                    double left = stack.Pop();

                    switch (op)
                    {
                        case "+": stack.Push(left + right); break;
                        case "-": stack.Push(left - right); break;
                        case "*": stack.Push(left * right); break;
                        case "/":
                            if (right == 0) throw new DivideByZeroException("Błąd RPN: Dzielenie przez zero!");
                            stack.Push(left / right);
                            break;
                        case "^": stack.Push(Math.Pow(left, right)); break;
                        default: throw new Exception($"Nieznany operator RPN: '{op}'");
                    }
                }
            }

            if (stack.Count != 1)
                throw new Exception("Błąd składni RPN: po wykonaniu działań na stosie pozostało za dużo wartości.");

            return stack.Pop();
        }

        private static bool IsUnaryOperation(string op)
        {
            return op == "SQRT" || op == "SIN" || op == "COS" || op == "TAN" || op == "ABS";
        }

        private static double PerformUnaryOperation(string op, double val)
        {
            switch (op)
            {
                case "SQRT": return Math.Sqrt(val);
                case "ABS": return Math.Abs(val);
                // Trygonometria (zakładamy, że inżynierowie podają stopnie, więc przeliczamy na radiany dla Math.Sin)
                case "SIN": return Math.Sin(val * Math.PI / 180.0);
                case "COS": return Math.Cos(val * Math.PI / 180.0);
                case "TAN": return Math.Tan(val * Math.PI / 180.0);
                default: throw new Exception("Nieznany operator.");
            }
        }
    }
}