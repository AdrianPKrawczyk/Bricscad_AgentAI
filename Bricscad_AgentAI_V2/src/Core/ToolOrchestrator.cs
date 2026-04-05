using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Zarządca narzędzi (Orkiestrator). Odpowiada za rejestrację, 
    /// generowanie schematów dla LLM oraz wywoływanie konkretnych funkcji.
    /// </summary>
    public class ToolOrchestrator
    {
        private readonly Dictionary<string, IToolV2> _tools = new Dictionary<string, IToolV2>();

        /// <summary>
        /// Globalna instancja orkiestratora dla narzędzi rekurencyjnych (np. Foreach).
        /// </summary>
        public static ToolOrchestrator Instance { get; private set; }

        /// <summary>
        /// Automatycznie skanuje bieżący zestaw klas w poszukiwaniu implementacji IToolV2.
        /// </summary>
        public void Initialize()
        {
            Instance = this;
            _tools.Clear();
            
            var toolType = typeof(IToolV2);
            var types = Assembly.GetExecutingAssembly().GetTypes()
                .Where(p => toolType.IsAssignableFrom(p) && !p.IsInterface && !p.IsAbstract);

            foreach (var type in types)
            {
                try
                {
                    var instance = (IToolV2)Activator.CreateInstance(type);
                    var schema = instance.GetToolSchema();
                    
                    if (schema?.Function?.Name != null)
                    {
                        _tools[schema.Function.Name] = instance;
                    }
                }
                catch (Exception ex)
                {
                    // W fazie deweloperskiej wypisujemy błąd inicjalizacji konkretnego narzędzia
                    System.Diagnostics.Debug.WriteLine($"Błąd inicjalizacji narzędzia {type.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Generuje listę narzędzi w formacie gotowym do wysłania w polu 'tools' do API LLM.
        /// </summary>
        public List<ToolDefinition> GetToolsPayload()
        {
            return _tools.Values.Select(t => t.GetToolSchema()).ToList();
        }

        /// <summary>
        /// Wywołuje wskazane narzędzie na podstawie nazwy rzuconej przez LLM.
        /// </summary>
        /// <param name="toolName">Nazwa funkcji z tool_calls.</param>
        /// <param name="arguments">Zdeserializowane argumenty JSON.</param>
        /// <param name="doc">Aktywny dokument CAD.</param>
        /// <returns>Wynik działania narzędzia (string) do przekazania z powrotem do Agenta.</returns>
        public string ExecuteTool(string toolName, JObject arguments, Document doc)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
            {
                return $"Błąd: Narzędzie o nazwie '{toolName}' nie zostało znalezione w systemie.";
            }

            try
            {
                return tool.Execute(doc, arguments);
            }
            catch (Exception ex)
            {
                return $"Błąd wykonania narzędzia '{toolName}': {ex.Message}";
            }
        }

        public string GetRegisteredToolsInfo()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Dostępne Narzędzia V2:");
            foreach (var kvp in _tools)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value.GetToolSchema()?.Function?.Description}");
            }
            return sb.ToString();
        }
    }
}
