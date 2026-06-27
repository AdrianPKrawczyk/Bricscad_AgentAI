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
                    Description = "Deleguje zadanie do wyspecjalizowanego Agenta Eksperta. UÄąÄ˝yj tego, gdy zadanie wykracza poza twoje kompetencje. Po zakoÄąâ€žczeniu zadania, ekspert zwrÄ‚Ĺ‚ci wynik.",
                    Parameters = new ParametersSchema
                    {
                        Type = "object",
                        Properties = new Dictionary<string, ToolParameter>
                        {
                            { "TargetProfile", new ToolParameter { Type = "string", Description = "Nazwa profilu docelowego eksperta. Dostępne profile: 'CadProfile' (ogólny), 'CadGeometryProfile' (geometria 2D), 'CadBlocksProfile' (bloki), 'CadTextProfile' (edycja tekstów DBText/MText, RTF, pola CAD w tekstach), 'CadMetadataProfile' (właściwości, metadane), 'WentCadProfile' (kondygnacje, pomieszczenia, systemy i bilans WentCad), 'CadMathProfile' (obliczenia), 'CadLayoutProfile' (arkusze wydruku), 'NotesProfile' (notatki), 'AuditorProfile' (audyt), 'Modeler3DProfile' (modelowanie brył 3D)." } },
                            { "SelectionScopeLock", new ToolParameter { Type = "boolean", Description = "Ustaw true, gdy zadanie ma operowac wylacznie na aktualnie zaznaczonych/wybranych obiektach uzytkownika. Blokuje Workerowi zastapienie selekcji globalnym wyszukiwaniem po modelu." } },
                            { "TaskDescription", new ToolParameter { Type = "string", Description = "SzczegÄ‚Ĺ‚Äąâ€šowa instrukcja dla eksperta." } }
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
                return "BÄąÂĂ„â€žD: Parametry 'TargetProfile' i 'TaskDescription' sĂ„â€¦ wymagane.";
            }

            var profiles = ToolConfigManager.GetProfiles();
            if (!profiles.TryGetValue(targetProfile, out var profileConfig))
            {
                return $"BÄąÂĂ„â€žD: Nie znaleziono profilu '{targetProfile}'. Zawsze weryfikuj nazwĂ„â„˘ profilu.";
            }

            // 1. ÄąÂadowanie system promptu eksperta
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

            // Fix v2.35.4 (BUG A): Synchronizacja ActiveSelection z BricsCAD przed delegacja.
            // User moze zaznaczyc obiekty w BricsCAD ale AgentMemoryState.ActiveSelection
            // jest statyczna tablica ktora NIE jest automatycznie aktualizowana.
            // Bez synchronizacji, poprzednia sesja Worker moze zostawic 65 obiektow w pamieci
            // i nowa sesja z Lock=true dostaje bledne informacje o "aktywnej selekcji".
            if (isSelectionScopedTask && AgentMemoryState.ActiveSelection.Length == 0)
            {
                try
                {
                    var activeDoc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
                    if (activeDoc != null)
                    {
                        var ed = activeDoc.Editor;
                        var selRes = ed.SelectImplied();
                        if (selRes != null && selRes.Status == Bricscad.EditorInput.PromptStatus.OK && selRes.Value != null)
                        {
                            var ids = selRes.Value.GetObjectIds();
                            if (ids != null && ids.Length > 0)
                            {
                                AgentMemoryState.Update(ids);
                                BielikLogger.LogInfo("[DelegateTask] zsynchronizowano ActiveSelection z BricsCAD: " + ids.Length + " obiektow.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    BielikLogger.LogWarn("[DelegateTask] Nie udalo sie pobrac selekcji z BricsCAD: " + ex.Message);
                }
            }

            if (isSelectionScopedTask && AgentMemoryState.ActiveSelection.Length == 0)
            {
                return "BLAD: Zadanie dotyczy aktualnie zaznaczonych obiektow, ale pamiec Agenta nie zawiera zadnego zaznaczenia. " +
                       "Sprawdz czy w BricsCAD sa zaznaczone obiekty (Ctrl+A zaznacza wszystko, lub zaznacz myszka). " +
                       "Jesli tak - zaznacz ponownie i ponow komende. " +
                       "Jesli nie - to zadanie wymaga globalnego wyszukiwania w Modelu i powinno byc delegowane BEZ SelectionScopeLock=true.";
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

            // 3. WstrzykniĂ„â„˘cie zawartoÄąâ€şci Blackboard jako kontekstu
            var blackboardState = SharedMemoryState.GetAll();
            if (blackboardState.Count > 0)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("=== DOSTĂ„ÂPNE ZMIENNE W PAMIĂ„ÂCI (BLACKBOARD) ===");
                sb.AppendLine("UÄąÄ˝yj odpowiedniego narzĂ„â„˘dzia (np. ReadFromBlackboardTool) lub wstrzykiwania zmiennych ($KLUCZ), aby odczytaĂ„â€ˇ peÄąâ€šne wartoÄąâ€şci.");
                foreach (var kvp in blackboardState)
                {
                    sb.AppendLine($"- {kvp.Key} (DÄąâ€šugoÄąâ€şĂ„â€ˇ: {kvp.Value?.Length ?? 0} znakÄ‚Ĺ‚w)");
                }
                localHistory.Add(new ChatMessage { Role = "system", Content = sb.ToString() });
            }

            // 4. Inicjalizacja klienta i synchroniczne oczekiwanie (dziaÄąâ€šamy w Task.Run w LLMClient)
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
                // Blokada delegacji do profilu, ktory zbyt wiele razy z rzÄ™du
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
                    return $"BĹÄ„D: Circuit Breaker zablokowal profil '{targetProfile}' po {snap.Failures} kolejnych awariach (prog={snap.Threshold}). " +
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

                    // Musimy zablokowaĂ„â€ˇ wĂ„â€¦tek i poczekaĂ„â€ˇ na wynik z eksperta, chroniĂ„â€¦c gÄąâ€šÄ‚Âłwny wĂ„â€¦tek przed Deadlockiem
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
                        // Fix v2.35.2 (BUG #4): Worker zglosil failure ALE mogl wykonac mutujace
                        // narzedzie (np. TextEditTool zwrocil "Zmodyfikowano 3" ale Worker z jakiegos
                        // powodu zglosil Failure - timeout, halucynacja, blad parsowania).
                        // Nie blokuj profilu - mutacja mogla zajsc.
                        if (auditorActive)
                        {
                            if (result.HasMutatingToolCall)
                            {
                                BielikLogger.LogWarn(
                                    $"[DELEGATE TASK] v2.35.2: Worker zglosil Failure ALE wykonal mutujace " +
                                    $"narzedzie ({result.LastMutatingToolName ?? "?"}). " +
                                    $"Resetuje CB zamiast blokowac profil.");
                                RewidentCircuitBreaker.Reset(targetProfile);
                                AgentTelemetry.ReportLoopLog(
                                    $"[CB OVERRIDE] Profil '{targetProfile}' nie zostal zablokowany - " +
                                    $"Worker wykonal mutujace narzedzie.");
                            }
                            else
                            {
                                RewidentCircuitBreaker.RecordFailure(targetProfile,
                                    $"Worker nie zwrocil sukcesu: {result.DisplayMessage?.Substring(0, System.Math.Min(120, result.DisplayMessage?.Length ?? 0))}");
                            }
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
                            // Fix v2.35.2 (BUG #4): Jesli Worker faktycznie wykonal mutujace
                            // narzedzie (HasMutatingToolCall=true) - mutacja mogla zajsc nawet
                            // jesli Rewident ja odrzucil. NIE blokuj profilu.
                            if (result.HasMutatingToolCall)
                            {
                                BielikLogger.LogWarn(
                                    $"[DELEGATE TASK] v2.35.2: Rewident Retry na ostatniej probie ale Worker " +
                                    $"wykonal mutujace narzedzie ({result.LastMutatingToolName ?? "?"}). " +
                                    $"Resetuje CB zamiast blokowac profil.");
                                RewidentCircuitBreaker.Reset(targetProfile);
                                AgentTelemetry.ReportLoopLog(
                                    $"[CB OVERRIDE] Profil '{targetProfile}' nie zostal zablokowany - " +
                                    $"Worker wykonal mutujace narzedzie, mutacja mogla zajsc.");
                            }
                            else
                            {
                                RewidentCircuitBreaker.RecordFailure(targetProfile,
                                    $"Rewident odrzucil po {attempt}/{maxValidationAttempts} probach: {auditReport.Reason}");
                            }
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
                    // Fix v2.35.2 (BUG #4): NIE blokuj profilu jesli Worker faktycznie wykonal
                    // narzedzie mutujace (HasMutatingToolCall=true). To oznacza ze mutacja mogla
                    // zajsc, nawet jesli Rewident nie byl w stanie tego zwalidowac (np. EngineTracer
                    // wylaczony w UI). Traktujemy to jako "sukces z warn" zamiast blokady profilu.
                    if (auditorActive && validation.Decision == WorkValidationDecision.Retry)
                    {
                        if (result.HasMutatingToolCall)
                        {
                            BielikLogger.LogWarn(
                                $"[DELEGATE TASK] v2.35.2: Walidator odrzucil ale Worker wykonal mutujace narzedzie " +
                                $"({result.LastMutatingToolName ?? "?"}). Mutacja mogla zajsc - " +
                                $"resetuje CB zamiast blokowac profil. Reason: {validation.Reason}");
                            RewidentCircuitBreaker.Reset(targetProfile);
                            AgentTelemetry.ReportLoopLog(
                                $"[CB OVERRIDE] Profil '{targetProfile}' nie zostal zablokowany - " +
                                $"Worker wykonal mutujace narzedzie, mutacja mogla zajsc.");
                        }
                        else
                        {
                            RewidentCircuitBreaker.RecordFailure(targetProfile,
                                $"WorkValidator RETRY po {attempt}/{maxValidationAttempts} probach: {validation.Reason}");
                        }
                    }

                    break;
                }

                if (result.IsSuccess)
                {
                    if (validation != null && validation.Decision != WorkValidationDecision.Accept)
                    {
                        string reason = validation.Reason ?? "Walidator nie zaakceptowal pracy subagenta.";
                        AgentTelemetry.ReportLoopLog($"[{targetProfile} -> Supervisor]\nBLAD WALIDACJI: {reason}");
                        return $"BĹÄ„D: '{targetProfile}' nie dostarczyl wystarczajacego dowodu wykonania zadania. {reason}";
                    }

                    if (auditReport != null && auditReport.Decision == WorkValidationDecision.Reject)
                    {
                        return $"BĹÄ„D: Auditor odrzucil prace '{targetProfile}' po {maxValidationAttempts} probach. {auditReport.Reason}";
                    }

                    if (IsLikelyUnfulfilledLayoutMutation(targetProfile, taskDescription, result.DisplayMessage))
                    {
                        return $"BĹÄ„D: '{targetProfile}' nie wykonaĹ‚ zleconej modyfikacji. ZwrĂłciĹ‚ tylko listÄ™ layoutĂłw zamiast uĹĽyÄ‡ PageSetupTool/Foreach do zmiany ustawieĹ„. PowtĂłrz delegowanie z jawnym nakazem wykonania PageSetupTool dla kaĹĽdego arkusza.";
                    }
                    AgentTelemetry.ReportLoopLog($"[{targetProfile} -> Supervisor]\n{result.DisplayMessage}");
                    return $"Zadanie zakoĹ„czone przez '{targetProfile}'. ZwrĂłcony wynik: {result.DisplayMessage}";
                }
                else
                {
                    AgentTelemetry.ReportLoopLog($"[{targetProfile} -> Supervisor]\nBLAD: {result.DisplayMessage}");
                    return $"BĹÄ„D: '{targetProfile}' zgĹ‚osiĹ‚ awariÄ™: {result.DisplayMessage}";
                }
            }
            catch (Exception ex)
            {
                return $"BÄąÂĂ„â€žD KRYTYCZNY podczas delegowania do '{targetProfile}': {ex.Message}";
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
                text.Contains("zmieĹ„") ||
                text.Contains("zastosuj") ||
                text.Contains("skonfiguruj") ||
                text.Contains("przypisz") ||
                text.Contains("ustawiÄ‡") ||
                text.Contains("zmieniÄ‡") ||
                text.Contains("ma byc") ||
                text.Contains("ma byÄ‡");

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
            "{ \"TargetProfile\": \"CadProfile\", \"TaskDescription\": \"Narysuj okrĂ„â€¦g o promieniu 50\" }"
        };
    }
}

