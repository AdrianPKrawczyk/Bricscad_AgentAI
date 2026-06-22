using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Bricscad.ApplicationServices;

namespace Bricscad_AgentAI_V2.Tools.Knowledge
{
    public class ImportJsonFileTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ImportJsonFile",
                    Description = "Importuje zestaw danych z pliku JSON na dysku (ścieżka absolutna) i zapisuje go jako dataset. Preferuj to narzędzie zamiast ManageDataset, gdy dane są duże lub dostępne jako plik - eliminuje limit tokenów i ryzyko obcięcia stringa. Obsługiwane formaty wejściowe: (1) flat array [ {..}, {..} ], (2) pojedynczy obiekt {..} (zostanie opakowany w jednoelementową tablicę). CRITICAL NORMALIZATION RULE: Datasets MUST be strictly flat arrays of objects (SQL-like tables). If the user provides a hierarchical file or a file with multiple distinct categories (e.g., circular vs rectangular ducts), you MUST EITHER: Option A: Normalize them into ONE flat table by adding a discriminator column (e.g., [{'Type': 'Circular', 'Dim': 100}, {'Type': 'Rectangular', 'Dim': 150}]). Option B: Call this tool multiple times to create completely separate datasets (e.g., 'Ducts_Circular' and 'Ducts_Rectangular'). DO NOT merge incompatible columns into a single flat object.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "filePath", new ToolParameter { Type = "string", Description = "Absolutna ścieżka do pliku JSON na dysku (np. C:\\\\dane\\\\rury.json)." } },
                            { "datasetName", new ToolParameter { Type = "string", Description = "Nazwa nowego zestawu danych (bez .json)." } },
                            { "description", new ToolParameter { Type = "string", Description = "Opis zawartości datasetu." } },
                            { "category", new ToolParameter { Type = "string", Description = "Kategoria logiczna (np. 'HVAC/Wentylacja'). Definiuje podfolder." } },
                            { "tags", new ToolParameter { Type = "array", Description = "Lista tagów opisujących dataset.", Items = Newtonsoft.Json.Linq.JToken.FromObject(new { type = "string" }) } }
                        },
                        Required = new List<string> { "filePath", "datasetName", "description", "category" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject parameters)
        {
            try
            {
                string filePath = parameters["filePath"]?.ToString();
                string datasetName = parameters["datasetName"]?.ToString();
                string description = parameters["description"]?.ToString();
                string category = parameters["category"]?.ToString() ?? "Uncategorized";

                var tags = new List<string>();
                if (parameters["tags"] is JArray tagsArray)
                {
                    tags = tagsArray.ToObject<List<string>>();
                }

                if (string.IsNullOrWhiteSpace(filePath))
                    return "BŁĄD: Brak ścieżki pliku (parametr filePath).";
                if (string.IsNullOrWhiteSpace(datasetName))
                    return "BŁĄD: Brak nazwy datasetu.";

                if (!File.Exists(filePath))
                    return $"BŁĄD: Plik nie istnieje: {filePath}";

                string rawJson;
                try
                {
                    rawJson = File.ReadAllText(filePath);
                }
                catch (Exception readEx)
                {
                    return $"BŁĄD ODCZYTU PLIKU: {readEx.Message}";
                }

                if (string.IsNullOrWhiteSpace(rawJson))
                    return "BŁĄD: Plik jest pusty.";

                JToken parsed;
                try
                {
                    parsed = JToken.Parse(rawJson);
                }
                catch (JsonReaderException jex)
                {
                    return $"BŁĄD PARSOWANIA JSON w pliku: {jex.Message} (path: {filePath})";
                }

                JArray jsonArray;
                if (parsed is JArray arr)
                {
                    jsonArray = arr;
                }
                else if (parsed is JObject obj)
                {
                    jsonArray = new JArray(obj);
                }
                else
                {
                    return $"BŁĄD: Nieoczekiwany typ JSON (oczekiwano tablicy lub obiektu, otrzymano {parsed.Type}).";
                }

                var metadata = new DatasetMetadata
                {
                    DatasetId = datasetName,
                    Description = description,
                    Category = category,
                    Tags = tags
                };

                DatasetManager.SaveDataset(metadata, jsonArray.ToString(Formatting.None));
                return $"SUKCES: Zaimportowano {jsonArray.Count} rekordów z '{filePath}' do datasetu '{datasetName}' w kategorii '{category}'.";
            }
            catch (Exception ex)
            {
                return $"BŁĄD IMPORTU JSON: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string>();
    }
}