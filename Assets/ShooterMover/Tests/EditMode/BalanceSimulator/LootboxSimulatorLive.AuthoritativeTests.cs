using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Editor.BalanceSimulator.Tests
{
    public sealed partial class LootboxSimulatorLiveTests
    {
        [Test]
        public void AuthoritativeOpenAppliesHybridEquipmentAndConsumesExactBox()
        {
            AuthoritativeStrongboxSimulatorLive runtime =
                CreateAuthoritativeRuntime();
            AuthoritativeStrongboxPreparedOpen box = runtime.PrepareBatch(
                new[] { 5 },
                30,
                123456UL)[0];

            Assert.That(runtime.IsBoxOwned(box), Is.True);
            Assert.That(runtime.OpeningSequence, Is.Zero);
            Assert.That(runtime.ScrapBalance, Is.Zero);

            StrongboxOpeningResultLive result = runtime.OpenOrRetry(box);

            Assert.That(
                result.Status,
                Is.EqualTo(StrongboxOpeningLiveStatus.Opened));
            Assert.That(result.PreviousSequence, Is.Zero);
            Assert.That(result.CurrentSequence, Is.EqualTo(1L));
            Assert.That(runtime.OpeningSequence, Is.EqualTo(1L));
            Assert.That(runtime.IsBoxOwned(box), Is.False);
            Assert.That(runtime.ScrapBalance, Is.GreaterThan(0L));
            Assert.That(runtime.EquipmentFrom(result).Count, Is.EqualTo(1));
            Assert.That(result.GeneratedOutcome.Payloads.Count, Is.EqualTo(2));

            EquipmentInstance equipment = runtime.EquipmentFrom(result)[0];
            Assert.That(equipment.Augments, Is.Empty);
            GeneratedEquipmentAugmentSignature signature;
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
                    == RewardGrantKind.EquipmentReference)
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
            AuthoritativeStrongboxSimulatorLive runtime =
                CreateAuthoritativeRuntime();
            AuthoritativeStrongboxPreparedOpen box = runtime.PrepareBatch(
                new[] { 8 },
                30,
                4444UL)[0];
            StrongboxOpeningResultLive opened = runtime.OpenOrRetry(box);
            EquipmentInstance equipment = runtime.EquipmentFrom(opened)[0];
            GeneratedEquipmentAugmentSignature originalSignature;
            Assert.That(
                runtime.TryGetAugmentSignature(
                    equipment.InstanceId,
                    out originalSignature),
                Is.True);
            long scrap = runtime.ScrapBalance;
            long holdingsSequence = runtime.HoldingsSequence;
            long openingSequence = runtime.OpeningSequence;
            int signatureCount = runtime.AugmentSignatures.Count;

            StrongboxOpeningResultLive replay = runtime.OpenOrRetry(box);

            Assert.That(
                opened.Status,
                Is.EqualTo(StrongboxOpeningLiveStatus.Opened));
            Assert.That(
                replay.Status,
                Is.EqualTo(
                    StrongboxOpeningLiveStatus.ExactDuplicateNoChange));
            Assert.That(
                replay.GeneratedOutcome.Fingerprint,
                Is.EqualTo(opened.GeneratedOutcome.Fingerprint));
            Assert.That(runtime.ScrapBalance, Is.EqualTo(scrap));
            Assert.That(runtime.HoldingsSequence, Is.EqualTo(holdingsSequence));
            Assert.That(runtime.OpeningSequence, Is.EqualTo(openingSequence));
            Assert.That(runtime.AugmentSignatures.Count, Is.EqualTo(signatureCount));
            GeneratedEquipmentAugmentSignature replaySignature;
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
            AuthoritativeStrongboxSimulatorLive runtime =
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
            AuthoritativeStrongboxSimulatorLive left =
                CreateAuthoritativeRuntime();
            AuthoritativeStrongboxSimulatorLive right =
                CreateAuthoritativeRuntime();

            AuthoritativeStrongboxPreparedOpen first =
                left.PrepareBatch(new[] { 11 }, 30, 555UL)[0];
            AuthoritativeStrongboxPreparedOpen replay =
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
            GeneratedEquipmentAugmentSignature leftSignature;
            GeneratedEquipmentAugmentSignature rightSignature;
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
            AuthoritativeStrongboxSimulatorLive runtime;
            string diagnostic;

            Assert.That(
                AuthoritativeStrongboxSimulatorLive.TryCreate(
                    "{}",
                    out runtime,
                    out diagnostic),
                Is.False);
            Assert.That(runtime, Is.Null);
            Assert.That(diagnostic, Is.Not.Empty);
        }

        private static AuthoritativeStrongboxSimulatorLive
            CreateAuthoritativeRuntime()
        {
            AuthoritativeStrongboxSimulatorLive runtime;
            string diagnostic;
            bool created = AuthoritativeStrongboxSimulatorLive.TryCreate(
                BuildCatalogJson(),
                out runtime,
                out diagnostic);
            Assert.That(created, Is.True, diagnostic);
            Assert.That(runtime, Is.Not.Null);
            return runtime;
        }
    }
}
