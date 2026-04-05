using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    /// <summary>
    /// Narzędzie do modyfikacji zawartości (geometrii i właściwości) wewnątrz definicji bloku (BlockTableRecord).
    /// Zmiany wpłyną na WSZYSTKIE wystąpienia tego bloku w rysunku.
    /// </summary>
    public class EditBlockTool : IToolV2
    {
        public string[] ToolTags => new[] { "#bloki" };

        private HashSet<string> _warnings = new HashSet<string>();
        private HashSet<ObjectId> _visitedBtrs = new HashSet<ObjectId>();
        private int _modifiedEntities = 0;

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "EditBlock",
                    Description = "Modyfikuje obiekty wewnątrz definicji bloku (BlockTableRecord). Zmiany zostaną odzwierciedlone we wszystkich wstawieniach bloku.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Target", new ToolParameter
                                {
                                    Type = "string",
                                    Enum = new List<string> { "Selection", "ByName" },
                                    Description = "Określa czy edytować bloki z aktualnego zaznaczenia ('Selection') czy konkretny blok o podanej nazwie ('ByName')."
                                }
                            },
                            {
                                "BlockName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa bloku do edycji (wymagane tylko jeśli Target: 'ByName')."
                                }
                            },
                            {
                                "Recursive", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy edytować również bloki zagnieżdżone wewnątrz edytowanego bloku (domyślnie true)."
                                }
                            },
                            {
                                "RemoveDimensions", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Jeśli true, usuwa (Erase) wszystkie obiekty typu Dimension z wnętrza bloku (zgodnie z filtrami)."
                                }
                            },
                            {
                                "FindText", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Tekst do znalezienia wewnątrz obiektów tekstowych bloku."
                                }
                            },
                            {
                                "ReplaceText", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Tekst, na który należy zamienić znaleziony ciąg (wymaga podania FindText)."
                                }
                            },
                            {
                                "Modifications", new ToolParameter
                                {
                                    Type = "array",
                                    Description = "Lista zmian właściwości do zaaplikowania, np.: [{\"Prop\": \"Layer\", \"Val\": \"RED\"}, {\"Prop\": \"Color\", \"Val\": \"1\"}]."
                                }
                            },
                            {
                                "Filters", new ToolParameter
                                {
                                    Type = "object",
                                    Description = "Opcjonalne filtry obiektów we wnętrzu bloku: {\"Type\": \"Line\", \"Layer\": \"0\", \"Color\": 256}."
                                }
                            }
                        },
                        Required = new List<string> { "Target" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            _warnings.Clear();
            _visitedBtrs.Clear();
            _modifiedEntities = 0;

            string targetMode = args["Target"]?.ToString();
            string blockName = args["BlockName"]?.ToString();
            bool recursive = args["Recursive"]?.Value<bool>() ?? true;
            bool removeDims = args["RemoveDimensions"]?.Value<bool>() ?? false;
            string findText = args["FindText"]?.ToString();
            string replaceText = args["ReplaceText"]?.ToString();
            JArray mods = args["Modifications"] as JArray;
            JObject filters = args["Filters"] as JObject;

            if (targetMode == "ByName" && string.IsNullOrEmpty(blockName))
                return "BŁĄD: Musisz podać 'BlockName' dla trybu Target: 'ByName'.";

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    List<ObjectId> targetBtrIds = new List<ObjectId>();

                    if (targetMode == "ByName")
                    {
                        BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                        if (bt.Has(blockName))
                        {
                            targetBtrIds.Add(bt[blockName]);
                        }
                        else
                        {
                            return $"BŁĄD: Blok o nazwie '{blockName}' nie został odnaleziony w tabeli bloków.";
                        }
                    }
                    else
                    {
                        var selection = AgentMemoryState.ActiveSelection;
                        if (selection == null || selection.Length == 0)
                            return "BŁĄD: Pamięć Agenta (zaznaczenie) jest pusta. Zaznacz najpierw bloki do edycji.";

                        foreach (ObjectId id in selection)
                        {
                            BlockReference br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                            if (br != null)
                            {
                                ObjectId btrId = br.BlockTableRecord;
                                if (!targetBtrIds.Contains(btrId)) targetBtrIds.Add(btrId);
                            }
                        }

                        if (targetBtrIds.Count == 0)
                            return "BŁĄD: W zaznaczeniu nie znaleziono żadnych odniesień do bloków (BlockReference).";
                    }

                    // Przetwarzanie definicji bloków
                    foreach (ObjectId btrId in targetBtrIds)
                    {
                        ProcessBlockDefinition(btrId, tr, recursive, removeDims, findText, replaceText, mods, filters);
                    }

                    tr.Commit();
                }

                string summary = $"WYNIK: Zmodyfikowano {_modifiedEntities} obiektów wewnątrz definicji bloku/bloków.";
                if (_warnings.Count > 0)
                {
                    summary += "\nUWAGI:\n- " + string.Join("\n- ", _warnings);
                }
                return summary;
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY PODCZAS EDYCJI BLOKU: {ex.Message}";
            }
        }

        private void ProcessBlockDefinition(ObjectId btrId, Transaction tr, bool recursive, bool removeDims, string find, string replace, JArray mods, JObject filters)
        {
            if (_visitedBtrs.Contains(btrId)) return;
            _visitedBtrs.Add(btrId);

            BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
            if (btr == null) return;

            // OCHRONA BAZY DANYCH
            if (btr.IsFromExternalReference || btr.IsDependent)
            {
                _warnings.Add($"Pominięto blok '{btr.Name}' - edycja XREF-ów nie jest dozwolona.");
                return;
            }
            if (btr.IsLayout)
            {
                _warnings.Add($"Pominięto blok '{btr.Name}' - nie można edytować definicji arkusza/układu jako bloku.");
                return;
            }

            foreach (ObjectId innerId in btr)
            {
                Entity ent = tr.GetObject(innerId, OpenMode.ForRead) as Entity;
                if (ent == null) continue;

                // 1. Filtracja
                if (!IsMatchingFilters(ent, filters)) continue;

                bool isModified = false;

                // 2. Akcja Specjalna: Usuwanie wymiarów
                if (removeDims && ent is Dimension)
                {
                    ent.UpgradeOpen();
                    ent.Erase();
                    _modifiedEntities++;
                    continue; // Obiekt usunięty, przejdź do następnego
                }

                // 3. Akcja: Zamiana tekstu
                if (!string.IsNullOrEmpty(find) && replace != null)
                {
                    if (ent is DBText dbText && dbText.TextString.Contains(find))
                    {
                        dbText.UpgradeOpen();
                        dbText.TextString = dbText.TextString.Replace(find, replace);
                        isModified = true;
                    }
                    else if (ent is MText mText && mText.Contents.Contains(find))
                    {
                        mText.UpgradeOpen();
                        mText.Contents = mText.Contents.Replace(find, replace);
                        isModified = true;
                    }
                }

                // 4. Akcja: Modyfikacja właściwości
                if (mods != null && mods.Count > 0)
                {
                    if (ApplyProperties(ent, mods)) isModified = true;
                }

                if (isModified) _modifiedEntities++;

                // 5. Rekurencja
                if (recursive && ent is BlockReference nestedBr)
                {
                    ProcessBlockDefinition(nestedBr.BlockTableRecord, tr, recursive, removeDims, find, replace, mods, filters);
                }
            }

            // ODŚWIEŻANIE WIDOKU (Side-effects)
            try 
            {
                foreach (ObjectId refId in btr.GetBlockReferenceIds(true, false))
                {
                    BlockReference br = tr.GetObject(refId, OpenMode.ForWrite) as BlockReference;
                    br?.RecordGraphicsModified(true);
                }
                if (btr.IsDynamicBlock) btr.UpdateAnonymousBlocks();
            } 
            catch { /* Ignorujemy błędy odświeżania na tym etapie */ }
        }

        private bool IsMatchingFilters(Entity ent, JObject filters)
        {
            if (filters == null) return true;

            string fType = filters["Type"]?.ToString();
            if (!string.IsNullOrEmpty(fType) && !ent.GetType().Name.Equals(fType, StringComparison.OrdinalIgnoreCase)) return false;

            string fLayer = filters["Layer"]?.ToString();
            if (!string.IsNullOrEmpty(fLayer) && !ent.Layer.Equals(fLayer, StringComparison.OrdinalIgnoreCase)) return false;

            int? fColor = filters["Color"]?.Value<int>();
            if (fColor.HasValue && ent.ColorIndex != fColor.Value) return false;

            return true;
        }

        private bool ApplyProperties(Entity ent, JArray mods)
        {
            bool anyChange = false;
            string className = ent.GetType().Name;

            foreach (JObject mod in mods)
            {
                string rawProp = mod["Prop"]?.ToString();
                string val = mod["Val"]?.ToString();

                if (string.IsNullOrEmpty(rawProp) || val == null) continue;

                // TARCZA ANTY-HALUCYNACYJNA V2
                if (!Bricscad_AgentAI_V2.Core.PropertyValidator.IsPropertyValid(className, rawProp))
                {
                    _warnings.Add($"Właściwość '{rawProp}' zignorowana dla {className} (niepoprawna wg walidatora).");
                    continue;
                }

                string targetPropName = rawProp;
                // Mapowanie nazw (DRY z ModifyPropertiesTool)
                if (targetPropName.Equals("Color", StringComparison.OrdinalIgnoreCase) || targetPropName.Equals("ColorIndex", StringComparison.OrdinalIgnoreCase)) targetPropName = "ColorIndex";
                if (ent is MText && targetPropName.Equals("Height", StringComparison.OrdinalIgnoreCase)) targetPropName = "TextHeight";
                if (targetPropName.Equals("Value", StringComparison.OrdinalIgnoreCase))
                {
                    if (ent is DBText) targetPropName = "TextString";
                    else if (ent is MText) targetPropName = "Text";
                }

                PropertyInfo pi = ent.GetType().GetProperty(targetPropName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (pi == null || !pi.CanWrite) continue;

                try
                {
                    object finalVal = null;
                    Type t = pi.PropertyType;

                    if (t == typeof(string)) finalVal = val;
                    else if (t == typeof(int)) finalVal = int.Parse(val, CultureInfo.InvariantCulture);
                    else if (t == typeof(double)) finalVal = double.Parse(val.Replace(",", "."), CultureInfo.InvariantCulture);
                    else if (targetPropName == "ColorIndex" && t == typeof(Teigha.Colors.Color))
                    {
                        if (short.TryParse(val, out short cIdx))
                            finalVal = Teigha.Colors.Color.FromColorIndex(Teigha.Colors.ColorMethod.ByAci, cIdx);
                    }

                    if (finalVal != null)
                    {
                        ent.UpgradeOpen();
                        pi.SetValue(ent, finalVal, null);
                        anyChange = true;
                    }
                }
                catch (Exception ex)
                {
                    _warnings.Add($"Błąd zapisu '{rawProp}' dla {className}: {ex.Message}");
                }
            }
            return anyChange;
        }
    }
}
