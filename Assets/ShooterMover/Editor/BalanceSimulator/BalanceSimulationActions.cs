using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Editor.BalanceSimulator
{
    public interface IBalanceSimulationLive
    {
        BalanceSimulationIterationResult Run(BalanceSimulationIterationRequest request);
    }

    public sealed class BalanceSimulationActions
    {
        private static readonly StableId IterationPurpose = StableId.Parse("balance-simulator.iteration");
        private readonly IBalanceSimulationLive runtime;

        public BalanceSimulationActions(IBalanceSimulationLive runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public BalanceSimulationReport Run(BalanceSimulationRequest request)
        {
            if (request == null) { throw new ArgumentNullException(nameof(request)); }

            Dictionary<string, long> rewardTypes = new Dictionary<string, long>(StringComparer.Ordinal);
            Dictionary<string, long> definitions = new Dictionary<string, long>(StringComparer.Ordinal);
            Dictionary<string, long> categories = new Dictionary<string, long>(StringComparer.Ordinal);
            Dictionary<string, long> itemLevels = new Dictionary<string, long>(StringComparer.Ordinal);
            Dictionary<string, long> qualities = new Dictionary<string, long>(StringComparer.Ordinal);
            Dictionary<string, long> augmentCounts = new Dictionary<string, long>(StringComparer.Ordinal);
            Dictionary<string, long> augmentTiers = new Dictionary<string, long>(StringComparer.Ordinal);
            Dictionary<string, long> augmentLevels = new Dictionary<string, long>(StringComparer.Ordinal);
            Dictionary<string, long> rejections = new Dictionary<string, long>(StringComparer.Ordinal);
            HashSet<StableId> uniqueInstanceIds = new HashSet<StableId>();
            List<BalanceSimulationIterationResult> samples = new List<BalanceSimulationIterationResult>();

            long equipmentCount = 0L;
            long moneyDelta = 0L;
            long scrapDelta = 0L;
            long shopMoneyRequired = 0L;
            long craftingScrapRequired = 0L;
            long upgradeMoneyRequired = 0L;
            long softEligibleCandidates = 0L;
            int minimumCraftingUnlock = int.MaxValue;
            int maximumCraftingUnlock = 0;

            for (int index = 0; index < request.NumberOfSimulations; index++)
            {
                ulong iterationSeed = DeriveIterationSeed(request.DeterministicSeed, index);
                BalanceSimulationIterationResult result = runtime.Run(
                    new BalanceSimulationIterationRequest(request, index, iterationSeed));
                if (result == null) { throw new InvalidOperationException("Balance runtime returned a null result."); }
                if (samples.Count < 20) { samples.Add(result); }

                for (int rewardIndex = 0; rewardIndex < result.Rewards.Count; rewardIndex++)
                {
                    Add(rewardTypes, result.Rewards[rewardIndex].RewardType, result.Rewards[rewardIndex].Quantity);
                }

                for (int equipmentIndex = 0; equipmentIndex < result.Equipment.Count; equipmentIndex++)
                {
                    BalanceEquipmentObservation observation = result.Equipment[equipmentIndex];
                    EquipmentInstance instance = observation.Equipment;
                    equipmentCount++;
                    uniqueInstanceIds.Add(instance.InstanceId);
                    Add(definitions, instance.DefinitionId + " | " + observation.DefinitionDisplayName, 1L);
                    Add(categories, observation.CategoryId.ToString(), 1L);
                    Add(itemLevels, instance.ItemLevel.ToString(), 1L);
                    Add(qualities, instance.QualityId.ToString(), 1L);
                    Add(augmentCounts, instance.Augments.Count.ToString(), 1L);
                    for (int augmentIndex = 0; augmentIndex < instance.Augments.Count; augmentIndex++)
                    {
                        Add(augmentTiers, instance.Augments[augmentIndex].Tier.ToString(), 1L);
                        Add(augmentLevels, instance.Augments[augmentIndex].Level.ToString(), 1L);
                    }
                }

                for (int rejectionIndex = 0; rejectionIndex < result.Rejections.Count; rejectionIndex++)
                {
                    Add(rejections, result.Rejections[rejectionIndex].Key, 1L);
                }

                moneyDelta = checked(moneyDelta + result.MoneyDelta);
                scrapDelta = checked(scrapDelta + result.ScrapDelta);
                shopMoneyRequired = checked(shopMoneyRequired + result.ShopMoneyRequired);
                craftingScrapRequired = checked(craftingScrapRequired + result.CraftingScrapRequired);
                upgradeMoneyRequired = checked(upgradeMoneyRequired + result.UpgradeMoneyRequired);
                softEligibleCandidates = checked(softEligibleCandidates + result.SoftEligibleCandidateCount);
                minimumCraftingUnlock = Math.Min(minimumCraftingUnlock, result.CraftingUnlockLevel);
                maximumCraftingUnlock = Math.Max(maximumCraftingUnlock, result.CraftingUnlockLevel);
            }

            long duplicateDefinitions = 0L;
            foreach (KeyValuePair<string, long> pair in definitions)
            {
                if (pair.Value > 1L) { duplicateDefinitions = checked(duplicateDefinitions + pair.Value - 1L); }
            }

            return new BalanceSimulationReport(
                request,
                ToCounts(rewardTypes),
                ToCounts(definitions),
                ToCounts(categories),
                ToCounts(itemLevels),
                ToCounts(qualities),
                ToCounts(augmentCounts),
                ToCounts(augmentTiers),
                ToCounts(augmentLevels),
                ToCounts(rejections),
                samples,
                equipmentCount,
                uniqueInstanceIds.Count,
                duplicateDefinitions,
                moneyDelta,
                scrapDelta,
                shopMoneyRequired,
                craftingScrapRequired,
                upgradeMoneyRequired,
                softEligibleCandidates,
                minimumCraftingUnlock == int.MaxValue ? 0 : minimumCraftingUnlock,
                maximumCraftingUnlock);
        }

        private static ulong DeriveIterationSeed(ulong rootSeed, int iterationIndex)
        {
            DeterministicRandom stream = DeterministicRandom.Create(rootSeed)
                .Fork(IterationPurpose, checked((ulong)iterationIndex));
            stream.NextUInt64(out ulong seed);
            return seed;
        }

        private static void Add(IDictionary<string, long> values, string key, long quantity)
        {
            long current;
            values.TryGetValue(key, out current);
            values[key] = checked(current + quantity);
        }

        private static IEnumerable<BalanceCount> ToCounts(Dictionary<string, long> values)
        {
            long total = 0L;
            foreach (long count in values.Values) { total = checked(total + count); }
            List<BalanceCount> result = new List<BalanceCount>();
            foreach (KeyValuePair<string, long> pair in values)
            {
                result.Add(new BalanceCount(pair.Key, pair.Value, total));
            }
            result.Sort();
            return result;
        }
    }
}
