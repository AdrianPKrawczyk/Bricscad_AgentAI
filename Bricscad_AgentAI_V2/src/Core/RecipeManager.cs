using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Core
{
    public static class RecipeManager
    {
        private static List<AgentRecipe> _recipes = new List<AgentRecipe>();
        private static string _configPath;

        private static string ConfigPath
        {
            get
            {
                if (_configPath == null)
                {
                    _configPath = Path.Combine(
                        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                        "AgentRecipes.json"
                    );
                }
                return _configPath;
            }
        }

        static RecipeManager()
        {
            Load();
        }

        public static void Load()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    _recipes = JsonConvert.DeserializeObject<List<AgentRecipe>>(json) ?? new List<AgentRecipe>();
                }
                catch
                {
                    _recipes = new List<AgentRecipe>();
                }
            }
            else
            {
                _recipes = new List<AgentRecipe>();
            }
        }

        public static void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_recipes, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd zapisu przepisów: {ex.Message}");
            }
        }

        public static List<AgentRecipe> GetAll() => _recipes;

        public static void AddOrUpdate(AgentRecipe recipe)
        {
            var existing = _recipes.Find(r => r.Trigger.Equals(recipe.Trigger, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                _recipes.Remove(existing);
            }
            _recipes.Add(recipe);
            Save();
        }

        public static void Delete(string trigger)
        {
            var existing = _recipes.Find(r => r.Trigger.Equals(trigger, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                _recipes.Remove(existing);
                Save();
            }
        }

        public static AgentRecipe GetByTrigger(string trigger)
        {
            if (string.IsNullOrEmpty(trigger)) return null;
            string cleanTrigger = trigger.TrimStart('$');
            return _recipes.Find(r => r.Trigger.Equals(cleanTrigger, StringComparison.OrdinalIgnoreCase));
        }
    }
}
