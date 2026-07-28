using System;
using ShooterMover.RunPickups;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunPickups
{
    /// <summary>
    /// Optional presentation decorator for one immutable authoritative pickup snapshot.
    /// Returning false leaves the existing registry sprite presentation intact.
    /// </summary>
    public interface IRunRewardPickupProjectionBinderV1
    {
        bool TryBindRunPickup(
            RunPickupSnapshotV1 immutablePickup,
            out string diagnostic);
    }

    /// <summary>
    /// Optional accepted-collection feedback. The caller invokes this only after the
    /// canonical authority accepts the exact collection or its exact replay.
    /// </summary>
    public interface IRunRewardPickupAcceptedFeedbackV1
    {
        bool TryPlayAcceptedCollectionFeedback(
            Transform attractionTarget,
            Action completed);
    }
}
