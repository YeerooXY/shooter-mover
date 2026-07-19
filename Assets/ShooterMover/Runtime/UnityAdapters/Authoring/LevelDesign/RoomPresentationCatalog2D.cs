using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Authoring.LevelDesign
{
    [Serializable]
    public sealed class RoomPresentationCatalogEntry2D
    {
        [SerializeField] private string presentationStableId = "presentation.unassigned";
        [SerializeField] private GameObject prefab;

        public StableId PresentationStableId
        {
            get { return StableId.Parse(presentationStableId); }
        }

        public GameObject Prefab
        {
            get { return prefab; }
        }

        public void ConfigureForTests(string stableId, GameObject configuredPrefab)
        {
            presentationStableId = stableId;
            prefab = configuredPrefab;
        }
    }

    [CreateAssetMenu(
        fileName = "RoomPresentationCatalog2D",
        menuName = "Shooter Mover/Level Design/Room Presentation Catalog 2D")]
    public sealed class RoomPresentationCatalog2D : ScriptableObject
    {
        [SerializeField] private RoomPresentationCatalogEntry2D[] entries =
            Array.Empty<RoomPresentationCatalogEntry2D>();

        private Dictionary<StableId, GameObject> resolved;

        public bool TryResolve(StableId presentationStableId, out GameObject prefab)
        {
            if (presentationStableId == null)
            {
                prefab = null;
                return false;
            }

            EnsureResolved();
            return resolved.TryGetValue(presentationStableId, out prefab)
                && prefab != null;
        }

        public void ValidateFor(AuthorableRoomGraphDefinitionV1 definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            EnsureResolved();
            for (int roomIndex = 0; roomIndex < definition.Rooms.Count; roomIndex++)
            {
                AuthorableRoomDefinitionV1 room = definition.Rooms[roomIndex];
                for (int index = 0; index < room.Placements.Count; index++)
                {
                    Require(room.Placements[index].PresentationStableId);
                }

                for (int index = 0; index < room.Doors.Count; index++)
                {
                    Require(room.Doors[index].PresentationStableId);
                }
            }
        }

        public void ConfigureForTests(params RoomPresentationCatalogEntry2D[] configuredEntries)
        {
            entries = configuredEntries == null
                ? Array.Empty<RoomPresentationCatalogEntry2D>()
                : (RoomPresentationCatalogEntry2D[])configuredEntries.Clone();
            resolved = null;
        }

        private void Require(StableId presentationStableId)
        {
            GameObject prefab;
            if (!resolved.TryGetValue(presentationStableId, out prefab) || prefab == null)
            {
                throw new InvalidOperationException(
                    "room-live-presentation-missing:" + presentationStableId);
            }
        }

        private void EnsureResolved()
        {
            if (resolved != null) return;

            resolved = new Dictionary<StableId, GameObject>();
            RoomPresentationCatalogEntry2D[] authoredEntries = entries
                ?? Array.Empty<RoomPresentationCatalogEntry2D>();
            for (int index = 0; index < authoredEntries.Length; index++)
            {
                RoomPresentationCatalogEntry2D entry = authoredEntries[index];
                if (entry == null)
                {
                    throw new InvalidOperationException(
                        "Room presentation catalog cannot contain null entries.");
                }

                StableId id = entry.PresentationStableId;
                if (entry.Prefab == null)
                {
                    throw new InvalidOperationException(
                        "room-live-presentation-prefab-missing:" + id);
                }

                if (resolved.ContainsKey(id))
                {
                    throw new InvalidOperationException(
                        "room-live-presentation-duplicate:" + id);
                }

                resolved.Add(id, entry.Prefab);
            }
        }
    }
}
