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
        public const int SupportedSchemaVersion = 1;

        private readonly ReadOnlyCollection<EnemyDefinitionV1> definitions;
        private readonly Dictionary<StableId, EnemyDefinitionV1> definitionsById;

        public EnemyCatalogV1(
            int schemaVersion,
            StableId contentVersion,
            IEnumerable<EnemyDefinitionV1> definitions)
        {
            if (contentVersion == null)
            {
                throw new ArgumentNullException(nameof(contentVersion));
            }
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var ordered = new List<EnemyDefinitionV1>();
            foreach (EnemyDefinitionV1 definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Enemy catalogs cannot contain null definitions.",
                        nameof(definitions));
                }
                ordered.Add(definition);
            }
            ordered.Sort(CompareDefinitions);

            definitionsById = new Dictionary<StableId, EnemyDefinitionV1>();
            for (int index = 0; index < ordered.Count; index++)
            {
                EnemyDefinitionV1 definition = ordered[index];
                if (definitionsById.ContainsKey(definition.DefinitionId))
                {
                    throw new ArgumentException(
                        "Enemy definition is duplicated: " + definition.DefinitionId,
                        nameof(definitions));
                }
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

        public IReadOnlyList<EnemyDefinitionV1> Definitions
        {
            get { return definitions; }
        }

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
            {
                throw new KeyNotFoundException(
                    "Enemy definition is not present in the catalog: " + definitionId);
            }
            return definition;
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
            if (contentVersion == null)
            {
                throw new ArgumentNullException(nameof(contentVersion));
            }
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var ordered = new List<EnemyDefinitionV1>();
            foreach (EnemyDefinitionV1 definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Fingerprint input cannot contain null definitions.",
                        nameof(definitions));
                }
                ordered.Add(definition);
            }
            ordered.Sort((left, right) => left.DefinitionId.CompareTo(right.DefinitionId));

            var builder = new StringBuilder();
            builder.Append("enemy-catalog-v1|schema|")
                .Append(schemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append("|content|")
                .Append(contentVersion);
            for (int index = 0; index < ordered.Count; index++)
            {
                AppendDefinition(builder, ordered[index]);
            }
            return Hash(builder.ToString());
        }

        public static string BuildDefinition(EnemyDefinitionV1 definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var builder = new StringBuilder("enemy-definition-v1");
            AppendDefinition(builder, definition);
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
                .Append("|attack-geometry|")
                .Append(Number(definition.AttackArcDegrees))
                .Append('|')
                .Append(Number(definition.MinimumAttackRange))
                .Append('|')
                .Append(Number(definition.PreferredAttackRange))
                .Append('|')
                .Append(Number(definition.MaximumAttackRange))
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
            EnemyAttackCapabilityDescriptorV1 attack)
        {
            builder.Append("|attack|")
                .Append(Id(attack == null ? null : attack.AttackId))
                .Append('|')
                .Append(Id(attack == null ? null : attack.CapabilityId))
                .Append('|')
                .Append(attack == null ? "-" : Number(attack.CooldownSeconds))
                .Append('|')
                .Append(attack == null ? "-" : Number(attack.Damage))
                .Append('|')
                .Append(Id(attack == null ? null : attack.DamageChannelId));

            if (attack == null)
            {
                return;
            }

            EnemyProjectileAttackParametersV1 projectile = attack.Projectile;
            builder.Append("|projectile|")
                .Append(projectile == null ? "-" : Id(projectile.ProjectileProfileId))
                .Append('|')
                .Append(projectile == null
                    ? "-"
                    : projectile.ProjectileCount.ToString(CultureInfo.InvariantCulture))
                .Append('|')
                .Append(projectile == null ? "-" : Number(projectile.ProjectileSpeed))
                .Append('|')
                .Append(projectile == null ? "-" : Number(projectile.MaximumTravelDistance))
                .Append('|')
                .Append(projectile == null ? "-" : Number(projectile.CollisionRadius))
                .Append('|')
                .Append(projectile == null ? "-" : Number(projectile.SpreadDegrees))
                .Append('|')
                .Append(projectile == null
                    ? "-"
                    : projectile.PierceCount.ToString(CultureInfo.InvariantCulture));

            EnemyAreaAttackParametersV1 area = attack.Area;
            builder.Append("|area|")
                .Append(area == null ? "-" : Number(area.Radius))
                .Append('|')
                .Append(area == null ? "-" : Number(area.DurationSeconds))
                .Append('|')
                .Append(area == null
                    ? "-"
                    : area.MaximumTargets.ToString(CultureInfo.InvariantCulture));

            EnemyMeleeAttackParametersV1 melee = attack.Melee;
            builder.Append("|melee|")
                .Append(melee == null ? "-" : Number(melee.ContactRadius))
                .Append('|')
                .Append(melee == null ? "-" : Number(melee.PounceDistance))
                .Append('|')
                .Append(melee == null ? "-" : Number(melee.WindUpSeconds))
                .Append('|')
                .Append(melee == null ? "-" : Number(melee.CommitmentSeconds));
        }

        private static int CompareAttacks(
            EnemyAttackCapabilityDescriptorV1 left,
            EnemyAttackCapabilityDescriptorV1 right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
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
