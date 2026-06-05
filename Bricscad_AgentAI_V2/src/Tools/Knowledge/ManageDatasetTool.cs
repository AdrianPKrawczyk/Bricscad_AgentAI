using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Bricscad.ApplicationServices;

namespace Bricscad_AgentAI_V2.Tools.Knowledge
{
    public class ManageDatasetTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ManageDataset",
                    Description = "Zapisuje lub nadpisuje plik zestawu danych używając tablicy obiektów JSON. CRITICAL NORMALIZATION RULE: Datasets MUST be strictly flat arrays of objects (SQL-like tables). If the user provides a hierarchical file or a file with multiple distinct categories (e.g., circular vs rectangular ducts), you MUST EITHER: Option A: Normalize them into ONE flat table by adding a discriminator column (e.g., [{'Type': 'Circular', 'Dim': 100}, {'Type': 'Rectangular', 'Dim': 150}]). Option B: Call this tool multiple times to create completely separate datasets (e.g., 'Ducts_Circular' and 'Ducts_Rectangular'). DO NOT merge incompatible columns into a single flat object.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "datasetName", new ToolParameter { Type = "string", Description = "Nazwa zestawu danych (bez .json)." } },
                            { "description", new ToolParameter { Type = "string", Description = "Opis zawartości datasetu." } },
                            { "category", new ToolParameter { Type = "string", Description = "Kategoria logiczna (np. 'HVAC/Wentylacja'). Definiuje podfolder." } },
                            { "tags", new ToolParameter { Type = "array", Description = "Lista tagów opisujących dataset.", Items = Newtonsoft.Json.Linq.JToken.FromObject(new { type = "string" }) } },
                            { "jsonArrayData", new ToolParameter { Type = "string", Description = "Struktura JSON - płaska tablica obiektów: [ {..}, {..} ]." } }
                        },
                        Required = new List<string> { "datasetName", "jsonArrayData", "description", "category" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject parameters)
        {
            try
            {
                string datasetName = parameters["datasetName"]?.ToString();
                string jsonArrayData = parameters["jsonArrayData"]?.ToString();
                string description = parameters["description"]?.ToString();
                string category = parameters["category"]?.ToString() ?? "Uncategorized";
                
                var tags = new List<string>();
                if (parameters["tags"] is JArray tagsArray)
                {
                    tags = tagsArray.ToObject<List<string>>();
                }

                if (string.IsNullOrWhiteSpace(datasetName) || string.IsNullOrWhiteSpace(jsonArrayData))
                    return "BŁĄD: Brak nazwy datasetu lub danych JSON.";

                var metadata = new DatasetMetadata
                {
                    DatasetId = datasetName,
                    Description = description,
                    Category = category,
                    Tags = tags
                };

                DatasetManager.SaveDataset(metadata, jsonArrayData);
                return $"SUKCES: Zapisano zestaw danych '{datasetName}' w kategorii '{category}'.";
            }
            catch (Exception ex)
            {
                return $"BŁĄD ZAPISU: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string>();
    }
}
