using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Application.Guns.Catalog
{
    public static class GunAugments
    {
        public const int MaximumLevel = 12;

        public static readonly StableId DamageId =
            StableId.Parse("augment.gun-damage");
        public static readonly StableId FireRateId =
            StableId.Parse("augment.gun-fire-rate");
        public static readonly StableId RicochetId =
            StableId.Parse("augment.gun-ricochet");

        private static readonly ReadOnlyCollection<AugmentDefinition>
            DefinitionsValue = new ReadOnlyCollection<AugmentDefinition>(
                new List<AugmentDefinition>
                {
                    Definition(DamageId, "damage", "Damage"),
                    Definition(FireRateId, "fire-rate", "Fire Rate"),
                    Definition(RicochetId, "ricochet", "Ricochet"),
                });

        public static IReadOnlyList<AugmentDefinition> Definitions
        {
            get { return DefinitionsValue; }
        }

        public static bool TryResolve(
            EquipmentCatalog catalog,
            AugmentInstance instance,
            out GunAugmentModifierSet modifierSet,
            out string rejectionCode)
        {
            modifierSet = null;
            if (catalog == null
                || instance == null
                || instance.DefinitionId == null
                || instance.Level < 1
                || instance.Level > MaximumLevel)
            {
                rejectionCode = "gun-augment-input-invalid";
                return false;
            }

            AugmentDefinition definition = catalog.FindAugmentDefinition(
                instance.DefinitionId);
            if (definition == null)
            {
                rejectionCode = "gun-augment-definition-missing";
                return false;
            }

            GunStatModifier modifier;
            if (instance.DefinitionId == DamageId)
            {
                modifier = GunStatModifier.AdditivePercent(
                    GunEffectiveStat.DirectDamage,
                    0.10d * instance.Level);
            }
            else if (instance.DefinitionId == FireRateId)
            {
                modifier = GunStatModifier.AdditivePercent(
                    GunEffectiveStat.RateOfFire,
                    0.10d * instance.Level);
            }
            else if (instance.DefinitionId == RicochetId)
            {
                modifier = GunStatModifier.Flat(
                    GunEffectiveStat.RicochetTenths,
                    instance.Level);
            }
            else
            {
                rejectionCode = "gun-augment-definition-unsupported";
                return false;
            }

            modifierSet = GunAugmentModifierSet.Create(
                definition,
                instance,
                new[] { modifier });
            rejectionCode = string.Empty;
            return true;
        }

        private static AugmentDefinition Definition(
            StableId augmentId,
            string family,
            string displayName)
        {
            return AugmentDefinition.Create(
                augmentId,
                StableId.Create("augment-family", "gun-" + family),
                displayName,
                AugmentCompatibility.Create(
                    new[] { EquipmentCategoryIds.Gun },
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>(),
                    Array.Empty<StableId>()),
                Array.Empty<StableId>(),
                AugmentDuplicatePolicy.DisallowSameDefinition,
                InclusiveIntRange.Create(1, 1),
                InclusiveIntRange.Create(1, MaximumLevel));
        }
    }
}
