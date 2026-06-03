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
            EnsureSupervisorPromptFile();

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

        private static void EnsureSupervisorPromptFile()
        {
            try
            {
                string supervisorPromptPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    "system_prompt_supervisor.txt"
                );
                bool needsWrite = !File.Exists(supervisorPromptPath);
                if (!needsWrite)
                {
                    try
                    {
                        string currentText = File.ReadAllText(supervisorPromptPath);
                        if (!currentText.Contains("CadGeometryProfile"))
                        {
                            needsWrite = true; // Auto-upgrade starych wersji promptu
                        }
                    }
                    catch { }
                }

                if (needsWrite)
                {
                    string defaultSupervisorPrompt = 
                        "Jesteś Głównym Menedżerem (Supervisorem) systemu Bielik V2 w BricsCAD.\n" +
                        "Twoim jedynym zadaniem jest zarządzanie i przekazywanie zadań do wyspecjalizowanych ekspertów.\n" +
                        "ZABRANIA SIĘ samodzielnego wykonywania zadań CAD, rysowania, zmieniania właściwości itp.\n" +
                        "ZABRANIA SIĘ prowadzenia luźnych konwersacji z użytkownikiem, tłumaczenia swojego toku myślenia w zwykłym tekście lub zadawania pytań o oprogramowanie CAD (użytkownik ZAWSZE pracuje w BricsCAD).\n\n" +
                        "Dostępne profile ekspertów (wybierz najbardziej optymalny do danego zadania):\n" +
                        "- CadGeometryProfile: ekspert od tworzenia i modyfikacji geometrii (linie, polilinie, kreskowania, warstwy, wymiary, teksty, właściwości obiektów, np. kolory, grubość linii).\n" +
                        "- CadBlocksProfile: ekspert od bloków i atrybutów (tworzenie bloków, wstawianie, listowanie, edycja atrybutów bloku).\n" +
                        "- CadMetadataProfile: ekspert od analityki rysunku, pomiarów, XData (czytanie właściwości, metadane XData, wyszukiwanie w rysunku, inspekcja obiektów, zrzuty ekranu CAD).\n" +
                        "- CadProfile: uniwersalny profil awaryjny (używaj tylko jeśli zadanie łączy wiele z powyższych dziedzin w jeden ciąg).\n\n" +
                        "ZASADY UŻYCIA NARZĘDZI:\n" +
                        "1. Jeśli użytkownik prosi o operację CAD, MUSISZ natychmiast wywołać narzędzie DelegateTask, dobierając właściwy TargetProfile.\n" +
                        "2. ZABRANIA SIĘ pisania odpowiedzi zwykłym tekstem przed wywołaniem narzędzia. Zwróć bezpośrednio wywołanie DelegateTask.\n" +
                        "3. Po otrzymaniu wyniku od eksperta, przedstaw go krótko i rzeczowo użytkownikowi.";
                    File.WriteAllText(supervisorPromptPath, defaultSupervisorPrompt, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd podczas generowania system_prompt_supervisor.txt: {ex.Message}");
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

            if (_config.Profiles == null)
            {
                _config.Profiles = new Dictionary<string, AgentProfileConfig>(StringComparer.OrdinalIgnoreCase);
                changed = true;
            }

            // 1. Zabezpieczenie/Synchronizacja SupervisorProfile
            if (!_config.Profiles.TryGetValue("SupervisorProfile", out var supervisorProf))
            {
                supervisorProf = new AgentProfileConfig { SystemPromptFile = "system_prompt_supervisor.txt" };
                _config.Profiles["SupervisorProfile"] = supervisorProf;
                changed = true;
            }
            var supervisorDefaults = new List<string> { "UserInput", "UserChoice", "ReadFromBlackboard", "WriteToBlackboard", "DelegateTask" };
            if (supervisorProf.AllowedTools == null)
            {
                supervisorProf.AllowedTools = new List<string>();
                changed = true;
            }
            foreach (var tool in supervisorDefaults)
            {
                if (!supervisorProf.AllowedTools.Contains(tool))
                {
                    supervisorProf.AllowedTools.Add(tool);
                    changed = true;
                }
            }

            // 2. Zabezpieczenie/Synchronizacja CadProfile
            if (!_config.Profiles.TryGetValue("CadProfile", out var cadProf))
            {
                cadProf = new AgentProfileConfig { SystemPromptFile = "system_prompt.txt", AllowedTags = new List<string> { "#cad", "#wymiary", "#xdata" } };
                _config.Profiles["CadProfile"] = cadProf;
                changed = true;
            }
            if (cadProf.SystemPromptFile != "system_prompt.txt")
            {
                cadProf.SystemPromptFile = "system_prompt.txt";
                changed = true;
            }
            var cadDefaults = new List<string> 
            { 
                "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach", 
                "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice",
                "DimensionEditTool", "ExecuteMacro", "ReadPropertyTool", "InspectEntity", "GetPropertiesTool",
                "AnalyzeSelectionTool", "ReadTextSampleTool", "TextEditTool", "ManageAnnoScales", "EditBlock",
                "EditAttributes", "ListBlocks", "InsertBlock", "CreateBlock", "ReadXData", "WriteXData",
                "FindXData", "CaptureVisionArea"
            };
            if (cadProf.AllowedTools == null)
            {
                cadProf.AllowedTools = new List<string>();
                changed = true;
            }
            foreach (var tool in cadDefaults)
            {
                if (!cadProf.AllowedTools.Contains(tool))
                {
                    cadProf.AllowedTools.Add(tool);
                    changed = true;
                }
            }

            // 3. Zabezpieczenie/Synchronizacja CadGeometryProfile
            if (!_config.Profiles.TryGetValue("CadGeometryProfile", out var geomProf))
            {
                geomProf = new AgentProfileConfig 
                { 
                    SystemPromptFile = "system_prompt.txt", 
                    AllowedTools = new List<string> { "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "DimensionEditTool", "TextEditTool" },
                    AllowedTags = new List<string> { "#cad" }
                };
                _config.Profiles["CadGeometryProfile"] = geomProf;
                changed = true;
            }

            // 4. Zabezpieczenie/Synchronizacja CadBlocksProfile
            if (!_config.Profiles.TryGetValue("CadBlocksProfile", out var blocksProf))
            {
                blocksProf = new AgentProfileConfig 
                { 
                    SystemPromptFile = "system_prompt.txt", 
                    AllowedTools = new List<string> { "ListBlocks", "InsertBlock", "CreateBlock", "EditBlock", "EditAttributes", "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice" },
                    AllowedTags = new List<string> { "#bloki" }
                };
                _config.Profiles["CadBlocksProfile"] = blocksProf;
                changed = true;
            }

            // 5. Zabezpieczenie/Synchronizacja CadMetadataProfile
            if (!_config.Profiles.TryGetValue("CadMetadataProfile", out var metadataProf))
            {
                metadataProf = new AgentProfileConfig 
                { 
                    SystemPromptFile = "system_prompt.txt", 
                    AllowedTools = new List<string> { "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool", "ReadPropertyTool", "ReadTextSampleTool", "ReadXData", "WriteXData", "FindXData", "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "CaptureVisionArea" },
                    AllowedTags = new List<string> { "#xdata" }
                };
                _config.Profiles["CadMetadataProfile"] = metadataProf;
                changed = true;
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
                SystemPromptFile = "system_prompt.txt",
                AllowedTools = new List<string> 
                { 
                    "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach", 
                    "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice",
                    "DimensionEditTool", "ExecuteMacro", "ReadPropertyTool", "InspectEntity", "GetPropertiesTool",
                    "AnalyzeSelectionTool", "ReadTextSampleTool", "TextEditTool", "ManageAnnoScales", "EditBlock",
                    "EditAttributes", "ListBlocks", "InsertBlock", "CreateBlock", "ReadXData", "WriteXData",
                    "FindXData", "CaptureVisionArea"
                },
                AllowedTags = new List<string> { "#cad", "#wymiary", "#xdata" }
            };

            _config.Profiles["CadGeometryProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = "system_prompt.txt",
                AllowedTools = new List<string> { "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "DimensionEditTool", "TextEditTool" },
                AllowedTags = new List<string> { "#cad" }
            };

            _config.Profiles["CadBlocksProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = "system_prompt.txt",
                AllowedTools = new List<string> { "ListBlocks", "InsertBlock", "CreateBlock", "EditBlock", "EditAttributes", "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice" },
                AllowedTags = new List<string> { "#bloki" }
            };

            _config.Profiles["CadMetadataProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = "system_prompt.txt",
                AllowedTools = new List<string> { "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool", "ReadPropertyTool", "ReadTextSampleTool", "ReadXData", "WriteXData", "FindXData", "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "CaptureVisionArea" },
                AllowedTags = new List<string> { "#xdata" }
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
