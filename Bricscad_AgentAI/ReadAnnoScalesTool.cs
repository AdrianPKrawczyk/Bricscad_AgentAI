using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;
using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    public class ReadAnnoScalesTool : ITool
    {
        public string ActionTag => "[ACTION:READ_ANNO_SCALES]";
        public string Description => "Odczytuje skale opisowe. Tryb Summary (podsumowanie) lub Detailed (dla każdego obiektu z osobna).";

        public string Execute(Document doc, string jsonArgs)
        {
            ObjectId[] ids = Komendy.AktywneZaznaczenie;
            if (ids == null || ids.Length == 0) return "WYNIK: Brak zaznaczonych obiektów.";

            string mode = "Summary";
            Match mMode = Regex.Match(jsonArgs, @"\""Mode\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mMode.Success) mode = mMode.Groups[1].Value;

            HashSet<string> unikalneSkale = new HashSet<string>();
            List<string> wynikiSzczegolowe = new List<string>();
            int opisoweCount = 0;

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                ObjectContextManager ocm = doc.Database.ObjectContextManager;
                if (ocm == null) return "WYNIK: Błąd API - Brak ObjectContextManager.";
                ObjectContextCollection occ = ocm.GetContextCollection("ACDB_ANNOTATIONSCALES");

                List<ObjectContext> allScales = new List<ObjectContext>();
                if (occ != null) { foreach (ObjectContext ctx in occ) allScales.Add(ctx); }

                foreach (ObjectId id in ids)
                {
                    try
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent == null) continue;

                        bool isAnnotative = false;

                        // NAPRAWIONY KOD: Zabezpieczenie przed eNullObjectId
                        if (ent is BlockReference blkRef)
                        {
                            ObjectId defId = blkRef.DynamicBlockTableRecord != ObjectId.Null ? blkRef.DynamicBlockTableRecord : blkRef.BlockTableRecord;
                            BlockTableRecord btr = tr.GetObject(defId, OpenMode.ForRead) as BlockTableRecord;

                            isAnnotative = (btr != null && btr.Annotative == AnnotativeStates.True) || (ent.Annotative == AnnotativeStates.True);
                        }
                        else
                        {
                            isAnnotative = (ent.Annotative == AnnotativeStates.True);
                        }

                        if (isAnnotative)
                        {
                            opisoweCount++;
                            List<string> skaleObiektu = new List<string>();

                            foreach (ObjectContext ctx in allScales)
                            {
                                if (ent.HasContext(ctx))
                                {
                                    unikalneSkale.Add(ctx.Name);
                                    skaleObiektu.Add(ctx.Name);
                                }
                            }

                            if (mode.Equals("Detailed", StringComparison.OrdinalIgnoreCase))
                            {
                                string lista = skaleObiektu.Count > 0 ? string.Join(", ", skaleObiektu) : "Brak";
                                wynikiSzczegolowe.Add($"- Obiekt [{ent.GetType().Name}]: {lista}");
                            }
                        }
                        else if (mode.Equals("Detailed", StringComparison.OrdinalIgnoreCase))
                        {
                            wynikiSzczegolowe.Add($"- Obiekt [{ent.GetType().Name}]: Brak (Nie jest Opisowy)");
                        }
                    }
                    catch (Exception ex)
                    {
                        if (mode.Equals("Detailed", StringComparison.OrdinalIgnoreCase)) wynikiSzczegolowe.Add($"- Obiekt: BŁĄD ({ex.Message})");
                    }
                }
                tr.Commit();
            }

            if (mode.Equals("Detailed", StringComparison.OrdinalIgnoreCase))
                return "WYNIK ODCZYTU SKAL OPISOWYCH (Szczegółowy):\n" + string.Join("\n", wynikiSzczegolowe);
            else
            {
                if (opisoweCount == 0) return "WYNIK: W zaznaczeniu nie ma żadnych obiektów opisowych (Annotative).";
                if (unikalneSkale.Count == 0) return $"WYNIK: Znaleziono {opisoweCount} obiekt(ów) opisowych, ale z powodu błędu CAD nie mają przypisanej żadnej skali.";

                List<string> posortowane = unikalneSkale.ToList();
                posortowane.Sort();
                return $"WYNIK: Przeanalizowano {opisoweCount} obiektów opisowych. Ich przypisane skale to: {string.Join(", ", posortowane)}";
            }
        }

        public string Execute(Document doc) => Execute(doc, "");
    }
}