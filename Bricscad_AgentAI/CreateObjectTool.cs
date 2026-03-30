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
        public string Description => "Tworzy obiekty (Line, Circle, DBText). Obsługuje RPN w polach Diameter i Height.";

        public string Execute(Document doc, string argsJson = "")
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                string entityType = Regex.Match(argsJson, @"\""EntityType\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                if (string.IsNullOrEmpty(entityType)) return "BŁĄD: Brak 'EntityType'.";

                bool selectObject = Regex.IsMatch(argsJson, @"\""SelectObject\""\s*:\s*(true|""true"")", RegexOptions.IgnoreCase);

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord currentSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    Entity newEnt = null;

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
                        string diaRaw = Regex.Match(argsJson, @"\""Diameter\""\s*:\s*(\""[^\""]+\""|[0-9.]+)").Groups[1].Value.Trim('\"');
                        Point3d cen = cenStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase) ? ed.GetPoint("\nŚrodek: ").Value : ParsePoint(cenStr);

                        double radius = 1.0;
                        if (diaRaw.Equals("AskUser", StringComparison.OrdinalIgnoreCase)) radius = ed.GetDistance("\nŚrednica: ").Value / 2.0;
                        else if (diaRaw.Contains("RPN:")) radius = double.Parse(RpnCalculator.Evaluate(diaRaw.Replace("RPN:", "").Trim()), System.Globalization.CultureInfo.InvariantCulture) / 2.0;
                        else radius = double.Parse(diaRaw.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture) / 2.0;

                        newEnt = new Circle(cen, Vector3d.ZAxis, radius);
                    }


                    // ==========================================
                    // 3. TWORZENIE DBTEXT (Zwykły tekst)
                    // ==========================================
                    else if (entityType.Equals("DBText", StringComparison.OrdinalIgnoreCase))
                    {
                        string posStr = Regex.Match(argsJson, @"\""Position\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string txt = Regex.Match(argsJson, @"\""Text\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;

                        if (txt.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                        {
                            PromptStringOptions pso = new PromptStringOptions("\n[Agent AI] Wpisz treść tekstu: ");
                            pso.AllowSpaces = true;
                            PromptResult pr = ed.GetString(pso);
                            if (pr.Status != PromptStatus.OK) return "BŁĄD: Anulowano wpisywanie.";
                            txt = pr.StringResult;
                        }
                        else if (txt.Contains("RPN:"))
                        {
                            txt = RpnCalculator.Evaluate(txt.Replace("RPN:", "").Trim());
                        }

                        var hMatch = Regex.Match(argsJson, @"\""Height\""\s*:\s*(\""[^\""]+\""|[0-9.]+)");
                        string hRaw = hMatch.Success ? hMatch.Groups[1].Value.Trim('\"') : "";

                        // --- NOWE: Pobieranie rotacji z JSON ---
                        var rotMatch = Regex.Match(argsJson, @"\""Rotation\""\s*:\s*(\""[^\""]+\""|[-0-9.]+)");
                        double rot = 0;
                        if (rotMatch.Success)
                        {
                            string rRaw = rotMatch.Groups[1].Value.Trim('\"');
                            if (rRaw.Contains("RPN:")) rot = double.Parse(RpnCalculator.Evaluate(rRaw.Replace("RPN:", "").Trim()), System.Globalization.CultureInfo.InvariantCulture);
                            else rot = double.Parse(rRaw.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                        }
                        // ---------------------------------------

                        Point3d pos = posStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase) ? ed.GetPoint("\nPunkt: ").Value : ParsePoint(posStr);

                        double h = db.Textsize;
                        if (hRaw.Equals("AskUser", StringComparison.OrdinalIgnoreCase)) h = ed.GetDistance("\nWysokość: ").Value;
                        else if (hRaw.Contains("RPN:")) h = double.Parse(RpnCalculator.Evaluate(hRaw.Replace("RPN:", "").Trim()), System.Globalization.CultureInfo.InvariantCulture);
                        else if (!string.IsNullOrEmpty(hRaw)) h = double.Parse(hRaw.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);

                        DBText dbText = new DBText { Position = pos, TextString = txt, Height = h };

                        // Przypisanie rotacji
                        dbText.Rotation = rot;

                        // Justowanie
                        if (argsJson.Contains("MiddleCenter")) { dbText.Justify = AttachmentPoint.MiddleCenter; dbText.AlignmentPoint = pos; }
                        else if (argsJson.Contains("BottomCenter")) { dbText.Justify = AttachmentPoint.BottomCenter; dbText.AlignmentPoint = pos; }

                        newEnt = dbText;
                    }
                    // ==========================================
                    // 4. TWORZENIE MTEXT (Tekst wielowierszowy)
                    // ==========================================
                    else if (entityType.Equals("MText", StringComparison.OrdinalIgnoreCase))
                    {
                        string posStr = Regex.Match(argsJson, @"\""Position\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string txt = Regex.Match(argsJson, @"\""Text\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        var hMatch = Regex.Match(argsJson, @"\""Height\""\s*:\s*(\""[^\""]+\""|[0-9.]+)");
                        string hRaw = hMatch.Success ? hMatch.Groups[1].Value.Trim('\"') : "";

                        // --- NOWE: Pobieranie rotacji z JSON ---
                        var rotMatch = Regex.Match(argsJson, @"\""Rotation\""\s*:\s*(\""[^\""]+\""|[-0-9.]+)");
                        double rot = 0;
                        if (rotMatch.Success)
                        {
                            string rRaw = rotMatch.Groups[1].Value.Trim('\"');
                            if (rRaw.Contains("RPN:")) rot = double.Parse(RpnCalculator.Evaluate(rRaw.Replace("RPN:", "").Trim()), System.Globalization.CultureInfo.InvariantCulture);
                            else rot = double.Parse(rRaw.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                        }
                        // ---------------------------------------

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
                        else if (txt.Contains("RPN:"))
                        {
                            txt = RpnCalculator.Evaluate(txt.Replace("RPN:", "").Trim());
                        }

                        txt = txt.Replace("\\\\", "\\");

                        double h = db.Textsize;
                        if (hRaw.Equals("AskUser", StringComparison.OrdinalIgnoreCase)) h = ed.GetDistance("\nWysokość: ").Value;
                        else if (hRaw.Contains("RPN:")) h = double.Parse(RpnCalculator.Evaluate(hRaw.Replace("RPN:", "").Trim()), System.Globalization.CultureInfo.InvariantCulture);
                        else if (!string.IsNullOrEmpty(hRaw)) h = double.Parse(hRaw.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);

                        MText mt = new MText();
                        mt.Location = pos;
                        mt.Contents = txt;
                        mt.TextHeight = h;

                        // Przypisanie rotacji
                        mt.Rotation = rot;

                        // Justowanie
                        if (argsJson.Contains("MiddleCenter")) { mt.Attachment = AttachmentPoint.MiddleCenter; }
                        else if (argsJson.Contains("BottomCenter")) { mt.Attachment = AttachmentPoint.BottomCenter; }

                        newEnt = mt;
                    }

                    // ==========================================
                    // 5. TWORZENIE MLEADER (Linia odniesienia)
                    // ==========================================
                    else if (entityType.Equals("MLeader", StringComparison.OrdinalIgnoreCase))
                    {
                        string arrowStr = Regex.Match(argsJson, @"\""ArrowPoint\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string landingStr = Regex.Match(argsJson, @"\""LandingPoint\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string txt = Regex.Match(argsJson, @"\""Text\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;

                        // Szukamy opcjonalnej wysokości w JSON
                        var hMatch = Regex.Match(argsJson, @"\""Height\""\s*:\s*(\""[^\""]+\""|[0-9.]+)");
                        string hRaw = hMatch.Success ? hMatch.Groups[1].Value.Trim('\"') : "";

                        // 1. Punkt Strzałki
                        Point3d arrowPt = arrowStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase)
                            ? ed.GetPoint("\n[Agent AI] Wskaż punkt STRZAŁKI: ").Value
                            : ParsePoint(arrowStr);

                        // 2. Punkt Tekstu (z GUMKĄ naprowadzającą!)
                        Point3d landingPt;
                        if (landingStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                        {
                            PromptPointOptions ppo = new PromptPointOptions("\n[Agent AI] Wskaż miejsce dla TEKSTU: ");
                            ppo.UseBasePoint = true;
                            ppo.BasePoint = arrowPt; // To narysuje linię od strzałki do Twojego kursora!
                            PromptPointResult ppr = ed.GetPoint(ppo);
                            if (ppr.Status != PromptStatus.OK) return "BŁĄD: Anulowano.";
                            landingPt = ppr.Value;
                        }
                        else if (!string.IsNullOrEmpty(landingStr))
                        {
                            landingPt = ParsePoint(landingStr);
                        }
                        else
                        {
                            landingPt = new Point3d(arrowPt.X + 10, arrowPt.Y + 10, arrowPt.Z);
                        }

                        // 3. Treść Tekstu (AskUser jako inteligentny wypełniacz / wildcard)
                        if (txt.IndexOf("AskUser", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            PromptStringOptions pso = new PromptStringOptions("\n[Agent AI] Wpisz brakujący fragment tekstu: ");
                            pso.AllowSpaces = true;
                            PromptResult pr = ed.GetString(pso);
                            if (pr.Status != PromptStatus.OK) return "BŁĄD: Anulowano wpisywanie.";

                            // Podmieniamy słowo "AskUser" wewnątrz ciągu na to, co wpisałeś
                            txt = Regex.Replace(txt, "AskUser", pr.StringResult, RegexOptions.IgnoreCase);

                            // Obliczamy RPN (jeśli występuje) dopiero po podstawieniu Twojej wartości
                            if (txt.Contains("RPN:")) txt = RpnCalculator.Evaluate(txt.Replace("RPN:", "").Trim());
                        }
                        else if (txt.Contains("RPN:"))
                        {
                            txt = RpnCalculator.Evaluate(txt.Replace("RPN:", "").Trim());
                        }

                        MLeader ml = new MLeader();
                        ml.SetDatabaseDefaults(); // Pobiera aktualny styl
                        ml.ContentType = ContentType.MTextContent;

                        // Pobieramy wysokość ze stylu jako bazę
                        double finalHeight = 2.5;
                        using (MLeaderStyle mlStyle = (MLeaderStyle)tr.GetObject(db.MLeaderstyle, OpenMode.ForRead))
                        {
                            finalHeight = mlStyle.TextHeight;
                        }

                        // Jeśli w tagu JSON podano inną wysokość - nadpisujemy styl
                        if (!string.IsNullOrEmpty(hRaw) && !hRaw.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                        {
                            finalHeight = double.Parse(hRaw.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                        }

                        // Konwersja podwójnych ukośników z JSON na pojedyncze dla formatowania MText
                        txt = txt.Replace("\\\\", "\\");
                        MText mt = new MText { Contents = txt, Height = finalHeight };
                        ml.MText = mt;
                        ml.TextHeight = finalHeight; // KLUCZOWE: Ustawia wysokość widoczną

                        int leadIdx = ml.AddLeader();
                        int lineIdx = ml.AddLeaderLine(leadIdx);
                        ml.AddFirstVertex(lineIdx, arrowPt);
                        ml.AddLastVertex(lineIdx, landingPt);

                        newEnt = ml;
                    }

                    
                    if (newEnt != null)
                    {
                        string layer = Regex.Match(argsJson, @"\""Layer\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        if (!string.IsNullOrEmpty(layer)) try { newEnt.Layer = layer; } catch { }

                        currentSpace.AppendEntity(newEnt);
                        tr.AddNewlyCreatedDBObject(newEnt, true);
                        ObjectId id = newEnt.Id;
                        tr.Commit();
                        if (selectObject) { Komendy.AktywneZaznaczenie = new ObjectId[] { id }; ed.SetImpliedSelection(Komendy.AktywneZaznaczenie); }
                        return $"WYNIK: Utworzono {entityType}.";
                    }
                    return "BŁĄD: Nieobsługiwany typ.";
                }
            }
            catch (Exception ex) { return $"BŁĄD: {ex.Message}"; }
        }
        public string Execute(Document doc) => Execute(doc, "");
        private Point3d ParsePoint(string s)
        {
            s = s.Replace("(", "").Replace(")", "").Trim(); string[] p = s.Split(',');
            return new Point3d(double.Parse(p[0], CultureInfo.InvariantCulture), double.Parse(p[1], CultureInfo.InvariantCulture), p.Length > 2 ? double.Parse(p[2], CultureInfo.InvariantCulture) : 0);
        }
    }
}