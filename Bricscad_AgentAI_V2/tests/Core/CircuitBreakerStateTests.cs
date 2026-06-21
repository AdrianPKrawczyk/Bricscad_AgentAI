using NUnit.Framework;
using Bricscad_AgentAI_V2.Core;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    /// <summary>
    /// Testy jednostkowe CircuitBreakerState (Filar 4 Agenta Rewidenta).
    /// Walidacja: IsTripped, RecordFailure, Reset, ForceReset, prog z AgentMemoryState.
    /// </summary>
    [TestFixture]
    public class CircuitBreakerStateTests
    {
        private const string TestProfile = "TestCadProfile_CB";

        [SetUp]
        public void Setup()
        {
            // Reset stanu CB i flag przed kazdym testem
            CircuitBreakerState.ForceReset(TestProfile);
            AgentMemoryState.CircuitBreakerEnabled = true;
            AgentMemoryState.CircuitBreakerThreshold = 3;
        }

        [TearDown]
        public void Teardown()
        {
            CircuitBreakerState.ForceReset(TestProfile);
        }

        [Test]
        public void IsTripped_NewProfile_ReturnsFalse()
        {
            Assert.IsFalse(CircuitBreakerState.IsTripped(TestProfile));
        }

        [Test]
        public void RecordFailure_OneFailure_NotTripped()
        {
            CircuitBreakerState.RecordFailure(TestProfile, "test 1");
            Assert.IsFalse(CircuitBreakerState.IsTripped(TestProfile));
        }

        [Test]
        public void RecordFailure_ThresholdReached_IsTripped()
        {
            for (int i = 0; i < 3; i++)
            {
                CircuitBreakerState.RecordFailure(TestProfile, $"failure {i}");
            }
            Assert.IsTrue(CircuitBreakerState.IsTripped(TestProfile));
        }

        [Test]
        public void RecordFailure_OverThreshold_StillTripped()
        {
            for (int i = 0; i < 10; i++)
            {
                CircuitBreakerState.RecordFailure(TestProfile);
            }
            Assert.IsTrue(CircuitBreakerState.IsTripped(TestProfile));
        }

        [Test]
        public void Reset_AfterFailures_NotTripped()
        {
            for (int i = 0; i < 3; i++) CircuitBreakerState.RecordFailure(TestProfile);
            Assert.IsTrue(CircuitBreakerState.IsTripped(TestProfile));

            CircuitBreakerState.Reset(TestProfile);
            Assert.IsFalse(CircuitBreakerState.IsTripped(TestProfile));
        }

        [Test]
        public void Reset_NoFailures_NoOp()
        {
            // Reset na profile ktory nie mial awarii - nie powinien crashowac
            Assert.DoesNotThrow(() => CircuitBreakerState.Reset(TestProfile));
            Assert.IsFalse(CircuitBreakerState.IsTripped(TestProfile));
        }

        [Test]
        public void ForceReset_AfterTripped_NotTripped()
        {
            for (int i = 0; i < 3; i++) CircuitBreakerState.RecordFailure(TestProfile);
            Assert.IsTrue(CircuitBreakerState.IsTripped(TestProfile));

            CircuitBreakerState.ForceReset(TestProfile);
            Assert.IsFalse(CircuitBreakerState.IsTripped(TestProfile));
        }

        [Test]
        public void IsTripped_CircuitBreakerDisabled_AlwaysFalse()
        {
            for (int i = 0; i < 10; i++) CircuitBreakerState.RecordFailure(TestProfile);
            AgentMemoryState.CircuitBreakerEnabled = false;
            Assert.IsFalse(CircuitBreakerState.IsTripped(TestProfile));
        }

        [Test]
        public void IsTripped_CustomThreshold_TripsAtCustomValue()
        {
            AgentMemoryState.CircuitBreakerThreshold = 5;
            for (int i = 0; i < 4; i++) CircuitBreakerState.RecordFailure(TestProfile);
            Assert.IsFalse(CircuitBreakerState.IsTripped(TestProfile));

            CircuitBreakerState.RecordFailure(TestProfile); // 5
            Assert.IsTrue(CircuitBreakerState.IsTripped(TestProfile));
        }

        [Test]
        public void IsTripped_NullOrEmptyProfile_ReturnsFalse()
        {
            Assert.IsFalse(CircuitBreakerState.IsTripped(null));
            Assert.IsFalse(CircuitBreakerState.IsTripped(""));
        }

        [Test]
        public void GetSnapshot_ReturnsCorrectValues()
        {
            for (int i = 0; i < 2; i++) CircuitBreakerState.RecordFailure(TestProfile, "snap test");
            var snap = CircuitBreakerState.GetSnapshot(TestProfile);
            Assert.AreEqual(TestProfile, snap.ProfileName);
            Assert.AreEqual(2, snap.Failures);
            Assert.AreEqual(3, snap.Threshold);
            Assert.IsFalse(snap.IsTripped);
            Assert.IsNotNull(snap.LastFailureUtc);
        }

        [Test]
        public void GetSnapshot_NoFailures_ZeroValues()
        {
            var snap = CircuitBreakerState.GetSnapshot(TestProfile);
            Assert.AreEqual(0, snap.Failures);
            Assert.IsFalse(snap.IsTripped);
            Assert.IsNull(snap.LastFailureUtc);
        }

        [Test]
        public void GetAllSnapshots_ContainsAllFailedProfiles()
        {
            const string P1 = "ProfileA_CB";
            const string P2 = "ProfileB_CB";
            CircuitBreakerState.ForceReset(P1);
            CircuitBreakerState.ForceReset(P2);
            try
            {
                CircuitBreakerState.RecordFailure(P1);
                CircuitBreakerState.RecordFailure(P2);
                CircuitBreakerState.RecordFailure(P2);

                var all = CircuitBreakerState.GetAllSnapshots();
                Assert.IsTrue(all.Exists(s => s.ProfileName == P1 && s.Failures == 1));
                Assert.IsTrue(all.Exists(s => s.ProfileName == P2 && s.Failures == 2));
            }
            finally
            {
                CircuitBreakerState.ForceReset(P1);
                CircuitBreakerState.ForceReset(P2);
            }
        }

        [Test]
        public void ResetAll_ClearsEverything()
        {
            CircuitBreakerState.RecordFailure("X1_CB");
            CircuitBreakerState.RecordFailure("X2_CB");
            CircuitBreakerState.RecordFailure("X3_CB");
            CircuitBreakerState.ResetAll();
            Assert.AreEqual(0, CircuitBreakerState.GetAllSnapshots().Count);
        }

        [Test]
        public void PerProfile_Isolation()
        {
            const string PA = "IsoA_CB";
            const string PB = "IsoB_CB";
            CircuitBreakerState.ForceReset(PA);
            CircuitBreakerState.ForceReset(PB);
            try
            {
                for (int i = 0; i < 3; i++) CircuitBreakerState.RecordFailure(PA);
                Assert.IsTrue(CircuitBreakerState.IsTripped(PA));
                Assert.IsFalse(CircuitBreakerState.IsTripped(PB));
            }
            finally
            {
                CircuitBreakerState.ForceReset(PA);
                CircuitBreakerState.ForceReset(PB);
            }
        }
    }
}
