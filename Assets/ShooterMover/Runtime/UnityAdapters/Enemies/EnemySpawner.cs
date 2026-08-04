using System;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    public delegate void EnemySetup(
        GameObject gameObject,
        LevelRooms roomOwner,
        StableId roomStableId,
        RoomPlacedEntityDefinition placement,
        int enemyLevel);

    public delegate void EnemyCollisionRulesFactory(GameObject gameObject);

    /// <summary>
    /// Game-facing entry point for setting up enemies created by a room.
    /// It owns the setup callback, collision rules, and room containment applied to
    /// each newly created enemy object.
    /// </summary>
    public static class EnemySpawner
    {
        private static EnemySetup setup;
        private static EnemyCollisionRulesFactory collisionRulesFactory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            setup = null;
            collisionRulesFactory = null;
        }

        public static void SetSetup(EnemySetup configuredSetup)
        {
            if (configuredSetup == null)
            {
                throw new ArgumentNullException(nameof(configuredSetup));
            }

            if (setup != null && setup != configuredSetup)
            {
                throw new InvalidOperationException(
                    "compact-enemy-scene-factory-already-registered");
            }

            setup = configuredSetup;
        }

        public static void SetCollisionRules(
            EnemyCollisionRulesFactory createRules)
        {
            if (createRules == null)
            {
                throw new ArgumentNullException(nameof(createRules));
            }

            if (collisionRulesFactory != null
                && collisionRulesFactory != createRules)
            {
                throw new InvalidOperationException(
                    "enemy-collision-rules-already-set");
            }

            collisionRulesFactory = createRules;
        }

        public static void SetUp(
            GameObject gameObject,
            LevelRooms roomOwner,
            StableId roomStableId,
            RoomPlacedEntityDefinition placement,
            int enemyLevel)
        {
            if (setup == null)
            {
                throw new InvalidOperationException(
                    "compact-enemy-scene-factory-not-registered");
            }
            if (gameObject == null)
            {
                throw new ArgumentNullException(nameof(gameObject));
            }
            if (roomOwner == null)
            {
                throw new ArgumentNullException(nameof(roomOwner));
            }
            if (roomStableId == null)
            {
                throw new ArgumentNullException(nameof(roomStableId));
            }
            if (placement == null)
            {
                throw new ArgumentNullException(nameof(placement));
            }

            setup(
                gameObject,
                roomOwner,
                roomStableId,
                placement,
                enemyLevel);

            if (collisionRulesFactory != null)
            {
                collisionRulesFactory(gameObject);
            }

            CompactEnemyRoomClamp clamp =
                gameObject.GetComponent<CompactEnemyRoomClamp>()
                ?? gameObject.AddComponent<CompactEnemyRoomClamp>();
            clamp.Configure(roomOwner, roomStableId);
        }
    }
}
