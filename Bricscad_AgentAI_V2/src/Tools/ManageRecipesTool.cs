using System;
using System.Collections.Generic;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ManageRecipesTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ManageRecipes",
                    Description = "Pozwala na zarządzanie receptami agenta. Recepty to szablony wywołań narzędzi (Few-Shot Prompts), które uczą agenta jak wykonywać złożone, wieloetapowe operacje. Użyj tego narzędzia, aby przeglądać dostępne recepty, czytać ich kod źródłowy, tworzyć nowe recepty na podstawie własnych doświadczeń lub je usuwać.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "Action", new ToolParameter { Type = "string", Enum = new List<string> { "List", "Read", "CreateOrUpdate", "Delete" }, Description = "Akcja do wykonania." } },
                            { "Trigger", new ToolParameter { Type = "string", Description = "Nazwa (trigger) recepty, np. 'rysuj_okno'. Wymagane dla Read, CreateOrUpdate i Delete." } },
                            { "Description", new ToolParameter { Type = "string", Description = "Opis recepty. Wymagany dla CreateOrUpdate." } },
                            { "ToolExampleJson", new ToolParameter { Type = "string", Description = "Schemat wywołania w formacie JSON (tablica ToolCall). Wymagany dla CreateOrUpdate." } },
                            { "AutoLoadCategories", new ToolParameter { Type = "array", Items = JToken.FromObject(new ToolParameter { Type = "string" }), Description = "Opcjonalna tablica kategorii narzędzi, które powinny być załadowane dla tej recepty (np. ['#core', '#wymiary'])." } }
                        },
                        Required = new List<string> { "Action" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string action = args["Action"]?.ToString();
            string trigger = args["Trigger"]?.ToString();
            string description = args["Description"]?.ToString();
            string toolExampleJson = args["ToolExampleJson"]?.ToString();
            JArray categoriesArray = args["AutoLoadCategories"] as JArray;

            switch (action)
            {
                case "List":
                    var recipes = RecipeManager.GetAll();
                    if (recipes.Count == 0) return "Brak dostępnych recept.";
                    var list = recipes.Select(r => $"- ${r.Trigger}: {r.Description}");
                    return "Dostępne recepty:\n" + string.Join("\n", list) + "\n\nAby przeczytać szczegóły recepty, użyj akcji 'Read' podając 'Trigger'.";

                case "Read":
                    if (string.IsNullOrEmpty(trigger)) return "BŁĄD: Parametr 'Trigger' jest wymagany do odczytu.";
                    var recipe = RecipeManager.GetByTrigger(trigger);
                    if (recipe == null) return $"BŁĄD: Nie znaleziono recepty '{trigger}'.";
                    
                    return $"--- Recepta: ${recipe.Trigger} ---\nOpis: {recipe.Description}\nKategorie: {(recipe.AutoLoadCategories != null ? string.Join(", ", recipe.AutoLoadCategories) : "Brak")}\nKod JSON ToolCall:\n{JsonConvert.SerializeObject(recipe.ToolExample, Formatting.Indented)}";

                case "CreateOrUpdate":
                    if (string.IsNullOrEmpty(trigger) || string.IsNullOrEmpty(description) || string.IsNullOrEmpty(toolExampleJson))
                    {
                        return "BŁĄD: Parametry 'Trigger', 'Description' i 'ToolExampleJson' są wymagane do utworzenia/aktualizacji recepty.";
                    }

                    JArray parsedToolExample;
                    try
                    {
                        parsedToolExample = JArray.Parse(toolExampleJson);
                    }
                    catch (Exception ex)
                    {
                        return $"BŁĄD: 'ToolExampleJson' nie jest poprawną tablicą JSON: {ex.Message}";
                    }

                    List<string> categories = null;
                    if (categoriesArray != null)
                    {
                        categories = categoriesArray.Select(c => c.ToString()).ToList();
                    }

                    var newRecipe = new AgentRecipe
                    {
                        Trigger = trigger.TrimStart('$'),
                        Description = description,
                        ToolExample = parsedToolExample,
                        AutoLoadCategories = categories
                    };

                    RecipeManager.AddOrUpdate(newRecipe);
                    return $"Sukces: Recepta '${newRecipe.Trigger}' została pomyślnie utworzona/zaktualizowana.";

                case "Delete":
                    if (string.IsNullOrEmpty(trigger)) return "BŁĄD: Parametr 'Trigger' jest wymagany do usunięcia.";
                    var existing = RecipeManager.GetByTrigger(trigger);
                    if (existing == null) return $"BŁĄD: Nie znaleziono recepty '{trigger}'.";
                    
                    RecipeManager.Delete(trigger);
                    return $"Sukces: Recepta '${trigger}' została usunięta.";

                default:
                    return $"BŁĄD: Nieznana akcja '{action}'.";
            }
        }

        public List<string> Examples => new List<string>
        {
            "{ \"Action\": \"List\" }",
            "{ \"Action\": \"Read\", \"Trigger\": \"mojarecepta\" }"
        };
    }
}
