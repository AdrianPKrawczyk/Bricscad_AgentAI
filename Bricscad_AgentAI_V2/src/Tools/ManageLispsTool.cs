using System;
using System.Collections.Generic;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ManageLispsTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "manage_lisps",
                    Description = "Zarządza skryptami LISP. Pozwala na tworzenie, edycję, czytanie i usuwanie skryptów. Skrypty LISP używane są przez profil do zadań związanych z automatyzacją.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "Action", new ToolParameter { Type = "string", Enum = new List<string> { "save_lisp", "read_lisp", "execute_lisp", "delete_lisp" }, Description = "Akcja do wykonania." } },
                            { "LispId", new ToolParameter { Type = "string", Description = "Unikalny identyfikator skryptu (np. 'c:prostokat' lub 'moj_test'). Bez spacji, z użyciem podkreśleń." } },
                            { "Category", new ToolParameter { Type = "string", Description = "Kategoria skryptu LISP. Wymagane przy zapisie. Przykłady: 'Geometria', 'Narzędzia', 'Testy'." } },
                            { "Description", new ToolParameter { Type = "string", Description = "Krótki opis co robi skrypt. Wymagane przy zapisie." } },
                            { "LispCode", new ToolParameter { Type = "string", Description = "Kod źródłowy LISP. Wymagane przy zapisie." } }
                        },
                        Required = new List<string> { "Action", "LispId" }
                    }
                }
            };
        }

        public List<string> Examples => new List<string>
        {
            "{ \"Action\": \"save_lisp\", \"LispId\": \"test_srodowiska\", \"Category\": \"Testy\", \"Description\": \"Testowy skrypt\", \"LispCode\": \"(defun c:test_srodowiska ... )\" }",
            "{ \"Action\": \"read_lisp\", \"LispId\": \"test_srodowiska\" }",
            "{ \"Action\": \"execute_lisp\", \"LispId\": \"test_srodowiska\" }",
            "{ \"Action\": \"delete_lisp\", \"LispId\": \"test_srodowiska\" }"
        };

        public string Execute(Document doc, JObject args)
        {
            if (args["Action"] == null)
                return "Błąd: Brak parametru Action.";
            
            if (args["LispId"] == null)
                return "Błąd: Brak parametru LispId.";

            string action = args["Action"].ToString();
            string lispId = args["LispId"].ToString();

            try
            {
                if (action == "save_lisp")
                {
                    if (args["LispCode"] == null) return "Błąd: Brak parametru LispCode.";
                    if (args["Category"] == null) return "Błąd: Brak parametru Category.";
                    if (args["Description"] == null) return "Błąd: Brak parametru Description.";

                    var metadata = new LispMetadata
                    {
                        LispId = lispId,
                        Category = args["Category"].ToString(),
                        Description = args["Description"].ToString(),
                        CreatedAt = DateTime.Now
                    };

                    LispManager.SaveLisp(metadata, args["LispCode"].ToString());
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

                    LispManager.TriggerLispExecution(lispId, code);

                    return $"[SUKCES] Polecenie uruchomienia skryptu '{lispId}' w BricsCAD zostało przekazane i jest wykonywane.";
                }
                else if (action == "delete_lisp")
                {
                    LispManager.DeleteLisp(lispId);
                    return $"[SUKCES] Skrypt LISP '{lispId}' został usunięty z Bazy Wiedzy.";
                }
                else
                {
                    return $"Błąd: Nieznana akcja '{action}'. Dozwolone to: save_lisp, read_lisp, execute_lisp, delete_lisp.";
                }
            }
            catch (Exception ex)
            {
                return $"[BŁĄD] Podczas obsługi skryptu LISP wystąpił wyjątek: {ex.Message}";
            }
        }
    }
}
