using System;
using System.Collections.Generic;
using System.IO;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using CadLayout = Teigha.DatabaseServices.Layout;

namespace Bricscad_AgentAI_V2.Tools.Layout
{
    public class PlotLayoutTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "PlotLayoutTool",
                    Description = "Drukuje wybrany layout do PDF/DWF. Konfiguruje urzadzenie 'DWG To PDF' lub 'DWF6 ePlot' i uruchamia plotowanie.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "LayoutName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa layoutu (opcjonalny; domyslnie biezacy)."
                                }
                            },
                            {
                                "OutputPath", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Sciezka docelowa PDF (obowiazkowe). Jesli brak rozszerzenia, zostanie dodane .pdf."
                                }
                            },
                            {
                                "OutputFormat", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Format wyjsciowy: 'PDF' (DWG To PDF), 'DWF' (DWF6 ePlot), 'PNG' (PNG). Domyslnie PDF."
                                }
                            },
                            {
                                "ScaleToFit", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy dopasowac skale do formatu (true/false, domyslnie true)."
                                }
                            },
                            {
                                "CenterOnPage", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy wycentrowac na stronie (true/false, domyslnie false)."
                                }
                            },
                            {
                                "PlotWithStyles", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy drukowac z plot styles (true/false, domyslnie true)."
                                }
                            },
                            {
                                "ShadePlot", new ToolParameter
                                {
                                    Type = "string",
                                    Description = $"Tryb shade plot. Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.ShadePlotMap)}."
                                }
                            },
                            {
                                "OverwriteIfExists", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy nadpisac istniejacy plik (domyslnie false)."
                                }
                            }
                        },
                        Required = new List<string> { "OutputPath" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string layoutName = args["LayoutName"]?.ToString() ?? "";
            string outputPath = args["OutputPath"]?.ToString() ?? "";
            string outputFormat = (args["OutputFormat"]?.ToString() ?? "PDF").ToUpperInvariant();
            bool scaleToFit = true;
            bool centerOnPage = false;
            bool plotWithStyles = true;
            bool overwrite = false;

            if (args["ScaleToFit"] != null)
                try { scaleToFit = args["ScaleToFit"].Value<bool>(); } catch { }
            if (args["CenterOnPage"] != null)
                try { centerOnPage = args["CenterOnPage"].Value<bool>(); } catch { }
            if (args["PlotWithStyles"] != null)
                try { plotWithStyles = args["PlotWithStyles"].Value<bool>(); } catch { }
            if (args["OverwriteIfExists"] != null)
                try { overwrite = args["OverwriteIfExists"].Value<bool>(); } catch { }

            if (string.IsNullOrWhiteSpace(outputPath))
                return "BLAD: OutputPath jest wymagany.";

            string resolvedPath = LayoutHelpers.ValidateAndResolvePath(outputPath);

            string ext = Path.GetExtension(resolvedPath).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext))
            {
                switch (outputFormat)
                {
                    case "PDF": resolvedPath += ".pdf"; break;
                    case "DWF": resolvedPath += ".dwf"; break;
                    case "PNG": resolvedPath += ".png"; break;
                    default: resolvedPath += ".pdf"; break;
                }
            }

            if (File.Exists(resolvedPath) && !overwrite)
            {
                return $"BLAD: Plik '{resolvedPath}' juz istnieje. Ustaw OverwriteIfExists=true.";
            }

            string dir = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }

            Database db = doc.Database;
            Database oldDb = HostApplicationServices.WorkingDatabase;
            HostApplicationServices.WorkingDatabase = db;

            try
            {
                string pc3 = GetPdfDeviceName();
                switch (outputFormat)
                {
                    case "DWF": pc3 = "DWF6 ePlot.pc3"; break;
                    case "PNG": pc3 = "PublishToWeb PNG.pc3"; break;
                    default: break; // Używa GetPdfDeviceName()
                }

                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        CadLayout layout = LayoutHelpers.GetLayoutByNameOrCurrent(db, tr, layoutName);
                        if (layout == null)
                        {
                            return $"BLAD: Layout '{(string.IsNullOrEmpty(layoutName) ? "<biezacy>" : layoutName)}' nie istnieje.";
                        }

                        if (LayoutHelpers.IsModelLayout(layout))
                        {
                            return "BLAD: Nie mozna plotowac layoutu 'Model'. Uzyj PlotLayoutTool z konkretna nazwa arkusza.";
                        }

                        layout.UpgradeOpen();

                        string extOut;
                        switch (outputFormat)
                        {
                            case "DWF":
                                extOut = ".dwf";
                                break;
                            case "PNG":
                                extOut = ".png";
                                break;
                            default:
                                extOut = ".pdf";
                                break;
                        }

                        try
                        {
                            PlotSettingsValidator.Current.SetPlotConfigurationName(layout, pc3, null);
                            PlotSettingsValidator.Current.SetCanonicalMediaName(layout, layout.CanonicalMediaName ?? "ISO_A4_(210.00_x_297.00_MM)");
                        }
                        catch (Exception ex)
                        {
                            return $"BLAD KONFIGURACJI PLOT DEVICE: {ex.Message}. Sprawdz czy sterownik '{pc3}' jest zainstalowany.";
                        }

                        if (scaleToFit)
                        {
                            try
                            {
                                PlotSettingsValidator.Current.SetUseStandardScale(layout, true);
                                PlotSettingsValidator.Current.SetStdScaleType(layout, StdScaleType.ScaleToFit);
                            }
                            catch (Exception ex)
                            {
                                return $"BLAD KONFIGURACJI SKALI: {ex.Message}";
                            }
                        }

                        if (centerOnPage)
                        {
                            try
                            {
                                PlotSettingsValidator.Current.SetPlotCentered(layout, true);
                            }
                            catch { }
                        }

                        try
                        {
                            layout.PlotPlotStyles = plotWithStyles;
                        }
                        catch { }

                        if (args.TryGetValue("ShadePlot", out var tokShade))
                        {
                            if (LayoutEnums.TryParseEnum(tokShade.ToString(), LayoutEnums.ShadePlotMap, out int sp))
                            {
                                layout.ShadePlot = (PlotSettingsShadePlotType)sp;
                            }
                        }

                        try { string _ = layout.PlotConfigurationName; } catch { }

                        tr.Commit();
                    }

                    // Usunięto synchroniczne wywołanie LayoutManager.CurrentLayout
                }

                string safePath = resolvedPath.Replace("\\", "/").Replace("\"", "\\\"");
                string command = "";
                if (!string.IsNullOrEmpty(layoutName))
                {
                    command += $"CTAB\n{layoutName}\n";
                }
                
                // _.-PLOT -> NIE dla szczegółowej -> Enter (layout) -> Enter (page setup) -> Nazwa urządzenia -> Plik -> Zapisz (Nie) -> Kontynuuj (Tak)
                command += $"_.-PLOT\n_No\n\n\n{pc3}\n\"{safePath}\"\n_No\n_Yes\n";
                doc.SendStringToExecute(command, true, false, false);

                return $"SUKCES: Zlecono plotowanie do '{resolvedPath}'. Format: {outputFormat}, Layout: '{(string.IsNullOrEmpty(layoutName) ? "<biezacy>" : layoutName)}'. Plik powinien zostac wygenerowany przez BricsCAD w tle.";
            }
            catch (Exception ex)
            {
                return $"BLAD PLOTOWANIA: {ex.Message}";
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = oldDb;
            }
        }

        private string GetPdfDeviceName()
        {
            try
            {
                var devices = PlotSettingsValidator.Current.GetPlotDeviceList();
                if (devices != null)
                {
                    foreach (string dev in devices)
                    {
                        if (dev.Equals("Print As PDF.pc3", StringComparison.OrdinalIgnoreCase)) return dev;
                    }
                    foreach (string dev in devices)
                    {
                        if (dev.Equals("DWG To PDF.pc3", StringComparison.OrdinalIgnoreCase)) return dev;
                    }
                    foreach (string dev in devices)
                    {
                        if (dev.IndexOf("PDF", StringComparison.OrdinalIgnoreCase) >= 0 && dev.EndsWith(".pc3", StringComparison.OrdinalIgnoreCase)) return dev;
                    }
                }
            }
            catch { }
            return "Print As PDF.pc3";
        }

        public List<string> Examples => new List<string>
        {
            "{ \"LayoutName\": \"A4-PION\", \"OutputPath\": \"C:/export/projekt.pdf\" }",
            "{ \"LayoutName\": \"A4-PION\", \"OutputPath\": \"C:/export/projekt\", \"OutputFormat\": \"PDF\" }",
            "{ \"OutputPath\": \"C:/export/plik\", \"OutputFormat\": \"DWF\", \"ScaleToFit\": true, \"PlotWithStyles\": false }"
        };
    }
}