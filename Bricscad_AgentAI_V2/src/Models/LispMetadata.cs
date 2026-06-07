using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bricscad_AgentAI_V2.Models
{
    public class LispMetadata
    {
        [JsonProperty("lispId")]
        public string LispId { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; } = "Uncategorized";

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();
    }
}
