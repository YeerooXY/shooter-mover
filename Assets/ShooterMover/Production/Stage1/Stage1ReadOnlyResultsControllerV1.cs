using System;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Production.Stage1;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Production.Stage1
{
    /// <summary>
    /// Compatibility alias retained for DEMO-CUTOVER-001 callers.
    /// New code should use <see cref="Stage1MissionSummaryProjectionV1"/>.
    /// </summary>
    [Obsolete("Use Stage1MissionSummaryProjectionV1. The old results-projection name is retained only for source compatibility.")]
    internal sealed class Stage1ReadOnlyResultsProjectionV1 :
        Stage1MissionSummaryProjectionV1
    {
        public Stage1ReadOnlyResultsProjectionV1(
            MissionResultPayloadV1 result,
            string playerName,
            string className,
            int level,
            StableId participantStableId,
            int kills,
            long experience,
            long money,
            long scrap)
            : base(
                result,
                playerName,
                className,
                level,
                participantStableId,
                kills,
                experience,
                money,
                scrap)
        {
        }
    }

    /// <summary>
    /// Compatibility component retained so the cutover composition can migrate without
    /// replacing serialized Unity references. New code should use
    /// <see cref="Stage1MissionSummaryControllerV1"/>.
    /// </summary>
    [Obsolete("Use Stage1MissionSummaryControllerV1. The old read-only-results name is retained only for compatibility.")]
    [DisallowMultipleComponent]
    internal sealed class Stage1ReadOnlyResultsControllerV1 :
        Stage1MissionSummaryControllerV1
    {
    }
}
