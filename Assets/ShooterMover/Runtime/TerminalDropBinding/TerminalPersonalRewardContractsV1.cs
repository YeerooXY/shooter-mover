using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.TerminalDropBinding
{
    /// <summary>Exact room/placement identity supplied by the terminal-fact owner.</summary>
    public sealed class TerminalRewardPlacementContextV1
    {
        public TerminalRewardPlacementContextV1(
            StableId terminalEventStableId,
            StableId roomStableId,
            int roomLifecycleGeneration,
            StableId placementStableId,
            string fingerprint)
        {
            TerminalEventStableId = terminalEventStableId
                ?? throw new ArgumentNullException(nameof(terminalEventStableId));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            PlacementStableId = placementStableId
                ?? throw new ArgumentNullException(nameof(placementStableId));
            if (roomLifecycleGeneration < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(roomLifecycleGeneration));
            }
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                throw new ArgumentException(
                    "A canonical room/placement fingerprint is required.",
                    nameof(fingerprint));
            }
            RoomLifecycleGeneration = roomLifecycleGeneration;
            Fingerprint = fingerprint.Trim();
        }

        public StableId TerminalEventStableId { get; }
        public StableId RoomStableId { get; }
        public int RoomLifecycleGeneration { get; }
        public StableId PlacementStableId { get; }
        public string Fingerprint { get; }
    }

    /// <summary>Frozen participant eligibility facts for one shared terminal event.</summary>
    public sealed class TerminalRewardParticipantV1 :
        IComparable<TerminalRewardParticipantV1>
    {
        public TerminalRewardParticipantV1(
            StableId participantStableId,
            int playerLevel,
            bool activeInRun,
            bool connectedOrReconnectReserved,
            bool presentInRoom,
            bool contributionEligible,
            bool spectator)
        {
            ParticipantStableId = participantStableId
                ?? throw new ArgumentNullException(nameof(participantStableId));
            if (playerLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }
            PlayerLevel = playerLevel;
            ActiveInRun = activeInRun;
            ConnectedOrReconnectReserved = connectedOrReconnectReserved;
            PresentInRoom = presentInRoom;
            ContributionEligible = contributionEligible;
            Spectator = spectator;
        }

        public StableId ParticipantStableId { get; }
        public int PlayerLevel { get; }
        public bool ActiveInRun { get; }
        public bool ConnectedOrReconnectReserved { get; }
        public bool PresentInRoom { get; }
        public bool ContributionEligible { get; }
        public bool Spectator { get; }

        public int CompareTo(TerminalRewardParticipantV1 other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : ParticipantStableId.CompareTo(other.ParticipantStableId);
        }
    }

    public sealed class TerminalRewardEligibilityPolicyV1
    {
        public TerminalRewardEligibilityPolicyV1(
            bool requireRoomPresence,
            bool requireContribution,
            bool allowSpectators)
        {
            RequireRoomPresence = requireRoomPresence;
            RequireContribution = requireContribution;
            AllowSpectators = allowSpectators;
        }

        public bool RequireRoomPresence { get; }
        public bool RequireContribution { get; }
        public bool AllowSpectators { get; }

        public bool IsEligible(TerminalRewardParticipantV1 participant)
        {
            if (participant == null
                || !participant.ActiveInRun
                || !participant.ConnectedOrReconnectReserved)
            {
                return false;
            }
            if (!AllowSpectators && participant.Spectator)
            {
                return false;
            }
            if (RequireRoomPresence && !participant.PresentInRoom)
            {
                return false;
            }
            return !RequireContribution || participant.ContributionEligible;
        }
    }

    public sealed class TerminalRewardEnvironmentV1
    {
        private readonly ReadOnlyCollection<StableId> eventModifierIds;

        public TerminalRewardEnvironmentV1(
            StableId gameModeStableId,
            IEnumerable<StableId> eventModifierIds,
            int moneyQuantityMultiplierPermille,
            int scrapQuantityMultiplierPermille,
            RunDropPacingPolicyV1 pacingPolicy)
        {
            GameModeStableId = gameModeStableId
                ?? throw new ArgumentNullException(nameof(gameModeStableId));
            if (moneyQuantityMultiplierPermille < 0
                || scrapQuantityMultiplierPermille < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(moneyQuantityMultiplierPermille));
            }
            MoneyQuantityMultiplierPermille = moneyQuantityMultiplierPermille;
            ScrapQuantityMultiplierPermille = scrapQuantityMultiplierPermille;
            PacingPolicy = pacingPolicy
                ?? throw new ArgumentNullException(nameof(pacingPolicy));
            this.eventModifierIds = CopyIds(eventModifierIds);
        }

        public StableId GameModeStableId { get; }
        public IReadOnlyList<StableId> EventModifierIds
        {
            get { return eventModifierIds; }
        }
        public int MoneyQuantityMultiplierPermille { get; }
        public int ScrapQuantityMultiplierPermille { get; }
        public RunDropPacingPolicyV1 PacingPolicy { get; }

        private static ReadOnlyCollection<StableId> CopyIds(
            IEnumerable<StableId> source)
        {
            var values = new SortedSet<StableId>();
            if (source != null)
            {
                foreach (StableId value in source)
                {
                    if (value == null)
                    {
                        throw new ArgumentException(
                            "Event modifier identities must not contain null entries.",
                            nameof(source));
                    }
                    values.Add(value);
                }
            }
            return new ReadOnlyCollection<StableId>(
                new List<StableId>(values));
        }
    }

    public sealed class TerminalRewardOverrideSetV1
    {
        private readonly ReadOnlyCollection<RewardProfileOverrideV1> eventOverrides;

        public TerminalRewardOverrideSetV1(
            RewardProfileOverrideV1 gameModeOverride,
            RewardProfileOverrideV1 missionOverride,
            RewardProfileOverrideV1 difficultyOverride,
            IEnumerable<RewardProfileOverrideV1> eventOverrides,
            RewardProfileOverrideV1 placementOverride)
        {
            GameModeOverride = gameModeOverride;
            MissionOverride = missionOverride;
            DifficultyOverride = difficultyOverride;
            PlacementOverride = placementOverride;
            var copy = new List<RewardProfileOverrideV1>();
            if (eventOverrides != null)
            {
                foreach (RewardProfileOverrideV1 value in eventOverrides)
                {
                    if (value == null)
                    {
                        throw new ArgumentException(
                            "Event overrides must not contain null entries.",
                            nameof(eventOverrides));
                    }
                    copy.Add(value);
                }
            }
            copy.Sort();
            this.eventOverrides =
                new ReadOnlyCollection<RewardProfileOverrideV1>(copy);
        }

        public RewardProfileOverrideV1 GameModeOverride { get; }
        public RewardProfileOverrideV1 MissionOverride { get; }
        public RewardProfileOverrideV1 DifficultyOverride { get; }
        public IReadOnlyList<RewardProfileOverrideV1> EventOverrides
        {
            get { return eventOverrides; }
        }
        public RewardProfileOverrideV1 PlacementOverride { get; }

        public static TerminalRewardOverrideSetV1 Empty()
        {
            return new TerminalRewardOverrideSetV1(
                null,
                null,
                null,
                Array.Empty<RewardProfileOverrideV1>(),
                null);
        }
    }

    public interface ITerminalRewardParticipantResolverV1
    {
        bool TryResolve(
            TerminalDropSourceFactV1 source,
            TerminalDropRunGenerationContextV1 runContext,
            TerminalRewardPlacementContextV1 placementContext,
            out IReadOnlyList<TerminalRewardParticipantV1> participants,
            out TerminalRewardEligibilityPolicyV1 eligibilityPolicy,
            out string diagnostic);
    }

    public interface ITerminalRewardEnvironmentResolverV1
    {
        bool TryResolve(
            TerminalDropSourceFactV1 source,
            TerminalDropRunGenerationContextV1 runContext,
            out TerminalRewardEnvironmentV1 environment,
            out string diagnostic);
    }

    public interface ITerminalRewardOverrideResolverV1
    {
        bool TryResolve(
            TerminalDropSourceFactV1 source,
            TerminalDropRunGenerationContextV1 runContext,
            TerminalRewardEnvironmentV1 environment,
            TerminalRewardPlacementContextV1 placementContext,
            out TerminalRewardOverrideSetV1 overrides,
            out string diagnostic);
    }

    public enum TerminalPersonalRewardBatchStatusV1
    {
        Generated = 1,
        ExplicitNoDrop = 2,
        NoEligibleParticipants = 3,
        Rejected = 4,
    }

    public sealed class TerminalPersonalRewardBatchV1
    {
        private readonly ReadOnlyCollection<GeneratedTerminalDropResultV1> results;

        public TerminalPersonalRewardBatchV1(
            TerminalPersonalRewardBatchStatusV1 status,
            TerminalDropSourceFactV1 source,
            IEnumerable<GeneratedTerminalDropResultV1> results,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(TerminalPersonalRewardBatchStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Source = source;
            var copy = new List<GeneratedTerminalDropResultV1>();
            if (results != null)
            {
                foreach (GeneratedTerminalDropResultV1 result in results)
                {
                    if (result == null)
                    {
                        throw new ArgumentException(
                            "Personal terminal results must not contain null entries.",
                            nameof(results));
                    }
                    copy.Add(result);
                }
            }
            copy.Sort(delegate(
                GeneratedTerminalDropResultV1 left,
                GeneratedTerminalDropResultV1 right)
            {
                StableId leftId = left.SourceFact == null
                    ? null
                    : left.SourceFact.AttributedParticipantStableId;
                StableId rightId = right.SourceFact == null
                    ? null
                    : right.SourceFact.AttributedParticipantStableId;
                if (leftId == null) return rightId == null ? 0 : -1;
                return rightId == null ? 1 : leftId.CompareTo(rightId);
            });
            this.results =
                new ReadOnlyCollection<GeneratedTerminalDropResultV1>(copy);
            Diagnostic = diagnostic ?? string.Empty;
        }

        public TerminalPersonalRewardBatchStatusV1 Status { get; }
        public TerminalDropSourceFactV1 Source { get; }
        public IReadOnlyList<GeneratedTerminalDropResultV1> Results
        {
            get { return results; }
        }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get { return Status != TerminalPersonalRewardBatchStatusV1.Rejected; }
        }
    }
}
