using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bricscad_AgentAI_V2.Models
{
    public class MetricVisionBounds
    {
        [JsonProperty("min_x")]
        public double MinX { get; set; }

        [JsonProperty("min_y")]
        public double MinY { get; set; }

        [JsonProperty("max_x")]
        public double MaxX { get; set; }

        [JsonProperty("max_y")]
        public double MaxY { get; set; }
    }

    public class MetricVisionTile
    {
        [JsonProperty("tile_id")]
        public string TileId { get; set; }

        [JsonProperty("row")]
        public int Row { get; set; }

        [JsonProperty("col")]
        public int Col { get; set; }

        [JsonProperty("image_path")]
        public string ImagePath { get; set; }

        [JsonProperty("cad_bounds")]
        public MetricVisionBounds CadBounds { get; set; }

        [JsonProperty("resolution")]
        public int Resolution { get; set; }

        [JsonProperty("cad_units_per_pixel_x")]
        public double CadUnitsPerPixelX { get; set; }

        [JsonProperty("cad_units_per_pixel_y")]
        public double CadUnitsPerPixelY { get; set; }

        [JsonProperty("unit_type")]
        public string UnitType { get; set; }

        [JsonProperty("profile")]
        public string Profile { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("neighbors")]
        public List<string> Neighbors { get; set; } = new List<string>();
    }

    public class MetricVisionObservation
    {
        [JsonProperty("tile_id")]
        public string TileId { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("pixel_box")]
        public double[] PixelBox { get; set; }

        [JsonProperty("cad_point")]
        public double[] CadPoint { get; set; }

        [JsonProperty("confidence")]
        public double Confidence { get; set; }
    }

    public class MetricVisionScanIndex
    {
        [JsonProperty("scan_id")]
        public string ScanId { get; set; }

        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonProperty("drawing")]
        public string Drawing { get; set; }

        [JsonProperty("scope")]
        public string Scope { get; set; }

        [JsonProperty("profile")]
        public string Profile { get; set; }

        [JsonProperty("folder")]
        public string Folder { get; set; }

        [JsonProperty("index_path")]
        public string IndexPath { get; set; }

        [JsonProperty("cad_bounds")]
        public MetricVisionBounds CadBounds { get; set; }

        [JsonProperty("tiles")]
        public List<MetricVisionTile> Tiles { get; set; } = new List<MetricVisionTile>();

        [JsonProperty("observations")]
        public List<MetricVisionObservation> Observations { get; set; } = new List<MetricVisionObservation>();
    }
}
