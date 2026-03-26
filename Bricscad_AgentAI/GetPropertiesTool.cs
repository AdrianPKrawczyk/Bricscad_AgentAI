using Bricscad.ApplicationServices;
using System;
using System.Reflection;
using System.Text;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace BricsCAD_Agent
{
    public class GetPropertiesTool : ITool
    {
        public string ActionTag => "[ACTION:GET_PROPERTIES]";

        public string Description =>
            "Odczytuje i zwraca wszystkie dostępne właściwości zaznaczonych obiektów (wersja FULL). Nie wymaga argumentów.";

        public string Execute(Document doc, string jsonArgs)
        {
            ObjectId[] ids = Komendy.AktywneZaznaczenie;
            if (ids == null || ids.Length == 0) return "[Błąd]: Nie mam w pamięci żadnych obiektów! Użyj najpierw tagu SELECT.";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("--- WŁAŚCIWOŚCI ZAZNACZONYCH OBIEKTÓW ---");

            // Zabezpieczenie przed zapchaniem pamięci - jeśli ktoś zaznaczy 1000 linii, zbadamy tylko pierwsze 5 sztuk
            int limitSkanowania = Math.Min(ids.Length, 5);

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                for (int i = 0; i < limitSkanowania; i++)
                {
                    Entity ent = tr.GetObject(ids[i], OpenMode.ForRead) as Entity;
                    if (ent == null) continue;

                    sb.AppendLine($"\n[{i + 1}] KLASA: {ent.GetType().Name} (Handle: {ent.Handle})");

                    // Magia Refleksji: pobieramy WSZYSTKIE publiczne właściwości tego obiektu
                    PropertyInfo[] properties = ent.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

                    // Sortujemy je alfabetycznie (konsekwencja UX)
                    Array.Sort(properties, (x, y) => string.Compare(x.Name, y.Name));

                    foreach (PropertyInfo prop in properties)
                    {
                        if (!prop.CanRead) continue;

                        try
                        {
                            Type pt = prop.PropertyType;

                            // FILTR BEZPIECZEŃSTWA: Wyciągamy tylko czytelne dla LLMa dane
                            if (pt.IsPrimitive || pt.IsEnum || pt == typeof(string) ||
                                pt == typeof(Point3d) || pt == typeof(Vector3d) ||
                                pt.Name.Contains("Color") || pt.Name.Contains("LineWeight"))
                            {
                                object val = prop.GetValue(ent, null);
                                if (val != null)
                                {
                                    // Sprytne formatowanie punktów 3D
                                    if (val is Point3d p3d)
                                        sb.AppendLine($"  - {prop.Name}: ({Math.Round(p3d.X, 3)}, {Math.Round(p3d.Y, 3)}, {Math.Round(p3d.Z, 3)})");
                                    else if (val is Vector3d v3d)
                                        sb.AppendLine($"  - {prop.Name}: [{Math.Round(v3d.X, 3)}, {Math.Round(v3d.Y, 3)}, {Math.Round(v3d.Z, 3)}]");
                                    else
                                        sb.AppendLine($"  - {prop.Name}: {val.ToString()}");
                                }
                            }
                        }
                        catch
                        {
                            // Ciche ignorowanie właściwości, które rzucają błędem 
                            // (np. gdy dany obiekt nie wspiera danej cechy mimo jej posiadania)
                        }
                    }
                }
                tr.Commit();
            }

            if (ids.Length > 5)
            {
                sb.AppendLine($"\n... oraz {ids.Length - 5} innych obiektów (odczyt ograniczony do 5 pierwszych, by nie zapchać pamięci).");
            }

            return sb.ToString();
        }

        public string Execute(Document doc) { return Execute(doc, ""); }
    }
}