using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace Bricscad_AgentAI_V2.Core
{
    public class UISettings
    {
        public int DatasetStudioSplitterDistance { get; set; } = 180;
        public int ToolSandboxHeaderHeight { get; set; } = 350;
        public int AgentRecipeSplitterDistance { get; set; } = 250;
        public string LastDatasetFilePath { get; set; }
        public System.Collections.Generic.List<string> RecentDatasetFiles { get; set; } = new System.Collections.Generic.List<string>();
        public string CustomKnowledgePath { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> BenchmarkVisibleColumns { get; set; } = new System.Collections.Generic.List<string>();
        public string LastBenchmarkProfileName { get; set; } = string.Empty;
        public string LastSubAgentProfileName { get; set; } = string.Empty;
        public string CustomLLMConfigPath { get; set; } = string.Empty;
        public bool EnablePromptWarmup { get; set; } = true;
        public bool PromptWarmupOnAiOpen { get; set; } = true;
        public bool PromptWarmupOnSessionLoad { get; set; } = true;
        public int PromptWarmupAfterTypingIdleMs { get; set; } = 2500;
        public int VisionOcrClipboardMaxPixels { get; set; } = 2048;
        public int VisionOcrAttachmentMaxPixels { get; set; } = 2048;
        public string VisionOcrTilingMode { get; set; } = "Auto";
        public int VisionOcrTileMaxDim { get; set; } = 2100;
        public int VisionOcrTileOverlap { get; set; } = 200;
        public int VisionOcrTileMaxCount { get; set; } = 16;
        public double VisionOcrTileMaxAspectRatio { get; set; } = 2.0;
        public int VisionOcrPdfDpi { get; set; } = 300;
        public int VisionOcrPdfMaxPages { get; set; } = 3;
        public string VisionOcrPdfRendererPath { get; set; } = "pdftoppm.exe";
        public string LastVisionOcrQualityPresetName { get; set; } = string.Empty;
        public System.Collections.Generic.List<VisionOcrQualityPreset> VisionOcrQualityPresets { get; set; } = new System.Collections.Generic.List<VisionOcrQualityPreset>();

        // Workflow Settings
        public int AIStartupBehavior { get; set; } = 0; // 0 = Ładuj poprzednią, 1 = Twórz nową, 2 = Wybór manualny
        public int BricsCADStartupBehavior { get; set; } = 1; // 0 = Automatycznie uruchom agenta, 1 = Uruchomienie manualne

        // Auditor (Agent Rewident) - Filar 3
        public bool AuditorEnabled { get; set; } = false;
        public bool AuditorPrewarmEnabled { get; set; } = false;
        public bool AuditorEvidenceEnabled { get; set; } = false;

        // Auditor (Agent Rewident) - Filar 4
        public bool CircuitBreakerEnabled { get; set; } = true;
        public int CircuitBreakerThreshold { get; set; } = 3;

        // Auditor (Agent Rewident) - Filar 5 (Auto-Inject Properties)
        public bool AutoInjectPropertiesEnabled { get; set; } = true;
        public int AutoInjectSamplePercent { get; set; } = 2;

        // Filar 6: Master kill switch dla calego Agenta Rewidenta.
        // Gdy true - WSZYSTKIE mechanizmy Auditora sa wylaczone (Auto-Inject, CB,
        // rollback detection, Filar A i B). Worker dziala jak w v2.28.x.
        public bool AuditorGloballyDisabled { get; set; } = false;

        // Rewident (v2.35.0+) - Filar 3 (LLM RewidentProfile - dedykowany do mutacji DWG)
        public bool RewidentEnabled { get; set; } = false;
        public bool RewidentPrewarmEnabled { get; set; } = false;
        public bool RewidentEvidenceEnabled { get; set; } = false;

        // Rewident (v2.35.0+) - Filar 4 (Circuit Breaker - niezalezny od starego CB Auditora)
        public bool RewidentCircuitBreakerEnabled { get; set; } = true;
        public int RewidentCircuitBreakerThreshold { get; set; } = 3;

        // Rewident (v2.35.0+) - Filar 5 (Auto-Inject Properties - niezalezny od starego AutoInject)
        public bool RewidentAutoInjectPropertiesEnabled { get; set; } = true;
        public int RewidentAutoInjectSamplePercent { get; set; } = 2;

        // Rewident (v2.35.0+) - Filar 6 (master kill switch - niezalezny od starego AuditorGloballyDisabled)
        public bool RewidentGloballyDisabled { get; set; } = false;
    }

    public class VisionOcrQualityPreset
    {
        public string Name { get; set; } = string.Empty;
        public int ClipboardMaxPixels { get; set; } = 2048;
        public int AttachmentMaxPixels { get; set; } = 2048;
        public string TilingMode { get; set; } = "Auto";
        public int TileMaxDim { get; set; } = 2100;
        public int TileOverlap { get; set; } = 200;
        public int TileMaxCount { get; set; } = 16;
        public double TileMaxAspectRatio { get; set; } = 2.0;
    }

    /// <summary>
    /// Zarządza trwałymi ustawieniami interfejsu użytkownika (np. pozycje splitterów).
    /// </summary>
    public static class UISettingsManager
    {
        private static string ConfigPath => AppPaths.GetUISettingsPath();

        private static string DefaultAppDataRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Bricscad_AgentAI"
        );

        private static string DefaultConfigPath => Path.Combine(DefaultAppDataRoot, "ui_settings.json");

        private static string LegacyConfigPath => Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "ui_settings.json"
        );

        private static UISettings _settings;

        static UISettingsManager()
        {
            Load();
        }

        public static UISettings Settings
        {
            get
            {
                if (_settings == null) Load();
                return _settings;
            }
        }

        public static void Load()
        {
            // Migracja ze starej lokalizacji (obok DLL) do AppData - jednorazowa
            if (!File.Exists(ConfigPath) && File.Exists(LegacyConfigPath))
            {
                try
                {
                    AppPaths.EnsureDirectoriesExist();
                    File.Copy(LegacyConfigPath, ConfigPath, true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Nie udało się zmigrować ui_settings.json: {ex.Message}");
                }
            }

            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    _settings = JsonConvert.DeserializeObject<UISettings>(json) ?? new UISettings();
                }
                catch
                {
                    _settings = new UISettings();
                }
            }
            else
            {
                _settings = new UISettings();
            }
        }

        public static void Save()
        {
            try
            {
                AppPaths.EnsureDirectoriesExist();
                string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd zapisu ustawień UI: {ex.Message}");
            }
        }

        public static void UpdateDatasetStudioSplitter(int distance)
        {
            if (distance <= 0) return;
            Settings.DatasetStudioSplitterDistance = distance;
            Save();
        }

        public static void UpdateCustomLLMConfigPath(string path)
        {
            string oldRoot = AppPaths.GetAppDataRoot();
            string newRoot = path ?? string.Empty;

            Settings.CustomLLMConfigPath = newRoot;
            Save();

            if (!string.IsNullOrWhiteSpace(newRoot))
            {
                Directory.CreateDirectory(newRoot);
                CopyConfigFileIfExists(oldRoot, newRoot, "llm_providers.json");
                CopyConfigFileIfExists(oldRoot, newRoot, "tools_config.json");
                WriteSettingsSnapshot(Path.Combine(newRoot, "ui_settings.json"));
            }

            Directory.CreateDirectory(DefaultAppDataRoot);
            WriteSettingsSnapshot(DefaultConfigPath);
            AppPaths.InvalidateAppDataRootCache();
        }

        private static void CopyConfigFileIfExists(string sourceRoot, string targetRoot, string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourceRoot) || string.IsNullOrWhiteSpace(targetRoot)) return;
                string source = Path.Combine(sourceRoot, fileName);
                string target = Path.Combine(targetRoot, fileName);
                if (!File.Exists(source) || File.Exists(target)) return;
                File.Copy(source, target, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Nie udalo sie skopiowac {fileName}: {ex.Message}");
            }
        }

        private static void WriteSettingsSnapshot(string path)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = JsonConvert.SerializeObject(_settings, Formatting.Indented);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Nie udalo sie zapisac snapshotu ustawien UI: {ex.Message}");
            }
        }
    }
}
