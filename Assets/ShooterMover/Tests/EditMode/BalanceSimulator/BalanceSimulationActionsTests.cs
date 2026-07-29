using System;
using NUnit.Framework;

namespace ShooterMover.Editor.BalanceSimulator.Tests
{
    public sealed class BalanceSimulationActionsTests
    {
        [Test]
        public void SameRequestProducesSameReportFingerprint()
        {
            BalanceSimulationRequest request = Request(BalanceSimulationMode.Batch, 25, 123456UL, 50);
            BalanceSimulationActions service = new BalanceSimulationActions(new LiveBalanceScenario());

            BalanceSimulationReport first = service.Run(request);
            BalanceSimulationReport second = service.Run(request);

            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(second.EquipmentInstanceCount, Is.EqualTo(first.EquipmentInstanceCount));
            Assert.That(second.MoneyDelta, Is.EqualTo(first.MoneyDelta));
            Assert.That(second.ScrapDelta, Is.EqualTo(first.ScrapDelta));
        }

        [Test]
        public void SingleOpenForcesExactlyOneIteration()
        {
            BalanceSimulationRequest request = Request(BalanceSimulationMode.SingleOpen, 25, 9UL, 999);
            BalanceSimulationReport report = new BalanceSimulationActions(
                new LiveBalanceScenario()).Run(request);

            Assert.That(report.Request.NumberOfSimulations, Is.EqualTo(1));
            Assert.That(report.Samples, Has.Count.EqualTo(1));
        }

        [Test]
        public void SameStrongboxDefinitionCreatesSeparateEquipmentInstances()
        {
            BalanceSimulationReport report = new BalanceSimulationActions(
                new LiveBalanceScenario()).Run(Request(BalanceSimulationMode.SingleOpen, 25, 77UL, 1));
            BalanceSimulationIterationResult sample = report.Samples[0];
            BalanceEquipmentObservation first = null;
            BalanceEquipmentObservation second = null;
            for (int index = 0; index < sample.Equipment.Count; index++)
            {
                if (sample.Equipment[index].Source != "strongbox") { continue; }
                if (first == null) { first = sample.Equipment[index]; }
                else { second = sample.Equipment[index]; break; }
            }

            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(second.Equipment.DefinitionId, Is.EqualTo(first.Equipment.DefinitionId));
            Assert.That(second.Equipment.InstanceId, Is.Not.EqualTo(first.Equipment.InstanceId));
        }

        [Test]
        public void BatchCountsDefinitionsWithoutCollapsingInstanceIdentity()
        {
            BalanceSimulationReport report = new BalanceSimulationActions(
                new LiveBalanceScenario()).Run(Request(BalanceSimulationMode.Batch, 25, 42UL, 20));

            Assert.That(report.EquipmentInstanceCount, Is.GreaterThan(20L));
            Assert.That(report.UniqueEquipmentInstanceCount, Is.EqualTo(report.EquipmentInstanceCount));
            Assert.That(report.DuplicateDefinitionCount, Is.GreaterThan(0L));
            Assert.That(report.DuplicateDefinitionFrequency, Is.GreaterThan(0.0));
        }

        [Test]
        public void SoftRequirementsAreReportedWithoutHardGatingCandidates()
        {
            BalanceSimulationReport report = new BalanceSimulationActions(
                new LiveBalanceScenario()).Run(Request(BalanceSimulationMode.Batch, 1, 100UL, 10));

            Assert.That(report.SoftEligibleCandidateCount, Is.GreaterThan(0L));
            Assert.That(report.MinimumCraftingUnlockLevel, Is.GreaterThan(1));
            Assert.That(report.FindCount(report.Rejections, "crafting:soft-level-requirement"), Is.EqualTo(10L));
        }

        [Test]
        public void RuntimeRejectionsAreAggregatedDeterministically()
        {
            BalanceSimulationActions service = new BalanceSimulationActions(new RejectingLive());
            BalanceSimulationReport report = service.Run(Request(BalanceSimulationMode.Batch, 1, 5UL, 3));

            Assert.That(report.FindCount(report.Rejections, "test:impossible-roll"), Is.EqualTo(3L));
            Assert.That(report.EquipmentInstanceCount, Is.Zero);
        }

        private static BalanceSimulationRequest Request(
            BalanceSimulationMode mode,
            int characterLevel,
            ulong seed,
            int simulations)
        {
            return new BalanceSimulationRequest(
                mode,
                characterLevel,
                2,
                characterLevel,
                characterLevel,
                seed,
                simulations,
                10000L,
                10000L);
        }

        private sealed class RejectingLive : IBalanceSimulationLive
        {
            public BalanceSimulationIterationResult Run(BalanceSimulationIterationRequest request)
            {
                return new BalanceSimulationIterationResult(
                    request.IterationIndex,
                    request.IterationSeed,
                    Array.Empty<BalanceRewardObservation>(),
                    Array.Empty<BalanceEquipmentObservation>(),
                    0L,
                    0L,
                    0L,
                    0L,
                    0L,
                    0,
                    0,
                    new[] { new BalanceRejection("test", "impossible-roll", "fixture") });
            }
        }
    }
}
