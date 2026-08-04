using System;
using UnityEngine;

namespace ShooterMover.UI.Game
{
    /// <summary>
    /// The authored walkable floor for the room currently presented to the player.
    /// Movement rules can use it without creating physical colliders, so projectiles
    /// continue to pass over missing-floor and no-move cells.
    /// </summary>
    internal static class RoomFloor
    {
        private static FloorGrid current;
        private static int revision;

        internal static void Set(FloorGrid floor)
        {
            current = floor ?? throw new ArgumentNullException(nameof(floor));
            revision = revision == int.MaxValue ? 1 : revision + 1;
        }

        internal static bool TryGet(
            out FloorGrid floor,
            out int currentRevision)
        {
            floor = current;
            currentRevision = revision;
            return floor != null;
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            current = null;
            revision = 0;
        }
    }
}
