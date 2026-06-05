using System.Collections.Generic;
using Newtonsoft.Json;

namespace Bricscad_AgentAI_V2.Models
{
    public class FormulaInputDef
    {
        [JsonProperty("key")]
        public string Key { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("expectedType")]
        public string ExpectedType { get; set; }
    }

    public class FormulaMetadata
    {
        [JsonProperty("formulaId")]
        public string FormulaId { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; } = "Uncategorized";

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [JsonProperty("requiredInputs")]
        public List<FormulaInputDef> RequiredInputs { get; set; } = new List<FormulaInputDef>();

        [JsonProperty("outputDescription")]
        public string OutputDescription { get; set; }
    }
}
