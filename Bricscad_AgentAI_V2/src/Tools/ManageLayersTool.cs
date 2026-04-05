using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using Teigha.Colors;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ManageLayersTool : IToolV2
    {
        public string[] ToolTags => new[] { "#warstwy" };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ManageLayers",
                    Description = "Zarządza warstwami w rysunku (tworzenie, modyfikacja, usuwanie). Obsługuje maski nazw (np. 'INST_*') dla operacji masowych.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "Action", new ToolParameter { Type = "string", Description = "Akcja: 'Create', 'Modify', 'Delete'." } },
                            { "LayerName", new ToolParameter { Type = "string", Description = "Nazwa warstwy lub maska (np. 'MECH-*'). Zalecane wielkie litery, brak polskich znaków." } },
                            { "ColorIndex", new ToolParameter { Type = "integer", Description = "Wskaźnik koloru ACI (1-255)." } },
                            { "IsOff", new ToolParameter { Type = "boolean", Description = "Czy warstwa ma być wyłączona." } },
                            { "IsFrozen", new ToolParameter { Type = "boolean", Description = "Czy warstwa ma być zamrożona." } },
                            { "IsLocked", new ToolParameter { Type = "boolean", Description = "Czy warstwa ma być zablokowana." } },
                            { "Linetype", new ToolParameter { Type = "string", Description = "Nazwa rodzaju linii." } }
                        },
                        Required = new List<string> { "Action", "LayerName" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            Database db = doc.Database;
            string action = args["Action"]?.ToString() ?? "";
            string layerPattern = args["LayerName"]?.ToString() ?? "";
            
            if (string.IsNullOrEmpty(layerPattern)) return "BŁĄD: Nazwa warstwy nie może być pusta.";

            StringBuilder sb = new StringBuilder();
            bool isWildcard = layerPattern.Contains("*") || layerPattern.Contains("?");
            Regex regex = isWildcard ? new Regex("^" + Regex.Escape(layerPattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase) : null;

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

                    if (action.Equals("Create", StringComparison.OrdinalIgnoreCase))
                    {
                        if (isWildcard) return "BŁĄD: Nie można używać masek przy tworzeniu nowej warstwy.";
                        if (lt.Has(layerPattern))
                        {
                            sb.AppendLine($"INFO: Warstwa '{layerPattern}' już istnieje. Przechodzę do modyfikacji.");
                            action = "Modify";
                        }
                        else
                        {
                            lt.UpgradeOpen();
                            LayerTableRecord ltr = new LayerTableRecord { Name = layerPattern };
                            ApplyProperties(ltr, args, db, tr);
                            lt.Add(ltr);
                            tr.AddNewlyCreatedDBObject(ltr, true);
                            sb.AppendLine($"SUKCES: Utworzono warstwę '{layerPattern}'.");
                        }
                    }

                    if (action.Equals("Modify", StringComparison.OrdinalIgnoreCase) || action.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                    {
                        int count = 0;
                        foreach (ObjectId id in lt)
                        {
                            LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                            if (Matches(ltr.Name, layerPattern, regex))
                            {
                                if (action.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (IsProtected(ltr.Name))
                                    {
                                        sb.AppendLine($"BŁĄD: Warstwa '{ltr.Name}' jest chroniona i nie może być usunięta.");
                                        continue;
                                    }
                                    if (db.Clayer == id)
                                    {
                                        sb.AppendLine($"BŁĄD: Nie można usunąć warstwy '{ltr.Name}', ponieważ jest aktualna.");
                                        continue;
                                    }

                                    try
                                    {
                                        ltr.UpgradeOpen();
                                        ltr.Erase();
                                        sb.AppendLine($"SUKCES: Usunięto warstwę '{ltr.Name}'.");
                                        count++;
                                    }
                                    catch
                                    {
                                        sb.AppendLine($"BŁĄD: Nie można usunąć warstwy '{ltr.Name}' (prawdopodobnie zawiera obiekty lub jest używana w bloku).");
                                    }
                                }
                                else // Modify
                                {
                                    ltr.UpgradeOpen();
                                    ApplyProperties(ltr, args, db, tr);
                                    sb.AppendLine($"SUKCES: Zmodyfikowano warstwę '{ltr.Name}'.");
                                    count++;
                                }
                            }
                        }
                        if (count == 0 && !sb.ToString().Contains("SUKCES")) sb.AppendLine($"INFO: Nie znaleziono warstw pasujących do '{layerPattern}'.");
                    }

                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY: {ex.Message}";
            }

            return sb.ToString().Trim();
        }

        private bool Matches(string name, string pattern, Regex regex)
        {
            if (regex != null) return regex.IsMatch(name);
            return name.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsProtected(string name)
        {
            return name == "0" || name.Equals("Defpoints", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyProperties(LayerTableRecord ltr, JObject args, Database db, Transaction tr)
        {
            if (args["ColorIndex"] != null && int.TryParse(args["ColorIndex"].ToString(), out int color))
                ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, (short)color);
            
            if (args["IsOff"] != null) ltr.IsOff = (bool)args["IsOff"];
            if (args["IsFrozen"] != null) ltr.IsFrozen = (bool)args["IsFrozen"];
            if (args["IsLocked"] != null) ltr.IsLocked = (bool)args["IsLocked"];
            
            if (args["Linetype"] != null)
            {
                string ltName = args["Linetype"].ToString();
                LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                if (ltt.Has(ltName)) ltr.LinetypeObjectId = ltt[ltName];
            }
        }
    }
}
