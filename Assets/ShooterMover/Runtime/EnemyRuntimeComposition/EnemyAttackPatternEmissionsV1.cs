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
    public enum EnemyAttackEffectEmissionKindV1
    {
        Projectile = 1,
        MeleeStrike = 2,
    }

    /// <summary>
    /// Canonical live presentation/effect fact for one scheduled projectile or melee strike.
    /// </summary>
    public sealed class EnemyAttackEffectEmissionV1
    {
        internal EnemyAttackEffectEmissionV1(
            StableId emissionStableId,
            StableId sequenceStableId,
            string sequenceFingerprint,
            EnemyAttackEffectEmissionKindV1 kind,
            EnemyAttackExecutionRequestV1 execution,
            double scheduledAtSeconds,
            double activeUntilSeconds,
            EnemyAttackScheduledProjectileV1 projectile,
            EnemyAttackScheduledMeleeStrikeV1 meleeStrike)
        {
            EmissionStableId = emissionStableId
                ?? throw new ArgumentNullException(nameof(emissionStableId));
            SequenceStableId = sequenceStableId
                ?? throw new ArgumentNullException(nameof(sequenceStableId));
            if (string.IsNullOrWhiteSpace(sequenceFingerprint))
                throw new ArgumentException(
                    "A canonical sequence fingerprint is required.",
                    nameof(sequenceFingerprint));
            if (!Enum.IsDefined(typeof(EnemyAttackEffectEmissionKindV1), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            Execution = execution ?? throw new ArgumentNullException(nameof(execution));
            RequireFiniteNonNegative(scheduledAtSeconds, nameof(scheduledAtSeconds));
            RequireFiniteNonNegative(activeUntilSeconds, nameof(activeUntilSeconds));
            if (activeUntilSeconds < scheduledAtSeconds)
                throw new ArgumentOutOfRangeException(nameof(activeUntilSeconds));
            if ((kind == EnemyAttackEffectEmissionKindV1.Projectile) != (projectile != null))
                throw new ArgumentException(
                    "Projectile emissions must carry exactly one projectile fact.");
            if ((kind == EnemyAttackEffectEmissionKindV1.MeleeStrike) != (meleeStrike != null))
                throw new ArgumentException(
                    "Melee emissions must carry exactly one melee-strike fact.");

            SequenceFingerprint = sequenceFingerprint.Trim();
            Kind = kind;
            ScheduledAtSeconds = scheduledAtSeconds;
            ActiveUntilSeconds = activeUntilSeconds;
            Projectile = projectile;
            MeleeStrike = meleeStrike;
            Fingerprint = BuildFingerprint();
        }

        public StableId EmissionStableId { get; }
        public StableId SequenceStableId { get; }
        public string SequenceFingerprint { get; }
        public EnemyAttackEffectEmissionKindV1 Kind { get; }
        public EnemyAttackExecutionRequestV1 Execution { get; }
        public double ScheduledAtSeconds { get; }
        public double ActiveUntilSeconds { get; }
        public EnemyAttackScheduledProjectileV1 Projectile { get; }
        public EnemyAttackScheduledMeleeStrikeV1 MeleeStrike { get; }
        public StableId SourceEntityStableId
        {
            get { return Execution.Identity.EntityInstanceId; }
        }
        public StableId SourceRunParticipantStableId
        {
            get { return Execution.Identity.RunParticipantId; }
        }
        public long SourceLifecycleGeneration
        {
            get { return Execution.LifecycleGeneration; }
        }
        public StableId AttackStableId
        {
            get { return Execution.Descriptor.AttackId; }
        }
        public EnemyAttackIntent CommittedIntent
        {
            get { return Execution.CommittedIntent; }
        }
        public double ResolvedDamage
        {
            get { return Execution.ResolvedDamage; }
        }
        public string Fingerprint { get; }

        private string BuildFingerprint()
        {
            var builder = new StringBuilder("enemy-attack-effect-emission-v1");
            Append(builder, "emission", EmissionStableId);
            Append(builder, "sequence", SequenceStableId);
            Append(builder, "sequence-fingerprint", SequenceFingerprint);
            Append(
                builder,
                "execution-fingerprint",
                EnemyAttackPatternFingerprintV1.Execution(Execution));
            Append(builder, "kind", ((int)Kind).ToString(CultureInfo.InvariantCulture));
            Append(
                builder,
                "scheduled",
                ScheduledAtSeconds.ToString("R", CultureInfo.InvariantCulture));
            Append(
                builder,
                "active-until",
                ActiveUntilSeconds.ToString("R", CultureInfo.InvariantCulture));
            Append(
                builder,
                "projectile",
                Projectile == null ? null : Projectile.ProjectileStableId.ToString());
            Append(
                builder,
                "projectile-spread",
                Projectile == null
                    ? null
                    : Projectile.SpreadOffsetDegrees.ToString(
                        "R",
                        CultureInfo.InvariantCulture));
            Append(
                builder,
                "strike",
                MeleeStrike == null ? null : MeleeStrike.StrikeStableId.ToString());
            return Hash(builder);
        }

        internal static string Hash(StringBuilder builder)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                var result = new StringBuilder(bytes.Length * 2);
                for (int index = 0; index < bytes.Length; index++)
                    result.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }

        internal static void Append(StringBuilder builder, string name, StableId value)
        {
            Append(builder, name, value == null ? null : value.ToString());
        }

        internal static void Append(StringBuilder builder, string name, string value)
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

        private static void RequireFiniteNonNegative(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
                throw new ArgumentOutOfRangeException(name);
        }
    }

    public static class EnemyAttackEffectEmissionProjectorV1
    {
        public static IReadOnlyList<EnemyAttackEffectEmissionV1> Project(
            EnemyAttackExecutionRequestV1 execution,
            EnemyAttackSequenceV1 sequence)
        {
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            if (sequence.Identity.OperationStableId != execution.OperationStableId
                || sequence.Identity.SourceEntityStableId != execution.Identity.EntityInstanceId
                || sequence.Identity.SourceLifecycleGeneration != execution.LifecycleGeneration
                || sequence.Identity.AttackStableId != execution.Descriptor.AttackId)
            {
                throw new ArgumentException(
                    "Scheduled sequence does not match its accepted execution.",
                    nameof(sequence));
            }

            var emissions = new List<EnemyAttackEffectEmissionV1>();
            for (int index = 0; index < sequence.Projectiles.Count; index++)
            {
                EnemyAttackScheduledProjectileV1 projectile = sequence.Projectiles[index];
                emissions.Add(new EnemyAttackEffectEmissionV1(
                    projectile.ProjectileStableId,
                    sequence.Identity.SequenceStableId,
                    sequence.Fingerprint,
                    EnemyAttackEffectEmissionKindV1.Projectile,
                    execution,
                    projectile.ScheduledAtSeconds,
                    projectile.ScheduledAtSeconds,
                    projectile,
                    null));
            }
            for (int index = 0; index < sequence.MeleeStrikes.Count; index++)
            {
                EnemyAttackScheduledMeleeStrikeV1 strike = sequence.MeleeStrikes[index];
                emissions.Add(new EnemyAttackEffectEmissionV1(
                    strike.StrikeStableId,
                    sequence.Identity.SequenceStableId,
                    sequence.Fingerprint,
                    EnemyAttackEffectEmissionKindV1.MeleeStrike,
                    execution,
                    strike.ActiveFromSeconds,
                    strike.ActiveUntilSeconds,
                    null,
                    strike));
            }
            emissions.Sort((left, right) =>
            {
                int time = left.ScheduledAtSeconds.CompareTo(right.ScheduledAtSeconds);
                return time != 0 ? time : left.EmissionStableId.CompareTo(right.EmissionStableId);
            });
            return new ReadOnlyCollection<EnemyAttackEffectEmissionV1>(emissions);
        }
    }
}
