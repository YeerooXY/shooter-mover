using System;
using System.Globalization;
using System.Reflection;
using System.Text;
using ShooterMover.Application.Missions.Rooms;
using ShooterMover.Contracts.Missions.Rooms;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Enemies;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Missions.Rooms
{
    [DisallowMultipleComponent]
    public sealed class RoomPlacedInstance2D : MonoBehaviour
    {
        private RoomRuntimeComposition2D owner;

        public StableId RoomStableId { get; private set; }

        public StableId InstanceStableId { get; private set; }

        public StableId DefinitionStableId { get; private set; }

        public RoomLivePlacementKindV1 PlacementKind { get; private set; }

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
            RoomRuntimeComposition2D configuredOwner,
            StableId roomStableId,
            RoomPlacedEntityDefinitionV1 definition)
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

        public RoomLiveOperationResultV1 ReportTerminal(StableId operationStableId)
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

    /// <summary>
    /// Generic bridge from real EN-002 authority state to room terminal facts. It binds
    /// either directly to an IEnemyActor2DAuthority component or to a component exposing
    /// one through a public Authority property. No enemy package names are inspected.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyActorTerminalFactSource2D : MonoBehaviour
    {
        private IEnemyActor2DAuthority authority;
        private Component authorityOwner;
        private PropertyInfo generationProperty;

        public bool IsBound
        {
            get { return authority != null; }
        }

        public bool TryReadTerminal(
            out StableId actorStableId,
            out long sourceGeneration)
        {
            actorStableId = null;
            sourceGeneration = 0L;
            if (authority == null && !TryBind())
            {
                return false;
            }

            EnemyActorState state;
            if (!authority.TryReadState(out state) || state == null)
            {
                return false;
            }

            actorStableId = state.ActorId;
            sourceGeneration = ReadGeneration();
            return state.IsDestroyed;
        }

        public bool TryBind()
        {
            MonoBehaviour[] components = GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0; index < components.Length; index++)
            {
                MonoBehaviour component = components[index];
                if (component == null || component == this) continue;

                IEnemyActor2DAuthority direct = component as IEnemyActor2DAuthority;
                if (direct != null)
                {
                    Bind(component, direct);
                    return true;
                }

                PropertyInfo property = component.GetType().GetProperty(
                    "Authority",
                    BindingFlags.Public | BindingFlags.Instance);
                if (property == null
                    || property.GetIndexParameters().Length != 0
                    || !typeof(IEnemyActor2DAuthority).IsAssignableFrom(
                        property.PropertyType))
                {
                    continue;
                }

                IEnemyActor2DAuthority nested =
                    property.GetValue(component, null) as IEnemyActor2DAuthority;
                if (nested == null) continue;
                Bind(component, nested);
                return true;
            }

            return false;
        }

        private void Bind(Component owner, IEnemyActor2DAuthority boundAuthority)
        {
            authorityOwner = owner;
            authority = boundAuthority;
            generationProperty = owner.GetType().GetProperty(
                "Generation",
                BindingFlags.Public | BindingFlags.Instance);
            if (generationProperty != null
                && generationProperty.PropertyType != typeof(long))
            {
                generationProperty = null;
            }
        }

        private long ReadGeneration()
        {
            if (authorityOwner == null || generationProperty == null)
            {
                return 0L;
            }

            object value = generationProperty.GetValue(authorityOwner, null);
            return value is long ? (long)value : 0L;
        }
    }

    [DisallowMultipleComponent]
    public sealed class RoomOccupantTerminalRelay2D : MonoBehaviour
    {
        private RoomPlacedInstance2D placedInstance;
        private EnemyActorTerminalFactSource2D terminalSource;
        private StableId lastActorStableId;
        private long lastSourceGeneration;
        private long lastRoomGeneration;
        private bool terminalReported;

        public bool IsConfigured
        {
            get { return placedInstance != null && terminalSource != null; }
        }

        public void Configure(
            RoomPlacedInstance2D configuredPlacedInstance,
            EnemyActorTerminalFactSource2D configuredTerminalSource)
        {
            if (IsConfigured)
            {
                throw new InvalidOperationException(
                    "Room terminal relay may only be configured once.");
            }

            placedInstance = configuredPlacedInstance
                ?? throw new ArgumentNullException(nameof(configuredPlacedInstance));
            terminalSource = configuredTerminalSource
                ?? throw new ArgumentNullException(nameof(configuredTerminalSource));
        }

        public bool PollNow()
        {
            if (!IsConfigured) return false;
            StableId actorStableId;
            long sourceGeneration;
            if (!terminalSource.TryReadTerminal(
                out actorStableId,
                out sourceGeneration))
            {
                return false;
            }

            if (actorStableId != placedInstance.InstanceStableId)
            {
                return false;
            }

            long roomGeneration = placedInstance.RuntimeLifecycleGeneration;
            if (terminalReported
                && lastActorStableId == actorStableId
                && lastSourceGeneration == sourceGeneration
                && lastRoomGeneration == roomGeneration)
            {
                return false;
            }

            StableId operationStableId = CreateOperationStableId(
                placedInstance.RoomStableId,
                placedInstance.InstanceStableId,
                actorStableId,
                sourceGeneration,
                roomGeneration);
            RoomLiveOperationResultV1 result = placedInstance.ReportTerminal(
                operationStableId);
            if (result.Status == RoomLiveOperationStatusV1.Rejected)
            {
                return false;
            }

            terminalReported = true;
            lastActorStableId = actorStableId;
            lastSourceGeneration = sourceGeneration;
            lastRoomGeneration = roomGeneration;
            return result.Changed;
        }

        private void Update()
        {
            PollNow();
        }

        private static StableId CreateOperationStableId(
            StableId roomStableId,
            StableId placedInstanceStableId,
            StableId actorStableId,
            long sourceGeneration,
            long roomGeneration)
        {
            string payload = roomStableId
                + "|"
                + placedInstanceStableId
                + "|"
                + actorStableId
                + "|"
                + sourceGeneration.ToString(CultureInfo.InvariantCulture)
                + "|"
                + roomGeneration.ToString(CultureInfo.InvariantCulture);
            using (System.Security.Cryptography.SHA256 sha =
                System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var token = new StringBuilder(32);
                for (int index = 0; index < 16; index++)
                {
                    token.Append(hash[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }

                return StableId.Create(
                    "operation",
                    "room-terminal-" + token.ToString());
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class RoomDoorInstance2D : MonoBehaviour
    {
        private RoomRuntimeComposition2D owner;
        private Collider2D[] colliders = Array.Empty<Collider2D>();
        private bool[] authoredColliderEnabled = Array.Empty<bool>();

        public StableId RoomStableId { get; private set; }

        public StableId DoorInstanceStableId { get; private set; }

        public StableId ExitStableId { get; private set; }

        public bool IsOpen { get; private set; }

        public bool IsConfigured { get; private set; }

        public void Configure(
            RoomRuntimeComposition2D configuredOwner,
            StableId roomStableId,
            RoomDoorDefinitionV1 definition)
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
        }

        public RoomLiveOperationResultV1 TryTraverse(StableId operationStableId)
        {
            if (!IsConfigured || owner == null)
            {
                throw new InvalidOperationException("Room door is not configured.");
            }

            return owner.Traverse(operationStableId, ExitStableId);
        }
    }

    [DisallowMultipleComponent]
    public sealed class RoomDropInstance2D : MonoBehaviour
    {
        private RoomRuntimeComposition2D owner;

        public StableId RoomStableId { get; private set; }

        public StableId DropInstanceStableId { get; private set; }

        public bool IsConfigured { get; private set; }

        public void Configure(
            RoomRuntimeComposition2D configuredOwner,
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

        public RoomLiveOperationResultV1 ReportCollected(StableId operationStableId)
        {
            if (!IsConfigured || owner == null)
            {
                throw new InvalidOperationException(
                    "Room drop instance is not configured.");
            }

            RoomLiveOperationResultV1 result = owner.ReportDropCollected(
                operationStableId,
                RoomStableId,
                DropInstanceStableId);
            if (result.Status != RoomLiveOperationStatusV1.Rejected)
            {
                gameObject.SetActive(false);
            }

            return result;
        }
    }
}
