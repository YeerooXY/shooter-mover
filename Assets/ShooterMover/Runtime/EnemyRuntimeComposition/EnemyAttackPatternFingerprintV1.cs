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
    internal static class EnemyAttackPatternFingerprintV1
    {
        public static string Execution(EnemyAttackExecutionRequestV1 execution)
        {
            if (execution == null) return Hash("enemy-attack-pattern-execution-v1|null");
            var builder = new StringBuilder("enemy-attack-pattern-execution-v1");
            AppendId(builder, "operation", execution.OperationStableId);
            AppendIdentity(builder, execution.Identity);
            AppendLong(builder, "lifecycle", execution.LifecycleGeneration);
            AppendNumber(builder, "occurred", execution.OccurredAtSeconds);
            AppendDescriptor(builder, execution.Descriptor);
            AppendIntent(builder, execution.CommittedIntent);
            AppendId(builder, "item", execution.ItemInstanceStableId);
            AppendInt(builder, "kind", (int)execution.ExecutionKind);
            AppendNumber(builder, "damage", execution.ResolvedDamage);
            AppendNumber(builder, "cooldown", execution.ResolvedCooldownSeconds);
            return Hash(builder.ToString());
        }

        public static string Sequence(EnemyAttackSequenceV1 sequence)
        {
            var builder = new StringBuilder("enemy-attack-sequence-v1");
            EnemyAttackSequenceIdentityV1 identity = sequence.Identity;
            AppendId(builder, "sequence", identity.SequenceStableId);
            AppendId(builder, "operation", identity.OperationStableId);
            AppendId(builder, "source", identity.SourceEntityStableId);
            AppendId(builder, "participant", identity.SourceRunParticipantStableId);
            AppendLong(builder, "lifecycle", identity.SourceLifecycleGeneration);
            AppendId(builder, "attack", identity.AttackStableId);
            AppendNumber(builder, "started", sequence.StartedAtSeconds);
            AppendNumber(builder, "recovery-end", sequence.RecoveryEndsAtSeconds);
            AppendDescriptor(builder, sequence.Descriptor);
            AppendIntent(builder, sequence.CommittedIntent);
            for (int index = 0; index < sequence.Shots.Count; index++)
            {
                EnemyAttackScheduledShotV1 shot = sequence.Shots[index];
                AppendId(builder, "shot-id", shot.ShotStableId);
                AppendInt(builder, "shot-ordinal", shot.ShotOrdinal);
                AppendNumber(builder, "shot-time", shot.ScheduledAtSeconds);
                AppendInt(builder, "shot-aim", (int)shot.AimPolicy);
            }
            for (int index = 0; index < sequence.Projectiles.Count; index++)
            {
                EnemyAttackScheduledProjectileV1 projectile = sequence.Projectiles[index];
                AppendId(builder, "projectile-id", projectile.ProjectileStableId);
                AppendId(builder, "projectile-shot", projectile.ShotStableId);
                AppendInt(builder, "projectile-ordinal", projectile.ProjectileOrdinal);
                AppendNumber(builder, "projectile-time", projectile.ScheduledAtSeconds);
                AppendNumber(builder, "projectile-spread", projectile.SpreadOffsetDegrees);
            }
            for (int index = 0; index < sequence.MeleeStrikes.Count; index++)
            {
                EnemyAttackScheduledMeleeStrikeV1 strike = sequence.MeleeStrikes[index];
                AppendId(builder, "strike-id", strike.StrikeStableId);
                AppendInt(builder, "strike-ordinal", strike.StrikeOrdinal);
                AppendNumber(builder, "strike-from", strike.ActiveFromSeconds);
                AppendNumber(builder, "strike-until", strike.ActiveUntilSeconds);
            }
            return Hash(builder.ToString());
        }

        public static string CancellationCommand(EnemyAttackLifecycleCancellationCommandV1 command)
        {
            if (command == null) return Hash("enemy-attack-cancellation-command-v1|null");
            var builder = new StringBuilder("enemy-attack-cancellation-command-v1");
            AppendId(builder, "operation", command.OperationStableId);
            AppendId(builder, "source", command.SourceEntityStableId);
            AppendLong(builder, "lifecycle", command.SourceLifecycleGeneration);
            AppendNumber(builder, "occurred", command.OccurredAtSeconds);
            return Hash(builder.ToString());
        }

        public static string Cancellation(EnemyAttackSequenceCancellationFactV1 fact)
        {
            var builder = new StringBuilder("enemy-attack-cancellation-fact-v1");
            AppendId(builder, "cancellation", fact.CancellationStableId);
            AppendId(builder, "source", fact.SourceEntityStableId);
            AppendLong(builder, "lifecycle", fact.SourceLifecycleGeneration);
            AppendNumber(builder, "occurred", fact.OccurredAtSeconds);
            AppendIds(builder, "shot", fact.CancelledShotStableIds);
            AppendIds(builder, "projectile", fact.CancelledProjectileStableIds);
            AppendIds(builder, "strike", fact.CancelledMeleeStrikeStableIds);
            return Hash(builder.ToString());
        }

        private static void AppendDescriptor(
            StringBuilder builder,
            EnemyAttackCapabilityDescriptorV1 descriptor)
        {
            if (descriptor == null)
            {
                AppendText(builder, "descriptor", null);
                return;
            }
            AppendId(builder, "attack", descriptor.AttackId);
            AppendId(builder, "capability", descriptor.CapabilityId);
            AppendInt(builder, "priority", descriptor.SelectionPriority);
            AppendNumber(builder, "arc", descriptor.AttackArcDegrees);
            AppendNumber(builder, "min", descriptor.MinimumAttackRange);
            AppendNumber(builder, "preferred", descriptor.PreferredAttackRange);
            AppendNumber(builder, "max", descriptor.MaximumAttackRange);
            AppendNumber(builder, "damage", descriptor.Damage);
            AppendId(builder, "channel", descriptor.DamageChannelId);
            EnemyShootingPatternV1 shooting = descriptor.ShootingPattern;
            if (shooting == null)
            {
                AppendText(builder, "shooting", null);
            }
            else
            {
                AppendInt(builder, "shots", shooting.ShotsPerSequence);
                AppendNumber(builder, "shot-interval", shooting.IntervalBetweenShotsSeconds);
                AppendInt(builder, "projectiles-per-shot", shooting.ProjectilesPerShot);
                AppendNumber(builder, "spread", shooting.PerShotSpreadDegrees);
                AppendInt(builder, "sequence-aim", (int)shooting.SequenceAimPolicy);
                AppendNumber(builder, "shooting-windup", shooting.WindUpSeconds);
                AppendNumber(builder, "shooting-recovery", shooting.PostSequenceRecoverySeconds);
                AppendInt(builder, "shooting-interruption", (int)shooting.InterruptionPolicy);
            }
            EnemyProjectilePayloadV1 payload = descriptor.ProjectilePayload;
            if (payload == null)
            {
                AppendText(builder, "payload", null);
            }
            else
            {
                AppendId(builder, "projectile-profile", payload.ProjectileProfileId);
                AppendNumber(builder, "projectile-speed", payload.Speed);
                AppendNumber(builder, "projectile-distance", payload.MaximumTravelDistance);
                AppendNumber(builder, "projectile-radius", payload.CollisionRadius);
                AppendInt(builder, "projectile-pierce", payload.PierceCount);
                EnemyAreaPayloadV1 area = payload.AreaPayload;
                if (area == null)
                {
                    AppendText(builder, "area", null);
                }
                else
                {
                    AppendNumber(builder, "area-radius", area.Radius);
                    AppendNumber(builder, "area-duration", area.DurationSeconds);
                    AppendInt(builder, "area-targets", area.MaximumTargets);
                }
            }
            EnemyMeleePatternV1 melee = descriptor.MeleePattern;
            if (melee == null)
            {
                AppendText(builder, "melee", null);
            }
            else
            {
                AppendNumber(builder, "melee-windup", melee.WindUpSeconds);
                AppendNumber(builder, "melee-active", melee.ActiveWindowSeconds);
                AppendInt(builder, "melee-strikes", melee.StrikeCount);
                AppendNumber(builder, "melee-interval", melee.IntervalBetweenStrikesSeconds);
                AppendNumber(builder, "melee-radius", melee.ContactRadius);
                AppendNumber(builder, "melee-lunge", melee.LungeDistance);
                AppendInt(builder, "melee-aim", (int)melee.AimCommitPolicy);
                AppendNumber(builder, "melee-recovery", melee.RecoverySeconds);
                AppendInt(builder, "melee-hits-per-target", melee.HitsPerTarget);
                AppendInt(builder, "melee-terminal", (int)melee.TerminalOnImpactPolicy);
                AppendInt(builder, "melee-interruption", (int)melee.InterruptionPolicy);
            }
        }

        private static void AppendIntent(StringBuilder builder, EnemyAttackIntent intent)
        {
            if (intent == null)
            {
                AppendText(builder, "intent", null);
                return;
            }
            AppendId(builder, "intent-source", intent.AttackerEntityId);
            AppendId(builder, "intent-participant", intent.SourceRunParticipantId);
            AppendId(builder, "intent-target", intent.TargetEntityId);
            AppendId(builder, "intent-attack", intent.AttackId);
            AppendVector(builder, "intent-origin", intent.CommittedOrigin);
            AppendVector(builder, "intent-direction", intent.CommittedDirection);
            AppendVector(builder, "intent-point", intent.CommittedTargetPoint);
            AppendId(builder, "intent-decision", intent.DecisionId);
            AppendId(builder, "intent-phase", intent.BehaviorPhaseId);
            AppendId(builder, "intent-reason", intent.ReasonCode);
        }

        private static void AppendIdentity(StringBuilder builder, EnemyRuntimeIdentityV1 identity)
        {
            if (identity == null)
            {
                AppendText(builder, "identity", null);
                return;
            }
            AppendId(builder, "identity-entity", identity.EntityInstanceId);
            AppendId(builder, "identity-participant", identity.RunParticipantId);
            AppendId(builder, "identity-run", identity.RunStableId);
            AppendId(builder, "identity-room-runtime", identity.RoomRuntimeInstanceStableId);
            AppendId(builder, "identity-room", identity.RoomStableId);
            AppendId(builder, "identity-placement", identity.PlacementStableId);
        }

        private static void AppendIds(
            StringBuilder builder,
            string name,
            IReadOnlyList<StableId> values)
        {
            for (int index = 0; index < values.Count; index++)
                AppendId(builder, name, values[index]);
        }

        private static void AppendVector(StringBuilder builder, string name, EnemyVector2 value)
        {
            builder.Append('|').Append(name).Append('|')
                .Append(value.X.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(value.Y.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendId(StringBuilder builder, string name, StableId value)
        {
            AppendText(builder, name, value == null ? null : value.ToString());
        }

        private static void AppendNumber(StringBuilder builder, string name, double value)
        {
            AppendText(builder, name, value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendLong(StringBuilder builder, string name, long value)
        {
            AppendText(builder, name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendInt(StringBuilder builder, string name, int value)
        {
            AppendText(builder, name, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendText(StringBuilder builder, string name, string value)
        {
            builder.Append('|').Append(name).Append('|');
            if (value == null)
            {
                builder.Append('-');
                return;
            }
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(value);
        }

        private static string Hash(string canonical)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var result = new StringBuilder(bytes.Length * 2);
                for (int index = 0; index < bytes.Length; index++)
                    result.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
    }
}
