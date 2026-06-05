using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Core.DynamicSystems
{
    public class DatasetManager : IDatasetProvider
    {
        public DatasetManager()
        {
            AppPaths.EnsureDirectoriesExist();
        }

        private JArray LoadDataset(string datasetName)
        {
            string basePath = AppPaths.GetDatasetsPath();
            var files = Directory.GetFiles(basePath, $"{datasetName}.data.json", SearchOption.AllDirectories);
            
            if (files.Length == 0)
            {
                // Fallback to legacy
                files = Directory.GetFiles(basePath, $"{datasetName}.json", SearchOption.AllDirectories);
                if (files.Length == 0)
                    throw new FileNotFoundException($"Baza danych '{datasetName}' nie została znaleziona.");
            }

            string path = files[0];
            string json = File.ReadAllText(path);
            return JArray.Parse(json);
        }

        private IEnumerable<JObject> FilterData(JArray data, Dictionary<string, string> filters)
        {
            var rows = data.OfType<JObject>();
            if (filters != null && filters.Count > 0)
            {
                rows = rows.Where(row =>
                {
                    foreach (var kvp in filters)
                    {
                        if (!row.TryGetValue(kvp.Key, StringComparison.OrdinalIgnoreCase, out JToken val) ||
                            !val.ToString().Equals(kvp.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }
                    return true;
                });
            }
            return rows;
        }

        public JToken GetExactMatch(string datasetName, string searchColumn, string searchValue, Dictionary<string, string> filters = null)
        {
            var data = LoadDataset(datasetName);
            var filteredData = FilterData(data, filters);
            foreach (JObject row in filteredData)
            {
                if (row.TryGetValue(searchColumn, StringComparison.OrdinalIgnoreCase, out JToken val))
                {
                    if (val.ToString().Equals(searchValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return row;
                    }
                }
            }
            return null;
        }

        public JToken GetNearestGreater(string datasetName, string searchColumn, double targetValue, Dictionary<string, string> filters = null)
        {
            var data = LoadDataset(datasetName);
            var filteredData = FilterData(data, filters);
            JObject bestMatch = null;
            double minDiff = double.MaxValue;

            foreach (JObject row in filteredData)
            {
                if (row.TryGetValue(searchColumn, StringComparison.OrdinalIgnoreCase, out JToken val))
                {
                    if (double.TryParse(val.ToString(), out double numVal))
                    {
                        if (numVal >= targetValue)
                        {
                            double diff = numVal - targetValue;
                            if (diff < minDiff)
                            {
                                minDiff = diff;
                                bestMatch = row;
                            }
                        }
                    }
                }
            }

            return bestMatch;
        }

        public JToken GetNearestLower(string datasetName, string searchColumn, double targetValue, Dictionary<string, string> filters = null)
        {
            var data = LoadDataset(datasetName);
            var filteredData = FilterData(data, filters);
            JObject bestMatch = null;
            double minDiff = double.MaxValue;

            foreach (JObject row in filteredData)
            {
                if (row.TryGetValue(searchColumn, StringComparison.OrdinalIgnoreCase, out JToken val))
                {
                    if (double.TryParse(val.ToString(), out double numVal))
                    {
                        if (numVal <= targetValue)
                        {
                            double diff = targetValue - numVal;
                            if (diff < minDiff)
                            {
                                minDiff = diff;
                                bestMatch = row;
                            }
                        }
                    }
                }
            }

            return bestMatch;
        }

        public static void SaveDataset(DatasetMetadata metadata, string jsonArrayData)
        {
            AppPaths.EnsureDirectoriesExist();
            
            // Normalize category string for filesystem
            string safeCategory = string.IsNullOrWhiteSpace(metadata.Category) ? "Uncategorized" : metadata.Category;
            foreach (char c in Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()))
            {
                if (c != '/' && c != '\\') // Allow subdirectories
                {
                    safeCategory = safeCategory.Replace(c.ToString(), "");
                }
            }

            string categoryPath = Path.Combine(AppPaths.GetDatasetsPath(), safeCategory);
            Directory.CreateDirectory(categoryPath);

            string metadataPath = Path.Combine(categoryPath, $"{metadata.DatasetId}.json");
            string dataPath = Path.Combine(categoryPath, $"{metadata.DatasetId}.data.json");
            
            // Validate JSON array
            var array = JArray.Parse(jsonArrayData);
            
            // Save metadata
            string metadataJson = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            File.WriteAllText(metadataPath, metadataJson);

            // Save data
            File.WriteAllText(dataPath, array.ToString(Formatting.Indented));
            
            BielikLogger.LogInfo($"[DatasetManager] Zapisano bazę danych: {metadata.DatasetId} w {safeCategory}");
        }

        public static IEnumerable<DatasetMetadata> GetAvailableDatasets()
        {
            string path = AppPaths.GetDatasetsPath();
            if (!Directory.Exists(path)) return new List<DatasetMetadata>();

            var datasets = new List<DatasetMetadata>();
            var files = Directory.GetFiles(path, "*.json", SearchOption.AllDirectories)
                                 .Where(f => !f.EndsWith(".data.json", StringComparison.OrdinalIgnoreCase));

            foreach (var file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var metadata = JsonConvert.DeserializeObject<DatasetMetadata>(json);
                    
                    // Fallback dla starych plików (które były czystymi tablicami)
                    if (metadata == null || string.IsNullOrEmpty(metadata.DatasetId))
                    {
                        BielikLogger.LogWarn($"[DatasetManager] Zignorowano stary plik bez metadanych: {file}");
                        continue;
                    }
                    
                    datasets.Add(metadata);
                }
                catch (Exception ex)
                {
                    BielikLogger.LogWarn($"[DatasetManager] Błąd deserializacji pliku metadanych {file}: {ex.Message}");
                }
            }

            return datasets;
        }
    }
}
