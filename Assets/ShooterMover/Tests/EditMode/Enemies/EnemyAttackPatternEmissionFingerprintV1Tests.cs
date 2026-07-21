using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class EnemyAttackPatternEmissionFingerprintV1Tests
    {
        [Test]
        public void FingerprintAndReplayConflict_ChangeForPayloadSpreadAndCommittedTarget()
        {
            EnemyRuntimeIdentityV1 identity = Identity();
            EnemyAttackExecutionRequestV1 baselineExecution = Execution(
                identity,
                Descriptor(12d, 30d),
                Id("entity", "player-a"),
                new EnemyVector2(1d, 0d),
                new EnemyVector2(8d, 2d));
            EnemyAttackExecutionRequestV1 changedPayloadExecution = Execution(
                identity,
                Descriptor(18d, 30d),
                Id("entity", "player-a"),
                new EnemyVector2(1d, 0d),
                new EnemyVector2(8d, 2d));
            EnemyAttackExecutionRequestV1 changedSpreadExecution = Execution(
                identity,
                Descriptor(12d, 60d),
                Id("entity", "player-a"),
                new EnemyVector2(1d, 0d),
                new EnemyVector2(8d, 2d));
            EnemyAttackExecutionRequestV1 changedTargetExecution = Execution(
                identity,
                Descriptor(12d, 30d),
                Id("entity", "player-b"),
                new EnemyVector2(0d, 1d),
                new EnemyVector2(2d, 9d));

            EnemyAttackEffectEmissionV1 baseline =
                FirstEmission(baselineExecution);
            EnemyAttackEffectEmissionV1 changedPayload =
                FirstEmission(changedPayloadExecution);
            EnemyAttackEffectEmissionV1 changedSpread =
                FirstEmission(changedSpreadExecution);
            EnemyAttackEffectEmissionV1 changedTarget =
                FirstEmission(changedTargetExecution);

            Assert.That(changedPayload.EmissionStableId,
                Is.EqualTo(baseline.EmissionStableId));
            Assert.That(changedSpread.EmissionStableId,
                Is.EqualTo(baseline.EmissionStableId));
            Assert.That(changedTarget.EmissionStableId,
                Is.EqualTo(baseline.EmissionStableId));

            Assert.That(changedPayload.Fingerprint,
                Is.Not.EqualTo(baseline.Fingerprint));
            Assert.That(changedSpread.Fingerprint,
                Is.Not.EqualTo(baseline.Fingerprint));
            Assert.That(changedTarget.Fingerprint,
                Is.Not.EqualTo(baseline.Fingerprint));

            Assert.That(changedPayload.SequenceFingerprint,
                Is.Not.EqualTo(baseline.SequenceFingerprint));
            Assert.That(changedSpread.Projectile.SpreadOffsetDegrees,
                Is.Not.EqualTo(baseline.Projectile.SpreadOffsetDegrees));
            Assert.That(changedTarget.CommittedIntent.TargetEntityId,
                Is.Not.EqualTo(baseline.CommittedIntent.TargetEntityId));

            var authority = new EnemyAttackPatternAuthorityV1(identity, 4L);
            Assert.That(authority.Start(baselineExecution).IsAccepted, Is.True);
            AssertConflict(authority.Start(changedPayloadExecution));
            AssertConflict(authority.Start(changedSpreadExecution));
            AssertConflict(authority.Start(changedTargetExecution));
        }

        private static void AssertConflict(
            EnemyAttackPatternStartResultV1 result)
        {
            Assert.That(result.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatusV1.Rejected));
            Assert.That(result.Rejection,
                Is.EqualTo(
                    EnemyAttackPatternRejectionCodeV1.ConflictingDuplicate));
            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Sequence, Is.Null);
        }

        private static EnemyAttackEffectEmissionV1 FirstEmission(
            EnemyAttackExecutionRequestV1 execution)
        {
            EnemyAttackSequenceV1 sequence =
                EnemyAttackPatternSchedulerV1.Schedule(execution);
            return EnemyAttackEffectEmissionProjectorV1.Project(
                execution,
                sequence)[0];
        }

        private static EnemyAttackExecutionRequestV1 Execution(
            EnemyRuntimeIdentityV1 identity,
            EnemyAttackCapabilityDescriptorV1 descriptor,
            StableId targetEntityId,
            EnemyVector2 direction,
            EnemyVector2 targetPoint)
        {
            var intent = new EnemyAttackIntent(
                identity.EntityInstanceId,
                identity.RunParticipantId,
                targetEntityId,
                descriptor.AttackId,
                new EnemyVector2(1d, 2d),
                direction,
                targetPoint,
                Id("enemy-decision", "fingerprint"),
                Id("enemy-phase", "ready"),
                Id("enemy-decision-reason", "attack-ready"));
            return new EnemyAttackExecutionRequestV1(
                Id("enemy-operation", "fingerprint-shared"),
                identity,
                4L,
                10d,
                descriptor,
                intent,
                Id("equipment-instance", "fingerprint"),
                EnemyAttackExecutionKindV1.Projectile,
                descriptor.Damage,
                descriptor.CooldownSeconds);
        }

        private static EnemyAttackCapabilityDescriptorV1 Descriptor(
            double projectileSpeed,
            double spreadDegrees)
        {
            return new EnemyAttackCapabilityDescriptorV1(
                Id("enemy-attack-profile", "fingerprint"),
                Id("enemy-attack", "ranged-projectile"),
                10,
                120d,
                0d,
                5d,
                12d,
                3d,
                Id("damage", "kinetic"),
                new EnemyShootingPatternV1(
                    1,
                    0d,
                    3,
                    spreadDegrees,
                    EnemySequenceAimPolicyV1.LockAtSequenceStart,
                    0d,
                    1d,
                    EnemyAttackInterruptionPolicyV1
                        .CancelPendingOnLifecycleEnd),
                new EnemyProjectilePayloadV1(
                    Id("projectile", "fingerprint"),
                    projectileSpeed,
                    20d,
                    0.15d,
                    0,
                    null),
                null);
        }

        private static EnemyRuntimeIdentityV1 Identity()
        {
            return new EnemyRuntimeIdentityV1(
                Id("enemy-entity", "fingerprint"),
                Id("run-participant", "enemy-fingerprint"),
                Id("run", "fingerprint"),
                Id("room-runtime", "fingerprint"),
                Id("room", "fingerprint"),
                Id("room-placement", "fingerprint"));
        }

        private static StableId Id(string scope, string value)
        {
            return StableId.Create(scope, value);
        }
    }
}
