using System;
using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ReadSelectedBlockInfoTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ReadSelectedBlockInfo",
                    Description = "Odczytuje nazwę i podstawowe informacje o pierwszym bloku (BlockReference) z aktualnego zaznaczenia. Używaj, gdy użytkownik pyta o nazwę zaznaczonego bloku, jego definicję lub podstawową identyfikację.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>()
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            var ids = AgentMemoryState.ActiveSelection;
            if (ids == null || ids.Length == 0)
            {
                return "BŁĄD: ActiveSelection jest puste. Zaznacz blok przed odczytem jego nazwy.";
            }

            try
            {
                using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                {
                    foreach (ObjectId id in ids)
                    {
                        BlockReference br = tr.GetObject(id, OpenMode.ForRead) as BlockReference;
                        if (br == null) continue;

                        BlockTableRecord defBtr = tr.GetObject(br.BlockTableRecord, OpenMode.ForRead) as BlockTableRecord;
                        BlockTableRecord dynBtr = br.DynamicBlockTableRecord != ObjectId.Null
                            ? tr.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead) as BlockTableRecord
                            : null;

                        string instanceName = br.Name ?? "(brak)";
                        string definitionName = defBtr?.Name ?? "(brak)";
                        string dynamicName = dynBtr?.Name ?? "(brak)";

                        return
                            "WYNIK: Informacje o zaznaczonym bloku:\n" +
                            $"- InstanceName: {instanceName}\n" +
                            $"- DefinitionName: {definitionName}\n" +
                            $"- DynamicDefinitionName: {dynamicName}\n" +
                            $"- Layer: {br.Layer}\n" +
                            $"- Handle: {br.Handle}\n" +
                            $"- Position: [{Math.Round(br.Position.X, 4)}, {Math.Round(br.Position.Y, 4)}, {Math.Round(br.Position.Z, 4)}]\n" +
                            $"- Scale: X={Math.Round(br.ScaleFactors.X, 4)}, Y={Math.Round(br.ScaleFactors.Y, 4)}, Z={Math.Round(br.ScaleFactors.Z, 4)}";
                    }

                    return "BŁĄD: W ActiveSelection nie znaleziono żadnego obiektu typu BlockReference.";
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD ODCZYTU BLOKU: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string>
        {
            "{}"
        };
    }
}
