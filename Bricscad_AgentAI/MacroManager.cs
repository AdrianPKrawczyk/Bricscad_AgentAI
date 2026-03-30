using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Bricscad.ApplicationServices;

namespace BricsCAD_Agent
{
    public class MacroItem
    {
        public string Name { get; set; }
        public string Tag { get; set; }
        public override string ToString() => Name;
    }

    public static class MacroManager
    {
        // Ścieżka do pliku, który zapamięta ustawienia użytkownika
        public static string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BricsCAD_Agent", "MacroConfig.txt");

        private static string _globalMacrosPath = null;
        public static string GlobalMacrosPath
        {
            get
            {
                if (_globalMacrosPath == null)
                {
                    if (File.Exists(ConfigPath))
                    {
                        _globalMacrosPath = File.ReadAllText(ConfigPath).Trim();
                    }
                    else
                    {
                        // Domyślna ścieżka przy pierwszym uruchomieniu
                        _globalMacrosPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BricsCAD_Agent", "GlobalMacros.jsonl");
                    }
                }
                return _globalMacrosPath;
            }
            set
            {
                _globalMacrosPath = value;
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                File.WriteAllText(ConfigPath, value); // Zapisujemy preferencję
            }
        }

        public static string GetLocalMacrosPath(Document doc)
        {
            if (doc == null || string.IsNullOrEmpty(doc.Database.Filename)) return null;
            return Path.ChangeExtension(doc.Database.Filename, ".jsonl");
        }

        public static List<MacroItem> LoadMacros(string path)
        {
            var list = new List<MacroItem>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return list;

            string[] lines = File.ReadAllLines(path);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string name = "Nieznane Makro";
                string tag = "";

                // TRYB 1: Format Makra (zapisane komendą AGENT_SAVE_MACRO)
                Match mNameA = Regex.Match(line, @"\""Name\""\s*:\s*\""(.*?)\""");
                Match mTagA = Regex.Match(line, @"\""Tag\""\s*:\s*\""(.*?)\""");

                if (mNameA.Success && mTagA.Success)
                {
                    name = mNameA.Groups[1].Value;
                    tag = mTagA.Groups[1].Value;
                }
                // TRYB 2: Format Treningowy AI (Złote Standardy z Agent_Training_Data.jsonl)
                else if (line.Contains("\"messages\""))
                {
                    // Jako nazwę makra bierzemy pierwsze polecenie od użytkownika
                    Match mUser = Regex.Match(line, @"\""role\""\s*:\s*\""user\""\s*,\s*\""content\""\s*:\s*\""(.*?)\""");
                    if (mUser.Success) name = mUser.Groups[1].Value;

                    // Jako tag do wykonania zbieramy wszystkie odpowiedzi asystenta
                    MatchCollection mAssistants = Regex.Matches(line, @"\""role\""\s*:\s*\""assistant\""\s*,\s*\""content\""\s*:\s*\""(.*?)\""");
                    List<string> tags = new List<string>();
                    foreach (Match m in mAssistants) tags.Add(m.Groups[1].Value);

                    tag = string.Join(" ", tags);
                }
                else
                {
                    continue; // Pusta lub uszkodzona linia
                }

                // Oczyszczamy stringi ze znaków ucieczki JSON-a (\")
                name = name.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", " ");
                tag = tag.Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\n", "\n");

                list.Add(new MacroItem { Name = name, Tag = tag });
            }
            return list;
        }

        public static void SaveMacro(string path, string name, string tag)
        {
            if (string.IsNullOrEmpty(path)) return;
            string safeName = Komendy.SafeJson(name);
            string safeTag = Komendy.SafeJson(tag);
            string jsonLine = $"{{\"Name\": \"{safeName}\", \"Tag\": \"{safeTag}\"}}\n";
            File.AppendAllText(path, jsonLine);
        }

        // NOWA METODA: Zapisuje całą listę makr na nowo (używane przy edycji i usuwaniu)
        public static void SaveAllMacros(string path, List<MacroItem> macros)
        {
            if (string.IsNullOrEmpty(path)) return;
            List<string> lines = new List<string>();
            foreach (var m in macros)
            {
                string safeName = Komendy.SafeJson(m.Name);
                string safeTag = Komendy.SafeJson(m.Tag);
                lines.Add($"{{\"Name\": \"{safeName}\", \"Tag\": \"{safeTag}\"}}");
            }
            File.WriteAllLines(path, lines);
        }
    }
}