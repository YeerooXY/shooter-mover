using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Missions.Rooms;

namespace ShooterMover.Contracts.Missions.Rooms
{
    public enum RoomOccupantClearRole
    {
        RequiredEnemy = 1,
        ObjectiveEntity = 2,
        OptionalEnemy = 3,
        NonParticipant = 4,
    }

    public enum RoomOccupancyStatus
    {
        Applied = 1,
        DuplicateNoChange = 2,
        NoChange = 3,
        Rejected = 4,
    }

    public sealed class RoomOccupantRegistration
    {
        public RoomOccupantRegistration(
            StableId entityStableId,
            StableId definitionStableId,
            RoomOccupantClearRole clearRole)
        {
            EntityStableId = entityStableId
                ?? throw new ArgumentNullException(nameof(entityStableId));
            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            if (!Enum.IsDefined(typeof(RoomOccupantClearRole), clearRole))
            {
                throw new ArgumentOutOfRangeException(nameof(clearRole));
            }

            ClearRole = clearRole;
        }

        public StableId EntityStableId { get; }

        public StableId DefinitionStableId { get; }

        public RoomOccupantClearRole ClearRole { get; }

        public bool BlocksRoomClear
        {
            get
            {
                return ClearRole == RoomOccupantClearRole.RequiredEnemy
                    || ClearRole == RoomOccupantClearRole.ObjectiveEntity;
            }
        }
    }

    public sealed class RoomOccupantView
    {
        public RoomOccupantView(
            StableId entityStableId,
            StableId definitionStableId,
            RoomOccupantClearRole clearRole,
            bool isTerminal)
        {
            EntityStableId = entityStableId
                ?? throw new ArgumentNullException(nameof(entityStableId));
            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            if (!Enum.IsDefined(typeof(RoomOccupantClearRole), clearRole))
            {
                throw new ArgumentOutOfRangeException(nameof(clearRole));
            }

            ClearRole = clearRole;
            IsTerminal = isTerminal;
        }

        public StableId EntityStableId { get; }

        public StableId DefinitionStableId { get; }

        public RoomOccupantClearRole ClearRole { get; }

        public bool IsTerminal { get; }

        public bool BlocksRoomClear
        {
            get
            {
                return ClearRole == RoomOccupantClearRole.RequiredEnemy
                    || ClearRole == RoomOccupantClearRole.ObjectiveEntity;
            }
        }
    }

    public sealed class RoomExitEligibilityView
    {
        public RoomExitEligibilityView(
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

    public sealed class RoomOccupancyView
    {
        private readonly ReadOnlyCollection<RoomOccupantView> occupants;
        private readonly ReadOnlyCollection<RoomExitEligibilityView> connectedExits;

        public RoomOccupancyView(
            StableId runtimeInstanceStableId,
            StableId roomStableId,
            long lifecycleGeneration,
            bool isActive,
            bool isOccupancyRegistered,
            bool isCleared,
            IEnumerable<RoomOccupantView> occupants,
            IEnumerable<RoomExitEligibilityView> connectedExits)
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

        public IReadOnlyList<RoomOccupantView> Occupants
        {
            get { return occupants; }
        }

        public IReadOnlyList<RoomExitEligibilityView> ConnectedExits
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

        private static ReadOnlyCollection<RoomOccupantView> CopyOccupants(
            IEnumerable<RoomOccupantView> source)
        {
            var copy = new List<RoomOccupantView>(
                source ?? Array.Empty<RoomOccupantView>());
            copy.Sort((left, right) => left.EntityStableId.CompareTo(
                right.EntityStableId));
            return new ReadOnlyCollection<RoomOccupantView>(copy);
        }

        private static ReadOnlyCollection<RoomExitEligibilityView> CopyExits(
            IEnumerable<RoomExitEligibilityView> source)
        {
            var copy = new List<RoomExitEligibilityView>(
                source ?? Array.Empty<RoomExitEligibilityView>());
            copy.Sort((left, right) => left.ExitStableId.CompareTo(
                right.ExitStableId));
            return new ReadOnlyCollection<RoomExitEligibilityView>(copy);
        }
    }

    public sealed class RoomOccupancySnapshot
    {
        private readonly ReadOnlyCollection<RoomOccupancyView> rooms;

        public RoomOccupancySnapshot(
            StableId runtimeInstanceStableId,
            StableId layoutStableId,
            string definitionFingerprint,
            long lifecycleGeneration,
            long sequence,
            IEnumerable<RoomOccupancyView> rooms)
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
            var copy = new List<RoomOccupancyView>(
                rooms ?? throw new ArgumentNullException(nameof(rooms)));
            copy.Sort((left, right) => left.RoomStableId.CompareTo(
                right.RoomStableId));
            this.rooms = new ReadOnlyCollection<RoomOccupancyView>(copy);
        }

        public StableId RuntimeInstanceStableId { get; }

        public StableId LayoutStableId { get; }

        public string DefinitionFingerprint { get; }

        public long LifecycleGeneration { get; }

        public long Sequence { get; }

        public IReadOnlyList<RoomOccupancyView> Rooms
        {
            get { return rooms; }
        }

        public RoomOccupancyView GetRoom(StableId roomStableId)
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

    public sealed class RoomClearTransition
    {
        public RoomClearTransition(
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

    public sealed class RegisterRoomOccupantsCommand
    {
        private readonly ReadOnlyCollection<RoomOccupantRegistration> occupants;

        public RegisterRoomOccupantsCommand(
            StableId runtimeInstanceStableId,
            StableId operationStableId,
            long lifecycleGeneration,
            StableId roomStableId,
            IEnumerable<RoomOccupantRegistration> occupants)
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
            this.occupants = new ReadOnlyCollection<RoomOccupantRegistration>(
                new List<RoomOccupantRegistration>(
                    occupants ?? throw new ArgumentNullException(nameof(occupants))));
        }

        public StableId RuntimeInstanceStableId { get; }

        public StableId OperationStableId { get; }

        public long LifecycleGeneration { get; }

        public StableId RoomStableId { get; }

        public IReadOnlyList<RoomOccupantRegistration> Occupants
        {
            get { return occupants; }
        }
    }

    public sealed class ActivateRoomCommand
    {
        public ActivateRoomCommand(
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

    public sealed class ReportRoomOccupantTerminalCommand
    {
        public ReportRoomOccupantTerminalCommand(
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

    public sealed class RestartRoomOccupancyCommand
    {
        public RestartRoomOccupancyCommand(
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

    public sealed class RoomOccupancyResult
    {
        public RoomOccupancyResult(
            RoomOccupancyStatus status,
            string rejectionCode,
            RoomOccupancySnapshot previousProjection,
            RoomOccupancySnapshot currentProjection,
            RoomClearTransition clearTransition)
        {
            if (!Enum.IsDefined(typeof(RoomOccupancyStatus), status))
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

        public RoomOccupancyStatus Status { get; }

        public string RejectionCode { get; }

        public RoomOccupancySnapshot PreviousProjection { get; }

        public RoomOccupancySnapshot CurrentProjection { get; }

        public RoomClearTransition ClearTransition { get; }

        public bool Changed
        {
            get { return Status == RoomOccupancyStatus.Applied; }
        }
    }

    public interface IRoomOccupancy
    {
        StableId RuntimeInstanceStableId { get; }

        RoomGraphDefinition Definition { get; }

        RoomOccupancySnapshot CurrentProjection { get; }

        RoomOccupancyView GetRoomProjection(StableId roomStableId);

        RoomOccupancyResult RegisterOccupants(
            RegisterRoomOccupantsCommand command);

        RoomOccupancyResult ActivateRoom(ActivateRoomCommand command);

        RoomOccupancyResult ReportTerminal(
            ReportRoomOccupantTerminalCommand command);

        RoomOccupancyResult Restart(RestartRoomOccupancyCommand command);
    }
}
