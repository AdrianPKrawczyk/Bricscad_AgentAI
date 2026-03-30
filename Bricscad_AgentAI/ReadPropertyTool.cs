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
        public string Description => "Odczytuje konkretną pojedynczą właściwość (np. Center, Radius) z zaznaczonych obiektów.";

        public string Execute(Document doc, string jsonArgs)
        {
            ObjectId[] ids = Komendy.AktywneZaznaczenie;
            if (ids == null || ids.Length == 0) return "WYNIK: Brak zaznaczonych obiektów.";

            string propName = "";
            Match mProp = Regex.Match(jsonArgs, @"\""Property\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mProp.Success) propName = mProp.Groups[1].Value;

            if (string.IsNullOrEmpty(propName)) return "WYNIK: Nie podano nazwy właściwości (Property) do odczytania w tagu JSON.";

            // --- SEKCJA PAMIĘCI (Zadeklarowana tylko RAZ) ---
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
                        // Obsługa zagnieżdżeń (np. Center.X)
                        string[] zagniezdzenia = propName.Split('.');
                        object wartoscObiektu = ent;
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

                        if (wartoscObiektu != null)
                        {
                            string valStr = wartoscObiektu.ToString();

                            // Ładne formatowanie dla punktów 3D z wymuszeniem kropki dziesiętnej!
                            if (wartoscObiektu is Teigha.Geometry.Point3d pt)
                            {
                                string x = pt.X.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
                                string y = pt.Y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
                                string z = pt.Z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
                                valStr = $"({x},{y},{z})";
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