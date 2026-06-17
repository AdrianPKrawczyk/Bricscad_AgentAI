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
    public class ManageLayoutTool : IToolV2
    {
        private static readonly string[] ValidActions = {
            "Create", "Delete", "Rename", "Clone", "SetCurrent", "CopyFromTemplate"
        };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ManageLayoutTool",
                    Description = "Zarzadzanie layoutami (arkusze wydruku): tworzenie, usuwanie, zmiana nazwy, klonowanie, ustawianie aktywnego, import z szablonu DWT/DWG.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "Action", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Akcja: 'Create', 'Delete', 'Rename', 'Clone', 'SetCurrent', 'CopyFromTemplate'."
                                }
                            },
                            {
                                "LayoutName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa layoutu (docelowego lub zrodlowego w zaleznosci od akcji)."
                                }
                            },
                            {
                                "NewName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nowa nazwa (dla Rename/Clone)."
                                }
                            },
                            {
                                "SourceDwgPath", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Sciezka do pliku zrodlowego DWG/DWT (tylko dla CopyFromTemplate)."
                                }
                            },
                            {
                                "TemplateLayoutName", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Nazwa layoutu w pliku zrodlowym do zaimportowania (tylko dla CopyFromTemplate)."
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
                        Required = new List<string> { "Action" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string action = args["Action"]?.ToString() ?? "";
            string layoutName = args["LayoutName"]?.ToString() ?? "";
            string newName = args["NewName"]?.ToString() ?? "";
            string sourceDwgPath = args["SourceDwgPath"]?.ToString() ?? "";
            string templateLayoutName = args["TemplateLayoutName"]?.ToString() ?? "";
            bool overwrite = false;
            if (args["OverwriteIfExists"] != null)
            {
                try { overwrite = args["OverwriteIfExists"].Value<bool>(); } catch { }
            }

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
                        case "create":
                            return CreateLayout(db, layoutName, overwrite);
                        case "delete":
                            return DeleteLayout(db, layoutName);
                        case "rename":
                            return RenameLayout(db, layoutName, newName);
                        case "clone":
                            return CloneLayout(db, layoutName, newName, overwrite);
                        case "setcurrent":
                            return SetCurrentLayout(db, layoutName);
                        case "copyfromtemplate":
                            return CopyFromTemplate(doc, db, layoutName, templateLayoutName, sourceDwgPath, overwrite);
                        default:
                            return $"BLAD: Nieznana akcja '{action}'.";
                    }
                }
            }
            catch (Exception ex)
            {
                return $"BLAD KRYTYCZNY NARZEDZIA: {ex.Message}";
            }
            finally
            {
                HostApplicationServices.WorkingDatabase = oldDb;
            }
        }

        private string CreateLayout(Database db, string layoutName, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(layoutName))
                return "BLAD: LayoutName jest wymagane dla akcji Create.";

            if (string.Equals(layoutName, LayoutHelpers.ModelLayoutName, StringComparison.OrdinalIgnoreCase))
                return "BLAD: Nie mozna tworzyc layoutu o nazwie 'Model'.";

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                if (LayoutHelpers.LayoutExists(db, layoutName, tr))
                {
                    if (!overwrite)
                    {
                        return $"BLAD: Layout '{layoutName}' juz istnieje. Ustaw OverwriteIfExists=true, aby go najpierw usunac.";
                    }

                    tr.Abort();
                }

                LayoutManager.Current.CreateLayout(layoutName);
                tr.Commit();
            }

            return $"SUKCES: Utworzono layout '{layoutName}'.";
        }

        private string DeleteLayout(Database db, string layoutName)
        {
            if (string.IsNullOrWhiteSpace(layoutName))
                return "BLAD: LayoutName jest wymagane dla akcji Delete.";

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                CadLayout layout = LayoutHelpers.GetLayoutByName(db, layoutName, tr);
                if (layout == null)
                {
                    return $"BLAD: Layout '{layoutName}' nie istnieje.";
                }

                if (LayoutHelpers.IsModelLayout(layout))
                {
                    return "BLAD: Nie mozna usunac layoutu 'Model'.";
                }

                string nameToDelete = layout.LayoutName;
                LayoutManager.Current.DeleteLayout(nameToDelete);
                tr.Commit();
            }

            return $"SUKCES: Usunieto layout '{layoutName}'.";
        }

        private string RenameLayout(Database db, string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
                return "BLAD: LayoutName i NewName sa wymagane dla akcji Rename.";

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                if (!LayoutHelpers.LayoutExists(db, oldName, tr))
                {
                    return $"BLAD: Layout '{oldName}' nie istnieje.";
                }

                if (LayoutHelpers.LayoutExists(db, newName, tr))
                {
                    return $"BLAD: Layout o nowej nazwie '{newName}' juz istnieje.";
                }

                LayoutManager.Current.RenameLayout(oldName, newName);
                tr.Commit();
            }

            return $"SUKCES: Zmieniono nazwe layoutu '{oldName}' na '{newName}'.";
        }

        private string CloneLayout(Database db, string sourceName, string newName, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(sourceName) || string.IsNullOrWhiteSpace(newName))
                return "BLAD: LayoutName (zrodlo) i NewName (kopia) sa wymagane dla akcji Clone.";

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                if (!LayoutHelpers.LayoutExists(db, sourceName, tr))
                {
                    return $"BLAD: Layout zrodlowy '{sourceName}' nie istnieje.";
                }

                if (LayoutHelpers.LayoutExists(db, newName, tr) && !overwrite)
                {
                    return $"BLAD: Layout docelowy '{newName}' juz istnieje. Ustaw OverwriteIfExists=true.";
                }

                try
                {
                    ObjectId sourceId = LayoutManager.Current.GetLayoutId(sourceName);
                    CadLayout sourceLayout = tr.GetObject(sourceId, OpenMode.ForRead) as CadLayout;
                    ObjectId sourceBtrId = sourceLayout.BlockTableRecordId;
                    BlockTableRecord sourceBtr = tr.GetObject(sourceBtrId, OpenMode.ForRead) as BlockTableRecord;

                    IdMapping mapping = new IdMapping();
                    ObjectIdCollection ids = new ObjectIdCollection { sourceBtrId };
                    db.DeepCloneObjects(ids, db.BlockTableId, mapping, false);
                }
                catch
                {
                }

                LayoutManager.Current.CloneLayout(sourceName, newName, 0);
                tr.Commit();
            }

            return $"SUKCES: Sklonowano layout '{sourceName}' jako '{newName}'.";
        }

        private string SetCurrentLayout(Database db, string layoutName)
        {
            if (string.IsNullOrWhiteSpace(layoutName))
                return "BLAD: LayoutName jest wymagane dla akcji SetCurrent.";

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                if (!LayoutHelpers.LayoutExists(db, layoutName, tr))
                {
                    return $"BLAD: Layout '{layoutName}' nie istnieje.";
                }

                LayoutManager.Current.CurrentLayout = layoutName;
                tr.Commit();
            }

            return $"SUKCES: Ustawiono aktywny layout na '{layoutName}'.";
        }

        private string CopyFromTemplate(Document doc, Database targetDb, string targetName,
            string templateName, string sourcePath, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(targetName))
                return "BLAD: LayoutName (nazwa docelowa) jest wymagane.";
            if (string.IsNullOrWhiteSpace(templateName))
                return "BLAD: TemplateLayoutName jest wymagane.";
            if (string.IsNullOrWhiteSpace(sourcePath))
                return "BLAD: SourceDwgPath jest wymagane.";

            string resolvedPath = LayoutHelpers.ValidateAndResolvePath(sourcePath);
            if (!File.Exists(resolvedPath))
            {
                return $"BLAD: Plik zrodlowy nie istnieje: '{resolvedPath}'.";
            }

            using (Transaction tr = targetDb.TransactionManager.StartTransaction())
            {
                if (LayoutHelpers.LayoutExists(targetDb, targetName, tr) && !overwrite)
                {
                    return $"BLAD: Layout docelowy '{targetName}' juz istnieje. Ustaw OverwriteIfExists=true.";
                }

                tr.Abort();
            }

            try
            {
                using (Database sourceDb = new Database(false, true))
                {
                    sourceDb.ReadDwgFile(resolvedPath, FileOpenMode.OpenForReadAndAllShare, true, "");

                    using (Transaction sourceTr = sourceDb.TransactionManager.StartTransaction())
                    {
                        DBDictionary sourceLayoutDict = sourceTr.GetObject(
                            sourceDb.LayoutDictionaryId, OpenMode.ForRead) as DBDictionary;
                        if (sourceLayoutDict == null || !sourceLayoutDict.Contains(templateName))
                        {
                            return $"BLAD: Layout '{templateName}' nie istnieje w pliku zrodlowym.";
                        }

                        ObjectId sourceLayoutId = sourceLayoutDict.GetAt(templateName);
                        CadLayout sourceLayout = sourceTr.GetObject(sourceLayoutId, OpenMode.ForRead) as CadLayout;
                        ObjectId sourceBtrId = sourceLayout.BlockTableRecordId;

                        using (DocumentLock docLock = doc.LockDocument())
                        {
                            using (Transaction targetTr = targetDb.TransactionManager.StartTransaction())
                            {
                                if (LayoutHelpers.LayoutExists(targetDb, targetName, targetTr))
                                {
                                    LayoutManager.Current.DeleteLayout(targetName);
                                }

                                IdMapping mapping = new IdMapping();
                                ObjectIdCollection ids = new ObjectIdCollection { sourceBtrId };
                                sourceDb.WblockCloneObjects(ids, targetDb.BlockTableId, mapping, DuplicateRecordCloning.Replace, false);
                                sourceTr.Commit();
                                targetTr.Commit();
                            }
                        }

                        if (LayoutHelpers.LayoutExists(targetDb, targetName,
                            targetDb.TransactionManager.StartTransaction() as Transaction))
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return $"BLAD IMPORTU SZABLONU: {ex.Message}";
            }

            try
            {
                using (Transaction tr = targetDb.TransactionManager.StartTransaction())
                {
                    if (LayoutHelpers.LayoutExists(targetDb, templateName, tr) &&
                        !string.Equals(templateName, targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!LayoutHelpers.LayoutExists(targetDb, targetName, tr))
                        {
                            LayoutManager.Current.RenameLayout(templateName, targetName);
                        }
                    }
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                return $"SUKCES CZESCIOWY: Zaimportowano layout z '{resolvedPath}', ale nie udalo sie zmienic nazwy: {ex.Message}";
            }

            return $"SUKCES: Zaimportowano layout '{templateName}' z '{resolvedPath}' jako '{targetName}'.";
        }

        public List<string> Examples => new List<string>
        {
            "{ \"Action\": \"Create\", \"LayoutName\": \"A4-PION\" }",
            "{ \"Action\": \"Delete\", \"LayoutName\": \"A4-PION\" }",
            "{ \"Action\": \"Rename\", \"LayoutName\": \"A4-PION\", \"NewName\": \"A4-PION-RZUT\" }",
            "{ \"Action\": \"SetCurrent\", \"LayoutName\": \"A4-PION\" }",
            "{ \"Action\": \"CopyFromTemplate\", \"LayoutName\": \"A4-IMPORT\", \"TemplateLayoutName\": \"A4-PION\", \"SourceDwgPath\": \"C:/templates/standard.dwt\" }"
        };
    }
}