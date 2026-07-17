using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Editor.BalanceSimulator.Tests
{
    public sealed partial class LootboxSimulatorRuntimeV1Tests
    {
        [Test]
        public void AuthoritativeOpenAppliesEquipmentAndScrapThenConsumesExactBox()
        {
            AuthoritativeStrongboxSimulatorRuntimeV1 runtime = CreateAuthoritativeRuntime();
            AuthoritativeStrongboxPreparedOpenV1 box = runtime.PrepareBatch(
                new[] { 5 },
                30,
                123456UL)[0];

            Assert.That(runtime.IsBoxOwned(box), Is.True);
            Assert.That(runtime.OpeningSequence, Is.Zero);
            Assert.That(runtime.ScrapBalance, Is.Zero);

            StrongboxOpeningResultRuntimeV1 result = runtime.OpenOrRetry(box);

            Assert.That(result.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(result.PreviousSequence, Is.Zero);
            Assert.That(result.CurrentSequence, Is.EqualTo(1L));
            Assert.That(runtime.OpeningSequence, Is.EqualTo(1L));
            Assert.That(runtime.IsBoxOwned(box), Is.False);
            Assert.That(runtime.ScrapBalance, Is.GreaterThan(0L));
            Assert.That(runtime.EquipmentFrom(result).Count, Is.EqualTo(1));
            Assert.That(result.GeneratedOutcome.Payloads.Count, Is.EqualTo(2));

            EquipmentInstance equipment = runtime.EquipmentFrom(result)[0];
            bool exactGrantFound = false;
            for (int index = 0; index < result.GeneratedOutcome.Payloads.Count; index++)
            {
                if (result.GeneratedOutcome.Payloads[index].Grant.Kind
                    == RewardGrantKindV1.EquipmentReference)
                {
                    exactGrantFound = true;
                    Assert.That(
                        result.GeneratedOutcome.Payloads[index].Grant.ContentStableId,
                        Is.EqualTo(equipment.DefinitionId));
                }
            }
            Assert.That(exactGrantFound, Is.True);
        }

        [Test]
        public void AuthoritativeReplayCannotGrantOrConsumeTwice()
        {
            AuthoritativeStrongboxSimulatorRuntimeV1 runtime = CreateAuthoritativeRuntime();
            AuthoritativeStrongboxPreparedOpenV1 box = runtime.PrepareBatch(
                new[] { 8 },
                30,
                4444UL)[0];
            StrongboxOpeningResultRuntimeV1 opened = runtime.OpenOrRetry(box);
            long scrap = runtime.ScrapBalance;
            long holdingsSequence = runtime.HoldingsSequence;
            long openingSequence = runtime.OpeningSequence;

            StrongboxOpeningResultRuntimeV1 replay = runtime.OpenOrRetry(box);

            Assert.That(opened.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(replay.Status, Is.EqualTo(StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange));
            Assert.That(replay.GeneratedOutcome.Fingerprint, Is.EqualTo(opened.GeneratedOutcome.Fingerprint));
            Assert.That(runtime.ScrapBalance, Is.EqualTo(scrap));
            Assert.That(runtime.HoldingsSequence, Is.EqualTo(holdingsSequence));
            Assert.That(runtime.OpeningSequence, Is.EqualTo(openingSequence));
            Assert.That(runtime.IsBoxOwned(box), Is.False);
        }

        [Test]
        public void FrozenBatchPreservesClickOrderAndOneGlobalBoxSequence()
        {
            AuthoritativeStrongboxSimulatorRuntimeV1 runtime = CreateAuthoritativeRuntime();
            var boxes = runtime.PrepareBatch(
                new[] { 1, 11, 3 },
                30,
                98765UL);

            Assert.That(boxes.Count, Is.EqualTo(3));
            Assert.That(boxes[0].Tier.TierNumber, Is.EqualTo(1));
            Assert.That(boxes[1].Tier.TierNumber, Is.EqualTo(11));
            Assert.That(boxes[2].Tier.TierNumber, Is.EqualTo(3));
            Assert.That(boxes[0].Context.InstanceStableId, Is.Not.EqualTo(boxes[1].Context.InstanceStableId));
            Assert.That(boxes[1].Context.InstanceStableId, Is.Not.EqualTo(boxes[2].Context.InstanceStableId));
            Assert.That(boxes[0].Context.TierStableId, Is.Not.EqualTo(boxes[1].Context.TierStableId));

            Assert.That(runtime.OpenOrRetry(boxes[0]).CurrentSequence, Is.EqualTo(1L));
            Assert.That(runtime.OpenOrRetry(boxes[1]).CurrentSequence, Is.EqualTo(2L));
            Assert.That(runtime.OpenOrRetry(boxes[2]).CurrentSequence, Is.EqualTo(3L));
            Assert.That(runtime.OpeningSequence, Is.EqualTo(3L));
        }

        [Test]
        public void SameAuthoritativeBatchInputProducesByteIdenticalCommitments()
        {
            AuthoritativeStrongboxSimulatorRuntimeV1 left = CreateAuthoritativeRuntime();
            AuthoritativeStrongboxSimulatorRuntimeV1 right = CreateAuthoritativeRuntime();

            var first = left.PrepareBatch(new[] { 2, 7, 11 }, 30, 555UL);
            var replay = right.PrepareBatch(new[] { 2, 7, 11 }, 30, 555UL);

            Assert.That(replay.Count, Is.EqualTo(first.Count));
            for (int index = 0; index < first.Count; index++)
            {
                Assert.That(replay[index].Fingerprint, Is.EqualTo(first[index].Fingerprint));
                Assert.That(
                    replay[index].ToCanonicalString(),
                    Is.EqualTo(first[index].ToCanonicalString()));
                Assert.That(
                    replay[index].CommittedSourceDefinitionId,
                    Is.EqualTo(first[index].CommittedSourceDefinitionId));
            }
        }

        [Test]
        public void AuthoritativeRuntimeRejectsInvalidCatalogBeforeCreatingAuthorities()
        {
            AuthoritativeStrongboxSimulatorRuntimeV1 runtime;
            string diagnostic;

            Assert.That(
                AuthoritativeStrongboxSimulatorRuntimeV1.TryCreate(
                    "{}",
                    out runtime,
                    out diagnostic),
                Is.False);
            Assert.That(runtime, Is.Null);
            Assert.That(diagnostic, Is.Not.Empty);
        }

        private static AuthoritativeStrongboxSimulatorRuntimeV1 CreateAuthoritativeRuntime()
        {
            AuthoritativeStrongboxSimulatorRuntimeV1 runtime;
            string diagnostic;
            bool created = AuthoritativeStrongboxSimulatorRuntimeV1.TryCreate(
                BuildCatalogJson(),
                out runtime,
                out diagnostic);
            Assert.That(created, Is.True, diagnostic);
            Assert.That(runtime, Is.Not.Null);
            return runtime;
        }
    }
}
