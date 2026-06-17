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

                        var preflightErrors = new List<string>();
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
                            StringCollection mediaList = validator.GetCanonicalMediaNameList(new PlotSettings(false));
                            if (mediaList != null && mediaList.Count > 0 && !mediaList.Contains(mediaName))
                            {
                                string isoMatches = string.Join(", ", mediaList.Cast<string>()
                                    .Where(m => m.IndexOf("ISO", StringComparison.OrdinalIgnoreCase) >= 0
                                             || m.IndexOf("A4", StringComparison.OrdinalIgnoreCase) >= 0
                                             || m.IndexOf("A3", StringComparison.OrdinalIgnoreCase) >= 0)
                                    .Take(20));
                                if (string.IsNullOrEmpty(isoMatches))
                                {
                                    isoMatches = string.Join(", ", mediaList.Cast<string>().Take(15));
                                    if (mediaList.Count > 15) isoMatches += ", ...";
                                }
                                preflightErrors.Add($"MediaName '{mediaName}' nie istnieje w systemie. Dostepne formaty (filtrowane ISO/A3/A4): {isoMatches}. Mozliwe ze format ma inna nazwe - sprawdz PlotConfigurationName.");
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
                        int applied = 0;

                        if (args.TryGetValue("PlotDevice", out var tokDevice))
                        {
                            try
                            {
                                validator.SetPlotConfigurationName(layout, tokDevice.ToString(), null);
                                applied++;
                            }
                            catch (Exception ex)
                            {
                                warnings.Add($"PlotDevice '{tokDevice}': {ex.Message}");
                            }
                        }

                        if (args.TryGetValue("MediaName", out var tokMedia))
                        {
                            try
                            {
                                validator.SetCanonicalMediaName(layout, tokMedia.ToString());
                                applied++;
                            }
                            catch (Exception ex)
                            {
                                warnings.Add($"MediaName '{tokMedia}': {ex.Message}");
                            }
                        }

                        if (args.TryGetValue("StyleSheet", out var tokStyle))
                        {
                            try
                            {
                                validator.SetCurrentStyleSheet(layout, tokStyle.ToString());
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
                                validator.SetPlotType(layout, (PlotType)pt);
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
                                validator.SetPlotRotation(layout, (PlotRotation)pr);
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
                                validator.SetPlotCentered(layout, tokCentered.Value<bool>());
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
                                validator.SetPlotOrigin(layout, new Point2d(ox, oy));
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
                                validator.SetPlotPaperUnits(layout, (PlotPaperUnit)pu);
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
                                    validator.SetUseStandardScale(layout, tokUseStd.Value<bool>());
                                    validator.SetStdScaleType(layout, (StdScaleType)st);
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
                                validator.SetCustomPrintScale(layout, new CustomScale(num, den));
                                applied++;
                            }
                            catch (Exception ex)
                            {
                                warnings.Add($"CustomScale: {ex.Message}");
                            }
                        }

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

                        return $"SUKCES: Zastosowano {applied} ustawien Page Setup dla layoutu '{layout.LayoutName}'.";
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
    }
}