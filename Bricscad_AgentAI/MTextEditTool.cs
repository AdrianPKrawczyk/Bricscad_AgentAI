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
            "{\"Mode\": \"Append\"|\"Prepend\"|\"Replace\", \"Text\": \"tekst do dodania/podmiany\", \"FindText\": \"szukany tekst\" (tylko dla Replace), \"Color\": nr_koloru (opcjonalnie), \"Underline\": true/false}";

        public void Execute(Document doc, string jsonArgs)
        {
            Editor ed = doc.Editor;
            ObjectId[] ids = Komendy.OstatnieZaznaczenie;

            if (ids == null || ids.Length == 0)
            {
                ed.WriteMessage("\n[Bielik]: Nie mam w pamięci żadnych obiektów! Zaznacz je myszką.");
                return;
            }

            // --- BEZPIECZNE PARSOWANIE JSON (.*? pozwala wyłapać spacje na początku i końcu słowa) ---
            string mode = Regex.Match(jsonArgs, @"\""Mode\""\s*:\s*\""(.*?)\""").Groups[1].Value;
            string text = Regex.Match(jsonArgs, @"\""Text\""\s*:\s*\""(.*?)\""").Groups[1].Value;
            string findText = Regex.Match(jsonArgs, @"\""FindText\""\s*:\s*\""(.*?)\""").Groups[1].Value;
            string colorStr = Regex.Match(jsonArgs, @"\""Color\""\s*:\s*(\d+)").Groups[1].Value;
            bool underline = Regex.IsMatch(jsonArgs, @"\""Underline\""\s*:\s*true", RegexOptions.IgnoreCase);

            if (string.IsNullOrEmpty(mode) || string.IsNullOrEmpty(text))
            {
                ed.WriteMessage("\n[Błąd Narzędzia]: Brak wymaganego parametru Mode lub Text.");
                return;
            }

            // --- BUDOWANIE FORMATOWANIA DLA NOWEGO TEKSTU ---
            string formattedText = text;

            if (underline)
            {
                formattedText = $"\\L{formattedText}\\l"; // \L to start podkreślenia, \l to jego koniec
            }

            if (!string.IsNullOrEmpty(colorStr) && int.TryParse(colorStr, out int c))
            {
                formattedText = $"\\C{c};{formattedText}"; // dodajemy definicję koloru
            }

            // Jeśli dodaliśmy specjalne formatowanie, musimy zamknąć tekst w "ochronne" klamry RTF
            if (underline || !string.IsNullOrEmpty(colorStr))
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

                    if (mode == "Append")
                    {
                        mtext.Contents += formattedText;
                    }
                    else if (mode == "Prepend")
                    {
                        mtext.Contents = formattedText + mtext.Contents;
                    }
                    else if (mode == "Replace" && !string.IsNullOrEmpty(findText))
                    {
                        mtext.Contents = mtext.Contents.Replace(findText, formattedText);
                    }

                    mtext.RecordGraphicsModified(true);
                    zmodyfikowane++;
                }
                tr.Commit();
            }
            ed.WriteMessage($"\n[Sukces MText Edit]: Zmodyfikowano treść {zmodyfikowane} obiektów.");
        }

        public void Execute(Document doc) { Execute(doc, ""); }
    }
}