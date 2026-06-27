using System;
using System.Globalization;
using System.Windows.Forms.Integration;
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad.Windows;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Runtime;
using WentCad.Core;
using WentCad.Jigs;
using WentCad.Reactors;

[assembly: CommandClass(typeof(WentCad.UI.PaletteSetManager))]

namespace WentCad.UI
{
    public class PaletteSetManager
    {
        private static readonly Guid PaletteGuid = new Guid("A7369F37-90E5-48A0-A2DB-1C9960C87F1A");
        private static PaletteSet _paletteSet;
        private static MainView _mainView;

        public static MainView MainViewInstance => _mainView;

        [CommandMethod("WENTCAD")]
        public static void ShowPalette()
        {
            if (_paletteSet == null)
            {
                _paletteSet = new PaletteSet("WentCad", PaletteGuid)
                {
                    Style = PaletteSetStyles.ShowCloseButton | PaletteSetStyles.ShowPropertiesMenu | PaletteSetStyles.ShowAutoHideButton
                };

                _mainView = new MainView();
                var host = new ElementHost
                {
                    AutoSize = true,
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Child = _mainView
                };
                _paletteSet.Add("WentCad", host);

                Application.DocumentManager.DocumentActivated += (s, e) => _mainView.LoadActiveDocument();
            }

            _mainView.LoadActiveDocument();
            FloorRegionReactor.Register();
            _paletteSet.Visible = true;
        }

        [CommandMethod("WENTCAD_SYNC")]
        public static void Sync()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var project = ProjectFileService.LoadOrCreate(doc);
            BalanceEngine.Recalculate(project);
            ProjectFileService.Save(doc, project);
            NodManager.SaveProjectIndex(doc, project);
            GeometryManager.WriteRoomXData(doc, project);
            _mainView?.LoadActiveDocument();
            doc.Editor.WriteMessage("\nWentCad: zsynchronizowano .wentcad, NOD i XData.");
        }

        [CommandMethod("WENTCAD_DRAW_FLOOR_REGION")]
        public static void DrawFloorRegion()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var floor = _mainView?.ViewModel.SelectedFloor;
            if (doc == null || floor == null)
            {
                doc?.Editor.WriteMessage("\nWentCad: wybierz kondygnacje w panelu.");
                return;
            }

            var editor = doc.Editor;
            var start = editor.GetPoint("\nWskaz pierwszy naroznik zakresu kondygnacji: ");
            if (start.Status != PromptStatus.OK) return;

            var jig = new FloorRegionDrawJig(start.Value);
            var drag = editor.Drag(jig);
            if (drag.Status != PromptStatus.OK) return;

            using (doc.LockDocument())
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                GeometryManager.EnsureLayer(doc.Database, tr, WentCadConstants.FloorRegionLayer, 3, false, false);
                GeometryManager.EnsureRegApp(doc.Database, tr, WentCadConstants.FloorRegionRegApp);

                var polyline = jig.GetEntity();
                polyline.Layer = WentCadConstants.FloorRegionLayer;
                polyline.XData = new ResultBuffer(
                    new TypedValue((short)DxfCode.ExtendedDataRegAppName, WentCadConstants.FloorRegionRegApp),
                    new TypedValue((short)DxfCode.ExtendedDataAsciiString, floor.FloorId));

                var blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                var model = (BlockTableRecord)tr.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                model.AppendEntity(polyline);
                tr.AddNewlyCreatedDBObject(polyline, true);
                floor.Region = GeometryManager.PolylineToPoints(polyline);
                floor.DrukWidokiViewId = null;
                tr.Commit();
            }

            _mainView.ViewModel.Save();
            _mainView.LoadActiveDocument();
        }

        [CommandMethod("WENTCAD_PICK_FLOOR_REGION")]
        public static void PickFloorRegion()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var floor = _mainView?.ViewModel.SelectedFloor;
            if (doc == null || floor == null)
            {
                doc?.Editor.WriteMessage("\nWentCad: wybierz kondygnacje w panelu.");
                return;
            }

            var options = new PromptEntityOptions("\nWybierz zamknieta polilinie zakresu kondygnacji: ");
            options.SetRejectMessage("\nWentCad: wybierz obiekt typu Polyline.");
            options.AddAllowedClass(typeof(Polyline), true);
            var result = doc.Editor.GetEntity(options);
            if (result.Status != PromptStatus.OK) return;

            using (doc.LockDocument())
            using (var tr = doc.TransactionManager.StartTransaction())
            {
                GeometryManager.EnsureLayer(doc.Database, tr, WentCadConstants.FloorRegionLayer, 3, false, false);
                GeometryManager.EnsureRegApp(doc.Database, tr, WentCadConstants.FloorRegionRegApp);

                var polyline = tr.GetObject(result.ObjectId, OpenMode.ForWrite) as Polyline;
                if (polyline == null || !polyline.Closed || polyline.NumberOfVertices < 3)
                {
                    doc.Editor.WriteMessage("\nWentCad: region kondygnacji musi byc zamknieta polilinia z min. 3 wierzcholkami.");
                    return;
                }

                polyline.Layer = WentCadConstants.FloorRegionLayer;
                polyline.XData = new ResultBuffer(
                    new TypedValue((short)DxfCode.ExtendedDataRegAppName, WentCadConstants.FloorRegionRegApp),
                    new TypedValue((short)DxfCode.ExtendedDataAsciiString, floor.FloorId));

                floor.Region = GeometryManager.PolylineToPoints(polyline);
                floor.DrukWidokiViewId = null;
                tr.Commit();
            }

            _mainView.ViewModel.Save();
            _mainView.LoadActiveDocument();
            doc.Editor.WriteMessage($"\nWentCad: przypisano wybrany region do kondygnacji {floor.Name}.");
        }

        [CommandMethod("WENTCAD_PICK_BASE_POINT")]
        public static void PickBasePoint()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var floor = _mainView?.ViewModel.SelectedFloor;
            if (doc == null || floor == null)
            {
                doc?.Editor.WriteMessage("\nWentCad: wybierz kondygnacje w panelu.");
                return;
            }

            var result = doc.Editor.GetPoint("\nWskaz punkt bazowy kondygnacji (np. przeciecie osi A-1): ");
            if (result.Status != PromptStatus.OK) return;

            floor.BasePoint.X = result.Value.X;
            floor.BasePoint.Y = result.Value.Y;
            string description = FormatBasePointDescription(result.Value.X, result.Value.Y);
            if (string.IsNullOrWhiteSpace(floor.BasePointDescription) ||
                floor.BasePointDescription.StartsWith("Punkt bazowy", StringComparison.OrdinalIgnoreCase))
            {
                floor.BasePointDescription = description;
            }
            _mainView.ViewModel.Save();
            _mainView.LoadActiveDocument();
            doc.Editor.WriteMessage($"\nWentCad: ustawiono punkt bazowy kondygnacji {floor.Name}: {description}.");
        }

        private static string FormatBasePointDescription(double x, double y)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Punkt bazowy X={0:0.###}, Y={1:0.###}",
                x,
                y);
        }
    }
}
