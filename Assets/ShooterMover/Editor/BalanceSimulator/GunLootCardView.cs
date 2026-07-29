using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Guns.Catalog;

namespace ShooterMover.Editor.BalanceSimulator
{
    /// <summary>
    /// Immutable player-facing projection of one exact generated gun.
    /// Resolution is deliberately strict:
    /// EquipmentInstance.DefinitionId -> EquipmentDefinition
    /// -> RuntimeGunReferenceId -> GunDefinitionData.
    /// </summary>
    public sealed class GunLootCardView
    {
        private readonly string canonicalText;
        private readonly string primaryCardText;

        private GunLootCardView(
            EquipmentInstance equipment,
            EquipmentDefinition equipmentDefinition,
            GunDefinitionData gunDefinition,
            string displayName,
            string qualityLabel,
            string typeLine,
            string damageText,
            string shotsPerSecondText,
            string dpsText,
            string pierceText,
            string projectileCountText,
            string augmentSymbols)
        {
            EquipmentInstanceId = equipment.InstanceId;
            EquipmentDefinitionId = equipment.DefinitionId;
            EquipmentFingerprint = equipment.Fingerprint;
            RuntimeGunReferenceId =
                equipmentDefinition.RuntimeGunReferenceId;
            GunDefinitionId = gunDefinition.DefinitionId;
            ItemLevel = equipment.ItemLevel;
            QualityId = equipment.QualityId;
            DisplayName = displayName;
            QualityLabel = qualityLabel;
            TypeLine = typeLine;
            DamageText = damageText;
            ShotsPerSecondText = shotsPerSecondText;
            DpsText = dpsText;
            PierceText = pierceText;
            ProjectileCountText = projectileCountText;
            AugmentCapacity = equipmentDefinition.MaximumAugmentSlots;
            AugmentSymbols = augmentSymbols;

            primaryCardText = BuildPrimaryCardText();
            canonicalText = BuildCanonicalText();
            Fingerprint = Strongbox.Fingerprint(canonicalText);
        }

        public StableId EquipmentInstanceId { get; }
        public StableId EquipmentDefinitionId { get; }
        public string EquipmentFingerprint { get; }
        public StableId RuntimeGunReferenceId { get; }
        public string GunDefinitionId { get; }
        public int ItemLevel { get; }
        public StableId QualityId { get; }
        public string DisplayName { get; }
        public string QualityLabel { get; }
        public string TypeLine { get; }
        public string DamageText { get; }
        public string ShotsPerSecondText { get; }
        public string DpsText { get; }
        public string PierceText { get; }
        public string ProjectileCountText { get; }
        public int AugmentCapacity { get; }
        public string AugmentSymbols { get; }
        public string Fingerprint { get; }

        public bool ShowsPierce
        {
            get { return !string.IsNullOrEmpty(PierceText); }
        }

        public bool ShowsProjectileCount
        {
            get { return !string.IsNullOrEmpty(ProjectileCountText); }
        }

        public static bool TryCreate(
            EquipmentInstance equipment,
            EquipmentCatalog equipmentCatalog,
            GunCatalog gunCatalog,
            out GunLootCardView projection,
            out string diagnostic)
        {
            projection = null;
            diagnostic = string.Empty;

            if (equipment == null)
            {
                diagnostic = "gun-card-equipment-null";
                return false;
            }
            if (equipmentCatalog == null)
            {
                diagnostic = "gun-card-equipment-catalog-null";
                return false;
            }
            if (gunCatalog == null)
            {
                diagnostic = "gun-card-gun-catalog-null";
                return false;
            }
            if (equipment.Augments == null)
            {
                diagnostic = "gun-card-augment-collection-null";
                return false;
            }
            if (equipment.Augments.Count != 0)
            {
                diagnostic =
                    "gun-card-fresh-strongbox-equipment-has-installed-augments:"
                    + equipment.Augments.Count.ToString(
                        CultureInfo.InvariantCulture);
                return false;
            }

            EquipmentDefinition equipmentDefinition =
                equipmentCatalog.FindEquipmentDefinition(
                    equipment.DefinitionId);
            if (equipmentDefinition == null)
            {
                diagnostic =
                    "gun-card-equipment-definition-unresolved:"
                    + Safe(equipment.DefinitionId);
                return false;
            }
            if (equipmentDefinition.CategoryId
                != EquipmentCategoryIds.Gun)
            {
                diagnostic =
                    "gun-card-equipment-definition-is-not-gun:"
                    + Safe(equipmentDefinition.DefinitionId);
                return false;
            }
            if (equipmentDefinition.RuntimeGunReferenceId == null)
            {
                diagnostic =
                    "gun-card-runtime-gun-reference-missing:"
                    + Safe(equipmentDefinition.DefinitionId);
                return false;
            }

            GunDefinitionData gunDefinition;
            if (!TryResolveGunDefinition(
                    equipmentDefinition.RuntimeGunReferenceId,
                    gunCatalog,
                    out gunDefinition,
                    out diagnostic))
            {
                return false;
            }

            string qualityLabel;
            if (!TryResolveQualityLabel(
                    equipmentDefinition,
                    equipment.QualityId,
                    out qualityLabel))
            {
                diagnostic =
                    "gun-card-quality-label-unresolved:"
                    + Safe(equipment.QualityId)
                    + "@"
                    + Safe(equipmentDefinition.DefinitionId);
                return false;
            }

            if (gunDefinition.Mark < 1)
            {
                diagnostic =
                    "gun-card-mark-invalid:"
                    + gunDefinition.Mark.ToString(
                        CultureInfo.InvariantCulture);
                return false;
            }
            if (gunDefinition.ProjectilesPerTrigger < 1)
            {
                diagnostic =
                    "gun-card-projectile-count-invalid:"
                    + gunDefinition.ProjectilesPerTrigger.ToString(
                        CultureInfo.InvariantCulture);
                return false;
            }
            if (gunDefinition.FireRate < 0d
                || gunDefinition.TargetDps < 0d
                || gunDefinition.DamagePerProjectile < 0d)
            {
                diagnostic = "gun-card-negative-player-facing-stat";
                return false;
            }
            if (equipmentDefinition.MaximumAugmentSlots < 0)
            {
                diagnostic = "gun-card-augment-capacity-invalid";
                return false;
            }

            string damage = FormatNumber(
                gunDefinition.DamagePerProjectile);
            if (gunDefinition.ProjectilesPerTrigger > 1)
            {
                damage += " × "
                    + gunDefinition.ProjectilesPerTrigger.ToString(
                        CultureInfo.InvariantCulture);
            }

            projection = new GunLootCardView(
                equipment,
                equipmentDefinition,
                gunDefinition,
                ComposeDisplayName(
                    gunDefinition.DisplayName,
                    gunDefinition.Mark),
                qualityLabel,
                ComposeTypeLine(
                    gunDefinition.Archetype,
                    gunDefinition.DamageType),
                damage,
                FormatNumber(gunDefinition.FireRate),
                FormatNumber(gunDefinition.TargetDps),
                gunDefinition.Pierce > 0
                    ? gunDefinition.Pierce.ToString(
                        CultureInfo.InvariantCulture)
                    : string.Empty,
                gunDefinition.ProjectilesPerTrigger > 1
                    ? gunDefinition.ProjectilesPerTrigger.ToString(
                        CultureInfo.InvariantCulture)
                    : string.Empty,
                BuildAugmentSymbols(
                    equipmentDefinition.MaximumAugmentSlots));
            return true;
        }

        public string ToPrimaryCardText()
        {
            return primaryCardText;
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        private string BuildPrimaryCardText()
        {
            var builder = new StringBuilder();
            builder.Append(DisplayName).Append('\n');
            builder.Append(QualityLabel.ToUpperInvariant()).Append("\n\n");
            builder.Append(TypeLine).Append("\n\n");
            builder.Append("Damage: ").Append(DamageText).Append('\n');
            builder.Append("Shots/sec: ")
                .Append(ShotsPerSecondText)
                .Append('\n');
            builder.Append("DPS: ").Append(DpsText).Append('\n');
            if (ShowsPierce)
            {
                builder.Append("Pierce: ").Append(PierceText).Append('\n');
            }
            if (ShowsProjectileCount)
            {
                builder.Append("Projectiles: ")
                    .Append(ProjectileCountText)
                    .Append('\n');
            }
            if (AugmentCapacity > 0)
            {
                builder.Append('\n').Append(AugmentSymbols).Append('\n');
            }
            return builder.ToString();
        }

        private string BuildCanonicalText()
        {
            var builder = new StringBuilder();
            Strongbox.AppendToken(
                builder,
                "schema",
                "gun-loot-card-projection-v1");
            Strongbox.AppendToken(
                builder,
                "equipment_instance_id",
                Safe(EquipmentInstanceId));
            Strongbox.AppendToken(
                builder,
                "equipment_definition_id",
                Safe(EquipmentDefinitionId));
            Strongbox.AppendToken(
                builder,
                "equipment_fingerprint",
                EquipmentFingerprint ?? string.Empty);
            Strongbox.AppendToken(
                builder,
                "runtime_gun_reference_id",
                Safe(RuntimeGunReferenceId));
            Strongbox.AppendToken(
                builder,
                "gun_definition_id",
                GunDefinitionId);
            Strongbox.AppendToken(
                builder,
                "item_level",
                ItemLevel.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(
                builder,
                "quality_id",
                Safe(QualityId));
            Strongbox.AppendToken(
                builder,
                "primary_card",
                primaryCardText);
            Strongbox.AppendToken(
                builder,
                "augment_capacity",
                AugmentCapacity.ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static bool TryResolveGunDefinition(
            StableId runtimeGunReferenceId,
            GunCatalog gunCatalog,
            out GunDefinitionData definition,
            out string diagnostic)
        {
            definition = null;
            diagnostic = string.Empty;
            string runtimeId = runtimeGunReferenceId.ToString();

            // Production equipment may store the canonical gun StableId directly.
            // Imported definitions use a deterministic canonical gun.* projection.
            // Count both forms and accept only one unique target.
            int matches = 0;
            GunDefinitionData direct;
            if (gunCatalog.TryGetDefinition(runtimeId, out direct)
                && direct != null)
            {
                matches++;
                definition = direct;
            }

            IReadOnlyList<GunDefinitionData> candidates =
                gunCatalog.GetDefinitions(GunCatalogContentFilter.All);
            for (int index = 0; index < candidates.Count; index++)
            {
                GunDefinitionData candidate = candidates[index];
                StableId projectedReference = Strongbox.DeriveId(
                    "gun",
                    candidate.DefinitionId);
                if (projectedReference == runtimeGunReferenceId
                    && (definition == null
                        || !string.Equals(
                            definition.DefinitionId,
                            candidate.DefinitionId,
                            StringComparison.Ordinal)))
                {
                    matches++;
                    definition = candidate;
                }
            }

            if (matches == 1 && definition != null)
            {
                return true;
            }

            diagnostic = matches == 0
                ? "gun-card-runtime-gun-reference-unresolved:" + runtimeId
                : "gun-card-runtime-gun-reference-ambiguous:" + runtimeId;
            definition = null;
            return false;
        }

        private static bool TryResolveQualityLabel(
            EquipmentDefinition definition,
            StableId qualityId,
            out string label)
        {
            label = null;
            if (definition == null
                || qualityId == null
                || definition.QualityTiers == null)
            {
                return false;
            }

            int matches = 0;
            for (int index = 0;
                index < definition.QualityTiers.Count;
                index++)
            {
                EquipmentQualityTier tier =
                    definition.QualityTiers[index];
                if (tier != null && tier.QualityId == qualityId)
                {
                    matches++;
                    label = tier.Label;
                }
            }

            return matches == 1
                && !string.IsNullOrWhiteSpace(label);
        }

        private static string ComposeDisplayName(
            string displayName,
            int mark)
        {
            string source = (displayName ?? string.Empty).Trim();
            string roman = ToRoman(mark);
            string[] suffixes =
            {
                " MK " + roman,
                " MK" + roman,
                " MK " + mark.ToString(CultureInfo.InvariantCulture),
                " MK" + mark.ToString(CultureInfo.InvariantCulture),
            };

            for (int index = 0; index < suffixes.Length; index++)
            {
                string suffix = suffixes[index];
                if (source.EndsWith(
                        suffix,
                        StringComparison.OrdinalIgnoreCase))
                {
                    source = source.Substring(
                        0,
                        source.Length - suffix.Length).TrimEnd();
                    break;
                }
            }

            return source + " MK " + roman;
        }

        private static string ComposeTypeLine(
            string archetype,
            string damageType)
        {
            string left = (archetype ?? string.Empty).Trim();
            string right = (damageType ?? string.Empty).Trim();
            if (left.Length == 0)
            {
                return right;
            }
            if (right.Length == 0)
            {
                return left;
            }
            return left + " · " + right;
        }

        private static string BuildAugmentSymbols(int capacity)
        {
            if (capacity <= 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (int index = 0; index < capacity; index++)
            {
                if (index > 0)
                {
                    builder.Append(' ');
                }
                builder.Append('◇');
            }
            return builder.ToString();
        }

        private static string FormatNumber(double value)
        {
            double rounded = Math.Round(value);
            if (Math.Abs(value - rounded) < 0.0000001d)
            {
                return rounded.ToString(
                    "N0",
                    CultureInfo.InvariantCulture);
            }
            return value.ToString(
                "#,0.##",
                CultureInfo.InvariantCulture);
        }

        private static string ToRoman(int value)
        {
            if (value < 1 || value > 3999)
            {
                return value.ToString(CultureInfo.InvariantCulture);
            }

            int[] numbers =
            {
                1000, 900, 500, 400, 100, 90, 50, 40,
                10, 9, 5, 4, 1,
            };
            string[] numerals =
            {
                "M", "CM", "D", "CD", "C", "XC", "L", "XL",
                "X", "IX", "V", "IV", "I",
            };
            var builder = new StringBuilder();
            int remaining = value;
            for (int index = 0; index < numbers.Length; index++)
            {
                while (remaining >= numbers[index])
                {
                    builder.Append(numerals[index]);
                    remaining -= numbers[index];
                }
            }
            return builder.ToString();
        }

        private static string Safe(StableId value)
        {
            return value == null ? "null" : value.ToString();
        }
    }
}
