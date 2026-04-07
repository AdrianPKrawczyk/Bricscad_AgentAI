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
            string linetype = args["Linetype"]?.ToString() ?? "";
            
            // BEZPIECZNE parsowanie booleana z JSON-a
            bool makeCurrent = false;
            if (args["MakeCurrent"] != null)
            {
                bool.TryParse(args["MakeCurrent"].ToString(), out makeCurrent);
            }

            if (string.IsNullOrEmpty(layerPattern)) return "BŁĄD: Nazwa warstwy (LayerName) nie może być pusta.";

            string[] validActions = { "Create", "Toggle", "Rename", "Delete" };
            if (!validActions.Contains(action, StringComparer.OrdinalIgnoreCase))
            {
                return $"BŁĄD: Nieobsługiwana akcja '{action}'. Dozwolone wartości to: Create, Toggle, Rename, Delete.";
            }

            int successCount = 0;
            string layerToMakeCurrent = null;

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
                                if (isWildcard) return $"BŁĄD: Nie można utworzyć warstwy ze znakami maski (*, ?): {lName}";
                                
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
                                {
                                    ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, cIdx);
                                }

                                // Linetype
                                if (!string.IsNullOrEmpty(linetype))
                                {
                                    LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                                    if (ltt.Has(linetype)) ltr.LinetypeObjectId = ltt[linetype];
                                    else return $"BŁĄD: Rodzaj linii '{linetype}' nie istnieje w rysunku.";
                                }

                                if (makeCurrent) layerToMakeCurrent = lName; // Zapamiętujemy, by aktywować PO transakcji
                                successCount++;
                            }
                            else if (action.Equals("Rename", StringComparison.OrdinalIgnoreCase))
                            {
                                if (isWildcard) return "BŁĄD: Zmiana nazwy (Rename) nie obsługuje masek (wildcards).";
                                if (string.IsNullOrEmpty(newName)) return "BŁĄD: Parametr 'NewName' nie może być pusty.";
                                if (!lt.Has(lName)) return $"BŁĄD: Warstwa źródłowa '{lName}' nie istnieje.";
                                if (lt.Has(newName)) return $"BŁĄD: Warstwa docelowa '{newName}' już istnieje.";

                                LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[lName], OpenMode.ForWrite);
                                ltr.Name = newName;
                                successCount++;
                            }
                            else if (action.Equals("Toggle", StringComparison.OrdinalIgnoreCase) || action.Equals("Delete", StringComparison.OrdinalIgnoreCase))
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
                                            bool stateValid = true;
                                            
                                            if (state.Equals("Locked", StringComparison.OrdinalIgnoreCase)) ltr.IsLocked = true;
                                            else if (state.Equals("Unlocked", StringComparison.OrdinalIgnoreCase)) ltr.IsLocked = false;
                                            else if (state.Equals("Frozen", StringComparison.OrdinalIgnoreCase)) { if (db.Clayer != id) ltr.IsFrozen = true; }
                                            else if (state.Equals("Thawed", StringComparison.OrdinalIgnoreCase)) ltr.IsFrozen = false;
                                            else if (state.Equals("Off", StringComparison.OrdinalIgnoreCase)) ltr.IsOff = true;
                                            else if (state.Equals("On", StringComparison.OrdinalIgnoreCase)) ltr.IsOff = false;
                                            else stateValid = false;

                                            if (stateValid) successCount++;
                                            else return $"BŁĄD: Nieznany lub brakujący stan '{state}'. Dozwolone: Locked, Unlocked, Frozen, Thawed, Off, On.";
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
                                                catch { } // Pomija błąd np. gdy warstwa nie jest pusta
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        tr.Commit();
                    }

                    // DRUGA FAZA: Ustawienie jako aktualna po upewnieniu się, że obiekt fizycznie istnieje w bazie
                    if (!string.IsNullOrEmpty(layerToMakeCurrent))
                    {
                        using (Transaction tr2 = db.TransactionManager.StartTransaction())
                        {
                            LayerTable lt2 = (LayerTable)tr2.GetObject(db.LayerTableId, OpenMode.ForRead);
                            if (lt2.Has(layerToMakeCurrent))
                            {
                                db.Clayer = lt2[layerToMakeCurrent];
                            }
                            tr2.Commit();
                        }
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