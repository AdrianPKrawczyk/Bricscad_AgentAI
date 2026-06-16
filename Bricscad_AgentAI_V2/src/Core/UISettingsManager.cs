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
        public string CustomLLMConfigPath { get; set; } = string.Empty;
        public bool EnablePromptWarmup { get; set; } = true;
        public bool PromptWarmupOnAiOpen { get; set; } = true;
        public bool PromptWarmupOnSessionLoad { get; set; } = true;
        public int PromptWarmupAfterTypingIdleMs { get; set; } = 2500;

        // Workflow Settings
        public int AIStartupBehavior { get; set; } = 0; // 0 = Ładuj poprzednią, 1 = Twórz nową, 2 = Wybór manualny
        public int BricsCADStartupBehavior { get; set; } = 1; // 0 = Automatycznie uruchom agenta, 1 = Uruchomienie manualne
    }

    /// <summary>
    /// Zarządza trwałymi ustawieniami interfejsu użytkownika (np. pozycje splitterów).
    /// </summary>
    public static class UISettingsManager
    {
        private static string ConfigPath => AppPaths.GetUISettingsPath();

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
            Settings.CustomLLMConfigPath = path ?? string.Empty;
            Save();
            AppPaths.InvalidateAppDataRootCache();
        }
    }
}
