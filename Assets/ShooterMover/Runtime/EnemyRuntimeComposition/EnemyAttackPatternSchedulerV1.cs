using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.GameplayEntities.Enemies;

namespace ShooterMover.EnemyRuntimeComposition
{
    public static class EnemyAttackPatternSchedulerV1
    {
        public static EnemyAttackSequenceV1 Schedule(EnemyAttackExecutionRequestV1 execution)
        {
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            if (execution.Identity == null) throw new ArgumentException("Execution identity is required.");
            if (execution.Descriptor == null) throw new ArgumentException("Attack descriptor is required.");
            if (execution.CommittedIntent == null) throw new ArgumentException("Committed intent is required.");
            double authoredDuration = execution.Descriptor.CooldownSeconds;
            double timingScale = authoredDuration > 0d
                ? execution.ResolvedCooldownSeconds / authoredDuration
                : 1d;
            if (double.IsNaN(timingScale) || double.IsInfinity(timingScale) || timingScale <= 0d)
                timingScale = 1d;
            return Schedule(
                execution.OperationStableId,
                execution.Identity,
                execution.LifecycleGeneration,
                execution.Descriptor,
                execution.CommittedIntent,
                execution.OccurredAtSeconds,
                timingScale);
        }

        public static EnemyAttackSequenceV1 Schedule(
            StableId operationStableId,
            EnemyRuntimeIdentityV1 identity,
            long lifecycleGeneration,
            EnemyAttackCapabilityDescriptorV1 descriptor,
            EnemyAttackIntent committedIntent,
            double startedAtSeconds,
            double timingScale)
        {
            if (operationStableId == null) throw new ArgumentNullException(nameof(operationStableId));
            if (identity == null) throw new ArgumentNullException(nameof(identity));
            if (lifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
            if (committedIntent == null) throw new ArgumentNullException(nameof(committedIntent));
            if (committedIntent.AttackerEntityId != identity.EntityInstanceId)
                throw new ArgumentException("Committed intent source does not match sequence identity.");
            RequireFiniteNonNegative(startedAtSeconds, nameof(startedAtSeconds));
            if (double.IsNaN(timingScale) || double.IsInfinity(timingScale) || timingScale <= 0d)
                throw new ArgumentOutOfRangeException(nameof(timingScale));

            bool shooting = descriptor.ShootingPattern != null;
            bool melee = descriptor.MeleePattern != null;
            if (shooting == melee)
                throw new ArgumentException("Exactly one shooting_pattern or melee_pattern is required.");
            if (shooting && descriptor.ProjectilePayload == null)
                throw new ArgumentException("Shooting patterns require a projectile_payload.");

            StableId sequenceId = StableId.Create(
                "enemy-attack-sequence",
                "runtime-" + DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                    identity.EntityInstanceId
                    + "|" + lifecycleGeneration.ToString(CultureInfo.InvariantCulture)
                    + "|" + operationStableId
                    + "|" + descriptor.AttackId));
            var sequenceIdentity = new EnemyAttackSequenceIdentityV1(
                sequenceId,
                operationStableId,
                identity.EntityInstanceId,
                identity.RunParticipantId,
                lifecycleGeneration,
                descriptor.AttackId);
            var shots = new List<EnemyAttackScheduledShotV1>();
            var projectiles = new List<EnemyAttackScheduledProjectileV1>();
            var strikes = new List<EnemyAttackScheduledMeleeStrikeV1>();
            double recoveryEndsAt;

            if (shooting)
            {
                EnemyShootingPatternV1 pattern = descriptor.ShootingPattern;
                for (int shotOrdinal = 0; shotOrdinal < pattern.ShotsPerSequence; shotOrdinal++)
                {
                    double scheduled = startedAtSeconds
                        + ((pattern.WindUpSeconds
                            + (shotOrdinal * pattern.IntervalBetweenShotsSeconds)) * timingScale);
                    StableId shotId = ChildId(
                        "enemy-attack-shot",
                        sequenceId,
                        "shot",
                        shotOrdinal);
                    shots.Add(new EnemyAttackScheduledShotV1(
                        shotId,
                        sequenceId,
                        shotOrdinal,
                        scheduled,
                        pattern.SequenceAimPolicy));
                    for (int projectileOrdinal = 0;
                        projectileOrdinal < pattern.ProjectilesPerShot;
                        projectileOrdinal++)
                    {
                        projectiles.Add(new EnemyAttackScheduledProjectileV1(
                            ChildId(
                                "enemy-projectile",
                                shotId,
                                "projectile",
                                projectileOrdinal),
                            shotId,
                            projectileOrdinal,
                            scheduled,
                            SpreadOffset(
                                pattern.PerShotSpreadDegrees,
                                pattern.ProjectilesPerShot,
                                projectileOrdinal),
                            descriptor.ProjectilePayload));
                    }
                }
                recoveryEndsAt = startedAtSeconds + (pattern.TotalDurationSeconds * timingScale);
            }
            else
            {
                EnemyMeleePatternV1 pattern = descriptor.MeleePattern;
                for (int strikeOrdinal = 0; strikeOrdinal < pattern.StrikeCount; strikeOrdinal++)
                {
                    double activeFrom = startedAtSeconds
                        + ((pattern.WindUpSeconds
                            + (strikeOrdinal * pattern.IntervalBetweenStrikesSeconds)) * timingScale);
                    double activeUntil = activeFrom + (pattern.ActiveWindowSeconds * timingScale);
                    strikes.Add(new EnemyAttackScheduledMeleeStrikeV1(
                        ChildId(
                            "enemy-melee-strike",
                            sequenceId,
                            "strike",
                            strikeOrdinal),
                        sequenceId,
                        strikeOrdinal,
                        activeFrom,
                        activeUntil,
                        pattern));
                }
                recoveryEndsAt = startedAtSeconds + (pattern.TotalDurationSeconds * timingScale);
            }

            return new EnemyAttackSequenceV1(
                sequenceIdentity,
                descriptor,
                committedIntent,
                startedAtSeconds,
                recoveryEndsAt,
                shots,
                projectiles,
                strikes);
        }

        private static StableId ChildId(
            string kind,
            StableId parentId,
            string ordinalKind,
            int ordinal)
        {
            return StableId.Create(
                kind,
                "runtime-" + DeterministicEnemyRuntimeIdentityDeriverV1.Hash64(
                    parentId
                    + "|" + ordinalKind
                    + "|" + ordinal.ToString(CultureInfo.InvariantCulture)));
        }

        private static double SpreadOffset(double spreadDegrees, int count, int ordinal)
        {
            if (count <= 1) return 0d;
            return (-spreadDegrees / 2d)
                + ((spreadDegrees * ordinal) / (count - 1d));
        }

        private static void RequireFiniteNonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
