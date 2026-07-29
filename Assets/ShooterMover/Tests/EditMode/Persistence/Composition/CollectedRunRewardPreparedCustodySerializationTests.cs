using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Rewards.CollectedRunTransfers;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Persistence.Composition
{
    public sealed class CollectedRunRewardPreparedCustodySerializationTests
    {
        [Test]
        public void AwaitingAcceptedEndRoundTripPreservesExactRecord()
        {
            CollectedRunRewardPreparedTransfer source = CreateAwaiting("awaiting");

            CollectedRunRewardPreparedTransfer restored = RoundTrip(source);

            Assert.That(restored.State,
                Is.EqualTo(
                    CollectedRunRewardPreparedTransferState.AwaitingAcceptedEnd));
            Assert.That(restored.Fingerprint, Is.EqualTo(source.Fingerprint));
            Assert.That(restored.TransferOperationStableId, Is.Null);
            Assert.That(restored.AcceptedMissionResultStableId, Is.Null);
        }

        [Test]
        public void PreparedRoundTripPreservesAcceptedMissionAndPlanFacts()
        {
            CollectedRunRewardPreparedTransfer source = CreatePrepared("prepared");

            CollectedRunRewardPreparedTransfer restored = RoundTrip(source);

            Assert.That(restored.State,
                Is.EqualTo(CollectedRunRewardPreparedTransferState.Prepared));
            Assert.That(restored.Fingerprint, Is.EqualTo(source.Fingerprint));
            Assert.That(restored.TransferOperationStableId,
                Is.EqualTo(source.TransferOperationStableId));
            Assert.That(restored.AcceptedMissionResultStableId,
                Is.EqualTo(source.AcceptedMissionResultStableId));
            Assert.That(restored.AcceptedMissionResultFingerprint,
                Is.EqualTo(source.AcceptedMissionResultFingerprint));
            Assert.That(restored.BatchFingerprint,
                Is.EqualTo(source.BatchFingerprint));
            Assert.That(restored.ApplicationPlanFingerprint,
                Is.EqualTo(source.ApplicationPlanFingerprint));
        }

        [Test]
        public void PersistedRoundTripPreservesReceiptFingerprint()
        {
            CollectedRunRewardPreparedTransfer source =
                CreatePrepared("persisted").MarkPersisted(
                    Fingerprint("receipt-persisted"));

            CollectedRunRewardPreparedTransfer restored = RoundTrip(source);

            Assert.That(restored.State,
                Is.EqualTo(CollectedRunRewardPreparedTransferState.Persisted));
            Assert.That(restored.PersistedReceiptFingerprint,
                Is.EqualTo(source.PersistedReceiptFingerprint));
            Assert.That(restored.Fingerprint, Is.EqualTo(source.Fingerprint));
        }

        [Test]
        public void ExactEquipmentInstanceAndAugmentsSurviveRoundTrip()
        {
            CollectedRunRewardPreparedTransfer source = CreatePrepared("equipment");

            CollectedRunRewardPreparedTransfer restored = RoundTrip(source);
            EquipmentInstance expected = source.Equipment.Single();
            EquipmentInstance actual = restored.Equipment.Single();

            Assert.That(actual.InstanceId, Is.EqualTo(expected.InstanceId));
            Assert.That(actual.DefinitionId, Is.EqualTo(expected.DefinitionId));
            Assert.That(actual.ItemLevel, Is.EqualTo(expected.ItemLevel));
            Assert.That(actual.QualityId, Is.EqualTo(expected.QualityId));
            Assert.That(actual.Fingerprint, Is.EqualTo(expected.Fingerprint));
            Assert.That(actual.Augments.Count, Is.EqualTo(2));
            for (int index = 0; index < expected.Augments.Count; index++)
            {
                Assert.That(actual.Augments[index].InstanceId,
                    Is.EqualTo(expected.Augments[index].InstanceId));
                Assert.That(actual.Augments[index].DefinitionId,
                    Is.EqualTo(expected.Augments[index].DefinitionId));
                Assert.That(actual.Augments[index].Tier,
                    Is.EqualTo(expected.Augments[index].Tier));
                Assert.That(actual.Augments[index].Level,
                    Is.EqualTo(expected.Augments[index].Level));
                Assert.That(actual.Augments[index].ToCanonicalString(),
                    Is.EqualTo(expected.Augments[index].ToCanonicalString()));
            }
        }

        [Test]
        public void ExactUnopenedStrongboxContextSurvivesRoundTrip()
        {
            CollectedRunRewardPreparedTransfer source = CreatePrepared("strongbox");

            CollectedRunRewardPreparedTransfer restored = RoundTrip(source);
            StrongboxInstanceContext expected = source.Strongboxes.Single();
            StrongboxInstanceContext actual = restored.Strongboxes.Single();

            Assert.That(actual.InstanceStableId,
                Is.EqualTo(expected.InstanceStableId));
            Assert.That(actual.TierStableId, Is.EqualTo(expected.TierStableId));
            Assert.That(actual.RootSeed, Is.EqualTo(expected.RootSeed));
            Assert.That(actual.AlgorithmVersion,
                Is.EqualTo(expected.AlgorithmVersion));
            Assert.That(actual.ProgressionContext.Fingerprint,
                Is.EqualTo(expected.ProgressionContext.Fingerprint));
            Assert.That(actual.SourceContextStableId,
                Is.EqualTo(expected.SourceContextStableId));
            Assert.That(actual.CollectionProvenanceStableId,
                Is.EqualTo(expected.CollectionProvenanceStableId));
            Assert.That(actual.AlgorithmContentFingerprint,
                Is.EqualTo(expected.AlgorithmContentFingerprint));
            Assert.That(actual.Fingerprint, Is.EqualTo(expected.Fingerprint));
        }

        [Test]
        public void IdenticalSnapshotsEncodeToByteIdenticalPayloads()
        {
            CollectedRunRewardPreparedTransferSnapshot first =
                new CollectedRunRewardPreparedTransferSnapshot(
                    7L,
                    new[] { CreatePrepared("identical") });
            CollectedRunRewardPreparedTransferSnapshot second =
                new CollectedRunRewardPreparedTransferSnapshot(
                    7L,
                    new[] { CreatePrepared("identical") });

            string firstPayload = Codec.Encode(first);
            string secondPayload = Codec.Encode(second);

            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(secondPayload, Is.EqualTo(firstPayload));
            CollectionAssert.AreEqual(
                Convert.FromBase64String(firstPayload),
                Convert.FromBase64String(secondPayload));
        }

        [Test]
        public void CorruptedPayloadIsRejected()
        {
            CollectedRunRewardPreparedTransferSnapshot source = Snapshot(
                CreatePrepared("corrupt"));
            byte[] bytes = Convert.FromBase64String(Codec.Encode(source));
            bytes[0] ^= 0x7F;

            CollectedRunRewardPreparedTransferSnapshot decoded;
            string rejection;
            bool accepted = Codec.TryDecode(
                Convert.ToBase64String(bytes),
                out decoded,
                out rejection);

            Assert.That(accepted, Is.False);
            Assert.That(decoded, Is.Null);
            Assert.That(rejection,
                Does.StartWith(
                    "collected-run-prepared-transfer-payload-invalid:"));
        }

        [Test]
        public void TrailingDataIsRejected()
        {
            CollectedRunRewardPreparedTransferSnapshot source = Snapshot(
                CreatePrepared("trailing"));
            byte[] original = Convert.FromBase64String(Codec.Encode(source));
            byte[] withTrailing = new byte[original.Length + 3];
            Buffer.BlockCopy(original, 0, withTrailing, 0, original.Length);
            withTrailing[original.Length] = 1;
            withTrailing[original.Length + 1] = 2;
            withTrailing[original.Length + 2] = 3;

            CollectedRunRewardPreparedTransferSnapshot decoded;
            string rejection;
            bool accepted = Codec.TryDecode(
                Convert.ToBase64String(withTrailing),
                out decoded,
                out rejection);

            Assert.That(accepted, Is.False);
            Assert.That(decoded, Is.Null);
            Assert.That(rejection,
                Does.StartWith(
                    "collected-run-prepared-transfer-payload-invalid:"));
        }

        [Test]
        public void ComponentFingerprintMismatchIsRejectedByAccountExpectation()
        {
            CollectedRunRewardPreparedTransfer source =
                CreatePrepared("fingerprint-mismatch");
            CollectedRunRewardPreparedTransferStore authority =
                new CollectedRunRewardPreparedTransferStore(
                    Snapshot(source));
            ISaveComponentBridge adapter =
                CollectedRunRewardPreparedTransferSaveComponent
                    .CreateAdapter(authority);
            SaveComponentSnapshot component = adapter.ExportComponent();
            CharacterInstanceSnapshot[] slots =
                new CharacterInstanceSnapshot[
                    PlayerAccountSnapshot.CharacterSlotCount];
            slots[0] = new CharacterInstanceSnapshot(
                source.SelectedCharacterStableId,
                Id("loadout-profile.striker"),
                0,
                "Fingerprint Pilot",
                0L,
                new[] { component });
            PlayerAccountSnapshot account = new PlayerAccountSnapshot(
                Id("account.fingerprint-mismatch"),
                0L,
                slots,
                null);
            using (CollectedRunRewardPersistenceExpectation.Begin(
                source.SelectedCharacterStableId,
                new Dictionary<StableId, string>
                {
                    {
                        component.ComponentStableId,
                        Fingerprint("wrong-component-fingerprint")
                    },
                }))
            {
                SaveComponentValidationResult validation =
                    CollectedRunRewardPersistenceExpectation.Validate(account);

                Assert.That(validation.Succeeded, Is.False);
                Assert.That(validation.RejectionCode,
                    Is.EqualTo(
                        "collected-run-persistence-expected-component-mismatch:"
                        + component.ComponentStableId));
            }
        }

        [Test]
        public void ReorderedUnorderedInputsProduceCanonicalOutput()
        {
            CollectedRunRewardPreparedTransfer forward =
                CreateAwaiting("canonical", false);
            CollectedRunRewardPreparedTransfer reversed =
                CreateAwaiting("canonical", true);
            CollectedRunRewardPreparedTransferSnapshot first =
                new CollectedRunRewardPreparedTransferSnapshot(
                    3L,
                    new[] { forward });
            CollectedRunRewardPreparedTransferSnapshot second =
                new CollectedRunRewardPreparedTransferSnapshot(
                    3L,
                    new[] { reversed });

            Assert.That(reversed.Fingerprint, Is.EqualTo(forward.Fingerprint));
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(Codec.Encode(second), Is.EqualTo(Codec.Encode(first)));
        }

        [Test]
        public void SixCharacterSlotsRemainIsolatedAfterRoundTrip()
        {
            List<CollectedRunRewardPreparedTransfer> records =
                new List<CollectedRunRewardPreparedTransfer>();
            for (int slot = 0; slot < 6; slot++)
            {
                records.Add(CreatePrepared(
                    "slot-" + slot,
                    Id("character-instance.slot-" + slot)));
            }
            CollectedRunRewardPreparedTransferSnapshot source =
                new CollectedRunRewardPreparedTransferSnapshot(
                    19L,
                    records.Reverse<CollectedRunRewardPreparedTransfer>());

            CollectedRunRewardPreparedTransferSnapshot restored =
                RoundTripSnapshot(source);
            CollectedRunRewardPreparedTransferStore authority =
                new CollectedRunRewardPreparedTransferStore(restored);

            Assert.That(restored.Records.Count, Is.EqualTo(6));
            for (int slot = 0; slot < 6; slot++)
            {
                StableId character = Id("character-instance.slot-" + slot);
                IReadOnlyList<CollectedRunRewardPreparedTransfer> recoverable =
                    authority.ExportRecoverable(character);
                Assert.That(recoverable.Count, Is.EqualTo(1));
                Assert.That(recoverable[0].SelectedCharacterStableId,
                    Is.EqualTo(character));
                Assert.That(recoverable[0].CustodyStableId,
                    Is.EqualTo(Id("custody.slot-" + slot)));
            }
        }

        private static CollectedRunRewardPreparedTransferSaveComponent.Codec
            Codec
        {
            get
            {
                return CollectedRunRewardPreparedTransferSaveComponent
                    .Codec.Instance;
            }
        }

        private static CollectedRunRewardPreparedTransfer RoundTrip(
            CollectedRunRewardPreparedTransfer source)
        {
            return RoundTripSnapshot(Snapshot(source)).Records.Single();
        }

        private static CollectedRunRewardPreparedTransferSnapshot
            RoundTripSnapshot(
                CollectedRunRewardPreparedTransferSnapshot source)
        {
            string payload = Codec.Encode(source);
            CollectedRunRewardPreparedTransferSnapshot restored;
            string rejection;
            Assert.That(Codec.TryDecode(payload, out restored, out rejection),
                Is.True,
                rejection);
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Revision, Is.EqualTo(source.Revision));
            Assert.That(restored.Fingerprint, Is.EqualTo(source.Fingerprint));
            Assert.That(Codec.Encode(restored), Is.EqualTo(payload));
            return restored;
        }

        private static CollectedRunRewardPreparedTransferSnapshot Snapshot(
            CollectedRunRewardPreparedTransfer record)
        {
            return new CollectedRunRewardPreparedTransferSnapshot(
                1L,
                new[] { record });
        }

        private static CollectedRunRewardPreparedTransfer CreatePrepared(
            string suffix,
            StableId character = null)
        {
            CollectedRunRewardPreparedTransfer awaiting =
                CreateAwaiting(suffix, false, character);
            return awaiting.AcceptEnd(
                Id("operation.transfer-" + suffix),
                Id("mission-result." + suffix),
                Fingerprint("mission-result-" + suffix),
                Fingerprint("batch-" + suffix),
                Fingerprint("plan-" + suffix));
        }

        private static CollectedRunRewardPreparedTransfer CreateAwaiting(
            string suffix,
            bool reverse = false,
            StableId character = null)
        {
            StableId run = Id("run-instance." + suffix);
            StableId equipmentId = Id("equipment-instance." + suffix);
            StableId equipmentDefinition =
                Id("equipment-definition." + suffix);
            StableId strongboxId = Id("strongbox-instance." + suffix);
            StableId strongboxTier = Id("strongbox-tier." + suffix);
            EquipmentInstance equipment = EquipmentInstance.Create(
                equipmentId,
                equipmentDefinition,
                17,
                Id("quality.epic"),
                new[]
                {
                    AugmentInstance.Create(
                        Id("augment-instance." + suffix + "-a"),
                        Id("augment-definition.damage"),
                        3,
                        12),
                    AugmentInstance.Create(
                        Id("augment-instance." + suffix + "-b"),
                        Id("augment-definition.cooldown"),
                        2,
                        9),
                });
            ProgressionContext progression = ProgressionContext.Create(
                17,
                14,
                Id("difficulty.veteran"),
                3,
                reverse
                    ? new[]
                    {
                        Id("progression-tag.event"),
                        Id("progression-tag.campaign"),
                    }
                    : new[]
                    {
                        Id("progression-tag.campaign"),
                        Id("progression-tag.event"),
                    });
            StrongboxInstanceContext strongbox =
                StrongboxInstanceContext.Create(
                    strongboxId,
                    strongboxTier,
                    0xA11CEUL,
                    2,
                    progression,
                    Id("source-context." + suffix),
                    Id("collection-provenance." + suffix),
                    Fingerprint("strongbox-algorithm-" + suffix));
            CollectedRunRewardTransferItem equipmentReward = Reward(
                equipmentId,
                RewardGrantKind.EquipmentReference,
                equipmentDefinition,
                run,
                1L,
                1L,
                suffix + "-equipment");
            CollectedRunRewardTransferItem strongboxReward = Reward(
                strongboxId,
                RewardGrantKind.Strongbox,
                strongboxTier,
                run,
                1L,
                2L,
                suffix + "-strongbox");
            Dictionary<string, string> authorities =
                new Dictionary<string, string>(StringComparer.Ordinal);
            if (reverse)
            {
                authorities.Add("strongboxes", Fingerprint("boxes-" + suffix));
                authorities.Add("holdings", Fingerprint("holdings-" + suffix));
                authorities.Add("money", Fingerprint("money-" + suffix));
            }
            else
            {
                authorities.Add("money", Fingerprint("money-" + suffix));
                authorities.Add("holdings", Fingerprint("holdings-" + suffix));
                authorities.Add("strongboxes", Fingerprint("boxes-" + suffix));
            }
            IEnumerable<CollectedRunRewardTransferItem> rewards = reverse
                ? new[] { strongboxReward, equipmentReward }
                : new[] { equipmentReward, strongboxReward };

            return CollectedRunRewardPreparedTransfer.AwaitingAcceptedEnd(
                Id("custody." + suffix),
                Id("operation.prepare-" + suffix),
                run,
                1L,
                character ?? Id("character-instance." + suffix),
                8L,
                Fingerprint("character-" + suffix),
                Id("operation.end-" + suffix),
                Fingerprint("end-command-" + suffix),
                0xB0BUL,
                2,
                progression,
                Fingerprint("event-modifier-" + suffix),
                11L,
                12L,
                13L,
                authorities,
                rewards,
                new[] { equipment },
                new[] { strongbox });
        }

        private static CollectedRunRewardTransferItem Reward(
            StableId rewardInstance,
            RewardGrantKind kind,
            StableId content,
            StableId run,
            long lifecycle,
            long collectionOrder,
            string suffix)
        {
            return new CollectedRunRewardTransferItem(
                rewardInstance,
                kind,
                content,
                1L,
                Id("pickup." + suffix),
                Id("grant." + suffix),
                Id("operation.drop-" + suffix),
                Id("terminal-event." + suffix),
                null,
                run,
                lifecycle,
                Id("source-entity." + suffix),
                Id("source-placement." + suffix),
                1L,
                Id("source-definition." + suffix),
                Id("participant." + suffix),
                Fingerprint("generated-batch-" + suffix),
                Fingerprint("generated-reward-" + suffix),
                Id("room." + suffix),
                4.5d,
                -2.25d,
                Fingerprint("world-spawn-" + suffix),
                Fingerprint("available-pickup-" + suffix),
                Id("collector-entity." + suffix),
                Id("collector-participant." + suffix),
                Id("operation.collect-" + suffix),
                collectionOrder,
                100L + collectionOrder,
                Fingerprint("collected-reward-" + suffix));
        }

        private static string Fingerprint(string material)
        {
            return Strongbox.Fingerprint(material);
        }

        private static StableId Id(string canonical)
        {
            return StableId.Parse(canonical);
        }
    }
}
