using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Models
{
    public class ChatMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("active_document_path", NullValueHandling = NullValueHandling.Ignore)]
        public string ActiveDocumentPath { get; set; }

        /// <summary>
        /// Treść wiadomości. Może być 'string' dla tekstu lub 'List<VisionContentPart>' dla treści multimodalnych.
        /// </summary>
        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
        public object Content { get; set; }

        [JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
        public List<ToolCall> ToolCalls { get; set; }

        [JsonProperty("tool_call_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ToolCallId { get; set; }
    }

    public class VisionContentPart
    {
        [JsonProperty("type")]
        public string Type { get; set; } // "text" lub "image_url"

        [JsonProperty("text", NullValueHandling = NullValueHandling.Ignore)]
        public string Text { get; set; }

        [JsonProperty("image_url", NullValueHandling = NullValueHandling.Ignore)]
        public VisionImageUrl ImageUrl { get; set; }
    }

    public class VisionImageUrl
    {
        [JsonProperty("url")]
        public string Url { get; set; } // Format: "data:image/jpeg;base64,{base64_string}"
    }

    public class ToolCall
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } = "function";

        [JsonProperty("function")]
        public ToolCallFunction Function { get; set; }
    }

    public class ToolCallFunction
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("arguments")]
        public string Arguments { get; set; }
    }

    public class LLMStats
    {
        public long TotalTimeMs { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens => PromptTokens + CompletionTokens;
        public double TokensPerSecond => TotalTimeMs > 0 ? (TotalTokens * 1000.0) / TotalTimeMs : 0;
    }
}
