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

        // ZMIANA: public string zamiast public void
        public string Execute(Document doc, string jsonArgs)
        {
            Editor ed = doc.Editor;
            ObjectId[] ids = Komendy.AktywneZaznaczenie;

            if (ids == null || ids.Length == 0)
            {
                // ZMIANA: Zwracamy tekst błędu do Agenta
                return "[Błąd]: Nie mam w pamięci żadnych obiektów! Zaznacz je myszką lub każ mi je znaleźć.";
            }

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
                    if (mtext == null) continue;

                    string zawartosc = mtext.Contents;

                    // --- GENIALNY HACK NA BRICSCADA ---
                    // BricsCAD właściwością .Text brutalnie wymazuje \P sklejając wyrazy.
                    // Tymczasowo zamieniamy \P na bezpieczny ciąg znaków (dodajemy spacje na wszelki wypadek)
                    mtext.Contents = zawartosc.Replace("\\P", " @@@NL@@@ ").Replace("\\n", " @@@NL@@@ ");

                    // Teraz CAD czyści kolory, formaty i czcionki, ale nasz znacznik przeżyje!
                    string wyczyszczonyZrtf = mtext.Text;

                    // Przywracamy obiektowi jego prawdziwy RTF (jest to krytyczne dla trybu HighlightWord)
                    mtext.Contents = zawartosc;

                    // Wymieniamy znaczniki z powrotem na enter MTextowy (\P)
                    string czystyTekst = wyczyszczonyZrtf.Replace(" @@@NL@@@ ", "\\P").Replace("@@@NL@@@", "\\P");

                    // ============================================

                    if (mode == "HighlightWord" && !string.IsNullOrEmpty(word))
                    {
                        string formatCode = $"\\C{color};";
                        if (bold) formatCode += "\\fArial|b1;";
                        string sformatowaneSlowo = $"{{{formatCode}{word}}}";

                        // Tu korzystamy z oryginalnej zawartosci (RTF), bo szukamy i podmieniamy w locie
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

                        // Używamy naszego uratowanego, czystego tekstu z zachowanymi enterami
                        mtext.Contents = $"{{{formatCode}{czystyTekst}}}";
                        mtext.RecordGraphicsModified(true);
                        zmodyfikowane++;
                    }
                    else if (mode == "ClearFormatting")
                    {
                        // Wrzucamy czysty tekst z uratowanymi enterami
                        mtext.Contents = czystyTekst;
                        mtext.RecordGraphicsModified(true);
                        zmodyfikowane++;
                    }
                }
                tr.Commit();
            }
            // ZMIANA: Zwracamy sukces do Agenta
            return $"WYNIK: Zmodyfikowano formatowanie {zmodyfikowane} obiektów MText.";
        }

        // ZMIANA: Wymagana do obsługi interfejsu
        public string Execute(Document doc) { return Execute(doc, ""); }
    }
}