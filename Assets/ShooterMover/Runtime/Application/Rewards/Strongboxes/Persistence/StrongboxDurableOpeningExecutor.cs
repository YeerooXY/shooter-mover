using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public interface IStrongboxDurableOpeningExecutor
    {
        StrongboxOpeningResultLive OpenAndPersist(
            MissionRunStrongboxResult selectedStrongbox,
            ShooterMover.Application.Rewards.Strongboxes
                .StrongboxOpeningActions openingService,
            StrongboxOpenCommand command);
    }
}
