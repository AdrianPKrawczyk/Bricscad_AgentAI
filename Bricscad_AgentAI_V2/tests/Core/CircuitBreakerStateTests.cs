using System;
using System.Diagnostics;
using Bricscad_AgentAI_V2.Core;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    /// <summary>
    /// Testy jednostkowe CircuitBreakerState (Filar 4 Agenta Rewidenta).
    /// Wzorzec V2: static class + RunTests() + Debug.Assert (jak VisionOcrTilerTests).
    /// Walidacja: IsTripped, RecordFailure, Reset, ForceReset, prog z AgentMemoryState.
    /// </summary>
    public static class CircuitBreakerStateTests
    {
        private const string TestProfile = "TestCadProfile_CB";
        private static int _passed;
        private static int _failed;

        public static void RunTests()
        {
            _passed = 0;
            _failed = 0;
            Debug.WriteLine("=== CircuitBreakerStateTests START ===");

            // Reset stanu CB i flag
            CircuitBreakerState.ForceReset(TestProfile);
            AgentMemoryState.CircuitBreakerEnabled = true;
            AgentMemoryState.CircuitBreakerThreshold = 3;

            try
            {
                IsTripped_NewProfile_ReturnsFalse();
                RecordFailure_OneFailure_NotTripped();
                RecordFailure_ThresholdReached_IsTripped();
                RecordFailure_OverThreshold_StillTripped();
                Reset_AfterFailures_NotTripped();
                Reset_NoFailures_NoOp();
                ForceReset_AfterTripped_NotTripped();
                IsTripped_CircuitBreakerDisabled_AlwaysFalse();
                IsTripped_CustomThreshold_TripsAtCustomValue();
                IsTripped_NullOrEmptyProfile_ReturnsFalse();
                GetSnapshot_ReturnsCorrectValues();
                GetSnapshot_NoFailures_ZeroValues();
                GetAllSnapshots_ContainsAllFailedProfiles();
                ResetAll_ClearsEverything();
                PerProfile_Isolation();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UNHANDLED EXCEPTION] {ex.Message}");
                _failed++;
            }
            finally
            {
                CircuitBreakerState.ForceReset(TestProfile);
            }

            Debug.WriteLine($"=== CircuitBreakerStateTests END: {_passed} passed, {_failed} failed ===");
        }

        private static void Pass(string name)
        {
            _passed++;
            Debug.WriteLine($"[PASS] {name}");
        }

        private static void Fail(string name, string message)
        {
            _failed++;
            Debug.WriteLine($"[FAIL] {name}: {message}");
        }

        private static void AssertTrue(bool condition, string name, string message)
        {
            if (condition) Pass(name);
            else Fail(name, message);
        }

        private static void IsTripped_NewProfile_ReturnsFalse()
        {
            CircuitBreakerState.ForceReset(TestProfile);
            AssertTrue(!CircuitBreakerState.IsTripped(TestProfile), nameof(IsTripped_NewProfile_ReturnsFalse), "Nowy profil powinien byc odblokowany");
        }

        private static void RecordFailure_OneFailure_NotTripped()
        {
            CircuitBreakerState.ForceReset(TestProfile);
            CircuitBreakerState.RecordFailure(TestProfile, "test 1");
            AssertTrue(!CircuitBreakerState.IsTripped(TestProfile), nameof(RecordFailure_OneFailure_NotTripped), "1 awaria < prog 3");
        }

        private static void RecordFailure_ThresholdReached_IsTripped()
        {
            CircuitBreakerState.ForceReset(TestProfile);
            for (int i = 0; i < 3; i++) CircuitBreakerState.RecordFailure(TestProfile, $"failure {i}");
            AssertTrue(CircuitBreakerState.IsTripped(TestProfile), nameof(RecordFailure_ThresholdReached_IsTripped), "3 awarie powinny zablokowac");
        }

        private static void RecordFailure_OverThreshold_StillTripped()
        {
            CircuitBreakerState.ForceReset(TestProfile);
            for (int i = 0; i < 10; i++) CircuitBreakerState.RecordFailure(TestProfile);
            AssertTrue(CircuitBreakerState.IsTripped(TestProfile), nameof(RecordFailure_OverThreshold_StillTripped), "Nawet >3 awarii powinno zostawic zablokowany");
        }

        private static void Reset_AfterFailures_NotTripped()
        {
            CircuitBreakerState.ForceReset(TestProfile);
            for (int i = 0; i < 3; i++) CircuitBreakerState.RecordFailure(TestProfile);
            CircuitBreakerState.Reset(TestProfile);
            AssertTrue(!CircuitBreakerState.IsTripped(TestProfile), nameof(Reset_AfterFailures_NotTripped), "Reset powinien odblokowac");
        }

        private static void Reset_NoFailures_NoOp()
        {
            CircuitBreakerState.ForceReset(TestProfile);
            try
            {
                CircuitBreakerState.Reset(TestProfile);
                AssertTrue(!CircuitBreakerState.IsTripped(TestProfile), nameof(Reset_NoFailures_NoOp), "Reset bez awarii nie powinien crashowac");
            }
            catch (Exception ex)
            {
                Fail(nameof(Reset_NoFailures_NoOp), ex.Message);
            }
        }

        private static void ForceReset_AfterTripped_NotTripped()
        {
            CircuitBreakerState.ForceReset(TestProfile);
            for (int i = 0; i < 3; i++) CircuitBreakerState.RecordFailure(TestProfile);
            CircuitBreakerState.ForceReset(TestProfile);
            AssertTrue(!CircuitBreakerState.IsTripped(TestProfile), nameof(ForceReset_AfterTripped_NotTripped), "ForceReset powinien odblokowac");
        }

        private static void IsTripped_CircuitBreakerDisabled_AlwaysFalse()
        {
            CircuitBreakerState.ForceReset(TestProfile);
            for (int i = 0; i < 10; i++) CircuitBreakerState.RecordFailure(TestProfile);
            AgentMemoryState.CircuitBreakerEnabled = false;
            AssertTrue(!CircuitBreakerState.IsTripped(TestProfile), nameof(IsTripped_CircuitBreakerDisabled_AlwaysFalse), "Wylaczony CB nie blokuje");
            AgentMemoryState.CircuitBreakerEnabled = true; // restore
        }

        private static void IsTripped_CustomThreshold_TripsAtCustomValue()
        {
            CircuitBreakerState.ForceReset(TestProfile);
            AgentMemoryState.CircuitBreakerThreshold = 5;
            for (int i = 0; i < 4; i++) CircuitBreakerState.RecordFailure(TestProfile);
            AssertTrue(!CircuitBreakerState.IsTripped(TestProfile), nameof(IsTripped_CustomThreshold_TripsAtCustomValue) + " (4 < 5)", "Przy progu 5, 4 awarii nie blokuja");
            CircuitBreakerState.RecordFailure(TestProfile); // 5
            AssertTrue(CircuitBreakerState.IsTripped(TestProfile), nameof(IsTripped_CustomThreshold_TripsAtCustomValue) + " (5 == 5)", "Przy progu 5, 5 awarii blokuje");
            AgentMemoryState.CircuitBreakerThreshold = 3; // restore
        }

        private static void IsTripped_NullOrEmptyProfile_ReturnsFalse()
        {
            AssertTrue(!CircuitBreakerState.IsTripped(null), nameof(IsTripped_NullOrEmptyProfile_ReturnsFalse) + " (null)", "Null profil nie powinien byc zablokowany");
            AssertTrue(!CircuitBreakerState.IsTripped(""), nameof(IsTripped_NullOrEmptyProfile_ReturnsFalse) + " (empty)", "Pusty profil nie powinien byc zablokowany");
        }

        private static void GetSnapshot_ReturnsCorrectValues()
        {
            CircuitBreakerState.ForceReset(TestProfile);
            for (int i = 0; i < 2; i++) CircuitBreakerState.RecordFailure(TestProfile, "snap test");
            var snap = CircuitBreakerState.GetSnapshot(TestProfile);
            AssertTrue(snap.ProfileName == TestProfile, nameof(GetSnapshot_ReturnsCorrectValues) + " (name)", "Snapshot ma poprawna nazwe");
            AssertTrue(snap.Failures == 2, nameof(GetSnapshot_ReturnsCorrectValues) + " (count)", "Snapshot ma poprawna liczbe awarii");
            AssertTrue(snap.Threshold == 3, nameof(GetSnapshot_ReturnsCorrectValues) + " (threshold)", "Snapshot ma poprawny prog");
            AssertTrue(!snap.IsTripped, nameof(GetSnapshot_ReturnsCorrectValues) + " (not tripped)", "Snapshot nie jest tripped przy 2/3");
            AssertTrue(snap.LastFailureUtc != null, nameof(GetSnapshot_ReturnsCorrectValues) + " (lastFailure)", "Snapshot ma timestamp ostatniej awarii");
        }

        private static void GetSnapshot_NoFailures_ZeroValues()
        {
            CircuitBreakerState.ForceReset(TestProfile);
            var snap = CircuitBreakerState.GetSnapshot(TestProfile);
            AssertTrue(snap.Failures == 0, nameof(GetSnapshot_NoFailures_ZeroValues) + " (count)", "0 awarii");
            AssertTrue(!snap.IsTripped, nameof(GetSnapshot_NoFailures_ZeroValues) + " (not tripped)", "Nie tripped");
            AssertTrue(snap.LastFailureUtc == null, nameof(GetSnapshot_NoFailures_ZeroValues) + " (no timestamp)", "Brak timestamp");
        }

        private static void GetAllSnapshots_ContainsAllFailedProfiles()
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
                AssertTrue(all.Exists(s => s.ProfileName == P1 && s.Failures == 1), nameof(GetAllSnapshots_ContainsAllFailedProfiles) + " (P1)", "P1 ma 1 awarie");
                AssertTrue(all.Exists(s => s.ProfileName == P2 && s.Failures == 2), nameof(GetAllSnapshots_ContainsAllFailedProfiles) + " (P2)", "P2 ma 2 awarie");
            }
            finally
            {
                CircuitBreakerState.ForceReset(P1);
                CircuitBreakerState.ForceReset(P2);
            }
        }

        private static void ResetAll_ClearsEverything()
        {
            CircuitBreakerState.RecordFailure("X1_CB");
            CircuitBreakerState.RecordFailure("X2_CB");
            CircuitBreakerState.RecordFailure("X3_CB");
            CircuitBreakerState.ResetAll();
            AssertTrue(CircuitBreakerState.GetAllSnapshots().Count == 0, nameof(ResetAll_ClearsEverything), "Wszystkie profile odblokowane");
        }

        private static void PerProfile_Isolation()
        {
            const string PA = "IsoA_CB";
            const string PB = "IsoB_CB";
            CircuitBreakerState.ForceReset(PA);
            CircuitBreakerState.ForceReset(PB);
            try
            {
                for (int i = 0; i < 3; i++) CircuitBreakerState.RecordFailure(PA);
                AssertTrue(CircuitBreakerState.IsTripped(PA), nameof(PerProfile_Isolation) + " (PA tripped)", "PA ma 3 awarie - tripped");
                AssertTrue(!CircuitBreakerState.IsTripped(PB), nameof(PerProfile_Isolation) + " (PB not tripped)", "PB ma 0 awarii - nie tripped");
            }
            finally
            {
                CircuitBreakerState.ForceReset(PA);
                CircuitBreakerState.ForceReset(PB);
            }
        }
    }
}
