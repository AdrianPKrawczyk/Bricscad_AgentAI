using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Bricscad.ApplicationServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools.WentCad
{
    internal static class WentCadProjectStore
    {
        public const string RoomRegApp = "WENTCAD_ROOM";

        public static string GetProjectPath(Document doc)
        {
            if (doc == null || string.IsNullOrWhiteSpace(doc.Name) || !Path.IsPathRooted(doc.Name))
            {
                string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WentCad");
                Directory.CreateDirectory(fallback);
                return Path.Combine(fallback, "Untitled.wentcad");
            }

            string dir = Path.GetDirectoryName(doc.Name);
            string name = Path.GetFileNameWithoutExtension(doc.Name);
            return Path.Combine(dir, name + ".wentcad");
        }

        public static JObject LoadOrCreate(Document doc)
        {
            string path = GetProjectPath(doc);
            JObject project = null;
            if (File.Exists(path))
            {
                project = JObject.Parse(File.ReadAllText(path));
            }

            if (project == null)
            {
                project = CreateDefaultProject(doc);
            }

            EnsureDefaults(project, doc);
            return project;
        }

        public static void Save(Document doc, JObject project)
        {
            EnsureDefaults(project, doc);
            project["UpdatedAt"] = DateTime.UtcNow;

            string path = GetProjectPath(doc);
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.WriteAllText(path, project.ToString(Formatting.Indented));
        }

        public static void SaveContractToDwg(Document doc, JObject project, bool writeRoomXData)
        {
            using (doc.LockDocument())
            using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
            {
                SaveJson(tr, doc.Database, WentCadContractReader.ProjectNod, "project", BuildProjectSummary(project).ToString(Formatting.None));
                SaveJsonMap(tr, doc.Database, WentCadContractReader.FloorsNod, ObjectMap(project, "Floors"), "FloorId");
                SaveJsonMap(tr, doc.Database, WentCadContractReader.RoomsNod, ObjectMap(project, "Rooms"), "RoomId");
                if (writeRoomXData)
                {
                    WriteRoomXData(tr, doc.Database, project);
                }
                tr.Commit();
            }
        }

        public static JObject UpsertFloor(JObject project, JObject args)
        {
            EnsureDefaults(project, null);
            var floors = ObjectMap(project, "Floors");
            string floorId = Clean(args["FloorId"]);
            JObject floor = null;

            if (!string.IsNullOrWhiteSpace(floorId))
            {
                floor = floors[floorId] as JObject;
            }

            if (floor == null && !string.IsNullOrWhiteSpace(Clean(args["Name"])))
            {
                floor = floors.Properties()
                    .Select(p => p.Value as JObject)
                    .FirstOrDefault(f => string.Equals(Clean(f?["Name"]), Clean(args["Name"]), StringComparison.OrdinalIgnoreCase));
                floorId = Clean(floor?["FloorId"]);
            }

            if (floor == null)
            {
                floorId = string.IsNullOrWhiteSpace(floorId) ? Guid.NewGuid().ToString() : floorId;
                floor = new JObject
                {
                    ["FloorId"] = floorId,
                    ["Name"] = Clean(args["Name"]) ?? "Kondygnacja",
                    ["Order"] = floors.Count,
                    ["Elevation"] = 0.0,
                    ["HeightNet"] = 3.0,
                    ["HeightTotal"] = 3.5,
                    ["BasePoint"] = new JObject { ["X"] = 0.0, ["Y"] = 0.0, ["Z"] = 0.0 },
                    ["BasePointDescription"] = "",
                    ["DrukWidokiViewId"] = null,
                    ["Region"] = new JArray()
                };
                floors[floorId] = floor;
            }

            PatchString(floor, args, "Name");
            PatchInt(floor, args, "Order");
            PatchDouble(floor, args, "Elevation");
            PatchDouble(floor, args, "HeightNet");
            PatchDouble(floor, args, "HeightTotal");
            PatchString(floor, args, "BasePointDescription");
            PatchString(floor, args, "DrukWidokiViewId");

            var basePoint = floor["BasePoint"] as JObject ?? new JObject();
            if (args["BasePointX"] != null) basePoint["X"] = ToDouble(args["BasePointX"], ToDouble(basePoint["X"], 0));
            if (args["BasePointY"] != null) basePoint["Y"] = ToDouble(args["BasePointY"], ToDouble(basePoint["Y"], 0));
            if (args["BasePointZ"] != null) basePoint["Z"] = ToDouble(args["BasePointZ"], ToDouble(basePoint["Z"], 0));
            floor["BasePoint"] = basePoint;

            return floor;
        }

        public static JObject UpsertRoom(JObject project, JObject args)
        {
            EnsureDefaults(project, null);
            var rooms = ObjectMap(project, "Rooms");
            string roomId = Clean(args["RoomId"]);
            string floorIdArg = Clean(args["FloorId"]);
            string numberArg = Clean(args["Number"]);
            JObject room = null;

            if (!string.IsNullOrWhiteSpace(roomId))
            {
                room = rooms[roomId] as JObject;
            }

            if (room == null && !string.IsNullOrWhiteSpace(floorIdArg) && !string.IsNullOrWhiteSpace(numberArg))
            {
                room = rooms.Properties()
                    .Select(p => p.Value as JObject)
                    .FirstOrDefault(r =>
                        string.Equals(Clean(r?["FloorId"]), floorIdArg, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(Clean(r?["Number"]), numberArg, StringComparison.OrdinalIgnoreCase));
                roomId = Clean(room?["RoomId"]);
            }

            if (room == null && !string.IsNullOrWhiteSpace(floorIdArg) && !string.IsNullOrWhiteSpace(numberArg))
            {
                var globalMatches = rooms.Properties()
                    .Select(p => p.Value as JObject)
                    .Where(r => string.Equals(Clean(r?["Number"]), numberArg, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (globalMatches.Count == 1)
                {
                    room = globalMatches[0];
                    roomId = Clean(room?["RoomId"]);
                }
            }

            if (room == null)
            {
                if (string.IsNullOrWhiteSpace(floorIdArg) || string.IsNullOrWhiteSpace(numberArg))
                {
                    throw new InvalidOperationException("ManageWentCadRooms nie tworzy nowego pomieszczenia bez FloorId i Number. Uzyj UpdateWentCadRoomByNumber z FloorName/FloorId albo podaj jednoznaczna kondygnacje.");
                }

                roomId = string.IsNullOrWhiteSpace(roomId) ? Guid.NewGuid().ToString() : roomId;
                room = new JObject
                {
                    ["RoomId"] = roomId,
                    ["FloorId"] = floorIdArg,
                    ["Number"] = numberArg,
                    ["Name"] = Clean(args["Name"]) ?? "",
                    ["ActivityType"] = "CUSTOM",
                    ["BoundaryHandle"] = "",
                    ["TagHandle"] = "",
                    ["SupplySystemId"] = "",
                    ["ExhaustSystemId"] = "",
                    ["Area"] = 0.0,
                    ["Height"] = 3.0,
                    ["Volume"] = 0.0,
                    ["Occupants"] = 1,
                    ["DosePerOccupant"] = 30.0,
                    ["IsTargetAchManual"] = false,
                    ["ManualTargetAch"] = 0.0,
                    ["TargetAch"] = 1.5,
                    ["CalculationMode"] = "AUTO_MAX",
                    ["ManualSupply"] = 0.0,
                    ["ManualExhaust"] = 0.0,
                    ["CalculatedSupply"] = 0.0,
                    ["CalculatedExhaust"] = 0.0,
                    ["TransferIn"] = 0.0,
                    ["TransferOut"] = 0.0,
                    ["NetBalance"] = 0.0,
                    ["RealAch"] = 0.0
                };
                rooms[roomId] = room;
            }

            foreach (string name in new[] { "FloorId", "Number", "Name", "ActivityType", "BoundaryHandle", "TagHandle", "SupplySystemId", "ExhaustSystemId", "CalculationMode" })
            {
                PatchString(room, args, name);
            }
            foreach (string name in new[] { "Area", "Height", "DosePerOccupant", "ManualTargetAch", "TargetAch", "ManualSupply", "ManualExhaust", "TransferIn", "TransferOut" })
            {
                PatchDouble(room, args, name);
            }
            PatchInt(room, args, "Occupants");
            PatchBool(room, args, "IsTargetAchManual");
            return room;
        }

        public static JObject FindFloor(JObject project, string floorId, string floorName)
        {
            EnsureDefaults(project, null);
            var floors = ObjectMap(project, "Floors");
            if (!string.IsNullOrWhiteSpace(floorId))
            {
                var floor = floors[floorId] as JObject;
                if (floor != null) return floor;
            }

            if (!string.IsNullOrWhiteSpace(floorName))
            {
                return floors.Properties()
                    .Select(p => p.Value as JObject)
                    .FirstOrDefault(f => string.Equals(Clean(f?["Name"]), floorName, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        public static JObject FindRoomByFloorAndNumber(JObject project, string floorId, string number)
        {
            if (string.IsNullOrWhiteSpace(floorId) || string.IsNullOrWhiteSpace(number)) return null;
            return ObjectMap(project, "Rooms").Properties()
                .Select(p => p.Value as JObject)
                .FirstOrDefault(r =>
                    string.Equals(Clean(r?["FloorId"]), floorId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Clean(r?["Number"]), number, StringComparison.OrdinalIgnoreCase));
        }

        public static JObject UpdateExistingRoomByNumber(JObject project, JObject args)
        {
            EnsureDefaults(project, null);
            string floorId = Clean(args["FloorId"]);
            string floorName = Clean(args["FloorName"]);
            string number = Clean(args["Number"]);
            if (string.IsNullOrWhiteSpace(number))
            {
                throw new InvalidOperationException("UpdateWentCadRoomByNumber wymaga Number.");
            }

            var floor = FindFloor(project, floorId, floorName);
            if (floor == null)
            {
                throw new InvalidOperationException("Nie znaleziono kondygnacji po FloorId/FloorName.");
            }
            floorId = Clean(floor["FloorId"]);

            var room = FindRoomByFloorAndNumber(project, floorId, number);
            if (room == null)
            {
                throw new InvalidOperationException("Nie znaleziono istniejacego pomieszczenia dla FloorId/FloorName + Number. Narzedzie nie tworzy nowych rekordow.");
            }

            var patchArgs = (JObject)args.DeepClone();
            patchArgs["RoomId"] = room["RoomId"];
            patchArgs["FloorId"] = floorId;
            patchArgs["Number"] = number;
            return UpsertRoom(project, patchArgs);
        }

        public static JObject UpsertSystem(JObject project, JObject args)
        {
            EnsureDefaults(project, null);
            var systems = project["Systems"] as JArray;
            string systemId = Clean(args["SystemId"]);
            if (string.IsNullOrWhiteSpace(systemId)) systemId = Guid.NewGuid().ToString();

            JObject system = systems.Children<JObject>()
                .FirstOrDefault(s => string.Equals(Clean(s["SystemId"]), systemId, StringComparison.OrdinalIgnoreCase));
            if (system == null)
            {
                system = new JObject
                {
                    ["SystemId"] = systemId,
                    ["Name"] = Clean(args["Name"]) ?? systemId,
                    ["Type"] = Clean(args["Type"]) ?? "SUPPLY",
                    ["ColorIndex"] = 5,
                    ["TotalSupply"] = 0.0,
                    ["TotalExhaust"] = 0.0
                };
                systems.Add(system);
            }

            PatchString(system, args, "Name");
            PatchString(system, args, "Type");
            PatchInt(system, args, "ColorIndex");
            return system;
        }

        public static bool DeleteItem(JObject project, string mapName, string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return false;
            var map = ObjectMap(project, mapName);
            return map.Remove(id);
        }

        public static bool DeleteSystem(JObject project, string systemId)
        {
            var systems = project["Systems"] as JArray;
            if (systems == null || string.IsNullOrWhiteSpace(systemId)) return false;
            var item = systems.Children<JObject>()
                .FirstOrDefault(s => string.Equals(Clean(s["SystemId"]), systemId, StringComparison.OrdinalIgnoreCase));
            if (item == null) return false;
            item.Remove();
            return true;
        }

        public static void Recalculate(JObject project)
        {
            foreach (JObject room in ObjectMap(project, "Rooms").Properties().Select(p => p.Value).OfType<JObject>())
            {
                RecalculateRoom(room);
            }

            var systems = project["Systems"] as JArray ?? new JArray();
            foreach (JObject system in systems.Children<JObject>())
            {
                string systemId = Clean(system["SystemId"]);
                double totalSupply = 0;
                double totalExhaust = 0;
                foreach (JObject room in ObjectMap(project, "Rooms").Properties().Select(p => p.Value).OfType<JObject>())
                {
                    if (string.Equals(Clean(room["SupplySystemId"]), systemId, StringComparison.OrdinalIgnoreCase))
                    {
                        totalSupply += ToDouble(room["CalculatedSupply"], 0);
                    }
                    if (string.Equals(Clean(room["ExhaustSystemId"]), systemId, StringComparison.OrdinalIgnoreCase))
                    {
                        totalExhaust += ToDouble(room["CalculatedExhaust"], 0);
                    }
                }
                system["TotalSupply"] = totalSupply;
                system["TotalExhaust"] = totalExhaust;
            }
        }

        public static JObject BuildSummary(JObject project, string projectPath)
        {
            return new JObject
            {
                ["ProjectPath"] = projectPath,
                ["ProjectId"] = project["ProjectId"],
                ["ProjectName"] = project["ProjectName"],
                ["FloorsCount"] = ObjectMap(project, "Floors").Count,
                ["RoomsCount"] = ObjectMap(project, "Rooms").Count,
                ["SystemsCount"] = (project["Systems"] as JArray)?.Count ?? 0,
                ["UpdatedAt"] = project["UpdatedAt"]
            };
        }

        public static JObject ObjectMap(JObject project, string name)
        {
            var map = project[name] as JObject;
            if (map == null)
            {
                map = new JObject();
                project[name] = map;
            }
            return map;
        }

        public static string Clean(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            string value = token.ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static JObject CreateDefaultProject(Document doc)
        {
            var project = new JObject
            {
                ["SchemaVersion"] = 1,
                ["ProjectId"] = Guid.NewGuid(),
                ["DwgPath"] = doc?.Name ?? "",
                ["ProjectName"] = Path.GetFileNameWithoutExtension(doc?.Name ?? "WentCad"),
                ["Floors"] = new JObject(),
                ["Rooms"] = new JObject(),
                ["Systems"] = new JArray(),
                ["DetectionMapping"] = new JObject(),
                ["UpdatedAt"] = DateTime.UtcNow
            };
            ((JArray)project["Systems"]).Add(new JObject { ["SystemId"] = "N1", ["Name"] = "Nawiew 1", ["Type"] = "SUPPLY", ["ColorIndex"] = 5, ["TotalSupply"] = 0.0, ["TotalExhaust"] = 0.0 });
            ((JArray)project["Systems"]).Add(new JObject { ["SystemId"] = "W1", ["Name"] = "Wywiew 1", ["Type"] = "EXHAUST", ["ColorIndex"] = 1, ["TotalSupply"] = 0.0, ["TotalExhaust"] = 0.0 });
            return project;
        }

        private static void EnsureDefaults(JObject project, Document doc)
        {
            if (project["SchemaVersion"] == null) project["SchemaVersion"] = 1;
            if (project["ProjectId"] == null) project["ProjectId"] = Guid.NewGuid();
            if (doc != null) project["DwgPath"] = doc.Name ?? Clean(project["DwgPath"]) ?? "";
            if (project["ProjectName"] == null) project["ProjectName"] = Path.GetFileNameWithoutExtension(doc?.Name ?? "WentCad");
            ObjectMap(project, "Floors");
            ObjectMap(project, "Rooms");
            if (!(project["Systems"] is JArray)) project["Systems"] = new JArray();
            if (((JArray)project["Systems"]).Count == 0)
            {
                ((JArray)project["Systems"]).Add(new JObject { ["SystemId"] = "N1", ["Name"] = "Nawiew 1", ["Type"] = "SUPPLY", ["ColorIndex"] = 5, ["TotalSupply"] = 0.0, ["TotalExhaust"] = 0.0 });
                ((JArray)project["Systems"]).Add(new JObject { ["SystemId"] = "W1", ["Name"] = "Wywiew 1", ["Type"] = "EXHAUST", ["ColorIndex"] = 1, ["TotalSupply"] = 0.0, ["TotalExhaust"] = 0.0 });
            }
            if (!(project["DetectionMapping"] is JObject)) project["DetectionMapping"] = new JObject();
        }

        private static void RecalculateRoom(JObject room)
        {
            double area = ToDouble(room["Area"], 0);
            double height = ToDouble(room["Height"], 0);
            double volume = Math.Round(area * height, 2);
            room["Volume"] = volume;

            bool manualAch = ToBool(room["IsTargetAchManual"], false);
            double ach = manualAch ? ToDouble(room["ManualTargetAch"], 0) : DefaultAch(Clean(room["ActivityType"]));
            room["TargetAch"] = ach;

            double hygienic = Math.Max(0, ToInt(room["Occupants"], 0)) * Math.Max(0, ToDouble(room["DosePerOccupant"], 0));
            double achFlow = ach * volume;
            double supply;
            switch ((Clean(room["CalculationMode"]) ?? "AUTO_MAX").ToUpperInvariant())
            {
                case "MANUAL":
                    supply = ToDouble(room["ManualSupply"], 0);
                    break;
                case "HYGIENIC_ONLY":
                    supply = hygienic;
                    break;
                case "ACH_ONLY":
                    supply = achFlow;
                    break;
                default:
                    supply = Math.Max(hygienic, Math.Max(achFlow, ToDouble(room["ManualSupply"], 0)));
                    break;
            }

            double calculatedSupply = Math.Ceiling(Math.Max(0, supply));
            double transferIn = ToDouble(room["TransferIn"], 0);
            double transferOut = ToDouble(room["TransferOut"], 0);
            double manualExhaust = ToDouble(room["ManualExhaust"], 0);
            double calculatedExhaust = Math.Ceiling(Math.Max(0, manualExhaust > 0 ? manualExhaust : calculatedSupply + transferIn - transferOut));

            room["CalculatedSupply"] = calculatedSupply;
            room["CalculatedExhaust"] = calculatedExhaust;
            room["NetBalance"] = (calculatedSupply + transferIn) - (calculatedExhaust + transferOut);
            double dominant = Math.Max(calculatedSupply + transferIn, calculatedExhaust + transferOut);
            room["RealAch"] = volume > 0 ? Math.Round(dominant / volume, 2) : 0;
        }

        private static double DefaultAch(string activityType)
        {
            switch (activityType ?? "")
            {
                case "Gastronomia: Kuchnia": return 22.5;
                case "Gastronomia: Zmywalnia": return 10.0;
                case "Natryski": return 5.0;
                case "Umywalnia": return 2.0;
                case "Komunikacja / Korytarz": return 1.5;
                case "Pomieszczenie socjalne": return 2.0;
                default: return 1.5;
            }
        }

        private static JObject BuildProjectSummary(JObject project)
        {
            return new JObject
            {
                ["SchemaVersion"] = project["SchemaVersion"],
                ["ProjectId"] = project["ProjectId"],
                ["ProjectName"] = project["ProjectName"],
                ["DwgPath"] = project["DwgPath"],
                ["UpdatedAt"] = project["UpdatedAt"]
            };
        }

        private static void SaveJsonMap(Transaction tr, Database db, string dictionaryName, JObject values, string idField)
        {
            var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
            DBDictionary dict;
            if (nod.Contains(dictionaryName))
            {
                dict = (DBDictionary)tr.GetObject(nod.GetAt(dictionaryName), OpenMode.ForWrite);
                var keys = new List<string>();
                foreach (var entry in dict) keys.Add(entry.Key);
                foreach (var key in keys) dict.Remove(key);
            }
            else
            {
                dict = new DBDictionary();
                nod.SetAt(dictionaryName, dict);
                tr.AddNewlyCreatedDBObject(dict, true);
            }

            foreach (var prop in values.Properties())
            {
                var value = prop.Value as JObject;
                if (value == null) continue;
                string key = Clean(value[idField]) ?? prop.Name;
                var xrec = new Xrecord
                {
                    Data = new ResultBuffer(new TypedValue((short)DxfCode.Text, value.ToString(Formatting.None)))
                };
                dict.SetAt(key, xrec);
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
            var xrec = new Xrecord
            {
                Data = new ResultBuffer(new TypedValue((short)DxfCode.Text, json))
            };
            dict.SetAt(key, xrec);
            tr.AddNewlyCreatedDBObject(xrec, true);
        }

        private static void WriteRoomXData(Transaction tr, Database db, JObject project)
        {
            EnsureRegApp(db, tr, RoomRegApp);
            foreach (JObject room in ObjectMap(project, "Rooms").Properties().Select(p => p.Value).OfType<JObject>())
            {
                ObjectId id;
                if (!TryGetObjectId(db, Clean(room["BoundaryHandle"]), out id)) continue;
                var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                if (ent == null) continue;
                ent.XData = BuildRoomXData(project, room);
            }
        }

        private static ResultBuffer BuildRoomXData(JObject project, JObject room)
        {
            var values = new List<TypedValue>
            {
                new TypedValue((short)DxfCode.ExtendedDataRegAppName, RoomRegApp)
            };
            AddX(values, "ProjectId", Clean(project["ProjectId"]));
            AddX(values, "RoomId", Clean(room["RoomId"]));
            AddX(values, "FloorId", Clean(room["FloorId"]));
            AddX(values, "Number", Clean(room["Number"]));
            AddX(values, "Name", Clean(room["Name"]));
            AddX(values, "SupplySystemId", Clean(room["SupplySystemId"]));
            AddX(values, "ExhaustSystemId", Clean(room["ExhaustSystemId"]));
            AddX(values, "Area", Format(ToDouble(room["Area"], 0), "0.##"));
            AddX(values, "Volume", Format(ToDouble(room["Volume"], 0), "0.##"));
            AddX(values, "SupplyFlow", Format(ToDouble(room["CalculatedSupply"], 0), "0"));
            AddX(values, "ExhaustFlow", Format(ToDouble(room["CalculatedExhaust"], 0), "0"));
            return new ResultBuffer(values.ToArray());
        }

        private static void AddX(List<TypedValue> values, string key, string value)
        {
            values.Add(new TypedValue((short)DxfCode.ExtendedDataAsciiString, key));
            values.Add(new TypedValue((short)DxfCode.ExtendedDataAsciiString, value ?? ""));
        }

        private static void EnsureRegApp(Database db, Transaction tr, string appName)
        {
            var rat = (RegAppTable)tr.GetObject(db.RegAppTableId, OpenMode.ForRead);
            if (rat.Has(appName)) return;
            rat.UpgradeOpen();
            var rec = new RegAppTableRecord { Name = appName };
            rat.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
        }

        private static bool TryGetObjectId(Database db, string handleString, out ObjectId id)
        {
            id = ObjectId.Null;
            if (string.IsNullOrWhiteSpace(handleString)) return false;
            long value;
            if (!long.TryParse(handleString, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)) return false;
            try
            {
                id = db.GetObjectId(false, new Handle(value), 0);
                return !id.IsNull && !id.IsErased;
            }
            catch
            {
                return false;
            }
        }

        private static void PatchString(JObject target, JObject source, string name)
        {
            if (source[name] != null) target[name] = Clean(source[name]) ?? "";
        }

        private static void PatchDouble(JObject target, JObject source, string name)
        {
            if (source[name] != null) target[name] = ToDouble(source[name], ToDouble(target[name], 0));
        }

        private static void PatchInt(JObject target, JObject source, string name)
        {
            if (source[name] != null) target[name] = ToInt(source[name], ToInt(target[name], 0));
        }

        private static void PatchBool(JObject target, JObject source, string name)
        {
            if (source[name] != null) target[name] = ToBool(source[name], ToBool(target[name], false));
        }

        private static double ToDouble(JToken token, double fallback)
        {
            if (token == null || token.Type == JTokenType.Null) return fallback;
            if (token.Type == JTokenType.Float || token.Type == JTokenType.Integer)
            {
                try
                {
                    return token.Value<double>();
                }
                catch
                {
                    return fallback;
                }
            }

            string raw = token.ToString().Trim();
            double value;
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out value)) return value;
            if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value)) return value;
            string normalized = raw.Replace(',', '.');
            if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return value;
            return fallback;
        }

        private static int ToInt(JToken token, int fallback)
        {
            if (token == null || token.Type == JTokenType.Null) return fallback;
            if (token.Type == JTokenType.Integer)
            {
                try
                {
                    return token.Value<int>();
                }
                catch
                {
                    return fallback;
                }
            }

            int value;
            string raw = token.ToString().Trim();
            return int.TryParse(raw, NumberStyles.Any, CultureInfo.CurrentCulture, out value) ||
                   int.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }

        private static bool ToBool(JToken token, bool fallback)
        {
            if (token == null || token.Type == JTokenType.Null) return fallback;
            bool value;
            return bool.TryParse(token.ToString(), out value) ? value : fallback;
        }

        private static string Format(double value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
    }
}
