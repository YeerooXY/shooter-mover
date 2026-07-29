using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Physics
{
    public enum MovementContactProcessResult
    {
        WallReflected = 1,
        WallNoIncomingImpact = 2,
        WallContactLimitReached = 3,
        EnemyResolved = 4,
        EnemyGraceIgnored = 5,
        EnemyCapacityRejected = 6,
        DuplicateCallbackIgnored = 7,
        UnknownContactIgnored = 8,
        InvalidContactIgnored = 9,
        TunnelingSentinelIgnored = 10,
        AuthorityUnavailable = 11,
        AdapterUnavailable = 12,
    }

    /// <summary>
    /// Immutable read model supplied by the authoritative movement owner at a contact boundary.
    /// The Unity adapter never manufactures or retains domain movement state.
    /// </summary>
    public sealed class MovementContactStateSnapshot
    {
        public MovementContactStateSnapshot(
            ThrusterBurstState movement,
            MovementThrusterTuningProfile tuning,
            double normalizedMoveX,
            double normalizedMoveY,
            PerContactGraceTracker graceTracker)
        {
            if (movement == null)
            {
                throw new ArgumentNullException(nameof(movement));
            }

            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (graceTracker == null)
            {
                throw new ArgumentNullException(nameof(graceTracker));
            }

            ValidateFinite(normalizedMoveX, nameof(normalizedMoveX));
            ValidateFinite(normalizedMoveY, nameof(normalizedMoveY));
            MovementThrusterTuningProfileValidator.Validate(tuning);

            if (movement.TuningIdentity != tuning.DeterministicIdentity)
            {
                throw new ArgumentException(
                    "Movement state and tuning must use the same deterministic identity.",
                    nameof(tuning));
            }

            if (graceTracker.TuningIdentity != tuning.DeterministicIdentity)
            {
                throw new ArgumentException(
                    "Contact grace and tuning must use the same deterministic identity.",
                    nameof(graceTracker));
            }

            Movement = movement;
            Tuning = tuning;
            NormalizedMoveX = normalizedMoveX;
            NormalizedMoveY = normalizedMoveY;
            GraceTracker = graceTracker;
        }

        public ThrusterBurstState Movement { get; }

        public MovementThrusterTuningProfile Tuning { get; }

        public double NormalizedMoveX { get; }

        public double NormalizedMoveY { get; }

        public PerContactGraceTracker GraceTracker { get; }

        private static void ValidateFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "Movement contact snapshot values must be finite.");
            }
        }
    }

    /// <summary>
    /// Inward-facing authority port. Implementations own the current movement state and grace tracker;
    /// this Unity adapter only requests a snapshot and submits validated MT-005/MT-006 outcomes.
    /// </summary>
    public interface IMovementContactState
    {
        bool TryReadContactSnapshot(out MovementContactStateSnapshot snapshot);

        void ApplyWallContact(WallReflectionResult result);

        void ApplyEnemyContact(
            ContactGraceRegistration registration,
            MovementContactResolution resolution,
            PerContactGraceTracker nextGraceTracker);
    }

    /// <summary>
    /// Converts Unity 2D collision callbacks into explicit movement-domain policy calls.
    /// Duplicate collider/normal callbacks are accepted at most once per fixed step. Corner normals
    /// remain distinct, while zero-normal contacts are treated as tunneling sentinels and ignored.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MovementContact : MonoBehaviour
    {
        private const double MinimumNormalMagnitudeSquared = 0.000000000001d;
        private const float ContactNormalQuantization = 100000f;

        private readonly HashSet<ContactCallbackKey> processedCallbacks =
            new HashSet<ContactCallbackKey>();

        private MovementBody bodyAdapter;
        private IMovementContactState authority;
        private long currentFixedStep = -1L;
        private int wallContactsProcessed;

        public bool IsConfigured
        {
            get { return bodyAdapter != null && authority != null; }
        }

        public long CurrentFixedStep
        {
            get { return currentFixedStep; }
        }

        public int WallContactsProcessed
        {
            get { return wallContactsProcessed; }
        }

        public void Configure(Rigidbody2D body, IMovementContactState contactAuthority)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (contactAuthority == null)
            {
                throw new ArgumentNullException(nameof(contactAuthority));
            }

            if (IsConfigured)
            {
                if (object.ReferenceEquals(bodyAdapter.Body, body)
                    && object.ReferenceEquals(authority, contactAuthority))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "MovementContact is already configured with different dependencies.");
            }

            bodyAdapter = new MovementBody(body);
            authority = contactAuthority;
            BeginFixedStep(0L);
        }

        /// <summary>
        /// Opens a deterministic fixed-step boundary. Repeating the same step is idempotent;
        /// moving backward is rejected without clearing duplicate protection.
        /// </summary>
        public bool BeginFixedStep(long fixedStep)
        {
            if (fixedStep < 0L || fixedStep < currentFixedStep)
            {
                return false;
            }

            if (fixedStep == currentFixedStep)
            {
                return true;
            }

            currentFixedStep = fixedStep;
            wallContactsProcessed = 0;
            processedCallbacks.Clear();
            return true;
        }

        /// <summary>
        /// Public deterministic seam used by collision callbacks, composition, and play-mode tests.
        /// The supplied normal must point from the contacted surface/body toward the mover.
        /// </summary>
        public MovementContactProcessResult TryProcessContact(
            Collider2D contactedCollider,
            Vector2 contactNormal,
            double observedAtSeconds)
        {
            if (!IsConfigured || !isActiveAndEnabled)
            {
                return MovementContactProcessResult.AdapterUnavailable;
            }

            if (!TryNormalizeContactNormal(contactNormal, out Vector2 normalizedNormal))
            {
                return MovementContactProcessResult.TunnelingSentinelIgnored;
            }

            if (double.IsNaN(observedAtSeconds)
                || double.IsInfinity(observedAtSeconds)
                || observedAtSeconds < 0d)
            {
                return MovementContactProcessResult.InvalidContactIgnored;
            }

            MovementContactDescriptor descriptor;
            MovementContactClassificationResult classification =
                MovementContactClassifier.Classify(contactedCollider, out descriptor);
            if (classification == MovementContactClassificationResult.MissingContract)
            {
                return MovementContactProcessResult.UnknownContactIgnored;
            }

            if (classification != MovementContactClassificationResult.Classified)
            {
                return MovementContactProcessResult.InvalidContactIgnored;
            }

            ContactCallbackKey callbackKey = new ContactCallbackKey(
                contactedCollider.GetInstanceID(),
                descriptor.Kind,
                normalizedNormal);
            if (!processedCallbacks.Add(callbackKey))
            {
                return MovementContactProcessResult.DuplicateCallbackIgnored;
            }

            MovementContactStateSnapshot snapshot;
            try
            {
                if (!authority.TryReadContactSnapshot(out snapshot) || snapshot == null)
                {
                    return MovementContactProcessResult.AuthorityUnavailable;
                }
            }
            catch (ArgumentException)
            {
                return MovementContactProcessResult.AuthorityUnavailable;
            }
            catch (InvalidOperationException)
            {
                return MovementContactProcessResult.AuthorityUnavailable;
            }

            return descriptor.Kind == MovementContactKind.Wall
                ? ProcessWall(snapshot, normalizedNormal)
                : ProcessEnemy(descriptor, snapshot, normalizedNormal, observedAtSeconds);
        }

        public int ProcessCollision(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return 0;
            }

            int processed = 0;
            double observedAtSeconds = Time.fixedTimeAsDouble;
            int contactCount = collision.contactCount;
            for (int index = 0; index < contactCount; index++)
            {
                ContactPoint2D contact = collision.GetContact(index);
                MovementContactProcessResult result = TryProcessContact(
                    collision.collider,
                    contact.normal,
                    observedAtSeconds);
                if (IsAppliedOutcome(result))
                {
                    processed++;
                }
            }

            return processed;
        }

        private MovementContactProcessResult ProcessWall(
            MovementContactStateSnapshot snapshot,
            Vector2 normal)
        {
            try
            {
                WallReflectionResult result = WallReflectionPolicy.Reflect(
                    snapshot.Movement,
                    normal.x,
                    normal.y,
                    snapshot.NormalizedMoveX,
                    snapshot.NormalizedMoveY,
                    wallContactsProcessed,
                    snapshot.Tuning);

                wallContactsProcessed = result.ContactsProcessed;
                authority.ApplyWallContact(result);
                bodyAdapter.Apply(result.State);

                if (result.Outcome == WallReflectionOutcome.Reflected)
                {
                    return MovementContactProcessResult.WallReflected;
                }

                return result.Outcome == WallReflectionOutcome.ContactLimitReached
                    ? MovementContactProcessResult.WallContactLimitReached
                    : MovementContactProcessResult.WallNoIncomingImpact;
            }
            catch (ArgumentException)
            {
                return MovementContactProcessResult.InvalidContactIgnored;
            }
            catch (InvalidOperationException)
            {
                return MovementContactProcessResult.InvalidContactIgnored;
            }
        }

        private MovementContactProcessResult ProcessEnemy(
            MovementContactDescriptor descriptor,
            MovementContactStateSnapshot snapshot,
            Vector2 normal,
            double observedAtSeconds)
        {
            try
            {
                ContactGraceRegistration[] registrations;
                PerContactGraceTracker nextGraceTracker = snapshot.GraceTracker.RegisterMany(
                    new[] { descriptor.EnemyId },
                    observedAtSeconds,
                    out registrations);
                ContactGraceRegistration registration = registrations[0];

                MovementContactResolution resolution = null;
                if (registration.ContactAccepted)
                {
                    resolution = MovementContactPolicy.Resolve(
                        snapshot.Movement,
                        (int)descriptor.WeightMessage.Result,
                        normal.x,
                        normal.y,
                        snapshot.Tuning);
                }

                authority.ApplyEnemyContact(registration, resolution, nextGraceTracker);
                if (resolution != null)
                {
                    bodyAdapter.ApplyAuthoritativeVelocity(
                        resolution.VelocityX,
                        resolution.VelocityY);
                    return MovementContactProcessResult.EnemyResolved;
                }

                return registration.Decision == ContactGraceDecision.CapacityRejected
                    ? MovementContactProcessResult.EnemyCapacityRejected
                    : MovementContactProcessResult.EnemyGraceIgnored;
            }
            catch (ArgumentException)
            {
                return MovementContactProcessResult.InvalidContactIgnored;
            }
            catch (InvalidOperationException)
            {
                return MovementContactProcessResult.InvalidContactIgnored;
            }
        }

        private static bool TryNormalizeContactNormal(Vector2 normal, out Vector2 normalized)
        {
            if (float.IsNaN(normal.x)
                || float.IsInfinity(normal.x)
                || float.IsNaN(normal.y)
                || float.IsInfinity(normal.y))
            {
                normalized = Vector2.zero;
                return false;
            }

            double magnitudeSquared =
                ((double)normal.x * normal.x)
                + ((double)normal.y * normal.y);
            if (magnitudeSquared <= MinimumNormalMagnitudeSquared)
            {
                normalized = Vector2.zero;
                return false;
            }

            double inverseMagnitude = 1d / Math.Sqrt(magnitudeSquared);
            normalized = new Vector2(
                (float)(normal.x * inverseMagnitude),
                (float)(normal.y * inverseMagnitude));
            return true;
        }

        private static bool IsAppliedOutcome(MovementContactProcessResult result)
        {
            return result == MovementContactProcessResult.WallReflected
                || result == MovementContactProcessResult.WallNoIncomingImpact
                || result == MovementContactProcessResult.WallContactLimitReached
                || result == MovementContactProcessResult.EnemyResolved
                || result == MovementContactProcessResult.EnemyGraceIgnored
                || result == MovementContactProcessResult.EnemyCapacityRejected;
        }

        private void FixedUpdate()
        {
            long nextFixedStep = currentFixedStep == long.MaxValue
                ? long.MaxValue
                : currentFixedStep + 1L;
            BeginFixedStep(nextFixedStep);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            ProcessCollision(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            ProcessCollision(collision);
        }

        private void OnDisable()
        {
            processedCallbacks.Clear();
            wallContactsProcessed = 0;
        }

        private void OnDestroy()
        {
            processedCallbacks.Clear();
            bodyAdapter = null;
            authority = null;
        }

        private readonly struct ContactCallbackKey : IEquatable<ContactCallbackKey>
        {
            private readonly int colliderInstanceId;
            private readonly MovementContactKind kind;
            private readonly int normalX;
            private readonly int normalY;

            public ContactCallbackKey(
                int colliderInstanceId,
                MovementContactKind kind,
                Vector2 normalizedNormal)
            {
                this.colliderInstanceId = colliderInstanceId;
                this.kind = kind;
                normalX = Mathf.RoundToInt(normalizedNormal.x * ContactNormalQuantization);
                normalY = Mathf.RoundToInt(normalizedNormal.y * ContactNormalQuantization);
            }

            public bool Equals(ContactCallbackKey other)
            {
                return colliderInstanceId == other.colliderInstanceId
                    && kind == other.kind
                    && normalX == other.normalX
                    && normalY == other.normalY;
            }

            public override bool Equals(object obj)
            {
                return obj is ContactCallbackKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = (hash * 31) + colliderInstanceId;
                    hash = (hash * 31) + kind.GetHashCode();
                    hash = (hash * 31) + normalX;
                    hash = (hash * 31) + normalY;
                    return hash;
                }
            }
        }
    }
}
