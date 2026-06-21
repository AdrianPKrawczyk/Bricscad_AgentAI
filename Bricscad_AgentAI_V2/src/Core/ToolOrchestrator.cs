using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;

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

        public List<ToolDefinition> GetToolsPayloadForProfile(string profileName)
        {
            if (ToolConfigManager.GetProfiles().TryGetValue(profileName, out var profile))
            {
                var allowedTools = new HashSet<string>(profile.AllowedTools ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                var allowedTags = new HashSet<string>(profile.AllowedTags ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

                return _tools.Values
                    .Select(t => t.GetToolSchema())
                    .Where(schema => 
                    {
                        if (schema?.Function == null) return false;
                        string toolName = schema.Function.Name;

                        // 1. Bezpośrednio dozwolone w profilu
                        if (allowedTools.Contains(toolName)) return true;

                        // 2. Aktywowane dynamicznie w sesji
                        if (ToolConfigManager.SessionDynamicTags.Contains(toolName)) return true;

                        // 3. Sprawdzenie tagów narzędzia z dozwolonymi/dynamicznymi tagami
                        var toolSettings = ToolConfigManager.GetSettings(toolName);
                        if (toolSettings != null && !string.IsNullOrEmpty(toolSettings.Tags))
                        {
                            var toolTags = toolSettings.Tags.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                                           .Select(tag => tag.Trim().ToLower());
                            foreach (var tag in toolTags)
                            {
                                if (allowedTags.Contains(tag) || ToolConfigManager.SessionDynamicTags.Contains(tag))
                                    return true;
                            }
                        }

                        return false;
                    })
                    .ToList();
            }
            return new List<ToolDefinition>();
        }

        public string ExecuteTool(string toolName, JObject arguments, IExecutionContext context, string callerProfile = "SupervisorProfile")
        {
            // OCHRONA PRZED INFINITE LOOP (Agent Inception)
            if (toolName.Equals("DelegateTask", StringComparison.OrdinalIgnoreCase) &&
                !callerProfile.Equals("SupervisorProfile", StringComparison.OrdinalIgnoreCase))
            {
                BielikLogger.LogWarn($"[TOOL WARN] Próba uruchomienia DelegateTask przez profil '{callerProfile}'.");
                return "BŁĄD KRYTYCZNY (ZABEZPIECZENIE ARCHITEKTONICZNE): Tylko główny profil 'SupervisorProfile' ma uprawnienia do delegowania zadań. Nie możesz używać tego narzędzia.";
            }

            // ---- OCHRONA AUDITOR PROFILE (READ-ONLY) ----
            // Profil z IsReadOnly=true (np. AuditorProfile) nie może wywoływać narzędzi mutujących.
            // Sprawdzenie wykonywane jest ZANIM narzędzie zostanie w ogóle zlookupowane,
            // więc chroni to też przed pomyłkowym dopuszczeniem do AllowedTools.
            if (!string.IsNullOrEmpty(callerProfile))
            {
                var callerConfig = ToolConfigManager.GetProfiles() != null
                    && ToolConfigManager.GetProfiles().TryGetValue(callerProfile, out var pc) ? pc : null;
                if (callerConfig != null && callerConfig.IsReadOnly && WorkValidator.MutatingTools.Contains(toolName))
                {
                    BielikLogger.LogWarn($"[TOOL WARN] Profil '{callerProfile}' (IsReadOnly=true) próbował wywołać mutujące narzędzie '{toolName}'.");
                    return $"BŁĄD KRYTYCZNY (ZABEZPIECZENIE AUDYTORA): Profil '{callerProfile}' ma tryb read-only i nie może wywoływać mutującego narzędzia '{toolName}'. Użyj wyłącznie narzędzi odczytowych (InspectEntity, GetPropertiesTool, AnalyzeSelectionTool, ReadPropertyTool, ReadFromBlackboard, ListBlocks, ReadXData, FindXData, ReadTextSampleTool, ReadSelectedBlockInfo).";
                }
            }
            // -------------------------------------------

            // ---- NOWY KOD BŁOKADY ZAZNACZENIA ----
            if (toolName.Equals("SelectEntities", StringComparison.OrdinalIgnoreCase) && AgentMemoryState.IsSelectionScopeLocked)
            {
                // Domyślnie Scope to "Model", chyba że przekazano "Blocks" do filtrowania zagnieżdżonego
                string scope = arguments?["Scope"]?.ToString() ?? "Model";

                if (scope.Equals("Model", StringComparison.OrdinalIgnoreCase))
                {
                    BielikLogger.LogWarn($"[TOOL WARN] Subagent próbuje nadpisać zaznaczenie, gdy SelectionScopeLock = true.");
                    return "BŁĄD KRYTYCZNY: Modyfikacja zaznaczenia jest zablokowana przez Głównego Supervisora! Nie używaj narzędzia 'SelectEntities' do szukania w całym modelu. Używaj narzędzi operujących bezpośrednio na istniejącym zaznaczeniu (np. EditAttributes z Target='Selection').";
                }
            }
            // --------------------------------------

            if (!_tools.TryGetValue(toolName, out var tool))
            {
                BielikLogger.LogWarn($"[TOOL WARN] Próba wywołania uśpionego narzędzia: {toolName}");
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
                        BielikLogger.LogInfo($"[TOOL DYNAMIC] Żądanie dynamicznego ładowania tagu/kategorii: {categoryName}");
                        ToolConfigManager.SessionDynamicTags.Add(categoryName);
                    }
                }
            }

            try
            {
                string argsStr = arguments?.ToString(Newtonsoft.Json.Formatting.None);
                if (argsStr != null && argsStr.Length > 250) argsStr = argsStr.Substring(0, 250) + "...";
                BielikLogger.LogInfo($"[TOOL START] Wywołanie: {toolName}, Argumenty: {argsStr}");

                var cadContext = context as CadExecutionContext;
                string result = null;

                // ============== CHAIN OF EVIDENCE (Auditor Filar 2) ==============
                // Dla narzedzi mutujacych (z wyjatkiem AuditorProfile, ktory jest read-only
                // i nie dojdzie do tego miejsca) zbieramy snapshot wlasciwosci PRZED wywolaniem
                // dla kazdego Handle w ActiveSelection. Po wywolaniu zbieramy AFTER dla
                // wszystkich Handle z ModifiedEntities (nowo dodane + zmodyfikowane).
                // Cap 64 Handle/sesje - chroni kontekst Rewidenta przed eksplozja
                // (np. ManageLayers modyfikujacy 500 obiektow - Twoja notatka techniczna).
                bool isMutating = WorkValidator.MutatingTools.Contains(toolName);
                var beforeTargets = new List<string>();
                int mutationsBefore = AgentMemoryState.MutationCount;
                if (isMutating && AgentMemoryState.EvidenceEnabled)
                {
                    var currentSel = AgentMemoryState.ActiveSelection;
                    int cap = Math.Min(currentSel.Length, AgentMemoryState.MaxEvidenceHandles);
                    for (int i = 0; i < cap; i++)
                    {
                        var id = currentSel[i];
                        if (id.IsNull) continue;
                        var snap = EngineTracer.CaptureSnapshot(id, "before");
                        if (snap == null) continue;
                        EngineTracer.WriteSnapshotToBlackboard(snap, "before");
                        beforeTargets.Add(snap.Handle);
                    }
                }
                // ================================================================

                // WYMUSZENIE GŁÓWNEGO WĄTKU (Main Thread) dla operacji CAD/ACIS
                if (System.Windows.Application.Current != null && System.Windows.Application.Current.Dispatcher != null)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        try
                        {
                            result = tool.Execute(cadContext?.CadDocument, arguments);
                        }
                        catch (Exception ex)
                        {
                            result = $"Błąd wykonania (wątek UI): {ex.Message}";
                        }
                    });
                }
                else
                {
                    result = tool.Execute(cadContext?.CadDocument, arguments);
                }

                // ============== CHAIN OF EVIDENCE - AFTER ==============
                if (isMutating && AgentMemoryState.EvidenceEnabled)
                {
                    int mutationsAfter = AgentMemoryState.MutationCount;
                    if (mutationsAfter > mutationsBefore)
                    {
                        // Nowe lub zmodyfikowane obiekty od tego wywolania
                        var modified = AgentMemoryState.GetModifiedEntitiesSnapshot();
                        int take = Math.Min(modified.Length, AgentMemoryState.MaxEvidenceHandles);
                        for (int i = 0; i < take; i++)
                        {
                            var id = modified[i];
                            if (id.IsNull) continue;
                            var snap = EngineTracer.CaptureSnapshot(id, "after");
                            if (snap == null) continue;
                            EngineTracer.WriteSnapshotToBlackboard(snap, "after");
                        }
                    }
                }
                // =======================================================

                string resPreview = result;
                if (resPreview != null && resPreview.Length > 150) resPreview = resPreview.Substring(0, 150) + "...";
                BielikLogger.LogInfo($"[TOOL END] Sukces: {toolName}, Wynik: {resPreview}");
                return result;
            }
            catch (Exception ex)
            {
                BielikLogger.LogError($"[TOOL ERR] Błąd krytyczny w narzędziu '{toolName}': {ex.Message}", ex);
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
