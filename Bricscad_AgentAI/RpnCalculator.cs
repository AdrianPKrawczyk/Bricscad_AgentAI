using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BricsCAD_Agent
{
    public static class RpnCalculator
    {
        public static string Evaluate(string expression, object initialValue = null)
        {
            Stack<object> stack = new Stack<object>();

            // Wrzucamy początkową wartość obiektu na dno stosu
            if (initialValue != null)
            {
                stack.Push(initialValue);
            }

            // Rozbijamy wyrażenie z uwzględnieniem cudzysłowów dla tekstów
            List<string> tokens = Tokenize(expression);

            foreach (string t in tokens)
            {
                string token = t.Trim();
                if (string.IsNullOrEmpty(token)) continue;

                string upperToken = token.ToUpperInvariant();

                try
                {
                    switch (upperToken)
                    {
                        // ==========================================
                        // OPERACJE NA STOSIE (Styl G50 / HP)
                        // ==========================================
                        case "SWAP":
                            {
                                object b = stack.Pop();
                                object a = stack.Pop();
                                stack.Push(b);
                                stack.Push(a);
                                break;
                            }
                        case "DUP":
                            {
                                object a = stack.Peek();
                                stack.Push(a);
                                break;
                            }
                        case "DROP":
                            {
                                stack.Pop();
                                break;
                            }

                        // ==========================================
                        // OPERACJE MATEMATYCZNE
                        // ==========================================
                        case "+":
                            {
                                object b = stack.Pop();
                                object a = stack.Pop();
                                // Jeśli oba są liczbami, dodaj. W przeciwnym razie - KONKATENACJA!
                                if (IsNumber(a) && IsNumber(b)) stack.Push(GetNum(a) + GetNum(b));
                                else stack.Push(GetString(a) + GetString(b));
                                break;
                            }
                        case "-":
                            {
                                double b = GetNum(stack.Pop());
                                double a = GetNum(stack.Pop());
                                stack.Push(a - b);
                                break;
                            }
                        case "*":
                            {
                                double b = GetNum(stack.Pop());
                                double a = GetNum(stack.Pop());
                                stack.Push(a * b);
                                break;
                            }
                        case "/":
                            {
                                double b = GetNum(stack.Pop());
                                double a = GetNum(stack.Pop());
                                stack.Push(a / b);
                                break;
                            }
                        case "^":
                            {
                                double b = GetNum(stack.Pop());
                                double a = GetNum(stack.Pop());
                                stack.Push(Math.Pow(a, b));
                                break;
                            }
                        case "SQRT": stack.Push(Math.Sqrt(GetNum(stack.Pop()))); break;
                        case "SIN": stack.Push(Math.Sin(GetNum(stack.Pop()) * Math.PI / 180.0)); break;
                        case "COS": stack.Push(Math.Cos(GetNum(stack.Pop()) * Math.PI / 180.0)); break;
                        case "ROUND":
                            {
                                int decimals = (int)GetNum(stack.Pop());
                                double val = GetNum(stack.Pop());
                                stack.Push(Math.Round(val, decimals));
                                break;
                            }

                        // ==========================================
                        // ZAAWANSOWANE OPERACJE TEKSTOWE
                        // ==========================================
                        case "CONCAT":
                        case "&":
                            {
                                string b = GetString(stack.Pop());
                                string a = GetString(stack.Pop());
                                stack.Push(a + b);
                                break;
                            }
                        case "UPPER": stack.Push(GetString(stack.Pop()).ToUpper()); break;
                        case "LOWER": stack.Push(GetString(stack.Pop()).ToLower()); break;
                        case "TRIM": stack.Push(GetString(stack.Pop()).Trim()); break;
                        case "LEN": stack.Push((double)GetString(stack.Pop()).Length); break;

                        case "REPLACE": // [Cel, Stary, Nowy] -> REPLACE
                            {
                                string newStr = GetString(stack.Pop());
                                string oldStr = GetString(stack.Pop());
                                string target = GetString(stack.Pop());
                                stack.Push(target.Replace(oldStr, newStr));
                                break;
                            }
                        case "SUBSTR": // [Cel, Start(0-based), Długość] -> SUBSTR
                            {
                                int len = (int)GetNum(stack.Pop());
                                int start = (int)GetNum(stack.Pop());
                                string target = GetString(stack.Pop());
                                if (start < 0) start = 0;
                                if (start >= target.Length) stack.Push("");
                                else
                                {
                                    if (start + len > target.Length) len = target.Length - start;
                                    stack.Push(target.Substring(start, len));
                                }
                                break;
                            }
                        case "FIND": // [Cel, Szukany] -> FIND (Zwraca indeks lub -1)
                            {
                                string search = GetString(stack.Pop());
                                string target = GetString(stack.Pop());
                                stack.Push((double)target.IndexOf(search));
                                break;
                            }
                        case "SPLIT": // [Cel, Separator, Indeks] -> SPLIT (np. dla "A_B" "_" 1 SPLIT -> "B")
                            {
                                int index = (int)GetNum(stack.Pop());
                                string sep = GetString(stack.Pop());
                                string target = GetString(stack.Pop());
                                string[] parts = target.Split(new string[] { sep }, StringSplitOptions.None);
                                if (index >= 0 && index < parts.Length) stack.Push(parts[index]);
                                else stack.Push("");
                                break;
                            }
                        default:
                            // Jeśli to ani liczba matematyczna, ani operator to po prostu ładujemy na stos.
                            // Tokenizer usunął już z niego ewentualne cudzysłowy ochronne.
                            stack.Push(token);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Błąd RPN przy tokenie '{token}': {ex.Message}");
                }
            }

            if (stack.Count > 0)
            {
                return GetString(stack.Pop());
            }
            return "";
        }

        // --- POMOCNICZE PARSERY ---

        private static bool IsNumber(object obj)
        {
            if (obj is double || obj is int || obj is float || obj is decimal) return true;
            if (obj is string s) return double.TryParse(s.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out _);
            return false;
        }

        private static double GetNum(object obj)
        {
            if (obj is double d) return d;
            if (obj is int i) return i;
            if (obj is string s)
            {
                if (double.TryParse(s.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double res)) return res;
                return 0;
            }
            return 0;
        }

        private static string GetString(object obj)
        {
            if (obj == null) return "";
            if (obj is double d) return d.ToString(CultureInfo.InvariantCulture); // Zabezpieczenie kropek
            return obj.ToString();
        }

        // --- INTELIGENTNY TOKENIZER (Zrozumienie cudzysłowów) ---
        private static List<string> Tokenize(string expression)
        {
            List<string> tokens = new List<string>();
            bool inQuotes = false;
            char quoteChar = '\0';
            StringBuilder current = new StringBuilder();

            for (int i = 0; i < expression.Length; i++)
            {
                char c = expression[i];

                if ((c == '"' || c == '\'') && (!inQuotes || quoteChar == c))
                {
                    if (inQuotes)
                    {
                        // Zamykamy tekst (zapisujemy bez cudzysłowów ochronnych!)
                        tokens.Add(current.ToString());
                        current.Clear();
                        inQuotes = false;
                        quoteChar = '\0';
                    }
                    else
                    {
                        // Otwieramy nowy tekst
                        if (current.Length > 0 && current.ToString().Trim().Length > 0)
                        {
                            tokens.Add(current.ToString().Trim());
                            current.Clear();
                        }
                        inQuotes = true;
                        quoteChar = c;
                    }
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                tokens.Add(current.ToString().Trim());
            }

            return tokens;
        }
    }
}