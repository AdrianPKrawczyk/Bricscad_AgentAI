using Teigha.DatabaseServices;
using Bielik.DrukWidoki.Models;
using System.Collections.Generic;

namespace Bielik.DrukWidoki.Core
{
    public class ImportManager
    {
        public static List<BielikViewDef> ImportFromDwg(string filepath)
        {
            var views = new List<BielikViewDef>();
            using (Database extDb = new Database(false, true))
            {
                try
                {
                    extDb.ReadDwgFile(filepath, System.IO.FileShare.Read, true, "");
                    views = NodManager.LoadViews(extDb);
                }
                catch
                {
                    // Handle errors silently for now
                }
            }
            return views;
        }
    }
}
