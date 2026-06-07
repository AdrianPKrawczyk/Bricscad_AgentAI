using System;
using System.Collections.Generic;
using System.IO;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Models;
using Bricscad_AgentAI_V2.Core;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class WriteQAReportTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "WriteQAReport",
                    Description = "Zapisuje sformatowany raport (notatkę Markdown) po wykonanym audycie. Zostanie zapisany w Bricscad_AgentAI_V2/Autotesty/Reports/.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "FileName", new ToolParameter { Type = "string", Description = "Nazwa pliku np. 'Audit_InsertBlockTool.md'." } },
                            { "Content", new ToolParameter { Type = "string", Description = "Zawartość raportu w Markdown." } }
                        },
                        Required = new List<string> { "FileName", "Content" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string fileName = args["FileName"]?.ToString();
            string content = args["Content"]?.ToString();
            
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(content))
                return "BŁĄD: Parametry 'FileName' i 'Content' są wymagane.";

            try
            {
                string baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                string rootDir = Path.GetFullPath(Path.Combine(baseDir, "..", ".."));
                string reportDir = Path.Combine(rootDir, "Autotesty", "Reports");
                
                if (!Directory.Exists(reportDir))
                    Directory.CreateDirectory(reportDir);
                
                string safeFileName = Path.GetFileName(fileName);
                if (!safeFileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    safeFileName += ".md";

                string targetFile = Path.Combine(reportDir, safeFileName);
                File.WriteAllText(targetFile, content);

                return $"SUKCES: Raport został zapisany pod adresem: {targetFile}";
            }
            catch (Exception ex)
            {
                return $"BŁĄD I/O: {ex.Message}";
            }
        }

        public List<string> Examples => new List<string> { "{ \"FileName\": \"Test1.md\", \"Content\": \"# Raport...\" }" };
    }
}
