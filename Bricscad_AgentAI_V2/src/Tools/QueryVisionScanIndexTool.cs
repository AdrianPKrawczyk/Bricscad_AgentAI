using System;
using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class QueryVisionScanIndexTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "QueryVisionScanIndex",
                    Description = "Odczytuje ostatni lub wskazany VisionScanIndex.json i filtruje kafle/obserwacje po tekscie, profilu lub identyfikatorze.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "ScanId", new ToolParameter { Type = "string", Description = "Id skanu, sciezka do VisionScanIndex.json albo 'latest'. Domyslnie latest." } },
                            { "Query", new ToolParameter { Type = "string", Description = "Fraza do wyszukania w tile_id, sciezce obrazu, profilu lub obserwacjach. Puste zwraca podsumowanie." } },
                            { "Profile", new ToolParameter { Type = "string", Enum = new List<string> { "OcrLabels", "Symbols", "Diagnostics" }, Description = "Opcjonalny filtr profilu." } },
                            { "Limit", new ToolParameter { Type = "integer", Description = "Maksymalna liczba zwracanych kafli/obserwacji. Domyslnie 20." } }
                        },
                        Required = new List<string>()
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            try
            {
                string scanId = args["ScanId"]?.ToString();
                string query = args["Query"]?.ToString();
                string profile = args["Profile"]?.ToString();
                int limit = args["Limit"]?.Value<int?>() ?? 20;
                if (limit <= 0) limit = 20;

                MetricVisionScanIndex index = MetricVisionRenderer.LoadScanIndex(scanId);
                IEnumerable<MetricVisionTile> tiles = index.Tiles;
                if (!string.IsNullOrWhiteSpace(profile))
                {
                    tiles = tiles.Where(t => string.Equals(t.Profile, profile, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(query))
                {
                    tiles = tiles.Where(t =>
                        Contains(t.TileId, query) ||
                        Contains(t.ImagePath, query) ||
                        Contains(t.Profile, query) ||
                        (t.Neighbors != null && t.Neighbors.Any(n => Contains(n, query))));
                }

                IEnumerable<MetricVisionObservation> observations = index.Observations ?? Enumerable.Empty<MetricVisionObservation>();
                if (!string.IsNullOrWhiteSpace(query))
                {
                    observations = observations.Where(o =>
                        Contains(o.TileId, query) ||
                        Contains(o.Type, query) ||
                        Contains(o.Text, query));
                }

                var result = new
                {
                    status = "success",
                    scan_id = index.ScanId,
                    index_path = index.IndexPath,
                    drawing = index.Drawing,
                    scope = index.Scope,
                    profile = index.Profile,
                    tile_count = index.Tiles.Count,
                    observation_count = index.Observations?.Count ?? 0,
                    tiles = tiles.Take(limit).ToList(),
                    observations = observations.Take(limit).ToList()
                };

                return JsonConvert.SerializeObject(result, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return $"BLAD QUERY VISION INDEX: {ex.Message}";
            }
        }

        private static bool Contains(string text, string query)
        {
            return text != null && query != null && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public List<string> Examples => new List<string>
        {
            "{ \"ScanId\": \"latest\", \"Query\": \"r000\", \"Limit\": 10 }"
        };
    }
}
