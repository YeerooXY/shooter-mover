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

        public void Configure(string stableId, GameObject configuredPrefab)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                throw new ArgumentException(
                    "A presentation stable ID is required.",
                    nameof(stableId));
            }
            presentationStableId = stableId.Trim();
            prefab = configuredPrefab
                ?? throw new ArgumentNullException(nameof(configuredPrefab));
        }

        public void ConfigureForTests(string stableId, GameObject configuredPrefab)
        {
            Configure(stableId, configuredPrefab);
        }
    }

    [CreateAssetMenu(
        fileName = "RoomPresentationCatalog2D",
        menuName = "Shooter Mover/Level Design/Room Presentation Catalog 2D")]
    public sealed class RoomPresentationCatalog2D :
        ScriptableObject,
        ISerializationCallbackReceiver
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
            if (!resolved.TryGetValue(presentationStableId, out prefab))
            {
                return false;
            }
            if (prefab != null)
            {
                return true;
            }

            // Unity objects can become fake-null after asset replacement or reimport while
            // domain reload is disabled. Rebuild once from the serialized source of truth.
            InvalidateResolved();
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

        public void Configure(params RoomPresentationCatalogEntry2D[] configuredEntries)
        {
            entries = configuredEntries == null
                ? Array.Empty<RoomPresentationCatalogEntry2D>()
                : (RoomPresentationCatalogEntry2D[])configuredEntries.Clone();
            InvalidateResolved();
        }

        public void ConfigureForTests(params RoomPresentationCatalogEntry2D[] configuredEntries)
        {
            Configure(configuredEntries);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            InvalidateResolved();
        }

        private void OnEnable()
        {
            InvalidateResolved();
        }

        private void OnValidate()
        {
            InvalidateResolved();
        }

        private void Require(StableId presentationStableId)
        {
            GameObject prefab;
            if (resolved.TryGetValue(presentationStableId, out prefab)
                && prefab != null)
            {
                return;
            }

            if (resolved.ContainsKey(presentationStableId))
            {
                InvalidateResolved();
                EnsureResolved();
                if (resolved.TryGetValue(presentationStableId, out prefab)
                    && prefab != null)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                "room-live-presentation-missing:" + presentationStableId);
        }

        private void EnsureResolved()
        {
            if (resolved != null) return;

            var candidate = new Dictionary<StableId, GameObject>();
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

                if (candidate.ContainsKey(id))
                {
                    throw new InvalidOperationException(
                        "room-live-presentation-duplicate:" + id);
                }

                candidate.Add(id, entry.Prefab);
            }

            resolved = candidate;
        }

        private void InvalidateResolved()
        {
            resolved = null;
        }
    }
}
