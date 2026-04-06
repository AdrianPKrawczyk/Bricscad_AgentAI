using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace Bricscad_AgentAI_V2.Tools
{
    /// <summary>
    /// Narzędzie do tworzenia nowej definicji bloku (BlockTableRecord) z aktualnie zaznaczonych obiektów.
    /// Obsługuje klonowanie głębokie (DeepCloneObjects) oraz usuwanie oryginałów.
    /// </summary>
    public class CreateBlockTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "CreateBlock",
                    Description = "Tworzy nową definicję bloku (BlockTableRecord) z aktualnie zaznaczonych obiektów.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "BlockName", new ToolParameter { Type = "string", Description = "Nazwa nowej definicji bloku." } },
                            { "BasePoint", new ToolParameter { Type = "string", Description = "Punkt bazowy bloku [x,y,z] lub 'AskUser'." } },
                            { "DeleteOriginals", new ToolParameter { Type = "boolean", Description = "Czy usunąć oryginalne obiekty z rysunku po utworzeniu bloku (domyślnie false)." } }
                        },
                        Required = new List<string> { "BlockName", "BasePoint" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string blockName = args["BlockName"]?.ToString();
            string pointStr = args["BasePoint"]?.ToString();
            bool deleteOriginals = args["DeleteOriginals"]?.Value<bool>() ?? false;

            var selection = AgentMemoryState.ActiveSelection;
            if (selection == null || selection.Length == 0)
                return "BŁĄD: Brak zaznaczonych obiektów. Najpierw zaznacz obiekty, z których chcesz stworzyć blok.";

            Point3d basePt;
            if (pointStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
            {
                var ppr = doc.Editor.GetPoint("\nWskaż punkt bazowy dla nowego bloku: ");
                if (ppr.Status != PromptStatus.OK) return "BŁĄD: Anulowano wybór punktu bazowego.";
                basePt = ppr.Value;
            }
            else
            {
                try { basePt = ParsePoint(pointStr); }
                catch { return $"BŁĄD: Niepoprawny format punktu bazowego: '{pointStr}'. Oczekiwano [x,y,z] lub AskUser."; }
            }

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForWrite) as BlockTable;
                    
                    // POLITYKA NADPISYWANIA: BŁĄD JEŚLI ISTNIEJE
                    if (bt.Has(blockName)) return $"BŁĄD: Definicja bloku o nazwie '{blockName}' już istnieje. Wybierz inną nazwę.";

                    BlockTableRecord btr = new BlockTableRecord();
                    btr.Name = blockName;
                    btr.Origin = basePt;

                    ObjectId btrId = bt.Add(btr);
                    tr.AddNewlyCreatedDBObject(btr, true);

                    // KLONOWANIE GŁĘBOKIE (DeepCloneObjects)
                    ObjectIdCollection ids = new ObjectIdCollection(selection);
                    IdMapping mapping = new IdMapping();
                    doc.Database.DeepCloneObjects(ids, btrId, mapping, false);

                    // Usuwanie oryginałów jeśli wskazano
                    if (deleteOriginals)
                    {
                        foreach (ObjectId id in selection)
                        {
                            Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                            ent?.Erase();
                        }
                        // Czyścimy pamięć zaznaczenia, bo obiekty przestały istnieć w przestrzeni modelu
                        AgentMemoryState.Clear();
                    }

                    tr.Commit();
                }
                return $"WYNIK: Pomyślnie utworzono nową definicję bloku '{blockName}' z {selection.Length} obiektów.";
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY TWORZENIA BLOKU: {ex.Message}";
            }
        }

        private Point3d ParsePoint(string ptStr)
        {
            ptStr = ptStr.Replace("[", "").Replace("]", "").Replace("(", "").Replace(")", "").Trim();
            string[] parts = ptStr.Split(',');
            double x = double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
            double y = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            double z = parts.Length > 2 ? double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) : 0;
            return new Point3d(x, y, z);
        }
    }
}
