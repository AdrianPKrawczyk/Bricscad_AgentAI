using System;
using System.IO;

namespace Bricscad_AgentAI_V2.Core
{
    public static class AppPaths
    {
        public static string GetCustomKnowledgePath()
        {
            string customPath = UISettingsManager.Settings.CustomKnowledgePath;
            if (!string.IsNullOrWhiteSpace(customPath) && Directory.Exists(customPath))
            {
                return customPath;
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
        }
    }
}
