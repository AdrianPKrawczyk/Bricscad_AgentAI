using System;
using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using CadLayout = Teigha.DatabaseServices.Layout;

namespace Bricscad_AgentAI_V2.Core
{
    public static class LayoutHelpers
    {
        public const string ModelLayoutName = "Model";

        public static CadLayout GetLayoutByName(Database db, string layoutName, Transaction tr)
        {
            if (string.IsNullOrWhiteSpace(layoutName))
            {
                return null;
            }

            DBDictionary layoutDict = tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
            if (layoutDict == null || !layoutDict.Contains(layoutName))
            {
                return null;
            }

            ObjectId layoutId = layoutDict.GetAt(layoutName);
            return tr.GetObject(layoutId, OpenMode.ForRead) as CadLayout;
        }

        public static CadLayout GetLayoutByNameOrCurrent(Database db, Transaction tr, string layoutName)
        {
            if (string.IsNullOrWhiteSpace(layoutName))
            {
                return GetCurrentLayout(db, tr);
            }
            return GetLayoutByName(db, layoutName, tr);
        }

        public static CadLayout GetCurrentLayout(Database db, Transaction tr)
        {
            ObjectId currentSpaceId = db.CurrentSpaceId;
            if (currentSpaceId.IsNull)
            {
                return null;
            }

            ObjectId layoutDictId = db.LayoutDictionaryId;
            DBDictionary layoutDict = tr.GetObject(layoutDictId, OpenMode.ForRead) as DBDictionary;
            if (layoutDict == null)
            {
                return null;
            }

            foreach (DBDictionaryEntry entry in layoutDict)
            {
                ObjectId id = entry.Value;
                CadLayout layout = tr.GetObject(id, OpenMode.ForRead) as CadLayout;
                if (layout != null && layout.BlockTableRecordId == currentSpaceId)
                {
                    return layout;
                }
            }

            return null;
        }

        public static bool IsModelLayout(CadLayout layout)
        {
            return layout != null && layout.ModelType;
        }

        public static bool LayoutExists(Database db, string layoutName, Transaction tr)
        {
            if (string.IsNullOrWhiteSpace(layoutName)) return false;
            DBDictionary layoutDict = tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
            return layoutDict != null && layoutDict.Contains(layoutName);
        }

        public static List<string> ListLayoutNames(Database db, Transaction tr)
        {
            var names = new List<string>();
            DBDictionary layoutDict = tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
            if (layoutDict == null) return names;

            foreach (DBDictionaryEntry entry in layoutDict)
            {
                names.Add(entry.Key);
            }
            return names;
        }

        public static string ValidateAndResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                if (System.IO.Path.IsPathRooted(path))
                {
                    return path;
                }

                string pluginDir = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                string resolved = System.IO.Path.Combine(pluginDir, path);
                if (System.IO.File.Exists(resolved))
                {
                    return resolved;
                }
                return path;
            }
            catch
            {
                return path;
            }
        }
    }
}