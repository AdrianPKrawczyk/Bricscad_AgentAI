using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace BricsCAD_Agent
{
    public class CreateObjectTool : ITool
    {
        public string ActionTag => "[ACTION:CREATE_OBJECT]";
        public string Description => "Tworzy obiekty (Line, Circle, DBText, MText, MLeader). Obsługuje RPN i dynamiczne parametry.";

        public string Execute(Document doc, string argsJson = "")
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                string entityType = Regex.Match(argsJson, @"\""EntityType\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                if (string.IsNullOrEmpty(entityType)) return "BŁĄD: Brak 'EntityType'.";

                bool selectObject = Regex.IsMatch(argsJson, @"\""SelectObject\""\s*:\s*(true|""true"")", RegexOptions.IgnoreCase);
                Entity newEnt = null;

                // =========================================================
                // FAZA 1: POBIERANIE DANYCH OD UŻYTKOWNIKA (BEZ BLOKAD!)
                // =========================================================
                if (entityType.Equals("Line", StringComparison.OrdinalIgnoreCase))
                {
                    string spStr = Regex.Match(argsJson, @"\""StartPoint\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                    string epStr = Regex.Match(argsJson, @"\""EndPoint\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                    Point3d sp = spStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase) ? ed.GetPoint("\nStart: ").Value : ParsePoint(spStr);
                    Point3d ep = epStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase) ? ed.GetPoint("\nKoniec: ").Value : ParsePoint(epStr);
                    newEnt = new Line(sp, ep);
                }
                else if (entityType.Equals("Circle", StringComparison.OrdinalIgnoreCase))
                {
                    string cenStr = Regex.Match(argsJson, @"\""Center\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                    Point3d cen = cenStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase) ? ed.GetPoint("\nŚrodek: ").Value : ParsePoint(cenStr);

                    double radius = 1.0;

                    // Szukamy w JSON-ie i Promienia (Radius) i Średnicy (Diameter)
                    Match radMatch = Regex.Match(argsJson, @"\""Radius\""\s*:\s*(\""[^\""]+\""|[0-9.]+)");
                    Match diaMatch = Regex.Match(argsJson, @"\""Diameter\""\s*:\s*(\""[^\""]+\""|[0-9.]+)");

                    if (radMatch.Success)
                    {
                        string radRaw = radMatch.Groups[1].Value.Trim('\"');
                        if (radRaw.Equals("AskUser", StringComparison.OrdinalIgnoreCase)) radius = ed.GetDistance("\nPromień: ").Value;
                        else if (radRaw.Contains("RPN:")) { if (double.TryParse(RpnCalculator.Evaluate(radRaw.Replace("RPN:", "").Trim()).Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double r)) radius = r; }
                        else { if (double.TryParse(radRaw.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double r)) radius = r; }
                    }
                    else if (diaMatch.Success)
                    {
                        string diaRaw = diaMatch.Groups[1].Value.Trim('\"');
                        if (diaRaw.Equals("AskUser", StringComparison.OrdinalIgnoreCase)) radius = ed.GetDistance("\nŚrednica: ").Value / 2.0;
                        else if (diaRaw.Contains("RPN:")) { if (double.TryParse(RpnCalculator.Evaluate(diaRaw.Replace("RPN:", "").Trim()).Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double r)) radius = r / 2.0; }
                        else { if (double.TryParse(diaRaw.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double r)) radius = r / 2.0; }
                    }

                    newEnt = new Circle(cen, Vector3d.ZAxis, radius);
                }
                // DBTEXT & MTEXT
                else if (entityType.Equals("DBText", StringComparison.OrdinalIgnoreCase) || entityType.Equals("MText", StringComparison.OrdinalIgnoreCase))
                {
                    string posStr = Regex.Match(argsJson, @"\""Position\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                    string txt = Regex.Match(argsJson, @"\""Text\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                    Point3d pos = posStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase) ? ed.GetPoint("\n[Agent AI] Wskaż punkt wstawienia tekstu: ").Value : ParsePoint(posStr);

                    if (txt.IndexOf("AskUser", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        PromptStringOptions pso = new PromptStringOptions("\n[Agent AI] Wpisz treść tekstu (użyj \\P dla nowej linii): ");
                        pso.AllowSpaces = true;
                        PromptResult pr = ed.GetString(pso);
                        if (pr.Status != PromptStatus.OK) return "BŁĄD: Anulowano wpisywanie.";
                        txt = Regex.Replace(txt, "AskUser", pr.StringResult, RegexOptions.IgnoreCase);
                        if (txt.Contains("RPN:")) txt = RpnCalculator.Evaluate(txt.Replace("RPN:", "").Trim());
                    }
                    else if (txt.Contains("RPN:")) txt = RpnCalculator.Evaluate(txt.Replace("RPN:", "").Trim());
                    txt = txt.Replace("\\\\", "\\");

                    var hMatch = Regex.Match(argsJson, @"\""Height\""\s*:\s*(\""[^\""]+\""|[0-9.]+)");
                    string hRaw = hMatch.Success ? hMatch.Groups[1].Value.Trim('\"') : "";
                    double h = db.Textsize; // Domyślny rozmiar (można czytać z bazy bez blokady)
                    if (hRaw.Equals("AskUser", StringComparison.OrdinalIgnoreCase)) h = ed.GetDistance("\nWysokość: ").Value;
                    else if (hRaw.Contains("RPN:")) { if (double.TryParse(RpnCalculator.Evaluate(hRaw.Replace("RPN:", "").Trim()).Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double hVal)) h = hVal; }
                    else if (!string.IsNullOrEmpty(hRaw)) { if (double.TryParse(hRaw.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double hVal)) h = hVal; }

                    var rotMatch = Regex.Match(argsJson, @"\""Rotation\""\s*:\s*(\""[^\""]+\""|[-0-9.]+)");
                    double rot = 0;
                    if (rotMatch.Success)
                    {
                        string rRaw = rotMatch.Groups[1].Value.Trim('\"');
                        if (rRaw.Contains("RPN:")) { if (double.TryParse(RpnCalculator.Evaluate(rRaw.Replace("RPN:", "").Trim()).Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double rVal)) rot = rVal; }
                        else { if (double.TryParse(rRaw.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double rVal)) rot = rVal; }
                    }

                    if (entityType.Equals("DBText", StringComparison.OrdinalIgnoreCase))
                    {
                        DBText dbText = new DBText { Position = pos, TextString = txt, Height = h, Rotation = rot };
                        if (argsJson.Contains("MiddleCenter")) { dbText.Justify = AttachmentPoint.MiddleCenter; dbText.AlignmentPoint = pos; }
                        else if (argsJson.Contains("BottomCenter")) { dbText.Justify = AttachmentPoint.BottomCenter; dbText.AlignmentPoint = pos; }
                        newEnt = dbText;
                    }
                    else
                    {
                        MText mt = new MText { Location = pos, Contents = txt, TextHeight = h, Rotation = rot };
                        if (argsJson.Contains("MiddleCenter")) { mt.Attachment = AttachmentPoint.MiddleCenter; }
                        else if (argsJson.Contains("BottomCenter")) { mt.Attachment = AttachmentPoint.BottomCenter; }
                        newEnt = mt;
                    }
                }
                // MLEADER
                else if (entityType.Equals("MLeader", StringComparison.OrdinalIgnoreCase))
                {
                    string arrowStr = Regex.Match(argsJson, @"\""ArrowPoint\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                    string landingStr = Regex.Match(argsJson, @"\""LandingPoint\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                    string txt = Regex.Match(argsJson, @"\""Text\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;

                    var hMatch = Regex.Match(argsJson, @"\""Height\""\s*:\s*(\""[^\""]+\""|[0-9.]+)");
                    string hRaw = hMatch.Success ? hMatch.Groups[1].Value.Trim('\"') : "";

                    Point3d arrowPt = arrowStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase) ? ed.GetPoint("\n[Agent AI] Wskaż punkt STRZAŁKI: ").Value : ParsePoint(arrowStr);
                    Point3d landingPt;
                    if (landingStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                    {
                        PromptPointOptions ppo = new PromptPointOptions("\n[Agent AI] Wskaż miejsce dla TEKSTU: ");
                        ppo.UseBasePoint = true; ppo.BasePoint = arrowPt;
                        PromptPointResult ppr = ed.GetPoint(ppo);
                        if (ppr.Status != PromptStatus.OK) return "BŁĄD: Anulowano.";
                        landingPt = ppr.Value;
                    }
                    else if (!string.IsNullOrEmpty(landingStr)) landingPt = ParsePoint(landingStr);
                    else landingPt = new Point3d(arrowPt.X + 10, arrowPt.Y + 10, arrowPt.Z);

                    if (txt.IndexOf("AskUser", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        PromptStringOptions pso = new PromptStringOptions("\n[Agent AI] Wpisz treść opisu (użyj \\P dla nowej linii): ");
                        pso.AllowSpaces = true;
                        PromptResult pr = ed.GetString(pso);
                        if (pr.Status != PromptStatus.OK) return "BŁĄD: Anulowano wpisywanie.";
                        txt = Regex.Replace(txt, "AskUser", pr.StringResult, RegexOptions.IgnoreCase);
                        if (txt.Contains("RPN:")) txt = RpnCalculator.Evaluate(txt.Replace("RPN:", "").Trim());
                    }
                    else if (txt.Contains("RPN:")) txt = RpnCalculator.Evaluate(txt.Replace("RPN:", "").Trim());
                    txt = txt.Replace("\\\\", "\\");

                    MLeader ml = new MLeader();
                    ml.SetDatabaseDefaults();
                    ml.ContentType = ContentType.MTextContent;

                    double finalHeight = 2.5;
                    if (!string.IsNullOrEmpty(hRaw) && !hRaw.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                    {
                        if (double.TryParse(hRaw.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double hVal)) finalHeight = hVal;
                    }

                    MText mt = new MText { Contents = txt, Height = finalHeight };
                    ml.MText = mt;
                    ml.TextHeight = finalHeight;

                    int leadIdx = ml.AddLeader();
                    int lineIdx = ml.AddLeaderLine(leadIdx);
                    ml.AddFirstVertex(lineIdx, arrowPt);
                    ml.AddLastVertex(lineIdx, landingPt);

                    newEnt = ml;
                }

                // =========================================================
                // FAZA 2: ZAPIS DO BAZY (TUTAJ JEST LOKALNA BLOKADA I TRANSAKCJA)
                // =========================================================
                if (newEnt != null)
                {
                    using (DocumentLock dl = doc.LockDocument()) // <--- LOKALNA BLOKADA
                    using (Transaction tr = db.TransactionManager.StartTransaction()) // <--- TRANSAKCJA
                    {
                        BlockTableRecord currentSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                        string layer = Regex.Match(argsJson, @"\""Layer\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        if (!string.IsNullOrEmpty(layer)) try { newEnt.Layer = layer; } catch { }

                        currentSpace.AppendEntity(newEnt);
                        tr.AddNewlyCreatedDBObject(newEnt, true);
                        ObjectId id = newEnt.Id;
                        tr.Commit();

                        if (selectObject)
                        {
                            Komendy.AktywneZaznaczenie = new ObjectId[] { id };
                            ed.SetImpliedSelection(Komendy.AktywneZaznaczenie);
                        }
                        return $"WYNIK: Utworzono {entityType}.";
                    }
                }

                return "BŁĄD: Nieobsługiwany typ.";
            }
            catch (Exception ex) { return $"BŁĄD: {ex.Message}"; }
        }

        public string Execute(Document doc) => Execute(doc, "");

        // --- SUPER BEZPIECZNE PARSOWANIE PUNKTÓW (Odporne na błędy) ---
        private Point3d ParsePoint(string s)
        {
            s = s.Replace("(", "").Replace(")", "").Trim(); string[] p = s.Split(',');
            double x = p.Length > 0 && double.TryParse(p[0].Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double px) ? px : 0;
            double y = p.Length > 1 && double.TryParse(p[1].Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double py) ? py : 0;
            double z = p.Length > 2 && double.TryParse(p[2].Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double pz) ? pz : 0;
            return new Point3d(x, y, z);
        }
    }
}