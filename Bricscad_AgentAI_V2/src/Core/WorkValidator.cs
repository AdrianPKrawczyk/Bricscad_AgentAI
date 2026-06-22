using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Bricscad_AgentAI_V2.Models;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Core
{
    public enum WorkIntent
    {
        Informational,
        Mutating,
        Mixed
    }

    public enum ToolEffect
    {
        Unknown,
        ReadOnly,
        Mutating,
        Composite,
        Delegating,
        UserInteraction
    }

    public enum WorkValidationDecision
    {
        Accept,
        Retry,
        Reject
    }

    public class ToolExecutionFact
    {
        public string ToolName { get; set; }
        public string Arguments { get; set; }
        public string Result { get; set; }
        public ToolEffect Effect { get; set; }
        public bool Success { get; set; }
        public int? SuccessCount { get; set; }
        public int? TotalCount { get; set; }
        public string CompositeTargetTool { get; set; }
        public ToolEffect CompositeTargetEffect { get; set; }
    }

    public class WorkValidationReport
    {
        public WorkIntent Intent { get; set; }
        public WorkValidationDecision Decision { get; set; }
        public string Reason { get; set; }
        public List<string> Issues { get; } = new List<string>();
        public List<ToolExecutionFact> Facts { get; } = new List<ToolExecutionFact>();

        public string ToLoopLog()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[WORK VALIDATION]");
            sb.AppendLine($"Intent: {Intent}");
            sb.AppendLine($"Decision: {Decision}");
            if (!string.IsNullOrWhiteSpace(Reason)) sb.AppendLine($"Reason: {Reason}");
            if (Issues.Count > 0)
            {
                sb.AppendLine("Issues:");
                foreach (string issue in Issues) sb.AppendLine($"- {issue}");
            }
            sb.AppendLine("Observed tools:");
            if (Facts.Count == 0)
            {
                sb.AppendLine("- none");
            }
            else
            {
                foreach (var fact in Facts)
                {
                    string count = fact.SuccessCount.HasValue && fact.TotalCount.HasValue
                        ? $" {fact.SuccessCount}/{fact.TotalCount}"
                        : "";
                    string target = string.IsNullOrWhiteSpace(fact.CompositeTargetTool)
                        ? ""
                        : $" -> {fact.CompositeTargetTool}({fact.CompositeTargetEffect})";
                    sb.AppendLine($"- {fact.ToolName}: {fact.Effect}{target}, success={fact.Success}{count}");
                }
            }
            return sb.ToString().TrimEnd();
        }

        public string BuildRepairPrompt()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[SYSTEM WORK VALIDATION]");
            sb.AppendLine("Nie zakonczono jeszcze zadania. Walidator sprawdzil fakty z narzedzi i uznal probe za niepelna.");
            sb.AppendLine($"Powod: {Reason}");
            if (Issues.Count > 0)
            {
                sb.AppendLine("Problemy:");
                foreach (string issue in Issues) sb.AppendLine($"- {issue}");
            }
            sb.AppendLine("Popraw zadanie na podstawie faktow, nie deklaruj sukcesu bez realnego narzedzia wykonawczego.");
            sb.AppendLine("Jesli zadanie wymaga zmiany w rysunku, uzyj narzedzia mutujacego lub Foreach z mutujacym ToolName i zwroc liczbe wykonanych zmian.");
            sb.AppendLine("Jesli poprzednio uzyles tylko narzedzi odczytowych, wykonaj wlasciwa akcje teraz.");
            return sb.ToString().TrimEnd();
        }
    }

    public static class WorkValidator
    {
        public static readonly HashSet<string> MutatingTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CreateObject", "ModifyProperties", "ManageLayers", "InsertBlock", "CreateBlock",
            "EditBlock", "EditAttributes", "TextEditTool", "DimensionEditTool", "ManageAnnoScales",
            "PageSetupTool", "PlotStyleTool", "ManageLayoutTool", "ImportLayoutTemplateTool",
            "ExportLayoutTemplateTool", "PlotLayoutTool", "PublishToPdfTool", "ManageViewportsTool",
            "ManageSheetSetTool", "SheetSetSheetTool",
            "WriteXData", "BatchWriteXData", "WriteProjectFile",
            "WriteQAReport", "ImportCsvDataset", "ImportJsonFile", "SaveMacro", "SavePermanentFormula",
            "ManageDataset", "ManageRecipes", "ManageSkills", "manage_lisps",
            "ExecuteMacro", "ExecuteFormula"
        };

        public static readonly HashSet<string> ReadOnlyTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ListLayoutsTool", "ListPlotDevicesTool", "ListBlocks", "GetPropertiesTool", "AnalyzeSelectionTool",
            "ReadPropertyTool", "ReadTextSampleTool", "ReadXData", "FindXData", "InspectEntity",
            "ReadFromBlackboard", "ReadProjectFile", "ReadKnowledgeTool", "SearchKnowledgeBase",
            "SearchUnitsNetTool", "QueryDataset", "SearchFileContent", "ListSourceFiles", "ReadSourceCode",
            "RunToolTest", "ReadHelp", "DiagnoseMetricVisionGraphicsSystem", "QueryVisionScanIndex",
            // Fix v2.35.1 (BUG #2): WriteToBlackboard to zapis do pamieci wspoldzielonej
            // miedzy agentami (context/notes), NIE jest mutacja rysunku DWG. Wczesniej
            // nieslusznie w MutatingTools powodowalo to, ze Wariant A (Auditor Rewident)
            // liczyl WriteToBlackboard jako "mutacje", a EngineTracer (ktory subskrybuje
            // zdarzenia bazy DWG) jej nie widzial - falszywy alarm "BRAK MUTACJI W DWG".
            "WriteToBlackboard"
        };

        public static WorkValidationReport Validate(string targetProfile, string taskDescription, List<ChatMessage> history, string displayMessage)
        {
            var report = new WorkValidationReport
            {
                Intent = ClassifyIntent(taskDescription),
                Decision = WorkValidationDecision.Accept,
                Reason = "Zadanie ma wystarczajacy dowod wykonania albo jest informacyjne."
            };

            report.Facts.AddRange(ExtractFacts(history));

            if (report.Intent == WorkIntent.Informational)
            {
                return report;
            }

            if (!RequiresToolEvidence(targetProfile, taskDescription))
            {
                report.Reason = "Zadanie wykonawcze nie wyglada na modyfikacje stanu CAD wymagajaca dowodu z narzedzi.";
                return report;
            }

            bool hasMutatingEvidence = report.Facts.Any(HasMutatingEvidence);
            bool hasOnlyReadOnlyEvidence = report.Facts.Count > 0 && report.Facts.All(f => f.Effect == ToolEffect.ReadOnly || f.Effect == ToolEffect.Unknown);
            bool hasHardFailureWithoutLaterMutation = report.Facts.Any(f => !f.Success) && !hasMutatingEvidence;

            if (!hasMutatingEvidence)
            {
                report.Decision = WorkValidationDecision.Retry;
                report.Reason = "Zadanie wykonawcze nie ma dowodu uzycia skutecznego narzedzia mutujacego.";
                if (hasOnlyReadOnlyEvidence)
                {
                    report.Issues.Add("Uzyto tylko narzedzi odczytowych/listujacych.");
                }
                if (report.Facts.Count == 0)
                {
                    report.Issues.Add("Subagent nie uzyl zadnego narzedzia.");
                }
            }

            if (hasHardFailureWithoutLaterMutation)
            {
                report.Decision = WorkValidationDecision.Retry;
                report.Reason = "Ostatnia proba zawiera blad i nie ma pozniejszego dowodu skutecznej zmiany.";
                foreach (var fact in report.Facts.Where(f => !f.Success).Take(3))
                {
                    report.Issues.Add($"{fact.ToolName}: {Trim(fact.Result, 220)}");
                }
            }

            return report;
        }

        private static WorkIntent ClassifyIntent(string taskDescription)
        {
            if (string.IsNullOrWhiteSpace(taskDescription)) return WorkIntent.Informational;
            string text = taskDescription.ToLowerInvariant();

            bool mutating =
                ContainsAny(text, "ustaw", "zmien", "zmień", "przypisz", "zastosuj", "skonfiguruj",
                    "dodaj", "usun", "usuń", "utworz", "utwórz", "narysuj", "wstaw", "edytuj",
                    "popraw", "zmodyfikuj", "zapisz", "wykonaj");

            bool informational =
                ContainsAny(text, "wypisz", "pokaz", "pokaż", "sprawdz", "sprawdź", "odczytaj",
                    "znajdz", "znajdź", "policz", "lista", "listę", "jakie", "jaki");

            if (mutating && informational) return WorkIntent.Mixed;
            return mutating ? WorkIntent.Mutating : WorkIntent.Informational;
        }

        private static bool RequiresToolEvidence(string targetProfile, string taskDescription)
        {
            if (!string.IsNullOrWhiteSpace(targetProfile) &&
                targetProfile.StartsWith("Cad", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(taskDescription)) return false;
            string text = taskDescription.ToLowerInvariant();
            return ContainsAny(text,
                "rysunk", "obiekt", "warstw", "arkusz", "layout", "blok", "atrybut",
                "tekst", "wymiar", "xdata", "drukark", "ctb", "stb", "format papieru",
                "wydruk", "drukuj", "pdf", "publikuj", "pagesetup", "page setup",
                "model space", "paperspace");
        }

        private static IEnumerable<ToolExecutionFact> ExtractFacts(List<ChatMessage> history)
        {
            if (history == null) yield break;

            var callsById = new Dictionary<string, ToolCall>();
            foreach (var message in history)
            {
                if (message?.ToolCalls != null)
                {
                    foreach (var call in message.ToolCalls)
                    {
                        if (!string.IsNullOrWhiteSpace(call?.Id))
                        {
                            callsById[call.Id] = call;
                        }
                    }
                }

                if (message != null &&
                    string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(message.ToolCallId) &&
                    callsById.TryGetValue(message.ToolCallId, out var toolCall))
                {
                    string toolName = toolCall.Function?.Name ?? "(unknown)";
                    string args = toolCall.Function?.Arguments ?? "";
                    string result = message.Content?.ToString() ?? "";
                    var fact = new ToolExecutionFact
                    {
                        ToolName = toolName,
                        Arguments = args,
                        Result = result,
                        Effect = ClassifyToolEffect(toolName),
                        Success = !LooksLikeFailure(result)
                    };
                    ParseCounts(result, fact);
                    if (string.Equals(toolName, "Foreach", StringComparison.OrdinalIgnoreCase))
                    {
                        fact.CompositeTargetTool = ExtractForeachTargetTool(args);
                        fact.CompositeTargetEffect = ClassifyToolEffect(fact.CompositeTargetTool);
                    }
                    yield return fact;
                }
            }
        }

        private static bool HasMutatingEvidence(ToolExecutionFact fact)
        {
            if (fact == null || !fact.Success) return false;

            if (fact.Effect == ToolEffect.Mutating)
            {
                return !fact.SuccessCount.HasValue || fact.SuccessCount.Value > 0;
            }

            if (fact.Effect == ToolEffect.Composite && fact.CompositeTargetEffect == ToolEffect.Mutating)
            {
                return !fact.SuccessCount.HasValue || fact.SuccessCount.Value > 0;
            }

            return false;
        }

        private static ToolEffect ClassifyToolEffect(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName)) return ToolEffect.Unknown;
            if (string.Equals(toolName, "Foreach", StringComparison.OrdinalIgnoreCase)) return ToolEffect.Composite;
            if (string.Equals(toolName, "DelegateTask", StringComparison.OrdinalIgnoreCase)) return ToolEffect.Delegating;
            if (string.Equals(toolName, "UserInput", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(toolName, "UserChoice", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(toolName, "RequestAdditionalTools", StringComparison.OrdinalIgnoreCase))
            {
                return ToolEffect.UserInteraction;
            }
            if (MutatingTools.Contains(toolName)) return ToolEffect.Mutating;
            if (ReadOnlyTools.Contains(toolName)) return ToolEffect.ReadOnly;
            if (toolName.StartsWith("List", StringComparison.OrdinalIgnoreCase) ||
                toolName.StartsWith("Read", StringComparison.OrdinalIgnoreCase) ||
                toolName.StartsWith("Get", StringComparison.OrdinalIgnoreCase) ||
                toolName.StartsWith("Search", StringComparison.OrdinalIgnoreCase) ||
                toolName.StartsWith("Query", StringComparison.OrdinalIgnoreCase) ||
                toolName.StartsWith("Analyze", StringComparison.OrdinalIgnoreCase) ||
                toolName.StartsWith("Inspect", StringComparison.OrdinalIgnoreCase))
            {
                return ToolEffect.ReadOnly;
            }
            return ToolEffect.Unknown;
        }

        private static string ExtractForeachTargetTool(string arguments)
        {
            if (string.IsNullOrWhiteSpace(arguments)) return null;
            try
            {
                var parsed = JObject.Parse(arguments);
                string action = parsed["Action"]?.ToString();
                if (string.IsNullOrWhiteSpace(action)) return null;
                var actionJson = JObject.Parse(action);
                return actionJson["ToolName"]?.ToString() ?? "CreateObject";
            }
            catch
            {
                return null;
            }
        }

        private static void ParseCounts(string result, ToolExecutionFact fact)
        {
            if (string.IsNullOrWhiteSpace(result) || fact == null) return;

            var foreachMatch = Regex.Match(result, @"Wykonano\s+(?<ok>\d+)\s*/\s*(?<all>\d+)", RegexOptions.IgnoreCase);
            if (foreachMatch.Success)
            {
                fact.SuccessCount = int.Parse(foreachMatch.Groups["ok"].Value);
                fact.TotalCount = int.Parse(foreachMatch.Groups["all"].Value);
                return;
            }

            var layoutMatch = Regex.Match(result, @"do\s+(?<ok>\d+)\s+layout", RegexOptions.IgnoreCase);
            if (layoutMatch.Success)
            {
                fact.SuccessCount = int.Parse(layoutMatch.Groups["ok"].Value);
                fact.TotalCount = fact.SuccessCount;
            }
        }

        private static bool LooksLikeFailure(string result)
        {
            if (string.IsNullOrWhiteSpace(result)) return false;
            string lower = result.ToLowerInvariant();
            return lower.StartsWith("blad", StringComparison.Ordinal) ||
                   lower.StartsWith("błąd", StringComparison.Ordinal) ||
                   lower.StartsWith("error", StringComparison.Ordinal) ||
                   lower.Contains("blad ") ||
                   lower.Contains("błąd") ||
                   lower.Contains("bledy:") ||
                   lower.Contains("błędy:") ||
                   lower.Contains("error:") ||
                   lower.Contains("wykonano 0/");
        }

        private static bool ContainsAny(string text, params string[] needles)
        {
            return needles.Any(n => text.Contains(n));
        }

        private static string Trim(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
            return text.Substring(0, max) + "...";
        }
    }
}
