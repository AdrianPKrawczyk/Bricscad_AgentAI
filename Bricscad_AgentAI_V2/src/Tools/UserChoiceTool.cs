using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    /// <summary>
    /// Narzędzie do wyświetlenia użytkownikowi listy słów kluczowych (Keywords) do wyboru w linii komend.
    /// </summary>
    public class UserChoiceTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "UserChoice",
                    Description = "Zatrzymuje Agenta i wyświetla użytkownikowi sztywną listę opcji do wyboru w linii komend BricsCAD.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "PromptMessage", new ToolParameter { Type = "string", Description = "Komunikat wyświetlany użytkownikowi (np. 'Wybierz typ rury:')." } },
                            { "Options", new ToolParameter { Type = "array", Description = "Lista dostępnych opcji (np. ['Stal', 'PCV']). Spacje w nazwach zostaną automatycznie zamienione na podkreślenia." } },
                            { "SaveAs", new ToolParameter { Type = "string", Description = "Opcjonalna nazwa zmiennej do zapisu wyboru (bez @)." } }
                        },
                        Required = new List<string> { "PromptMessage", "Options" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string promptMsg = args["PromptMessage"]?.ToString();
            JArray optionsArr = args["Options"] as JArray;
            string saveAs = args["SaveAs"]?.ToString();

            if (optionsArr == null || optionsArr.Count == 0)
                return "BŁĄD: Brak zdefiniowanych opcji w parametrze Options.";

            Editor ed = doc.Editor;
            
            try
            {
                return (string)Bricscad_AgentAI_V2.UI.AgentControl.Instance.Invoke(new Func<string>(() =>
                {
                    Application.MainWindow.Focus();

                    PromptKeywordOptions pko = new PromptKeywordOptions($"\n[DECYZJA AI] {promptMsg}: ");
                    pko.AllowNone = false;

                    // Przygotowanie Keywords (zamiana spacji na podkreślenia zgodnie z wymogiem API)
                    foreach (var opt in optionsArr)
                    {
                        string cleanOpt = opt.ToString().Replace(" ", "_");
                        if (!string.IsNullOrEmpty(cleanOpt))
                        {
                            pko.Keywords.Add(cleanOpt);
                        }
                    }

                    PromptResult pr = ed.GetKeywords(pko);

                    if (pr.Status != PromptStatus.OK)
                        return "[ANULOWANO] Użytkownik przerwał wybór opcji.";

                    string selected = pr.StringResult;

                    if (!string.IsNullOrEmpty(saveAs))
                    {
                        AgentMemoryState.Variables[saveAs] = selected;
                    }

                    return $"WYNIK: Użytkownik wybrał: {selected}";
                }));
            }
            catch (Exception ex)
            {
                return $"BŁĄD WYBORU UŻYTKOWNIKA: {ex.Message}";
            }
        }
        public List<string> Examples => null;
    }
}

