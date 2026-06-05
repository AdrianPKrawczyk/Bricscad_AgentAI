using System;
using System.Collections.Generic;
using System.IO;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Bricscad_AgentAI_V2.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools.Knowledge
{
    public class SavePermanentFormulaTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "SavePermanentFormula",
                    Description = "Kompiluje i zapisuje kod C# jako permanentną formułę w bazie wiedzy (plik .csx). Formuła staje się od razu dostępna w systemie. Inputs are strings containing values and units (e.g., '50 kg'). When writing C# code for SavePermanentFormulaTool, you MUST parse these inputs using UnitsNet classes. When writing Parse methods, ALWAYS use CultureInfo.InvariantCulture. Example: Length.Parse(Inputs[\"h\"], System.Globalization.CultureInfo.InvariantCulture); and return a string representation of the final UnitsNet quantity.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "FormulaId", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Unikalna nazwa formuły bez spacji i rozszerzenia (np. 'ObliczPole', 'Hvac_FlowRate')."
                                }
                            },
                            {
                                "Description", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Krótki opis tego, co robi formuła."
                                }
                            },
                            {
                                "CSharpCode", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Kod C# operujący na zmiennej 'Inputs'. Musi kończyć się instrukcją 'return [wartość w postaci stringa z jednostką];'. Kod zostanie sprawdzony przez kompilator Roslyn przed zapisem."
                                }
                            }
                        },
                        Required = new List<string> { "FormulaId", "CSharpCode" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string id = args["FormulaId"]?.ToString();
            string description = args["Description"]?.ToString();
            string code = args["CSharpCode"]?.ToString();

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(code))
            {
                return "BŁĄD: Parametry FormulaId oraz CSharpCode są wymagane.";
            }

            // KRYTYCZNE ZABEZPIECZENIE: Kompilacja "na sucho"
            try
            {
                var options = ScriptOptions.Default
                    .WithReferences(typeof(UnitsNet.Length).Assembly)
                    .WithImports("System", "System.Math", "System.Collections.Generic", "UnitsNet", "System.Globalization");
                var script = CSharpScript.Create<string>(code, options, globalsType: typeof(ScriptGlobals));
                
                var diagnostics = script.Compile();
                if (diagnostics.Length > 0)
                {
                    bool hasErrors = false;
                    var errorMsg = new System.Text.StringBuilder("BŁĄD KOMPILACJI (DRY RUN):\n");
                    foreach (var diag in diagnostics)
                    {
                        if (diag.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                        {
                            hasErrors = true;
                            errorMsg.AppendLine(diag.ToString());
                        }
                    }
                    if (hasErrors) return errorMsg.ToString();
                }
            }
            catch (Exception ex)
            {
                return $"BŁĄD KOMPILACJI (DRY RUN): {ex.Message}";
            }

            // Jeśli kompilacja powiodła się, zapisujemy plik
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string formulasPath = Path.Combine(appData, "Bricscad_AgentAI", "CustomKnowledge", "Formulas");
                
                if (!Directory.Exists(formulasPath)) Directory.CreateDirectory(formulasPath);

                string filePath = Path.Combine(formulasPath, $"{id}.csx");
                string fileContent = $"// OPIS: {description}\n\n{code}";
                
                File.WriteAllText(filePath, fileContent);
                
                // Przeładowanie bazy
                DynamicFormulaManager.LoadAndCompileAll();
                
                return $"SUKCES: Formuła '{id}' skompilowana i zapisana. Baza została zaktualizowana.";
            }
            catch (Exception ex)
            {
                return $"BŁĄD ZAPISU PLIKU: {ex.Message}";
            }
        }

        public List<string> Examples => null;
    }
}
