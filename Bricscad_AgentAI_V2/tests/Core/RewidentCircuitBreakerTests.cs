using System;
using System.Diagnostics;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.Rewident;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    /// <summary>
    /// Testy jednostkowe RewidentCircuitBreaker (Filar 4 Agenta Rewidenta).
    /// Wzorzec V2: static class + RunTests() + Debug.Assert (jak VisionOcrTilerTests).
    /// Walidacja: IsTripped, RecordFailure, Reset, ForceReset, prog z AgentMemoryState.
    /// </summary>
    public static class RewidentCircuitBreakerTests
    {
        private const string TestProfile = "TestCadProfile_CB";
        private static int _passed;
        private static int _failed;

        public static void RunTests()
        {
            _passed = 0;
            _failed = 0;
            Debug.WriteLine("=== RewidentCircuitBreakerTests START ===");

            // Reset stanu CB i flag
            RewidentCircuitBreaker.ForceReset(TestProfile);
            RewidentState.CircuitBreakerEnabled = true;
            RewidentState.CircuitBreakerThreshold = 3;

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
                RewidentCircuitBreaker.ForceReset(TestProfile);
            }

            Debug.WriteLine($"=== RewidentCircuitBreakerTests END: {_passed} passed, {_failed} failed ===");
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
            RewidentCircuitBreaker.ForceReset(TestProfile);
            AssertTrue(!RewidentCircuitBreaker.IsTripped(TestProfile), nameof(IsTripped_NewProfile_ReturnsFalse), "Nowy profil powinien byc odblokowany");
        }

        private static void RecordFailure_OneFailure_NotTripped()
        {
            RewidentCircuitBreaker.ForceReset(TestProfile);
            RewidentCircuitBreaker.RecordFailure(TestProfile, "test 1");
            AssertTrue(!RewidentCircuitBreaker.IsTripped(TestProfile), nameof(RecordFailure_OneFailure_NotTripped), "1 awaria < prog 3");
        }

        private static void RecordFailure_ThresholdReached_IsTripped()
        {
            RewidentCircuitBreaker.ForceReset(TestProfile);
            for (int i = 0; i < 3; i++) RewidentCircuitBreaker.RecordFailure(TestProfile, $"failure {i}");
            AssertTrue(RewidentCircuitBreaker.IsTripped(TestProfile), nameof(RecordFailure_ThresholdReached_IsTripped), "3 awarie powinny zablokowac");
        }

        private static void RecordFailure_OverThreshold_StillTripped()
        {
            RewidentCircuitBreaker.ForceReset(TestProfile);
            for (int i = 0; i < 10; i++) RewidentCircuitBreaker.RecordFailure(TestProfile);
            AssertTrue(RewidentCircuitBreaker.IsTripped(TestProfile), nameof(RecordFailure_OverThreshold_StillTripped), "Nawet >3 awarii powinno zostawic zablokowany");
        }

        private static void Reset_AfterFailures_NotTripped()
        {
            RewidentCircuitBreaker.ForceReset(TestProfile);
            for (int i = 0; i < 3; i++) RewidentCircuitBreaker.RecordFailure(TestProfile);
            RewidentCircuitBreaker.Reset(TestProfile);
            AssertTrue(!RewidentCircuitBreaker.IsTripped(TestProfile), nameof(Reset_AfterFailures_NotTripped), "Reset powinien odblokowac");
        }

        private static void Reset_NoFailures_NoOp()
        {
            RewidentCircuitBreaker.ForceReset(TestProfile);
            try
            {
                RewidentCircuitBreaker.Reset(TestProfile);
                AssertTrue(!RewidentCircuitBreaker.IsTripped(TestProfile), nameof(Reset_NoFailures_NoOp), "Reset bez awarii nie powinien crashowac");
            }
            catch (Exception ex)
            {
                Fail(nameof(Reset_NoFailures_NoOp), ex.Message);
            }
        }

        private static void ForceReset_AfterTripped_NotTripped()
        {
            RewidentCircuitBreaker.ForceReset(TestProfile);
            for (int i = 0; i < 3; i++) RewidentCircuitBreaker.RecordFailure(TestProfile);
            RewidentCircuitBreaker.ForceReset(TestProfile);
            AssertTrue(!RewidentCircuitBreaker.IsTripped(TestProfile), nameof(ForceReset_AfterTripped_NotTripped), "ForceReset powinien odblokowac");
        }

        private static void IsTripped_CircuitBreakerDisabled_AlwaysFalse()
        {
            RewidentCircuitBreaker.ForceReset(TestProfile);
            for (int i = 0; i < 10; i++) RewidentCircuitBreaker.RecordFailure(TestProfile);
            RewidentState.CircuitBreakerEnabled = false;
            AssertTrue(!RewidentCircuitBreaker.IsTripped(TestProfile), nameof(IsTripped_CircuitBreakerDisabled_AlwaysFalse), "Wylaczony CB nie blokuje");
            RewidentState.CircuitBreakerEnabled = true; // restore
        }

        private static void IsTripped_CustomThreshold_TripsAtCustomValue()
        {
            RewidentCircuitBreaker.ForceReset(TestProfile);
            RewidentState.CircuitBreakerThreshold = 5;
            for (int i = 0; i < 4; i++) RewidentCircuitBreaker.RecordFailure(TestProfile);
            AssertTrue(!RewidentCircuitBreaker.IsTripped(TestProfile), nameof(IsTripped_CustomThreshold_TripsAtCustomValue) + " (4 < 5)", "Przy progu 5, 4 awarii nie blokuja");
            RewidentCircuitBreaker.RecordFailure(TestProfile); // 5
            AssertTrue(RewidentCircuitBreaker.IsTripped(TestProfile), nameof(IsTripped_CustomThreshold_TripsAtCustomValue) + " (5 == 5)", "Przy progu 5, 5 awarii blokuje");
            RewidentState.CircuitBreakerThreshold = 3; // restore
        }

        private static void IsTripped_NullOrEmptyProfile_ReturnsFalse()
        {
            AssertTrue(!RewidentCircuitBreaker.IsTripped(null), nameof(IsTripped_NullOrEmptyProfile_ReturnsFalse) + " (null)", "Null profil nie powinien byc zablokowany");
            AssertTrue(!RewidentCircuitBreaker.IsTripped(""), nameof(IsTripped_NullOrEmptyProfile_ReturnsFalse) + " (empty)", "Pusty profil nie powinien byc zablokowany");
        }

        private static void GetSnapshot_ReturnsCorrectValues()
        {
            RewidentCircuitBreaker.ForceReset(TestProfile);
            for (int i = 0; i < 2; i++) RewidentCircuitBreaker.RecordFailure(TestProfile, "snap test");
            var snap = RewidentCircuitBreaker.GetSnapshot(TestProfile);
            AssertTrue(snap.ProfileName == TestProfile, nameof(GetSnapshot_ReturnsCorrectValues) + " (name)", "Snapshot ma poprawna nazwe");
            AssertTrue(snap.Failures == 2, nameof(GetSnapshot_ReturnsCorrectValues) + " (count)", "Snapshot ma poprawna liczbe awarii");
            AssertTrue(snap.Threshold == 3, nameof(GetSnapshot_ReturnsCorrectValues) + " (threshold)", "Snapshot ma poprawny prog");
            AssertTrue(!snap.IsTripped, nameof(GetSnapshot_ReturnsCorrectValues) + " (not tripped)", "Snapshot nie jest tripped przy 2/3");
            AssertTrue(snap.LastFailureUtc != null, nameof(GetSnapshot_ReturnsCorrectValues) + " (lastFailure)", "Snapshot ma timestamp ostatniej awarii");
        }

        private static void GetSnapshot_NoFailures_ZeroValues()
        {
            RewidentCircuitBreaker.ForceReset(TestProfile);
            var snap = RewidentCircuitBreaker.GetSnapshot(TestProfile);
            AssertTrue(snap.Failures == 0, nameof(GetSnapshot_NoFailures_ZeroValues) + " (count)", "0 awarii");
            AssertTrue(!snap.IsTripped, nameof(GetSnapshot_NoFailures_ZeroValues) + " (not tripped)", "Nie tripped");
            AssertTrue(snap.LastFailureUtc == null, nameof(GetSnapshot_NoFailures_ZeroValues) + " (no timestamp)", "Brak timestamp");
        }

        private static void GetAllSnapshots_ContainsAllFailedProfiles()
        {
            const string P1 = "ProfileA_CB";
            const string P2 = "ProfileB_CB";
            RewidentCircuitBreaker.ForceReset(P1);
            RewidentCircuitBreaker.ForceReset(P2);
            try
            {
                RewidentCircuitBreaker.RecordFailure(P1);
                RewidentCircuitBreaker.RecordFailure(P2);
                RewidentCircuitBreaker.RecordFailure(P2);

                var all = RewidentCircuitBreaker.GetAllSnapshots();
                AssertTrue(all.Exists(s => s.ProfileName == P1 && s.Failures == 1), nameof(GetAllSnapshots_ContainsAllFailedProfiles) + " (P1)", "P1 ma 1 awarie");
                AssertTrue(all.Exists(s => s.ProfileName == P2 && s.Failures == 2), nameof(GetAllSnapshots_ContainsAllFailedProfiles) + " (P2)", "P2 ma 2 awarie");
            }
            finally
            {
                RewidentCircuitBreaker.ForceReset(P1);
                RewidentCircuitBreaker.ForceReset(P2);
            }
        }

        private static void ResetAll_ClearsEverything()
        {
            RewidentCircuitBreaker.RecordFailure("X1_CB");
            RewidentCircuitBreaker.RecordFailure("X2_CB");
            RewidentCircuitBreaker.RecordFailure("X3_CB");
            RewidentCircuitBreaker.ResetAll();
            AssertTrue(RewidentCircuitBreaker.GetAllSnapshots().Count == 0, nameof(ResetAll_ClearsEverything), "Wszystkie profile odblokowane");
        }

        private static void PerProfile_Isolation()
        {
            const string PA = "IsoA_CB";
            const string PB = "IsoB_CB";
            RewidentCircuitBreaker.ForceReset(PA);
            RewidentCircuitBreaker.ForceReset(PB);
            try
            {
                for (int i = 0; i < 3; i++) RewidentCircuitBreaker.RecordFailure(PA);
                AssertTrue(RewidentCircuitBreaker.IsTripped(PA), nameof(PerProfile_Isolation) + " (PA tripped)", "PA ma 3 awarie - tripped");
                AssertTrue(!RewidentCircuitBreaker.IsTripped(PB), nameof(PerProfile_Isolation) + " (PB not tripped)", "PB ma 0 awarii - nie tripped");
            }
            finally
            {
                RewidentCircuitBreaker.ForceReset(PA);
                RewidentCircuitBreaker.ForceReset(PB);
            }
        }
    }
}
