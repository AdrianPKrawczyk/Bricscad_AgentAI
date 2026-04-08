using System;
using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using Teigha.Colors;
using System.Globalization;

namespace Bricscad_AgentAI_V2.Tools
{
    public class DimensionEditTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "DimensionEditTool",
                    Description = "Specjalistyczne narzędzie do modyfikacji anatomii wymiarów w BricsCAD. Operuje na elementach aktualnie znajdujących się w pamięci Agenta (SelectEntities).",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "TextOverride", new ToolParameter { Type = "string", Description = "Nadpisanie tekstu. Aby usunąć formatowanie i przywrócić domyślny pomiar, należy wysłać pusty string \"\"." } },
                            { "OverallScale", new ToolParameter { Type = "number", Description = "Skala globalna wymiaru (Dimscale). Mnożnik wielkości tekstów i strzałek." } },
                            { "ArrowBlock", new ToolParameter { Type = "string", Description = "Nazwa bloku grotu/strzałki (np. _ARCHTICK, _DOT, \"\" dla domyślnego)." } },
                            { "TextColor", new ToolParameter { Type = "integer", Description = "Indeks koloru ACI dla samego tekstu wymiarowego (Dimclrt)." } },
                            { "DimLineColor", new ToolParameter { Type = "integer", Description = "Indeks koloru ACI dla linii wymiarowej (Dimclrd)." } },
                            { "ExtLineColor", new ToolParameter { Type = "integer", Description = "Indeks koloru ACI dla linii pomocniczych (Dimclre)." } }
                        }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            if (AgentMemoryState.ActiveSelection == null || AgentMemoryState.ActiveSelection.Length == 0)
            {
                return "BŁĄD: Pamięć Agenta jest pusta. Użyj najpierw SelectEntitiesTool, by wybrać wymiary.";
            }

            Database db = doc.Database;
            int udane = 0;
            int pominiete = 0;
            var ostrzezenia = new List<string>();

            try
            {
                // 1. Blokada dokumentu zapobiega eLockViolation
                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        foreach (ObjectId id in AgentMemoryState.ActiveSelection)
                        {
                            try
                            {
                                // 2. KRYTYCZNE: Otwarcie do zapisu (ForWrite)
                                DBObject obj = tr.GetObject(id, OpenMode.ForWrite);
                                if (obj is Dimension dim)
                                {
                                    bool modified = false;

                                    // TextOverride
                                    if (args.TryGetValue("TextOverride", out JToken tokText))
                                    {
                                        dim.DimensionText = tokText.ToString();
                                        modified = true;
                                    }

                                    // OverallScale (Bezpieczne parsowanie, nawet jeśli LLM wyśle string "3.5")
                                    if (args.TryGetValue("OverallScale", out JToken tokScale))
                                    {
                                        if (double.TryParse(tokScale.ToString().Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double scale))
                                        {
                                            dim.Dimscale = scale;
                                            modified = true;
                                        }
                                    }

                                    // ArrowBlock (Bezpieczne Arrow Logic z poprzedniego hotfixa)
                                    if (args.TryGetValue("ArrowBlock", out JToken tokArrow))
                                    {
                                        string blkName = tokArrow.ToString();
                                        try
                                        {
                                            ObjectId arrowId = GetArrowObjectId(db, blkName);
                                            if (!arrowId.IsNull)
                                            {
                                                dim.Dimblk = arrowId;
                                                dim.Dimblk1 = arrowId;
                                                dim.Dimblk2 = arrowId;
                                                modified = true;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            ostrzezenia.Add($"API odrzuciło strzałkę '{blkName}': {ex.Message}");
                                        }
                                    }

                                    // TextColor (Bezpieczne parsowanie indeksu koloru ACI)
                                    if (args.TryGetValue("TextColor", out JToken tokColorT) && short.TryParse(tokColorT.ToString(), out short colT))
                                    {
                                        dim.Dimclrt = Color.FromColorIndex(ColorMethod.ByAci, colT);
                                        modified = true;
                                    }

                                    // DimLineColor
                                    if (args.TryGetValue("DimLineColor", out JToken tokColorD) && short.TryParse(tokColorD.ToString(), out short colD))
                                    {
                                        dim.Dimclrd = Color.FromColorIndex(ColorMethod.ByAci, colD);
                                        modified = true;
                                    }

                                    // ExtLineColor
                                    if (args.TryGetValue("ExtLineColor", out JToken tokColorE) && short.TryParse(tokColorE.ToString(), out short colE))
                                    {
                                        dim.Dimclre = Color.FromColorIndex(ColorMethod.ByAci, colE);
                                        modified = true;
                                    }

                                    if (modified)
                                    {
                                        udane++;
                                        // 3. Rekompilacja grafiki wymiaru w pamięci bazy
                                        dim.RecomputeDimensionBlock(true);
                                    }
                                }
                                else
                                {
                                    pominiete++;
                                }
                            }
                            catch (Exception entityEx)
                            {
                                ostrzezenia.Add($"Błąd edycji obiektu {id}: {entityEx.Message}");
                            }
                        }
                        tr.Commit();
                    }

                    // 4. KRYTYCZNE: Szturchnięcie silnika BricsCAD do odświeżenia okna widoku (Stale UI Fix)
                    doc.SendStringToExecute("_.REGEN \n", true, false, false);
                }

                string summary = $"SUKCES: Zmodyfikowano {udane} wymiarów.";
                if (pominiete > 0) summary += $" Pominięto {pominiete} obiektów (nie były wymiarami).";
                if (ostrzezenia.Count > 0) summary += $" Ostrzeżenia: {string.Join(" | ", ostrzezenia.Distinct())}";

                return summary;
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY NARZĘDZIA: {ex.Message}";
            }
        }

        /// <summary>
        /// Pobiera ObjectId bloku strzałki na podstawie nazwy (np. _ARCHTICK).
        /// W BricsCAD API V22/V23 wymaga to często SymbolUtilityServices.
        /// </summary>
        private ObjectId GetArrowObjectId(Database db, string name)
        {
            if (string.IsNullOrEmpty(name)) return ObjectId.Null;
            
            // Obsługa standardowego grotu (pusta nazwa w BricsCAD to często Closed Filled)
            if (name.Equals("_DEFAULT", StringComparison.OrdinalIgnoreCase)) return ObjectId.Null;

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                if (bt.Has(name))
                {
                    return bt[name];
                }
                
                return ObjectId.Null;
            }
        }

        public List<string> Examples => new List<string>
        {
            "{\"TextOverride\": \"<>\", \"OverallScale\": 1.0} // Przywróć domyślny pomiar i ustaw skalę 1.0",
            "{\"TextColor\": 1, \"DimLineColor\": 1, \"ExtLineColor\": 1} // Ustaw wszystkie elementy wymiaru na czerwony (ACI 1)",
            "{\"ArrowBlock\": \"_ARCHTICK\"} // Zmień strzałki na groty architektoniczne"
        };
    }
}
