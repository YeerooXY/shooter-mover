#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace ShooterMover.Tests.PlayMode.VisibleSliceIntegration
{
    public sealed class Stage1CanonicalResultsOwnershipPlayModeTests
    {
        private const string CanonicalControllerTypeName =
            "ShooterMover.UI.ProductionFlow.ProductionResultsControllerV1";
        private const string SummaryTypeName =
            "ShooterMover.UI.ProductionFlow.ProductionResultsSummaryV1";
        private const string BridgeTypeName =
            "ShooterMover.UI.ProductionFlow.ProductionReadOnlyResultsBridgeV1";
        private const string RemovedControllerTypeName =
            "ShooterMover.UnityAdapters.Production.Stage1.Stage1ReadOnlyResultsControllerV1";
        private const string CompositionPath =
            "Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.cs";
        private const string FlowPath =
            "Assets/ShooterMover/Production/Stage1/Stage1PlayableLoopCompositionV1.Flow.cs";

        [Test]
        public void Stage1UsesCanonicalResultsController()
        {
            Type canonical = FindType(CanonicalControllerTypeName);
            Type summary = FindType(SummaryTypeName);
            Type bridge = FindType(BridgeTypeName);

            Assert.That(FindTypeOrNull(RemovedControllerTypeName), Is.Null);
            Assert.That(summary, Is.Not.Null);
            Assert.That(
                bridge.GetMethod(
                    "Present",
                    BindingFlags.Static | BindingFlags.Public),
                Is.Not.Null);
            Assert.That(
                canonical.GetProperty(
                    "Summary",
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Not.Null);

            string compositionSource = File.ReadAllText(CompositionPath);
            string flowSource = File.ReadAllText(FlowPath);
            Assert.That(compositionSource, Does.Not.Contain("pendingResults"));
            Assert.That(
                compositionSource,
                Does.Not.Contain("Stage1ReadOnlyResultsControllerV1"));
            Assert.That(flowSource, Does.Not.Contain("ReadResultsBackground"));
            Assert.That(
                flowSource,
                Does.Contain("ProductionReadOnlyResultsBridgeV1.Present"));
        }

        private static Type FindType(string fullName)
        {
            Type type = FindTypeOrNull(fullName);
            if (type == null)
            {
                throw new InvalidOperationException(
                    "Required type not found: " + fullName);
            }
            return type;
        }

        private static Type FindTypeOrNull(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }
    }
}
#endif
