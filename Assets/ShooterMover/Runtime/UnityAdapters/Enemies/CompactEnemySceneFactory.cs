using System;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    [Obsolete("Use EnemySetup with EnemySpawner.SetSetup.")]
    public delegate void CompactEnemySceneFactoryDelegate(
        GameObject gameObject,
        LevelRooms roomOwner,
        StableId roomStableId,
        RoomPlacedEntityDefinition placement,
        int enemyLevel);

    /// <summary>
    /// Temporary source-compatible bridge for callers that have not yet moved to
    /// EnemySpawner. New code must use EnemySpawner directly.
    /// </summary>
    [Obsolete("Use EnemySpawner. This compatibility bridge will be removed.")]
    public static class CompactEnemySceneFactory
    {
        private static CompactEnemySceneFactoryDelegate legacySetup;
        private static EnemySetup setupAdapter;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            legacySetup = null;
            setupAdapter = null;
        }

        public static void Register(CompactEnemySceneFactoryDelegate configuredFactory)
        {
            if (configuredFactory == null)
            {
                throw new ArgumentNullException(nameof(configuredFactory));
            }

            if (legacySetup != null)
            {
                if (legacySetup != configuredFactory)
                {
                    throw new InvalidOperationException(
                        "compact-enemy-scene-factory-already-registered");
                }
                return;
            }

            legacySetup = configuredFactory;
            setupAdapter = delegate(
                GameObject gameObject,
                LevelRooms roomOwner,
                StableId roomStableId,
                RoomPlacedEntityDefinition placement,
                int enemyLevel)
            {
                legacySetup(
                    gameObject,
                    roomOwner,
                    roomStableId,
                    placement,
                    enemyLevel);
            };
            EnemySpawner.SetSetup(setupAdapter);
        }

        public static void SetCollisionRules(
            EnemyCollisionRulesFactory configuredFactory)
        {
            EnemySpawner.SetCollisionRules(configuredFactory);
        }

        public static void Configure(
            GameObject gameObject,
            LevelRooms roomOwner,
            StableId roomStableId,
            RoomPlacedEntityDefinition placement,
            int enemyLevel)
        {
            EnemySpawner.SetUp(
                gameObject,
                roomOwner,
                roomStableId,
                placement,
                enemyLevel);
        }
    }
}
