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
    public enum BalanceSimulationMode
    {
        SingleOpen = 1,
        Batch = 2,
    }

    public sealed class BalanceSimulationRequest
    {
        public BalanceSimulationRequest(
            BalanceSimulationMode mode,
            int characterLevel,
            int strongboxTier,
            int strongboxLevel,
            int shopLevel,
            ulong deterministicSeed,
            int numberOfSimulations,
            long startingMoney,
            long startingScrap)
        {
            if (!Enum.IsDefined(typeof(BalanceSimulationMode), mode))
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
            NumberOfSimulations = mode == BalanceSimulationMode.SingleOpen ? 1 : numberOfSimulations;
            StartingMoney = startingMoney;
            StartingScrap = startingScrap;
        }

        public BalanceSimulationMode Mode { get; }
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

    public sealed class BalanceSimulationIterationRequest
    {
        public BalanceSimulationIterationRequest(
            BalanceSimulationRequest request,
            int iterationIndex,
            ulong iterationSeed)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            if (iterationIndex < 0) { throw new ArgumentOutOfRangeException(nameof(iterationIndex)); }
            IterationIndex = iterationIndex;
            IterationSeed = iterationSeed;
        }

        public BalanceSimulationRequest Request { get; }
        public int IterationIndex { get; }
        public ulong IterationSeed { get; }
    }

    public sealed class BalanceRewardObservation
    {
        public BalanceRewardObservation(string rewardType, long quantity)
        {
            if (string.IsNullOrWhiteSpace(rewardType)) { throw new ArgumentException("Reward type is required.", nameof(rewardType)); }
            if (quantity < 1L) { throw new ArgumentOutOfRangeException(nameof(quantity)); }
            RewardType = rewardType;
            Quantity = quantity;
        }

        public string RewardType { get; }
        public long Quantity { get; }
    }

    public sealed class BalanceEquipmentObservation
    {
        public BalanceEquipmentObservation(
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

    public sealed class BalanceRejection
    {
        public BalanceRejection(string system, string code, string detail)
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

    public sealed class BalanceSimulationIterationResult
    {
        private readonly ReadOnlyCollection<BalanceRewardObservation> rewards;
        private readonly ReadOnlyCollection<BalanceEquipmentObservation> equipment;
        private readonly ReadOnlyCollection<BalanceRejection> rejections;

        public BalanceSimulationIterationResult(
            int iterationIndex,
            ulong iterationSeed,
            IEnumerable<BalanceRewardObservation> rewards,
            IEnumerable<BalanceEquipmentObservation> equipment,
            long moneyDelta,
            long scrapDelta,
            long shopMoneyRequired,
            long craftingScrapRequired,
            long upgradeMoneyRequired,
            int softEligibleCandidateCount,
            int craftingUnlockLevel,
            IEnumerable<BalanceRejection> rejections)
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
        public IReadOnlyList<BalanceRewardObservation> Rewards { get { return rewards; } }
        public IReadOnlyList<BalanceEquipmentObservation> Equipment { get { return equipment; } }
        public long MoneyDelta { get; }
        public long ScrapDelta { get; }
        public long ShopMoneyRequired { get; }
        public long CraftingScrapRequired { get; }
        public long UpgradeMoneyRequired { get; }
        public int SoftEligibleCandidateCount { get; }
        public int CraftingUnlockLevel { get; }
        public IReadOnlyList<BalanceRejection> Rejections { get { return rejections; } }

        private static ReadOnlyCollection<T> Copy<T>(IEnumerable<T> values)
        {
            return new ReadOnlyCollection<T>(new List<T>(values ?? Array.Empty<T>()));
        }
    }

    public sealed class BalanceCount : IComparable<BalanceCount>
    {
        public BalanceCount(string key, long count, long total)
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
        public int CompareTo(BalanceCount other) { return ReferenceEquals(other, null) ? 1 : string.CompareOrdinal(Key, other.Key); }
    }

    public sealed class BalanceSimulationReport
    {
        private readonly ReadOnlyCollection<BalanceCount> rewardTypes;
        private readonly ReadOnlyCollection<BalanceCount> equipmentDefinitions;
        private readonly ReadOnlyCollection<BalanceCount> equipmentCategories;
        private readonly ReadOnlyCollection<BalanceCount> itemLevels;
        private readonly ReadOnlyCollection<BalanceCount> qualities;
        private readonly ReadOnlyCollection<BalanceCount> augmentCounts;
        private readonly ReadOnlyCollection<BalanceCount> augmentTiers;
        private readonly ReadOnlyCollection<BalanceCount> augmentLevels;
        private readonly ReadOnlyCollection<BalanceCount> rejections;
        private readonly ReadOnlyCollection<BalanceSimulationIterationResult> samples;

        internal BalanceSimulationReport(
            BalanceSimulationRequest request,
            IEnumerable<BalanceCount> rewardTypes,
            IEnumerable<BalanceCount> equipmentDefinitions,
            IEnumerable<BalanceCount> equipmentCategories,
            IEnumerable<BalanceCount> itemLevels,
            IEnumerable<BalanceCount> qualities,
            IEnumerable<BalanceCount> augmentCounts,
            IEnumerable<BalanceCount> augmentTiers,
            IEnumerable<BalanceCount> augmentLevels,
            IEnumerable<BalanceCount> rejections,
            IEnumerable<BalanceSimulationIterationResult> samples,
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
            this.samples = new ReadOnlyCollection<BalanceSimulationIterationResult>(new List<BalanceSimulationIterationResult>(samples ?? Array.Empty<BalanceSimulationIterationResult>()));
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
            Fingerprint = RewardGenerationFingerprint.Compute(BuildCanonicalString());
        }

        public BalanceSimulationRequest Request { get; }
        public IReadOnlyList<BalanceCount> RewardTypes { get { return rewardTypes; } }
        public IReadOnlyList<BalanceCount> EquipmentDefinitions { get { return equipmentDefinitions; } }
        public IReadOnlyList<BalanceCount> EquipmentCategories { get { return equipmentCategories; } }
        public IReadOnlyList<BalanceCount> ItemLevels { get { return itemLevels; } }
        public IReadOnlyList<BalanceCount> Qualities { get { return qualities; } }
        public IReadOnlyList<BalanceCount> AugmentCounts { get { return augmentCounts; } }
        public IReadOnlyList<BalanceCount> AugmentTiers { get { return augmentTiers; } }
        public IReadOnlyList<BalanceCount> AugmentLevels { get { return augmentLevels; } }
        public IReadOnlyList<BalanceCount> Rejections { get { return rejections; } }
        public IReadOnlyList<BalanceSimulationIterationResult> Samples { get { return samples; } }
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

        public long FindCount(IReadOnlyList<BalanceCount> values, string key)
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

        private static void Append(StringBuilder builder, string label, IReadOnlyList<BalanceCount> values)
        {
            builder.Append('\n').Append(label).Append("_count=").Append(values.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < values.Count; index++)
            {
                builder.Append('\n').Append(label).Append('_').Append(index.ToString("D4", CultureInfo.InvariantCulture))
                    .Append('=').Append(values[index].Key).Append('|').Append(values[index].Count.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static ReadOnlyCollection<BalanceCount> CopyAndSort(IEnumerable<BalanceCount> values)
        {
            List<BalanceCount> copy = new List<BalanceCount>(values ?? Array.Empty<BalanceCount>());
            copy.Sort();
            return new ReadOnlyCollection<BalanceCount>(copy);
        }
    }
}
