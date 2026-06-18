using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools.Layout
{
    public class ManageSheetSetTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ManageSheetSetTool",
                    Description = "Zarzadza zestawem arkuszy (plikiem .dst). Pozwala czytac i modyfikowac strukture Sheet Set Manager'a. Uzywa poznego wiazania COM (Late-binding) do obslugi obiektu AcSmSheetSetMgr.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Action", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Akcja do wykonania: 'Create' (tworzy plik .dst), 'List' (wypisuje strukture), 'CreateSubset' (tworzy nowa podgrupe).",
                                    Enum = new List<string> { "Create", "List", "CreateSubset" }
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
                                "Name", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna nazwa zestawu arkuszy uzywana przy tworzeniu (Create). Domyslnie brana z nazwy pliku."
                                }
                            },
                            {
                                "SubsetName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa subsetu (podgrupy) uzywana przy tworzeniu (CreateSubset)."
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
            string name = args["Name"]?.ToString();
            string subsetName = args["SubsetName"]?.ToString();

            if (string.Equals(action, "Create", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(dstPath))
                    return "BLAD: Plik DST musi miec podana sciezke do utworzenia.";
                
                // Dla Create plik nie musi istniec, ale upewnijmy sie ze folder istnieje
                string dir = Path.GetDirectoryName(dstPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    try { Directory.CreateDirectory(dir); } catch { return "BLAD: Nie mozna utworzyc folderu dla pliku DST."; }
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(dstPath) || !File.Exists(dstPath))
                {
                    return "BLAD: Plik DST nie istnieje lub nie podano poprawnej sciezki.";
                }
            }

            try
            {
                Type mgrType = Type.GetTypeFromProgID("BricscadSm.AcSmSheetSetMgr") 
                               ?? Type.GetTypeFromProgID("AutoCAD.AcSmSheetSetMgr.22")
                               ?? Type.GetTypeFromProgID("AutoCAD.AcSmSheetSetMgr.24");

                if (mgrType == null)
                {
                    return "BLAD: Nie znaleziono obiektu COM AcSmSheetSetMgr w systemie (brak zarejestrowanego ProgID).";
                }

                dynamic mgr = Activator.CreateInstance(mgrType);
                dynamic db = null;
                
                if (string.Equals(action, "Create", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        db = mgr.CreateDatabase(dstPath, "", true);
                    }
                    catch (Exception ex)
                    {
                        return $"BLAD: Blad w trakcie tworzenia bazy DST przez COM. Uzyj szablonu (Template) jesli to konieczne. Komunikat: {ex.Message}";
                    }
                    if (db == null) return "BLAD: Nie udalo sie utworzyc bazy DST.";
                    bool createLocked = SheetSetComHelpers.TryLockDatabase(mgr, db);
                    try
                    {
                        dynamic ss = db.GetSheetSet();
                        ss.SetName(string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(dstPath) : name);
                        ss.SetDesc("Zestaw arkuszy wygenerowany przez Agenta");
                        if (!createLocked)
                        {
                            SheetSetComHelpers.TrySaveDatabase(mgr, db, dstPath);
                        }
                        return $"SUKCES: Zestaw arkuszy '{dstPath}' zostal utworzony pomyślnie.";
                    }
                    finally
                    {
                        if (createLocked) SheetSetComHelpers.TryUnlockDatabase(mgr, db);
                        SheetSetComHelpers.TryCloseDatabase(mgr, db);
                        if (!File.Exists(dstPath))
                        {
                            throw new InvalidOperationException($"COM nie zapisal pliku DST na dysku: '{dstPath}'.");
                        }

                        if (new FileInfo(dstPath).Length == 0)
                        {
                            throw new InvalidOperationException($"COM utworzyl pusty plik DST: '{dstPath}'.");
                        }
                    }
                }

                db = mgr.OpenDatabase(dstPath, false); 
                
                if (db == null) return "BLAD: Nie udalo sie otworzyc pliku DST.";

                bool locked = false;
                try
                {
                    locked = SheetSetComHelpers.TryLockDatabase(mgr, db);
                    dynamic sheetSet = db.GetSheetSet();

                    if (string.Equals(action, "List", StringComparison.OrdinalIgnoreCase))
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($"--- ZESTAW ARKUSZY: {sheetSet.GetName()} ---");
                        sb.AppendLine($"Opis: {sheetSet.GetDesc()}");
                        ListItems(sheetSet, sb, 0);
                        return sb.ToString();
                    }
                    else if (string.Equals(action, "CreateSubset", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(subsetName)) return "BLAD: Wymagany SubsetName.";
                        dynamic newSubset = sheetSet.CreateSubset(subsetName, subsetName);
                        sheetSet.InsertComponent(newSubset, null);
                        if (!locked) SheetSetComHelpers.TrySaveDatabase(mgr, db, dstPath);
                        return $"SUKCES: Utworzono podgrupe '{subsetName}'.";
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
                return $"BLAD PODCZAS OBSLUGI SHEET SET MANAGER (COM): {ex.Message}";
            }
        }

        private void ListItems(dynamic parent, StringBuilder sb, int indentLevel)
        {
            string indent = new string(' ', indentLevel * 2);
            dynamic iter = parent.GetSheetEnumerator();
            if (iter == null) return;
            iter.Reset();
            dynamic comp = iter.Next();
            while (comp != null)
            {
                string typeName = comp.GetTypeName();
                string name = comp.GetName();
                
                if (typeName == "AcSmSubset")
                {
                    sb.AppendLine($"{indent}[Subset] {name}");
                    ListItems(comp, sb, indentLevel + 1);
                }
                else if (typeName == "AcSmSheet")
                {
                    string num = comp.GetNumber();
                    sb.AppendLine($"{indent}[Sheet] {num} - {name}");
                }
                comp = iter.Next();
            }
        }

        public List<string> Examples => new List<string>
        {
            "{ \"Action\": \"List\", \"DstFilePath\": \"C:\\\\Projekty\\\\Projekt.dst\" }",
            "{ \"Action\": \"CreateSubset\", \"DstFilePath\": \"C:\\\\Projekty\\\\Projekt.dst\", \"SubsetName\": \"Instalacje\" }"
        };
    }
}
