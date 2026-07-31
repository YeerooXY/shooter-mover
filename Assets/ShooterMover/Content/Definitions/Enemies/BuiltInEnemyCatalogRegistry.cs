using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;

namespace ShooterMover.Content.Definitions.Enemies
{
    /// <summary>
    /// Production validation references for the authored enemy catalogue. This registry
    /// describes supported content identities only; enemy statistics remain in JSON.
    /// </summary>
    public static class BuiltInEnemyCatalogRegistry
    {
        public static EnemyCatalogRegistry Create()
        {
            EnemyAttackParameterKinds projectile =
                EnemyAttackParameterKinds.Projectile;
            EnemyAttackParameterKinds projectileArea =
                EnemyAttackParameterKinds.Projectile
                | EnemyAttackParameterKinds.Area;
            EnemyAttackParameterKinds melee = EnemyAttackParameterKinds.Melee;

            return new EnemyCatalogRegistry(
                Ids(
                    "enemy-movement.mobile-positioning",
                    "enemy-movement.pursuit",
                    "enemy-movement.stationary"),
                Ids(
                    "enemy-decision.ranged-standard",
                    "enemy-decision.pounce-standard",
                    "enemy-decision.turret-standard",
                    "enemy-decision.contact-standard",
                    "enemy-decision.multi-attack-standard"),
                new[]
                {
                    Attack("enemy-attack.ranged-projectile", projectile, projectile),
                    Attack("enemy-attack.projectile-area", projectileArea, projectileArea),
                    Attack("enemy-attack.contact", melee, melee),
                    Attack("enemy-attack.pounce", melee, melee),
                },
                Ids(
                    "enemy-special.locked-commitment",
                    "enemy-special.rotating-aim"),
                Ids(
                    "presentation.enemy-mobile-blaster-droid",
                    "presentation.enemy-ram-pouncer",
                    "presentation.enemy-blaster-turret",
                    "presentation.enemy-pursuer-drone",
                    "presentation.enemy-hybrid-sentinel"),
                Ids(
                    "projectile.enemy-blaster",
                    "projectile.enemy-rocket"),
                Ids(
                    "damage.kinetic",
                    "damage.impact",
                    "damage.thermal"),
                Ids(
                    "xp.enemy-standard",
                    "xp.enemy-turret",
                    "xp.enemy-light"),
                Ids(
                    "drop-source.normal-enemy",
                    "drop-source.small-enemy",
                    "drop-source.large-enemy",
                    "drop-source.explicit-no-drop"));
        }

        private static EnemyAttackCapabilityRegistration Attack(
            string capabilityId,
            EnemyAttackParameterKinds required,
            EnemyAttackParameterKinds allowed)
        {
            return new EnemyAttackCapabilityRegistration(
                StableId.Parse(capabilityId),
                required,
                allowed);
        }

        private static StableId[] Ids(params string[] values)
        {
            var result = new StableId[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                result[index] = StableId.Parse(values[index]);
            }

            return result;
        }
    }
}
