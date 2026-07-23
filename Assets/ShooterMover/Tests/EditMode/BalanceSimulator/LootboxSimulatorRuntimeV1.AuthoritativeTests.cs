using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Editor.BalanceSimulator.Tests
{
    public sealed partial class LootboxSimulatorRuntimeV1Tests
    {
        [Test]
        public void AuthoritativeOpenAppliesHybridEquipmentAndConsumesExactBox()
        {
            AuthoritativeStrongboxSimulatorRuntimeV1 runtime =
                CreateAuthoritativeRuntime();
            AuthoritativeStrongboxPreparedOpenV1 box = runtime.PrepareBatch(
                new[] { 5 },
                30,
                123456UL)[0];

            Assert.That(runtime.IsBoxOwned(box), Is.True);
            Assert.That(runtime.OpeningSequence, Is.Zero);
            Assert.That(runtime.ScrapBalance, Is.Zero);

            StrongboxOpeningResultRuntimeV1 result = runtime.OpenOrRetry(box);

            Assert.That(
                result.Status,
                Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(result.PreviousSequence, Is.Zero);
            Assert.That(result.CurrentSequence, Is.EqualTo(1L));
            Assert.That(runtime.OpeningSequence, Is.EqualTo(1L));
            Assert.That(runtime.IsBoxOwned(box), Is.False);
            Assert.That(runtime.ScrapBalance, Is.GreaterThan(0L));
            Assert.That(runtime.EquipmentFrom(result).Count, Is.EqualTo(1));
            Assert.That(result.GeneratedOutcome.Payloads.Count, Is.EqualTo(2));

            EquipmentInstance equipment = runtime.EquipmentFrom(result)[0];
            Assert.That(equipment.Augments, Is.Empty);
            GeneratedEquipmentAugmentSignatureV1 signature;
            Assert.That(
                runtime.TryGetAugmentSignature(
                    equipment.InstanceId,
                    out signature),
                Is.True);
            Assert.That(signature, Is.Not.Null);
            Assert.That(
                signature.EquipmentInstanceStableId,
                Is.EqualTo(equipment.InstanceId));
            Assert.That(
                signature.SourceStrongboxInstanceStableId,
                Is.EqualTo(box.Context.InstanceStableId));
            Assert.That(signature.Capacity, Is.GreaterThanOrEqualTo(0));
            Assert.That(signature.SharedLevel, Is.GreaterThanOrEqualTo(0));
            Assert.That(
                signature.Capacity == 0,
                Is.EqualTo(signature.SharedLevel == 0));

            bool exactGrantFound = false;
            for (int index = 0;
                 index < result.GeneratedOutcome.Payloads.Count;
                 index++)
            {
                if (result.GeneratedOutcome.Payloads[index].Grant.Kind
                    == RewardGrantKindV1.EquipmentReference)
                {
                    exactGrantFound = true;
                    Assert.That(
                        result.GeneratedOutcome.Payloads[index]
                            .Grant.ContentStableId,
                        Is.EqualTo(equipment.DefinitionId));
                }
            }
            Assert.That(exactGrantFound, Is.True);
        }

        [Test]
        public void AuthoritativeReplayCannotGrantConsumeOrRecordTwice()
        {
            AuthoritativeStrongboxSimulatorRuntimeV1 runtime =
                CreateAuthoritativeRuntime();
            AuthoritativeStrongboxPreparedOpenV1 box = runtime.PrepareBatch(
                new[] { 8 },
                30,
                4444UL)[0];
            StrongboxOpeningResultRuntimeV1 opened = runtime.OpenOrRetry(box);
            EquipmentInstance equipment = runtime.EquipmentFrom(opened)[0];
            GeneratedEquipmentAugmentSignatureV1 originalSignature;
            Assert.That(
                runtime.TryGetAugmentSignature(
                    equipment.InstanceId,
                    out originalSignature),
                Is.True);
            long scrap = runtime.ScrapBalance;
            long holdingsSequence = runtime.HoldingsSequence;
            long openingSequence = runtime.OpeningSequence;
            int signatureCount = runtime.AugmentSignatures.Count;

            StrongboxOpeningResultRuntimeV1 replay = runtime.OpenOrRetry(box);

            Assert.That(
                opened.Status,
                Is.EqualTo(StrongboxOpeningRuntimeStatusV1.Opened));
            Assert.That(
                replay.Status,
                Is.EqualTo(
                    StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange));
            Assert.That(
                replay.GeneratedOutcome.Fingerprint,
                Is.EqualTo(opened.GeneratedOutcome.Fingerprint));
            Assert.That(runtime.ScrapBalance, Is.EqualTo(scrap));
            Assert.That(runtime.HoldingsSequence, Is.EqualTo(holdingsSequence));
            Assert.That(runtime.OpeningSequence, Is.EqualTo(openingSequence));
            Assert.That(runtime.AugmentSignatures.Count, Is.EqualTo(signatureCount));
            GeneratedEquipmentAugmentSignatureV1 replaySignature;
            Assert.That(
                runtime.TryGetAugmentSignature(
                    equipment.InstanceId,
                    out replaySignature),
                Is.True);
            Assert.That(
                replaySignature.Fingerprint,
                Is.EqualTo(originalSignature.Fingerprint));
            Assert.That(runtime.IsBoxOwned(box), Is.False);
        }

        [Test]
        public void FrozenBatchPreservesClickOrderAndOneGlobalBoxSequence()
        {
            AuthoritativeStrongboxSimulatorRuntimeV1 runtime =
                CreateAuthoritativeRuntime();
            var boxes = runtime.PrepareBatch(
                new[] { 1, 11, 3 },
                30,
                98765UL);

            Assert.That(boxes.Count, Is.EqualTo(3));
            Assert.That(boxes[0].Tier.TierNumber, Is.EqualTo(1));
            Assert.That(boxes[1].Tier.TierNumber, Is.EqualTo(11));
            Assert.That(boxes[2].Tier.TierNumber, Is.EqualTo(3));
            Assert.That(
                boxes[0].Context.InstanceStableId,
                Is.Not.EqualTo(boxes[1].Context.InstanceStableId));
            Assert.That(
                boxes[1].Context.InstanceStableId,
                Is.Not.EqualTo(boxes[2].Context.InstanceStableId));
            Assert.That(
                boxes[0].Context.TierStableId,
                Is.Not.EqualTo(boxes[1].Context.TierStableId));

            Assert.That(
                runtime.OpenOrRetry(boxes[0]).CurrentSequence,
                Is.EqualTo(1L));
            Assert.That(
                runtime.OpenOrRetry(boxes[1]).CurrentSequence,
                Is.EqualTo(2L));
            Assert.That(
                runtime.OpenOrRetry(boxes[2]).CurrentSequence,
                Is.EqualTo(3L));
            Assert.That(runtime.OpeningSequence, Is.EqualTo(3L));
        }

        [Test]
        public void SameAuthoritativeInputsProduceIdenticalHybridEquipmentAndSignature()
        {
            AuthoritativeStrongboxSimulatorRuntimeV1 left =
                CreateAuthoritativeRuntime();
            AuthoritativeStrongboxSimulatorRuntimeV1 right =
                CreateAuthoritativeRuntime();

            AuthoritativeStrongboxPreparedOpenV1 first =
                left.PrepareBatch(new[] { 11 }, 30, 555UL)[0];
            AuthoritativeStrongboxPreparedOpenV1 replay =
                right.PrepareBatch(new[] { 11 }, 30, 555UL)[0];
            Assert.That(replay.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(
                replay.ToCanonicalString(),
                Is.EqualTo(first.ToCanonicalString()));

            EquipmentInstance leftEquipment =
                left.EquipmentFrom(left.OpenOrRetry(first))[0];
            EquipmentInstance rightEquipment =
                right.EquipmentFrom(right.OpenOrRetry(replay))[0];
            Assert.That(
                rightEquipment.ToCanonicalString(),
                Is.EqualTo(leftEquipment.ToCanonicalString()));
            GeneratedEquipmentAugmentSignatureV1 leftSignature;
            GeneratedEquipmentAugmentSignatureV1 rightSignature;
            Assert.That(
                left.TryGetAugmentSignature(
                    leftEquipment.InstanceId,
                    out leftSignature),
                Is.True);
            Assert.That(
                right.TryGetAugmentSignature(
                    rightEquipment.InstanceId,
                    out rightSignature),
                Is.True);
            Assert.That(
                rightSignature.ToCanonicalString(),
                Is.EqualTo(leftSignature.ToCanonicalString()));
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

        private static AuthoritativeStrongboxSimulatorRuntimeV1
            CreateAuthoritativeRuntime()
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
