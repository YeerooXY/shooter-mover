using System;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Missions.Rooms
{
    [DisallowMultipleComponent]
    public sealed class RoomObjectInstance : MonoBehaviour
    {
        private LevelRooms owner;

        public StableId RoomStableId { get; private set; }

        public StableId InstanceStableId { get; private set; }

        public StableId DefinitionStableId { get; private set; }

        public RoomLivePlacementKind PlacementKind { get; private set; }

        public bool IsConfigured { get; private set; }

        public long RuntimeLifecycleGeneration
        {
            get
            {
                return owner == null || owner.CurrentProjection == null
                    ? 0L
                    : owner.CurrentProjection.LifecycleGeneration;
            }
        }

        public void Configure(
            LevelRooms configuredOwner,
            StableId roomStableId,
            RoomPlacedEntityDefinition definition)
        {
            if (IsConfigured)
            {
                throw new InvalidOperationException(
                    "Room placed instance may only be configured once.");
            }

            owner = configuredOwner
                ?? throw new ArgumentNullException(nameof(configuredOwner));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            InstanceStableId = definition.InstanceStableId;
            DefinitionStableId = definition.DefinitionStableId;
            PlacementKind = definition.PlacementKind;
            IsConfigured = true;
        }

        public RoomLiveOperationResult ReportTerminal(StableId operationStableId)
        {
            if (!IsConfigured || owner == null)
            {
                throw new InvalidOperationException(
                    "Room placed instance is not configured.");
            }

            return owner.ReportOccupantTerminal(
                operationStableId,
                RoomStableId,
                InstanceStableId);
        }
    }

    [DisallowMultipleComponent]
    public sealed class RoomDoor : MonoBehaviour
    {
        private static readonly Color DebugOpenColor =
            new Color(0.18f, 0.9f, 0.38f, 1f);

        private LevelRooms owner;
        private Collider2D[] colliders = Array.Empty<Collider2D>();
        private bool[] authoredColliderEnabled = Array.Empty<bool>();
        private SpriteRenderer debugRenderer;
        private Color debugClosedColor;

        public StableId RoomStableId { get; private set; }

        public StableId DoorInstanceStableId { get; private set; }

        public StableId ExitStableId { get; private set; }

        public bool IsOpen { get; private set; }

        public bool IsConfigured { get; private set; }

        public void Configure(
            LevelRooms configuredOwner,
            StableId roomStableId,
            RoomDoorDefinition definition)
        {
            if (IsConfigured)
            {
                throw new InvalidOperationException(
                    "Room door instance may only be configured once.");
            }

            owner = configuredOwner
                ?? throw new ArgumentNullException(nameof(configuredOwner));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            DoorInstanceStableId = definition.DoorInstanceStableId;
            ExitStableId = definition.ExitStableId;
            colliders = GetComponentsInChildren<Collider2D>(true);
            authoredColliderEnabled = new bool[colliders.Length];
            for (int index = 0; index < colliders.Length; index++)
            {
                authoredColliderEnabled[index] = colliders[index] != null
                    && colliders[index].enabled;
            }

            Transform debugVisual = transform.Find("Debug Visual");
            debugRenderer = debugVisual == null
                ? null
                : debugVisual.GetComponent<SpriteRenderer>();
            if (debugRenderer != null)
            {
                debugClosedColor = debugRenderer.color;
            }

            IsConfigured = true;
        }

        public void SetOpen(bool open)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("Room door is not configured.");
            }

            IsOpen = open;
            for (int index = 0; index < colliders.Length; index++)
            {
                if (colliders[index] != null)
                {
                    colliders[index].enabled = !open && authoredColliderEnabled[index];
                }
            }

            if (debugRenderer != null)
            {
                debugRenderer.color = open
                    ? DebugOpenColor
                    : debugClosedColor;
            }
        }

        public RoomLiveOperationResult TryTraverse(StableId operationStableId)
        {
            if (!IsConfigured || owner == null)
            {
                throw new InvalidOperationException("Room door is not configured.");
            }

            return owner.Traverse(operationStableId, ExitStableId);
        }
    }

    [DisallowMultipleComponent]
    public sealed class RoomLoot : MonoBehaviour
    {
        private LevelRooms owner;

        public StableId RoomStableId { get; private set; }

        public StableId DropInstanceStableId { get; private set; }

        public bool IsConfigured { get; private set; }

        public void Configure(
            LevelRooms configuredOwner,
            StableId roomStableId,
            StableId dropInstanceStableId)
        {
            if (IsConfigured)
            {
                throw new InvalidOperationException(
                    "Room drop instance may only be configured once.");
            }

            owner = configuredOwner
                ?? throw new ArgumentNullException(nameof(configuredOwner));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            DropInstanceStableId = dropInstanceStableId
                ?? throw new ArgumentNullException(nameof(dropInstanceStableId));
            IsConfigured = true;
        }

        public RoomLiveOperationResult ReportCollected(StableId operationStableId)
        {
            if (!IsConfigured || owner == null)
            {
                throw new InvalidOperationException(
                    "Room drop instance is not configured.");
            }

            RoomLiveOperationResult result = owner.ReportDropCollected(
                operationStableId,
                RoomStableId,
                DropInstanceStableId);
            if (result.Status != RoomLiveOperationStatus.Rejected)
            {
                gameObject.SetActive(false);
            }

            return result;
        }
    }
}
