using ShooterMover.Domain.Common;

namespace ShooterMover.Content.Definitions.Enemies
{
    /// <summary>
    /// Content-owned behavior attached to the built-in enemy projectile profiles.
    /// Runtime adapters consume this typed profile instead of inferring rocket behavior
    /// from an enemy name, prefab, room, or attack capability.
    /// </summary>
    public static class BuiltInEnemyProjectileProfiles
    {
        private static readonly StableId Rocket =
            StableId.Parse("projectile.enemy-rocket");

        public const double RocketDamageOverTimeTotalDamage = 2d;
        public const double RocketDamageOverTimeDurationSeconds = 2d;
        public const int RocketDamageOverTimeTickCount = 2;

        public static StableId RocketProfileId
        {
            get { return Rocket; }
        }

        public static bool IsRocket(StableId projectileProfileId)
        {
            return projectileProfileId != null
                && projectileProfileId == Rocket;
        }
    }
}
