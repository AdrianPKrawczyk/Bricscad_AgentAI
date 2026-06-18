using System;
using System.Collections.Generic;
using System.IO;
using System.Collections.Specialized;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using CadLayout = Teigha.DatabaseServices.Layout;

namespace Bricscad_AgentAI_V2.Tools.Layout
{
    public class PlotStyleTool : IToolV2
    {
        private static readonly string[] ValidActions = { "List", "Load", "GetInfo", "Assign" };
        private static readonly string[] ValidApplyTo = { "CurrentLayout", "AllLayouts", "ByName" };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "PlotStyleTool",
                    Description = "Zarzadzanie stylami wydruku (CTB/STB): listowanie, ladowanie z pliku, odczyt informacji, przypisywanie do layoutu.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Action", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Akcja: 'List', 'Load', 'GetInfo', 'Assign'."
                                }
                            },
                            {
                                "StyleSheetName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa stylu wydruku (np. 'acad.ctb')."
                                }
                            },
                            {
                                "FilePath", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Sciezka do pliku CTB/STB (tylko dla Load)."
                                }
                            },
                            {
                                "PlotStyleType", new ToolParameter
                                {
                                    Type = "string",
                                    Description = $"Typ plot style: 'ColorDependent' (CTB) lub 'Named' (STB). Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.PlotStyleTypeMap)}."
                                }
                            },
                            {
                                "ApplyTo", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Zakres przypisania: 'CurrentLayout', 'AllLayouts', 'ByName'."
                                }
                            },
                            {
                                "LayoutName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa layoutu (dla ApplyTo='ByName')."
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
            string action = args["Action"]?.ToString() ?? "";
            string styleName = args["StyleSheetName"]?.ToString() ?? "";
            string filePath = args["FilePath"]?.ToString() ?? "";
            string plotStyleType = args["PlotStyleType"]?.ToString() ?? "";
            string applyTo = args["ApplyTo"]?.ToString() ?? "CurrentLayout";
            string layoutName = args["LayoutName"]?.ToString() ?? "";

            if (Array.IndexOf(ValidActions, action) < 0)
            {
                return $"BLAD: Nieobslugiwana akcja '{action}'. Dozwolone: {string.Join(", ", ValidActions)}.";
            }

            Database db = doc.Database;
            Database oldDb = HostApplicationServices.WorkingDatabase;
            HostApplicationServices.WorkingDatabase = db;

            try
            {
                using (doc.LockDocument())
                {
                    switch (action.ToLowerInvariant())
                    {
                        case "list":
                            return ListAvailableStyles();
                        case "load":
                            return LoadStyleSheet(doc, styleName, filePath);
                        case "getinfo":
                            return GetStyleInfo(styleName);
                        case "assign":
                            return AssignStyleSheet(db, styleName, applyTo, layoutName);
                        default:
                            return $"BLAD: Nieznana akcja '{action}'.";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"BLAD KRYTYCZNY PLOT STYLE: {ex.Message}";
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = oldDb;
            }
        }

        private string ListAvailableStyles()
        {
            try
            {
                var validator = PlotSettingsValidator.Current;
                validator.RefreshLists(new PlotSettings(false));

                StringCollection stylesCol = validator.GetPlotStyleSheetList();
                if (stylesCol == null || stylesCol.Count == 0)
                {
                    return "WYNIK: Brak zaladowanych stylow wydruku. Uzyj akcji 'Load' aby wczytac plik CTB/STB.";
                }

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"WYNIK: Dostepnych {stylesCol.Count} stylow wydruku:");
                foreach (string s in stylesCol)
                {
                    string ext = System.IO.Path.GetExtension(s).ToLowerInvariant();
                    string type = ext == ".ctb" ? "CTB" : (ext == ".stb" ? "STB" : "?");
                    sb.AppendLine($"  - [{type}] {s}");
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"BLAD LISTOWANIA STYLOW: {ex.Message}";
            }
        }

        private string LoadStyleSheet(Document doc, string styleName, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return "BLAD: FilePath jest wymagany dla akcji Load.";

            string resolvedPath = LayoutHelpers.ValidateAndResolvePath(filePath);
            if (!File.Exists(resolvedPath))
            {
                return $"BLAD: Plik '{resolvedPath}' nie istnieje.";
            }

            try
            {
                string safePath = resolvedPath.Replace("\\", "/");
                doc.SendStringToExecute($"_.PSETUPIN\n\"{safePath}\"\n", true, false, false);
                return $"SUKCES: Zlecono ladowanie stylu z '{resolvedPath}' przez komende PSETUPIN. BricsCAD doda plik do listy dostepnych CTB/STB.";
            }
            catch (Exception ex)
            {
                return $"BLAD LADOWANIA STYLU: {ex.Message}";
            }
        }

        private string GetStyleInfo(string styleName)
        {
            if (string.IsNullOrWhiteSpace(styleName))
                return "BLAD: StyleSheetName jest wymagany dla akcji GetInfo.";

            try
            {
                var validator = PlotSettingsValidator.Current;
                validator.RefreshLists(new PlotSettings(false));

                StringCollection stylesCol = validator.GetPlotStyleSheetList();
                if (stylesCol == null || !stylesCol.Contains(styleName))
                {
                    return $"BLAD: Styl '{styleName}' nie jest zaladowany. Uzyj 'List' aby zobaczyc dostepne.";
                }

                return $"WYNIK: Styl '{styleName}' jest dostepny w systemie.";
            }
            catch (Exception ex)
            {
                return $"BLAD ODCZYTU STYLU: {ex.Message}";
            }
        }

        private string AssignStyleSheet(Database db, string styleName, string applyTo, string layoutName)
        {
            if (string.IsNullOrWhiteSpace(styleName))
                return "BLAD: StyleSheetName jest wymagany dla akcji Assign.";

            if (Array.IndexOf(ValidApplyTo, applyTo) < 0)
            {
                return $"BLAD: Nieobslugiwany ApplyTo '{applyTo}'. Dozwolone: {string.Join(", ", ValidApplyTo)}.";
            }

            int assignedCount = 0;
            var warnings = new List<string>();

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                List<string> targetLayouts = new List<string>();

                if (applyTo.Equals("CurrentLayout", StringComparison.OrdinalIgnoreCase))
                {
                    CadLayout current = LayoutHelpers.GetCurrentLayout(db, tr);
                    if (current != null)
                    {
                        targetLayouts.Add(current.LayoutName);
                    }
                    else
                    {
                        return "BLAD: Nie mozna ustalic biezacego layoutu.";
                    }
                }
                else if (applyTo.Equals("AllLayouts", StringComparison.OrdinalIgnoreCase))
                {
                    targetLayouts.AddRange(LayoutHelpers.ListLayoutNames(db, tr));
                }
                else if (applyTo.Equals("ByName", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(layoutName))
                        return "BLAD: LayoutName jest wymagany dla ApplyTo='ByName'.";
                    if (!LayoutHelpers.LayoutExists(db, layoutName, tr))
                        return $"BLAD: Layout '{layoutName}' nie istnieje.";
                    targetLayouts.Add(layoutName);
                }

                foreach (string lname in targetLayouts)
                {
                    if (lname.Equals(LayoutHelpers.ModelLayoutName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    CadLayout layout = LayoutHelpers.GetLayoutByName(db, lname, tr);
                    if (layout == null) continue;

                    try
                    {
                        layout.UpgradeOpen();
                        PlotSettingsValidator.Current.SetCurrentStyleSheet(layout, styleName);
                        assignedCount++;
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"{lname}: {ex.Message}");
                    }
                }

                tr.Commit();
            }

            if (warnings.Count > 0)
            {
                string warningText = string.Join(" | ", warnings);
                if (assignedCount == 0)
                {
                    return $"BLAD PLOT STYLE: Nie przypisano stylu '{styleName}' do zadnego layoutu. Ostrzezenia: {warningText}";
                }

                return $"BLAD CZESCIOWY PLOT STYLE: Przypisano styl '{styleName}' do {assignedCount} layout(ow), ale wystapily ostrzezenia: {warningText}";
            }
            return $"SUKCES: Przypisano styl '{styleName}' do {assignedCount} layout(ow).";
        }

        public List<string> Examples => new List<string>
        {
            "{ \"Action\": \"List\" }",
            "{ \"Action\": \"Load\", \"FilePath\": \"C:/plotstyles/acad.ctb\" }",
            "{ \"Action\": \"Assign\", \"StyleSheetName\": \"acad.ctb\", \"ApplyTo\": \"AllLayouts\" }",
            "{ \"Action\": \"Assign\", \"StyleSheetName\": \"monochrome.ctb\", \"ApplyTo\": \"ByName\", \"LayoutName\": \"A4-PION\" }"
        };
    }
}
