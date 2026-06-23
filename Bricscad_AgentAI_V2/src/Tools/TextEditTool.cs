using System;
using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    public class TextEditTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "TextEditTool",
                    Description = "Modyfikuje treść oraz formatowanie wizualne (RTF) obiektów tekstowych (DBText, MText) w zaznaczeniu. " +
                                  "UWAGA: tryb Replace NIE nadpisuje tekstu zawierającego pola CAD (%<\\Ac...>) - " +
                                  "obiekt zostanie pominięty z ostrzeżeniem. Do pracy z polami użyj narzędzia ManageFields.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Mode", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Tryb edycji: Append (dodaj na końcu), Prepend (dodaj na początku), Replace (zamień), FormatHighlight (podświetl słowo), ClearFormatting (wyczyść formatowanie RTF).",
                                    Enum = new List<string> { "Append", "Prepend", "Replace", "FormatHighlight", "ClearFormatting" }
                                }
                            },
                            {
                                "FindText", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Tekst do znalezienia (wymagany dla Replace i FormatHighlight)."
                                }
                            },
                            {
                                "ReplaceWith", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nowa treść (używana w Append, Prepend, Replace)."
                                }
                            },
                            {
                                "ColorIndex", new ToolParameter
                                {
                                    Type = "integer",
                                    Description = "Indeks koloru ACI (1-255) dla FormatHighlight. Domyślnie 1 (Czerwony)."
                                }
                            },
                            {
                                "IsBold", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy zastosować pogrubienie dla FormatHighlight."
                                }
                            },
                            {
                                "AllowFieldOverride", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Domyślnie false. Gdy true, tryb Replace nadpisze tekst nawet w obiektach z polami CAD - " +
                                                  "UŻYWAJ TYLKO gdy świadomie chcesz zniszczyć istniejące pola inline."
                                }
                            }
                        },
                        Required = new List<string> { "Mode" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            var ids = AgentMemoryState.ActiveSelection;
            if (ids == null || ids.Length == 0)
            {
                return "BŁĄD: Brak zaznaczonych obiektów w pamięci Agenta.";
            }

            string mode = args["Mode"]?.ToString();
            string findText = args["FindText"]?.ToString();
            string replaceWith = args["ReplaceWith"]?.ToString() ?? "";
            int colorIndex = args["ColorIndex"]?.Value<int>() ?? 1;
            bool isBold = args["IsBold"]?.Value<bool>() ?? false;
            bool allowFieldOverride = args["AllowFieldOverride"]?.Value<bool>() ?? false;

            if (string.IsNullOrEmpty(mode)) return "BŁĄD: Brak wymaganego parametru Mode.";

            int modifiedCount = 0;
            var warnings = new HashSet<string>();

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    BielikLogger.LogInfo($"[TextEditTool] Start: Mode={mode}, FindText='{findText}', ReplaceWith='{replaceWith}', Ids={ids.Length}, TargetHandle={args["TargetHandle"]?.ToString() ?? "(null)"}");
                    foreach (ObjectId id in ids)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                        if (ent == null) continue;

                        string currentText = "";
                        if (ent is DBText dbText2) currentText = dbText2.TextString;
                        else if (ent is MText mText2) currentText = mText2.Contents;
                        bool willMatch = !string.IsNullOrEmpty(findText) && currentText.Contains(findText);
                        string truncText = currentText.Length > 60 ? currentText.Substring(0, 60) + "..." : currentText;
                        BielikLogger.LogInfo($"[TextEditTool] Handle={id.Handle:X}, Type={ent.GetType().Name}, Text='{truncText}', WillMatch={willMatch}");

                        if (ent is DBText dbText)
                        {
                            HandleDBText(dbText, mode, findText, replaceWith, allowFieldOverride, warnings, ref modifiedCount);
                        }
                        else if (ent is MText mText)
                        {
                            HandleMText(mText, mode, findText, replaceWith, colorIndex, isBold, allowFieldOverride, warnings, ref modifiedCount);
                        }
                    }

                    tr.Commit();
                }

                // Po mutacji MText / DBText - wymus _.REGEN, aby BricsCAD odswiezył
                // cache widoku. Bez tego po Replace na MText rysunek moze pokazywac
                // stary tekst obok nowego (artefakt z bufora graficznego), mimo ze
                // tekst w bazie jest poprawnie zmieniony.
                if (modifiedCount > 0)
                {
                    try
                    {
                        doc.SendStringToExecute("_.REGEN \n", true, false, false);
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Wyslanie _.REGEN po edycji nie powiodlo sie: {ex.Message}");
                    }
                }

                string result = $"SUKCES: Zmodyfikowano {modifiedCount} obiektów tekstowych.";
                if (warnings.Count > 0)
                {
                    result += "\n\nOSTRZEŻENIA:\n" + string.Join("\n", warnings);
                }
                return result;
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY CAD: {ex.Message}";
            }
        }

        private void HandleDBText(DBText dbText, string mode, string findText, string replaceWith, bool allowFieldOverride, HashSet<string> warnings, ref int modifiedCount)
        {
            // Sprawdz czy obiekt ma aktywne pole CAD - jesli tak, zabezpiecz przed zniszczeniem.
            // Binarny marker (%<\_FldIdx) PO konwersji NIE jest aktywnym polem - pozwalamy edytowac,
            // ale informujemy uzytkownika, ze marker zostanie zachowany.
            var fieldDetection = DetectField(dbText);
            if (!allowFieldOverride && fieldDetection == FieldDetection.ActiveField)
            {
                warnings.Add($"[BLOKADA POLA] DBText (ID: {dbText.Id}) zawiera pole CAD (%<\\Ac...>). " +
                             $"Tryb '{mode}' nie zostanie wykonany. Uzyj ManageFieldsTool " +
                             $"lub ponow wywolanie z AllowFieldOverride=true.");
                return;
            }
            if (fieldDetection == FieldDetection.BinaryMarkerOnly)
            {
                warnings.Add($"[INFO MARKER] DBText (ID: {dbText.Id}) zawiera binarny marker pola (%<\\FldIdx) - " +
                             $"prawdopodobnie rezultat ConvertToText bez wczesniejszego _.REGEN. " +
                             $"Edycja trybem '{mode}' zostanie wykonana, ale marker zostanie zachowany w tekscie.");
            }

            switch (mode)
            {
                case "Append":
                    dbText.TextString += replaceWith;
                    modifiedCount++;
                    break;
                case "Prepend":
                    dbText.TextString = replaceWith + dbText.TextString;
                    modifiedCount++;
                    break;
                case "Replace":
                    if (!string.IsNullOrEmpty(findText) && dbText.TextString.Contains(findText))
                    {
                        dbText.TextString = dbText.TextString.Replace(findText, replaceWith);
                        modifiedCount++;
                    }
                    break;
                case "FormatHighlight":
                    warnings.Add("[OSTRZEŻENIE] Zignorowano obiekt DBText (ID: " + dbText.Id + ") w trybie FormatHighlight, ponieważ nie obsługuje on kodów RTF.");
                    break;
                case "ClearFormatting":
                    warnings.Add("[OSTRZEŻENIE] Pominięto obiekt DBText (ID: " + dbText.Id + ") w trybie ClearFormatting, ponieważ nie zawiera on kodów RTF.");
                    break;
            }
        }

        private void HandleMText(MText mText, string mode, string findText, string replaceWith, int colorIndex, bool isBold, bool allowFieldOverride, HashSet<string> warnings, ref int modifiedCount)
        {
            // Sprawdz czy obiekt ma aktywne pole CAD.
            // Trzy mozliwosci:
            //   ActiveField       -> flaga HasFields=true -> BLOKUJ edycje (chron przed zniszczeniem)
            //   BinaryMarkerOnly  -> HasFields=false ale Contents zawiera "%<" (rezultat ConvertToText bez REGEN) ->
            //                        pozwol na edycje, ale ostrzez ze marker zostanie zachowany
            //   None              -> brak pola -> normalna edycja
            var fieldDetection = DetectField(mText);

            if (!allowFieldOverride && mode == "Replace" && fieldDetection == FieldDetection.ActiveField)
            {
                warnings.Add($"[BLOKADA POLA] MText (ID: {mText.Id}) zawiera pole CAD (%<\\Ac...>). " +
                             $"Tryb Replace nie zostanie wykonany. Uzyj ManageFieldsTool " +
                             $"lub ponow wywolanie z AllowFieldOverride=true.");
                return;
            }

            if (fieldDetection == FieldDetection.ActiveField &&
                (mode == "Append" || mode == "Prepend" || mode == "FormatHighlight"))
            {
                warnings.Add($"[OSTRZEZENIE POLA] MText (ID: {mText.Id}) zawiera pole CAD. " +
                             $"Tryb '{mode}' zostanie wykonany, ale moze zakłócic formatowanie pola. " +
                             $"Rozwaz ManageFieldsTool.InsertField.");
            }

            if (fieldDetection == FieldDetection.BinaryMarkerOnly)
            {
                warnings.Add($"[INFO MARKER] MText (ID: {mText.Id}) zawiera binarny marker pola (%<\\FldIdx) - " +
                             $"prawdopodobnie rezultat ConvertToText bez wczesniejszego _.REGEN. " +
                             $"Edycja trybem '{mode}' zostanie wykonana, ale marker zostanie zachowany w tekscie.");
            }

            switch (mode)
            {
                case "Append":
                    mText.Contents += replaceWith;
                    modifiedCount++;
                    break;
                case "Prepend":
                    mText.Contents = replaceWith + mText.Contents;
                    modifiedCount++;
                    break;
                case "Replace":
                    if (!string.IsNullOrEmpty(findText) && mText.Contents.Contains(findText))
                    {
                        mText.Contents = mText.Contents.Replace(findText, replaceWith);
                        modifiedCount++;
                    }
                    break;
                case "FormatHighlight":
                    if (!string.IsNullOrEmpty(findText))
                    {
                        string formatCode = $"\\C{colorIndex};";
                        if (isBold) formatCode += "\\fArial|b1;";
                        string sformatowaneSlowo = $"{{{formatCode}{findText}}}";

                        if (mText.Contents.Contains(findText))
                        {
                            mText.Contents = mText.Contents.Replace(findText, sformatowaneSlowo);
                            modifiedCount++;
                        }
                    }
                    break;
                case "ClearFormatting":
                    if (fieldDetection == FieldDetection.ActiveField && !allowFieldOverride)
                    {
                        warnings.Add($"[BLOKADA POLA] MText (ID: {mText.Id}) zawiera pole CAD. " +
                                     $"Tryb ClearFormatting nie zostanie wykonany, bo usunalby " +
                                     $"zawartosc z kodami pol. Uzyj ManageFieldsTool.ConvertToText.");
                        return;
                    }
                    if (fieldDetection == FieldDetection.BinaryMarkerOnly)
                    {
                        warnings.Add($"[INFO MARKER] MText (ID: {mText.Id}) ClearFormatting zachowa binarny marker pola (%<\\FldIdx) w tekscie.");
                    }
                    // HACK NL: Zachowanie znaków nowej linii \P
                    string originalContents = mText.Contents;
                    mText.Contents = originalContents.Replace("\\P", " @@@NL@@@ ").Replace("\\n", " @@@NL@@@ ");

                    // .Text automatycznie usuwa kody RTF
                    string cleanText = mText.Text;

                    // Przywracamy \P
                    mText.Contents = cleanText.Replace(" @@@NL@@@ ", "\\P").Replace("@@@NL@@@", "\\P").Trim();
                    modifiedCount++;
                    break;
            }
        }

        /// <summary>
        /// Wynik detekcji pola w obiekcie tekstowym.
        /// </summary>
        private enum FieldDetection
        {
            /// <summary>Brak pola - normalna edycja.</summary>
            None,
            /// <summary>Binarny marker pola (%&lt;\_FldIdx) w tresci, ale flaga HasFields=false.
            /// Typowy stan po ConvertToText bez wczesniejszego _.REGEN - pole zostalo
            /// zbinaryzowane do markera zamiast rozwiazane do wartosci. Pozwol na edycje,
            /// ale ostrzez ze marker zostanie zachowany w tekscie.</summary>
            BinaryMarkerOnly,
            /// <summary>Aktywne pole (flaga HasFields=true). Tryb Replace zablokowany
            /// (chyba ze AllowFieldOverride=true). Tryby Append/Prepend/FormatHighlight
            /// z ostrzezeniem.</summary>
            ActiveField
        }

        private static FieldDetection DetectField(Entity ent)
        {
            // Zrodlo prawdy: flaga HasFields z Teigha.
            bool hasFieldsFlag = false;
            try { hasFieldsFlag = ent.HasFields; }
            catch { hasFieldsFlag = false; }

            if (hasFieldsFlag) return FieldDetection.ActiveField;

            // Flaga=false - sprawdz czy w tresci zostal binarny marker pola.
            // Takie markery pojawiaja sie po ConvertFieldToText dla pol, ktorych
            // Teigha nie umiala rozwiazac (brak przeliczenia REGEN). Sam marker
            // nie jest aktywnym polem - mozna go bezpiecznie edytowac.
            try
            {
                string content = "";
                switch (ent)
                {
                    case DBText dt:
                        content = dt.TextString ?? "";
                        break;
                    case MText mt:
                        content = mt.Contents ?? "";
                        break;
                }
                if (content.Contains("%<"))
                {
                    return FieldDetection.BinaryMarkerOnly;
                }
            }
            catch
            {
                // Brak dostepu do tresci - zakladamy brak pola.
            }
            return FieldDetection.None;
        }
        public List<string> Examples => null;
    }
}

