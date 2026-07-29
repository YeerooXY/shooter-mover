using System;
using System.Runtime.CompilerServices;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Runtime composition bridge keyed by the character-local canonical holdings authority. It
    /// lets retained UI seams resolve the one canonical mount authority without creating a second
    /// source of equipped truth.
    /// </summary>
    public static class WeaponMountLoadoutRegistry
    {
        private static readonly ConditionalWeakTable<
            WeaponHoldingsState,
            WeaponMountLoadoutState> authorities =
                new ConditionalWeakTable<
                    WeaponHoldingsState,
                    WeaponMountLoadoutState>();

        public static void Register(
            WeaponHoldingsState holdings,
            WeaponMountLoadoutState authority)
        {
            if (holdings == null) throw new ArgumentNullException(nameof(holdings));
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            authorities.Remove(holdings);
            authorities.Add(holdings, authority);
        }

        public static bool TryResolve(
            WeaponHoldingsState holdings,
            out WeaponMountLoadoutState authority)
        {
            authority = null;
            return holdings != null
                && authorities.TryGetValue(holdings, out authority)
                && authority != null;
        }
    }
}
