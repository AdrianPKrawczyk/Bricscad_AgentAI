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
    // ==============================================================
    // SILNIK ANALIZY WYMIAROWEJ (STYL HP-50g)
    // ==============================================================
    public struct UnitDim : IEquatable<UnitDim>
    {
        public int L, M, T, I, Theta, N, J; // Długość(m), Masa(kg), Czas(s), Prąd(A), Temp(K), Mol(mol), Światło(cd)

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

        public override string ToString()
        {
            if (IsDimensionless()) return "";
            string explicitName = UnitEngine.GetUnitName(this);
            if (explicitName != null) return explicitName;

            // Generowanie awaryjne, np. m^2*kg/s^3
            List<string> pos = new List<string>(), neg = new List<string>();
            Action<int, string> add = (val, sym) => {
                if (val > 0) pos.Add(val == 1 ? sym : $"{sym}^{val}");
                else if (val < 0) neg.Add(val == -1 ? sym : $"{sym}^{-val}");
            };
            add(L, "m"); add(M, "kg"); add(T, "s"); add(I, "A"); add(Theta, "K"); add(N, "mol"); add(J, "cd");

            string num = pos.Count > 0 ? string.Join("*", pos) : "1";
            string den = neg.Count > 0 ? "/" + string.Join("*", neg) : "";
            return num + den;
        }
    }

    public struct PhysicalValue
    {
        public double Value;
        public UnitDim Dim;
        public string PrefUnit;
        public double Offset; // NOWOŚĆ! Obsługa temperatury absolutnej

        public PhysicalValue(double v, UnitDim d, string pref = null, double off = 0)
        {
            Value = v; Dim = d; PrefUnit = pref; Offset = off;
        }

        public override string ToString()
        {
            if (Dim.IsDimensionless() && string.IsNullOrEmpty(PrefUnit)) return Math.Round(Value, 6).ToString(CultureInfo.InvariantCulture);

            if (!string.IsNullOrEmpty(PrefUnit))
            {
                try
                {
                    var pUnit = UnitEngine.ParseUnit(PrefUnit);
                    if (this.Dim == pUnit.Dim || (Dim.IsDimensionless() && pUnit.Dim.IsDimensionless()))
                    {
                        // Wyświetlając, najpierw skalujemy, a potem zdejmujemy offset!
                        double displayVal = (this.Value / pUnit.Value) - pUnit.Offset;
                        return $"{Math.Round(displayVal, 6).ToString(CultureInfo.InvariantCulture)}_{PrefUnit}";
                    }
                }
                catch { }
            }
            return $"{Math.Round(Value, 6).ToString(CultureInfo.InvariantCulture)}_{Dim.ToString()}";
        }
    }

    public static class UnitEngine
    {
        public static Dictionary<string, PhysicalValue> Units = new Dictionary<string, PhysicalValue>(StringComparer.OrdinalIgnoreCase);

        static UnitEngine()
        {
            // BAZOWE SI
            Units["m"] = new PhysicalValue(1.0, new UnitDim { L = 1 });
            Units["kg"] = new PhysicalValue(1.0, new UnitDim { M = 1 });
            Units["s"] = new PhysicalValue(1.0, new UnitDim { T = 1 });
            Units["A"] = new PhysicalValue(1.0, new UnitDim { I = 1 });
            Units["K"] = new PhysicalValue(1.0, new UnitDim { Theta = 1 });
            Units["mol"] = new PhysicalValue(1.0, new UnitDim { N = 1 });

            // DŁUGOŚĆ / POLE / OBJĘTOŚĆ (Rozszerzone)
            Units["mm"] = new PhysicalValue(1e-3, new UnitDim { L = 1 });
            Units["cm"] = new PhysicalValue(1e-2, new UnitDim { L = 1 });
            Units["km"] = new PhysicalValue(1e3, new UnitDim { L = 1 });
            Units["in"] = new PhysicalValue(0.0254, new UnitDim { L = 1 });
            Units["mm2"] = new PhysicalValue(1e-6, new UnitDim { L = 2 });
            Units["cm2"] = new PhysicalValue(1e-4, new UnitDim { L = 2 });
            Units["m2"] = new PhysicalValue(1.0, new UnitDim { L = 2 });
            Units["ha"] = new PhysicalValue(10000.0, new UnitDim { L = 2 });
            Units["m3"] = new PhysicalValue(1.0, new UnitDim { L = 3 });
            Units["L"] = new PhysicalValue(1e-3, new UnitDim { L = 3 });
            Units["dm3"] = new PhysicalValue(1e-3, new UnitDim { L = 3 });
            Units["cm3"] = new PhysicalValue(1e-6, new UnitDim { L = 3 });
            Units["ml"] = new PhysicalValue(1e-6, new UnitDim { L = 3 });

            // MASA I CZAS (Rozszerzone)
            Units["g"] = new PhysicalValue(1e-3, new UnitDim { M = 1 });
            Units["t"] = new PhysicalValue(1000.0, new UnitDim { M = 1 });
            Units["min"] = new PhysicalValue(60.0, new UnitDim { T = 1 });
            Units["h"] = new PhysicalValue(3600.0, new UnitDim { T = 1 });
            Units["dzień"] = new PhysicalValue(86400.0, new UnitDim { T = 1 });
            Units["rok"] = new PhysicalValue(31557600.0, new UnitDim { T = 1 });

            // MECHANIKA I CIŚNIENIE (Rozszerzone)
            Units["N"] = new PhysicalValue(1.0, new UnitDim { M = 1, L = 1, T = -2 });
            Units["kN"] = new PhysicalValue(1e3, new UnitDim { M = 1, L = 1, T = -2 });
            Units["Pa"] = new PhysicalValue(1.0, new UnitDim { M = 1, L = -1, T = -2 });
            Units["kPa"] = new PhysicalValue(1e3, new UnitDim { M = 1, L = -1, T = -2 });
            Units["MPa"] = new PhysicalValue(1e6, new UnitDim { M = 1, L = -1, T = -2 });
            Units["bar"] = new PhysicalValue(1e5, new UnitDim { M = 1, L = -1, T = -2 });
            Units["mbar"] = new PhysicalValue(100.0, new UnitDim { M = 1, L = -1, T = -2 });
            Units["mWody"] = new PhysicalValue(9806.65, new UnitDim { M = 1, L = -1, T = -2 });

            // ENERGIA I PRACA (Rozszerzone)
            Units["J"] = new PhysicalValue(1.0, new UnitDim { M = 1, L = 2, T = -2 });
            Units["kJ"] = new PhysicalValue(1e3, new UnitDim { M = 1, L = 2, T = -2 });
            Units["MJ"] = new PhysicalValue(1e6, new UnitDim { M = 1, L = 2, T = -2 });
            Units["GJ"] = new PhysicalValue(1e9, new UnitDim { M = 1, L = 2, T = -2 });
            Units["TJ"] = new PhysicalValue(1e12, new UnitDim { M = 1, L = 2, T = -2 });
            Units["W"] = new PhysicalValue(1.0, new UnitDim { M = 1, L = 2, T = -3 });
            Units["kW"] = new PhysicalValue(1e3, new UnitDim { M = 1, L = 2, T = -3 });
            Units["Wh"] = new PhysicalValue(3600.0, new UnitDim { M = 1, L = 2, T = -2 });
            Units["kWh"] = new PhysicalValue(3.6e6, new UnitDim { M = 1, L = 2, T = -2 });
            Units["MWh"] = new PhysicalValue(3.6e9, new UnitDim { M = 1, L = 2, T = -2 });
            Units["GWh"] = new PhysicalValue(3.6e12, new UnitDim { M = 1, L = 2, T = -2 });
            Units["TWh"] = new PhysicalValue(3.6e15, new UnitDim { M = 1, L = 2, T = -2 });

            // PŁYNY, TERMODYNAMIKA, INNE (Rozszerzone)
            Units["Hz"] = new PhysicalValue(1.0, new UnitDim { T = -1 });
            Units["1/h"] = new PhysicalValue(1.0 / 3600.0, new UnitDim { T = -1 });
            Units["kg/m3"] = new PhysicalValue(1.0, new UnitDim { M = 1, L = -3 });
            Units["m/s"] = new PhysicalValue(1.0, new UnitDim { L = 1, T = -1 });
            Units["m/s2"] = new PhysicalValue(1.0, new UnitDim { L = 1, T = -2 });
            Units["J/kgK"] = new PhysicalValue(1.0, new UnitDim { L = 2, T = -2, Theta = -1 });
            Units["kJ/kg"] = new PhysicalValue(1000.0, new UnitDim { L = 2, T = -2 });
            Units["kJ/kgK"] = new PhysicalValue(1000.0, new UnitDim { L = 2, T = -2, Theta = -1 });
            Units["cSt"] = new PhysicalValue(1e-6, new UnitDim { L = 2, T = -1 });
            Units["St"] = new PhysicalValue(1e-4, new UnitDim { L = 2, T = -1 });
            Units["cP"] = new PhysicalValue(1e-3, new UnitDim { M = 1, L = -1, T = -1 });
            Units["g/kg"] = new PhysicalValue(1e-3, new UnitDim());
            Units["%"] = new PhysicalValue(0.01, new UnitDim());

            // PRZEPŁYWY (Objętościowe i Masowe)
            Units["m3/h"] = new PhysicalValue(1.0 / 3600.0, new UnitDim { L = 3, T = -1 });
            Units["kg/h"] = new PhysicalValue(1.0 / 3600.0, new UnitDim { M = 1, T = -1 });
            Units["dm3/s"] = new PhysicalValue(1e-3, new UnitDim { L = 3, T = -1 });
            Units["g/h"] = new PhysicalValue(1e-3 / 3600.0, new UnitDim { M = 1, T = -1 });
            Units["dm3/min"] = new PhysicalValue(1e-3 / 60.0, new UnitDim { L = 3, T = -1 });
            Units["l/s"] = new PhysicalValue(1e-3, new UnitDim { L = 3, T = -1 });
            Units["g/s"] = new PhysicalValue(1e-3, new UnitDim { M = 1, T = -1 });
            Units["m3/s"] = new PhysicalValue(1.0, new UnitDim { L = 3, T = -1 });
            Units["m3/dzień"] = new PhysicalValue(1.0 / 86400.0, new UnitDim { L = 3, T = -1 });
            Units["m3/rok"] = new PhysicalValue(1.0 / 31557600.0, new UnitDim { L = 3, T = -1 });
            Units["kg/s"] = new PhysicalValue(1.0, new UnitDim { M = 1, T = -1 });

            // ELEKTRYKA
            Units["V"] = new PhysicalValue(1.0, new UnitDim { M = 1, L = 2, T = -3, I = -1 });
            Units[" kV"] = new PhysicalValue(1e3, new UnitDim { M = 1, L = 2, T = -3, I = -1 });
            Units["mV"] = new PhysicalValue(1e-3, new UnitDim { M = 1, L = 2, T = -3, I = -1 });
            Units["A"] = new PhysicalValue(1.0, new UnitDim { I = 1 });
            Units["mA"] = new PhysicalValue(1e-3, new UnitDim { I = 1 });
            Units["F"] = new PhysicalValue(1.0, new UnitDim { M = -1, L = -2, T = 4, I = 2 });
            Units["pF"] = new PhysicalValue(1e-12, new UnitDim { M = -1, L = -2, T = 4, I = 2 });
            Units["uF"] = new PhysicalValue(1e-6, new UnitDim { M = -1, L = -2, T = 4, I = 2 });
            Units["mF"] = new PhysicalValue(1e-3, new UnitDim { M = -1, L = -2, T = 4, I = 2 });
            Units["Ohm"] = new PhysicalValue(1.0, new UnitDim { M = 1, L = 2, T = -3, I = -2 });
            Units["kOhm"] = new PhysicalValue(1e3, new UnitDim { M = 1, L = 2, T = -3, I = -2 });
            Units["MOhm"] = new PhysicalValue(1e6, new UnitDim { M = 1, L = 2, T = -3, I = -2 });
            Units["H"] = new PhysicalValue(1.0, new UnitDim { M = 1, L = 2, T = -2, I = -2 });
            Units["mH"] = new PhysicalValue(1e-3, new UnitDim { M = 1, L = 2, T = -2, I = -2 });

            // TEMPERATURA Z OFFSETEM
            Units["degC"] = new PhysicalValue(1.0, new UnitDim { Theta = 1 }, null, 273.15);
        }
        // --- NOWOŚĆ: ALGEBRAICZNY PARSER JEDNOSTEK ---
        public static PhysicalValue ParseUnit(string expr)
        {
            if (Units.TryGetValue(expr, out var exactMatch)) return exactMatch;

            // Zaktualizowany Regex - łapie polskie znaki i jednostki % jako całość
            var tokens = Regex.Matches(expr, @"[a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ%][a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ0-9%]*|-?\d+|[/*^()]")
                              .Cast<Match>().Select(m => m.Value).ToList();

            var output = new List<string>();
            var ops = new Stack<string>();
            int Precedence(string op) => op == "^" ? 3 : (op == "*" || op == "/" ? 2 : 0);

            foreach (var t in tokens)
            {
                if (char.IsLetter(t[0]) || char.IsDigit(t[0]) || t == "%" || (t.Length > 1 && t[0] == '-')) output.Add(t);
                else if (t == "(") ops.Push(t);
                else if (t == ")")
                {
                    while (ops.Count > 0 && ops.Peek() != "(") output.Add(ops.Pop());
                    if (ops.Count > 0) ops.Pop();
                }
                else
                {
                    while (ops.Count > 0 && Precedence(ops.Peek()) >= Precedence(t)) output.Add(ops.Pop());
                    ops.Push(t);
                }
            }
            while (ops.Count > 0) output.Add(ops.Pop());

            var stack = new Stack<PhysicalValue>();
            foreach (var t in output)
            {
                // Mnożenie, dzielenie i potęgowanie ZAWSZE ZERUJE OFFSET! (degC użyte w ułamkach traci Celsjusza i staje się wirtualnym Kelvinem)
                if (t == "*") { if (stack.Count < 2) continue; var b = stack.Pop(); var a = stack.Pop(); stack.Push(new PhysicalValue(a.Value * b.Value, a.Dim + b.Dim, null, 0)); }
                else if (t == "/") { if (stack.Count < 2) continue; var b = stack.Pop(); var a = stack.Pop(); stack.Push(new PhysicalValue(a.Value / b.Value, a.Dim - b.Dim, null, 0)); }
                else if (t == "^") { if (stack.Count < 2) continue; var b = stack.Pop(); var a = stack.Pop(); stack.Push(new PhysicalValue(Math.Pow(a.Value, b.Value), a.Dim * (int)Math.Round(b.Value), null, 0)); }
                else if (char.IsDigit(t[0]) || t.StartsWith("-")) stack.Push(new PhysicalValue(double.Parse(t, CultureInfo.InvariantCulture), new UnitDim(), null, 0));
                else
                {
                    if (Units.TryGetValue(t, out var u)) stack.Push(u);
                    else throw new Exception(t);
                }
            }
            if (stack.Count != 1) return new PhysicalValue(1.0, new UnitDim());
            return stack.Pop();
        }


        public static string GetUnitName(UnitDim dim)
        {
            if (dim == new UnitDim { L = 1 }) return "m";
            if (dim == new UnitDim { L = 2 }) return "m2";
            if (dim == new UnitDim { L = 3 }) return "m3";
            if (dim == new UnitDim { M = 1, L = 1, T = -2 }) return "N";
            if (dim == new UnitDim { M = 1, L = -1, T = -2 }) return "Pa";
            if (dim == new UnitDim { M = 1, L = 2, T = -2 }) return "J";
            if (dim == new UnitDim { M = 1, L = 2, T = -3 }) return "W";
            if (dim == new UnitDim { T = -1 }) return "Hz";
            if (dim == new UnitDim { L = 1, T = -1 }) return "m/s";
            if (dim == new UnitDim { L = 1, T = -2 }) return "m/s2";
            if (dim == new UnitDim { M = 1, L = -3 }) return "kg/m3";
            return null;
        }
    }

    public static class RpnCalculator
    {
        private static Dictionary<Document, List<object>> _stacks = new Dictionary<Document, List<object>>();

        // --- NOWOŚĆ: Pamięć zmiennych użytkownika ---
        private static Dictionary<Document, Dictionary<string, PhysicalValue>> _variables = new Dictionary<Document, Dictionary<string, PhysicalValue>>();

        private static Dictionary<string, PhysicalValue> CurrentVariables
        {
            get
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return new Dictionary<string, PhysicalValue>();
                if (!_variables.ContainsKey(doc)) _variables[doc] = new Dictionary<string, PhysicalValue>();
                return _variables[doc];
            }
        }

        private static HashSet<Document> _loadedDocs = new HashSet<Document>();
        private const int MaxStackSize = 50;
        public static bool AutoPreview { get; set; } = true;

        private static List<object> CurrentStack
        {
            get
            {
                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null) return new List<object>();
                if (!_stacks.ContainsKey(doc)) _stacks[doc] = new List<object>();
                return _stacks[doc];
            }
        }

        public static List<string> GetStack()
        {
            return CurrentStack.Select(o => GetString(o)).ToList();
        }

        // ==============================================================
        // ZAPIS/ODCZYT DWG (V2 Standard)
        // ==============================================================
        public static void LoadStackFromDwg(Database db)
        {
            if (db == null) return;
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DBDictionary nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                    if (nod.Contains("BIELIK_RPN_STACK"))
                    {
                        Xrecord xRec = (Xrecord)tr.GetObject(nod.GetAt("BIELIK_RPN_STACK"), OpenMode.ForRead);
                        if (xRec.Data != null)
                        {
                            var stack = CurrentStack;
                            stack.Clear();
                            foreach (TypedValue tv in xRec.Data)
                            {
                                if (tv.Value.ToString() != "EMPTY_STACK") stack.Add(tv.Value.ToString());
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            catch { }
        }

        public static void SaveStackToDwg(Database db)
        {
            if (db == null) return;
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DBDictionary nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
                    Xrecord xRec = new Xrecord();
                    ResultBuffer rb = new ResultBuffer();
                    var stack = CurrentStack;
                    if (stack.Count == 0) rb.Add(new TypedValue((int)DxfCode.Text, "EMPTY_STACK"));
                    else foreach (var item in stack) rb.Add(new TypedValue((int)DxfCode.XTextString, GetString(item)));
                    xRec.Data = rb;

                    if (nod.Contains("BIELIK_RPN_STACK"))
                    {
                        ObjectId oldId = nod.GetAt("BIELIK_RPN_STACK");
                        DBObject oldObj = tr.GetObject(oldId, OpenMode.ForWrite);
                        oldObj.Erase();
                    }
                    nod.SetAt("BIELIK_RPN_STACK", xRec);
                    tr.AddNewlyCreatedDBObject(xRec, true);
                    tr.Commit();
                }
            }
            catch { }
        }

        // ==============================================================
        // ZAPIS/ODCZYT DWG (Legacy/Compat)
        // ==============================================================
        public static void LoadFromDWG(Document doc)
        {
            if (doc == null || _loadedDocs.Contains(doc)) return;
            LoadStackFromDwg(doc.Database);
            _loadedDocs.Add(doc);
        }

        public static void SaveToDWG(Document doc)
        {
            if (doc == null || doc.IsReadOnly) return;
            try
            {
                using (DocumentLock loc = doc.LockDocument())
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    DBDictionary nod = (DBDictionary)tr.GetObject(doc.Database.NamedObjectsDictionaryId, OpenMode.ForWrite);
                    DBDictionary dict;
                    if (nod.Contains("BIELIK_RPN_MEMORY")) dict = (DBDictionary)tr.GetObject(nod.GetAt("BIELIK_RPN_MEMORY"), OpenMode.ForWrite);
                    else { dict = new DBDictionary(); nod.SetAt("BIELIK_RPN_MEMORY", dict); tr.AddNewlyCreatedDBObject(dict, true); }

                    Xrecord xRec = new Xrecord();
                    ResultBuffer rb = new ResultBuffer();
                    var stack = CurrentStack;
                    if (stack.Count == 0) rb.Add(new TypedValue((int)DxfCode.Text, "EMPTY_STACK"));
                    else foreach (var item in stack) rb.Add(new TypedValue((int)DxfCode.XTextString, GetString(item)));
                    xRec.Data = rb;

                    if (dict.Contains("STACK")) { Object oldObj = tr.GetObject(dict.GetAt("STACK"), OpenMode.ForWrite); ((DBObject)oldObj).Erase(); }
                    dict.SetAt("STACK", xRec); tr.AddNewlyCreatedDBObject(xRec, true); tr.Commit();
                }
            }
            catch { }
        }

        // ==============================================================
        // MECHANIKA I PODGLĄD STOSU
        // ==============================================================
        public static string GetStackState()
        {
            var stack = CurrentStack;
            if (stack.Count == 0) return "Stos jest pusty.";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < stack.Count; i++) sb.AppendLine($"  {stack.Count - i}: {GetString(stack[stack.Count - 1 - i])}");
            return sb.ToString();
        }

        public static string GetTopAsString() { var s = CurrentStack; return s.Count > 0 ? GetString(s[s.Count - 1]) : ""; }

        public static string GetHPStackView(int maxLevels = 5)
        {
            var stack = CurrentStack;
            if (stack.Count == 0) return " 1: [Pusty]";
            StringBuilder sb = new StringBuilder();
            int levelsToShow = Math.Min(stack.Count, maxLevels);
            for (int i = levelsToShow; i >= 1; i--) sb.AppendLine($" {i}: {GetString(stack[stack.Count - i])}");
            return sb.ToString().TrimEnd();
        }

        public static void ClearStack() => CurrentStack.Clear();
        private static void Push(object item) { var s = CurrentStack; s.Add(item); if (s.Count > MaxStackSize) s.RemoveAt(0); }
        private static object Pop() { var s = CurrentStack; if (s.Count == 0) throw new Exception("Stos jest pusty!"); object item = s[s.Count - 1]; s.RemoveAt(s.Count - 1); return item; }
        private static object Peek() { var s = CurrentStack; if (s.Count == 0) throw new Exception("Stos jest pusty!"); return s[s.Count - 1]; }

        // ==============================================================
        // INTEGRACJA BricsCAD (DL, DX, DY, DZ) + SCALOWANIE INSUNITS
        // ==============================================================
        private static PhysicalValue GetDistanceFromCad(Editor ed, string mode)
        {
            if (ed == null) throw new Exception("Pobieranie punktów jest dostępne tylko w CADzie!");

            PromptPointOptions ppo1 = new PromptPointOptions($"\n[{mode}] Wskaż pierwszy punkt: ");
            PromptPointResult ppr1 = ed.GetPoint(ppo1);
            if (ppr1.Status != PromptStatus.OK) throw new Exception("Anulowano wskazywanie.");

            PromptPointOptions ppo2 = new PromptPointOptions($"\n[{mode}] Wskaż drugi punkt: ");
            ppo2.UseBasePoint = true; ppo2.BasePoint = ppr1.Value;
            if (mode == "DX" || mode == "DY") ppo2.UseDashedLine = true;

            PromptPointResult ppr2 = ed.GetPoint(ppo2);
            if (ppr2.Status != PromptStatus.OK) throw new Exception("Anulowano wskazywanie.");

            Point3d p1 = ppr1.Value; Point3d p2 = ppr2.Value;
            double dist = mode == "DX" ? Math.Abs(p2.X - p1.X) : mode == "DY" ? Math.Abs(p2.Y - p1.Y) : mode == "DZ" ? Math.Abs(p2.Z - p1.Z) : p1.DistanceTo(p2);

            // Automatyczne dopasowanie do jednostek BricsCADa!
            short insunits = Convert.ToInt16(Application.GetSystemVariable("INSUNITS"));
            string prefUnit = "m";
            if (insunits == 4) prefUnit = "mm";
            else if (insunits == 5) prefUnit = "cm";

            if (UnitEngine.Units.TryGetValue(prefUnit, out var uDef))
                return new PhysicalValue(dist * uDef.Value, uDef.Dim, prefUnit);

            return new PhysicalValue(dist, new UnitDim());
        }

        // ==============================================================
        // GŁÓWNY SILNIK RPN Z OBSŁUGĄ WZORCÓW FIZYCZNYCH
        // ==============================================================
        public static string Evaluate(string expression, object initialValue = null, Entity context = null, Editor ed = null)
        {
            if (string.IsNullOrEmpty(expression)) return initialValue?.ToString() ?? "";
            
            // ODPORNOŚĆ NA BIAŁE ZNAKI (V2 Hotfix)
            string expr = expression.Trim();
            
            if (initialValue != null) Push(initialValue);
            List<string> tokens = Tokenize(expr);

            foreach (string t in tokens)
            {
                string token = t.Trim();
                if (string.IsNullOrEmpty(token)) continue;
                string upperToken = token.ToUpperInvariant();

                try
                {
                    switch (upperToken)
                    {
                        case "DL": case "DX": case "DY": case "DZ": Push(GetDistanceFromCad(ed, upperToken)); break;
                        case "CLEAR": ClearStack(); break;
                        case "SWAP": { if (CurrentStack.Count < 2) continue; object b = Pop(); object a = Pop(); Push(b); Push(a); break; }
                        case "DUP": { if (CurrentStack.Count > 0) Push(Peek()); break; }
                        case "DROP": { if (CurrentStack.Count > 0) Pop(); break; }
                        case "PICK": { if (CurrentStack.Count == 0) continue; int idx = (int)GetNum(Pop()); var s = CurrentStack; if (idx < 1 || idx > s.Count) throw new Exception("Brak elementu"); Push(s[s.Count - idx]); break; }

                        // FIZYKA MATEMATYCZNA
                        case "+":
                            {
                                if (CurrentStack.Count < 2) continue;
                                var pb = GetPhys(Pop()); var pa = GetPhys(Pop());
                                if (pa.Dim != pb.Dim && !pa.Dim.IsDimensionless() && !pb.Dim.IsDimensionless()) throw new Exception($"Niezgodność: {pa.Dim} + {pb.Dim}");
                                Push(new PhysicalValue(pa.Value + pb.Value, pa.Dim != new UnitDim() ? pa.Dim : pb.Dim, pa.PrefUnit));
                                break;
                            }
                        case "-":
                            {
                                if (CurrentStack.Count < 2) continue;
                                var pb = GetPhys(Pop()); var pa = GetPhys(Pop());
                                if (pa.Dim != pb.Dim && !pa.Dim.IsDimensionless() && !pb.Dim.IsDimensionless()) throw new Exception($"Niezgodność: {pa.Dim} - {pb.Dim}");
                                string pref = pa.PrefUnit;
                                // Zabezpieczenie: różnica temperatur to zawsze temperatura względna (Delta), więc wymuszamy czyste Kelviny
                                if (pa.Dim == new UnitDim { Theta = 1 }) pref = "K";
                                Push(new PhysicalValue(pa.Value - pb.Value, pa.Dim != new UnitDim() ? pa.Dim : pb.Dim, pref));
                                break;
                            }
                        case "*":
                            {
                                if (CurrentStack.Count < 2) continue;
                                var pb = GetPhys(Pop()); var pa = GetPhys(Pop());
                                Push(new PhysicalValue(pa.Value * pb.Value, pa.Dim + pb.Dim));
                                break;
                            }
                        case "/":
                            {
                                if (CurrentStack.Count < 2) continue;
                                var pb = GetPhys(Pop()); var pa = GetPhys(Pop());
                                Push(new PhysicalValue(pa.Value / pb.Value, pa.Dim - pb.Dim));
                                break;
                            }
                        case "^":
                            {
                                if (CurrentStack.Count < 2) continue;
                                var pb = GetPhys(Pop()); var pa = GetPhys(Pop());
                                if (!pb.Dim.IsDimensionless()) throw new Exception("Wykładnik musi być bezwymiarowy!");
                                int exp = (int)Math.Round(pb.Value);
                                Push(new PhysicalValue(Math.Pow(pa.Value, pb.Value), pa.Dim * exp));
                                break;
                            }

                        // --- KOMENDY JEDNOSTKOWE HP-50g ---
                        case "CONVE":
                            {
                                if (CurrentStack.Count < 2) continue;
                                object tgtObj = Pop();
                                // Wyciągamy docelową jednostkę (nawet jeśli użytkownik podał "1_mm" zamiast "'mm'")
                                string tgtUnit = tgtObj is PhysicalValue pvTgt ? (pvTgt.PrefUnit ?? pvTgt.Dim.ToString()) : GetString(tgtObj).Replace("'", "").Replace("\"", "");
                                var pv = GetPhys(Pop());

                                try
                                {
                                    PhysicalValue uDef = UnitEngine.ParseUnit(tgtUnit);
                                    if (pv.Dim != uDef.Dim && !pv.Dim.IsDimensionless()) throw new Exception($"Niezgodność wymiarów: {pv.Dim} a {uDef.Dim}");
                                    Push(new PhysicalValue(pv.Value, uDef.Dim, tgtUnit));
                                }
                                catch (Exception ex) { throw new Exception($"Nieznana jednostka docelowa: {tgtUnit} (brak: {ex.Message})"); }
                                break;
                            }
                        case "+UNIT":
                            {
                                if (CurrentStack.Count < 2) continue;
                                object tgtObj = Pop();
                                string unitStr = tgtObj is PhysicalValue pvTgt ? (pvTgt.PrefUnit ?? pvTgt.Dim.ToString()) : GetString(tgtObj).Replace("'", "").Replace("\"", "");
                                var pv = GetPhys(Pop());
                                if (!pv.Dim.IsDimensionless()) throw new Exception("Wartość ma już wymiar. Użyj CONVE.");

                                try
                                {
                                    PhysicalValue uDef = UnitEngine.ParseUnit(unitStr);
                                    Push(new PhysicalValue(pv.Value * uDef.Value, uDef.Dim, unitStr));
                                }
                                catch (Exception ex) { throw new Exception($"Nieznana jednostka docelowa: {unitStr} (brak: {ex.Message})"); }
                                break;
                            }
                        case "UNBASE":
                            {
                                if (CurrentStack.Count == 0) continue;
                                var pv = GetPhys(Pop());
                                Push(new PhysicalValue(pv.Value, pv.Dim, null));
                                break;
                            }
                        case "UVAL":
                            {
                                if (CurrentStack.Count == 0) continue;
                                var pv = GetPhys(Pop());
                                double displayVal = pv.Value;
                                if (!string.IsNullOrEmpty(pv.PrefUnit))
                                {
                                    try
                                    {
                                        var pUnit = UnitEngine.ParseUnit(pv.PrefUnit);
                                        displayVal = (displayVal / pUnit.Value) - pUnit.Offset;
                                    }
                                    catch { }
                                }
                                Push(new PhysicalValue(displayVal, new UnitDim()));
                                break;
                            }

                        case "PRETTY":
                            {
                                if (CurrentStack.Count < 2) continue;
                                int d = (int)GetNum(Pop()); // Pożądana precyzja
                                var p = GetPhys(Pop());

                                double baseValue = p.Value;
                                string unitStr = p.PrefUnit;

                                // --- LOGIKA SMART SCALING ---
                                if (string.IsNullOrEmpty(unitStr))
                                {
                                    if (p.Dim == new UnitDim { L = 1 })
                                    {
                                        if (baseValue >= 1000) unitStr = "km";
                                        else if (baseValue >= 1.0) unitStr = "m";
                                        else if (baseValue >= 0.01) unitStr = "cm";
                                        else unitStr = "mm";
                                    }
                                    else if (p.Dim == new UnitDim { T = 1 })
                                    {
                                        if (baseValue >= 31557600) unitStr = "rok";
                                        else if (baseValue >= 86400) unitStr = "dzień";
                                        else if (baseValue >= 3600) unitStr = "h";
                                        else if (baseValue >= 60) unitStr = "min";
                                        else unitStr = "s";
                                    }
                                    else if (p.Dim == new UnitDim { M = 1, L = 2, T = -2 })
                                    {
                                        if (baseValue >= 1e9) unitStr = "GJ";
                                        else if (baseValue >= 1e6) unitStr = "MJ";
                                        else if (baseValue >= 1000) unitStr = "kJ";
                                        else unitStr = "J";
                                    }
                                    else if (p.Dim == new UnitDim { M = 1, L = -1, T = -2 })
                                    {
                                        if (baseValue >= 1e6) unitStr = "MPa";
                                        else if (baseValue >= 1000) unitStr = "kPa";
                                        else unitStr = "Pa";
                                    }
                                    else if (p.Dim == new UnitDim { L = 3 })
                                    {
                                        if (baseValue >= 1.0) unitStr = "m3";
                                        else if (baseValue >= 0.001) unitStr = "dm3";
                                        else unitStr = "ml";
                                    }
                                    else unitStr = p.Dim.ToString();
                                }

                                double displayVal = baseValue;
                                try
                                {
                                    var u = UnitEngine.ParseUnit(unitStr);
                                    displayVal = (baseValue / u.Value) - u.Offset;
                                }
                                catch { }

                                int requiredSigFigs = 3;
                                int sigPlaces = displayVal != 0 ? requiredSigFigs - 1 - (int)Math.Floor(Math.Log10(Math.Abs(displayVal))) : 0;
                                int finalD = Math.Max(d, Math.Max(0, sigPlaces));

                                string formattedNum = Math.Round(displayVal, finalD).ToString(CultureInfo.InvariantCulture);
                                Push($"{formattedNum} {unitStr}");
                                break;
                            }


                        case "UFACT":
                            {
                                if (CurrentStack.Count < 2) continue;
                                string tgtUnit = GetString(Pop());
                                var pv = GetPhys(Pop());
                                if (!UnitEngine.Units.ContainsKey(tgtUnit)) throw new Exception($"Nieznana jednostka: {tgtUnit}");
                                Push(new PhysicalValue(pv.Value, pv.Dim, tgtUnit));
                                break;
                            }
                        // --- ZMIENNE UŻYTKOWNIKA ---
                        case "STO":
                            {
                                if (CurrentStack.Count < 2) continue;
                                string vName = GetString(Pop()).Replace("'", "").Replace("\"", "").ToUpperInvariant();
                                if (!vName.StartsWith("$")) vName = "$" + vName; 
                                var vVal = GetPhys(Pop());
                                CurrentVariables[vName] = vVal;
                                break;
                            }
                        case "RCL":
                            {
                                if (CurrentStack.Count == 0) continue;
                                string vName = GetString(Pop()).Replace("'", "").Replace("\"", "").ToUpperInvariant();
                                if (!vName.StartsWith("$")) vName = "$" + vName; 
                                if (CurrentVariables.TryGetValue(vName, out var val)) Push(val);
                                else throw new Exception($"Brak zmiennej: {vName}");
                                break;
                            }
                        case "VARS_CLEAR":
                            {
                                CurrentVariables.Clear();
                                break;
                            }

                        // MATEMATYKA / TEKST
                        case "SQRT": { if (CurrentStack.Count == 0) continue; var p = GetPhys(Pop()); Push(new PhysicalValue(Math.Sqrt(p.Value), new UnitDim())); break; }
                        case "ROUND":
                            {
                                if (CurrentStack.Count < 2) continue;
                                int d = (int)GetNum(Pop());
                                var p = GetPhys(Pop());
                                int requiredSigFigs = 3;

                                if (!string.IsNullOrEmpty(p.PrefUnit))
                                {
                                    try
                                    {
                                        var pUnit = UnitEngine.ParseUnit(p.PrefUnit);
                                        double uFact = pUnit.Value;
                                        double displayVal = (p.Value / uFact) - pUnit.Offset;

                                        int sigPlaces = displayVal != 0 ? requiredSigFigs - 1 - (int)Math.Floor(Math.Log10(Math.Abs(displayVal))) : 0;
                                        int finalD = Math.Max(d, Math.Max(0, sigPlaces));

                                        double roundedDisplay = Math.Round(displayVal, finalD);
                                        Push(new PhysicalValue((roundedDisplay + pUnit.Offset) * uFact, p.Dim, p.PrefUnit));
                                        break;
                                    }
                                    catch { }
                                }

                                int baseSigPlaces = p.Value != 0 ? requiredSigFigs - 1 - (int)Math.Floor(Math.Log10(Math.Abs(p.Value))) : 0;
                                int baseFinalD = Math.Max(d, Math.Max(0, baseSigPlaces));
                                double baseRounded = Math.Round(p.Value, baseFinalD);
                                Push(new PhysicalValue(baseRounded, p.Dim, p.PrefUnit));
                                break;
                            }
                        case "ABS": { if (CurrentStack.Count == 0) continue; var p = GetPhys(Pop()); Push(new PhysicalValue(Math.Abs(p.Value), p.Dim, p.PrefUnit)); break; }

                        case "==": case "=": { if (CurrentStack.Count < 2) continue; string b = GetString(Pop()); string a = GetString(Pop()); Push(new PhysicalValue(a.Equals(b, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0, new UnitDim())); break; }
                        case "!=": { if (CurrentStack.Count < 2) continue; string b = GetString(Pop()); string a = GetString(Pop()); Push(new PhysicalValue(a.Equals(b, StringComparison.OrdinalIgnoreCase) ? 0.0 : 1.0, new UnitDim())); break; }
                        case ">": { if (CurrentStack.Count < 2) continue; double b = GetNum(Pop()); double a = GetNum(Pop()); Push(new PhysicalValue(a > b ? 1.0 : 0.0, new UnitDim())); break; }
                        case "<": { if (CurrentStack.Count < 2) continue; double b = GetNum(Pop()); double a = GetNum(Pop()); Push(new PhysicalValue(a < b ? 1.0 : 0.0, new UnitDim())); break; }

                        case "CONCAT": case "&": { if (CurrentStack.Count < 2) continue; string b = GetString(Pop()); string a = GetString(Pop()); Push(a + b); break; }
                        case "REPLACE": { if (CurrentStack.Count < 3) continue; string n = GetString(Pop()); string o = GetString(Pop()); string tg = GetString(Pop()); Push(tg.Replace(o, n)); break; }

                        case "IFEMPTY":
                            {
                                if (CurrentStack.Count < 2) continue;
                                string fallback = GetString(Pop());
                                string val = GetString(Pop());
                                Push(string.IsNullOrEmpty(val) ? fallback : val);
                                break;
                            }
                        case "SPLIT": { if (CurrentStack.Count < 3) continue; int idx = (int)GetNum(Pop()); string sep = GetString(Pop()); string tg = GetString(Pop()); string[] p = tg.Split(new[] { sep }, StringSplitOptions.None); Push(idx >= 0 && idx < p.Length ? p[idx] : ""); break; }
                        case "NUM_ADD":
                            {
                                if (CurrentStack.Count < 2) continue;
                                double valToAdd = GetNum(Pop());
                                string baseText = GetString(Pop());

                                string result = Regex.Replace(baseText, @"\d+([.,]\d+)?", match =>
                                {
                                    if (double.TryParse(match.Value.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedNum))
                                    {
                                        return (parsedNum + valToAdd).ToString(CultureInfo.InvariantCulture);
                                    }
                                    return match.Value;
                                });
                                Push(result);
                                break;
                            }

                        default:
                            if (token.StartsWith("$"))
                            {
                                string vName = upperToken; 

                                if (CurrentVariables.TryGetValue(vName, out var storedVal))
                                {
                                    Push(storedVal);
                                }
                                else
                                {
                                    string agentKey = vName.Substring(1);
                                    if (AgentMemoryState.Variables.TryGetValue(agentKey, out string agentVal))
                                    {
                                        Push(GetPhys(agentVal));
                                    }
                                    else throw new Exception($"Brak zmiennej: {vName}");
                                }
                                break;
                            }
                            
                            // OBSŁUGA ZMIENNYCH GLOBALNYCH @ZMIENNA
                            if (token.StartsWith("@"))
                            {
                                string agentKey = upperToken.Substring(1);
                                if (AgentMemoryState.Variables.TryGetValue(agentKey, out string agentVal))
                                {
                                    Push(GetPhys(agentVal));
                                }
                                else throw new Exception($"Brak zmiennej globalnej: {token}");
                                break;
                            }

                            if (token.StartsWith("#"))
                            {
                                if (upperToken == "#PI") { Push(new PhysicalValue(Math.PI, new UnitDim())); break; }
                                if (upperToken == "#G") { Push(new PhysicalValue(9.81, new UnitDim { L = 1, T = -2 }, "m/s2")); break; }
                                if (upperToken == "#C") { Push(new PhysicalValue(299792458.0, new UnitDim { L = 1, T = -1 }, "m/s")); break; }

                                if (upperToken == "#UNITA")
                                {
                                    short ins = Convert.ToInt16(Application.GetSystemVariable("INSUNITS"));
                                    Push(ins == 4 ? "mm2" : (ins == 5 ? "cm2" : "m2"));
                                    break;
                                }
                                if (upperToken == "#UNITL")
                                {
                                    short ins = Convert.ToInt16(Application.GetSystemVariable("INSUNITS"));
                                    Push(ins == 4 ? "mm" : (ins == 5 ? "cm" : "m"));
                                    break;
                                }
                                throw new Exception($"Nieznana stała fizyczna: {token}");
                            }

                            Match m = Regex.Match(token, @"^([-0-9.,]+)_?([a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ%(][a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ0-9/*^()%-]*)$");
                            if (m.Success)
                            {
                                try
                                {
                                    double val = double.Parse(m.Groups[1].Value.Replace(",", "."), CultureInfo.InvariantCulture);
                                    string unitStr = m.Groups[2].Value;
                                    PhysicalValue uVal = UnitEngine.ParseUnit(unitStr);
                                    Push(new PhysicalValue((val + uVal.Offset) * uVal.Value, uVal.Dim, unitStr));
                                    break;
                                }
                                catch (Exception ex)
                                {
                                    throw new Exception($"Nieznana składnia jednostki w: {token} (Brak: {ex.Message})");
                                }
                            }

                            if (double.TryParse(token.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double num)) { Push(new PhysicalValue(num, new UnitDim())); }
                            else { Push(token); }
                            break;
                    }
                }
                catch (Exception ex) { throw new Exception($"Błąd przy '{token}': {ex.Message}"); }
            }
            return CurrentStack.Count > 0 ? GetString(Peek()) : "";
        }

        private static PhysicalValue GetPhys(object o)
        {
            if (o is PhysicalValue p) return p;
            string s = GetString(o);

            Match m = Regex.Match(s, @"^([-0-9.,]+)_?([a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ%(][a-zA-ZąćęłńóśźżĄĆĘŁŃÓŚŹŻ0-9/*^()%-]*)$");
            if (m.Success)
            {
                try
                {
                    double val = double.Parse(m.Groups[1].Value.Replace(",", "."), CultureInfo.InvariantCulture);
                    PhysicalValue uVal = UnitEngine.ParseUnit(m.Groups[2].Value);
                    return new PhysicalValue((val + uVal.Offset) * uVal.Value, uVal.Dim, m.Groups[2].Value);
                }
                catch { }
            }

            double.TryParse(s.Replace("_", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double num);
            return new PhysicalValue(num, new UnitDim());
        }

        private static double GetNum(object o)
        {
            if (o is PhysicalValue p) return p.Value;
            double.TryParse(GetString(o).Replace("_", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double num);
            return num;
        }

        private static string GetString(object o)
        {
            string s = o?.ToString() ?? "";
            if (s.StartsWith("'") && s.EndsWith("'") && s.Length >= 2) return s.Substring(1, s.Length - 2);
            if (s.StartsWith("\"") && s.EndsWith("\"") && s.Length >= 2) return s.Substring(1, s.Length - 2);
            return s;
        }

        private static List<string> Tokenize(string expr)
        {
            List<string> ts = new List<string>(); StringBuilder sb = new StringBuilder(); bool q = false;
            foreach (char c in expr)
            {
                if (c == '\'' || c == '\"') { q = !q; sb.Append(c); }
                else if (char.IsWhiteSpace(c) && !q) { if (sb.Length > 0) { ts.Add(sb.ToString()); sb.Clear(); } }
                else sb.Append(c);
            }
            if (sb.Length > 0) ts.Add(sb.ToString()); return ts;
        }
    }
}
