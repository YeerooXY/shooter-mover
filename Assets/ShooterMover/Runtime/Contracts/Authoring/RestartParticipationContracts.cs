using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Authoring
{
    public enum RestartLifecyclePhase
    {
        RetireAttempt = 0,
        ReleaseTransientResources = 1,
        ApplyResetProjection = 2,
        CompleteRebind = 3
    }

    public sealed class RestartContext
    {
        public RestartContext(
            StableId runId,
            StableId runtimeProjectionId,
            long retiringAttemptGeneration,
            long replacementAttemptGeneration)
        {
            RunId = runId ?? throw new ArgumentNullException(nameof(runId));
            RuntimeProjectionId = runtimeProjectionId
                ?? throw new ArgumentNullException(nameof(runtimeProjectionId));

            if (retiringAttemptGeneration < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(retiringAttemptGeneration));
            }

            if (replacementAttemptGeneration <= retiringAttemptGeneration)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(replacementAttemptGeneration),
                    replacementAttemptGeneration,
                    "Replacement attempt generation must advance monotonically.");
            }

            RetiringAttemptGeneration = retiringAttemptGeneration;
            ReplacementAttemptGeneration = replacementAttemptGeneration;
        }

        public StableId RunId { get; }

        public StableId RuntimeProjectionId { get; }

        public long RetiringAttemptGeneration { get; }

        public long ReplacementAttemptGeneration { get; }
    }

    /// <summary>
    /// Package-owned participant. The generic authoring layer sequences phases but
    /// never decides how health, rewards, projectiles, doors, or mission state reset.
    /// </summary>
    public interface IRestartParticipant
    {
        StableId RestartParticipantId { get; }

        void OnRestartPhase(RestartContext context, RestartLifecyclePhase phase);
    }

    public sealed class RestartParticipantRegistrationRequest
    {
        public RestartParticipantRegistrationRequest(
            IRestartParticipant participant,
            object ownerToken,
            string diagnosticLocation)
        {
            Participant = participant ?? throw new ArgumentNullException(nameof(participant));
            OwnerToken = ownerToken ?? throw new ArgumentNullException(nameof(ownerToken));
            DiagnosticLocation = string.IsNullOrEmpty(diagnosticLocation)
                ? "<unspecified>"
                : diagnosticLocation;

            if (Participant.RestartParticipantId == null)
            {
                throw new ArgumentException(
                    "Restart participant identity is required.",
                    nameof(participant));
            }
        }

        public IRestartParticipant Participant { get; }

        public object OwnerToken { get; }

        public string DiagnosticLocation { get; }
    }

    public enum RestartParticipantRegistrationStatus
    {
        Registered = 0,
        DuplicateNoChange = 1,
        RejectedDuplicateParticipantId = 2,
        InvalidRequest = 3
    }

    public sealed class RestartParticipantRegistrationResult
    {
        private RestartParticipantRegistrationResult(
            RestartParticipantRegistrationStatus status,
            StableId participantId,
            string existingLocation,
            string attemptedLocation,
            string diagnostic)
        {
            Status = status;
            ParticipantId = participantId;
            ExistingLocation = existingLocation;
            AttemptedLocation = attemptedLocation;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public RestartParticipantRegistrationStatus Status { get; }

        public StableId ParticipantId { get; }

        public string ExistingLocation { get; }

        public string AttemptedLocation { get; }

        public string Diagnostic { get; }

        public bool IsAccepted
        {
            get
            {
                return Status == RestartParticipantRegistrationStatus.Registered
                    || Status == RestartParticipantRegistrationStatus.DuplicateNoChange;
            }
        }

        public static RestartParticipantRegistrationResult Registered(
            StableId participantId,
            string location)
        {
            return new RestartParticipantRegistrationResult(
                RestartParticipantRegistrationStatus.Registered,
                participantId,
                null,
                location,
                "Restart participant registered.");
        }

        public static RestartParticipantRegistrationResult DuplicateNoChange(
            StableId participantId,
            string location)
        {
            return new RestartParticipantRegistrationResult(
                RestartParticipantRegistrationStatus.DuplicateNoChange,
                participantId,
                location,
                location,
                "Exact restart participant retry produced no change.");
        }

        public static RestartParticipantRegistrationResult DuplicateId(
            StableId participantId,
            string existingLocation,
            string attemptedLocation)
        {
            return new RestartParticipantRegistrationResult(
                RestartParticipantRegistrationStatus.RejectedDuplicateParticipantId,
                participantId,
                existingLocation,
                attemptedLocation,
                "A different participant attempted to reuse a restart participant ID.");
        }

        public static RestartParticipantRegistrationResult Invalid(string diagnostic)
        {
            return new RestartParticipantRegistrationResult(
                RestartParticipantRegistrationStatus.InvalidRequest,
                null,
                null,
                null,
                diagnostic);
        }
    }

    public sealed class RestartParticipantRegistry
    {
        private sealed class Entry
        {
            public Entry(RestartParticipantRegistrationRequest request)
            {
                Request = request;
            }

            public RestartParticipantRegistrationRequest Request { get; }
        }

        private readonly Dictionary<StableId, Entry> _entries =
            new Dictionary<StableId, Entry>();

        public int Count
        {
            get { return _entries.Count; }
        }

        public RestartParticipantRegistrationResult Register(
            RestartParticipantRegistrationRequest request)
        {
            if (request == null)
            {
                return RestartParticipantRegistrationResult.Invalid(
                    "Restart participant registration request cannot be null.");
            }

            StableId id = request.Participant.RestartParticipantId;
            Entry existing;
            if (!_entries.TryGetValue(id, out existing))
            {
                _entries.Add(id, new Entry(request));
                return RestartParticipantRegistrationResult.Registered(
                    id,
                    request.DiagnosticLocation);
            }

            if (ReferenceEquals(existing.Request.OwnerToken, request.OwnerToken)
                && ReferenceEquals(existing.Request.Participant, request.Participant))
            {
                return RestartParticipantRegistrationResult.DuplicateNoChange(
                    id,
                    existing.Request.DiagnosticLocation);
            }

            return RestartParticipantRegistrationResult.DuplicateId(
                id,
                existing.Request.DiagnosticLocation,
                request.DiagnosticLocation);
        }

        public bool Unregister(StableId participantId, object ownerToken)
        {
            if (participantId == null || ownerToken == null)
            {
                return false;
            }

            Entry existing;
            if (!_entries.TryGetValue(participantId, out existing)
                || !ReferenceEquals(existing.Request.OwnerToken, ownerToken))
            {
                return false;
            }

            return _entries.Remove(participantId);
        }

        public IReadOnlyList<IRestartParticipant> ReadOrderedSnapshot()
        {
            List<IRestartParticipant> participants =
                new List<IRestartParticipant>(_entries.Count);

            foreach (KeyValuePair<StableId, Entry> pair in _entries)
            {
                participants.Add(pair.Value.Request.Participant);
            }

            participants.Sort((left, right) =>
                left.RestartParticipantId.CompareTo(right.RestartParticipantId));

            return new ReadOnlyCollection<IRestartParticipant>(participants);
        }
    }

    public interface IRestartParticipantRegistrar
    {
        RestartParticipantRegistrationResult RegisterRestartParticipant(
            RestartParticipantRegistrationRequest request);

        bool UnregisterRestartParticipant(StableId participantId, object ownerToken);
    }
}
