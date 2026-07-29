using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class EnemyAttackPatternEmissionFingerprintTests
    {
        [Test]
        public void FingerprintAndReplayConflict_ChangeForPayloadSpreadAndCommittedTarget()
        {
            EnemyLiveIdentity identity = Identity();
            EnemyAttackExecutionRequest baselineExecution = Execution(
                identity,
                Descriptor(12d, 30d),
                Id("entity", "player-a"),
                new EnemyVector2(1d, 0d),
                new EnemyVector2(8d, 2d));
            EnemyAttackExecutionRequest changedPayloadExecution = Execution(
                identity,
                Descriptor(18d, 30d),
                Id("entity", "player-a"),
                new EnemyVector2(1d, 0d),
                new EnemyVector2(8d, 2d));
            EnemyAttackExecutionRequest changedSpreadExecution = Execution(
                identity,
                Descriptor(12d, 60d),
                Id("entity", "player-a"),
                new EnemyVector2(1d, 0d),
                new EnemyVector2(8d, 2d));
            EnemyAttackExecutionRequest changedTargetExecution = Execution(
                identity,
                Descriptor(12d, 30d),
                Id("entity", "player-b"),
                new EnemyVector2(0d, 1d),
                new EnemyVector2(2d, 9d));

            EnemyAttackEffectEmission baseline =
                FirstEmission(baselineExecution);
            EnemyAttackEffectEmission changedPayload =
                FirstEmission(changedPayloadExecution);
            EnemyAttackEffectEmission changedSpread =
                FirstEmission(changedSpreadExecution);
            EnemyAttackEffectEmission changedTarget =
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

            var authority = new EnemyAttackPatternState(identity, 4L);
            Assert.That(authority.Start(baselineExecution).IsAccepted, Is.True);
            AssertConflict(authority.Start(changedPayloadExecution));
            AssertConflict(authority.Start(changedSpreadExecution));
            AssertConflict(authority.Start(changedTargetExecution));
        }

        private static void AssertConflict(
            EnemyAttackPatternStartResult result)
        {
            Assert.That(result.Status,
                Is.EqualTo(EnemyAttackPatternOperationStatus.Rejected));
            Assert.That(result.Rejection,
                Is.EqualTo(
                    EnemyAttackPatternRejectionCode.ConflictingDuplicate));
            Assert.That(result.IsAccepted, Is.False);
            Assert.That(result.Sequence, Is.Null);
        }

        private static EnemyAttackEffectEmission FirstEmission(
            EnemyAttackExecutionRequest execution)
        {
            EnemyAttackSequence sequence =
                EnemyAttackPatternScheduler.Schedule(execution);
            return EnemyAttackEffectEmissionProjector.Project(
                execution,
                sequence)[0];
        }

        private static EnemyAttackExecutionRequest Execution(
            EnemyLiveIdentity identity,
            EnemyAttackCapabilityDescriptor descriptor,
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
            return new EnemyAttackExecutionRequest(
                Id("enemy-operation", "fingerprint-shared"),
                identity,
                4L,
                10d,
                descriptor,
                intent,
                Id("equipment-instance", "fingerprint"),
                EnemyAttackExecutionKind.Projectile,
                descriptor.Damage,
                descriptor.CooldownSeconds);
        }

        private static EnemyAttackCapabilityDescriptor Descriptor(
            double projectileSpeed,
            double spreadDegrees)
        {
            return new EnemyAttackCapabilityDescriptor(
                Id("enemy-attack-profile", "fingerprint"),
                Id("enemy-attack", "ranged-projectile"),
                10,
                120d,
                0d,
                5d,
                12d,
                3d,
                Id("damage", "kinetic"),
                new EnemyShootingPattern(
                    1,
                    0d,
                    3,
                    spreadDegrees,
                    EnemySequenceAimPolicy.LockAtSequenceStart,
                    0d,
                    1d,
                    EnemyAttackInterruptionPolicy
                        .CancelPendingOnLifecycleEnd),
                new EnemyProjectilePayload(
                    Id("projectile", "fingerprint"),
                    projectileSpeed,
                    20d,
                    0.15d,
                    0,
                    null),
                null);
        }

        private static EnemyLiveIdentity Identity()
        {
            return new EnemyLiveIdentity(
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
