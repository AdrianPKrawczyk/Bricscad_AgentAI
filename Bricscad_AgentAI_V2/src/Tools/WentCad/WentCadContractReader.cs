using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    internal static class WentCadContractReader
    {
        public const string ProjectNod = "WENTCAD_PROJECT";
        public const string FloorsNod = "WENTCAD_FLOORS";
        public const string RoomsNod = "WENTCAD_ROOMS";
        public const string WallsNod = "WENTCAD_WALLS";
        public const string WindowsNod = "WENTCAD_WINDOWS";

        public static JObject Read(Database db)
        {
            var result = new JObject
            {
                ["Project"] = null,
                ["Floors"] = new JArray(),
                ["Rooms"] = new JArray(),
                ["Walls"] = new JArray(),
                ["Windows"] = new JArray()
            };

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForRead);
                result["Project"] = ReadSingleJson(tr, nod, ProjectNod, "project");
                result["Floors"] = ReadDictionaryJson(tr, nod, FloorsNod);
                result["Rooms"] = ReadDictionaryJson(tr, nod, RoomsNod);
                result["Walls"] = ReadDictionaryJson(tr, nod, WallsNod);
                result["Windows"] = ReadDictionaryJson(tr, nod, WindowsNod);
                tr.Commit();
            }

            return result;
        }

        private static JToken ReadSingleJson(Transaction tr, DBDictionary nod, string dictionaryName, string key)
        {
            if (!nod.Contains(dictionaryName)) return null;
            var dict = (DBDictionary)tr.GetObject(nod.GetAt(dictionaryName), OpenMode.ForRead);
            if (!dict.Contains(key)) return null;
            return ParseJson(tr.GetObject(dict.GetAt(key), OpenMode.ForRead) as Xrecord);
        }

        private static JArray ReadDictionaryJson(Transaction tr, DBDictionary nod, string dictionaryName)
        {
            var array = new JArray();
            if (!nod.Contains(dictionaryName)) return array;

            var dict = (DBDictionary)tr.GetObject(nod.GetAt(dictionaryName), OpenMode.ForRead);
            foreach (var entry in dict)
            {
                var token = ParseJson(tr.GetObject(entry.Value, OpenMode.ForRead) as Xrecord);
                if (token != null) array.Add(token);
            }

            return array;
        }

        private static JToken ParseJson(Xrecord xrecord)
        {
            if (xrecord?.Data == null) return null;
            foreach (TypedValue value in xrecord.Data)
            {
                if (value.TypeCode != (short)DxfCode.Text) continue;
                try
                {
                    return JToken.Parse(value.Value?.ToString() ?? "");
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
    }
}
