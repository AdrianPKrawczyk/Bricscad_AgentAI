using System.Diagnostics;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    /// <summary>
    /// Testy DTO AuditorReport (Filar 3 Agenta Rewidenta).
    /// Wzorzec V2: static class + RunTests() + Debug.Assert.
    /// Wlasciwe testy AuditMutationAsync wymagaja LLMClient i BricsCAD
    /// (testy integracyjne do uruchomienia w BricsCAD).
    /// </summary>
    public static class AuditorReportTests
    {
        private static int _passed;
        private static int _failed;

        public static void RunTests()
        {
            _passed = 0;
            _failed = 0;
            Debug.WriteLine("=== AuditorReportTests START ===");

            try
            {
                Default_DecisionIsAccept();
                Default_ReasonIsEmpty();
                Default_SeverityIsInfo();
                Default_HeuristicOnlyIsFalse();
                Default_IssuesIsEmpty();
                Issues_CanBeAdded();
                Decision_CanBeSetToAllValues();
                HeuristicOnly_TrueForWariantAReports();
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[UNHANDLED EXCEPTION] {ex.Message}");
                _failed++;
            }

            Debug.WriteLine($"=== AuditorReportTests END: {_passed} passed, {_failed} failed ===");
        }

        private static void Pass(string name) { _passed++; Debug.WriteLine($"[PASS] {name}"); }
        private static void Fail(string name, string message) { _failed++; Debug.WriteLine($"[FAIL] {name}: {message}"); }
        private static void AssertTrue(bool condition, string name, string message)
        {
            if (condition) Pass(name);
            else Fail(name, message);
        }

        private static void Default_DecisionIsAccept()
        {
            var report = new AuditorReport();
            AssertTrue(report.Decision == WorkValidationDecision.Accept, nameof(Default_DecisionIsAccept), "Default Decision powinno byc Accept");
        }

        private static void Default_ReasonIsEmpty()
        {
            var report = new AuditorReport();
            AssertTrue(report.Reason == string.Empty, nameof(Default_ReasonIsEmpty), "Default Reason powinno byc puste");
        }

        private static void Default_SeverityIsInfo()
        {
            var report = new AuditorReport();
            AssertTrue(report.Severity == "info", nameof(Default_SeverityIsInfo), "Default Severity powinno byc info");
        }

        private static void Default_HeuristicOnlyIsFalse()
        {
            var report = new AuditorReport();
            AssertTrue(!report.HeuristicOnly, nameof(Default_HeuristicOnlyIsFalse), "Default HeuristicOnly=false");
        }

        private static void Default_IssuesIsEmpty()
        {
            var report = new AuditorReport();
            AssertTrue(report.Issues != null, nameof(Default_IssuesIsEmpty) + " (not null)", "Issues nie powinno byc null");
            AssertTrue(report.Issues.Count == 0, nameof(Default_IssuesIsEmpty) + " (empty)", "Issues powinno byc puste");
        }

        private static void Issues_CanBeAdded()
        {
            var report = new AuditorReport();
            report.Issues.Add("Layer mismatch");
            report.Issues.Add("Color mismatch");
            AssertTrue(report.Issues.Count == 2, nameof(Issues_CanBeAdded) + " (count)", "2 issues");
            AssertTrue(report.Issues[0] == "Layer mismatch", nameof(Issues_CanBeAdded) + " (first)", "Pierwszy issue to Layer mismatch");
        }

        private static void Decision_CanBeSetToAllValues()
        {
            var report = new AuditorReport();
            foreach (WorkValidationDecision d in System.Enum.GetValues(typeof(WorkValidationDecision)))
            {
                report.Decision = d;
                AssertTrue(report.Decision == d, nameof(Decision_CanBeSetToAllValues) + " (" + d + ")", "Decision=" + d);
            }
        }

        private static void HeuristicOnly_TrueForWariantAReports()
        {
            var report = new AuditorReport
            {
                HeuristicOnly = true,
                Reason = "BRAK MUTACJI W DWG",
                Decision = WorkValidationDecision.Retry
            };
            AssertTrue(report.HeuristicOnly, nameof(HeuristicOnly_TrueForWariantAReports) + " (heuristic)", "HeuristicOnly=true");
            AssertTrue(report.Decision == WorkValidationDecision.Retry, nameof(HeuristicOnly_TrueForWariantAReports) + " (decision)", "Decision=Retry");
        }
    }
}
