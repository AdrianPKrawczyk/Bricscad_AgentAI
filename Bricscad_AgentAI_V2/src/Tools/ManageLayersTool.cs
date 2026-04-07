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
                            { "Action", new ToolParameter { Type = "string", Description = "Akcja: 'Create', 'Toggle', 'Rename', 'Delete'." } },
                            { "LayerName", new ToolParameter { Type = "string", Description = "Nazwa warstwy, lista nazw po przecinku lub maska (np. 'MECH-*')." } },
                            { "NewName", new ToolParameter { Type = "string", Description = "Nowa nazwa (tylko dla akcji 'Rename')." } },
                            { "ColorIndex", new ToolParameter { Type = "integer", Description = "Wskaźnik koloru ACI (1-255) dla akcji 'Create'." } },
                            { "State", new ToolParameter { Type = "string", Description = "Stan dla akcji 'Toggle': 'Locked', 'Unlocked', 'Frozen', 'Thawed', 'Off', 'On'." } },
                            { "MakeCurrent", new ToolParameter { Type = "boolean", Description = "Czy ustawić warstwę jako aktualną (tylko przy 'Create')." } },
                            { "Linetype", new ToolParameter { Type = "string", Description = "Nazwa rodzaju linii (opcjonalne przy 'Create')." } }
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
            string newName = args["NewName"]?.ToString() ?? "";
            string color = args["ColorIndex"]?.ToString() ?? "";
            string state = args["State"]?.ToString() ?? "";
            bool makeCurrent = args["MakeCurrent"] != null && (bool)args["MakeCurrent"];
            string linetype = args["Linetype"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(layerPattern)) return "BŁĄD: Nazwa warstwy nie może być pusta.";

            int successCount = 0;
            StringBuilder sb = new StringBuilder();

            try
            {
                using (DocumentLock docLock = doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);

                        // Obsługa wielu warstw (lista po przecinku)
                        string[] layersToProcess = layerPattern.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string rawName in layersToProcess)
                        {
                            string lName = rawName.Trim();
                            bool isWildcard = lName.Contains("*") || lName.Contains("?");

                            if (action.Equals("Create", StringComparison.OrdinalIgnoreCase))
                            {
                                if (isWildcard) continue;
                                LayerTableRecord ltr;
                                if (!lt.Has(lName))
                                {
                                    ltr = new LayerTableRecord { Name = lName };
                                    lt.Add(ltr);
                                    tr.AddNewlyCreatedDBObject(ltr, true);
                                }
                                else
                                {
                                    ltr = (LayerTableRecord)tr.GetObject(lt[lName], OpenMode.ForWrite);
                                }

                                // Kolor
                                if (!string.IsNullOrEmpty(color) && short.TryParse(color, out short cIdx))
                                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, cIdx);

                                // Linetype
                                if (!string.IsNullOrEmpty(linetype))
                                {
                                    LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                                    if (ltt.Has(linetype)) ltr.LinetypeObjectId = ltt[linetype];
                                }

                                if (makeCurrent) db.Clayer = ltr.ObjectId;
                                successCount++;
                            }
                            else if (action.Equals("Toggle", StringComparison.OrdinalIgnoreCase) || action.Equals("Rename", StringComparison.OrdinalIgnoreCase) || action.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                            {
                                Regex regex = isWildcard ? new Regex("^" + Regex.Escape(lName).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase) : null;

                                foreach (ObjectId id in lt)
                                {
                                    LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                                    if (isWildcard ? regex.IsMatch(ltr.Name) : ltr.Name.Equals(lName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (action.Equals("Toggle", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ltr.UpgradeOpen();
                                            if (state.Equals("Locked", StringComparison.OrdinalIgnoreCase)) ltr.IsLocked = true;
                                            else if (state.Equals("Unlocked", StringComparison.OrdinalIgnoreCase)) ltr.IsLocked = false;
                                            else if (state.Equals("Frozen", StringComparison.OrdinalIgnoreCase)) { if (db.Clayer != id) ltr.IsFrozen = true; }
                                            else if (state.Equals("Thawed", StringComparison.OrdinalIgnoreCase)) ltr.IsFrozen = false;
                                            else if (state.Equals("Off", StringComparison.OrdinalIgnoreCase)) ltr.IsOff = true;
                                            else if (state.Equals("On", StringComparison.OrdinalIgnoreCase)) ltr.IsOff = false;
                                            successCount++;
                                        }
                                        else if (action.Equals("Rename", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(newName) && !isWildcard)
                                        {
                                            if (!lt.Has(newName))
                                            {
                                                ltr.UpgradeOpen();
                                                ltr.Name = newName;
                                                successCount++;
                                            }
                                        }
                                        else if (action.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (ltr.Name != "0" && !ltr.Name.Equals("Defpoints", StringComparison.OrdinalIgnoreCase) && db.Clayer != id)
                                            {
                                                try
                                                {
                                                    ltr.UpgradeOpen();
                                                    ltr.Erase();
                                                    successCount++;
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        tr.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY: {ex.Message}";
            }

            return $"SUKCES: Wykonano akcję '{action}' dla {successCount} warstw(y).";
        }
    }
}
