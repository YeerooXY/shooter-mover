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
    public sealed class TerminalRewardPlacementContext
    {
        public TerminalRewardPlacementContext(
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
    public sealed class TerminalRewardParticipant :
        IComparable<TerminalRewardParticipant>
    {
        public TerminalRewardParticipant(
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

        public int CompareTo(TerminalRewardParticipant other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : ParticipantStableId.CompareTo(other.ParticipantStableId);
        }
    }

    public sealed class TerminalRewardEligibilityPolicy
    {
        public TerminalRewardEligibilityPolicy(
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

        public bool IsEligible(TerminalRewardParticipant participant)
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

    public sealed class TerminalRewardEnvironment
    {
        private readonly ReadOnlyCollection<StableId> eventModifierIds;

        public TerminalRewardEnvironment(
            StableId gameModeStableId,
            IEnumerable<StableId> eventModifierIds,
            int moneyQuantityMultiplierPermille,
            int scrapQuantityMultiplierPermille,
            RunDropPacingPolicy pacingPolicy)
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
        public RunDropPacingPolicy PacingPolicy { get; }

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

    public sealed class TerminalRewardOverrideSet
    {
        private readonly ReadOnlyCollection<RewardProfileOverride> eventOverrides;

        public TerminalRewardOverrideSet(
            RewardProfileOverride gameModeOverride,
            RewardProfileOverride missionOverride,
            RewardProfileOverride difficultyOverride,
            IEnumerable<RewardProfileOverride> eventOverrides,
            RewardProfileOverride placementOverride)
        {
            GameModeOverride = gameModeOverride;
            MissionOverride = missionOverride;
            DifficultyOverride = difficultyOverride;
            PlacementOverride = placementOverride;
            var copy = new List<RewardProfileOverride>();
            if (eventOverrides != null)
            {
                foreach (RewardProfileOverride value in eventOverrides)
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
                new ReadOnlyCollection<RewardProfileOverride>(copy);
        }

        public RewardProfileOverride GameModeOverride { get; }
        public RewardProfileOverride MissionOverride { get; }
        public RewardProfileOverride DifficultyOverride { get; }
        public IReadOnlyList<RewardProfileOverride> EventOverrides
        {
            get { return eventOverrides; }
        }
        public RewardProfileOverride PlacementOverride { get; }

        public static TerminalRewardOverrideSet Empty()
        {
            return new TerminalRewardOverrideSet(
                null,
                null,
                null,
                Array.Empty<RewardProfileOverride>(),
                null);
        }
    }

    public interface ITerminalRewardParticipantResolver
    {
        bool TryResolve(
            TerminalDropSourceFact source,
            TerminalDropRunGenerationContext runContext,
            TerminalRewardPlacementContext placementContext,
            out IReadOnlyList<TerminalRewardParticipant> participants,
            out TerminalRewardEligibilityPolicy eligibilityPolicy,
            out string diagnostic);
    }

    public interface ITerminalRewardEnvironmentResolver
    {
        bool TryResolve(
            TerminalDropSourceFact source,
            TerminalDropRunGenerationContext runContext,
            out TerminalRewardEnvironment environment,
            out string diagnostic);
    }

    public interface ITerminalRewardOverrideResolver
    {
        bool TryResolve(
            TerminalDropSourceFact source,
            TerminalDropRunGenerationContext runContext,
            TerminalRewardEnvironment environment,
            TerminalRewardPlacementContext placementContext,
            out TerminalRewardOverrideSet overrides,
            out string diagnostic);
    }

    public enum TerminalPersonalRewardBatchStatus
    {
        Generated = 1,
        ExplicitNoDrop = 2,
        NoEligibleParticipants = 3,
        Rejected = 4,
    }

    public sealed class TerminalPersonalRewardBatch
    {
        private readonly ReadOnlyCollection<GeneratedTerminalDropResult> results;

        public TerminalPersonalRewardBatch(
            TerminalPersonalRewardBatchStatus status,
            TerminalDropSourceFact source,
            IEnumerable<GeneratedTerminalDropResult> results,
            string diagnostic)
        {
            if (!Enum.IsDefined(typeof(TerminalPersonalRewardBatchStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Source = source;
            var copy = new List<GeneratedTerminalDropResult>();
            if (results != null)
            {
                foreach (GeneratedTerminalDropResult result in results)
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
                GeneratedTerminalDropResult left,
                GeneratedTerminalDropResult right)
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
                new ReadOnlyCollection<GeneratedTerminalDropResult>(copy);
            Diagnostic = diagnostic ?? string.Empty;
        }

        public TerminalPersonalRewardBatchStatus Status { get; }
        public TerminalDropSourceFact Source { get; }
        public IReadOnlyList<GeneratedTerminalDropResult> Results
        {
            get { return results; }
        }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get { return Status != TerminalPersonalRewardBatchStatus.Rejected; }
        }
    }
}
