using System;

namespace Bricscad_AgentAI_V2.Models
{
    public class LlmModelDescriptor
    {
        public string Id { get; set; }
        public string ModelKey { get; set; }
        public string DisplayName { get; set; }
        public string Quantization { get; set; }
        public string ParamsString { get; set; }
        public long SizeBytes { get; set; }
        public bool IsLoaded { get; set; }
        public int LoadedContextLength { get; set; }
        public string Architecture { get; set; }
        public string Publisher { get; set; }

        public string SizeLabel
        {
            get
            {
                if (SizeBytes <= 0) return null;
                double gb = SizeBytes / 1e9;
                if (gb >= 1.0) return $"~{gb:F1} GB";
                double mb = SizeBytes / 1e6;
                return $"~{mb:F0} MB";
            }
        }

        public string FormatStatusLine()
        {
            if (string.IsNullOrEmpty(Id)) return null;

            var name = !string.IsNullOrEmpty(DisplayName) ? DisplayName : Id;
            var parts = new System.Collections.Generic.List<string>();

            if (!string.IsNullOrEmpty(ParamsString))
                parts.Add(ParamsString);
            if (!string.IsNullOrEmpty(Quantization))
                parts.Add(Quantization);
            var size = SizeLabel;
            if (!string.IsNullOrEmpty(size))
                parts.Add(size);
            if (IsLoaded && LoadedContextLength > 0)
                parts.Add($"ctx {LoadedContextLength}");

            string details = parts.Count > 0 ? " • " + string.Join(" • ", parts) : "";
            return $"● {name}{details}";
        }
    }
}
