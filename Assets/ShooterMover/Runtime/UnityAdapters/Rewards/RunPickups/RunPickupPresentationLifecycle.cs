using System;
using ShooterMover.RunPickups;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunPickups
{
    /// <summary>
    /// Optional presentation decorator for one immutable authoritative pickup snapshot.
    /// Returning false leaves the existing registry sprite presentation intact.
    /// </summary>
    public interface IRunRewardPickupViewBinder
    {
        bool TryBindRunPickup(
            RunPickupSnapshot immutablePickup,
            out string diagnostic);
    }

    /// <summary>
    /// Optional accepted-collection feedback. The caller invokes this only after the
    /// canonical authority accepts the exact collection or its exact replay.
    /// </summary>
    public interface IRunRewardPickupAcceptedFeedback
    {
        bool TryPlayAcceptedCollectionFeedback(
            Transform attractionTarget,
            Action completed);
    }
}
