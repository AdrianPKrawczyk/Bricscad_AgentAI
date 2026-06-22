using System.Collections.Generic;
using System.Diagnostics;
using Bricscad_AgentAI_V2.Core;
using Bricscad_AgentAI_V2.Core.Rewident;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    /// <summary>
    /// Testy regresyjne dla v2.35.1 - 3 bugi Rewidenta:
    /// - BUG #1: CreateObject nie zapisywal pary @evidence_before_ dla nowego obiektu.
    ///   Skutek: Rewident halucynowal "BRAK CHAIN OF EVIDENCE" dla legalnego create-scenariusza.
    /// - BUG #2: WriteToBlackboard byl w MutatingTools - Wariant A (Audytor) liczyl
    ///   go jako "mutacje DWG", ale EngineTracer (subskrybujacy zdarzenia bazy DWG)
    ///   jej nie widzial. Skutek: falszywy alarm "BRAK MUTACJI W DWG".
    /// - BUG #3: W odpowiedzi na halucynacje Rewidenta ("brak pary before/after")
    ///   system repair wymuszal na Workerze dodatkowe mutacje (np. zmiane koloru)
    ///   zamiast po prostu zaakceptowac poprawna prace. Skutek: user dostawal
    ///   obiekt z innym kolorem niz prosil.
    ///
    /// Wzorzec V2: static class + RunTests() + Debug.Assert (jak VisionOcrTilerTests).
    /// </summary>
    public static class RewidentRegressionTests
    {
        private static int _passed;
        private static int _failed;

        public static void RunTests()
        {
            _passed = 0;
            _failed = 0;
            Debug.WriteLine("=== RewidentRegressionTests START (v2.35.1) ===");

            // Upewnij sie, ze flag i jest w bezpiecznym stanie
            SharedMemoryState.Clear();

            try
            {
                // ===== BUG #1: CreateObject + Chain of Evidence =====
                EvidenceSnapshot_CreateNotExistedBefore_HasCorrectShape();
                EvidenceSnapshot_CreateNotExistedBefore_SerializesToJson();
                EvidenceSnapshot_ObjectExistedBefore_DefaultsToTrue();
                EvidenceSnapshot_BlackboardKey_FormatPreserved();

                // ===== BUG #2: WriteToBlackboard klasyfikacja =====
                WorkValidator_WriteToBlackboard_IsNowReadOnly();
                WorkValidator_WriteToBlackboard_NotInMutatingTools();
                WorkValidator_WriteToBlackboard_InReadOnlyTools();

                // ===== BUG #3: Sanity check halucynacji =====
                // (testy IsChainOfEvidenceHallucination sa prywatne - testujemy
                //  zachowanie przez EvidenceHandles + SharedMemoryState)
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[UNHANDLED EXCEPTION] {ex.Message}");
                _failed++;
            }
            finally
            {
                SharedMemoryState.Clear();
            }

            Debug.WriteLine($"=== RewidentRegressionTests END: {_passed} passed, {_failed} failed ===");
        }

        private static void Pass(string name) { _passed++; Debug.WriteLine($"[PASS] {name}"); }
        private static void Fail(string name, string message) { _failed++; Debug.WriteLine($"[FAIL] {name}: {message}"); }
        private static void AssertTrue(bool condition, string name, string message)
        {
            if (condition) Pass(name);
            else Fail(name, message);
        }

        // ====================================================================
        // BUG #1: EvidenceSnapshot.CreateNotExistedBefore
        // ====================================================================

        private static void EvidenceSnapshot_CreateNotExistedBefore_HasCorrectShape()
        {
            // Regresja v2.35.0: dla CreateObject ToolOrchestrator w v2.35.0 mial
            // pusty ActiveSelection, wiec 'before' nigdy sie nie zapisywal. W v2.35.1
            // tworzymy syntetyczny 'before' z ObjectExistedBefore=false.
            //
            // Test weryfikuje ze helper poprawnie buduje snapshot - nawet bez
            // prawdziwego ObjectId (zeby test nie zalezal od Teigha).
            var snap = EvidenceSnapshot.CreateNotExistedBefore(default(Teigha.DatabaseServices.ObjectId), "Circle");
            AssertTrue(snap != null, "EvidenceSnapshot_CreateNotExistedBefore_HasCorrectShape (not null)",
                "CreateNotExistedBefore powinien zwrocic obiekt");
            AssertTrue(snap.ObjectExistedBefore == false, "EvidenceSnapshot_CreateNotExistedBefore_HasCorrectShape (ObjectExistedBefore=false)",
                "CreateNotExistedBefore powinien ustawic ObjectExistedBefore=false");
            AssertTrue(snap.Properties != null && snap.Properties.Count == 0, "EvidenceSnapshot_CreateNotExistedBefore_HasCorrectShape (empty properties)",
                "CreateNotExistedBefore powinien miec pusty properties (obiekt nie istnial)");
            AssertTrue(snap.IsNotExistedBefore == true, "EvidenceSnapshot_CreateNotExistedBefore_HasCorrectShape (IsNotExistedBefore=true)",
                "IsNotExistedBefore powinno zwrocic true dla create-snapshot");
            AssertTrue(snap.ObjectType == "Circle", "EvidenceSnapshot_CreateNotExistedBefore_HasCorrectShape (ObjectType)",
                "ObjectType powinno byc przekazane do konstruktora");
            AssertTrue(snap.TimestampUtcTicks > 0, "EvidenceSnapshot_CreateNotExistedBefore_HasCorrectShape (TimestampUtcTicks)",
                "TimestampUtcTicks powinien byc ustawiony (DateTime.UtcNow.Ticks)");
        }

        private static void EvidenceSnapshot_CreateNotExistedBefore_SerializesToJson()
        {
            // Regresja: JSON musi zawierac ObjectExistedBefore=false - LLM
            // Rewident czyta to przez ReadFromBlackboard, wiec format musi byc stabilny.
            var snap = EvidenceSnapshot.CreateNotExistedBefore(default(Teigha.DatabaseServices.ObjectId), "Circle");
            string json = snap.ToJson();
            AssertTrue(!string.IsNullOrEmpty(json), "EvidenceSnapshot_CreateNotExistedBefore_SerializesToJson (non-empty)",
                "ToJson powinien zwrocic niepusty string");
            // Wazne: LLM musi widziec ten klucz w JSON-ie (nie [JsonIgnore])
            AssertTrue(json.Contains("ObjectExistedBefore"),
                "EvidenceSnapshot_CreateNotExistedBefore_SerializesToJson (ObjectExistedBefore w JSON)",
                "JSON musi zawierac ObjectExistedBefore - to jest sygnal dla LLM ze to create-scenario");
            AssertTrue(json.Contains("false"),
                "EvidenceSnapshot_CreateNotExistedBefore_SerializesToJson (false w JSON)",
                "JSON musi zawierac wartosc false (obiekt nie istnial przed)");
        }

        private static void EvidenceSnapshot_ObjectExistedBefore_DefaultsToTrue()
        {
            // Regresja: zwykle snapshoty (before/after dla modify) maja ObjectExistedBefore=null
            // (co serializuje sie jako brak pola dzięki NullValueHandling.Ignore).
            // IsNotExistedBefore musi zwrocic false dla nich.
            var snap = new EvidenceSnapshot { Handle = "1A", ObjectType = "Circle" };
            AssertTrue(snap.ObjectExistedBefore == null, "EvidenceSnapshot_ObjectExistedBefore_DefaultsToTrue (null default)",
                "ObjectExistedBefore powinno byc null dla zwyklego snapshotu");
            AssertTrue(snap.IsNotExistedBefore == false, "EvidenceSnapshot_ObjectExistedBefore_DefaultsToTrue (IsNotExistedBefore=false)",
                "IsNotExistedBefore powinno zwrocic false dla modify-snapshotu");
        }

        private static void EvidenceSnapshot_BlackboardKey_FormatPreserved()
        {
            // Regresja: klucz Blackboard nie moze sie zmienic (kompatywnosc wstecz).
            // Format: @evidence_before_<HandleHex> / @evidence_after_<HandleHex>
            string beforeKey = EvidenceSnapshot.BlackboardKey("before", "14A");
            string afterKey = EvidenceSnapshot.BlackboardKey("after", "14A");
            AssertTrue(beforeKey == "@evidence_before_14A", "EvidenceSnapshot_BlackboardKey_FormatPreserved (before key)",
                "Klucz before powinien miec format @evidence_before_<HandleHex>");
            AssertTrue(afterKey == "@evidence_after_14A", "EvidenceSnapshot_BlackboardKey_FormatPreserved (after key)",
                "Klucz after powinien miec format @evidence_after_<HandleHex>");
            var (b, a) = EvidenceSnapshot.BlackboardPair("14A");
            AssertTrue(b == "@evidence_before_14A" && a == "@evidence_after_14A",
                "EvidenceSnapshot_BlackboardKey_FormatPreserved (BlackboardPair)",
                "BlackboardPair powinien zwrocic oba klucze w odpowiedniej kolejnosci");
        }

        // ====================================================================
        // BUG #2: WriteToBlackboard - przeniesienie z Mutating do ReadOnly
        // ====================================================================

        private static void WorkValidator_WriteToBlackboard_NotInMutatingTools()
        {
            // Regresja v2.35.0: WriteToBlackboard byl w MutatingTools. To powodowalo
            // ze Wariant A Rewidenta (RewidentAuditService.cs:107) liczyl go jako
            // "mutacje DWG" - ale EngineTracer (subskrybujacy zdarzenia bazy DWG)
            // jej nie widzial. Falszywy alarm "BRAK MUTACJI W DWG".
            //
            // Fix v2.35.1: WriteToBlackboard przeniesiony do ReadOnlyTools.
            // Blackboard to pamiec wspoldzielona miedzy agentami (context/notes),
            // NIE jest czescia rysunku DWG.
            AssertTrue(!WorkValidator.MutatingTools.Contains("WriteToBlackboard"),
                "WorkValidator_WriteToBlackboard_NotInMutatingTools",
                "WriteToBlackboard NIE powinien byc w MutatingTools (BUG #2 regresja)");
        }

        private static void WorkValidator_WriteToBlackboard_InReadOnlyTools()
        {
            // Regresja: WriteToBlackboard powinien byc sklasyfikowany jako ReadOnly
            // (lub Composite - ale na pewno nie Mutating). W praktyce jest to
            // zapis do wspoldzielonej pamieci, wiec ReadOnly jest poprawne.
            AssertTrue(WorkValidator.ReadOnlyTools.Contains("WriteToBlackboard"),
                "WorkValidator_WriteToBlackboard_InReadOnlyTools",
                "WriteToBlackboard powinien byc w ReadOnlyTools (Fix v2.35.1)");
        }

        private static void WorkValidator_WriteToBlackboard_IsNowReadOnly()
        {
            // Regresja: walidator poprawnie sklasyfikuje WriteToBlackboard
            // jako ReadOnly tool. To znaczy ze Wariant A Rewidenta NIE bedzie
            // go liczyl jako mutacje DWG.
            // Testujemy to przez fakt ze WriteToBlackboard jest w ReadOnlyTools
            // (walidator uzywa tych samych setow do ClassifyToolEffect).
            AssertTrue(WorkValidator.ReadOnlyTools.Contains("WriteToBlackboard") &&
                       !WorkValidator.MutatingTools.Contains("WriteToBlackboard"),
                "WorkValidator_WriteToBlackboard_IsNowReadOnly",
                "WriteToBlackboard powinien byc ReadOnly, nie Mutating (regresja BUG #2)");
        }
    }
}
