using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Flow.PlaySelection
{
    public enum PlayModeAvailability
    {
        Available = 1,
        PrototypeUnavailable = 2,
    }

    public enum PlayModeDestination
    {
        None = 0,
        LevelSelection = 1,
    }

    public enum PlaySelectionRoute
    {
        None = 0,
        Hub = 1,
        LevelSelection = 2,
    }

    public enum PlaySelectionStatus
    {
        RouteEmitted = 1,
        ModeUnavailable = 2,
        UnknownMode = 3,
        InvalidPayload = 4,
        InputLocked = 5,
    }

    public sealed class PlayModeDefinition : IEquatable<PlayModeDefinition>
    {
        public PlayModeDefinition(
            StableId modeStableId,
            string displayName,
            string description,
            PlayModeAvailability availability,
            PlayModeDestination destination,
            int sortOrder)
        {
            ModeStableId = modeStableId
                ?? throw new ArgumentNullException(nameof(modeStableId));
            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A play mode display name is required.",
                    nameof(displayName));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException(
                    "A play mode description is required.",
                    nameof(description));
            }

            if (!Enum.IsDefined(typeof(PlayModeAvailability), availability))
            {
                throw new ArgumentOutOfRangeException(nameof(availability));
            }

            if (!Enum.IsDefined(typeof(PlayModeDestination), destination))
            {
                throw new ArgumentOutOfRangeException(nameof(destination));
            }

            if (sortOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sortOrder));
            }

            if (availability == PlayModeAvailability.Available
                && destination == PlayModeDestination.None)
            {
                throw new ArgumentException(
                    "An available play mode requires a destination.",
                    nameof(destination));
            }

            if (availability != PlayModeAvailability.Available
                && destination != PlayModeDestination.None)
            {
                throw new ArgumentException(
                    "An unavailable play mode cannot expose a destination.",
                    nameof(destination));
            }

            DisplayName = displayName.Trim();
            Description = description.Trim();
            Availability = availability;
            Destination = destination;
            SortOrder = sortOrder;
        }

        public StableId ModeStableId { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public PlayModeAvailability Availability { get; }

        public PlayModeDestination Destination { get; }

        public int SortOrder { get; }

        public bool Equals(PlayModeDefinition other)
        {
            return !ReferenceEquals(other, null)
                && ModeStableId == other.ModeStableId
                && string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal)
                && string.Equals(Description, other.Description, StringComparison.Ordinal)
                && Availability == other.Availability
                && Destination == other.Destination
                && SortOrder == other.SortOrder;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayModeDefinition);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + ModeStableId.GetHashCode();
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(DisplayName);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Description);
                hash = (hash * 31) + (int)Availability;
                hash = (hash * 31) + (int)Destination;
                hash = (hash * 31) + SortOrder;
                return hash;
            }
        }
    }

    public sealed class PlayModeCatalog
    {
        private sealed class DefinitionComparer : IComparer<PlayModeDefinition>
        {
            public int Compare(PlayModeDefinition left, PlayModeDefinition right)
            {
                if (ReferenceEquals(left, right))
                {
                    return 0;
                }

                if (ReferenceEquals(left, null))
                {
                    return -1;
                }

                if (ReferenceEquals(right, null))
                {
                    return 1;
                }

                int orderComparison = left.SortOrder.CompareTo(right.SortOrder);
                if (orderComparison != 0)
                {
                    return orderComparison;
                }

                return string.Compare(
                    left.ModeStableId.ToString(),
                    right.ModeStableId.ToString(),
                    StringComparison.Ordinal);
            }
        }

        private readonly ReadOnlyCollection<PlayModeDefinition> modes;
        private readonly Dictionary<StableId, PlayModeDefinition> modesById;

        public PlayModeCatalog(IEnumerable<PlayModeDefinition> modes)
        {
            if (modes == null)
            {
                throw new ArgumentNullException(nameof(modes));
            }

            var ordered = new List<PlayModeDefinition>(modes);
            if (ordered.Count == 0)
            {
                throw new ArgumentException(
                    "At least one play mode is required.",
                    nameof(modes));
            }

            ordered.Sort(new DefinitionComparer());
            modesById = new Dictionary<StableId, PlayModeDefinition>();
            for (int index = 0; index < ordered.Count; index++)
            {
                PlayModeDefinition definition = ordered[index];
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Play mode definitions cannot contain null.",
                        nameof(modes));
                }

                if (modesById.ContainsKey(definition.ModeStableId))
                {
                    throw new ArgumentException(
                        "Play mode identities must be unique.",
                        nameof(modes));
                }

                modesById.Add(definition.ModeStableId, definition);
            }

            this.modes = new ReadOnlyCollection<PlayModeDefinition>(ordered);
        }

        public IReadOnlyList<PlayModeDefinition> Modes
        {
            get { return modes; }
        }

        public bool TryGet(
            StableId modeStableId,
            out PlayModeDefinition definition)
        {
            if (modeStableId == null)
            {
                definition = null;
                return false;
            }

            return modesById.TryGetValue(modeStableId, out definition);
        }
    }

    public sealed class PlaySelectionResult
    {
        internal PlaySelectionResult(
            PlaySelectionStatus status,
            PlaySelectionRoute route,
            StableId selectedModeStableId,
            PlayerRouteProfilePayload payload,
            string feedbackCode)
        {
            Status = status;
            Route = route;
            SelectedModeStableId = selectedModeStableId;
            Payload = payload;
            FeedbackCode = feedbackCode ?? string.Empty;
        }

        public PlaySelectionStatus Status { get; }

        public PlaySelectionRoute Route { get; }

        public StableId SelectedModeStableId { get; }

        public PlayerRouteProfilePayload Payload { get; }

        public string FeedbackCode { get; }

        public bool RouteEmitted
        {
            get { return Status == PlaySelectionStatus.RouteEmitted; }
        }
    }

    /// <summary>
    /// Presentation boundary for the next flow owner. PLAY-001 only emits route intent;
    /// it never loads gameplay or starts a networking implementation.
    /// </summary>
    public interface IPlaySelectionRouteBridge
    {
        void Present(
            PlaySelectionRoute route,
            PlayerRouteProfilePayload payload);
    }

    /// <summary>
    /// Deterministic decision owner for the Play screen. It retains the exact incoming
    /// immutable HUB payload and locks after the first emitted terminal route.
    /// </summary>
    public sealed class PlaySelectionActions
    {
        public const string SoloModeStableIdText = "play-mode.solo";
        public const string MultiplayerModeStableIdText = "play-mode.multiplayer";

        private readonly PlayerRouteProfilePayload payload;
        private readonly PlayModeCatalog catalog;
        private PlaySelectionResult terminalResult;

        public PlaySelectionActions(
            PlayerRouteProfilePayload payload,
            PlayModeCatalog catalog)
        {
            this.payload = payload;
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
        }

        public PlayerRouteProfilePayload Payload
        {
            get { return payload; }
        }

        public PlayModeCatalog Catalog
        {
            get { return catalog; }
        }

        public bool IsInputLocked
        {
            get { return terminalResult != null; }
        }

        public PlaySelectionResult TerminalResult
        {
            get { return terminalResult; }
        }

        public PlaySelectionResult SelectMode(StableId modeStableId)
        {
            if (terminalResult != null)
            {
                return Result(
                    PlaySelectionStatus.InputLocked,
                    PlaySelectionRoute.None,
                    modeStableId,
                    "play-selection-input-locked");
            }

            if (!HasValidPayload())
            {
                return Result(
                    PlaySelectionStatus.InvalidPayload,
                    PlaySelectionRoute.None,
                    modeStableId,
                    "play-selection-payload-invalid");
            }

            PlayModeDefinition definition;
            if (!catalog.TryGet(modeStableId, out definition))
            {
                return Result(
                    PlaySelectionStatus.UnknownMode,
                    PlaySelectionRoute.None,
                    modeStableId,
                    "play-selection-mode-unknown");
            }

            if (definition.Availability != PlayModeAvailability.Available)
            {
                return Result(
                    PlaySelectionStatus.ModeUnavailable,
                    PlaySelectionRoute.None,
                    definition.ModeStableId,
                    "play-selection-mode-prototype-unavailable");
            }

            PlaySelectionRoute route;
            switch (definition.Destination)
            {
                case PlayModeDestination.LevelSelection:
                    route = PlaySelectionRoute.LevelSelection;
                    break;
                default:
                    return Result(
                        PlaySelectionStatus.UnknownMode,
                        PlaySelectionRoute.None,
                        definition.ModeStableId,
                        "play-selection-destination-unsupported");
            }

            terminalResult = Result(
                PlaySelectionStatus.RouteEmitted,
                route,
                definition.ModeStableId,
                string.Empty);
            return terminalResult;
        }

        public PlaySelectionResult NavigateBack()
        {
            if (terminalResult != null)
            {
                return Result(
                    PlaySelectionStatus.InputLocked,
                    PlaySelectionRoute.None,
                    null,
                    "play-selection-input-locked");
            }

            if (!HasValidPayload())
            {
                return Result(
                    PlaySelectionStatus.InvalidPayload,
                    PlaySelectionRoute.None,
                    null,
                    "play-selection-payload-invalid");
            }

            terminalResult = Result(
                PlaySelectionStatus.RouteEmitted,
                PlaySelectionRoute.Hub,
                null,
                string.Empty);
            return terminalResult;
        }

        private bool HasValidPayload()
        {
            return payload != null && payload.HasValidFingerprint();
        }

        private PlaySelectionResult Result(
            PlaySelectionStatus status,
            PlaySelectionRoute route,
            StableId selectedModeStableId,
            string feedbackCode)
        {
            return new PlaySelectionResult(
                status,
                route,
                selectedModeStableId,
                payload,
                feedbackCode);
        }
    }
}
