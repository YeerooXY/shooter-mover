using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>
    /// Authored contextual override layers before LootDropBinding projects them
    /// into one effective personal reward profile.
    /// </summary>
    public sealed class RewardContextOverrideResolution
    {
        private readonly ReadOnlyCollection<RewardProfileOverride> eventOverrides;

        public RewardContextOverrideResolution(
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
            var values = new List<RewardProfileOverride>();
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
                    values.Add(value);
                }
            }
            values.Sort();
            this.eventOverrides =
                new ReadOnlyCollection<RewardProfileOverride>(values);
        }

        public RewardProfileOverride GameModeOverride { get; }
        public RewardProfileOverride MissionOverride { get; }
        public RewardProfileOverride DifficultyOverride { get; }
        public IReadOnlyList<RewardProfileOverride> EventOverrides
        {
            get { return eventOverrides; }
        }
        public RewardProfileOverride PlacementOverride { get; }
    }
}
