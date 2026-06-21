using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Raport z audytu Chain of Evidence wykonanego przez Auditora (lub Wariant A heurystyczny).
    /// Decyzja: Accept (sukces) | Retry (napraw) | Reject (odrzuc - wyczerpane proby).
    /// </summary>
    public class AuditorReport
    {
        public WorkValidationDecision Decision { get; set; } = WorkValidationDecision.Accept;
        public string Reason { get; set; } = "";
        public string FeedbackForWorker { get; set; } = "";
        public List<string> Issues { get; } = new List<string>();
        public string Severity { get; set; } = "info";
        public bool HeuristicOnly { get; set; } = false;
    }

    /// <summary>
    /// Koordynuje audyt Chain of Evidence dla Filaru 3 (Reflexion Loop).
    /// Dwie warstwy:
    /// - Wariant A (heurystyczny, C#): szybki test - czy Worker zglosil sukces
    ///   zadania mutujacego, ale EngineTracer nie odnotowal zadnej mutacji.
    /// - Wariant B (LLM Auditor): budzi AuditorProfile z Chain of Evidence
    ///   w Blackboard i prosi o decyzje {isSuccess, feedback, evidence, severity}.
    /// </summary>
    public static class AuditorAuditService
    {
        // Regex do parsowania JSON {isSuccess, feedback, severity} z DisplayMessage
        // Auditora. Akceptuje rowniez tekst poprzedzajacy lub nastepujacy po JSON.
        private static readonly Regex JsonBlockRegex = new Regex(
            @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}",
            RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Wysyla cichy request z system promptem Auditora + task description
        /// aby rozgrzac KV cache jeszcze zanim Worker skonczy prace.
        /// Wykorzystuje WarmupPromptAsync (max_tokens=1). Uruchamiaj w Task.Run.
        /// </summary>
        public static async Task PrewarmAuditorAsync(
            LLMClient client,
            string targetExpert,
            string taskDescription,
            CancellationToken ct = default)
        {
            try
            {
                var messages = BuildAuditorSeedMessages(targetExpert, taskDescription);
                if (messages == null || messages.Count == 0) return;
                await client.WarmupPromptAsync(messages, "AuditorProfile", ct);
            }
            catch (Exception ex)
            {
                BielikLogger.LogWarn($"[AUDITOR PREWARM] Blad: {ex.Message}");
            }
        }

        /// <summary>
        /// Glowna metoda audytu. Najpierw Wariant A (tani test C#), potem
        /// ewentualnie Wariant B (LLM Auditor). Zwraca raport z decyzja.
        /// </summary>
        public static async Task<AuditorReport> AuditMutationAsync(
            LLMClient client,
            string targetExpert,
            string taskDescription,
            AgentExecutionResult workerResult,
            int mutationsBeforeWorker,
            int modelSpaceCountBefore,
            int attempt,
            int maxAttempts,
            CancellationToken ct = default)
        {
            var report = new AuditorReport();
            int mutationsAfterWorker = AgentMemoryState.MutationCount;
            int newMutations = mutationsAfterWorker - mutationsBeforeWorker;
            // Deklaracja PRZED if-em zeby byla widoczna w Wariancie B (linia 143+).
            // Bez EngineTracer (wylaczony) ModelSpace count jest jedynym zrodlem prawdy.
            int modelSpaceCountAfter = EngineTracer.CountObjectsInModelSpace();
            int modelSpaceDelta = (modelSpaceCountBefore >= 0 && modelSpaceCountAfter >= 0)
                ? modelSpaceCountAfter - modelSpaceCountBefore : 0;

            // === WARIANT A: heurystyczny (C# only, darmowy) ===
            // Fix v2.34.13: dwojaki detektor mutacji -
            //   1. EngineTracer.MutationCount (z subskrypcji ObjectAppended/Modified - dziala TYLKO jesli EngineTracer wlaczony)
            //   2. CountObjectsInModelSpace PRZED i PO (czyste C#, dziala ZAWSZE - nie wymaga subskrypcji)
            // Worker klamie = oba detektory == 0.
            // Fix v2.34.20: dla MODYFIKACJI (Worker zwraca "Zmodyfikowano obiekty: N") nie sprawdzamy
            // BRAK MUTACJI - modyfikacja nie zmienia ModelSpace count.
            bool taskLooksMutating = LooksLikeMutatingTask(taskDescription);
            bool looksLikeModification = workerResult.IsSuccess
                && workerResult.DisplayMessage != null
                && (workerResult.DisplayMessage.Contains("Zmodyfikowano obiekt")
                    || workerResult.DisplayMessage.Contains("Modified")
                    || workerResult.DisplayMessage.Contains("Edycja"));
            if (taskLooksMutating && workerResult.IsSuccess && !looksLikeModification)
            {
                // Heurystyka: brak mutacji gdy oba detektory mowia 0
                bool noMutationDetected = (newMutations == 0 && modelSpaceDelta == 0);

                if (noMutationDetected)
                {
                    report.Decision = WorkValidationDecision.Retry;
                    report.Severity = "error";
                    report.HeuristicOnly = true;
                    report.Reason = $"BRAK MUTACJI W DWG: Worker zglosil sukces zadania mutujacego, ale baza danych nie zawiera nowych obiektow " +
                                     $"(EngineTracer delta={newMutations}, ModelSpace delta={modelSpaceDelta}, " +
                                     $"przed={modelSpaceCountBefore}, po={modelSpaceCountAfter}).";
                    report.FeedbackForWorker =
                        "[AUDIT HEURYSTYCZNY] " + report.Reason + " " +
                        "Sprawdz, czy Twoje wywolanie narzedzia mutujacego (CreateObject, ModifyProperties, ManageLayers, EditBlock, itp.) rzeczywiscie zostalo wykonane. " +
                        "Jesli to modyfikacja (np. zmiana warstwy istniejacego obiektu), Worker powinien zwrocic informacje o zmienionym Handle. " +
                        "Jesli nie mozesz wykonac mutacji w tym zadaniu - zglos Failure zamiast Success.";
                    report.Issues.Add($"MutationCount delta = {newMutations}, ModelSpace delta = {modelSpaceDelta} mimo IsSuccess=true i mutujacego intent zadania.");
                    return report;
                }
            }

            // === WARIANT A2: rollback detected ===
            if (AgentMemoryState.RollbackCount > 0 && workerResult.IsSuccess)
            {
                report.Decision = WorkValidationDecision.Retry;
                report.Severity = "critical";
                report.HeuristicOnly = true;
                report.Reason = "ROLLBACK WYKRYTY: Transakcja CAD zostala przerwana (tr.Abort()), ale Worker zglosil sukces.";
                report.FeedbackForWorker =
                    "[AUDIT HEURYSTYCZNY] " + report.Reason + " " +
                    "Narzedzie wywolalo tr.Abort() - wszystkie mutacje z tej transakcji zostaly wycofane. " +
                    "Sprawdz pierwotna przyczyne bledu (np. bledne argumenty, brak uprawnien, nieistniejacy Handle) i popraw.";
                report.Issues.Add("RollbackCount > 0 mimo IsSuccess=true.");
                return report;
            }

            // === WARIANT B: LLM Auditor (tylko jesli Wariant A przepuscil) ===
            // Auditor przejrzy Chain of Evidence w Blackboard i oceni,
            // czy mutacje odpowiadaja intencji zadania.
            if (!AgentMemoryState.AuditorEnabled)
            {
                report.Reason = "Wariant A (heurystyczny) zaakceptowal - Wariant B (LLM) wylaczony w ustawieniach.";
                return report;
            }
            // Fix v2.34.22: Auditor LLM BEZ Chain of Evidence jest BESENSOWNY.
            // AuditorProfile bedzie pytal o @evidence_before/after ktorych nie ma,
            // odrzuci prace, a Worker w repair prompt zacznie HALUCYNOWAC dodatkowe
            // mutacje (jak w tescie 2 v2.34.20-21 - 3 okregi zamiast 1).
            // Wymuszamy "przepuszczam WorkerResult" zamiast odpalac LLM.
            if (!AgentMemoryState.EvidenceEnabled)
            {
                report.Reason = "Wariant A zaakceptowal - Wariant B (LLM) wymaga Chain of Evidence (wlacz w Ustawienia -> Rewident -> Evidence).";
                return report;
            }
            // Wariant B (LLM) potrzebny tylko jesli sa mutacje do walidacji.
            // Sprawdzamy OBA detektory (EngineTracer subscription + ModelSpace polling) -
            // bo EngineTracer moze nie byc wlaczony i wowczas newMutations==0 mimo realnej mutacji.
            if (newMutations == 0 && modelSpaceDelta == 0)
            {
                report.Reason = "Wariant A potwierdzil brak mutacji - Wariant B (LLM) pominiety.";
                return report;
            }

            try
            {
                var auditorMessages = BuildAuditorAuditMessages(targetExpert, taskDescription, workerResult);
                var auditorResult = await client.SendMessageReActAsync(
                    conversationHistory: auditorMessages,
                    context: null,
                    initialTags: null,
                    earlyExitEnabled: false,
                    maxIterations: 3,
                    profileName: "AuditorProfile");

                if (!auditorResult.IsSuccess)
                {
                    // Auditor nie odpowiedzial - przepuszczamy (nie blokujemy flow)
                    BielikLogger.LogWarn($"[AUDITOR] AuditorProfile nie zwrocil odpowiedzi: {auditorResult.DisplayMessage}");
                    return report;
                }

                var parsed = ParseAuditorJson(auditorResult.DisplayMessage);
                if (parsed == null)
                {
                    // Auditor zwrocil tekst, ale nie JSON - traktujemy jako decyzje pozytywna
                    // (brak danych do odrzucenia). Bedzie zalogowane.
                    BielikLogger.LogInfo("[AUDITOR] Brak JSON w odpowiedzi - przepuszczam WorkerResult.");
                    report.Reason = "Wariant B (LLM) zwrocil tekst bez JSON - przepuszczam WorkerResult.";
                    return report;
                }

                bool auditorApproved = parsed.Value<bool>("isSuccess");
                if (auditorApproved)
                {
                    report.Decision = WorkValidationDecision.Accept;
                    report.Severity = "info";
                    report.Reason = "Auditor zaakceptowal (sukces).";
                    return report;
                }

                // Auditor odrzucil - budujemy raport + progressive hint
                string feedback = parsed.Value<string>("feedback") ?? parsed.Value<string>("reason") ?? "Auditor odrzucil bez uzasadnienia.";
                string severity = parsed.Value<string>("severity") ?? "warn";
                report.Decision = WorkValidationDecision.Retry;
                report.Severity = severity;
                report.Reason = $"Auditor odrzucil: {feedback}";
                report.FeedbackForWorker = BuildRepairPrompt(feedback, attempt, maxAttempts);
                report.Issues.Add(feedback);
                return report;
            }
            catch (Exception ex)
            {
                // Auditor zglosil wyjatek - przepuszczamy, logujemy
                BielikLogger.LogWarn($"[AUDITOR] Wyjatek podczas audytu: {ex.Message}");
                report.Reason = $"Wariant B (LLM) zwrocil wyjatek, przepuszczam WorkerResult: {ex.Message}";
                return report;
            }
        }

        private static string BuildRepairPrompt(string feedback, int attempt, int maxAttempts)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[SYSTEM AUDIT REPAIR - PROBА " + attempt + "/" + maxAttempts + "]");
            sb.AppendLine("Audytor Chain of Evidence odrzucil Twoja prace. Wymienione niezgodnosci:");

            // Progressive Hinting - z kazda proba doklejamy silniejsza reprymende
            if (attempt == 1)
            {
                sb.AppendLine("- Feedback: " + feedback);
                sb.AppendLine("- Popraw ZANIM zglosisz sukces. Uzyj narzedzi mutujacych ktore faktycznie zmieniaja rysunek.");
            }
            else if (attempt == 2)
            {
                sb.AppendLine("- Feedback: " + feedback);
                sb.AppendLine("- UWAGA: To juz " + attempt + " proba. Sprawdz dokladnie argumenty narzedzia - szczegolnie BasePoint, BlockName, Handle, Layer.");
                sb.AppendLine("- Jesli uzywasz Foreach - upewnij sie, ze Items jest lista i kazdy element wywoluje mutujace narzedzie.");
            }
            else
            {
                sb.AppendLine("- Feedback: " + feedback);
                sb.AppendLine("- OSTATNIA PROBA (" + attempt + "/" + maxAttempts + "). Jesli nie mozesz wykonac zadania - zglos Failure zamiast Success.");
                sb.AppendLine("- Nie powtarzaj dokladnie tego samego wywolania narzedzia (pulapka deterministyczna).");
                sb.AppendLine("- Sprobuj innej strategii: inny Layer, inny Foreach pattern, lub WriteToBlackboard ze statusem zadania.");
            }
            return sb.ToString().TrimEnd();
        }

        // === Helpers ===

        private static List<ChatMessage> BuildAuditorSeedMessages(string targetExpert, string taskDescription)
        {
            var messages = new List<ChatMessage>();
            string auditorPrompt = ToolConfigManager.LoadEffectivePromptForProfile("AuditorProfile");
            if (string.IsNullOrWhiteSpace(auditorPrompt)) return messages;
            messages.Add(new ChatMessage { Role = "system", Content = auditorPrompt });
            messages.Add(new ChatMessage
            {
                Role = "user",
                Content = $"[AUDIT SEED] Profil '{targetExpert}' wykona zadanie: \"{taskDescription}\". " +
                          "Przygotuj kontekst. Po wykonaniu zadania otrzymasz Chain of Evidence do walidacji. " +
                          "Odpowiedz jednym tokenem (max_tokens=1 w warmup)."
            });
            return messages;
        }

        private static List<ChatMessage> BuildAuditorAuditMessages(string targetExpert, string taskDescription, AgentExecutionResult workerResult)
        {
            var messages = new List<ChatMessage>();
            string auditorPrompt = ToolConfigManager.LoadEffectivePromptForProfile("AuditorProfile");
            if (!string.IsNullOrWhiteSpace(auditorPrompt))
            {
                messages.Add(new ChatMessage { Role = "system", Content = auditorPrompt });
            }

            // Wstrzyknij liste Handle w Chain of Evidence jako kontekst
            var handles = workerResult.EvidenceHandles ?? new List<string>();
            string handleList = handles.Count > 0
                ? string.Join(", ", handles)
                : "(brak Handle w Evidence - Worker prawdopodobnie nie wykonal zadnych mutacji)";

            var sb = new StringBuilder();
            sb.AppendLine("=== AUDIT REQUEST ===");
            sb.AppendLine($"Profil delegowany: {targetExpert}");
            sb.AppendLine($"Zadanie: {taskDescription}");
            sb.AppendLine();
            sb.AppendLine($"Wynik Workera: {(workerResult.IsSuccess ? "SUCCESS" : "FAILURE")}");
            sb.AppendLine($"DisplayMessage: {workerResult.DisplayMessage}");
            sb.AppendLine($"MutationsDetected: {workerResult.MutationsDetected}");
            sb.AppendLine($"RollbacksDetected: {workerResult.RollbacksDetected}");
            sb.AppendLine();
            sb.AppendLine($"Handle z Chain of Evidence (max 64): {handleList}");
            sb.AppendLine();
            sb.AppendLine("INSTRUKCJA AUDYTU:");
            sb.AppendLine("1. Uzyj ReadFromBlackboard aby odczytac pary @evidence_before_<HandleHex> i @evidence_after_<HandleHex>.");
            sb.AppendLine("2. Porownaj properties (Layer, Color, Linetype, Length, Radius, Area, Center, TextString, itp.).");
            sb.AppendLine("3. Jesli Handle nie ma pary before/after - raportuj BRAK CHAIN OF EVIDENCE jako FAIL.");
            sb.AppendLine("4. Jesli properties after != oczekiwane - raportuj konkretna roznice.");
            sb.AppendLine("5. Zastosuj narzedzia read-only TYLKO: InspectEntity, GetPropertiesTool, AnalyzeSelectionTool, ReadPropertyTool, ReadXData, FindXData, ReadTextSampleTool, ReadFromBlackboard, ListBlocks.");
            sb.AppendLine();
            sb.AppendLine("Odpowiedz WYŁĄCZNIE jako JSON (bez Markdown): { \"isSuccess\": bool, \"feedback\": string, \"evidence\": dict, \"severity\": \"info|warn|error|critical\" }");

            messages.Add(new ChatMessage { Role = "user", Content = sb.ToString() });
            return messages;
        }

        private static JObject ParseAuditorJson(string displayMessage)
        {
            if (string.IsNullOrWhiteSpace(displayMessage)) return null;
            try
            {
                var match = JsonBlockRegex.Match(displayMessage);
                if (match.Success)
                {
                    var token = JToken.Parse(match.Value);
                    if (token is JObject obj && obj.ContainsKey("isSuccess"))
                    {
                        return obj;
                    }
                }
                // Fallback: probuj caly string
                var directToken = JToken.Parse(displayMessage.Trim());
                if (directToken is JObject directObj && directObj.ContainsKey("isSuccess"))
                {
                    return directObj;
                }
            }
            catch
            {
                // Cichy fallback - Auditor mogl odpowiedziec tekstem
            }
            return null;
        }

        private static bool LooksLikeMutatingTask(string taskDescription)
        {
            if (string.IsNullOrWhiteSpace(taskDescription)) return false;
            string lower = taskDescription.ToLowerInvariant();
            // Reuzyj heurystyki z WorkValidator.ClassifyIntent (Mutating verbs)
            string[] mutVerbs = { "ustaw", "zmien", "zmień", "przypisz", "zastosuj", "skonfiguruj",
                "dodaj", "usun", "usuń", "utworz", "utwórz", "narysuj", "wstaw", "edytuj",
                "popraw", "zmodyfikuj", "zapisz", "wykonaj", "stworz", "stwórz" };
            foreach (var v in mutVerbs)
            {
                if (lower.Contains(v)) return true;
            }
            return false;
        }
    }
}
