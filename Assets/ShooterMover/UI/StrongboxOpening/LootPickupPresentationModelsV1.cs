using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.UI.StrongboxOpening
{
    public enum LootPickupPresentationKindV1
    {
        Credits = 1,
        Scrap = 2,
        Strongbox = 3,
    }

    /// <summary>
    /// Immutable physical-pickup projection. It contains exact identities and display
    /// facts only; it cannot collect, grant, consume, or mutate a reward.
    /// </summary>
    public sealed class LootPickupPresentationV1
    {
        private LootPickupPresentationV1(
            StableId pickupStableId,
            StableId rewardInstanceStableId,
            RewardGrantKindV1 rewardKind,
            StableId contentStableId,
            long quantity,
            LootPickupPresentationKindV1 presentationKind,
            string label,
            StableId tierStableId,
            int tierNumber,
            float glowStrength)
        {
            PickupStableId = pickupStableId;
            RewardInstanceStableId = rewardInstanceStableId;
            RewardKind = rewardKind;
            ContentStableId = contentStableId;
            Quantity = quantity;
            PresentationKind = presentationKind;
            Label = label;
            TierStableId = tierStableId;
            TierNumber = tierNumber;
            GlowStrength = glowStrength;
        }

        public StableId PickupStableId { get; }
        public StableId RewardInstanceStableId { get; }
        public RewardGrantKindV1 RewardKind { get; }
        public StableId ContentStableId { get; }
        public long Quantity { get; }
        public LootPickupPresentationKindV1 PresentationKind { get; }
        public string Label { get; }
        public StableId TierStableId { get; }
        public int TierNumber { get; }
        public float GlowStrength { get; }
        public bool IsStrongbox { get { return PresentationKind == LootPickupPresentationKindV1.Strongbox; } }

        public static bool TryCreate(
            StableId pickupStableId,
            StableId rewardInstanceStableId,
            RewardGrantKindV1 rewardKind,
            StableId contentStableId,
            long quantity,
            out LootPickupPresentationV1 presentation,
            out string diagnostic)
        {
            presentation = null;
            diagnostic = string.Empty;
            if (pickupStableId == null)
            {
                diagnostic = "loot-presentation-pickup-id-missing";
                return false;
            }
            if (rewardInstanceStableId == null)
            {
                diagnostic = "loot-presentation-reward-instance-id-missing";
                return false;
            }
            if (contentStableId == null)
            {
                diagnostic = "loot-presentation-content-id-missing";
                return false;
            }
            if (quantity < 1L)
            {
                diagnostic = "loot-presentation-quantity-invalid";
                return false;
            }

            LootPickupPresentationKindV1 kind;
            string label;
            StableId tierStableId = null;
            int tierNumber = 0;
            float glowStrength;
            switch (rewardKind)
            {
                case RewardGrantKindV1.Money:
                    kind = LootPickupPresentationKindV1.Credits;
                    label = "CREDITS";
                    glowStrength = 0.24f;
                    break;
                case RewardGrantKindV1.Scrap:
                    kind = LootPickupPresentationKindV1.Scrap;
                    label = "SCRAP";
                    glowStrength = 0.12f;
                    break;
                case RewardGrantKindV1.Strongbox:
                    ProductionStrongboxTierV1 tier;
                    if (!ProductionStrongboxCatalogV1.TryGet(contentStableId, out tier))
                    {
                        diagnostic = "loot-presentation-strongbox-tier-unknown:" + contentStableId;
                        return false;
                    }
                    kind = LootPickupPresentationKindV1.Strongbox;
                    label = tier.DisplayName + " STRONGBOX";
                    tierStableId = tier.TierStableId;
                    tierNumber = tier.TierNumber;
                    glowStrength = ProductionStrongboxCatalogV1.Tiers.Count <= 1
                        ? 1f
                        : 0.08f + (0.92f * (tierNumber - 1f)
                            / (ProductionStrongboxCatalogV1.Tiers.Count - 1f));
                    break;
                default:
                    diagnostic = "loot-presentation-reward-kind-unsupported:" + rewardKind;
                    return false;
            }

            presentation = new LootPickupPresentationV1(
                pickupStableId,
                rewardInstanceStableId,
                rewardKind,
                contentStableId,
                quantity,
                kind,
                label,
                tierStableId,
                tierNumber,
                glowStrength);
            return true;
        }
    }

    /// <summary>
    /// Immutable run-HUD totals. The caller supplies authority-derived values; this
    /// object deliberately exposes no increment or decrement operation.
    /// </summary>
    public sealed class RunLootTotalsPresentationV1
    {
        public RunLootTotalsPresentationV1(long credits, long scrap, long strongboxes)
        {
            if (credits < 0L) throw new ArgumentOutOfRangeException(nameof(credits));
            if (scrap < 0L) throw new ArgumentOutOfRangeException(nameof(scrap));
            if (strongboxes < 0L) throw new ArgumentOutOfRangeException(nameof(strongboxes));
            Credits = credits;
            Scrap = scrap;
            Strongboxes = strongboxes;
        }

        public long Credits { get; }
        public long Scrap { get; }
        public long Strongboxes { get; }
    }

    public static class RunLootTotalsProjectorV1
    {
        public static bool TryProject(
            IEnumerable<RunSessionCollectedRewardV1> immutableCollectedRewards,
            out RunLootTotalsPresentationV1 totals,
            out string diagnostic)
        {
            totals = null;
            diagnostic = string.Empty;
            if (immutableCollectedRewards == null)
            {
                diagnostic = "loot-presentation-run-rewards-null";
                return false;
            }

            long credits = 0L;
            long scrap = 0L;
            long strongboxes = 0L;
            var seenOperations = new HashSet<StableId>();
            try
            {
                foreach (RunSessionCollectedRewardV1 reward in immutableCollectedRewards)
                {
                    if (reward == null)
                    {
                        diagnostic = "loot-presentation-run-reward-null";
                        return false;
                    }
                    if (!seenOperations.Add(reward.CollectionOperationStableId))
                    {
                        diagnostic = "loot-presentation-run-reward-duplicate-operation:"
                            + reward.CollectionOperationStableId;
                        return false;
                    }
                    switch (reward.RewardKind)
                    {
                        case RewardGrantKindV1.Money:
                            credits = checked(credits + reward.Quantity);
                            break;
                        case RewardGrantKindV1.Scrap:
                            scrap = checked(scrap + reward.Quantity);
                            break;
                        case RewardGrantKindV1.Strongbox:
                            strongboxes = checked(strongboxes + reward.Quantity);
                            break;
                    }
                }
            }
            catch (OverflowException)
            {
                diagnostic = "loot-presentation-run-total-overflow";
                return false;
            }

            totals = new RunLootTotalsPresentationV1(credits, scrap, strongboxes);
            return true;
        }
    }

}
