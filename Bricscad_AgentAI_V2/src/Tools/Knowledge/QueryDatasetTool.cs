using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Bricscad.ApplicationServices;

namespace Bricscad_AgentAI_V2.Tools.Knowledge
{
    public class QueryDatasetTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "QueryDataset",
                    Description = "Wyszukuje rekord w katalogu inżynierskim (Dataset) na podstawie kryteriów.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "datasetName", new ToolParameter { Type = "string", Description = "Nazwa zestawu danych (bez rozszerzenia .json)." } },
                            { "searchColumn", new ToolParameter { Type = "string", Description = "Nazwa kolumny, w której szukamy wartości." } },
                            { "searchValue", new ToolParameter { Type = "string", Description = "Wartość docelowa (tekst lub liczba)." } },
                            { "queryType", new ToolParameter { Type = "string", Description = "Typ zapytania: 'Exact', 'NearestGreater', lub 'NearestLower'." } },
                            { "filters", new ToolParameter { Type = "object", Description = "Opcjonalny słownik klucz-wartość (dodatkowe filtry zawężające, np. { \"Material\": \"PEX\" })." } }
                        },
                        Required = new List<string> { "datasetName", "searchColumn", "searchValue", "queryType" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject parameters)
        {
            try
            {
                string datasetName = parameters["datasetName"]?.ToString();
                string searchColumn = parameters["searchColumn"]?.ToString();
                string searchValueStr = parameters["searchValue"]?.ToString();
                string queryType = parameters["queryType"]?.ToString();
                
                Dictionary<string, string> filters = null;
                if (parameters.ContainsKey("filters") && parameters["filters"] != null && parameters["filters"].Type == JTokenType.Object)
                {
                    filters = parameters["filters"].ToObject<Dictionary<string, string>>();
                }

                var db = new DatasetManager();
                JToken result = null;

                if (queryType.Equals("Exact", StringComparison.OrdinalIgnoreCase))
                {
                    result = db.GetExactMatch(datasetName, searchColumn, searchValueStr, filters);
                }
                else if (queryType.Equals("NearestGreater", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(searchValueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                        result = db.GetNearestGreater(datasetName, searchColumn, val, filters);
                    else
                        return "BŁĄD: searchValue musi być liczbą dla NearestGreater.";
                }
                else if (queryType.Equals("NearestLower", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse(searchValueStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                        result = db.GetNearestLower(datasetName, searchColumn, val, filters);
                    else
                        return "BŁĄD: searchValue musi być liczbą dla NearestLower.";
                }
                else
                {
                    return $"BŁĄD: Nieznany queryType: {queryType}";
                }

                if (result != null)
                {
                    return result.ToString(Newtonsoft.Json.Formatting.None);
                }
                return "Nie znaleziono pasującego rekordu.";
            }
            catch (Exception ex)
            {
                return $"BŁĄD ZAPYTANIA: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string> { "{\"datasetName\": \"rury\", \"searchColumn\": \"srednica\", \"searchValue\": \"100\", \"queryType\": \"Exact\"}" };
    }
}
