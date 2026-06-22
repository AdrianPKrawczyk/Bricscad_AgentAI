using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.Rewident;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Tools
{
    public class DelegateTaskTool : IToolV2
    {
        public ToolDefinition GetToolSchema()
        {
            return new ToolDefinition
            {
                Type = "function",
                Function = new FunctionSchema
                {
                    Name = "DelegateTask",
                    Description = "Deleguje zadanie do wyspecjalizowanego Agenta Eksperta. UĹĽyj tego, gdy zadanie wykracza poza twoje kompetencje. Po zakoĹ„czeniu zadania, ekspert zwrĂłci wynik.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "TargetProfile", new ToolParameter { Type = "string", Description = "Nazwa profilu docelowego eksperta. Dostępne profile: 'CadProfile' (ogólny), 'CadGeometryProfile' (geometria 2D), 'CadBlocksProfile' (bloki), 'CadMetadataProfile' (właściwości, metadane), 'CadMathProfile' (obliczenia), 'CadLayoutProfile' (arkusze wydruku), 'NotesProfile' (notatki), 'AuditorProfile' (audyt), 'Modeler3DProfile' (modelowanie brył 3D)." } },
                            { "SelectionScopeLock", new ToolParameter { Type = "boolean", Description = "Ustaw true, gdy zadanie ma operowac wylacznie na aktualnie zaznaczonych/wybranych obiektach uzytkownika. Blokuje Workerowi zastapienie selekcji globalnym wyszukiwaniem po modelu." } },
                            { "TaskDescription", new ToolParameter { Type = "string", Description = "SzczegĂłĹ‚owa instrukcja dla eksperta." } }
                        },
                        Required = new List<string> { "TargetProfile", "TaskDescription" }
                    }
                }
            };
        }

        public string Execute(Document doc, JObject args)
        {
            string targetProfile = args["TargetProfile"]?.ToString();
            string taskDescription = args["TaskDescription"]?.ToString();

            if (string.IsNullOrEmpty(targetProfile) || string.IsNullOrEmpty(taskDescription))
            {
                return "BĹÄ„D: Parametry 'TargetProfile' i 'TaskDescription' sÄ… wymagane.";
            }

            var profiles = ToolConfigManager.GetProfiles();
            if (!profiles.TryGetValue(targetProfile, out var profileConfig))
            {
                return $"BĹÄ„D: Nie znaleziono profilu '{targetProfile}'. Zawsze weryfikuj nazwÄ™ profilu.";
            }

            // 1. Ĺadowanie system promptu eksperta
            string systemPrompt = ToolConfigManager.LoadEffectivePromptForProfile(targetProfile);

            bool explicitSelectionScopeLock = args["SelectionScopeLock"]?.Value<bool>() ?? false;
            if (explicitSelectionScopeLock &&
                string.Equals(targetProfile, "CadLayoutProfile", StringComparison.OrdinalIgnoreCase) &&
                IsLayoutMutationTask(taskDescription))
            {
                explicitSelectionScopeLock = false;
                BielikLogger.LogInfo("[DelegateTask] Ignoring SelectionScopeLock=true for CadLayoutProfile layout/page-setup task.");
            }
            bool isSelectionScopedTask = explicitSelectionScopeLock || IsSelectionScopedTask(taskDescription);
            if (isSelectionScopedTask && AgentMemoryState.ActiveSelection.Length == 0)
            {
                return "BĹÄ„D: Zadanie dotyczy aktualnie zaznaczonych obiektĂłw, ale pamiÄ™Ä‡ Agenta nie zawiera ĹĽadnego zaznaczenia. Zaznacz obiekty ponownie albo najpierw zsynchronizuj SelectionSet.";
            }

            bool lockSelectionScope = isSelectionScopedTask;
            if (lockSelectionScope)
            {
                AgentMemoryState.LockSelectionScope();
            }

            // 2. Przygotowanie izolowanej historii
            var localHistory = new List<ChatMessage>
            {
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = taskDescription }
            };

            // 3. WstrzykniÄ™cie zawartoĹ›ci Blackboard jako kontekstu
            var blackboardState = SharedMemoryState.GetAll();
            if (blackboardState.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== DOSTÄPNE ZMIENNE W PAMIÄCI (BLACKBOARD) ===");
                sb.AppendLine("UĹĽyj odpowiedniego narzÄ™dzia (np. ReadFromBlackboardTool) lub wstrzykiwania zmiennych ($KLUCZ), aby odczytaÄ‡ peĹ‚ne wartoĹ›ci.");
                foreach (var kvp in blackboardState)
                {
                    sb.AppendLine($"- {kvp.Key} (DĹ‚ugoĹ›Ä‡: {kvp.Value?.Length ?? 0} znakĂłw)");
                }
                localHistory.Add(new ChatMessage { Role = "system", Content = sb.ToString() });
            }

            // 4. Inicjalizacja klienta i synchroniczne oczekiwanie (dziaĹ‚amy w Task.Run w LLMClient)
            var client = new LLMClient(ToolOrchestrator.Instance);
            
            client.OnStatusUpdate += (msg) => {
                System.Diagnostics.Debug.WriteLine($"[{targetProfile}] {msg}");
                AgentTelemetry.ReportStatus($"[{targetProfile}] {msg}");
            };
            client.OnToolCallLogged += (log) => {
                AgentTelemetry.ReportToolLog($"--- [{targetProfile}] ---\n{log}");
            };
            client.OnLoopLogged += (log) => {
                AgentTelemetry.ReportLoopLog(log);
            };
            client.OnStatsUpdate += (stats) => {
                AgentTelemetry.ReportStats(stats);
            };

            var context = new CadExecutionContext(doc);

            try
            {
                // Filar 6: Master kill switch. Gdy RewidentGloballyDisabled=true,
                // Worker dziala jak w v2.28.x (bez audytu, bez CB, bez Auto-Inject).
                // Uzyteczne do szybkich testow i dla userow ktorzy nie chca audytu.
                // v2.35.0: migracja AgentMemoryState.AuditorGloballyDisabled -> RewidentState.GloballyDisabled
                bool auditorActive = !RewidentState.GloballyDisabled;

                // ============== CIRCUIT BREAKER (Filar 4) ==============
                // Blokada delegacji do profilu, ktory zbyt wiele razy z rzędu
                // zglosil awarie/odrzucona prace. Chroni przed Agent Death Loop.
                // v2.35.0: migracja CircuitBreakerState -> RewidentCircuitBreaker
                if (auditorActive && RewidentCircuitBreaker.IsTripped(targetProfile))
                {
                    var snap = RewidentCircuitBreaker.GetSnapshot(targetProfile);
                    AgentTelemetry.ReportLoopLog(
                        $"[CIRCUIT BREAKER] BLOKADA delegacji do '{targetProfile}'. " +
                        $"Awarie={snap.Failures}/{snap.Threshold}. " +
                        $"Ostatnia awaria: {snap.LastFailureUtc:yyyy-MM-dd HH:mm:ss} UTC. " +
                        $"Precyzuj prompt, zmien model LLM, lub wywolaj CircuitBreakerState.ForceReset('{targetProfile}').");
                    return $"BŁĄD: Circuit Breaker zablokowal profil '{targetProfile}' po {snap.Failures} kolejnych awariach (prog={snap.Threshold}). " +
                           $"Ostatnia awaria: {snap.LastFailureUtc:yyyy-MM-dd HH:mm:ss} UTC. " +
                           $"Aby odblokowac: (a) precyzyj polecenie, (b) zmien model LLM, lub (c) wywolaj reczny reset. " +
                           $"Prawdopodobna przyczyna: Worker wpada w pulapke deterministyczna mimo progresywnego naprowadzania.";
                }
                // ========================================================

                AgentTelemetry.ReportLoopLog($"[Supervisor -> {targetProfile}]\n{taskDescription}");
                AgentExecutionResult result = null;
                WorkValidationReport validation = null;
                RewidentReport auditReport = null;
                const int maxValidationAttempts = 3;

                // ============== AUDITOR PRE-WARM (Filar 3) ==============
                // Rozgrzej KV cache Auditora W TLE jeszcze zanim Worker zacznie prace.
                // max_tokens=1 (WarmupPromptAsync), wiec koszt jest minimalny.
                // Wymaga AuditorEnabled=true. AuditorPrewarmEnabled steruje czy
                // wykonujemy w ogole (domyslnie false - opt-in dla Multi-GPU).
                Task prewarmTask = null;
                // v2.35.0: migracja AgentMemoryState.AuditorEnabled/AuditorPrewarmEnabled -> RewidentState.AuditorEnabled/AuditorPrewarmEnabled
                // v2.35.0: migracja AuditorAuditService.PrewarmAuditorAsync -> RewidentAuditService.PrewarmRewidentAsync
                if (auditorActive && RewidentState.AuditorEnabled && RewidentState.AuditorPrewarmEnabled)
                {
                    prewarmTask = Task.Run(async () =>
                    {
                        await RewidentAuditService.PrewarmRewidentAsync(client, targetProfile, taskDescription);
                    });
                }
                // ========================================================

                for (int attempt = 1; attempt <= maxValidationAttempts; attempt++)
                {
                    // Snapshot licznika mutacji PRZED Workerem (do Wariantu A)
                    int mutationsBeforeWorker = RewidentState.MutationCount;
                    // Fallback: snapshot liczby obiektow w ModelSpace (niezalezny od subskrypcji EngineTracer).
                    // Fix v2.34.13: EngineTracer subskrybuje ObjectAppended/Modified TYLKO gdy jest wlaczony
                    // (checkbox chkEnableTracer w UI). Gdy wylaczony - MutationCount==0 mimo realnej mutacji
                    // i Wariant A falszywie odrzucal prace. CountObjectsInModelSpace dziala zawsze.
                    int modelSpaceCountBefore = EngineTracer.CountObjectsInModelSpace();

                    // Musimy zablokowaÄ‡ wÄ…tek i poczekaÄ‡ na wynik z eksperta, chroniÄ…c gĹ‚Ă³wny wÄ…tek przed Deadlockiem
                    result = Task.Run(async () => {
                        return await client.SendMessageReActAsync(
                            conversationHistory: localHistory,
                            context: context,
                            initialTags: null,
                            earlyExitEnabled: AgentMemoryState.EarlyExitEnabled,
                            maxIterations: 10,
                            profileName: targetProfile);
                    }).GetAwaiter().GetResult();

                    // Jesli pre-warm jeszcze trwa (Worker byl bardzo szybki), poczekaj na niego
                    // przed odpaleniem audytu - inaczej audyt wykonalby sie PRZED rozgrzaniem.
                    if (prewarmTask != null && !prewarmTask.IsCompleted)
                    {
                        try { prewarmTask.Wait(TimeSpan.FromSeconds(2)); }
                        catch { /* Timeout - warmup nie udal sie, jedziemy bez */ }
                    }

                    // --- DATASET STUDIO INTEGRATION FOR WORKER ---
                    try
                    {
                        var historySnapshot = new List<ChatMessage>(localHistory);
                        var toolsSnapshot = ToolOrchestrator.Instance.GetToolsPayloadForProfile(targetProfile);
                        AgentTelemetry.ReportDatasetRecord(
                            $"[{targetProfile}] {taskDescription}",
                            historySnapshot,
                            toolsSnapshot,
                            client.LastStats
                        );
                    }
                    catch { }

                    if (!result.IsSuccess)
                    {
                        // Filar 4: Worker zglosil awarie (np. wyjatek, max iteracji)
                        // Filar 6: pomin CB gdy auditor wylaczony.
                        // v2.35.0: migracja CircuitBreakerState.RecordFailure -> RewidentCircuitBreaker.RecordFailure
                        if (auditorActive)
                        {
                            RewidentCircuitBreaker.RecordFailure(targetProfile,
                                $"Worker nie zwrocil sukcesu: {result.DisplayMessage?.Substring(0, System.Math.Min(120, result.DisplayMessage?.Length ?? 0))}");
                        }
                        break;
                    }

                    // ============== AUDYT (Filar 3 - Wariant A + B) ==============
                    // AuditChain uruchamia sie PO Workervalidator (Accept), ale PRZED
                    // zwroceniem wyniku do Supervisora. Jesli Wariant A odrzuci
                    // (np. Worker klamal o mutacji) - NIE trzeba budzic LLM Rewident.
                    // Filar 6: pomin caly blok gdy GloballyDisabled.
                    // v2.35.0: migracja AuditorAuditService.AuditMutationAsync -> RewidentAuditService.AuditMutationAsync
                    if (auditorActive)
                    {
                        auditReport = Task.Run(async () =>
                        {
                            return await RewidentAuditService.AuditMutationAsync(
                                client, targetProfile, taskDescription, result,
                                mutationsBeforeWorker, modelSpaceCountBefore, attempt, maxValidationAttempts);
                        }).GetAwaiter().GetResult();

                        AgentTelemetry.ReportLoopLog(
                            $"[REWIDENT -> {targetProfile}] Decision={auditReport.Decision} Severity={auditReport.Severity} Heuristic={auditReport.HeuristicOnly} Reason={auditReport.Reason}");

                        if (auditReport.Decision == WorkValidationDecision.Reject)
                        {
                            // Filar 4: Rewident odrzucil kategorycznie
                            // v2.35.0: migracja CircuitBreakerState -> RewidentCircuitBreaker
                            RewidentCircuitBreaker.RecordFailure(targetProfile,
                                $"Rewident REJECT: {auditReport.Reason}");
                            break;
                        }

                        if (auditReport.Decision == WorkValidationDecision.Retry)
                        {
                            if (attempt < maxValidationAttempts)
                            {
                                string repair = auditReport.FeedbackForWorker;
                                AgentTelemetry.ReportLoopLog($"[REWIDENT REPAIR -> {targetProfile}]\n{repair}");
                                localHistory.Add(new ChatMessage { Role = "user", Content = repair });
                                continue;
                            }
                            // Filar 4: Ostatnia proba - Rewident odrzucil, nie ma wiecej szans
                            RewidentCircuitBreaker.RecordFailure(targetProfile,
                                $"Rewident odrzucil po {attempt}/{maxValidationAttempts} probach: {auditReport.Reason}");
                            break;
                        }
                    }
                    // Accept - sukces audytu, przejdz do standardowej walidacji WorkValidator
                    // ===============================================================

                    validation = WorkValidator.Validate(targetProfile, taskDescription, localHistory, result.DisplayMessage);
                    AgentTelemetry.ReportLoopLog(validation.ToLoopLog());

                    if (validation.Decision == WorkValidationDecision.Accept)
                    {
                        // Filar 4: Sukces calkowity (Rewident Accept + Validator Accept) - reset CB
                        // Filar 6: pomin CB gdy auditor wylaczony.
                        // v2.35.0: migracja CircuitBreakerState.Reset -> RewidentCircuitBreaker.Reset
                        if (auditorActive)
                        {
                            RewidentCircuitBreaker.Reset(targetProfile);
                        }
                        break;
                    }

                    if (validation.Decision == WorkValidationDecision.Retry && attempt < maxValidationAttempts)
                    {
                        string repairPrompt = validation.BuildRepairPrompt();
                        AgentTelemetry.ReportLoopLog($"[WORK VALIDATION -> {targetProfile}]\n{repairPrompt}");
                        localHistory.Add(new ChatMessage { Role = "user", Content = repairPrompt });
                        continue;
                    }

                    // Filar 4: Validator Retry na ostatniej probie = awaria
                    // v2.35.0: migracja CircuitBreakerState.RecordFailure -> RewidentCircuitBreaker.RecordFailure
                    if (auditorActive && validation.Decision == WorkValidationDecision.Retry)
                    {
                        RewidentCircuitBreaker.RecordFailure(targetProfile,
                            $"WorkValidator RETRY po {attempt}/{maxValidationAttempts} probach: {validation.Reason}");
                    }

                    break;
                }

                if (result.IsSuccess)
                {
                    if (validation != null && validation.Decision != WorkValidationDecision.Accept)
                    {
                        string reason = validation.Reason ?? "Walidator nie zaakceptowal pracy subagenta.";
                        AgentTelemetry.ReportLoopLog($"[{targetProfile} -> Supervisor]\nBLAD WALIDACJI: {reason}");
                        return $"BŁĄD: '{targetProfile}' nie dostarczyl wystarczajacego dowodu wykonania zadania. {reason}";
                    }

                    if (auditReport != null && auditReport.Decision == WorkValidationDecision.Reject)
                    {
                        return $"BŁĄD: Auditor odrzucil prace '{targetProfile}' po {maxValidationAttempts} probach. {auditReport.Reason}";
                    }

                    if (IsLikelyUnfulfilledLayoutMutation(targetProfile, taskDescription, result.DisplayMessage))
                    {
                        return $"BŁĄD: '{targetProfile}' nie wykonał zleconej modyfikacji. Zwrócił tylko listę layoutów zamiast użyć PageSetupTool/Foreach do zmiany ustawień. Powtórz delegowanie z jawnym nakazem wykonania PageSetupTool dla każdego arkusza.";
                    }
                    AgentTelemetry.ReportLoopLog($"[{targetProfile} -> Supervisor]\n{result.DisplayMessage}");
                    return $"Zadanie zakończone przez '{targetProfile}'. Zwrócony wynik: {result.DisplayMessage}";
                }
                else
                {
                    AgentTelemetry.ReportLoopLog($"[{targetProfile} -> Supervisor]\nBLAD: {result.DisplayMessage}");
                    return $"BŁĄD: '{targetProfile}' zgłosił awarię: {result.DisplayMessage}";
                }
            }
            catch (Exception ex)
            {
                return $"BĹÄ„D KRYTYCZNY podczas delegowania do '{targetProfile}': {ex.Message}";
            }
            finally
            {
                if (lockSelectionScope)
                {
                    AgentMemoryState.UnlockSelectionScope();
                }
            }
        }

        private bool IsLikelyUnfulfilledLayoutMutation(string targetProfile, string taskDescription, string displayMessage)
        {
            if (!string.Equals(targetProfile, "CadLayoutProfile", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!IsLayoutMutationTask(taskDescription))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(displayMessage))
            {
                return true;
            }

            string result = displayMessage.ToLowerInvariant();
            bool isOnlyListResult =
                result.Contains("wynik: znaleziono") &&
                result.Contains("layout(ow)") &&
                !result.Contains("sukces: zastosowano") &&
                !result.Contains("wykonano") &&
                !result.Contains("pagesetup") &&
                !result.Contains("page setup");

            return isOnlyListResult;
        }

        private bool IsLayoutMutationTask(string taskDescription)
        {
            if (string.IsNullOrWhiteSpace(taskDescription)) return false;

            string text = taskDescription.ToLowerInvariant();
            bool hasActionVerb =
                text.Contains("ustaw") ||
                text.Contains("zmien") ||
                text.Contains("zmień") ||
                text.Contains("zastosuj") ||
                text.Contains("skonfiguruj") ||
                text.Contains("przypisz") ||
                text.Contains("ustawić") ||
                text.Contains("zmienić") ||
                text.Contains("ma byc") ||
                text.Contains("ma być");

            bool mentionsLayoutOrPlot =
                text.Contains("arkusz") ||
                text.Contains("layout") ||
                text.Contains("drukark") ||
                text.Contains("format papieru") ||
                text.Contains("styl wydruku") ||
                text.Contains("ctb") ||
                text.Contains("stb") ||
                text.Contains("page setup") ||
                text.Contains("pagesetup");

            return hasActionVerb && mentionsLayoutOrPlot;
        }

        private bool IsSelectionScopedTask(string taskDescription)
        {
            if (string.IsNullOrWhiteSpace(taskDescription)) return false;

            string text = taskDescription.ToLowerInvariant();
            return text.Contains("zaznaczon") ||
                   text.Contains("wybran") ||
                   text.Contains("activeselection") ||
                   text.Contains("selectionset") ||
                   text.Contains("aktualnym wybor") ||
                   text.Contains("obecnym wybor");
        }


        public List<string> Examples => new List<string>
        {
            "{ \"TargetProfile\": \"CadProfile\", \"TaskDescription\": \"Narysuj okrÄ…g o promieniu 50\" }"
        };
    }
}
