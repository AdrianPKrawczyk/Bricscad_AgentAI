using System;
using System.Collections.Generic;
using System.IO;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class DelegateTaskToAntigravityTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "DelegateTaskToAntigravity",
                    Description = "Tworzy oficjalne zlecenie naprawy/zmiany w kodzie dla zewnętrznego Agenta Kodowania (Antigravity AI). Użyj tego, aby zlecić fizyczne poprawienie błędu w plikach .cs.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "TaskTitle", new ToolParameter { Type = "string", Description = "Krótki tytuł zadania (np. 'Naprawa błędu NaN w InsertBlockTool')." } },
                            { "TargetFiles", new ToolParameter { Type = "string", Description = "Pliki, które wymagają edycji (np. 'src/Tools/InsertBlockTool.cs')." } },
                            { "TaskDescription", new ToolParameter { Type = "string", Description = "Szczegółowy opis dla Antigravity: co ma zmienić, jaki kod napisać i w jakim pliku." } }
                        },
                        Required = new List<string> { "TaskTitle", "TargetFiles", "TaskDescription" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string title = args["TaskTitle"]?.ToString();
            string targetFiles = args["TargetFiles"]?.ToString();
            string description = args["TaskDescription"]?.ToString();
            
            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description))
                return "BŁĄD: Parametry 'TaskTitle' i 'TaskDescription' są wymagane.";

            try
            {
                string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string rootDir = Path.GetFullPath(Path.Combine(baseDir, "..", ".."));
                string tasksDir = Path.Combine(rootDir, "Autotesty", "TasksForAntigravity");
                
                if (!Directory.Exists(tasksDir))
                    Directory.CreateDirectory(tasksDir);
                
                string safeFileName = $"Task_{DateTime.Now:yyyyMMdd_HHmmss}.md";
                string targetFile = Path.Combine(tasksDir, safeFileName);
                
                string content = $"# ZLECENIE DLA ANTIGRAVITY AI\n\n";
                content += $"**Data Zlecenia:** {DateTime.Now}\n";
                content += $"**Tytuł:** {title}\n";
                content += $"**Pliki Docelowe:** {targetFiles}\n\n";
                content += $"## Opis Problemu i Oczekiwane Zmiany\n{description}\n\n";
                content += $"---\n*Wygenerowano przez BricsCAD QA Agent*";

                File.WriteAllText(targetFile, content);

                return $"SUKCES: Zlecenie dla Antigravity zostało utworzone w pliku: {targetFile}. Użytkownik musi powiadomić Antigravity, aby wykonał to zadanie.";
            }
            catch (Exception ex)
            {
                return $"BŁĄD I/O: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string> { "{ \"TaskTitle\": \"Dodaj metodę Foo\", \"TargetFiles\": \"src/Core/Bar.cs\", \"TaskDescription\": \"Dodaj logikę...\" }" };
    }
}
