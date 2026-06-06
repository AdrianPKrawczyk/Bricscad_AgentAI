using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ReadProjectFileTool : IToolV2
    {
        private static readonly string[] AllowedExtensions = { ".txt", ".md", ".qmd", ".json", ".xml", ".csv" };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ReadProjectFile",
                    Description = "Czyta zawartość bezpiecznego pliku tekstowego z folderu aktywnego projektu CAD. Użyj tego, aby zapoznać się z zawartością przed jej modyfikacją. Dozwolone formaty: txt, md, qmd, json, xml, csv. CRITICAL: ZAKAZ odczytu oficjalnych notatek *.ai_note.md.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "FileName", new ToolParameter { Type = "string", Description = "Tylko nazwa pliku (np. 'raport.csv'). C# sam doklei odpowiednią ścieżkę absolutną." } }
                        },
                        Required = new List<string> { "FileName" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string fileName = args["FileName"]?.ToString();
            
            if (string.IsNullOrEmpty(fileName))
                return "BŁĄD: Parametr 'FileName' jest wymagany.";

            if (fileName.EndsWith(".ai_note.md", StringComparison.OrdinalIgnoreCase))
                return "CRITICAL ERROR: Nie masz uprawnień do bezpośredniej edycji plików notatek AI. Musisz delegować to zadanie do profilu 'NotesProfile' przy użyciu narzędzia DelegateTaskTool.";

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return "BŁĄD: Niedozwolone rozszerzenie pliku. Akceptowane to tylko txt, md, qmd, json, xml, csv.";

            try
            {
                string directoryPath = string.Empty;
                if (doc != null && !string.IsNullOrEmpty(doc.Name) && Path.IsPathRooted(doc.Name))
                {
                    directoryPath = Path.GetDirectoryName(doc.Name);
                }
                else
                {
                    directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bricscad_AgentAI", "TempNotes");
                    if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
                }

                // Dodatkowe zabezpieczenie przeciwko Path Traversal (np. FileName = "../../windows/system32/cmd.exe")
                string safeFileName = Path.GetFileName(fileName);
                string fullPath = Path.Combine(directoryPath, safeFileName);

                if (!File.Exists(fullPath))
                    return $"BŁĄD: Plik '{safeFileName}' nie istnieje w folderze projektu ({directoryPath}).";

                string content = File.ReadAllText(fullPath);
                return $"Odczytano plik: {safeFileName}\n\n=== ZAWARTOŚĆ ===\n{content}";
            }
            catch (Exception ex)
            {
                return $"BŁĄD I/O podczas czytania pliku: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string> { "{ \"FileName\": \"dane.csv\" }" };
    }
}
