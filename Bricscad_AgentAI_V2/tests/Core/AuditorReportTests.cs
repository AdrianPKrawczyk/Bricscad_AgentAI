using NUnit.Framework;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    /// <summary>
    /// Testy DTO AuditorReport (Filar 3 Agenta Rewidenta).
    /// Wlasciwe testy AuditMutationAsync wymagaja LLMClient i BricsCAD
    /// (coverage tam bedzie w testach integracyjnych).
    /// </summary>
    [TestFixture]
    public class AuditorReportTests
    {
        [Test]
        public void Default_DecisionIsAccept()
        {
            var report = new AuditorReport();
            Assert.AreEqual(WorkValidationDecision.Accept, report.Decision);
        }

        [Test]
        public void Default_ReasonIsEmpty()
        {
            var report = new AuditorReport();
            Assert.AreEqual(string.Empty, report.Reason);
        }

        [Test]
        public void Default_SeverityIsInfo()
        {
            var report = new AuditorReport();
            Assert.AreEqual("info", report.Severity);
        }

        [Test]
        public void Default_HeuristicOnlyIsFalse()
        {
            var report = new AuditorReport();
            Assert.IsFalse(report.HeuristicOnly);
        }

        [Test]
        public void Default_IssuesIsEmpty()
        {
            var report = new AuditorReport();
            Assert.IsNotNull(report.Issues);
            Assert.AreEqual(0, report.Issues.Count);
        }

        [Test]
        public void Issues_CanBeAdded()
        {
            var report = new AuditorReport();
            report.Issues.Add("Layer mismatch");
            report.Issues.Add("Color mismatch");
            Assert.AreEqual(2, report.Issues.Count);
            Assert.AreEqual("Layer mismatch", report.Issues[0]);
        }

        [Test]
        public void Decision_CanBeSetToAllValues()
        {
            var report = new AuditorReport();
            foreach (WorkValidationDecision d in System.Enum.GetValues(typeof(WorkValidationDecision)))
            {
                report.Decision = d;
                Assert.AreEqual(d, report.Decision);
            }
        }

        [Test]
        public void HeuristicOnly_TrueForWariantAReports()
        {
            var report = new AuditorReport
            {
                HeuristicOnly = true,
                Reason = "BRAK MUTACJI W DWG",
                Decision = WorkValidationDecision.Retry
            };
            Assert.IsTrue(report.HeuristicOnly);
            Assert.AreEqual(WorkValidationDecision.Retry, report.Decision);
        }
    }
}
