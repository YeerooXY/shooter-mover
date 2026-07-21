using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public interface IStrongboxDurableOpeningExecutorV1
    {
        StrongboxOpeningResultRuntimeV1 OpenAndPersist(
            MissionRunStrongboxResultV1 selectedStrongbox,
            ShooterMover.Application.Rewards.Strongboxes
                .StrongboxOpeningServiceV1 openingService,
            StrongboxOpenCommandV1 command);
    }
}
