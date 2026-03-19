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

        public string Description =>
            "Edytuje wewnętrzne formatowanie MText (RTF). Wymaga argumentów JSON np.: " +
            "{\"Mode\": \"HighlightWord\", \"Word\": \"PVC\", \"Color\": 3, \"Bold\": true} " +
            "lub {\"Mode\": \"ClearFormatting\"}";

        // Zmodyfikowaliśmy nieco metodę, aby przyjmowała parametry z JSONa
        public void Execute(Document doc, string jsonArgs)
        {
            Editor ed = doc.Editor;

            // 1. Próba pobrania zaznaczenia (PickFirst)
            PromptSelectionResult selRes = ed.SelectImplied();
            ObjectId[] ids;

            if (selRes.Status == PromptStatus.OK)
            {
                ids = selRes.Value.GetObjectIds();
            }
            else
            {
                // FALLBACK: Jeśli SelectImplied nie zadziałało, spróbujmy pobrać to, co jest aktualnie podświetlone
                PromptSelectionResult selLast = ed.SelectAll(); // Pobiera wszystko, ale my przefiltrujemy po tym co jest w transakcji
                                                                // Uwaga: W profesjonalnym systemie tutaj lepiej byłoby przekazać listę ID z Agenta, 
                                                                // ale na razie spróbujmy wymusić odczyt zaznaczenia.
                ed.WriteMessage("\n[Narzędzie MText]: Nie wykryto zaznaczenia przez SelectImplied. Upewnij się, że obiekty są podświetlone.");
                return;
            }

            // Wyciąganie parametrów
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
                    ed.WriteMessage($"\n[Debug]: Przetwarzam MText: '{mtext.Text}'");

                    if (mode == "HighlightWord" && !string.IsNullOrEmpty(word))
                    {
                        // Uproszczony kod formatowania koloru (bez skomplikowanych czcionek na start)
                        // \C2; to kolor żółty, \f...; to czcionka. Spróbujmy sam kolor:
                        string formatCode = $"\\C{color};";
                        if (bold) formatCode += "\\fArial|b1;";

                        string sformatowaneSlowo = $"{{{formatCode}{word}}}";

                        if (zawartosc.Contains(word))
                        {
                            // Używamy zwykłego Replace zamiast Regex dla testu stabilności
                            zawartosc = zawartosc.Replace(word, sformatowaneSlowo);
                            mtext.Contents = zawartosc;
                            mtext.RecordGraphicsModified(true);
                            zmodyfikowane++;
                        }
                    }
                }
                tr.Commit();
            }
            ed.WriteMessage($"\n[Sukces MText]: Zmodyfikowano {zmodyfikowane} obiekt(ów).");
        }

        // Dostosowanie interfejsu do obsługi starych metod bez parametrów
        public void Execute(Document doc) { Execute(doc, ""); }
    }
}