using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using System;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;

namespace BricsCAD_Agent
{
    public class MTextFormatTool : ITool
    {
        public string ActionTag => "[ACTION:MTEXT_FORMAT]";

        public string Description =>
            "Edytuje formatowanie MText. Wymaga JSON: " +
            "{\"Mode\": \"HighlightWord\"|\"FormatAll\"|\"ClearFormatting\", \"Word\": \"słowo\" (tylko HighlightWord), \"Color\": nr_koloru (indeks ACI od 1 do 255), \"Bold\": true/false}";

        public void Execute(Document doc, string jsonArgs)
        {
            Editor ed = doc.Editor;

            // --- CZYTAMY WPROST Z PODŚWIADOMEJ PAMIĘCI AGENTA ---
            ObjectId[] ids = Komendy.OstatnieZaznaczenie;

            if (ids == null || ids.Length == 0)
            {
                ed.WriteMessage("\n[Bielik]: Nie mam w pamięci żadnych obiektów! Zaznacz je myszką lub każ mi je znaleźć (np. 'Zaznacz wszystkie teksty').");
                return;
            }

            // --- BEZPIECZNE PARSOWANIE JSON ---
            string mode = Regex.Match(jsonArgs, @"\""Mode\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
            string word = Regex.Match(jsonArgs, @"\""Word\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
            string colorStr = Regex.Match(jsonArgs, @"\""Color\""\s*:\s*(\d+)").Groups[1].Value;
            bool bold = Regex.IsMatch(jsonArgs, @"\""Bold\""\s*:\s*true", RegexOptions.IgnoreCase);
            int color = int.TryParse(colorStr, out int c) ? c : 3;

            int zmodyfikowane = 0;

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objId in ids)
                {
                    MText mtext = tr.GetObject(objId, OpenMode.ForWrite) as MText;
                    if (mtext == null) continue; // Ignorujemy, jeśli w pamięci plącze się np. linia

                    string zawartosc = mtext.Contents;

                    if (mode == "HighlightWord" && !string.IsNullOrEmpty(word))
                    {
                        string formatCode = $"\\C{color};";
                        if (bold) formatCode += "\\fArial|b1;";
                        string sformatowaneSlowo = $"{{{formatCode}{word}}}";

                        if (zawartosc.Contains(word))
                        {
                            zawartosc = zawartosc.Replace(word, sformatowaneSlowo);
                            mtext.Contents = zawartosc;
                            mtext.RecordGraphicsModified(true);
                            zmodyfikowane++;
                        }
                    }
                    else if (mode == "FormatAll")
                    {
                        string formatCode = $"\\C{color};";
                        if (bold) formatCode += "\\fArial|b1;";

                        mtext.Contents = $"{{{formatCode}{mtext.Text}}}";
                        mtext.RecordGraphicsModified(true);
                        zmodyfikowane++;
                    }
                    else if (mode == "ClearFormatting")
                    {
                        mtext.Contents = mtext.Text;
                        mtext.RecordGraphicsModified(true);
                        zmodyfikowane++;
                    }
                }
                tr.Commit();
            }
            ed.WriteMessage($"\n[Sukces MText]: Zmodyfikowano formatowanie {zmodyfikowane} obiektów.");
        }

        public void Execute(Document doc) { Execute(doc, ""); }
    }
}