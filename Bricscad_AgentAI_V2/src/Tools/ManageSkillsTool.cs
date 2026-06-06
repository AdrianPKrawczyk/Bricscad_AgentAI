using System;
using System.Collections.Generic;
using System.Linq;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Bricscad.ApplicationServices;

namespace Bricscad_AgentAI_V2.Core
{
    public class ManageSkillsTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "manage_skills",
                    Description = "Zarządza systemem Skilli (Markdown z YAML Frontmatter). Używaj 'list_skills' by zobaczyć dostępne skille (Poziom 0). Używaj 'read_skill' by wczytać pełną wiedzę z konkretnego skilla (Poziom 1). Używaj 'create_skill' by zapisać nową wiedzę w formacie .md, którą zdobyłeś, aby użyć jej w przyszłości.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "action", new ToolParameter { Type = "string", Description = "Akcja do wykonania: 'list_skills', 'read_skill' lub 'create_skill'." } },
                            { "skillId", new ToolParameter { Type = "string", Description = "ID skilla (np. 'ObliczeniaDachu'). Wymagane dla read_skill i create_skill." } },
                            { "category", new ToolParameter { Type = "string", Description = "Kategoria nowego skilla. Używane tylko w create_skill." } },
                            { "description", new ToolParameter { Type = "string", Description = "Krótki opis, max 1 zdanie. Używane tylko w create_skill." } },
                            { "tags", new ToolParameter { Type = "string", Description = "Tagi po przecinku, np. 'dach,konstrukcja'. Używane tylko w create_skill." } },
                            { "markdownContent", new ToolParameter { Type = "string", Description = "Główna zawartość instrukcji w formacie Markdown (bez YAML Frontmatter na górze - system sam go wygeneruje). Używane tylko w create_skill." } }
                        },
                        Required = new List<string> { "action" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            try
            {
                string action = args["action"]?.ToString();
                if (string.IsNullOrEmpty(action))
                {
                    return "Błąd: Brak parametru 'action'.";
                }

                if (action == "list_skills")
                {
                    var skills = SkillManager.GetAvailableSkills().ToList();
                    if (skills.Count == 0) return "Brak dostępnych skilli.";

                    var resultList = skills.Select(s => new
                    {
                        Id = s.Id,
                        Category = s.Category,
                        Description = s.Description,
                        Tags = s.Tags
                    });

                    return JsonConvert.SerializeObject(resultList, Formatting.Indented);
                }
                else if (action == "read_skill")
                {
                    string id = args["skillId"]?.ToString();
                    if (string.IsNullOrWhiteSpace(id))
                        return "Błąd: Akcja 'read_skill' wymaga parametru 'skillId'.";
                    
                    try
                    {
                        var skill = SkillManager.GetSkill(id);
                        return skill.Content;
                    }
                    catch
                    {
                        return $"Błąd: Nie znaleziono skilla o ID: {id}";
                    }
                }
                else if (action == "create_skill")
                {
                    string id = args["skillId"]?.ToString();
                    string md = args["markdownContent"]?.ToString();

                    if (string.IsNullOrWhiteSpace(id))
                        return "Błąd: Akcja 'create_skill' wymaga parametru 'skillId'.";
                    if (string.IsNullOrWhiteSpace(md))
                        return "Błąd: Akcja 'create_skill' wymaga parametru 'markdownContent'.";

                    string cat = args["category"]?.ToString() ?? "Uncategorized";
                    string desc = args["description"]?.ToString() ?? "";
                    
                    var tagList = new List<string>();
                    string tagsStr = args["tags"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(tagsStr))
                    {
                        tagList = tagsStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(t => t.Trim()).ToList();
                    }

                    var skill = new AgentSkill
                    {
                        Id = id,
                        Category = cat,
                        Description = desc,
                        Tags = tagList,
                        Content = md
                    };

                    SkillManager.SaveSkill(skill);
                    return $"Sukces: Utworzono i zapisano skill '{id}'.";
                }
                else
                {
                    return $"Błąd: Nieznana akcja '{action}'.";
                }
            }
            catch (Exception ex)
            {
                return $"Wystąpił błąd podczas zarządzania skillami: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string>
        {
            "{\"action\": \"list_skills\"}",
            "{\"action\": \"read_skill\", \"skillId\": \"ProjektDachu\"}",
            "{\"action\": \"create_skill\", \"skillId\": \"NowaFunkcja\", \"category\": \"General\", \"description\": \"Krótki opis\", \"tags\": \"test,przyklad\", \"markdownContent\": \"TREŚĆ\"}"
        };
    }
}
