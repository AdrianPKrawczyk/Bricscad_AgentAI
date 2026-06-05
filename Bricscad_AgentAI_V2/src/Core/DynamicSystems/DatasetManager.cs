using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Bricscad_AgentAI_V2.Core;

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
            string path = Path.Combine(AppPaths.GetDatasetsPath(), $"{datasetName}.json");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Baza danych '{datasetName}' nie została znaleziona w {path}.");
            }

            string json = File.ReadAllText(path);
            var array = JArray.Parse(json);
            return array;
        }

        public JToken GetExactMatch(string datasetName, string searchColumn, string searchValue)
        {
            var data = LoadDataset(datasetName);
            foreach (JObject row in data)
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

        public JToken GetNearestGreater(string datasetName, string searchColumn, double targetValue)
        {
            var data = LoadDataset(datasetName);
            JObject bestMatch = null;
            double minDiff = double.MaxValue;

            foreach (JObject row in data)
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

        public JToken GetNearestLower(string datasetName, string searchColumn, double targetValue)
        {
            var data = LoadDataset(datasetName);
            JObject bestMatch = null;
            double minDiff = double.MaxValue;

            foreach (JObject row in data)
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

        public static void SaveDataset(string datasetName, string jsonArrayData)
        {
            AppPaths.EnsureDirectoriesExist();
            string path = Path.Combine(AppPaths.GetDatasetsPath(), $"{datasetName}.json");
            
            // Walidacja struktury przed zapisem
            var array = JArray.Parse(jsonArrayData);
            
            File.WriteAllText(path, array.ToString(Formatting.Indented));
            BielikLogger.LogInfo($"[DatasetManager] Zapisano bazę danych: {datasetName}");
        }

        public static IEnumerable<string> GetAvailableDatasets()
        {
            string path = AppPaths.GetDatasetsPath();
            if (!Directory.Exists(path)) return new List<string>();

            return Directory.GetFiles(path, "*.json")
                .Select(Path.GetFileNameWithoutExtension);
        }
    }
}
