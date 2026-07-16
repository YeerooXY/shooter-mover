using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Progression.Curves;

namespace ShooterMover.Domain.Crafting
{
    public enum CraftingQualityPolicyKindV1
    {
        Fixed = 1,
        DeterministicWeightedRandom = 2,
    }

    public sealed class CraftingDelayVarianceV1 : IEquatable<CraftingDelayVarianceV1>
    {
        public CraftingDelayVarianceV1(int minimumAdditionalLevels, int maximumAdditionalLevels)
        {
            if (minimumAdditionalLevels < 0 || maximumAdditionalLevels < minimumAdditionalLevels)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumAdditionalLevels));
            }

            MinimumAdditionalLevels = minimumAdditionalLevels;
            MaximumAdditionalLevels = maximumAdditionalLevels;
        }

        public int MinimumAdditionalLevels { get; }
        public int MaximumAdditionalLevels { get; }
        public bool IsFixed { get { return MinimumAdditionalLevels == MaximumAdditionalLevels; } }

        public string ToCanonicalString()
        {
            return MinimumAdditionalLevels.ToString(CultureInfo.InvariantCulture)
                + ".."
                + MaximumAdditionalLevels.ToString(CultureInfo.InvariantCulture);
        }

        public bool Equals(CraftingDelayVarianceV1 other)
        {
            return !ReferenceEquals(other, null)
                && MinimumAdditionalLevels == other.MinimumAdditionalLevels
                && MaximumAdditionalLevels == other.MaximumAdditionalLevels;
        }

        public override bool Equals(object obj) { return Equals(obj as CraftingDelayVarianceV1); }
        public override int GetHashCode() { return CraftingCanonicalV1.DeterministicHash(ToCanonicalString()); }
    }

    public sealed class CraftingWeightedDefinitionV1 : IComparable<CraftingWeightedDefinitionV1>
    {
        public CraftingWeightedDefinitionV1(StableId definitionStableId, ulong weight)
        {
            DefinitionStableId = definitionStableId ?? throw new ArgumentNullException(nameof(definitionStableId));
            if (weight == 0UL || weight > long.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(weight));
            }

            Weight = weight;
        }

        public StableId DefinitionStableId { get; }
        public ulong Weight { get; }

        public int CompareTo(CraftingWeightedDefinitionV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : DefinitionStableId.CompareTo(other.DefinitionStableId);
        }

        public string ToCanonicalString()
        {
            return "definition_id=" + DefinitionStableId
                + "\nweight=" + Weight.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class CraftingGeneratorPolicyV1
    {
        public CraftingGeneratorPolicyV1(
            StableId policyStableId,
            int algorithmVersion,
            SoftActivationCurveParameters activation,
            ObsolescenceCurveParameters obsolescence)
        {
            PolicyStableId = policyStableId ?? throw new ArgumentNullException(nameof(policyStableId));
            DeterministicRandom.Create(0UL, algorithmVersion);
            AlgorithmVersion = algorithmVersion;
            Activation = activation ?? throw new ArgumentNullException(nameof(activation));
            Obsolescence = obsolescence ?? throw new ArgumentNullException(nameof(obsolescence));
        }

        public StableId PolicyStableId { get; }
        public int AlgorithmVersion { get; }
        public SoftActivationCurveParameters Activation { get; }
        public ObsolescenceCurveParameters Obsolescence { get; }

        public string ToCanonicalString()
        {
            return "policy_id=" + PolicyStableId
                + "\nalgorithm_version=" + AlgorithmVersion.ToString(CultureInfo.InvariantCulture)
                + "\nactivation=" + Activation.EarlyTailWeight.ToString("R", CultureInfo.InvariantCulture)
                + "|" + Activation.EarlyTailLevels.ToString(CultureInfo.InvariantCulture)
                + "|" + Activation.PostNominalActivationLevels.ToString(CultureInfo.InvariantCulture)
                + "\nobsolescence=" + Obsolescence.DecayStartsAfterLevels.ToString(CultureInfo.InvariantCulture)
                + "|" + Obsolescence.HalfLifeLevels.ToString("R", CultureInfo.InvariantCulture)
                + "|" + Obsolescence.MinimumRetention.ToString("R", CultureInfo.InvariantCulture);
        }
    }

    public sealed class CraftingRecipeV1 : IComparable<CraftingRecipeV1>, IEquatable<CraftingRecipeV1>
    {
        private static readonly StableId UnlockDelayPurpose = StableId.Create("crafting-rng", "unlock-delay");
        private readonly ReadOnlyCollection<CraftingWeightedDefinitionV1> qualityOptions;
        private readonly ReadOnlyCollection<CraftingWeightedDefinitionV1> augmentOptions;
        private readonly string canonicalText;

        public CraftingRecipeV1(
            int version,
            StableId recipeStableId,
            StableId targetEquipmentDefinitionStableId,
            StableId naturalDiscoverySourceStableId,
            int naturalDiscoveryLevel,
            int ordinaryDiscoveryActivationLevel,
            int craftingDelayLevels,
            CraftingDelayVarianceV1 delayVariance,
            long scrapCost,
            CraftingQualityPolicyKindV1 qualityPolicyKind,
            IEnumerable<CraftingWeightedDefinitionV1> qualityOptions,
            int minimumItemLevel,
            int maximumItemLevel,
            int minimumAugmentSlots,
            int maximumAugmentSlots,
            int maximumAugmentTier,
            int maximumAugmentLevel,
            IEnumerable<CraftingWeightedDefinitionV1> augmentOptions,
            CraftingGeneratorPolicyV1 generatorPolicy)
        {
            if (version < 1) { throw new ArgumentOutOfRangeException(nameof(version)); }
            Version = version;
            RecipeStableId = recipeStableId ?? throw new ArgumentNullException(nameof(recipeStableId));
            TargetEquipmentDefinitionStableId = targetEquipmentDefinitionStableId
                ?? throw new ArgumentNullException(nameof(targetEquipmentDefinitionStableId));
            NaturalDiscoverySourceStableId = naturalDiscoverySourceStableId
                ?? throw new ArgumentNullException(nameof(naturalDiscoverySourceStableId));
            if (naturalDiscoveryLevel < 0) { throw new ArgumentOutOfRangeException(nameof(naturalDiscoveryLevel)); }
            if (ordinaryDiscoveryActivationLevel < 0) { throw new ArgumentOutOfRangeException(nameof(ordinaryDiscoveryActivationLevel)); }
            if (craftingDelayLevels <= 0) { throw new ArgumentOutOfRangeException(nameof(craftingDelayLevels)); }
            if (scrapCost <= 0L) { throw new ArgumentOutOfRangeException(nameof(scrapCost)); }
            if (!Enum.IsDefined(typeof(CraftingQualityPolicyKindV1), qualityPolicyKind))
            {
                throw new ArgumentOutOfRangeException(nameof(qualityPolicyKind));
            }
            if (minimumItemLevel < 1 || maximumItemLevel < minimumItemLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumItemLevel));
            }
            if (minimumAugmentSlots < 0 || maximumAugmentSlots < minimumAugmentSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumAugmentSlots));
            }
            if (maximumAugmentTier < 1) { throw new ArgumentOutOfRangeException(nameof(maximumAugmentTier)); }
            if (maximumAugmentLevel < 1) { throw new ArgumentOutOfRangeException(nameof(maximumAugmentLevel)); }

            NaturalDiscoveryLevel = naturalDiscoveryLevel;
            OrdinaryDiscoveryActivationLevel = ordinaryDiscoveryActivationLevel;
            CraftingDelayLevels = craftingDelayLevels;
            DelayVariance = delayVariance ?? new CraftingDelayVarianceV1(0, 0);
            ScrapCost = scrapCost;
            QualityPolicyKind = qualityPolicyKind;
            this.qualityOptions = CopyUnique(qualityOptions, nameof(qualityOptions), true);
            if (this.qualityOptions.Count == 0)
            {
                throw new ArgumentException("At least one quality option is required.", nameof(qualityOptions));
            }
            if (qualityPolicyKind == CraftingQualityPolicyKindV1.Fixed && this.qualityOptions.Count != 1)
            {
                throw new ArgumentException("Fixed-quality crafting requires exactly one quality option.", nameof(qualityOptions));
            }

            MinimumItemLevel = minimumItemLevel;
            MaximumItemLevel = maximumItemLevel;
            MinimumAugmentSlots = minimumAugmentSlots;
            MaximumAugmentSlots = maximumAugmentSlots;
            MaximumAugmentTier = maximumAugmentTier;
            MaximumAugmentLevel = maximumAugmentLevel;
            this.augmentOptions = CopyUnique(augmentOptions, nameof(augmentOptions), false);
            if (minimumAugmentSlots > 0 && this.augmentOptions.Count == 0)
            {
                throw new ArgumentException("Required augment slots need at least one augment option.", nameof(augmentOptions));
            }

            GeneratorPolicy = generatorPolicy ?? throw new ArgumentNullException(nameof(generatorPolicy));
            MinimumUnlockLevel = CheckedUnlock(
                naturalDiscoveryLevel,
                craftingDelayLevels,
                DelayVariance.MinimumAdditionalLevels);
            MaximumUnlockLevel = CheckedUnlock(
                naturalDiscoveryLevel,
                craftingDelayLevels,
                DelayVariance.MaximumAdditionalLevels);
            if (MinimumUnlockLevel <= ordinaryDiscoveryActivationLevel)
            {
                throw new ArgumentException(
                    "Minimum crafting unlock must be later than ordinary discovery activation.",
                    nameof(ordinaryDiscoveryActivationLevel));
            }

            canonicalText = BuildCanonicalText();
            Fingerprint = CraftingCanonicalV1.Fingerprint(canonicalText);
        }

        public int Version { get; }
        public StableId RecipeStableId { get; }
        public StableId TargetEquipmentDefinitionStableId { get; }
        public StableId NaturalDiscoverySourceStableId { get; }
        public int NaturalDiscoveryLevel { get; }
        public int OrdinaryDiscoveryActivationLevel { get; }
        public int CraftingDelayLevels { get; }
        public CraftingDelayVarianceV1 DelayVariance { get; }
        public int MinimumUnlockLevel { get; }
        public int MaximumUnlockLevel { get; }
        public long ScrapCost { get; }
        public CraftingQualityPolicyKindV1 QualityPolicyKind { get; }
        public IReadOnlyList<CraftingWeightedDefinitionV1> QualityOptions { get { return qualityOptions; } }
        public int MinimumItemLevel { get; }
        public int MaximumItemLevel { get; }
        public int MinimumAugmentSlots { get; }
        public int MaximumAugmentSlots { get; }
        public int MaximumAugmentTier { get; }
        public int MaximumAugmentLevel { get; }
        public IReadOnlyList<CraftingWeightedDefinitionV1> AugmentOptions { get { return augmentOptions; } }
        public CraftingGeneratorPolicyV1 GeneratorPolicy { get; }
        public string Fingerprint { get; }

        public int ResolveUnlockLevel(ulong rootSeed)
        {
            if (DelayVariance.IsFixed)
            {
                return MinimumUnlockLevel;
            }

            DeterministicRandom random = DeterministicRandom.Create(rootSeed, GeneratorPolicy.AlgorithmVersion)
                .Fork(UnlockDelayPurpose, CraftingCanonicalV1.StableOrdinal(RecipeStableId));
            int additional;
            random.NextInt32(
                DelayVariance.MinimumAdditionalLevels,
                checked(DelayVariance.MaximumAdditionalLevels + 1),
                out additional);
            return CheckedUnlock(NaturalDiscoveryLevel, CraftingDelayLevels, additional);
        }

        public int CompareTo(CraftingRecipeV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : RecipeStableId.CompareTo(other.RecipeStableId);
        }

        public bool Equals(CraftingRecipeV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) { return Equals(obj as CraftingRecipeV1); }
        public override int GetHashCode() { return CraftingCanonicalV1.DeterministicHash(canonicalText); }
        public string ToCanonicalString() { return canonicalText; }

        private string BuildCanonicalText()
        {
            var builder = new StringBuilder();
            builder.Append("schema=crafting-recipe-v1")
                .Append("\nversion=").Append(Version.ToString(CultureInfo.InvariantCulture))
                .Append("\nrecipe_id=").Append(RecipeStableId)
                .Append("\ntarget_equipment_id=").Append(TargetEquipmentDefinitionStableId)
                .Append("\nnatural_discovery_source_id=").Append(NaturalDiscoverySourceStableId)
                .Append("\nnatural_discovery_level=").Append(NaturalDiscoveryLevel.ToString(CultureInfo.InvariantCulture))
                .Append("\nordinary_discovery_activation_level=").Append(OrdinaryDiscoveryActivationLevel.ToString(CultureInfo.InvariantCulture))
                .Append("\ncrafting_delay_levels=").Append(CraftingDelayLevels.ToString(CultureInfo.InvariantCulture))
                .Append("\ndelay_variance=").Append(DelayVariance.ToCanonicalString())
                .Append("\nscrap_cost=").Append(ScrapCost.ToString(CultureInfo.InvariantCulture))
                .Append("\nquality_policy=").Append(((int)QualityPolicyKind).ToString(CultureInfo.InvariantCulture))
                .Append("\nitem_levels=").Append(MinimumItemLevel.ToString(CultureInfo.InvariantCulture))
                .Append("..").Append(MaximumItemLevel.ToString(CultureInfo.InvariantCulture))
                .Append("\naugment_slots=").Append(MinimumAugmentSlots.ToString(CultureInfo.InvariantCulture))
                .Append("..").Append(MaximumAugmentSlots.ToString(CultureInfo.InvariantCulture))
                .Append("\nmaximum_augment_tier=").Append(MaximumAugmentTier.ToString(CultureInfo.InvariantCulture))
                .Append("\nmaximum_augment_level=").Append(MaximumAugmentLevel.ToString(CultureInfo.InvariantCulture))
                .Append("\ngenerator_policy:\n").Append(GeneratorPolicy.ToCanonicalString());
            AppendOptions(builder, "quality", qualityOptions);
            AppendOptions(builder, "augment", augmentOptions);
            return builder.ToString();
        }

        private static void AppendOptions(
            StringBuilder builder,
            string name,
            IReadOnlyList<CraftingWeightedDefinitionV1> options)
        {
            builder.Append('\n').Append(name).Append("_count=")
                .Append(options.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < options.Count; index++)
            {
                builder.Append('\n').Append(name).Append('_')
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append(":\n").Append(options[index].ToCanonicalString());
            }
        }

        private static ReadOnlyCollection<CraftingWeightedDefinitionV1> CopyUnique(
            IEnumerable<CraftingWeightedDefinitionV1> source,
            string parameterName,
            bool requiredCollection)
        {
            if (source == null)
            {
                if (requiredCollection) { throw new ArgumentNullException(parameterName); }
                return new ReadOnlyCollection<CraftingWeightedDefinitionV1>(
                    new List<CraftingWeightedDefinitionV1>());
            }

            var copy = new List<CraftingWeightedDefinitionV1>();
            var ids = new HashSet<StableId>();
            foreach (CraftingWeightedDefinitionV1 value in source)
            {
                if (value == null)
                {
                    throw new ArgumentException("Options must not contain null entries.", parameterName);
                }
                if (!ids.Add(value.DefinitionStableId))
                {
                    throw new ArgumentException(
                        "Options contain duplicate identity " + value.DefinitionStableId + ".",
                        parameterName);
                }
                copy.Add(value);
            }
            copy.Sort();
            return new ReadOnlyCollection<CraftingWeightedDefinitionV1>(copy);
        }

        private static int CheckedUnlock(int naturalLevel, int delay, int variance)
        {
            return checked(checked(naturalLevel + delay) + variance);
        }
    }

    public sealed class CraftingRecipeCatalogV1
    {
        private readonly ReadOnlyCollection<CraftingRecipeV1> recipes;
        private readonly Dictionary<StableId, CraftingRecipeV1> byId;

        public CraftingRecipeCatalogV1(IEnumerable<CraftingRecipeV1> recipes)
        {
            if (recipes == null) { throw new ArgumentNullException(nameof(recipes)); }
            var copy = new List<CraftingRecipeV1>();
            byId = new Dictionary<StableId, CraftingRecipeV1>();
            foreach (CraftingRecipeV1 recipe in recipes)
            {
                if (recipe == null) { throw new ArgumentException("Recipe catalog must not contain null entries.", nameof(recipes)); }
                if (byId.ContainsKey(recipe.RecipeStableId))
                {
                    throw new ArgumentException("Duplicate recipe identity " + recipe.RecipeStableId + ".", nameof(recipes));
                }
                byId.Add(recipe.RecipeStableId, recipe);
                copy.Add(recipe);
            }
            copy.Sort();
            this.recipes = new ReadOnlyCollection<CraftingRecipeV1>(copy);
            var builder = new StringBuilder("schema=crafting-recipe-catalog-v1");
            builder.Append("\nrecipe_count=").Append(copy.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < copy.Count; index++)
            {
                builder.Append("\nrecipe_").Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append("_fingerprint=").Append(copy[index].Fingerprint);
            }
            Fingerprint = CraftingCanonicalV1.Fingerprint(builder.ToString());
        }

        public IReadOnlyList<CraftingRecipeV1> Recipes { get { return recipes; } }
        public string Fingerprint { get; }

        public CraftingRecipeV1 Find(StableId recipeStableId)
        {
            CraftingRecipeV1 value;
            return recipeStableId != null && byId.TryGetValue(recipeStableId, out value) ? value : null;
        }
    }

    public static class CraftingCanonicalV1
    {
        public static string Fingerprint(string canonicalText)
        {
            if (canonicalText == null) { throw new ArgumentNullException(nameof(canonicalText)); }
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(Encoding.UTF8.GetBytes(canonicalText));
            }
            var builder = new StringBuilder("sha256:", 71);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }

        public static StableId DeriveStableId(string namespaceName, params string[] parts)
        {
            var builder = new StringBuilder("schema=crafting-derived-id-v1");
            for (int index = 0; index < parts.Length; index++)
            {
                string part = parts[index] ?? string.Empty;
                builder.Append("\npart_").Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append("_length=").Append(part.Length.ToString(CultureInfo.InvariantCulture))
                    .Append("\npart_").Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('=').Append(part);
            }
            string fingerprint = Fingerprint(builder.ToString());
            return StableId.Create(namespaceName, fingerprint.Substring(7, 48));
        }

        public static ulong StableOrdinal(StableId stableId)
        {
            if (stableId == null) { throw new ArgumentNullException(nameof(stableId)); }
            unchecked
            {
                ulong hash = 14695981039346656037UL;
                string text = stableId.ToString();
                for (int index = 0; index < text.Length; index++)
                {
                    hash ^= (byte)text[index];
                    hash *= 1099511628211UL;
                }
                return hash;
            }
        }

        public static int DeterministicHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619u;
                }
                return (int)hash;
            }
        }
    }
}
