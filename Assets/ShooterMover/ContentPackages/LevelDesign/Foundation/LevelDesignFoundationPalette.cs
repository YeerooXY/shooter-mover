using System;
using UnityEngine;

namespace ShooterMover.ContentPackages.LevelDesign.Foundation
{
    /// <summary>
    /// Optional drag palette for the reusable LEVELDES-001 authoring prefabs.
    /// It stores references only and owns no level, room, spawn, door, reward,
    /// enemy, prop, or pickup runtime truth.
    /// </summary>
    [CreateAssetMenu(
        fileName = "LevelDesignFoundationPalette",
        menuName = "Shooter Mover/Level Design/Foundation Palette")]
    public sealed class LevelDesignFoundationPalette : ScriptableObject
    {
        [SerializeField] private GameObject sceneRootPrefab;
        [SerializeField] private GameObject roomAnchorPrefab;
        [SerializeField] private GameObject configuredDoorPrefab;
        [SerializeField] private GameObject playerSpawnPrefab;
        [SerializeField] private GameObject enemySpawnPrefab;
        [SerializeField] private GameObject propPlacementPrefab;
        [SerializeField] private GameObject pickupSpawnPrefab;
        [SerializeField] private GameObject rewardSocketPrefab;
        [SerializeField] private GameObject entryExitPrefab;
        [SerializeField] private GameObject voidRegionPrefab;
        [SerializeField] private Sprite openDoorSprite;

        public GameObject SceneRootPrefab => sceneRootPrefab;
        public GameObject RoomAnchorPrefab => roomAnchorPrefab;
        public GameObject ConfiguredDoorPrefab => configuredDoorPrefab;
        public GameObject PlayerSpawnPrefab => playerSpawnPrefab;
        public GameObject EnemySpawnPrefab => enemySpawnPrefab;
        public GameObject PropPlacementPrefab => propPlacementPrefab;
        public GameObject PickupSpawnPrefab => pickupSpawnPrefab;
        public GameObject RewardSocketPrefab => rewardSocketPrefab;
        public GameObject EntryExitPrefab => entryExitPrefab;
        public GameObject VoidRegionPrefab => voidRegionPrefab;
        public Sprite OpenDoorSprite => openDoorSprite;

        public string[] ValidateReferences()
        {
            string[] names =
            {
                sceneRootPrefab == null ? nameof(sceneRootPrefab) : null,
                roomAnchorPrefab == null ? nameof(roomAnchorPrefab) : null,
                configuredDoorPrefab == null ? nameof(configuredDoorPrefab) : null,
                playerSpawnPrefab == null ? nameof(playerSpawnPrefab) : null,
                enemySpawnPrefab == null ? nameof(enemySpawnPrefab) : null,
                propPlacementPrefab == null ? nameof(propPlacementPrefab) : null,
                pickupSpawnPrefab == null ? nameof(pickupSpawnPrefab) : null,
                rewardSocketPrefab == null ? nameof(rewardSocketPrefab) : null,
                entryExitPrefab == null ? nameof(entryExitPrefab) : null,
                voidRegionPrefab == null ? nameof(voidRegionPrefab) : null,
                openDoorSprite == null ? nameof(openDoorSprite) : null,
            };

            return Array.FindAll(names, value => value != null);
        }
    }
}
