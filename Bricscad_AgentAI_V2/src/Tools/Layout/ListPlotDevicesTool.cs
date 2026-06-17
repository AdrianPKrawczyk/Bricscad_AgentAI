using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools.Layout
{
    public class ListPlotDevicesTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ListPlotDevicesTool",
                    Description = "Zwraca liste dostepnych w systemie urzadzen drukujacych (ploterow/ploterow PDF) oraz liste wspieranych formatow papieru (CanonicalMediaName) dla aktywnego rysunku. Plotery HP moga uzywac formatow UserXXX albo nazw driver'a (np. 'A4', 'B2', 'Tabloid') - sprawdz to przed ustawianiem MediaName w PageSetupTool.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Filter", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalny filtr (case-insensitive). Np. 'HP', 'PDF', 'DWF'. Pusty = wszystkie urzadzenia."
                                }
                            },
                            {
                                "IncludeCanonicalMediaNames", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy dolaczyc liste CanonicalMediaName (true/false, domyslnie true). Lista moze byc dluga dla drukarek z wieloma formatami."
                                }
                            },
                            {
                                "IncludeMediaPerDevice", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy dla pasujacych urzadzen pobrac formaty papieru po ustawieniu konkretnego plotera (wazne dla HP/UserXXX). Domyslnie true, gdy podano filtr."
                                }
                            },
                            {
                                "SaveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna nazwa zmiennej do zapisu w pamieci Agenta (bez @)."
                                }
                            }
                        }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string filter = args["Filter"]?.ToString();
            bool includeMedia = true;
            if (args["IncludeCanonicalMediaNames"] != null)
            {
                try { includeMedia = args["IncludeCanonicalMediaNames"].Value<bool>(); } catch { }
            }
            bool includeMediaPerDevice = !string.IsNullOrWhiteSpace(filter);
            if (args["IncludeMediaPerDevice"] != null)
            {
                try { includeMediaPerDevice = args["IncludeMediaPerDevice"].Value<bool>(); } catch { }
            }
            string saveAs = args["SaveAs"]?.ToString();

            Database db = doc.Database;
            Database oldDb = HostApplicationServices.WorkingDatabase;
            HostApplicationServices.WorkingDatabase = db;

            try
            {
                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        var validator = PlotSettingsValidator.Current;

                        StringCollection deviceList = null;
                        try { deviceList = validator.GetPlotDeviceList(); } catch { }
                        if (deviceList == null) deviceList = new StringCollection();

                        var sb = new StringBuilder();
                        sb.AppendLine($"LISTA URZADZEN DRUKUJACYCH ({deviceList.Count}):");

                        var matchedDevices = new List<string>();
                        foreach (string dev in deviceList)
                        {
                            if (string.IsNullOrEmpty(dev)) continue;
                            if (!string.IsNullOrEmpty(filter) &&
                                dev.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                            matchedDevices.Add(dev);
                            sb.AppendLine($"  - {dev}");
                        }

                        if (matchedDevices.Count == 0)
                        {
                            if (!string.IsNullOrEmpty(filter))
                            {
                                sb.AppendLine();
                                sb.AppendLine($"Brak urzadzen pasujacych do filtra '{filter}'.");
                            }
                        }

                        if (includeMedia)
                        {
                            sb.AppendLine();
                            if (includeMediaPerDevice && matchedDevices.Count > 0)
                            {
                                sb.AppendLine("LISTA FORMATOW PAPIERU DLA PASUJACYCH URZADZEN:");
                                int devicesShown = 0;
                                foreach (string dev in matchedDevices)
                                {
                                    if (devicesShown >= 8)
                                    {
                                        sb.AppendLine($"  ... pominieto {matchedDevices.Count - devicesShown} kolejnych urzadzen (zawez filtr).");
                                        break;
                                    }

                                    string mediaWarning;
                                    List<string> mediaNames = GetCanonicalMediaNamesForDevice(validator, dev, out mediaWarning);
                                    sb.AppendLine($"  [{dev}] CanonicalMediaName ({mediaNames.Count}):");
                                    if (!string.IsNullOrWhiteSpace(mediaWarning))
                                    {
                                        sb.AppendLine($"    UWAGA: {mediaWarning}");
                                    }

                                    if (mediaNames.Count == 0 &&
                                        dev.EndsWith(".pc3", StringComparison.OrdinalIgnoreCase))
                                    {
                                        AppendPc3Diagnostic(sb, dev);
                                    }

                                    int mediaShown = 0;
                                    foreach (string media in mediaNames)
                                    {
                                        if (string.IsNullOrEmpty(media)) continue;
                                        string locale = TryGetLocaleMediaName(validator, dev, media);
                                        if (!string.IsNullOrWhiteSpace(locale) && !string.Equals(locale, media, StringComparison.OrdinalIgnoreCase))
                                        {
                                            sb.AppendLine($"    - {media} | DriverName: {locale}");
                                        }
                                        else
                                        {
                                            sb.AppendLine($"    - {media}");
                                        }

                                        mediaShown++;
                                        if (mediaShown > 250)
                                        {
                                            sb.AppendLine($"    ... i {mediaNames.Count - mediaShown} wiecej.");
                                            break;
                                        }
                                    }
                                    devicesShown++;
                                }
                            }
                            else
                            {
                                List<string> mediaList = GetCanonicalMediaNamesForDevice(validator, null, out string mediaWarning);
                                sb.AppendLine($"LISTA FORMATOW PAPIERU GLOBALNA (CanonicalMediaName, {mediaList.Count}):");
                                if (!string.IsNullOrWhiteSpace(mediaWarning))
                                {
                                    sb.AppendLine($"  UWAGA: {mediaWarning}");
                                }

                                int mediaShown = 0;
                                foreach (string media in mediaList)
                                {
                                    if (string.IsNullOrEmpty(media)) continue;
                                    sb.AppendLine($"  - {media}");
                                    mediaShown++;
                                    if (mediaShown > 200)
                                    {
                                        sb.AppendLine($"  ... i {mediaList.Count - mediaShown} wiecej (uzyj IncludeCanonicalMediaNames=false aby ukryc).");
                                        break;
                                    }
                                }
                            }
                        }

                        sb.AppendLine();
                        sb.AppendLine("UWAGA DLA PLOTEROW HP:");
                        sb.AppendLine("- Plotery HP moga raportowac formaty UserXXX (np. User254, User266) ktore sa niestandardowe.");
                        sb.AppendLine("- Niektore plotery uzywaja nazw driver'a zamiast Canonical (np. 'A4', 'B2', 'Tabloid').");
                        sb.AppendLine("- Jesli nazwa z powyzszej listy nie dziala w PageSetupTool, sprawdz GUI BricsCAD: menu Format -> Plotter Setup -> lista 'Papier' (zawiera nazwy driver'a).");

                        string resultStr = sb.ToString();
                        if (!string.IsNullOrEmpty(saveAs))
                        {
                            AgentMemoryState.Variables[saveAs] = resultStr;
                        }
                        return resultStr;
                    }
                }
            }
            catch (Exception ex)
            {
                return $"BLAD LISTOWANIA PLOTEROW: {ex.Message}";
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = oldDb;
            }
        }

        public List<string> Examples => new List<string>
        {
            "{}",
            "{ \"Filter\": \"HP\" }",
            "{ \"Filter\": \"HP DesignJet T120\", \"IncludeMediaPerDevice\": true }",
            "{ \"Filter\": \"PDF\" }",
            "{ \"IncludeCanonicalMediaNames\": false }",
            "{ \"SaveAs\": \"AvailablePlotters\" }"
        };

        private static List<string> GetCanonicalMediaNamesForDevice(PlotSettingsValidator validator, string deviceName, out string warning)
        {
            warning = null;
            var result = new List<string>();

            // UWAGA: Plotery .pc3 czesto maja zarejestrowane media w systemie Windows,
            // ale BricsCAD API GetCanonicalMediaNameList zwraca pusta liste dla PC3
            // (CanonicalMediaName sa glownie dla wbudowanych sterownikow systemowych).
            // Dla PC3 musimy zaufac nazwa z GUI BricsCAD (DriverName).
            bool isPc3 = !string.IsNullOrWhiteSpace(deviceName) && deviceName.EndsWith(".pc3", StringComparison.OrdinalIgnoreCase);

            try
            {
                using (var settings = new PlotSettings(false))
                {
                    if (!string.IsNullOrWhiteSpace(deviceName))
                    {
                        validator.SetPlotConfigurationName(settings, deviceName, null);
                    }
                    validator.RefreshLists(settings);
                    StringCollection media = validator.GetCanonicalMediaNameList(settings);
                    if (media != null)
                    {
                        foreach (string item in media)
                        {
                            if (!string.IsNullOrWhiteSpace(item) && !result.Contains(item))
                            {
                                result.Add(item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                warning = ex.Message;
            }

            if (isPc3 && result.Count == 0)
            {
                warning = "Ten ploter uzywa pliku .pc3 (sterownik zewnetrzny). BricsCAD API nie zwraca listy mediów dla PC3. Uzyj GUI BricsCAD: Format -> Plotter Setup -> lista 'Papier' (zawiera nazwy driver'a jak 'A4', 'B2', 'Tabloid', '594x840'). Albo uzyj ListLayoutsTool z LayoutName aby zobaczyc aktualny MediaName tego layoutu.";
            }

            return result;
        }

        private static void AppendPc3Diagnostic(StringBuilder sb, string pc3DeviceName)
        {
            string pc3Path = ResolvePc3Path(pc3DeviceName);
            if (string.IsNullOrEmpty(pc3Path))
            {
                sb.AppendLine("    Nie znaleziono pliku PC3 dla: " + pc3DeviceName);
                return;
            }

            var pc3Info = Pc3Parser.Parse(pc3Path);
            if (pc3Info == null || !pc3Info.ParseSucceeded)
            {
                sb.AppendLine($"    Nie udalo sie odczytac pliku PC3: {pc3Info?.ParseError ?? "nieznany blad"}");
                return;
            }

            sb.AppendLine("    --- DIAGNOSTYKA PLIKU PC3 (Pc3Parser) ---");
            if (!string.IsNullOrEmpty(pc3Info.FriendlyNetName))
                sb.AppendLine($"    Ploter: {pc3Info.FriendlyNetName}");
            else if (!string.IsNullOrEmpty(pc3Info.WinDriverName))
                sb.AppendLine($"    Ploter: {pc3Info.WinDriverName}");

            if (!string.IsNullOrEmpty(pc3Info.DriverPath))
                sb.AppendLine($"    Sterownik Windows: {pc3Info.DriverPath} (v{pc3Info.DriverVersion ?? "?"})");

            if (pc3Info.SelectedMedia != null && !string.IsNullOrEmpty(pc3Info.SelectedMedia.Name))
            {
                sb.AppendLine($"    Aktualnie wybrany format: {pc3Info.SelectedMedia}");
            }
            else
            {
                sb.AppendLine("    Brak informacji o aktualnie wybranym formacie w PC3.");
            }

            if (pc3Info.Resolution != null)
            {
                sb.AppendLine($"    Rozdzielczosc: {pc3Info.Resolution}");
            }

            string gpdPath = GpdParser.FindHpGpdForDevice(
                pc3DeviceName, pc3Info.FriendlyNetName, pc3Info.WinDriverName);
            if (!string.IsNullOrEmpty(gpdPath))
            {
                var gpdInfo = GpdParser.Parse(gpdPath);
                if (gpdInfo != null && gpdInfo.ParseSucceeded)
                {
                    AppendGpdMediaList(sb, gpdInfo, pc3Info);
                    return;
                }
            }

            sb.AppendLine("    UWAGA: Nie znaleziono pliku GPD (Generic Printer Description) dla tego plotera.");
            sb.AppendLine("          Plik PC3 przechowuje tylko AKTUALNIE WYBRANY format.");
            sb.AppendLine("          Pelna lista formatow jest zdefiniowana binarnie w driverze Windows (.hdi).");
            sb.AppendLine("          Aby ustawic format, uzyj PageSetupTool z nazwa driver'a widoczna w BricsCAD GUI");
            sb.AppendLine("          (menu Format -> Plotter Setup -> lista 'Papier'), np.: 'A4', 'B2', '297x600', 'Tabloid'.");
        }

        private static void AppendGpdMediaList(StringBuilder sb, GpdInfo gpdInfo, Pc3Info pc3Info)
        {
            sb.AppendLine();
            sb.AppendLine($"    --- LISTA MEDIOW Z GPD: {gpdInfo.ModelName ?? Path.GetFileName(gpdInfo.FilePath)} ---");

            var priority = new[] { "A4", "ISOA3", "ISOA2", "ISOA1", "ISOA0" };
            var priorityFormats = gpdInfo.MediaFormats
                .Where(m => Array.IndexOf(priority, m.OptionName) >= 0)
                .OrderBy(m => Array.IndexOf(priority, m.OptionName))
                .ToList();

            if (priorityFormats.Count > 0)
            {
                sb.AppendLine("    Formaty standardowe (priorytetowe A4/A3/A2/A1/A0):");
                foreach (var media in priorityFormats)
                {
                    sb.AppendLine($"      * {media}  | PageSetupTool: MediaName=\"{media.OptionName}\"");
                }
            }

            var bSeries = gpdInfo.MediaFormats
                .Where(m => m.OptionName.StartsWith("ISOB") || m.OptionName.StartsWith("JISB"))
                .ToList();
            if (bSeries.Count > 0)
            {
                sb.AppendLine("    Formaty B-series (ISO B + JIS B):");
                foreach (var media in bSeries)
                {
                    sb.AppendLine($"      * {media}  | PageSetupTool: MediaName=\"{media.OptionName}\"");
                }
            }

            var ansi = gpdInfo.MediaFormats
                .Where(m => m.OptionName == "LETTER" ||
                            m.OptionName == "NorthAmericaTabloid" ||
                            m.OptionName.StartsWith("NorthAmericaCSheet") ||
                            m.OptionName.StartsWith("NorthAmericaDSheet") ||
                            m.OptionName == "NorthAmericaLegal" ||
                            m.OptionName == "11X14" || m.OptionName == "F")
                .ToList();
            if (ansi.Count > 0)
            {
                sb.AppendLine("    Formaty ANSI/Letter:");
                foreach (var media in ansi)
                {
                    sb.AppendLine($"      * {media}  | PageSetupTool: MediaName=\"{media.OptionName}\"");
                }
            }

            var arch = gpdInfo.MediaFormats
                .Where(m => m.OptionName.StartsWith("NorthAmericaArchitecture") ||
                            m.OptionName.StartsWith("Arch"))
                .ToList();
            if (arch.Count > 0)
            {
                sb.AppendLine("    Formaty Architecture:");
                foreach (var media in arch)
                {
                    sb.AppendLine($"      * {media}  | PageSetupTool: MediaName=\"{media.OptionName}\"");
                }
            }

            if (gpdInfo.CustomSize != null)
            {
                sb.AppendLine();
                sb.AppendLine("    CUSTOM SIZE (formaty zdefiniowane przez uzytkownika):");
                sb.AppendLine($"      {gpdInfo.CustomSize}");
                sb.AppendLine("      Format nazwy w PageSetupTool MediaName: \"{szerokosc}x{dlugosc}\" (mm), np.: \"297x600\".");
                sb.AppendLine("      Szerokosc musi miescic sie w zakresie, dlugosc moze byc dowolna.");
                sb.AppendLine("      Opcjonalny suffix \"_p\" dla pelnych wielokrotnosci (np. \"297x1320_p\" = 2x600+120).");
            }

            sb.AppendLine();
            sb.AppendLine($"    RAZEM: {gpdInfo.MediaFormats.Count} formatow (w tym custom).");
            sb.AppendLine("    Uzywaj nazw w PageSetupTool dokladnie tak jak powyzej (np. \"A4\", \"ISOA3\", \"NorthAmericaTabloid\").");
        }

        private static string ResolvePc3Path(string deviceName)
        {
            if (string.IsNullOrWhiteSpace(deviceName)) return null;

            if (System.IO.Path.IsPathRooted(deviceName) && System.IO.File.Exists(deviceName))
            {
                return deviceName;
            }

            string resolved = LayoutHelpers.ValidateAndResolvePath(deviceName);
            if (!string.IsNullOrEmpty(resolved) && System.IO.File.Exists(resolved))
            {
                return resolved;
            }

            string justName = System.IO.Path.GetFileName(deviceName);
            if (string.IsNullOrEmpty(justName)) return null;

            string[] plotConfigDirs;
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                plotConfigDirs = new[]
                {
                    System.IO.Path.Combine(appData, "Bricsys", "BricsCAD", "V22x64", "pl_PL", "PlotConfig"),
                    System.IO.Path.Combine(appData, "Bricsys", "BricsCAD", "V23x64", "pl_PL", "PlotConfig"),
                    System.IO.Path.Combine(localAppData, "Bricsys", "BricsCAD", "V22x64", "pl_PL", "PlotConfig"),
                    System.IO.Path.Combine(localAppData, "Bricsys", "BricsCAD", "V23x64", "pl_PL", "PlotConfig"),
                    System.IO.Path.Combine(programData, "Bricsys", "BricsCAD", "V22 pl_PL", "UserDataCache", "PlotConfig"),
                    System.IO.Path.Combine(programData, "Bricsys", "BricsCAD", "V23 pl_PL", "UserDataCache", "PlotConfig")
                };
            }
            catch
            {
                return null;
            }

            foreach (string dir in plotConfigDirs)
            {
                try
                {
                    string candidate = System.IO.Path.Combine(dir, justName);
                    if (System.IO.File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return null;
        }

        private static string TryGetLocaleMediaName(PlotSettingsValidator validator, string deviceName, string canonicalMediaName)
        {
            try
            {
                MethodInfo method = validator.GetType().GetMethod(
                    "GetLocaleMediaName",
                    new[] { typeof(PlotSettings), typeof(string) });
                if (method == null) return null;

                using (var settings = new PlotSettings(false))
                {
                    if (!string.IsNullOrWhiteSpace(deviceName))
                    {
                        validator.SetPlotConfigurationName(settings, deviceName, null);
                    }
                    validator.RefreshLists(settings);
                    object value = method.Invoke(validator, new object[] { settings, canonicalMediaName });
                    return value?.ToString();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
