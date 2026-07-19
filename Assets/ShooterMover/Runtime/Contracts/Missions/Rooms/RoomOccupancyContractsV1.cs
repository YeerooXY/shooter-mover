using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Contracts.Missions.Rooms
{
    public enum RoomOccupantClearRoleV1
    {
        RequiredEnemy = 1,
        ObjectiveEntity = 2,
        OptionalEnemy = 3,
        NonParticipant = 4,
    }

    public enum RoomRuntimeOperationStatusV1
    {
        Applied = 1,
        DuplicateNoChange = 2,
        NoChange = 3,
        Rejected = 4,
    }

    public sealed class RoomOccupantRegistrationV1
    {
        public RoomOccupantRegistrationV1(
            StableId entityStableId,
            StableId definitionStableId,
            RoomOccupantClearRoleV1 clearRole)
        {
            EntityStableId = entityStableId
                ?? throw new ArgumentNullException(nameof(entityStableId));
            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            if (!Enum.IsDefined(typeof(RoomOccupantClearRoleV1), clearRole))
            {
                throw new ArgumentOutOfRangeException(nameof(clearRole));
            }

            ClearRole = clearRole;
        }

        public StableId EntityStableId { get; }

        public StableId DefinitionStableId { get; }

        public RoomOccupantClearRoleV1 ClearRole { get; }

        public bool BlocksRoomClear
        {
            get
            {
                return ClearRole == RoomOccupantClearRoleV1.RequiredEnemy
                    || ClearRole == RoomOccupantClearRoleV1.ObjectiveEntity;
            }
        }
    }

    public sealed class RoomOccupantProjectionV1
    {
        public RoomOccupantProjectionV1(
            StableId entityStableId,
            StableId definitionStableId,
            RoomOccupantClearRoleV1 clearRole,
            bool isTerminal)
        {
            EntityStableId = entityStableId
                ?? throw new ArgumentNullException(nameof(entityStableId));
            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            if (!Enum.IsDefined(typeof(RoomOccupantClearRoleV1), clearRole))
            {
                throw new ArgumentOutOfRangeException(nameof(clearRole));
            }

            ClearRole = clearRole;
            IsTerminal = isTerminal;
        }

        public StableId EntityStableId { get; }

        public StableId DefinitionStableId { get; }

        public RoomOccupantClearRoleV1 ClearRole { get; }

        public bool IsTerminal { get; }

        public bool BlocksRoomClear
        {
            get
            {
                return ClearRole == RoomOccupantClearRoleV1.RequiredEnemy
                    || ClearRole == RoomOccupantClearRoleV1.ObjectiveEntity;
            }
        }
    }

    public sealed class RoomExitEligibilityProjectionV1
    {
        public RoomExitEligibilityProjectionV1(
            StableId exitStableId,
            bool isEligible)
        {
            ExitStableId = exitStableId
                ?? throw new ArgumentNullException(nameof(exitStableId));
            IsEligible = isEligible;
        }

        public StableId ExitStableId { get; }

        public bool IsEligible { get; }
    }

    public sealed class RoomOccupancyProjectionV1
    {
        private readonly ReadOnlyCollection<RoomOccupantProjectionV1> occupants;
        private readonly ReadOnlyCollection<RoomExitEligibilityProjectionV1> connectedExits;

        public RoomOccupancyProjectionV1(
            StableId runtimeInstanceStableId,
            StableId roomStableId,
            long lifecycleGeneration,
            bool isActive,
            bool isOccupancyRegistered,
            bool isCleared,
            IEnumerable<RoomOccupantProjectionV1> occupants,
            IEnumerable<RoomExitEligibilityProjectionV1> connectedExits)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }

            LifecycleGeneration = lifecycleGeneration;
            IsActive = isActive;
            IsOccupancyRegistered = isOccupancyRegistered;
            IsCleared = isCleared;
            this.occupants = CopyOccupants(occupants);
            this.connectedExits = CopyExits(connectedExits);
        }

        public StableId RuntimeInstanceStableId { get; }

        public StableId RoomStableId { get; }

        public long LifecycleGeneration { get; }

        public bool IsActive { get; }

        public bool IsOccupancyRegistered { get; }

        public bool IsCleared { get; }

        public IReadOnlyList<RoomOccupantProjectionV1> Occupants
        {
            get { return occupants; }
        }

        public IReadOnlyList<RoomExitEligibilityProjectionV1> ConnectedExits
        {
            get { return connectedExits; }
        }

        public bool IsExitEligible(StableId exitStableId)
        {
            if (exitStableId == null)
            {
                throw new ArgumentNullException(nameof(exitStableId));
            }

            for (int index = 0; index < connectedExits.Count; index++)
            {
                if (connectedExits[index].ExitStableId == exitStableId)
                {
                    return connectedExits[index].IsEligible;
                }
            }

            return false;
        }

        private static ReadOnlyCollection<RoomOccupantProjectionV1> CopyOccupants(
            IEnumerable<RoomOccupantProjectionV1> source)
        {
            var copy = new List<RoomOccupantProjectionV1>(
                source ?? Array.Empty<RoomOccupantProjectionV1>());
            copy.Sort((left, right) => left.EntityStableId.CompareTo(
                right.EntityStableId));
            return new ReadOnlyCollection<RoomOccupantProjectionV1>(copy);
        }

        private static ReadOnlyCollection<RoomExitEligibilityProjectionV1> CopyExits(
            IEnumerable<RoomExitEligibilityProjectionV1> source)
        {
            var copy = new List<RoomExitEligibilityProjectionV1>(
                source ?? Array.Empty<RoomExitEligibilityProjectionV1>());
            copy.Sort((left, right) => left.ExitStableId.CompareTo(
                right.ExitStableId));
            return new ReadOnlyCollection<RoomExitEligibilityProjectionV1>(copy);
        }
    }

    public sealed class RoomRuntimeProjectionV1
    {
        private readonly ReadOnlyCollection<RoomOccupancyProjectionV1> rooms;

        public RoomRuntimeProjectionV1(
            StableId runtimeInstanceStableId,
            StableId layoutStableId,
            string definitionFingerprint,
            long lifecycleGeneration,
            long sequence,
            IEnumerable<RoomOccupancyProjectionV1> rooms)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            LayoutStableId = layoutStableId
                ?? throw new ArgumentNullException(nameof(layoutStableId));
            DefinitionFingerprint = definitionFingerprint ?? string.Empty;
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }

            if (sequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            LifecycleGeneration = lifecycleGeneration;
            Sequence = sequence;
            var copy = new List<RoomOccupancyProjectionV1>(
                rooms ?? throw new ArgumentNullException(nameof(rooms)));
            copy.Sort((left, right) => left.RoomStableId.CompareTo(
                right.RoomStableId));
            this.rooms = new ReadOnlyCollection<RoomOccupancyProjectionV1>(copy);
        }

        public StableId RuntimeInstanceStableId { get; }

        public StableId LayoutStableId { get; }

        public string DefinitionFingerprint { get; }

        public long LifecycleGeneration { get; }

        public long Sequence { get; }

        public IReadOnlyList<RoomOccupancyProjectionV1> Rooms
        {
            get { return rooms; }
        }

        public RoomOccupancyProjectionV1 GetRoom(StableId roomStableId)
        {
            if (roomStableId == null)
            {
                throw new ArgumentNullException(nameof(roomStableId));
            }

            for (int index = 0; index < rooms.Count; index++)
            {
                if (rooms[index].RoomStableId == roomStableId)
                {
                    return rooms[index];
                }
            }

            throw new KeyNotFoundException(
                "Unknown room identity: " + roomStableId);
        }
    }

    public sealed class RoomClearTransitionV1
    {
        public RoomClearTransitionV1(
            StableId runtimeInstanceStableId,
            StableId roomStableId,
            StableId operationStableId,
            long lifecycleGeneration,
            long sequence)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }

            if (sequence <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(sequence));
            }

            LifecycleGeneration = lifecycleGeneration;
            Sequence = sequence;
        }

        public StableId RuntimeInstanceStableId { get; }

        public StableId RoomStableId { get; }

        public StableId OperationStableId { get; }

        public long LifecycleGeneration { get; }

        public long Sequence { get; }
    }

    public sealed class RegisterRoomOccupantsCommandV1
    {
        private readonly ReadOnlyCollection<RoomOccupantRegistrationV1> occupants;

        public RegisterRoomOccupantsCommandV1(
            StableId runtimeInstanceStableId,
            StableId operationStableId,
            long lifecycleGeneration,
            StableId roomStableId,
            IEnumerable<RoomOccupantRegistrationV1> occupants)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }

            LifecycleGeneration = lifecycleGeneration;
            this.occupants = new ReadOnlyCollection<RoomOccupantRegistrationV1>(
                new List<RoomOccupantRegistrationV1>(
                    occupants ?? throw new ArgumentNullException(nameof(occupants))));
        }

        public StableId RuntimeInstanceStableId { get; }

        public StableId OperationStableId { get; }

        public long LifecycleGeneration { get; }

        public StableId RoomStableId { get; }

        public IReadOnlyList<RoomOccupantRegistrationV1> Occupants
        {
            get { return occupants; }
        }
    }

    public sealed class ActivateRoomCommandV1
    {
        public ActivateRoomCommandV1(
            StableId runtimeInstanceStableId,
            StableId operationStableId,
            long lifecycleGeneration,
            StableId roomStableId)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }

            LifecycleGeneration = lifecycleGeneration;
        }

        public StableId RuntimeInstanceStableId { get; }

        public StableId OperationStableId { get; }

        public long LifecycleGeneration { get; }

        public StableId RoomStableId { get; }
    }

    public sealed class ReportRoomOccupantTerminalCommandV1
    {
        public ReportRoomOccupantTerminalCommandV1(
            StableId runtimeInstanceStableId,
            StableId operationStableId,
            long lifecycleGeneration,
            StableId roomStableId,
            StableId occupantEntityStableId)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            RoomStableId = roomStableId
                ?? throw new ArgumentNullException(nameof(roomStableId));
            OccupantEntityStableId = occupantEntityStableId
                ?? throw new ArgumentNullException(nameof(occupantEntityStableId));
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }

            LifecycleGeneration = lifecycleGeneration;
        }

        public StableId RuntimeInstanceStableId { get; }

        public StableId OperationStableId { get; }

        public long LifecycleGeneration { get; }

        public StableId RoomStableId { get; }

        public StableId OccupantEntityStableId { get; }
    }

    public sealed class RestartRoomRuntimeCommandV1
    {
        public RestartRoomRuntimeCommandV1(
            StableId runtimeInstanceStableId,
            StableId operationStableId,
            long lifecycleGeneration)
        {
            RuntimeInstanceStableId = runtimeInstanceStableId
                ?? throw new ArgumentNullException(nameof(runtimeInstanceStableId));
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            if (lifecycleGeneration <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }

            LifecycleGeneration = lifecycleGeneration;
        }

        public StableId RuntimeInstanceStableId { get; }

        public StableId OperationStableId { get; }

        public long LifecycleGeneration { get; }
    }

    public sealed class RoomRuntimeOperationResultV1
    {
        public RoomRuntimeOperationResultV1(
            RoomRuntimeOperationStatusV1 status,
            string rejectionCode,
            RoomRuntimeProjectionV1 previousProjection,
            RoomRuntimeProjectionV1 currentProjection,
            RoomClearTransitionV1 clearTransition)
        {
            if (!Enum.IsDefined(typeof(RoomRuntimeOperationStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            PreviousProjection = previousProjection
                ?? throw new ArgumentNullException(nameof(previousProjection));
            CurrentProjection = currentProjection
                ?? throw new ArgumentNullException(nameof(currentProjection));
            ClearTransition = clearTransition;
        }

        public RoomRuntimeOperationStatusV1 Status { get; }

        public string RejectionCode { get; }

        public RoomRuntimeProjectionV1 PreviousProjection { get; }

        public RoomRuntimeProjectionV1 CurrentProjection { get; }

        public RoomClearTransitionV1 ClearTransition { get; }

        public bool Changed
        {
            get { return Status == RoomRuntimeOperationStatusV1.Applied; }
        }
    }

    public interface IRoomRuntimeAuthorityV1
    {
        StableId RuntimeInstanceStableId { get; }

        RoomGraphDefinitionV1 Definition { get; }

        RoomRuntimeProjectionV1 CurrentProjection { get; }

        RoomOccupancyProjectionV1 GetRoomProjection(StableId roomStableId);

        RoomRuntimeOperationResultV1 RegisterOccupants(
            RegisterRoomOccupantsCommandV1 command);

        RoomRuntimeOperationResultV1 ActivateRoom(ActivateRoomCommandV1 command);

        RoomRuntimeOperationResultV1 ReportTerminal(
            ReportRoomOccupantTerminalCommandV1 command);

        RoomRuntimeOperationResultV1 Restart(RestartRoomRuntimeCommandV1 command);
    }
}
