using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Bricscad.ApplicationServices;

namespace Bricscad_AgentAI_V2.Tools.Knowledge
{
    public class ImportCsvDatasetTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ImportCsvDataset",
                    Description = "Konwertuje tekst CSV (pierwszy wiersz to nagłówki) na tablicę JSON i zapisuje jako dataset.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "datasetName", new ToolParameter { Type = "string", Description = "Nazwa nowego zestawu danych (bez .json)." } },
                            { "csvContent", new ToolParameter { Type = "string", Description = "Surowa zawartość CSV (z nagłówkami w pierwszym wierszu)." } },
                            { "delimiter", new ToolParameter { Type = "string", Description = "Znak oddzielający, np. ',' lub ';' (domyślnie ';')." } }
                        },
                        Required = new List<string> { "datasetName", "csvContent" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject parameters)
        {
            try
            {
                string datasetName = parameters["datasetName"]?.ToString();
                string csvContent = parameters["csvContent"]?.ToString();
                string delimiter = parameters.ContainsKey("delimiter") && parameters["delimiter"] != null 
                                   ? parameters["delimiter"].ToString() 
                                   : ";";

                if (string.IsNullOrWhiteSpace(csvContent)) return "BŁĄD: Pusty plik CSV.";

                var lines = csvContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 2) return "BŁĄD: CSV musi zawierać co najmniej wiersz nagłówków i jeden wiersz danych.";

                string[] headers = lines[0].Split(new[] { delimiter }, StringSplitOptions.None);
                JArray jsonArray = new JArray();

                for (int i = 1; i < lines.Length; i++)
                {
                    string[] values = lines[i].Split(new[] { delimiter }, StringSplitOptions.None);
                    JObject row = new JObject();
                    for (int j = 0; j < headers.Length; j++)
                    {
                        string header = headers[j].Trim();
                        string val = j < values.Length ? values[j].Trim() : "";
                        
                        if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double numVal))
                        {
                            row[header] = numVal;
                        }
                        else
                        {
                            row[header] = val;
                        }
                    }
                    jsonArray.Add(row);
                }

                DatasetManager.SaveDataset(datasetName, jsonArray.ToString(Formatting.None));
                return $"SUKCES: Zapisano zestaw danych '{datasetName}' ({jsonArray.Count} rekordów).";
            }
            catch (Exception ex)
            {
                return $"BŁĄD IMPORTU: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string>();
    }
}
