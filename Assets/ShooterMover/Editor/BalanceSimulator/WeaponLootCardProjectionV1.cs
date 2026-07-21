using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Editor.BalanceSimulator
{
    /// <summary>
    /// Immutable player-facing projection of one exact generated weapon.
    /// Resolution is deliberately strict:
    /// EquipmentInstance.DefinitionId -> EquipmentDefinition
    /// -> RuntimeWeaponReferenceId -> WeaponDefinitionData.
    /// </summary>
    public sealed class WeaponLootCardProjectionV1
    {
        private readonly string canonicalText;
        private readonly string primaryCardText;

        private WeaponLootCardProjectionV1(
            EquipmentInstance equipment,
            EquipmentDefinition equipmentDefinition,
            WeaponDefinitionData weaponDefinition,
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
            RuntimeWeaponReferenceId =
                equipmentDefinition.RuntimeWeaponReferenceId;
            WeaponDefinitionId = weaponDefinition.DefinitionId;
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
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId EquipmentInstanceId { get; }
        public StableId EquipmentDefinitionId { get; }
        public string EquipmentFingerprint { get; }
        public StableId RuntimeWeaponReferenceId { get; }
        public string WeaponDefinitionId { get; }
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
            WeaponCatalog weaponCatalog,
            out WeaponLootCardProjectionV1 projection,
            out string diagnostic)
        {
            projection = null;
            diagnostic = string.Empty;

            if (equipment == null)
            {
                diagnostic = "weapon-card-equipment-null";
                return false;
            }
            if (equipmentCatalog == null)
            {
                diagnostic = "weapon-card-equipment-catalog-null";
                return false;
            }
            if (weaponCatalog == null)
            {
                diagnostic = "weapon-card-weapon-catalog-null";
                return false;
            }
            if (equipment.Augments == null)
            {
                diagnostic = "weapon-card-augment-collection-null";
                return false;
            }
            if (equipment.Augments.Count != 0)
            {
                diagnostic =
                    "weapon-card-fresh-strongbox-equipment-has-installed-augments:"
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
                    "weapon-card-equipment-definition-unresolved:"
                    + Safe(equipment.DefinitionId);
                return false;
            }
            if (equipmentDefinition.CategoryId
                != EquipmentCategoryIds.Weapon)
            {
                diagnostic =
                    "weapon-card-equipment-definition-is-not-weapon:"
                    + Safe(equipmentDefinition.DefinitionId);
                return false;
            }
            if (equipmentDefinition.RuntimeWeaponReferenceId == null)
            {
                diagnostic =
                    "weapon-card-runtime-weapon-reference-missing:"
                    + Safe(equipmentDefinition.DefinitionId);
                return false;
            }

            WeaponDefinitionData weaponDefinition;
            if (!TryResolveWeaponDefinition(
                    equipmentDefinition.RuntimeWeaponReferenceId,
                    weaponCatalog,
                    out weaponDefinition,
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
                    "weapon-card-quality-label-unresolved:"
                    + Safe(equipment.QualityId)
                    + "@"
                    + Safe(equipmentDefinition.DefinitionId);
                return false;
            }

            if (weaponDefinition.Mark < 1)
            {
                diagnostic =
                    "weapon-card-mark-invalid:"
                    + weaponDefinition.Mark.ToString(
                        CultureInfo.InvariantCulture);
                return false;
            }
            if (weaponDefinition.ProjectilesPerTrigger < 1)
            {
                diagnostic =
                    "weapon-card-projectile-count-invalid:"
                    + weaponDefinition.ProjectilesPerTrigger.ToString(
                        CultureInfo.InvariantCulture);
                return false;
            }
            if (weaponDefinition.FireRate < 0d
                || weaponDefinition.TargetDps < 0d
                || weaponDefinition.DamagePerProjectile < 0d)
            {
                diagnostic = "weapon-card-negative-player-facing-stat";
                return false;
            }
            if (equipmentDefinition.MaximumAugmentSlots < 0)
            {
                diagnostic = "weapon-card-augment-capacity-invalid";
                return false;
            }

            string damage = FormatNumber(
                weaponDefinition.DamagePerProjectile);
            if (weaponDefinition.ProjectilesPerTrigger > 1)
            {
                damage += " × "
                    + weaponDefinition.ProjectilesPerTrigger.ToString(
                        CultureInfo.InvariantCulture);
            }

            projection = new WeaponLootCardProjectionV1(
                equipment,
                equipmentDefinition,
                weaponDefinition,
                ComposeDisplayName(
                    weaponDefinition.DisplayName,
                    weaponDefinition.Mark),
                qualityLabel,
                ComposeTypeLine(
                    weaponDefinition.Archetype,
                    weaponDefinition.DamageType),
                damage,
                FormatNumber(weaponDefinition.FireRate),
                FormatNumber(weaponDefinition.TargetDps),
                weaponDefinition.Pierce > 0
                    ? weaponDefinition.Pierce.ToString(
                        CultureInfo.InvariantCulture)
                    : string.Empty,
                weaponDefinition.ProjectilesPerTrigger > 1
                    ? weaponDefinition.ProjectilesPerTrigger.ToString(
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
            StrongboxCanonicalV1.AppendToken(
                builder,
                "schema",
                "weapon-loot-card-projection-v1");
            StrongboxCanonicalV1.AppendToken(
                builder,
                "equipment_instance_id",
                Safe(EquipmentInstanceId));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "equipment_definition_id",
                Safe(EquipmentDefinitionId));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "equipment_fingerprint",
                EquipmentFingerprint ?? string.Empty);
            StrongboxCanonicalV1.AppendToken(
                builder,
                "runtime_weapon_reference_id",
                Safe(RuntimeWeaponReferenceId));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "weapon_definition_id",
                WeaponDefinitionId);
            StrongboxCanonicalV1.AppendToken(
                builder,
                "item_level",
                ItemLevel.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "quality_id",
                Safe(QualityId));
            StrongboxCanonicalV1.AppendToken(
                builder,
                "primary_card",
                primaryCardText);
            StrongboxCanonicalV1.AppendToken(
                builder,
                "augment_capacity",
                AugmentCapacity.ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }


        private static bool TryResolveWeaponDefinition(
            StableId runtimeWeaponReferenceId,
            WeaponCatalog weaponCatalog,
            out WeaponDefinitionData definition,
            out string diagnostic)
        {
            definition = null;
            diagnostic = string.Empty;
            string runtimeId = runtimeWeaponReferenceId.ToString();

            // Production equipment stores the canonical weapon StableId directly.
            if (weaponCatalog.TryGetDefinition(runtimeId, out definition)
                && definition != null)
            {
                return true;
            }

            // SIM-002 predates that production convention and stores a deterministic
            // runtime-reference projection. Resolve it uniquely instead of bypassing
            // EquipmentDefinition.RuntimeWeaponReferenceId.
            int matches = 0;
            IReadOnlyList<WeaponDefinitionData> candidates =
                weaponCatalog.GetDefinitions(WeaponCatalogContentFilter.All);
            for (int index = 0; index < candidates.Count; index++)
            {
                WeaponDefinitionData candidate = candidates[index];
                StableId projectedReference = StrongboxCanonicalV1.DeriveId(
                    "weaponruntime",
                    candidate.DefinitionId);
                if (projectedReference == runtimeWeaponReferenceId)
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
                ? "weapon-card-runtime-weapon-reference-unresolved:" + runtimeId
                : "weapon-card-runtime-weapon-reference-ambiguous:" + runtimeId;
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
