using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.Sources
{
    public enum LootSourceOverrideAuthoringMode
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
        [SerializeField] private RewardScalingInputKind kind =
            RewardScalingInputKind.CharacterLevel;

        public RewardScalingInputOverrideAuthoring()
        {
        }

        public RewardScalingInputOverrideAuthoring(
            string inputId,
            RewardScalingInputKind kind)
        {
            this.inputId = inputId ?? throw new ArgumentNullException(nameof(inputId));
            this.kind = kind;
        }

        public RewardScalingInputDescriptor Build()
        {
            return RewardScalingInputDescriptor.Create(
                StableId.Parse(inputId),
                kind);
        }
    }

    [Serializable]
    public sealed class RewardGrantOverrideAuthoring
    {
        [SerializeField] private string grantId = "reward-grant.unassigned";
        [SerializeField] private RewardGrantKind kind = RewardGrantKind.Miscellaneous;
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
            RewardGrantKind kind,
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

        public RewardGrantKind Kind
        {
            get { return kind; }
        }

        public RewardGrantSpecification Build()
        {
            List<RewardScalingInputDescriptor> builtInputs =
                new List<RewardScalingInputDescriptor>(
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

            return RewardGrantSpecification.Create(
                StableId.Parse(grantId),
                kind,
                StableId.Parse(contentId),
                RewardQuantityRange.Create(minimumQuantity, maximumQuantity),
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

        public WeightedRewardOutcome Build(string grantId)
        {
            StableId baseGrantId = StableId.Parse(grantId);
            StableId tierGrantId = StableId.Create(
                baseGrantId.Namespace,
                baseGrantId.Value
                    + "-tier-"
                    + tierOrder.ToString(CultureInfo.InvariantCulture));
            return WeightedRewardOutcome.CreateGrant(
                StableId.Parse(outcomeId),
                weight,
                RewardGrantSpecification.CreateFixed(
                    tierGrantId,
                    RewardGrantKind.Strongbox,
                    StableId.Parse(tierContentId),
                    1L));
        }
    }

    [Serializable]
    public sealed class LootSourceOverrideAuthoring
    {
        [SerializeField] private LootSourceOverrideAuthoringMode mode =
            LootSourceOverrideAuthoringMode.Inherit;
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

        public LootSourceOverrideAuthoringMode Mode
        {
            get { return mode; }
        }

        public static LootSourceOverrideAuthoring Inherit(string overrideId)
        {
            return CreateBase(LootSourceOverrideAuthoringMode.Inherit, overrideId, null);
        }

        public static LootSourceOverrideAuthoring None(
            string overrideId,
            string resultProfileId)
        {
            return CreateBase(
                LootSourceOverrideAuthoringMode.None,
                overrideId,
                resultProfileId);
        }

        public static LootSourceOverrideAuthoring Replace(
            string overrideId,
            ScriptableObject replacementProfileSource)
        {
            LootSourceOverrideAuthoring value = CreateBase(
                LootSourceOverrideAuthoringMode.Replace,
                overrideId,
                null);
            value.replacementProfileSource = replacementProfileSource
                ?? throw new ArgumentNullException(nameof(replacementProfileSource));
            return value;
        }

        public static LootSourceOverrideAuthoring AppendGuaranteed(
            string overrideId,
            string resultProfileId,
            params RewardGrantOverrideAuthoring[] entries)
        {
            LootSourceOverrideAuthoring value = CreateBase(
                LootSourceOverrideAuthoringMode.AppendGuaranteed,
                overrideId,
                resultProfileId);
            value.guaranteedEntries = entries ?? Array.Empty<RewardGrantOverrideAuthoring>();
            return value;
        }

        public static LootSourceOverrideAuthoring MoneyOnly(
            string overrideId,
            string resultProfileId,
            string grantId,
            string contentId,
            long minimum,
            long maximum)
        {
            LootSourceOverrideAuthoring value = CreateBase(
                LootSourceOverrideAuthoringMode.MoneyOnly,
                overrideId,
                resultProfileId);
            value.moneyGrantId = grantId ?? throw new ArgumentNullException(nameof(grantId));
            value.moneyContentId = contentId ?? throw new ArgumentNullException(nameof(contentId));
            value.moneyMinimum = minimum;
            value.moneyMaximum = maximum;
            return value;
        }

        public static LootSourceOverrideAuthoring StrongboxExactTier(
            string overrideId,
            string resultProfileId,
            string grantId,
            string tierContentId)
        {
            LootSourceOverrideAuthoring value = CreateBase(
                LootSourceOverrideAuthoringMode.StrongboxExactTier,
                overrideId,
                resultProfileId);
            value.strongboxGrantId = grantId ?? throw new ArgumentNullException(nameof(grantId));
            value.exactStrongboxTierId = tierContentId
                ?? throw new ArgumentNullException(nameof(tierContentId));
            return value;
        }

        public static LootSourceOverrideAuthoring StrongboxTierRange(
            string overrideId,
            string resultProfileId,
            string groupId,
            string grantId,
            int minimumTierOrder,
            int maximumTierOrder,
            params StrongboxTierOptionAuthoring[] options)
        {
            LootSourceOverrideAuthoring value = CreateBase(
                LootSourceOverrideAuthoringMode.StrongboxTierRange,
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

        public static LootSourceOverrideAuthoring Miscellaneous(
            string overrideId,
            string resultProfileId,
            params RewardGrantOverrideAuthoring[] entries)
        {
            LootSourceOverrideAuthoring value = CreateBase(
                LootSourceOverrideAuthoringMode.Miscellaneous,
                overrideId,
                resultProfileId);
            value.guaranteedEntries = entries ?? Array.Empty<RewardGrantOverrideAuthoring>();
            return value;
        }

        public RewardProfile Resolve(
            StableId sourceInstanceId,
            RewardProfile inheritedProfile)
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
                case LootSourceOverrideAuthoringMode.Inherit:
                    return LootSourceOverride.Inherit(
                        parsedOverrideId,
                        sourceInstanceId).Resolve(inheritedProfile);
                case LootSourceOverrideAuthoringMode.None:
                    return LootSourceOverride.NoReward(
                        parsedOverrideId,
                        sourceInstanceId,
                        StableId.Parse(resultProfileId)).Resolve(inheritedProfile);
                case LootSourceOverrideAuthoringMode.Replace:
                    return LootSourceOverride.ReplaceEntirely(
                        parsedOverrideId,
                        sourceInstanceId,
                        RewardProfileCapabilityReader.BuildProfile(
                            replacementProfileSource)).Resolve(inheritedProfile);
                case LootSourceOverrideAuthoringMode.AppendGuaranteed:
                    return LootSourceOverride.AppendGuaranteedEntries(
                        parsedOverrideId,
                        sourceInstanceId,
                        StableId.Parse(resultProfileId),
                        BuildEntries(guaranteedEntries, false)).Resolve(inheritedProfile);
                case LootSourceOverrideAuthoringMode.MoneyOnly:
                    return ReplaceWith(
                        parsedOverrideId,
                        sourceInstanceId,
                        RewardProfile.Create(
                            StableId.Parse(resultProfileId),
                            new[]
                            {
                                RewardGrantSpecification.Create(
                                    StableId.Parse(moneyGrantId),
                                    RewardGrantKind.Money,
                                    StableId.Parse(moneyContentId),
                                    RewardQuantityRange.Create(
                                        moneyMinimum,
                                        moneyMaximum),
                                    Array.Empty<RewardScalingInputDescriptor>())
                            },
                            Array.Empty<IndependentRewardRoll>(),
                            Array.Empty<ExclusiveRewardGroup>()),
                        inheritedProfile);
                case LootSourceOverrideAuthoringMode.StrongboxExactTier:
                    return ReplaceWith(
                        parsedOverrideId,
                        sourceInstanceId,
                        RewardProfile.Create(
                            StableId.Parse(resultProfileId),
                            new[]
                            {
                                RewardGrantSpecification.CreateFixed(
                                    StableId.Parse(strongboxGrantId),
                                    RewardGrantKind.Strongbox,
                                    StableId.Parse(exactStrongboxTierId),
                                    1L)
                            },
                            Array.Empty<IndependentRewardRoll>(),
                            Array.Empty<ExclusiveRewardGroup>()),
                        inheritedProfile);
                case LootSourceOverrideAuthoringMode.StrongboxTierRange:
                    return ReplaceWith(
                        parsedOverrideId,
                        sourceInstanceId,
                        BuildStrongboxRangeProfile(),
                        inheritedProfile);
                case LootSourceOverrideAuthoringMode.Miscellaneous:
                    return ReplaceWith(
                        parsedOverrideId,
                        sourceInstanceId,
                        RewardProfile.Create(
                            StableId.Parse(resultProfileId),
                            BuildEntries(guaranteedEntries, true),
                            Array.Empty<IndependentRewardRoll>(),
                            Array.Empty<ExclusiveRewardGroup>()),
                        inheritedProfile);
                default:
                    throw new InvalidOperationException(
                        $"Unsupported reward source override mode '{mode}'.");
            }
        }

        private static LootSourceOverrideAuthoring CreateBase(
            LootSourceOverrideAuthoringMode mode,
            string overrideId,
            string resultProfileId)
        {
            return new LootSourceOverrideAuthoring
            {
                mode = mode,
                overrideId = overrideId ?? throw new ArgumentNullException(nameof(overrideId)),
                resultProfileId = resultProfileId ?? "reward-profile.override-result"
            };
        }

        private static RewardProfile ReplaceWith(
            StableId overrideId,
            StableId sourceInstanceId,
            RewardProfile replacement,
            RewardProfile inheritedProfile)
        {
            return LootSourceOverride.ReplaceEntirely(
                overrideId,
                sourceInstanceId,
                replacement).Resolve(inheritedProfile);
        }

        private static List<RewardGrantSpecification> BuildEntries(
            RewardGrantOverrideAuthoring[] entries,
            bool miscellaneousOnly)
        {
            List<RewardGrantSpecification> result =
                new List<RewardGrantSpecification>(entries == null ? 0 : entries.Length);
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
                        && entry.Kind != RewardGrantKind.Miscellaneous
                        && entry.Kind != RewardGrantKind.PremiumAmmo)
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

        private RewardProfile BuildStrongboxRangeProfile()
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

            List<WeightedRewardOutcome> outcomes =
                new List<WeightedRewardOutcome>();
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

            return RewardProfile.Create(
                StableId.Parse(resultProfileId),
                Array.Empty<RewardGrantSpecification>(),
                Array.Empty<IndependentRewardRoll>(),
                new[]
                {
                    ExclusiveRewardGroup.Create(
                        StableId.Parse(strongboxRangeGroupId),
                        outcomes)
                });
        }
    }
}
