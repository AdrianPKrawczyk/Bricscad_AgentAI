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
    public class FindXDataTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "FindXData",
                    Description = "Skanuje zaznaczone obiekty lub wnętrze definicji bloku (rekurencyjnie) w poszukiwaniu elementów posiadających metadane XData.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Mode", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Tryb skanowania. Wybierz 'Selection' (skanuje ActiveSelection) lub 'Block' (skanuje wnętrze definicji bloku).",
                                    Enum = new List<string> { "Selection", "Block" }
                                }
                            },
                            {
                                "BlockName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa bloku do przeskanowania (wymagane tylko gdy Mode='Block')."
                                }
                            },
                            {
                                "SaveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna nazwa zmiennej (bez @), pod którą zostanie zapisana lista uchwytów (Handles) znalezionych obiektów."
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
            string mode = args["Mode"]?.ToString();
            string blockName = args["BlockName"]?.ToString();
            string saveAs = args["SaveAs"]?.ToString();

            Database db = doc.Database;
            List<string> foundHandles = new List<string>();
            List<string> reportLines = new List<string>();
            HashSet<ObjectId> visitedBlocks = new HashSet<ObjectId>();

            try
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    IEnumerable<ObjectId> idsToScan = null;

                    if (mode.Equals("Selection", StringComparison.OrdinalIgnoreCase))
                    {
                        idsToScan = AgentMemoryState.ActiveSelection;
                        if (idsToScan == null || !idsToScan.Any())
                            return "BŁĄD: Tryb 'Selection' wymaga wcześniejszego zaznaczenia obiektów (ActiveSelection jest puste).";
                    }
                    else if (mode.Equals("Block", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(blockName))
                            return "BŁĄD: Tryb 'Block' wymaga podania 'BlockName'.";

                        BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                        if (!bt.Has(blockName))
                            return $"BŁĄD: Nie znaleziono definicji bloku '{blockName}'.";

                        BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[blockName], OpenMode.ForRead);
                        visitedBlocks.Add(btr.ObjectId);
                        idsToScan = btr.Cast<ObjectId>();
                    }
                    else
                    {
                        return "BŁĄD: Nieznany tryb. Użyj 'Selection' lub 'Block'.";
                    }

                    // SKANOWANIE REKURENCYJNE
                    ScanRecursive(idsToScan, tr, foundHandles, reportLines, visitedBlocks);

                    tr.Commit();
                }

                if (foundHandles.Count == 0)
                    return $"INFO: Skanowanie zakończone. Nie znaleziono obiektów z XData w trybie {mode}.";

                string finalReport = $"SUKCES: Znaleziono {foundHandles.Count} obiektów posiadających XData:\n" + string.Join("\n", reportLines);

                if (!string.IsNullOrEmpty(saveAs))
                {
                    AgentMemoryState.Variables[saveAs] = string.Join(",", foundHandles);
                    finalReport = $"ZAPISANO UCHWYTY W @{saveAs}.\n" + finalReport;
                }

                return finalReport;
            }
            catch (Exception ex)
            {
                return $"BŁĄD SKANOWANIA XDATA: {ex.Message}";
            }
        }

        private void ScanRecursive(IEnumerable<ObjectId> ids, Transaction tr, List<string> foundHandles, List<string> reportLines, HashSet<ObjectId> visitedBlocks)
        {
            foreach (ObjectId id in ids)
            {
                Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent == null) continue;

                // 1. Sprawdź XData bieżącego obiektu
                using (ResultBuffer rb = ent.XData)
                {
                    if (rb != null)
                    {
                        HashSet<string> apps = new HashSet<string>();
                        foreach (TypedValue tv in rb)
                        {
                            if (tv.TypeCode == 1001 && tv.Value != null)
                                apps.Add(tv.Value.ToString());
                        }

                        if (apps.Count > 0)
                        {
                            foundHandles.Add(ent.Handle.ToString());
                            reportLines.Add($"- [{ent.GetType().Name}] (Handle: {ent.Handle}), Apps: {string.Join(", ", apps)}");
                        }
                    }
                }

                // 2. Jeśli to BlockReference, wejdź głębiej do definicji (rekurencja)
                if (ent is BlockReference br)
                {
                    ObjectId btrId = br.BlockTableRecord;
                    if (visitedBlocks.Add(btrId))
                    {
                        BlockTableRecord subBtr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                        ScanRecursive(subBtr.Cast<ObjectId>(), tr, foundHandles, reportLines, visitedBlocks);
                    }
                }
            }
        }

        public List<string> Examples => new List<string>
        {
            "{\"Mode\": \"Selection\"}",
            "{\"Mode\": \"Block\", \"BlockName\": \"WINDOW_X1\", \"SaveAs\": \"FoundList\"}"
        };
    }
}