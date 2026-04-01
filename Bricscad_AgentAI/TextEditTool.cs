using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using System;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;

namespace BricsCAD_Agent
{
    public class TextEditTool : ITool
    {
        public string ActionTag => "[ACTION:TEXT_EDIT]";

        // ZMIANA: Zaktualizowany opis, by Agent wiedział, że to narzędzie uniwersalne
        public string Description =>
            "Dodaje lub zamienia tekst w obiektach TEXT (DBText) oraz MText. Wymaga JSON: " +
            "{\"Mode\": \"Append\"|\"Prepend\"|\"Replace\", \"Text\": \"tekst do dodania/podmiany\" (zostaw puste przy usuwaniu), \"FindText\": \"szukany tekst\" (tylko dla Replace), \"Color\": nr_koloru (opcjonalnie)}";

        public string Execute(Document doc, string jsonArgs)
        {
            Editor ed = doc.Editor;
            ObjectId[] ids = Komendy.AktywneZaznaczenie;

            if (ids == null || ids.Length == 0) return "[Błąd]: Nie mam w pamięci żadnych obiektów! Zaznacz je myszką.";

            string mode = Regex.Match(jsonArgs, @"\""Mode\""\s*:\s*\""(.*?)\""").Groups[1].Value;
            string text = Regex.Match(jsonArgs, @"\""Text\""\s*:\s*\""(.*?)\""").Groups[1].Value;

            // --- WSTRZYKIWANIE RPN ---
            if (text.StartsWith("RPN:"))
            {
                try { text = RpnCalculator.Evaluate(text.Substring(4).Trim(), null, null, ed); }
                catch (Exception ex) { return $"[Błąd RPN w TEXT_EDIT]: {ex.Message}"; }
            }
            // -------------------------
            string findText = Regex.Match(jsonArgs, @"\""FindText\""\s*:\s*\""(.*?)\""").Groups[1].Value;
            string colorStr = Regex.Match(jsonArgs, @"\""Color\""\s*:\s*(\d+)").Groups[1].Value;

            if (string.IsNullOrEmpty(mode)) return "[Błąd Narzędzia]: Brak wymaganego parametru Mode.";
            if ((mode == "Append" || mode == "Prepend") && string.IsNullOrEmpty(text)) return "[Błąd Narzędzia]: Tryb Append/Prepend wymaga parametru Text.";
            if (mode == "Replace" && string.IsNullOrEmpty(findText)) return "[Błąd Narzędzia]: Tryb Replace wymaga parametru FindText (czego szukać).";

            int zmodyfikowane = 0;

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objId in ids)
                {
                    // ZMIANA: Pobieramy obiekt jako ogólne Entity
                    Entity ent = tr.GetObject(objId, OpenMode.ForWrite) as Entity;
                    if (ent == null) continue;

                    // --- OBSŁUGA DBTEXT ---
                    if (ent is DBText dbText)
                    {
                        if (mode == "Append") dbText.TextString += text;
                        else if (mode == "Prepend") dbText.TextString = text + dbText.TextString;
                        else if (mode == "Replace" && !string.IsNullOrEmpty(findText)) dbText.TextString = dbText.TextString.Replace(findText, text);

                        if (!string.IsNullOrEmpty(colorStr) && int.TryParse(colorStr, out int c)) dbText.ColorIndex = c;

                        dbText.RecordGraphicsModified(true);
                        zmodyfikowane++;
                    }
                    // --- OBSŁUGA MTEXT ---
                    else if (ent is MText mText)
                    {
                        if (mode == "Append") mText.Contents += text;
                        else if (mode == "Prepend") mText.Contents = text + mText.Contents;
                        else if (mode == "Replace" && !string.IsNullOrEmpty(findText)) mText.Contents = mText.Contents.Replace(findText, text);

                        if (!string.IsNullOrEmpty(colorStr) && int.TryParse(colorStr, out int c)) mText.ColorIndex = c;

                        mText.RecordGraphicsModified(true);
                        zmodyfikowane++;
                    }
                }
                tr.Commit();
            }
            return $"WYNIK: Zmodyfikowano treść {zmodyfikowane} obiektów tekstowych.";
        }

        public string Execute(Document doc) { return Execute(doc, ""); }
    }
}