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
                    Description = "Zapisuje lub nadpisuje plik zestawu danych używając tablicy obiektów JSON.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "datasetName", new ToolParameter { Type = "string", Description = "Nazwa zestawu danych (bez .json)." } },
                            { "jsonArrayData", new ToolParameter { Type = "string", Description = "Struktura JSON - płaska tablica obiektów: [ {..}, {..} ]." } }
                        },
                        Required = new List<string> { "datasetName", "jsonArrayData" }
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

                if (string.IsNullOrWhiteSpace(datasetName) || string.IsNullOrWhiteSpace(jsonArrayData))
                    return "BŁĄD: Brak nazwy datasetu lub danych JSON.";

                DatasetManager.SaveDataset(datasetName, jsonArrayData);
                return $"SUKCES: Zapisano zestaw danych '{datasetName}'.";
            }
            catch (Exception ex)
            {
                return $"BŁĄD ZAPISU: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string>();
    }
}
