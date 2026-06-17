using System;
using System.IO;
using Newtonsoft.Json;

namespace Bricscad_AgentAI_V2.Core
{
    public static class AppPaths
    {
        private static string _appDataRootCache;
        private static readonly object _cacheLock = new object();

        /// <summary>
        /// Bezpieczne pobranie ścieżki AppData z fallbackiem w razie problemów z UISettingsManager (cykliczna zależność).
        /// </summary>
        private static string GetDefaultAppDataRoot()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Bricscad_AgentAI");
        }

        /// <summary>
        /// Bezpośredni odczyt pliku ui_settings.json z domyślnej lokalizacji (legacy obok DLL lub AppData).
        /// Unika cyklicznej zależności z UISettingsManager podczas inicjalizacji.
        /// </summary>
        private static string ReadCustomLLMConfigPathDirect()
        {
            try
            {
                string[] candidates = new[]
                {
                    Path.Combine(GetDefaultAppDataRoot(), "ui_settings.json"),
                    Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "ui_settings.json")
                };
                foreach (string path in candidates)
                {
                    string normalized = Path.GetFullPath(path);
                    if (File.Exists(normalized))
                    {
                        try
                        {
                            string json = File.ReadAllText(normalized);
                            var settings = JsonConvert.DeserializeObject<UISettings>(json);
                            if (settings != null && !string.IsNullOrWhiteSpace(settings.CustomLLMConfigPath))
                            {
                                return settings.CustomLLMConfigPath;
                            }
                        }
                        catch
                        {
                            // Cichy fallback - niepoprawny JSON
                        }
                    }
                }
            }
            catch
            {
                // Wszelkie błędy - zwróć null (użyjemy domyślnej ścieżki)
            }
            return null;
        }

        public static string GetCustomKnowledgePath()
        {
            try
            {
                string customPath = UISettingsManager.Settings.CustomKnowledgePath;
                if (!string.IsNullOrWhiteSpace(customPath) && Directory.Exists(customPath))
                {
                    return customPath;
                }
            }
            catch
            {
                // UISettingsManager może być jeszcze nie zainicjalizowany
            }

            // Domyślna ścieżka (Fallback)
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Bricscad_AgentAI",
                "CustomKnowledge");
        }

        public static string GetFormulasPath()
        {
            return Path.Combine(GetCustomKnowledgePath(), "Formulas");
        }

        public static string GetMacrosPath()
        {
            return Path.Combine(GetCustomKnowledgePath(), "Macros");
        }

        public static string GetDatasetsPath()
        {
            return Path.Combine(GetCustomKnowledgePath(), "Datasets");
        }
        
        public static string GetRecipesPath()
        {
            return Path.Combine(GetCustomKnowledgePath(), "Recipes");
        }

        public static string GetSkillsPath()
        {
            return Path.Combine(GetCustomKnowledgePath(), "Skills");
        }
        
        public static string GetLispPath()
        {
            return Path.Combine(GetCustomKnowledgePath(), "Lisp");
        }

        public static string GetPromptOverridesPath()
        {
            return Path.Combine(GetCustomKnowledgePath(), "PromptOverrides");
        }

        public static string GetVisionScansPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Bricscad_AgentAI",
                "VisionScans");
        }

        public static string GetSessionImagesPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Bricscad_AgentAI",
                "SessionImages");
        }

        public static string GetVisionOcrTestRunsPath()
        {
            return Path.Combine(GetAppDataRoot(), "VisionOcrTestRuns");
        }

        /// <summary>
        /// Folder bazowy w AppData dla plików konfiguracyjnych wtyczki.
        /// Zmienny przez użytkownika (CustomLLMConfigPath) - pozwala na synchronizację między komputerami (OneDrive/Dropbox).
        /// Bezpieczne w kontekście cyklicznej zależności z UISettingsManager: przy pierwszym wywołaniu
        /// pomija UISettingsManager (co mogłoby wywołać rekurencyjne ładowanie) i czyta ui_settings.json
        /// bezpośrednio z dysku. Po pełnej inicjalizacji UISettingsManager kolejne wywołania korzystają z niego.
        /// </summary>
        public static string GetAppDataRoot()
        {
            if (_appDataRootCache != null) return _appDataRootCache;

            lock (_cacheLock)
            {
                if (_appDataRootCache != null) return _appDataRootCache;

                string defaultPath = GetDefaultAppDataRoot();

                // Strategia 1: Bezpośredni odczyt JSON (omija UISettingsManager → nie powoduje cyklu)
                string directPath = ReadCustomLLMConfigPathDirect();
                if (!string.IsNullOrWhiteSpace(directPath) && Directory.Exists(directPath))
                {
                    _appDataRootCache = directPath;
                    return _appDataRootCache;
                }

                _appDataRootCache = defaultPath;
                return _appDataRootCache;
            }
        }

        /// <summary>
        /// Reset cache ścieżki AppData - wywoływane po zmianie CustomLLMConfigPath w UI.
        /// </summary>
        public static void InvalidateAppDataRootCache()
        {
            _appDataRootCache = null;
        }

        /// <summary>
        /// Ścieżka do llm_providers.json. Przetrwa kompilacje (AppData).
        /// </summary>
        public static string GetLLMConfigPath()
        {
            return Path.Combine(GetAppDataRoot(), "llm_providers.json");
        }

        /// <summary>
        /// Ścieżka do ui_settings.json. Przetrwa kompilacje (AppData).
        /// </summary>
        public static string GetUISettingsPath()
        {
            return Path.Combine(GetAppDataRoot(), "ui_settings.json");
        }

        public static string GetToolConfigPath()
        {
            return Path.Combine(GetAppDataRoot(), "tools_config.json");
        }

        public static string GetPromptOverrideFilePath(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName))
            {
                profileName = "default";
            }

            string safeName = profileName
                .Replace("\\", "_")
                .Replace("/", "_")
                .Replace(":", "_")
                .Replace("*", "_")
                .Replace("?", "_")
                .Replace("\"", "_")
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace("|", "_");

            return Path.Combine(GetPromptOverridesPath(), $"{safeName}.txt");
        }

        public static void EnsureDirectoriesExist()
        {
            string formulas = GetFormulasPath();
            if (!Directory.Exists(formulas))
            {
                Directory.CreateDirectory(formulas);
            }
            string macros = GetMacrosPath();
            if (!Directory.Exists(macros))
            {
                Directory.CreateDirectory(macros);
            }
            string datasets = GetDatasetsPath();
            if (!Directory.Exists(datasets))
            {
                Directory.CreateDirectory(datasets);
            }
            string recipes = GetRecipesPath();
            if (!Directory.Exists(recipes))
            {
                Directory.CreateDirectory(recipes);
            }
            string skills = GetSkillsPath();
            if (!Directory.Exists(skills))
            {
                Directory.CreateDirectory(skills);
            }
            string lisp = GetLispPath();
            if (!Directory.Exists(lisp))
            {
                Directory.CreateDirectory(lisp);
            }
            string promptOverrides = GetPromptOverridesPath();
            if (!Directory.Exists(promptOverrides))
            {
                Directory.CreateDirectory(promptOverrides);
            }
            string visionScans = GetVisionScansPath();
            if (!Directory.Exists(visionScans))
            {
                Directory.CreateDirectory(visionScans);
            }
            string sessionImages = GetSessionImagesPath();
            if (!Directory.Exists(sessionImages))
            {
                Directory.CreateDirectory(sessionImages);
            }
            string visionOcrTestRuns = GetVisionOcrTestRunsPath();
            if (!Directory.Exists(visionOcrTestRuns))
            {
                Directory.CreateDirectory(visionOcrTestRuns);
            }
            string appRoot = GetAppDataRoot();
            if (!Directory.Exists(appRoot))
            {
                Directory.CreateDirectory(appRoot);
            }
        }
    }
}
