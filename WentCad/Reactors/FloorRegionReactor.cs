using System;
using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using WentCad.Core;
using WentCad.Models;

namespace WentCad.Reactors
{
    public static class FloorRegionReactor
    {
        private static bool _registered;
        private static bool _idleRegistered;
        private static readonly Dictionary<string, List<PointDto>> PendingUpdates = new Dictionary<string, List<PointDto>>(StringComparer.OrdinalIgnoreCase);

        public static void Register()
        {
            if (_registered) return;

            var manager = Application.DocumentManager;
            manager.DocumentCreated += (s, e) => e.Document.Database.ObjectModified += Database_ObjectModified;
            if (manager.MdiActiveDocument != null)
            {
                manager.MdiActiveDocument.Database.ObjectModified += Database_ObjectModified;
            }
            _registered = true;
        }

        private static void Database_ObjectModified(object sender, ObjectEventArgs e)
        {
            var polyline = e.DBObject as Polyline;
            if (polyline == null || polyline.IsUndoing || polyline.Layer != WentCadConstants.FloorRegionLayer || polyline.XData == null)
            {
                return;
            }

            string floorId = null;
            bool foundRegApp = false;
            foreach (TypedValue value in polyline.XData)
            {
                if (value.TypeCode == (short)DxfCode.ExtendedDataRegAppName &&
                    string.Equals(value.Value?.ToString(), WentCadConstants.FloorRegionRegApp, StringComparison.OrdinalIgnoreCase))
                {
                    foundRegApp = true;
                    continue;
                }

                if (foundRegApp && value.TypeCode == (short)DxfCode.ExtendedDataAsciiString)
                {
                    floorId = value.Value?.ToString();
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(floorId)) return;

            PendingUpdates[floorId] = GeometryManager.PolylineToPoints(polyline);
            if (!_idleRegistered)
            {
                Application.Idle += Application_Idle;
                _idleRegistered = true;
            }
        }

        private static void Application_Idle(object sender, EventArgs e)
        {
            Application.Idle -= Application_Idle;
            _idleRegistered = false;
            if (PendingUpdates.Count == 0) return;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var project = ProjectFileService.LoadOrCreate(doc);
            foreach (var update in PendingUpdates)
            {
                if (project.Floors.TryGetValue(update.Key, out var floor))
                {
                    floor.Region = update.Value;
                }
            }

            ProjectFileService.Save(doc, project);
            NodManager.SaveProjectIndex(doc, project);
            PendingUpdates.Clear();

            UI.PaletteSetManager.MainViewInstance?.ViewModel.Load(doc);
        }
    }
}
