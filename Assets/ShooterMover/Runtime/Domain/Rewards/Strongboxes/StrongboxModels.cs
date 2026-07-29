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
    public static class Strongbox
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

    public sealed class StrongboxRewardCountPolicy : IEquatable<StrongboxRewardCountPolicy>
    {
        private readonly string canonicalText;

        private StrongboxRewardCountPolicy(int minimumGrantCount, int maximumGrantCount)
        {
            if (minimumGrantCount < 1) { throw new ArgumentOutOfRangeException(nameof(minimumGrantCount)); }
            if (maximumGrantCount < minimumGrantCount) { throw new ArgumentOutOfRangeException(nameof(maximumGrantCount)); }
            MinimumGrantCount = minimumGrantCount;
            MaximumGrantCount = maximumGrantCount;
            canonicalText = "minimum_grant_count=" + minimumGrantCount.ToString(CultureInfo.InvariantCulture)
                + "\nmaximum_grant_count=" + maximumGrantCount.ToString(CultureInfo.InvariantCulture);
            Fingerprint = Strongbox.Fingerprint(canonicalText);
        }

        public int MinimumGrantCount { get; }
        public int MaximumGrantCount { get; }
        public string Fingerprint { get; }

        public static StrongboxRewardCountPolicy Create(int minimumGrantCount, int maximumGrantCount)
        {
            return new StrongboxRewardCountPolicy(minimumGrantCount, maximumGrantCount);
        }

        public bool Accepts(int count) { return count >= MinimumGrantCount && count <= MaximumGrantCount; }
        public string ToCanonicalString() { return canonicalText; }
        public bool Equals(StrongboxRewardCountPolicy other)
        {
            return !ReferenceEquals(other, null) && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as StrongboxRewardCountPolicy); }
        public override int GetHashCode() { return Strongbox.DeterministicHash(canonicalText); }
    }

    public sealed class StrongboxMandatoryScrapPolicy : IEquatable<StrongboxMandatoryScrapPolicy>
    {
        private readonly string canonicalText;

        private StrongboxMandatoryScrapPolicy(
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
            Fingerprint = Strongbox.Fingerprint(canonicalText);
        }

        public StableId CurrencyStableId { get; }
        public long MinimumQuantity { get; }
        public long MaximumQuantity { get; }
        public string Fingerprint { get; }

        public static StrongboxMandatoryScrapPolicy Create(
            StableId currencyStableId,
            long minimumQuantity,
            long maximumQuantity)
        {
            return new StrongboxMandatoryScrapPolicy(currencyStableId, minimumQuantity, maximumQuantity);
        }

        public RewardGrantSpecification CreateGrant(StableId grantStableId)
        {
            return RewardGrantSpecification.Create(
                grantStableId,
                RewardGrantKind.Scrap,
                CurrencyStableId,
                RewardQuantityRange.Create(MinimumQuantity, MaximumQuantity),
                Array.Empty<RewardScalingInputDescriptor>());
        }

        public string ToCanonicalString() { return canonicalText; }
        public bool Equals(StrongboxMandatoryScrapPolicy other)
        {
            return !ReferenceEquals(other, null) && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as StrongboxMandatoryScrapPolicy); }
        public override int GetHashCode() { return Strongbox.DeterministicHash(canonicalText); }
    }

    public sealed class StrongboxDefinition : IEquatable<StrongboxDefinition>, IComparable<StrongboxDefinition>
    {
        private readonly string canonicalText;

        private StrongboxDefinition(
            StableId tierStableId,
            int displayOrder,
            long generationBias,
            long qualityBias,
            long exceptionalRollBias,
            StrongboxRewardCountPolicy rewardCountPolicy,
            StrongboxMandatoryScrapPolicy mandatoryScrapPolicy,
            StableId compatibleGenerationPolicyStableId,
            RewardProfile baseRewardProfile,
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
            Strongbox.AppendToken(builder, "tier_stable_id", TierStableId.ToString());
            Strongbox.AppendToken(builder, "display_order", DisplayOrder.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "generation_bias", GenerationBias.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "quality_bias", QualityBias.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "exceptional_roll_bias", ExceptionalRollBias.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "reward_count_policy", RewardCountPolicy.ToCanonicalString());
            Strongbox.AppendToken(builder, "mandatory_scrap_policy", MandatoryScrapPolicy.ToCanonicalString());
            Strongbox.AppendToken(builder, "compatible_generation_policy", CompatibleGenerationPolicyStableId.ToString());
            Strongbox.AppendToken(builder, "base_reward_profile", BaseRewardProfile.ToCanonicalString());
            Strongbox.AppendToken(builder, "tier_scaling_input", TierScalingInputStableId.ToString());
            Strongbox.AppendToken(builder, "exceptional_scaling_input", ExceptionalScalingInputStableId.ToString());
            canonicalText = builder.ToString();
            Fingerprint = Strongbox.Fingerprint(canonicalText);
        }

        public StableId TierStableId { get; }
        public int DisplayOrder { get; }
        public long GenerationBias { get; }
        public long QualityBias { get; }
        public long ExceptionalRollBias { get; }
        public StrongboxRewardCountPolicy RewardCountPolicy { get; }
        public StrongboxMandatoryScrapPolicy MandatoryScrapPolicy { get; }
        public StableId CompatibleGenerationPolicyStableId { get; }
        public RewardProfile BaseRewardProfile { get; }
        public StableId TierScalingInputStableId { get; }
        public StableId ExceptionalScalingInputStableId { get; }
        public string Fingerprint { get; }

        public static StrongboxDefinition Create(
            StableId tierStableId,
            int displayOrder,
            long generationBias,
            long qualityBias,
            long exceptionalRollBias,
            StrongboxRewardCountPolicy rewardCountPolicy,
            StrongboxMandatoryScrapPolicy mandatoryScrapPolicy,
            StableId compatibleGenerationPolicyStableId,
            RewardProfile baseRewardProfile,
            StableId tierScalingInputStableId,
            StableId exceptionalScalingInputStableId)
        {
            return new StrongboxDefinition(
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
        public int CompareTo(StrongboxDefinition other)
        {
            if (ReferenceEquals(other, null)) { return 1; }
            int order = DisplayOrder.CompareTo(other.DisplayOrder);
            return order != 0 ? order : TierStableId.CompareTo(other.TierStableId);
        }
        public bool Equals(StrongboxDefinition other)
        {
            return !ReferenceEquals(other, null) && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as StrongboxDefinition); }
        public override int GetHashCode() { return Strongbox.DeterministicHash(canonicalText); }
    }

    public sealed class StrongboxDefinitionCatalog
    {
        private readonly ReadOnlyCollection<StrongboxDefinition> definitions;
        private readonly Dictionary<StableId, StrongboxDefinition> byId;
        private readonly string canonicalText;

        public StrongboxDefinitionCatalog(IEnumerable<StrongboxDefinition> definitions)
        {
            if (definitions == null) { throw new ArgumentNullException(nameof(definitions)); }
            List<StrongboxDefinition> copy = new List<StrongboxDefinition>();
            byId = new Dictionary<StableId, StrongboxDefinition>();
            foreach (StrongboxDefinition definition in definitions)
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
            this.definitions = new ReadOnlyCollection<StrongboxDefinition>(copy);
            StringBuilder builder = new StringBuilder();
            Strongbox.AppendToken(builder, "definition_count", copy.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < copy.Count; index++)
            {
                Strongbox.AppendToken(builder, "definition_" + index.ToString("D4", CultureInfo.InvariantCulture), copy[index].ToCanonicalString());
            }
            canonicalText = builder.ToString();
            Fingerprint = Strongbox.Fingerprint(canonicalText);
        }

        public IReadOnlyList<StrongboxDefinition> Definitions { get { return definitions; } }
        public string Fingerprint { get; }
        public bool TryGet(StableId tierStableId, out StrongboxDefinition definition)
        {
            if (tierStableId == null) { definition = null; return false; }
            return byId.TryGetValue(tierStableId, out definition);
        }
        public string ToCanonicalString() { return canonicalText; }
    }

    public sealed class StrongboxInstanceContext : IEquatable<StrongboxInstanceContext>, IComparable<StrongboxInstanceContext>
    {
        private readonly string canonicalText;

        private StrongboxInstanceContext(
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
            if (algorithmContentFingerprint != null && !Strongbox.IsFingerprint(algorithmContentFingerprint))
            {
                throw new ArgumentException("Algorithm/content fingerprint must be canonical when supplied.", nameof(algorithmContentFingerprint));
            }
            AlgorithmContentFingerprint = algorithmContentFingerprint;

            StringBuilder builder = new StringBuilder();
            Strongbox.AppendToken(builder, "instance_stable_id", InstanceStableId.ToString());
            Strongbox.AppendToken(builder, "tier_stable_id", TierStableId.ToString());
            Strongbox.AppendToken(builder, "root_seed", RootSeed.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "algorithm_version", AlgorithmVersion.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "progression_context", ProgressionContext.ToCanonicalString());
            Strongbox.AppendToken(builder, "source_context_stable_id", SourceContextStableId.ToString());
            Strongbox.AppendToken(builder, "collection_provenance_stable_id", CollectionProvenanceStableId.ToString());
            Strongbox.AppendToken(builder, "algorithm_content_fingerprint", AlgorithmContentFingerprint ?? "none");
            canonicalText = builder.ToString();
            Fingerprint = Strongbox.Fingerprint(canonicalText);
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

        public static StrongboxInstanceContext Create(
            StableId instanceStableId,
            StableId tierStableId,
            ulong rootSeed,
            int algorithmVersion,
            ProgressionContext progressionContext,
            StableId sourceContextStableId,
            StableId collectionProvenanceStableId,
            string algorithmContentFingerprint = null)
        {
            return new StrongboxInstanceContext(
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
        public int CompareTo(StrongboxInstanceContext other)
        {
            return ReferenceEquals(other, null) ? 1 : InstanceStableId.CompareTo(other.InstanceStableId);
        }
        public bool Equals(StrongboxInstanceContext other)
        {
            return !ReferenceEquals(other, null) && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as StrongboxInstanceContext); }
        public override int GetHashCode() { return Strongbox.DeterministicHash(canonicalText); }
    }
}
