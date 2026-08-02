using ShooterMover.Domain.Guns;

namespace ShooterMover.Application.Guns.Catalog
{
    /// <summary>
    /// Synthetic early-game depth used to exercise Strongbox rarity at levels one through ten.
    /// All families remain three-Mark because the current canonical and flat catalogues require it.
    /// </summary>
    public static partial class GunCatalogue
    {
        private static GunFamily[] BuildLowLevelStrongboxTestFamilies()
        {
            return new[]
            {
                BuildFamily(
                    "hv_finch",
                    "HV Finch",
                    "common",
                    new[] { 1, 4, 8 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
                BuildFamily(
                    "hv_buckler",
                    "HV Buckler",
                    "rare",
                    new[] { 2, 6, 10 },
                    ProvisionalGunTestProfile.Sweeper,
                    true),
                BuildFamily(
                    "teknova_flicker",
                    "Teknova Flicker",
                    "common",
                    new[] { 1, 5, 9 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
                BuildFamily(
                    "teknova_vector",
                    "Teknova Vector",
                    "epic",
                    new[] { 3, 7, 10 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
                BuildFamily(
                    "ronsen_ember",
                    "Ronsen Ember",
                    "common",
                    new[] { 2, 5, 8 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
                BuildFamily(
                    "ronsen_ashmaker",
                    "Ronsen Ashmaker",
                    "rare",
                    new[] { 3, 6, 9 },
                    ProvisionalGunTestProfile.Sweeper,
                    true),
                BuildFamily(
                    "virex_thorn",
                    "Virex Thorn",
                    "rare",
                    new[] { 1, 4, 7 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
                BuildFamily(
                    "virex_crown",
                    "Virex Crown",
                    "epic",
                    new[] { 2, 7, 10 },
                    ProvisionalGunTestProfile.Sweeper,
                    true),
                BuildFamily(
                    "hv_paragon",
                    "HV Paragon",
                    "legendary",
                    new[] { 2, 6, 10 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
                BuildFamily(
                    "ronsen_warden",
                    "Ronsen Warden",
                    "legendary",
                    new[] { 4, 8, 10 },
                    ProvisionalGunTestProfile.Sweeper,
                    true),
                BuildFamily(
                    "teknova_singularity",
                    "Teknova Singularity",
                    "artifact",
                    new[] { 3, 7, 10 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
            };
        }
    }
}
