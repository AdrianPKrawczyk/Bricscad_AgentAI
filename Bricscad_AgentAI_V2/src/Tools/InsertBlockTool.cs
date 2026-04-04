using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace Bricscad_AgentAI_V2.Tools
{
    /// <summary>
    /// Narzędzie do wstawiania instancji bloku (BlockReference) w podanym punkcie.
    /// Obsługuje automatyczną synchronizację atrybutów oraz podawanie ich wartości.
    /// </summary>
    public class InsertBlockTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "InsertBlock",
                    Description = "Wstawia nową instancję bloku na rysunek w podanym punkcie, z opcjonalnymi atrybutami.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "BlockName", new ToolParameter { Type = "string", Description = "Nazwa bloku do wstawienia." } },
                            { "InsertionPoint", new ToolParameter { Type = "string", Description = "Punkt wstawienia np. [100,200,0] lub 'AskUser'." } },
                            { "Scale", new ToolParameter { Type = "number", Description = "Skala XYZ (domyślnie 1.0)." } },
                            { "Rotation", new ToolParameter { Type = "number", Description = "Obrót w STOPNIACH (domyślnie 0.0)." } },
                            { "Attributes", new ToolParameter { Type = "array", Description = "Lista atrybutów do wypełnienia: [{\"Tag\": \"T1\", \"Value\": \"V1\"}, ...]" } }
                        },
                        Required = new List<string> { "BlockName", "InsertionPoint" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string blockName = args["BlockName"]?.ToString();
            string pointStr = args["InsertionPoint"]?.ToString();
            double scale = args["Scale"]?.Value<double>() ?? 1.0;
            double rotationDeg = args["Rotation"]?.Value<double>() ?? 0.0;
            JArray attrsArr = args["Attributes"] as JArray;

            Point3d insertPt;
            if (pointStr.Equals("AskUser", StringComparison.OrdinalIgnoreCase))
            {
                var ppr = doc.Editor.GetPoint($"\nWskaż punkt wstawienia dla bloku '{blockName}': ");
                if (ppr.Status != PromptStatus.OK) return "BŁĄD: Anulowano wybór punktu wstawienia.";
                insertPt = ppr.Value;
            }
            else
            {
                try { insertPt = ParsePoint(pointStr); }
                catch { return $"BŁĄD: Niepoprawny format punktu: '{pointStr}'. Oczekiwano [x,y,z] lub AskUser."; }
            }

            try
            {
                using (doc.LockDocument())
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    if (!bt.Has(blockName)) return $"BŁAD: Blok '{blockName}' nie istnieje w tabeli bloków bieżącego rysunku.";

                    ObjectId btrId = bt[blockName];
                    BlockTableRecord btr = tr.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                    BlockTableRecord currentSpace = tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

                    BlockReference br = new BlockReference(insertPt, btrId);
                    br.ScaleFactors = new Scale3d(scale);
                    br.Rotation = rotationDeg * Math.PI / 180.0; // Stopnie na radiany

                    currentSpace.AppendEntity(br);
                    tr.AddNewlyCreatedDBObject(br, true);

                    // SYNCHRONIZACJA ATRYBUTÓW
                    if (btr.HasAttributeDefinitions)
                    {
                        foreach (ObjectId subId in btr)
                        {
                            AttributeDefinition attDef = tr.GetObject(subId, OpenMode.ForRead) as AttributeDefinition;
                            if (attDef != null && !attDef.Constant)
                            {
                                AttributeReference attRef = new AttributeReference();
                                attRef.SetAttributeFromBlock(attDef, br.BlockTransform);
                                
                                // Wypełnianie wartości przekazanej w JSON
                                if (attrsArr != null)
                                {
                                    var match = attrsArr.FirstOrDefault(a => a["Tag"]?.ToString().Equals(attDef.Tag, StringComparison.OrdinalIgnoreCase) == true);
                                    if (match != null)
                                    {
                                        string rawVal = match["Value"]?.ToString() ?? "";
                                        string finalVal = AgentMemoryState.InjectVariables(rawVal);

                                        if (attDef.IsMTextAttributeDefinition)
                                        {
                                            MText mtxt = attRef.MTextAttribute;
                                            mtxt.Contents = finalVal;
                                            attRef.MTextAttribute = mtxt;
                                        }
                                        else
                                        {
                                            attRef.TextString = finalVal;
                                        }
                                    }
                                }

                                br.AttributeCollection.AppendAttribute(attRef);
                                tr.AddNewlyCreatedDBObject(attRef, true);
                            }
                        }
                    }

                    tr.Commit();
                }
                return $"WYNIK: Pomyślnie wstawiono blok '{blockName}' w punkcie {insertPt}.";
            }
            catch (Exception ex)
            {
                return $"BŁĄD KRYTYCZNY WSTAWIANIA BLOKU: {ex.Message}";
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
