using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Authoring
{
    /// <summary>
    /// Unity-facing definitions implement this narrow port so adapters never need
    /// an outward reference to the Content.Definitions assembly.
    /// </summary>
    public interface IObjectCapabilityDefinitionSource
    {
        StableId CapabilityId { get; }

        CapabilityDefinition BuildDefinition();
    }

    /// <summary>
    /// Produces one immutable engine-independent family snapshot.
    /// </summary>
    public interface IObjectFamilyDefinitionSource
    {
        StableId FamilyId { get; }

        ObjectFamilyDefinition BuildDefinition();
    }

    /// <summary>
    /// The only accepted input for an identity that did not originate as an authored placement.
    /// </summary>
    public sealed class RuntimeSpawnIdentityInput
    {
        public RuntimeSpawnIdentityInput(StableId runtimeObjectId, StableId spawnOperationId)
        {
            RuntimeObjectId = runtimeObjectId ?? throw new ArgumentNullException(nameof(runtimeObjectId));
            SpawnOperationId = spawnOperationId ?? throw new ArgumentNullException(nameof(spawnOperationId));
        }

        public StableId RuntimeObjectId { get; }

        public StableId SpawnOperationId { get; }

        public PlacedObjectIdentity CreateIdentity()
        {
            return PlacedObjectIdentity.CreateRuntimeSpawned(RuntimeObjectId, SpawnOperationId);
        }
    }

    /// <summary>
    /// Immutable registration payload for one runtime projection of an authored object.
    /// </summary>
    public sealed class PlacedParticipantRegistration : IEquatable<PlacedParticipantRegistration>
    {
        private readonly ReadOnlyCollection<CapabilityReference> _declaredCapabilities;

        public PlacedParticipantRegistration(
            PlacedObjectIdentity identity,
            ObjectDefinitionReference definition,
            StableId runtimeProjectionId,
            StableId runId,
            long attemptGeneration,
            IEnumerable<CapabilityReference> declaredCapabilities,
            string resolvedFingerprint)
        {
            Identity = identity ?? throw new ArgumentNullException(nameof(identity));
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            RuntimeProjectionId = runtimeProjectionId
                ?? throw new ArgumentNullException(nameof(runtimeProjectionId));
            RunId = runId ?? throw new ArgumentNullException(nameof(runId));

            if (attemptGeneration < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attemptGeneration),
                    attemptGeneration,
                    "Attempt generation cannot be negative.");
            }

            if (string.IsNullOrEmpty(resolvedFingerprint))
            {
                throw new ArgumentException(
                    "Resolved fingerprint is required.",
                    nameof(resolvedFingerprint));
            }

            AttemptGeneration = attemptGeneration;
            ResolvedFingerprint = resolvedFingerprint;

            List<CapabilityReference> ordered = new List<CapabilityReference>();
            if (declaredCapabilities != null)
            {
                foreach (CapabilityReference capability in declaredCapabilities)
                {
                    if (capability == null)
                    {
                        throw new ArgumentException(
                            "Declared capabilities cannot contain null.",
                            nameof(declaredCapabilities));
                    }

                    ordered.Add(capability);
                }
            }

            ordered.Sort((left, right) =>
                left.CapabilityId.CompareTo(right.CapabilityId));

            for (int index = 1; index < ordered.Count; index++)
            {
                if (ordered[index - 1].CapabilityId.Equals(ordered[index].CapabilityId))
                {
                    throw new ArgumentException(
                        "Declared capabilities cannot contain duplicate IDs.",
                        nameof(declaredCapabilities));
                }
            }

            _declaredCapabilities = new ReadOnlyCollection<CapabilityReference>(ordered);
            CanonicalFingerprint = BuildFingerprint();
        }

        public PlacedObjectIdentity Identity { get; }

        public ObjectDefinitionReference Definition { get; }

        public StableId RuntimeProjectionId { get; }

        public StableId RunId { get; }

        public long AttemptGeneration { get; }

        public IReadOnlyList<CapabilityReference> DeclaredCapabilities
        {
            get { return _declaredCapabilities; }
        }

        public string ResolvedFingerprint { get; }

        public string CanonicalFingerprint { get; }

        public bool Equals(PlacedParticipantRegistration other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    CanonicalFingerprint,
                    other.CanonicalFingerprint,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlacedParticipantRegistration);
        }

        public override int GetHashCode()
        {
            return DeterministicHash32(CanonicalFingerprint);
        }

        private string BuildFingerprint()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("identity=").Append(Identity.Value).Append('|');
            builder.Append("kind=").Append((int)Identity.Kind).Append('|');
            builder.Append("spawn=").Append(Identity.SpawnOperationId).Append('|');
            builder.Append("family=").Append(Definition.FamilyId).Append('|');
            builder.Append("variant=").Append(Definition.VariantId).Append('|');
            builder.Append("projection=").Append(RuntimeProjectionId).Append('|');
            builder.Append("run=").Append(RunId).Append('|');
            builder.Append("attempt=").Append(AttemptGeneration).Append('|');
            builder.Append("resolved=").Append(ResolvedFingerprint).Append('|');

            for (int index = 0; index < _declaredCapabilities.Count; index++)
            {
                builder.Append("capability=")
                    .Append(_declaredCapabilities[index].CapabilityId)
                    .Append('|');
            }

            return DeterministicFingerprint64(builder.ToString());
        }

        internal static string DeterministicFingerprint64(string text)
        {
            unchecked
            {
                const ulong offsetBasis = 14695981039346656037UL;
                const ulong prime = 1099511628211UL;
                ulong hash = offsetBasis;

                for (int index = 0; index < text.Length; index++)
                {
                    char value = text[index];
                    hash ^= (byte)(value & 0xff);
                    hash *= prime;
                    hash ^= (byte)(value >> 8);
                    hash *= prime;
                }

                return hash.ToString("x16");
            }
        }

        private static int DeterministicHash32(string text)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619u;
                }

                return (int)hash;
            }
        }
    }

    public sealed class SceneScopeRegistrationRequest
    {
        public SceneScopeRegistrationRequest(
            PlacedParticipantRegistration registration,
            object ownerToken,
            string diagnosticLocation)
        {
            Registration = registration ?? throw new ArgumentNullException(nameof(registration));
            OwnerToken = ownerToken ?? throw new ArgumentNullException(nameof(ownerToken));
            DiagnosticLocation = string.IsNullOrEmpty(diagnosticLocation)
                ? "<unspecified>"
                : diagnosticLocation;
        }

        public PlacedParticipantRegistration Registration { get; }

        /// <summary>
        /// Runtime-only reference identity. It is never serialized or fingerprinted.
        /// </summary>
        public object OwnerToken { get; }

        public string DiagnosticLocation { get; }
    }

    public enum SceneScopeRegistrationStatus
    {
        Registered = 0,
        DuplicateNoChange = 1,
        RejectedDuplicateIdentity = 2,
        RejectedConflictingRegistration = 3,
        InvalidRequest = 4
    }

    public sealed class SceneScopeRegistrationResult
    {
        private SceneScopeRegistrationResult(
            SceneScopeRegistrationStatus status,
            StableId placedInstanceId,
            string existingLocation,
            string attemptedLocation,
            string diagnostic)
        {
            Status = status;
            PlacedInstanceId = placedInstanceId;
            ExistingLocation = existingLocation;
            AttemptedLocation = attemptedLocation;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public SceneScopeRegistrationStatus Status { get; }

        public StableId PlacedInstanceId { get; }

        public string ExistingLocation { get; }

        public string AttemptedLocation { get; }

        public string Diagnostic { get; }

        public bool IsAccepted
        {
            get
            {
                return Status == SceneScopeRegistrationStatus.Registered
                    || Status == SceneScopeRegistrationStatus.DuplicateNoChange;
            }
        }

        public static SceneScopeRegistrationResult Registered(
            StableId placedInstanceId,
            string attemptedLocation)
        {
            return new SceneScopeRegistrationResult(
                SceneScopeRegistrationStatus.Registered,
                placedInstanceId,
                null,
                attemptedLocation,
                "Placed participant registered.");
        }

        public static SceneScopeRegistrationResult DuplicateNoChange(
            StableId placedInstanceId,
            string existingLocation)
        {
            return new SceneScopeRegistrationResult(
                SceneScopeRegistrationStatus.DuplicateNoChange,
                placedInstanceId,
                existingLocation,
                existingLocation,
                "Exact registration retry produced no change.");
        }

        public static SceneScopeRegistrationResult DuplicateIdentity(
            StableId placedInstanceId,
            string existingLocation,
            string attemptedLocation)
        {
            return new SceneScopeRegistrationResult(
                SceneScopeRegistrationStatus.RejectedDuplicateIdentity,
                placedInstanceId,
                existingLocation,
                attemptedLocation,
                "A different runtime owner attempted to reuse an identity already registered in this scope.");
        }

        public static SceneScopeRegistrationResult ConflictingRegistration(
            StableId placedInstanceId,
            string existingLocation,
            string attemptedLocation)
        {
            return new SceneScopeRegistrationResult(
                SceneScopeRegistrationStatus.RejectedConflictingRegistration,
                placedInstanceId,
                existingLocation,
                attemptedLocation,
                "The same runtime owner retried the placed identity with a conflicting immutable payload.");
        }

        public static SceneScopeRegistrationResult Invalid(string diagnostic)
        {
            return new SceneScopeRegistrationResult(
                SceneScopeRegistrationStatus.InvalidRequest,
                null,
                null,
                null,
                diagnostic);
        }
    }

    /// <summary>
    /// Deterministic per-scope registration index. A scope owns one instance;
    /// there is intentionally no static or globally discovered registry.
    /// </summary>
    public sealed class SceneScopeRegistrationRegistry
    {
        private sealed class Entry
        {
            public Entry(SceneScopeRegistrationRequest request)
            {
                Request = request;
            }

            public SceneScopeRegistrationRequest Request { get; }
        }

        private readonly Dictionary<StableId, Entry> _entries =
            new Dictionary<StableId, Entry>();

        public int Count
        {
            get { return _entries.Count; }
        }

        public SceneScopeRegistrationResult Register(SceneScopeRegistrationRequest request)
        {
            if (request == null)
            {
                return SceneScopeRegistrationResult.Invalid(
                    "Scene-scope registration request cannot be null.");
            }

            StableId id = request.Registration.Identity.Value;
            Entry existing;
            if (!_entries.TryGetValue(id, out existing))
            {
                _entries.Add(id, new Entry(request));
                return SceneScopeRegistrationResult.Registered(
                    id,
                    request.DiagnosticLocation);
            }

            if (!ReferenceEquals(existing.Request.OwnerToken, request.OwnerToken))
            {
                return SceneScopeRegistrationResult.DuplicateIdentity(
                    id,
                    existing.Request.DiagnosticLocation,
                    request.DiagnosticLocation);
            }

            if (existing.Request.Registration.Equals(request.Registration))
            {
                return SceneScopeRegistrationResult.DuplicateNoChange(
                    id,
                    existing.Request.DiagnosticLocation);
            }

            return SceneScopeRegistrationResult.ConflictingRegistration(
                id,
                existing.Request.DiagnosticLocation,
                request.DiagnosticLocation);
        }

        public bool Unregister(StableId placedInstanceId, object ownerToken)
        {
            if (placedInstanceId == null || ownerToken == null)
            {
                return false;
            }

            Entry existing;
            if (!_entries.TryGetValue(placedInstanceId, out existing)
                || !ReferenceEquals(existing.Request.OwnerToken, ownerToken))
            {
                return false;
            }

            return _entries.Remove(placedInstanceId);
        }

        public bool Contains(StableId placedInstanceId)
        {
            return placedInstanceId != null && _entries.ContainsKey(placedInstanceId);
        }

        public IReadOnlyList<PlacedParticipantRegistration> ReadOrderedSnapshot()
        {
            List<PlacedParticipantRegistration> snapshot =
                new List<PlacedParticipantRegistration>(_entries.Count);

            foreach (KeyValuePair<StableId, Entry> pair in _entries)
            {
                snapshot.Add(pair.Value.Request.Registration);
            }

            snapshot.Sort((left, right) =>
                left.Identity.Value.CompareTo(right.Identity.Value));

            return new ReadOnlyCollection<PlacedParticipantRegistration>(snapshot);
        }
    }

    public interface IPlacedObjectSceneScope
    {
        StableId ScopeId { get; }

        StableId CompatibilityId { get; }

        StableId RuntimeProjectionId { get; }

        StableId RunId { get; }

        long AttemptGeneration { get; }

        bool IsCompatible(StableId requiredCompatibilityId);

        SceneScopeRegistrationResult Register(SceneScopeRegistrationRequest request);

        bool Unregister(StableId placedInstanceId, object ownerToken);
    }

    public enum SceneScopeBindingStatus
    {
        Bound = 0,
        MissingScope = 1,
        ConflictingParentScopes = 2,
        IncompatibleExplicitScope = 3,
        CrossSceneExplicitScope = 4,
        InvalidIdentity = 5,
        InvalidDefinition = 6,
        RegistrationRejected = 7,
        AlreadyBound = 8
    }

    public enum SceneScopeBindingDiagnosticCode
    {
        None = 0,
        MissingRequiredScope = 1,
        MultipleCompatibleScopesAtNearestAncestor = 2,
        ExplicitScopeIsIncompatible = 3,
        ExplicitScopeCrossesSceneBoundary = 4,
        AuthoredIdentityIsMalformed = 5,
        FamilyDefinitionIsMissing = 6,
        FamilyDefinitionIsInvalid = 7,
        VariantResolutionFailed = 8,
        DuplicatePlacedIdentity = 9,
        ConflictingRegistration = 10,
        ScopeConfigurationIsInvalid = 11
    }

    public sealed class SceneScopeBindingResult
    {
        private SceneScopeBindingResult(
            SceneScopeBindingStatus status,
            SceneScopeBindingDiagnosticCode diagnosticCode,
            StableId placedInstanceId,
            StableId scopeId,
            SceneScopeRegistrationResult registrationResult,
            string diagnostic)
        {
            Status = status;
            DiagnosticCode = diagnosticCode;
            PlacedInstanceId = placedInstanceId;
            ScopeId = scopeId;
            RegistrationResult = registrationResult;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public SceneScopeBindingStatus Status { get; }

        public SceneScopeBindingDiagnosticCode DiagnosticCode { get; }

        public StableId PlacedInstanceId { get; }

        public StableId ScopeId { get; }

        public SceneScopeRegistrationResult RegistrationResult { get; }

        public string Diagnostic { get; }

        public bool IsBound
        {
            get
            {
                return Status == SceneScopeBindingStatus.Bound
                    || Status == SceneScopeBindingStatus.AlreadyBound;
            }
        }

        public static SceneScopeBindingResult Bound(
            StableId placedInstanceId,
            StableId scopeId,
            SceneScopeRegistrationResult registrationResult)
        {
            return new SceneScopeBindingResult(
                SceneScopeBindingStatus.Bound,
                SceneScopeBindingDiagnosticCode.None,
                placedInstanceId,
                scopeId,
                registrationResult,
                "Placed object bound to its scene scope.");
        }

        public static SceneScopeBindingResult AlreadyBound(
            StableId placedInstanceId,
            StableId scopeId)
        {
            return new SceneScopeBindingResult(
                SceneScopeBindingStatus.AlreadyBound,
                SceneScopeBindingDiagnosticCode.None,
                placedInstanceId,
                scopeId,
                null,
                "Placed object is already bound.");
        }

        public static SceneScopeBindingResult Failed(
            SceneScopeBindingStatus status,
            SceneScopeBindingDiagnosticCode diagnosticCode,
            StableId placedInstanceId,
            StableId scopeId,
            SceneScopeRegistrationResult registrationResult,
            string diagnostic)
        {
            if (status == SceneScopeBindingStatus.Bound
                || status == SceneScopeBindingStatus.AlreadyBound)
            {
                throw new ArgumentException(
                    "A failure result cannot use a bound status.",
                    nameof(status));
            }

            return new SceneScopeBindingResult(
                status,
                diagnosticCode,
                placedInstanceId,
                scopeId,
                registrationResult,
                diagnostic);
        }
    }
}
