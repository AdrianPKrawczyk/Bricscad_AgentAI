using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;
using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    public class RemoveAnnoScaleTool : ITool
    {
        public string ActionTag => "[ACTION:REMOVE_ANNO_SCALE]";
        public string Description => "Usuwa konkretną skalę opisową lub całkowicie wyłącza opisowość (działa na definicjach bloków).";

        public string Execute(Document doc, string jsonArgs)
        {
            ObjectId[] ids = Komendy.AktywneZaznaczenie;
            if (ids == null || ids.Length == 0) return "WYNIK: Brak zaznaczonych obiektów.";

            string scaleName = "";
            Match mScale = Regex.Match(jsonArgs, @"\""Scale\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mScale.Success) scaleName = mScale.Groups[1].Value;

            bool removeAll = false;
            Match mMode = Regex.Match(jsonArgs, @"\""Mode\""\s*:\s*\""RemoveAll\""", RegexOptions.IgnoreCase);
            if (mMode.Success) removeAll = true;

            if (string.IsNullOrEmpty(scaleName) && !removeAll)
                return "WYNIK: Nie podano skali (Scale) ani trybu (Mode: RemoveAll).";

            int zmieniono = 0;
            int btrZmieniono = 0;

            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    ObjectContextManager ocm = doc.Database.ObjectContextManager;
                    ObjectContextCollection occ = ocm?.GetContextCollection("ACDB_ANNOTATIONSCALES");
                    ObjectContext context = null;

                    List<ObjectContext> allScales = new List<ObjectContext>();
                    if (occ != null) { foreach (ObjectContext ctx in occ) allScales.Add(ctx); }

                    if (!removeAll)
                    {
                        if (occ == null) return "WYNIK: Błąd API - Słownik skal pusty.";
                        context = occ.GetContext(scaleName);
                        if (context == null) return $"WYNIK: Skala '{scaleName}' nie istnieje w tym rysunku.";
                    }

                    HashSet<ObjectId> processedBTRs = new HashSet<ObjectId>();

                    foreach (ObjectId id in ids)
                    {
                        try
                        {
                            Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                            if (ent == null) continue;

                            bool isAnnotative = false;
                            BlockTableRecord btr = null;

                            // NAPRAWIONY KOD: Zabezpieczenie przed eNullObjectId
                            if (ent is BlockReference blkRef)
                            {
                                ObjectId defId = blkRef.DynamicBlockTableRecord != ObjectId.Null ? blkRef.DynamicBlockTableRecord : blkRef.BlockTableRecord;
                                btr = tr.GetObject(defId, OpenMode.ForWrite) as BlockTableRecord;
                                isAnnotative = (btr != null && btr.Annotative == AnnotativeStates.True) || (ent.Annotative == AnnotativeStates.True);
                            }
                            else
                            {
                                isAnnotative = (ent.Annotative == AnnotativeStates.True);
                            }

                            if (removeAll)
                            {
                                // Uderzamy podwójnie: W bibliotekę bloku ORAZ we wstawkę
                                if (btr != null && !processedBTRs.Contains(btr.ObjectId))
                                {
                                    btr.Annotative = AnnotativeStates.False;
                                    processedBTRs.Add(btr.ObjectId);
                                    btrZmieniono++;
                                }

                                if (isAnnotative)
                                {
                                    try { ent.Annotative = AnnotativeStates.False; } catch { }
                                    foreach (ObjectContext ctx in allScales)
                                    {
                                        if (ent.HasContext(ctx)) ent.RemoveContext(ctx);
                                    }
                                    zmieniono++;
                                }
                            }
                            else if (context != null && isAnnotative)
                            {
                                if (ent.HasContext(context))
                                {
                                    ent.RemoveContext(context);
                                    zmieniono++;
                                }
                            }
                        }
                        catch { }
                    }
                    tr.Commit();
                }
            }

            if (removeAll) return $"WYNIK: Całkowicie wyłączono opisowość dla {zmieniono} obiektów/wstawek (zmodyfikowano {btrZmieniono} definicji bloków w bazie).";
            return $"WYNIK: Usunięto skalę '{scaleName}' z {zmieniono} obiektów.";
        }

        public string Execute(Document doc) => Execute(doc, "");
    }
}