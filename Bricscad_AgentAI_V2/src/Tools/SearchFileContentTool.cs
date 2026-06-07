using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Core
{
    public class SearchFileContentTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "SearchFileContent",
                    Description = "Przeszukuje zawartość plików w wybranym folderze. Zwraca linie tekstu zawierające poszukiwaną frazę (działa jak systemowy grep). Wymaga ścieżek relatywnych do roota środowiska.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "DirectoryType", new ToolParameter { Type = "string", Enum = new List<string> { "SourceCode", "DrawingFolder" }, Description = "Określa punkt startowy. 'SourceCode' to kod C# pluginu. 'DrawingFolder' to folder aktualnego pliku .dwg." } },
                            { "RelativePath", new ToolParameter { Type = "string", Description = "Opcjonalny podfolder względem punktu startowego (np. 'src/Core' lub 'Notes'). Puste pole oznacza główny folder." } },
                            { "SearchQuery", new ToolParameter { Type = "string", Description = "Fraza do odszukania w treści pliku. Ignoruje wielkość liter." } },
                            { "FileExtension", new ToolParameter { Type = "string", Description = "Opcjonalny filtr rozszerzenia, np. '*.cs' lub '*.md'. Zostaw puste aby przeszukać wszystkie." } }
                        },
                        Required = new List<string> { "DirectoryType", "SearchQuery" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string dirType = args["DirectoryType"]?.ToString();
            string relativePath = args["RelativePath"]?.ToString() ?? "";
            string searchQuery = args["SearchQuery"]?.ToString();
            string fileExtension = args["FileExtension"]?.ToString();
            if (string.IsNullOrEmpty(fileExtension)) fileExtension = "*.*";

            if (string.IsNullOrEmpty(searchQuery))
            {
                return "[BŁĄD]: SearchQuery nie może być puste.";
            }

            try
            {
                string baseDir = "";

                if (dirType == "SourceCode")
                {
                    string execDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    baseDir = Path.GetFullPath(Path.Combine(execDir, "..", ".."));
                }
                else if (dirType == "DrawingFolder")
                {
                    string docPath = doc?.Name;
                    if (string.IsNullOrEmpty(docPath) || docPath.Contains("Rysunek") || docPath.Contains("Drawing"))
                    {
                        return "[BŁĄD]: Rysunek nie jest jeszcze zapisany. Zapisz plik na dysku przed przeszukiwaniem DrawingFolder.";
                    }
                    baseDir = Path.GetDirectoryName(docPath);
                }
                else
                {
                    return "[BŁĄD]: Nieznany typ DirectoryType. Użyj SourceCode lub DrawingFolder.";
                }

                string targetDir = Path.GetFullPath(Path.Combine(baseDir, relativePath));

                if (!targetDir.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                {
                    return "[BŁĄD]: Próba dostępu poza dozwolony katalog.";
                }

                if (!Directory.Exists(targetDir))
                    return $"[BŁĄD]: Katalog '{targetDir}' nie istnieje.";

                var files = Directory.EnumerateFiles(targetDir, fileExtension, SearchOption.AllDirectories);

                List<string> results = new List<string>();
                int matchCount = 0;
                int maxMatches = 150; 

                foreach (var file in files)
                {
                    if (matchCount >= maxMatches)
                    {
                        results.Add("... (osiągnięto limit wyników, zawęż wyszukiwanie) ...");
                        break;
                    }

                    try
                    {
                        var lines = File.ReadLines(file);
                        int lineNum = 1;
                        bool fileHeaderAdded = false;

                        foreach (var line in lines)
                        {
                            if (line.IndexOf(searchQuery, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                if (!fileHeaderAdded)
                                {
                                    results.Add($"\n[{Path.GetFileName(file)}] ({file}):");
                                    fileHeaderAdded = true;
                                }
                                results.Add($"  L{lineNum}: {line.Trim()}");
                                matchCount++;

                                if (matchCount >= maxMatches) break;
                            }
                            lineNum++;
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add($"  [Błąd odczytu {Path.GetFileName(file)}]: {ex.Message}");
                    }
                }

                if (matchCount == 0)
                {
                    return $"Nie znaleziono frazy '{searchQuery}' w folderze: {targetDir}";
                }

                return string.Join("\n", results);
            }
            catch (Exception ex)
            {
                return $"[BŁĄD KRYTYCZNY]: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string> 
        { 
            "{ \"DirectoryType\": \"SourceCode\", \"RelativePath\": \"src\", \"SearchQuery\": \"ToolOrchestrator\", \"FileExtension\": \"*.cs\" }" 
        };
    }
}
