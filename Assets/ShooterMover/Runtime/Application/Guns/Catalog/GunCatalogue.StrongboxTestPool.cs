using ShooterMover.Domain.Guns;

namespace ShooterMover.Application.Guns.Catalog
{
    /// <summary>
    /// Synthetic deterministic catalogue depth used to exercise Strongbox rarity and level distribution.
    /// Creator identity currently lives in display names and authoring descriptions; no new schema field is implied.
    /// </summary>
    public static partial class GunCatalogue
    {
        private static GunFamily[] BuildStrongboxTestFamilies()
        {
            return new[]
            {
                BuildFamily(
                    "hv_kestrel",
                    "HV Kestrel",
                    "common",
                    new[] { 4, 29, 57 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
                BuildFamily(
                    "hv_breacher",
                    "HV Breacher",
                    "rare",
                    new[] { 18, 47, 73 },
                    ProvisionalGunTestProfile.Sweeper,
                    true),
                BuildFamily(
                    "hv_vanguard",
                    "HV Vanguard",
                    "legendary",
                    new[] { 52, 79, 104 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
                BuildFamily(
                    "teknova_spark",
                    "Teknova Spark",
                    "rare",
                    new[] { 11, 36, 64 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
                BuildFamily(
                    "teknova_pulse",
                    "Teknova Pulse",
                    "epic",
                    new[] { 27, 58, 83 },
                    ProvisionalGunTestProfile.Sweeper,
                    true),
                BuildFamily(
                    "teknova_sovereign",
                    "Teknova Sovereign",
                    "legendary",
                    new[] { 60, 87, 109 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
                BuildFamily(
                    "ronsen_cinder",
                    "Ronsen Cinder",
                    "common",
                    new[] { 7, 32, 55 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
                BuildFamily(
                    "ronsen_furnace",
                    "Ronsen Furnace",
                    "rare",
                    new[] { 24, 45, 76 },
                    ProvisionalGunTestProfile.Sweeper,
                    true),
                BuildFamily(
                    "ronsen_sunspike",
                    "Ronsen Sunspike",
                    "epic",
                    new[] { 41, 69, 96 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
                BuildFamily(
                    "virex_needle",
                    "Virex Needle",
                    "common",
                    new[] { 14, 38, 62 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
                BuildFamily(
                    "virex_corroder",
                    "Virex Corroder",
                    "epic",
                    new[] { 35, 65, 93 },
                    ProvisionalGunTestProfile.Sweeper,
                    true),
                BuildFamily(
                    "virex_apex",
                    "Virex Apex",
                    "artifact",
                    new[] { 72, 94, 110 },
                    ProvisionalGunTestProfile.Rattler,
                    true),
            };
        }
    }
}
