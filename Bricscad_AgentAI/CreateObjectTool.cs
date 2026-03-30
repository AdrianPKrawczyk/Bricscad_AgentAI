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
        public string Description => "Tworzy nowe obiekty na rysunku. Potrafi pytać użytkownika o parametry (w tym wybór warstw z okna).";

        public string Execute(Document doc, string argsJson = "")
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                string entityType = Regex.Match(argsJson, @"\""EntityType\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                if (string.IsNullOrEmpty(entityType))
                    return "BŁĄD: Brak wymaganego parametru 'EntityType'.";

                // Sprawdzamy, czy AI chce od razu zaznaczyć ten obiekt po utworzeniu
                bool selectObject = Regex.IsMatch(argsJson, @"\""SelectObject\""\s*:\s*(true|""true"")", RegexOptions.IgnoreCase);

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord currentSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    Entity newEnt = null;

                    // ==========================================
                    // 1. TWORZENIE LINII
                    // ==========================================
                    if (entityType.Equals("Line", StringComparison.OrdinalIgnoreCase))
                    {
                        string spStr = Regex.Match(argsJson, @"\""StartPoint\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string epStr = Regex.Match(argsJson, @"\""EndPoint\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;

                        Point3d sp = Point3d.Origin;
                        if (spStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                        {
                            PromptPointResult ppr = ed.GetPoint("\n[Agent AI] Wskaż Pkt. Początkowy linii: ");
                            if (ppr.Status != PromptStatus.OK) return "BŁĄD: Anulowano rysowanie.";
                            sp = ppr.Value;
                        }
                        else sp = ParsePoint(spStr);

                        Point3d ep = Point3d.Origin;
                        if (epStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                        {
                            PromptPointOptions ppo = new PromptPointOptions("\n[Agent AI] Wskaż Pkt. Końcowy linii: ");
                            ppo.UseBasePoint = true;
                            ppo.BasePoint = sp;
                            PromptPointResult ppr = ed.GetPoint(ppo);
                            if (ppr.Status != PromptStatus.OK) return "BŁĄD: Anulowano rysowanie.";
                            ep = ppr.Value;
                        }
                        else ep = ParsePoint(epStr);

                        newEnt = new Line(sp, ep);
                    }
                    // ==========================================
                    // 2. TWORZENIE OKRĘGU (Średnica)
                    // ==========================================
                    else if (entityType.Equals("Circle", StringComparison.OrdinalIgnoreCase))
                    {
                        string cenStr = Regex.Match(argsJson, @"\""Center\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string diaStr = Regex.Match(argsJson, @"\""Diameter\""\s*:\s*([0-9.]+|""AskUser"")").Groups[1].Value.Replace("\"", "");

                        Point3d cen = Point3d.Origin;
                        if (cenStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                        {
                            PromptPointResult ppr = ed.GetPoint("\n[Agent AI] Wskaż Środek okręgu: ");
                            if (ppr.Status != PromptStatus.OK) return "BŁĄD: Anulowano rysowanie.";
                            cen = ppr.Value;
                        }
                        else cen = ParsePoint(cenStr);

                        double rad = 1.0;
                        if (diaStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                        {
                            PromptDistanceOptions pdo = new PromptDistanceOptions("\n[Agent AI] Podaj lub wskaż ŚREDNICĘ (Diameter) okręgu: ");
                            pdo.UseBasePoint = true;
                            pdo.BasePoint = cen;
                            PromptDoubleResult pdr = ed.GetDistance(pdo);
                            if (pdr.Status != PromptStatus.OK) return "BŁĄD: Anulowano podawanie średnicy.";
                            rad = pdr.Value / 2.0;
                        }
                        else
                        {
                            double dia = double.Parse(diaStr, System.Globalization.CultureInfo.InvariantCulture);
                            rad = dia / 2.0;
                        }

                        newEnt = new Circle(cen, Vector3d.ZAxis, rad);
                    }
                    // ==========================================
                    // 3. TWORZENIE TEKSTU
                    // ==========================================
                    else if (entityType.Equals("DBText", StringComparison.OrdinalIgnoreCase))
                    {
                        string posStr = Regex.Match(argsJson, @"\""Position\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string txt = Regex.Match(argsJson, @"\""Text\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string hStr = Regex.Match(argsJson, @"\""Height\""\s*:\s*([0-9.]+|""AskUser"")").Groups[1].Value.Replace("\"", "");

                        Point3d pos = Point3d.Origin;
                        if (posStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                        {
                            PromptPointResult ppr = ed.GetPoint("\n[Agent AI] Wskaż punkt wstawienia tekstu: ");
                            if (ppr.Status != PromptStatus.OK) return "BŁĄD: Anulowano wstawianie.";
                            pos = ppr.Value;
                        }
                        else pos = ParsePoint(posStr);

                        if (txt.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                        {
                            PromptStringOptions pso = new PromptStringOptions("\n[Agent AI] Wpisz treść tekstu: ");
                            pso.AllowSpaces = true;
                            PromptResult pr = ed.GetString(pso);
                            if (pr.Status != PromptStatus.OK) return "BŁĄD: Anulowano wpisywanie.";
                            txt = pr.StringResult;
                        }

                        double height = 2.5;
                        if (hStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                        {
                            PromptDistanceOptions pdo = new PromptDistanceOptions("\n[Agent AI] Podaj Wysokość tekstu: ");
                            pdo.UseBasePoint = true;
                            pdo.BasePoint = pos;
                            PromptDoubleResult pdr = ed.GetDistance(pdo);
                            if (pdr.Status != PromptStatus.OK) return "BŁĄD: Anulowano.";
                            height = pdr.Value;
                        }
                        else if (!string.IsNullOrEmpty(hStr))
                        {
                            height = double.Parse(hStr, System.Globalization.CultureInfo.InvariantCulture);
                        }

                        DBText dbText = new DBText();
                        dbText.Position = pos;
                        dbText.TextString = txt;
                        dbText.Height = height;
                        newEnt = dbText;
                    }

                    if (newEnt == null)
                        return $"BŁĄD: Nieobsługiwany typ obiektu '{entityType}'.";

                    // ==========================================
                    // 4. INTERAKTYWNA WARSTWA (Z użyciem okna z Listą!)
                    // ==========================================
                    string layerName = Regex.Match(argsJson, @"\""Layer\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                    if (layerName.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                    {
                        PromptKeywordOptions pkoLay = new PromptKeywordOptions("\n[Agent AI] Jak ustawić Warstwę?");
                        pkoLay.Keywords.Add("Istniejaca", "Istniejaca", "Istniejaca");
                        pkoLay.Keywords.Add("Nowa", "Nowa", "Nowa");
                        pkoLay.Keywords.Add("Pomin", "Pomin", "Pomin");
                        pkoLay.Keywords.Default = "Istniejaca";

                        PromptResult prLay = ed.GetKeywords(pkoLay);
                        if (prLay.Status == PromptStatus.OK)
                        {
                            if (prLay.StringResult == "Istniejaca")
                            {
                                UserChoiceTool ucTool = new UserChoiceTool();
                                string ucArgs = $"[ACTION:USER_CHOICE {{\"Question\": \"Wybierz istniejącą warstwę dla nowego obiektu:\", \"FetchTarget\": \"Property\", \"FetchScope\": \"Database\", \"FetchProperty\": \"Layer\"}}]";

                                string wynikUC = ucTool.Execute(doc, ucArgs);

                                if (wynikUC.StartsWith("WYNIK: Użytkownik wybrał opcje:"))
                                {
                                    layerName = wynikUC.Replace("WYNIK: Użytkownik wybrał opcje:", "").Trim();
                                }
                                else
                                {
                                    layerName = "";
                                }
                            }
                            else if (prLay.StringResult == "Nowa")
                            {
                                PromptStringOptions psoNew = new PromptStringOptions("\nPodaj nazwę dla NOWEJ warstwy: ");
                                psoNew.AllowSpaces = true;
                                PromptResult prNew = ed.GetString(psoNew);
                                if (prNew.Status == PromptStatus.OK && !string.IsNullOrWhiteSpace(prNew.StringResult))
                                {
                                    string newLayName = prNew.StringResult.Trim();
                                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                                    if (!lt.Has(newLayName))
                                    {
                                        LayerTableRecord newLtr = new LayerTableRecord();
                                        newLtr.Name = newLayName;
                                        lt.Add(newLtr);
                                        tr.AddNewlyCreatedDBObject(newLtr, true);
                                        ed.WriteMessage($"\n[System] Pomyślnie utworzono nową warstwę '{newLayName}'.");
                                    }
                                    layerName = newLayName;
                                }
                                else layerName = "";
                            }
                            else layerName = "";
                        }
                        else layerName = "";
                    }

                    if (!string.IsNullOrEmpty(layerName))
                    {
                        try { newEnt.Layer = layerName; } catch { }
                    }

                    // ==========================================
                    // 5. KOLOR (AskUser)
                    // ==========================================
                    string colorStr = Regex.Match(argsJson, @"\""Color\""\s*:\s*([0-9]+|""AskUser"")").Groups[1].Value.Replace("\"", "");
                    if (colorStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                    {
                        PromptStringOptions psoCol = new PromptStringOptions("\n[Agent AI] Podaj numer Koloru (1-255) lub ENTER by pominąć: ");
                        PromptResult prCol = ed.GetString(psoCol);
                        if (prCol.Status == PromptStatus.OK && int.TryParse(prCol.StringResult, out int c))
                        {
                            newEnt.ColorIndex = c;
                        }
                    }
                    else if (!string.IsNullOrEmpty(colorStr) && int.TryParse(colorStr, out int colorIndex))
                    {
                        newEnt.ColorIndex = colorIndex;
                    }

                    // Finał: Wrzucenie obiektu do Rysunku
                    currentSpace.AppendEntity(newEnt);
                    tr.AddNewlyCreatedDBObject(newEnt, true);

                    // Zapisujemy ID nowo utworzonego obiektu zanim zamkniemy transakcję
                    ObjectId createdId = newEnt.Id;

                    tr.Commit();

                    // ==========================================
                    // 6. MAGIA ZAZNACZANIA (Chaining)
                    // ==========================================
                    if (selectObject)
                    {
                        Komendy.AktywneZaznaczenie = new ObjectId[] { createdId };
                        try { ed.SetImpliedSelection(Komendy.AktywneZaznaczenie); } catch { }
                        return $"WYNIK: Pomyślnie utworzono i ZAZNACZONO obiekt {entityType}. Możesz od razu wykonać na nim kolejną akcję (np. CREATE_BLOCK).";
                    }

                    return $"WYNIK: Pomyślnie utworzono obiekt {entityType}.";
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD narzędzia CREATE_OBJECT: {ex.Message}";
            }
        }

        public string Execute(Document doc) => Execute(doc, "");

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