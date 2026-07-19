using System;
using System.Collections.Generic;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.UnityAdapters.Weapons.Live
{
    /// <summary>
    /// Builds the immutable subset consumed by WPN-CORE-002. Effect payloads that the
    /// live sink applies after core acceptance remain on the original catalog definition.
    /// This is a read-only projection, not a second catalog or weapon authority.
    /// </summary>
    internal static class WeaponCatalogExecutionProjection
    {
        public static WeaponCatalog Create(WeaponCatalog source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            List<WeaponDefinitionData> definitions =
                new List<WeaponDefinitionData>(source.Definitions.Count);
            for (int index = 0; index < source.Definitions.Count; index++)
            {
                definitions.Add(Project(source.Definitions[index]));
            }

            Dictionary<string, WeaponArchetypeDefinition> archetypes =
                new Dictionary<string, WeaponArchetypeDefinition>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, WeaponArchetypeDefinition> pair in source.Archetypes)
            {
                archetypes.Add(pair.Key, pair.Value);
            }

            return new WeaponCatalog(
                source.Version,
                source.Status,
                source.Rules,
                source.Inputs,
                archetypes,
                source.Families,
                definitions);
        }

        private static WeaponDefinitionData Project(WeaponDefinitionData value)
        {
            if (value == null)
            {
                throw new ArgumentException("Weapon catalogs cannot contain null definitions.", nameof(value));
            }

            return new WeaponDefinitionData(
                value.DefinitionId,
                value.DisplayName,
                value.FamilyId,
                value.Mark,
                value.DamageType,
                value.Archetype,
                value.BuildAffinity,
                value.FirstAppearance,
                value.PeakDropLevel,
                value.PowerAnchor,
                value.Rarity,
                value.RarityWeight,
                value.DefinitionWeightModifier,
                value.FinalBaseWeight,
                value.EarlyTail,
                value.LateTail,
                value.AcquisitionClass,
                value.TopBoxOnly,
                value.CraftingRoute,
                value.ArchetypeDpsFactor,
                value.PowerIndex,
                value.TargetDps,
                value.DirectShare,
                value.AreaShare,
                0d,
                value.FireRate,
                value.ProjectilesPerTrigger,
                value.BurstCount,
                value.DamagePerProjectile,
                value.SpreadDegrees,
                value.ProjectileSpeed,
                value.Range,
                value.Pierce,
                value.ExplosionRadius,
                value.AreaDamagePerTrigger,
                0d,
                0d,
                0d,
                0d,
                value.ChainTargets,
                value.ChainRange,
                value.Knockback,
                value.PowerCost,
                value.HealingPerSecond,
                value.PrimaryEffect,
                value.Notes,
                value.Availability,
                value.SideProfileArtReferences);
        }
    }
}
