using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Teigha.DatabaseServices;

namespace BricsCAD_Agent
{
    public static class RpnCalculator
    {
        // ==============================================================
        // ETAP 1: TRWAŁA PAMIĘĆ STOSU I USTAWIENIA
        // ==============================================================
        private static List<object> _stack = new List<object>();
        private const int MaxStackSize = 50; // Zabezpieczenie (Punkt 5)

        public static bool AutoPreview { get; set; } = true; // Tryb podglądu (Punkt 6)

        // API DLA AGENTA AI (Punkt 7)
        public static string GetStackState()
        {
            if (_stack.Count == 0) return "Stos jest pusty.";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < _stack.Count; i++)
            {
                // Wyświetla od góry (1:) do dołu
                sb.AppendLine($"  {_stack.Count - i}: {GetString(_stack[_stack.Count - 1 - i])}");
            }
            return sb.ToString();
        }

        // ==============================================================
        // MECHANIKA STOSU
        // ==============================================================
        public static void ClearStack() => _stack.Clear();

        private static void Push(object item)
        {
            _stack.Add(item);
            if (_stack.Count > MaxStackSize)
            {
                _stack.RemoveAt(0); // Wypychanie najstarszych danych z dna stosu
            }
        }

        private static object Pop()
        {
            if (_stack.Count == 0) throw new Exception("Stos jest pusty!");
            object item = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            return item;
        }

        private static object Peek()
        {
            if (_stack.Count == 0) throw new Exception("Stos jest pusty!");
            return _stack[_stack.Count - 1];
        }

        // Pobiera wartość ze szczytu stosu bez jej usuwania
        public static string GetTopAsString()
        {
            if (_stack.Count > 0) return GetString(_stack[_stack.Count - 1]);
            return "";
        }

        // Kompaktowy widok stosu do wyświetlania w pętli (pokazuje max 5 ostatnich elementów w jednej linii)
        public static string GetHPStackView(int maxLevels = 5)
        {
            if (_stack.Count == 0) return " 1: [Pusty]"; // Pusty stos wciąż ma poziom 1

            StringBuilder sb = new StringBuilder();

            // Ograniczamy wyświetlanie, by nie zalać paska poleceń, np. do 5-6 poziomów
            int levelsToShow = Math.Min(_stack.Count, maxLevels);

            // Pętla iteruje "od góry ekranu" (najwyższy numer) w dół do poziomu 1:
            for (int i = levelsToShow; i >= 1; i--)
            {
                int stackIndex = _stack.Count - i;
                sb.AppendLine($" {i}: {GetString(_stack[stackIndex])}");
            }

            return sb.ToString().TrimEnd(); // Zwracamy piękny pionowy stos bez ostatniego entera
        }

        // ==============================================================
        // GŁÓWNY SILNIK OBLICZENIOWY
        // ==============================================================
        public static string Evaluate(string expression, object initialValue = null, Entity context = null)
        {
            if (initialValue != null) Push(initialValue);

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
                        // Nowe komendy stosu (Punkt 1 i 5)
                        case "CLEAR": ClearStack(); break;
                        case "SWAP": { object b = Pop(); object a = Pop(); Push(b); Push(a); break; }
                        case "DUP": { Push(Peek()); break; }
                        case "DROP": { Pop(); break; }
                        case "PICK":
                            {
                                int index = (int)GetNum(Pop());
                                if (index < 1 || index > _stack.Count) throw new Exception($"Brak elementu na pozycji {index}");
                                // 1 PICK kopiuje sam szczyt, 2 PICK kopiuje element pod nim itd.
                                Push(_stack[_stack.Count - index]);
                                break;
                            }

                        // Matematyka i Logika
                        case "+":
                            {
                                object b = Pop(); object a = Pop();
                                if (IsNumber(a) && IsNumber(b)) Push(GetNum(a) + GetNum(b));
                                else Push(GetString(a) + GetString(b));
                                break;
                            }
                        case "-": { double b = GetNum(Pop()); double a = GetNum(Pop()); Push(a - b); break; }
                        case "*": { double b = GetNum(Pop()); double a = GetNum(Pop()); Push(a * b); break; }
                        case "/": { double b = GetNum(Pop()); double a = GetNum(Pop()); Push(a / b); break; }
                        case "^": { double b = GetNum(Pop()); double a = GetNum(Pop()); Push(Math.Pow(a, b)); break; }
                        case "SQRT": Push(Math.Sqrt(GetNum(Pop()))); break;
                        case "SIN": Push(Math.Sin(GetNum(Pop()) * Math.PI / 180.0)); break;
                        case "COS": Push(Math.Cos(GetNum(Pop()) * Math.PI / 180.0)); break;
                        case "ROUND": { int d = (int)GetNum(Pop()); double v = GetNum(Pop()); Push(Math.Round(v, d)); break; }
                        case "==": case "=": { string b = GetString(Pop()); string a = GetString(Pop()); Push(a.Equals(b, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0); break; }
                        case "!=": { string b = GetString(Pop()); string a = GetString(Pop()); Push(a.Equals(b, StringComparison.OrdinalIgnoreCase) ? 0.0 : 1.0); break; }
                        case ">": { double b = GetNum(Pop()); double a = GetNum(Pop()); Push(a > b ? 1.0 : 0.0); break; }
                        case "<": { double b = GetNum(Pop()); double a = GetNum(Pop()); Push(a < b ? 1.0 : 0.0); break; }
                        case "IFTE": { object f = Pop(); object tr = Pop(); double c = GetNum(Pop()); Push(c != 0 ? tr : f); break; }

                        // Operacje na Tekstach
                        case "CONCAT": case "&": { string b = GetString(Pop()); string a = GetString(Pop()); Push(a + b); break; }
                        case "UPPER": Push(GetString(Pop()).ToUpper()); break;
                        case "LOWER": Push(GetString(Pop()).ToLower()); break;
                        case "TRIM": Push(GetString(Pop()).Trim()); break;
                        case "LEN": Push((double)GetString(Pop()).Length); break;
                        case "REPLACE": { string n = GetString(Pop()); string o = GetString(Pop()); string tg = GetString(Pop()); Push(tg.Replace(o, n)); break; }
                        case "SUBSTR": { int l = (int)GetNum(Pop()); int s = (int)GetNum(Pop()); string tg = GetString(Pop()); if (s < 0) s = 0; Push(s >= tg.Length ? "" : tg.Substring(s, Math.Min(l, tg.Length - s))); break; }
                        case "FIND": { string s = GetString(Pop()); string tg = GetString(Pop()); Push((double)tg.IndexOf(s)); break; }
                        case "ABS": Push(Math.Abs(GetNum(Pop()))); break;
                        case "SPLIT": { int idx = (int)GetNum(Pop()); string sep = GetString(Pop()); string tg = GetString(Pop()); string[] p = tg.Split(new[] { sep }, StringSplitOptions.None); Push(idx >= 0 && idx < p.Length ? p[idx] : ""); break; }

                        // Ekstrakcja właściwości CAD
                        case "GET":
                            {
                                string pName = GetString(Pop());
                                if (context != null)
                                {
                                    var pi = context.GetType().GetProperty(pName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                                    Push(pi != null ? pi.GetValue(context) : 0);
                                }
                                else Push(0);
                                break;
                            }
                        default: Push(token); break;
                    }
                }
                catch (Exception ex) { throw new Exception($"Błąd RPN przy '{token}': {ex.Message}"); }
            }

            // UWAGA: Zamiast Pop(), używamy Peek(). Wynik ZOSTANIE na stosie do użycia w kolejnych komendach!
            return _stack.Count > 0 ? GetString(Peek()) : "";
        }

        // ==============================================================
        // NARZĘDZIA POMOCNICZE
        // ==============================================================
        private static bool IsNumber(object o) => double.TryParse(GetString(o).Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out _);
        private static double GetNum(object o) => double.TryParse(GetString(o).Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double r) ? r : 0;
        private static string GetString(object o) => o?.ToString() ?? "";

        private static List<string> Tokenize(string expr)
        {
            List<string> ts = new List<string>(); StringBuilder sb = new StringBuilder(); bool q = false;
            foreach (char c in expr)
            {
                if (c == '\'' || c == '\"') q = !q;
                else if (char.IsWhiteSpace(c) && !q) { if (sb.Length > 0) { ts.Add(sb.ToString()); sb.Clear(); } }
                else sb.Append(c);
            }
            if (sb.Length > 0) ts.Add(sb.ToString()); return ts;
        }
    }
}