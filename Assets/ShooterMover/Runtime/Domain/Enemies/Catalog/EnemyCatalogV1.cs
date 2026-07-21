using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Enemies.Catalog
{
    public sealed class EnemyCatalogV1
    {
        public const int MinimumSupportedSchemaVersion = 1;
        public const int SupportedSchemaVersion = 2;

        private readonly ReadOnlyCollection<EnemyDefinitionV1> definitions;
        private readonly Dictionary<StableId, EnemyDefinitionV1> definitionsById;

        public EnemyCatalogV1(
            int schemaVersion,
            StableId contentVersion,
            IEnumerable<EnemyDefinitionV1> definitions)
        {
            if (contentVersion == null) throw new ArgumentNullException(nameof(contentVersion));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            var ordered = new List<EnemyDefinitionV1>();
            foreach (EnemyDefinitionV1 definition in definitions)
            {
                if (definition == null)
                    throw new ArgumentException(
                        "Enemy catalogs cannot contain null definitions.",
                        nameof(definitions));
                EnemyDefinitionV1 canonical = CanonicalizeDefinition(definition);
                if (schemaVersion <= 1)
                {
                    for (int attackIndex = 0;
                        attackIndex < canonical.Attacks.Count;
                        attackIndex++)
                    {
                        EnemyAttackDescriptorCompatibilityV1.MarkLegacyCompatibility(
                            canonical.Attacks[attackIndex]);
                    }
                }
                ordered.Add(canonical);
            }
            ordered.Sort(CompareDefinitions);

            definitionsById = new Dictionary<StableId, EnemyDefinitionV1>();
            for (int index = 0; index < ordered.Count; index++)
            {
                EnemyDefinitionV1 definition = ordered[index];
                if (definitionsById.ContainsKey(definition.DefinitionId))
                    throw new ArgumentException(
                        "Enemy definition is duplicated: " + definition.DefinitionId,
                        nameof(definitions));
                definitionsById.Add(definition.DefinitionId, definition);
            }

            SchemaVersion = schemaVersion;
            ContentVersion = contentVersion;
            this.definitions = new ReadOnlyCollection<EnemyDefinitionV1>(ordered);
            Fingerprint = EnemyCatalogFingerprintV1.BuildCatalog(
                schemaVersion,
                contentVersion,
                ordered);
        }

        public int SchemaVersion { get; }
        public StableId ContentVersion { get; }
        public IReadOnlyList<EnemyDefinitionV1> Definitions { get { return definitions; } }
        public string Fingerprint { get; }

        public bool TryGetDefinition(
            StableId definitionId,
            out EnemyDefinitionV1 definition)
        {
            definition = null;
            return definitionId != null
                && definitionsById.TryGetValue(definitionId, out definition)
                && definition != null;
        }

        public EnemyDefinitionV1 GetDefinition(StableId definitionId)
        {
            EnemyDefinitionV1 definition;
            if (!TryGetDefinition(definitionId, out definition))
                throw new KeyNotFoundException(
                    "Enemy definition is not present in the catalog: " + definitionId);
            return definition;
        }

        private static EnemyDefinitionV1 CanonicalizeDefinition(EnemyDefinitionV1 definition)
        {
            // Every catalog owns distinct canonical descriptor instances. Compatibility metadata
            // can therefore never leak when one source definition is reused by schema-v1 and
            // schema-v2 catalogs in the same process.
            var attacks = new List<EnemyAttackCapabilityDescriptorV1>();
            for (int index = 0; index < definition.Attacks.Count; index++)
                attacks.Add(CloneAttack(definition.Attacks[index]));
            attacks.Sort(EnemyCatalogFingerprintV1.CompareAttacks);
            return definition.WithAttacks(attacks);
        }

        private static EnemyAttackCapabilityDescriptorV1 CloneAttack(
            EnemyAttackCapabilityDescriptorV1 attack)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            return new EnemyAttackCapabilityDescriptorV1(
                attack.AttackId,
                attack.CapabilityId,
                attack.SelectionPriority,
                attack.AttackArcDegrees,
                attack.MinimumAttackRange,
                attack.PreferredAttackRange,
                attack.MaximumAttackRange,
                attack.Damage,
                attack.DamageChannelId,
                attack.ShootingPattern,
                attack.ProjectilePayload,
                attack.MeleePattern);
        }

        private static int CompareDefinitions(
            EnemyDefinitionV1 left,
            EnemyDefinitionV1 right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            if (left.DefinitionId == null) return right.DefinitionId == null ? 0 : -1;
            return left.DefinitionId.CompareTo(right.DefinitionId);
        }
    }

    public static class EnemyCatalogFingerprintV1
    {
        public static string BuildCatalog(
            int schemaVersion,
            StableId contentVersion,
            IEnumerable<EnemyDefinitionV1> definitions)
        {
            if (contentVersion == null) throw new ArgumentNullException(nameof(contentVersion));
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            var ordered = new List<EnemyDefinitionV1>();
            foreach (EnemyDefinitionV1 definition in definitions)
            {
                if (definition == null)
                    throw new ArgumentException(
                        "Fingerprint input cannot contain null definitions.",
                        nameof(definitions));
                ordered.Add(definition);
            }
            ordered.Sort((left, right) => left.DefinitionId.CompareTo(right.DefinitionId));

            var builder = new StringBuilder();
            builder.Append("enemy-catalog-v2|schema|")
                .Append(schemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append("|content|")
                .Append(contentVersion);
            for (int index = 0; index < ordered.Count; index++)
                AppendDefinition(builder, ordered[index]);
            return Hash(builder.ToString());
        }

        public static string BuildDefinition(EnemyDefinitionV1 definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            var builder = new StringBuilder("enemy-definition-v2");
            AppendDefinition(builder, definition);
            return Hash(builder.ToString());
        }

        public static string BuildAttack(EnemyAttackCapabilityDescriptorV1 attack)
        {
            if (attack == null) throw new ArgumentNullException(nameof(attack));
            var builder = new StringBuilder("enemy-attack-pattern-v2");
            AppendAttack(builder, attack);
            return Hash(builder.ToString());
        }

        private static void AppendDefinition(
            StringBuilder builder,
            EnemyDefinitionV1 definition)
        {
            builder.Append("|definition|")
                .Append(Id(definition.DefinitionId))
                .Append('|')
                .Append(Id(definition.PresentationId))
                .Append('|')
                .Append(Number(definition.BaseHealth));

            EnemyLevelScalingProfileV1 scaling = definition.LevelScaling;
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

            var attacks = new List<EnemyAttackCapabilityDescriptorV1>();
            for (int index = 0; index < definition.Attacks.Count; index++)
                attacks.Add(definition.Attacks[index]);
            attacks.Sort(CompareAttacks);
            for (int index = 0; index < attacks.Count; index++)
                AppendAttack(builder, attacks[index]);

            var specials = new List<StableId>();
            for (int index = 0; index < definition.SpecialCapabilityIds.Count; index++)
                specials.Add(definition.SpecialCapabilityIds[index]);
            specials.Sort();
            for (int index = 0; index < specials.Count; index++)
                builder.Append("|special|").Append(Id(specials[index]));
        }

        private static void AppendAttack(
            StringBuilder builder,
            EnemyAttackCapabilityDescriptorV1 attack)
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

            EnemyShootingPatternV1 shooting = attack.ShootingPattern;
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

            EnemyProjectilePayloadV1 payload = attack.ProjectilePayload;
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

            EnemyAreaPayloadV1 area = payload == null ? null : payload.AreaPayload;
            builder.Append("|area-payload|")
                .Append(area == null ? "-" : Number(area.Radius))
                .Append('|')
                .Append(area == null ? "-" : Number(area.DurationSeconds))
                .Append('|')
                .Append(area == null ? "-" : area.MaximumTargets.ToString(CultureInfo.InvariantCulture));

            EnemyMeleePatternV1 melee = attack.MeleePattern;
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
            EnemyAttackCapabilityDescriptorV1 left,
            EnemyAttackCapabilityDescriptorV1 right)
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
                    result.Append(bytes[index].ToString("x2", CultureInfo.InvariantCulture));
                return result.ToString();
            }
        }
    }
}
