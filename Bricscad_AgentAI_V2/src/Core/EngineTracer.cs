using System;
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Klasa statyczna EngineTracer służy do niskopoziomowej diagnostyki zdarzeń silnika Teigha w BricsCAD.
    /// </summary>
    public static class EngineTracer
    {
        private static Action<string> _logCallback;
        private static bool _isEnabled = false;

        public static void SetLogCallback(Action<string> callback)
        {
            _logCallback = callback;
        }

        public static void Enable(bool enable)
        {
            if (_isEnabled == enable) return;
            _isEnabled = enable;

            Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Database db = doc.Database;

            if (_isEnabled)
            {
                db.ObjectAppended += Db_ObjectAppended;
                db.ObjectModified += Db_ObjectModified;
                Log(">>> Engine Tracer włączony. Nasłuchiwanie zdarzeń bazy danych...");
            }
            else
            {
                db.ObjectAppended -= Db_ObjectAppended;
                db.ObjectModified -= Db_ObjectModified;
                Log("<<< Engine Tracer wyłączony.");
            }
        }

        public static void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            _logCallback?.Invoke($"[{timestamp}] {message}");
        }

        private static void Db_ObjectAppended(object sender, ObjectEventArgs e)
        {
            try
            {
                Log($"[APPENDED] {e.DBObject.GetType().Name} (Hand: {e.DBObject.Handle})");
            }
            catch { }
        }

        private static void Db_ObjectModified(object sender, ObjectEventArgs e)
        {
            try
            {
                Log($"[MODIFIED] {e.DBObject.GetType().Name} (Hand: {e.DBObject.Handle})");
            }
            catch { }
        }
    }
}
