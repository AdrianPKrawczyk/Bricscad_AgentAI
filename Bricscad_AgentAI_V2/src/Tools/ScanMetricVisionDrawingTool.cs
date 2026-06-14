using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ScanMetricVisionDrawingTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ScanMetricVisionDrawing",
                    Description = "Dzieli rysunek, zaznaczenie lub warstwy na metryczne kafle Vision i zapisuje atlas PNG oraz VisionScanIndex.json.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "Scope", new ToolParameter { Type = "string", Enum = new List<string> { "Model", "Selection", "Layer", "Window" }, Description = "Zakres skanowania. Domyslnie Model. 'Window' wymaga podania WindowBounds." } },
                            { "LayerNames", new ToolParameter { Type = "array", Description = "Lista nazw warstw dla Scope=Layer.", Items = new JObject { ["type"] = "string" } } },
                            { "FilterOutliers", new ToolParameter { Type = "boolean", Description = "Czy filtrować oddalone, zabłąkane elementy przy skanowaniu całego modelu. Domyślnie true." } },
                            { "MarginPercent", new ToolParameter { Type = "number", Description = "Margines powiększający badany obszar w procentach (np. 10 to +10%). Domyślnie 0." } },
                            { "WindowBounds", new ToolParameter { Type = "object", Description = "Obiekt z polami MinX, MinY, MaxX, MaxY wymaganymi dla Scope=Window." } },
                            { "TileCadSize", new ToolParameter { Type = "number", Description = "Rozmiar jednego kafla w jednostkach CAD. Jesli brak, obejmie caly zakres jednym kaflem." } },
                            { "Resolution", new ToolParameter { Type = "integer", Description = "Rozdzielczosc kafla w pikselach. Domyslnie 1024." } },
                            { "OverlapPercent", new ToolParameter { Type = "number", Description = "Overlap miedzy kaflami w procentach. Domyslnie 10." } },
                            { "Profile", new ToolParameter { Type = "string", Enum = new List<string> { "OcrLabels", "Symbols", "Diagnostics" }, Description = "Profil analizy. Na start aktywny jest atlas dla wszystkich, pelna analiza VLM tylko jako osobny krok." } },
                            { "AnalyzeNow", new ToolParameter { Type = "boolean", Description = "Zarezerwowane dla pozniejszej automatycznej analizy VLM. W MVP pozostaw false." } },
                            { "AddOverlay", new ToolParameter { Type = "boolean", Description = "Czy nalozyc siatke kalibracyjna na kafle. Domyslnie true." } },
                            { "MaxTiles", new ToolParameter { Type = "integer", Description = "Bezpiecznik liczby kafli. Domyslnie 64. Jesli wyliczony atlas jest wiekszy, narzedzie przerwie przed renderowaniem." } },
                            { "AllowLargeScan", new ToolParameter { Type = "boolean", Description = "Ustaw true tylko gdy swiadomie chcesz przekroczyc MaxTiles. Domyslnie false." } },
                            { "UseExperimentalOffscreen", new ToolParameter { Type = "boolean", Description = "Uruchamia eksperymentalny backend BricsCAD GraphicsSystem off-screen. Domyslnie false, bo w V22 moze wywolywac bledy runtime." } },
                            { "AllowScreenFallback", new ToolParameter { Type = "boolean", Description = "Diagnostycznie pozwala uzyc zrzutu ekranu CopyFromScreen. Domyslnie false, bo fallback moze lapac UI i nie jest metrycznie wiarygodny." } },
                            { "FadeOtherLayers", new ToolParameter { Type = "boolean", Description = "Jeśli true, warstwy niespełniające warunków LayerNames (lub niewybrane do izolacji) otrzymają 70% przezroczystości (zamiast zostać wyłączone)." } },
                            { "GrayOtherLayers", new ToolParameter { Type = "boolean", Description = "Jeśli true, warstwy niespełniające warunków otrzymają szary kolor (ColorIndex 8)." } }
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
                string scope = args["Scope"]?.ToString() ?? "Model";
                string profile = MetricVisionRenderer.NormalizeProfile(args["Profile"]?.ToString());
                int resolution = args["Resolution"]?.Value<int?>() ?? 1024;
                double overlap = args["OverlapPercent"]?.Value<double?>() ?? 10.0;
                bool analyzeNow = args["AnalyzeNow"]?.Value<bool?>() ?? false;
                bool addOverlay = args["AddOverlay"]?.Value<bool?>() ?? true;
                int maxTiles = args["MaxTiles"]?.Value<int?>() ?? 64;
                bool allowLargeScan = args["AllowLargeScan"]?.Value<bool?>() ?? false;
                bool useExperimentalOffscreen = args["UseExperimentalOffscreen"]?.Value<bool?>() ?? false;
                bool allowScreenFallback = args["AllowScreenFallback"]?.Value<bool?>() ?? false;
                bool fadeOtherLayers = args["FadeOtherLayers"]?.Value<bool?>() ?? false;
                bool grayOtherLayers = args["GrayOtherLayers"]?.Value<bool?>() ?? false;
                List<string> layerNames = ReadLayerNames(args["LayerNames"]);

                bool filterOutliers = args["FilterOutliers"]?.Value<bool?>() ?? true;
                double marginPercent = args["MarginPercent"]?.Value<double?>() ?? 0.0;
                MetricVisionBounds windowBounds = null;
                if (args["WindowBounds"] is JObject wb)
                {
                    windowBounds = wb.ToObject<MetricVisionBounds>();
                }

                MetricVisionBounds extents = MetricVisionRenderer.GetScopeExtents(doc, scope, layerNames, filterOutliers, marginPercent, windowBounds);
                double tileCadSize = args["TileCadSize"]?.Value<double?>() ?? Math.Max(extents.MaxX - extents.MinX, extents.MaxY - extents.MinY);
                List<MetricVisionBounds> tileBounds = MetricVisionRenderer.BuildTiles(extents, tileCadSize, overlap);

                if (!allowLargeScan && tileBounds.Count > maxTiles)
                {
                    return JsonConvert.SerializeObject(new
                    {
                        status = "blocked",
                        reason = "tile_count_exceeds_limit",
                        tile_count = tileBounds.Count,
                        max_tiles = maxTiles,
                        scope = scope,
                        profile = profile,
                        cad_bounds = extents,
                        message = "Atlas bylby zbyt duzy. Zwiekasz TileCadSize, zawez Scope/LayerNames albo ustaw AllowLargeScan=true z wiekszym MaxTiles."
                    }, Formatting.Indented);
                }

                string scanId = MetricVisionRenderer.CreateScanId();
                string folder = Path.Combine(AppPaths.GetVisionScansPath(), scanId);
                string tilesFolder = Path.Combine(folder, "tiles");
                Directory.CreateDirectory(tilesFolder);

                MetricVisionScanIndex index = new MetricVisionScanIndex
                {
                    ScanId = scanId,
                    CreatedAt = DateTime.Now,
                    Drawing = doc?.Name,
                    Scope = scope,
                    Profile = profile,
                    Folder = folder,
                    IndexPath = Path.Combine(folder, "VisionScanIndex.json"),
                    CadBounds = extents
                };

                int cols = EstimateColumnCount(tileBounds);
                for (int i = 0; i < tileBounds.Count; i++)
                {
                    int row = cols > 0 ? i / cols : 0;
                    int col = cols > 0 ? i % cols : i;
                    string tileId = $"r{row:000}_c{col:000}";
                    string imagePath = Path.Combine(tilesFolder, tileId + ".png");
                    MetricVisionTile tile = MetricVisionRenderer.RenderTile(doc, tileBounds[i], imagePath, resolution, profile, addOverlay, useExperimentalOffscreen, allowScreenFallback, tileId, row, col, layerNames, null, fadeOtherLayers, grayOtherLayers);
                    index.Tiles.Add(tile);
                }

                AssignNeighbors(index.Tiles);
                File.WriteAllText(index.IndexPath, JsonConvert.SerializeObject(index, Formatting.Indented));

                string analyzeMessage = analyzeNow
                    ? "AnalyzeNow jest zarezerwowane dla kolejnego etapu; utworzono atlas i indeks bez automatycznej analizy VLM."
                    : "Utworzono atlas i indeks bez automatycznej analizy VLM.";
                bool usedScreenFallback = index.Tiles.Any(t => string.Equals(t.Status, "rendered_screen_fallback", StringComparison.OrdinalIgnoreCase));
                if (usedScreenFallback)
                {
                    analyzeMessage += " Uwaga: kafle wyrenderowano ekranowym fallbackiem diagnostycznym; kalibracja pixel->CAD moze byc niewiarygodna.";
                }

                return JsonConvert.SerializeObject(new
                {
                    status = "success",
                    scan_id = scanId,
                    folder = folder,
                    index_path = index.IndexPath,
                    tile_count = index.Tiles.Count,
                    scope = scope,
                    profile = profile,
                    cad_bounds = index.CadBounds,
                    message = analyzeMessage
                }, Formatting.Indented);
            }
            catch (Exception ex)
            {
                return $"BLAD SKANU METRIC VISION: {ex.Message}";
            }
        }

        private static List<string> ReadLayerNames(JToken token)
        {
            if (token is JArray arr)
            {
                return arr.Select(t => t.ToString()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            }

            string raw = token?.ToString();
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
            return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();
        }

        private static int EstimateColumnCount(List<MetricVisionBounds> tiles)
        {
            if (tiles == null || tiles.Count == 0) return 0;
            double firstY = tiles[0].MinY;
            int count = 0;
            foreach (var tile in tiles)
            {
                if (Math.Abs(tile.MinY - firstY) > 0.0001) break;
                count++;
            }
            return Math.Max(count, 1);
        }

        private static void AssignNeighbors(List<MetricVisionTile> tiles)
        {
            foreach (MetricVisionTile tile in tiles)
            {
                foreach (MetricVisionTile other in tiles)
                {
                    if (ReferenceEquals(tile, other)) continue;
                    int dr = Math.Abs(tile.Row - other.Row);
                    int dc = Math.Abs(tile.Col - other.Col);
                    if (dr <= 1 && dc <= 1 && dr + dc > 0)
                    {
                        tile.Neighbors.Add(other.TileId);
                    }
                }
            }
        }

        public List<string> Examples => new List<string>
        {
            "{ \"Scope\": \"Model\", \"TileCadSize\": 5000, \"Resolution\": 1024, \"OverlapPercent\": 10, \"Profile\": \"OcrLabels\", \"AnalyzeNow\": false, \"MaxTiles\": 64 }"
        };
    }
}
