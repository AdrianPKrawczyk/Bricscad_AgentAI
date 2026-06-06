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
        private static readonly Dictionary<string, AgentRecipe> _recipes = new Dictionary<string, AgentRecipe>(StringComparer.OrdinalIgnoreCase);

        static RecipeManager()
        {
            Load();
        }

        public static void Load()
        {
            _recipes.Clear();
            
            // 1. Zapewniamy istnienie folderu
            AppPaths.EnsureDirectoriesExist();
            string recipesPath = AppPaths.GetRecipesPath();

            // 2. Migracja ze starego pliku AgentRecipes.json (jeśli istnieje)
            string oldConfigPath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "AgentRecipes.json"
            );

            if (File.Exists(oldConfigPath))
            {
                try
                {
                    string oldJson = File.ReadAllText(oldConfigPath);
                    var oldRecipesList = JsonConvert.DeserializeObject<List<AgentRecipe>>(oldJson);
                    if (oldRecipesList != null)
                    {
                        foreach (var recipe in oldRecipesList)
                        {
                            if (!string.IsNullOrWhiteSpace(recipe.Trigger))
                            {
                                string cleanTrigger = recipe.Trigger.TrimStart('$');
                                string filePath = Path.Combine(recipesPath, $"{cleanTrigger}.json");
                                if (!File.Exists(filePath))
                                {
                                    File.WriteAllText(filePath, JsonConvert.SerializeObject(recipe, Formatting.Indented));
                                    BielikLogger.LogInfo($"[Migracja] Zapisano receptę '{cleanTrigger}' do nowego formatu.");
                                }
                            }
                        }
                    }
                    // Po udanej migracji, zmieniamy nazwę pliku, żeby nie migrować go ponownie
                    string backupPath = oldConfigPath + ".bak";
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Move(oldConfigPath, backupPath);
                    BielikLogger.LogInfo("[Migracja] Stary plik AgentRecipes.json został przeniesiony do .bak.");
                }
                catch (Exception ex)
                {
                    BielikLogger.LogError("[Migracja] Błąd podczas migracji starego pliku z receptami.", ex);
                }
            }

            // 3. Właściwe ładowanie recept z folderu Recipes
            try
            {
                var files = Directory.GetFiles(recipesPath, "*.json", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    try
                    {
                        string json = File.ReadAllText(file);
                        var recipe = JsonConvert.DeserializeObject<AgentRecipe>(json);
                        if (recipe != null && !string.IsNullOrWhiteSpace(recipe.Trigger))
                        {
                            string cleanTrigger = recipe.Trigger.TrimStart('$');
                            _recipes[cleanTrigger] = recipe;
                        }
                    }
                    catch (Exception ex)
                    {
                        BielikLogger.LogError($"[Recepty] Błąd ładowania recepty z pliku '{file}'", ex);
                    }
                }
                BielikLogger.LogInfo($"[Recepty] Załadowano {_recipes.Count} recept.");
            }
            catch (Exception ex)
            {
                BielikLogger.LogError("[Recepty] Błąd odczytu folderu z receptami.", ex);
            }
        }

        public static void Save()
        {
            // Opcjonalne: funkcja dla wstecznej kompatybilności,
            // ale teraz zapisujemy każdą receptę osobno w `AddOrUpdate`.
        }

        public static List<AgentRecipe> GetAll() 
        {
            return new List<AgentRecipe>(_recipes.Values);
        }

        public static void AddOrUpdate(AgentRecipe recipe)
        {
            if (string.IsNullOrWhiteSpace(recipe.Trigger)) return;
            string cleanTrigger = recipe.Trigger.TrimStart('$');
            
            _recipes[cleanTrigger] = recipe;
            
            try
            {
                string filePath = Path.Combine(AppPaths.GetRecipesPath(), $"{cleanTrigger}.json");
                File.WriteAllText(filePath, JsonConvert.SerializeObject(recipe, Formatting.Indented));
            }
            catch (Exception ex)
            {
                BielikLogger.LogError($"[Recepty] Błąd zapisu recepty '{cleanTrigger}'", ex);
            }
        }

        public static void Delete(string trigger)
        {
            if (string.IsNullOrWhiteSpace(trigger)) return;
            string cleanTrigger = trigger.TrimStart('$');
            
            if (_recipes.ContainsKey(cleanTrigger))
            {
                _recipes.Remove(cleanTrigger);
            }

            try
            {
                string filePath = Path.Combine(AppPaths.GetRecipesPath(), $"{cleanTrigger}.json");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                BielikLogger.LogError($"[Recepty] Błąd usuwania pliku recepty '{cleanTrigger}'", ex);
            }
        }

        public static AgentRecipe GetByTrigger(string trigger)
        {
            if (string.IsNullOrEmpty(trigger)) return null;
            string cleanTrigger = trigger.TrimStart('$');
            
            if (_recipes.TryGetValue(cleanTrigger, out var recipe))
            {
                return recipe;
            }
            return null;
        }
    }
}
