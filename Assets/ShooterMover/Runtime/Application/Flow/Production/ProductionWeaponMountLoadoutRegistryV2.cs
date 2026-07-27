using System;
using System.Runtime.CompilerServices;

namespace ShooterMover.Application.Flow.Production
{
    /// <summary>
    /// Runtime composition bridge keyed by the character-local canonical holdings authority. It
    /// lets retained UI seams resolve the one canonical mount authority without creating a second
    /// source of equipped truth.
    /// </summary>
    public static class ProductionWeaponMountLoadoutRegistryV2
    {
        private static readonly ConditionalWeakTable<
            ProductionWeaponHoldingsAuthorityV2,
            ProductionWeaponMountLoadoutAuthorityV2> authorities =
                new ConditionalWeakTable<
                    ProductionWeaponHoldingsAuthorityV2,
                    ProductionWeaponMountLoadoutAuthorityV2>();

        public static void Register(
            ProductionWeaponHoldingsAuthorityV2 holdings,
            ProductionWeaponMountLoadoutAuthorityV2 authority)
        {
            if (holdings == null) throw new ArgumentNullException(nameof(holdings));
            if (authority == null) throw new ArgumentNullException(nameof(authority));
            authorities.Remove(holdings);
            authorities.Add(holdings, authority);
        }

        public static bool TryResolve(
            ProductionWeaponHoldingsAuthorityV2 holdings,
            out ProductionWeaponMountLoadoutAuthorityV2 authority)
        {
            authority = null;
            return holdings != null
                && authorities.TryGetValue(holdings, out authority)
                && authority != null;
        }
    }
}
