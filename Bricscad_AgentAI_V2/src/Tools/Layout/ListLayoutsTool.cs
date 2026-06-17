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
                    Description = "Listuje wszystkie layouty (arkusze wydruku) w biezacym rysunku wraz z kluczowymi metadanymi: nazwa, typ (Model/Layout), format papieru, urzadzenie drukujace, styl wydruku, obrot, skala.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "SaveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna nazwa zmiennej do zapisu listy layoutow w pamieci Agenta (bez @)."
                                }
                            },
                            {
                                "IncludeModel", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy uwzglednic layout 'Model' (domyslnie true)."
                                }
                            }
                        }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string saveAs = args["SaveAs"]?.ToString();
            bool includeModel = true;
            if (args["IncludeModel"] != null)
            {
                try { includeModel = args["IncludeModel"].Value<bool>(); } catch { }
            }

            var layouts = new List<object>();
            Database db = doc.Database;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    DBDictionary layoutDict = tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
                    if (layoutDict == null)
                    {
                        return "BLAD: Nie udalo sie uzyskac dostepu do slownika layoutow.";
                    }

                    foreach (DBDictionaryEntry entry in layoutDict)
                    {
                        CadLayout layout = tr.GetObject(entry.Value, OpenMode.ForRead) as CadLayout;
                        if (layout == null) continue;

                        bool isModel = layout.ModelType;
                        if (isModel && !includeModel) continue;

                        var info = new
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
                        };

                        layouts.Add(info);
                    }

                    tr.Commit();
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
            catch (Exception ex)
            {
                return $"BLAD PODCZAS LISTOWANIA LAYOUTOW: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string>
        {
            "{}",
            "{ \"IncludeModel\": false }",
            "{ \"SaveAs\": \"LayoutsList\" }"
        };
    }
}