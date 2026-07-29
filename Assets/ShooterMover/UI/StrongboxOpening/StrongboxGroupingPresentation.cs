using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.UI.StrongboxOpening
{
    public sealed class OwnedStrongboxInstancePresentation
    {
        private OwnedStrongboxInstancePresentation(
            StableId instanceStableId,
            StrongboxTier tier)
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
            MissionRunStrongboxResult immutableResult,
            out OwnedStrongboxInstancePresentation instance,
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
            out OwnedStrongboxInstancePresentation instance,
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

            StrongboxTier tier;
            if (!StrongboxCatalog.TryGet(tierStableId, out tier))
            {
                diagnostic = "loot-presentation-box-tier-unknown:" + tierStableId;
                return false;
            }

            instance = new OwnedStrongboxInstancePresentation(instanceStableId, tier);
            return true;
        }
    }

    public sealed class OwnedStrongboxGroupPresentation
    {
        private readonly ReadOnlyCollection<OwnedStrongboxInstancePresentation> instances;

        internal OwnedStrongboxGroupPresentation(
            StrongboxTier tier,
            IEnumerable<OwnedStrongboxInstancePresentation> instances)
        {
            TierStableId = tier.TierStableId;
            TierNumber = tier.TierNumber;
            TierLabel = tier.DisplayName;
            this.instances = new ReadOnlyCollection<OwnedStrongboxInstancePresentation>(
                new List<OwnedStrongboxInstancePresentation>(instances));
        }

        public StableId TierStableId { get; }
        public int TierNumber { get; }
        public string TierLabel { get; }
        public int Quantity { get { return instances.Count; } }
        public IReadOnlyList<OwnedStrongboxInstancePresentation> Instances { get { return instances; } }
    }

    public static class StrongboxGroupingProjector
    {
        public static bool TryProjectUnopened(
            IEnumerable<MissionRunStrongboxResult> immutableResults,
            out IReadOnlyList<OwnedStrongboxGroupPresentation> groups,
            out string diagnostic)
        {
            groups = Array.Empty<OwnedStrongboxGroupPresentation>();
            diagnostic = string.Empty;
            if (immutableResults == null)
            {
                diagnostic = "loot-presentation-box-results-null";
                return false;
            }

            var instances = new List<OwnedStrongboxInstancePresentation>();
            foreach (MissionRunStrongboxResult result in immutableResults)
            {
                OwnedStrongboxInstancePresentation instance;
                if (!OwnedStrongboxInstancePresentation.TryCreate(
                    result, out instance, out diagnostic))
                {
                    return false;
                }
                instances.Add(instance);
            }
            return TryProject(instances, out groups, out diagnostic);
        }

        public static bool TryProject(
            IEnumerable<OwnedStrongboxInstancePresentation> exactInstances,
            out IReadOnlyList<OwnedStrongboxGroupPresentation> groups,
            out string diagnostic)
        {
            groups = Array.Empty<OwnedStrongboxGroupPresentation>();
            diagnostic = string.Empty;
            if (exactInstances == null)
            {
                diagnostic = "loot-presentation-box-instances-null";
                return false;
            }

            var seen = new HashSet<StableId>();
            var byTier = new Dictionary<StableId, List<OwnedStrongboxInstancePresentation>>();
            foreach (OwnedStrongboxInstancePresentation instance in exactInstances)
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

                List<OwnedStrongboxInstancePresentation> tierInstances;
                if (!byTier.TryGetValue(instance.TierStableId, out tierInstances))
                {
                    tierInstances = new List<OwnedStrongboxInstancePresentation>();
                    byTier.Add(instance.TierStableId, tierInstances);
                }
                tierInstances.Add(instance);
            }

            var projected = new List<OwnedStrongboxGroupPresentation>();
            foreach (KeyValuePair<StableId, List<OwnedStrongboxInstancePresentation>> pair in byTier)
            {
                StrongboxTier tier;
                if (!StrongboxCatalog.TryGet(pair.Key, out tier))
                {
                    diagnostic = "loot-presentation-box-tier-became-unknown:" + pair.Key;
                    return false;
                }
                pair.Value.Sort(delegate(
                    OwnedStrongboxInstancePresentation left,
                    OwnedStrongboxInstancePresentation right)
                {
                    return StringComparer.Ordinal.Compare(
                        left.InstanceStableId.ToString(),
                        right.InstanceStableId.ToString());
                });
                projected.Add(new OwnedStrongboxGroupPresentation(tier, pair.Value));
            }
            projected.Sort(delegate(
                OwnedStrongboxGroupPresentation left,
                OwnedStrongboxGroupPresentation right)
            {
                return left.TierNumber.CompareTo(right.TierNumber);
            });
            groups = new ReadOnlyCollection<OwnedStrongboxGroupPresentation>(projected);
            return true;
        }
    }

    /// <summary>
    /// Presentation-owned exact selection. Group counts remain projections and batch
    /// resolution returns identities without consuming or removing anything.
    /// </summary>
    public sealed class ExactStrongboxSelection
    {
        private readonly ReadOnlyCollection<OwnedStrongboxGroupPresentation> groups;
        private readonly Dictionary<StableId, OwnedStrongboxGroupPresentation> groupByInstance =
            new Dictionary<StableId, OwnedStrongboxGroupPresentation>();

        public ExactStrongboxSelection(
            IEnumerable<OwnedStrongboxGroupPresentation> projectedGroups)
        {
            if (projectedGroups == null) throw new ArgumentNullException(nameof(projectedGroups));
            var copy = new List<OwnedStrongboxGroupPresentation>();
            foreach (OwnedStrongboxGroupPresentation group in projectedGroups)
            {
                if (group == null) throw new ArgumentException("Groups must not contain null entries.", nameof(projectedGroups));
                copy.Add(group);
                for (int index = 0; index < group.Instances.Count; index++)
                {
                    OwnedStrongboxInstancePresentation instance = group.Instances[index];
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
            groups = new ReadOnlyCollection<OwnedStrongboxGroupPresentation>(copy);
        }

        public IReadOnlyList<OwnedStrongboxGroupPresentation> Groups { get { return groups; } }
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

            OwnedStrongboxGroupPresentation group = groupByInstance[SelectedInstanceStableId];
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

    public static class StrongboxPresentationPlayback
    {
        /// <summary>
        /// Completes only the current visual stages. It does not call the opening
        /// delegate and therefore cannot reroll, retry, grant, or consume anything.
        /// </summary>
        public static bool SkipToComplete(StrongboxOpeningSceneSession session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            if (session.Result == null || session.Result.Pending
                || session.Stage == StrongboxRevealStage.BoxClosed)
            {
                return false;
            }
            if (session.Stage == StrongboxRevealStage.OpeningAnimation)
            {
                session.Advance(session.Configuration.OpeningDurationSeconds);
            }
            if (session.Stage == StrongboxRevealStage.RewardReveal)
            {
                int count = session.Result == null ? 0 : session.Result.Items.Count;
                float revealDuration = Math.Max(0, count - 1)
                    * session.Configuration.RevealIntervalSeconds
                    + session.Configuration.RevealCompleteHoldSeconds;
                session.Advance(revealDuration);
            }
            return session.Stage == StrongboxRevealStage.ContinueOrBack;
        }
    }

}
