using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Enemies.Catalog
{
    /// <summary>
    /// Compatibility fingerprint utility retained for the public EnemyDefinition.Fingerprint
    /// surface. The catalogue container was removed, but the engine-neutral descriptor model
    /// still exposes deterministic definition and attack fingerprints.
    /// </summary>
    public static class EnemyCatalogFingerprint
    {
        public static string BuildDefinition(EnemyDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var builder = new StringBuilder("enemy-definition-v2");
            AppendDefinition(builder, definition);
            return Hash(builder.ToString());
        }

        public static string BuildAttack(EnemyAttackCapabilityDescriptor attack)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            var builder = new StringBuilder("enemy-attack-pattern-v2");
            AppendAttack(builder, attack);
            return Hash(builder.ToString());
        }

        private static void AppendDefinition(
            StringBuilder builder,
            EnemyDefinition definition)
        {
            builder.Append("|definition|")
                .Append(Id(definition.DefinitionId))
                .Append('|')
                .Append(Id(definition.PresentationId))
                .Append('|')
                .Append(Number(definition.BaseHealth));

            EnemyLevelScalingProfile scaling = definition.LevelScaling;
            builder.Append("|scaling|")
                .Append(scaling == null
                    ? "-"
                    : scaling.BaseLevel.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(scaling == null
                    ? "-"
                    : scaling.MaximumLevel.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(scaling == null ? "-" : Number(scaling.AdditiveHealthPerLevel))
                .Append('|')
                .Append(scaling == null ? "-" : Number(scaling.MultiplicativeHealthPerLevel))
                .Append("|faction|")
                .Append(Id(definition.FactionId))
                .Append("|perception|")
                .Append(Number(definition.DetectionRadius))
                .Append('|')
                .Append(Number(definition.VisionArcDegrees))
                .Append("|movement|")
                .Append(Id(definition.MovementPolicyId))
                .Append("|decision|")
                .Append(Id(definition.DecisionPolicyId))
                .Append("|experience|")
                .Append(Id(definition.ExperienceProfileId))
                .Append("|drop|")
                .Append(Id(definition.DropProfileId))
                .Append("|room-clear|")
                .Append(((int)definition.RoomClearRole).ToString(CultureInfo.InvariantCulture));

            var attacks = new List<EnemyAttackCapabilityDescriptor>();
            for (int index = 0; index < definition.Attacks.Count; index++)
            {
                attacks.Add(definition.Attacks[index]);
            }
            attacks.Sort(CompareAttacks);
            for (int index = 0; index < attacks.Count; index++)
            {
                AppendAttack(builder, attacks[index]);
            }

            var specials = new List<StableId>();
            for (int index = 0; index < definition.SpecialCapabilityIds.Count; index++)
            {
                specials.Add(definition.SpecialCapabilityIds[index]);
            }
            specials.Sort();
            for (int index = 0; index < specials.Count; index++)
            {
                builder.Append("|special|").Append(Id(specials[index]));
            }
        }

        private static void AppendAttack(
            StringBuilder builder,
            EnemyAttackCapabilityDescriptor attack)
        {
            builder.Append("|attack|")
                .Append(Id(attack == null ? null : attack.AttackId))
                .Append('|')
                .Append(Id(attack == null ? null : attack.CapabilityId))
                .Append('|')
                .Append(attack == null
                    ? "-"
                    : attack.SelectionPriority.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(attack == null ? "-" : Number(attack.AttackArcDegrees))
                .Append('|')
                .Append(attack == null ? "-" : Number(attack.MinimumAttackRange))
                .Append('|')
                .Append(attack == null ? "-" : Number(attack.PreferredAttackRange))
                .Append('|')
                .Append(attack == null ? "-" : Number(attack.MaximumAttackRange))
                .Append('|')
                .Append(attack == null ? "-" : Number(attack.Damage))
                .Append('|')
                .Append(Id(attack == null ? null : attack.DamageChannelId));
            if (attack == null) return;

            EnemyShootingPattern shooting = attack.ShootingPattern;
            builder.Append("|shooting|")
                .Append(shooting == null ? "-" : shooting.ShotsPerSequence.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(shooting == null ? "-" : Number(shooting.IntervalBetweenShotsSeconds))
                .Append('|')
                .Append(shooting == null ? "-" : shooting.ProjectilesPerShot.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(shooting == null ? "-" : Number(shooting.PerShotSpreadDegrees))
                .Append('|')
                .Append(shooting == null ? "-" : ((int)shooting.SequenceAimPolicy).ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(shooting == null ? "-" : Number(shooting.WindUpSeconds))
                .Append('|')
                .Append(shooting == null ? "-" : Number(shooting.PostSequenceRecoverySeconds))
                .Append('|')
                .Append(shooting == null ? "-" : ((int)shooting.InterruptionPolicy).ToString(CultureInfo.InvariantCulture));

            EnemyProjectilePayload payload = attack.ProjectilePayload;
            builder.Append("|projectile-payload|")
                .Append(payload == null ? "-" : Id(payload.ProjectileProfileId))
                .Append('|')
                .Append(payload == null ? "-" : Number(payload.Speed))
                .Append('|')
                .Append(payload == null ? "-" : Number(payload.MaximumTravelDistance))
                .Append('|')
                .Append(payload == null ? "-" : Number(payload.CollisionRadius))
                .Append('|')
                .Append(payload == null ? "-" : payload.PierceCount.ToString(CultureInfo.InvariantCulture));

            EnemyAreaPayload area = payload == null ? null : payload.AreaPayload;
            builder.Append("|area-payload|")
                .Append(area == null ? "-" : Number(area.Radius))
                .Append('|')
                .Append(area == null ? "-" : Number(area.DurationSeconds))
                .Append('|')
                .Append(area == null ? "-" : area.MaximumTargets.ToString(CultureInfo.InvariantCulture));

            EnemyMeleePattern melee = attack.MeleePattern;
            builder.Append("|melee-pattern|")
                .Append(melee == null ? "-" : Number(melee.WindUpSeconds))
                .Append('|')
                .Append(melee == null ? "-" : Number(melee.ActiveWindowSeconds))
                .Append('|')
                .Append(melee == null ? "-" : melee.StrikeCount.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(melee == null ? "-" : Number(melee.IntervalBetweenStrikesSeconds))
                .Append('|')
                .Append(melee == null ? "-" : Number(melee.ContactRadius))
                .Append('|')
                .Append(melee == null ? "-" : Number(melee.LungeDistance))
                .Append('|')
                .Append(melee == null ? "-" : ((int)melee.AimCommitPolicy).ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(melee == null ? "-" : Number(melee.RecoverySeconds))
                .Append('|')
                .Append(melee == null ? "-" : melee.HitsPerTarget.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(melee == null ? "-" : ((int)melee.TerminalOnImpactPolicy).ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(melee == null ? "-" : ((int)melee.InterruptionPolicy).ToString(CultureInfo.InvariantCulture));
        }

        internal static int CompareAttacks(
            EnemyAttackCapabilityDescriptor left,
            EnemyAttackCapabilityDescriptor right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            int priority = left.SelectionPriority.CompareTo(right.SelectionPriority);
            if (priority != 0) return priority;
            if (left.AttackId == null) return right.AttackId == null ? 0 : -1;
            return left.AttackId.CompareTo(right.AttackId);
        }

        private static string Id(StableId value)
        {
            return value == null ? "-" : value.ToString();
        }

        private static string Number(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Hash(string canonical)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                var result = new StringBuilder(bytes.Length * 2);
                for (int index = 0; index < bytes.Length; index++)
                {
                    result.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                }
                return result.ToString();
            }
        }
    }
}
