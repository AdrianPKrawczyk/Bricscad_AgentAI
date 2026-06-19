using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Runtime;
using Bielik.DrukWidoki.Core;
using Bielik.DrukWidoki.Models;
using System;
using System.Collections.Generic;

[assembly: CommandClass(typeof(Bielik.DrukWidoki.UI.ViewDrawCommand))]

namespace Bielik.DrukWidoki.UI
{
    public class ViewDrawCommand
    {
        [CommandMethod("BIELIK_DRAW_VIEW")]
        public void DrawView()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;

            var ppo = new PromptPointOptions("\nWskaż pierwszy narożnik widoku: ");
            var ppr = ed.GetPoint(ppo);
            if (ppr.Status != PromptStatus.OK) return;

            var jig = new Jigs.ViewDrawJig(ppr.Value);
            var pr = ed.Drag(jig);

            if (pr.Status == PromptStatus.OK)
            {
                var viewId = Guid.NewGuid();
                var poly = jig.GetEntity();
                
                using (var docLock = doc.LockDocument())
                {
                    using (var tr = doc.TransactionManager.StartTransaction())
                    {
                        GeometryManager.EnsureLayerExists(db, tr);
                        poly.Layer = GeometryManager.LayerName;
                        GeometryManager.EnsureRegAppExists(db, tr);

                        var rb = new ResultBuffer(
                            new TypedValue((short)DxfCode.ExtendedDataRegAppName, GeometryManager.RegAppName),
                            new TypedValue((short)DxfCode.ExtendedDataAsciiString, viewId.ToString())
                        );
                        poly.XData = rb;

                        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                        btr.AppendEntity(poly);
                        tr.AddNewlyCreatedDBObject(poly, true);

                        var viewDef = new BielikViewDef
                        {
                            ViewId = viewId,
                            Name = "Nowy Widok",
                            Type = "Rzut",
                            Geometry = new List<PointDTO>()
                        };
                        for (int i = 0; i < poly.NumberOfVertices; i++)
                        {
                            var pt = poly.GetPoint2dAt(i);
                            viewDef.Geometry.Add(new PointDTO { X = pt.X, Y = pt.Y });
                        }

                        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
                        DBDictionary dict;
                        if (nod.Contains(NodManager.NodDictionaryName))
                        {
                            dict = (DBDictionary)tr.GetObject(nod.GetAt(NodManager.NodDictionaryName), OpenMode.ForWrite);
                        }
                        else
                        {
                            dict = new DBDictionary();
                            nod.SetAt(NodManager.NodDictionaryName, dict);
                            tr.AddNewlyCreatedDBObject(dict, true);
                        }

                        Xrecord xrec = new Xrecord();
                        string json = Newtonsoft.Json.JsonConvert.SerializeObject(viewDef);
                        xrec.Data = new ResultBuffer(new TypedValue((short)DxfCode.Text, json));
                        dict.SetAt(viewId.ToString(), xrec);
                        tr.AddNewlyCreatedDBObject(xrec, true);

                        tr.Commit();
                    }
                }

                if (PaletteSetManager.MainViewInstance != null)
                {
                    var views = NodManager.LoadViews(db);
                    PaletteSetManager.MainViewInstance.ViewModel.LoadData(views);
                }
            }
        }
    }
}
