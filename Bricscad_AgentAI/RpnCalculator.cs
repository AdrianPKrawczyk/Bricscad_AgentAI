using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Teigha.DatabaseServices;

namespace BricsCAD_Agent
{
    public static class RpnCalculator
    {
        public static string Evaluate(string expression, object initialValue = null, Entity context = null)
        {
            Stack<object> stack = new Stack<object>();

            if (initialValue != null) stack.Push(initialValue);

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
                        case "SWAP": { object b = stack.Pop(); object a = stack.Pop(); stack.Push(b); stack.Push(a); break; }
                        case "DUP": { stack.Push(stack.Peek()); break; }
                        case "DROP": { stack.Pop(); break; }
                        case "+":
                            {
                                object b = stack.Pop(); object a = stack.Pop();
                                if (IsNumber(a) && IsNumber(b)) stack.Push(GetNum(a) + GetNum(b));
                                else stack.Push(GetString(a) + GetString(b));
                                break;
                            }
                        case "-": { double b = GetNum(stack.Pop()); double a = GetNum(stack.Pop()); stack.Push(a - b); break; }
                        case "*": { double b = GetNum(stack.Pop()); double a = GetNum(stack.Pop()); stack.Push(a * b); break; }
                        case "/": { double b = GetNum(stack.Pop()); double a = GetNum(stack.Pop()); stack.Push(a / b); break; }
                        case "^": { double b = GetNum(stack.Pop()); double a = GetNum(stack.Pop()); stack.Push(Math.Pow(a, b)); break; }
                        case "SQRT": stack.Push(Math.Sqrt(GetNum(stack.Pop()))); break;
                        case "SIN": stack.Push(Math.Sin(GetNum(stack.Pop()) * Math.PI / 180.0)); break;
                        case "COS": stack.Push(Math.Cos(GetNum(stack.Pop()) * Math.PI / 180.0)); break;
                        case "ROUND": { int d = (int)GetNum(stack.Pop()); double v = GetNum(stack.Pop()); stack.Push(Math.Round(v, d)); break; }
                        case "==": case "=": { string b = GetString(stack.Pop()); string a = GetString(stack.Pop()); stack.Push(a.Equals(b, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0); break; }
                        case "!=": { string b = GetString(stack.Pop()); string a = GetString(stack.Pop()); stack.Push(a.Equals(b, StringComparison.OrdinalIgnoreCase) ? 0.0 : 1.0); break; }
                        case ">": { double b = GetNum(stack.Pop()); double a = GetNum(stack.Pop()); stack.Push(a > b ? 1.0 : 0.0); break; }
                        case "<": { double b = GetNum(stack.Pop()); double a = GetNum(stack.Pop()); stack.Push(a < b ? 1.0 : 0.0); break; }
                        case "IFTE": { object f = stack.Pop(); object tr = stack.Pop(); double c = GetNum(stack.Pop()); stack.Push(c != 0 ? tr : f); break; }
                        case "CONCAT": case "&": { string b = GetString(stack.Pop()); string a = GetString(stack.Pop()); stack.Push(a + b); break; }
                        case "UPPER": stack.Push(GetString(stack.Pop()).ToUpper()); break;
                        case "LOWER": stack.Push(GetString(stack.Pop()).ToLower()); break;
                        case "TRIM": stack.Push(GetString(stack.Pop()).Trim()); break;
                        case "LEN": stack.Push((double)GetString(stack.Pop()).Length); break;
                        case "REPLACE": { string n = GetString(stack.Pop()); string o = GetString(stack.Pop()); string tg = GetString(stack.Pop()); stack.Push(tg.Replace(o, n)); break; }
                        case "SUBSTR": { int l = (int)GetNum(stack.Pop()); int s = (int)GetNum(stack.Pop()); string tg = GetString(stack.Pop()); if (s < 0) s = 0; stack.Push(s >= tg.Length ? "" : tg.Substring(s, Math.Min(l, tg.Length - s))); break; }
                        case "FIND": { string s = GetString(stack.Pop()); string tg = GetString(stack.Pop()); stack.Push((double)tg.IndexOf(s)); break; }
                        case "ABS": stack.Push(Math.Abs(GetNum(stack.Pop()))); break;
                        case "SPLIT": { int idx = (int)GetNum(stack.Pop()); string sep = GetString(stack.Pop()); string tg = GetString(stack.Pop()); string[] p = tg.Split(new[] { sep }, StringSplitOptions.None); stack.Push(idx >= 0 && idx < p.Length ? p[idx] : ""); break; }
                        case "GET":
                            {
                                string pName = GetString(stack.Pop());
                                if (context != null)
                                {
                                    var pi = context.GetType().GetProperty(pName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                                    stack.Push(pi != null ? pi.GetValue(context) : 0);
                                }
                                else stack.Push(0);
                                break;
                            }
                        default: stack.Push(token); break;
                    }
                }
                catch (Exception ex) { throw new Exception($"Błąd RPN przy '{token}': {ex.Message}"); }
            }
            return stack.Count > 0 ? GetString(stack.Pop()) : "";
        }

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