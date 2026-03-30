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
                            // Jeśli tekst zawiera RPN, przeliczamy go (np. dla numeracji z offsetem)
                            txt = RpnCalculator.Evaluate(txt.Replace("RPN:", "").Trim());
                        }
                        string hRaw = Regex.Match(argsJson, @"\""Height\""\s*:\s*(\""[^\""]+\""|[0-9.]+)").Groups[1].Value.Trim('\"');
                        Point3d pos = posStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase) ? ed.GetPoint("\nPunkt: ").Value : ParsePoint(posStr);

                        // --- POPRAWKA: Dynamiczna wysokość ---
                        double h = db.Textsize; // Pobiera aktualną wartość zmiennej TEXTSIZE z rysunku

                        if (hRaw.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                        {
                            h = ed.GetDistance("\nWysokość: ").Value;
                        }
                        else if (hRaw.Contains("RPN:"))
                        {
                            h = double.Parse(RpnCalculator.Evaluate(hRaw.Replace("RPN:", "").Trim()), System.Globalization.CultureInfo.InvariantCulture);
                        }
                        else if (!string.IsNullOrEmpty(hRaw))
                        {
                            // Jeśli podano konkretną liczbę w JSON, używamy jej
                            h = double.Parse(hRaw.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                        }
                        // Jeśli hRaw jest puste, h pozostaje równe db.Textsize

                        newEnt = new DBText { Position = pos, TextString = txt, Height = h };
                    }

                    // ==========================================
                    // 4. TWORZENIE MLEADER (Linia odniesienia)
                    // ==========================================
                    else if (entityType.Equals("MLeader", StringComparison.OrdinalIgnoreCase))
                    {
                        string arrowStr = Regex.Match(argsJson, @"\""ArrowPoint\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string landingStr = Regex.Match(argsJson, @"\""LandingPoint\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                        string txt = Regex.Match(argsJson, @"\""Text\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;

                        // Szukamy opcjonalnej wysokości w JSON
                        var hMatch = Regex.Match(argsJson, @"\""Height\""\s*:\s*(\""[^\""]+\""|[0-9.]+)");
                        string hRaw = hMatch.Success ? hMatch.Groups[1].Value.Trim('\"') : "";

                        Point3d arrowPt = arrowStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase) ? ed.GetPoint("\nStrzałka: ").Value : ParsePoint(arrowStr);
                        Point3d landingPt = landingStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase) ? ed.GetPoint("\nTekst: ").Value : ParsePoint(landingStr);

                        if (txt.Contains("RPN:")) txt = RpnCalculator.Evaluate(txt.Replace("RPN:", "").Trim());

                        MLeader ml = new MLeader();
                        ml.SetDatabaseDefaults(); // Pobiera aktualny styl
                        ml.ContentType = ContentType.MTextContent;

                        // Pobieramy wysokość ze stylu jako bazę
                        double finalHeight = 2.5;
                        using (MLeaderStyle mlStyle = (MLeaderStyle)tr.GetObject(db.MLeaderstyle, OpenMode.ForRead))
                        {
                            finalHeight = mlStyle.TextHeight; // Tu powinno być Twoje 20
                        }

                        // Jeśli w tagu JSON podano inną wysokość - nadpisujemy styl
                        if (!string.IsNullOrEmpty(hRaw) && !hRaw.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                        {
                            finalHeight = double.Parse(hRaw.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
                        }

                        MText mt = new MText { Contents = txt };
                        ml.MText = mt;
                        ml.TextHeight = finalHeight; // KLUCZOWE: To ustawia widoczną wysokość

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