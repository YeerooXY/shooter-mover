using System;
using ShooterMover.Contracts.Rooms;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.ContentPackages.Environment.Doors
{
    public enum DoorTravelDirection
    {
        Forward = 1,
        Reverse = 2,
    }

    public enum DoorOneWayPolicy
    {
        Bidirectional = 1,
        ForwardOnly = 2,
        ReverseOnly = 3,
    }

    public enum DoorTransitionValidationCode
    {
        Valid = 0,
        MissingSourceSocket = 1,
        MissingDestinationSocket = 2,
        IncompatibleSockets = 3,
    }

    public enum DoorTransitionRequestStatus
    {
        Authorized = 1,
        DoorClosed = 2,
        RejectedByOneWayPolicy = 3,
        MissingTransitionConfiguration = 4,
        InvalidTransitionSockets = 5,
        MissingAuthorizationPort = 6,
        AuthorizationDenied = 7,
    }

    [Serializable]
    public sealed class DoorSocketAuthoring
    {
        [SerializeField] private string roomId = "room.unassigned";
        [SerializeField] private string projectionId = "projection.unassigned";
        [SerializeField] private string socketId = "socket.unassigned";
        [SerializeField] private RoomSocketDirection direction =
            RoomSocketDirection.Bidirectional;

        public RoomSocket BuildSocket()
        {
            return new RoomSocket(
                new RoomProjectionIdentity(
                    StableId.Parse(roomId),
                    StableId.Parse(projectionId)),
                StableId.Parse(socketId),
                direction);
        }

        public static DoorSocketAuthoring CreateRuntime(
            string roomId,
            string projectionId,
            string socketId,
            RoomSocketDirection direction)
        {
            return new DoorSocketAuthoring
            {
                roomId = roomId ?? string.Empty,
                projectionId = projectionId ?? string.Empty,
                socketId = socketId ?? string.Empty,
                direction = direction,
            };
        }
    }

    public sealed class DoorTransitionValidationResult
    {
        internal DoorTransitionValidationResult(
            DoorTransitionValidationCode code,
            string diagnostic)
        {
            Code = code;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public DoorTransitionValidationCode Code { get; }

        public string Diagnostic { get; }

        public bool IsValid
        {
            get { return Code == DoorTransitionValidationCode.Valid; }
        }
    }

    public sealed class DoorTransitionDefinition
    {
        public DoorTransitionDefinition(
            RoomSocket forwardSource,
            RoomSocket forwardDestination)
        {
            ForwardSource = forwardSource;
            ForwardDestination = forwardDestination;
            Validation = Validate(forwardSource, forwardDestination);
        }

        public RoomSocket ForwardSource { get; }

        public RoomSocket ForwardDestination { get; }

        public DoorTransitionValidationResult Validation { get; }

        public RoomSocket GetSource(DoorTravelDirection direction)
        {
            RequireDirection(direction);
            return direction == DoorTravelDirection.Forward
                ? ForwardSource
                : ForwardDestination;
        }

        public RoomSocket GetDestination(DoorTravelDirection direction)
        {
            RequireDirection(direction);
            return direction == DoorTravelDirection.Forward
                ? ForwardDestination
                : ForwardSource;
        }

        public static DoorTransitionValidationResult Validate(
            RoomSocket source,
            RoomSocket destination)
        {
            if (source == null)
            {
                return new DoorTransitionValidationResult(
                    DoorTransitionValidationCode.MissingSourceSocket,
                    "Transition door requires a typed source room socket.");
            }

            if (destination == null)
            {
                return new DoorTransitionValidationResult(
                    DoorTransitionValidationCode.MissingDestinationSocket,
                    "Transition door requires a typed destination room socket.");
            }

            if (!source.CanConnectTo(destination))
            {
                return new DoorTransitionValidationResult(
                    DoorTransitionValidationCode.IncompatibleSockets,
                    "Transition sockets must belong to distinct projections and have compatible directions.");
            }

            return new DoorTransitionValidationResult(
                DoorTransitionValidationCode.Valid,
                "Transition socket pair is valid.");
        }

        private static void RequireDirection(DoorTravelDirection direction)
        {
            if (direction != DoorTravelDirection.Forward
                && direction != DoorTravelDirection.Reverse)
            {
                throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
    }

    public sealed class DoorTransitionRequest
    {
        public DoorTransitionRequest(
            StableId doorPlacedInstanceId,
            DoorTravelDirection direction,
            RoomSocket source,
            RoomSocket destination)
        {
            DoorPlacedInstanceId = doorPlacedInstanceId
                ?? throw new ArgumentNullException(nameof(doorPlacedInstanceId));
            if (direction != DoorTravelDirection.Forward
                && direction != DoorTravelDirection.Reverse)
            {
                throw new ArgumentOutOfRangeException(nameof(direction));
            }

            Direction = direction;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Destination = destination
                ?? throw new ArgumentNullException(nameof(destination));
        }

        public StableId DoorPlacedInstanceId { get; }

        public DoorTravelDirection Direction { get; }

        public RoomSocket Source { get; }

        public RoomSocket Destination { get; }
    }

    public sealed class DoorTransitionAuthorization
    {
        private DoorTransitionAuthorization(bool isAuthorized, string diagnostic)
        {
            IsAuthorized = isAuthorized;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public bool IsAuthorized { get; }

        public string Diagnostic { get; }

        public static DoorTransitionAuthorization Authorized(string diagnostic = null)
        {
            return new DoorTransitionAuthorization(
                true,
                diagnostic ?? "Transition authorization was granted.");
        }

        public static DoorTransitionAuthorization Denied(string diagnostic)
        {
            return new DoorTransitionAuthorization(
                false,
                string.IsNullOrEmpty(diagnostic)
                    ? "Transition authorization was denied."
                    : diagnostic);
        }
    }

    public interface IDoorTransitionAuthorizationPort
    {
        DoorTransitionAuthorization Authorize(DoorTransitionRequest request);
    }

    public sealed class DoorTransitionRequestResult
    {
        internal DoorTransitionRequestResult(
            DoorTransitionRequestStatus status,
            DoorTransitionRequest request,
            DoorTransitionAuthorization authorization,
            string diagnostic)
        {
            Status = status;
            Request = request;
            Authorization = authorization;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public DoorTransitionRequestStatus Status { get; }

        public DoorTransitionRequest Request { get; }

        public DoorTransitionAuthorization Authorization { get; }

        public string Diagnostic { get; }

        public bool IsAuthorized
        {
            get { return Status == DoorTransitionRequestStatus.Authorized; }
        }
    }

    /// <summary>
    /// Small injectable authorization fixture. It does not load rooms, mutate mission
    /// state, consume keys, or spend currency.
    /// </summary>
    public sealed class DoorTransitionAuthorizationStub :
        IDoorTransitionAuthorizationPort
    {
        private bool authorized;
        private string diagnostic = "Transition authorization was denied.";

        public int RequestCount { get; private set; }

        public DoorTransitionRequest LastRequest { get; private set; }

        public void SetAuthorized(bool value, string reason = null)
        {
            authorized = value;
            diagnostic = reason
                ?? (value
                    ? "Transition authorization was granted."
                    : "Transition authorization was denied.");
        }

        public DoorTransitionAuthorization Authorize(DoorTransitionRequest request)
        {
            LastRequest = request
                ?? throw new ArgumentNullException(nameof(request));
            RequestCount++;
            return authorized
                ? DoorTransitionAuthorization.Authorized(diagnostic)
                : DoorTransitionAuthorization.Denied(diagnostic);
        }
    }
}
