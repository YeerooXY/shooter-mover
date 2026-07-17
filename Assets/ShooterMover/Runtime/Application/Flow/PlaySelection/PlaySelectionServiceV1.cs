using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Contracts.Flow.Session;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Flow.PlaySelection
{
    public enum PlayModeAvailabilityV1
    {
        Available = 1,
        PrototypeUnavailable = 2,
    }

    public enum PlayModeDestinationV1
    {
        None = 0,
        LevelSelection = 1,
    }

    public enum PlaySelectionRouteV1
    {
        None = 0,
        Hub = 1,
        LevelSelection = 2,
    }

    public enum PlaySelectionStatusV1
    {
        RouteEmitted = 1,
        ModeUnavailable = 2,
        UnknownMode = 3,
        InvalidPayload = 4,
        InputLocked = 5,
    }

    public sealed class PlayModeDefinitionV1 : IEquatable<PlayModeDefinitionV1>
    {
        public PlayModeDefinitionV1(
            StableId modeStableId,
            string displayName,
            string description,
            PlayModeAvailabilityV1 availability,
            PlayModeDestinationV1 destination,
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

            if (!Enum.IsDefined(typeof(PlayModeAvailabilityV1), availability))
            {
                throw new ArgumentOutOfRangeException(nameof(availability));
            }

            if (!Enum.IsDefined(typeof(PlayModeDestinationV1), destination))
            {
                throw new ArgumentOutOfRangeException(nameof(destination));
            }

            if (sortOrder < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sortOrder));
            }

            if (availability == PlayModeAvailabilityV1.Available
                && destination == PlayModeDestinationV1.None)
            {
                throw new ArgumentException(
                    "An available play mode requires a destination.",
                    nameof(destination));
            }

            if (availability != PlayModeAvailabilityV1.Available
                && destination != PlayModeDestinationV1.None)
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

        public PlayModeAvailabilityV1 Availability { get; }

        public PlayModeDestinationV1 Destination { get; }

        public int SortOrder { get; }

        public bool Equals(PlayModeDefinitionV1 other)
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
            return Equals(obj as PlayModeDefinitionV1);
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

    public sealed class PlayModeCatalogV1
    {
        private sealed class DefinitionComparer : IComparer<PlayModeDefinitionV1>
        {
            public int Compare(PlayModeDefinitionV1 left, PlayModeDefinitionV1 right)
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

        private readonly ReadOnlyCollection<PlayModeDefinitionV1> modes;
        private readonly Dictionary<StableId, PlayModeDefinitionV1> modesById;

        public PlayModeCatalogV1(IEnumerable<PlayModeDefinitionV1> modes)
        {
            if (modes == null)
            {
                throw new ArgumentNullException(nameof(modes));
            }

            var ordered = new List<PlayModeDefinitionV1>(modes);
            if (ordered.Count == 0)
            {
                throw new ArgumentException(
                    "At least one play mode is required.",
                    nameof(modes));
            }

            ordered.Sort(new DefinitionComparer());
            modesById = new Dictionary<StableId, PlayModeDefinitionV1>();
            for (int index = 0; index < ordered.Count; index++)
            {
                PlayModeDefinitionV1 definition = ordered[index];
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

            this.modes = new ReadOnlyCollection<PlayModeDefinitionV1>(ordered);
        }

        public IReadOnlyList<PlayModeDefinitionV1> Modes
        {
            get { return modes; }
        }

        public bool TryGet(
            StableId modeStableId,
            out PlayModeDefinitionV1 definition)
        {
            if (modeStableId == null)
            {
                definition = null;
                return false;
            }

            return modesById.TryGetValue(modeStableId, out definition);
        }
    }

    public sealed class PlaySelectionResultV1
    {
        internal PlaySelectionResultV1(
            PlaySelectionStatusV1 status,
            PlaySelectionRouteV1 route,
            StableId selectedModeStableId,
            PlayerRouteProfilePayloadV1 payload,
            string feedbackCode)
        {
            Status = status;
            Route = route;
            SelectedModeStableId = selectedModeStableId;
            Payload = payload;
            FeedbackCode = feedbackCode ?? string.Empty;
        }

        public PlaySelectionStatusV1 Status { get; }

        public PlaySelectionRouteV1 Route { get; }

        public StableId SelectedModeStableId { get; }

        public PlayerRouteProfilePayloadV1 Payload { get; }

        public string FeedbackCode { get; }

        public bool RouteEmitted
        {
            get { return Status == PlaySelectionStatusV1.RouteEmitted; }
        }
    }

    /// <summary>
    /// Presentation boundary for the next flow owner. PLAY-001 only emits route intent;
    /// it never loads gameplay or starts a networking implementation.
    /// </summary>
    public interface IPlaySelectionRouteAdapterV1
    {
        void Present(
            PlaySelectionRouteV1 route,
            PlayerRouteProfilePayloadV1 payload);
    }

    /// <summary>
    /// Deterministic decision owner for the Play screen. It retains the exact incoming
    /// immutable HUB payload and locks after the first emitted terminal route.
    /// </summary>
    public sealed class PlaySelectionServiceV1
    {
        public const string SoloModeStableIdText = "play-mode.solo";
        public const string MultiplayerModeStableIdText = "play-mode.multiplayer";

        private readonly PlayerRouteProfilePayloadV1 payload;
        private readonly PlayModeCatalogV1 catalog;
        private PlaySelectionResultV1 terminalResult;

        public PlaySelectionServiceV1(
            PlayerRouteProfilePayloadV1 payload,
            PlayModeCatalogV1 catalog)
        {
            this.payload = payload;
            this.catalog = catalog
                ?? throw new ArgumentNullException(nameof(catalog));
        }

        public PlayerRouteProfilePayloadV1 Payload
        {
            get { return payload; }
        }

        public PlayModeCatalogV1 Catalog
        {
            get { return catalog; }
        }

        public bool IsInputLocked
        {
            get { return terminalResult != null; }
        }

        public PlaySelectionResultV1 TerminalResult
        {
            get { return terminalResult; }
        }

        public PlaySelectionResultV1 SelectMode(StableId modeStableId)
        {
            if (terminalResult != null)
            {
                return Result(
                    PlaySelectionStatusV1.InputLocked,
                    PlaySelectionRouteV1.None,
                    modeStableId,
                    "play-selection-input-locked");
            }

            if (!HasValidPayload())
            {
                return Result(
                    PlaySelectionStatusV1.InvalidPayload,
                    PlaySelectionRouteV1.None,
                    modeStableId,
                    "play-selection-payload-invalid");
            }

            PlayModeDefinitionV1 definition;
            if (!catalog.TryGet(modeStableId, out definition))
            {
                return Result(
                    PlaySelectionStatusV1.UnknownMode,
                    PlaySelectionRouteV1.None,
                    modeStableId,
                    "play-selection-mode-unknown");
            }

            if (definition.Availability != PlayModeAvailabilityV1.Available)
            {
                return Result(
                    PlaySelectionStatusV1.ModeUnavailable,
                    PlaySelectionRouteV1.None,
                    definition.ModeStableId,
                    "play-selection-mode-prototype-unavailable");
            }

            PlaySelectionRouteV1 route;
            switch (definition.Destination)
            {
                case PlayModeDestinationV1.LevelSelection:
                    route = PlaySelectionRouteV1.LevelSelection;
                    break;
                default:
                    return Result(
                        PlaySelectionStatusV1.UnknownMode,
                        PlaySelectionRouteV1.None,
                        definition.ModeStableId,
                        "play-selection-destination-unsupported");
            }

            terminalResult = Result(
                PlaySelectionStatusV1.RouteEmitted,
                route,
                definition.ModeStableId,
                string.Empty);
            return terminalResult;
        }

        public PlaySelectionResultV1 NavigateBack()
        {
            if (terminalResult != null)
            {
                return Result(
                    PlaySelectionStatusV1.InputLocked,
                    PlaySelectionRouteV1.None,
                    null,
                    "play-selection-input-locked");
            }

            if (!HasValidPayload())
            {
                return Result(
                    PlaySelectionStatusV1.InvalidPayload,
                    PlaySelectionRouteV1.None,
                    null,
                    "play-selection-payload-invalid");
            }

            terminalResult = Result(
                PlaySelectionStatusV1.RouteEmitted,
                PlaySelectionRouteV1.Hub,
                null,
                string.Empty);
            return terminalResult;
        }

        private bool HasValidPayload()
        {
            return payload != null && payload.HasValidFingerprint();
        }

        private PlaySelectionResultV1 Result(
            PlaySelectionStatusV1 status,
            PlaySelectionRouteV1 route,
            StableId selectedModeStableId,
            string feedbackCode)
        {
            return new PlaySelectionResultV1(
                status,
                route,
                selectedModeStableId,
                payload,
                feedbackCode);
        }
    }
}
