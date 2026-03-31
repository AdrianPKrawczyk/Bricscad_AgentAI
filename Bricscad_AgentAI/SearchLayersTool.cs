using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;

namespace BricsCAD_Agent
{
    public class SearchLayersTool : ITool
    {
        public string ActionTag => "[ACTION:SEARCH_LAYERS]";
        public string Description => "Wyszukuje warstwy w rysunku na podstawie fragmentu nazwy (np. Contains, StartsWith).";

        public string Execute(Document doc, string jsonArgs)
        {
            // Oczyszczenie z ukośników dla bezpieczeństwa JSONL
            string cleanArgs = jsonArgs.Replace("\\\"", "\"").Replace("\\n", "\n");

            string condition = "Contains";
            Match mCond = Regex.Match(cleanArgs, @"""Condition""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (mCond.Success) condition = mCond.Groups[1].Value;

            string val = "";
            Match mVal = Regex.Match(cleanArgs, @"""Value""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (mVal.Success) val = mVal.Groups[1].Value;

            if (string.IsNullOrEmpty(val)) return "WYNIK: BŁĄD - Nie podano szukanej wartości w kluczu 'Value'.";

            List<string> foundLayers = new List<string>();

            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = doc.TransactionManager.StartTransaction())
                {
                    LayerTable lt = tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead) as LayerTable;
                    foreach (ObjectId ltrId in lt)
                    {
                        LayerTableRecord ltr = tr.GetObject(ltrId, OpenMode.ForRead) as LayerTableRecord;
                        string ln = ltr.Name;

                        bool match = false;
                        if (condition.Equals("Contains", StringComparison.OrdinalIgnoreCase))
                            match = ln.IndexOf(val, StringComparison.OrdinalIgnoreCase) >= 0;
                        else if (condition.Equals("StartsWith", StringComparison.OrdinalIgnoreCase))
                            match = ln.StartsWith(val, StringComparison.OrdinalIgnoreCase);
                        else if (condition.Equals("EndsWith", StringComparison.OrdinalIgnoreCase))
                            match = ln.EndsWith(val, StringComparison.OrdinalIgnoreCase);
                        else if (condition.Equals("Equals", StringComparison.OrdinalIgnoreCase))
                            match = ln.Equals(val, StringComparison.OrdinalIgnoreCase);

                        if (match) foundLayers.Add(ln);
                    }
                    tr.Commit();
                }
            }

            // --- NOWE: Obsługa zapisu do pamięci (SaveAs) ---
            string saveAs = "";
            Match mSave = Regex.Match(cleanArgs, @"""SaveAs""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (mSave.Success) saveAs = mSave.Groups[1].Value;

            if (!string.IsNullOrEmpty(saveAs) && foundLayers.Count > 0)
            {
                // Zapisujemy warstwy do pamięci, oddzielone " | " aby pętla FOREACH mogła po nich iterować
                AgentMemory.Variables[saveAs] = string.Join(" | ", foundLayers);
            }
            // ------------------------------------------------

            if (foundLayers.Count == 0) return $"WYNIK: Nie znaleziono żadnych warstw spełniających warunek {condition} '{val}'.";

            string pelnyWynik = $"WYNIK: Znaleziono warstwy ({foundLayers.Count}): {string.Join(", ", foundLayers)}";

            if (!string.IsNullOrEmpty(saveAs) && foundLayers.Count > 0)
            {
                pelnyWynik = $"[ZAPISANO W PAMIĘCI JAKO: @{saveAs}]\n" + pelnyWynik;
            }

            return pelnyWynik;
        }

        public string Execute(Document doc) => Execute(doc, "");
    }
}