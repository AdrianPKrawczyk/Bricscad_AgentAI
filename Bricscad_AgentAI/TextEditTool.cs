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

        public string Description =>
            "Dodaje lub zamienia tekst w zwykłym obiekcie TEXT (DBText). Wymaga JSON: " +
            "{\"Mode\": \"Append\"|\"Prepend\"|\"Replace\", \"Text\": \"tekst do dodania/podmiany\" (zostaw puste przy usuwaniu), \"FindText\": \"szukany tekst\" (tylko dla Replace), \"Color\": nr_koloru (opcjonalnie)}";

        public string Execute(Document doc, string jsonArgs)
        {
            Editor ed = doc.Editor;
            ObjectId[] ids = Komendy.OstatnieZaznaczenie;

            if (ids == null || ids.Length == 0)
            {
                ed.WriteMessage("\n[Bielik]: Nie mam w pamięci żadnych obiektów! Zaznacz je myszką.");
                return;
            }

            string mode = Regex.Match(jsonArgs, @"\""Mode\""\s*:\s*\""(.*?)\""").Groups[1].Value;
            string text = Regex.Match(jsonArgs, @"\""Text\""\s*:\s*\""(.*?)\""").Groups[1].Value;
            string findText = Regex.Match(jsonArgs, @"\""FindText\""\s*:\s*\""(.*?)\""").Groups[1].Value;
            string colorStr = Regex.Match(jsonArgs, @"\""Color\""\s*:\s*(\d+)").Groups[1].Value;

            // --- POPRAWIONE ZABEZPIECZENIA ---
            if (string.IsNullOrEmpty(mode))
            {
                ed.WriteMessage("\n[Błąd Narzędzia]: Brak wymaganego parametru Mode.");
                return;
            }
            if ((mode == "Append" || mode == "Prepend") && string.IsNullOrEmpty(text))
            {
                ed.WriteMessage("\n[Błąd Narzędzia]: Tryb Append/Prepend wymaga parametru Text.");
                return;
            }
            if (mode == "Replace" && string.IsNullOrEmpty(findText))
            {
                ed.WriteMessage("\n[Błąd Narzędzia]: Tryb Replace wymaga parametru FindText (czego szukać).");
                return;
            }

            int zmodyfikowane = 0;

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objId in ids)
                {
                    DBText dbText = tr.GetObject(objId, OpenMode.ForWrite) as DBText;
                    if (dbText == null) continue;

                    if (mode == "Append")
                    {
                        dbText.TextString += text;
                    }
                    else if (mode == "Prepend")
                    {
                        dbText.TextString = text + dbText.TextString;
                    }
                    else if (mode == "Replace" && !string.IsNullOrEmpty(findText))
                    {
                        // Jeśli "text" jest puste, po prostu usunie "findText"
                        dbText.TextString = dbText.TextString.Replace(findText, text);
                    }

                    if (!string.IsNullOrEmpty(colorStr) && int.TryParse(colorStr, out int c))
                    {
                        dbText.ColorIndex = c;
                    }

                    dbText.RecordGraphicsModified(true);
                    zmodyfikowane++;
                }
                tr.Commit();
            }
            ed.WriteMessage($"\n[Sukces TEXT Edit]: Zmodyfikowano treść {zmodyfikowane} obiektów TEXT.");
        }

        public void Execute(Document doc) { Execute(doc, ""); }
    }
}