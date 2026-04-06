using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace Bricscad_AgentAI_V2.Core
{
    public class ToolSettings
    {
        public bool IsCore { get; set; }
        public string Tags { get; set; } // Rozdzielane przecinkami, np. "#bloki, #architektura"
    }

    /// <summary>
    /// Zarządza dynamiczną konfiguracją narzędzi (IsCore, Tagi) zapisaną w JSON.
    /// Zapobiega twardemu kodowaniu tagów wewnątrz klas IToolV2.
    /// </summary>
    public static class ToolConfigManager
    {
        private static string ConfigPath => Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), 
            "tools_config.json"
        );

        private static Dictionary<string, ToolSettings> _settings = new Dictionary<string, ToolSettings>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Inicjalizuje konfigurację. Jeśli plik nie istnieje, generuje domyślny 
        /// na podstawie zarejestrowanych narzędzi.
        /// </summary>
        public static void Initialize(IEnumerable<IToolV2> registeredTools)
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    _settings = JsonConvert.DeserializeObject<Dictionary<string, ToolSettings>>(json) 
                                ?? new Dictionary<string, ToolSettings>(StringComparer.OrdinalIgnoreCase);
                    
                    // Uzupełnij o ewentualne nowe narzędzia, których nie ma w JSON
                    SyncWithTools(registeredTools);
                }
                catch
                {
                    GenerateDefaultConfig(registeredTools);
                }
            }
            else
            {
                GenerateDefaultConfig(registeredTools);
            }
        }

        private static void SyncWithTools(IEnumerable<IToolV2> registeredTools)
        {
            bool changed = false;
            foreach (var tool in registeredTools)
            {
                string name = tool.GetType().Name;
                if (!_settings.ContainsKey(name))
                {
                    _settings[name] = new ToolSettings { IsCore = false, Tags = "" };
                    changed = true;
                }
            }
            if (changed) SaveConfig();
        }

        private static void GenerateDefaultConfig(IEnumerable<IToolV2> registeredTools)
        {
            _settings.Clear();
            var coreTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "CreateObjectTool", "ForeachTool", "SelectEntitiesTool", 
                "ModifyPropertiesTool", "RequestAdditionalToolsTool", "GetPropertiesTool",
                "UserInputTool", "UserChoiceTool"
            };

            foreach (var tool in registeredTools)
            {
                string name = tool.GetType().Name;
                _settings[name] = new ToolSettings 
                { 
                    IsCore = coreTools.Contains(name),
                    Tags = "" // Na początku puste, użytkownik uzupełni w UI
                };
            }
            SaveConfig();
        }

        public static void SaveConfig()
        {
            string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }

        public static Dictionary<string, ToolSettings> GetAllSettings() => _settings;

        public static void UpdateSettings(Dictionary<string, ToolSettings> newSettings)
        {
            _settings = newSettings;
            SaveConfig();
        }

        /// <summary>
        /// Sprawdza, czy narzędzie o podanej nazwie klasy powinno być aktywne 
        /// dla zestawu żądanych tagów.
        /// </summary>
        public static bool IsToolActive(string toolClassName, IEnumerable<string> requestedTags)
        {
            if (!_settings.TryGetValue(toolClassName, out var s)) return false;
            if (s.IsCore) return true;

            if (requestedTags == null || !requestedTags.Any()) return false;

            var toolTags = s.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(t => t.Trim().ToLower());

            return requestedTags.Any(rt => toolTags.Contains(rt.ToLower()));
        }

        /// <summary>
        /// Zwraca listę unikalnych tagów (spoza core) dostępnych w systemie.
        /// </summary>
        public static IEnumerable<string> GetAvailableCategories()
        {
            return _settings.Values
                .SelectMany(s => s.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .Distinct(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Zwraca listę narzędzi przypisanych do danej kategorii.
        /// </summary>
        public static IEnumerable<string> GetToolsInCategory(string category)
        {
            return _settings
                .Where(kv => kv.Value.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Any(t => t.Trim().Equals(category, StringComparison.OrdinalIgnoreCase)))
                .Select(kv => kv.Key);
        }
    }
}
