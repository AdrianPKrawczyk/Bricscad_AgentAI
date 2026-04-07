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
                            { "Action", new ToolParameter { Type = "string", Description = "Akcja: 'Create', 'Modify', 'Toggle', 'Rename', 'Delete', 'SetCurrent'." } },
                            { "LayerName", new ToolParameter { Type = "string", Description = "Nazwa warstwy, lista nazw po przecinku lub maska. Aby edytować podkłady XREF, użyj znaku '|' (np. 'PODKLAD|*' do wyszarzenia całego podkładu)." } },
                            { "NewName", new ToolParameter { Type = "string", Description = "Nowa nazwa (tylko dla akcji 'Rename')." } },
                            { "ColorIndex", new ToolParameter { Type = "integer", Description = "Wskaźnik koloru ACI (1-255)." } },
                            { "State", new ToolParameter { Type = "string", Description = "Stan dla akcji 'Toggle': 'Locked', 'Unlocked', 'Frozen', 'Thawed', 'Off', 'On'." } },
                            { "MakeCurrent", new ToolParameter { Type = "boolean", Description = "Czy ustawić warstwę jako aktualną." } },
                            { "Linetype", new ToolParameter { Type = "string", Description = "Nazwa rodzaju linii (np. 'Continuous', 'Hidden')." } },
                            { "Transparency", new ToolParameter { Type = "integer", Description = "Przezroczystość 0-90 (0 = brak, 90 = pełna)." } },
                            { "LineWeight", new ToolParameter { Type = "integer", Description = "Szerokość linii w setnych milimetra (np. 30 dla 0.30mm, -1 = ByLayer, -3 = Default)." } },
                            { "Plottable", new ToolParameter { Type = "boolean", Description = "Czy warstwa ma być drukowana (true/false)." } }
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

            // Bezpieczne parsowanie Boolean
            bool makeCurrent = false;
            if (args["MakeCurrent"] != null)
            {
                makeCurrent = args["MakeCurrent"].Value<bool>();
            }

            if (string.IsNullOrEmpty(layerPattern)) return "BŁĄD: Nazwa warstwy (LayerName) nie może być pusta.";

            string[] validActions = { "Create", "Modify", "Toggle", "Rename", "Delete", "SetCurrent" };
            if (!validActions.Contains(action, StringComparer.OrdinalIgnoreCase))
            {
                return $"BŁĄD: Nieobsługiwana akcja '{action}'. Dozwolone wartości to: Create, Modify, Toggle, Rename, Delete, SetCurrent.";
            }

            int successCount = 0;
            string layerToMakeCurrent = null; // Zapisujemy NAZWĘ, nie ObjectId

            // KRYTYCZNE ZABEZPIECZENIE TEIGHA API (zgodnie z v2.12.5)
            Database oldDb = HostApplicationServices.WorkingDatabase;
            HostApplicationServices.WorkingDatabase = db;

            try
            {
                using (DocumentLock docLock = doc.LockDocument())
                {
                    // ==========================================
                    // FAZA 1: Tworzenie struktury warstwy
                    // ==========================================
                    using (Transaction tr = doc.TransactionManager.StartTransaction()) // doc.TransactionManager!
                    {
                        LayerTable lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForWrite);
                        string[] layersToProcess = layerPattern.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (string rawName in layersToProcess)
                        {
                            string lName = rawName.Trim();
                            bool isWildcard = lName.Contains("*") || lName.Contains("?");

                            bool isCreate = action.Equals("Create", StringComparison.OrdinalIgnoreCase);
                            bool isModify = action.Equals("Modify", StringComparison.OrdinalIgnoreCase);

                            if (isCreate || isModify)
                            {
                                Regex regex = isWildcard ? new Regex("^" + Regex.Escape(lName).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase) : null;
                                // HOTFIX: Operujemy na wskaźnikach w pamięci, aby uniknąć błędu Double-Open
                                List<LayerTableRecord> targets = new List<LayerTableRecord>();

                                if (isCreate)
                                {
                                    if (isWildcard) return $"BŁĄD: Nie można utworzyć warstwy ze znakami maski (*, ?): {lName}";

                                    if (!lt.Has(lName))
                                    {
                                        LayerTableRecord newLtr = new LayerTableRecord();
                                        newLtr.Name = lName;
                                        lt.Add(newLtr);
                                        tr.AddNewlyCreatedDBObject(newLtr, true);
                                        
                                        targets.Add(newLtr); // Dodajemy bezpośrednią referencję!
                                        EngineTracer.Log($"Próba dodania warstwy '{lName}' do tabeli...");
                                    }
                                    else
                                    {
                                        LayerTableRecord modLtr = (LayerTableRecord)tr.GetObject(lt[lName], OpenMode.ForWrite);
                                        targets.Add(modLtr);
                                    }
                                }
                                else // Akcja: Modify
                                {
                                    if (isWildcard)
                                    {
                                        foreach (ObjectId id in lt)
                                        {
                                            LayerTableRecord checkLtr = (LayerTableRecord)tr.GetObject(id, OpenMode.ForRead);
                                            if (regex.IsMatch(checkLtr.Name))
                                            {
                                                checkLtr.UpgradeOpen(); // Bezpieczne podniesienie uprawnień dla istniejącego obiektu
                                                targets.Add(checkLtr);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (lt.Has(lName))
                                        {
                                            LayerTableRecord modLtr = (LayerTableRecord)tr.GetObject(lt[lName], OpenMode.ForWrite);
                                            targets.Add(modLtr);
                                        }
                                        else return $"BŁĄD: Warstwa '{lName}' nie istnieje w rysunku.";
                                    }
                                }

                                // Pętla przypisująca atrybuty bez ponownego GetObject!
                                foreach (LayerTableRecord ltr in targets)
                                {
                                    if (!string.IsNullOrEmpty(color) && short.TryParse(color, out short cIdx))
                                    {
                                        ltr.Color = Color.FromColorIndex(ColorMethod.ByAci, cIdx);
                                    }

                                    if (!string.IsNullOrEmpty(linetype))
                                    {
                                        LinetypeTable ltt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                                        if (ltt.Has(linetype)) ltr.LinetypeObjectId = ltt[linetype];
                                        else return $"BŁĄD: Rodzaj linii '{linetype}' nie istnieje w rysunku.";
                                    }

                                    if (args["Transparency"] != null)
                                    {
                                        int transVal = args["Transparency"].Value<int>();
                                        if (transVal < 0) transVal = 0;
                                        if (transVal > 90) transVal = 90;
                                        byte alpha = (byte)(255 * (100 - transVal) / 100);
                                        ltr.Transparency = new Transparency(alpha);
                                    }

                                    if (args["LineWeight"] != null)
                                    {
                                        int lwVal = args["LineWeight"].Value<int>();
                                        ltr.LineWeight = (LineWeight)lwVal;
                                    }

                                    if (args["Plottable"] != null)
                                    {
                                        ltr.IsPlottable = args["Plottable"].Value<bool>();
                                    }

                                    successCount++;
                                }

                                if (makeCurrent && !isWildcard && targets.Count > 0)
                                {
                                    if (targets[0].IsDependent) return $"BŁĄD: Warstwa '{lName}' pochodzi z odnośnika XREF i nie może być warstwą roboczą.";
                                    layerToMakeCurrent = lName;
                                }
                            }
                            else if (action.Equals("SetCurrent", StringComparison.OrdinalIgnoreCase))
                            {
                                if (isWildcard) return "BŁĄD: Ustawianie warstwy bieżącej (SetCurrent) nie obsługuje masek (wildcards).";
                                if (!lt.Has(lName)) return $"BŁĄD: Warstwa '{lName}' nie istnieje w rysunku.";

                                LayerTableRecord checkLtr = (LayerTableRecord)tr.GetObject(lt[lName], OpenMode.ForRead);
                                if (checkLtr.IsDependent) return $"BŁĄD: Warstwa '{lName}' jest warstwą zależną (XREF) i nie może stać się roboczą.";

                                layerToMakeCurrent = lName;
                                makeCurrent = true; // Wymuszamy aktywację w Fazie 2 (AutoLISP)
                                successCount++;
                            }
                            else if (action.Equals("Rename", StringComparison.OrdinalIgnoreCase))
                            {
                                if (isWildcard) return "BŁĄD: Zmiana nazwy (Rename) nie obsługuje masek (wildcards).";
                                if (string.IsNullOrEmpty(newName)) return "BŁĄD: Parametr 'NewName' nie może być pusty.";
                                if (!lt.Has(lName)) return $"BŁĄD: Warstwa '{lName}' nie istnieje.";
                                if (lt.Has(newName)) return $"BŁĄD: Warstwa docelowa '{newName}' już istnieje.";

                                LayerTableRecord ltr = (LayerTableRecord)tr.GetObject(lt[lName], OpenMode.ForWrite);
                                if (ltr.IsDependent) return $"BŁĄD: Nie można zmieniać nazwy warstwom z odnośników XREF ({lName}).";
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
                                            else return $"BŁĄD: Nieznany stan '{state}'. Dozwolone: Locked, Unlocked, Frozen, Thawed, Off, On.";
                                        }
                                        else if (action.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (ltr.Name != "0" && !ltr.Name.Equals("Defpoints", StringComparison.OrdinalIgnoreCase) && db.Clayer != id && !ltr.IsDependent)
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
                    // Twardy Commit zapisuje warstwę i powiadamia interfejs o nowym obiekcie
                    tr.Commit();
                    EngineTracer.Log("Zakończono Fazę 1 (DB Commit). Oczekuję na przetworzenie UI...");
                }

                    // ==========================================
                    // FAZA 2: Aktywacja warstwy i odświeżenie GUI (Wątek Główny)
                    // ==========================================
                    bool suppressUI = args["SuppressUI"]?.Value<bool>() ?? false;

                    if (!suppressUI)
                    {
                        StringBuilder lispCommands = new StringBuilder();

                        if (makeCurrent && !string.IsNullOrEmpty(layerToMakeCurrent))
                        {
                            // LISP (setvar "CLAYER" "nazwa") to kuloodporny sposób na ustawienie warstwy w tle
                            lispCommands.Append($"(setvar \"CLAYER\" \"{layerToMakeCurrent}\") ");
                        }

                        // Opcjonalnie: Jeśli zmieniono widoczność lub usunięto warstwy, wymuszamy odrysowanie geometrii
                        if (action.Equals("Toggle", StringComparison.OrdinalIgnoreCase) || action.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                        {
                            lispCommands.Append("(command \"_.REGEN\") ");
                        }

                        // [CRITICAL FIX]: Gwarancja odświeżenia UI! Zawsze szturchamy wątek główny (nawet przy Create),
                        // zmuszając Menedżer Warstw do pokazania zmian z Fazy 1.
                        if (lispCommands.Length == 0)
                        {
                            lispCommands.Append("(princ) ");
                        }

                        // Wysłanie paczki poleceń do głównej pętli. Spacja na końcu działa jak ostateczny Enter.
                        doc.SendStringToExecute(lispCommands.ToString() + "\n", true, false, false);
                    }
                }
            }
        catch (Exception ex)
        {
            return $"BŁĄD KRYTYCZNY: {ex.Message}";
        }
        finally
        {
            // Zawsze przywracamy stary kontekst bazy!
            HostApplicationServices.WorkingDatabase = oldDb;
        }

        return $"SUKCES: Wykonano akcję '{action}' dla {successCount} warstw(y).";
    }
}
}