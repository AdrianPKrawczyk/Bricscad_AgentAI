using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Core
{
    public static class AutotestRegistry
    {
        private static Dictionary<string, AutotestRecord> _registry = new Dictionary<string, AutotestRecord>(StringComparer.OrdinalIgnoreCase);
        private static string _registryPath;

        static AutotestRegistry()
        {
            string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string rootDir = Path.GetFullPath(Path.Combine(baseDir, "..", ".."));
            string autoTestDir = Path.Combine(rootDir, "Autotesty");
            
            if (!Directory.Exists(autoTestDir))
            {
                Directory.CreateDirectory(autoTestDir);
            }

            _registryPath = Path.Combine(autoTestDir, "test_registry.json");
            Load();
        }

        public static void Load()
        {
            if (File.Exists(_registryPath))
            {
                try
                {
                    string json = File.ReadAllText(_registryPath);
                    var list = JsonConvert.DeserializeObject<List<AutotestRecord>>(json);
                    _registry.Clear();
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            _registry[item.ToolName] = item;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n[ERROR] Błąd odczytu test_registry.json: {ex.Message}");
                }
            }
        }

        public static void Save()
        {
            try
            {
                var list = new List<AutotestRecord>(_registry.Values);
                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(_registryPath, json);
            }
            catch (Exception ex)
            {
                Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n[ERROR] Błąd zapisu test_registry.json: {ex.Message}");
            }
        }

        public static AutotestRecord GetRecord(string toolName)
        {
            if (_registry.TryGetValue(toolName, out var record))
            {
                return record;
            }
            return new AutotestRecord
            {
                ToolName = toolName,
                StaticTestsCount = 0,
                InteractiveTestsCount = 0,
                LastStatus = "-",
                LastTestDate = DateTime.MinValue
            };
        }

        public static void UpdateRecord(string toolName, bool isStatic, bool passed)
        {
            var record = GetRecord(toolName);
            
            if (isStatic)
                record.StaticTestsCount++;
            else
                record.InteractiveTestsCount++;

            record.LastStatus = passed ? "Sukces (Passed)" : "Wymaga Poprawki (Failed)";
            record.LastTestDate = DateTime.Now;

            _registry[toolName] = record;
            Save();
        }
        
        public static void MarkAsFixed(string toolName)
        {
            var record = GetRecord(toolName);
            record.LastStatus = "Poprawione (Fixed)";
            _registry[toolName] = record;
            Save();
        }
    }
}
