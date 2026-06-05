using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Newtonsoft.Json;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Core.DynamicSystems
{
    public class ScriptGlobals
    {
        public Dictionary<string, string> Inputs { get; set; } = new Dictionary<string, string>();
        public IDatasetProvider Data { get; set; }
    }

    public class DynamicFormulaManager
    {
        private static readonly Dictionary<string, ScriptRunner<string>> _compiledScripts = new Dictionary<string, ScriptRunner<string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, FormulaMetadata> _metadata = new Dictionary<string, FormulaMetadata>(StringComparer.OrdinalIgnoreCase);

        public static string CustomFormulasPath { get; set; } = null;

        public static void LoadAndCompileAll()
        {
            _compiledScripts.Clear();
            _metadata.Clear();
            
            string formulasPath;
            if (!string.IsNullOrEmpty(CustomFormulasPath))
            {
                formulasPath = CustomFormulasPath;
            }
            else
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                formulasPath = Path.Combine(appData, "Bricscad_AgentAI", "CustomKnowledge", "Formulas");
            }

            if (!Directory.Exists(formulasPath))
            {
                BielikLogger.LogInfo("Folder Formulas nie istnieje. Pomijam ładowanie formuł.");
                return;
            }

            var files = Directory.GetFiles(formulasPath, "*.csx", SearchOption.AllDirectories);
            
            var options = ScriptOptions.Default
                .WithReferences(typeof(UnitsNet.Length).Assembly, typeof(Newtonsoft.Json.Linq.JObject).Assembly)
                .WithImports("System", "System.Math", "System.Collections.Generic", "UnitsNet", "System.Globalization", "Newtonsoft.Json.Linq");

            foreach (var file in files)
            {
                string id = Path.GetFileNameWithoutExtension(file);
                try
                {
                    string code = File.ReadAllText(file);
                    var script = CSharpScript.Create<string>(code, options, globalsType: typeof(ScriptGlobals));
                    script.Compile();
                    var runner = script.CreateDelegate();
                    _compiledScripts[id] = runner;

                    string jsonFile = Path.ChangeExtension(file, ".json");
                    if (File.Exists(jsonFile))
                    {
                        string json = File.ReadAllText(jsonFile);
                        var meta = JsonConvert.DeserializeObject<FormulaMetadata>(json);
                        if (meta != null) _metadata[id] = meta;
                    }
                    else
                    {
                        BielikLogger.LogInfo($"[Roslyn] Brak metadanych JSON dla formuły: {id}");
                    }

                    BielikLogger.LogInfo($"[Roslyn] Skompilowano pomyślnie formułę: {id}");
                }
                catch (Exception ex)
                {
                    BielikLogger.LogError($"[Roslyn] Błąd kompilacji formuły '{id}'", ex);
                }
            }
            
            BielikLogger.LogInfo($"[Roslyn] Załadowano {_compiledScripts.Count} formuł z dysku.");
        }

        public static async Task<string> ExecuteFormula(string id, Dictionary<string, string> inputs)
        {
            if (!_compiledScripts.TryGetValue(id, out var runner))
            {
                throw new KeyNotFoundException($"Nie znaleziono skompilowanej formuły o ID: {id}");
            }

            var globals = new ScriptGlobals 
            { 
                Inputs = inputs ?? new Dictionary<string, string>(),
                Data = new DatasetManager() 
            };
            
            try
            {
                string result = await runner(globals);
                return result;
            }
            catch (Exception ex)
            {
                BielikLogger.LogError($"[Roslyn] Błąd wykonywania formuły '{id}'", ex);
                throw;
            }
        }
        
        public static IEnumerable<string> GetAvailableFormulas()
        {
            return _compiledScripts.Keys;
        }

        public static FormulaMetadata GetMetadata(string id)
        {
            if (_metadata.TryGetValue(id, out var meta)) return meta;
            return null;
        }

        public static bool DeleteFormula(string id)
        {
            string formulasPath = AppPaths.GetFormulasPath();
            var csxFiles = Directory.GetFiles(formulasPath, $"{id}.csx", SearchOption.AllDirectories);
            
            if (csxFiles.Length > 0)
            {
                string csxPath = csxFiles[0];
                string jsonPath = Path.ChangeExtension(csxPath, ".json");

                File.Delete(csxPath);
                _compiledScripts.Remove(id);
                if (File.Exists(jsonPath)) File.Delete(jsonPath);
                _metadata.Remove(id);
                BielikLogger.LogInfo($"[Roslyn] Usunięto formułę: {id}");
                return true;
            }
            else
            {
                return false;
            }
        }

        public static void SaveFormula(string id, string code, string jsonMetadata)
        {
            var options = ScriptOptions.Default
                .WithReferences(typeof(UnitsNet.Length).Assembly, typeof(Newtonsoft.Json.Linq.JObject).Assembly)
                .WithImports("System", "System.Math", "System.Collections.Generic", "UnitsNet", "System.Globalization", "Newtonsoft.Json.Linq");

            var script = CSharpScript.Create<string>(code, options, globalsType: typeof(ScriptGlobals));
            var diagnostics = script.Compile();
            if (diagnostics.Length > 0)
            {
                bool hasErrors = false;
                var errorMsg = new System.Text.StringBuilder("BŁĄD KOMPILACJI:\n");
                foreach (var diag in diagnostics)
                {
                    if (diag.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
                    {
                        hasErrors = true;
                        errorMsg.AppendLine(diag.ToString());
                    }
                }
                if (hasErrors) throw new InvalidOperationException(errorMsg.ToString());
            }

            FormulaMetadata meta = null;
            if (!string.IsNullOrWhiteSpace(jsonMetadata))
            {
                try
                {
                    meta = JsonConvert.DeserializeObject<FormulaMetadata>(jsonMetadata);
                    if (meta == null) throw new InvalidOperationException("Pusty obiekt metadanych.");
                    meta.FormulaId = id;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"BŁĄD JSON: {ex.Message}");
                }
            }

            string formulasPath = AppPaths.GetFormulasPath();
            string targetDir = formulasPath;
            if (meta != null && !string.IsNullOrWhiteSpace(meta.Category) && meta.Category != "Uncategorized")
            {
                targetDir = Path.Combine(formulasPath, meta.Category);
            }
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            // Jeśli plik już istniał w innym miejscu, to go przenieś/usuń
            var existingFiles = Directory.GetFiles(formulasPath, $"{id}.csx", SearchOption.AllDirectories);
            if (existingFiles.Length > 0)
            {
                string oldDir = Path.GetDirectoryName(existingFiles[0]);
                if (oldDir != targetDir)
                {
                    File.Delete(existingFiles[0]);
                    string oldJson = Path.ChangeExtension(existingFiles[0], ".json");
                    if (File.Exists(oldJson)) File.Delete(oldJson);
                }
            }

            string csxPath = Path.Combine(targetDir, $"{id}.csx");
            string jsonPath = Path.Combine(targetDir, $"{id}.json");

            File.WriteAllText(csxPath, code);
            if (meta != null)
            {
                File.WriteAllText(jsonPath, JsonConvert.SerializeObject(meta, Formatting.Indented));
            }

            _compiledScripts[id] = script.CreateDelegate();
            if (meta != null) _metadata[id] = meta;
            
            BielikLogger.LogInfo($"[Roslyn] Zapisano formułę: {id}");
        }
    }
}
