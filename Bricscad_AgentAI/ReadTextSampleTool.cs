using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using System;
using System.Collections.Generic;

namespace BricsCAD_Agent
{
    public class ReadTextSampleTool : ITool
    {
        public string ActionTag => "[ACTION:READ_SAMPLE]";
        public string Description => "Pobiera próbkę tekstów z zaznaczenia, abyś mógł je przeczytać przed edycją. Wymaga JSON: {}";

        public string Execute(Document doc, string jsonArgs)
        {
            ObjectId[] ids = Komendy.OstatnieZaznaczenie;
            if (ids == null || ids.Length == 0) return "WYNIK: Brak obiektów w pamięci.";

            List<string> teksty = new List<string>();
            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId id in ids)
                {
                    Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent is MText mt) teksty.Add(mt.Contents);
                    else if (ent is DBText dt) teksty.Add(dt.TextString);
                }
                tr.Commit();
            }

            if (teksty.Count == 0) return "WYNIK: W zaznaczeniu nie ma żadnych obiektów tekstowych.";

            // Twój genialny nieliniowy algorytm skalowania (pierwiastek z n, max 15)
            int sampleSize = Math.Min(15, Math.Max(1, (int)Math.Ceiling(Math.Sqrt(teksty.Count))));
            List<string> probki = new List<string>();

            // Równomierne próbkowanie (żeby złapać teksty z początku, środka i końca listy)
            double step = teksty.Count > 1 && sampleSize > 1 ? (double)(teksty.Count - 1) / (sampleSize - 1) : 1;
            for (int i = 0; i < sampleSize; i++)
            {
                int index = (int)Math.Round(i * step);
                if (index >= teksty.Count) index = teksty.Count - 1;
                if (!probki.Contains(teksty[index])) probki.Add(teksty[index]);
            }

            return $"Pobrano {probki.Count} próbek (z {teksty.Count} tekstów):\n" + string.Join("\n", probki);
        }

        public string Execute(Document doc) => Execute(doc, "");
    }
}