using System;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    public delegate void CompactEnemySceneFactoryDelegate(
        GameObject gameObject,
        LevelRooms roomOwner,
        StableId roomStableId,
        RoomPlacedEntityDefinition placement,
        int enemyLevel);

    public delegate void CompactEnemyCollisionPolicyFactoryDelegate(
        GameObject gameObject);

    /// <summary>
    /// Tiny composition seam between room-owned GameObjects and the gameplay assembly that
    /// can access the live player-damage authority. It carries no enemy catalogue or policy.
    /// </summary>
    public static class CompactEnemySceneFactory
    {
        private static CompactEnemySceneFactoryDelegate factory;
        private static CompactEnemyCollisionPolicyFactoryDelegate collisionPolicyFactory;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            factory = null;
            collisionPolicyFactory = null;
        }

        public static void Register(CompactEnemySceneFactoryDelegate configuredFactory)
        {
            if (configuredFactory == null)
            {
                throw new ArgumentNullException(nameof(configuredFactory));
            }

            if (factory != null && factory != configuredFactory)
            {
                throw new InvalidOperationException(
                    "compact-enemy-scene-factory-already-registered");
            }

            factory = configuredFactory;
        }

        public static void RegisterCollisionPolicy(
            CompactEnemyCollisionPolicyFactoryDelegate configuredFactory)
        {
            if (configuredFactory == null)
            {
                throw new ArgumentNullException(nameof(configuredFactory));
            }

            if (collisionPolicyFactory != null
                && collisionPolicyFactory != configuredFactory)
            {
                throw new InvalidOperationException(
                    "compact-enemy-collision-policy-factory-already-registered");
            }

            collisionPolicyFactory = configuredFactory;
        }

        public static void Configure(
            GameObject gameObject,
            LevelRooms roomOwner,
            StableId roomStableId,
            RoomPlacedEntityDefinition placement,
            int enemyLevel)
        {
            if (factory == null)
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

            factory(
                gameObject,
                roomOwner,
                roomStableId,
                placement,
                enemyLevel);

            if (collisionPolicyFactory != null)
            {
                collisionPolicyFactory(gameObject);
            }

            CompactEnemyRoomClamp clamp =
                gameObject.GetComponent<CompactEnemyRoomClamp>()
                ?? gameObject.AddComponent<CompactEnemyRoomClamp>();
            clamp.Configure(roomOwner, roomStableId);
        }
    }
}
