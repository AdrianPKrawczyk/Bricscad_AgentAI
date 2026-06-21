using System.Collections.Generic;
using System.Diagnostics;
using Bricscad_AgentAI_V2.Core;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    /// <summary>
    /// Testy jednostkowe WorkValidator - publiczne MutatingTools/ReadOnlyTools
    /// (udostepnione w v2.34.0 dla Filaru 1 Agenta Rewidenta).
    /// Wzorzec V2: static class + RunTests() + Debug.Assert.
    /// </summary>
    public static class WorkValidatorTests
    {
        private static int _passed;
        private static int _failed;

        public static void RunTests()
        {
            _passed = 0;
            _failed = 0;
            Debug.WriteLine("=== WorkValidatorTests START ===");

            try
            {
                MutatingTools_ContainsCoreMutators();
                MutatingTools_ContainsLayoutMutators();
                ReadOnlyTools_ContainsCoreReaders();
                ReadOnlyTools_DoesNotContainMutators();
                MutatingTools_DoesNotContainReaders();
                MutatingTools_AreCaseInsensitive();
                ReadOnlyTools_AreCaseInsensitive();
                MutatingAndReadOnly_Disjoint();
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"[UNHANDLED EXCEPTION] {ex.Message}");
                _failed++;
            }

            Debug.WriteLine($"=== WorkValidatorTests END: {_passed} passed, {_failed} failed ===");
        }

        private static void Pass(string name) { _passed++; Debug.WriteLine($"[PASS] {name}"); }
        private static void Fail(string name, string message) { _failed++; Debug.WriteLine($"[FAIL] {name}: {message}"); }

        private static void AssertTrue(bool condition, string name, string message)
        {
            if (condition) Pass(name);
            else Fail(name, message);
        }

        private static void AssertContains(HashSet<string> set, string item, string testName)
        {
            AssertTrue(set.Contains(item), testName + " (contains " + item + ")", "Zbior powinien zawierac " + item);
        }

        private static void AssertNotContains(HashSet<string> set, string item, string testName)
        {
            AssertTrue(!set.Contains(item), testName + " (not contains " + item + ")", "Zbior NIE powinien zawierac " + item);
        }

        private static void MutatingTools_ContainsCoreMutators()
        {
            AssertContains(WorkValidator.MutatingTools, "CreateObject", nameof(MutatingTools_ContainsCoreMutators));
            AssertContains(WorkValidator.MutatingTools, "ModifyProperties", nameof(MutatingTools_ContainsCoreMutators));
            AssertContains(WorkValidator.MutatingTools, "ManageLayers", nameof(MutatingTools_ContainsCoreMutators));
            AssertContains(WorkValidator.MutatingTools, "EditBlock", nameof(MutatingTools_ContainsCoreMutators));
            AssertContains(WorkValidator.MutatingTools, "InsertBlock", nameof(MutatingTools_ContainsCoreMutators));
        }

        private static void MutatingTools_ContainsLayoutMutators()
        {
            AssertContains(WorkValidator.MutatingTools, "PageSetupTool", nameof(MutatingTools_ContainsLayoutMutators));
            AssertContains(WorkValidator.MutatingTools, "ManageLayoutTool", nameof(MutatingTools_ContainsLayoutMutators));
            AssertContains(WorkValidator.MutatingTools, "PlotLayoutTool", nameof(MutatingTools_ContainsLayoutMutators));
        }

        private static void ReadOnlyTools_ContainsCoreReaders()
        {
            AssertContains(WorkValidator.ReadOnlyTools, "InspectEntity", nameof(ReadOnlyTools_ContainsCoreReaders));
            AssertContains(WorkValidator.ReadOnlyTools, "GetPropertiesTool", nameof(ReadOnlyTools_ContainsCoreReaders));
            AssertContains(WorkValidator.ReadOnlyTools, "AnalyzeSelectionTool", nameof(ReadOnlyTools_ContainsCoreReaders));
            AssertContains(WorkValidator.ReadOnlyTools, "ReadFromBlackboard", nameof(ReadOnlyTools_ContainsCoreReaders));
        }

        private static void ReadOnlyTools_DoesNotContainMutators()
        {
            AssertNotContains(WorkValidator.ReadOnlyTools, "CreateObject", nameof(ReadOnlyTools_DoesNotContainMutators));
            AssertNotContains(WorkValidator.ReadOnlyTools, "ModifyProperties", nameof(ReadOnlyTools_DoesNotContainMutators));
            AssertNotContains(WorkValidator.ReadOnlyTools, "ManageLayers", nameof(ReadOnlyTools_DoesNotContainMutators));
        }

        private static void MutatingTools_DoesNotContainReaders()
        {
            AssertNotContains(WorkValidator.MutatingTools, "InspectEntity", nameof(MutatingTools_DoesNotContainReaders));
            AssertNotContains(WorkValidator.MutatingTools, "GetPropertiesTool", nameof(MutatingTools_DoesNotContainReaders));
        }

        private static void MutatingTools_AreCaseInsensitive()
        {
            AssertContains(WorkValidator.MutatingTools, "createobject", nameof(MutatingTools_AreCaseInsensitive));
            AssertContains(WorkValidator.MutatingTools, "CREATEOBJECT", nameof(MutatingTools_AreCaseInsensitive));
            AssertContains(WorkValidator.MutatingTools, "CreateObject", nameof(MutatingTools_AreCaseInsensitive));
        }

        private static void ReadOnlyTools_AreCaseInsensitive()
        {
            AssertContains(WorkValidator.ReadOnlyTools, "inspectentity", nameof(ReadOnlyTools_AreCaseInsensitive));
            AssertContains(WorkValidator.ReadOnlyTools, "INSPECTENTITY", nameof(ReadOnlyTools_AreCaseInsensitive));
        }

        private static void MutatingAndReadOnly_Disjoint()
        {
            // Zadna nazwa nie moze byc jednoczesnie mutujaca i readonly
            // (to by lamalo mechanizm guard w ToolOrchestrator)
            bool overlap = false;
            string overlapItem = null;
            foreach (var tool in WorkValidator.MutatingTools)
            {
                if (WorkValidator.ReadOnlyTools.Contains(tool))
                {
                    overlap = true;
                    overlapItem = tool;
                    break;
                }
            }
            AssertTrue(!overlap, nameof(MutatingAndReadOnly_Disjoint),
                overlapItem != null ? "Zbior Mutating i ReadOnly maja czesc wspolna: " + overlapItem : "Disjoint");
        }
    }
}
