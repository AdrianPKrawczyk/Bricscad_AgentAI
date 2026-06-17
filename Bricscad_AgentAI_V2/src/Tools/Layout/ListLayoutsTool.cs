using System;
using System.Collections.Generic;
using System.Text;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using CadLayout = Teigha.DatabaseServices.Layout;

namespace Bricscad_AgentAI_V2.Tools.Layout
{
    public class ListLayoutsTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ListLayoutsTool",
                    Description = "Listuje layouty (arkusze wydruku) w biezacym rysunku. Jesli podano LayoutName, zwraca SZCZEGOLOWE ustawienia Page Setup tylko dla tego layoutu (20+ wlasciwosci). Bez LayoutName zwraca krotka liste wszystkich layoutow z metadanymi.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "LayoutName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna nazwa konkretnego layoutu (np. 'IS.W.01'). Jesli podana, zwraca SZCZEGOLOWE ustawienia Page Setup tylko dla tego layoutu z 20+ wlasciwosciami. Jesli brak - lista wszystkich layoutow."
                                }
                            },
                            {
                                "SaveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna nazwa zmiennej do zapisu wyniku w pamieci Agenta (bez @)."
                                }
                            },
                            {
                                "IncludeModel", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy uwzglednic layout 'Model' w trybie listy wszystkich (domyslnie true). Ignorowane gdy podano LayoutName."
                                }
                            }
                        }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string layoutName = args["LayoutName"]?.ToString();
            string saveAs = args["SaveAs"]?.ToString();
            bool includeModel = true;
            if (args["IncludeModel"] != null)
            {
                try { includeModel = args["IncludeModel"].Value<bool>(); } catch { }
            }

            Database db = doc.Database;
            Database oldDb = HostApplicationServices.WorkingDatabase;
            HostApplicationServices.WorkingDatabase = db;

            try
            {
                using (doc.LockDocument())
                {
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        if (!string.IsNullOrWhiteSpace(layoutName))
                        {
                            return GetDetailedLayoutSettings(tr, layoutName, saveAs);
                        }

                        return GetAllLayoutsSummary(tr, includeModel, saveAs);
                    }
                }
            }
            catch (Exception ex)
            {
                return $"BLAD PODCZAS LISTOWANIA LAYOUTOW: {ex.Message}";
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = oldDb;
            }
        }

        private string GetDetailedLayoutSettings(Transaction tr, string layoutName, string saveAs)
        {
            DBDictionary layoutDict = tr.GetObject(HostApplicationServices.WorkingDatabase.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
            if (layoutDict == null)
            {
                return "BLAD: Nie udalo sie uzyskac dostepu do slownika layoutow.";
            }

            if (!layoutDict.Contains(layoutName))
            {
                return $"BLAD: Layout '{layoutName}' nie istnieje w biezacym rysunku. Uzyj ListLayoutsTool bez LayoutName aby zobaczyc liste dostepnych layoutow.";
            }

            ObjectId layoutId = layoutDict.GetAt(layoutName);
            CadLayout layout = tr.GetObject(layoutId, OpenMode.ForRead) as CadLayout;
            if (layout == null)
            {
                return $"BLAD: Nie udalo sie otworzyc layoutu '{layoutName}'.";
            }

            if (layout.ModelType)
            {
                return $"INFO: '{layoutName}' to layout Model - nie posiada ustawien Page Setup (brak arkusza).";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"SZCZEGOLOWE USTAWIENIA PAGE SETUP dla layoutu '{layoutName}':");
            sb.AppendLine();
            sb.AppendLine($"--- Media i Urzadzenie ---");
            sb.AppendLine($"Papier (CanonicalMediaName): {layout.CanonicalMediaName ?? "(nie ustawiono)"}");
            sb.AppendLine($"Urzadzenie (PlotDevice): {layout.PlotConfigurationName ?? "(nie ustawiono)"}");
            sb.AppendLine($"Rozmiar papieru (PlotPaperSize): {layout.PlotPaperSize}");
            sb.AppendLine($"Marginesy (PlotPaperMargins): {layout.PlotPaperMargins}");
            sb.AppendLine($"Jednostki papieru (PlotPaperUnits): {layout.PlotPaperUnits}");
            sb.AppendLine();
            sb.AppendLine($"--- Styl Wydruku ---");
            sb.AppendLine($"Styl (StyleSheet): {layout.CurrentStyleSheet ?? "(nie ustawiono)"}");
            sb.AppendLine($"Drukuj z plot styles (PlotPlotStyles): {layout.PlotPlotStyles}");
            sb.AppendLine($"Drukuj lineweights (PrintLineweights): {layout.PrintLineweights}");
            sb.AppendLine($"Skaluj lineweights (ScaleLineweights): {layout.ScaleLineweights}");
            sb.AppendLine();
            sb.AppendLine($"--- Obszar i Skala ---");
            sb.AppendLine($"Typ obszaru (PlotType): {layout.PlotType}");
            sb.AppendLine($"Punkt origin (PlotOrigin): {layout.PlotOrigin}");
            sb.AppendLine($"Wycentrowany (PlotCentered): {layout.PlotCentered}");
            sb.AppendLine($"Skala standardowa (UseStandardScale): {layout.UseStandardScale}");
            sb.AppendLine($"Typ skali standardowej (StdScaleType): {layout.StdScaleType}");
            sb.AppendLine($"Skala wlasna (CustomPrintScale): Num={layout.CustomPrintScale.Numerator}, Den={layout.CustomPrintScale.Denominator}");
            sb.AppendLine();
            sb.AppendLine($"--- Obrot ---");
            sb.AppendLine($"Obrot (PlotRotation): {layout.PlotRotation}");
            sb.AppendLine();
            sb.AppendLine($"--- Opcje Rysowania ---");
            sb.AppendLine($"Drukuj ukryte linie (PlotHidden): {layout.PlotHidden}");
            sb.AppendLine($"Rysuj viewporty najpierw (DrawViewportsFirst): {layout.DrawViewportsFirst}");
            sb.AppendLine($"Drukuj ramki viewportow (PlotViewportBorders): {layout.PlotViewportBorders}");
            sb.AppendLine($"Przezroczystosc (PlotTransparency): {layout.PlotTransparency}");
            sb.AppendLine();
            sb.AppendLine($"--- Shade Plot ---");
            sb.AppendLine($"Tryb (ShadePlot): {layout.ShadePlot}");
            sb.AppendLine($"Poziom rozdzielczosci (ShadePlotResLevel): {layout.ShadePlotResLevel}");
            sb.AppendLine($"DPI custom (ShadePlotCustomDpi): {layout.ShadePlotCustomDpi}");

            string resultStr = sb.ToString();
            if (!string.IsNullOrEmpty(saveAs))
            {
                AgentMemoryState.Variables[saveAs] = resultStr;
            }
            return resultStr;
        }

        private string GetAllLayoutsSummary(Transaction tr, bool includeModel, string saveAs)
        {
            DBDictionary layoutDict = tr.GetObject(HostApplicationServices.WorkingDatabase.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
            if (layoutDict == null)
            {
                return "BLAD: Nie udalo sie uzyskac dostepu do slownika layoutow.";
            }

            var layouts = new List<object>();
            foreach (DBDictionaryEntry entry in layoutDict)
            {
                CadLayout layout = tr.GetObject(entry.Value, OpenMode.ForRead) as CadLayout;
                if (layout == null) continue;

                bool isModel = layout.ModelType;
                if (isModel && !includeModel) continue;

                layouts.Add(new
                {
                    Name = layout.LayoutName,
                    IsModel = isModel,
                    TabOrder = layout.TabOrder,
                    TabSelected = layout.TabSelected,
                    CanonicalMediaName = layout.CanonicalMediaName ?? "",
                    PlotConfigurationName = layout.PlotConfigurationName ?? "",
                    CurrentStyleSheet = layout.CurrentStyleSheet ?? "",
                    StdScale = layout.UseStandardScale ? layout.StdScale.ToString() : "Custom",
                    PlotRotation = layout.PlotRotation.ToString(),
                    PlotType = layout.PlotType.ToString()
                });
            }

            var sb = new StringBuilder();
            sb.AppendLine($"WYNIK: Znaleziono {layouts.Count} layout(ow):");

            foreach (dynamic l in layouts)
            {
                string typ = (bool)l.IsModel ? "[MODEL]" : "[ARKUSZ]";
                sb.AppendLine($"  - {typ} {l.Name}");
                if (!(bool)l.IsModel)
                {
                    sb.AppendLine($"      Papier: {l.CanonicalMediaName}");
                    sb.AppendLine($"      Urzadzenie: {l.PlotConfigurationName}");
                    sb.AppendLine($"      Styl: {l.CurrentStyleSheet}");
                    sb.AppendLine($"      Skala: {l.StdScale}, Obrot: {l.PlotRotation}");
                }
            }

            string resultStr = sb.ToString();
            if (!string.IsNullOrEmpty(saveAs))
            {
                AgentMemoryState.Variables[saveAs] = resultStr;
            }
            return resultStr;
        }

        public List<string> Examples => new List<string>
        {
            "{}",
            "{ \"LayoutName\": \"IS.W.01\" }",
            "{ \"LayoutName\": \"A4-PION\", \"SaveAs\": \"MyLayoutSettings\" }",
            "{ \"IncludeModel\": false }",
            "{ \"SaveAs\": \"LayoutsList\" }"
        };
    }
}