using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using WentCad.Models;

namespace WentCad.Core
{
    public static class NodManager
    {
        public static void SaveProjectIndex(Document doc, WentCadProject project)
        {
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                SaveJson(tr, doc.Database, WentCadConstants.ProjectNod, "project", JObject.FromObject(new
                {
                    project.SchemaVersion,
                    project.ProjectId,
                    project.ProjectName,
                    project.DwgPath,
                    project.UpdatedAt
                }).ToString(Formatting.None));

                SaveJsonMap(tr, doc.Database, WentCadConstants.FloorsNod, project.Floors);
                SaveJsonMap(tr, doc.Database, WentCadConstants.RoomsNod, project.Rooms);
                tr.Commit();
            }
        }

        public static List<DrukWidokiViewDef> LoadDrukWidokiViews(Database db)
        {
            var views = new List<DrukWidokiViewDef>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                if (!nod.Contains(WentCadConstants.DrukWidokiNod))
                {
                    tr.Commit();
                    return views;
                }

                var dict = (DBDictionary)tr.GetObject(nod.GetAt(WentCadConstants.DrukWidokiNod), OpenMode.ForRead);
                foreach (var entry in dict)
                {
                    var xrec = tr.GetObject(entry.Value, OpenMode.ForRead) as Xrecord;
                    if (xrec?.Data == null) continue;
                    foreach (TypedValue tv in xrec.Data)
                    {
                        if (tv.TypeCode == (short)DxfCode.Text)
                        {
                            try
                            {
                                var view = JsonConvert.DeserializeObject<DrukWidokiViewDef>(tv.Value.ToString());
                                if (view != null) views.Add(view);
                            }
                            catch { }
                            break;
                        }
                    }
                }
                tr.Commit();
            }
            return views;
        }

        public static JObject ReadWentCadContract(Database db)
        {
            var result = new JObject();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                result["Project"] = ReadFirstJson(tr, nod, WentCadConstants.ProjectNod, "project");
                result["Floors"] = ReadDictionaryJson(tr, nod, WentCadConstants.FloorsNod);
                result["Rooms"] = ReadDictionaryJson(tr, nod, WentCadConstants.RoomsNod);
                tr.Commit();
            }
            return result;
        }

        private static void SaveJsonMap<T>(Transaction tr, Database db, string dictionaryName, IDictionary<string, T> values)
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
            DBDictionary dict;
            if (nod.Contains(dictionaryName))
            {
                dict = (DBDictionary)tr.GetObject(nod.GetAt(dictionaryName), OpenMode.ForWrite);
                var keys = new List<string>();
                foreach (var entry in dict)
                {
                    keys.Add(entry.Key);
                }

                foreach (var key in keys)
                {
                    dict.Remove(key);
                }
            }
            else
            {
                dict = new DBDictionary();
                nod.SetAt(dictionaryName, dict);
                tr.AddNewlyCreatedDBObject(dict, true);
            }

            foreach (var kv in values)
            {
                var xrec = new Xrecord();
                xrec.Data = new ResultBuffer(new TypedValue((short)DxfCode.Text, JsonConvert.SerializeObject(kv.Value)));
                dict.SetAt(kv.Key, xrec);
                tr.AddNewlyCreatedDBObject(xrec, true);
            }
        }

        private static void SaveJson(Transaction tr, Database db, string dictionaryName, string key, string json)
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
            DBDictionary dict;
            if (nod.Contains(dictionaryName))
            {
                dict = (DBDictionary)tr.GetObject(nod.GetAt(dictionaryName), OpenMode.ForWrite);
            }
            else
            {
                dict = new DBDictionary();
                nod.SetAt(dictionaryName, dict);
                tr.AddNewlyCreatedDBObject(dict, true);
            }

            if (dict.Contains(key)) dict.Remove(key);
            var xrec = new Xrecord();
            xrec.Data = new ResultBuffer(new TypedValue((short)DxfCode.Text, json));
            dict.SetAt(key, xrec);
            tr.AddNewlyCreatedDBObject(xrec, true);
        }

        private static JToken ReadFirstJson(Transaction tr, DBDictionary nod, string dictionaryName, string key)
        {
            if (!nod.Contains(dictionaryName)) return null;
            var dict = (DBDictionary)tr.GetObject(nod.GetAt(dictionaryName), OpenMode.ForRead);
            if (!dict.Contains(key)) return null;
            var xrec = tr.GetObject(dict.GetAt(key), OpenMode.ForRead) as Xrecord;
            return ParseXRecordJson(xrec);
        }

        private static JArray ReadDictionaryJson(Transaction tr, DBDictionary nod, string dictionaryName)
        {
            var arr = new JArray();
            if (!nod.Contains(dictionaryName)) return arr;
            var dict = (DBDictionary)tr.GetObject(nod.GetAt(dictionaryName), OpenMode.ForRead);
            foreach (var entry in dict)
            {
                var token = ParseXRecordJson(tr.GetObject(entry.Value, OpenMode.ForRead) as Xrecord);
                if (token != null) arr.Add(token);
            }
            return arr;
        }

        private static JToken ParseXRecordJson(Xrecord xrec)
        {
            if (xrec?.Data == null) return null;
            foreach (TypedValue tv in xrec.Data)
            {
                if (tv.TypeCode == (short)DxfCode.Text)
                {
                    try { return JToken.Parse(tv.Value.ToString()); } catch { return null; }
                }
            }
            return null;
        }
    }
}
