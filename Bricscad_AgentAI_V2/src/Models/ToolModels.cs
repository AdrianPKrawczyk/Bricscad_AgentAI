using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bricscad_AgentAI_V2.Models
{
    public class ToolDefinition
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "function";

        [JsonProperty("function")]
        public FunctionSchema Function { get; set; }
    }

    public class FunctionSchema
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("parameters")]
        public ParametersSchema Parameters { get; set; }
    }

    public class ParametersSchema
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "object";

        [JsonProperty("properties")]
        public Dictionary<string, ToolParameter> Properties { get; set; }

        [JsonProperty("required")]
        public List<string> Required { get; set; }
    }

    public class ToolParameter
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("enum", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Enum { get; set; }

        [JsonProperty("properties", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, ToolParameter> Properties { get; set; }

        [JsonProperty("items", NullValueHandling = NullValueHandling.Ignore)]
        public Newtonsoft.Json.Linq.JToken Items { get; set; }
    }
}
