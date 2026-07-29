using System;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Runs.Session
{
    /// <summary>
    /// Typed immutable snapshot source implemented by the existing mission-result port.
    /// Downstream persistence never inspects private authority fields.
    /// </summary>
    public interface IRunMissionStrongboxSnapshotSource
    {
        PlayerHoldingsSnapshot ExportCollectedStrongboxHoldings();
        StrongboxOpeningSnapshot ExportCollectedStrongboxRegistrations();
    }

    /// <summary>
    /// Optional binding for downstream mission-result decorators that must validate the
    /// exact lifecycle generation owned by the enclosing Run Session aggregate.
    /// </summary>
    public interface IRunMissionResultLifecycleBinding
    {
        void BindRunLifecycle(
            StableId runStableId,
            Func<long> lifecycleGenerationExporter);
    }

    /// <summary>
    /// Optional retry classification consumed by Run Session. A port may keep the same
    /// End command retryable after an inner mission result has already terminalized when
    /// a downstream compensated transaction failed transiently.
    /// </summary>
    public interface IRunMissionResultEndRetryPolicy
    {
        bool IsRetryableEndFailure(
            EndRunSessionCommand command,
            MissionRunStateResult result);
    }
}
