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
    public class ManageAnnoScalesTool : IToolV2
    {
        public string[] ToolTags => new[] { "#tekst" };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ManageAnnoScales",
                    Description = "Zarządza skalami opisowymi (Annotative Scales) dla obiektów wspierających tę funkcję (Teksty, Wymiary, Bloki, Odnośniki, Kreskowania).",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Action", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Akcja do wykonania: Add (dodaj skalę), Remove (usuń skalę), Read (odczytaj skale).",
                                    Enum = new List<string> { "Add", "Remove", "Read" }
                                }
                            },
                            {
                                "ScaleName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa skali (np. '1:50', '1:100'). Wymagane dla Add i Remove."
                                }
                            },
                            {
                                "DisableAnnotative", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Tylko dla Remove: Całkowicie wyłącza opisowość obiektu (Annotative = False)."
                                }
                            },
                            {
                                "SaveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Tylko dla Read: Nazwa zmiennej do zapisu unikalnej listy skal (bez @)."
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
            var ids = AgentMemoryState.ActiveSelection;
            if (ids == null || ids.Length == 0)
            {
                return "BŁĄD: Brak zaznaczonych obiektów w pamięci Agenta.";
            }

            string action = args["Action"]?.ToString();
            string scaleName = args["ScaleName"]?.ToString();
            string saveAs = args["SaveAs"]?.ToString();
            bool disableAnnotative = args["DisableAnnotative"]?.Value<bool>() ?? false;

            if (string.IsNullOrEmpty(action)) return "BŁĄD: Brak wymaganego parametru Action.";
            if ((action == "Add" || action == "Remove") && string.IsNullOrEmpty(scaleName) && !disableAnnotative)
            {
                return "BŁĄD: Akcja Add/Remove wymaga podania ScaleName (chyba że używasz DisableAnnotative).";
            }

            this._warnings.Clear();
            int successCount = 0;
            int btrModified = 0;
            int incompatibleCount = 0;
            var uniqueScales = new HashSet<string>();

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    ObjectContextManager ocm = doc.Database.ObjectContextManager;
                    ObjectContextCollection occ = ocm?.GetContextCollection("ACDB_ANNOTATIONSCALES");
                    ObjectContext targetContext = null;

                    if (occ != null && !string.IsNullOrEmpty(scaleName))
                    {
                        targetContext = occ.GetContext(scaleName);
                    }

                    if ((action == "Add" || (action == "Remove" && !disableAnnotative)) && targetContext == null && !string.IsNullOrEmpty(scaleName))
                    {
                        return $"BŁĄD: Skala '{scaleName}' nie istnieje w tym rysunku.";
                    }

                    var processedBTRs = new HashSet<ObjectId>();

                    foreach (ObjectId id in ids)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                        if (ent == null) continue;

                        if (!IsCompatible(ent))
                        {
                            incompatibleCount++;
                            continue;
                        }

                        switch (action)
                        {
                            case "Add":
                                if (ent is BlockReference blkRef)
                                {
                                    EnsureBtrAnnotative(blkRef, tr, processedBTRs, ref btrModified);
                                }
                                if (ent.Annotative == AnnotativeStates.False) ent.Annotative = AnnotativeStates.True;
                                
                                if (!ent.HasContext(targetContext))
                                {
                                    ent.AddContext(targetContext);
                                    successCount++;
                                }
                                break;

                            case "Remove":
                                if (disableAnnotative)
                                {
                                    if (ent is BlockReference blkRef2)
                                    {
                                        ObjectId defId = blkRef2.DynamicBlockTableRecord != ObjectId.Null ? blkRef2.DynamicBlockTableRecord : blkRef2.BlockTableRecord;
                                        if (!processedBTRs.Contains(defId))
                                        {
                                            BlockTableRecord btr = tr.GetObject(defId, OpenMode.ForWrite) as BlockTableRecord;
                                            if (btr != null)
                                            {
                                                btr.Annotative = AnnotativeStates.False;
                                                processedBTRs.Add(defId);
                                                btrModified++;
                                            }
                                        }
                                    }
                                    ent.Annotative = AnnotativeStates.False;
                                    successCount++;
                                }
                                else if (ent.Annotative == AnnotativeStates.True)
                                {
                                    if (ent.HasContext(targetContext))
                                    {
                                        ent.RemoveContext(targetContext);
                                        successCount++;
                                    }
                                }
                                break;

                            case "Read":
                                if (ent.Annotative == AnnotativeStates.True)
                                {
                                    successCount++;
                                    if (occ != null)
                                    {
                                        foreach (ObjectContext ctx in occ)
                                        {
                                            if (ent.HasContext(ctx)) uniqueScales.Add(ctx.Name);
                                        }
                                    }
                                }
                                break;
                        }
                    }

                    tr.Commit();
                }

                return FormatResult(action, successCount, btrModified, incompatibleCount, uniqueScales, scaleName, saveAs, disableAnnotative);
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY CAD: {ex.Message}";
            }
        }

        private bool IsCompatible(Entity ent)
        {
            return ent is Dimension || ent is MText || ent is DBText || ent is BlockReference || 
                   ent is Leader || ent is MLeader || ent is Hatch;
        }

        private void EnsureBtrAnnotative(BlockReference blkRef, Transaction tr, HashSet<ObjectId> processed, ref int modified)
        {
            ObjectId defId = blkRef.DynamicBlockTableRecord != ObjectId.Null ? blkRef.DynamicBlockTableRecord : blkRef.BlockTableRecord;
            if (processed.Contains(defId)) return;

            BlockTableRecord btr = tr.GetObject(defId, OpenMode.ForWrite) as BlockTableRecord;
            if (btr != null)
            {
                if (btr.Annotative == AnnotativeStates.False)
                {
                    btr.Annotative = AnnotativeStates.True;
                    modified++;
                }
                processed.Add(defId);
            }
        }

        private string FormatResult(string action, int success, int btrMod, int incomp, HashSet<string> scales, string scaleName, string saveAs, bool disabled)
        {
            string baseMsg = $"WYNIK ({action}): ";
            if (incomp > 0) _warnings.Add($"Pominięto {incomp} obiektów (nie obsługują skali opisowej).");

            switch (action)
            {
                case "Add":
                    baseMsg += $"Dodano skalę '{scaleName}' do {success} obiektów.";
                    if (btrMod > 0) baseMsg += $" Zmodyfikowano {btrMod} definicji bloków.";
                    break;
                case "Remove":
                    if (disabled) baseMsg += $"Całkowicie wyłączono opisowość dla {success} obiektów.";
                    else baseMsg += $"Usunięto skalę '{scaleName}' z {success} obiektów.";
                    break;
                case "Read":
                    string scaleList = string.Join(", ", scales.OrderBy(s => s));
                    baseMsg += $"Przeanalizowano {success} obiektów opisowych. Skale: {(scales.Count > 0 ? scaleList : "Brak")}";
                    if (!string.IsNullOrEmpty(saveAs) && scales.Count > 0)
                    {
                        string val = string.Join(" | ", scales.OrderBy(s => s));
                        AgentMemoryState.Variables[saveAs] = val;
                        baseMsg += $"\nZAPISANO W PAMIĘCI JAKO: @{saveAs}";
                    }
                    break;
            }

            if (_warnings.Count > 0) baseMsg += "\nUWAGI: " + string.Join("; ", _warnings);
            return baseMsg;
        }

        private HashSet<string> _warnings = new HashSet<string>();
    }
}
