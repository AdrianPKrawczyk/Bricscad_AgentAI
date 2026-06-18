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
    public class ImportLayoutTemplateTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ImportLayoutTemplateTool",
                    Description = "Importuje layout (z Page Setup i zawartoscia geometryczna) z innego pliku DWG lub DWT. Uzywane do ladowania szablonow arkuszy.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "SourcePath", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Sciezka do pliku zrodlowego DWG/DWT (obowiazkowe)."
                                }
                            },
                            {
                                "SourceLayoutName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa layoutu w pliku zrodlowym do zaimportowania (obowiazkowe)."
                                }
                            },
                            {
                                "TargetLayoutName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa docelowa layoutu w biezacym rysunku (opcjonalny; domyslnie taka sama jak zrodlowa)."
                                }
                            },
                            {
                                "ImportPlotSettings", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy zaimportowac Page Setup (true/false, domyslnie true)."
                                }
                            },
                            {
                                "ImportEntities", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy zaimportowac geometrie/layout entities (true/false, domyslnie true)."
                                }
                            },
                            {
                                "OverwriteIfExists", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy nadpisac istniejacy layout (domyslnie false)."
                                }
                            }
                        },
                        Required = new List<string> { "SourcePath", "SourceLayoutName" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string sourcePath = args["SourcePath"]?.ToString() ?? "";
            string sourceLayoutName = args["SourceLayoutName"]?.ToString() ?? "";
            string targetLayoutName = args["TargetLayoutName"]?.ToString() ?? "";
            bool importPlotSettings = true;
            bool importEntities = true;
            bool overwrite = false;

            if (args["ImportPlotSettings"] != null)
                try { importPlotSettings = args["ImportPlotSettings"].Value<bool>(); } catch { }
            if (args["ImportEntities"] != null)
                try { importEntities = args["ImportEntities"].Value<bool>(); } catch { }
            if (args["OverwriteIfExists"] != null)
                try { overwrite = args["OverwriteIfExists"].Value<bool>(); } catch { }

            if (string.IsNullOrWhiteSpace(sourcePath))
                return "BLAD: SourcePath jest wymagany.";
            if (string.IsNullOrWhiteSpace(sourceLayoutName))
                return "BLAD: SourceLayoutName jest wymagany.";

            if (string.IsNullOrWhiteSpace(targetLayoutName))
                targetLayoutName = sourceLayoutName;

            string resolvedPath = LayoutHelpers.ValidateAndResolvePath(sourcePath);
            if (!File.Exists(resolvedPath))
            {
                return $"BLAD: Plik zrodlowy nie istnieje: '{resolvedPath}'.";
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
                        if (LayoutHelpers.LayoutExists(db, targetLayoutName, tr) && !overwrite)
                        {
                            return $"BLAD: Layout docelowy '{targetLayoutName}' juz istnieje. Ustaw OverwriteIfExists=true.";
                        }

                        if (LayoutHelpers.LayoutExists(db, targetLayoutName, tr))
                        {
                            try
                            {
                                LayoutManager.Current.DeleteLayout(targetLayoutName);
                            }
                            catch { }
                        }
                        tr.Commit();
                    }

                    using (Database sourceDb = new Database(false, true))
                    {
                        sourceDb.ReadDwgFile(resolvedPath, FileOpenMode.OpenForReadAndAllShare, true, "");

                        using (Transaction sourceTr = sourceDb.TransactionManager.StartTransaction())
                        {
                            DBDictionary sourceLayoutDict = sourceTr.GetObject(
                                sourceDb.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
                            if (sourceLayoutDict == null || !sourceLayoutDict.Contains(sourceLayoutName))
                            {
                                return $"BLAD: Layout '{sourceLayoutName}' nie istnieje w pliku zrodlowym '{resolvedPath}'.";
                            }

                            ObjectId sourceLayoutId = sourceLayoutDict.GetAt(sourceLayoutName);
                            CadLayout sourceLayout = sourceTr.GetObject(sourceLayoutId, OpenMode.ForRead) as CadLayout;
                            ObjectId sourceBtrId = sourceLayout.BlockTableRecordId;

                            using (Transaction targetTr = db.TransactionManager.StartTransaction())
                            {
                                if (importEntities)
                                {
                                    var layoutsBefore = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                    DBDictionary layoutDictPre = targetTr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
                                    if (layoutDictPre != null)
                                    {
                                        foreach (DBDictionaryEntry e2 in layoutDictPre)
                                            layoutsBefore.Add(e2.Key);
                                    }

                                    if (LayoutHelpers.LayoutExists(db, targetLayoutName, targetTr))
                                    {
                                        try
                                        {
                                            LayoutManager.Current.DeleteLayout(targetLayoutName);
                                        }
                                        catch { }
                                    }

                                    IdMapping mapping = new IdMapping();
                                    ObjectIdCollection ids = new ObjectIdCollection { sourceBtrId };

                                    sourceDb.WblockCloneObjects(
                                        ids,
                                        db.BlockTableId,
                                        mapping,
                                        DuplicateRecordCloning.Replace,
                                        false);

                                    DBDictionary layoutDictPost = targetTr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
                                    if (layoutDictPost != null)
                                    {
                                        string newlyCreatedLayoutName = null;
                                        foreach (DBDictionaryEntry e2 in layoutDictPost)
                                        {
                                            if (!layoutsBefore.Contains(e2.Key) && !e2.Key.Equals(targetLayoutName, StringComparison.OrdinalIgnoreCase))
                                            {
                                                newlyCreatedLayoutName = e2.Key;
                                                break; // Znaleźliśmy nowo utworzony arkusz
                                            }
                                        }

                                        if (!string.IsNullOrEmpty(newlyCreatedLayoutName))
                                        {
                                            try
                                            {
                                                LayoutManager.Current.RenameLayout(newlyCreatedLayoutName, targetLayoutName);
                                            }
                                            catch { }
                                        }
                                    }
                                }

                                if (importPlotSettings)
                                {
                                    CadLayout sourceSettings = sourceLayout;
                                    if (!LayoutHelpers.LayoutExists(db, targetLayoutName, targetTr))
                                    {
                                        LayoutManager.Current.CreateLayout(targetLayoutName);
                                    }

                                    if (LayoutHelpers.LayoutExists(db, targetLayoutName, targetTr))
                                    {
                                        CadLayout newLayout = LayoutHelpers.GetLayoutByName(db, targetLayoutName, targetTr);
                                        if (newLayout != null)
                                        {
                                            newLayout.UpgradeOpen();

                                            var validator = PlotSettingsValidator.Current;
                                            try { validator.SetPlotConfigurationName(newLayout, sourceSettings.PlotConfigurationName ?? "", null); } catch { }
                                            try { validator.SetCanonicalMediaName(newLayout, sourceSettings.CanonicalMediaName ?? ""); } catch { }
                                            try { validator.SetCurrentStyleSheet(newLayout, sourceSettings.CurrentStyleSheet ?? ""); } catch { }
                                            try { validator.SetPlotType(newLayout, sourceSettings.PlotType); } catch { }
                                            try { validator.SetPlotRotation(newLayout, sourceSettings.PlotRotation); } catch { }
                                            try { validator.SetPlotCentered(newLayout, sourceSettings.PlotCentered); } catch { }
                                            try { validator.SetPlotOrigin(newLayout, sourceSettings.PlotOrigin); } catch { }
                                            try { validator.SetUseStandardScale(newLayout, sourceSettings.UseStandardScale); } catch { }
                                            try { validator.SetStdScaleType(newLayout, sourceSettings.StdScaleType); } catch { }
                                            try { validator.SetCustomPrintScale(newLayout, new CustomScale(sourceSettings.CustomPrintScale.Numerator, sourceSettings.CustomPrintScale.Denominator)); } catch { }
                                            try { validator.SetPlotPaperUnits(newLayout, sourceSettings.PlotPaperUnits); } catch { }
                                        }
                                    }
                                }
                                else if (importEntities && !LayoutHelpers.LayoutExists(db, targetLayoutName, targetTr))
                                {
                                    LayoutManager.Current.CreateLayout(targetLayoutName);
                                }

                                targetTr.Commit();
                            }

                            sourceTr.Commit();
                        }
                    }
                }

                return $"SUKCES: Zaimportowano layout '{sourceLayoutName}' z '{resolvedPath}' jako '{targetLayoutName}' (PlotSettings={importPlotSettings}, Entities={importEntities}).";
            }
            catch (Exception ex)
            {
                return $"BLAD IMPORTU LAYOUT: {ex.Message}";
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = oldDb;
            }
        }

        public List<string> Examples => new List<string>
        {
            "{ \"SourcePath\": \"C:/templates/standard.dwt\", \"SourceLayoutName\": \"A4-PION\" }",
            "{ \"SourcePath\": \"C:/templates/standard.dwt\", \"SourceLayoutName\": \"A4-PION\", \"TargetLayoutName\": \"A4-IMPORT\" }",
            "{ \"SourcePath\": \"C:/templates/standard.dwt\", \"SourceLayoutName\": \"A4-PION\", \"ImportPlotSettings\": true, \"ImportEntities\": false, \"OverwriteIfExists\": true }"
        };
    }
}