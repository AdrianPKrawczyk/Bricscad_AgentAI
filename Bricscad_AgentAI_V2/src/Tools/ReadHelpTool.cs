using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class ReadHelpTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "ReadHelp",
                    Description = "Pozwala na odczytywanie wbudowanej dokumentacji i plików pomocy wtyczki BricsCAD Agent AI. Narzędzie zwraca listę dostępnych plików pomocy lub szczegółową zawartość konkretnego pliku Markdown.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "FileName", new ToolParameter { Type = "string", Description = "Nazwa pliku z pomocą do odczytania (np. 'USER_GUIDE.md'). Jeśli parametr zostanie pominięty, narzędzie zwróci listę wszystkich dostępnych plików z pliku index.json." } }
                        },
                        Required = new List<string>() // Opcjonalne
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string fileName = args["FileName"]?.ToString();
            
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string helpDir = Path.Combine(exeDir, "resources", "help");

            if (!Directory.Exists(helpDir))
            {
                return "BŁĄD: Katalog pomocy nie istnieje.";
            }

            if (string.IsNullOrEmpty(fileName))
            {
                // Odczyt spisu treści
                string indexFile = Path.Combine(helpDir, "index.json");
                if (!File.Exists(indexFile))
                {
                    return "BŁĄD: Nie znaleziono pliku indeksu pomocy (index.json).";
                }

                try
                {
                    string indexContent = File.ReadAllText(indexFile);
                    return "SPIS TREŚCI POMOCY:\n" + indexContent + "\n\nUżyj tego samego narzędzia podając 'FileName' z powyższej listy, aby przeczytać pełną zawartość danego poradnika.";
                }
                catch (Exception ex)
                {
                    return "BŁĄD przy odczycie indeksu: " + ex.Message;
                }
            }
            else
            {
                // Odczyt pliku MD
                string filePath = Path.Combine(helpDir, fileName);
                
                // Zabezpieczenie przed wychodzeniem poza katalog (path traversal)
                string fullPath = Path.GetFullPath(filePath);
                if (!fullPath.StartsWith(Path.GetFullPath(helpDir), StringComparison.OrdinalIgnoreCase))
                {
                    return "BŁĄD: Próba dostępu do pliku poza katalogiem pomocy jest zabroniona.";
                }

                if (!File.Exists(filePath))
                {
                    return $"BŁĄD: Plik '{fileName}' nie istnieje w katalogu pomocy.";
                }

                try
                {
                    string content = File.ReadAllText(filePath);
                    return $"--- ZAWARTOŚĆ PLIKU {fileName} ---\n\n" + content;
                }
                catch (Exception ex)
                {
                    return "BŁĄD przy odczycie pliku pomocy: " + ex.Message;
                }
            }
        }

        public List<string> Examples => new List<string>
        {
            "{ }", // Pobranie spisu
            "{ \"FileName\": \"USER_GUIDE.md\" }" // Czytanie pliku
        };
    }
}
