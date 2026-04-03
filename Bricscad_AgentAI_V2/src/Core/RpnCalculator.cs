using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;
using Bricscad.EditorInput;
using Teigha.Geometry;
using Bricscad.ApplicationServices;
using System.Linq;

namespace Bricscad_AgentAI_V2.Core
{
    public struct UnitDim : IEquatable<UnitDim>
    {
        public int L, M, T, I, Theta, N, J;
        public static UnitDim operator +(UnitDim a, UnitDim b) =>
            new UnitDim { L = a.L + b.L, M = a.M + b.M, T = a.T + b.T, I = a.I + b.I, Theta = a.Theta + b.Theta, N = a.N + b.N, J = a.J + b.J };
        public static UnitDim operator -(UnitDim a, UnitDim b) =>
            new UnitDim { L = a.L - b.L, M = a.M - b.M, T = a.T - b.T, I = a.I - b.I, Theta = a.Theta - b.Theta, N = a.N - b.N, J = a.J - b.J };
        public static UnitDim operator *(UnitDim a, int mult) =>
            new UnitDim { L = a.L * mult, M = a.M * mult, T = a.T * mult, I = a.I * mult, Theta = a.Theta * mult, N = a.N * mult, J = a.J * mult };
        public bool Equals(UnitDim other) => L == other.L && M == other.M && T == other.T && I == other.I && Theta == other.Theta && N == other.N && J == other.J;
        public override bool Equals(object obj) => obj is UnitDim other && Equals(other);
        public override int GetHashCode() => (L, M, T, I, Theta, N, J).GetHashCode();
        public static bool operator ==(UnitDim a, UnitDim b) => a.Equals(b);
        public static bool operator !=(UnitDim a, UnitDim b) => !a.Equals(b);
        public bool IsDimensionless() => this == new UnitDim();
    }

    public struct PhysicalValue
    {
        public double Value;
        public UnitDim Dim;
        public string PrefUnit;
        public double Offset;
        public PhysicalValue(double v, UnitDim d, string pref = null, double off = 0)
        {
            Value = v; Dim = d; PrefUnit = pref; Offset = off;
        }
        public override string ToString()
        {
            if (Dim.IsDimensionless() && string.IsNullOrEmpty(PrefUnit)) return Math.Round(Value, 6).ToString(CultureInfo.InvariantCulture);
            return $"{Math.Round(Value, 6).ToString(CultureInfo.InvariantCulture)}_{PrefUnit ?? Dim.ToString()}";
        }
    }

    public static class UnitEngine
    {
        public static Dictionary<string, PhysicalValue> Units = new Dictionary<string, PhysicalValue>(System.StringComparer.OrdinalIgnoreCase);
        static UnitEngine()
        {
            Units["m"] = new PhysicalValue(1.0, new UnitDim { L = 1 });
            Units["mm"] = new PhysicalValue(1e-3, new UnitDim { L = 1 });
            Units["cm"] = new PhysicalValue(1e-2, new UnitDim { L = 1 });
            Units["kg"] = new PhysicalValue(1.0, new UnitDim { M = 1 });
            Units["s"] = new PhysicalValue(1.0, new UnitDim { T = 1 });
            Units["degC"] = new PhysicalValue(1.0, new UnitDim { Theta = 1 }, null, 273.15);
        }
        public static PhysicalValue ParseUnit(string expr)
        {
            if (Units.TryGetValue(expr, out var exactMatch)) return exactMatch;
            return new PhysicalValue(1.0, new UnitDim());
        }
    }

    public static class RpnCalculator
    {
        private static Dictionary<Document, List<object>> _stacks = new Dictionary<Document, List<object>>();
        
        private static List<object> GetStack(Document doc)
        {
            if (doc == null) return new List<object>();
            if (!_stacks.ContainsKey(doc)) _stacks[doc] = new List<object>();
            return _stacks[doc];
        }

        public static string Evaluate(string expression, Document doc = null)
        {
            var stack = GetStack(doc ?? Application.DocumentManager.MdiActiveDocument);
            string[] tokens = expression.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string token in tokens)
            {
                try
                {
                    if (token == "+")
                    {
                        var b = GetPhys(Pop(stack)); var a = GetPhys(Pop(stack));
                        stack.Add(new PhysicalValue(a.Value + b.Value, a.Dim, a.PrefUnit));
                    }
                    else if (token == "*")
                    {
                        var b = GetPhys(Pop(stack)); var a = GetPhys(Pop(stack));
                        stack.Add(new PhysicalValue(a.Value * b.Value, a.Dim, a.PrefUnit));
                    }
                    else if (token.StartsWith("@"))
                    {
                        string varName = token.Substring(1);
                        if (AgentMemoryState.Variables.TryGetValue(varName, out string val)) stack.Add(GetPhys(val));
                    }
                    else if (double.TryParse(token.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
                    {
                        stack.Add(new PhysicalValue(num, new UnitDim()));
                    }
                    else
                    {
                        stack.Add(token);
                    }
                }
                catch { }
            }
            return stack.Count > 0 ? stack.Last().ToString() : "";
        }

        private static object Pop(List<object> stack)
        {
            if (stack.Count == 0) return 0.0;
            var item = stack.Last();
            stack.RemoveAt(stack.Count - 1);
            return item;
        }

        private static PhysicalValue GetPhys(object obj)
        {
            if (obj is PhysicalValue pv) return pv;
            if (obj is string s && double.TryParse(s.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double n)) return new PhysicalValue(n, new UnitDim());
            return new PhysicalValue(0, new UnitDim());
        }
    }
}
