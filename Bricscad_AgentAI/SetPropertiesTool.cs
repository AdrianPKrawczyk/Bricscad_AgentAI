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

            var warunki = new System.Collections.Generic.List<(string Prop, string Val)>();
            MatchCollection matches = Regex.Matches(jsonArgs, @"\""Property\""\s*:\s*\""([^\""]+)\"".*?\""Value\""\s*:\s*(\""[^\""]+\""|[^\s,}]+)", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match m in matches)
            {
                warunki.Add((m.Groups[1].Value, m.Groups[2].Value.Trim('\"')));
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
                        string valStr = warunek.Val;

                        try
                        {
                            // --- SPECJALNA OBSŁUGA DLA GŁÓWNYCH KLAS CAD ---
                            if (propName.Equals("Layer", StringComparison.OrdinalIgnoreCase))
                            {
                                ent.Layer = valStr;
                                czyZmienionoCosWObiekcie = true;
                            }
                            else if (propName.Equals("Color", StringComparison.OrdinalIgnoreCase) || propName.Equals("ColorIndex", StringComparison.OrdinalIgnoreCase))
                            {
                                if (int.TryParse(valStr, out int c)) { ent.ColorIndex = c; czyZmienionoCosWObiekcie = true; }
                            }
                            else if (propName.Equals("Linetype", StringComparison.OrdinalIgnoreCase))
                            {
                                ent.Linetype = valStr; czyZmienionoCosWObiekcie = true;
                            }
                            else if (propName.Equals("LinetypeScale", StringComparison.OrdinalIgnoreCase))
                            {
                                if (double.TryParse(valStr.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double s))
                                { ent.LinetypeScale = s; czyZmienionoCosWObiekcie = true; }
                            }
                            else if (propName.Equals("LineWeight", StringComparison.OrdinalIgnoreCase))
                            {
                                if (int.TryParse(valStr, out int lwInt)) { ent.LineWeight = (LineWeight)lwInt; czyZmienionoCosWObiekcie = true; }
                                else if (valStr.Equals("ByLayer", StringComparison.OrdinalIgnoreCase)) { ent.LineWeight = LineWeight.ByLayer; czyZmienionoCosWObiekcie = true; }
                                else if (valStr.Equals("ByBlock", StringComparison.OrdinalIgnoreCase)) { ent.LineWeight = LineWeight.ByBlock; czyZmienionoCosWObiekcie = true; }
                            }
                            // --- REFLEKSJA DLA POZOSTAŁYCH WŁAŚCIWOŚCI (np. Radius, Dimclrt) ---
                            else
                            {
                                if (ent is MText && propName.Equals("Height", StringComparison.OrdinalIgnoreCase)) propName = "TextHeight";
                                else if (ent is Dimension && (propName.Equals("Height", StringComparison.OrdinalIgnoreCase) || propName.Equals("TextHeight", StringComparison.OrdinalIgnoreCase))) propName = "Dimtxt";

                                System.Reflection.PropertyInfo propInfo = ent.GetType().GetProperty(propName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                                if (propInfo != null && propInfo.CanWrite)
                                {
                                    Type t = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;

                                    // Automatyczna obsługa ukrytych kolorów w wymiarach (np. Dimclrd, Dimclrt, Dimclre)
                                    if (t == typeof(Teigha.Colors.Color))
                                    {
                                        if (int.TryParse(valStr, out int c))
                                        {
                                            propInfo.SetValue(ent, Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, (short)c), null);
                                            czyZmienionoCosWObiekcie = true;
                                        }
                                    }
                                    else
                                    {
                                        object safeValue = Convert.ChangeType(valStr.Replace(",", "."), t, System.Globalization.CultureInfo.InvariantCulture);
                                        propInfo.SetValue(ent, safeValue, null);
                                        czyZmienionoCosWObiekcie = true;
                                    }
                                }
                                else
                                {
                                    ed.WriteMessage($"\n[Ostrzeżenie]: Właściwość '{propName}' nie istnieje lub jest tylko do odczytu w klasie {ent.GetType().Name}.");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Zamiast połykać błąd, krzyczymy o nim do konsoli!
                            ed.WriteMessage($"\n[Błąd SetProps]: Nie udało się zmienić '{propName}' na wartość '{valStr}'. Powód: {ex.Message}");
                        }
                    }

                    if (czyZmienionoCosWObiekcie)
                    {
                        ent.RecordGraphicsModified(true); // Informacja dla BricsCADa, że obiekt się zmienił
                        ent.Draw(); // Wymuszenie odrysowania na ekranie
                        zmodyfikowane++;
                    }
                }
                tr.Commit();
            }

            // Opcjonalnie twarde odświeżenie ekranu, jeśli grafika "zamarznie"
            if (zmodyfikowane > 0) doc.Editor.Regen();

            return $"WYNIK: Zmodyfikowano właściwości dla {zmodyfikowane} obiektów.";
        }

        public string Execute(Document doc) { return Execute(doc, ""); }
    }
}