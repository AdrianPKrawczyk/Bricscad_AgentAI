using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using System;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;

namespace BricsCAD_Agent
{
    public class SetPropertiesTool : ITool
    {
        public string ActionTag => "[ACTION:SET_PROPERTIES]";

        public string Description =>
            "Zmienia właściwości zaznaczonych obiektów. Wymaga JSON: " +
            "{\"Properties\": [{\"Property\": \"Layer\", \"Value\": \"0\"}, {\"Property\": \"Color\", \"Value\": 1}]}";

        public string Execute(Document doc, string jsonArgs)
        {
            Editor ed = doc.Editor;
            ObjectId[] ids = Komendy.AktywneZaznaczenie;
            if (ids == null || ids.Length == 0) return "[Błąd]: Nie mam w pamięci żadnych obiektów! Użyj najpierw tagu SELECT.";

            var warunki = new System.Collections.Generic.List<(string Prop, string Op, string Val)>();

            MatchCollection matches = Regex.Matches(jsonArgs, @"\{\s*\""Property\""\s*:\s*\""([^\""]+)\""(.*?)\}", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                string propName = m.Groups[1].Value;
                string reszta = m.Groups[2].Value;

                string opSign = "=";
                Match mOp = Regex.Match(reszta, @"\""Operator\""\s*:\s*\""([^\""]+)\""");
                if (mOp.Success) opSign = mOp.Groups[1].Value;

                string valStr = "";
                Match mVal = Regex.Match(reszta, @"\""Value\""\s*:\s*(\""[^\""]+\""|[^\s,}]+)");
                if (mVal.Success) valStr = mVal.Groups[1].Value.Trim('\"');

                warunki.Add((propName, opSign, valStr));
            }

            if (warunki.Count == 0) return "[Błąd Narzędzia]: Brak poprawnie zdefiniowanych właściwości do zmiany.";

            int zmodyfikowane = 0;

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objId in ids)
                {
                    Entity ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
                    if (ent == null) continue;

                    bool czyZmienionoCosWObiekcie = false;

                    foreach (var warunek in warunki)
                    {
                        string propName = warunek.Prop;
                        string opSign = warunek.Op;
                        string valStr = warunek.Val;

                        try
                        {
                            // --- SPECJALNA OBSŁUGA DLA GŁÓWNYCH KLAS CAD ---
                            if (propName.Equals("Layer", StringComparison.OrdinalIgnoreCase))
                            {
                                // NAPRAWA: Jeśli RPN, obliczamy nową nazwę warstwy
                                ent.Layer = opSign == "RPN" ? RpnCalculator.Evaluate(valStr, ent.Layer) : valStr;
                                czyZmienionoCosWObiekcie = true;
                            }
                            else if (propName.Equals("Color", StringComparison.OrdinalIgnoreCase) || propName.Equals("ColorIndex", StringComparison.OrdinalIgnoreCase))
                            {
                                // --- NAPRAWA: OBLICZAMY RPN DLA KOLORU ZANIM SPRÓBUJEMY GO PRZYPISAĆ! ---
                                string finalValStr = opSign == "RPN" ? RpnCalculator.Evaluate(valStr, ent.ColorIndex) : valStr;

                                if (int.TryParse(finalValStr, out int c))
                                {
                                    ent.ColorIndex = c;
                                    czyZmienionoCosWObiekcie = true;
                                }
                                else if (finalValStr.Contains(","))
                                {
                                    string[] rgb = finalValStr.Split(',');
                                    if (rgb.Length == 3 && byte.TryParse(rgb[0].Trim(), out byte r) && byte.TryParse(rgb[1].Trim(), out byte g) && byte.TryParse(rgb[2].Trim(), out byte b))
                                    {
                                        ent.Color = Teigha.Colors.Color.FromRgb(r, g, b);
                                        czyZmienionoCosWObiekcie = true;
                                    }
                                }
                            }
                            else if (propName.Equals("Linetype", StringComparison.OrdinalIgnoreCase))
                            {
                                ent.Linetype = opSign == "RPN" ? RpnCalculator.Evaluate(valStr, ent.Linetype) : valStr;
                                czyZmienionoCosWObiekcie = true;
                            }
                            else if (propName.Equals("LinetypeScale", StringComparison.OrdinalIgnoreCase))
                            {
                                if (double.TryParse(valStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double s))
                                { ent.LinetypeScale = s; czyZmienionoCosWObiekcie = true; }
                            }
                            else if (propName.Equals("LineWeight", StringComparison.OrdinalIgnoreCase))
                            {
                                // --- NAPRAWA: OBLICZAMY RPN DLA GRUBOŚCI LINII ---
                                string finalValStr = valStr;
                                if (opSign == "RPN")
                                {
                                    int obecnaGrubosc = (ent.LineWeight == LineWeight.ByLayer) ? -1 : (ent.LineWeight == LineWeight.ByBlock ? -2 : (ent.LineWeight == LineWeight.ByLineWeightDefault ? -3 : (int)ent.LineWeight));
                                    finalValStr = RpnCalculator.Evaluate(valStr, obecnaGrubosc);
                                }

                                if (int.TryParse(finalValStr, out int lwInt)) { ent.LineWeight = (LineWeight)lwInt; czyZmienionoCosWObiekcie = true; }
                                else if (finalValStr.Equals("ByLayer", StringComparison.OrdinalIgnoreCase)) { ent.LineWeight = LineWeight.ByLayer; czyZmienionoCosWObiekcie = true; }
                                else if (finalValStr.Equals("ByBlock", StringComparison.OrdinalIgnoreCase)) { ent.LineWeight = LineWeight.ByBlock; czyZmienionoCosWObiekcie = true; }
                            }
                            else if (propName.Equals("Transparency", StringComparison.OrdinalIgnoreCase))
                            {
                                if (valStr.Equals("ByLayer", StringComparison.OrdinalIgnoreCase))
                                { ent.Transparency = new Teigha.Colors.Transparency(Teigha.Colors.TransparencyMethod.ByLayer); czyZmienionoCosWObiekcie = true; }
                                else if (valStr.Equals("ByBlock", StringComparison.OrdinalIgnoreCase))
                                { ent.Transparency = new Teigha.Colors.Transparency(Teigha.Colors.TransparencyMethod.ByBlock); czyZmienionoCosWObiekcie = true; }
                                else if (double.TryParse(valStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double transpVal))
                                {
                                    transpVal = Math.Max(0, Math.Min(90, transpVal));
                                    byte alpha = (byte)Math.Round(255.0 * (100.0 - transpVal) / 100.0);
                                    ent.Transparency = new Teigha.Colors.Transparency(alpha);
                                    czyZmienionoCosWObiekcie = true;
                                }
                            }
                            else if (propName.Equals("AnnotationScale", StringComparison.OrdinalIgnoreCase) || propName.Equals("ObjectScale", StringComparison.OrdinalIgnoreCase))
                            {
                                if (ent.Annotative == AnnotativeStates.False) ent.Annotative = AnnotativeStates.True;

                                ObjectContextManager ocm = doc.Database.ObjectContextManager;
                                if (ocm != null)
                                {
                                    ObjectContextCollection occ = ocm.GetContextCollection("ACDB_ANNOTATIONSCALES");
                                    if (occ != null)
                                    {
                                        ObjectContext nowaSkala = occ.GetContext(valStr);
                                        if (nowaSkala != null)
                                        {
                                            if (!ent.HasContext(nowaSkala)) { ent.AddContext(nowaSkala); czyZmienionoCosWObiekcie = true; }
                                        }
                                        else ed.WriteMessage($"\n[Błąd]: Skala o nazwie '{valStr}' nie istnieje w tym rysunku!");
                                    }
                                }
                            }
                            else if (propName.Equals("RemoveAnnotationScale", StringComparison.OrdinalIgnoreCase))
                            {
                                if (ent.Annotative == AnnotativeStates.True)
                                {
                                    ObjectContextManager ocm = doc.Database.ObjectContextManager;
                                    if (ocm != null)
                                    {
                                        ObjectContextCollection occ = ocm.GetContextCollection("ACDB_ANNOTATIONSCALES");
                                        if (occ != null)
                                        {
                                            if (valStr.Equals("All", StringComparison.OrdinalIgnoreCase))
                                            {
                                                ObjectContext aktualnaSkala = occ.CurrentContext;
                                                foreach (ObjectContext ctx in occ)
                                                {
                                                    if (ctx.Name != aktualnaSkala.Name && ent.HasContext(ctx))
                                                    { try { ent.RemoveContext(ctx); czyZmienionoCosWObiekcie = true; } catch { } }
                                                }
                                            }
                                            else
                                            {
                                                ObjectContext skalaDoUsuniecia = occ.GetContext(valStr);
                                                if (skalaDoUsuniecia != null && ent.HasContext(skalaDoUsuniecia))
                                                { try { ent.RemoveContext(skalaDoUsuniecia); czyZmienionoCosWObiekcie = true; } catch { } }
                                            }
                                        }
                                    }
                                }
                            }
                            else if (propName.Equals("Annotative", StringComparison.OrdinalIgnoreCase))
                            {
                                if (valStr.Equals("True", StringComparison.OrdinalIgnoreCase) || valStr == "1") { ent.Annotative = AnnotativeStates.True; czyZmienionoCosWObiekcie = true; }
                                else if (valStr.Equals("False", StringComparison.OrdinalIgnoreCase) || valStr == "0") { ent.Annotative = AnnotativeStates.False; czyZmienionoCosWObiekcie = true; }
                            }

                            // --- REFLEKSJA DLA POZOSTAŁYCH WŁAŚCIWOŚCI ---
                            else
                            {
                                if (ent is MText && propName.Equals("Height", StringComparison.OrdinalIgnoreCase)) propName = "TextHeight";
                                else if (ent is Dimension && (propName.Equals("Height", StringComparison.OrdinalIgnoreCase) || propName.Equals("TextHeight", StringComparison.OrdinalIgnoreCase))) propName = "Dimtxt";

                                if (propName.Contains("."))
                                {
                                    string[] parts = propName.Split('.');
                                    string basePropName = parts[0];
                                    string subPropName = parts[1].ToUpper();

                                    System.Reflection.PropertyInfo basePropInfo = ent.GetType().GetProperty(basePropName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                                    if (basePropInfo != null && basePropInfo.CanWrite)
                                    {
                                        object baseVal = basePropInfo.GetValue(ent);

                                        if (baseVal is Teigha.Geometry.Point3d pt)
                                        {
                                            double operandVal = 0;
                                            bool isNumber = double.TryParse(valStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out operandVal);

                                            if (opSign == "RPN" || isNumber)
                                            {
                                                double currentAxisVal = subPropName == "X" ? pt.X : (subPropName == "Y" ? pt.Y : pt.Z);
                                                double finalVal = operandVal;

                                                // NAPRAWA: Zmieniono kolejność na (wyrażenie, wartosc) oraz dodano Convert.ToDouble
                                                if (opSign == "RPN") finalVal = Convert.ToDouble(RpnCalculator.Evaluate(valStr, currentAxisVal).Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                                                else if (opSign == "+") finalVal = currentAxisVal + operandVal;
                                                else if (opSign == "-") finalVal = currentAxisVal - operandVal;
                                                else if (opSign == "*") finalVal = currentAxisVal * operandVal;

                                                Teigha.Geometry.Point3d newPt;
                                                if (subPropName == "X") newPt = new Teigha.Geometry.Point3d(finalVal, pt.Y, pt.Z);
                                                else if (subPropName == "Y") newPt = new Teigha.Geometry.Point3d(pt.X, finalVal, pt.Z);
                                                else if (subPropName == "Z") newPt = new Teigha.Geometry.Point3d(pt.X, pt.Y, finalVal);
                                                else throw new Exception($"Nieznana oś punktu: '{subPropName}'");

                                                basePropInfo.SetValue(ent, newPt, null);
                                                czyZmienionoCosWObiekcie = true;
                                            }
                                            else throw new Exception($"Wartość '{valStr}' nie jest poprawną liczbą dla współrzędnej.");
                                        }
                                        else ed.WriteMessage($"\n[Ostrzeżenie]: Zagnieżdżenia wspierane są tylko dla punktów 3D.");
                                    }
                                    else ed.WriteMessage($"\n[Ostrzeżenie]: Główna właściwość '{basePropName}' nie istnieje.");
                                }
                                else
                                {
                                    System.Reflection.PropertyInfo propInfo = ent.GetType().GetProperty(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                                    if (propInfo != null && propInfo.CanWrite)
                                    {
                                        Type t = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;

                                        if (t == typeof(Teigha.Colors.Color))
                                        {
                                            if (int.TryParse(valStr, out int c))
                                            { propInfo.SetValue(ent, Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, (short)c), null); czyZmienionoCosWObiekcie = true; }
                                            else if (valStr.Contains(","))
                                            {
                                                string[] rgb = valStr.Split(',');
                                                if (rgb.Length == 3 && byte.TryParse(rgb[0].Trim(), out byte r) && byte.TryParse(rgb[1].Trim(), out byte g) && byte.TryParse(rgb[2].Trim(), out byte b))
                                                { propInfo.SetValue(ent, Teigha.Colors.Color.FromRgb(r, g, b), null); czyZmienionoCosWObiekcie = true; }
                                            }
                                        }
                                        else
                                        {
                                            if (opSign != "=" && (t == typeof(double) || t == typeof(int) || t == typeof(short) || t == typeof(float)))
                                            {
                                                double currentVal = Convert.ToDouble(propInfo.GetValue(ent));
                                                double operandVal = 0;

                                                if (opSign != "RPN") operandVal = Convert.ToDouble(valStr.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                                                double finalVal = operandVal;

                                                // NAPRAWA: Zmieniono kolejność i dodano konwersję na typ liczbowy
                                                if (opSign == "RPN") finalVal = Convert.ToDouble(RpnCalculator.Evaluate(valStr, currentVal).Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                                                else if (opSign == "+") finalVal = currentVal + operandVal;
                                                else if (opSign == "-") finalVal = currentVal - operandVal;
                                                else if (opSign == "*") finalVal = currentVal * operandVal;

                                                object safeValue = Convert.ChangeType(finalVal, t, System.Globalization.CultureInfo.InvariantCulture);
                                                propInfo.SetValue(ent, safeValue, null);
                                                czyZmienionoCosWObiekcie = true;
                                            }
                                            else
                                            {
                                                // NAPRAWA: Wymuszenie przetworzenia tekstu w RPN dla zwykłych właściwości (np. Nazw, Stylów)
                                                string finalStr = valStr;
                                                if (opSign == "RPN")
                                                {
                                                    object currentVal = propInfo.GetValue(ent);
                                                    finalStr = RpnCalculator.Evaluate(valStr, currentVal);
                                                }

                                                object safeValue = Convert.ChangeType(finalStr.Replace(",", "."), t, System.Globalization.CultureInfo.InvariantCulture);
                                                propInfo.SetValue(ent, safeValue, null);
                                                czyZmienionoCosWObiekcie = true;
                                            }
                                        }
                                    }
                                    else ed.WriteMessage($"\n[Ostrzeżenie]: Właściwość '{propName}' nie istnieje lub jest tylko do odczytu.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ed.WriteMessage($"\n[Błąd SetProps]: Nie udało się zmienić '{propName}' na wartość '{valStr}'. Powód: {ex.Message}");
                        }
                    }

                    if (czyZmienionoCosWObiekcie)
                    {
                        ent.RecordGraphicsModified(true);
                        ent.Draw();
                        zmodyfikowane++;
                    }
                }
                tr.Commit();
            }

            if (zmodyfikowane > 0) doc.Editor.Regen();

            return $"WYNIK: Zmodyfikowano właściwości dla {zmodyfikowane} obiektów.";
        }

        public string Execute(Document doc) { return Execute(doc, ""); }
    }
}