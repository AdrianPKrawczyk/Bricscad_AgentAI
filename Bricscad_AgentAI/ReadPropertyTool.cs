using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;
using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    public class ReadPropertyTool : ITool
    {
        public string ActionTag => "[ACTION:READ_PROPERTY]";
        public string Description => "Odczytuje konkretną pojedynczą właściwość (np. Center, Radius) z zaznaczonych obiektów. Obsługuje też wirtualny 'MidPoint'.";

        public string Execute(Document doc, string jsonArgs)
        {
            ObjectId[] ids = Komendy.AktywneZaznaczenie;
            if (ids == null || ids.Length == 0) return "WYNIK: Brak zaznaczonych obiektów.";

            string propName = "";
            Match mProp = Regex.Match(jsonArgs, @"\""Property\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mProp.Success) propName = mProp.Groups[1].Value;

            if (string.IsNullOrEmpty(propName)) return "WYNIK: Nie podano nazwy właściwości (Property) do odczytania w tagu JSON.";

            // --- SEKCJA PAMIĘCI ---
            string saveAs = "";
            Match mSave = Regex.Match(jsonArgs, @"\""SaveAs\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mSave.Success) saveAs = mSave.Groups[1].Value;
            // ------------------------------------------------

            List<string> wyniki = new List<string>();
            List<string> czysteWartosci = new List<string>();

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in ids)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    try
                    {
                        object wartoscObiektu = null;

                        // --- ROZWIĄZANIE 1: WIRTUALNA WŁAŚCIWOŚĆ MIDPOINT ---
                        // Działa dla Line, Polyline, Arc, Spline (klasa bazowa Curve)
                        if (propName.Equals("MidPoint", StringComparison.OrdinalIgnoreCase) && ent is Curve curve)
                        {
                            try
                            {
                                // Oblicza połowę odległości całej krzywej i znajduje ten punkt w przestrzeni
                                double dist = curve.GetDistanceAtParameter(curve.EndParam) / 2.0;
                                wartoscObiektu = curve.GetPointAtDist(dist);
                            }
                            catch { }
                        }
                        else
                        {
                            // Tradycyjna obsługa przez refleksję i zagnieżdżenia
                            string[] zagniezdzenia = propName.Split('.');
                            wartoscObiektu = ent;
                            System.Reflection.PropertyInfo propInfo = null;

                            foreach (string czesc in zagniezdzenia)
                            {
                                if (wartoscObiektu == null) break;
                                propInfo = wartoscObiektu.GetType().GetProperty(czesc, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);

                                if (propInfo != null)
                                {
                                    wartoscObiektu = propInfo.GetValue(wartoscObiektu);
                                }
                                else
                                {
                                    wartoscObiektu = null;
                                    break;
                                }
                            }
                        }

                        if (wartoscObiektu != null)
                        {
                            string valStr = wartoscObiektu.ToString();

                            // --- ROZWIĄZANIE 2: WYMUSZANIE KROPKI DZIESIĘTNEJ (Formatowanie ułamków) ---
                            if (wartoscObiektu is Teigha.Geometry.Point3d pt)
                            {
                                string x = pt.X.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
                                string y = pt.Y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
                                string z = pt.Z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
                                valStr = $"({x},{y},{z})";
                            }
                            else if (wartoscObiektu is double dbl)
                            {
                                // To naprawia błędy z MTextem i przecinkami np. 164690,35 na 164690.35
                                valStr = dbl.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
                            }
                            else if (wartoscObiektu is Teigha.Colors.Color col)
                            {
                                valStr = col.ColorIndex.ToString();
                            }

                            wyniki.Add($"- Obiekt [{ent.GetType().Name}]: {valStr}");
                            czysteWartosci.Add(valStr); // Zbieramy czystą wartość do pamięci
                        }
                        else
                        {
                            wyniki.Add($"- Obiekt [{ent.GetType().Name}]: Brak właściwości '{propName}'");
                        }
                    }
                    catch (Exception ex)
                    {
                        wyniki.Add($"- Obiekt [{ent.GetType().Name}]: BŁĄD ({ex.Message})");
                    }
                }
                tr.Commit();
            }

            string pelnyWynik = $"WYNIK ODCZYTU WŁAŚCIWOŚCI '{propName}':\n" + string.Join("\n", wyniki);

            // Jeśli użytkownik poprosił o zapis i mamy jakieś dane - zapisujemy
            if (!string.IsNullOrEmpty(saveAs) && czysteWartosci.Count > 0)
            {
                AgentMemory.Variables[saveAs] = string.Join(" | ", czysteWartosci);
                pelnyWynik = $"[ZAPISANO W PAMIĘCI JAKO: @{saveAs}]\n" + pelnyWynik;
            }

            return pelnyWynik;
        }

        public string Execute(Document doc) => Execute(doc, "");
    }
}