using NUnit.Framework;
using Bricscad_AgentAI_V2.Core;

namespace Bricscad_AgentAI_V2.Tests.Core
{
    /// <summary>
    /// Testy jednostkowe WorkValidator - w szczegolnosci publiczne MutatingTools/ReadOnlyTools
    /// (udostepnione w v2.34.0 dla Filaru 1 Agenta Rewidenta).
    /// </summary>
    [TestFixture]
    public class WorkValidatorTests
    {
        [Test]
        public void MutatingTools_ContainsCoreMutators()
        {
            Assert.IsTrue(WorkValidator.MutatingTools.Contains("CreateObject"));
            Assert.IsTrue(WorkValidator.MutatingTools.Contains("ModifyProperties"));
            Assert.IsTrue(WorkValidator.MutatingTools.Contains("ManageLayers"));
            Assert.IsTrue(WorkValidator.MutatingTools.Contains("EditBlock"));
            Assert.IsTrue(WorkValidator.MutatingTools.Contains("InsertBlock"));
        }

        [Test]
        public void MutatingTools_ContainsLayoutMutators()
        {
            Assert.IsTrue(WorkValidator.MutatingTools.Contains("PageSetupTool"));
            Assert.IsTrue(WorkValidator.MutatingTools.Contains("ManageLayoutTool"));
            Assert.IsTrue(WorkValidator.MutatingTools.Contains("PlotLayoutTool"));
        }

        [Test]
        public void ReadOnlyTools_ContainsCoreReaders()
        {
            Assert.IsTrue(WorkValidator.ReadOnlyTools.Contains("InspectEntity"));
            Assert.IsTrue(WorkValidator.ReadOnlyTools.Contains("GetPropertiesTool"));
            Assert.IsTrue(WorkValidator.ReadOnlyTools.Contains("AnalyzeSelectionTool"));
            Assert.IsTrue(WorkValidator.ReadOnlyTools.Contains("ReadFromBlackboard"));
        }

        [Test]
        public void ReadOnlyTools_DoesNotContainMutators()
        {
            Assert.IsFalse(WorkValidator.ReadOnlyTools.Contains("CreateObject"));
            Assert.IsFalse(WorkValidator.ReadOnlyTools.Contains("ModifyProperties"));
            Assert.IsFalse(WorkValidator.ReadOnlyTools.Contains("ManageLayers"));
        }

        [Test]
        public void MutatingTools_DoesNotContainReaders()
        {
            Assert.IsFalse(WorkValidator.MutatingTools.Contains("InspectEntity"));
            Assert.IsFalse(WorkValidator.MutatingTools.Contains("GetPropertiesTool"));
        }

        [Test]
        public void MutatingTools_AreCaseInsensitive()
        {
            // HashSet z StringComparer.OrdinalIgnoreCase - lookup powinien dzialac w kazdym case
            Assert.IsTrue(WorkValidator.MutatingTools.Contains("createobject"));
            Assert.IsTrue(WorkValidator.MutatingTools.Contains("CREATEOBJECT"));
            Assert.IsTrue(WorkValidator.MutatingTools.Contains("CreateObject"));
        }

        [Test]
        public void ReadOnlyTools_AreCaseInsensitive()
        {
            Assert.IsTrue(WorkValidator.ReadOnlyTools.Contains("inspectentity"));
            Assert.IsTrue(WorkValidator.ReadOnlyTools.Contains("INSPECTENTITY"));
        }

        [Test]
        public void MutatingAndReadOnly_Disjoint()
        {
            // Zadna nazwa nie moze byc jednoczesnie mutujaca i readonly
            // (to by lamalo mechanizm guard w ToolOrchestrator)
            foreach (var tool in WorkValidator.MutatingTools)
            {
                Assert.IsFalse(WorkValidator.ReadOnlyTools.Contains(tool),
                    $"Tool '{tool}' jest zarowno w Mutating jak ReadOnly");
            }
        }
    }
}
