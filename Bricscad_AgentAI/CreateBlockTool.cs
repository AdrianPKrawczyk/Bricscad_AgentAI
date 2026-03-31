using System;
using System.Text.RegularExpressions;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace BricsCAD_Agent
{
    public class CreateBlockTool : ITool
    {
        public string ActionTag => "[ACTION:CREATE_BLOCK]";
        public string Description => "Tworzy nowy blok z aktualnie zaznaczonych obiektów.";

        public string Execute(Document doc, string argsJson = "")
        {
            Database db = doc.Database;
            Editor ed = doc.Editor;

            // Sprawdzamy czy AI pamiętało, żeby najpierw coś zaznaczyć
            if (Komendy.AktywneZaznaczenie == null || Komendy.AktywneZaznaczenie.Length == 0)
            {
                return "BŁĄD: Brak zaznaczonych obiektów. Najpierw użyj tagu [SELECT: ...].";
            }

            try
            {
                string blockName = Regex.Match(argsJson, @"\""Name\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
                string basePointStr = Regex.Match(argsJson, @"\""BasePoint\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;

                // 1. Pobieranie nazwy bloku (jeśli AI przekazało "AskUser" lub nic nie wpisało)
                if (string.IsNullOrEmpty(blockName) || blockName.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                {
                    PromptStringOptions pso = new PromptStringOptions("\n[Agent AI] Podaj nazwę dla nowego bloku: ");
                    pso.AllowSpaces = true;
                    PromptResult pr = ed.GetString(pso);
                    if (pr.Status != PromptStatus.OK || string.IsNullOrWhiteSpace(pr.StringResult))
                        return "BŁĄD: Anulowano wprowadzanie nazwy bloku.";
                    blockName = pr.StringResult.Trim();
                }

                // 2. Pobieranie punktu bazowego (jeśli "AskUser", to pozwalamy kliknąć na ekranie)
                Point3d basePt = Point3d.Origin;
                if (string.IsNullOrEmpty(basePointStr) || basePointStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
                {
                    PromptPointResult ppr = ed.GetPoint("\n[Agent AI] Wskaż punkt bazowy dla bloku: ");
                    if (ppr.Status != PromptStatus.OK)
                        return "BŁĄD: Anulowano wybór punktu bazowego.";
                    basePt = ppr.Value;
                }
                else
                {
                    basePt = ParsePoint(basePointStr);
                }

                // 3. Właściwa logika Bazy Danych
                using (DocumentLock dl = doc.LockDocument()) // <--- DODANA LOKALNA BLOKADA
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

                        // Zabezpieczenie przed nadpisaniem istniejącego bloku
                        if (bt.Has(blockName))
                    {
                        return $"BŁĄD: Blok o nazwie '{blockName}' już istnieje w rysunku.";
                    }

                    bt.UpgradeOpen();

                    // Tworzenie "Pojemnika" na blok w pamięci rysunku
                    BlockTableRecord btr = new BlockTableRecord();
                    btr.Name = blockName;
                    btr.Origin = basePt;
                    ObjectId btrId = bt.Add(btr);
                    tr.AddNewlyCreatedDBObject(btr, true);

                    // Klonowanie zaznaczonych obiektów do wnętrza tego pojemnika
                    ObjectIdCollection idsToClone = new ObjectIdCollection(Komendy.AktywneZaznaczenie);
                    IdMapping mapping = new IdMapping();
                    db.DeepCloneObjects(idsToClone, btrId, mapping, false);

                    // Wstawienie referencji (odniesienia) bloku w miejsce starych obiektów
                    BlockTableRecord currentSpace = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                    BlockReference bref = new BlockReference(basePt, btrId);
                    currentSpace.AppendEntity(bref);
                    tr.AddNewlyCreatedDBObject(bref, true);

                    // Usunięcie oryginalnych, luźnych obiektów
                    foreach (ObjectId id in Komendy.AktywneZaznaczenie)
                    {
                        Entity ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                        ent.Erase();
                    }

                    tr.Commit();

                        // Czyścimy pamięć zaznaczenia, bo oryginalne obiekty wyleciały z rysunku
                        Komendy.AktywneZaznaczenie = new ObjectId[0];

                        return $"WYNIK: Pomyślnie utworzono blok '{blockName}' z {idsToClone.Count} obiektów i zamieniono zaznaczenie na to odniesienie bloku.";
                    } // To zamyka Transaction
                } // <--- TUTAJ DODAJ BRAKUJĄCĄ KLAMRĘ (Ona zamyka DocumentLock)
            } // To zamyka blok try
            catch (Exception ex)
            {
                return $"BŁĄD narzędzia CREATE_BLOCK: {ex.Message}";
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