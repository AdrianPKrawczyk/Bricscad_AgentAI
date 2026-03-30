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

                        // --- ZESTAW WIRTUALNYCH WŁAŚCIWOŚCI ---
                        if (propName.Equals("MidPoint", StringComparison.OrdinalIgnoreCase) && ent is Curve curveMid)
                        {
                            try { wartoscObiektu = curveMid.GetPointAtDist(curveMid.GetDistanceAtParameter(curveMid.EndParam) / 2.0); } catch { }
                        }
                        else if (propName.Equals("Length", StringComparison.OrdinalIgnoreCase) && ent is Curve curveLen)
                        {
                            // Niezawodne pobieranie długości dla każdego typu krzywej
                            try { wartoscObiektu = curveLen.GetDistanceAtParameter(curveLen.EndParam); } catch { }
                        }
                        else if (propName.Equals("Area", StringComparison.OrdinalIgnoreCase))
                        {
                            // Ujednolicone pobieranie pola dla krzywych zamkniętych i kreskowań
                            try
                            {
                                if (ent is Curve curveArea && curveArea.Closed) wartoscObiektu = curveArea.Area;
                                else if (ent is Hatch hatch) wartoscObiektu = hatch.Area;
                            }
                            catch { }
                        }
                        else if (propName.Equals("Volume", StringComparison.OrdinalIgnoreCase) && ent is Solid3d solidVol)
                        {
                            // Wyciąganie objętości z MassProperties
                            try { wartoscObiektu = solidVol.MassProperties.Volume; } catch { }
                        }
                        else if (propName.Equals("Centroid", StringComparison.OrdinalIgnoreCase) && ent is Solid3d solidCent)
                        {
                            // Wyciąganie środka ciężkości bryły
                            try { wartoscObiektu = solidCent.MassProperties.Centroid; } catch { }
                        }

                        else if (propName.Equals("Angle", StringComparison.OrdinalIgnoreCase) && ent is Curve curveAng)
                        {
                            try
                            {
                                // Oblicza kąt nachylenia krzywej w radianach w jej punkcie środkowym
                                Teigha.Geometry.Vector3d dir = curveAng.GetFirstDerivative(curveAng.GetParameterAtDistance(curveAng.GetDistanceAtParameter(curveAng.EndParam) / 2.0));
                                wartoscObiektu = Math.Atan2(dir.Y, dir.X);
                            }
                            catch { }
                        }

                        else
                        {
                            // Tradycyjna obsługa przez refleksję dla natywnych właściwości API
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