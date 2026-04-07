using System;
using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using Teigha.Colors;

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

            int udane = 0;
            int pominiete = 0;
            var ostrzezenia = new List<string>();

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    BlockTable bt = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);

                    foreach (ObjectId id in AgentMemoryState.ActiveSelection)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                        if (ent == null) continue;

                        if (ent is Dimension dim)
                        {
                            bool modified = false;

                            // 1. TextOverride
                            if (args.TryGetValue("TextOverride", out JToken tokenText))
                            {
                                dim.DimensionText = tokenText.ToString();
                                modified = true;
                            }

                            // 2. OverallScale (Dimscale)
                            if (args.TryGetValue("OverallScale", out JToken tokenScale))
                            {
                                if (double.TryParse(tokenScale.ToString(), out double scale))
                                {
                                    dim.Dimscale = scale;
                                    modified = true;
                                }
                            }

                            // 3. TextColor (Dimclrt)
                            if (args.TryGetValue("TextColor", out JToken tokenTextColor))
                            {
                                if (short.TryParse(tokenTextColor.ToString(), out short aci))
                                {
                                    dim.Dimclrt = Color.FromColorIndex(ColorMethod.ByAci, aci);
                                    modified = true;
                                }
                            }

                            // 4. DimLineColor (Dimclrd)
                            if (args.TryGetValue("DimLineColor", out JToken tokenDimCol))
                            {
                                if (short.TryParse(tokenDimCol.ToString(), out short aci))
                                {
                                    dim.Dimclrd = Color.FromColorIndex(ColorMethod.ByAci, aci);
                                    modified = true;
                                }
                            }

                            // 5. ExtLineColor (Dimclre)
                            if (args.TryGetValue("ExtLineColor", out JToken tokenExtCol))
                            {
                                if (short.TryParse(tokenExtCol.ToString(), out short aci))
                                {
                                    dim.Dimclre = Color.FromColorIndex(ColorMethod.ByAci, aci);
                                    modified = true;
                                }
                            }

                            // 6. ArrowBlock (Dimblk)
                            if (args.TryGetValue("ArrowBlock", out JToken tokenArrow))
                            {
                                string blkName = tokenArrow.ToString();
                                if (string.IsNullOrEmpty(blkName))
                                {
                                    dim.Dimblk = ObjectId.Null;
                                    modified = true;
                                }
                                else if (bt.Has(blkName))
                                {
                                    dim.Dimblk = bt[blkName];
                                    modified = true;
                                }
                                else
                                {
                                    ostrzezenia.Add($"Ostrzeżenie: Nie znaleziono bloku strzałki '{blkName}' w rysunku.");
                                }
                            }

                            if (modified) udane++;
                        }
                        else
                        {
                            pominiete++;
                        }
                    }

                    tr.Commit();
                }

                string summary = $"Pomyślnie zmodyfikowano {udane} wymiarów.";
                if (pominiete > 0) summary += $" Pominięto {pominiete} obiektów (nie były wymiarami).";
                if (ostrzezenia.Count > 0) summary += "\n" + string.Join("\n", ostrzezenia.Distinct());

                return summary;
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY: {ex.Message}";
            }
        }
    }
}
