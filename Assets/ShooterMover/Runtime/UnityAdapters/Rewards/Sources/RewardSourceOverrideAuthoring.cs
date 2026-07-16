using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.Sources
{
    public enum RewardSourceOverrideAuthoringMode
    {
        Inherit = 0,
        None = 1,
        Replace = 2,
        AppendGuaranteed = 3,
        MoneyOnly = 4,
        StrongboxExactTier = 5,
        StrongboxTierRange = 6,
        Miscellaneous = 7
    }

    [Serializable]
    public sealed class RewardScalingInputOverrideAuthoring
    {
        [SerializeField] private string inputId = "reward-input.unassigned";
        [SerializeField] private RewardScalingInputKindV1 kind =
            RewardScalingInputKindV1.CharacterLevel;

        public RewardScalingInputOverrideAuthoring()
        {
        }

        public RewardScalingInputOverrideAuthoring(
            string inputId,
            RewardScalingInputKindV1 kind)
        {
            this.inputId = inputId ?? throw new ArgumentNullException(nameof(inputId));
            this.kind = kind;
        }

        public RewardScalingInputDescriptorV1 Build()
        {
            return RewardScalingInputDescriptorV1.Create(
                StableId.Parse(inputId),
                kind);
        }
    }

    [Serializable]
    public sealed class RewardGrantOverrideAuthoring
    {
        [SerializeField] private string grantId = "reward-grant.unassigned";
        [SerializeField] private RewardGrantKindV1 kind = RewardGrantKindV1.Miscellaneous;
        [SerializeField] private string contentId = "reward-content.unassigned";
        [SerializeField] private long minimumQuantity = 1L;
        [SerializeField] private long maximumQuantity = 1L;
        [SerializeField] private RewardScalingInputOverrideAuthoring[] scalingInputs =
            Array.Empty<RewardScalingInputOverrideAuthoring>();

        public RewardGrantOverrideAuthoring()
        {
        }

        public RewardGrantOverrideAuthoring(
            string grantId,
            RewardGrantKindV1 kind,
            string contentId,
            long minimumQuantity,
            long maximumQuantity,
            params RewardScalingInputOverrideAuthoring[] scalingInputs)
        {
            this.grantId = grantId ?? throw new ArgumentNullException(nameof(grantId));
            this.kind = kind;
            this.contentId = contentId ?? throw new ArgumentNullException(nameof(contentId));
            this.minimumQuantity = minimumQuantity;
            this.maximumQuantity = maximumQuantity;
            this.scalingInputs = scalingInputs
                ?? Array.Empty<RewardScalingInputOverrideAuthoring>();
        }

        public RewardGrantKindV1 Kind
        {
            get { return kind; }
        }

        public RewardGrantSpecificationV1 Build()
        {
            List<RewardScalingInputDescriptorV1> builtInputs =
                new List<RewardScalingInputDescriptorV1>(
                    scalingInputs == null ? 0 : scalingInputs.Length);
            if (scalingInputs != null)
            {
                for (int index = 0; index < scalingInputs.Length; index++)
                {
                    RewardScalingInputOverrideAuthoring input = scalingInputs[index];
                    if (input == null)
                    {
                        throw new InvalidOperationException(
                            $"Reward grant '{grantId}' contains a null scaling input.");
                    }

                    builtInputs.Add(input.Build());
                }
            }

            return RewardGrantSpecificationV1.Create(
                StableId.Parse(grantId),
                kind,
                StableId.Parse(contentId),
                RewardQuantityRangeV1.Create(minimumQuantity, maximumQuantity),
                builtInputs);
        }
    }

    [Serializable]
    public sealed class StrongboxTierOptionAuthoring
    {
        [SerializeField] private int tierOrder;
        [SerializeField] private string outcomeId = "reward-outcome.unassigned";
        [SerializeField] private string tierContentId = "strongbox-tier.unassigned";
        [SerializeField] private long weight = 1L;

        public StrongboxTierOptionAuthoring()
        {
        }

        public StrongboxTierOptionAuthoring(
            int tierOrder,
            string outcomeId,
            string tierContentId,
            long weight)
        {
            this.tierOrder = tierOrder;
            this.outcomeId = outcomeId ?? throw new ArgumentNullException(nameof(outcomeId));
            this.tierContentId = tierContentId
                ?? throw new ArgumentNullException(nameof(tierContentId));
            this.weight = weight;
        }

        public int TierOrder
        {
            get { return tierOrder; }
        }

        public WeightedRewardOutcomeV1 Build(string grantId)
        {
            StableId baseGrantId = StableId.Parse(grantId);
            StableId tierGrantId = StableId.Create(
                baseGrantId.Namespace,
                baseGrantId.Value
                    + "-tier-"
                    + tierOrder.ToString(CultureInfo.InvariantCulture));
            return WeightedRewardOutcomeV1.CreateGrant(
                StableId.Parse(outcomeId),
                weight,
                RewardGrantSpecificationV1.CreateFixed(
                    tierGrantId,
                    RewardGrantKindV1.Strongbox,
                    StableId.Parse(tierContentId),
                    1L));
        }
    }

    [Serializable]
    public sealed class RewardSourceOverrideAuthoring
    {
        [SerializeField] private RewardSourceOverrideAuthoringMode mode =
            RewardSourceOverrideAuthoringMode.Inherit;
        [SerializeField] private string overrideId = "reward-override.unassigned";
        [SerializeField] private string resultProfileId = "reward-profile.override-result";
        [SerializeField] private ScriptableObject replacementProfileSource;
        [SerializeField] private RewardGrantOverrideAuthoring[] guaranteedEntries =
            Array.Empty<RewardGrantOverrideAuthoring>();

        [Header("Money-only")]
        [SerializeField] private string moneyGrantId = "reward-grant.money";
        [SerializeField] private string moneyContentId = "currency.money";
        [SerializeField] private long moneyMinimum = 1L;
        [SerializeField] private long moneyMaximum = 1L;

        [Header("Strongbox exact tier")]
        [SerializeField] private string strongboxGrantId = "reward-grant.strongbox";
        [SerializeField] private string exactStrongboxTierId = "strongbox-tier.unassigned";

        [Header("Strongbox tier range")]
        [SerializeField] private string strongboxRangeGroupId = "reward-group.strongbox-range";
        [SerializeField] private int minimumStrongboxTierOrder;
        [SerializeField] private int maximumStrongboxTierOrder;
        [SerializeField] private StrongboxTierOptionAuthoring[] strongboxTierOptions =
            Array.Empty<StrongboxTierOptionAuthoring>();

        public RewardSourceOverrideAuthoringMode Mode
        {
            get { return mode; }
        }

        public static RewardSourceOverrideAuthoring Inherit(string overrideId)
        {
            return CreateBase(RewardSourceOverrideAuthoringMode.Inherit, overrideId, null);
        }

        public static RewardSourceOverrideAuthoring None(
            string overrideId,
            string resultProfileId)
        {
            return CreateBase(
                RewardSourceOverrideAuthoringMode.None,
                overrideId,
                resultProfileId);
        }

        public static RewardSourceOverrideAuthoring Replace(
            string overrideId,
            ScriptableObject replacementProfileSource)
        {
            RewardSourceOverrideAuthoring value = CreateBase(
                RewardSourceOverrideAuthoringMode.Replace,
                overrideId,
                null);
            value.replacementProfileSource = replacementProfileSource
                ?? throw new ArgumentNullException(nameof(replacementProfileSource));
            return value;
        }

        public static RewardSourceOverrideAuthoring AppendGuaranteed(
            string overrideId,
            string resultProfileId,
            params RewardGrantOverrideAuthoring[] entries)
        {
            RewardSourceOverrideAuthoring value = CreateBase(
                RewardSourceOverrideAuthoringMode.AppendGuaranteed,
                overrideId,
                resultProfileId);
            value.guaranteedEntries = entries ?? Array.Empty<RewardGrantOverrideAuthoring>();
            return value;
        }

        public static RewardSourceOverrideAuthoring MoneyOnly(
            string overrideId,
            string resultProfileId,
            string grantId,
            string contentId,
            long minimum,
            long maximum)
        {
            RewardSourceOverrideAuthoring value = CreateBase(
                RewardSourceOverrideAuthoringMode.MoneyOnly,
                overrideId,
                resultProfileId);
            value.moneyGrantId = grantId ?? throw new ArgumentNullException(nameof(grantId));
            value.moneyContentId = contentId ?? throw new ArgumentNullException(nameof(contentId));
            value.moneyMinimum = minimum;
            value.moneyMaximum = maximum;
            return value;
        }

        public static RewardSourceOverrideAuthoring StrongboxExactTier(
            string overrideId,
            string resultProfileId,
            string grantId,
            string tierContentId)
        {
            RewardSourceOverrideAuthoring value = CreateBase(
                RewardSourceOverrideAuthoringMode.StrongboxExactTier,
                overrideId,
                resultProfileId);
            value.strongboxGrantId = grantId ?? throw new ArgumentNullException(nameof(grantId));
            value.exactStrongboxTierId = tierContentId
                ?? throw new ArgumentNullException(nameof(tierContentId));
            return value;
        }

        public static RewardSourceOverrideAuthoring StrongboxTierRange(
            string overrideId,
            string resultProfileId,
            string groupId,
            string grantId,
            int minimumTierOrder,
            int maximumTierOrder,
            params StrongboxTierOptionAuthoring[] options)
        {
            RewardSourceOverrideAuthoring value = CreateBase(
                RewardSourceOverrideAuthoringMode.StrongboxTierRange,
                overrideId,
                resultProfileId);
            value.strongboxRangeGroupId = groupId
                ?? throw new ArgumentNullException(nameof(groupId));
            value.strongboxGrantId = grantId
                ?? throw new ArgumentNullException(nameof(grantId));
            value.minimumStrongboxTierOrder = minimumTierOrder;
            value.maximumStrongboxTierOrder = maximumTierOrder;
            value.strongboxTierOptions = options ?? Array.Empty<StrongboxTierOptionAuthoring>();
            return value;
        }

        public static RewardSourceOverrideAuthoring Miscellaneous(
            string overrideId,
            string resultProfileId,
            params RewardGrantOverrideAuthoring[] entries)
        {
            RewardSourceOverrideAuthoring value = CreateBase(
                RewardSourceOverrideAuthoringMode.Miscellaneous,
                overrideId,
                resultProfileId);
            value.guaranteedEntries = entries ?? Array.Empty<RewardGrantOverrideAuthoring>();
            return value;
        }

        public RewardProfileV1 Resolve(
            StableId sourceInstanceId,
            RewardProfileV1 inheritedProfile)
        {
            if (sourceInstanceId == null)
            {
                throw new ArgumentNullException(nameof(sourceInstanceId));
            }

            if (inheritedProfile == null)
            {
                throw new ArgumentNullException(nameof(inheritedProfile));
            }

            StableId parsedOverrideId = StableId.Parse(overrideId);
            switch (mode)
            {
                case RewardSourceOverrideAuthoringMode.Inherit:
                    return RewardSourceOverrideV1.Inherit(
                        parsedOverrideId,
                        sourceInstanceId).Resolve(inheritedProfile);
                case RewardSourceOverrideAuthoringMode.None:
                    return RewardSourceOverrideV1.NoReward(
                        parsedOverrideId,
                        sourceInstanceId,
                        StableId.Parse(resultProfileId)).Resolve(inheritedProfile);
                case RewardSourceOverrideAuthoringMode.Replace:
                    return RewardSourceOverrideV1.ReplaceEntirely(
                        parsedOverrideId,
                        sourceInstanceId,
                        RewardProfileCapabilityReader.BuildProfile(
                            replacementProfileSource)).Resolve(inheritedProfile);
                case RewardSourceOverrideAuthoringMode.AppendGuaranteed:
                    return RewardSourceOverrideV1.AppendGuaranteedEntries(
                        parsedOverrideId,
                        sourceInstanceId,
                        StableId.Parse(resultProfileId),
                        BuildEntries(guaranteedEntries, false)).Resolve(inheritedProfile);
                case RewardSourceOverrideAuthoringMode.MoneyOnly:
                    return ReplaceWith(
                        parsedOverrideId,
                        sourceInstanceId,
                        RewardProfileV1.Create(
                            StableId.Parse(resultProfileId),
                            new[]
                            {
                                RewardGrantSpecificationV1.Create(
                                    StableId.Parse(moneyGrantId),
                                    RewardGrantKindV1.Money,
                                    StableId.Parse(moneyContentId),
                                    RewardQuantityRangeV1.Create(
                                        moneyMinimum,
                                        moneyMaximum),
                                    Array.Empty<RewardScalingInputDescriptorV1>())
                            },
                            Array.Empty<IndependentRewardRollV1>(),
                            Array.Empty<ExclusiveRewardGroupV1>()),
                        inheritedProfile);
                case RewardSourceOverrideAuthoringMode.StrongboxExactTier:
                    return ReplaceWith(
                        parsedOverrideId,
                        sourceInstanceId,
                        RewardProfileV1.Create(
                            StableId.Parse(resultProfileId),
                            new[]
                            {
                                RewardGrantSpecificationV1.CreateFixed(
                                    StableId.Parse(strongboxGrantId),
                                    RewardGrantKindV1.Strongbox,
                                    StableId.Parse(exactStrongboxTierId),
                                    1L)
                            },
                            Array.Empty<IndependentRewardRollV1>(),
                            Array.Empty<ExclusiveRewardGroupV1>()),
                        inheritedProfile);
                case RewardSourceOverrideAuthoringMode.StrongboxTierRange:
                    return ReplaceWith(
                        parsedOverrideId,
                        sourceInstanceId,
                        BuildStrongboxRangeProfile(),
                        inheritedProfile);
                case RewardSourceOverrideAuthoringMode.Miscellaneous:
                    return ReplaceWith(
                        parsedOverrideId,
                        sourceInstanceId,
                        RewardProfileV1.Create(
                            StableId.Parse(resultProfileId),
                            BuildEntries(guaranteedEntries, true),
                            Array.Empty<IndependentRewardRollV1>(),
                            Array.Empty<ExclusiveRewardGroupV1>()),
                        inheritedProfile);
                default:
                    throw new InvalidOperationException(
                        $"Unsupported reward source override mode '{mode}'.");
            }
        }

        private static RewardSourceOverrideAuthoring CreateBase(
            RewardSourceOverrideAuthoringMode mode,
            string overrideId,
            string resultProfileId)
        {
            return new RewardSourceOverrideAuthoring
            {
                mode = mode,
                overrideId = overrideId ?? throw new ArgumentNullException(nameof(overrideId)),
                resultProfileId = resultProfileId ?? "reward-profile.override-result"
            };
        }

        private static RewardProfileV1 ReplaceWith(
            StableId overrideId,
            StableId sourceInstanceId,
            RewardProfileV1 replacement,
            RewardProfileV1 inheritedProfile)
        {
            return RewardSourceOverrideV1.ReplaceEntirely(
                overrideId,
                sourceInstanceId,
                replacement).Resolve(inheritedProfile);
        }

        private static List<RewardGrantSpecificationV1> BuildEntries(
            RewardGrantOverrideAuthoring[] entries,
            bool miscellaneousOnly)
        {
            List<RewardGrantSpecificationV1> result =
                new List<RewardGrantSpecificationV1>(entries == null ? 0 : entries.Length);
            if (entries != null)
            {
                for (int index = 0; index < entries.Length; index++)
                {
                    RewardGrantOverrideAuthoring entry = entries[index];
                    if (entry == null)
                    {
                        throw new InvalidOperationException(
                            $"Reward override contains a null grant at index {index}.");
                    }

                    if (miscellaneousOnly
                        && entry.Kind != RewardGrantKindV1.Miscellaneous
                        && entry.Kind != RewardGrantKindV1.PremiumAmmo)
                    {
                        throw new InvalidOperationException(
                            "Miscellaneous override entries must use Miscellaneous or PremiumAmmo kinds.");
                    }

                    result.Add(entry.Build());
                }
            }

            if (result.Count == 0)
            {
                throw new InvalidOperationException(
                    "The selected reward override mode requires at least one grant.");
            }

            return result;
        }

        private RewardProfileV1 BuildStrongboxRangeProfile()
        {
            if (minimumStrongboxTierOrder > maximumStrongboxTierOrder)
            {
                throw new InvalidOperationException(
                    "Strongbox tier range minimum must not exceed maximum.");
            }

            Dictionary<int, StrongboxTierOptionAuthoring> byOrder =
                new Dictionary<int, StrongboxTierOptionAuthoring>();
            if (strongboxTierOptions != null)
            {
                for (int index = 0; index < strongboxTierOptions.Length; index++)
                {
                    StrongboxTierOptionAuthoring option = strongboxTierOptions[index];
                    if (option == null)
                    {
                        throw new InvalidOperationException(
                            $"Strongbox tier range contains a null option at index {index}.");
                    }

                    if (byOrder.ContainsKey(option.TierOrder))
                    {
                        throw new InvalidOperationException(
                            $"Strongbox tier order {option.TierOrder} is duplicated.");
                    }

                    byOrder.Add(option.TierOrder, option);
                }
            }

            List<WeightedRewardOutcomeV1> outcomes =
                new List<WeightedRewardOutcomeV1>();
            for (int tier = minimumStrongboxTierOrder;
                tier <= maximumStrongboxTierOrder;
                tier++)
            {
                StrongboxTierOptionAuthoring option;
                if (!byOrder.TryGetValue(tier, out option))
                {
                    throw new InvalidOperationException(
                        $"Strongbox tier range is missing authored tier order {tier}.");
                }

                outcomes.Add(option.Build(strongboxGrantId));
                if (tier == int.MaxValue)
                {
                    break;
                }
            }

            return RewardProfileV1.Create(
                StableId.Parse(resultProfileId),
                Array.Empty<RewardGrantSpecificationV1>(),
                Array.Empty<IndependentRewardRollV1>(),
                new[]
                {
                    ExclusiveRewardGroupV1.Create(
                        StableId.Parse(strongboxRangeGroupId),
                        outcomes)
                });
        }
    }
}
