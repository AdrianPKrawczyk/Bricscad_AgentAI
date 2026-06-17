using System;
using System.Collections.Generic;

namespace Bricscad_AgentAI_V2.Models.Session
{
    public class ChatSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Description { get; set; } = "Nowa sesja";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public Dictionary<string, string> Blackboard { get; set; } = new Dictionary<string, string>();
        public List<VisionImageContext> VisionImages { get; set; } = new List<VisionImageContext>();
    }

    public class VisionImageContext
    {
        public string ImageId { get; set; }
        public string SourceLabel { get; set; }
        public string OriginalPath { get; set; }
        public string CachedPath { get; set; }
        public string MimeType { get; set; }
        public string Sha256 { get; set; }
        public long FileSizeBytes { get; set; }
        public int OriginalWidth { get; set; }
        public int OriginalHeight { get; set; }
        public int MaxPixelsUsed { get; set; }
        public string OcrTilingMode { get; set; }
        public int TileMaxDim { get; set; }
        public int TileOverlap { get; set; }
        public int TileRows { get; set; }
        public int TileColumns { get; set; }
        public int TileCount { get; set; }
        public List<VisionImageTileContext> Tiles { get; set; } = new List<VisionImageTileContext>();
        public string ProviderName { get; set; }
        public string ModelName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public List<VisionOcrObservation> OcrHistory { get; set; } = new List<VisionOcrObservation>();
    }

    public class VisionImageTileContext
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public string FileName { get; set; }
        public string CachedPath { get; set; }
        public int SourceX { get; set; }
        public int SourceY { get; set; }
        public int SourceWidth { get; set; }
        public int SourceHeight { get; set; }
    }

    public class VisionOcrObservation
    {
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Prompt { get; set; }
        public string Result { get; set; }
        public bool Success { get; set; }
        public string Error { get; set; }
        public string ProviderName { get; set; }
        public string ModelName { get; set; }
    }
}
