using System;
using System.Collections.Generic;

namespace Bricscad_AgentAI_V2.Models
{
    public class LLMProviderConfig
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Nowy Dostawca";
        public string EndpointUrl { get; set; } = "http://localhost:1234/v1/chat/completions";
        public string ApiKey { get; set; } = "not-needed";
        public string ModelName { get; set; } = "local-model";
        public double Temperature { get; set; } = 0.2;
        public int MaxTokens { get; set; } = 4096;
        
        // Zaawansowane parametry samplingu (przydatne lokalnie dla LM Studio / Ollama)
        public double TopP { get; set; } = 0.95;
        public int TopK { get; set; } = 0;
        public double MinP { get; set; } = 0.0;
        public double RepetitionPenalty { get; set; } = 1.0;
        public string ReasoningEffort { get; set; } = "none";

        // Parametry dynamicznego ładowania modelu (LM Studio)
        public bool AutoLoadModel { get; set; } = false;
        public string GpuOffload { get; set; } = "default";
        public int LoadContextLength { get; set; } = 0;
        public int TtlSeconds { get; set; } = 0;
        public bool FlashAttention { get; set; } = false;
        public bool OffloadKvCache { get; set; } = false;
        
        // Specyficzne dla OpenRouter
        public string SiteUrl { get; set; } = "";
        public string SiteName { get; set; } = "BricsCAD Agent AI V2";
    }

    public class LLMConfig
    {
        public List<LLMProviderConfig> Providers { get; set; } = new List<LLMProviderConfig>();
        public Guid ActiveProviderId { get; set; }
    }
}
