using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Domain.Shops
{
    public enum ShopProgressionContextPolicyV1
    {
        FreezeOnFirstOpen = 1,
        SnapshotOnAcceptedRefresh = 2,
    }

    public enum ShopRefreshPolicyV1
    {
        Disabled = 1,
        ExplicitRunBound = 2,
    }

    public sealed class ShopPricingPolicyV1
    {
        private readonly string canonicalText;

        private ShopPricingPolicyV1(
            StableId policyStableId,
            long minimumPrice,
            long basePrice,
            long perItemLevel,
            long perQualityRank,
            long perAugment,
            long perAugmentTier,
            long perAugmentLevel)
        {
            PolicyStableId = policyStableId ?? throw new ArgumentNullException(nameof(policyStableId));
            if (minimumPrice < 1L)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumPrice));
            }

            ValidateNonNegative(basePrice, nameof(basePrice));
            ValidateNonNegative(perItemLevel, nameof(perItemLevel));
            ValidateNonNegative(perQualityRank, nameof(perQualityRank));
            ValidateNonNegative(perAugment, nameof(perAugment));
            ValidateNonNegative(perAugmentTier, nameof(perAugmentTier));
            ValidateNonNegative(perAugmentLevel, nameof(perAugmentLevel));

            MinimumPrice = minimumPrice;
            BasePrice = basePrice;
            PerItemLevel = perItemLevel;
            PerQualityRank = perQualityRank;
            PerAugment = perAugment;
            PerAugmentTier = perAugmentTier;
            PerAugmentLevel = perAugmentLevel;
            canonicalText = BuildCanonicalText();
            Fingerprint = ShopCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId PolicyStableId { get; }
        public long MinimumPrice { get; }
        public long BasePrice { get; }
        public long PerItemLevel { get; }
        public long PerQualityRank { get; }
        public long PerAugment { get; }
        public long PerAugmentTier { get; }
        public long PerAugmentLevel { get; }
        public string Fingerprint { get; }

        public static ShopPricingPolicyV1 Create(
            StableId policyStableId,
            long minimumPrice,
            long basePrice,
            long perItemLevel,
            long perQualityRank,
            long perAugment,
            long perAugmentTier,
            long perAugmentLevel)
        {
            return new ShopPricingPolicyV1(
                policyStableId,
                minimumPrice,
                basePrice,
                perItemLevel,
                perQualityRank,
                perAugment,
                perAugmentTier,
                perAugmentLevel);
        }

        public bool TryCalculatePrice(
            EquipmentInstance equipment,
            EquipmentCatalog catalog,
            out long price,
            out string rejectionCode)
        {
            price = 0L;
            rejectionCode = null;
            if (equipment == null || catalog == null)
            {
                rejectionCode = "shop-price-input-null";
                return false;
            }

            EquipmentDefinition definition = catalog.FindEquipmentDefinition(equipment.DefinitionId);
            if (definition == null)
            {
                rejectionCode = "shop-price-definition-unknown";
                return false;
            }

            int qualityRank = 0;
            for (int index = 0; index < definition.QualityTiers.Count; index++)
            {
                EquipmentQualityTier quality = definition.QualityTiers[index];
                if (quality != null && quality.QualityId == equipment.QualityId)
                {
                    qualityRank = quality.Rank;
                    break;
                }
            }

            if (qualityRank < 1)
            {
                rejectionCode = "shop-price-quality-unknown";
                return false;
            }

            try
            {
                long computed = BasePrice;
                computed = checked(computed + checked(PerItemLevel * equipment.ItemLevel));
                computed = checked(computed + checked(PerQualityRank * qualityRank));
                computed = checked(computed + checked(PerAugment * equipment.Augments.Count));
                for (int index = 0; index < equipment.Augments.Count; index++)
                {
                    AugmentInstance augment = equipment.Augments[index];
                    computed = checked(computed + checked(PerAugmentTier * augment.Tier));
                    computed = checked(computed + checked(PerAugmentLevel * augment.Level));
                }

                price = Math.Max(MinimumPrice, computed);
                return true;
            }
            catch (OverflowException)
            {
                rejectionCode = "shop-price-overflow";
                return false;
            }
        }

        public string ToCanonicalString() { return canonicalText; }

        private string BuildCanonicalText()
        {
            return "schema=shop-pricing-policy-v1"
                + "\npolicy_id=" + PolicyStableId
                + "\nminimum_price=" + MinimumPrice.ToString(CultureInfo.InvariantCulture)
                + "\nbase_price=" + BasePrice.ToString(CultureInfo.InvariantCulture)
                + "\nper_item_level=" + PerItemLevel.ToString(CultureInfo.InvariantCulture)
                + "\nper_quality_rank=" + PerQualityRank.ToString(CultureInfo.InvariantCulture)
                + "\nper_augment=" + PerAugment.ToString(CultureInfo.InvariantCulture)
                + "\nper_augment_tier=" + PerAugmentTier.ToString(CultureInfo.InvariantCulture)
                + "\nper_augment_level=" + PerAugmentLevel.ToString(CultureInfo.InvariantCulture);
        }

        private static void ValidateNonNegative(long value, string parameterName)
        {
            if (value < 0L)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }

    public sealed class ShopDefinitionV1
    {
        public const int CurrentSchemaVersion = 1;
        private readonly ReadOnlyCollection<StableId> eligibleCategoryIds;
        private readonly ReadOnlyCollection<StableId> requiredEquipmentTags;
        private readonly ReadOnlyCollection<StableId> excludedEquipmentTags;
        private readonly string canonicalText;

        private ShopDefinitionV1(
            int schemaVersion,
            StableId shopStableId,
            int inventorySize,
            IEnumerable<StableId> eligibleCategoryIds,
            IEnumerable<StableId> requiredEquipmentTags,
            IEnumerable<StableId> excludedEquipmentTags,
            EquipmentGenerationPolicyV1 generationPolicy,
            ShopProgressionContextPolicyV1 progressionContextPolicy,
            ShopPricingPolicyV1 pricingPolicy,
            ShopRefreshPolicyV1 refreshPolicy,
            int maximumRunRefreshCount,
            int baseLockCapacity,
            int algorithmVersion)
        {
            if (schemaVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }

            SchemaVersion = schemaVersion;
            ShopStableId = shopStableId ?? throw new ArgumentNullException(nameof(shopStableId));
            if (inventorySize < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(inventorySize));
            }

            InventorySize = inventorySize;
            this.eligibleCategoryIds = CopyIds(eligibleCategoryIds, nameof(eligibleCategoryIds));
            this.requiredEquipmentTags = CopyIds(requiredEquipmentTags, nameof(requiredEquipmentTags));
            this.excludedEquipmentTags = CopyIds(excludedEquipmentTags, nameof(excludedEquipmentTags));
            GenerationPolicy = generationPolicy ?? throw new ArgumentNullException(nameof(generationPolicy));
            if (!Enum.IsDefined(typeof(ShopProgressionContextPolicyV1), progressionContextPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(progressionContextPolicy));
            }

            ProgressionContextPolicy = progressionContextPolicy;
            PricingPolicy = pricingPolicy ?? throw new ArgumentNullException(nameof(pricingPolicy));
            if (!Enum.IsDefined(typeof(ShopRefreshPolicyV1), refreshPolicy))
            {
                throw new ArgumentOutOfRangeException(nameof(refreshPolicy));
            }

            if (maximumRunRefreshCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumRunRefreshCount));
            }

            if (refreshPolicy == ShopRefreshPolicyV1.Disabled && maximumRunRefreshCount != 0)
            {
                throw new ArgumentException(
                    "Disabled refresh policy requires a zero run-bound refresh maximum.",
                    nameof(maximumRunRefreshCount));
            }

            RefreshPolicy = refreshPolicy;
            MaximumRunRefreshCount = maximumRunRefreshCount;
            if (baseLockCapacity < 0 || baseLockCapacity > inventorySize)
            {
                throw new ArgumentOutOfRangeException(nameof(baseLockCapacity));
            }

            BaseLockCapacity = baseLockCapacity;
            if (algorithmVersion < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(algorithmVersion));
            }

            AlgorithmVersion = algorithmVersion;
            canonicalText = BuildCanonicalText();
            Fingerprint = ShopCanonicalV1.Fingerprint(canonicalText);
        }

        public int SchemaVersion { get; }
        public StableId ShopStableId { get; }
        public int InventorySize { get; }
        public IReadOnlyList<StableId> EligibleCategoryIds { get { return eligibleCategoryIds; } }
        public IReadOnlyList<StableId> RequiredEquipmentTags { get { return requiredEquipmentTags; } }
        public IReadOnlyList<StableId> ExcludedEquipmentTags { get { return excludedEquipmentTags; } }
        public EquipmentGenerationPolicyV1 GenerationPolicy { get; }
        public ShopProgressionContextPolicyV1 ProgressionContextPolicy { get; }
        public ShopPricingPolicyV1 PricingPolicy { get; }
        public ShopRefreshPolicyV1 RefreshPolicy { get; }
        public int MaximumRunRefreshCount { get; }
        public int BaseLockCapacity { get; }
        public int AlgorithmVersion { get; }
        public string Fingerprint { get; }

        public static ShopDefinitionV1 Create(
            StableId shopStableId,
            int inventorySize,
            IEnumerable<StableId> eligibleCategoryIds,
            IEnumerable<StableId> requiredEquipmentTags,
            IEnumerable<StableId> excludedEquipmentTags,
            EquipmentGenerationPolicyV1 generationPolicy,
            ShopProgressionContextPolicyV1 progressionContextPolicy,
            ShopPricingPolicyV1 pricingPolicy,
            ShopRefreshPolicyV1 refreshPolicy,
            int maximumRunRefreshCount,
            int baseLockCapacity,
            int algorithmVersion,
            int schemaVersion = CurrentSchemaVersion)
        {
            return new ShopDefinitionV1(
                schemaVersion,
                shopStableId,
                inventorySize,
                eligibleCategoryIds,
                requiredEquipmentTags,
                excludedEquipmentTags,
                generationPolicy,
                progressionContextPolicy,
                pricingPolicy,
                refreshPolicy,
                maximumRunRefreshCount,
                baseLockCapacity,
                algorithmVersion);
        }

        public bool Allows(EquipmentDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            if (eligibleCategoryIds.Count > 0 && !Contains(eligibleCategoryIds, definition.CategoryId))
            {
                return false;
            }

            for (int index = 0; index < requiredEquipmentTags.Count; index++)
            {
                if (!definition.HasTag(requiredEquipmentTags[index]))
                {
                    return false;
                }
            }

            for (int index = 0; index < excludedEquipmentTags.Count; index++)
            {
                if (definition.HasTag(excludedEquipmentTags[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public ProgressionContext SelectRefreshContext(
            ProgressionContext firstOpenContext,
            ProgressionContext requestedRefreshContext)
        {
            if (firstOpenContext == null)
            {
                throw new ArgumentNullException(nameof(firstOpenContext));
            }

            switch (ProgressionContextPolicy)
            {
                case ShopProgressionContextPolicyV1.FreezeOnFirstOpen:
                    return firstOpenContext;
                case ShopProgressionContextPolicyV1.SnapshotOnAcceptedRefresh:
                    return requestedRefreshContext
                        ?? throw new ArgumentNullException(nameof(requestedRefreshContext));
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public string ToCanonicalString() { return canonicalText; }

        private string BuildCanonicalText()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("schema=shop-definition-v1")
                .Append("\nschema_version=").Append(SchemaVersion.ToString(CultureInfo.InvariantCulture))
                .Append("\nshop_id=").Append(ShopStableId)
                .Append("\ninventory_size=").Append(InventorySize.ToString(CultureInfo.InvariantCulture))
                .Append("\ngeneration_policy_fingerprint=").Append(GenerationPolicy.Fingerprint)
                .Append("\nprogression_context_policy=").Append(((int)ProgressionContextPolicy).ToString(CultureInfo.InvariantCulture))
                .Append("\npricing_policy_fingerprint=").Append(PricingPolicy.Fingerprint)
                .Append("\nrefresh_policy=").Append(((int)RefreshPolicy).ToString(CultureInfo.InvariantCulture))
                .Append("\nmaximum_run_refresh_count=").Append(MaximumRunRefreshCount.ToString(CultureInfo.InvariantCulture))
                .Append("\nbase_lock_capacity=").Append(BaseLockCapacity.ToString(CultureInfo.InvariantCulture))
                .Append("\nalgorithm_version=").Append(AlgorithmVersion.ToString(CultureInfo.InvariantCulture));
            AppendIds(builder, "eligible_category", eligibleCategoryIds);
            AppendIds(builder, "required_tag", requiredEquipmentTags);
            AppendIds(builder, "excluded_tag", excludedEquipmentTags);
            return builder.ToString();
        }

        private static ReadOnlyCollection<StableId> CopyIds(
            IEnumerable<StableId> source,
            string parameterName)
        {
            SortedSet<StableId> ids = new SortedSet<StableId>();
            if (source != null)
            {
                foreach (StableId id in source)
                {
                    if (id == null)
                    {
                        throw new ArgumentException(
                            parameterName + " must not contain null entries.",
                            parameterName);
                    }

                    ids.Add(id);
                }
            }

            return new ReadOnlyCollection<StableId>(new List<StableId>(ids));
        }

        private static bool Contains(IReadOnlyList<StableId> source, StableId value)
        {
            for (int index = 0; index < source.Count; index++)
            {
                if (source[index] == value)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AppendIds(
            StringBuilder builder,
            string label,
            IReadOnlyList<StableId> values)
        {
            builder.Append('\n').Append(label).Append("_count=")
                .Append(values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                builder.Append('\n').Append(label).Append('_')
                    .Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('=').Append(values[index]);
            }
        }
    }

    public sealed class ShopLockCapacityQueryV1
    {
        public ShopLockCapacityQueryV1(
            StableId runStableId,
            StableId shopStableId,
            int currentRefreshOrdinal,
            int baseCapacity)
        {
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            ShopStableId = shopStableId ?? throw new ArgumentNullException(nameof(shopStableId));
            if (currentRefreshOrdinal < 0 || baseCapacity < 0)
            {
                throw new ArgumentOutOfRangeException();
            }

            CurrentRefreshOrdinal = currentRefreshOrdinal;
            BaseCapacity = baseCapacity;
        }

        public StableId RunStableId { get; }
        public StableId ShopStableId { get; }
        public int CurrentRefreshOrdinal { get; }
        public int BaseCapacity { get; }
    }

    public interface IShopLockCapacityExtensionV1
    {
        int GetAdditionalCapacity(ShopLockCapacityQueryV1 query);
    }

    public static class ShopCanonicalV1
    {
        public static string Fingerprint(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

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

        public static StableId DeriveStableId(string namespaceName, params string[] parts)
        {
            if (namespaceName == null)
            {
                throw new ArgumentNullException(nameof(namespaceName));
            }

            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < parts.Length; index++)
            {
                if (index > 0) { builder.Append('|'); }
                builder.Append(parts[index] ?? "null");
            }

            string fingerprint = Fingerprint(builder.ToString());
            return StableId.Create(namespaceName, fingerprint.Substring(7, 48));
        }

        public static ulong DeriveInventorySeed(
            StableId runStableId,
            StableId shopStableId,
            int refreshOrdinal,
            int algorithmVersion)
        {
            if (runStableId == null || shopStableId == null)
            {
                throw new ArgumentNullException();
            }

            if (refreshOrdinal < 0 || algorithmVersion < 1)
            {
                throw new ArgumentOutOfRangeException();
            }

            string text = "schema=shop-inventory-seed-v1"
                + "\nrun_id=" + runStableId
                + "\nshop_id=" + shopStableId
                + "\nrefresh_ordinal=" + refreshOrdinal.ToString(CultureInfo.InvariantCulture)
                + "\nalgorithm_version=" + algorithmVersion.ToString(CultureInfo.InvariantCulture);
            byte[] digest;
            using (SHA256 algorithm = SHA256.Create())
            {
                digest = algorithm.ComputeHash(Encoding.UTF8.GetBytes(text));
            }

            ulong result = 0UL;
            for (int index = 0; index < 8; index++)
            {
                result = (result << 8) | digest[index];
            }

            return result;
        }
    }
}
