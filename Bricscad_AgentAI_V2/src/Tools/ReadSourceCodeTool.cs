using System;
using System.Collections.Generic;
using System.IO;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ReadSourceCodeTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ReadSourceCode",
                    Description = "Czyta zawartość pliku źródłowego z folderu aplikacji BricsCAD AgentAI. Używaj tego narzędzia do odczytu kodu np. src/Tools/InsertBlockTool.cs.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "RelativePath", new ToolParameter { Type = "string", Description = "Względna ścieżka do pliku (np. 'src/Tools/ForeachTool.cs')." } }
                        },
                        Required = new List<string> { "RelativePath" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string relativePath = args["RelativePath"]?.ToString();
            
            if (string.IsNullOrEmpty(relativePath))
                return "BŁĄD: Parametr 'RelativePath' jest wymagany.";

            try
            {
                string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string rootDir = Path.GetFullPath(Path.Combine(baseDir, "..", ".."));
                
                string targetFile = Path.GetFullPath(Path.Combine(rootDir, relativePath));

                if (!targetFile.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
                {
                    return "BŁĄD: Próba dostępu poza dozwolony katalog projektu.";
                }

                if (!File.Exists(targetFile))
                    return $"BŁĄD: Plik '{relativePath}' nie istnieje.";

                string content = File.ReadAllText(targetFile);
                return $"Odczytano plik: {relativePath}\n\n=== ZAWARTOŚĆ ===\n{content}";
            }
            catch (Exception ex)
            {
                return $"BŁĄD I/O: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string> { "{ \"RelativePath\": \"src/Core/ToolOrchestrator.cs\" }" };
    }
}
