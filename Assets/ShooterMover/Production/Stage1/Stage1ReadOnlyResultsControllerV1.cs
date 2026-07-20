using System.ComponentModel;
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
    [EditorBrowsable(EditorBrowsableState.Never)]
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
    [EditorBrowsable(EditorBrowsableState.Never)]
    [DisallowMultipleComponent]
    internal sealed class Stage1ReadOnlyResultsControllerV1 :
        Stage1MissionSummaryControllerV1
    {
        private new void Update()
        {
            base.Update();
        }

        private new void OnGUI()
        {
            base.OnGUI();
        }

        private new void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}
