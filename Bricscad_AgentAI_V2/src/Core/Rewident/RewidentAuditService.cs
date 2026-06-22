using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bricscad.ApplicationServices;
using Bricscad_AgentAI_V2.Core; // BielikLogger
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;
using Teigha.DatabaseServices;
using Teigha.Runtime;

namespace Bricscad_AgentAI_V2.Core.Rewident
{
    /// <summary>
    /// Raport z audytu Chain of Evidence wykonanego przez Rewidenta (lub Wariant A heurystyczny).
    /// Decyzja: Accept (sukces) | Retry (napraw) | Reject (odrzuc - wyczerpane proby).
    /// Przeniesiony z AuditorReport w v2.35.0 (refaktor Auditor -> Rewident).
    /// </summary>
    public class RewidentReport
    {
        public WorkValidationDecision Decision { get; set; } = WorkValidationDecision.Accept;
        public string Reason { get; set; } = "";
        public string FeedbackForWorker { get; set; } = "";
        public List<string> Issues { get; } = new List<string>();
        public string Severity { get; set; } = "info";
        public bool HeuristicOnly { get; set; } = false;
    }

    /// <summary>
    /// Koordynuje audyt Chain of Evidence dla Filaru 3 (Reflexion Loop) - profilu Rewident.
    /// Dwie warstwy:
    /// - Wariant A (heurystyczny, C#): szybki test - czy Worker zglosil sukces
    ///   zadania mutujacego, ale EngineTracer nie odnotowal zadnej mutacji.
    /// - Wariant B (LLM Rewident): budzi RewidentProfile z Chain of Evidence
    ///   w Blackboard i prosi o decyzje {isSuccess, feedback, evidence, severity}.
    ///
    /// Wydzielony z AuditorAuditService w v2.35.0 (refaktor Auditor -> Rewident).
    /// Stary AuditorProfile zostaje dla QA/audytu kodu.
    /// </summary>
    public static class RewidentAuditService
    {
        // Regex do parsowania JSON {isSuccess, feedback, severity} z DisplayMessage
        // Rewidenta. Akceptuje rowniez tekst poprzedzajacy lub nastepujacy po JSON.
        private static readonly Regex JsonBlockRegex = new Regex(
            @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}",
            RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Wysyla cichy request z system promptem Rewidenta + task description
        /// aby rozgrzac KV cache jeszcze zanim Worker skonczy prace.
        /// Wykorzystuje WarmupPromptAsync (max_tokens=1). Uruchamiaj w Task.Run.
        /// </summary>
        public static async Task PrewarmRewidentAsync(
            LLMClient client,
            string targetExpert,
            string taskDescription,
            CancellationToken ct = default)
        {
            try
            {
                var messages = BuildRewidentSeedMessages(targetExpert, taskDescription);
                if (messages == null || messages.Count == 0) return;
                await client.WarmupPromptAsync(messages, "RewidentProfile", ct);
            }
            catch (System.Exception ex)
            {
                BielikLogger.LogWarn($"[REWIDENT PREWARM] Blad: {ex.Message}");
            }
        }

        /// <summary>
        /// Glowna metoda audytu. Najpierw Wariant A (tani test C#), potem
        /// ewentualnie Wariant B (LLM Rewident). Zwraca raport z decyzja.
        /// </summary>
        public static async Task<RewidentReport> AuditMutationAsync(
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
            var report = new RewidentReport();
            int mutationsAfterWorker = RewidentState.MutationCount;
            int newMutations = mutationsAfterWorker - mutationsBeforeWorker;
            // Deklaracja PRZED if-em zeby byla widoczna w Wariancie B (linia 143+).
            // Bez EngineTracer (wylaczony) ModelSpace count jest jedynym zrodlem prawdy.
            int modelSpaceCountAfter = EngineTracer.CountObjectsInModelSpace();
            int modelSpaceDelta = (modelSpaceCountBefore >= 0 && modelSpaceCountAfter >= 0)
                ? modelSpaceCountAfter - modelSpaceCountBefore : 0;

            // Fix v2.35.2 (BUG #1): Detektor #3 - niezalezny od EngineTracer subskrypcji.
            // Sprawdza czy Worker faktycznie zmodyfikowal zawartosc MText.Contents / DBText.TextString
            // dla Handle'ow zarejestrowanych przez TextEditTool tuz przed wywolaniem (musi byc
            // wywolany PRZED mutacja przez TextEditTool i po Commit). Dziala nawet gdy EngineTracer
            // jest wylaczony w UI - nie wymaga subskrypcji zdarzen bazy DWG.
            int textMutationDelta = DetectTextMutationsFromToolHistory(workerResult);

            // Fix v2.35.1 (BUG #1 + BUG #3): Automatyczne zabezpieczenie Chain of Evidence.
            // Jesli ToolOrchestrator nie zapisal pary before/after (np. EngineTracer wylaczony
            // w UI - subskrypcja zdarzen DWG nieaktywna - LUB w ogole create-scenario dla
            // CreateObject, gdzie 'before' nie istnieje fizycznie), sami uzupelniamy pary
            // w Blackboard na podstawie DisplayMessage ("Handle: 14A") i EngineTracer.CaptureSnapshot.
            // To jest LINIĄ OBRONY nawet gdy ToolOrchestrator ma blad - sanity check halucynacji
            // (BUG #3) zalezy od poprawnej pary w Blackboard.
            if (RewidentState.EvidenceEnabled && !RewidentState.GloballyDisabled && workerResult.IsSuccess)
            {
                EnsureChainOfEvidenceInBlackboard(workerResult, newMutations, modelSpaceDelta);
            }

            // === WARIANT A: heurystyczny (C# only, darmowy) ===
            // Fix v2.34.13: dwojaki detektor mutacji -
            //   1. EngineTracer.MutationCount (z subskrypcji ObjectAppended/Modified - dziala TYLKO jesli EngineTracer wlaczony)
            //   2. CountObjectsInModelSpace PRZED i PO (czyste C#, dziala ZAWSZE - nie wymaga subskrypcji)
            // Fix v2.35.2 (BUG #1): detektor #3 - textMutationDelta (niezależny od subskrypcji,
            //   bada zawartosc MText.Contents / DBText.TextString bezposrednio z DWG)
            // Worker klamie = wszystkie 3 detektory == 0.
            // Fix v2.34.20: dla MODYFIKACJI (Worker zwraca "Zmodyfikowano obiekty: N") nie sprawdzamy
            // BRAK MUTACJI - modyfikacja nie zmienia ModelSpace count.
            bool taskLooksMutating = LooksLikeMutatingTask(taskDescription);
            bool looksLikeModification = workerResult.IsSuccess
                && (workerResult.DisplayMessage != null
                    && (workerResult.DisplayMessage.Contains("Zmodyfikowano obiekt")
                        || workerResult.DisplayMessage.Contains("Modified")
                        || workerResult.DisplayMessage.Contains("Edycja")))
                || (workerResult.HasMutatingToolCall && textMutationDelta > 0);
            if (taskLooksMutating && workerResult.IsSuccess && !looksLikeModification)
            {
                // Heurystyka: brak mutacji gdy WSZYSTKIE 3 detektory mowia 0
                bool noMutationDetected = (newMutations == 0 && modelSpaceDelta == 0 && textMutationDelta == 0);

                if (noMutationDetected)
                {
                    report.Decision = WorkValidationDecision.Retry;
                    report.Severity = "error";
                    report.HeuristicOnly = true;
                    report.Reason = $"BRAK MUTACJI W DWG: Worker zglosil sukces zadania mutujacego, ale baza danych nie zawiera nowych obiektow " +
                                     $"(EngineTracer delta={newMutations}, ModelSpace delta={modelSpaceDelta}, " +
                                     $"TextMutation delta={textMutationDelta}, " +
                                     $"przed={modelSpaceCountBefore}, po={modelSpaceCountAfter}).";
                    report.FeedbackForWorker =
                        "[AUDIT HEURYSTYCZNY] " + report.Reason + " " +
                        "Sprawdz, czy Twoje wywolanie narzedzia mutujacego (CreateObject, ModifyProperties, ManageLayers, EditBlock, itp.) rzeczywiscie zostalo wykonane. " +
                        "Jesli to modyfikacja (np. zmiana warstwy istniejacego obiektu), Worker powinien zwrocic informacje o zmienionym Handle. " +
                        "Jesli nie mozesz wykonac mutacji w tym zadaniu - zglos Failure zamiast Success.";
                    report.Issues.Add($"MutationCount delta = {newMutations}, ModelSpace delta = {modelSpaceDelta}, TextMutation delta = {textMutationDelta} mimo IsSuccess=true i mutujacego intent zadania.");
                    return report;
                }
            }

            // === WARIANT A2: rollback detected ===
            if (RewidentState.RollbackCount > 0 && workerResult.IsSuccess)
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

            // === WARIANT B: LLM Rewident (tylko jesli Wariant A przepuscil) ===
            // Rewident przejrzy Chain of Evidence w Blackboard i oceni,
            // czy mutacje odpowiadaja intencji zadania.
            if (!RewidentState.AuditorEnabled)
            {
                report.Reason = "Wariant A (heurystyczny) zaakceptowal - Wariant B (LLM) wylaczony w ustawieniach.";
                return report;
            }
            // Fix v2.34.22: Rewident LLM BEZ Chain of Evidence jest BESENSOWNY.
            // RewidentProfile bedzie pytal o @evidence_before/after ktorych nie ma,
            // odrzuci prace, a Worker w repair prompt zacznie HALUCYNOWAC dodatkowe
            // mutacje (jak w tescie 2 v2.34.20-21 - 3 okregi zamiast 1).
            // Wymuszamy "przepuszczam WorkerResult" zamiast odpalac LLM.
            if (!RewidentState.EvidenceEnabled)
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
                var auditorMessages = BuildRewidentAuditMessages(targetExpert, taskDescription, workerResult);
                var auditorResult = await client.SendMessageReActAsync(
                    conversationHistory: auditorMessages,
                    context: null,
                    initialTags: null,
                    earlyExitEnabled: false,
                    maxIterations: 3,
                    profileName: "RewidentProfile");

                if (!auditorResult.IsSuccess)
                {
                    // Rewident nie odpowiedzial - przepuszczamy (nie blokujemy flow)
                    BielikLogger.LogWarn($"[REWIDENT] RewidentProfile nie zwrocil odpowiedzi: {auditorResult.DisplayMessage}");
                    return report;
                }

                var parsed = ParseRewidentJson(auditorResult.DisplayMessage);
                if (parsed == null)
                {
                    // Rewident zwrocil tekst, ale nie JSON - traktujemy jako decyzje pozytywna
                    // (brak danych do odrzucenia). Bedzie zalogowane.
                    BielikLogger.LogInfo("[REWIDENT] Brak JSON w odpowiedzi - przepuszczam WorkerResult.");
                    report.Reason = "Wariant B (LLM) zwrocil tekst bez JSON - przepuszczam WorkerResult.";
                    return report;
                }

                bool auditorApproved = parsed.Value<bool>("isSuccess");
                if (auditorApproved)
                {
                    report.Decision = WorkValidationDecision.Accept;
                    report.Severity = "info";
                    report.Reason = "Rewident zaakceptowal (sukces).";
                    return report;
                }

                // Rewident odrzucil - budujemy raport + progressive hint
                string feedback = parsed.Value<string>("feedback") ?? parsed.Value<string>("reason") ?? "Rewident odrzucil bez uzasadnienia.";
                string severity = parsed.Value<string>("severity") ?? "warn";

                // Fix v2.35.1 (BUG #3): SANITY CHECK - jesli Rewident mowi "BRAK CHAIN OF
                // EVIDENCE" ale handle w rzeczywistosci MA pare before/after w Blackboard
                // (zapisana przez EngineTracer lub syntetycznie dla CreateObject), to jest
                // halucynacja LLM. Wczesniej prowadzilo to do wymuszenia dodatkowej mutacji
                // (Worker "poprawial" cos, co nie bylo zepsute) - patrz v2.35.0 log: okrag
                // z kolorem 2 (zolty) zamiast domyslnego, bo Worker dorzucil ModifyProperties.
                if (IsChainOfEvidenceHallucination(feedback, workerResult))
                {
                    BielikLogger.LogWarn($"[REWIDENT] Sanity-check: halucynacja 'BRAK CHAIN OF EVIDENCE' odrzucona. Handle ma poprawna pare. Feedback: {feedback}");
                    report.Decision = WorkValidationDecision.Accept;
                    report.Severity = "warn";
                    report.Reason = $"Rewident halucynowal BRAK CHAIN OF EVIDENCE (zignorowano - handle ma spojna pare before/after w Blackboard).";
                    return report;
                }

                report.Decision = WorkValidationDecision.Retry;
                report.Severity = severity;
                report.Reason = $"Rewident odrzucil: {feedback}";
                report.FeedbackForWorker = BuildRepairPrompt(feedback, attempt, maxAttempts);
                report.Issues.Add(feedback);
                return report;
            }
            catch (System.Exception ex)
            {
                // Rewident zglosil wyjatek - przepuszczamy, logujemy
                BielikLogger.LogWarn($"[REWIDENT] Wyjatek podczas audytu: {ex.Message}");
                report.Reason = $"Wariant B (LLM) zwrocil wyjatek, przepuszczam WorkerResult: {ex.Message}";
                return report;
            }
        }

        private static string BuildRepairPrompt(string feedback, int attempt, int maxAttempts)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[SYSTEM AUDIT REPAIR - PROBА " + attempt + "/" + maxAttempts + "]");
            sb.AppendLine("Rewident Chain of Evidence odrzucil Twoja prace. Wymienione niezgodnosci:");

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

        private static List<ChatMessage> BuildRewidentSeedMessages(string targetExpert, string taskDescription)
        {
            var messages = new List<ChatMessage>();
            string rewidentPrompt = ToolConfigManager.LoadEffectivePromptForProfile("RewidentProfile");
            if (string.IsNullOrWhiteSpace(rewidentPrompt)) return messages;
            messages.Add(new ChatMessage { Role = "system", Content = rewidentPrompt });
            messages.Add(new ChatMessage
            {
                Role = "user",
                Content = $"[AUDIT SEED] Profil '{targetExpert}' wykona zadanie: \"{taskDescription}\". " +
                          "Przygotuj kontekst. Po wykonaniu zadania otrzymasz Chain of Evidence do walidacji. " +
                          "Odpowiedz jednym tokenem (max_tokens=1 w warmup)."
            });
            return messages;
        }

        private static List<ChatMessage> BuildRewidentAuditMessages(string targetExpert, string taskDescription, AgentExecutionResult workerResult)
        {
            var messages = new List<ChatMessage>();
            string rewidentPrompt = ToolConfigManager.LoadEffectivePromptForProfile("RewidentProfile");
            if (!string.IsNullOrWhiteSpace(rewidentPrompt))
            {
                messages.Add(new ChatMessage { Role = "system", Content = rewidentPrompt });
            }

            // Wstrzyknij liste Handle w Chain of Evidence jako kontekst
            var handles = workerResult.EvidenceHandles ?? new List<string>();
            string handleList = handles.Count > 0
                ? string.Join(", ", handles)
                : "(brak Handle w Evidence - Worker prawdopodobnie nie wykonal zadnych mutacji)";

            var sb = new StringBuilder();
            sb.AppendLine("=== AUDIT REQUEST (REVIDENT) ===");
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
            sb.AppendLine("INSTRUKCJA AUDYTU (REVIDENT - profil RewidentProfile):");
            sb.AppendLine("1. Uzyj ReadFromBlackboard aby odczytac pary @evidence_before_<HandleHex> i @evidence_after_<HandleHex>.");
            sb.AppendLine("2. Porownaj properties (Layer, Color, Linetype, Length, Radius, Area, Center, TextString, itp.).");
            sb.AppendLine("3. SPECIAL CASE - CreateObject: jesli @evidence_before ma ObjectExistedBefore=false i puste properties - to jest OCZEKIWANY stan 'before' dla nowo utworzonego obiektu. Para jest SPOJNA, to NIE jest blad.");
            sb.AppendLine("4. Jesli Handle nie ma pary before/after (oba klucze null) - raportuj BRAK CHAIN OF EVIDENCE jako FAIL.");
            sb.AppendLine("5. Jesli properties after != oczekiwane - raportuj konkretna roznice.");
            sb.AppendLine("6. Zastosuj narzedzia read-only TYLKO: InspectEntity, GetPropertiesTool, AnalyzeSelectionTool, ReadPropertyTool, ReadXData, FindXData, ReadTextSampleTool, ReadFromBlackboard, ListBlocks.");
            sb.AppendLine();
            sb.AppendLine("Odpowiedz WYŁĄCZNIE jako JSON (bez Markdown): { \"isSuccess\": bool, \"feedback\": string, \"evidence\": dict, \"severity\": \"info|warn|error|critical\" }");

            messages.Add(new ChatMessage { Role = "user", Content = sb.ToString() });
            return messages;
        }

        private static JObject ParseRewidentJson(string displayMessage)
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
                // Cichy fallback - Rewident mogl odpowiedziec tekstem
            }
            return null;
        }

        /// <summary>
        /// Fix v2.35.1 (BUG #3): Sanity-check dla halucynacji Rewidenta.
        /// Jesli LLM odrzuca prace powolajac sie na "BRAK CHAIN OF EVIDENCE" (lub podobne
        /// sformulowania), ale w Blackboard faktycznie istnieja OBIE wartosci
        /// (@evidence_before_ i @evidence_after_) dla kazdego Handle z EvidenceHandles,
        /// to halucynacja - ignorujemy odrzucenie i przepuszczamy prace Workera.
        ///
        /// Powod: w v2.35.0 obserwowano przypadek gdzie Worker poprawnie wykonal
        /// CreateObject (zapisal pary w CoE), ale Rewident twierdzil "BRAK CHAIN OF
        /// EVIDENCE" mimo ze Handle 0x14A mial @evidence_after_14A. W odpowiedzi na
        /// repair prompt Worker wykonywal dodatkowe modyfikacje (zmiana koloru) aby
        /// "spelnic wymogi" - patrz log: okrag z kolorem zoltym zamiast domyslnego.
        ///
        /// v2.35.1 FIX: Jesli workerResult.EvidenceHandles jest puste (np. ToolOrchestrator
        /// nie ustawil go z powodu wylaczonego EngineTracer), skanujemy Blackboard w
        /// poszukiwaniu kluczy @evidence_after_ zeby odtworzyc liste Handle do walidacji.
        /// </summary>
        private static bool IsChainOfEvidenceHallucination(string feedback, AgentExecutionResult workerResult)
        {
            if (string.IsNullOrWhiteSpace(feedback)) return false;
            if (workerResult == null) return false;

            // Tylko reaguj na slowa kluczowe zwiazane z Chain of Evidence
            // (zeby nie tlumic innych waznych odrzucen, np. "Handle 0x14A.Layer = 1, oczekiwano 0").
            string lower = feedback.ToLowerInvariant();
            bool mentionsCoEIssue = lower.Contains("chain of evidence")
                || lower.Contains("evidence_before")
                || lower.Contains("evidence_after")
                || (lower.Contains("brak") && (lower.Contains("before") || lower.Contains("after") || lower.Contains("pary")));
            if (!mentionsCoEIssue) return false;

            // Fix v2.35.1: jesli EvidenceHandles jest puste, odczytaj Handle z Blackboard.
            // Format kluczy: @evidence_after_<HandleHex>. Filtrujemy klucze nalezace do biezacej
            // sesji (ignorujemy @evidence_before_* - moga byc z poprzednich wywolan).
            List<string> handles = workerResult.EvidenceHandles;
            if (handles == null || handles.Count == 0)
            {
                handles = new List<string>();
                var allKeys = SharedMemoryState.GetAll().Keys;
                foreach (var key in allKeys)
                {
                    const string AfterPrefix = "@evidence_after_";
                    if (key.StartsWith(AfterPrefix, System.StringComparison.OrdinalIgnoreCase))
                    {
                        string handleHex = key.Substring(AfterPrefix.Length);
                        if (!string.IsNullOrEmpty(handleHex) && !handles.Contains(handleHex))
                        {
                            handles.Add(handleHex);
                        }
                    }
                }
            }

            if (handles.Count == 0) return false;

            // Sprawdz kazdy Handle - czy MA pare before/after w Blackboard.
            int totalHandles = 0;
            int handlesWithFullPair = 0;
            foreach (var handleHex in handles)
            {
                if (string.IsNullOrWhiteSpace(handleHex)) continue;
                totalHandles++;
                var (beforeKey, afterKey) = EvidenceSnapshot.BlackboardPair(handleHex);
                string beforeVal = SharedMemoryState.Read(beforeKey);
                string afterVal = SharedMemoryState.Read(afterKey);
                if (!string.IsNullOrEmpty(beforeVal) && !string.IsNullOrEmpty(afterVal))
                {
                    handlesWithFullPair++;
                }
            }

            // Halucynacja: feedback mowi "brak pary" ale wszystkie Handle maja pelna pare.
            return totalHandles > 0 && handlesWithFullPair == totalHandles;
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

        /// <summary>
        /// Fix v2.35.2 (BUG #1): Detektor mutacji tekstu NIEZALEZNY od EngineTracer.
        ///
        /// Problem: TextEditTool zmienia MText.Contents / DBText.TextString wewnatrz
        /// transakcji Write. EngineTracer subskrybuje ObjectModified, ALE:
        /// - Domyslnie EngineTracer._isEnabled = false (wylaczony checkboxem w UI),
        /// - subskrypcja ObjectModified nie zawsze jest odpalana dla zmian wewnetrznych
        ///   Properties obiektu (zalezy od wersji Teigha),
        /// - ModelSpace count nie zmienia sie (tekst zostal zmieniony, ale to nadal
        ///   ten sam obiekt).
        ///
        /// Rozwiazanie: Jesli Worker wywolal TextEditTool (sprawdzamy po nazwie
        /// LastMutatingToolName ustawianej przez AgentExecutionResult.HasMutatingToolCall),
        /// traktujemy to jako SIGNAL ze mutacja mogla zajsc. Dodatkowo sprawdzamy
        /// zawartosc AgentMemoryState.ActiveSelection PRZED (Blackboard @text_snapshot_before)
        /// i PO (Blackboard @text_snapshot_after) jesli TextEditTool zapisal stan PRZED.
        ///
        /// Zwracamy: >= 1 gdy sygnal mutacji tekstu (TextEditTool wywolany), 0 gdy nie.
        /// </summary>
        private static int DetectTextMutationsFromToolHistory(AgentExecutionResult workerResult)
        {
            if (workerResult == null) return 0;

            // Jesli Worker wywolal TextEditTool, DimensionEditTool lub inny edytor tresci -
            // to jest pozytywny sygnal mutacji (niezaleznie od EngineTracer).
            var contentMutators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "TextEditTool", "DimensionEditTool", "EditAttributes", "ManageFields"
            };

            if (workerResult.MutatingToolNames != null)
            {
                foreach (var name in workerResult.MutatingToolNames)
                {
                    if (contentMutators.Contains(name)) return 1;
                }
            }

            if (contentMutators.Contains(workerResult.LastMutatingToolName ?? "")) return 1;

            return 0;
        }

        // Fix v2.35.1 (BUG #1 + BUG #3): Automatyczne zabezpieczenie Chain of Evidence.
        //
        // Kontekst: ToolOrchestrator powinien zapisac pary @evidence_before_/<HandleHex> i
        // @evidence_after_/<HandleHex> do Blackboard dla kazdej mutacji. W praktyce:
        //   - EngineTracer moze byc wylaczony w UI (subskrypcja zdarzen DWG nieaktywna)
        //     wtedy mutationsAfter == 0 i ToolOrchestrator pomija zapis.
        //   - W create-scenario (CreateObject) Handle nie istnial PRZED mutacja - w
        //     v2.35.0+ ToolOrchestrator zapisuje syntetyczny 'before' z ObjectExistedBefore=false,
        //     ale to wymaga poprawnego przebudowania ToolOrchestrator.
        //
        // Ta metoda jest DRUGĄ LINIĄ OBRONY (defense-in-depth): parsuje Handle z
        // DisplayMessage ("Handle: 14A" z CreateObject/ModifyProperties) i zapisuje
        // pary before/after w Blackboard samodzielnie - nawet jesli ToolOrchestrator tego nie zrobil.
        //
        // Dzieki temu:
        //   1. IsChainOfEvidenceHallucination (sanity check BUG #3) ma co czytac
        //   2. LLM Rewident widzi spojna pare (nie halucynuje "BRAK CHAIN OF EVIDENCE")
        //   3. Nawet jesli binarka ma stary ToolOrchestrator (build problem), audyt dziala.
        //
        // Format DisplayMessage parsowany:
        //   - "SUKCES: Utworzono Circle (Handle: 14A). Center=..."
        //   - "SUKCES: Zmodyfikowano obiektów: 1. Odrzucono atrybutów: 0."  (Handle brak w modyfikacji - pomijamy)
        //   - inne - Handle nieobecny, pomijamy.
        //
        // BEZPIECZNE: nie wymaga subskrypcji EngineTracer, dziala na podstawie
        // EngineTracer.CaptureSnapshot (czyste C# + transakcja).
        private static readonly Regex HandleRegex = new Regex(
            @"\(Handle:\s*([0-9A-Fa-f]+)\)",
            RegexOptions.Compiled);

        private static void EnsureChainOfEvidenceInBlackboard(
            AgentExecutionResult workerResult, int engineTracerDelta, int modelSpaceDelta)
        {
            if (workerResult == null || string.IsNullOrEmpty(workerResult.DisplayMessage)) return;

            // Nie zapisuj jesli Worker zaraportowal awarie - to nie jest mutacja.
            if (!workerResult.IsSuccess) return;

            // Wymagaj przynajmniej jednego detektora mutacji (EngineTracer LUB ModelSpace count).
            // Bez tego bylby to falszywy "audit trail" dla nieistniejacych obiektow.
            if (engineTracerDelta <= 0 && modelSpaceDelta <= 0) return;

            // Parsuj Handle z DisplayMessage - moze byc wiele (np. multi-create).
            var matches = HandleRegex.Matches(workerResult.DisplayMessage);
            if (matches.Count == 0) return;

            foreach (Match m in matches)
            {
                string handleHex = m.Groups[1].Value;
                if (string.IsNullOrEmpty(handleHex)) continue;

                string beforeKey = EvidenceSnapshot.BlackboardKey("before", handleHex);
                string afterKey = EvidenceSnapshot.BlackboardKey("after", handleHex);

                // Sprawdz czy para juz istnieje - nie nadpisuj (ToolOrchestrator mial pierwszenstwo).
                string existingBefore = SharedMemoryState.Read(beforeKey);
                string existingAfter = SharedMemoryState.Read(afterKey);
                if (!string.IsNullOrEmpty(existingBefore) && !string.IsNullOrEmpty(existingAfter))
                {
                    continue;
                }

                // Sprobuj pobrac ObjectId z biezacego dokumentu.
                ObjectId id = TryFindObjectIdByHandleHex(handleHex);
                if (id.IsNull)
                {
                    // Handle nie istnieje w DWG - moze zostal usuniety. Pomijamy.
                    BielikLogger.LogWarn($"[CHAIN OF EVIDENCE FALLBACK] Handle 0x{handleHex} nie znaleziony w DWG, pomijam.");
                    continue;
                }

                // Zapisz 'after' z biezacego stanu DWG (czyste C#, dziala bez EngineTracer).
                if (string.IsNullOrEmpty(existingAfter))
                {
                    var afterSnap = EngineTracer.CaptureSnapshot(id, "after");
                    if (afterSnap != null)
                    {
                        EngineTracer.WriteSnapshotToBlackboard(afterSnap, "after");
                        BielikLogger.LogInfo($"[CHAIN OF EVIDENCE FALLBACK] Zapisano 'after' dla Handle=0x{handleHex}");
                    }
                }

                // Zapisz syntetyczny 'before' jesli brak.
                // Nie mozemy rozroznic create vs modify bez dodatkowej wiedzy, ale
                // ObjectExistedBefore=false jest bezpieczne dla obu - LLM zobaczy
                // pusty properties (cos nie istnialo) i zaakceptuje to jako create-scenario.
                // Dla modify, LLM powinien porownac properties before(=/null) vs after(real)
                // i wykryc zmiane, ale w 99% przypadkow Handle w DisplayMessage
                // pochodzi z CreateObject (bo tylko CreateObject zwraca "Handle: 14A"
                // w swoim formacie - ModifyProperties zwraca "Zmodyfikowano obiektów: 1").
                if (string.IsNullOrEmpty(existingBefore))
                {
                    string objectType = TryDetectObjectType(id);
                    var beforeSnap = EvidenceSnapshot.CreateNotExistedBefore(id, objectType);
                    EngineTracer.WriteSnapshotToBlackboard(beforeSnap, "before");
                    BielikLogger.LogInfo($"[CHAIN OF EVIDENCE FALLBACK] Syntetyczny 'before' (ObjectExistedBefore=false) dla Handle=0x{handleHex}");
                }
            }
        }

        /// <summary>
        /// Szuka ObjectId w biezacym dokumencie po Handle (hex string).
        /// Dziala bez subskrypcji EngineTracer (czyste C# + transakcja).
        /// Zwraca ObjectId.Null jesli nie znaleziono.
        /// </summary>
        private static ObjectId TryFindObjectIdByHandleHex(string handleHex)
        {
            if (string.IsNullOrEmpty(handleHex)) return ObjectId.Null;
            Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return ObjectId.Null;
            try
            {
                // Teigha.Handle nie ma statycznej metody TryParse (w odróżnieniu od .NET Core).
                // Parsujemy recznie z hex stringa (HandleHex to liczba szesnastkowa bez "0x" prefix).
                // Handle.Value to System.Int64, wiec max 8 bajtow = 16 znakow hex.
                long handleValue;
                if (!long.TryParse(handleHex, System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out handleValue))
                {
                    return ObjectId.Null;
                }
                Handle h = new Handle(handleValue);
                return doc.Database.GetObjectId(false, h, 0);
            }
            catch
            {
                return ObjectId.Null;
            }
        }

        /// <summary>
        /// Zwraca typ obiektu CAD (np. "Circle", "Line") dla ObjectId, lub "(unknown)".
        /// </summary>
        private static string TryDetectObjectType(ObjectId id)
        {
            if (id.IsNull) return "(unknown)";
            Document doc = Bricscad.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return "(unknown)";
            try
            {
                using (var tr = doc.Database.TransactionManager.StartOpenCloseTransaction())
                {
                    DBObject obj = tr.GetObject(id, OpenMode.ForRead, false);
                    if (obj == null) return "(unknown)";
                    return obj.GetType().Name;
                }
            }
            catch
            {
                return "(unknown)";
            }
        }
    }
}
