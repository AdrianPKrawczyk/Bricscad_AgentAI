using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    public class ModifyGeometryTool : ITool
    {
        public string ActionTag => "[ACTION:MODIFY_GEOMETRY]";
        public string Description => "Przesuwanie, Kopiowanie, Obracanie, Skalowanie oraz Usuwanie (Erase) zaznaczonych obiektów.";

        public string Execute(Document doc, string jsonArgs)
        {
            ObjectId[] ids = Komendy.AktywneZaznaczenie;
            if (ids == null || ids.Length == 0) return "WYNIK: Brak zaznaczonych obiektów do modyfikacji.";

            string mode = "Erase";
            Match mMode = Regex.Match(jsonArgs, @"\""Mode\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mMode.Success) mode = mMode.Groups[1].Value;

            // Ekstrakcja parametrów z JSON za pomocą wyrażeń regularnych
            string vectorStr = Regex.Match(jsonArgs, @"\""Vector\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase).Groups[1].Value;
            string basePtStr = Regex.Match(jsonArgs, @"\""BasePoint\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase).Groups[1].Value;
            string angleStr = Regex.Match(jsonArgs, @"\""Angle\""\s*:\s*([0-9.-]+)", RegexOptions.IgnoreCase).Groups[1].Value;
            string factorStr = Regex.Match(jsonArgs, @"\""Factor\""\s*:\s*([0-9.-]+)", RegexOptions.IgnoreCase).Groups[1].Value;

            int zmodyfikowano = 0;

            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);

                    foreach (ObjectId id in ids)
                    {
                        try
                        {
                            Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                            if (ent == null) continue;

                            if (mode.Equals("Erase", StringComparison.OrdinalIgnoreCase))
                            {
                                ent.Erase();
                                zmodyfikowano++;
                            }
                            else if (mode.Equals("Move", StringComparison.OrdinalIgnoreCase))
                            {
                                Vector3d vec = ParseVector(vectorStr);
                                ent.TransformBy(Matrix3d.Displacement(vec));
                                zmodyfikowano++;
                            }
                            else if (mode.Equals("Copy", StringComparison.OrdinalIgnoreCase))
                            {
                                Vector3d vec = ParseVector(vectorStr);
                                Entity sklonowany = ent.Clone() as Entity;
                                sklonowany.TransformBy(Matrix3d.Displacement(vec));
                                btr.AppendEntity(sklonowany);
                                tr.AddNewlyCreatedDBObject(sklonowany, true);
                                zmodyfikowano++;
                            }
                            else if (mode.Equals("Rotate", StringComparison.OrdinalIgnoreCase))
                            {
                                Point3d basePt = ParsePoint(basePtStr);
                                double angleDeg = ParseDouble(angleStr, 90.0);
                                double angleRad = angleDeg * Math.PI / 180.0;
                                ent.TransformBy(Matrix3d.Rotation(angleRad, Vector3d.ZAxis, basePt));
                                zmodyfikowano++;
                            }
                            else if (mode.Equals("Scale", StringComparison.OrdinalIgnoreCase))
                            {
                                Point3d basePt = ParsePoint(basePtStr);
                                double factor = ParseDouble(factorStr, 1.0);
                                if (factor != 0)
                                {
                                    ent.TransformBy(Matrix3d.Scaling(factor, basePt));
                                    zmodyfikowano++;
                                }
                            }
                        }
                        catch { } // Ignorujemy błędy na pojedynczych obiektach (np. zablokowana warstwa)
                    }
                    tr.Commit();
                }
            }

            if (mode.Equals("Erase", StringComparison.OrdinalIgnoreCase))
            {
                Komendy.AktywneZaznaczenie = new ObjectId[0]; // Czyścimy pamięć po usunięciu
                return $"WYNIK: Pomyślnie usunięto {zmodyfikowano} obiektów. Pamięć zaznaczenia została wyczyszczona.";
            }

            return $"WYNIK: Pomyślnie wykonano operację '{mode.ToUpper()}' na {zmodyfikowano} obiektach.";
        }

        public string Execute(Document doc) => Execute(doc, "");

        // --- Metody pomocnicze do parsowania ---
        private Vector3d ParseVector(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return new Vector3d(0, 0, 0); // Naprawiony błąd!
            string[] parts = str.Replace("(", "").Replace(")", "").Split(',');
            double x = parts.Length > 0 ? ParseDouble(parts[0], 0) : 0;
            double y = parts.Length > 1 ? ParseDouble(parts[1], 0) : 0;
            double z = parts.Length > 2 ? ParseDouble(parts[2], 0) : 0;
            return new Vector3d(x, y, z);
        }

        private Point3d ParsePoint(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return new Point3d(0, 0, 0); // Bezpieczny punkt zerowy
            string[] parts = str.Replace("(", "").Replace(")", "").Split(',');
            double x = parts.Length > 0 ? ParseDouble(parts[0], 0) : 0;
            double y = parts.Length > 1 ? ParseDouble(parts[1], 0) : 0;
            double z = parts.Length > 2 ? ParseDouble(parts[2], 0) : 0;
            return new Point3d(x, y, z);
        }

        private double ParseDouble(string str, double defaultVal)
        {
            if (double.TryParse(str.Trim().Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                return val;
            return defaultVal;
        }
    }
}