using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ExecuteMacroTool : IToolV2
    {
        public string[] ToolTags => new[] { "#makro" };

        // Mapa statyczna makr dla wersji GOLD (zamiast SQLite dla uproszczenia i szybkości)
        private static readonly Dictionary<string, string> _macros = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "CleanDrawings", "(command \"_PURGE\" \"_A\" \"*\" \"_N\") (command \"_AUDIT\" \"_Y\")" },
            { "ResetLayers", "LISP:(foreach p (layoutlist) (setvar \"CTAB\" p) (command \"_LAYER\" \"_ON\" \"*\" \"_THAW\" \"*\" \"\"))" },
            { "ZoomExtents", "_ZOOM _E " }
        };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ExecuteMacro",
                    Description = "Uruchamia zdefiniowane makro lub skrypt LISP/RPN. Pozwala na automatyzację złożonych zadań administracyjnych lub rysunkowych.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "MacroName", new ToolParameter { Type = "string", Description = "Nazwa makra do uruchomienia (np. 'CleanDrawings', 'ResetLayers', 'ZoomExtents')." } },
                            { "CustomCommand", new ToolParameter { Type = "string", Description = "Opcjonalnie: własna komenda LISP lub CAD do wykonania, jeśli MacroName nie jest znane." } }
                        },
                        Required = new List<string> { }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string macroName = args["MacroName"]?.ToString() ?? "";
            string customCmd = args["CustomCommand"]?.ToString() ?? "";
            
            string toExecute = "";

            if (!string.IsNullOrEmpty(macroName))
            {
                if (_macros.TryGetValue(macroName, out string macroCmd))
                {
                    toExecute = macroCmd;
                }
                else if (string.IsNullOrEmpty(customCmd))
                {
                    return $"BŁĄD: Nie znaleziono makra o nazwie '{macroName}'. Dostępne: {string.Join(", ", _macros.Keys)}.";
                }
            }
            
            if (string.IsNullOrEmpty(toExecute)) toExecute = customCmd;
            if (string.IsNullOrEmpty(toExecute)) return "BŁĄD: Nie podano makra ani własnej komendy.";

            try
            {
                if (toExecute.StartsWith("LISP:", StringComparison.OrdinalIgnoreCase))
                {
                    string lispCode = toExecute.Substring(5);
                    doc.SendStringToExecute(lispCode + " ", true, false, false);
                    return $"SUKCES: Wysłano skrypt LISP do wykonania: {lispCode}";
                }
                else
                {
                    doc.SendStringToExecute(toExecute + "\n", true, false, false);
                    return $"SUKCES: Wysłano komendę do wykonania: {toExecute}";
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD WYKONANIA MAKRA: {ex.Message}";
            }
        }
    }
}
