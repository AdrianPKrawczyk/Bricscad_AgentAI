using System;
using System.Collections.Generic;
using System.Linq;
using Teigha.DatabaseServices;
using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    // NOWOŚĆ: Dopisek ": ITool"
    public class ListBlocksTool : ITool
    {
        // NOWOŚĆ: Zwróć uwagę na argumenty. Muszą być dokładnie takie jak w innych narzędziach (np. dodany string jsonArgs)
        public string Execute(string jsonArgs, Document doc, List<ObjectId> aktywneZaznaczenie)
        {
            if (aktywneZaznaczenie == null || aktywneZaznaczenie.Count == 0)
            {
                return "WYNIK: Brak zaznaczonych obiektów.";
            }

            HashSet<string> unikalneNazwy = new HashSet<string>();

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in aktywneZaznaczenie)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is BlockReference blkRef)
                    {
                        // Chroni przed nazwami bloków dynamicznych (*U...)
                        BlockTableRecord btr = tr.GetObject(blkRef.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord;

                        if (btr != null)
                        {
                            unikalneNazwy.Add(btr.Name);
                        }
                        else
                        {
                            unikalneNazwy.Add(blkRef.Name);
                        }
                    }
                }
                tr.Commit();
            }

            if (unikalneNazwy.Count == 0)
            {
                return "WYNIK: W zaznaczeniu nie znaleziono żadnych bloków.";
            }

            List<string> posortowane = unikalneNazwy.ToList();
            posortowane.Sort();
            string lista = string.Join(", ", posortowane);

            return $"WYNIK: Znaleziono unikalne bloki ({posortowane.Count}): {lista}";
        }
    }
}