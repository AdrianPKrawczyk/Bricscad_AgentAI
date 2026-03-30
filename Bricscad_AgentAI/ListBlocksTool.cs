using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;
using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    public class ListBlocksTool : ITool
    {
        public string ActionTag => "[ACTION:LIST_BLOCKS]";

        // Zaktualizowany opis dla interfejsu
        public string Description => "Zwraca listę unikalnych nazw bloków. Przyjmuje opcjonalny argument {\"Scope\": \"Selection\"|\"Database\"}.";

        public string Execute(Document doc, string jsonArgs)
        {
            // Domyślnie szukamy w zaznaczeniu
            string scope = "Selection";

            string saveAs = "";
            // Jeśli LLM podał argumenty, próbujemy wyciągnąć Scope i SaveAs
            if (!string.IsNullOrWhiteSpace(jsonArgs))
            {
                Match mScope = Regex.Match(jsonArgs, @"\""Scope\""\s*:\s*\""([^\""]+)\""");
                if (mScope.Success) scope = mScope.Groups[1].Value;

                Match mSave = Regex.Match(jsonArgs, @"\""SaveAs\""\s*:\s*\""([^\""]+)\""", RegexOptions.IgnoreCase);
                if (mSave.Success) saveAs = mSave.Groups[1].Value;
            }

            HashSet<string> unikalneNazwy = new HashSet<string>();

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                if (scope.Equals("Database", StringComparison.OrdinalIgnoreCase))
                {
                    // --- TRYB: SKANOWANIE CAŁEJ PAMIĘCI RYSUNKU ---
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);
                    foreach (ObjectId btrId in bt)
                    {
                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

                        // Odfiltrowujemy arkusze (Layouty), bloki anonimowe (zaczynające się od * itp.) i Xrefy
                        if (!btr.IsLayout && !btr.IsAnonymous && !btr.IsFromExternalReference && !btr.Name.StartsWith("*"))
                        {
                            unikalneNazwy.Add(btr.Name);
                        }
                    }
                }
                else
                {
                    // --- TRYB: SKANOWANIE AKTYWNEGO ZAZNACZENIA (STARE ZACHOWANIE) ---
                    ObjectId[] ids = Komendy.AktywneZaznaczenie;

                    if (ids == null || ids.Length == 0)
                    {
                        return "WYNIK: Brak zaznaczonych obiektów. Użyj {\"Scope\": \"Database\"} jeśli chcesz przeszukać pamięć rysunku.";
                    }

                    foreach (ObjectId id in ids)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent is BlockReference blkRef)
                        {
                            string nazwaBloku = blkRef.Name;
                            try
                            {
                                ObjectId dynId = blkRef.DynamicBlockTableRecord;
                                if (dynId != ObjectId.Null)
                                {
                                    BlockTableRecord btr = tr.GetObject(dynId, OpenMode.ForRead) as BlockTableRecord;
                                    if (btr != null && !string.IsNullOrEmpty(btr.Name)) nazwaBloku = btr.Name;
                                }
                            }
                            catch { }
                            unikalneNazwy.Add(nazwaBloku);
                        }
                    }
                }
                tr.Commit();
            }

            if (unikalneNazwy.Count == 0)
            {
                return scope.Equals("Database", StringComparison.OrdinalIgnoreCase)
                    ? "WYNIK: Ten rysunek nie posiada jeszcze żadnych zdefiniowanych bloków w pamięci."
                    : "WYNIK: W zaznaczeniu nie znaleziono żadnych bloków.";
            }

            List<string> posortowane = unikalneNazwy.ToList();
            posortowane.Sort();
            string lista = string.Join(", ", posortowane);

            string finalMsg = $"WYNIK: Znaleziono unikalne bloki ({posortowane.Count}): {lista}";

            if (!string.IsNullOrEmpty(saveAs))
            {
                AgentMemory.Variables[saveAs] = lista;
                finalMsg = $"[ZAPISANO W PAMIĘCI JAKO: @{saveAs}]\n" + finalMsg;
            }

            return finalMsg;
        }

        public string Execute(Document doc) => Execute(doc, "");
    }
}