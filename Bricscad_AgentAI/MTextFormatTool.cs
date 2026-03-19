using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Teigha.DatabaseServices;
using Teigha.Runtime;

namespace BricsCAD_Agent
{
    public class MTextFormatTool : ITool
    {
        public string ActionTag => "[ACTION:MTEXT_FORMAT]";

        // ZMIANA 1: Aktualizacja opisu dla AI
        public string Description =>
            "Edytuje formatowanie MText. Wymaga JSON: " +
            "{\"Mode\": \"HighlightWord\"|\"FormatAll\"|\"ClearFormatting\", \"Word\": \"słowo\" (tylko HighlightWord), \"Color\": 1-czerwony, 2-żółty, 3-zielony itd., \"Bold\": true/false}";

        public void Execute(Document doc, string jsonArgs)
        {
            Editor ed = doc.Editor;
            PromptSelectionResult selRes = ed.SelectImplied();
            ObjectId[] ids;

            if (selRes.Status == PromptStatus.OK) { ids = selRes.Value.GetObjectIds(); }
            else
            {
                ed.WriteMessage("\n[Narzędzie MText]: Nie wykryto zaznaczenia przez SelectImplied. Upewnij się, że obiekty są podświetlone.");
                return;
            }

            string mode = Regex.Match(jsonArgs, @"\""Mode\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
            string word = Regex.Match(jsonArgs, @"\""Word\""\s*:\s*\""([^\""]+)\""").Groups[1].Value;
            string colorStr = Regex.Match(jsonArgs, @"\""Color\""\s*:\s*(\d+)").Groups[1].Value;
            bool bold = jsonArgs.IndexOf("\"Bold\": true", StringComparison.OrdinalIgnoreCase) >= 0;
            int color = int.TryParse(colorStr, out int c) ? c : 3;

            int zmodyfikowane = 0;

            using (Transaction tr = doc.TransactionManager.StartTransaction())
            {
                foreach (ObjectId objId in ids)
                {
                    MText mtext = tr.GetObject(objId, OpenMode.ForWrite) as MText;
                    if (mtext == null) continue;

                    string zawartosc = mtext.Contents;

                    // ZMIANA 2: Dodajemy obsługę różnych trybów (FormatAll i ClearFormatting)
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
                    else if (mode == "FormatAll") // NOWY TRYB DO FORMATOWANIA CAŁOŚCI
                    {
                        string formatCode = $"\\C{color};";
                        if (bold) formatCode += "\\fArial|b1;";

                        // Używamy mtext.Text (czysty tekst bez rtf) i owijamy go formatowaniem
                        mtext.Contents = $"{{{formatCode}{mtext.Text}}}";
                        mtext.RecordGraphicsModified(true);
                        zmodyfikowane++;
                    }
                    else if (mode == "ClearFormatting") // NAPRAWIONE CZYSZCZENIE
                    {
                        mtext.Contents = mtext.Text;
                        mtext.RecordGraphicsModified(true);
                        zmodyfikowane++;
                    }
                }
                tr.Commit();
            }
            ed.WriteMessage($"\n[Sukces MText]: Zmodyfikowano {zmodyfikowane} obiekt(ów).");
        }

        public void Execute(Document doc) { Execute(doc, ""); }
    }
}