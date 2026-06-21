using System;
using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Klasa statyczna EngineTracer służy do niskopoziomowej diagnostyki zdarzeń silnika Teigha w BricsCAD.
    /// Rozszerzona o mechanizm Chain of Evidence (Filar 2 Agenta Rewidenta):
    /// - subskrybuje ObjectAppended, ObjectModified, ObjectErased, TransactionAborted
    /// - udostepnia CaptureSnapshot(ObjectId) do pobrania wlasciwosci obiektu jako JSON
    /// - udostepnia CountMutationsSinceSession() do taniej walidacji heurystycznej (Wariant A)
    /// </summary>
    public static class EngineTracer
    {
        private static Action<string> _logCallback;
        private static bool _isEnabled = false;

        // Limit rozmiaru pojedynczego snapshota - zapobiega zapchaniu okna kontekstowego
        // Rewidenta w przypadku obiektow z setkami wlasciwosci (np. AttributeReference z DXF).
        private const int MaxSnapshotProperties = 64;

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
                try { db.ObjectErased += Db_ObjectErased; } catch { /* ObjectErased moze nie istniec w starszych wersjach */ }
                Log(">>> Engine Tracer włączony. Nasłuchiwanie zdarzeń bazy danych...");
            }
            else
            {
                db.ObjectAppended -= Db_ObjectAppended;
                db.ObjectModified -= Db_ObjectModified;
                try { db.ObjectErased -= Db_ObjectErased; } catch { }
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
                ObjectId id = e.DBObject?.Id ?? ObjectId.Null;
                if (!id.IsNull) AgentMemoryState.RecordMutation(id);
                Log($"[APPENDED] {e.DBObject?.GetType().Name ?? "?"} (Hand: {(id.IsNull ? "null" : id.Handle.ToString())})");
            }
            catch { }
        }

        private static void Db_ObjectModified(object sender, ObjectEventArgs e)
        {
            try
            {
                ObjectId id = e.DBObject?.Id ?? ObjectId.Null;
                if (!id.IsNull) AgentMemoryState.RecordMutation(id);
                Log($"[MODIFIED] {e.DBObject?.GetType().Name ?? "?"} (Hand: {(id.IsNull ? "null" : id.Handle.ToString())})");
            }
            catch { }
        }

        private static void Db_ObjectErased(object sender, EventArgs e)
        {
            try
            {
                // ObjectErasedEventArgs moze nie istniec w starszych wersjach Teigha -
                // uzywamy refleksji zamiast twardego castu, aby nie lamac kompilacji.
                string handleStr = "unknown";
                bool eraseFlag = true;
                var prop = e.GetType().GetProperty("DBObject");
                if (prop != null)
                {
                    var dbObj = prop.GetValue(e) as DBObject;
                    if (dbObj != null) handleStr = dbObj.Handle.ToString();
                }
                var eraseProp = e.GetType().GetProperty("Erase");
                if (eraseProp != null && eraseProp.PropertyType == typeof(bool))
                {
                    eraseFlag = (bool)eraseProp.GetValue(e);
                }
                Log($"[ERASED] Hand: 0x{handleStr} (Erase={eraseFlag})");
            }
            catch { }
        }

        /// <summary>
        /// Wykonuje snapshot wlasciwosci obiektu CAD i serializuje go do JSON.
        /// Bezpieczne do wywolania z kazdego watku (otwiera wlasna transakcje).
        /// </summary>
        public static EvidenceSnapshot CaptureSnapshot(ObjectId id, string mode = "before")
        {
            if (id.IsNull) return null;
            Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return null;
            Database db = doc.Database;
            try
            {
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    DBObject obj = tr.GetObject(id, OpenMode.ForRead, false);
                    if (obj == null) return null;

                    var snap = new EvidenceSnapshot
                    {
                        Handle = id.Handle.ToString(),
                        ObjectType = obj.GetType().Name,
                        TimestampUtcTicks = DateTime.UtcNow.Ticks
                    };

                    // Warstwa Layer/Color/Linetype jest na Entity (nie na bazowym DBObject)
                    Entity ent = obj as Entity;
                    if (ent != null)
                    {
                        int count = 0;
                        foreach (var derivedProps in EnumerateSafeProperties(ent))
                        {
                            if (count >= MaxSnapshotProperties) break;
                            string key = derivedProps.Key;
                            string val = derivedProps.Value;
                            if (val == null) val = "";
                            if (val.Length > 256) val = val.Substring(0, 256) + "...";
                            snap.Properties[key] = val;
                            count++;
                        }
                    }

                    tr.Commit();
                    Log($"[SNAPSHOT/{mode}] {snap.ObjectType} Hand:0x{snap.Handle} props={snap.Properties.Count}");
                    return snap;
                }
            }
            catch (Exception ex)
            {
                Log($"[SNAPSHOT/{mode} ERROR] {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Zapisuje snapshot do Blackboard w kluczu @evidence_{mode}_{handle_hex}.
        /// Centralizuje zapis, zeby ToolOrchestrator nie musial znac formatu kluczy.
        /// </summary>
        public static void WriteSnapshotToBlackboard(EvidenceSnapshot snap, string mode)
        {
            if (snap == null || string.IsNullOrEmpty(snap.Handle)) return;
            string key = EvidenceSnapshot.BlackboardKey(mode, snap.Handle);
            try
            {
                SharedMemoryState.Write(key, snap.ToJson());
            }
            catch (Exception ex)
            {
                Log($"[SNAPSHOT WRITE ERROR] key={key}: {ex.Message}");
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> EnumerateSafeProperties(Entity obj)
        {
            // Warstwa 1: podstawowe wlasciwosci refleksyjne (Layer, Color, Linetype, itp.)
            // Te propertisy sa na Entity (nie na bazowym DBObject).
            yield return new KeyValuePair<string, string>("Layer", SafeGet(() => obj.Layer));
            yield return new KeyValuePair<string, string>("ColorIndex", SafeGet(() => obj.ColorIndex.ToString()));
            yield return new KeyValuePair<string, string>("Linetype", SafeGet(() => obj.Linetype));
            yield return new KeyValuePair<string, string>("IsErased", SafeGet(() => obj.IsErased.ToString()));
            yield return new KeyValuePair<string, string>("IsModified", SafeGet(() => obj.IsModified.ToString()));

            // Warstwa 2: typowo specyficzne (Length dla Line, Radius dla Circle, itp.)
            switch (obj)
            {
                case Line line:
                    yield return new KeyValuePair<string, string>("Length", SafeGet(() => line.Length.ToString("F4")));
                    yield return new KeyValuePair<string, string>("Angle", SafeGet(() => line.Angle.ToString("F4")));
                    yield return new KeyValuePair<string, string>("StartPoint", SafeGet(() => FormatPoint(line.StartPoint)));
                    yield return new KeyValuePair<string, string>("EndPoint", SafeGet(() => FormatPoint(line.EndPoint)));
                    break;
                case Circle circle:
                    yield return new KeyValuePair<string, string>("Radius", SafeGet(() => circle.Radius.ToString("F4")));
                    yield return new KeyValuePair<string, string>("Area", SafeGet(() => circle.Area.ToString("F4")));
                    yield return new KeyValuePair<string, string>("Center", SafeGet(() => FormatPoint(circle.Center)));
                    break;
                case Polyline pl:
                    yield return new KeyValuePair<string, string>("NumberOfVertices", SafeGet(() => pl.NumberOfVertices.ToString()));
                    break;
                case DBText txt:
                    yield return new KeyValuePair<string, string>("TextString", SafeGet(() => txt.TextString));
                    yield return new KeyValuePair<string, string>("Position", SafeGet(() => FormatPoint(txt.Position)));
                    yield return new KeyValuePair<string, string>("Height", SafeGet(() => txt.Height.ToString("F4")));
                    break;
                case MText mtxt:
                    yield return new KeyValuePair<string, string>("Contents", SafeGet(() => mtxt.Contents));
                    yield return new KeyValuePair<string, string>("Location", SafeGet(() => FormatPoint(mtxt.Location)));
                    break;
            }
        }

        private static string SafeGet(Func<string> getter)
        {
            try { return getter() ?? ""; }
            catch { return ""; }
        }

        private static string FormatPoint(Point3d p)
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4}", p.X, p.Y, p.Z);
        }

        /// <summary>
        /// FALLBACK DETEKTOR MUTACJI (uzywany przez AuditorAuditService gdy subskrypcja EngineTracer
        /// nie zarejestrowala zadnej mutacji). Liczy obiekty w ModelSpace PRZED i PO wywolaniu
        /// narzedzia mutujacego. Roznica > 0 = faktyczna mutacja.
        ///
        /// Wazne: nie wymaga subskrypcji zdarzen bazy DWG - dziala nawet jesli EngineTracer
        /// nie jest włączony (checkbox chkEnableTracer w UI). To jest FIX dla v2.34.13.
        /// </summary>
        public static int CountObjectsInModelSpace()
        {
            Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return -1;
            Database db = doc.Database;
            try
            {
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    BlockTableRecord modelSpace = tr.GetObject(db.CurrentSpaceId, OpenMode.ForRead) as BlockTableRecord;
                    if (modelSpace == null) return -1;
                    int count = 0;
                    foreach (var id in modelSpace)
                    {
                        count++;
                    }
                    tr.Commit();
                    return count;
                }
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Czy ModelSpace ma otwarty rysunek (do walidacji w AuditorAuditService).
        /// </summary>
        public static bool HasActiveDocument()
        {
            return Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument != null;
        }
    }
}
