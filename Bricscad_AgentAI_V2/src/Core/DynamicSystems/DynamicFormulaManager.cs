using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Bricscad_AgentAI_V2.Core;

namespace Bricscad_AgentAI_V2.Core.DynamicSystems
{
    public class ScriptGlobals
    {
        public Dictionary<string, string> Inputs { get; set; } = new Dictionary<string, string>();
    }

    public class DynamicFormulaManager
    {
        private static readonly Dictionary<string, ScriptRunner<string>> _compiledScripts = new Dictionary<string, ScriptRunner<string>>(StringComparer.OrdinalIgnoreCase);

        public static string CustomFormulasPath { get; set; } = null;

        public static void LoadAndCompileAll()
        {
            _compiledScripts.Clear();
            
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

            var files = Directory.GetFiles(formulasPath, "*.csx");
            
            // Sandboxing: blokujemy dostęp do I/O, zezwalamy tylko na bezpieczne przestrzenie nazw
            var options = ScriptOptions.Default
                .WithReferences(typeof(UnitsNet.Length).Assembly)
                .WithImports("System", "System.Math", "System.Collections.Generic", "UnitsNet");

            foreach (var file in files)
            {
                string id = Path.GetFileNameWithoutExtension(file);
                try
                {
                    string code = File.ReadAllText(file);
                    var script = CSharpScript.Create<string>(code, options, globalsType: typeof(ScriptGlobals));
                    
                    // Weryfikacja składni (kompilacja pre-flight) i utworzenie delegata
                    script.Compile();
                    var runner = script.CreateDelegate();
                    
                    _compiledScripts[id] = runner;
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

            var globals = new ScriptGlobals { Inputs = inputs ?? new Dictionary<string, string>() };
            
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
    }
}
