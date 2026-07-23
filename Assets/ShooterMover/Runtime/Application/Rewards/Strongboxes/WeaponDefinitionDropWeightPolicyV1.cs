
using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Strongboxes;
using ShooterMover.Domain.Weapons.Catalog;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    public sealed class WeaponDefinitionDropWeightContextV1
    {
        public WeaponDefinitionDropWeightContextV1(
            StrongboxHybridLootPolicyV1 tierPolicy,
            int strongboxTierNumber,
            StrongboxTargetLevelRollV1 target,
            WeaponDefinitionData definition,
            StableId normalizedRarityId)
        {
            TierPolicy = tierPolicy ?? throw new ArgumentNullException(nameof(tierPolicy));
            if (strongboxTierNumber < 1) throw new ArgumentOutOfRangeException(nameof(strongboxTierNumber));
            StrongboxTierNumber = strongboxTierNumber;
            Target = target ?? throw new ArgumentNullException(nameof(target));
            TargetLevel = target.TargetLevel;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            NormalizedRarityId = normalizedRarityId
                ?? throw new ArgumentNullException(nameof(normalizedRarityId));
        }

        public WeaponDefinitionDropWeightContextV1(
            StrongboxHybridLootPolicyV1 tierPolicy,
            int strongboxTierNumber,
            int targetLevel,
            WeaponDefinitionData definition,
            StableId normalizedRarityId)
        {
            TierPolicy = tierPolicy ?? throw new ArgumentNullException(nameof(tierPolicy));
            if (strongboxTierNumber < 1) throw new ArgumentOutOfRangeException(nameof(strongboxTierNumber));
            if (targetLevel < 1) throw new ArgumentOutOfRangeException(nameof(targetLevel));
            StrongboxTierNumber = strongboxTierNumber;
            Target = null;
            TargetLevel = targetLevel;
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            NormalizedRarityId = normalizedRarityId
                ?? throw new ArgumentNullException(nameof(normalizedRarityId));
        }

        public StrongboxHybridLootPolicyV1 TierPolicy { get; }
        public int StrongboxTierNumber { get; }
        public StrongboxTargetLevelRollV1 Target { get; }
        public int TargetLevel { get; }
        public WeaponDefinitionData Definition { get; }
        public StableId NormalizedRarityId { get; }
    }

    public sealed class WeaponDefinitionDropEligibilityContextV1
    {
        public WeaponDefinitionDropEligibilityContextV1(
            WeaponDefinitionData definition,
            int strongboxTierNumber,
            int topStrongboxTierNumber)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (strongboxTierNumber < 1) throw new ArgumentOutOfRangeException(nameof(strongboxTierNumber));
            if (topStrongboxTierNumber < strongboxTierNumber)
            {
                throw new ArgumentOutOfRangeException(nameof(topStrongboxTierNumber));
            }
            StrongboxTierNumber = strongboxTierNumber;
            TopStrongboxTierNumber = topStrongboxTierNumber;
        }

        public WeaponDefinitionData Definition { get; }
        public int StrongboxTierNumber { get; }
        public int TopStrongboxTierNumber { get; }
    }

    public interface IWeaponDefinitionDropEligibilityPolicyV1
    {
        string Fingerprint { get; }
        bool IsEligible(WeaponDefinitionDropEligibilityContextV1 context);
    }

    public sealed class WeaponDefinitionDropEligibilityPolicyV1 :
        IWeaponDefinitionDropEligibilityPolicyV1
    {
        public string Fingerprint
        {
            get { return "weapon-definition-drop-eligibility.live-top-box-v1"; }
        }

        public static WeaponDefinitionDropEligibilityPolicyV1 CreateBaselineV1()
        {
            return new WeaponDefinitionDropEligibilityPolicyV1();
        }

        public bool IsEligible(WeaponDefinitionDropEligibilityContextV1 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return context.Definition.Availability == WeaponCatalogAvailability.Live
                && (!context.Definition.TopBoxOnly
                    || context.StrongboxTierNumber
                        == context.TopStrongboxTierNumber);
        }
    }

    public interface IWeaponDefinitionDropWeightPolicyV1
    {
        string Fingerprint { get; }
        ulong EvaluateWeightUnits(WeaponDefinitionDropWeightContextV1 context);
    }

    /// <summary>
    /// Deterministic fixed-point soft curve. PeakDropLevel is the maximum. EarlyTail
    /// shapes the approach and the extra pre-FirstAppearance tail; LateTail shapes the
    /// persistent post-peak tail. Every otherwise eligible definition receives at least
    /// MinimumTailWeightUnits, so ordinary level distance never becomes a hidden hard gate.
    /// Strongbox tier enters through its authored normalized-rarity multiplier.
    /// </summary>
    public sealed class WeaponDefinitionDropWeightPolicyV1 :
        IWeaponDefinitionDropWeightPolicyV1
    {
        public const ulong WeightScale = 1000000UL;
        public const ulong MinimumTailWeightUnits = 1UL;
        public const ulong MaximumWeightUnits = 1000000000000UL;
        private const decimal MinimumTailWidth = 0.01m;

        public string Fingerprint
        {
            get
            {
                return "weapon-definition-drop-weight.soft-tail-fixed-v1"
                    + "|scale=" + WeightScale
                    + "|min=" + MinimumTailWeightUnits
                    + "|max=" + MaximumWeightUnits;
            }
        }

        public static WeaponDefinitionDropWeightPolicyV1 CreateBaselineV1()
        {
            return new WeaponDefinitionDropWeightPolicyV1();
        }

        public ulong EvaluateWeightUnits(WeaponDefinitionDropWeightContextV1 context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            WeaponDefinitionData definition = context.Definition;
            RequireFinitePositive(definition.FinalBaseWeight, nameof(definition.FinalBaseWeight));
            RequireFinitePositive(definition.EarlyTail, nameof(definition.EarlyTail));
            RequireFinitePositive(definition.LateTail, nameof(definition.LateTail));

            decimal earlyWidth = Math.Max(MinimumTailWidth, Convert.ToDecimal(definition.EarlyTail));
            decimal lateWidth = Math.Max(MinimumTailWidth, Convert.ToDecimal(definition.LateTail));
            int targetLevel = context.TargetLevel;
            int first = Math.Max(1, definition.FirstAppearance);
            int peak = Math.Max(first, definition.PeakDropLevel);

            decimal levelAffinity;
            if (targetLevel <= peak)
            {
                decimal peakDistance = peak - targetLevel;
                levelAffinity = ReciprocalSquare(peakDistance / earlyWidth);
                if (targetLevel < first)
                {
                    decimal preAppearanceDistance = first - targetLevel;
                    levelAffinity *= ReciprocalSquare(preAppearanceDistance / earlyWidth);
                }
            }
            else
            {
                decimal lateDistance = targetLevel - peak;
                levelAffinity = ReciprocalSquare(lateDistance / lateWidth);
            }

            int authoredRarityMilli = context.TierPolicy
                .ResolveDefinitionRaritySelectionMultiplierMilli(context.NormalizedRarityId);
            decimal rarityMultiplier = Math.Max(1, authoredRarityMilli)
                / (decimal)StrongboxHybridLootPolicyV1.RarityMultiplierScale;
            decimal rawUnits = Convert.ToDecimal(definition.FinalBaseWeight)
                * levelAffinity
                * rarityMultiplier
                * WeightScale;
            if (rawUnits <= MinimumTailWeightUnits) return MinimumTailWeightUnits;
            if (rawUnits >= MaximumWeightUnits) return MaximumWeightUnits;
            return Math.Max(MinimumTailWeightUnits, (ulong)decimal.Round(rawUnits, 0, MidpointRounding.AwayFromZero));
        }

        private static decimal ReciprocalSquare(decimal normalizedDistance)
        {
            decimal denominator = 1m + Math.Max(0m, normalizedDistance);
            return 1m / (denominator * denominator);
        }

        private static void RequireFinitePositive(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
