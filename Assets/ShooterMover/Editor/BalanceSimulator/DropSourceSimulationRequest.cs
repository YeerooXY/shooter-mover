using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.EditorTools.BalanceSimulator
{
    public sealed class DropSourceSimulationRequest
    {
        private readonly ReadOnlyCollection<RewardSimulationParticipantInput>
            participants;
        private readonly ReadOnlyCollection<StableId> eventModifierIds;
        private readonly ReadOnlyCollection<RewardProfileOverride> eventOverrides;

        public DropSourceSimulationRequest(
            StableId sourceProfileReferenceId,
            IEnumerable<RewardSimulationParticipantInput> participants,
            int missionLevel,
            StableId difficultyStableId,
            StableId gameModeStableId,
            IEnumerable<StableId> eventModifierIds,
            int sourcesPerRoom,
            int roomCount,
            ulong seed,
            int sampleCount,
            int moneyQuantityMultiplierPermille,
            int scrapQuantityMultiplierPermille,
            RunDropPacingPolicy pacingPolicy,
            RewardProfileOverride gameModeOverride = null,
            RewardProfileOverride missionOverride = null,
            RewardProfileOverride difficultyOverride = null,
            IEnumerable<RewardProfileOverride> eventOverrides = null,
            RewardProfileOverride placementOverride = null)
        {
            SourceProfileReferenceId = sourceProfileReferenceId
                ?? throw new ArgumentNullException(nameof(sourceProfileReferenceId));
            DifficultyStableId = difficultyStableId
                ?? throw new ArgumentNullException(nameof(difficultyStableId));
            GameModeStableId = gameModeStableId
                ?? throw new ArgumentNullException(nameof(gameModeStableId));
            if (missionLevel < 0
                || sourcesPerRoom < 1
                || roomCount < 1
                || sampleCount < 1
                || moneyQuantityMultiplierPermille < 0
                || scrapQuantityMultiplierPermille < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(missionLevel));
            }
            this.participants = CopyParticipants(participants);
            this.eventModifierIds = CopyIds(eventModifierIds);
            this.eventOverrides = CopyOverrides(eventOverrides);
            MissionLevel = missionLevel;
            SourcesPerRoom = sourcesPerRoom;
            RoomCount = roomCount;
            Seed = seed;
            SampleCount = sampleCount;
            MoneyQuantityMultiplierPermille = moneyQuantityMultiplierPermille;
            ScrapQuantityMultiplierPermille = scrapQuantityMultiplierPermille;
            PacingPolicy = pacingPolicy
                ?? throw new ArgumentNullException(nameof(pacingPolicy));
            GameModeOverride = gameModeOverride;
            MissionOverride = missionOverride;
            DifficultyOverride = difficultyOverride;
            PlacementOverride = placementOverride;
        }

        public StableId SourceProfileReferenceId { get; }
        public IReadOnlyList<RewardSimulationParticipantInput> Participants
        {
            get { return participants; }
        }
        public int MissionLevel { get; }
        public StableId DifficultyStableId { get; }
        public StableId GameModeStableId { get; }
        public IReadOnlyList<StableId> EventModifierIds
        {
            get { return eventModifierIds; }
        }
        public int SourcesPerRoom { get; }
        public int RoomCount { get; }
        public ulong Seed { get; }
        public int SampleCount { get; }
        public int MoneyQuantityMultiplierPermille { get; }
        public int ScrapQuantityMultiplierPermille { get; }
        public RunDropPacingPolicy PacingPolicy { get; }
        public RewardProfileOverride GameModeOverride { get; }
        public RewardProfileOverride MissionOverride { get; }
        public RewardProfileOverride DifficultyOverride { get; }
        public IReadOnlyList<RewardProfileOverride> EventOverrides
        {
            get { return eventOverrides; }
        }
        public RewardProfileOverride PlacementOverride { get; }
        public int SourcesPerRun { get { return checked(SourcesPerRoom * RoomCount); } }

        private static ReadOnlyCollection<RewardSimulationParticipantInput>
            CopyParticipants(
                IEnumerable<RewardSimulationParticipantInput> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            var copy = new List<RewardSimulationParticipantInput>();
            var ids = new HashSet<StableId>();
            foreach (RewardSimulationParticipantInput participant in source)
            {
                if (participant == null
                    || !ids.Add(participant.ParticipantStableId))
                {
                    throw new ArgumentException(
                        "Simulation participants must be non-null and unique.",
                        nameof(source));
                }
                copy.Add(participant);
            }
            copy.Sort();
            if (copy.Count < 1 || copy.Count > 4)
            {
                throw new ArgumentException(
                    "Drop-source simulation supports one to four participants.",
                    nameof(source));
            }
            return new ReadOnlyCollection<RewardSimulationParticipantInput>(copy);
        }

        private static ReadOnlyCollection<StableId> CopyIds(
            IEnumerable<StableId> source)
        {
            var copy = new SortedSet<StableId>();
            if (source != null)
            {
                foreach (StableId value in source)
                {
                    if (value == null)
                    {
                        throw new ArgumentException(
                            "Event modifier IDs must not contain null entries.",
                            nameof(source));
                    }
                    copy.Add(value);
                }
            }
            return new ReadOnlyCollection<StableId>(new List<StableId>(copy));
        }

        private static ReadOnlyCollection<RewardProfileOverride> CopyOverrides(
            IEnumerable<RewardProfileOverride> source)
        {
            var copy = new List<RewardProfileOverride>();
            if (source != null)
            {
                foreach (RewardProfileOverride value in source)
                {
                    if (value == null)
                    {
                        throw new ArgumentException(
                            "Event overrides must not contain null entries.",
                            nameof(source));
                    }
                    copy.Add(value);
                }
            }
            copy.Sort();
            return new ReadOnlyCollection<RewardProfileOverride>(copy);
        }
    }
}
