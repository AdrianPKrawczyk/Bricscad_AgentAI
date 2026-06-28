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
        public AgentLlmBinding LlmBinding { get; set; }
        public bool IsReadOnly { get; set; } = false;
    }

    public class AgentLlmBinding
    {
        public bool UseDefaultProvider { get; set; } = true;
        public Guid? ProviderId { get; set; }
        public string ProviderNameFallback { get; set; }
        public string ModelName { get; set; }
        public bool OverridePayload { get; set; } = false;
        public double? Temperature { get; set; }
        public int? MaxTokens { get; set; }
        public double? TopP { get; set; }
        public int? TopK { get; set; }
        public double? MinP { get; set; }
        public double? RepetitionPenalty { get; set; }
        public string ReasoningEffort { get; set; }
        public bool? AutoLoadModel { get; set; }
        public int? LoadContextLength { get; set; }
        public string ContextPolicy { get; set; } = "UseLoadedIfAtLeastRequested";
    }

    public class VisionOcrBinding : AgentLlmBinding
    {
        public bool Enabled { get; set; } = false;
    }

    public class ToolConfigRoot
    {
        public Dictionary<string, ToolSettings> Tools { get; set; } = new Dictionary<string, ToolSettings>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, AgentProfileConfig> Profiles { get; set; } = new Dictionary<string, AgentProfileConfig>(StringComparer.OrdinalIgnoreCase);
        public VisionOcrBinding VisionOcrBinding { get; set; } = new VisionOcrBinding();
    }

    /// <summary>
    /// ZarzÄ…dza dynamicznÄ… konfiguracjÄ… narzÄ™dzi (IsCore, Tagi) zapisanÄ… w JSON.
    /// Zapobiega twardemu kodowaniu tagĂłw wewnÄ…trz klas IToolV2.
    /// </summary>
    public static class ToolConfigManager
    {
        private static ToolConfigRoot _config = new ToolConfigRoot();
        private static string _configPath;
        private const string SupervisorPromptFile = @"prompts\system_prompt_supervisor.txt";
        private const string CadPromptFile = @"prompts\system_prompt_cad.txt";
        private const string GeometryPromptFile = @"prompts\system_prompt_geometry.txt";
        private const string BlocksPromptFile = @"prompts\system_prompt_blocks.txt";
        private const string MetadataPromptFile = @"prompts\system_prompt_metadata.txt";
        private const string WentCadPromptFile = @"prompts\system_prompt_wentcad.txt";
        private const string MathPromptFile = @"prompts\system_prompt_math.txt";
        private const string NotesPromptFile = @"prompts\system_prompt_notes.txt";
        private const string AuditorPromptFile = @"prompts\system_prompt_auditor.txt";
        private const string RewidentPromptFile = @"prompts\system_prompt_rewident.txt";
        private const string LayoutPromptFile = @"prompts\system_prompt_layout.txt";
        private const string Modeler3DPromptFile = @"prompts\system_prompt_modeler3d.txt";
        private const string TextPromptFile = @"prompts\system_prompt_text.txt";

        public static HashSet<string> SessionDynamicTags { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private static string ConfigPath
        {
            get
            {
                if (_configPath == null)
                {
                    _configPath = AppPaths.GetToolConfigPath();
                }
                return _configPath;
            }
        }

        private static string LegacyConfigPath => Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "tools_config.json"
        );

        /// <summary>
        /// Inicjalizuje konfiguracjÄ™. JeĹ›li plik nie istnieje, generuje domyĹ›lny 
        /// na podstawie zarejestrowanych narzÄ™dzi.
        /// </summary>
        public static void Initialize(IEnumerable<IToolV2> registeredTools)
        {
            EnsureCadPromptFile();
            EnsureGeometryPromptFile();
            EnsureBlocksPromptFile();
            EnsureMetadataPromptFile();
            EnsureWentCadPromptFile();
            EnsureSupervisorPromptFile();
            EnsureMathPromptFile();
            EnsureNotesPromptFile();
            EnsureAuditorPromptFile();
            EnsureRewidentPromptFile();
            EnsureLayoutPromptFile();
            EnsureModeler3DPromptFile();
            EnsureTextPromptFile();

            MigrateLegacyConfigIfNeeded();

            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    // PrĂłba deserializacji do nowego formatu
                    var root = JsonConvert.DeserializeObject<ToolConfigRoot>(json);
                    if (root != null && root.Tools != null)
                    {
                        _config = root;
                        if (_config.VisionOcrBinding == null)
                        {
                            _config.VisionOcrBinding = new VisionOcrBinding();
                        }
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
                    
                    // UzupeĹ‚nij o ewentualne nowe narzÄ™dzia, ktĂłrych nie ma w JSON
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

        private static void MigrateLegacyConfigIfNeeded()
        {
            try
            {
                AppPaths.EnsureDirectoriesExist();
                if (File.Exists(ConfigPath) || !File.Exists(LegacyConfigPath))
                {
                    return;
                }

                File.Copy(LegacyConfigPath, ConfigPath, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Nie udalo sie zmigrowac tools_config.json do AppData: {ex.Message}");
            }
        }

        public static string GetDefaultSystemPromptFile(string profileName)
        {
            switch (profileName)
            {
                case "SupervisorProfile": return SupervisorPromptFile;
                case "CadProfile": return CadPromptFile;
                case "CadGeometryProfile": return GeometryPromptFile;
                case "CadBlocksProfile": return BlocksPromptFile;
                case "CadMetadataProfile": return MetadataPromptFile;
                case "WentCadProfile": return WentCadPromptFile;
                case "CadMathProfile": return MathPromptFile;
                case "NotesProfile": return NotesPromptFile;
                case "AuditorProfile": return AuditorPromptFile;
                case "RewidentProfile": return RewidentPromptFile;
                case "CadLayoutProfile": return LayoutPromptFile;
                case "Modeler3DProfile": return Modeler3DPromptFile;
                case "CadTextProfile": return TextPromptFile;
                default: return CadPromptFile;
            }
        }

        public static string GetRuntimePromptPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = CadPromptFile;
            }

            return Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                fileName
            );
        }

        /// <summary>
        /// Zwraca sciezke zrodlowa (developerska) pliku promptu w katalogu projektu.
        /// bin/Debug\prompts\x.txt -> cofamy sie 3 katalogi w gore i wchodzimy do resources\prompts\x.txt.
        /// Zwraca null jesli assembly nie znajduje sie w oczekiwanej lokalizacji dev (np. release install).
        /// </summary>
        public static string GetSourcePromptPath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            try
            {
                string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(assemblyDir))
                {
                    return null;
                }

                // bin/Debug\ -> project\resources\prompts\
                DirectoryInfo dir = new DirectoryInfo(assemblyDir);
                DirectoryInfo projectRoot = dir.Parent?.Parent?.Parent;
                if (projectRoot == null)
                {
                    return null;
                }

                return Path.Combine(projectRoot.FullName, "resources", "prompts", Path.GetFileName(fileName));
            }
            catch
            {
                return null;
            }
        }

        public static string LoadSystemPromptText(string fileName)
        {
            string path = GetRuntimePromptPath(fileName);
            if (File.Exists(path))
            {
                return File.ReadAllText(path, System.Text.Encoding.UTF8);
            }

            return "Jestes wyspecjalizowanym agentem systemu Bielik V2. Brak pliku promptu systemowego.";
        }

        public static string GetUserPromptOverride(string profileName)
        {
            AppPaths.EnsureDirectoriesExist();

            string path = AppPaths.GetPromptOverrideFilePath(profileName);
            if (!File.Exists(path))
            {
                return string.Empty;
            }

            return File.ReadAllText(path, System.Text.Encoding.UTF8);
        }

        public static void SaveUserPromptOverride(string profileName, string text)
        {
            AppPaths.EnsureDirectoriesExist();

            string path = AppPaths.GetPromptOverrideFilePath(profileName);
            string normalized = text ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalized))
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                return;
            }

            File.WriteAllText(path, normalized, System.Text.Encoding.UTF8);
        }

        public static string LoadEffectivePromptForProfile(string profileName)
        {
            string systemPrompt;
            if (_config.Profiles.TryGetValue(profileName, out var profile) && !string.IsNullOrWhiteSpace(profile.SystemPromptFile))
            {
                systemPrompt = LoadSystemPromptText(profile.SystemPromptFile);
            }
            else
            {
                systemPrompt = LoadSystemPromptText(GetDefaultSystemPromptFile(profileName));
            }

            string userOverride = GetUserPromptOverride(profileName).Trim();
            if (string.IsNullOrWhiteSpace(userOverride))
            {
                return systemPrompt;
            }

            return systemPrompt +
                "\n\n--- DODATKOWE WYTYCZNE UZYTKOWNIKA ---\n" +
                "Ponizsze instrukcje doprecyzowuja zachowanie profilu, ale nie uniewazniaja kontraktu narzedzi, zasad bezpieczenstwa ani ograniczen architektury.\n" +
                userOverride;
        }

        private static void EnsurePromptFile(string fileName, string fallbackContent)
        {
            try
            {
                string path = GetRuntimePromptPath(fileName);
                string directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string sourcePath = GetSourcePromptPath(fileName);
                bool sourceExists = !string.IsNullOrEmpty(sourcePath) && File.Exists(sourcePath);

                if (sourceExists)
                {
                    // Source-of-truth: resources/prompts/<plik>. Nadpisujemy runtime gdy:
                    // 1) runtime nie istnieje, lub
                    // 2) runtime jest starszy od source (np. po git pull bez clean build).
                    bool needsCopy = !File.Exists(path);
                    if (!needsCopy)
                    {
                        try
                        {
                            DateTime sourceStamp = File.GetLastWriteTimeUtc(sourcePath);
                            DateTime runtimeStamp = File.GetLastWriteTimeUtc(path);
                            needsCopy = sourceStamp > runtimeStamp;
                        }
                        catch
                        {
                            needsCopy = true;
                        }
                    }

                    if (needsCopy)
                    {
                        File.Copy(sourcePath, path, true);
                    }
                }
                else if (!File.Exists(path))
                {
                    // Brak zrodla developerskiego i brak runtime - ostateczny fallback z kodu.
                    File.WriteAllText(path, fallbackContent, System.Text.Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Blad podczas generowania pliku promptu '{fileName}': {ex.Message}");
            }
        }

        public static string GetDefaultCadPromptText()
        {
            return LoadSystemPromptText(CadPromptFile);
        }

        private static void EnsureCadPromptFile() => EnsurePromptFile(CadPromptFile, "Jestes glownym profilem CAD systemu Bielik V2. Realizuj ogolne zadania CAD narzedziami i nie wymyslaj skladni spoza kontraktu narzedzi.");

        private static void EnsureGeometryPromptFile() => EnsurePromptFile(GeometryPromptFile, "Jestes profilem geometrii CAD. Specjalizujesz sie w tworzeniu i modyfikacji geometrii, tekstow, warstw i podstawowych wlasciwosci obiektow.");

        private static void EnsureBlocksPromptFile() => EnsurePromptFile(BlocksPromptFile, "Jestes profilem blokow CAD. Specjalizujesz sie w blokach, atrybutach, definicjach blokow, wstawianiu i edycji wystapien.");

        private static void EnsureMetadataPromptFile() => EnsurePromptFile(MetadataPromptFile, "Jestes profilem metadanych CAD. Specjalizujesz sie w odczycie i analizie wlasciwosci, XData, selekcji, pomiarow i zestawien.");

        private static void EnsureWentCadPromptFile() => EnsurePromptFile(WentCadPromptFile, "Jestes profilem WentCadProfile. Zarzadzasz projektem WentCad przez plik .wentcad, NOD i XData WENTCAD_*, bez referencji do DLL WentCad.");

        private static void EnsureSupervisorPromptFile() => EnsurePromptFile(SupervisorPromptFile, "Jestes Supervisorem systemu Bielik V2. Rozpoznaj intencje uzytkownika, deleguj zadania do najlepszych profili i sam odpowiadaj tylko na luzna rozmowe oraz pytania ogolne.");

        private static void EnsureMathPromptFile() => EnsurePromptFile(MathPromptFile, "Jestes profilem CadMathProfile. Wykonujesz pelne obliczenia przez CalculateMath i nie zastepujesz wyniku zgadywaniem.");

        private static void EnsureNotesPromptFile() => EnsurePromptFile(NotesPromptFile, "Jestes profilem NotesProfile. Tworzysz czysta tresc notatek projektowych w Markdown i nie wykonujesz innych operacji.");

        private static void EnsureAuditorPromptFile() => EnsurePromptFile(AuditorPromptFile, "Jestes profilem AuditorProfile. Analizujesz, testujesz i raportujesz problemy w systemie Bielik V2, dbajac o bezpieczne testy i diagnostyke.");

        private static void EnsureRewidentPromptFile() => EnsurePromptFile(RewidentPromptFile, "Jestes profilem RewidentProfile. Walidujesz mutacje wykonane przez inne profile (CadProfile, CadBlocksProfile, CadLayoutProfile) w rysunku DWG. Masz dostep tylko do narzedzi read-only CAD (InspectEntity, GetPropertiesTool, ReadFromBlackboard, itd.) - nie mozesz nic modyfikowac.");

        private static void EnsureLayoutPromptFile() => EnsurePromptFile(LayoutPromptFile, "Jestes profilem CadLayoutProfile systemu Bielik V2. Specjalizujesz sie w zarzadzaniu arkuszami wydruku, konfiguracji strony i publikacji.");

        private static void EnsureModeler3DPromptFile() => EnsurePromptFile(Modeler3DPromptFile, "Jestes profilem Modeler3D systemu Bielik V2. Specjalizujesz sie w modelowaniu brylowym, tworzeniu prymitywow 3D oraz analizie geometrii 3D.");

        private static void EnsureTextPromptFile() => EnsurePromptFile(TextPromptFile, "Jestes profilem CadTextProfile systemu Bielik V2. Specjalizujesz sie w edycji tresci i formatowania (RTF) wolnych obiektow DBText i MText oraz w zarzadzaniu polami CAD w tekstach.");

        private static bool EnsureAllowedTools(AgentProfileConfig profile, IEnumerable<string> defaults)
        {
            bool changed = false;
            if (profile.AllowedTools == null)
            {
                profile.AllowedTools = new List<string>();
                changed = true;
            }

            foreach (var tool in defaults)
            {
                if (!profile.AllowedTools.Contains(tool))
                {
                    profile.AllowedTools.Add(tool);
                    changed = true;
                }
            }

            return changed;
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

                if (name.Equals("CaptureMetricVisionArea", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ScanMetricVisionDrawing", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("QueryVisionScanIndex", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("DiagnoseMetricVisionGraphicsSystem", StringComparison.OrdinalIgnoreCase))
                {
                    var settings = _config.Tools[name];
                    if (string.IsNullOrWhiteSpace(settings.Tags) || settings.Tags.IndexOf("#vision", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        settings.Tags = string.IsNullOrWhiteSpace(settings.Tags) ? "#vision" : settings.Tags + ", #vision";
                        changed = true;
                    }
                    if (settings.SupportsEarlyExit)
                    {
                        settings.SupportsEarlyExit = false;
                        changed = true;
                    }
                }

                if (name.Equals("ExtractRoomDataEntities", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("BatchWriteXData", StringComparison.OrdinalIgnoreCase))
                {
                    var settings = _config.Tools[name];
                    if (string.IsNullOrWhiteSpace(settings.Tags) || settings.Tags.IndexOf("#xdata", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        settings.Tags = string.IsNullOrWhiteSpace(settings.Tags) ? "#xdata, #metadata, #pokoje" : settings.Tags + ", #xdata, #metadata, #pokoje";
                        changed = true;
                    }
                    if (settings.SupportsEarlyExit)
                    {
                        settings.SupportsEarlyExit = false;
                        changed = true;
                    }
                }

                // Tagi dla narzędzi Layout/Plot (automatyczne tagowanie)
                if (name.Equals("ListLayoutsTool", StringComparison.OrdinalIgnoreCase))
                {
                    var settings = _config.Tools[name];
                    if (string.IsNullOrWhiteSpace(settings.Tags) || settings.Tags.IndexOf("#layout", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        settings.Tags = "#layout, #wydruk";
                        changed = true;
                    }
                    // ListLayoutsTool is often only the discovery step before a layout action.
                    // Let the ReAct loop decide whether to continue instead of returning a raw list as success.
                    if (settings.SupportsEarlyExit)
                    {
                        settings.SupportsEarlyExit = false;
                        changed = true;
                    }
                }

                if (name.Equals("ReadWentCadProject", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ReadWentCadRooms", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("UpdateWentCadProject", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ManageWentCadFloors", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ManageWentCadRooms", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ManageWentCadSystems", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("RecalculateWentCadBalance", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("RunWentCadCommand", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("SetWentCadFloorRegion", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ScanWentCadRooms", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("UpdateWentCadRoomByNumber", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ConfigureWentCadTestBuilding", StringComparison.OrdinalIgnoreCase))
                {
                    var settings = _config.Tools[name];
                    if (string.IsNullOrWhiteSpace(settings.Tags) || settings.Tags.IndexOf("#wentcad", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        settings.Tags = string.IsNullOrWhiteSpace(settings.Tags) ? "#wentcad, #xdata, #metadata, #pokoje" : settings.Tags + ", #wentcad, #xdata, #metadata, #pokoje";
                        changed = true;
                    }
                    if (settings.SupportsEarlyExit)
                    {
                        settings.SupportsEarlyExit = false;
                        changed = true;
                    }
                }

                if (name.Equals("ManageLayoutTool", StringComparison.OrdinalIgnoreCase))
                {
                    var settings = _config.Tools[name];
                    if (string.IsNullOrWhiteSpace(settings.Tags) || settings.Tags.IndexOf("#layout", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        settings.Tags = "#layout, #wydruk";
                        changed = true;
                    }
                    if (!settings.SupportsEarlyExit)
                    {
                        settings.SupportsEarlyExit = true;
                        changed = true;
                    }
                }

                if (name.Equals("PageSetupTool", StringComparison.OrdinalIgnoreCase))
                {
                    var settings = _config.Tools[name];
                    if (string.IsNullOrWhiteSpace(settings.Tags) || settings.Tags.IndexOf("#pagesetup", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        settings.Tags = "#layout, #wydruk, #pagesetup";
                        changed = true;
                    }
                    if (!settings.SupportsEarlyExit)
                    {
                        settings.SupportsEarlyExit = true;
                        changed = true;
                    }
                }

                if (name.Equals("ImportLayoutTemplateTool", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("BatchImportLayoutsTool", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ExportLayoutTemplateTool", StringComparison.OrdinalIgnoreCase))
                {
                    var settings = _config.Tools[name];
                    if (string.IsNullOrWhiteSpace(settings.Tags) || settings.Tags.IndexOf("#template", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        settings.Tags = "#layout, #template, #wydruk";
                        changed = true;
                    }
                }

                if (name.Equals("PlotLayoutTool", StringComparison.OrdinalIgnoreCase))
                {
                    var settings = _config.Tools[name];
                    if (string.IsNullOrWhiteSpace(settings.Tags) || settings.Tags.IndexOf("#plot", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        settings.Tags = "#wydruk, #plot";
                        changed = true;
                    }
                }

                if (name.Equals("PublishToPdfTool", StringComparison.OrdinalIgnoreCase))
                {
                    var settings = _config.Tools[name];
                    if (string.IsNullOrWhiteSpace(settings.Tags) || settings.Tags.IndexOf("#publish", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        settings.Tags = "#wydruk, #publish, #pdf";
                        changed = true;
                    }
                }

                if (name.Equals("PlotStyleTool", StringComparison.OrdinalIgnoreCase))
                {
                    var settings = _config.Tools[name];
                    if (string.IsNullOrWhiteSpace(settings.Tags) || settings.Tags.IndexOf("#ctb", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        settings.Tags = "#wydruk, #plotstyle, #ctb";
                        changed = true;
                    }
                    if (!settings.SupportsEarlyExit)
                    {
                        settings.SupportsEarlyExit = true;
                        changed = true;
                    }
                }

                if (name.Equals("ManageSheetSetTool", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("SheetSetSheetTool", StringComparison.OrdinalIgnoreCase))
                {
                    var settings = _config.Tools[name];
                    if (string.IsNullOrWhiteSpace(settings.Tags) || settings.Tags.IndexOf("#sheetset", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        settings.Tags = "#layout, #wydruk, #sheetset";
                        changed = true;
                    }
                }

                if (name.Equals("ListPlotDevicesTool", StringComparison.OrdinalIgnoreCase))
                {
                    var settings = _config.Tools[name];
                    if (string.IsNullOrWhiteSpace(settings.Tags) || settings.Tags.IndexOf("#wydruk", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        settings.Tags = "#wydruk, #ploter";
                        changed = true;
                    }
                    if (!settings.SupportsEarlyExit)
                    {
                        settings.SupportsEarlyExit = true;
                        changed = true;
                    }
                }

            // Tagi dla narzedzi obslugujacych pola CAD (Field codes)
            if (name.Equals("ReadFields", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("ManageFields", StringComparison.OrdinalIgnoreCase))
            {
                var settings = _config.Tools[name];
                if (string.IsNullOrWhiteSpace(settings.Tags) ||
                    settings.Tags.IndexOf("#fields", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    settings.Tags = "#fields, #metadata, #text";
                    changed = true;
                }
                if (settings.SupportsEarlyExit)
                {
                    settings.SupportsEarlyExit = false;
                    changed = true;
                }
            }

            // Tagi dla narzedzi edycji tekstu (DBText/MText) - profil CadTextProfile
            if (name.Equals("TextEditTool", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("ReadTextSampleTool", StringComparison.OrdinalIgnoreCase))
            {
                var settings = _config.Tools[name];
                if (string.IsNullOrWhiteSpace(settings.Tags) ||
                    settings.Tags.IndexOf("#text", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    settings.Tags = string.IsNullOrWhiteSpace(settings.Tags) ? "#text" : settings.Tags + ", #text";
                    changed = true;
                }
                if (settings.SupportsEarlyExit)
                {
                    settings.SupportsEarlyExit = false;
                    changed = true;
                }
            }

            // KROK-CadTextProfile.15: ModifyProperties rowniez obsluguje edycje
            // wlasciwosci systemowych tekstow (TextStyleName, Justify, Rotation, TextHeight,
            // Width, Position). Dodaj tag #text zeby CadTextProfile (ktory ma AllowedTags
            // zawierajacy #text) mogl automatycznie aktywowac to narzedzie.
            // ModifyProperties zostaje rowniez w AllowedTools innych profili (CadProfile,
            // CadGeometry, CadLayout) dzieki temu ze jest 'Core' w config - zostawiamy to.
            if (name.Equals("ModifyProperties", StringComparison.OrdinalIgnoreCase))
            {
                var settings = _config.Tools[name];
                if (string.IsNullOrWhiteSpace(settings.Tags))
                {
                    settings.Tags = "#cad, #text, #geometry";
                    changed = true;
                }
                else if (settings.Tags.IndexOf("#text", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    settings.Tags = settings.Tags + ", #text";
                    changed = true;
                }
            }

            // Tagi dla ReadXData/FindXData w kontekscie tekstu (kontekst, nie zapis)
            if (name.Equals("ReadXData", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("FindXData", StringComparison.OrdinalIgnoreCase))
            {
                var settings = _config.Tools[name];
                if (string.IsNullOrWhiteSpace(settings.Tags) ||
                    settings.Tags.IndexOf("#text", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    settings.Tags = string.IsNullOrWhiteSpace(settings.Tags) ? "#xdata, #text" : settings.Tags + ", #xdata, #text";
                    changed = true;
                }
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
                supervisorProf = new AgentProfileConfig { SystemPromptFile = SupervisorPromptFile };
                _config.Profiles["SupervisorProfile"] = supervisorProf;
                changed = true;
            }
            if (supervisorProf.SystemPromptFile != SupervisorPromptFile)
            {
                supervisorProf.SystemPromptFile = SupervisorPromptFile;
                changed = true;
            }
            var supervisorDefaults = new List<string> { "UserInput", "UserChoice", "ReadFromBlackboard", "WriteToBlackboard", "DelegateTask", "SearchKnowledgeBase", "SavePermanentFormula", "SaveMacro", "ExecuteFormula", "ExecuteMacro", "ReadKnowledgeTool", "SearchUnitsNetTool", "QueryDataset", "ImportCsvDataset", "ImportJsonFile", "ManageDataset", "ReadProjectFile", "WriteProjectFile", "ManageRecipes", "ReadHelp", "manage_skills", "SearchFileContent", "manage_lisps", "ExtractRoomDataEntities", "ReadViewDefinitionsTool", "read_view_definitions", "ReadWentCadProject", "ReadWentCadRooms", "UpdateWentCadProject", "ManageWentCadFloors", "ManageWentCadRooms", "UpdateWentCadRoomByNumber", "ManageWentCadSystems", "RecalculateWentCadBalance", "RunWentCadCommand", "SetWentCadFloorRegion", "ScanWentCadRooms", "ConfigureWentCadTestBuilding" };
            if (supervisorProf.AllowedTools == null)
            {
                supervisorProf.AllowedTools = new List<string>();
                changed = true;
            }
            if (EnsureAllowedTools(supervisorProf, supervisorDefaults)) changed = true;
            // Fix v2.36.0 (BUG agent_bug_01): Usun manage_lisps z AllowedTools
            // Supervisora - musi delegowac do profilow Worker, nie wywolywac
            // LISP bezposrednio. Wczesniej LLM Supervisor widzial manage_lisps w
            // arsenale i sam generowal LISP, omijajac Foreach (PRIORYTET 1 dla
            // masowej edycji w profilach Worker). Wykonujemy USUNIECIE aktywnie,
            // bo EnsureAllowedTools tylko dodaje, nie usuwa.
            if (supervisorProf.AllowedTools.Remove("manage_lisps")) changed = true;

            // 2. Zabezpieczenie/Synchronizacja CadProfile
            if (!_config.Profiles.TryGetValue("CadProfile", out var cadProf))
            {
                cadProf = new AgentProfileConfig { SystemPromptFile = CadPromptFile, AllowedTags = new List<string> { "#cad", "#wymiary", "#xdata" } };
                _config.Profiles["CadProfile"] = cadProf;
                changed = true;
            }
            if (cadProf.SystemPromptFile != CadPromptFile)
            {
                cadProf.SystemPromptFile = CadPromptFile;
                changed = true;
            }
            var cadDefaults = new List<string>
            {
                "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach",
                "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice",
                "DimensionEditTool", "ExecuteMacro", "ReadPropertyTool", "InspectEntity", "GetPropertiesTool",
                "AnalyzeSelectionTool", "ReadTextSampleTool", "TextEditTool", "ManageAnnoScales", "EditBlock",
                "EditAttributes", "ListBlocks", "InsertBlock", "CreateBlock", "ReadXData", "WriteXData",
                "FindXData", "CaptureVisionArea", "CaptureMetricVisionArea", "ScanMetricVisionDrawing", "QueryVisionScanIndex", "DiagnoseMetricVisionGraphicsSystem", "SearchKnowledgeBase", "SaveMacro", "ExecuteFormula", "ReadKnowledgeTool", "SearchUnitsNetTool", "QueryDataset", "ManageRecipes", "manage_lisps",
                "ListLayoutsTool", "ManageLayoutTool", "PageSetupTool",
                "ImportLayoutTemplateTool", "BatchImportLayoutsTool", "ExportLayoutTemplateTool",
                "ListPlotDevicesTool", "PlotLayoutTool", "PublishToPdfTool", "PlotStyleTool"
            };
            if (cadProf.AllowedTools == null)
            {
                cadProf.AllowedTools = new List<string>();
                changed = true;
            }
            if (EnsureAllowedTools(cadProf, cadDefaults)) changed = true;

            // 3. Zabezpieczenie/Synchronizacja CadGeometryProfile
            if (!_config.Profiles.TryGetValue("CadGeometryProfile", out var geomProf))
            {
                geomProf = new AgentProfileConfig 
                { 
                    SystemPromptFile = GeometryPromptFile, 
                    AllowedTools = new List<string> { "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "DimensionEditTool", "TextEditTool", "manage_lisps" },
                    AllowedTags = new List<string> { "#cad" }
                };
                _config.Profiles["CadGeometryProfile"] = geomProf;
                changed = true;
            }
            if (geomProf.SystemPromptFile != GeometryPromptFile)
            {
                geomProf.SystemPromptFile = GeometryPromptFile;
                changed = true;
            }
            if (EnsureAllowedTools(geomProf, new[] { "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "DimensionEditTool", "TextEditTool", "manage_lisps" })) changed = true;

            // 4. Zabezpieczenie/Synchronizacja CadBlocksProfile
            if (!_config.Profiles.TryGetValue("CadBlocksProfile", out var blocksProf))
            {
                blocksProf = new AgentProfileConfig 
                { 
                    SystemPromptFile = BlocksPromptFile, 
                    AllowedTools = new List<string> { "ListBlocks", "InsertBlock", "CreateBlock", "EditBlock", "EditAttributes", "SelectEntities", "Foreach", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "manage_lisps" },
                    AllowedTags = new List<string> { "#bloki" }
                };
                _config.Profiles["CadBlocksProfile"] = blocksProf;
                changed = true;
            }
            if (blocksProf.SystemPromptFile != BlocksPromptFile)
            {
                blocksProf.SystemPromptFile = BlocksPromptFile;
                changed = true;
            }
            if (EnsureAllowedTools(blocksProf, new[] { "ListBlocks", "InsertBlock", "CreateBlock", "EditBlock", "EditAttributes", "SelectEntities", "Foreach", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "manage_lisps", "ReadFields", "ManageFields" })) changed = true;

            // 4b. Zabezpieczenie/Synchronizacja CadTextProfile (edycja DBText/MText + pola CAD w tekstach)
            if (!_config.Profiles.TryGetValue("CadTextProfile", out var textProf))
            {
                textProf = new AgentProfileConfig
                {
                    SystemPromptFile = TextPromptFile,
                    AllowedTags = new List<string> { "#text", "#fields" }
                };
                _config.Profiles["CadTextProfile"] = textProf;
                changed = true;
            }
            if (textProf.SystemPromptFile != TextPromptFile)
            {
                textProf.SystemPromptFile = TextPromptFile;
                changed = true;
            }
            if (!textProf.AllowedTags.Contains("#text"))
            {
                textProf.AllowedTags.Add("#text");
                changed = true;
            }
            if (!textProf.AllowedTags.Contains("#fields"))
            {
                textProf.AllowedTags.Add("#fields");
                changed = true;
            }
            if (EnsureAllowedTools(textProf, new[] {
                "TextEditTool", "ReadTextSampleTool",
                "ReadFields", "ManageFields",
                "ReadXData", "FindXData",
                "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool", "ReadPropertyTool",
                "SelectEntities", "Foreach", "ReadFromBlackboard", "WriteToBlackboard",
                "RequestAdditionalTools", "UserInput", "UserChoice", "manage_lisps",
                // KROK-CadTextProfile.15: ModifyProperties do edycji wlasciwosci systemowych
                // tekstu (TextStyleName, Justify, Rotation, TextHeight, Width, Position).
                // Bez tego CadTextProfile nie mogl zmieniac stylu tekstu, justowania ani
                // obrotu - musial delegowac do CadGeometryProfile dla kazdej takiej zmiany.
                "ModifyProperties"
            })) changed = true;

            // 10. Zabezpieczenie/Synchronizacja Modeler3DProfile
            if (!_config.Profiles.TryGetValue("Modeler3DProfile", out var modelerProf))
            {
                modelerProf = new AgentProfileConfig 
                { 
                    SystemPromptFile = Modeler3DPromptFile, 
                    AllowedTools = new List<string> { "CreateSolid", "SelectEntities", "ModifyProperties", "ManageLayers", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice" },
                    AllowedTags = new List<string> { "#3dmodeler", "#solid" }
                };
                _config.Profiles["Modeler3DProfile"] = modelerProf;
                changed = true;
            }
            if (modelerProf.SystemPromptFile != Modeler3DPromptFile)
            {
                modelerProf.SystemPromptFile = Modeler3DPromptFile;
                changed = true;
            }
            if (EnsureAllowedTools(modelerProf, new[] { "CreateSolid", "SelectEntities", "ModifyProperties", "ManageLayers", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice" })) changed = true;

            // 5. Zabezpieczenie/Synchronizacja CadMetadataProfile
            if (!_config.Profiles.TryGetValue("CadMetadataProfile", out var metadataProf))
            {
                metadataProf = new AgentProfileConfig 
                { 
                    SystemPromptFile = MetadataPromptFile, 
                    AllowedTools = new List<string> { "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool", "ReadPropertyTool", "ReadTextSampleTool", "ReadXData", "WriteXData", "FindXData", "ExtractRoomDataEntities", "BatchWriteXData", "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "CaptureVisionArea", "CaptureMetricVisionArea", "ScanMetricVisionDrawing", "QueryVisionScanIndex", "DiagnoseMetricVisionGraphicsSystem", "manage_lisps" },
                    AllowedTags = new List<string> { "#xdata" }
                };
                _config.Profiles["CadMetadataProfile"] = metadataProf;
                changed = true;
            }
            if (metadataProf.SystemPromptFile != MetadataPromptFile)
            {
                metadataProf.SystemPromptFile = MetadataPromptFile;
                changed = true;
            }
            if (EnsureAllowedTools(metadataProf, new[] { "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool", "ReadPropertyTool", "ReadTextSampleTool", "ReadXData", "WriteXData", "FindXData", "ExtractRoomDataEntities", "BatchWriteXData", "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "CaptureVisionArea", "CaptureMetricVisionArea", "ScanMetricVisionDrawing", "QueryVisionScanIndex", "DiagnoseMetricVisionGraphicsSystem", "manage_lisps", "PlotStyleTool", "ReadFields", "ManageFields", "ReadWentCadProject", "ReadWentCadRooms", "RunWentCadCommand" })) changed = true;

            // 5b. Zabezpieczenie/Synchronizacja WentCadProfile
            if (!_config.Profiles.TryGetValue("WentCadProfile", out var wentCadProf))
            {
                wentCadProf = new AgentProfileConfig
                {
                    SystemPromptFile = WentCadPromptFile,
                    AllowedTags = new List<string> { "#wentcad", "#xdata", "#metadata", "#pokoje" }
                };
                _config.Profiles["WentCadProfile"] = wentCadProf;
                changed = true;
            }
            if (wentCadProf.SystemPromptFile != WentCadPromptFile)
            {
                wentCadProf.SystemPromptFile = WentCadPromptFile;
                changed = true;
            }
            if (wentCadProf.AllowedTags == null)
            {
                wentCadProf.AllowedTags = new List<string>();
                changed = true;
            }
            foreach (string tag in new[] { "#wentcad", "#xdata", "#metadata", "#pokoje" })
            {
                if (!wentCadProf.AllowedTags.Contains(tag))
                {
                    wentCadProf.AllowedTags.Add(tag);
                    changed = true;
                }
            }
            if (EnsureAllowedTools(wentCadProf, new[]
            {
                "ReadWentCadProject", "ReadWentCadRooms",
                "UpdateWentCadProject", "ManageWentCadFloors", "ManageWentCadRooms",
                "ManageWentCadSystems", "RecalculateWentCadBalance", "RunWentCadCommand",
                "SetWentCadFloorRegion", "ScanWentCadRooms", "UpdateWentCadRoomByNumber", "ConfigureWentCadTestBuilding",
                "ReadWentCadEnvelope", "ScanWentCadEnvelope", "UpdateWentCadWall", "UpdateWentCadWindow", "ConfigureWentCadEnvelopeTestBuilding",
                "ReadViewDefinitionsTool", "read_view_definitions",
                "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool", "ReadPropertyTool",
                "ReadXData", "FindXData", "ExtractRoomDataEntities",
                "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard",
                "RequestAdditionalTools", "UserInput", "UserChoice"
            })) changed = true;

            // 6. Zabezpieczenie/Synchronizacja CadMathProfile
            if (!_config.Profiles.TryGetValue("CadMathProfile", out var mathProf))
            {
                mathProf = new AgentProfileConfig { SystemPromptFile = MathPromptFile, AllowedTags = new List<string> { "#math", "#obliczenia" } };
                _config.Profiles["CadMathProfile"] = mathProf;
                changed = true;
            }
            if (mathProf.SystemPromptFile != MathPromptFile)
            {
                mathProf.SystemPromptFile = MathPromptFile;
                changed = true;
            }
            var mathDefaults = new List<string> { "CalculateMath", "ReadFromBlackboard", "WriteToBlackboard", "UserInput", "UserChoice", "SearchKnowledgeBase", "ExecuteFormula", "SavePermanentFormula", "ReadKnowledgeTool", "SearchUnitsNetTool", "QueryDataset", "manage_lisps" };
            if (mathProf.AllowedTools == null)
            {
                mathProf.AllowedTools = new List<string>();
                changed = true;
            }
            if (EnsureAllowedTools(mathProf, mathDefaults)) changed = true;
            if (mathProf.AllowedTools.RemoveAll(t => string.Equals(t, "CalculateRpn", StringComparison.OrdinalIgnoreCase)) > 0)
            {
                changed = true;
            }

            // 7. Zabezpieczenie/Synchronizacja NotesProfile
            if (!_config.Profiles.TryGetValue("NotesProfile", out var notesProf))
            {
                notesProf = new AgentProfileConfig { SystemPromptFile = NotesPromptFile };
                _config.Profiles["NotesProfile"] = notesProf;
                changed = true;
            }
            if (notesProf.SystemPromptFile != NotesPromptFile)
            {
                notesProf.SystemPromptFile = NotesPromptFile;
                changed = true;
            }

            // 8. Zabezpieczenie/Synchronizacja AuditorProfile
            if (!_config.Profiles.TryGetValue("AuditorProfile", out var auditorProf))
            {
                auditorProf = new AgentProfileConfig { SystemPromptFile = AuditorPromptFile };
                _config.Profiles["AuditorProfile"] = auditorProf;
                changed = true;
            }
            if (auditorProf.SystemPromptFile != AuditorPromptFile)
            {
                auditorProf.SystemPromptFile = AuditorPromptFile;
                changed = true;
            }
            var auditorDefaults = new List<string>
            {
                "ReadProjectFile", "WriteProjectFile", "UserInput", "UserChoice",
                "SaveMacro", "SavePermanentFormula", "ManageRecipes", "manage_skills",
                "ListSourceFiles", "ReadSourceCode", "RunToolTest", "WriteQAReport",
                "DelegateTaskToAntigravity", "SearchFileContent", "manage_lisps",
                "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool",
                "ReadPropertyTool", "ReadXData", "FindXData", "ReadTextSampleTool",
                "ListBlocks", "ReadSelectedBlockInfo", "ReadFromBlackboard"
            };
            if (auditorProf.AllowedTools == null)
            {
                auditorProf.AllowedTools = new List<string>();
                changed = true;
            }
            if (EnsureAllowedTools(auditorProf, auditorDefaults)) changed = true;
            if (!auditorProf.IsReadOnly)
            {
                auditorProf.IsReadOnly = true;
                changed = true;
            }

            // 8b. Zabezpieczenie/Synchronizacja RewidentProfile (nowy v2.35.0 - subagent mutation audit)
            if (!_config.Profiles.TryGetValue("RewidentProfile", out var rewidentProf))
            {
                rewidentProf = new AgentProfileConfig { SystemPromptFile = RewidentPromptFile };
                _config.Profiles["RewidentProfile"] = rewidentProf;
                changed = true;
            }
            if (rewidentProf.SystemPromptFile != RewidentPromptFile)
            {
                rewidentProf.SystemPromptFile = RewidentPromptFile;
                changed = true;
            }
            var rewidentDefaults = new List<string>
            {
                "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool",
                "ReadPropertyTool", "ReadXData", "FindXData", "ReadTextSampleTool",
                "ListBlocks", "ReadSelectedBlockInfo", "ReadFromBlackboard"
            };
            if (rewidentProf.AllowedTools == null)
            {
                rewidentProf.AllowedTools = new List<string>();
                changed = true;
            }
            if (EnsureAllowedTools(rewidentProf, rewidentDefaults)) changed = true;
            if (!rewidentProf.IsReadOnly)
            {
                rewidentProf.IsReadOnly = true;
                changed = true;
            }

            // 9. Zabezpieczenie/Synchronizacja CadLayoutProfile
            if (!_config.Profiles.TryGetValue("CadLayoutProfile", out var layoutProf))
            {
                layoutProf = new AgentProfileConfig
                {
                    SystemPromptFile = LayoutPromptFile,
                    AllowedTools = new List<string>(),
                    AllowedTags = new List<string> { "#layout", "#wydruk", "#plotstyle", "#sheetset" }
                };
                _config.Profiles["CadLayoutProfile"] = layoutProf;
                changed = true;
            }
            if (layoutProf.SystemPromptFile != LayoutPromptFile)
            {
                layoutProf.SystemPromptFile = LayoutPromptFile;
                changed = true;
            }
            if (!layoutProf.AllowedTags.Contains("#sheetset"))
            {
                layoutProf.AllowedTags.Add("#sheetset");
                changed = true;
            }
            var layoutDefaults = new List<string>
            {
                "ListLayoutsTool", "ManageLayoutTool", "PageSetupTool",
                "ImportLayoutTemplateTool", "BatchImportLayoutsTool", "ExportLayoutTemplateTool",
                "ListPlotDevicesTool", "PlotLayoutTool", "PublishToPdfTool", "PlotStyleTool",
                "ManageSheetSetTool", "SheetSetSheetTool",
                "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard",
                "RequestAdditionalTools", "UserInput", "UserChoice", "manage_lisps",
                "Foreach", "ManageViewportsTool", "ReadViewDefinitionsTool"
            };
            
            if (layoutProf.AllowedTools == null)
            {
                layoutProf.AllowedTools = new List<string>();
                changed = true;
            }
            if (EnsureAllowedTools(layoutProf, layoutDefaults)) changed = true;

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
            
            _config.Profiles["SupervisorProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = SupervisorPromptFile,
                // Fix v2.36.0 (BUG agent_bug_01): manage_lisps USUNIETY z AllowedTools
                // Supervisora. Supervisor MA DELEGOWAC zadania do profilow Worker
                // (CadGeometryProfile/CadProfile), a nie wywolywac LISP bezposrednio.
                // Wczesniej LLM Supervisor widzial manage_lisps w arsenale i sam
                // generowal LISP - to omijalo Foreach (PRIORYTET 1 dla masowej edycji)
                // i Chain of Evidence (Rewident sledzi mutacje Foreach, nie LISP).
                // Jesli user WPROST prosi o LISP - deleguj do CadGeometryProfile
                // z instrukcja "uzyj manage_lisps w profilu Worker".
                AllowedTools = new List<string> { "UserInput", "UserChoice", "ReadFromBlackboard", "WriteToBlackboard", "DelegateTask", "SearchKnowledgeBase", "SavePermanentFormula", "SaveMacro", "ExecuteFormula", "ExecuteMacro", "ReadKnowledgeTool", "SearchUnitsNetTool", "QueryDataset", "ImportCsvDataset", "ImportJsonFile", "ManageDataset", "ReadProjectFile", "WriteProjectFile", "ManageRecipes", "ReadHelp", "manage_skills", "SearchFileContent", "read_view_definitions", "ReadWentCadProject", "ReadWentCadRooms", "UpdateWentCadProject", "ManageWentCadFloors", "ManageWentCadRooms", "UpdateWentCadRoomByNumber", "ManageWentCadSystems", "RecalculateWentCadBalance", "RunWentCadCommand", "SetWentCadFloorRegion", "ScanWentCadRooms", "ConfigureWentCadTestBuilding", "ReadWentCadEnvelope", "ScanWentCadEnvelope", "UpdateWentCadWall", "UpdateWentCadWindow", "ConfigureWentCadEnvelopeTestBuilding" },
                AllowedTags = new List<string>()
            };
            
            _config.Profiles["CadProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = CadPromptFile,
                AllowedTools = new List<string>
                {
                    "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach",
                    "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice",
                    "DimensionEditTool", "ExecuteMacro", "ReadPropertyTool", "InspectEntity", "GetPropertiesTool",
                    "AnalyzeSelectionTool", "ReadTextSampleTool", "TextEditTool", "ManageAnnoScales", "EditBlock",
                    "EditAttributes", "ListBlocks", "InsertBlock", "CreateBlock", "ReadXData", "WriteXData",
                    "FindXData", "CaptureVisionArea", "CaptureMetricVisionArea", "ScanMetricVisionDrawing", "QueryVisionScanIndex", "DiagnoseMetricVisionGraphicsSystem", "SearchKnowledgeBase", "SaveMacro", "ExecuteFormula", "ReadKnowledgeTool", "SearchUnitsNetTool", "QueryDataset", "ManageRecipes",
                    "ListLayoutsTool", "ManageLayoutTool", "PageSetupTool",
                    "ImportLayoutTemplateTool", "BatchImportLayoutsTool", "ExportLayoutTemplateTool",
                    "PlotLayoutTool", "PublishToPdfTool", "PlotStyleTool"
                },
                AllowedTags = new List<string> { "#cad", "#wymiary", "#xdata", "#layout", "#wydruk" }
            };

            _config.Profiles["CadGeometryProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = GeometryPromptFile,
                AllowedTools = new List<string> { "CreateObject", "SelectEntities", "ModifyProperties", "ManageLayers", "Foreach", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "DimensionEditTool", "TextEditTool" },
                AllowedTags = new List<string> { "#cad" }
            };

            _config.Profiles["CadBlocksProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = BlocksPromptFile,
                AllowedTools = new List<string> { "ListBlocks", "InsertBlock", "CreateBlock", "EditBlock", "EditAttributes", "SelectEntities", "Foreach", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "ReadFields", "ManageFields" },
                AllowedTags = new List<string> { "#bloki" }
            };

            _config.Profiles["CadMetadataProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = MetadataPromptFile,
                AllowedTools = new List<string> { "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool", "ReadPropertyTool", "ReadTextSampleTool", "ReadXData", "WriteXData", "FindXData", "ExtractRoomDataEntities", "BatchWriteXData", "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard", "RequestAdditionalTools", "UserInput", "UserChoice", "CaptureVisionArea", "CaptureMetricVisionArea", "ScanMetricVisionDrawing", "QueryVisionScanIndex", "DiagnoseMetricVisionGraphicsSystem", "ReadFields", "ManageFields", "ReadWentCadProject", "ReadWentCadRooms", "RunWentCadCommand" },
                AllowedTags = new List<string> { "#xdata", "#fields" }
            };

            _config.Profiles["WentCadProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = WentCadPromptFile,
                AllowedTools = new List<string>
                {
                    "ReadWentCadProject", "ReadWentCadRooms",
                    "UpdateWentCadProject", "ManageWentCadFloors", "ManageWentCadRooms",
                    "ManageWentCadSystems", "RecalculateWentCadBalance", "RunWentCadCommand",
                    "SetWentCadFloorRegion", "ScanWentCadRooms", "UpdateWentCadRoomByNumber", "ConfigureWentCadTestBuilding",
                    "ReadWentCadEnvelope", "ScanWentCadEnvelope", "UpdateWentCadWall", "UpdateWentCadWindow", "ConfigureWentCadEnvelopeTestBuilding",
                    "ReadViewDefinitionsTool", "read_view_definitions",
                    "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool", "ReadPropertyTool",
                    "ReadXData", "FindXData", "ExtractRoomDataEntities",
                    "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard",
                    "RequestAdditionalTools", "UserInput", "UserChoice"
                },
                AllowedTags = new List<string> { "#wentcad", "#xdata", "#metadata", "#pokoje" }
            };

            _config.Profiles["CadMathProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = MathPromptFile,
                AllowedTools = new List<string> { "CalculateMath", "ReadFromBlackboard", "WriteToBlackboard", "UserInput", "UserChoice", "SearchKnowledgeBase", "ExecuteFormula", "SavePermanentFormula", "ReadKnowledgeTool", "SearchUnitsNetTool", "QueryDataset" },
                AllowedTags = new List<string> { "#math", "#obliczenia" }
            };

            _config.Profiles["NotesProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = NotesPromptFile,
                AllowedTools = new List<string>(),
                AllowedTags = new List<string>()
            };

            _config.Profiles["AuditorProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = AuditorPromptFile,
                AllowedTools = new List<string> { "ReadProjectFile", "WriteProjectFile", "UserInput", "UserChoice", "SaveMacro", "SavePermanentFormula", "ManageRecipes", "manage_skills", "ListSourceFiles", "ReadSourceCode", "RunToolTest", "WriteQAReport", "DelegateTaskToAntigravity", "SearchFileContent" },
                AllowedTags = new List<string>()
            };

            _config.Profiles["CadLayoutProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = LayoutPromptFile,
                AllowedTools = new List<string>
                {
                    "ListLayoutsTool", "ManageLayoutTool", "PageSetupTool",
                    "ImportLayoutTemplateTool", "ExportLayoutTemplateTool",
                    "PlotLayoutTool", "PublishToPdfTool", "PlotStyleTool",
                    "ManageSheetSetTool", "SheetSetSheetTool",
                    "SelectEntities", "ReadFromBlackboard", "WriteToBlackboard",
                    "RequestAdditionalTools", "UserInput", "UserChoice", "manage_lisps",
                    "Foreach"
                },
                AllowedTags = new List<string> { "#layout", "#wydruk", "#plotstyle", "#sheetset" }
            };

            _config.Profiles["CadTextProfile"] = new AgentProfileConfig
            {
                SystemPromptFile = TextPromptFile,
                AllowedTools = new List<string>
                {
                    "TextEditTool", "ReadTextSampleTool",
                    "ReadFields", "ManageFields",
                    "ReadXData", "FindXData",
                    "InspectEntity", "GetPropertiesTool", "AnalyzeSelectionTool", "ReadPropertyTool",
                    "SelectEntities", "Foreach", "ReadFromBlackboard", "WriteToBlackboard",
                    "RequestAdditionalTools", "UserInput", "UserChoice", "manage_lisps"
                },
                AllowedTags = new List<string> { "#text", "#fields" }
            };

            // BEZWZGLÄDNY ZAPIS PO WYGENEROWANIU
            SaveConfig();
        }

        public static void SaveConfig()
        {
            AppPaths.EnsureDirectoriesExist();
            string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
            File.WriteAllText(ConfigPath, json, System.Text.Encoding.UTF8);
        }

        public static Dictionary<string, ToolSettings> GetAllSettings() => _config.Tools;

        public static Dictionary<string, AgentProfileConfig> GetProfiles() => _config.Profiles;

        public static VisionOcrBinding GetVisionOcrBinding()
        {
            if (_config.VisionOcrBinding == null)
            {
                _config.VisionOcrBinding = new VisionOcrBinding();
            }
            return _config.VisionOcrBinding;
        }

        public static void UpdateVisionOcrBinding(VisionOcrBinding binding)
        {
            _config.VisionOcrBinding = binding ?? new VisionOcrBinding();
            SaveConfig();
        }

        public static AgentLlmBinding GetAgentLlmBinding(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName)) return null;
            if (_config?.Profiles == null) return null;
            return _config.Profiles.TryGetValue(profileName, out var profile) ? profile.LlmBinding : null;
        }

        public static void UpdateSettings(Dictionary<string, ToolSettings> newSettings)
        {
            _config.Tools = newSettings;
            SaveConfig();
        }

        public static void UpdateAgentProfile(string profileName, string promptFile, List<string> allowedTools)
        {
            if (_config.Profiles.TryGetValue(profileName, out var profile))
            {
                profile.SystemPromptFile = GetDefaultSystemPromptFile(profileName);
                profile.AllowedTools = allowedTools;
                SaveConfig();
            }
        }

        public static void UpdateAgentProfile(string profileName, string promptFile, List<string> allowedTools, AgentLlmBinding llmBinding)
        {
            if (_config.Profiles.TryGetValue(profileName, out var profile))
            {
                profile.SystemPromptFile = GetDefaultSystemPromptFile(profileName);
                profile.AllowedTools = allowedTools;
                profile.LlmBinding = llmBinding;
                SaveConfig();
            }
        }

        /// <summary>
        /// Sprawdza, czy narzÄ™dzie o podanej nazwie klasy powinno byÄ‡ aktywne 
        /// dla zestawu ĹĽÄ…danych tagĂłw.
        /// </summary>
        public static bool IsToolActive(string apiName, IEnumerable<string> requestedTags)
        {
            if (SessionDynamicTags.Contains(apiName)) return true;
            if (requestedTags != null && requestedTags.Any(rt => SessionDynamicTags.Contains(rt))) return true;

            if (!_config.Tools.TryGetValue(apiName, out var s)) return false;

            // NarzÄ™dzia Core sÄ… ZAWSZE aktywne
            if (s.IsCore) return true;

            // JeĹ›li to nie Core, a uĹĽytkownik poprosiĹ‚ o #all, Ĺ‚aduj wszystko
            if (requestedTags != null && requestedTags.Any(rt => rt.Equals("#all", StringComparison.OrdinalIgnoreCase))) return true;

            // Logika filtrowania tagĂłw dla Tool Pools
            if (requestedTags == null || !requestedTags.Any()) return false;

            // ZMIANA: Aktywacja bezpoĹ›rednio po nazwie narzÄ™dzia (fallback dla braku tagĂłw)
            if (requestedTags.Any(rt => rt.Equals(apiName, StringComparison.OrdinalIgnoreCase))) return true;
            var toolTags = s.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim().ToLower());
            return requestedTags.Any(rt => toolTags.Contains(rt.ToLower()));
        }

        /// <summary>
        /// Zwraca listÄ™ unikalnych tagĂłw (spoza core) dostÄ™pnych w systemie.
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
        /// Zwraca listÄ™ narzÄ™dzi przypisanych do danej kategorii.
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


