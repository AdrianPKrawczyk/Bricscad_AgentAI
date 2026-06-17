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
    public class ExportLayoutTemplateTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ExportLayoutTemplateTool",
                    Description = "Eksportuje wybrany layout do osobnego pliku DWT/DWG (szablon arkusza). Moze opcjonalnie uwzglednic powiazane bloki.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "LayoutName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa layoutu do eksportu (obowiazkowe)."
                                }
                            },
                            {
                                "TargetPath", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Sciezka docelowa pliku DWT/DWG (obowiazkowe)."
                                }
                            },
                            {
                                "IncludePlotSettings", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy uwzglednic Page Setup (domyslnie true)."
                                }
                            },
                            {
                                "IncludeBlocks", new ToolParameter
                                {
                                    Type = "boolean",
                                    Description = "Czy uwzglednic definicje blokow uzywanych przez layout (domyslnie true)."
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
                        Required = new List<string> { "LayoutName", "TargetPath" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string layoutName = args["LayoutName"]?.ToString() ?? "";
            string targetPath = args["TargetPath"]?.ToString() ?? "";
            bool includePlotSettings = true;
            bool includeBlocks = true;
            bool overwrite = false;

            if (args["IncludePlotSettings"] != null)
                try { includePlotSettings = args["IncludePlotSettings"].Value<bool>(); } catch { }
            if (args["IncludeBlocks"] != null)
                try { includeBlocks = args["IncludeBlocks"].Value<bool>(); } catch { }
            if (args["OverwriteIfExists"] != null)
                try { overwrite = args["OverwriteIfExists"].Value<bool>(); } catch { }

            if (string.IsNullOrWhiteSpace(layoutName))
                return "BLAD: LayoutName jest wymagany.";
            if (string.IsNullOrWhiteSpace(targetPath))
                return "BLAD: TargetPath jest wymagany.";

            string resolvedPath = LayoutHelpers.ValidateAndResolvePath(targetPath);

            if (File.Exists(resolvedPath) && !overwrite)
            {
                return $"BLAD: Plik docelowy '{resolvedPath}' juz istnieje. Ustaw OverwriteIfExists=true.";
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
                ObjectId layoutBtrId = ObjectId.Null;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    CadLayout layout = LayoutHelpers.GetLayoutByName(db, layoutName, tr);
                    if (layout == null)
                    {
                        return $"BLAD: Layout '{layoutName}' nie istnieje.";
                    }

                    if (LayoutHelpers.IsModelLayout(layout))
                    {
                        return "BLAD: Nie mozna eksportowac layoutu 'Model' jako szablon.";
                    }

                    layoutBtrId = layout.BlockTableRecordId;
                    tr.Commit();
                }

                using (Database targetDb = new Database(true, true))
                {
                    using (Transaction targetTr = targetDb.TransactionManager.StartTransaction())
                    {
                        BlockTable bt = targetTr.GetObject(targetDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                        if (bt != null && !bt.Has(BlockTableRecord.ModelSpace))
                        {
                            BlockTableRecord modelBtr = new BlockTableRecord();
                            modelBtr.Name = BlockTableRecord.ModelSpace;
                            bt.UpgradeOpen();
                            bt.Add(modelBtr);
                            targetTr.AddNewlyCreatedDBObject(modelBtr, true);
                        }
                        if (bt != null && !bt.Has(BlockTableRecord.PaperSpace))
                        {
                            BlockTableRecord paperBtr = new BlockTableRecord();
                            paperBtr.Name = BlockTableRecord.PaperSpace;
                            bt.UpgradeOpen();
                            bt.Add(paperBtr);
                            targetTr.AddNewlyCreatedDBObject(paperBtr, true);
                        }
                        targetTr.Commit();
                    }

                    using (Transaction targetTr = targetDb.TransactionManager.StartTransaction())
                    {
                        CadLayout targetLayout = null;
                        if (LayoutHelpers.LayoutExists(targetDb, layoutName, targetTr))
                        {
                            targetLayout = LayoutHelpers.GetLayoutByName(targetDb, layoutName, targetTr);
                        }
                        else
                        {
                            LayoutManager.Current.CreateLayout(layoutName);
                            targetLayout = LayoutHelpers.GetLayoutByName(targetDb, layoutName, targetTr);
                        }

                        if (targetLayout == null)
                        {
                            return "BLAD: Nie udalo sie utworzyc layoutu docelowego.";
                        }
                        targetTr.Commit();
                    }

                    IdMapping mapping = new IdMapping();
                    ObjectIdCollection ids = new ObjectIdCollection { layoutBtrId };

                    db.DeepCloneObjects(
                        ids,
                        targetDb.BlockTableId,
                        mapping,
                        false);

                    if (includePlotSettings)
                    {
                        using (Transaction sourceTr = db.TransactionManager.StartTransaction())
                        using (Transaction targetTr = targetDb.TransactionManager.StartTransaction())
                        {
                            CadLayout sourceLayout = LayoutHelpers.GetLayoutByName(db, layoutName, sourceTr);
                            CadLayout destLayout = LayoutHelpers.GetLayoutByName(targetDb, layoutName, targetTr);

                            if (sourceLayout != null && destLayout != null)
                            {
                                destLayout.UpgradeOpen();
                                var validator = PlotSettingsValidator.Current;
                                try { validator.SetPlotConfigurationName(destLayout, sourceLayout.PlotConfigurationName ?? "", null); } catch { }
                                try { validator.SetCanonicalMediaName(destLayout, sourceLayout.CanonicalMediaName ?? ""); } catch { }
                                try { validator.SetCurrentStyleSheet(destLayout, sourceLayout.CurrentStyleSheet ?? ""); } catch { }
                                try { validator.SetPlotType(destLayout, sourceLayout.PlotType); } catch { }
                                try { validator.SetPlotRotation(destLayout, sourceLayout.PlotRotation); } catch { }
                                try { validator.SetPlotCentered(destLayout, sourceLayout.PlotCentered); } catch { }
                                try { validator.SetPlotOrigin(destLayout, sourceLayout.PlotOrigin); } catch { }
                                try { validator.SetUseStandardScale(destLayout, sourceLayout.UseStandardScale); } catch { }
                                try { validator.SetStdScaleType(destLayout, sourceLayout.StdScaleType); } catch { }
                                try { validator.SetCustomPrintScale(destLayout, new CustomScale(sourceLayout.CustomPrintScale.Numerator, sourceLayout.CustomPrintScale.Denominator)); } catch { }
                                try { validator.SetPlotPaperUnits(destLayout, sourceLayout.PlotPaperUnits); } catch { }
                            }

                            sourceTr.Commit();
                            targetTr.Commit();
                        }
                    }

                    targetDb.Dispose();
                }

                if (doc != null)
                {
                    string safePath = resolvedPath.Replace("\\", "/").Replace("\"", "\\\"");
                    string command = $"-SAVEAS\n\"{safePath}\"\n";
                    doc.SendStringToExecute(command, true, false, false);
                }

                return $"SUKCES: Eksportowano layout '{layoutName}' do '{resolvedPath}' (PlotSettings={includePlotSettings}, Blocks={includeBlocks}).";
            }
            catch (Exception ex)
            {
                return $"BLAD EKSPORTU LAYOUT: {ex.Message}";
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = oldDb;
            }
        }

        public List<string> Examples => new List<string>
        {
            "{ \"LayoutName\": \"A4-PION\", \"TargetPath\": \"C:/templates/a4-template.dwt\" }",
            "{ \"LayoutName\": \"A4-PION\", \"TargetPath\": \"C:/export/a4.dwg\", \"IncludeBlocks\": true, \"OverwriteIfExists\": true }"
        };
    }
}