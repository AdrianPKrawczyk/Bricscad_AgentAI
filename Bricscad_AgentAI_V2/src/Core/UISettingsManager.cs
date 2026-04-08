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
        public int AgentRecipeSplitterDistance { get; set; } = 200;
        public string LastDatasetFilePath { get; set; }
        public System.Collections.Generic.List<string> RecentDatasetFiles { get; set; } = new System.Collections.Generic.List<string>();
    }

    /// <summary>
    /// Zarządza trwałymi ustawieniami interfejsu użytkownika (np. pozycje splitterów).
    /// </summary>
    public static class UISettingsManager
    {
        private static string ConfigPath => Path.Combine(
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
    }
}
