using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.UI.StrongboxOpening
{
    public sealed class OwnedStrongboxInstancePresentationV1
    {
        private OwnedStrongboxInstancePresentationV1(
            StableId instanceStableId,
            ProductionStrongboxTierV1 tier)
        {
            InstanceStableId = instanceStableId;
            TierStableId = tier.TierStableId;
            TierNumber = tier.TierNumber;
            TierLabel = tier.DisplayName;
        }

        public StableId InstanceStableId { get; }
        public StableId TierStableId { get; }
        public int TierNumber { get; }
        public string TierLabel { get; }

        public static bool TryCreate(
            MissionRunStrongboxResultV1 immutableResult,
            out OwnedStrongboxInstancePresentationV1 instance,
            out string diagnostic)
        {
            instance = null;
            diagnostic = string.Empty;
            if (immutableResult == null)
            {
                diagnostic = "loot-presentation-box-result-null";
                return false;
            }
            if (!immutableResult.IsUnopened)
            {
                diagnostic = "loot-presentation-box-result-already-opened:"
                    + immutableResult.InstanceStableId;
                return false;
            }
            return TryCreate(
                immutableResult.InstanceStableId,
                immutableResult.DefinitionStableId,
                out instance,
                out diagnostic);
        }

        public static bool TryCreate(
            StableId instanceStableId,
            StableId tierStableId,
            out OwnedStrongboxInstancePresentationV1 instance,
            out string diagnostic)
        {
            instance = null;
            diagnostic = string.Empty;
            if (instanceStableId == null)
            {
                diagnostic = "loot-presentation-box-instance-id-missing";
                return false;
            }
            if (tierStableId == null)
            {
                diagnostic = "loot-presentation-box-tier-id-missing";
                return false;
            }

            ProductionStrongboxTierV1 tier;
            if (!ProductionStrongboxCatalogV1.TryGet(tierStableId, out tier))
            {
                diagnostic = "loot-presentation-box-tier-unknown:" + tierStableId;
                return false;
            }

            instance = new OwnedStrongboxInstancePresentationV1(instanceStableId, tier);
            return true;
        }
    }

    public sealed class OwnedStrongboxGroupPresentationV1
    {
        private readonly ReadOnlyCollection<OwnedStrongboxInstancePresentationV1> instances;

        internal OwnedStrongboxGroupPresentationV1(
            ProductionStrongboxTierV1 tier,
            IEnumerable<OwnedStrongboxInstancePresentationV1> instances)
        {
            TierStableId = tier.TierStableId;
            TierNumber = tier.TierNumber;
            TierLabel = tier.DisplayName;
            this.instances = new ReadOnlyCollection<OwnedStrongboxInstancePresentationV1>(
                new List<OwnedStrongboxInstancePresentationV1>(instances));
        }

        public StableId TierStableId { get; }
        public int TierNumber { get; }
        public string TierLabel { get; }
        public int Quantity { get { return instances.Count; } }
        public IReadOnlyList<OwnedStrongboxInstancePresentationV1> Instances { get { return instances; } }
    }

    public static class StrongboxGroupingProjectorV1
    {
        public static bool TryProjectUnopened(
            IEnumerable<MissionRunStrongboxResultV1> immutableResults,
            out IReadOnlyList<OwnedStrongboxGroupPresentationV1> groups,
            out string diagnostic)
        {
            groups = Array.Empty<OwnedStrongboxGroupPresentationV1>();
            diagnostic = string.Empty;
            if (immutableResults == null)
            {
                diagnostic = "loot-presentation-box-results-null";
                return false;
            }

            var instances = new List<OwnedStrongboxInstancePresentationV1>();
            foreach (MissionRunStrongboxResultV1 result in immutableResults)
            {
                OwnedStrongboxInstancePresentationV1 instance;
                if (!OwnedStrongboxInstancePresentationV1.TryCreate(
                    result, out instance, out diagnostic))
                {
                    return false;
                }
                instances.Add(instance);
            }
            return TryProject(instances, out groups, out diagnostic);
        }

        public static bool TryProject(
            IEnumerable<OwnedStrongboxInstancePresentationV1> exactInstances,
            out IReadOnlyList<OwnedStrongboxGroupPresentationV1> groups,
            out string diagnostic)
        {
            groups = Array.Empty<OwnedStrongboxGroupPresentationV1>();
            diagnostic = string.Empty;
            if (exactInstances == null)
            {
                diagnostic = "loot-presentation-box-instances-null";
                return false;
            }

            var seen = new HashSet<StableId>();
            var byTier = new Dictionary<StableId, List<OwnedStrongboxInstancePresentationV1>>();
            foreach (OwnedStrongboxInstancePresentationV1 instance in exactInstances)
            {
                if (instance == null)
                {
                    diagnostic = "loot-presentation-box-instance-null";
                    return false;
                }
                if (!seen.Add(instance.InstanceStableId))
                {
                    diagnostic = "loot-presentation-box-instance-duplicate:" + instance.InstanceStableId;
                    return false;
                }

                List<OwnedStrongboxInstancePresentationV1> tierInstances;
                if (!byTier.TryGetValue(instance.TierStableId, out tierInstances))
                {
                    tierInstances = new List<OwnedStrongboxInstancePresentationV1>();
                    byTier.Add(instance.TierStableId, tierInstances);
                }
                tierInstances.Add(instance);
            }

            var projected = new List<OwnedStrongboxGroupPresentationV1>();
            foreach (KeyValuePair<StableId, List<OwnedStrongboxInstancePresentationV1>> pair in byTier)
            {
                ProductionStrongboxTierV1 tier;
                if (!ProductionStrongboxCatalogV1.TryGet(pair.Key, out tier))
                {
                    diagnostic = "loot-presentation-box-tier-became-unknown:" + pair.Key;
                    return false;
                }
                pair.Value.Sort(delegate(
                    OwnedStrongboxInstancePresentationV1 left,
                    OwnedStrongboxInstancePresentationV1 right)
                {
                    return StringComparer.Ordinal.Compare(
                        left.InstanceStableId.ToString(),
                        right.InstanceStableId.ToString());
                });
                projected.Add(new OwnedStrongboxGroupPresentationV1(tier, pair.Value));
            }
            projected.Sort(delegate(
                OwnedStrongboxGroupPresentationV1 left,
                OwnedStrongboxGroupPresentationV1 right)
            {
                return left.TierNumber.CompareTo(right.TierNumber);
            });
            groups = new ReadOnlyCollection<OwnedStrongboxGroupPresentationV1>(projected);
            return true;
        }
    }

    /// <summary>
    /// Presentation-owned exact selection. Group counts remain projections and batch
    /// resolution returns identities without consuming or removing anything.
    /// </summary>
    public sealed class ExactStrongboxSelectionV1
    {
        private readonly ReadOnlyCollection<OwnedStrongboxGroupPresentationV1> groups;
        private readonly Dictionary<StableId, OwnedStrongboxGroupPresentationV1> groupByInstance =
            new Dictionary<StableId, OwnedStrongboxGroupPresentationV1>();

        public ExactStrongboxSelectionV1(
            IEnumerable<OwnedStrongboxGroupPresentationV1> projectedGroups)
        {
            if (projectedGroups == null) throw new ArgumentNullException(nameof(projectedGroups));
            var copy = new List<OwnedStrongboxGroupPresentationV1>();
            foreach (OwnedStrongboxGroupPresentationV1 group in projectedGroups)
            {
                if (group == null) throw new ArgumentException("Groups must not contain null entries.", nameof(projectedGroups));
                copy.Add(group);
                for (int index = 0; index < group.Instances.Count; index++)
                {
                    OwnedStrongboxInstancePresentationV1 instance = group.Instances[index];
                    if (groupByInstance.ContainsKey(instance.InstanceStableId))
                    {
                        throw new ArgumentException("Duplicate exact strongbox instance identity.", nameof(projectedGroups));
                    }
                    groupByInstance.Add(instance.InstanceStableId, group);
                    if (SelectedInstanceStableId == null)
                    {
                        SelectedInstanceStableId = instance.InstanceStableId;
                    }
                }
            }
            groups = new ReadOnlyCollection<OwnedStrongboxGroupPresentationV1>(copy);
        }

        public IReadOnlyList<OwnedStrongboxGroupPresentationV1> Groups { get { return groups; } }
        public StableId SelectedInstanceStableId { get; private set; }

        public bool TrySelectExact(StableId instanceStableId, out string diagnostic)
        {
            diagnostic = string.Empty;
            if (instanceStableId == null)
            {
                diagnostic = "loot-presentation-selection-id-missing";
                return false;
            }
            if (!groupByInstance.ContainsKey(instanceStableId))
            {
                diagnostic = "loot-presentation-selection-id-unknown:" + instanceStableId;
                return false;
            }
            SelectedInstanceStableId = instanceStableId;
            return true;
        }

        public IReadOnlyList<StableId> ResolveBatch(int requestedCount)
        {
            if (requestedCount < 1) throw new ArgumentOutOfRangeException(nameof(requestedCount));
            if (SelectedInstanceStableId == null)
            {
                return Array.Empty<StableId>();
            }

            OwnedStrongboxGroupPresentationV1 group = groupByInstance[SelectedInstanceStableId];
            var result = new List<StableId>(Math.Min(requestedCount, group.Quantity))
            {
                SelectedInstanceStableId,
            };
            for (int index = 0; index < group.Instances.Count && result.Count < requestedCount; index++)
            {
                StableId candidate = group.Instances[index].InstanceStableId;
                if (candidate != SelectedInstanceStableId)
                {
                    result.Add(candidate);
                }
            }
            return new ReadOnlyCollection<StableId>(result);
        }
    }

    public static class StrongboxPresentationPlaybackV1
    {
        /// <summary>
        /// Completes only the current visual stages. It does not call the opening
        /// delegate and therefore cannot reroll, retry, grant, or consume anything.
        /// </summary>
        public static bool SkipToComplete(StrongboxOpeningSceneSessionV1 session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.Result == null || session.Result.Pending
                || session.Stage == StrongboxRevealStageV1.BoxClosed)
            {
                return false;
            }
            if (session.Stage == StrongboxRevealStageV1.OpeningAnimation)
            {
                session.Advance(session.Configuration.OpeningDurationSeconds);
            }
            if (session.Stage == StrongboxRevealStageV1.RewardReveal)
            {
                int count = session.Result == null ? 0 : session.Result.Items.Count;
                float revealDuration = Math.Max(0, count - 1)
                    * session.Configuration.RevealIntervalSeconds
                    + session.Configuration.RevealCompleteHoldSeconds;
                session.Advance(revealDuration);
            }
            return session.Stage == StrongboxRevealStageV1.ContinueOrBack;
        }
    }

}
