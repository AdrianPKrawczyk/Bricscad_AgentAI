using System;
using System.Collections.Generic;
using System.IO;
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
                                "CSharpCode", new ToolParameter
                                {
                                    Type = "string",
                                    Description = "Płaski, gotowy kod C#. CRITICAL SCRIPTING RULE: DO NOT wrap your C# code in any class, namespace, or method definitions (NO 'public class', NO 'public string Execute'). You MUST write FLAT, top-level script statements only. Parametry pobierasz WYŁĄCZNIE ze słownika 'Inputs', np. Inputs[\"velocity\"].ToString(). Nie istnieją one jako zmienne lokalne! Use UnitsNet classes and CultureInfo.InvariantCulture for parsing. UWAGA: Jeśli nie jesteś w 100% pewien, jak w bibliotece UnitsNet nazywa się dana wielkość fizyczna, UŻYJ NAJPIERW NARZĘDZIA SearchUnitsNetTool. CRITICAL: NEVER use 'UnitsNet.Parse<T>()' - this method DOES NOT EXIST! You must use the exact class name's Parse method, exactly as shown in SearchUnitsNetTool (e.g., 'UnitsNet.Speed.Parse(Inputs[\"velocity\"].ToString(), ... )'). Formuła MUSI na końcu zwracać wartość jako STRING. BARDZO WAŻNE: NIGDY nie ucinaj i nie streszczaj kodu. Musisz podać PEŁNY kod!"
                                }
                            }
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

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(code))
            {
                return "BŁĄD: Parametry FormulaId oraz CSharpCode są wymagane.";
            }

            var meta = new FormulaMetadata
            {
                FormulaId = id,
                Description = description,
                OutputDescription = outputDescription,
                RequiredInputs = new List<FormulaInputDef>()
            };

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
