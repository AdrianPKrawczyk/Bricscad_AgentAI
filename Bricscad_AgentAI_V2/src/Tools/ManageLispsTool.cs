using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core.DynamicSystems;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ManageLispsTool : IAgentTool
    {
        public string Name => "manage_lisps";
        public string Description => "Pozwala na zapisywanie, odczytywanie i wykonywanie skryptów LISP z Bazy Wiedzy (Knowledge Base).";

        public ToolSchema GetSchema()
        {
            return new ToolSchema
            {
                Name = this.Name,
                Description = this.Description,
                Parameters = new ToolParameters
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolParameterProperty>
                    {
                        {
                            "Action",
                            new ToolParameterProperty
                            {
                                Type = "string",
                                Description = "Akcja do wykonania: 'save_lisp' (zapis do Bazy Wiedzy), 'read_lisp' (odczyt), 'execute_lisp' (wykonanie istniejącego skryptu z bazy wiedzy)."
                            }
                        },
                        {
                            "LispId",
                            new ToolParameterProperty
                            {
                                Type = "string",
                                Description = "Unikalny identyfikator skryptu (np. 'c:prostokat' lub 'moj_test'). Bez spacji, z użyciem podkreśleń."
                            }
                        },
                        {
                            "Category",
                            new ToolParameterProperty
                            {
                                Type = "string",
                                Description = "Kategoria skryptu LISP. Wymagane przy zapisie. Przykłady: 'Geometria', 'Narzędzia', 'Testy'."
                            }
                        },
                        {
                            "Description",
                            new ToolParameterProperty
                            {
                                Type = "string",
                                Description = "Krótki opis co robi skrypt. Wymagane przy zapisie."
                            }
                        },
                        {
                            "LispCode",
                            new ToolParameterProperty
                            {
                                Type = "string",
                                Description = "Kod źródłowy LISP. Wymagane przy zapisie."
                            }
                        }
                    },
                    Required = new List<string> { "Action", "LispId" }
                }
            };
        }

        public async Task<string> ExecuteAsync(Dictionary<string, object> parameters)
        {
            if (!parameters.TryGetValue("Action", out var actionObj) || actionObj == null)
                return "Błąd: Brak parametru Action.";
            
            if (!parameters.TryGetValue("LispId", out var idObj) || idObj == null)
                return "Błąd: Brak parametru LispId.";

            string action = actionObj.ToString();
            string lispId = idObj.ToString();

            try
            {
                if (action == "save_lisp")
                {
                    if (!parameters.TryGetValue("LispCode", out var codeObj) || codeObj == null)
                        return "Błąd: Brak parametru LispCode wymaganego do zapisu.";
                    if (!parameters.TryGetValue("Category", out var catObj) || catObj == null)
                        return "Błąd: Brak parametru Category wymaganego do zapisu.";
                    if (!parameters.TryGetValue("Description", out var descObj) || descObj == null)
                        return "Błąd: Brak parametru Description wymaganego do zapisu.";

                    var metadata = new LispMetadata
                    {
                        LispId = lispId,
                        Category = catObj.ToString(),
                        Description = descObj.ToString(),
                        CreatedAt = DateTime.Now
                    };

                    LispManager.SaveLisp(metadata, codeObj.ToString());
                    return $"[SUKCES] Skrypt LISP '{lispId}' został zapisany w Bazie Wiedzy.";
                }
                else if (action == "read_lisp")
                {
                    string code = LispManager.GetLispCode(lispId);
                    if (string.IsNullOrEmpty(code))
                        return $"[BŁĄD] Nie znaleziono skryptu LISP o identyfikatorze '{lispId}' w Bazie Wiedzy.";
                    
                    return $"Skrypt LISP '{lispId}':\n```lisp\n{code}\n```";
                }
                else if (action == "execute_lisp")
                {
                    string code = LispManager.GetLispCode(lispId);
                    if (string.IsNullOrEmpty(code))
                        return $"[BŁĄD] Nie znaleziono skryptu LISP o identyfikatorze '{lispId}' w Bazie Wiedzy do wykonania.";

                    // Wywołujemy globalny event, który przechwyci AgentControl/UI
                    // Wymagać to będzie zdefiniowania eventu w LispManager lub w klasie UI, 
                    // np. LispExecutionRequested(lispId, code).
                    LispManager.TriggerLispExecution(lispId, code);

                    return $"[SUKCES] Polecenie uruchomienia skryptu '{lispId}' w BricsCAD zostało przekazane i jest wykonywane.";
                }
                else
                {
                    return $"Błąd: Nieznana akcja '{action}'. Dozwolone to: save_lisp, read_lisp, execute_lisp.";
                }
            }
            catch (Exception ex)
            {
                return $"[BŁĄD] Podczas obsługi skryptu LISP wystąpił wyjątek: {ex.Message}";
            }
        }
    }
}
