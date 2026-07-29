using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Application.Runs.Session;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.UI.StrongboxOpening
{
    public enum LootPickupPresentationKind
    {
        Credits = 1,
        Scrap = 2,
        Strongbox = 3,
    }

    /// <summary>
    /// Immutable physical-pickup projection. It contains exact identities and display
    /// facts only; it cannot collect, grant, consume, or mutate a reward.
    /// </summary>
    public sealed class LootPickupPresentation
    {
        private LootPickupPresentation(
            StableId pickupStableId,
            StableId rewardInstanceStableId,
            RewardGrantKind rewardKind,
            StableId contentStableId,
            long quantity,
            LootPickupPresentationKind presentationKind,
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
        public RewardGrantKind RewardKind { get; }
        public StableId ContentStableId { get; }
        public long Quantity { get; }
        public LootPickupPresentationKind PresentationKind { get; }
        public string Label { get; }
        public StableId TierStableId { get; }
        public int TierNumber { get; }
        public float GlowStrength { get; }
        public bool IsStrongbox { get { return PresentationKind == LootPickupPresentationKind.Strongbox; } }

        public static bool TryCreate(
            StableId pickupStableId,
            StableId rewardInstanceStableId,
            RewardGrantKind rewardKind,
            StableId contentStableId,
            long quantity,
            out LootPickupPresentation presentation,
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

            LootPickupPresentationKind kind;
            string label;
            StableId tierStableId = null;
            int tierNumber = 0;
            float glowStrength;
            switch (rewardKind)
            {
                case RewardGrantKind.Money:
                    kind = LootPickupPresentationKind.Credits;
                    label = "CREDITS";
                    glowStrength = 0.24f;
                    break;
                case RewardGrantKind.Scrap:
                    kind = LootPickupPresentationKind.Scrap;
                    label = "SCRAP";
                    glowStrength = 0.12f;
                    break;
                case RewardGrantKind.Strongbox:
                    StrongboxTier tier;
                    if (!StrongboxCatalog.TryGet(contentStableId, out tier))
                    {
                        diagnostic = "loot-presentation-strongbox-tier-unknown:" + contentStableId;
                        return false;
                    }
                    kind = LootPickupPresentationKind.Strongbox;
                    label = tier.DisplayName + " STRONGBOX";
                    tierStableId = tier.TierStableId;
                    tierNumber = tier.TierNumber;
                    glowStrength = StrongboxCatalog.Tiers.Count <= 1
                        ? 1f
                        : 0.08f + (0.92f * (tierNumber - 1f)
                            / (StrongboxCatalog.Tiers.Count - 1f));
                    break;
                default:
                    diagnostic = "loot-presentation-reward-kind-unsupported:" + rewardKind;
                    return false;
            }

            presentation = new LootPickupPresentation(
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
    public sealed class RunLootTotalsPresentation
    {
        public RunLootTotalsPresentation(long credits, long scrap, long strongboxes)
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

    public static class RunLootTotalsProjector
    {
        public static bool TryProject(
            IEnumerable<RunSessionCollectedReward> immutableCollectedRewards,
            out RunLootTotalsPresentation totals,
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
                foreach (RunSessionCollectedReward reward in immutableCollectedRewards)
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
                        case RewardGrantKind.Money:
                            credits = checked(credits + reward.Quantity);
                            break;
                        case RewardGrantKind.Scrap:
                            scrap = checked(scrap + reward.Quantity);
                            break;
                        case RewardGrantKind.Strongbox:
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

            totals = new RunLootTotalsPresentation(credits, scrap, strongboxes);
            return true;
        }
    }

}
