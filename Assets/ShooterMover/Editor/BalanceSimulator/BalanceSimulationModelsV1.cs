using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Editor.BalanceSimulator
{
    public enum BalanceSimulationModeV1
    {
        SingleOpen = 1,
        Batch = 2,
    }

    public sealed class BalanceSimulationRequestV1
    {
        public BalanceSimulationRequestV1(
            BalanceSimulationModeV1 mode,
            int characterLevel,
            int strongboxTier,
            int strongboxLevel,
            int shopLevel,
            ulong deterministicSeed,
            int numberOfSimulations,
            long startingMoney,
            long startingScrap)
        {
            if (!Enum.IsDefined(typeof(BalanceSimulationModeV1), mode))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            if (characterLevel < 0) { throw new ArgumentOutOfRangeException(nameof(characterLevel)); }
            if (strongboxTier < 0) { throw new ArgumentOutOfRangeException(nameof(strongboxTier)); }
            if (strongboxLevel < 0) { throw new ArgumentOutOfRangeException(nameof(strongboxLevel)); }
            if (shopLevel < 0) { throw new ArgumentOutOfRangeException(nameof(shopLevel)); }
            if (numberOfSimulations < 1) { throw new ArgumentOutOfRangeException(nameof(numberOfSimulations)); }
            if (startingMoney < 0L) { throw new ArgumentOutOfRangeException(nameof(startingMoney)); }
            if (startingScrap < 0L) { throw new ArgumentOutOfRangeException(nameof(startingScrap)); }

            Mode = mode;
            CharacterLevel = characterLevel;
            StrongboxTier = strongboxTier;
            StrongboxLevel = strongboxLevel;
            ShopLevel = shopLevel;
            DeterministicSeed = deterministicSeed;
            NumberOfSimulations = mode == BalanceSimulationModeV1.SingleOpen ? 1 : numberOfSimulations;
            StartingMoney = startingMoney;
            StartingScrap = startingScrap;
        }

        public BalanceSimulationModeV1 Mode { get; }
        public int CharacterLevel { get; }
        public int StrongboxTier { get; }
        public int StrongboxLevel { get; }
        public int ShopLevel { get; }
        public ulong DeterministicSeed { get; }
        public int NumberOfSimulations { get; }
        public long StartingMoney { get; }
        public long StartingScrap { get; }

        public string ToCanonicalString()
        {
            return "schema=balance-simulation-request-v1"
                + "\nmode=" + ((int)Mode).ToString(CultureInfo.InvariantCulture)
                + "\ncharacter_level=" + CharacterLevel.ToString(CultureInfo.InvariantCulture)
                + "\nstrongbox_tier=" + StrongboxTier.ToString(CultureInfo.InvariantCulture)
                + "\nstrongbox_level=" + StrongboxLevel.ToString(CultureInfo.InvariantCulture)
                + "\nshop_level=" + ShopLevel.ToString(CultureInfo.InvariantCulture)
                + "\nseed=" + DeterministicSeed.ToString(CultureInfo.InvariantCulture)
                + "\nsimulations=" + NumberOfSimulations.ToString(CultureInfo.InvariantCulture)
                + "\nstarting_money=" + StartingMoney.ToString(CultureInfo.InvariantCulture)
                + "\nstarting_scrap=" + StartingScrap.ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class BalanceSimulationIterationRequestV1
    {
        public BalanceSimulationIterationRequestV1(
            BalanceSimulationRequestV1 request,
            int iterationIndex,
            ulong iterationSeed)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            if (iterationIndex < 0) { throw new ArgumentOutOfRangeException(nameof(iterationIndex)); }
            IterationIndex = iterationIndex;
            IterationSeed = iterationSeed;
        }

        public BalanceSimulationRequestV1 Request { get; }
        public int IterationIndex { get; }
        public ulong IterationSeed { get; }
    }

    public sealed class BalanceRewardObservationV1
    {
        public BalanceRewardObservationV1(string rewardType, long quantity)
        {
            if (string.IsNullOrWhiteSpace(rewardType)) { throw new ArgumentException("Reward type is required.", nameof(rewardType)); }
            if (quantity < 1L) { throw new ArgumentOutOfRangeException(nameof(quantity)); }
            RewardType = rewardType;
            Quantity = quantity;
        }

        public string RewardType { get; }
        public long Quantity { get; }
    }

    public sealed class BalanceEquipmentObservationV1
    {
        public BalanceEquipmentObservationV1(
            string source,
            EquipmentInstance equipment,
            StableId categoryId,
            string definitionDisplayName)
        {
            if (string.IsNullOrWhiteSpace(source)) { throw new ArgumentException("Source is required.", nameof(source)); }
            Source = source;
            Equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
            CategoryId = categoryId ?? throw new ArgumentNullException(nameof(categoryId));
            DefinitionDisplayName = definitionDisplayName ?? equipment.DefinitionId.ToString();
        }

        public string Source { get; }
        public EquipmentInstance Equipment { get; }
        public StableId CategoryId { get; }
        public string DefinitionDisplayName { get; }
    }

    public sealed class BalanceRejectionV1
    {
        public BalanceRejectionV1(string system, string code, string detail)
        {
            if (string.IsNullOrWhiteSpace(system)) { throw new ArgumentException("System is required.", nameof(system)); }
            if (string.IsNullOrWhiteSpace(code)) { throw new ArgumentException("Code is required.", nameof(code)); }
            System = system;
            Code = code;
            Detail = detail ?? string.Empty;
        }

        public string System { get; }
        public string Code { get; }
        public string Detail { get; }
        public string Key { get { return System + ":" + Code; } }
    }

    public sealed class BalanceSimulationIterationResultV1
    {
        private readonly ReadOnlyCollection<BalanceRewardObservationV1> rewards;
        private readonly ReadOnlyCollection<BalanceEquipmentObservationV1> equipment;
        private readonly ReadOnlyCollection<BalanceRejectionV1> rejections;

        public BalanceSimulationIterationResultV1(
            int iterationIndex,
            ulong iterationSeed,
            IEnumerable<BalanceRewardObservationV1> rewards,
            IEnumerable<BalanceEquipmentObservationV1> equipment,
            long moneyDelta,
            long scrapDelta,
            long shopMoneyRequired,
            long craftingScrapRequired,
            long upgradeMoneyRequired,
            int softEligibleCandidateCount,
            int craftingUnlockLevel,
            IEnumerable<BalanceRejectionV1> rejections)
        {
            if (iterationIndex < 0) { throw new ArgumentOutOfRangeException(nameof(iterationIndex)); }
            if (shopMoneyRequired < 0L) { throw new ArgumentOutOfRangeException(nameof(shopMoneyRequired)); }
            if (craftingScrapRequired < 0L) { throw new ArgumentOutOfRangeException(nameof(craftingScrapRequired)); }
            if (upgradeMoneyRequired < 0L) { throw new ArgumentOutOfRangeException(nameof(upgradeMoneyRequired)); }
            if (softEligibleCandidateCount < 0) { throw new ArgumentOutOfRangeException(nameof(softEligibleCandidateCount)); }
            if (craftingUnlockLevel < 0) { throw new ArgumentOutOfRangeException(nameof(craftingUnlockLevel)); }

            IterationIndex = iterationIndex;
            IterationSeed = iterationSeed;
            this.rewards = Copy(rewards);
            this.equipment = Copy(equipment);
            this.rejections = Copy(rejections);
            MoneyDelta = moneyDelta;
            ScrapDelta = scrapDelta;
            ShopMoneyRequired = shopMoneyRequired;
            CraftingScrapRequired = craftingScrapRequired;
            UpgradeMoneyRequired = upgradeMoneyRequired;
            SoftEligibleCandidateCount = softEligibleCandidateCount;
            CraftingUnlockLevel = craftingUnlockLevel;
        }

        public int IterationIndex { get; }
        public ulong IterationSeed { get; }
        public IReadOnlyList<BalanceRewardObservationV1> Rewards { get { return rewards; } }
        public IReadOnlyList<BalanceEquipmentObservationV1> Equipment { get { return equipment; } }
        public long MoneyDelta { get; }
        public long ScrapDelta { get; }
        public long ShopMoneyRequired { get; }
        public long CraftingScrapRequired { get; }
        public long UpgradeMoneyRequired { get; }
        public int SoftEligibleCandidateCount { get; }
        public int CraftingUnlockLevel { get; }
        public IReadOnlyList<BalanceRejectionV1> Rejections { get { return rejections; } }

        private static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values)
        {
            return new ReadOnlyCollection<T>(new List<T>(values ?? Array.Empty<T>()));
        }
    }

    public sealed class BalanceCountV1 : IComparable<BalanceCountV1>
    {
        public BalanceCountV1(string key, long count, long total)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            if (count < 0L) { throw new ArgumentOutOfRangeException(nameof(count)); }
            if (total < 0L) { throw new ArgumentOutOfRangeException(nameof(total)); }
            Count = count;
            Percentage = total == 0L ? 0.0 : (100.0 * count) / total;
        }

        public string Key { get; }
        public long Count { get; }
        public double Percentage { get; }
        public int CompareTo(BalanceCountV1 other) { return ReferenceEquals(other, null) ? 1 : string.CompareOrdinal(Key, other.Key); }
    }

    public sealed class BalanceSimulationReportV1
    {
        private readonly ReadOnlyCollection<BalanceCountV1> rewardTypes;
        private readonly ReadOnlyCollection<BalanceCountV1> equipmentDefinitions;
        private readonly ReadOnlyCollection<BalanceCountV1> equipmentCategories;
        private readonly ReadOnlyCollection<BalanceCountV1> itemLevels;
        private readonly ReadOnlyCollection<BalanceCountV1> qualities;
        private readonly ReadOnlyCollection<BalanceCountV1> augmentCounts;
        private readonly ReadOnlyCollection<BalanceCountV1> augmentTiers;
        private readonly ReadOnlyCollection<BalanceCountV1> augmentLevels;
        private readonly ReadOnlyCollection<BalanceCountV1> rejections;
        private readonly ReadOnlyCollection<BalanceSimulationIterationResultV1> samples;

        internal BalanceSimulationReportV1(
            BalanceSimulationRequestV1 request,
            IEnumerable<BalanceCountV1> rewardTypes,
            IEnumerable<BalanceCountV1> equipmentDefinitions,
            IEnumerable<BalanceCountV1> equipmentCategories,
            IEnumerable<BalanceCountV1> itemLevels,
            IEnumerable<BalanceCountV1> qualities,
            IEnumerable<BalanceCountV1> augmentCounts,
            IEnumerable<BalanceCountV1> augmentTiers,
            IEnumerable<BalanceCountV1> augmentLevels,
            IEnumerable<BalanceCountV1> rejections,
            IEnumerable<BalanceSimulationIterationResultV1> samples,
            long equipmentInstanceCount,
            long uniqueEquipmentInstanceCount,
            long duplicateDefinitionCount,
            long moneyDelta,
            long scrapDelta,
            long shopMoneyRequired,
            long craftingScrapRequired,
            long upgradeMoneyRequired,
            long softEligibleCandidateCount,
            int minimumCraftingUnlockLevel,
            int maximumCraftingUnlockLevel)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            this.rewardTypes = CopyAndSort(rewardTypes);
            this.equipmentDefinitions = CopyAndSort(equipmentDefinitions);
            this.equipmentCategories = CopyAndSort(equipmentCategories);
            this.itemLevels = CopyAndSort(itemLevels);
            this.qualities = CopyAndSort(qualities);
            this.augmentCounts = CopyAndSort(augmentCounts);
            this.augmentTiers = CopyAndSort(augmentTiers);
            this.augmentLevels = CopyAndSort(augmentLevels);
            this.rejections = CopyAndSort(rejections);
            this.samples = new ReadOnlyCollection<BalanceSimulationIterationResultV1>(new List<BalanceSimulationIterationResultV1>(samples ?? Array.Empty<BalanceSimulationIterationResultV1>()));
            EquipmentInstanceCount = equipmentInstanceCount;
            UniqueEquipmentInstanceCount = uniqueEquipmentInstanceCount;
            DuplicateDefinitionCount = duplicateDefinitionCount;
            DuplicateDefinitionFrequency = equipmentInstanceCount == 0L ? 0.0 : (100.0 * duplicateDefinitionCount) / equipmentInstanceCount;
            MoneyDelta = moneyDelta;
            ScrapDelta = scrapDelta;
            ShopMoneyRequired = shopMoneyRequired;
            CraftingScrapRequired = craftingScrapRequired;
            UpgradeMoneyRequired = upgradeMoneyRequired;
            SoftEligibleCandidateCount = softEligibleCandidateCount;
            MinimumCraftingUnlockLevel = minimumCraftingUnlockLevel;
            MaximumCraftingUnlockLevel = maximumCraftingUnlockLevel;
            Fingerprint = RewardGenerationFingerprintV1.Compute(BuildCanonicalString());
        }

        public BalanceSimulationRequestV1 Request { get; }
        public IReadOnlyList<BalanceCountV1> RewardTypes { get { return rewardTypes; } }
        public IReadOnlyList<BalanceCountV1> EquipmentDefinitions { get { return equipmentDefinitions; } }
        public IReadOnlyList<BalanceCountV1> EquipmentCategories { get { return equipmentCategories; } }
        public IReadOnlyList<BalanceCountV1> ItemLevels { get { return itemLevels; } }
        public IReadOnlyList<BalanceCountV1> Qualities { get { return qualities; } }
        public IReadOnlyList<BalanceCountV1> AugmentCounts { get { return augmentCounts; } }
        public IReadOnlyList<BalanceCountV1> AugmentTiers { get { return augmentTiers; } }
        public IReadOnlyList<BalanceCountV1> AugmentLevels { get { return augmentLevels; } }
        public IReadOnlyList<BalanceCountV1> Rejections { get { return rejections; } }
        public IReadOnlyList<BalanceSimulationIterationResultV1> Samples { get { return samples; } }
        public long EquipmentInstanceCount { get; }
        public long UniqueEquipmentInstanceCount { get; }
        public long DuplicateDefinitionCount { get; }
        public double DuplicateDefinitionFrequency { get; }
        public long MoneyDelta { get; }
        public long ScrapDelta { get; }
        public long ShopMoneyRequired { get; }
        public long CraftingScrapRequired { get; }
        public long UpgradeMoneyRequired { get; }
        public long SoftEligibleCandidateCount { get; }
        public int MinimumCraftingUnlockLevel { get; }
        public int MaximumCraftingUnlockLevel { get; }
        public string Fingerprint { get; }

        public long FindCount(IReadOnlyList<BalanceCountV1> values, string key)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index].Key, key, StringComparison.Ordinal)) { return values[index].Count; }
            }
            return 0L;
        }

        private string BuildCanonicalString()
        {
            StringBuilder builder = new StringBuilder(Request.ToCanonicalString());
            Append(builder, "reward", rewardTypes);
            Append(builder, "definition", equipmentDefinitions);
            Append(builder, "category", equipmentCategories);
            Append(builder, "item_level", itemLevels);
            Append(builder, "quality", qualities);
            Append(builder, "augment_count", augmentCounts);
            Append(builder, "augment_tier", augmentTiers);
            Append(builder, "augment_level", augmentLevels);
            Append(builder, "rejection", rejections);
            builder.Append("\nequipment_instances=").Append(EquipmentInstanceCount.ToString(CultureInfo.InvariantCulture))
                .Append("\nunique_instances=").Append(UniqueEquipmentInstanceCount.ToString(CultureInfo.InvariantCulture))
                .Append("\nduplicate_definitions=").Append(DuplicateDefinitionCount.ToString(CultureInfo.InvariantCulture))
                .Append("\nmoney_delta=").Append(MoneyDelta.ToString(CultureInfo.InvariantCulture))
                .Append("\nscrap_delta=").Append(ScrapDelta.ToString(CultureInfo.InvariantCulture))
                .Append("\nshop_money_required=").Append(ShopMoneyRequired.ToString(CultureInfo.InvariantCulture))
                .Append("\ncrafting_scrap_required=").Append(CraftingScrapRequired.ToString(CultureInfo.InvariantCulture))
                .Append("\nupgrade_money_required=").Append(UpgradeMoneyRequired.ToString(CultureInfo.InvariantCulture))
                .Append("\nsoft_candidates=").Append(SoftEligibleCandidateCount.ToString(CultureInfo.InvariantCulture))
                .Append("\ncrafting_unlock_min=").Append(MinimumCraftingUnlockLevel.ToString(CultureInfo.InvariantCulture))
                .Append("\ncrafting_unlock_max=").Append(MaximumCraftingUnlockLevel.ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string label, IReadOnlyList<BalanceCountV1> values)
        {
            builder.Append('\n').Append(label).Append("_count=").Append(values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                builder.Append('\n').Append(label).Append('_').Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('=').Append(values[index].Key).Append('|').Append(values[index].Count.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static ReadOnlyCollection<BalanceCountV1> CopyAndSort(IEnumerable<BalanceCountV1> values)
        {
            List<BalanceCountV1> copy = new List<BalanceCountV1>(values ?? Array.Empty<BalanceCountV1>());
            copy.Sort();
            return new ReadOnlyCollection<BalanceCountV1>(copy);
        }
    }
}
