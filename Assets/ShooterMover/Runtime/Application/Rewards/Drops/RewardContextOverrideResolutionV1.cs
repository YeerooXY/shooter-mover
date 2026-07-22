using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>
    /// Authored contextual override layers before TerminalDropBinding projects them
    /// into one effective personal reward profile.
    /// </summary>
    public sealed class RewardContextOverrideResolutionV1
    {
        private readonly ReadOnlyCollection<RewardProfileOverrideV1> eventOverrides;

        public RewardContextOverrideResolutionV1(
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
            var values = new List<RewardProfileOverrideV1>();
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
                    values.Add(value);
                }
            }
            values.Sort();
            this.eventOverrides =
                new ReadOnlyCollection<RewardProfileOverrideV1>(values);
        }

        public RewardProfileOverrideV1 GameModeOverride { get; }
        public RewardProfileOverrideV1 MissionOverride { get; }
        public RewardProfileOverrideV1 DifficultyOverride { get; }
        public IReadOnlyList<RewardProfileOverrideV1> EventOverrides
        {
            get { return eventOverrides; }
        }
        public RewardProfileOverrideV1 PlacementOverride { get; }
    }
}
