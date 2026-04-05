using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Bricscad_AgentAI_V2.Tools
{
    /// <summary>
    /// Narzędzie do zatrzymania pętli Agenta i pobrania danych od użytkownika w linii komend BricsCAD.
    /// </summary>
    public class UserInputTool : IToolV2
    {
        public string[] ToolTags => new[] { "#core" };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "UserInput",
                    Description = "Zatrzymuje działanie Agenta i prosi użytkownika o podanie konkretnej wartości (Tekst, Liczba, Punkt) w linii komend BricsCAD.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "PromptMessage", new ToolParameter { Type = "string", Description = "Komunikat wyświetlany użytkownikowi (np. 'Podaj szerokość ściany:')." } },
                            { "InputType", new ToolParameter { Type = "string", Enum = new List<string> { "String", "Integer", "Double", "Point" }, Description = "Typ oczekiwanych danych." } },
                            { "SaveAs", new ToolParameter { Type = "string", Description = "Opcjonalna nazwa zmiennej do zapisu wyniku (bez @)." } }
                        },
                        Required = new List<string> { "PromptMessage", "InputType" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string promptMsg = args["PromptMessage"]?.ToString();
            string inputType = args["InputType"]?.ToString();
            string saveAs = args["SaveAs"]?.ToString();

            // Wstrzykiwanie zmiennych do komunikatu prompt
            promptMsg = AgentMemoryState.InjectVariables(promptMsg);
            
            Editor ed = doc.Editor;
            
            try
            {
                // Przekierowanie fokusu do głównego okna BricsCAD, aby użytkownik mógł od razu pisać
                Application.MainWindow.Focus();

                string resultValue = "";
                
                switch (inputType)
                {
                    case "String":
                        PromptStringOptions pso = new PromptStringOptions($"\n[KONSULTACJA AI] {promptMsg}: ");
                        pso.AllowSpaces = true;
                        PromptResult prStr = ed.GetString(pso);
                        if (prStr.Status != PromptStatus.OK) return "[ANULOWANO] Użytkownik przerwał wprowadzanie tekstu.";
                        resultValue = prStr.StringResult;
                        break;

                    case "Integer":
                        PromptIntegerOptions pio = new PromptIntegerOptions($"\n[KONSULTACJA AI] {promptMsg}: ");
                        PromptIntegerResult pir = ed.GetInteger(pio);
                        if (pir.Status != PromptStatus.OK) return "[ANULOWANO] Użytkownik przerwał wprowadzanie liczby całkowitej.";
                        resultValue = pir.Value.ToString();
                        break;

                    case "Double":
                        PromptDoubleOptions pdo = new PromptDoubleOptions($"\n[KONSULTACJA AI] {promptMsg}: ");
                        PromptDoubleResult pdr = ed.GetDouble(pdo);
                        if (pdr.Status != PromptStatus.OK) return "[ANULOWANO] Użytkownik przerwał wprowadzanie liczby.";
                        resultValue = pdr.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        break;

                    case "Point":
                        PromptPointOptions ppo = new PromptPointOptions($"\n[KONSULTACJA AI] {promptMsg}: ");
                        PromptPointResult ppr = ed.GetPoint(ppo);
                        if (ppr.Status != PromptStatus.OK) return "[ANULOWANO] Użytkownik przerwał wskazywanie punktu.";
                        resultValue = $"({ppr.Value.X.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}, " +
                                      $"{ppr.Value.Y.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)}, " +
                                      $"{ppr.Value.Z.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)})";
                        break;

                    default:
                        return $"BŁĄD: Nieobsługiwany typ wejścia: {inputType}.";
                }

                if (!string.IsNullOrEmpty(saveAs))
                {
                    AgentMemoryState.Variables[saveAs] = resultValue;
                }

                return $"WYNIK: Użytkownik podał wartość: {resultValue}";
            }
            catch (Exception ex)
            {
                return $"BŁĄD INTERAKCJI Z UŻYTKOWNIKIEM: {ex.Message}";
            }
        }
    }
}
