using System;
using System.Text.RegularExpressions;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace BricsCAD_Agent
{
    public class CreateObjectTool : ITool
    {
        public string ActionTag => "[ACTION:CREATE_OBJECT]";

        public string Execute(Document doc, string argsJson = "")
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                // Odczytujemy jakiego typu obiekt Agent chce narysować
                string entityType = Regex.Match(argsJson, @"\""EntityType\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                if (string.IsNullOrEmpty(entityType))
                    return "BŁĄD: Brak wymaganego parametru 'EntityType'.";

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // Pobieramy aktualną przestrzeń roboczą (Model lub Arkusz), do której wrzucimy nowy obiekt
                    BlockTableRecord currentSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    Entity newEnt = null;

                    // === 1. TWORZENIE LINII ===
                    if (entityType.Equals("Line", StringComparison.OrdinalIgnoreCase))
                    {
                        string spStr = Regex.Match(argsJson, @"\""StartPoint\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string epStr = Regex.Match(argsJson, @"\""EndPoint\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;

                        Point3d sp = ParsePoint(spStr);
                        Point3d ep = ParsePoint(epStr);
                        newEnt = new Line(sp, ep);
                    }
                    // === 2. TWORZENIE OKRĘGU ===
                    else if (entityType.Equals("Circle", StringComparison.OrdinalIgnoreCase))
                    {
                        string cenStr = Regex.Match(argsJson, @"\""Center\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string radStr = Regex.Match(argsJson, @"\""Radius\""\s*:\s*([0-9.]+)").Groups[1].Value;

                        Point3d cen = ParsePoint(cenStr);
                        double rad = double.Parse(radStr, System.Globalization.CultureInfo.InvariantCulture);
                        newEnt = new Circle(cen, Vector3d.ZAxis, rad);
                    }
                    // === 3. TWORZENIE TEKSTU ===
                    else if (entityType.Equals("DBText", StringComparison.OrdinalIgnoreCase))
                    {
                        string posStr = Regex.Match(argsJson, @"\""Position\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string txt = Regex.Match(argsJson, @"\""Text\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string hStr = Regex.Match(argsJson, @"\""Height\""\s*:\s*([0-9.]+)").Groups[1].Value;

                        Point3d pos = ParsePoint(posStr);
                        double height = string.IsNullOrEmpty(hStr) ? 2.5 : double.Parse(hStr, System.Globalization.CultureInfo.InvariantCulture);

                        DBText dbText = new DBText();
                        dbText.Position = pos;
                        dbText.TextString = txt;
                        dbText.Height = height;
                        newEnt = dbText;
                    }

                    // Jeśli LLM podał złą klasę
                    if (newEnt == null)
                        return $"BŁĄD: Nieobsługiwany lub nierozpoznany typ obiektu '{entityType}'. Dozwolone to: Line, Circle, DBText.";

                    // === OPCJONALNE: Wspólne właściwości (Warstwa i Kolor) ===
                    Match mLayer = Regex.Match(argsJson, @"\""Layer\""\s*:\s*\""([^\""]+)\""");
                    if (mLayer.Success)
                    {
                        try { newEnt.Layer = mLayer.Groups[1].Value; } catch { /* Ignoruj jeśli warstwa nie istnieje */ }
                    }

                    Match mColor = Regex.Match(argsJson, @"\""Color\""\s*:\s*([0-9]+)");
                    if (mColor.Success)
                    {
                        newEnt.ColorIndex = int.Parse(mColor.Groups[1].Value);
                    }

                    // Fizyczne dodanie do bazy rysunku
                    currentSpace.AppendEntity(newEnt);
                    tr.AddNewlyCreatedDBObject(newEnt, true);
                    tr.Commit();

                    return $"WYNIK: Pomyślnie utworzono nowy obiekt {entityType}.";
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD narzędzia CREATE_OBJECT: {ex.Message}";
            }
        }

        public string Execute(Document doc)
        {
            return Execute(doc, "");
        }

        // Pomocnicza funkcja do tłumaczenia stringa np. "(100,50,0)" na obiekt punktu 3D
        private Point3d ParsePoint(string ptStr)
        {
            if (string.IsNullOrEmpty(ptStr)) return new Point3d(0, 0, 0);
            ptStr = ptStr.Replace("(", "").Replace(")", "").Trim();
            string[] parts = ptStr.Split(',');

            double x = parts.Length > 0 ? double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) : 0;
            double y = parts.Length > 1 ? double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 0;
            double z = parts.Length > 2 ? double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) : 0;

            return new Point3d(x, y, z);
        }
    }
}