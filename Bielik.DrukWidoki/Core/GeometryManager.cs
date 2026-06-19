using System;
using System.Collections.Generic;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Teigha.Colors;
using Bielik.DrukWidoki.Models;

namespace Bielik.DrukWidoki.Core
{
    public class GeometryManager
    {
        public const string LayerName = "_ZAKRESY";
        public const string RegAppName = "BIELIK_VIEW_DEF";

        public static void EnsureLayerExists(Database db, Transaction tr)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(LayerName))
            {
                lt.UpgradeOpen();
                var ltr = new LayerTableRecord();
                ltr.Name = LayerName;
                ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, 1); // Red
                ltr.IsPlottable = false;
                ltr.IsLocked = true; // Zablokowana domyślnie
                ltr.LineWeight = LineWeight.LineWeight005; // 0.05
                
                // Próba wczytania i przypisania typu linii HIDDEN
                var ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                if (!ltt.Has("HIDDEN"))
                {
                    try
                    {
                        db.LoadLineTypeFile("HIDDEN", "default.lin");
                    }
                    catch
                    {
                        // Ignore if loading fails
                    }
                }
                if (ltt.Has("HIDDEN"))
                {
                    ltr.LinetypeObjectId = ltt["HIDDEN"];
                }

                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }

        public static void EnsureRegAppExists(Database db, Transaction tr)
        {
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (!rat.Has(RegAppName))
            {
                rat.UpgradeOpen();
                var ratr = new RegAppTableRecord();
                ratr.Name = RegAppName;
                rat.Add(ratr);
                tr.AddNewlyCreatedDBObject(ratr, true);
            }
        }

        public static ObjectId DrawViewPolyline(Database db, BielikViewDef viewDef)
        {
            ObjectId polyId = ObjectId.Null;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                EnsureLayerExists(db, tr);
                EnsureRegAppExists(db, tr);

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                var poly = new Polyline();
                for (int i = 0; i < viewDef.Geometry.Count; i++)
                {
                    poly.AddVertexAt(i, new Point2d(viewDef.Geometry[i].X, viewDef.Geometry[i].Y), 0, 0, 0);
                }
                poly.Closed = true;
                poly.Layer = LayerName;

                // Add XData
                var rb = new ResultBuffer(
                    new TypedValue((short)DxfCode.ExtendedDataRegAppName, RegAppName),
                    new TypedValue((short)DxfCode.ExtendedDataAsciiString, viewDef.ViewId.ToString())
                );
                poly.XData = rb;

                polyId = btr.AppendEntity(poly);
                tr.AddNewlyCreatedDBObject(poly, true);

                tr.Commit();
            }
            return polyId;
        }
    }
}
