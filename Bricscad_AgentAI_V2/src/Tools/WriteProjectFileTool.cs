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
    public class WriteProjectFileTool : IToolV2
    {
        private static readonly string[] AllowedExtensions = { ".txt", ".md", ".qmd", ".json", ".xml", ".csv" };

        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "WriteProjectFile",
                    Description = "Zapisuje lub nadpisuje plik tekstowy w folderze aktywnego projektu CAD. Używaj go do tworzenia raportów, zestawień i plików konfiguracyjnych. Dozwolone formaty: txt, md, qmd, json, xml, csv. CRITICAL: ZABRONIONE jest używanie tego narzędzia do edycji oficjalnej notatki rysunku (*.ai_note.md). W tym celu użyj DelegateTaskTool.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "FileName", new ToolParameter { Type = "string", Description = "Tylko nazwa pliku (np. 'raport.csv'). C# sam doklei odpowiednią ścieżkę absolutną." } },
                            { "Content", new ToolParameter { Type = "string", Description = "Zawartość tekstowa pliku, która zostanie zapisana (nadpisze plik jeśli istnieje)." } }
                        },
                        Required = new List<string> { "FileName", "Content" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string fileName = args["FileName"]?.ToString();
            string content = args["Content"]?.ToString() ?? string.Empty;
            
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

                // Dodatkowe zabezpieczenie przeciwko Path Traversal
                string safeFileName = Path.GetFileName(fileName);
                string fullPath = Path.Combine(directoryPath, safeFileName);

                File.WriteAllText(fullPath, content);
                return $"SUKCES: Zapisano plik '{safeFileName}' w folderze projektu ({directoryPath}).";
            }
            catch (Exception ex)
            {
                return $"BŁĄD I/O podczas zapisu pliku: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string> { "{ \"FileName\": \"raport.md\", \"Content\": \"# Raport...\" }" };
    }
}
