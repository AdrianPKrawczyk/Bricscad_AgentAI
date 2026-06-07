using System;
using System.Collections.Generic;
using System.IO;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ListSourceFilesTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ListSourceFiles",
                    Description = "Listuje pliki źródłowe i podkatalogi wewnątrz wybranego folderu aplikacji BricsCAD AgentAI. Główne foldery to: src, Autotesty.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "RelativePath", new ToolParameter { Type = "string", Description = "Względna ścieżka do folderu (np. 'src/Core', 'src/Tools' lub pusta ścieżka '' dla korzenia)." } }
                        },
                        Required = new List<string> { }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string relativePath = args["RelativePath"]?.ToString() ?? "";
            
            try
            {
                string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string rootDir = Path.GetFullPath(Path.Combine(baseDir, "..", ".."));
                
                string targetDir = Path.GetFullPath(Path.Combine(rootDir, relativePath));

                if (!targetDir.StartsWith(rootDir, StringComparison.OrdinalIgnoreCase))
                {
                    return "BŁĄD: Próba dostępu poza dozwolony katalog projektu.";
                }

                if (!Directory.Exists(targetDir))
                    return $"BŁĄD: Katalog '{relativePath}' nie istnieje.";

                var dirs = Directory.GetDirectories(targetDir);
                var files = Directory.GetFiles(targetDir);

                string result = $"Zawartość folderu: {(string.IsNullOrEmpty(relativePath) ? "/" : relativePath)}\n";
                result += "Katalogi:\n";
                foreach (var d in dirs) result += $"  [DIR]  {Path.GetFileName(d)}\n";
                
                result += "Pliki:\n";
                foreach (var f in files) result += $"  [FILE] {Path.GetFileName(f)}\n";

                return result;
            }
            catch (Exception ex)
            {
                return $"BŁĄD: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string> { "{ \"RelativePath\": \"src/Tools\" }" };
    }
}
