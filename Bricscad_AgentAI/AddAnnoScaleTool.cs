using System;
using Teigha.DatabaseServices;
using Bricscad.ApplicationServices;
using System.Text.RegularExpressions;

namespace BricsCAD_Agent
{
    public class AddAnnoScaleTool : ITool
    {
        public string ActionTag => "[ACTION:ADD_ANNO_SCALE]";
        public string Description => "Włącza opisowość i przypisuje konkretną skalę opisową (np. 1:50) do zaznaczonych obiektów (wymiary, teksty, bloki).";

        public string Execute(Document doc, string jsonArgs)
        {
            ObjectId[] ids = Komendy.AktywneZaznaczenie;
            if (ids == null || ids.Length == 0) return "WYNIK: Brak zaznaczonych obiektów.";

            string scaleName = "";
            Match mScale = Regex.Match(jsonArgs, @"\""Scale\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
            if (mScale.Success) scaleName = mScale.Groups[1].Value;

            if (string.IsNullOrEmpty(scaleName)) return "WYNIK: Nie podano nazwy skali w tagu (klucz 'Scale').";

            int zmieniono = 0;

            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    ObjectContextManager ocm = doc.Database.ObjectContextManager;
                    if (ocm == null) return "WYNIK: Błąd API - Brak ObjectContextManager.";

                    ObjectContextCollection occ = ocm.GetContextCollection("ACDB_ANNOTATIONSCALES");
                    if (occ == null) return "WYNIK: Błąd API - Słownik skal ACDB_ANNOTATIONSCALES jest pusty.";

                    ObjectContext context = occ.GetContext(scaleName);
                    if (context == null) return $"WYNIK: Skala '{scaleName}' nie istnieje na liście skal tego rysunku.";

                    foreach (ObjectId id in ids)
                    {
                        try
                        {
                            Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                            if (ent == null) continue;

                            if (ent is Dimension || ent is MText || ent is DBText || ent is BlockReference || ent is Leader || ent is MLeader || ent is Hatch)
                            {
                                // NAPRAWIONY KOD: Zabezpieczenie przed eNullObjectId
                                if (ent is BlockReference blkRef)
                                {
                                    ObjectId defId = blkRef.DynamicBlockTableRecord != ObjectId.Null ? blkRef.DynamicBlockTableRecord : blkRef.BlockTableRecord;
                                    BlockTableRecord btr = tr.GetObject(defId, OpenMode.ForWrite) as BlockTableRecord;

                                    if (btr != null && btr.Annotative == AnnotativeStates.False)
                                    {
                                        btr.Annotative = AnnotativeStates.True;
                                    }
                                    try { if (ent.Annotative == AnnotativeStates.False) ent.Annotative = AnnotativeStates.True; } catch { }
                                }
                                else
                                {
                                    if (ent.Annotative == AnnotativeStates.False)
                                    {
                                        ent.Annotative = AnnotativeStates.True;
                                    }
                                }

                                if (!ent.HasContext(context))
                                {
                                    ent.AddContext(context);
                                    zmieniono++;
                                }
                            }
                        }
                        catch { }
                    }
                    tr.Commit();
                }
            }

            return $"WYNIK: Pomyślnie włączono opisowość i dodano skalę '{scaleName}' do {zmieniono} obiektów.";
        }

        public string Execute(Document doc) => Execute(doc, "");
    }
}