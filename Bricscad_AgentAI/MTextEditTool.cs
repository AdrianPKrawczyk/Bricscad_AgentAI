using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using System;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;

namespace BricsCAD_Agent
{
    public class MTextEditTool : ITool
    {
        public string ActionTag => "[ACTION:MTEXT_EDIT]";

        public string Description =>
            "Dodaje lub zamienia tekst w MText. Wymaga JSON: " +
            "{\"Mode\": \"Append\"|\"Prepend\"|\"Replace\", \"Text\": \"tekst do dodania/podmiany\" (puste przy usuwaniu), \"FindText\": \"szukany tekst\" (tylko dla Replace), \"Color\": nr_koloru, \"Underline\": true/false, \"Bold\": true/false, \"Italic\": true/false}";

        public string Execute(Document doc, string jsonArgs)
        {
            Editor ed = doc.Editor;
            ObjectId[] ids = Komendy.AktywneZaznaczenie;

            if (ids == null || ids.Length == 0)
            {
                return "[Błąd]: Nie mam w pamięci żadnych obiektów! Zaznacz je myszką.";
            }

            string mode = Regex.Match(jsonArgs, @"\""Mode\""\s*:\s*\""(.*?)\""").Groups[1].Value;
            string text = Regex.Match(jsonArgs, @"\""Text\""\s*:\s*\""(.*?)\""").Groups[1].Value;

            // --- NOWOŚĆ: OBSŁUGA RPN ---
            if (text.StartsWith("RPN:"))
            {
                try { text = RpnCalculator.Evaluate(text.Substring(4).Trim(), null, null, ed); }
                catch (Exception ex) { return $"[Błąd RPN w MTEXT_EDIT]: {ex.Message}"; }
            }
            // -------------------------

            string findText = Regex.Match(jsonArgs, @"\""FindText\""\s*:\s*\""(.*?)\""").Groups[1].Value; string colorStr = Regex.Match(jsonArgs, @"\""Color\""\s*:\s*(\d+)").Groups[1].Value;
            bool underline = Regex.IsMatch(jsonArgs, @"\""Underline\""\s*:\s*true", RegexOptions.IgnoreCase);
            bool bold = Regex.IsMatch(jsonArgs, @"\""Bold\""\s*:\s*true", RegexOptions.IgnoreCase);
            bool italic = Regex.IsMatch(jsonArgs, @"\""Italic\""\s*:\s*true", RegexOptions.IgnoreCase);

            if (string.IsNullOrEmpty(mode)) return "[Błąd Narzędzia]: Brak wymaganego parametru Mode.";
            if ((mode == "Append" || mode == "Prepend") && string.IsNullOrEmpty(text)) return "[Błąd Narzędzia]: Tryb Append/Prepend wymaga parametru Text.";
            if (mode == "Replace" && string.IsNullOrEmpty(findText)) return "[Błąd Narzędzia]: Tryb Replace wymaga parametru FindText (czego szukać).";

            string formattedText = text;
            string fontFormat = "";

            if (bold || italic)
            {
                int b = bold ? 1 : 0;
                int i = italic ? 1 : 0;
                fontFormat = $"\\fArial|b{b}|i{i};";
            }

            if (underline) formattedText = $"\\L{formattedText}\\l";

            if (!string.IsNullOrEmpty(colorStr) && int.TryParse(colorStr, out int c))
            {
                formattedText = $"\\C{c};{fontFormat}{formattedText}";
            }
            else if (!string.IsNullOrEmpty(fontFormat))
            {
                formattedText = $"{fontFormat}{formattedText}";
            }

            if ((underline || bold || italic || !string.IsNullOrEmpty(colorStr)) && !string.IsNullOrEmpty(text))
            {
                formattedText = $"{{{formattedText}}}";
            }

            int zmodyfikowane = 0;

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objId in ids)
                {
                    MText mtext = tr.GetObject(objId, OpenMode.ForWrite) as MText;
                    if (mtext == null) continue;

                    if (mode == "Append") mtext.Contents += formattedText;
                    else if (mode == "Prepend") mtext.Contents = formattedText + mtext.Contents;
                    else if (mode == "Replace" && !string.IsNullOrEmpty(findText)) mtext.Contents = mtext.Contents.Replace(findText, formattedText);

                    mtext.RecordGraphicsModified(true);
                    zmodyfikowane++;
                }
                tr.Commit();
            }
            return $"WYNIK: Zmodyfikowano treść {zmodyfikowane} obiektów MText.";
        }

        public string Execute(Document doc) { return Execute(doc, ""); }
    }
}