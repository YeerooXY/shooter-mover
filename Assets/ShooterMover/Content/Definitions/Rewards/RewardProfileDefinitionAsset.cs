using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using UnityEngine;

namespace ShooterMover.Content.Definitions.Rewards
{
    [Serializable]
    public sealed class RewardScalingInputAuthoring
    {
        [SerializeField] private string inputId = "reward-input.unassigned";
        [SerializeField] private RewardScalingInputKindV1 kind =
            RewardScalingInputKindV1.CharacterLevel;

        public RewardScalingInputAuthoring()
        {
        }

        public RewardScalingInputAuthoring(
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
    public sealed class RewardGrantAuthoring
    {
        [SerializeField] private string grantId = "reward-grant.unassigned";
        [SerializeField] private RewardGrantKindV1 kind = RewardGrantKindV1.Money;
        [SerializeField] private string contentId = "reward-content.unassigned";
        [SerializeField] private long minimumQuantity = 1L;
        [SerializeField] private long maximumQuantity = 1L;
        [SerializeField] private RewardScalingInputAuthoring[] scalingInputs =
            Array.Empty<RewardScalingInputAuthoring>();

        public RewardGrantAuthoring()
        {
        }

        public RewardGrantAuthoring(
            string grantId,
            RewardGrantKindV1 kind,
            string contentId,
            long minimumQuantity,
            long maximumQuantity,
            params RewardScalingInputAuthoring[] scalingInputs)
        {
            this.grantId = grantId ?? throw new ArgumentNullException(nameof(grantId));
            this.kind = kind;
            this.contentId = contentId ?? throw new ArgumentNullException(nameof(contentId));
            this.minimumQuantity = minimumQuantity;
            this.maximumQuantity = maximumQuantity;
            this.scalingInputs = scalingInputs ?? Array.Empty<RewardScalingInputAuthoring>();
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
                    RewardScalingInputAuthoring input = scalingInputs[index];
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
    public sealed class IndependentRewardRollAuthoring
    {
        [SerializeField] private string rollId = "reward-roll.unassigned";
        [SerializeField] private int probabilityMillionths = 1000000;
        [SerializeField] private RewardGrantAuthoring grant = new RewardGrantAuthoring();

        public IndependentRewardRollAuthoring()
        {
        }

        public IndependentRewardRollAuthoring(
            string rollId,
            int probabilityMillionths,
            RewardGrantAuthoring grant)
        {
            this.rollId = rollId ?? throw new ArgumentNullException(nameof(rollId));
            this.probabilityMillionths = probabilityMillionths;
            this.grant = grant ?? throw new ArgumentNullException(nameof(grant));
        }

        public IndependentRewardRollV1 Build()
        {
            if (grant == null)
            {
                throw new InvalidOperationException(
                    $"Independent reward roll '{rollId}' requires a grant.");
            }

            return IndependentRewardRollV1.Create(
                StableId.Parse(rollId),
                probabilityMillionths,
                grant.Build());
        }
    }

    [Serializable]
    public sealed class WeightedRewardOutcomeAuthoring
    {
        [SerializeField] private string outcomeId = "reward-outcome.unassigned";
        [SerializeField] private long weight = 1L;
        [SerializeField] private WeightedRewardOutcomeKindV1 kind =
            WeightedRewardOutcomeKindV1.Grant;
        [SerializeField] private RewardGrantAuthoring grant = new RewardGrantAuthoring();

        public WeightedRewardOutcomeAuthoring()
        {
        }

        private WeightedRewardOutcomeAuthoring(
            string outcomeId,
            long weight,
            WeightedRewardOutcomeKindV1 kind,
            RewardGrantAuthoring grant)
        {
            this.outcomeId = outcomeId ?? throw new ArgumentNullException(nameof(outcomeId));
            this.weight = weight;
            this.kind = kind;
            this.grant = grant;
        }

        public static WeightedRewardOutcomeAuthoring Grant(
            string outcomeId,
            long weight,
            RewardGrantAuthoring grant)
        {
            return new WeightedRewardOutcomeAuthoring(
                outcomeId,
                weight,
                WeightedRewardOutcomeKindV1.Grant,
                grant ?? throw new ArgumentNullException(nameof(grant)));
        }

        public static WeightedRewardOutcomeAuthoring NoDrop(
            string outcomeId,
            long weight)
        {
            return new WeightedRewardOutcomeAuthoring(
                outcomeId,
                weight,
                WeightedRewardOutcomeKindV1.ExplicitNoDrop,
                null);
        }

        public WeightedRewardOutcomeV1 Build()
        {
            if (kind == WeightedRewardOutcomeKindV1.Grant)
            {
                if (grant == null)
                {
                    throw new InvalidOperationException(
                        $"Weighted reward outcome '{outcomeId}' requires a grant.");
                }

                return WeightedRewardOutcomeV1.CreateGrant(
                    StableId.Parse(outcomeId),
                    weight,
                    grant.Build());
            }

            if (kind == WeightedRewardOutcomeKindV1.ExplicitNoDrop)
            {
                if (grant != null)
                {
                    throw new InvalidOperationException(
                        $"No-drop outcome '{outcomeId}' must not carry a grant.");
                }

                return WeightedRewardOutcomeV1.CreateExplicitNoDrop(
                    StableId.Parse(outcomeId),
                    weight);
            }

            throw new InvalidOperationException(
                $"Unsupported weighted reward outcome kind '{kind}'.");
        }
    }

    [Serializable]
    public sealed class ExclusiveRewardGroupAuthoring
    {
        [SerializeField] private string groupId = "reward-group.unassigned";
        [SerializeField] private WeightedRewardOutcomeAuthoring[] outcomes =
            Array.Empty<WeightedRewardOutcomeAuthoring>();

        public ExclusiveRewardGroupAuthoring()
        {
        }

        public ExclusiveRewardGroupAuthoring(
            string groupId,
            params WeightedRewardOutcomeAuthoring[] outcomes)
        {
            this.groupId = groupId ?? throw new ArgumentNullException(nameof(groupId));
            this.outcomes = outcomes ?? Array.Empty<WeightedRewardOutcomeAuthoring>();
        }

        public ExclusiveRewardGroupV1 Build()
        {
            List<WeightedRewardOutcomeV1> builtOutcomes =
                new List<WeightedRewardOutcomeV1>(
                    outcomes == null ? 0 : outcomes.Length);
            if (outcomes != null)
            {
                for (int index = 0; index < outcomes.Length; index++)
                {
                    WeightedRewardOutcomeAuthoring outcome = outcomes[index];
                    if (outcome == null)
                    {
                        throw new InvalidOperationException(
                            $"Exclusive reward group '{groupId}' contains a null outcome.");
                    }

                    builtOutcomes.Add(outcome.Build());
                }
            }

            return ExclusiveRewardGroupV1.Create(
                StableId.Parse(groupId),
                builtOutcomes);
        }
    }

    /// <summary>
    /// Immutable authored configuration for one reusable reward profile. The asset
    /// translates into the REW-001 model and into a generic capability snapshot so
    /// UnityAdapters can consume it without an outward assembly reference.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RewardProfileDefinition",
        menuName = "Shooter Mover/Rewards/Reward Profile Definition")]
    public sealed class RewardProfileDefinitionAsset :
        ScriptableObject,
        IObjectCapabilityDefinitionSource
    {
        public const string CapabilityIdText = "capability.reward-source-profile-v1";

        [SerializeField] private string profileId = "reward-profile.unassigned";
        [SerializeField] private bool explicitNoDrop;
        [SerializeField] private RewardGrantAuthoring[] guaranteedEntries =
            Array.Empty<RewardGrantAuthoring>();
        [SerializeField] private IndependentRewardRollAuthoring[] independentRolls =
            Array.Empty<IndependentRewardRollAuthoring>();
        [SerializeField] private ExclusiveRewardGroupAuthoring[] exclusiveGroups =
            Array.Empty<ExclusiveRewardGroupAuthoring>();

        public StableId CapabilityId
        {
            get { return StableId.Parse(CapabilityIdText); }
        }

        public RewardProfileV1 BuildProfile()
        {
            StableId parsedProfileId = StableId.Parse(profileId);
            if (explicitNoDrop)
            {
                if (Count(guaranteedEntries) != 0
                    || Count(independentRolls) != 0
                    || Count(exclusiveGroups) != 0)
                {
                    throw new InvalidOperationException(
                        "An explicit no-drop reward profile must not contain entries.");
                }

                return RewardProfileV1.CreateExplicitNoDrop(parsedProfileId);
            }

            List<RewardGrantSpecificationV1> builtGuaranteed =
                BuildAll(
                    guaranteedEntries,
                    "guaranteed reward entry",
                    delegate(RewardGrantAuthoring value) { return value.Build(); });
            List<IndependentRewardRollV1> builtIndependent =
                BuildAll(
                    independentRolls,
                    "independent reward roll",
                    delegate(IndependentRewardRollAuthoring value) { return value.Build(); });
            List<ExclusiveRewardGroupV1> builtExclusive =
                BuildAll(
                    exclusiveGroups,
                    "exclusive reward group",
                    delegate(ExclusiveRewardGroupAuthoring value) { return value.Build(); });

            return RewardProfileV1.Create(
                parsedProfileId,
                builtGuaranteed,
                builtIndependent,
                builtExclusive);
        }

        public CapabilityDefinition BuildDefinition()
        {
            return RewardProfileCapabilityWriter.Build(BuildProfile());
        }

        public void ValidateOrThrow()
        {
            BuildDefinition();
        }

        public static RewardProfileDefinitionAsset CreateRuntime(
            string profileId,
            bool explicitNoDrop,
            RewardGrantAuthoring[] guaranteedEntries,
            IndependentRewardRollAuthoring[] independentRolls,
            ExclusiveRewardGroupAuthoring[] exclusiveGroups)
        {
            RewardProfileDefinitionAsset asset =
                CreateInstance<RewardProfileDefinitionAsset>();
            asset.profileId = profileId ?? throw new ArgumentNullException(nameof(profileId));
            asset.explicitNoDrop = explicitNoDrop;
            asset.guaranteedEntries = guaranteedEntries ?? Array.Empty<RewardGrantAuthoring>();
            asset.independentRolls = independentRolls
                ?? Array.Empty<IndependentRewardRollAuthoring>();
            asset.exclusiveGroups = exclusiveGroups
                ?? Array.Empty<ExclusiveRewardGroupAuthoring>();
            asset.hideFlags = HideFlags.HideAndDontSave;
            asset.ValidateOrThrow();
            return asset;
        }

        private static int Count<T>(T[] values)
        {
            return values == null ? 0 : values.Length;
        }

        private static List<TResult> BuildAll<TSource, TResult>(
            TSource[] values,
            string label,
            Func<TSource, TResult> build)
            where TSource : class
        {
            List<TResult> result = new List<TResult>(Count(values));
            if (values == null)
            {
                return result;
            }

            for (int index = 0; index < values.Length; index++)
            {
                TSource value = values[index];
                if (value == null)
                {
                    throw new InvalidOperationException(
                        $"Reward profile contains a null {label} at index {index}.");
                }

                result.Add(build(value));
            }

            return result;
        }

        private void OnValidate()
        {
            if (guaranteedEntries == null)
            {
                guaranteedEntries = Array.Empty<RewardGrantAuthoring>();
            }

            if (independentRolls == null)
            {
                independentRolls = Array.Empty<IndependentRewardRollAuthoring>();
            }

            if (exclusiveGroups == null)
            {
                exclusiveGroups = Array.Empty<ExclusiveRewardGroupAuthoring>();
            }
        }
    }

    internal static class RewardProfileCapabilityWriter
    {
        private const string FieldNamespace = "reward-profile";

        public static CapabilityDefinition Build(RewardProfileV1 profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            List<CapabilityField> fields = new List<CapabilityField>();
            AddStableId(fields, "profile-id", profile.ProfileStableId);
            AddInteger(fields, "disposition", (int)profile.Disposition);
            AddText(fields, "profile-fingerprint", profile.Fingerprint);
            AddInteger(fields, "guaranteed-count", profile.GuaranteedEntries.Count);
            for (int index = 0; index < profile.GuaranteedEntries.Count; index++)
            {
                WriteGrant(
                    fields,
                    "guaranteed-" + Index(index),
                    profile.GuaranteedEntries[index]);
            }

            AddInteger(fields, "independent-count", profile.IndependentRolls.Count);
            for (int index = 0; index < profile.IndependentRolls.Count; index++)
            {
                IndependentRewardRollV1 roll = profile.IndependentRolls[index];
                string prefix = "independent-" + Index(index);
                AddStableId(fields, prefix + "-roll-id", roll.RollStableId);
                AddInteger(fields, prefix + "-probability", roll.ProbabilityMillionths);
                WriteGrant(fields, prefix + "-grant", roll.Grant);
            }

            AddInteger(fields, "exclusive-count", profile.ExclusiveGroups.Count);
            for (int groupIndex = 0;
                groupIndex < profile.ExclusiveGroups.Count;
                groupIndex++)
            {
                ExclusiveRewardGroupV1 group = profile.ExclusiveGroups[groupIndex];
                string groupPrefix = "exclusive-" + Index(groupIndex);
                AddStableId(fields, groupPrefix + "-group-id", group.GroupStableId);
                AddInteger(fields, groupPrefix + "-outcome-count", group.Outcomes.Count);
                for (int outcomeIndex = 0;
                    outcomeIndex < group.Outcomes.Count;
                    outcomeIndex++)
                {
                    WeightedRewardOutcomeV1 outcome = group.Outcomes[outcomeIndex];
                    string outcomePrefix = groupPrefix + "-outcome-" + Index(outcomeIndex);
                    AddStableId(fields, outcomePrefix + "-outcome-id", outcome.OutcomeStableId);
                    AddInteger(fields, outcomePrefix + "-weight", outcome.Weight);
                    AddInteger(fields, outcomePrefix + "-kind", (int)outcome.Kind);
                    if (outcome.Grant != null)
                    {
                        WriteGrant(fields, outcomePrefix + "-grant", outcome.Grant);
                    }
                }
            }

            return new CapabilityDefinition(
                StableId.Parse(RewardProfileDefinitionAsset.CapabilityIdText),
                fields);
        }

        private static void WriteGrant(
            List<CapabilityField> fields,
            string prefix,
            RewardGrantSpecificationV1 grant)
        {
            AddStableId(fields, prefix + "-grant-id", grant.GrantStableId);
            AddInteger(fields, prefix + "-kind", (int)grant.Kind);
            AddStableId(fields, prefix + "-content-id", grant.ContentStableId);
            AddInteger(fields, prefix + "-quantity-min", grant.Quantity.Minimum);
            AddInteger(fields, prefix + "-quantity-max", grant.Quantity.Maximum);
            AddInteger(fields, prefix + "-scaling-count", grant.ScalingInputs.Count);
            for (int index = 0; index < grant.ScalingInputs.Count; index++)
            {
                RewardScalingInputDescriptorV1 input = grant.ScalingInputs[index];
                string inputPrefix = prefix + "-scaling-" + Index(index);
                AddStableId(fields, inputPrefix + "-input-id", input.InputStableId);
                AddInteger(fields, inputPrefix + "-kind", (int)input.Kind);
            }
        }

        private static void AddStableId(
            List<CapabilityField> fields,
            string fieldValue,
            StableId value)
        {
            fields.Add(
                new CapabilityField(
                    StableId.Create(FieldNamespace, fieldValue),
                    CapabilityFieldValue.FromStableId(value)));
        }

        private static void AddInteger(
            List<CapabilityField> fields,
            string fieldValue,
            long value)
        {
            fields.Add(
                new CapabilityField(
                    StableId.Create(FieldNamespace, fieldValue),
                    CapabilityFieldValue.FromInteger(value)));
        }

        private static void AddText(
            List<CapabilityField> fields,
            string fieldValue,
            string value)
        {
            fields.Add(
                new CapabilityField(
                    StableId.Create(FieldNamespace, fieldValue),
                    CapabilityFieldValue.FromText(value)));
        }

        private static string Index(int value)
        {
            return value.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
