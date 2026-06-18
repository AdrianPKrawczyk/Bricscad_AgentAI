using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using CadLayout = Teigha.DatabaseServices.Layout;

namespace Bricscad_AgentAI_V2.Tools.Layout
{
    public class PageSetupTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            var props = new Dictionary<string, ToolParameter>
            {
                { "LayoutName", new ToolParameter { Type = "string", Description = "Nazwa layoutu (opcjonalny; domyslnie biezacy)." } },
                { "PlotDevice", new ToolParameter { Type = "string", Description = "Nazwa urzadzenia drukujacego (np. 'DWG To PDF.pc3')." } },
                { "MediaName", new ToolParameter { Type = "string", Description = "Nazwa formatu papieru (np. 'ISO_A4_(210.00_x_297.00_MM)')." } },
                { "StyleSheet", new ToolParameter { Type = "string", Description = "Nazwa stylu wydruku (np. 'acad.ctb')." } },
                { "PlotType", new ToolParameter { Type = "string", Description = $"Typ obszaru. Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.PlotTypeMap)}." } },
                { "PlotRotation", new ToolParameter { Type = "string", Description = $"Obrot. Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.PlotRotationMap)}." } },
                { "PlotCentered", new ToolParameter { Type = "boolean", Description = "Czy wycentrowac na stronie." } },
                { "PlotOriginX", new ToolParameter { Type = "number", Description = "Przesuniecie X w mm (lub wg PlotPaperUnits)." } },
                { "PlotOriginY", new ToolParameter { Type = "number", Description = "Przesuniecie Y w mm (lub wg PlotPaperUnits)." } },
                { "UseStandardScale", new ToolParameter { Type = "boolean", Description = "Czy uzyc skali standardowej (true) czy wlasnej (false)." } },
                { "StdScaleType", new ToolParameter { Type = "string", Description = $"Typ skali standardowej. Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.StdScaleTypeMap)}." } },
                { "CustomScaleNumerator", new ToolParameter { Type = "number", Description = "Licznik skali wlasnej (np. 1 dla 1:50)." } },
                { "CustomScaleDenominator", new ToolParameter { Type = "number", Description = "Mianownik skali wlasnej (np. 50 dla 1:50)." } },
                { "PlotPaperUnits", new ToolParameter { Type = "string", Description = $"Jednostki papieru. Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.PlotPaperUnitsMap)}." } },
                { "ShadePlot", new ToolParameter { Type = "string", Description = $"Tryb shade plot. Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.ShadePlotMap)}." } },
                { "ShadePlotResLevel", new ToolParameter { Type = "string", Description = $"Poziom rozdzielczosci shade plot. Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.ShadePlotResLevelMap)}." } },
                { "ShadePlotCustomDpi", new ToolParameter { Type = "integer", Description = "DPI dla shade plot custom." } },
                { "PlotHidden", new ToolParameter { Type = "boolean", Description = "Czy drukowac ukryte linie." } },
                { "PlotViewportBorders", new ToolParameter { Type = "boolean", Description = "Czy drukowac ramki viewportow." } },
                { "PlotPlotStyles", new ToolParameter { Type = "boolean", Description = "Czy uzyc plot styles (CTB/STB)." } },
                { "PrintLineweights", new ToolParameter { Type = "boolean", Description = "Czy drukowac grubosci linii." } },
                { "ScaleLineweights", new ToolParameter { Type = "boolean", Description = "Czy skalowac grubosci linii." } },
                { "PlotTransparency", new ToolParameter { Type = "boolean", Description = "Czy uwzgledniac przezroczystosc." } },
                { "PlotWireframe", new ToolParameter { Type = "boolean", Description = "Czy drukowac tylko wireframe." } },
                { "DrawViewportsFirst", new ToolParameter { Type = "boolean", Description = "Czy rysowac viewporty w pierwszej kolejnosci." } },
                { "ShowPlotStyles", new ToolParameter { Type = "boolean", Description = "Czy pokazywac style na ekranie." } },
                { "PlotAsRaster", new ToolParameter { Type = "boolean", Description = "Czy plotowac jako raster." } }
            };

            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "PageSetupTool",
                    Description = "Konfiguruje ustawienia strony (Page Setup) dla wskazanego lub biezacego layoutu: format papieru, urzadzenie, styl, skala, obrot, opcje plotowania.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = props
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string layoutName = args["LayoutName"]?.ToString() ?? "";
            Database db = doc.Database;

            Database oldDb = HostApplicationServices.WorkingDatabase;
            HostApplicationServices.WorkingDatabase = db;

            try
            {
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
                            return "BLAD: Nie mozna modyfikowac Page Setup layoutu 'Model'.";
                        }

                        layout.UpgradeOpen();

                        var validator = PlotSettingsValidator.Current;
                        using (var workingSettings = new PlotSettings(layout.ModelType))
                        {
                            workingSettings.CopyFrom(layout);

                        var preflightErrors = new List<string>();
                        var preflightWarnings = new List<string>();
                        var infoMessages = new List<string>();
                        if (args.TryGetValue("PlotDevice", out var tokDevPre))
                        {
                            string devName = tokDevPre.ToString();
                            StringCollection devList = validator.GetPlotDeviceList();
                            if (devList != null && devList.Count > 0 && !devList.Contains(devName))
                            {
                                string preview = string.Join(", ", devList.Cast<string>().Take(15));
                                if (devList.Count > 15) preview += ", ...";
                                preflightErrors.Add($"PlotDevice '{devName}' nie istnieje w systemie. Podobne: {preview}");
                            }
                        }
if (args.TryGetValue("MediaName", out var tokMediaPre))
                        {
                            string mediaName = tokMediaPre.ToString();
                            string mediaDevice = args.TryGetValue("PlotDevice", out var tokDevForMedia)
                                ? tokDevForMedia.ToString()
                                : layout.PlotConfigurationName;
                            List<string> mediaList = GetCanonicalMediaNamesForDevice(validator, mediaDevice, out string mediaWarning);
                            if (mediaList.Count > 0 && !mediaList.Contains(mediaName))
                            {
                                string resolved = TryResolveCustomFormatViaWin32(mediaName, mediaDevice);
                                if (!string.IsNullOrEmpty(resolved))
                                {
                                    preflightWarnings.Add($"MediaName '{mediaName}' jest formatem custom (np. '{resolved}'). Plotery HP uzywaja UserXXX wewnetrznie - driver zaakceptuje nazwe '{mediaName}' bez zmian, ale rzeczywisty UserXXX to '{resolved}'.");
                                }
                                else
                                {
                                    string isoMatches = string.Join(", ", mediaList
                                        .Where(m => m.IndexOf("ISO", StringComparison.OrdinalIgnoreCase) >= 0
                                                 || m.IndexOf("A4", StringComparison.OrdinalIgnoreCase) >= 0
                                                 || m.IndexOf("A3", StringComparison.OrdinalIgnoreCase) >= 0
                                                 || m.IndexOf("User", StringComparison.OrdinalIgnoreCase) >= 0)
                                        .Take(20));
                                    if (string.IsNullOrEmpty(isoMatches))
                                    {
                                        isoMatches = string.Join(", ", mediaList.Take(15));
                                        if (mediaList.Count > 15) isoMatches += ", ...";
                                    }
                                    string warn = string.IsNullOrWhiteSpace(mediaWarning) ? "" : $" Ostrzezenie od drivera: {mediaWarning}.";
                                    preflightErrors.Add($"MediaName '{mediaName}' nie istnieje dla plotera '{mediaDevice}'. Plotery HP moga uzywac formatow UserXXX albo nazw driver'a drukarki (np. 'A4', 'B2', 'Tabloid', '594x840') - uzyj ListPlotDevicesTool z Filter='{mediaDevice}' i IncludeMediaPerDevice=true. Dostepne CanonicalMediaName (ISO/A3/A4/User): {isoMatches}.{warn}");
                                }
                            }
                            else if (mediaList.Count == 0 &&
                                     UserMediaResolver.TryParseCustomMediaName(mediaName, out double cw, out double ch, out bool isMult) && cw > 0 && ch > 0)
                            {
                                string gpdWarning = CheckCustomSizeAgainstGpd(mediaDevice, cw, ch);
                                if (!string.IsNullOrEmpty(gpdWarning))
                                {
                                    preflightErrors.Add(gpdWarning);
                                }
                            }
                        }
                        if (args.TryGetValue("StyleSheet", out var tokStylePre))
                        {
                            string styleName = tokStylePre.ToString();
                            StringCollection styleList = validator.GetPlotStyleSheetList();
                            if (styleList != null && styleList.Count > 0 && !styleList.Contains(styleName))
                            {
                                string preview = string.Join(", ", styleList.Cast<string>().Take(15));
                                if (styleList.Count > 15) preview += ", ...";
                                preflightErrors.Add($"StyleSheet '{styleName}' nie jest zaladowany. Dostepne: {preview}. Uzyj PlotStyleTool z Action='Load' aby wczytac plik.");
                            }
                        }

                        if (preflightErrors.Count > 0)
                        {
                            tr.Abort();
                            return "BLAD KRYTYCZNY PAGE SETUP (preflight): " + string.Join(" | ", preflightErrors);
                        }

                        var warnings = new List<string>();
                        if (preflightWarnings.Count > 0)
                        {
                            warnings.AddRange(preflightWarnings);
                        }

                        int applied = 0;

                        if (args.TryGetValue("PlotDevice", out var tokDevice))
                        {
                            try
                            {
                                validator.SetPlotConfigurationName(workingSettings, tokDevice.ToString(), null);
                                applied++;
                            }
                            catch (Exception ex)
                            {
                                warnings.Add($"PlotDevice '{tokDevice}': {ex.Message}");
                            }
                        }

                        if (args.TryGetValue("MediaName", out var tokMedia))
                        {
                            string mediaNameArg = tokMedia.ToString();

                            try
                            {
                                validator.SetCanonicalMediaName(workingSettings, mediaNameArg);
                                applied++;
                            }
                            catch (Exception ex)
                            {
                                if (!mediaNameArg.EndsWith("_p", StringComparison.Ordinal) &&
                                    !mediaNameArg.EndsWith("_a", StringComparison.Ordinal) &&
                                    UserMediaResolver.TryParseCustomMediaName(mediaNameArg, out double fw, out double fh, out _))
                                {
                                    try
                                    {
                                        validator.SetCanonicalMediaName(workingSettings, mediaNameArg + "_p");
                                        applied++;
                                        infoMessages.Add($"INFO: MediaName '{mediaNameArg}' nie zostal zaakceptowany przez driver. Automatycznie uzyto '{mediaNameArg}_p' (sufiks _p = format skladany do A4).");
                                    }
                                    catch (Exception ex2)
                                    {
                                        string currentDevice = args.TryGetValue("PlotDevice", out var tokDevNow)
                                            ? tokDevNow.ToString()
                                            : layout.PlotConfigurationName;
                                        string userMapping = TryResolveCustomFormatViaWin32(mediaNameArg, currentDevice);
                                        string extra = string.IsNullOrEmpty(userMapping)
                                            ? "Lista dostepnych: uzyj ListPlotDevicesTool z Filter='" + currentDevice + "' i IncludeMediaPerDevice=true."
                                            : "Format '" + mediaNameArg + "' wyglada na custom. UserXXX w driver to '" + userMapping + "'. Sprobuj MediaName=\"" + userMapping + "\" albo zostaw nazwe i zweryfikuj wynik w GUI BricsCAD.";
                                        warnings.Add($"MediaName '{mediaNameArg}': {ex.Message}. Automatyczna proba z '{mediaNameArg}_p' tez nie powiodla sie ({ex2.Message}). {extra}");
                                    }
                                }
                                else
                                {
                                    string currentDevice = args.TryGetValue("PlotDevice", out var tokDevNow)
                                        ? tokDevNow.ToString()
                                        : layout.PlotConfigurationName;
                                    string userMapping = TryResolveCustomFormatViaWin32(mediaNameArg, currentDevice);
                                    string extra = string.IsNullOrEmpty(userMapping)
                                        ? "Lista dostepnych: uzyj ListPlotDevicesTool z Filter='" + currentDevice + "' i IncludeMediaPerDevice=true."
                                        : "Format '" + mediaNameArg + "' wyglada na custom. UserXXX w driver to '" + userMapping + "'. Sprobuj MediaName=\"" + userMapping + "\" albo zostaw nazwe i zweryfikuj wynik w GUI BricsCAD.";
                                    warnings.Add($"MediaName '{mediaNameArg}': {ex.Message}. {extra}");
                                }
                            }
                        }

                        if (args.TryGetValue("StyleSheet", out var tokStyle))
                        {
                            try
                            {
                                validator.SetCurrentStyleSheet(workingSettings, tokStyle.ToString());
                                applied++;
                            }
                            catch (Exception ex)
                            {
                                warnings.Add($"StyleSheet '{tokStyle}': {ex.Message}");
                            }
                        }

                        if (args.TryGetValue("PlotType", out var tokType))
                        {
                            if (LayoutEnums.TryParseEnum(tokType.ToString(), LayoutEnums.PlotTypeMap, out int pt))
                            {
                                validator.SetPlotType(workingSettings, (PlotType)pt);
                                applied++;
                            }
                            else
                            {
                                warnings.Add($"PlotType '{tokType}': niedozwolona wartosc. Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.PlotTypeMap)}");
                            }
                        }

                        if (args.TryGetValue("PlotRotation", out var tokRot))
                        {
                            if (LayoutEnums.TryParseEnum(tokRot.ToString(), LayoutEnums.PlotRotationMap, out int pr))
                            {
                                validator.SetPlotRotation(workingSettings, (PlotRotation)pr);
                                applied++;
                            }
                            else
                            {
                                warnings.Add($"PlotRotation '{tokRot}': niedozwolona wartosc. Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.PlotRotationMap)}");
                            }
                        }

                        if (args.TryGetValue("PlotCentered", out var tokCentered))
                        {
                            try
                            {
                                validator.SetPlotCentered(workingSettings, tokCentered.Value<bool>());
                                applied++;
                            }
                            catch (Exception ex)
                            {
                                warnings.Add($"PlotCentered: {ex.Message}");
                            }
                        }

                        bool hasOriginX = args.TryGetValue("PlotOriginX", out var tokOriginX);
                        bool hasOriginY = args.TryGetValue("PlotOriginY", out var tokOriginY);
                        if (hasOriginX || hasOriginY)
                        {
                            double ox = layout.PlotOrigin.X;
                            double oy = layout.PlotOrigin.Y;
                            if (hasOriginX && tokOriginX != null && LayoutEnums.TryParseDouble(tokOriginX.ToString(), out double vx)) ox = vx;
                            if (hasOriginY && tokOriginY != null && LayoutEnums.TryParseDouble(tokOriginY.ToString(), out double vy)) oy = vy;
                            try
                            {
                                validator.SetPlotOrigin(workingSettings, new Point2d(ox, oy));
                                applied++;
                            }
                            catch (Exception ex)
                            {
                                warnings.Add($"PlotOrigin: {ex.Message}");
                            }
                        }

                        if (args.TryGetValue("PlotPaperUnits", out var tokUnits))
                        {
                            if (LayoutEnums.TryParseEnum(tokUnits.ToString(), LayoutEnums.PlotPaperUnitsMap, out int pu))
                            {
                                validator.SetPlotPaperUnits(workingSettings, (PlotPaperUnit)pu);
                                applied++;
                            }
                            else
                            {
                                warnings.Add($"PlotPaperUnits '{tokUnits}': niedozwolona wartosc. Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.PlotPaperUnitsMap)}");
                            }
                        }

                        if (args.TryGetValue("UseStandardScale", out var tokUseStd) &&
                            args.TryGetValue("StdScaleType", out var tokStdScale))
                        {
                            if (LayoutEnums.TryParseEnum(tokStdScale.ToString(), LayoutEnums.StdScaleTypeMap, out int st))
                            {
                                try
                                {
                                    validator.SetUseStandardScale(workingSettings, tokUseStd.Value<bool>());
                                    validator.SetStdScaleType(workingSettings, (StdScaleType)st);
                                    applied++;
                                }
                                catch (Exception ex)
                                {
                                    warnings.Add($"StdScaleType: {ex.Message}");
                                }
                            }
                            else
                            {
                                warnings.Add($"StdScaleType '{tokStdScale}': niedozwolona wartosc. Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.StdScaleTypeMap)}");
                            }
                        }

                        bool hasNum = args.TryGetValue("CustomScaleNumerator", out var tokNum);
                        bool hasDen = args.TryGetValue("CustomScaleDenominator", out var tokDen);
                        if (hasNum || hasDen)
                        {
                            double num = layout.CustomPrintScale.Numerator;
                            double den = layout.CustomPrintScale.Denominator;
                            if (hasNum && tokNum != null && LayoutEnums.TryParseDouble(tokNum.ToString(), out double vn)) num = vn;
                            if (hasDen && tokDen != null && LayoutEnums.TryParseDouble(tokDen.ToString(), out double vd)) den = vd;
                            try
                            {
                                validator.SetCustomPrintScale(workingSettings, new CustomScale(num, den));
                                applied++;
                            }
                            catch (Exception ex)
                            {
                                warnings.Add($"CustomScale: {ex.Message}");
                            }
                        }

                        layout.CopyFrom(workingSettings);

                        if (args.TryGetValue("ShadePlot", out var tokShade))
                        {
                            if (LayoutEnums.TryParseEnum(tokShade.ToString(), LayoutEnums.ShadePlotMap, out int sp))
                            {
                                layout.ShadePlot = (PlotSettingsShadePlotType)sp;
                                applied++;
                            }
                            else
                            {
                                warnings.Add($"ShadePlot '{tokShade}': niedozwolona wartosc. Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.ShadePlotMap)}");
                            }
                        }

                        if (args.TryGetValue("ShadePlotResLevel", out var tokShadeRes))
                        {
                            if (LayoutEnums.TryParseEnum(tokShadeRes.ToString(), LayoutEnums.ShadePlotResLevelMap, out int srl))
                            {
                                layout.ShadePlotResLevel = (ShadePlotResLevel)srl;
                                applied++;
                            }
                            else
                            {
                                warnings.Add($"ShadePlotResLevel '{tokShadeRes}': niedozwolona wartosc. Dozwolone: {LayoutEnums.FormatAllowed(LayoutEnums.ShadePlotResLevelMap)}");
                            }
                        }

                        if (args.TryGetValue("ShadePlotCustomDpi", out var tokDpi))
                        {
                            try
                            {
                                layout.ShadePlotCustomDpi = (short)tokDpi.Value<int>();
                                applied++;
                            }
                            catch (Exception ex)
                            {
                                warnings.Add($"ShadePlotCustomDpi: {ex.Message}");
                            }
                        }

                        if (args.TryGetValue("PlotHidden", out var tokHidden))
                        {
                            layout.PlotHidden = tokHidden.Value<bool>();
                            applied++;
                        }
                        if (args.TryGetValue("PlotViewportBorders", out var tokVpb))
                        {
                            layout.PlotViewportBorders = tokVpb.Value<bool>();
                            applied++;
                        }
                        if (args.TryGetValue("PlotPlotStyles", out var tokPps))
                        {
                            layout.PlotPlotStyles = tokPps.Value<bool>();
                            applied++;
                        }
                        if (args.TryGetValue("PrintLineweights", out var tokPlw))
                        {
                            layout.PrintLineweights = tokPlw.Value<bool>();
                            applied++;
                        }
                        if (args.TryGetValue("ScaleLineweights", out var tokSlw))
                        {
                            layout.ScaleLineweights = tokSlw.Value<bool>();
                            applied++;
                        }
                        if (args.TryGetValue("PlotTransparency", out var tokTr))
                        {
                            layout.PlotTransparency = tokTr.Value<bool>();
                            applied++;
                        }
                        if (args.TryGetValue("PlotWireframe", out var tokWire))
                        {
                            warnings.Add("PlotWireframe jest tylko do odczytu w BricsCAD V22 (pominiento).");
                        }
                        if (args.TryGetValue("DrawViewportsFirst", out var tokDvf))
                        {
                            layout.DrawViewportsFirst = tokDvf.Value<bool>();
                            applied++;
                        }
                        if (args.TryGetValue("ShowPlotStyles", out var tokSps))
                        {
                            layout.ShowPlotStyles = tokSps.Value<bool>();
                            applied++;
                        }
                        if (args.TryGetValue("PlotAsRaster", out var tokPar))
                        {
                            warnings.Add("PlotAsRaster jest tylko do odczytu w BricsCAD V22 (pominiento).");
                        }

                        if (warnings.Count > 0)
                        {
                            tr.Abort();
                            return "BLAD CZESCIOWY PAGE SETUP: " + string.Join(" | ", warnings) + " | Nic nie zostalo zapisane (transakcja wycofana). Uzyj ListLayoutsTool z LayoutName aby sprawdzic aktualny stan lub PlotStyleTool z Action='List' aby zobaczyc dostepne style.";
                        }

                        tr.Commit();

                        string result = $"SUKCES: Zastosowano {applied} ustawien Page Setup dla layoutu '{layout.LayoutName}'.";
                        if (infoMessages.Count > 0)
                        {
                            result += " | " + string.Join(" | ", infoMessages);
                        }
                        return result;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return $"BLAD KRYTYCZNY PAGE SETUP: {ex.Message}";
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = oldDb;
            }
        }

        public List<string> Examples => new List<string>
        {
            "{ \"LayoutName\": \"A4-PION\", \"MediaName\": \"ISO_A4_(210.00_x_297.00_MM)\", \"PlotDevice\": \"DWG To PDF.pc3\", \"PlotType\": \"Layout\", \"PlotRotation\": \"Zero\", \"UseStandardScale\": true, \"StdScaleType\": \"1_50\" }",
            "{ \"StyleSheet\": \"acad.ctb\", \"PlotCentered\": true, \"PlotPlotStyles\": true }"
        };

private static List<string> GetCanonicalMediaNamesForDevice(PlotSettingsValidator validator, string deviceName, out string warning)
        {
            warning = null;
            var result = new List<string>();
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
            return result;
        }

        private static string TryResolveCustomFormatViaWin32(string mediaName, string mediaDevice)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(mediaName)) return null;
                if (!UserMediaResolver.TryParseCustomMediaName(mediaName, out double w, out double h, out _))
                    return null;

                if (h > 1000.0 || w > 1000.0)
                {
                    return null;
                }

                var caps = Win32PrinterCapabilities.QueryMedia(mediaDevice);
                if (caps == null || !caps.QuerySucceeded) return null;

                var match = UserMediaResolver.ResolveCustomNameToUserFormat(mediaName, caps);
                return match?.UserFormName;
            }
            catch
            {
                return null;
            }
        }

        private static string CheckCustomSizeAgainstGpd(string deviceName, double widthMm, double heightMm)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(deviceName)) return null;

                string gpdPath = GpdParser.FindHpGpdForDevice(deviceName, null, null);
                if (string.IsNullOrEmpty(gpdPath)) return null;

                var gpd = GpdParser.Parse(gpdPath);
                if (gpd == null || !gpd.ParseSucceeded || gpd.CustomSize == null) return null;

                if (!UserMediaResolver.IsWithinGpdBounds(widthMm, heightMm, gpd))
                {
                    return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "Custom MediaName '{0}x{1}mm' przekracza zakres GPD dla '{2}': {3:F1}x{4:F1}mm min, {5:F1}x{6:F1}mm max. Driver odmowi lub obetnie arkusz.",
                        widthMm, heightMm, deviceName,
                        gpd.CustomSize.MinWidthMm, gpd.CustomSize.MinHeightMm,
                        gpd.CustomSize.MaxWidthMm, gpd.CustomSize.MaxHeightMm);
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
