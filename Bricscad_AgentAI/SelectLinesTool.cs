// Plik: SelectLinesTool.cs
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Teigha.DatabaseServices;

namespace BricsCAD_Agent
{
    public class SelectLinesTool : ITool
    {
        public string ActionTag => "[ACTION:SELECT_LINES]";
        public string Description => "zaznacza wszystkie linie i polilinie";

        public void Execute(Document doc)
        {
            Editor ed = doc.Editor;
            TypedValue[] filter = new TypedValue[] {
                new TypedValue((int)DxfCode.Operator, "<or"),
                new TypedValue((int)DxfCode.Start, "LINE"),
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                new TypedValue((int)DxfCode.Start, "POLYLINE"),
                new TypedValue((int)DxfCode.Operator, "or>")
            };
            SelectionFilter selFilter = new SelectionFilter(filter);
            PromptSelectionResult selRes = ed.SelectAll(selFilter);
            if (selRes.Status == PromptStatus.OK)
            {
                ed.SetImpliedSelection(selRes.Value.GetObjectIds()); // To fizycznie podświetla obiekty
                ed.WriteMessage($"\n[SYSTEM]: Zaznaczono {selRes.Value.Count} obiektów.");
            }
        }
    }
}