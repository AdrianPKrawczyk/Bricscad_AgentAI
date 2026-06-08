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
                    Description = "Narzędzie do odczytu i edycji atrybutów w blokach CAD. ZASADA KRYTYCZNA: Jeśli musisz zmienić wartość atrybutu X tylko dla bloków, w których atrybut Y ma konkretną wartość (np. VAL = T1), ABSOLUTNIE NIE UŻYWAJ instrukcji warunkowych w RPN (zakaz używania IFTE). Zamiast tego, wykonaj osobne wywołania Action: Update, używając parametrów 'FilterTag' (np. VAL) oraz 'FilterValue' (np. T1). Zmienna $OLD_VALUE w RPN zwraca ZAWSZE wartość edytowanego atrybutu, a nie atrybutu z filtra!",
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
                                    Description = "Lista atrybutów do zmiany. Wspiera stałe teksty oraz operacje matematyczne przy użyciu zmiennej $OLD_VALUE i notacji RPN, np: [{\"Tag\": \"DN_VAL\", \"Value\": \"RPN: $OLD_VALUE 1 +\"}]. PAMIĘTAJ: Jako 'Tag' podawaj zawsze czystą nazwę etykiety, bezwzględnie usuwając z niej wszelkie metadane w nawiasach kwadratowych z odczytu (np. pomiń [Ukryty] lub [Wieloliniowy])."
                                }
                            },
                            {
                                "SaveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Tylko dla Read: Nazwa zmiennej do zapisu zagregowanych wartości (bez @)."
                                }
                            },
                            {
                                "FilterTag", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalnie: Tag atrybutu służący do filtrowania (np. 'ID')."
                                }
                            },
                            {
                                "FilterValue", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalnie: Wartość atrybutu filtrującego (np. 'A2')."
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
            string filterTag = args["FilterTag"]?.ToString();
            string filterValue = args["FilterValue"]?.ToString();
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

                        // Filtrowanie po atrybucie
                        if (!string.IsNullOrEmpty(filterTag))
                        {
                            bool isMatch = false;
                            foreach (ObjectId attId in br.AttributeCollection)
                            {
                                AttributeReference attRef = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                                if (attRef != null && !attRef.IsErased && attRef.Tag.Equals(filterTag, StringComparison.OrdinalIgnoreCase))
                                {
                                    string val = attRef.IsMTextAttribute ? attRef.MTextAttribute.Text : attRef.TextString;
                                    if (val != null && val.Trim().Equals(filterValue?.Trim(), StringComparison.OrdinalIgnoreCase))
                                    {
                                        isMatch = true;
                                        break;
                                    }
                                }
                            }
                            if (!isMatch) continue;
                        }

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

                                    // Budowanie pigułki informacyjnej dla LLM
                                    string meta = "";
                                    if (attRef.Invisible) meta += " [Ukryty]";
                                    if (attRef.IsMTextAttribute) meta += " [Wieloliniowy]";
                                    // Jeśli wyciągniesz Prompt z definicji bloku, można go tu dodać: meta += $" [Opis: {prompt}]"

                                    if (!string.IsNullOrEmpty(val))
                                    {
                                        readValues.Add($"{currentTag}{meta}: {val}");
                                    }
                                }
                            }
                            else if (action == "Update" && attrList != null)
                            {
                                // Szukamy dopasowania, ale najpierw oczyszczamy to, co przysłał Agent (jeśli dodał nawiasy)
                                var match = attrList.FirstOrDefault(a => {
                                    string agentTag = a["Tag"]?.ToString() ?? "";
                                    if (agentTag.Contains("[")) agentTag = agentTag.Substring(0, agentTag.IndexOf('[')).Trim();
                                    return agentTag.Equals(currentTag, StringComparison.OrdinalIgnoreCase);
                                });

                                if (match != null)
                                {
                                    string rawVal = match["Value"]?.ToString() ?? "";

                                    // 1. Pobierz obecną wartość atrybutu
                                    string oldVal = attRef.IsMTextAttribute ? attRef.MTextAttribute.Text : attRef.TextString;

                                    // 2. Jeśli Agent próbuje operacji na starej wartości (np. przesyła RPN: "$OLD_VALUE 1 +")
                                    if (rawVal.Contains("$OLD_VALUE"))
                                    {
                                        // Ujednolicamy przecinki na kropki, żeby RPN i systemy liczące się nie gubiły
                                        oldVal = oldVal.Replace(",", ".");
                                        rawVal = rawVal.Replace("$OLD_VALUE", oldVal);
                                    }

                                    // 3. Wstrzykiwanie ewentualnych zmiennych z pamięci Agenta
                                    string finalVal = AgentMemoryState.InjectVariables(rawVal);

                                    // 4. Integracja z silnikiem matematycznym RPN (jeśli wykryto notację RPN)
                                    if (finalVal.StartsWith("RPN:"))
                                    {
                                        string rpnExpression = finalVal.Replace("RPN:", "").Trim();
                                        try
                                        {
                                            // Wykorzystanie silnika RpnCalculator z Core (zwraca obliczony wynik jako string)
                                            finalVal = Bricscad_AgentAI_V2.Core.RpnCalculator.Evaluate(rpnExpression);
                                        }
                                        catch
                                        {
                                            // W razie błędu obliczeń zostawiamy surowy tekst
                                        }
                                    }

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

