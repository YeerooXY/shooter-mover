
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Weapons.Catalog
{
    public static class WeaponEquipmentQualityIdsV1
    {
        public static readonly StableId Common =
            StableId.Parse("equipment-quality.common");
        public static readonly StableId Rare =
            StableId.Parse("equipment-quality.rare");
        public static readonly StableId Epic =
            StableId.Parse("equipment-quality.epic");
        public static readonly StableId Legendary =
            StableId.Parse("equipment-quality.legendary");
        public static readonly StableId MythicArtifact =
            StableId.Parse("equipment-quality.mythic-artifact");

        public static StableId ForNormalizedRarity(string normalizedRarity)
        {
            switch ((normalizedRarity ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "common": return Common;
                case "rare": return Rare;
                case "epic": return Epic;
                case "legendary": return Legendary;
                case "mythicartifact": return MythicArtifact;
                default:
                    throw new ArgumentException(
                        "The normalized weapon rarity has no canonical equipment quality.",
                        nameof(normalizedRarity));
            }
        }
    }

    public sealed class CanonicalWeaponCatalogEntryV1
    {
        public CanonicalWeaponCatalogEntryV1(
            WeaponDefinitionData weaponDefinition,
            EquipmentDefinition equipmentDefinition,
            string sourceRarity,
            string artReferenceId)
        {
            WeaponDefinition = weaponDefinition
                ?? throw new ArgumentNullException(nameof(weaponDefinition));
            EquipmentDefinition = equipmentDefinition
                ?? throw new ArgumentNullException(nameof(equipmentDefinition));
            SourceRarity = sourceRarity ?? string.Empty;
            ArtReferenceId = artReferenceId ?? string.Empty;
        }

        public WeaponDefinitionData WeaponDefinition { get; }
        public EquipmentDefinition EquipmentDefinition { get; }
        public string SourceRarity { get; }
        public string NormalizedRarity { get { return WeaponDefinition.Rarity; } }
        public StableId QualityId
        {
            get { return EquipmentDefinition.QualityTiers[0].QualityId; }
        }
        public string ArtReferenceId { get; }
    }

    public interface IWeaponCatalogProjectionV1
    {
        WeaponCatalog WeaponCatalog { get; }
        EquipmentCatalog EquipmentCatalog { get; }
        string SourceId { get; }
        string SourceFingerprint { get; }
        string NormalizationPolicyFingerprint { get; }
        string Fingerprint { get; }
        IReadOnlyList<CanonicalWeaponCatalogEntryV1> Entries { get; }
        bool TryGetByWeaponDefinitionId(string weaponDefinitionId, out CanonicalWeaponCatalogEntryV1 entry);
        bool TryGetByEquipmentDefinitionId(StableId equipmentDefinitionId, out CanonicalWeaponCatalogEntryV1 entry);
        bool TryGetByRuntimeReferenceId(StableId runtimeReferenceId, out CanonicalWeaponCatalogEntryV1 entry);
    }

    public sealed partial class CanonicalWeaponCatalogProjectionV1 : IWeaponCatalogProjectionV1
    {
        public const string BaselineRepositoryPath =
            "Assets/ShooterMover/Resources/WeaponCatalog/weapon_baseline_v01.json";

        internal static readonly Dictionary<string, StableId> ExistingEquipmentIds =
            new Dictionary<string, StableId>(StringComparer.Ordinal)
            {
                { "blaster.mk1", StableId.Parse("equipment.production-starter-blaster") },
                { "shotgun.mk1", StableId.Parse("equipment.production-starter-shotgun") },
                { "rocket_launcher.mk1", StableId.Parse("equipment.production-starter-rocket-launcher") },
                { "chain_weapon.mk1", StableId.Parse("equipment.production-starter-arc-gun") },
                { "ricochet_weapon.mk1", StableId.Parse("equipment.production-starter-ricochet-gun") },
            };

        private static readonly Dictionary<string, StableId> ExistingRuntimeReferences =
            new Dictionary<string, StableId>(StringComparer.Ordinal)
            {
                { "blaster.mk1", StableId.Parse("weapon.blaster-machine-gun") },
                { "shotgun.mk1", StableId.Parse("weapon.shotgun") },
                { "rocket_launcher.mk1", StableId.Parse("weapon.rocket-launcher") },
                { "chain_weapon.mk1", StableId.Parse("weapon.arc-gun") },
                { "ricochet_weapon.mk1", StableId.Parse("weapon.ricochet-gun") },
            };

        internal static readonly Dictionary<string, string> ExistingArtReferences =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "blaster.mk1", "weapon-art.blaster.side-v1" },
                { "shotgun.mk1", "weapon-art.shotgun-basic.side-v1" },
                { "rocket_launcher.mk1", "weapon-art.rocket-launcher.side-v1" },
                { "chain_weapon.mk1", "weapon-art.arc-rifle.side-v1" },
                { "ricochet_weapon.mk1", "weapon-art.ricochet-weapon.side-v1" },
            };

        private readonly ReadOnlyCollection<CanonicalWeaponCatalogEntryV1> entries;
        private readonly Dictionary<string, CanonicalWeaponCatalogEntryV1> byWeaponId;
        private readonly Dictionary<StableId, CanonicalWeaponCatalogEntryV1> byEquipmentId;
        private readonly Dictionary<StableId, CanonicalWeaponCatalogEntryV1> byRuntimeId;

        private CanonicalWeaponCatalogProjectionV1(
            string sourceId,
            string sourceFingerprint,
            string normalizationPolicyFingerprint,
            WeaponCatalog weaponCatalog,
            EquipmentCatalog equipmentCatalog,
            IEnumerable<CanonicalWeaponCatalogEntryV1> catalogEntries)
        {
            SourceId = sourceId;
            SourceFingerprint = sourceFingerprint;
            NormalizationPolicyFingerprint = normalizationPolicyFingerprint ?? string.Empty;
            WeaponCatalog = weaponCatalog;
            EquipmentCatalog = equipmentCatalog;
            var ordered = new List<CanonicalWeaponCatalogEntryV1>(catalogEntries);
            ordered.Sort(delegate(CanonicalWeaponCatalogEntryV1 left, CanonicalWeaponCatalogEntryV1 right)
            {
                return string.CompareOrdinal(left.WeaponDefinition.DefinitionId, right.WeaponDefinition.DefinitionId);
            });
            entries = new ReadOnlyCollection<CanonicalWeaponCatalogEntryV1>(ordered);
            byWeaponId = new Dictionary<string, CanonicalWeaponCatalogEntryV1>(StringComparer.Ordinal);
            byEquipmentId = new Dictionary<StableId, CanonicalWeaponCatalogEntryV1>();
            byRuntimeId = new Dictionary<StableId, CanonicalWeaponCatalogEntryV1>();
            for (int index = 0; index < ordered.Count; index++)
            {
                CanonicalWeaponCatalogEntryV1 entry = ordered[index];
                byWeaponId.Add(entry.WeaponDefinition.DefinitionId, entry);
                byEquipmentId.Add(entry.EquipmentDefinition.DefinitionId, entry);
                byRuntimeId.Add(entry.EquipmentDefinition.RuntimeWeaponReferenceId, entry);
            }
            Fingerprint = CalculateFingerprint(
                SourceId,
                SourceFingerprint,
                NormalizationPolicyFingerprint,
                WeaponCatalog,
                EquipmentCatalog,
                entries);
        }

        public WeaponCatalog WeaponCatalog { get; }
        public EquipmentCatalog EquipmentCatalog { get; }
        public string SourceId { get; }
        public string SourceFingerprint { get; }
        public string NormalizationPolicyFingerprint { get; }
        public string Fingerprint { get; }
        public IReadOnlyList<CanonicalWeaponCatalogEntryV1> Entries { get { return entries; } }

        public bool TryGetByWeaponDefinitionId(string weaponDefinitionId, out CanonicalWeaponCatalogEntryV1 entry)
        {
            return byWeaponId.TryGetValue((weaponDefinitionId ?? string.Empty).Trim(), out entry);
        }

        public bool TryGetByEquipmentDefinitionId(StableId equipmentDefinitionId, out CanonicalWeaponCatalogEntryV1 entry)
        {
            entry = null;
            return equipmentDefinitionId != null && byEquipmentId.TryGetValue(equipmentDefinitionId, out entry);
        }

        public bool TryGetByRuntimeReferenceId(StableId runtimeReferenceId, out CanonicalWeaponCatalogEntryV1 entry)
        {
            entry = null;
            return runtimeReferenceId != null && byRuntimeId.TryGetValue(runtimeReferenceId, out entry);
        }

        public static StableId RuntimeReferenceFor(string weaponDefinitionId)
        {
            string canonical = (weaponDefinitionId ?? string.Empty).Trim();
            StableId existing;
            if (ExistingRuntimeReferences.TryGetValue(canonical, out existing))
            {
                return existing;
            }
            return StableId.Create("weapon", "catalog-" + Slug(canonical));
        }

        public static bool TryResolveDefinitionId(
            WeaponCatalog catalog,
            StableId runtimeReferenceId,
            out string weaponDefinitionId)
        {
            weaponDefinitionId = string.Empty;
            if (catalog == null || runtimeReferenceId == null)
            {
                return false;
            }
            IReadOnlyList<WeaponDefinitionData> definitions =
                catalog.GetDefinitions(WeaponCatalogContentFilter.All);
            for (int index = 0; index < definitions.Count; index++)
            {
                WeaponDefinitionData definition = definitions[index];
                if (RuntimeReferenceFor(definition.DefinitionId) == runtimeReferenceId)
                {
                    weaponDefinitionId = definition.DefinitionId;
                    return true;
                }
            }
            return false;
        }

        internal static string Slug(string value)
        {
            string source = (value ?? string.Empty).Trim().ToLowerInvariant();
            var builder = new StringBuilder(source.Length);
            bool separator = false;
            for (int index = 0; index < source.Length; index++)
            {
                char current = source[index];
                if ((current >= 'a' && current <= 'z') || (current >= '0' && current <= '9'))
                {
                    builder.Append(current);
                    separator = false;
                }
                else if (!separator)
                {
                    builder.Append('-');
                    separator = true;
                }
            }
            return builder.ToString().Trim('-');
        }
    }
}
