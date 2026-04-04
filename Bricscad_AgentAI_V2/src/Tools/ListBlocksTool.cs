using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    /// <summary>
    /// Narzędzie do listowania dostępnych definicji bloków w rysunku.
    /// Pomija bloki anonimowe, XREF-y oraz arkusze.
    /// </summary>
    public class ListBlocksTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ListBlocks",
                    Description = "Zwraca listę nazw dostępnych definicji bloków (BlockTableRecord) w rysunku.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "SaveAs", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opcjonalna nazwa zmiennej do zapisu listy bloków w pamięci Agenta (bez @)."
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
            List<string> blockNames = new List<string>();

            try
            {
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                    foreach (ObjectId id in bt)
                    {
                        BlockTableRecord btr = tr.GetObject(id, OpenMode.ForRead) as BlockTableRecord;
                        if (btr == null) continue;

                        // Filtrowanie szumu (Layouty, Anonimowe, XREFy)
                        if (!btr.IsLayout && !btr.IsAnonymous && !btr.IsFromExternalReference && !btr.Name.StartsWith("*"))
                        {
                            blockNames.Add(btr.Name);
                        }
                    }
                    tr.Commit();
                }

                blockNames.Sort();
                string resultStr = blockNames.Count > 0 ? string.Join(", ", blockNames) : "Brak dostępnych bloków użytkownika.";
                
                if (!string.IsNullOrEmpty(saveAs))
                {
                    AgentMemoryState.Variables[saveAs] = resultStr;
                }

                return $"WYNIK: Znaleziono {blockNames.Count} definicji bloków: {resultStr}";
            }
            catch (Exception ex)
            {
                return $"BŁĄD PODCZAS LISTOWANIA BLOKÓW: {ex.Message}";
            }
        }
    }
}
