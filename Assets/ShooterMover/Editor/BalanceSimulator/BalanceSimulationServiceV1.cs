using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Equipment;

namespace ShooterMover.Editor.BalanceSimulator
{
    public interface IBalanceSimulationRuntimeV1
    {
        BalanceSimulationIterationResultV1 Run(BalanceSimulationIterationRequestV1 request);
    }

    public sealed class BalanceSimulationServiceV1
    {
        private static readonly StableId IterationPurpose = StableId.Parse("balance-simulator.iteration");
        private readonly IBalanceSimulationRuntimeV1 runtime;

        public BalanceSimulationServiceV1(IBalanceSimulationRuntimeV1 runtime)
        {
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        public BalanceSimulationReportV1 Run(BalanceSimulationRequestV1 request)
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
            List<BalanceSimulationIterationResultV1> samples = new List<BalanceSimulationIterationResultV1>();

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
                BalanceSimulationIterationResultV1 result = runtime.Run(
                    new BalanceSimulationIterationRequestV1(request, index, iterationSeed));
                if (result == null) { throw new InvalidOperationException("Balance runtime returned a null result."); }
                if (samples.Count < 20) { samples.Add(result); }

                for (int rewardIndex = 0; rewardIndex < result.Rewards.Count; rewardIndex++)
                {
                    Add(rewardTypes, result.Rewards[rewardIndex].RewardType, result.Rewards[rewardIndex].Quantity);
                }

                for (int equipmentIndex = 0; equipmentIndex < result.Equipment.Count; equipmentIndex++)
                {
                    BalanceEquipmentObservationV1 observation = result.Equipment[equipmentIndex];
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

            return new BalanceSimulationReportV1(
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

        private static IEnumerable<BalanceCountV1> ToCounts(Dictionary<string, long> values)
        {
            long total = 0L;
            foreach (long count in values.Values) { total = checked(total + count); }
            List<BalanceCountV1> result = new List<BalanceCountV1>();
            foreach (KeyValuePair<string, long> pair in values)
            {
                result.Add(new BalanceCountV1(pair.Key, pair.Value, total));
            }
            result.Sort();
            return result;
        }
    }
}
