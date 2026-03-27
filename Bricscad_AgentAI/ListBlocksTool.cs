using System;
using System.Collections.Generic;
using System.Linq;
using Teigha.DatabaseServices;
using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    public class ListBlocksTool : ITool
    {
        // 1. Wymagane właściwości z interfejsu (to dzięki nim AgentCommand sam rozpoznaje to narzędzie!)
        public string ActionTag => "[ACTION:LIST_BLOCKS]";
        public string Description => "Zwraca listę wszystkich unikalnych (niepowtarzających się) nazw bloków z aktualnego zaznaczenia. Nie wymaga argumentów JSON.";

        // 2. Główna metoda Execute
        public string Execute(Document doc, string jsonArgs)
        {
            // Pobieramy zaznaczenie z pamięci globalnej Agenta, dokładnie tak jak w AnalyzeSelectionTool
            ObjectId[] ids = Komendy.AktywneZaznaczenie;

            if (ids == null || ids.Length == 0)
            {
                return "WYNIK: Brak zaznaczonych obiektów.";
            }

            HashSet<string> unikalneNazwy = new HashSet<string>();

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in ids)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is BlockReference blkRef)
                    {
                        // Domyślnie bierzemy zwykłą nazwę
                        string nazwaBloku = blkRef.Name;

                        try
                        {
                            // Pobieramy ID definicji bloku dynamicznego
                            ObjectId dynId = blkRef.DynamicBlockTableRecord;

                            // UPEWNIAMY SIĘ, że ID nie jest puste (chroni przed eNullObjectId)
                            if (dynId != ObjectId.Null)
                            {
                                BlockTableRecord btr = tr.GetObject(dynId, OpenMode.ForRead) as BlockTableRecord;
                                if (btr != null && !string.IsNullOrEmpty(btr.Name))
                                {
                                    nazwaBloku = btr.Name; // Nadpisujemy prawdziwą nazwą, jeśli to blok dynamiczny
                                }
                            }
                        }
                        catch { } // Jeśli cokolwiek pójdzie nie tak z API, program i tak ma już domyślną nazwę

                        unikalneNazwy.Add(nazwaBloku);
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

        // 3. Druga wymagana metoda z interfejsu (przekierowuje do głównej z pustym JSON-em)
        public string Execute(Document doc) => Execute(doc, "");
    }
}