using System;
using System.Runtime.CompilerServices;

namespace ShooterMover.Application.Flow.Game
{
    /// <summary>
    /// Runtime composition bridge keyed by the character-local canonical holdings authority. It
    /// lets retained UI seams resolve the one canonical mount authority without creating a second
    /// source of equipped truth.
    /// </summary>
    public static class LoadoutRegistry
    {
        private static readonly ConditionalWeakTable<
            GunInventoryState,
            LoadoutState> authorities =
                new ConditionalWeakTable<
                    GunInventoryState,
                    LoadoutState>();

        public static void Register(
            GunInventoryState holdings,
            LoadoutState authority)
        {
            if (holdings == null) throw new ArgumentNullException(nameof(holdings));
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            authorities.Remove(holdings);
            authorities.Add(holdings, authority);
        }

        public static bool TryResolve(
            GunInventoryState holdings,
            out LoadoutState authority)
        {
            authority = null;
            return holdings != null
                && authorities.TryGetValue(holdings, out authority)
                && authority != null;
        }
    }
}
