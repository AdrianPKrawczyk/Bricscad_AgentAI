using System;
using System.Collections.Generic;
using System.IO;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools.Layout
{
    public class SheetSetSheetTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "SheetSetSheetTool",
                    Description = "Pozwala modyfikowac konkretne arkusze (Sheet) wewnatrz pliku Sheet Set (.dst). Umozliwia dodawanie (AddSheet z layoutu DWG), zmiane numeracji i nazwy. Uzywa COM Late-binding.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Action", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Akcja do wykonania na arkuszu: 'AddSheet', 'Rename', 'Renumber'.",
                                    Enum = new List<string> { "AddSheet", "Rename", "Renumber" }
                                }
                            },
                            {
                                "DstFilePath", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Sciezka do pliku .dst (zestaw arkuszy)."
                                }
                            },
                            {
                                "SheetNumber", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Obecny numer arkusza do edycji (dla Rename/Renumber)."
                                }
                            },
                            {
                                "NewSheetNumber", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nowy numer arkusza (dla Renumber)."
                                }
                            },
                            {
                                "NewSheetName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nowa nazwa arkusza (dla Rename)."
                                }
                            },
                            {
                                "SourceDwgPath", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Pelna sciezka do pliku DWG, z ktorego dodajemy arkusz (dla AddSheet)."
                                }
                            },
                            {
                                "SourceLayoutName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa layoutu w pliku DWG (dla AddSheet)."
                                }
                            }
                        },
                        Required = new List<string> { "Action", "DstFilePath" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string action = args["Action"]?.ToString();
            string dstPath = args["DstFilePath"]?.ToString();
            string sheetNumber = args["SheetNumber"]?.ToString();
            string newSheetNumber = args["NewSheetNumber"]?.ToString();
            string newSheetName = args["NewSheetName"]?.ToString();
            string sourceDwg = args["SourceDwgPath"]?.ToString();
            string sourceLayout = args["SourceLayoutName"]?.ToString();

            if (string.IsNullOrWhiteSpace(dstPath) || !File.Exists(dstPath))
                return "BLAD: Plik DST nie istnieje lub nie podano sciezki.";

            try
            {
                Type mgrType = Type.GetTypeFromProgID("BricscadSm.AcSmSheetSetMgr") 
                               ?? Type.GetTypeFromProgID("AutoCAD.AcSmSheetSetMgr.22")
                               ?? Type.GetTypeFromProgID("AutoCAD.AcSmSheetSetMgr.24");

                if (mgrType == null) return "BLAD: Nie znaleziono obiektu COM AcSmSheetSetMgr.";

                dynamic mgr = Activator.CreateInstance(mgrType);
                dynamic db = mgr.OpenDatabase(dstPath, false);
                
                if (db == null) return "BLAD: Nie udalo sie otworzyc pliku DST.";

                bool locked = false;
                try
                {
                    locked = SheetSetComHelpers.TryLockDatabase(mgr, db);
                    dynamic sheetSet = db.GetSheetSet();

                    if (string.Equals(action, "AddSheet", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(sourceDwg) || string.IsNullOrWhiteSpace(sourceLayout))
                            return "BLAD: Akcja AddSheet wymaga SourceDwgPath i SourceLayoutName.";

                        if (FindSheetByName(sheetSet, sourceLayout) != null)
                            return $"SUKCES: Arkusz '{sourceLayout}' juz istnieje w zestawie.";
                        
                        object newSheet;
                        string importError;
                        if (!SheetSetComHelpers.TryImportSheetTyped(sheetSet, sourceDwg, sourceLayout, out newSheet, out importError))
                        {
                            return $"BLAD: Nie udalo sie zaimportowac arkusza z pliku DWG. Typowany COM: {importError}";
                        }

                        if (newSheet == null) return $"BLAD: Nie udalo sie zaimportowac arkusza z pliku DWG.";

                        try
                        {
                            dynamic sheet = newSheet;
                            sheet.SetName(sourceLayout);
                            sheet.SetTitle(sourceLayout);
                            if (string.IsNullOrWhiteSpace(sheet.GetNumber()))
                                sheet.SetNumber(sourceLayout);
                        }
                        catch
                        {
                            // Some Sheet Set implementations fill metadata during import; continue with insertion.
                        }

                        bool inserted = FindSheetByName(sheetSet, sourceLayout) != null
                            || SheetSetComHelpers.TryInsertComponent(sheetSet, newSheet);

                        if (!inserted)
                            return $"BLAD: ImportSheet utworzyl obiekt arkusza, ale nie udalo sie wstawic go do struktury zestawu.";

                        SheetSetComHelpers.TrySaveDatabase(mgr, db, dstPath);

                        if (FindSheetByName(sheetSet, sourceLayout) == null)
                            return $"BLAD: ImportSheet zwrocil arkusz, ale po zapisie nie znaleziono go w enumeratorze zestawu. Nie zglaszam sukcesu.";

                        return $"SUKCES: Arkusz '{sourceLayout}' z pliku DWG zostal dodany do zestawu.";
                    }
                    else if (string.Equals(action, "Rename", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(sheetNumber) || string.IsNullOrWhiteSpace(newSheetName))
                            return "BLAD: Akcja Rename wymaga SheetNumber i NewSheetName.";
                        
                        dynamic targetSheet = FindSheetByNumber(sheetSet, sheetNumber);
                        if (targetSheet == null) return $"BLAD: Nie znaleziono arkusza o numerze '{sheetNumber}'.";
                        
                        targetSheet.SetName(newSheetName);
                        try { targetSheet.SetTitle(newSheetName); } catch { }
                        SheetSetComHelpers.TrySaveDatabase(mgr, db, dstPath);

                        dynamic renamedSheet = FindSheetByNumber(sheetSet, sheetNumber);
                        if (renamedSheet == null)
                            return $"BLAD: Po zmianie nazwy nie znaleziono arkusza o numerze '{sheetNumber}' w zestawie.";

                        string actualName = string.Empty;
                        string actualTitle = string.Empty;
                        try { actualName = renamedSheet.GetName(); } catch { }
                        try { actualTitle = renamedSheet.GetTitle(); } catch { }

                        if (!string.Equals(actualName, newSheetName, StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(actualTitle, newSheetName, StringComparison.OrdinalIgnoreCase))
                        {
                            return $"BLAD: Wywolano zmiane nazwy, ale arkusz nadal ma Name='{actualName}', Title='{actualTitle}'.";
                        }

                        return $"SUKCES: Arkusz '{sheetNumber}' zmienil nazwe na '{newSheetName}'.";
                    }
                    else if (string.Equals(action, "Renumber", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(sheetNumber) || string.IsNullOrWhiteSpace(newSheetNumber))
                            return "BLAD: Akcja Renumber wymaga SheetNumber i NewSheetNumber.";
                        
                        dynamic targetSheet = FindSheetByNumber(sheetSet, sheetNumber);
                        if (targetSheet == null) return $"BLAD: Nie znaleziono arkusza o numerze '{sheetNumber}'.";
                        
                        targetSheet.SetNumber(newSheetNumber);
                        SheetSetComHelpers.TrySaveDatabase(mgr, db, dstPath);

                        if (FindSheetByNumber(sheetSet, newSheetNumber) == null)
                            return $"BLAD: Wywolano zmiane numeru, ale nie znaleziono arkusza o nowym numerze '{newSheetNumber}'.";

                        return $"SUKCES: Arkusz oznaczony '{sheetNumber}' ma teraz numer '{newSheetNumber}'.";
                    }

                    return $"BLAD: Nieznana akcja '{action}'.";
                }
                finally
                {
                    if (locked) SheetSetComHelpers.TryUnlockDatabase(mgr, db);
                    SheetSetComHelpers.TryCloseDatabase(mgr, db);
                }
            }
            catch (Exception ex)
            {
                return $"BLAD PODCZAS EDYCJI ARKUSZA (COM): {ex.Message}";
            }
        }

        private dynamic FindSheetByNumber(dynamic parent, string targetNumber)
        {
            dynamic iter = parent.GetSheetEnumerator();
            if (iter == null) return null;
            iter.Reset();
            dynamic comp = iter.Next();
            while (comp != null)
            {
                string typeName = comp.GetTypeName();
                if (typeName == "AcSmSubset")
                {
                    dynamic found = FindSheetByNumber(comp, targetNumber);
                    if (found != null) return found;
                }
                else if (typeName == "AcSmSheet")
                {
                    string num = comp.GetNumber();
                    if (string.Equals(num, targetNumber, StringComparison.OrdinalIgnoreCase))
                        return comp;
                }
                comp = iter.Next();
            }
            return null;
        }

        private dynamic FindSheetByName(dynamic parent, string targetName)
        {
            dynamic iter = parent.GetSheetEnumerator();
            if (iter == null) return null;

            iter.Reset();
            dynamic comp = iter.Next();
            while (comp != null)
            {
                string typeName = comp.GetTypeName();
                if (typeName == "AcSmSubset")
                {
                    dynamic found = FindSheetByName(comp, targetName);
                    if (found != null) return found;
                }
                else if (typeName == "AcSmSheet")
                {
                    string name = comp.GetName();
                    string title = string.Empty;
                    try { title = comp.GetTitle(); } catch { }

                    if (string.Equals(name, targetName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(title, targetName, StringComparison.OrdinalIgnoreCase))
                        return comp;
                }

                comp = iter.Next();
            }

            return null;
        }

        public List<string> Examples => new List<string>
        {
            "{ \"Action\": \"AddSheet\", \"DstFilePath\": \"C:\\\\Test.dst\", \"SourceDwgPath\": \"C:\\\\Test.dwg\", \"SourceLayoutName\": \"Layout1\" }",
            "{ \"Action\": \"Rename\", \"DstFilePath\": \"C:\\\\Test.dst\", \"SheetNumber\": \"01\", \"NewSheetName\": \"Rzut Parteru\" }"
        };
    }
}
