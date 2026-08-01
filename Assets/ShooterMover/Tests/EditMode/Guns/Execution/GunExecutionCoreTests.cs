using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns.Catalog;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Tests.EditMode.Guns.Execution
{
    public sealed partial class GunExecutionCoreTests
    {
        private static readonly StableId ActorStableId =
            StableId.Parse("actor.test-player");
        private static readonly StableId ParticipantStableId =
            StableId.Parse("participant.local-player");
        private static readonly StableId EquipmentDefinitionStableId =
            StableId.Parse("equipment.test-gun");
        private static readonly StableId QualityStableId =
            StableId.Parse("quality.common");

        [Test]
        public void ExactEquipmentIdentity_IsForwardedIntoEveryEffect()
        {
            GunDefinitionData definition = Definition("shotgun.mk1", 7, 18d, 2d);
            EquipmentInstance equipment = Equipment("equipment-instance.shotgun-a");
            Harness harness = HarnessFor(definition, new[] { equipment });

            GunExecutionResult result = harness.Core.TryExecute(
                Command(equipment, "fire.exact", 0L));

            Assert.That(result.Status, Is.EqualTo(GunExecutionStatus.Accepted));
            Assert.That(harness.Sink.Batches[0].EffectCount, Is.EqualTo(7));
            foreach (IGunEffectDescription effect in harness.Sink.Batches[0].Effects)
            {
                Assert.That(
                    effect.Identity.EquipmentInstanceId.Value,
                    Is.EqualTo(equipment.InstanceId));
                Assert.That(
                    effect.Identity.ParticipantId.Value,
                    Is.EqualTo(ParticipantStableId));
            }
        }

        [Test]
        public void UnknownGunDefinition_FailsClosedWithoutFallback()
        {
            GunDefinitionData definition = Definition("known.mk1", 1, 0d, 5d);
            EquipmentInstance equipment = Equipment("equipment-instance.unknown");
            Harness harness = HarnessFor(
                definition,
                new[] { equipment },
                runtimeReferenceId: StableId.Parse("gun.unknown-mk1"));

            GunExecutionResult result = harness.Core.TryExecute(
                Command(equipment, "fire.unknown", 0L));

            Assert.That(
                result.Status,
                Is.EqualTo(GunExecutionStatus.UnknownGunDefinition));
            Assert.That(harness.Sink.Batches, Is.Empty);
        }

        [Test]
        public void MissingEquippedEquipment_IsRejected()
        {
            GunDefinitionData definition = Definition("rifle.mk1", 1, 0d, 5d);
            EquipmentInstance equipment = Equipment("equipment-instance.missing");
            Harness harness = HarnessFor(definition, new EquipmentInstance[0]);

            GunExecutionResult result = harness.Core.TryExecute(
                Command(equipment, "fire.missing", 0L));

            Assert.That(
                result.Status,
                Is.EqualTo(GunExecutionStatus.MissingEquippedEquipment));
        }

        [Test]
        public void InvalidAim_IsRejected()
        {
            GunDefinitionData definition = Definition("rifle.mk1", 1, 0d, 5d);
            EquipmentInstance equipment = Equipment("equipment-instance.invalid-aim");
            Harness harness = HarnessFor(definition, new[] { equipment });

            GunExecutionResult result = harness.Core.TryExecute(
                Command(
                    equipment,
                    "fire.invalid-aim",
                    0L,
                    aim: new GunVector2(0d, 0d)));

            Assert.That(
                result.Status,
                Is.EqualTo(GunExecutionStatus.InvalidCommand));
            Assert.That(harness.Sink.Batches, Is.Empty);
        }

        [Test]
        public void PreviewOnlyDefinition_FailsClosed()
        {
            GunDefinitionData definition = Definition(
                "preview.mk1",
                1,
                0d,
                5d,
                availability: GunCatalogAvailability.PreviewOnly);
            EquipmentInstance equipment = Equipment("equipment-instance.preview");
            Harness harness = HarnessFor(definition, new[] { equipment });

            GunExecutionResult result = harness.Core.TryExecute(
                Command(equipment, "fire.preview", 0L));

            Assert.That(
                result.Status,
                Is.EqualTo(GunExecutionStatus.PreviewOnlyGunDefinition));
        }

        [Test]
        public void UnsupportedEffect_FailsClosed()
        {
            GunDefinitionData definition = Definition(
                "dot.mk1",
                1,
                0d,
                5d,
                dotDps: 2d);
            EquipmentInstance equipment = Equipment("equipment-instance.dot");
            Harness harness = HarnessFor(definition, new[] { equipment });

            GunExecutionResult result = harness.Core.TryExecute(
                Command(equipment, "fire.dot", 0L));

            Assert.That(
                result.Status,
                Is.EqualTo(GunExecutionStatus.UnsupportedEffects));
        }

        [Test]
        public void InvalidTuning_FailsClosed()
        {
            GunDefinitionData definition = Definition("invalid.mk1", 1, 0d, 0d);
            EquipmentInstance equipment = Equipment("equipment-instance.invalid");
            Harness harness = HarnessFor(definition, new[] { equipment });

            GunExecutionResult result = harness.Core.TryExecute(
                Command(equipment, "fire.invalid", 0L));

            Assert.That(
                result.Status,
                Is.EqualTo(GunExecutionStatus.InvalidTuning));
        }

        [Test]
        public void DeterministicSpread_RejectedRetryBuildsEquivalentBatch()
        {
            GunDefinitionData definition = Definition("shotgun.mk1", 7, 24d, 2d);
            EquipmentInstance equipment = Equipment("equipment-instance.deterministic");
            RecordingSink sink = new RecordingSink { Reject = true };
            Harness harness = HarnessFor(
                definition,
                new[] { equipment },
                sink: sink);
            GunFireCommand command = Command(
                equipment,
                "fire.deterministic",
                0L,
                seed: 987654321UL);

            Assert.That(
                harness.Core.TryExecute(command).Status,
                Is.EqualTo(GunExecutionStatus.SinkRejected));
            Assert.That(
                harness.Core.TryExecute(command).Status,
                Is.EqualTo(GunExecutionStatus.SinkRejected));
            Assert.That(
                sink.Batches[0].Fingerprint,
                Is.EqualTo(sink.Batches[1].Fingerprint));
        }

        [Test]
        public void Shotgun_CountAndShotSequenceVaryDeterministically()
        {
            GunDefinitionData definition = Definition("shotgun.mk1", 7, 24d, 10d);
            EquipmentInstance equipment = Equipment("equipment-instance.sequence");
            Harness harness = HarnessFor(definition, new[] { equipment });

            Assert.That(
                harness.Core.TryExecute(
                    Command(equipment, "fire.sequence-a", 0L, seed: 42UL)).Succeeded,
                Is.True);
            Assert.That(
                harness.Core.TryExecute(
                    Command(equipment, "fire.sequence-b", 10L, seed: 42UL)).Succeeded,
                Is.True);
            Assert.That(harness.Sink.Batches[0].EffectCount, Is.EqualTo(7));
            Assert.That(harness.Sink.Batches[1].Identity.ShotSequence, Is.EqualTo(1L));
            Assert.That(
                harness.Sink.Batches[0].Fingerprint,
                Is.Not.EqualTo(harness.Sink.Batches[1].Fingerprint));
        }

        [Test]
        public void AtomicRejection_AllowsExactRetry()
        {
            GunDefinitionData definition = Definition("shotgun.mk1", 7, 20d, 2d);
            EquipmentInstance equipment = Equipment("equipment-instance.atomic-reject");
            RecordingSink sink = new RecordingSink { Reject = true };
            Harness harness = HarnessFor(
                definition,
                new[] { equipment },
                sink: sink);
            GunFireCommand command = Command(
                equipment,
                "fire.atomic-reject",
                0L);

            Assert.That(
                harness.Core.TryExecute(command).Status,
                Is.EqualTo(GunExecutionStatus.SinkRejected));
            sink.Reject = false;
            Assert.That(
                harness.Core.TryExecute(command).Status,
                Is.EqualTo(GunExecutionStatus.Accepted));
            Assert.That(sink.ValidatedCounts[0], Is.EqualTo(7));
            Assert.That(
                sink.Batches[0].Fingerprint,
                Is.EqualTo(sink.Batches[1].Fingerprint));
        }

        [Test]
        public void ExactReplayAfterAcceptance_DoesNotCallSinkTwice()
        {
            GunDefinitionData definition = Definition("rifle.mk1", 1, 0d, 5d);
            EquipmentInstance equipment = Equipment("equipment-instance.replay");
            Harness harness = HarnessFor(definition, new[] { equipment });
            GunFireCommand command = Command(equipment, "fire.replay", 0L);

            Assert.That(harness.Core.TryExecute(command).Succeeded, Is.True);
            Assert.That(
                harness.Core.TryExecute(command).Status,
                Is.EqualTo(GunExecutionStatus.ReplayAccepted));
            Assert.That(harness.Sink.Batches.Count, Is.EqualTo(1));
        }

        [Test]
        public void AcceptedOperationId_WithChangedCommandFacts_IsConflictingDuplicate()
        {
            GunDefinitionData definition = Definition("rifle.mk1", 1, 0d, 5d);
            EquipmentInstance equipment = Equipment("equipment-instance.conflict");
            Harness harness = HarnessFor(definition, new[] { equipment });
            const string operationId = "fire.conflict";

            Assert.That(
                harness.Core.TryExecute(
                    Command(equipment, operationId, 0L, seed: 10UL)).Succeeded,
                Is.True);

            List<GunFireCommand> conflicts = new List<GunFireCommand>
            {
                Command(equipment, operationId, 1L, seed: 10UL),
                Command(equipment, operationId, 0L, seed: 11UL),
                Command(
                    equipment,
                    operationId,
                    0L,
                    seed: 10UL,
                    aim: new GunVector2(0d, 1d)),
                Command(
                    equipment,
                    operationId,
                    0L,
                    seed: 10UL,
                    origin: new GunVector2(4d, 5d)),
            };

            foreach (GunFireCommand conflict in conflicts)
            {
                Assert.That(
                    harness.Core.TryExecute(conflict).Status,
                    Is.EqualTo(GunExecutionStatus.ConflictingDuplicate));
            }

            Assert.That(harness.Sink.Batches.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConcreteEquipmentInstances_HaveIndependentCooldowns()
        {
            GunDefinitionData definition = Definition("rifle.mk1", 1, 0d, 1d);
            EquipmentInstance first = Equipment("equipment-instance.cooldown-a");
            EquipmentInstance second = Equipment("equipment-instance.cooldown-b");
            Harness harness = HarnessFor(definition, new[] { first, second });

            Assert.That(
                harness.Core.TryExecute(
                    Command(first, "fire.cooldown-a", 0L)).Succeeded,
                Is.True);
            Assert.That(
                harness.Core.TryExecute(
                    Command(second, "fire.cooldown-b", 0L)).Succeeded,
                Is.True);
            Assert.That(
                harness.Core.TryExecute(
                    Command(first, "fire.cooldown-a-2", 0L)).Status,
                Is.EqualTo(GunExecutionStatus.CooldownActive));
        }

        [Test]
        public void NewLifecycleGeneration_ResetsState()
        {
            GunDefinitionData definition = Definition("shotgun.mk1", 3, 15d, 1d);
            EquipmentInstance equipment = Equipment("equipment-instance.lifecycle");
            Harness harness = HarnessFor(definition, new[] { equipment });

            Assert.That(
                harness.Core.TryExecute(
                    Command(equipment, "fire.lifecycle", 0L, generation: 0L)).Succeeded,
                Is.True);
            Assert.That(
                harness.Core.TryExecute(
                    Command(equipment, "fire.lifecycle", 0L, generation: 1L)).Succeeded,
                Is.True);
            Assert.That(harness.Sink.Batches[0].Identity.ShotSequence, Is.EqualTo(0L));
            Assert.That(harness.Sink.Batches[1].Identity.ShotSequence, Is.EqualTo(0L));
        }

        [Test]
        public void ExplosiveProfile_ProducesExplosiveDescription()
        {
            GunDefinitionData definition = Definition(
                "rocket.mk1",
                1,
                0d,
                1d,
                areaDamage: 12d,
                explosionRadius: 3d);
            EquipmentInstance equipment = Equipment("equipment-instance.rocket");
            Harness harness = HarnessFor(definition, new[] { equipment });

            Assert.That(
                harness.Core.TryExecute(
                    Command(equipment, "fire.rocket", 0L)).Succeeded,
                Is.True);
            Assert.That(
                harness.Sink.Batches[0].Effects[0],
                Is.TypeOf<ExplosiveProjectileEffect>());
        }

        [Test]
        public void ChainProfile_ProducesChainRequest()
        {
            GunDefinitionData definition = Definition(
                "arc.mk1",
                1,
                0d,
                2d,
                chainTargets: 4,
                chainRange: 8d);
            EquipmentInstance equipment = Equipment("equipment-instance.arc");
            Harness harness = HarnessFor(definition, new[] { equipment });

            Assert.That(
                harness.Core.TryExecute(
                    Command(equipment, "fire.arc", 0L)).Succeeded,
                Is.True);
            ChainArcEffect effect =
                (ChainArcEffect)harness.Sink.Batches[0].Effects[0];
            Assert.That(effect.MaximumTargets, Is.EqualTo(4));
        }

        [Test]
        public void FifthBehavior_RegistersWithoutCoreModification()
        {
            GunDefinitionData definition = Definition(
                "testburst.mk1",
                1,
                0d,
                5d);
            EquipmentInstance equipment = Equipment("equipment-instance.fifth");
            GunBehaviorId behaviorId = new GunBehaviorId(
                StableId.Parse("gun-behavior.test-burst"));
            GunBehaviorRegistry registry = GunBehaviorRegistry.CreateWithBuiltIns();
            registry.Register(new ThreeProjectileTestBehavior(behaviorId));
            Harness harness = HarnessFor(
                definition,
                new[] { equipment },
                selector: new ExactDefinitionSelector("testburst.mk1", behaviorId),
                registry: registry);

            GunExecutionResult result = harness.Core.TryExecute(
                Command(equipment, "fire.fifth", 0L));

            Assert.That(result.EffectCount, Is.EqualTo(3));
        }
    }
}
