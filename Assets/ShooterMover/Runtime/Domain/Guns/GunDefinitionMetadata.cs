using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Domain.Guns
{
    public sealed class GunPresentation
    {
        public GunPresentation(
            string inventorySideProfileReference,
            string mountedTopDownReference,
            string deliveryReference,
            string trailReference,
            string impactReference,
            string explosionReference)
        {
            InventorySideProfileReference = RequireText(inventorySideProfileReference, nameof(inventorySideProfileReference));
            MountedTopDownReference = RequireText(mountedTopDownReference, nameof(mountedTopDownReference));
            DeliveryReference = RequireText(deliveryReference, nameof(deliveryReference));
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
                throw new ArgumentException("A presentation reference is required.", parameterName);
            }
            return value;
        }

        private static string OptionalText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }

    public enum GunDropAvailability
    {
        Live = 1,
        PreviewOnly = 2,
        Disabled = 3,
    }

    public enum GunStrongboxEligibilityMode
    {
        MinimumTier = 1,
        ExplicitAllowedTierIds = 2,
        ExplicitAllowedTiers = ExplicitAllowedTierIds,
    }

    public sealed class GunStrongboxEligibility
    {
        private readonly ReadOnlyCollection<StableId> allowedTierIds;

        private GunStrongboxEligibility(
            GunStrongboxEligibilityMode mode,
            int minimumTier,
            IEnumerable<StableId> tierIds)
        {
            Mode = mode;
            MinimumTier = minimumTier;
            allowedTierIds = new ReadOnlyCollection<StableId>(
                new List<StableId>(tierIds ?? Array.Empty<StableId>()));
        }

        public GunStrongboxEligibilityMode Mode { get; }
        public int MinimumTier { get; }
        public IReadOnlyList<StableId> AllowedTierIds { get { return allowedTierIds; } }

        public static GunStrongboxEligibility FromMinimumTier(int minimumTier)
        {
            if (minimumTier < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumTier));
            }
            return new GunStrongboxEligibility(
                GunStrongboxEligibilityMode.MinimumTier,
                minimumTier,
                Array.Empty<StableId>());
        }

        public static GunStrongboxEligibility FromAllowedTierIds(IEnumerable<StableId> tierIds)
        {
            if (tierIds == null)
            {
                throw new ArgumentNullException(nameof(tierIds));
            }

            var copy = new List<StableId>(tierIds);
            if (copy.Count == 0)
            {
                throw new ArgumentException(
                    "At least one explicit strongbox tier identity is required.",
                    nameof(tierIds));
            }
            for (int index = 0; index < copy.Count; index++)
            {
                if (copy[index] == null)
                {
                    throw new ArgumentException(
                        "Explicit strongbox tier identities cannot contain null values.",
                        nameof(tierIds));
                }
            }
            copy.Sort();
            for (int index = 1; index < copy.Count; index++)
            {
                if (copy[index - 1].Equals(copy[index]))
                {
                    throw new ArgumentException(
                        "Explicit strongbox tier identities must be unique.",
                        nameof(tierIds));
                }
            }

            return new GunStrongboxEligibility(
                GunStrongboxEligibilityMode.ExplicitAllowedTierIds,
                0,
                copy);
        }

        internal static GunStrongboxEligibility FromAllowedTiers(IEnumerable<StableId> tierIds)
        {
            return FromAllowedTierIds(tierIds);
        }

        public bool IsEligibleForProgressionTier(int tier)
        {
            return Mode == GunStrongboxEligibilityMode.MinimumTier
                && tier >= MinimumTier;
        }

        public bool IsExplicitlyAllowed(StableId tierId)
        {
            if (tierId == null
                || Mode != GunStrongboxEligibilityMode.ExplicitAllowedTierIds)
            {
                return false;
            }

            int minimum = 0;
            int maximum = allowedTierIds.Count - 1;
            while (minimum <= maximum)
            {
                int middle = minimum + ((maximum - minimum) / 2);
                int comparison = allowedTierIds[middle].CompareTo(tierId);
                if (comparison == 0)
                {
                    return true;
                }
                if (comparison < 0)
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

        public bool IsEligible(StableId tierId, int progressionTier)
        {
            return Mode == GunStrongboxEligibilityMode.MinimumTier
                ? IsEligibleForProgressionTier(progressionTier)
                : IsExplicitlyAllowed(tierId);
        }
    }

    public sealed class GunDropMetadata
    {
        public GunDropMetadata(
            StableId equipmentDefinitionId,
            StableId rarityId,
            GunDropAvailability availability,
            int peakDropLevel,
            double baseSelectionWeight,
            GunStrongboxEligibility strongboxEligibility)
        {
            EquipmentDefinitionId = equipmentDefinitionId ?? throw new ArgumentNullException(nameof(equipmentDefinitionId));
            RarityId = rarityId ?? throw new ArgumentNullException(nameof(rarityId));
            if (!Enum.IsDefined(typeof(GunDropAvailability), availability))
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
            StrongboxEligibility = strongboxEligibility ?? throw new ArgumentNullException(nameof(strongboxEligibility));
        }

        public StableId EquipmentDefinitionId { get; }
        public StableId RarityId { get; }
        public GunDropAvailability Availability { get; }
        public int PeakDropLevel { get; }
        public double BaseSelectionWeight { get; }
        public GunStrongboxEligibility StrongboxEligibility { get; }
    }
}
