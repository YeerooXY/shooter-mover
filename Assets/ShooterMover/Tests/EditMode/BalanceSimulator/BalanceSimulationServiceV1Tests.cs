using System;
using NUnit.Framework;

namespace ShooterMover.Editor.BalanceSimulator.Tests
{
    public sealed class BalanceSimulationServiceV1Tests
    {
        [Test]
        public void SameRequestProducesSameReportFingerprint()
        {
            BalanceSimulationRequestV1 request = Request(BalanceSimulationModeV1.Batch, 25, 123456UL, 50);
            BalanceSimulationServiceV1 service = new BalanceSimulationServiceV1(new RuntimeBalanceScenarioV1());

            BalanceSimulationReportV1 first = service.Run(request);
            BalanceSimulationReportV1 second = service.Run(request);

            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(second.EquipmentInstanceCount, Is.EqualTo(first.EquipmentInstanceCount));
            Assert.That(second.MoneyDelta, Is.EqualTo(first.MoneyDelta));
            Assert.That(second.ScrapDelta, Is.EqualTo(first.ScrapDelta));
        }

        [Test]
        public void SingleOpenForcesExactlyOneIteration()
        {
            BalanceSimulationRequestV1 request = Request(BalanceSimulationModeV1.SingleOpen, 25, 9UL, 999);
            BalanceSimulationReportV1 report = new BalanceSimulationServiceV1(
                new RuntimeBalanceScenarioV1()).Run(request);

            Assert.That(report.Request.NumberOfSimulations, Is.EqualTo(1));
            Assert.That(report.Samples, Has.Count.EqualTo(1));
        }

        [Test]
        public void SameStrongboxDefinitionCreatesSeparateEquipmentInstances()
        {
            BalanceSimulationReportV1 report = new BalanceSimulationServiceV1(
                new RuntimeBalanceScenarioV1()).Run(Request(BalanceSimulationModeV1.SingleOpen, 25, 77UL, 1));
            BalanceSimulationIterationResultV1 sample = report.Samples[0];
            BalanceEquipmentObservationV1 first = null;
            BalanceEquipmentObservationV1 second = null;
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
            BalanceSimulationReportV1 report = new BalanceSimulationServiceV1(
                new RuntimeBalanceScenarioV1()).Run(Request(BalanceSimulationModeV1.Batch, 25, 42UL, 20));

            Assert.That(report.EquipmentInstanceCount, Is.GreaterThan(20L));
            Assert.That(report.UniqueEquipmentInstanceCount, Is.EqualTo(report.EquipmentInstanceCount));
            Assert.That(report.DuplicateDefinitionCount, Is.GreaterThan(0L));
            Assert.That(report.DuplicateDefinitionFrequency, Is.GreaterThan(0.0));
        }

        [Test]
        public void SoftRequirementsAreReportedWithoutHardGatingCandidates()
        {
            BalanceSimulationReportV1 report = new BalanceSimulationServiceV1(
                new RuntimeBalanceScenarioV1()).Run(Request(BalanceSimulationModeV1.Batch, 1, 100UL, 10));

            Assert.That(report.SoftEligibleCandidateCount, Is.GreaterThan(0L));
            Assert.That(report.MinimumCraftingUnlockLevel, Is.GreaterThan(1));
            Assert.That(report.FindCount(report.Rejections, "crafting:soft-level-requirement"), Is.EqualTo(10L));
        }

        [Test]
        public void RuntimeRejectionsAreAggregatedDeterministically()
        {
            BalanceSimulationServiceV1 service = new BalanceSimulationServiceV1(new RejectingRuntime());
            BalanceSimulationReportV1 report = service.Run(Request(BalanceSimulationModeV1.Batch, 1, 5UL, 3));

            Assert.That(report.FindCount(report.Rejections, "test:impossible-roll"), Is.EqualTo(3L));
            Assert.That(report.EquipmentInstanceCount, Is.Zero);
        }

        private static BalanceSimulationRequestV1 Request(
            BalanceSimulationModeV1 mode,
            int characterLevel,
            ulong seed,
            int simulations)
        {
            return new BalanceSimulationRequestV1(
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

        private sealed class RejectingRuntime : IBalanceSimulationRuntimeV1
        {
            public BalanceSimulationIterationResultV1 Run(BalanceSimulationIterationRequestV1 request)
            {
                return new BalanceSimulationIterationResultV1(
                    request.IterationIndex,
                    request.IterationSeed,
                    Array.Empty<BalanceRewardObservationV1>(),
                    Array.Empty<BalanceEquipmentObservationV1>(),
                    0L,
                    0L,
                    0L,
                    0L,
                    0L,
                    0,
                    0,
                    new[] { new BalanceRejectionV1("test", "impossible-roll", "fixture") });
            }
        }
    }
}
