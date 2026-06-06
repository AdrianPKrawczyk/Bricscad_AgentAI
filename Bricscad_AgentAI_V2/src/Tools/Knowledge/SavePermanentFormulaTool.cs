using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.DynamicSystems;
using Bricscad_AgentAI_V2.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
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
                    Description = "Nadpisuje (lub wstawia) permanentną formułę w bazie wiedzy. Tworzy powiązane pliki metadanych (JSON) i wykonywalny kod (CSX). Jeśli użytkownik prosi o modyfikację formuły, użyj najpierw ReadKnowledgeTool, zmodyfikuj pobrany kod i wywołaj to narzędzie z tym samym ID, by go nadpisać.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            {
                                "FormulaId", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Unikalna nazwa formuły bez spacji i rozszerzenia."
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
                                "RequiredInputs", new ToolParameter
                                {
                                    Type = "array",
                                    Description = "Lista wymaganych wejść dla formuły, każde w formacie JSON z polami 'key', 'description' i 'expectedType'."
                                }
                            },
                            {
                                "OutputDescription", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Opis typu zwracanej wartości, np. 'string (w formacie UnitsNet)'. "
                                }
                            },
                            {
                                "Category", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Kategoria formuły, używająca znaku ukośnika jako separatora (np. 'HVAC/Wentylacja'). Opcjonalna."
                                }
                            },
                            {
                                "Tags", new ToolParameter
                                {
                                    Type = "array",
                                    Description = "Lista tagów pomocniczych (np. ['przepływ', 'woda']). Opcjonalna."
                                }
                            },
                            {
                                "CSharpCode", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Płaski, gotowy kod C#. CRITICAL SCRIPTING RULE: DO NOT wrap your C# code in any class, namespace, or method definitions (NO 'public class', NO 'public string Execute'). You MUST write FLAT, top-level script statements only. Parametry pobierasz WYŁĄCZNIE ze słownika 'Inputs', np. Inputs[\"velocity\"].ToString(). Nie istnieją one jako zmienne lokalne! Use UnitsNet classes and CultureInfo.InvariantCulture for parsing. UWAGA: Jeśli nie jesteś w 100% pewien, jak w bibliotece UnitsNet nazywa się dana wielkość fizyczna, UŻYJ NAJPIERW NARZĘDZIA SearchUnitsNetTool. CRITICAL: NEVER use 'UnitsNet.Parse<T>()' - this method DOES NOT EXIST! You must use the exact class name's Parse method, exactly as shown in SearchUnitsNetTool (e.g., 'UnitsNet.Speed.Parse(Inputs[\"velocity\"].ToString(), ... )'). Formuła MUSI na końcu zwracać wartość jako STRING. BARDZO WAŻNE: NIGDY nie ucinaj i nie streszczaj kodu. Musisz podać PEŁNY kod!"
                                }
                            },
                            { "__DryRun", new ToolParameter { Type = "boolean", Description = "[REZERWACJA DLA AGENTA TESTOWEGO] Jeśli true, nie zapisuje na dysk, jedynie kompiluje w pamięci." } }
                        },
                        Required = new List<string> { "FormulaId", "CSharpCode", "RequiredInputs" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string id = args["FormulaId"]?.ToString();
            string description = args["Description"]?.ToString();
            string outputDescription = args["OutputDescription"]?.ToString();
            string code = args["CSharpCode"]?.ToString();
            string category = args["Category"]?.ToString();
            bool isDryRun = args["__DryRun"] != null && args["__DryRun"].Type == JTokenType.Boolean && (bool)args["__DryRun"];

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(code))
            {
                return "BŁĄD: Parametry FormulaId oraz CSharpCode są wymagane.";
            }

            // Sanitizacja kategorii
            if (!string.IsNullOrWhiteSpace(category))
            {
                category = category.Replace("\\", "/");
                var invalidChars = Path.GetInvalidPathChars();
                category = new string(category.Where(c => !invalidChars.Contains(c)).ToArray());
            }

            var meta = new FormulaMetadata
            {
                FormulaId = id,
                Description = description,
                OutputDescription = outputDescription,
                RequiredInputs = new List<FormulaInputDef>(),
                Category = string.IsNullOrWhiteSpace(category) ? "Uncategorized" : category,
                Tags = new List<string>()
            };

            if (args["Tags"] is JArray tagsArr)
            {
                foreach (var t in tagsArr)
                {
                    meta.Tags.Add(t.ToString());
                }
            }

            if (args["RequiredInputs"] is JArray arr)
            {
                try
                {
                    meta.RequiredInputs = arr.ToObject<List<FormulaInputDef>>();
                }
                catch (Exception ex)
                {
                    return $"BŁĄD PARSOWANIA RequiredInputs: {ex.Message}";
                }
            }

            try
            {
                string jsonMeta = JsonConvert.SerializeObject(meta, Formatting.Indented);
                
                if (isDryRun)
                {
                    // Symulacja kompilacji - sprawdzamy składnię używając CSharpScript.Create
                    var scriptOptions = ScriptOptions.Default.AddReferences("System.dll").AddImports("System");
                    var script = CSharpScript.Create<string>(code, scriptOptions, typeof(ScriptGlobals));
                    var diagnostics = script.Compile();
                    if (diagnostics.Any(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error))
                    {
                        var errors = string.Join("\n", diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).Select(d => d.GetMessage()));
                        return $"BŁĄD KOMPILACJI (DRY-RUN): {errors}";
                    }
                    return $"SUKCES (DRY-RUN): Formuła '{id}' skompilowana poprawnie. Zapis fizyczny pominięto.";
                }

                DynamicFormulaManager.SaveFormula(id, code, jsonMeta);
                DynamicFormulaManager.LoadAndCompileAll();
                return $"SUKCES: Formuła '{id}' skompilowana i zapisana. Baza została zaktualizowana.";
            }
            catch (Exception ex)
            {
                return $"BŁĄD ZAPISU/KOMPILACJI (DRY RUN): {ex.Message}";
            }
        }

        public List<string> Examples => null;
    }
}
