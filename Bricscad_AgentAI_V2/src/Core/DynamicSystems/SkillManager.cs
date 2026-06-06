using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Core.DynamicSystems
{
    public static class SkillManager
    {
        private static readonly Dictionary<string, AgentSkill> _skills = new Dictionary<string, AgentSkill>(StringComparer.OrdinalIgnoreCase);

        public static void LoadAllSkills()
        {
            _skills.Clear();
            try
            {
                AppPaths.EnsureDirectoriesExist();
                string skillsPath = AppPaths.GetSkillsPath();

                var files = Directory.GetFiles(skillsPath, "*.md", SearchOption.AllDirectories);

                foreach (var file in files)
                {
                    string id = Path.GetFileNameWithoutExtension(file);
                    try
                    {
                        string content = File.ReadAllText(file);
                        var skill = ParseSkill(id, content);
                        
                        if (skill != null)
                        {
                            _skills[id] = skill;
                        }
                    }
                    catch (Exception ex)
                    {
                        BielikLogger.LogError($"[Skille] Błąd ładowania skilla '{id}'", ex);
                    }
                }
                
                BielikLogger.LogInfo($"[Skille] Załadowano {_skills.Count} skilli z dysku.");
            }
            catch (Exception ex)
            {
                BielikLogger.LogError($"[Skille] Błąd podczas ładowania katalogu ze skillami: {ex.Message}", ex);
            }
        }

        public static AgentSkill ParseSkill(string id, string fileContent)
        {
            var skill = new AgentSkill { Id = id, Content = fileContent };
            
            // Proste parsowanie YAML Frontmatter
            // Szukamy bloku --- na początku pliku
            var match = Regex.Match(fileContent, @"^---\s*[\r\n]+(.*?)\s*[\r\n]+---\s*[\r\n]+", RegexOptions.Singleline);
            if (match.Success)
            {
                string frontmatter = match.Groups[1].Value;
                
                // Parsowanie kluczy i wartości
                var lines = frontmatter.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ':' }, 2);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim().ToLower();
                        string value = parts[1].Trim();

                        if (key == "category") skill.Category = value;
                        else if (key == "description") skill.Description = value;
                        else if (key == "tags")
                        {
                            // Zakładamy prosty format: [tag1, tag2] lub zwykły tekst
                            value = value.Trim('[', ']');
                            skill.Tags = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(t => t.Trim())
                                              .ToList();
                        }
                    }
                }
            }

            return skill;
        }

        public static string GenerateSkillFileContent(AgentSkill skill)
        {
            // Odtwarzanie frontmattera z obiektu
            string tags = skill.Tags != null && skill.Tags.Any() ? $"[{string.Join(", ", skill.Tags)}]" : "[]";
            string frontmatter = $"---\n" +
                                 $"category: {skill.Category}\n" +
                                 $"description: {skill.Description}\n" +
                                 $"tags: {tags}\n" +
                                 $"---\n\n";

            // Zastępujemy ewentualnie stary frontmatter nowym, lub dodajemy na początek
            string body = skill.Content ?? "";
            var match = Regex.Match(body, @"^---\s*[\r\n]+(.*?)\s*[\r\n]+---\s*[\r\n]+", RegexOptions.Singleline);
            if (match.Success)
            {
                body = body.Substring(match.Length);
            }

            return frontmatter + body;
        }

        public static void SaveSkill(AgentSkill skill)
        {
            if (string.IsNullOrWhiteSpace(skill.Id)) throw new ArgumentException("Id skilla nie może być puste.");

            string skillsPath = AppPaths.GetSkillsPath();
            string filePath = Path.Combine(skillsPath, $"{skill.Id}.md");

            string fullContent = GenerateSkillFileContent(skill);
            File.WriteAllText(filePath, fullContent);
            
            // Uaktualniamy Content i słownik w locie
            skill.Content = fullContent;
            _skills[skill.Id] = skill;
            
            BielikLogger.LogInfo($"[Skille] Zapisano skill: {skill.Id}");
        }

        public static AgentSkill GetSkill(string id)
        {
            if (_skills.TryGetValue(id, out var skill))
            {
                return skill;
            }
            throw new KeyNotFoundException($"Nie znaleziono skilla o ID: {id}");
        }

        public static IEnumerable<AgentSkill> GetAvailableSkills()
        {
            return _skills.Values;
        }

        public static void DeleteSkill(string id)
        {
            if (_skills.ContainsKey(id))
            {
                string path = Path.Combine(AppPaths.GetSkillsPath(), $"{id}.md");
                if (File.Exists(path))
                {
                    File.Delete(path);
                    BielikLogger.LogInfo($"[Skille] Usunięto z dysku plik skilla: {id}.md");
                }
                _skills.Remove(id);
            }
        }
    }
}
