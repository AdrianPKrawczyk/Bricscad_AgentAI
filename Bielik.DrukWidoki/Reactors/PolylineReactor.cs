using Teigha.DatabaseServices;
using Bricscad.ApplicationServices;
using System;
using System.Collections.Generic;
using Bielik.DrukWidoki.Core;
using Bielik.DrukWidoki.Models;
using Newtonsoft.Json;

namespace Bielik.DrukWidoki.Reactors
{
    public class PolylineReactor
    {
        private static bool _isRegistered = false;
        private static Dictionary<Guid, List<PointDTO>> _pendingUpdates = new Dictionary<Guid, List<PointDTO>>();
        private static bool _idleRegistered = false;

        public static void Register()
        {
            if (!_isRegistered)
            {
                var docMgr = Application.DocumentManager;
                docMgr.DocumentCreated += (s, e) => {
                    e.Document.Database.ObjectModified += Db_ObjectModified;
                };
                if (docMgr.MdiActiveDocument != null)
                {
                    docMgr.MdiActiveDocument.Database.ObjectModified += Db_ObjectModified;
                }
                _isRegistered = true;
            }
        }

        private static void Db_ObjectModified(object sender, ObjectEventArgs e)
        {
            var poly = e.DBObject as Polyline;
            if (poly == null || poly.IsUndoing) return;
            if (poly != null && poly.Layer == GeometryManager.LayerName && poly.XData != null)
            {
                string viewIdStr = null;
                bool foundApp = false;
                foreach (TypedValue tv in poly.XData)
                {
                    if (tv.TypeCode == (short)DxfCode.ExtendedDataRegAppName && tv.Value.ToString() == GeometryManager.RegAppName)
                        foundApp = true;
                    else if (foundApp && tv.TypeCode == (short)DxfCode.ExtendedDataAsciiString)
                    {
                        viewIdStr = tv.Value.ToString();
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(viewIdStr) && Guid.TryParse(viewIdStr, out Guid viewId))
                {
                    var points = new List<PointDTO>();
                    for (int i = 0; i < poly.NumberOfVertices; i++)
                    {
                        var pt = poly.GetPoint2dAt(i);
                        points.Add(new PointDTO { X = pt.X, Y = pt.Y });
                    }

                    _pendingUpdates[viewId] = points;

                    if (!_idleRegistered)
                    {
                        Application.Idle += Application_Idle;
                        _idleRegistered = true;
                    }
                }
            }
        }

        private static void Application_Idle(object sender, EventArgs e)
        {
            Application.Idle -= Application_Idle;
            _idleRegistered = false;

            if (_pendingUpdates.Count == 0) return;

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            using (var docLock = doc.LockDocument())
            {
                using (var tr = doc.TransactionManager.StartTransaction())
                {
                    var nod = (DBDictionary)tr.GetObject(doc.Database.NamedObjectsDictionaryId, OpenMode.ForRead);
                    if (nod.Contains(NodManager.NodDictionaryName))
                    {
                        var dict = (DBDictionary)tr.GetObject(nod.GetAt(NodManager.NodDictionaryName), OpenMode.ForWrite);
                        
                        foreach (var kvp in _pendingUpdates)
                        {
                            var viewId = kvp.Key;
                            var pts = kvp.Value;
                            
                            if (dict.Contains(viewId.ToString()))
                            {
                                var xrec = tr.GetObject(dict.GetAt(viewId.ToString()), OpenMode.ForWrite) as Xrecord;
                                if (xrec != null)
                                {
                                    string json = "";
                                    foreach (TypedValue tv in xrec.Data)
                                    {
                                        if (tv.TypeCode == (short)DxfCode.Text)
                                        {
                                            json = (string)tv.Value;
                                            break;
                                        }
                                    }

                                    try
                                    {
                                        var viewDef = JsonConvert.DeserializeObject<BielikViewDef>(json);
                                        if (viewDef != null)
                                        {
                                            viewDef.Geometry = pts;
                                            string newJson = JsonConvert.SerializeObject(viewDef);
                                            xrec.Data = new ResultBuffer(new TypedValue((short)DxfCode.Text, newJson));
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            _pendingUpdates.Clear();
            
            // Odśwież UI
            if (UI.PaletteSetManager.MainViewInstance != null)
            {
                var views = NodManager.LoadViews(doc.Database);
                UI.PaletteSetManager.MainViewInstance.ViewModel.LoadData(views);
            }
        }
    }
}
