using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;
using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    public class ListUniqueTool : ITool
    {
        public string ActionTag => "[ACTION:LIST_UNIQUE]";
        public string Description => "Zwraca listę unikalnych klas lub właściwości (Selection/Model/Blocks/Database).";

        public string Execute(Document doc, string jsonArgs)
        {
            string target = "Class";
            Match mTarget = Regex.Match(jsonArgs, @"\""Target\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mTarget.Success) target = mTarget.Groups[1].Value;

            string scope = "Selection";
            Match mScope = Regex.Match(jsonArgs, @"\""Scope\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mScope.Success) scope = mScope.Groups[1].Value;

            string propName = "";
            Match mProp = Regex.Match(jsonArgs, @"\""Property\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mProp.Success) propName = mProp.Groups[1].Value;

            if (target.Equals("Property", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(propName))
                return "WYNIK: Dla Target='Property' musisz podać nazwę właściwości w kluczu 'Property'.";

            HashSet<string> unikalneWartosci = new HashSet<string>();
            int przeanalizowano = 0;

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                void ProcessEntity(Entity ent)
                {
                    przeanalizowano++;
                    if (target.Equals("Class", StringComparison.OrdinalIgnoreCase))
                    {
                        unikalneWartosci.Add(ent.GetType().Name);
                    }
                    else if (target.Equals("Property", StringComparison.OrdinalIgnoreCase))
                    {
                        if (propName.Equals("Name", StringComparison.OrdinalIgnoreCase) && ent is BlockReference blkRef)
                        {
                            ObjectId defId = blkRef.DynamicBlockTableRecord != ObjectId.Null ? blkRef.DynamicBlockTableRecord : blkRef.BlockTableRecord;
                            BlockTableRecord btrDef = tr.GetObject(defId, OpenMode.ForRead) as BlockTableRecord;
                            if (btrDef != null) unikalneWartosci.Add(btrDef.Name);
                            else unikalneWartosci.Add(blkRef.Name);
                            return;
                        }

                        string[] zagniezdzenia = propName.Split('.');
                        object wartoscObiektu = ent;
                        System.Reflection.PropertyInfo propInfo = null;

                        foreach (string czesc in zagniezdzenia)
                        {
                            if (wartoscObiektu == null) break;
                            propInfo = wartoscObiektu.GetType().GetProperty(czesc, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                            if (propInfo != null) wartoscObiektu = propInfo.GetValue(wartoscObiektu);
                            else { wartoscObiektu = null; break; }
                        }

                        if (wartoscObiektu != null)
                        {
                            string valStr = wartoscObiektu.ToString();
                            if (wartoscObiektu is Teigha.Geometry.Point3d pt)
                                valStr = $"({Math.Round(pt.X, 4)},{Math.Round(pt.Y, 4)},{Math.Round(pt.Z, 4)})";
                            else if (wartoscObiektu is Teigha.Colors.Color col)
                                valStr = col.ColorIndex.ToString();

                            unikalneWartosci.Add(valStr);
                        }
                    }
                }

                if (scope.Equals("Selection", StringComparison.OrdinalIgnoreCase))
                {
                    ObjectId[] ids = Komendy.AktywneZaznaczenie;
                    if (ids != null)
                    {
                        foreach (ObjectId id in ids)
                        {
                            try { Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity; if (ent != null) ProcessEntity(ent); } catch { }
                        }
                    }
                }
                else if (scope.Equals("Model", StringComparison.OrdinalIgnoreCase))
                {
                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForRead);
                    foreach (ObjectId id in btr)
                    {
                        try { Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity; if (ent != null) ProcessEntity(ent); } catch { }
                    }
                }
                else if (scope.Equals("Blocks", StringComparison.OrdinalIgnoreCase))
                {
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId btrId in bt)
                    {
                        try
                        {
                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                            if (!btr.IsLayout && !btr.IsAnonymous)
                            {
                                foreach (ObjectId id in btr)
                                {
                                    try { Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity; if (ent != null) ProcessEntity(ent); } catch { }
                                }
                            }
                        }
                        catch { }
                    }
                }
                // --- NOWOŚĆ: Przeszukiwanie Tablic Systemowych ---
                else if (scope.Equals("Database", StringComparison.OrdinalIgnoreCase))
                {
                    if (target.Equals("Property", StringComparison.OrdinalIgnoreCase) && propName.Equals("Layer", StringComparison.OrdinalIgnoreCase))
                    {
                        LayerTable lt = (LayerTable)tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead);
                        foreach (ObjectId ltrId in lt)
                        {
                            LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(ltrId, OpenMode.ForRead);
                            unikalneWartosci.Add(ltr.Name);
                            przeanalizowano++;
                        }
                    }
                    else if (target.Equals("Property", StringComparison.OrdinalIgnoreCase) && propName.Equals("Name", StringComparison.OrdinalIgnoreCase))
                    {
                        BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                        foreach (ObjectId btrId in bt)
                        {
                            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                            if (!btr.IsLayout && !btr.IsAnonymous)
                            {
                                unikalneWartosci.Add(btr.Name);
                                przeanalizowano++;
                            }
                        }
                    }
                }

                tr.Commit();
            }

            if (unikalneWartosci.Count == 0) return $"WYNIK: Przeanalizowano {przeanalizowano} elementów, ale nie znaleziono żadnych wartości.";

            List<string> posortowane = unikalneWartosci.ToList();
            posortowane.Sort();

            string scopePl = scope.Equals("Selection", StringComparison.OrdinalIgnoreCase) ? "zaznaczeniu" :
                             (scope.Equals("Model", StringComparison.OrdinalIgnoreCase) ? "modelu" :
                             (scope.Equals("Database", StringComparison.OrdinalIgnoreCase) ? "bazie danych (wszystkie zdefiniowane)" : "definicjach bloków"));

            string targetName = target.Equals("Class", StringComparison.OrdinalIgnoreCase) ? "klas (typów)" : $"właściwości '{propName}'";
            return $"WYNIK: W {scopePl} znaleziono unikalnych {targetName} ({posortowane.Count}): {string.Join(", ", posortowane)}";
        }

        public string Execute(Document doc) => Execute(doc, "");
    }
}