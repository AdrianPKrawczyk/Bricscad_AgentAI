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
                                    Description = "Akcja do wykonania: 'Create' (tworzy plik .dst), 'List' (wypisuje strukture), 'CreateSubset' (tworzy nowa podgrupe), 'ListMetadata' (wypisuje ustawienia zestawu z UI Sheet Set Manager), 'SetMetadata' (zmienia ustawienia zestawu: nazwa/opis/lokalizacja/szablon/bloki), 'ListProperties' (wypisuje wlasciwosci uzytkownika), 'SetProperty' (tworzy lub zmienia wlasciwosc uzytkownika), 'RegisterPropertyName' (zapamietuje recznie utworzone nazwy etykiet do pozniejszego listowania), 'DiagnoseCom' (diagnostyka sygnatur COM na kopii DST), 'DiagnoseProperties' (diagnostyka CustomPropertyBag w DST).",
                                    Enum = new List<string> { "Create", "List", "CreateSubset", "ListMetadata", "SetMetadata", "ListProperties", "SetProperty", "RegisterPropertyName", "DiagnoseCom", "DiagnoseProperties" }
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
                                    Description = "Opcjonalna nazwa zestawu arkuszy uzywana przy tworzeniu (Create) albo zmianie metadanych (SetMetadata). Domyslnie brana z nazwy pliku."
                                }
                            },
                            {
                                "Description", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opis zestawu arkuszy dla Action=SetMetadata."
                                }
                            },
                            {
                                "NewSheetLocation", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Domyslna lokalizacja nowych arkuszy/DWG w Sheet Set Manager (SetNewSheetLocation)."
                                }
                            },
                            {
                                "SheetCreationTemplatePath", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Sciezka do pliku DWT/DWG uzywanego jako wzor tworzenia arkuszy (SetDefDwtLayout)."
                                }
                            },
                            {
                                "SheetCreationTemplateLayout", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa layoutu w pliku SheetCreationTemplatePath dla wzoru tworzenia arkuszy."
                                }
                            },
                            {
                                "LabelBlockPath", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Sciezka DWG/DWT z blokiem etykiety arkusza (SetDefLabelBlk)."
                                }
                            },
                            {
                                "LabelBlockName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa bloku etykiety arkusza w pliku LabelBlockPath."
                                }
                            },
                            {
                                "CalloutBlockPath", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Sciezka DWG/DWT z blokiem wywolania (callout)."
                                }
                            },
                            {
                                "CalloutBlockName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa bloku wywolania (callout) w pliku CalloutBlockPath."
                                }
                            },
                            {
                                "ProjectNumber", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Numer projektu z sekcji Kontrola Projektu. Action=SetMetadata probuje zapisac przez late-binding SetProjectNumber; jesli COM odrzuci, zwraca ostrzezenie zamiast falszywego sukcesu."
                                }
                            },
                            {
                                "ProjectName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa projektu z sekcji Kontrola Projektu. Action=SetMetadata probuje zapisac przez late-binding SetProjectName; jesli COM odrzuci, zwraca ostrzezenie zamiast falszywego sukcesu."
                                }
                            },
                            {
                                "ProjectPhase", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Faza projektu z sekcji Kontrola Projektu. Action=SetMetadata probuje zapisac przez late-binding SetProjectPhase; jesli COM odrzuci, zwraca ostrzezenie zamiast falszywego sukcesu."
                                }
                            },
                            {
                                "ProjectMilestone", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Kamien milowy projektu z sekcji Kontrola Projektu. Action=SetMetadata probuje zapisac przez late-binding SetProjectMilestone; jesli COM odrzuci, zwraca ostrzezenie zamiast falszywego sukcesu."
                                }
                            },
                            {
                                "SubsetName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa subsetu (podgrupy) uzywana przy tworzeniu (CreateSubset) oraz diagnostyce (DiagnoseCom)."
                                }
                            },
                            {
                                "SourceDwgPath", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna sciezka DWG do diagnostyki ImportSheet w Action=DiagnoseCom."
                                }
                            },
                            {
                                "SourceLayoutName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna nazwa layoutu do diagnostyki ImportSheet w Action=DiagnoseCom."
                                }
                            },
                            {
                                "PropertyScope", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Zakres wlasciwosci uzytkownika: 'SheetSet' dla Dostosuj Wlasciwosci Zestawu Arkuszy, 'Sheet' dla domyslnych Dostosuj Wlasciwosci Arkusza, albo 'SheetInstance' dla wartosci na konkretnym arkuszu.",
                                    Enum = new List<string> { "SheetSet", "Sheet", "SheetInstance" }
                                }
                            },
                            {
                                "PropertyName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa/etykieta wlasciwosci uzytkownika, np. 'Test_1'."
                                }
                            },
                            {
                                "PropertyNames", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna lista nazw etykiet oddzielona przecinkami/srednikami albo tablicą JSON. Uzywana przy ListProperties do sprawdzenia recznych etykiet oraz przy RegisterPropertyName do zapisania ich w indeksie."
                                }
                            },
                            {
                                "PropertyValue", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Wartosc lub domyslna wartosc wlasciwosci uzytkownika."
                                }
                            },
                            {
                                "SheetNumber", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Numer arkusza wymagany przy PropertyScope='SheetInstance'."
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
            string description = args["Description"]?.ToString();
            string newSheetLocation = args["NewSheetLocation"]?.ToString();
            string sheetCreationTemplatePath = args["SheetCreationTemplatePath"]?.ToString();
            string sheetCreationTemplateLayout = args["SheetCreationTemplateLayout"]?.ToString();
            string labelBlockPath = args["LabelBlockPath"]?.ToString();
            string labelBlockName = args["LabelBlockName"]?.ToString();
            string calloutBlockPath = args["CalloutBlockPath"]?.ToString();
            string calloutBlockName = args["CalloutBlockName"]?.ToString();
            string projectNumber = args["ProjectNumber"]?.ToString();
            string projectName = args["ProjectName"]?.ToString();
            string projectPhase = args["ProjectPhase"]?.ToString();
            string projectMilestone = args["ProjectMilestone"]?.ToString();
            string subsetName = args["SubsetName"]?.ToString();
            string sourceDwg = args["SourceDwgPath"]?.ToString();
            string sourceLayout = args["SourceLayoutName"]?.ToString();
            string propertyScope = args["PropertyScope"]?.ToString();
            string propertyName = args["PropertyName"]?.ToString();
            string[] propertyNames = ParsePropertyNames(args["PropertyNames"]);
            string propertyValue = args["PropertyValue"]?.ToString();
            string sheetNumber = args["SheetNumber"]?.ToString();
            string originalDstPath = dstPath;

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

            if (string.Equals(action, "DiagnoseCom", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string dir = Path.GetDirectoryName(dstPath);
                    string fileName = Path.GetFileNameWithoutExtension(dstPath);
                    string ext = Path.GetExtension(dstPath);
                    string diagPath = Path.Combine(dir, $"{fileName}.diag-{DateTime.Now:yyyyMMdd-HHmmss}{ext}");
                    File.Copy(dstPath, diagPath, false);
                    dstPath = diagPath;
                }
                catch (Exception ex)
                {
                    return $"BLAD: Nie mozna utworzyc kopii diagnostycznej DST. Komunikat: {ex.Message}";
                }
            }

            if (string.Equals(action, "RegisterPropertyName", StringComparison.OrdinalIgnoreCase))
            {
                return RegisterCustomPropertyNames(dstPath, propertyName, propertyNames);
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
                        sb.AppendLine($"Plik DST: {dstPath}");
                        sb.AppendLine($"Wszystkich arkuszy: {CountSheets(sheetSet)}");
                        ListItems(sheetSet, sb, 0);
                        return sb.ToString();
                    }
                    else if (string.Equals(action, "ListMetadata", StringComparison.OrdinalIgnoreCase))
                    {
                        return ListSheetSetMetadata(db, sheetSet, dstPath);
                    }
                    else if (string.Equals(action, "SetMetadata", StringComparison.OrdinalIgnoreCase))
                    {
                        return SetSheetSetMetadata(
                            mgr,
                            db,
                            sheetSet,
                            dstPath,
                            name,
                            description,
                            newSheetLocation,
                            sheetCreationTemplatePath,
                            sheetCreationTemplateLayout,
                            labelBlockPath,
                            labelBlockName,
                            calloutBlockPath,
                            calloutBlockName,
                            projectNumber,
                            projectName,
                            projectPhase,
                            projectMilestone);
                    }
                    else if (string.Equals(action, "ListProperties", StringComparison.OrdinalIgnoreCase))
                    {
                        return ListCustomProperties(sheetSet, propertyScope, sheetNumber, dstPath, propertyNames);
                    }
                    else if (string.Equals(action, "DiagnoseProperties", StringComparison.OrdinalIgnoreCase))
                    {
                        return DiagnoseCustomProperties(db, sheetSet);
                    }
                    else if (string.Equals(action, "DiagnoseCom", StringComparison.OrdinalIgnoreCase))
                    {
                        string report = DiagnoseComSignatures(mgr, db, sheetSet, originalDstPath, dstPath, subsetName, sourceDwg, sourceLayout);
                        string reportPath = Path.ChangeExtension(dstPath, ".diagnose.txt");
                        File.WriteAllText(reportPath, report, Encoding.UTF8);
                        return BuildDiagnosticSummary(report, reportPath);
                    }
                    else if (string.Equals(action, "CreateSubset", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrWhiteSpace(subsetName)) return "BLAD: Wymagany SubsetName.";
                        dynamic newSubset = sheetSet.CreateSubset(subsetName, subsetName);
                        string typedInsertError;
                        bool inserted = SheetSetComHelpers.TryInsertComponentTyped(sheetSet, newSubset, out typedInsertError)
                            || SheetSetComHelpers.TryInsertComponent(sheetSet, newSubset);
                        if (!inserted && SheetSetComHelpers.TryCreateStandaloneSubset(subsetName, subsetName, out object standaloneSubset))
                        {
                            inserted = SheetSetComHelpers.TryInsertComponentTyped(sheetSet, standaloneSubset, out typedInsertError)
                                || SheetSetComHelpers.TryInsertComponent(sheetSet, standaloneSubset);
                        }
                        if (!inserted && !ContainsSubsetByName(sheetSet, subsetName))
                        {
                            return $"BLAD: Utworzono obiekt podgrupy '{subsetName}', ale COM nie pozwolil wstawic go do zestawu przez InsertComponent/AddComponent. Typowany COM: {typedInsertError}";
                        }
                        if (!locked) SheetSetComHelpers.TrySaveDatabase(mgr, db, dstPath);
                        return $"SUKCES: Utworzono podgrupe '{subsetName}'.";
                    }
                    else if (string.Equals(action, "SetProperty", StringComparison.OrdinalIgnoreCase))
                    {
                        return SetCustomProperty(mgr, db, sheetSet, dstPath, propertyScope, propertyName, propertyValue, sheetNumber);
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

        private string ListSheetSetMetadata(dynamic db, dynamic sheetSet, string dstPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- METADANE ZESTAWU ARKUSZY: {SafeGetString(sheetSet, "GetName")} ---");
            sb.AppendLine();
            sb.AppendLine("[Ustawienie Arkusza]");
            sb.AppendLine($"Nazwa: {SafeGetString(sheetSet, "GetName")}");
            sb.AppendLine($"Opis: {SafeGetString(sheetSet, "GetDesc")}");
            sb.AppendLine($"Sciezka pliku: {dstPath}");
            sb.AppendLine($"Blok etykiety: {DescribeObjectReference(SafeInvoke(sheetSet, "GetDefLabelBlk"))}");
            sb.AppendLine($"Wywolanie bloku: {DescribeCalloutBlocks(SafeInvoke(sheetSet, "GetCalloutBlocks"))}");
            sb.AppendLine($"Wszystkich arkuszy: {CountSheets(sheetSet)}");
            sb.AppendLine();
            sb.AppendLine("[Kontrola Projektu]");
            AppendProjectControlProbe(sb, db, sheetSet);
            sb.AppendLine();
            sb.AppendLine("[Stworz Arkusz]");
            sb.AppendLine($"Nowa lokalizacja arkusza: {DescribeFileReference(SafeInvoke(sheetSet, "GetNewSheetLocation"))}");
            sb.AppendLine($"Wzor wydruku arkuszy: {DescribeObjectReference(SafeInvoke(sheetSet, "GetDefDwtLayout"))}");
            return sb.ToString();
        }

        private string SetSheetSetMetadata(
            dynamic mgr,
            dynamic db,
            dynamic sheetSet,
            string dstPath,
            string name,
            string description,
            string newSheetLocation,
            string sheetCreationTemplatePath,
            string sheetCreationTemplateLayout,
            string labelBlockPath,
            string labelBlockName,
            string calloutBlockPath,
            string calloutBlockName,
            string projectNumber,
            string projectName,
            string projectPhase,
            string projectMilestone)
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- ZMIANA METADANYCH ZESTAWU ARKUSZY ---");
            int changed = 0;
            int warnings = 0;

            if (!string.IsNullOrWhiteSpace(name))
            {
                if (TryInvoke(sheetSet, "SetName", name))
                {
                    changed++;
                    sb.AppendLine($"OK: Nazwa = {SafeGetString(sheetSet, "GetName")}");
                }
                else
                {
                    warnings++;
                    sb.AppendLine("BLAD: COM odrzucil SetName.");
                }
            }

            if (description != null)
            {
                if (TryInvoke(sheetSet, "SetDesc", description))
                {
                    changed++;
                    sb.AppendLine($"OK: Opis = {SafeGetString(sheetSet, "GetDesc")}");
                }
                else
                {
                    warnings++;
                    sb.AppendLine("BLAD: COM odrzucil SetDesc.");
                }
            }

            if (!string.IsNullOrWhiteSpace(newSheetLocation))
            {
                object fileReference = CreateFileReference(sheetSet, newSheetLocation);
                if (fileReference != null && TryInvoke(sheetSet, "SetNewSheetLocation", fileReference))
                {
                    changed++;
                    sb.AppendLine($"OK: Nowa lokalizacja arkusza = {DescribeFileReference(SafeInvoke(sheetSet, "GetNewSheetLocation"))}");
                }
                else
                {
                    warnings++;
                    sb.AppendLine("BLAD: Nie udalo sie ustawic NewSheetLocation.");
                }
            }

            if (!string.IsNullOrWhiteSpace(sheetCreationTemplatePath) || !string.IsNullOrWhiteSpace(sheetCreationTemplateLayout))
            {
                if (string.IsNullOrWhiteSpace(sheetCreationTemplatePath) || string.IsNullOrWhiteSpace(sheetCreationTemplateLayout))
                {
                    warnings++;
                    sb.AppendLine("BLAD: Wzor wydruku arkuszy wymaga SheetCreationTemplatePath oraz SheetCreationTemplateLayout.");
                }
                else
                {
                    object layoutReference = CreateLayoutReference(sheetSet, sheetCreationTemplatePath, sheetCreationTemplateLayout);
                    if (layoutReference != null && TryInvoke(sheetSet, "SetDefDwtLayout", layoutReference))
                    {
                        changed++;
                        sb.AppendLine($"OK: Wzor wydruku arkuszy = {DescribeObjectReference(SafeInvoke(sheetSet, "GetDefDwtLayout"))}");
                    }
                    else
                    {
                        warnings++;
                        sb.AppendLine("BLAD: Nie udalo sie ustawic DefDwtLayout.");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(labelBlockPath) || !string.IsNullOrWhiteSpace(labelBlockName))
            {
                if (string.IsNullOrWhiteSpace(labelBlockPath) || string.IsNullOrWhiteSpace(labelBlockName))
                {
                    warnings++;
                    sb.AppendLine("BLAD: Blok etykiety wymaga LabelBlockPath oraz LabelBlockName.");
                }
                else
                {
                    object blockReference = CreateBlockReference(sheetSet, labelBlockPath, labelBlockName);
                    if (blockReference != null && TryInvoke(sheetSet, "SetDefLabelBlk", blockReference))
                    {
                        changed++;
                        sb.AppendLine($"OK: Blok etykiety = {DescribeObjectReference(SafeInvoke(sheetSet, "GetDefLabelBlk"))}");
                    }
                    else
                    {
                        warnings++;
                        sb.AppendLine("BLAD: Nie udalo sie ustawic DefLabelBlk.");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(calloutBlockPath) || !string.IsNullOrWhiteSpace(calloutBlockName))
            {
                if (string.IsNullOrWhiteSpace(calloutBlockPath) || string.IsNullOrWhiteSpace(calloutBlockName))
                {
                    warnings++;
                    sb.AppendLine("BLAD: Wywolanie bloku wymaga CalloutBlockPath oraz CalloutBlockName.");
                }
                else
                {
                    object calloutBlocks = SafeInvoke(sheetSet, "GetCalloutBlocks");
                    object blockReference = CreateBlockReference(sheetSet, calloutBlockPath, calloutBlockName);
                    if (calloutBlocks != null && blockReference != null && TryInvoke(calloutBlocks, "Add", blockReference))
                    {
                        changed++;
                        sb.AppendLine($"OK: Dodano wywolanie bloku = {DescribeObjectReference(blockReference)}");
                    }
                    else
                    {
                        warnings++;
                        sb.AppendLine("BLAD: Nie udalo sie dodac CalloutBlock.");
                    }
                }
            }

            ApplyProjectControlValue(sheetSet, "Numer projektu", "ProjectNumber", projectNumber, ref changed, ref warnings, sb);
            ApplyProjectControlValue(sheetSet, "Nazwa projektu", "ProjectName", projectName, ref changed, ref warnings, sb);
            ApplyProjectControlValue(sheetSet, "Faza projektu", "ProjectPhase", projectPhase, ref changed, ref warnings, sb);
            ApplyProjectControlValue(sheetSet, "Kamien milowy projektu", "ProjectMilestone", projectMilestone, ref changed, ref warnings, sb);

            if (changed > 0)
                SheetSetComHelpers.TrySaveDatabase(mgr, db, dstPath);

            if (changed == 0 && warnings == 0)
                return "BLAD: Action=SetMetadata nie otrzymal zadnego parametru do zmiany.";

            sb.AppendLine();
            sb.AppendLine($"Podsumowanie: zmieniono {changed}, ostrzezenia/bledy {warnings}.");
            sb.AppendLine();
            sb.Append(ListSheetSetMetadata(db, sheetSet, dstPath));
            return sb.ToString();
        }

        private void AppendProjectControlProbe(StringBuilder sb, object db, object sheetSet)
        {
            AppendProjectControlLine(sb, db, sheetSet, "Numer projektu", "ProjectNumber", "Project Number", "Numer projektu");
            AppendProjectControlLine(sb, db, sheetSet, "Nazwa projektu", "ProjectName", "Project Name", "Nazwa projektu");
            AppendProjectControlLine(sb, db, sheetSet, "Faza projektu", "ProjectPhase", "Project Phase", "Faza projektu");
            AppendProjectControlLine(sb, db, sheetSet, "Kamien milowy projektu", "ProjectMilestone", "Project Milestone", "Kamien milowy projektu");
            sb.AppendLine("Status API: BricscadSm.Interop.dll nie deklaruje metod Get/SetProject..., ale narzedzie probuje je przez late-binding oraz przez CustomPropertyBag.");
        }

        private void AppendProjectControlLine(StringBuilder sb, object db, object sheetSet, string label, string methodSuffix, params string[] candidateNames)
        {
            string directValue = SafeGetString(sheetSet, "Get" + methodSuffix);
            if (!string.IsNullOrWhiteSpace(directValue))
            {
                sb.AppendLine($"{label}: {directValue} (Get{methodSuffix})");
                return;
            }

            foreach (string candidate in candidateNames)
            {
                if (SheetSetComHelpers.TryGetCustomProperty(sheetSet, candidate, out SheetSetComHelpers.CustomPropertyInfo property, out _))
                {
                    sb.AppendLine($"{label}: {property.Value} (SheetSet CustomPropertyBag: '{candidate}')");
                    return;
                }

                if (db != null && SheetSetComHelpers.TryGetCustomProperty(db, candidate, out property, out _))
                {
                    sb.AppendLine($"{label}: {property.Value} (Database CustomPropertyBag: '{candidate}')");
                    return;
                }
            }

            sb.AppendLine($"{label}: (brak / nieudostepnione przez COM)");
        }

        private void ApplyProjectControlValue(object sheetSet, string label, string methodSuffix, string value, ref int changed, ref int warnings, StringBuilder sb)
        {
            if (value == null)
                return;

            string setter = "Set" + methodSuffix;
            string getter = "Get" + methodSuffix;

            if (TryInvoke(sheetSet, setter, value))
            {
                string verified = SafeGetString(sheetSet, getter);
                changed++;
                sb.AppendLine(string.IsNullOrWhiteSpace(verified)
                    ? $"OK: {label} ustawiono przez {setter}, ale COM nie zwrocil wartosci przez {getter}."
                    : $"OK: {label} = {verified}");
                return;
            }

            warnings++;
            sb.AppendLine($"UWAGA: Nie udalo sie ustawic pola '{label}' ({setter}). Nie zapisuje go jako zwyklej Wlasciwosci Uzytkownika, zeby nie udawac zmiany pola UI.");
        }

        private int CountSheets(dynamic parent)
        {
            int count = 0;
            CountSheets(parent, ref count, 0);
            return count;
        }

        private void CountSheets(dynamic parent, ref int count, int depth)
        {
            if (depth > 20) return;

            dynamic iter = null;
            try { iter = parent.GetSheetEnumerator(); } catch { }
            if (iter == null) return;

            try { iter.Reset(); } catch { }
            for (int i = 0; i < 1000; i++)
            {
                dynamic comp = null;
                try { comp = iter.Next(); } catch { break; }
                if (comp == null) break;

                string typeName = SafeGetString(comp, "GetTypeName");
                if (string.Equals(typeName, "AcSmSheet", StringComparison.OrdinalIgnoreCase))
                    count++;
                else if (string.Equals(typeName, "AcSmSubset", StringComparison.OrdinalIgnoreCase))
                    CountSheets(comp, ref count, depth + 1);
            }
        }

        private string DescribeFileReference(object reference)
        {
            if (reference == null) return "(brak)";

            string fileName = SafeGetString(reference, "GetFileName");
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = SafeGetString(reference, "ResolveFileName");

            return string.IsNullOrWhiteSpace(fileName) ? "(brak)" : fileName;
        }

        private string DescribeObjectReference(object reference)
        {
            if (reference == null) return "(brak)";

            string fileName = SafeGetString(reference, "GetFileName");
            string name = SafeGetString(reference, "GetName");
            string handle = SafeGetString(reference, "GetAcDbHandle");

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(fileName)) parts.Add(fileName);
            if (!string.IsNullOrWhiteSpace(name)) parts.Add($"Name={name}");
            if (!string.IsNullOrWhiteSpace(handle)) parts.Add($"Handle={handle}");

            return parts.Count == 0 ? SheetSetComHelpers.DescribeComObject(reference) : string.Join("; ", parts);
        }

        private string DescribeCalloutBlocks(object calloutBlocks)
        {
            if (calloutBlocks == null) return "(brak)";

            object enumerator = SafeInvoke(calloutBlocks, "GetEnumerator");
            if (enumerator == null) return "(brak)";

            var values = new List<string>();
            TryInvoke(enumerator, "Reset");
            for (int i = 0; i < 200; i++)
            {
                object item = SafeInvoke(enumerator, "Next");
                if (item == null) break;
                values.Add(DescribeObjectReference(item));
            }

            return values.Count == 0 ? "(brak)" : string.Join(" | ", values.ToArray());
        }

        private object CreateFileReference(object owner, string fileName)
        {
            object reference = CreateSheetSetComObject(
                "BricscadSm.AcSmFileReference",
                "BricscadSm.AcSmFileReference.23.0",
                "BricscadSm.AcSmFileReference.22.0");

            if (reference == null) return null;
            TryInvoke(reference, "InitNew", owner);
            TryInvoke(reference, "SetOwner", owner);
            return TryInvoke(reference, "SetFileName", fileName) ? reference : null;
        }

        private object CreateLayoutReference(object owner, string fileName, string layoutName)
        {
            object reference = CreateSheetSetComObject(
                "BricscadSm.AcSmAcDbLayoutReference",
                "BricscadSm.AcSmAcDbLayoutReference.23.0",
                "BricscadSm.AcSmAcDbLayoutReference.22.0");

            if (reference == null) return null;
            TryInvoke(reference, "InitNew", owner);
            TryInvoke(reference, "SetOwner", owner);
            bool ok = TryInvoke(reference, "SetFileName", fileName)
                && TryInvoke(reference, "SetName", layoutName);
            return ok ? reference : null;
        }

        private object CreateBlockReference(object owner, string fileName, string blockName)
        {
            object reference = CreateSheetSetComObject(
                "BricscadSm.AcSmAcDbBlockRecordReference",
                "BricscadSm.AcSmAcDbBlockRecordReference.23.0",
                "BricscadSm.AcSmAcDbBlockRecordReference.22.0");

            if (reference == null) return null;
            TryInvoke(reference, "InitNew", owner);
            TryInvoke(reference, "SetOwner", owner);
            bool ok = TryInvoke(reference, "SetFileName", fileName)
                && TryInvoke(reference, "SetName", blockName);
            return ok ? reference : null;
        }

        private object CreateSheetSetComObject(params string[] progIds)
        {
            foreach (string progId in progIds)
            {
                Type type = Type.GetTypeFromProgID(progId);
                if (type == null) continue;

                try { return Activator.CreateInstance(type); }
                catch { }
            }

            return null;
        }

        private string SafeGetString(object target, string methodName)
        {
            object result = SafeInvoke(target, methodName);
            return result?.ToString() ?? string.Empty;
        }

        private object SafeInvoke(object target, string methodName, params object[] args)
        {
            if (target == null) return null;

            try
            {
                return target.GetType().InvokeMember(
                    methodName,
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    target,
                    args,
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        private bool TryInvoke(object target, string methodName, params object[] args)
        {
            if (target == null) return false;

            try
            {
                target.GetType().InvokeMember(
                    methodName,
                    System.Reflection.BindingFlags.InvokeMethod,
                    null,
                    target,
                    args,
                    System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool ContainsSubsetByName(dynamic parent, string subsetName)
        {
            dynamic iter = parent.GetSheetEnumerator();
            if (iter == null) return false;

            iter.Reset();
            dynamic comp = iter.Next();
            while (comp != null)
            {
                string typeName = comp.GetTypeName();
                string name = comp.GetName();

                if (typeName == "AcSmSubset")
                {
                    if (string.Equals(name, subsetName, StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (ContainsSubsetByName(comp, subsetName))
                        return true;
                }

                comp = iter.Next();
            }

            return false;
        }

        private string DiagnoseCustomProperties(dynamic database, dynamic sheetSet)
        {
            var sb = new StringBuilder();
            sb.AppendLine("DIAGNOZA WLASCIWOSCI UZYTKOWNIKA DST");
            sb.AppendLine($"Database: {SheetSetComHelpers.DescribeComObject((object)database)}");
            sb.AppendLine($"SheetSet: {SheetSetComHelpers.DescribeComObject((object)sheetSet)}");
            sb.AppendLine();

            AppendPropertyOwnerDiagnostics(sb, "Database", database);
            AppendPropertyOwnerDiagnostics(sb, "SheetSet", sheetSet);

            sb.AppendLine();
            sb.AppendLine("[Database.GetEnumerator()]");
            try
            {
                dynamic iter = database.GetEnumerator();
                if (iter == null)
                {
                    sb.AppendLine("(brak enumeratora)");
                }
                else
                {
                    iter.Reset();
                    for (int i = 0; i < 200; i++)
                    {
                        dynamic item = iter.Next();
                        if (item == null) break;
                        AppendPropertyOwnerDiagnostics(sb, $"DbItem[{i}]", item);
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"BLAD Database.GetEnumerator: {ex.GetType().Name}: {ex.Message}");
            }

            sb.AppendLine();
            sb.AppendLine("[SheetSet components]");
            AppendComponentPropertyDiagnostics(sb, sheetSet, "SheetSet", 0);

            return sb.ToString();
        }

        private void AppendPropertyOwnerDiagnostics(StringBuilder sb, string label, object owner)
        {
            sb.AppendLine($"-- {label}: {SheetSetComHelpers.DescribeComObject(owner)}");
            string report = SheetSetComHelpers.DescribeCustomPropertyBag(owner, new[] { "Test_1", "Test_2", "Test_3", "Test_4" });
            sb.AppendLine(report);
        }

        private void AppendComponentPropertyDiagnostics(StringBuilder sb, dynamic parent, string path, int depth)
        {
            if (depth > 8) return;

            dynamic iter = null;
            try { iter = parent.GetSheetEnumerator(); } catch { }
            if (iter == null) return;

            iter.Reset();
            for (int i = 0; i < 200; i++)
            {
                dynamic comp = iter.Next();
                if (comp == null) break;

                string typeName = string.Empty;
                string name = string.Empty;
                try { typeName = comp.GetTypeName(); } catch { }
                try { name = comp.GetName(); } catch { }

                string childPath = $"{path}/{typeName}:{name}";
                AppendPropertyOwnerDiagnostics(sb, childPath, comp);

                if (string.Equals(typeName, "AcSmSubset", StringComparison.OrdinalIgnoreCase))
                    AppendComponentPropertyDiagnostics(sb, comp, childPath, depth + 1);
            }
        }

        private string ListCustomProperties(dynamic sheetSet, string propertyScope, string sheetNumber, string dstPath, string[] requestedPropertyNames)
        {
            string scope = NormalizePropertyScope(propertyScope);
            string[] knownPropertyNames = GetKnownCustomPropertyNames(dstPath, requestedPropertyNames);
            var sb = new StringBuilder();
            sb.AppendLine($"--- WLASCIWOSCI UZYTKOWNIKA: {sheetSet.GetName()} ---");

            if (scope == "All")
            {
                AppendCustomPropertySection(sb, "Dostosuj Wlasciwosci Zestawu Arkuszy", sheetSet, SheetSetComHelpers.CustomSheetSetPropertyFlag, knownPropertyNames);
                AppendCustomPropertySection(sb, "Dostosuj Wlasciwosci Arkusza", sheetSet, SheetSetComHelpers.CustomSheetPropertyFlag, knownPropertyNames);
                AppendSheetPropertyFallbackSection(sb, sheetSet);
                return sb.ToString();
            }

            object owner = ResolvePropertyOwner(sheetSet, scope, sheetNumber, out string error);
            if (owner == null) return error;

            int flag = scope == "SheetSet"
                ? SheetSetComHelpers.CustomSheetSetPropertyFlag
                : SheetSetComHelpers.CustomSheetPropertyFlag;

            string title = scope == "SheetSet"
                ? "Dostosuj Wlasciwosci Zestawu Arkuszy"
                : scope == "SheetInstance"
                    ? $"Wlasciwosci arkusza {sheetNumber}"
                    : "Dostosuj Wlasciwosci Arkusza";

            AppendCustomPropertySection(sb, title, owner, flag, knownPropertyNames);
            if (scope == "Sheet")
                AppendSheetPropertyFallbackSection(sb, sheetSet);

            return sb.ToString();
        }

        private string SetCustomProperty(dynamic mgr, dynamic db, dynamic sheetSet, string dstPath, string propertyScope, string propertyName, string propertyValue, string sheetNumber)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return "BLAD: Action=SetProperty wymaga PropertyName.";

            string scope = NormalizePropertyScope(propertyScope);
            if (scope == "All") scope = "SheetSet";

            object owner = ResolvePropertyOwner(sheetSet, scope, sheetNumber, out string error);
            if (owner == null) return error;

            int flag = scope == "SheetSet"
                ? SheetSetComHelpers.CustomSheetSetPropertyFlag
                : SheetSetComHelpers.CustomSheetPropertyFlag;

            if (!SheetSetComHelpers.TrySetCustomProperty(owner, propertyName, propertyValue ?? string.Empty, flag, out error))
                return $"BLAD: Nie udalo sie ustawic wlasciwosci '{propertyName}'. {error}";

            if (scope == "Sheet")
                SheetSetComHelpers.TryUpdateSheetCustomProps(sheetSet);

            SheetSetComHelpers.TrySaveDatabase(mgr, db, dstPath);

            if (!SheetSetComHelpers.TryGetCustomProperty(owner, propertyName, out SheetSetComHelpers.CustomPropertyInfo verified, out error))
                return $"BLAD: Wywolano SetProperty, ale po zapisie nie znaleziono wlasciwosci '{propertyName}'. Nie zglaszam sukcesu.";

            string expectedValue = propertyValue ?? string.Empty;
            if (!string.Equals(verified.Value, expectedValue, StringComparison.Ordinal))
                return $"BLAD: Wlasciwosc '{propertyName}' zostala odczytana z wartoscia '{verified.Value}', oczekiwano '{expectedValue}'.";

            RememberCustomPropertyName(dstPath, propertyName);
            return $"SUKCES: Ustawiono wlasciwosc '{propertyName}' = '{expectedValue}' w zakresie {scope}.";
        }

        private void AppendCustomPropertySection(StringBuilder sb, string title, object owner, int flag, string[] knownPropertyNames)
        {
            sb.AppendLine();
            sb.AppendLine($"[{title}]");

            if (!SheetSetComHelpers.TryListCustomProperties(owner, null, out SheetSetComHelpers.CustomPropertyInfo[] allProperties, out string error))
            {
                sb.AppendLine($"BLAD: {error}");
                return;
            }

            var properties = new List<SheetSetComHelpers.CustomPropertyInfo>();
            foreach (SheetSetComHelpers.CustomPropertyInfo property in allProperties)
            {
                if ((property.Flags & flag) == flag)
                    properties.Add(property);
            }

            if (properties.Count == 0)
                AddKnownDirectProperties(owner, flag, properties, knownPropertyNames);

            if (properties.Count == 0 && allProperties.Length > 0)
            {
                foreach (SheetSetComHelpers.CustomPropertyInfo property in allProperties)
                {
                    properties.Add(property);
                }
            }

            if (properties.Count == 0)
            {
                sb.AppendLine("(brak)");
                return;
            }

            foreach (SheetSetComHelpers.CustomPropertyInfo property in properties)
            {
                sb.AppendLine($"- {property.Name} = {property.Value} (Flags={property.Flags})");
            }
        }

        private void AddKnownDirectProperties(object owner, int flag, List<SheetSetComHelpers.CustomPropertyInfo> properties, string[] knownPropertyNames)
        {
            foreach (string propertyName in knownPropertyNames)
            {
                if (!SheetSetComHelpers.TryGetCustomProperty(owner, propertyName, out SheetSetComHelpers.CustomPropertyInfo property, out _))
                    continue;

                if ((property.Flags & flag) != flag)
                    continue;

                bool alreadyExists = false;
                foreach (SheetSetComHelpers.CustomPropertyInfo existing in properties)
                {
                    if (string.Equals(existing.Name, property.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (!alreadyExists)
                    properties.Add(property);
            }
        }

        private string[] GetKnownCustomPropertyNames(string dstPath, string[] requestedPropertyNames)
        {
            var names = new List<string>
            {
                "Test_1",
                "Test_2",
                "Test_3",
                "Test_4"
            };

            foreach (string indexedName in LoadCustomPropertyNameIndex(dstPath))
            {
                bool exists = false;
                foreach (string name in names)
                {
                    if (string.Equals(name, indexedName, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    names.Add(indexedName);
            }

            foreach (string requestedName in requestedPropertyNames ?? new string[0])
            {
                bool exists = false;
                foreach (string name in names)
                {
                    if (string.Equals(name, requestedName, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    names.Add(requestedName);
            }

            return names.ToArray();
        }

        private string RegisterCustomPropertyNames(string dstPath, string propertyName, string[] propertyNames)
        {
            var names = new List<string>();
            if (!string.IsNullOrWhiteSpace(propertyName))
                names.Add(propertyName);

            foreach (string name in propertyNames ?? new string[0])
            {
                if (!string.IsNullOrWhiteSpace(name))
                    names.Add(name);
            }

            if (names.Count == 0)
                return "BLAD: RegisterPropertyName wymaga PropertyName albo PropertyNames.";

            foreach (string name in names)
                RememberCustomPropertyName(dstPath, name);

            return $"SUKCES: Zarejestrowano nazwy wlasciwosci uzytkownika dla DST: {string.Join(", ", names)}.";
        }

        private string[] ParsePropertyNames(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
                return new string[0];

            var result = new List<string>();
            if (token.Type == JTokenType.Array)
            {
                foreach (JToken item in token)
                {
                    string value = item?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                        result.Add(value.Trim());
                }
            }
            else
            {
                string raw = token.ToString();
                char[] separators = { ',', ';', '\n', '\r', '\t' };
                foreach (string part in raw.Split(separators, StringSplitOptions.RemoveEmptyEntries))
                {
                    string value = part.Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                        result.Add(value);
                }
            }

            return result.ToArray();
        }

        private string[] LoadCustomPropertyNameIndex(string dstPath)
        {
            string indexPath = GetCustomPropertyIndexPath(dstPath);
            if (string.IsNullOrWhiteSpace(indexPath) || !File.Exists(indexPath))
                return new string[0];

            try
            {
                JObject root = JObject.Parse(File.ReadAllText(indexPath, Encoding.UTF8));
                JArray names = root["Names"] as JArray;
                if (names == null)
                    return new string[0];

                var result = new List<string>();
                foreach (JToken item in names)
                {
                    string name = item?.ToString();
                    if (!string.IsNullOrWhiteSpace(name))
                        result.Add(name);
                }

                return result.ToArray();
            }
            catch
            {
                return new string[0];
            }
        }

        private void RememberCustomPropertyName(string dstPath, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                return;

            string indexPath = GetCustomPropertyIndexPath(dstPath);
            if (string.IsNullOrWhiteSpace(indexPath))
                return;

            try
            {
                var names = new List<string>(LoadCustomPropertyNameIndex(dstPath));
                bool exists = false;
                foreach (string name in names)
                {
                    if (string.Equals(name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    names.Add(propertyName);

                var root = new JObject
                {
                    ["Names"] = new JArray(names)
                };

                File.WriteAllText(indexPath, root.ToString(), Encoding.UTF8);
            }
            catch
            {
                // The index is only a fallback for BricsCAD COM builds with a broken property enumerator.
            }
        }

        private string GetCustomPropertyIndexPath(string dstPath)
        {
            if (string.IsNullOrWhiteSpace(dstPath))
                return null;

            return dstPath + ".properties.json";
        }

        private void AppendSheetPropertyFallbackSection(StringBuilder sb, dynamic sheetSet)
        {
            var found = new Dictionary<string, SheetSetComHelpers.CustomPropertyInfo>(StringComparer.OrdinalIgnoreCase);
            CollectSheetCustomProperties(sheetSet, found);
            if (found.Count == 0)
                return;

            sb.AppendLine();
            sb.AppendLine("[Wlasciwosci arkuszy odczytane z obiektow arkuszy]");
            foreach (KeyValuePair<string, SheetSetComHelpers.CustomPropertyInfo> item in found)
            {
                SheetSetComHelpers.CustomPropertyInfo property = item.Value;
                sb.AppendLine($"- {property.Name} = {property.Value} (Flags={property.Flags})");
            }
        }

        private void CollectSheetCustomProperties(dynamic parent, Dictionary<string, SheetSetComHelpers.CustomPropertyInfo> found)
        {
            dynamic iter = null;
            try { iter = parent.GetSheetEnumerator(); } catch { }
            if (iter == null) return;

            iter.Reset();
            for (int i = 0; i < 1000; i++)
            {
                dynamic comp = iter.Next();
                if (comp == null) break;

                string typeName = string.Empty;
                try { typeName = comp.GetTypeName(); } catch { }

                if (string.Equals(typeName, "AcSmSubset", StringComparison.OrdinalIgnoreCase))
                {
                    CollectSheetCustomProperties(comp, found);
                    continue;
                }

                if (!string.Equals(typeName, "AcSmSheet", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!SheetSetComHelpers.TryListCustomProperties((object)comp, null, out SheetSetComHelpers.CustomPropertyInfo[] properties, out _))
                    continue;

                foreach (SheetSetComHelpers.CustomPropertyInfo property in properties)
                {
                    if (string.IsNullOrWhiteSpace(property.Name))
                        continue;

                    if (!found.ContainsKey(property.Name))
                        found[property.Name] = property;
                }
            }
        }

        private void AppendListPropertiesTrace(StringBuilder sb, dynamic sheetSet, object owner, string scope, int flag, string dstPath)
        {
            var trace = new StringBuilder();
            trace.AppendLine("[DEBUG ListProperties full trace]");
            trace.AppendLine($"Marker: LPTRACE-20260619-1218");
            trace.AppendLine($"Scope: {scope}");
            trace.AppendLine($"ExpectedFlag: {flag}");
            trace.AppendLine($"SheetSetObject: {SheetSetComHelpers.DescribeComObject((object)sheetSet)}");
            trace.AppendLine($"OwnerObject: {SheetSetComHelpers.DescribeComObject(owner)}");

            int ownerEnumCount = -1;
            string ownerEnumError = null;
            if (SheetSetComHelpers.TryListCustomProperties(owner, null, out SheetSetComHelpers.CustomPropertyInfo[] ownerProperties, out string ownerError))
            {
                ownerEnumCount = ownerProperties.Length;
                trace.AppendLine($"OwnerEnumCount: {ownerProperties.Length}");
                foreach (SheetSetComHelpers.CustomPropertyInfo property in ownerProperties)
                    trace.AppendLine($"  owner enum: {property.Name} = {property.Value} (Flags={property.Flags})");
            }
            else
            {
                ownerEnumError = ownerError;
                trace.AppendLine($"OwnerEnumError: {ownerError}");
            }

            trace.AppendLine("OwnerDirectKnown:");
            trace.Append(SheetSetComHelpers.DescribeCustomPropertyBag(owner, new[] { "Test_1", "Test_2", "Test_3", "Test_4" }));

            trace.AppendLine("SheetEnumeratorTrace:");
            int subsetCount = 0;
            int sheetCount = 0;
            int propertyCount = 0;
            AppendSheetEnumeratorTrace(trace, sheetSet, "SheetSet", 0, ref subsetCount, ref sheetCount, ref propertyCount);
            trace.AppendLine($"SheetEnumeratorSummary: subsets={subsetCount}, sheets={sheetCount}, properties={propertyCount}");

            string tracePath = TryWriteListPropertiesTrace(dstPath, trace.ToString());

            sb.AppendLine();
            sb.AppendLine("[DEBUG ListProperties summary]");
            sb.AppendLine("Marker: LPTRACE-20260619-1218");
            sb.AppendLine($"Scope: {scope}");
            sb.AppendLine(ownerEnumCount >= 0
                ? $"OwnerEnumCount: {ownerEnumCount}"
                : $"OwnerEnumError: {ownerEnumError}");
            AppendKnownPropertySummary(sb, owner, "Test_1");
            AppendKnownPropertySummary(sb, owner, "Test_2");
            AppendKnownPropertySummary(sb, owner, "Test_3");
            AppendKnownPropertySummary(sb, owner, "Test_4");
            sb.AppendLine($"SheetEnumeratorSummary: subsets={subsetCount}, sheets={sheetCount}, properties={propertyCount}");
            if (!string.IsNullOrWhiteSpace(tracePath))
                sb.AppendLine($"FullTraceFile: {tracePath}");
        }

        private void AppendKnownPropertySummary(StringBuilder sb, object owner, string propertyName)
        {
            if (SheetSetComHelpers.TryGetCustomProperty(owner, propertyName, out SheetSetComHelpers.CustomPropertyInfo property, out string error))
                sb.AppendLine($"DirectKnown {propertyName}: {property.Value} (Flags={property.Flags})");
            else
                sb.AppendLine($"DirectKnown {propertyName}: BRAK ({error ?? "null"})");
        }

        private string TryWriteListPropertiesTrace(string dstPath, string content)
        {
            try
            {
                string directory = Path.GetDirectoryName(dstPath);
                string fileName = Path.GetFileNameWithoutExtension(dstPath);
                string tracePath = Path.Combine(directory, $"{fileName}.listprops-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(tracePath, content, Encoding.UTF8);
                return tracePath;
            }
            catch
            {
                return null;
            }
        }

        private void AppendSheetEnumeratorTrace(StringBuilder sb, dynamic parent, string path, int depth, ref int subsetCount, ref int sheetCount, ref int propertyCount)
        {
            if (depth > 8)
            {
                sb.AppendLine($"  {path}: depth limit");
                return;
            }

            dynamic iter = null;
            try { iter = parent.GetSheetEnumerator(); } catch (Exception ex) { sb.AppendLine($"  {path}: GetSheetEnumerator error {ex.Message}"); }
            if (iter == null)
            {
                sb.AppendLine($"  {path}: no enumerator");
                return;
            }

            try { iter.Reset(); } catch (Exception ex) { sb.AppendLine($"  {path}: Reset error {ex.Message}"); }

            for (int i = 0; i < 1000; i++)
            {
                dynamic comp = null;
                try { comp = iter.Next(); } catch (Exception ex) { sb.AppendLine($"  {path}: Next error {ex.Message}"); break; }
                if (comp == null) break;

                string typeName = string.Empty;
                string name = string.Empty;
                try { typeName = comp.GetTypeName(); } catch { }
                try { name = comp.GetName(); } catch { }

                string childPath = $"{path}/{typeName}:{name}";
                sb.AppendLine($"  component: {childPath}");

                if (string.Equals(typeName, "AcSmSubset", StringComparison.OrdinalIgnoreCase))
                {
                    subsetCount++;
                    AppendSheetEnumeratorTrace(sb, comp, childPath, depth + 1, ref subsetCount, ref sheetCount, ref propertyCount);
                    continue;
                }

                if (!string.Equals(typeName, "AcSmSheet", StringComparison.OrdinalIgnoreCase))
                    continue;

                sheetCount++;
                if (SheetSetComHelpers.TryListCustomProperties((object)comp, null, out SheetSetComHelpers.CustomPropertyInfo[] properties, out string error))
                {
                    sb.AppendLine($"    sheet enum count: {properties.Length}");
                    propertyCount += properties.Length;
                    foreach (SheetSetComHelpers.CustomPropertyInfo property in properties)
                        sb.AppendLine($"    sheet enum: {property.Name} = {property.Value} (Flags={property.Flags})");
                }
                else
                {
                    sb.AppendLine($"    sheet enum error: {error}");
                }
            }
        }

        private string NormalizePropertyScope(string propertyScope)
        {
            if (string.IsNullOrWhiteSpace(propertyScope))
                return "All";

            if (string.Equals(propertyScope, "SheetSet", StringComparison.OrdinalIgnoreCase))
                return "SheetSet";

            if (string.Equals(propertyScope, "Sheet", StringComparison.OrdinalIgnoreCase))
                return "Sheet";

            if (string.Equals(propertyScope, "SheetInstance", StringComparison.OrdinalIgnoreCase))
                return "SheetInstance";

            return propertyScope;
        }

        private object ResolvePropertyOwner(dynamic sheetSet, string scope, string sheetNumber, out string error)
        {
            error = null;

            if (scope == "SheetSet" || scope == "Sheet" || scope == "All")
                return (object)sheetSet;

            if (scope == "SheetInstance")
            {
                if (string.IsNullOrWhiteSpace(sheetNumber))
                {
                    error = "BLAD: PropertyScope='SheetInstance' wymaga SheetNumber.";
                    return null;
                }

                object sheet = FindSheetByNumber(sheetSet, sheetNumber);
                if (sheet == null)
                {
                    error = $"BLAD: Nie znaleziono arkusza o numerze '{sheetNumber}'.";
                    return null;
                }

                return sheet;
            }

            error = $"BLAD: Nieznany PropertyScope '{scope}'. Uzyj SheetSet, Sheet albo SheetInstance.";
            return null;
        }

        private object FindSheetByNumber(dynamic parent, string targetNumber)
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
                    object found = FindSheetByNumber(comp, targetNumber);
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

        private string DiagnoseComSignatures(dynamic manager, dynamic database, dynamic sheetSet, string originalDstPath, string diagnosticDstPath, string subsetName, string sourceDwg, string sourceLayout)
        {
            string diagnosticSubsetName = string.IsNullOrWhiteSpace(subsetName)
                ? "__AGENT_DIAG_SUBSET__"
                : "__AGENT_DIAG_" + subsetName + "__";

            var sb = new StringBuilder();
            sb.AppendLine("DIAGNOZA COM SHEET SET MANAGER");
            sb.AppendLine($"Original DST: {originalDstPath}");
            sb.AppendLine($"Diagnostic copy DST: {diagnosticDstPath}");
            sb.AppendLine($"Diagnostic copy exists: {File.Exists(diagnosticDstPath)}");
            if (File.Exists(diagnosticDstPath))
            {
                sb.AppendLine($"Diagnostic copy size: {new FileInfo(diagnosticDstPath).Length} bytes");
            }

            sb.AppendLine($"Manager: {SheetSetComHelpers.DescribeComObject((object)manager)}");
            sb.AppendLine($"Database: {SheetSetComHelpers.DescribeComObject((object)database)}");
            sb.AppendLine($"SheetSet: {SheetSetComHelpers.DescribeComObject((object)sheetSet)}");

            object createdSubset = null;
            sb.AppendLine();
            sb.AppendLine("[CreateSubset]");
            sb.AppendLine(SheetSetComHelpers.InvokeForDiagnostics(
                "sheetSet.CreateSubset(name, desc)",
                (object)sheetSet,
                "CreateSubset",
                diagnosticSubsetName,
                diagnosticSubsetName));

            try
            {
                createdSubset = sheetSet.CreateSubset(diagnosticSubsetName, diagnosticSubsetName);
                sb.AppendLine($"createdSubset object: {SheetSetComHelpers.DescribeComObject(createdSubset)}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"createdSubset capture failed: {ex.GetType().Name}: {ex.Message}");
            }

            sb.AppendLine();
            sb.AppendLine("[InsertComponent - subset from CreateSubset]");
            sb.AppendLine(SheetSetComHelpers.InvokeTypedInsertForDiagnostics("Typed InsertComponent(component, null)", sheetSet, createdSubset));
            AppendInsertDiagnostics(sb, sheetSet, createdSubset);

            sb.AppendLine();
            sb.AppendLine("[Standalone AcSmSubset]");
            if (SheetSetComHelpers.TryCreateStandaloneSubset(diagnosticSubsetName + "_STANDALONE", diagnosticSubsetName, out object standaloneSubset))
            {
                sb.AppendLine($"standaloneSubset object: {SheetSetComHelpers.DescribeComObject(standaloneSubset)}");
                sb.AppendLine(SheetSetComHelpers.InvokeTypedInsertForDiagnostics("Typed InsertComponent(component, null)", sheetSet, standaloneSubset));
                AppendInsertDiagnostics(sb, sheetSet, standaloneSubset);
            }
            else
            {
                sb.AppendLine("Nie udalo sie utworzyc BricscadSm.AcSmSubset przez ProgID.");
            }

            if (!string.IsNullOrWhiteSpace(sourceDwg) && !string.IsNullOrWhiteSpace(sourceLayout))
            {
                sb.AppendLine();
                sb.AppendLine("[ImportSheet]");
                sb.AppendLine($"SourceDwgPath: {sourceDwg} exists={File.Exists(sourceDwg)}");
                sb.AppendLine($"SourceLayoutName: {sourceLayout}");
                sb.AppendLine(SheetSetComHelpers.InvokeDispatchImportForDiagnostics("Dispatch ImportSheet(layoutReference)", sheetSet, sourceDwg, sourceLayout));
                sb.AppendLine(SheetSetComHelpers.InvokeTypedImportForDiagnostics("Typed ImportSheet(layoutReference)", sheetSet, sourceDwg, sourceLayout));
                sb.AppendLine(SheetSetComHelpers.InvokeForDiagnostics("sheetSet.ImportSheet(null, dwg, layout)", (object)sheetSet, "ImportSheet", null, sourceDwg, sourceLayout));
                sb.AppendLine(SheetSetComHelpers.InvokeForDiagnostics("sheetSet.ImportSheet(dwg, layout)", (object)sheetSet, "ImportSheet", sourceDwg, sourceLayout));
                sb.AppendLine(SheetSetComHelpers.InvokeForDiagnostics("sheetSet.ImportSheet(layout, dwg)", (object)sheetSet, "ImportSheet", sourceLayout, sourceDwg));
                sb.AppendLine(SheetSetComHelpers.InvokeForDiagnostics("sheetSet.ImportSheet(dwg)", (object)sheetSet, "ImportSheet", sourceDwg));
            }

            return sb.ToString();
        }

        private string BuildDiagnosticSummary(string report, string reportPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SUKCES: Zapisano pelny raport diagnostyki COM Sheet Set.");
            sb.AppendLine($"Raport: {reportPath}");
            sb.AppendLine();
            sb.AppendLine("Najwazniejsze linie:");

            string[] lines = report.Replace("\r\n", "\n").Split('\n');
            int added = 0;
            foreach (string line in lines)
            {
                if (line.StartsWith("Original DST:", StringComparison.OrdinalIgnoreCase) ||
                    line.StartsWith("Diagnostic copy DST:", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("CreateSubset") ||
                    line.Contains("InsertComponent") ||
                    line.Contains("AddComponent") ||
                    line.Contains("ImportSheet") ||
                    line.Contains("Exception") ||
                    line.Contains("DISP_") ||
                    line.Contains("Błąd") ||
                    line.Contains("Blad") ||
                    line.Contains("blad") ||
                    line.Contains("nie można") ||
                    line.Contains("Nie można"))
                {
                    sb.AppendLine(line);
                    added++;
                    if (added >= 18) break;
                }
            }

            if (added == 0)
            {
                sb.AppendLine("(Brak oczywistych bledow w skrocie; otworz pelny raport.)");
            }

            return sb.ToString();
        }

        private void AppendInsertDiagnostics(StringBuilder sb, dynamic sheetSet, object component)
        {
            sb.AppendLine(SheetSetComHelpers.InvokeForDiagnostics("InsertComponent(component, null)", (object)sheetSet, "InsertComponent", component, null));
            sb.AppendLine(SheetSetComHelpers.InvokeForDiagnostics("InsertComponent(component)", (object)sheetSet, "InsertComponent", component));
            sb.AppendLine(SheetSetComHelpers.InvokeForDiagnostics("InsertComponent(null, component)", (object)sheetSet, "InsertComponent", null, component));
            sb.AppendLine(SheetSetComHelpers.InvokeForDiagnostics("InsertComponent(Type.Missing, component)", (object)sheetSet, "InsertComponent", Type.Missing, component));
            sb.AppendLine(SheetSetComHelpers.InvokeForDiagnostics("InsertComponent(component, Type.Missing)", (object)sheetSet, "InsertComponent", component, Type.Missing));
            sb.AppendLine(SheetSetComHelpers.InvokeForDiagnostics("AddComponent(component)", (object)sheetSet, "AddComponent", component));
            sb.AppendLine(SheetSetComHelpers.InvokeForDiagnostics("AppendComponent(component)", (object)sheetSet, "AppendComponent", component));
            sb.AppendLine(SheetSetComHelpers.InvokeForDiagnostics("Add(component)", (object)sheetSet, "Add", component));
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
            "{ \"Action\": \"CreateSubset\", \"DstFilePath\": \"C:\\\\Projekty\\\\Projekt.dst\", \"SubsetName\": \"Instalacje\" }",
            "{ \"Action\": \"SetProperty\", \"DstFilePath\": \"C:\\\\Projekty\\\\Projekt.dst\", \"PropertyScope\": \"SheetSet\", \"PropertyName\": \"Projektant\", \"PropertyValue\": \"Jan Kowalski\" }",
            "{ \"Action\": \"SetProperty\", \"DstFilePath\": \"C:\\\\Projekty\\\\Projekt.dst\", \"PropertyScope\": \"Sheet\", \"PropertyName\": \"Branza\", \"PropertyValue\": \"Architektura\" }"
        };
    }
}
