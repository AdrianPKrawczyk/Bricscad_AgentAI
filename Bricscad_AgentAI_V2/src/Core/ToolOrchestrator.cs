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

        private static ToolOrchestrator _instance;
        private static readonly object _lock = new object();

        /// <summary>
        /// Globalna, bezpieczna wątkowo instancja orkiestratora.
        /// Automatycznie inicjalizuje się przy pierwszym wywołaniu.
        /// </summary>
        public static ToolOrchestrator Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ToolOrchestrator();
                            _instance.Initialize();
                        }
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Prywatny konstruktor zapobiega tworzeniu instancji przez 'new' z zewnątrz.
        /// </summary>
        private ToolOrchestrator() { }

        /// <summary>
        /// Automatycznie skanuje bieżący zestaw klas w poszukiwaniu implementacji IToolV2.
        /// </summary>
        private void Initialize()
        {
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
                    System.Diagnostics.Debug.WriteLine($"Błąd inicjalizacji narzędzia {type.Name}: {ex.Message}");
                }
            }

            // Inicjalizacja konfiguracji dynamicznej na podstawie wykrytych narzędzi
            ToolConfigManager.Initialize(_tools.Values);
        }

        /// <summary>
        /// Wymusza ponowne przeskanowanie narzędzi (np. po zmianie konfiguracji).
        /// </summary>
        public void RefreshTools()
        {
            Initialize();
        }

        public List<ToolDefinition> GetToolsPayload(IEnumerable<string> requestedTags)
        {
            return _tools.Values
                .Select(t => t.GetToolSchema())
                .Where(schema => schema?.Function != null && ToolConfigManager.IsToolActive(schema.Function.Name, requestedTags))
                .ToList();
        }

        public List<ToolDefinition> GetToolsPayload()
        {
            return GetToolsPayload(null);
        }

        public string ExecuteTool(string toolName, JObject arguments, Document doc)
        {
            if (!_tools.TryGetValue(toolName, out var tool))
            {
                return $"BŁĄD KRYTYCZNY (ZŁAMANIE PROTOKOŁU): Narzędzie '{toolName}' jest obecnie uśpione. MUSISZ najpierw wywołać narzędzie 'RequestAdditionalTools'.";
            }

            // PRZECHWYCENIE: Dynamiczne ładowanie narzędzi do sesji w locie
            if (toolName.Equals("RequestAdditionalTools", StringComparison.OrdinalIgnoreCase))
            {
                string action = arguments["Action"]?.ToString() ?? "";
                if (action.Equals("LoadCategory", StringComparison.OrdinalIgnoreCase))
                {
                    string categoryName = arguments["CategoryName"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(categoryName))
                    {
                        ToolConfigManager.SessionDynamicTags.Add(categoryName);
                    }
                }
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

        public IEnumerable<IToolV2> GetRegisteredTools()
        {
            return _tools.Values;
        }
    }
}
