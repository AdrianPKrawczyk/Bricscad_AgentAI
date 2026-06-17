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
    public class PublishToPdfTool : IToolV2
    {
        private const int MaxLayoutsInMultiSheet = 50;
        private static readonly string[] ValidModes = { "MultiSheet", "SingleFiles" };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "PublishToPdfTool",
                    Description = "Batch publish wielu layoutow do PDF. Tryb 'MultiSheet' = jeden PDF z wszystkimi layoutami. Tryb 'SingleFiles' = osobny PDF per layout.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "LayoutNames", new ToolParameter
                                {
                                    Type = "array",
                                    Description = "Lista nazw layoutow (opcjonalny; puste = wszystkie layouty z rysunku).",
                                    Items = new JObject { ["type"] = "string" }
                                }
                            },
                            {
                                "OutputPdfPath", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Sciezka docelowa PDF (obowiazkowe). Dla MultiSheet - jeden plik. Dla SingleFiles - katalog docelowy lub prefix."
                                }
                            },
                            {
                                "Mode", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Tryb publikacji: 'MultiSheet' lub 'SingleFiles'. Dozwolone: MultiSheet, SingleFiles."
                                }
                            },
                            {
                                "IncludeModel", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy uwzglednic Model w publikacji (domyslnie false)."
                                }
                            },
                            {
                                "OverwriteIfExists", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy nadpisac istniejace pliki (domyslnie false)."
                                }
                            }
                        },
                        Required = new List<string> { "OutputPdfPath", "Mode" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            JArray layoutNamesToken = args["LayoutNames"] as JArray;
            string outputPath = args["OutputPdfPath"]?.ToString() ?? "";
            string mode = args["Mode"]?.ToString() ?? "MultiSheet";
            bool includeModel = false;
            bool overwrite = false;

            if (args["IncludeModel"] != null)
                try { includeModel = args["IncludeModel"].Value<bool>(); } catch { }
            if (args["OverwriteIfExists"] != null)
                try { overwrite = args["OverwriteIfExists"].Value<bool>(); } catch { }

            if (string.IsNullOrWhiteSpace(outputPath))
                return "BLAD: OutputPdfPath jest wymagany.";

            if (Array.IndexOf(ValidModes, mode) < 0)
            {
                return $"BLAD: Nieobslugiwany tryb '{mode}'. Dozwolone: {string.Join(", ", ValidModes)}.";
            }

            string resolvedPath = LayoutHelpers.ValidateAndResolvePath(outputPath);

            Database db = doc.Database;
            Database oldDb = HostApplicationServices.WorkingDatabase;
            HostApplicationServices.WorkingDatabase = db;

            try
            {
                List<string> layoutsToProcess = new List<string>();

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    if (layoutNamesToken != null && layoutNamesToken.Count > 0)
                    {
                        foreach (var item in layoutNamesToken)
                        {
                            string name = item.ToString();
                            if (!LayoutHelpers.LayoutExists(db, name, tr))
                            {
                                return $"BLAD: Layout '{name}' nie istnieje.";
                            }
                            if (!includeModel && name.Equals(LayoutHelpers.ModelLayoutName, StringComparison.OrdinalIgnoreCase))
                                continue;
                            layoutsToProcess.Add(name);
                        }
                    }
                    else
                    {
                        foreach (string name in LayoutHelpers.ListLayoutNames(db, tr))
                        {
                            if (!includeModel && name.Equals(LayoutHelpers.ModelLayoutName, StringComparison.OrdinalIgnoreCase))
                                continue;
                            layoutsToProcess.Add(name);
                        }
                    }

                    tr.Commit();
                }

                if (layoutsToProcess.Count == 0)
                {
                    return "BLAD: Brak layoutow do opublikowania.";
                }

                if (mode.Equals("MultiSheet", StringComparison.OrdinalIgnoreCase) &&
                    layoutsToProcess.Count > MaxLayoutsInMultiSheet)
                {
                    return $"BLAD: Tryb MultiSheet obsluguje maksymalnie {MaxLayoutsInMultiSheet} layoutow. Znaleziono: {layoutsToProcess.Count}. Uzyj trybu 'SingleFiles' lub ogranicz liste.";
                }

                if (mode.Equals("MultiSheet", StringComparison.OrdinalIgnoreCase))
                {
                    return PublishMultiSheet(doc, layoutsToProcess, resolvedPath, overwrite);
                }
                else
                {
                    return PublishSingleFiles(doc, layoutsToProcess, resolvedPath, overwrite);
                }
            }
            catch (Exception ex)
            {
                return $"BLAD KRYTYCZNY PUBLISH: {ex.Message}";
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = oldDb;
            }
        }

        private string PublishMultiSheet(Document doc, List<string> layouts, string outputPdfPath, bool overwrite)
        {
            if (File.Exists(outputPdfPath) && !overwrite)
            {
                return $"BLAD: Plik '{outputPdfPath}' juz istnieje. Ustaw OverwriteIfExists=true.";
            }

            try
            {
                string safePath = outputPdfPath.Replace("\\", "/").Replace("\"", "\\\"");
                string layoutList = string.Join(",", layouts);
                string command = $"-PUBLISH\n\"{safePath}\"\n";
                doc.SendStringToExecute(command, true, false, false);

                return $"SUKCES: Zlecono publikacje MultiSheet ({layouts.Count} layoutow: {layoutList}) do '{outputPdfPath}'. Plik zostanie wygenerowany przez BricsCAD w tle.";
            }
            catch (Exception ex)
            {
                return $"BLAD MULTISHEET PUBLISH: {ex.Message}";
            }
        }

        private string PublishSingleFiles(Document doc, List<string> layouts, string outputPath, bool overwrite)
        {
            try
            {
                string dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string prefix = Path.GetFileNameWithoutExtension(outputPath);
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    prefix = Path.GetFileNameWithoutExtension(doc.Name) ?? "layout";
                }

                string baseDir = Path.GetDirectoryName(outputPath) ?? "";
                int successCount = 0;
                var warnings = new List<string>();

                foreach (string layoutName in layouts)
                {
                    try
                    {
                        string fileName = $"{prefix}_{SanitizeFileName(layoutName)}.pdf";
                        string fullPath = Path.Combine(baseDir, fileName);

                        if (File.Exists(fullPath) && !overwrite)
                        {
                            warnings.Add($"{layoutName}: plik '{fullPath}' juz istnieje (pominiento)");
                            continue;
                        }

                        LayoutManager.Current.CurrentLayout = layoutName;

                        string safePath = fullPath.Replace("\\", "/").Replace("\"", "\\\"");
                        string command = $"-PLOT\n\"\"\n\"\"\n\"{safePath}\"\n";
                        doc.SendStringToExecute(command, true, false, false);

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"{layoutName}: {ex.Message}");
                    }
                }

                string msg = $"SUKCES: Zlecono {successCount}/{layouts.Count} layout(ow) do SingleFiles w '{baseDir}'.";
                if (warnings.Count > 0)
                {
                    msg += $" Ostrzezenia: {string.Join(" | ", warnings)}";
                }
                return msg;
            }
            catch (Exception ex)
            {
                return $"BLAD SINGLEFILES PUBLISH: {ex.Message}";
            }
        }

        private string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "unnamed";
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }
            return name;
        }

        public List<string> Examples => new List<string>
        {
            "{ \"Mode\": \"MultiSheet\", \"OutputPdfPath\": \"C:/export/projekt.pdf\" }",
            "{ \"Mode\": \"MultiSheet\", \"OutputPdfPath\": \"C:/export/projekt.pdf\", \"LayoutNames\": [\"A4-PION-1\", \"A4-PION-2\"] }",
            "{ \"Mode\": \"SingleFiles\", \"OutputPdfPath\": \"C:/export/arkusze\" }"
        };
    }
}