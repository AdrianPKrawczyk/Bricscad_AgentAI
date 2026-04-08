using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    /// <summary>
    /// Narzędzie do zarządzania atrybutami (dynamicznymi tekstami) w konkretnych wystąpieniach bloków.
    /// Obsługuje odczyt (Read) oraz aktualizację (Update) wartości na podstawie tagów.
    /// </summary>
    public class EditAttributesTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "EditAttributes",
                    Description = "Modyfikuje lub odczytuje wartości atrybutów w konkretnych wystąpieniach bloków znajdujących się w zaznaczeniu.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Action", new ToolParameter
                                {
                                    Type = "string",
                                    Enum = new List<string> { "Read", "Update" },
                                    Description = "Akcja do wykonania: 'Read' (pobranie wartości) lub 'Update' (zmiana wartości)."
                                }
                            },
                            {
                                "Attributes", new ToolParameter
                                {
                                    Type = "array",
                                    Description = "Lista atrybutów. Dla Update: [{\"Tag\": \"NUMER\", \"Value\": \"101\"}]. Dla Read opcjonalnie lista tagów do pobrania."
                                }
                            },
                            {
                                "SaveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Tylko dla Read: Nazwa zmiennej do zapisu zagregowanych wartości (bez @)."
                                }
                            }
                        },
                        Required = new List<string> { "Action" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string action = args["Action"]?.ToString();
            string saveAs = args["SaveAs"]?.ToString();
            JArray attrList = args["Attributes"] as JArray;

            var ids = AgentMemoryState.ActiveSelection;
            if (ids == null || ids.Length == 0)
                return "BŁĄD: Pamięć Agenta jest pusta. Zaznacz najpierw bloki (SelectEntities).";

            int updatedCount = 0;
            int blocksProcessed = 0;
            var readValues = new List<string>();
            var warnings = new HashSet<string>();

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in ids)
                    {
                        BlockReference br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (br == null) continue;

                        blocksProcessed++;
                        var processedTagsInThisBlock = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        foreach (ObjectId attId in br.AttributeCollection)
                        {
                            AttributeReference attRef = tr.GetObject(attId, action == "Update" ? OpenMode.ForWrite : OpenMode.ForRead) as AttributeReference;
                            if (attRef == null || attRef.IsErased) continue;

                            string currentTag = attRef.Tag;
                            
                            if (action == "Read")
                            {
                                bool shouldRead = true;
                                if (attrList != null && attrList.Count > 0)
                                {
                                    shouldRead = attrList.Any(a => a["Tag"]?.ToString().Equals(currentTag, StringComparison.OrdinalIgnoreCase) == true);
                                }

                                if (shouldRead)
                                {
                                    string val = attRef.IsMTextAttribute ? attRef.MTextAttribute.Text : attRef.TextString;
                                    if (!string.IsNullOrEmpty(val)) readValues.Add(val);
                                }
                            }
                            else if (action == "Update" && attrList != null)
                            {
                                var match = attrList.FirstOrDefault(a => a["Tag"]?.ToString().Equals(currentTag, StringComparison.OrdinalIgnoreCase) == true);
                                if (match != null)
                                {
                                    string rawVal = match["Value"]?.ToString() ?? "";
                                    string finalVal = AgentMemoryState.InjectVariables(rawVal);

                                    if (attRef.IsMTextAttribute)
                                    {
                                        MText mtxt = attRef.MTextAttribute;
                                        mtxt.Contents = finalVal;
                                        attRef.MTextAttribute = mtxt;
                                    }
                                    else
                                    {
                                        attRef.TextString = finalVal;
                                    }
                                    updatedCount++;
                                    processedTagsInThisBlock.Add(currentTag);
                                }
                            }
                        }

                        // Raportowanie brakujących tagów
                        if (action == "Update" && attrList != null)
                        {
                            foreach (var attr in attrList)
                            {
                                string targetTag = attr["Tag"]?.ToString();
                                if (!string.IsNullOrEmpty(targetTag) && !processedTagsInThisBlock.Contains(targetTag))
                                {
                                    warnings.Add($"Tag '{targetTag}' nie znaleziony w bloku '{br.Name}' (ID: {id}).");
                                }
                            }
                            // Odświeżenie instancji bloku po zmianie atrybutów
                            br.UpgradeOpen();
                            br.RecordGraphicsModified(true);
                        }
                    }
                    tr.Commit();
                }

                if (action == "Read")
                {
                    string resultVal = string.Join(" | ", readValues.Distinct());
                    if (!string.IsNullOrEmpty(saveAs))
                    {
                        AgentMemoryState.Variables[saveAs] = resultVal;
                    }
                    return $"WYNIK (Read): Odczytano {readValues.Count} wartości z {blocksProcessed} bloków.{(string.IsNullOrEmpty(saveAs) ? " Treść: " + resultVal : " Zapisano w @" + saveAs)}";
                }
                else
                {
                    string result = $"WYNIK (Update): Zaktualizowano {updatedCount} atrybutów w {blocksProcessed} blokach.";
                    if (warnings.Count > 0)
                    {
                        result += "\nUWAGI:\n- " + string.Join("\n- ", warnings.Take(5));
                        if (warnings.Count > 5) result += $"\n... i {warnings.Count - 5} innych.";
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY EDYCJI ATRYBUTÓW: {ex.Message}";
            }
        }
        public List<string> Examples => null;
    }
}

