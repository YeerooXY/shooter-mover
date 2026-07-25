using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Weapons
{
    /// <summary>
    /// Presentation references are independent from combat semantics. Missing art is a content
    /// validation failure and never changes delivery behaviour.
    /// </summary>
    public sealed class WeaponPresentation
    {
        public WeaponPresentation(
            string inventorySideProfileReference,
            string mountedTopDownReference,
            string deliveryReference,
            string trailReference,
            string impactReference,
            string explosionReference)
        {
            InventorySideProfileReference = RequireText(
                inventorySideProfileReference,
                nameof(inventorySideProfileReference));
            MountedTopDownReference = RequireText(
                mountedTopDownReference,
                nameof(mountedTopDownReference));
            DeliveryReference = RequireText(
                deliveryReference,
                nameof(deliveryReference));
            TrailReference = OptionalText(trailReference);
            ImpactReference = OptionalText(impactReference);
            ExplosionReference = OptionalText(explosionReference);
        }

        public string InventorySideProfileReference { get; }
        public string MountedTopDownReference { get; }
        public string DeliveryReference { get; }
        public string TrailReference { get; }
        public string ImpactReference { get; }
        public string ExplosionReference { get; }

        private static string RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A presentation reference is required.",
                    parameterName);
            }
            return value;
        }

        private static string OptionalText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public enum WeaponDropAvailability
    {
        Live = 1,
        PreviewOnly = 2,
        Disabled = 3,
    }

    public enum WeaponStrongboxEligibilityMode
    {
        MinimumTier = 1,
        ExplicitAllowedTiers = 2,
    }

    /// <summary>
    /// Stable strongbox eligibility. MinimumTier means that tier and every later tier.
    /// ExplicitAllowedTiers is reserved for genuinely named-tier-exclusive content.
    /// </summary>
    public sealed class WeaponStrongboxEligibility
    {
        private readonly ReadOnlyCollection<int> allowedTiers;

        private WeaponStrongboxEligibility(
            WeaponStrongboxEligibilityMode mode,
            int minimumTier,
            IEnumerable<int> tiers)
        {
            Mode = mode;
            MinimumTier = minimumTier;
            allowedTiers = new ReadOnlyCollection<int>(
                new List<int>(tiers ?? Array.Empty<int>()));
        }

        public WeaponStrongboxEligibilityMode Mode { get; }
        public int MinimumTier { get; }
        public IReadOnlyList<int> AllowedTiers { get { return allowedTiers; } }

        public static WeaponStrongboxEligibility FromMinimumTier(int minimumTier)
        {
            if (minimumTier < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumTier));
            }
            return new WeaponStrongboxEligibility(
                WeaponStrongboxEligibilityMode.MinimumTier,
                minimumTier,
                Array.Empty<int>());
        }

        public static WeaponStrongboxEligibility FromAllowedTiers(
            IEnumerable<int> tiers)
        {
            if (tiers == null)
            {
                throw new ArgumentNullException(nameof(tiers));
            }

            var copy = new List<int>(tiers);
            copy.Sort();
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one explicit strongbox tier is required.",
                    nameof(tiers));
            }
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(tiers));
                }
                if (index > 0 && copy[index - 1] == copy[index])
                {
                    throw new ArgumentException(
                        "Explicit strongbox tiers must be unique.",
                        nameof(tiers));
                }
            }

            return new WeaponStrongboxEligibility(
                WeaponStrongboxEligibilityMode.ExplicitAllowedTiers,
                0,
                copy);
        }

        public bool IsEligible(int tier)
        {
            if (tier < 1)
            {
                return false;
            }
            if (Mode == WeaponStrongboxEligibilityMode.MinimumTier)
            {
                return tier >= MinimumTier;
            }

            int minimum = 0;
            int maximum = allowedTiers.Count - 1;
            while (minimum <= maximum)
            {
                int middle = minimum + ((maximum - minimum) / 2);
                int candidate = allowedTiers[middle];
                if (candidate == tier)
                {
                    return true;
                }
                if (candidate < tier)
                {
                    minimum = middle + 1;
                }
                else
                {
                    maximum = middle - 1;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Equipment-specific selection metadata consumed by the future canonical strongbox
    /// projection. Strongbox rarity percentages, level rolls, and augment tables remain owned by
    /// strongbox tier/profile authorities.
    /// </summary>
    public sealed class WeaponDropMetadata
    {
        public WeaponDropMetadata(
            StableId equipmentDefinitionId,
            StableId rarityId,
            WeaponDropAvailability availability,
            int peakDropLevel,
            double baseSelectionWeight,
            WeaponStrongboxEligibility strongboxEligibility)
        {
            EquipmentDefinitionId = equipmentDefinitionId
                ?? throw new ArgumentNullException(nameof(equipmentDefinitionId));
            RarityId = rarityId
                ?? throw new ArgumentNullException(nameof(rarityId));
            if (!Enum.IsDefined(typeof(WeaponDropAvailability), availability))
            {
                throw new ArgumentOutOfRangeException(nameof(availability));
            }
            if (peakDropLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(peakDropLevel));
            }
            if (double.IsNaN(baseSelectionWeight)
                || double.IsInfinity(baseSelectionWeight)
                || baseSelectionWeight <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(baseSelectionWeight));
            }

            Availability = availability;
            PeakDropLevel = peakDropLevel;
            BaseSelectionWeight = baseSelectionWeight;
            StrongboxEligibility = strongboxEligibility
                ?? throw new ArgumentNullException(nameof(strongboxEligibility));
        }

        public StableId EquipmentDefinitionId { get; }
        public StableId RarityId { get; }
        public WeaponDropAvailability Availability { get; }
        public int PeakDropLevel { get; }
        public double BaseSelectionWeight { get; }
        public WeaponStrongboxEligibility StrongboxEligibility { get; }
    }
}
