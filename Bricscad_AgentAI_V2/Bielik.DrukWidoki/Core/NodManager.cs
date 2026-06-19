using System;
using System.Collections.Generic;
using Teigha.DatabaseServices;
using Newtonsoft.Json;
using Bielik.DrukWidoki.Models;

namespace Bielik.DrukWidoki.Core
{
    public class NodManager
    {
        public const string NodDictionaryName = "BIELIK_DRUK_WIDOKI";

        public static List<BielikViewDef> LoadViews(Database db)
        {
            var views = new List<BielikViewDef>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (!nod.Contains(NodDictionaryName))
                    return views;

                var dict = (DBDictionary)tr.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForRead);
                foreach (var entry in dict)
                {
                    var xrec = tr.GetObject(entry.Value, OpenMode.ForRead) as Xrecord;
                    if (xrec != null)
                    {
                        foreach (TypedValue tv in xrec.Data)
                        {
                            if (tv.TypeCode == (short)DxfCode.Text)
                            {
                                try {
                                    var v = JsonConvert.DeserializeObject<BielikViewDef>((string)tv.Value);
                                    if (v != null) views.Add(v);
                                } catch { }
                                break;
                            }
                        }
                    }
                }
                tr.Commit();
            }
            return views;
        }

        public static void SaveView(Database db, BielikViewDef viewDef)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
                DBDictionary dict;
                if (nod.Contains(NodDictionaryName))
                {
                    dict = (DBDictionary)tr.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForWrite);
                }
                else
                {
                    dict = new DBDictionary();
                    nod.SetAt(NodDictionaryName, dict);
                    tr.AddNewlyCreatedDBObject(dict, true);
                }

                Xrecord xrec = new Xrecord();
                string json = JsonConvert.SerializeObject(viewDef);
                xrec.Data = new ResultBuffer(new TypedValue((short)DxfCode.Text, json));

                dict.SetAt(viewDef.ViewId.ToString(), xrec);
                tr.AddNewlyCreatedDBObject(xrec, true);
                
                tr.Commit();
            }
        }
        
        public static void DeleteView(Database db, Guid viewId)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (nod.Contains(NodDictionaryName))
                {
                    var dict = (DBDictionary)tr.GetObject(nod.GetAt(NodDictionaryName), OpenMode.ForWrite);
                    string key = viewId.ToString();
                    if (dict.Contains(key))
                    {
                        dict.Remove(key);
                    }
                }
                tr.Commit();
            }
        }
    }
}
