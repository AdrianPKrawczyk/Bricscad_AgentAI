using System;
using System.Collections.Generic;
using System.IO;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class CaptureMetricVisionAreaTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "CaptureMetricVisionArea",
                    Description = "Renderuje wskazany bbox CAD do skalibrowanego obrazu PNG i zwraca metadane pixel->CAD dla analizy Vision.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "MinX", new ToolParameter { Type = "number", Description = "Minimalna wspolrzedna X obszaru CAD." } },
                            { "MinY", new ToolParameter { Type = "number", Description = "Minimalna wspolrzedna Y obszaru CAD." } },
                            { "MaxX", new ToolParameter { Type = "number", Description = "Maksymalna wspolrzedna X obszaru CAD." } },
                            { "MaxY", new ToolParameter { Type = "number", Description = "Maksymalna wspolrzedna Y obszaru CAD." } },
                            { "Resolution", new ToolParameter { Type = "integer", Description = "Rozdzielczosc obrazu kwadratowego w pikselach. Domyslnie 1024, zakres 256-4096." } },
                            { "Profile", new ToolParameter { Type = "string", Enum = new List<string> { "OcrLabels", "Symbols", "Diagnostics" }, Description = "Profil analizy docelowej. Domyslnie OcrLabels." } },
                            { "AddOverlay", new ToolParameter { Type = "boolean", Description = "Czy nalozyc delikatna siatke i opis kalibracyjny na obraz. Domyslnie true." } },
                            { "UseExperimentalOffscreen", new ToolParameter { Type = "boolean", Description = "Uruchamia eksperymentalny backend BricsCAD GraphicsSystem off-screen. Domyslnie false, bo w V22 moze wywolywac bledy runtime." } },
                            { "AllowScreenFallback", new ToolParameter { Type = "boolean", Description = "Diagnostycznie pozwala uzyc zrzutu ekranu CopyFromScreen. Domyslnie false, bo fallback moze lapac UI i nie jest metrycznie wiarygodny." } }
                        },
                        Required = new List<string> { "MinX", "MinY", "MaxX", "MaxY" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            try
            {
                MetricVisionBounds bounds = new MetricVisionBounds
                {
                    MinX = ReadDouble(args, "MinX"),
                    MinY = ReadDouble(args, "MinY"),
                    MaxX = ReadDouble(args, "MaxX"),
                    MaxY = ReadDouble(args, "MaxY")
                };

                int resolution = args["Resolution"]?.Value<int?>() ?? 1024;
                string profile = MetricVisionRenderer.NormalizeProfile(args["Profile"]?.ToString());
                bool addOverlay = args["AddOverlay"]?.Value<bool?>() ?? true;
                bool useExperimentalOffscreen = args["UseExperimentalOffscreen"]?.Value<bool?>() ?? false;
                bool allowScreenFallback = args["AllowScreenFallback"]?.Value<bool?>() ?? false;

                string scanId = MetricVisionRenderer.CreateScanId();
                string folder = Path.Combine(AppPaths.GetVisionScansPath(), scanId);
                string tilesFolder = Path.Combine(folder, "tiles");
                string imagePath = Path.Combine(tilesFolder, "tile_r000_c000.png");

                MetricVisionTile tile = MetricVisionRenderer.RenderTile(doc, bounds, imagePath, resolution, profile, addOverlay, useExperimentalOffscreen, allowScreenFallback, "r000_c000", 0, 0);

                MetricVisionScanIndex index = new MetricVisionScanIndex
                {
                    ScanId = scanId,
                    CreatedAt = DateTime.Now,
                    Drawing = doc?.Name,
                    Scope = "Area",
                    Profile = profile,
                    Folder = folder,
                    IndexPath = Path.Combine(folder, "VisionScanIndex.json"),
                    CadBounds = tile.CadBounds
                };
                index.Tiles.Add(tile);

                Directory.CreateDirectory(folder);
                File.WriteAllText(index.IndexPath, JsonConvert.SerializeObject(index, Formatting.Indented));

                bool usedScreenFallback = string.Equals(tile.Status, "rendered_screen_fallback", StringComparison.OrdinalIgnoreCase);
                JObject payload = JObject.FromObject(new
                {
                    status = "success",
                    scan_id = scanId,
                    message = usedScreenFallback ? "Obszar wyrenderowany ekranowym fallbackiem diagnostycznym; kalibracja pixel->CAD moze byc niewiarygodna." : "Obszar wyrenderowany metrycznie przez off-screen GraphicsSystem.",
                    index_path = index.IndexPath,
                    tile = tile
                });

                return MetricVisionRenderer.MetricVisionToken + payload.ToString(Formatting.None);
            }
            catch (Exception ex)
            {
                return $"BLAD METRIC VISION: {ex.Message}";
            }
        }

        private static double ReadDouble(JObject args, string name)
        {
            if (args[name] == null) throw new ArgumentException($"Brak wymaganego parametru {name}.");
            return args[name].Value<double>();
        }

        public List<string> Examples => new List<string>
        {
            "{ \"MinX\": 0, \"MinY\": 0, \"MaxX\": 5000, \"MaxY\": 5000, \"Resolution\": 1024, \"Profile\": \"OcrLabels\", \"AddOverlay\": true }"
        };
    }
}
