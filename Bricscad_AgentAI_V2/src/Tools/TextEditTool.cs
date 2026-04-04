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
                    Description = "Modyfikuje treść oraz formatowanie wizualne (RTF) obiektów tekstowych (DBText, MText) w zaznaczeniu.",
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

            if (string.IsNullOrEmpty(mode)) return "BŁĄD: Brak wymaganego parametru Mode.";

            int modifiedCount = 0;
            var warnings = new HashSet<string>();

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in ids)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                        if (ent == null) continue;

                        if (ent is DBText dbText)
                        {
                            HandleDBText(dbText, mode, findText, replaceWith, warnings, ref modifiedCount);
                        }
                        else if (ent is MText mText)
                        {
                            HandleMText(mText, mode, findText, replaceWith, colorIndex, isBold, warnings, ref modifiedCount);
                        }
                    }

                    tr.Commit();
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

        private void HandleDBText(DBText dbText, string mode, string findText, string replaceWith, HashSet<string> warnings, ref int modifiedCount)
        {
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
                    if (!string.IsNullOrEmpty(findText))
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

        private void HandleMText(MText mText, string mode, string findText, string replaceWith, int colorIndex, bool isBold, HashSet<string> warnings, ref int modifiedCount)
        {
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
                    if (!string.IsNullOrEmpty(findText))
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
    }
}
