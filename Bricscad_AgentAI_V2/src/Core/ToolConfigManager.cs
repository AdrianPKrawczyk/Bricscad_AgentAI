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
        public bool SupportsEarlyExit { get; set; }
    }

    public class AgentProfileConfig
    {
        public string SystemPromptFile { get; set; }
        public List<string> AllowedTools { get; set; } = new List<string>();
        public List<string> AllowedTags { get; set; } = new List<string>();
    }

    public class ToolConfigRoot
    {
        public Dictionary<string, ToolSettings> Tools { get; set; } = new Dictionary<string, ToolSettings>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, AgentProfileConfig> Profiles { get; set; } = new Dictionary<string, AgentProfileConfig>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Zarządza dynamiczną konfiguracją narzędzi (IsCore, Tagi) zapisaną w JSON.
    /// Zapobiega twardemu kodowaniu tagów wewnątrz klas IToolV2.
    /// </summary>
    public static class ToolConfigManager
    {
        private static ToolConfigRoot _config = new ToolConfigRoot();
        private static string _configPath;

        public static HashSet<string> SessionDynamicTags { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string ConfigPath
        {
            get
            {
                if (_configPath == null)
                {
                    _configPath = Path.Combine(
                        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                        "tools_config.json"
                    );
                }
                return _configPath;
            }
        }

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
                    // Próba deserializacji do nowego formatu
                    var root = JsonConvert.DeserializeObject<ToolConfigRoot>(json);
                    if (root != null && root.Tools != null)
                    {
                        _config = root;
                    }
                    else
                    {
                        // Fallback dla starego formatu
                        var oldSettings = JsonConvert.DeserializeObject<Dictionary<string, ToolSettings>>(json);
                        if (oldSettings != null)
                        {
                            _config.Tools = oldSettings;
                        }
                    }
                    
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
                string name = tool.GetToolSchema()?.Function?.Name ?? tool.GetType().Name;
                if (!_config.Tools.ContainsKey(name))
                {
                    _config.Tools[name] = new ToolSettings { IsCore = false, Tags = "", SupportsEarlyExit = false };
                    changed = true;
                }
            }
            if (changed) SaveConfig();
        }

        private static void GenerateDefaultConfig(IEnumerable<IToolV2> registeredTools)
        {
            _config = new ToolConfigRoot();
            var coreTools = new[] { "CreateObject", "SelectEntities", "ModifyProperties", "Foreach", "RequestAdditionalTools", "UserInput", "UserChoice", "WriteToBlackboard", "ReadFromBlackboard" };
            var earlyExitTools = new[] { "CreateObject", "ModifyProperties", "ManageLayers", "InsertBlock", "CreateBlock", "ExecuteMacro" };

            foreach (var tool in registeredTools)
            {
                var schema = tool.GetToolSchema();
                if (schema == null || schema.Function == null) continue;
                string apiName = schema.Function.Name;

                _config.Tools[apiName] = new ToolSettings
                {
                    IsCore = coreTools.Contains(apiName, StringComparer.OrdinalIgnoreCase),
                    SupportsEarlyExit = earlyExitTools.Contains(apiName, StringComparer.OrdinalIgnoreCase),
                    Tags = ""
                };
            }
            
            // Generowanie domyślnych profili
            _config.Profiles["SupervisorProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = "system_prompt_supervisor.txt",
                AllowedTools = new List<string> { "UserInput", "UserChoice", "ReadFromBlackboard", "WriteToBlackboard", "DelegateTask" },
                AllowedTags = new List<string>()
            };
            
            _config.Profiles["CadProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = "system_prompt_cad.txt",
                AllowedTools = new List<string> { "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach", "ReadFromBlackboard", "WriteToBlackboard" },
                AllowedTags = new List<string> { "#cad", "#wymiary", "#xdata" }
            };

            // BEZWZGLĘDNY ZAPIS PO WYGENEROWANIU
            SaveConfig();
        }

        public static void SaveConfig()
        {
            string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(ConfigPath, json);
        }

        public static Dictionary<string, ToolSettings> GetAllSettings() => _config.Tools;

        public static Dictionary<string, AgentProfileConfig> GetProfiles() => _config.Profiles;

        public static void UpdateSettings(Dictionary<string, ToolSettings> newSettings)
        {
            _config.Tools = newSettings;
            SaveConfig();
        }

        /// <summary>
        /// Sprawdza, czy narzędzie o podanej nazwie klasy powinno być aktywne 
        /// dla zestawu żądanych tagów.
        /// </summary>
        public static bool IsToolActive(string apiName, IEnumerable<string> requestedTags)
        {
            if (SessionDynamicTags.Contains(apiName)) return true;
            if (requestedTags != null && requestedTags.Any(rt => SessionDynamicTags.Contains(rt))) return true;

            if (!_config.Tools.TryGetValue(apiName, out var s)) return false;

            // Narzędzia Core są ZAWSZE aktywne
            if (s.IsCore) return true;

            // Jeśli to nie Core, a użytkownik poprosił o #all, ładuj wszystko
            if (requestedTags != null && requestedTags.Any(rt => rt.Equals("#all", StringComparison.OrdinalIgnoreCase))) return true;

            // Logika filtrowania tagów dla Tool Pools
            if (requestedTags == null || !requestedTags.Any()) return false;

            // ZMIANA: Aktywacja bezpośrednio po nazwie narzędzia (fallback dla braku tagów)
            if (requestedTags.Any(rt => rt.Equals(apiName, StringComparison.OrdinalIgnoreCase))) return true;
            var toolTags = s.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToLower());
            return requestedTags.Any(rt => toolTags.Contains(rt.ToLower()));
        }

        /// <summary>
        /// Zwraca listę unikalnych tagów (spoza core) dostępnych w systemie.
        /// </summary>
        public static IEnumerable<string> GetAvailableCategories()
        {
            return _config.Tools.Values
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
            return _config.Tools
                .Where(kv => kv.Value.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Any(t => t.Trim().Equals(category, StringComparison.OrdinalIgnoreCase)))
                .Select(kv => kv.Key);
        }

        public static ToolSettings GetSettings(string toolClassName)
        {
            if (_config.Tools.TryGetValue(toolClassName, out var s)) return s;
            return null;
        }
    }
}
