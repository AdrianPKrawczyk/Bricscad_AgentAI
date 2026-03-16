// Plik: ColorRedTool.cs
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using Teigha.Colors;

namespace BricsCAD_Agent
{
    public class ColorRedTool : ITool
    {
        public string ActionTag => "[ACTION:RED_LINES]";
        public string Description => "zmienia kolory linii na czerwony";

        public void Execute(Document doc)
        {
            Database db = doc.Database;
            int count = 0;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in btr)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                    if (ent is Line || ent is Polyline || ent is Polyline3d)
                    {
                        ent.Color = Color.FromColorIndex(ColorMethod.ByColor, 1);
                        count++;
                    }
                }
                tr.Commit();
            }
            doc.Editor.WriteMessage($"\n[SYSTEM]: Zmieniono kolor {count} obiektów na czerwony.");
        }
    }
}