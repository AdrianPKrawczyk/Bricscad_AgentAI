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

            List<string> wyniki = new List<string>();

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

                            // Ładne formatowanie dla punktów 3D
                            if (wartoscObiektu is Teigha.Geometry.Point3d pt)
                                valStr = $"({Math.Round(pt.X, 4)},{Math.Round(pt.Y, 4)},{Math.Round(pt.Z, 4)})";
                            else if (wartoscObiektu is Teigha.Colors.Color col)
                                valStr = col.ColorIndex.ToString();

                            wyniki.Add($"- Obiekt [{ent.GetType().Name}]: {valStr}");
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

            return $"WYNIK ODCZYTU WŁAŚCIWOŚCI '{propName}':\n" + string.Join("\n", wyniki);
        }

        public string Execute(Document doc) => Execute(doc, "");
    }
}