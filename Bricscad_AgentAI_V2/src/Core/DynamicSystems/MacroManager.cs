using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Bricscad_AgentAI_V2.Core;

namespace Bricscad_AgentAI_V2.Core.DynamicSystems
{
    public class MacroStep
    {
        [JsonProperty("actionType")]
        public string ActionType { get; set; }

        [JsonProperty("targetId")]
        public string TargetId { get; set; }

        [JsonProperty("parameters")]
        public JObject Parameters { get; set; } = new JObject();
    }

    public class MacroDefinition
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; } = "Uncategorized";

        [JsonProperty("tags")]
        public List<string> Tags { get; set; } = new List<string>();

        [JsonProperty("steps")]
        public List<MacroStep> Steps { get; set; } = new List<MacroStep>();
    }

    public class MacroManager
    {
        private static readonly Dictionary<string, MacroDefinition> _macros = new Dictionary<string, MacroDefinition>(StringComparer.OrdinalIgnoreCase);

        public static void LoadAllMacros()
        {
            _macros.Clear();
            try
            {
                string macrosPath = AppPaths.GetMacrosPath();
                AppPaths.EnsureDirectoriesExist();

                var files = Directory.GetFiles(macrosPath, "*.json", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    string id = Path.GetFileNameWithoutExtension(file);
                    try
                    {
                        string json = File.ReadAllText(file);
                        var macro = JsonConvert.DeserializeObject<MacroDefinition>(json);
                        
                        if (macro != null)
                        {
                            if (string.IsNullOrEmpty(macro.Id)) macro.Id = id;
                            _macros[id] = macro;
                            BielikLogger.LogInfo($"[Makra] Załadowano pomyślnie makro: {id}");
                        }
                    }
                    catch (Exception ex)
                    {
                        BielikLogger.LogError($"[Makra] Błąd ładowania makra '{id}'", ex);
                    }
                }
                
                BielikLogger.LogInfo($"[Makra] Załadowano {_macros.Count} makr z dysku.");
            }
            catch (Exception ex)
            {
                BielikLogger.LogError($"[Makra] Błąd podczas przygotowania ścieżki do makr: {ex.Message}", ex);
            }
        }

        public static MacroDefinition GetMacro(string id)
        {
            if (_macros.TryGetValue(id, out var macro))
            {
                return macro;
            }
            throw new KeyNotFoundException($"Nie znaleziono makra o ID: {id}");
        }

        public static IEnumerable<string> GetAvailableMacros()
        {
            return _macros.Keys;
        }

        public static void ExecuteMacro(string id, IExecutionContext context)
        {
            var macro = GetMacro(id);
            BielikLogger.LogInfo($"[Makra] Uruchamianie makra: {id} - {macro.Description}");
            
            foreach (var step in macro.Steps)
            {
                BielikLogger.LogInfo($"[Makra] Krok: {step.ActionType}");
                
                try
                {
                    // Delegacja do ToolOrchestrator - zachowujemy kompatybilność z ekosystemem V2
                    string result = ToolOrchestrator.Instance.ExecuteTool(step.ActionType, step.Parameters, context);
                    BielikLogger.LogInfo($"[Makra] Wynik kroku {step.ActionType}: {result}");
                }
                catch (Exception ex)
                {
                    BielikLogger.LogError($"[Makra] Błąd podczas wykonywania kroku '{step.ActionType}' w makrze '{id}'", ex);
                    throw; // Przerywamy makro w razie błędu krytycznego narzędzia
                }
            }
            
            BielikLogger.LogInfo($"[Makra] Zakończono wykonywanie makra: {id}");
        }

        public static bool DeleteMacro(string id)
        {
            string macrosPath = AppPaths.GetMacrosPath();
            var files = Directory.GetFiles(macrosPath, $"{id}.json", SearchOption.AllDirectories);

            if (files.Length > 0)
            {
                File.Delete(files[0]);
                _macros.Remove(id);
                BielikLogger.LogInfo($"[Makra] Usunięto makro: {id}");
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void SaveMacro(string id, string jsonContent)
        {
            // Sprawdzenie poprawności JSON przed zapisem
            MacroDefinition macro;
            try
            {
                macro = JsonConvert.DeserializeObject<MacroDefinition>(jsonContent);
                if (macro == null) throw new InvalidOperationException("Zdeserializowany obiekt jest pusty.");
                if (string.IsNullOrEmpty(macro.Id)) macro.Id = id;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"BŁĄD PARSOWANIA JSON: {ex.Message}");
            }

            string macrosPath = AppPaths.GetMacrosPath();
            string targetDir = macrosPath;

            if (!string.IsNullOrWhiteSpace(macro.Category) && macro.Category != "Uncategorized")
            {
                targetDir = Path.Combine(macrosPath, macro.Category);
            }
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            // Jeśli plik już istniał w innym miejscu, to go przenieś/usuń
            var existingFiles = Directory.GetFiles(macrosPath, $"{id}.json", SearchOption.AllDirectories);
            if (existingFiles.Length > 0)
            {
                string oldDir = Path.GetDirectoryName(existingFiles[0]);
                if (oldDir != targetDir)
                {
                    File.Delete(existingFiles[0]);
                }
            }

            string filePath = Path.Combine(targetDir, $"{id}.json");
            File.WriteAllText(filePath, JsonConvert.SerializeObject(macro, Formatting.Indented));

            // Dodaj/Zaktualizuj w pamięci
            _macros[id] = macro;
            BielikLogger.LogInfo($"[Makra] Zapisano makro: {id}");
        }
    }
}
