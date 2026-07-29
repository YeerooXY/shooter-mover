using System;
using ShooterMover.RunLoot;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Rewards.RunLoots
{
    /// <summary>
    /// Optional presentation decorator for one immutable authoritative pickup snapshot.
    /// Returning false leaves the existing registry sprite presentation intact.
    /// </summary>
    public interface IRunLootPickupViewBinder
    {
        bool TryBindRunLoot(
            RunLootSnapshot immutablePickup,
            out string diagnostic);
    }

    /// <summary>
    /// Optional accepted-collection feedback. The caller invokes this only after the
    /// canonical authority accepts the exact collection or its exact replay.
    /// </summary>
    public interface IRunLootPickupAcceptedFeedback
    {
        bool TryPlayAcceptedCollectionFeedback(
            Transform attractionTarget,
            Action completed);
    }
}
