using System;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Game-facing entry point for enemy scene setup.
    ///
    /// The older CompactEnemySceneFactory remains behind this boundary only until its
    /// existing room-spawn callers can be migrated together with the wider CompactEnemy
    /// family and Unity serialization has been verified.
    /// </summary>
    public static class EnemySpawner
    {
        public static void SetCollisionRules(
            EnemyCollisionRulesFactory createRules)
        {
            if (createRules == null)
            {
                throw new ArgumentNullException(nameof(createRules));
            }

            CompactEnemySceneFactory.SetCollisionRules(createRules);
        }
    }
}
