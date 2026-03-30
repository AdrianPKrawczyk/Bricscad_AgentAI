using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using System;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace BricsCAD_Agent
{
    public class InsertBlockTool : ITool
    {
        public string ActionTag => "[ACTION:INSERT_BLOCK]";

        // --- DODANO BRAKUJĄCY ELEMENT 1 Z INTERFEJSU ITOOL ---
        public string Description => "Wstawia nowy blok na rysunek w podanym punkcie.";

        public string Execute(Document doc, string args)
        {
            Editor ed = doc.Editor;
            try
            {
                string name = Regex.Match(args, @"\""Name\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                string positionStr = Regex.Match(args, @"\""Position\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;

                // Opcjonalna warstwa
                string layer = "";
                Match mLayer = Regex.Match(args, @"\""Layer\""\s*:\s*\""([^\""]+)\""");
                if (mLayer.Success) layer = mLayer.Groups[1].Value;

                // Opcjonalna skala (domyślnie 1.0)
                double scale = 1.0;
                Match mScale = Regex.Match(args, @"\""Scale\""\s*:\s*([0-9.]+)");
                if (mScale.Success) double.TryParse(mScale.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out scale);

                // Opcjonalny obrót (domyślnie 0)
                double rotation = 0.0;
                Match mRot = Regex.Match(args, @"\""Rotation\""\s*:\s*([0-9.]+)");
                if (mRot.Success) double.TryParse(mRot.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out rotation);

                // Interakcja z użytkownikiem
                if (name.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                {
                    PromptStringOptions pso = new PromptStringOptions("\nPodaj nazwę bloku do wstawienia: ");
                    PromptResult pr = ed.GetString(pso);
                    if (pr.Status != PromptStatus.OK) return "Anulowano podawanie nazwy bloku.";
                    name = pr.StringResult;
                }

                Point3d pt = new Point3d(0, 0, 0);
                if (positionStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                {
                    PromptPointOptions ppo = new PromptPointOptions($"\nWskaż punkt wstawienia dla bloku '{name}': ");
                    PromptPointResult ppr = ed.GetPoint(ppo);
                    if (ppr.Status != PromptStatus.OK) return "Anulowano wskazywanie punktu.";
                    pt = ppr.Value;
                }
                else
                {
                    // Parsowanie "(X,Y,Z)"
                    string cleanPt = positionStr.Replace("(", "").Replace(")", "").Replace(" ", "");
                    string[] parts = cleanPt.Split(',');
                    if (parts.Length >= 3)
                    {
                        double.TryParse(parts[0].Replace(".", ","), out double x);
                        double.TryParse(parts[1].Replace(".", ","), out double y);
                        double.TryParse(parts[2].Replace(".", ","), out double z);
                        pt = new Point3d(x, y, z);
                    }
                }

                using (DocumentLock loc = doc.LockDocument())
                {
                    using (Transaction tr = doc.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);

                        // Zabezpieczenie przed brakiem bloku
                        if (!bt.Has(name))
                        {
                            return $"BŁĄD: Blok o nazwie '{name}' nie istnieje w bazie rysunku.";
                        }

                        ObjectId btrId = bt[name];
                        BlockTableRecord currentSpace = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);

                        BlockReference br = new BlockReference(pt, btrId);
                        br.ScaleFactors = new Scale3d(scale);
                        br.Rotation = rotation * Math.PI / 180.0; // Stopnie na radiany

                        if (!string.IsNullOrEmpty(layer))
                        {
                            LayerTable lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                            if (lt.Has(layer)) br.Layer = layer;
                        }

                        currentSpace.AppendEntity(br);
                        tr.AddNewlyCreatedDBObject(br, true);

                        // Opcjonalne zaznaczanie nowo wstawionego bloku (przydatne do chainingu!)
                        Match mSelect = Regex.Match(args, @"\""SelectObject\""\s*:\s*(true|false)", RegexOptions.IgnoreCase);
                        if (mSelect.Success && mSelect.Groups[1].Value.ToLower() == "true")
                        {
                            Komendy.AktywneZaznaczenie = new ObjectId[] { br.ObjectId };
                            try { ed.SetImpliedSelection(Komendy.AktywneZaznaczenie); } catch { }
                        }

                        tr.Commit();
                    }
                }
                return $"WYNIK: Pomyślnie wstawiono blok '{name}' w punkcie ({pt.X}, {pt.Y}, {pt.Z}).";
            }
            catch (Exception ex)
            {
                return $"BŁĄD: {ex.Message}";
            }
        }

        // --- DODANO BRAKUJĄCY ELEMENT 2 Z INTERFEJSU ITOOL ---
        public string Execute(Document doc)
        {
            return "BŁĄD: Narzędzie INSERT_BLOCK wymaga argumentów w formacie JSON.";
        }
    }
}