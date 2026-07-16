using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Domain.Rewards.Strongboxes
{
    public static class StrongboxCanonicalV1
    {
        public static void AppendToken(StringBuilder builder, string name, string value)
        {
            if (builder == null) { throw new ArgumentNullException(nameof(builder)); }
            if (name == null) { throw new ArgumentNullException(nameof(name)); }
            string text = value ?? "null";
            builder.Append(name).Append("_length=")
                .Append(text.Length.ToString(CultureInfo.InvariantCulture))
                .Append('\n').Append(name).Append('=').Append(text).Append('\n');
        }

        public static string Fingerprint(string canonicalText)
        {
            if (canonicalText == null) { throw new ArgumentNullException(nameof(canonicalText)); }
            byte[] digest;
            using (SHA256 algorithm = SHA256.Create())
            {
                digest = algorithm.ComputeHash(Encoding.UTF8.GetBytes(canonicalText));
            }

            StringBuilder builder = new StringBuilder("sha256:", 71);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static bool IsFingerprint(string value)
        {
            if (value == null || value.Length != 71 || !value.StartsWith("sha256:", StringComparison.Ordinal))
            {
                return false;
            }

            for (int index = 7; index < value.Length; index++)
            {
                char current = value[index];
                if (!((current >= '0' && current <= '9') || (current >= 'a' && current <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }

        public static StableId DeriveId(string namespaceName, params string[] parts)
        {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < parts.Length; index++)
            {
                AppendToken(builder, "part_" + index.ToString("D4", CultureInfo.InvariantCulture), parts[index]);
            }

            string fingerprint = Fingerprint(builder.ToString());
            return StableId.Create(namespaceName, fingerprint.Substring(7, 48));
        }

        public static int DeterministicHash(string canonicalText)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < canonicalText.Length; index++)
                {
                    hash ^= canonicalText[index];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }
    }

    public sealed class StrongboxRewardCountPolicyV1 : IEquatable<StrongboxRewardCountPolicyV1>
    {
        private readonly string canonicalText;

        private StrongboxRewardCountPolicyV1(int minimumGrantCount, int maximumGrantCount)
        {
            if (minimumGrantCount < 1) { throw new ArgumentOutOfRangeException(nameof(minimumGrantCount)); }
            if (maximumGrantCount < minimumGrantCount) { throw new ArgumentOutOfRangeException(nameof(maximumGrantCount)); }
            MinimumGrantCount = minimumGrantCount;
            MaximumGrantCount = maximumGrantCount;
            canonicalText = "minimum_grant_count=" + minimumGrantCount.ToString(CultureInfo.InvariantCulture)
                + "\nmaximum_grant_count=" + maximumGrantCount.ToString(CultureInfo.InvariantCulture);
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public int MinimumGrantCount { get; }
        public int MaximumGrantCount { get; }
        public string Fingerprint { get; }

        public static StrongboxRewardCountPolicyV1 Create(int minimumGrantCount, int maximumGrantCount)
        {
            return new StrongboxRewardCountPolicyV1(minimumGrantCount, maximumGrantCount);
        }

        public bool Accepts(int count) { return count >= MinimumGrantCount && count <= MaximumGrantCount; }
        public string ToCanonicalString() { return canonicalText; }
        public bool Equals(StrongboxRewardCountPolicyV1 other)
        {
            return !ReferenceEquals(other, null) && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as StrongboxRewardCountPolicyV1); }
        public override int GetHashCode() { return StrongboxCanonicalV1.DeterministicHash(canonicalText); }
    }

    public sealed class StrongboxMandatoryScrapPolicyV1 : IEquatable<StrongboxMandatoryScrapPolicyV1>
    {
        private readonly string canonicalText;

        private StrongboxMandatoryScrapPolicyV1(
            StableId currencyStableId,
            long minimumQuantity,
            long maximumQuantity)
        {
            CurrencyStableId = currencyStableId ?? throw new ArgumentNullException(nameof(currencyStableId));
            if (minimumQuantity < 1L) { throw new ArgumentOutOfRangeException(nameof(minimumQuantity)); }
            if (maximumQuantity < minimumQuantity) { throw new ArgumentOutOfRangeException(nameof(maximumQuantity)); }
            MinimumQuantity = minimumQuantity;
            MaximumQuantity = maximumQuantity;
            canonicalText = "currency_stable_id=" + CurrencyStableId
                + "\nminimum_quantity=" + minimumQuantity.ToString(CultureInfo.InvariantCulture)
                + "\nmaximum_quantity=" + maximumQuantity.ToString(CultureInfo.InvariantCulture);
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId CurrencyStableId { get; }
        public long MinimumQuantity { get; }
        public long MaximumQuantity { get; }
        public string Fingerprint { get; }

        public static StrongboxMandatoryScrapPolicyV1 Create(
            StableId currencyStableId,
            long minimumQuantity,
            long maximumQuantity)
        {
            return new StrongboxMandatoryScrapPolicyV1(currencyStableId, minimumQuantity, maximumQuantity);
        }

        public RewardGrantSpecificationV1 CreateGrant(StableId grantStableId)
        {
            return RewardGrantSpecificationV1.Create(
                grantStableId,
                RewardGrantKindV1.Scrap,
                CurrencyStableId,
                RewardQuantityRangeV1.Create(MinimumQuantity, MaximumQuantity),
                Array.Empty<RewardScalingInputDescriptorV1>());
        }

        public string ToCanonicalString() { return canonicalText; }
        public bool Equals(StrongboxMandatoryScrapPolicyV1 other)
        {
            return !ReferenceEquals(other, null) && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as StrongboxMandatoryScrapPolicyV1); }
        public override int GetHashCode() { return StrongboxCanonicalV1.DeterministicHash(canonicalText); }
    }

    public sealed class StrongboxDefinitionV1 : IEquatable<StrongboxDefinitionV1>, IComparable<StrongboxDefinitionV1>
    {
        private readonly string canonicalText;

        private StrongboxDefinitionV1(
            StableId tierStableId,
            int displayOrder,
            long generationBias,
            long qualityBias,
            long exceptionalRollBias,
            StrongboxRewardCountPolicyV1 rewardCountPolicy,
            StrongboxMandatoryScrapPolicyV1 mandatoryScrapPolicy,
            StableId compatibleGenerationPolicyStableId,
            RewardProfileV1 baseRewardProfile,
            StableId tierScalingInputStableId,
            StableId exceptionalScalingInputStableId)
        {
            TierStableId = tierStableId ?? throw new ArgumentNullException(nameof(tierStableId));
            if (displayOrder < 0) { throw new ArgumentOutOfRangeException(nameof(displayOrder)); }
            if (generationBias < 1L) { throw new ArgumentOutOfRangeException(nameof(generationBias)); }
            if (qualityBias < 1L) { throw new ArgumentOutOfRangeException(nameof(qualityBias)); }
            if (exceptionalRollBias < 0L) { throw new ArgumentOutOfRangeException(nameof(exceptionalRollBias)); }
            DisplayOrder = displayOrder;
            GenerationBias = generationBias;
            QualityBias = qualityBias;
            ExceptionalRollBias = exceptionalRollBias;
            RewardCountPolicy = rewardCountPolicy ?? throw new ArgumentNullException(nameof(rewardCountPolicy));
            MandatoryScrapPolicy = mandatoryScrapPolicy ?? throw new ArgumentNullException(nameof(mandatoryScrapPolicy));
            CompatibleGenerationPolicyStableId = compatibleGenerationPolicyStableId
                ?? throw new ArgumentNullException(nameof(compatibleGenerationPolicyStableId));
            BaseRewardProfile = baseRewardProfile ?? throw new ArgumentNullException(nameof(baseRewardProfile));
            TierScalingInputStableId = tierScalingInputStableId
                ?? throw new ArgumentNullException(nameof(tierScalingInputStableId));
            ExceptionalScalingInputStableId = exceptionalScalingInputStableId
                ?? throw new ArgumentNullException(nameof(exceptionalScalingInputStableId));
            if (TierScalingInputStableId == ExceptionalScalingInputStableId)
            {
                throw new ArgumentException("Tier and exceptional scaling identities must be distinct.");
            }

            StringBuilder builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "tier_stable_id", TierStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "display_order", DisplayOrder.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "generation_bias", GenerationBias.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "quality_bias", QualityBias.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "exceptional_roll_bias", ExceptionalRollBias.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "reward_count_policy", RewardCountPolicy.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "mandatory_scrap_policy", MandatoryScrapPolicy.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "compatible_generation_policy", CompatibleGenerationPolicyStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "base_reward_profile", BaseRewardProfile.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "tier_scaling_input", TierScalingInputStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "exceptional_scaling_input", ExceptionalScalingInputStableId.ToString());
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId TierStableId { get; }
        public int DisplayOrder { get; }
        public long GenerationBias { get; }
        public long QualityBias { get; }
        public long ExceptionalRollBias { get; }
        public StrongboxRewardCountPolicyV1 RewardCountPolicy { get; }
        public StrongboxMandatoryScrapPolicyV1 MandatoryScrapPolicy { get; }
        public StableId CompatibleGenerationPolicyStableId { get; }
        public RewardProfileV1 BaseRewardProfile { get; }
        public StableId TierScalingInputStableId { get; }
        public StableId ExceptionalScalingInputStableId { get; }
        public string Fingerprint { get; }

        public static StrongboxDefinitionV1 Create(
            StableId tierStableId,
            int displayOrder,
            long generationBias,
            long qualityBias,
            long exceptionalRollBias,
            StrongboxRewardCountPolicyV1 rewardCountPolicy,
            StrongboxMandatoryScrapPolicyV1 mandatoryScrapPolicy,
            StableId compatibleGenerationPolicyStableId,
            RewardProfileV1 baseRewardProfile,
            StableId tierScalingInputStableId,
            StableId exceptionalScalingInputStableId)
        {
            return new StrongboxDefinitionV1(
                tierStableId,
                displayOrder,
                generationBias,
                qualityBias,
                exceptionalRollBias,
                rewardCountPolicy,
                mandatoryScrapPolicy,
                compatibleGenerationPolicyStableId,
                baseRewardProfile,
                tierScalingInputStableId,
                exceptionalScalingInputStableId);
        }

        public string ToCanonicalString() { return canonicalText; }
        public int CompareTo(StrongboxDefinitionV1 other)
        {
            if (ReferenceEquals(other, null)) { return 1; }
            int order = DisplayOrder.CompareTo(other.DisplayOrder);
            return order != 0 ? order : TierStableId.CompareTo(other.TierStableId);
        }
        public bool Equals(StrongboxDefinitionV1 other)
        {
            return !ReferenceEquals(other, null) && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as StrongboxDefinitionV1); }
        public override int GetHashCode() { return StrongboxCanonicalV1.DeterministicHash(canonicalText); }
    }

    public sealed class StrongboxDefinitionCatalogV1
    {
        private readonly ReadOnlyCollection<StrongboxDefinitionV1> definitions;
        private readonly Dictionary<StableId, StrongboxDefinitionV1> byId;
        private readonly string canonicalText;

        public StrongboxDefinitionCatalogV1(IEnumerable<StrongboxDefinitionV1> definitions)
        {
            if (definitions == null) { throw new ArgumentNullException(nameof(definitions)); }
            List<StrongboxDefinitionV1> copy = new List<StrongboxDefinitionV1>();
            byId = new Dictionary<StableId, StrongboxDefinitionV1>();
            foreach (StrongboxDefinitionV1 definition in definitions)
            {
                if (definition == null) { throw new ArgumentException("Definitions must not contain null entries.", nameof(definitions)); }
                if (byId.ContainsKey(definition.TierStableId))
                {
                    throw new ArgumentException("Duplicate strongbox tier identity " + definition.TierStableId + ".", nameof(definitions));
                }
                byId.Add(definition.TierStableId, definition);
                copy.Add(definition);
            }
            if (copy.Count == 0) { throw new ArgumentException("At least one strongbox definition is required.", nameof(definitions)); }
            copy.Sort();
            this.definitions = new ReadOnlyCollection<StrongboxDefinitionV1>(copy);
            StringBuilder builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "definition_count", copy.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < copy.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(builder, "definition_" + index.ToString("D4", CultureInfo.InvariantCulture), copy[index].ToCanonicalString());
            }
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public IReadOnlyList<StrongboxDefinitionV1> Definitions { get { return definitions; } }
        public string Fingerprint { get; }
        public bool TryGet(StableId tierStableId, out StrongboxDefinitionV1 definition)
        {
            if (tierStableId == null) { definition = null; return false; }
            return byId.TryGetValue(tierStableId, out definition);
        }
        public string ToCanonicalString() { return canonicalText; }
    }

    public sealed class StrongboxInstanceContextV1 : IEquatable<StrongboxInstanceContextV1>, IComparable<StrongboxInstanceContextV1>
    {
        private readonly string canonicalText;

        private StrongboxInstanceContextV1(
            StableId instanceStableId,
            StableId tierStableId,
            ulong rootSeed,
            int algorithmVersion,
            ProgressionContext progressionContext,
            StableId sourceContextStableId,
            StableId collectionProvenanceStableId,
            string algorithmContentFingerprint)
        {
            InstanceStableId = instanceStableId ?? throw new ArgumentNullException(nameof(instanceStableId));
            TierStableId = tierStableId ?? throw new ArgumentNullException(nameof(tierStableId));
            if (algorithmVersion < 1) { throw new ArgumentOutOfRangeException(nameof(algorithmVersion)); }
            RootSeed = rootSeed;
            AlgorithmVersion = algorithmVersion;
            ProgressionContext = progressionContext ?? throw new ArgumentNullException(nameof(progressionContext));
            SourceContextStableId = sourceContextStableId ?? throw new ArgumentNullException(nameof(sourceContextStableId));
            CollectionProvenanceStableId = collectionProvenanceStableId ?? throw new ArgumentNullException(nameof(collectionProvenanceStableId));
            if (algorithmContentFingerprint != null && !StrongboxCanonicalV1.IsFingerprint(algorithmContentFingerprint))
            {
                throw new ArgumentException("Algorithm/content fingerprint must be canonical when supplied.", nameof(algorithmContentFingerprint));
            }
            AlgorithmContentFingerprint = algorithmContentFingerprint;

            StringBuilder builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "instance_stable_id", InstanceStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "tier_stable_id", TierStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "root_seed", RootSeed.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "algorithm_version", AlgorithmVersion.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "progression_context", ProgressionContext.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "source_context_stable_id", SourceContextStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "collection_provenance_stable_id", CollectionProvenanceStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "algorithm_content_fingerprint", AlgorithmContentFingerprint ?? "none");
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId InstanceStableId { get; }
        public StableId TierStableId { get; }
        public ulong RootSeed { get; }
        public int AlgorithmVersion { get; }
        public ProgressionContext ProgressionContext { get; }
        public StableId SourceContextStableId { get; }
        public StableId CollectionProvenanceStableId { get; }
        public string AlgorithmContentFingerprint { get; }
        public string Fingerprint { get; }

        public static StrongboxInstanceContextV1 Create(
            StableId instanceStableId,
            StableId tierStableId,
            ulong rootSeed,
            int algorithmVersion,
            ProgressionContext progressionContext,
            StableId sourceContextStableId,
            StableId collectionProvenanceStableId,
            string algorithmContentFingerprint = null)
        {
            return new StrongboxInstanceContextV1(
                instanceStableId,
                tierStableId,
                rootSeed,
                algorithmVersion,
                progressionContext,
                sourceContextStableId,
                collectionProvenanceStableId,
                algorithmContentFingerprint);
        }

        public string ToCanonicalString() { return canonicalText; }
        public int CompareTo(StrongboxInstanceContextV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : InstanceStableId.CompareTo(other.InstanceStableId);
        }
        public bool Equals(StrongboxInstanceContextV1 other)
        {
            return !ReferenceEquals(other, null) && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as StrongboxInstanceContextV1); }
        public override int GetHashCode() { return StrongboxCanonicalV1.DeterministicHash(canonicalText); }
    }
}
