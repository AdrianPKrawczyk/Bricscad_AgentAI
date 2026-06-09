using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Core
{
    public static class LLMConfigManager
    {
        private static LLMConfig _config;
        private static string ConfigPath => Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), 
            "llm_providers.json"
        );

        public static event Action OnConfigChanged;

        static LLMConfigManager()
        {
            Load();
        }

        public static LLMConfig Current => _config;

        public static void Load()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    _config = JsonConvert.DeserializeObject<LLMConfig>(json) ?? GenerateDefaultConfig();
                }
                catch
                {
                    _config = GenerateDefaultConfig();
                }
            }
            else
            {
                _config = GenerateDefaultConfig();
                Save();
            }

            // Fallback: Ensure active provider is valid
            if (_config.Providers.Count > 0 && !_config.Providers.Any(p => p.Id == _config.ActiveProviderId))
            {
                _config.ActiveProviderId = _config.Providers.First().Id;
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
                OnConfigChanged?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd zapisu ustawień LLM: {ex.Message}");
            }
        }

        public static LLMProviderConfig GetActiveProvider()
        {
            return _config?.Providers?.FirstOrDefault(p => p.Id == _config.ActiveProviderId) 
                ?? GenerateDefaultConfig().Providers.First();
        }

        /// <summary>
        /// Ustawia aktywnego providera po Id i zapisuje konfigurację.
        /// </summary>
        public static void SetActiveProvider(Guid id)
        {
            if (_config == null) return;
            if (!_config.Providers.Any(p => p.Id == id)) return;
            if (_config.ActiveProviderId == id) return;
            _config.ActiveProviderId = id;
            Save();
        }

        /// <summary>
        /// Modyfikuje aktywnego providera przez mutator i zapisuje konfigurację.
        /// Mutator powinien zwrócić zmieniony obiekt (lub ten sam, jeśli nie było zmian).
        /// </summary>
        public static void UpdateActiveProvider(Func<LLMProviderConfig, LLMProviderConfig> mutator)
        {
            if (_config == null || mutator == null) return;
            var active = _config.Providers.FirstOrDefault(p => p.Id == _config.ActiveProviderId);
            if (active == null) return;
            var updated = mutator(active);
            if (updated == null) return;
            int idx = _config.Providers.IndexOf(active);
            _config.Providers[idx] = updated;
            Save();
        }

        private static LLMConfig GenerateDefaultConfig()
        {
            var config = new LLMConfig();

            var lmStudio = new LLMProviderConfig
            {
                Id = Guid.NewGuid(),
                Name = "LM Studio (Lokalny)",
                EndpointUrl = "http://localhost:1234/v1/chat/completions",
                ApiKey = "not-needed",
                ModelName = "local-model",
                Temperature = 0.2,
                MaxTokens = 4096
            };

            var ollama = new LLMProviderConfig
            {
                Id = Guid.NewGuid(),
                Name = "Ollama (Lokalny)",
                EndpointUrl = "http://localhost:11434/v1/chat/completions",
                ApiKey = "ollama",
                ModelName = "llama3",
                Temperature = 0.2,
                MaxTokens = 4096
            };

            var openRouter = new LLMProviderConfig
            {
                Id = Guid.NewGuid(),
                Name = "OpenRouter",
                EndpointUrl = "https://openrouter.ai/api/v1/chat/completions",
                ApiKey = "",
                ModelName = "google/gemini-pro",
                Temperature = 0.2,
                MaxTokens = 4096,
                SiteName = "BricsCAD Agent AI V2"
            };

            config.Providers.Add(lmStudio);
            config.Providers.Add(ollama);
            config.Providers.Add(openRouter);

            config.ActiveProviderId = lmStudio.Id;

            return config;
        }
    }
}
