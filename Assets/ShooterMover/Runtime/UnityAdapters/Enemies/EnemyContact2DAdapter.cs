using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Physics;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    public enum EnemyContact2DRegistrationStatus
    {
        Registered = 1,
        AlreadyRegistered = 2,
        InvalidInput = 3,
        Ambiguous = 4,
        CapacityReached = 5,
    }

    public enum EnemyContact2DStatus
    {
        Accepted = 1,
        GraceIgnored = 2,
        DuplicateIgnored = 3,
        TargetAlreadyDestroyed = 4,
        NotConfigured = 5,
        AdapterInactive = 6,
        InvalidInput = 7,
        UnknownMover = 8,
        AuthorityUnavailable = 9,
        InvalidDomainResult = 10,
    }

    public sealed class EnemyContact2DApplication
    {
        internal EnemyContact2DApplication(
            EnemyContact2DStatus status,
            ContactMessage contactMessage,
            WeightMessage weightMessage,
            EnemyActorStepResult domainResult,
            EnemyContactNotification contactNotification)
        {
            Status = status;
            ContactMessage = contactMessage;
            WeightMessage = weightMessage;
            DomainResult = domainResult;
            ContactNotification = contactNotification;
        }

        public EnemyContact2DStatus Status { get; }

        public ContactMessage ContactMessage { get; }

        public WeightMessage WeightMessage { get; }

        public EnemyActorStepResult DomainResult { get; }

        public EnemyContactNotification ContactNotification { get; }

        public bool RequestsMoverDamage
        {
            get
            {
                return ContactNotification != null
                    && ContactNotification.RequestsMoverDamage;
            }
        }

        public double MoverDamageAmount
        {
            get
            {
                return ContactNotification == null
                    ? 0d
                    : ContactNotification.MoverDamageAmount;
            }
        }
    }

    /// <summary>
    /// Shared enemy-side Physics2D contact boundary. It submits one EN-002 contact
    /// command per mover/classification/fixed-step and also implements MT-009's
    /// explicit movement-contact contract. It never receives or writes a player body.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyContact2DAdapter :
        MonoBehaviour,
        IMovementContact2DContract
    {
        public const int HardMaximumMoverColliders = 64;

        private sealed class MoverBinding
        {
            public MoverBinding(
                Collider2D collider,
                StableId moverId,
                CombatWeightClass moverWeight)
            {
                Collider = collider;
                MoverId = moverId;
                MoverWeight = moverWeight;
            }

            public Collider2D Collider { get; }

            public StableId MoverId { get; }

            public CombatWeightClass MoverWeight { get; }
        }

        private readonly Dictionary<int, MoverBinding> moversByInstanceId =
            new Dictionary<int, MoverBinding>();
        private readonly HashSet<ContactCallbackKey> processedCallbacks =
            new HashSet<ContactCallbackKey>();

        private EnemyTarget2DAdapter enemyTarget;
        private IEnemyActor2DAuthority authority;
        private StableId expectedMoverId;
        private CombatWeightClass expectedMoverWeight;
        private int maximumMoverColliders;
        private long currentFixedStep = -1L;
        private long generation;
        private bool activeRequested;

        public bool IsConfigured
        {
            get
            {
                return enemyTarget != null
                    && authority != null
                    && expectedMoverId != null
                    && maximumMoverColliders > 0;
            }
        }

        public bool IsActive
        {
            get { return activeRequested && isActiveAndEnabled; }
        }

        public int RegisteredMoverColliderCount
        {
            get { return moversByInstanceId.Count; }
        }

        public int ProcessedCallbackCount
        {
            get { return processedCallbacks.Count; }
        }

        public long CurrentFixedStep
        {
            get { return currentFixedStep; }
        }

        public long Generation
        {
            get { return generation; }
        }

        public void Configure(
            EnemyTarget2DAdapter target,
            IEnemyActor2DAuthority actorAuthority,
            StableId moverId,
            CombatWeightClass moverWeight,
            int moverColliderCapacity)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (actorAuthority == null)
            {
                throw new ArgumentNullException(nameof(actorAuthority));
            }

            if (moverId == null)
            {
                throw new ArgumentNullException(nameof(moverId));
            }

            if (!Enum.IsDefined(typeof(CombatWeightClass), moverWeight))
            {
                throw new ArgumentOutOfRangeException(nameof(moverWeight));
            }

            if (moverColliderCapacity <= 0
                || moverColliderCapacity > HardMaximumMoverColliders)
            {
                throw new ArgumentOutOfRangeException(nameof(moverColliderCapacity));
            }

            if (!target.IsConfigured
                || target.TargetCollider == null
                || !target.CanReceiveEnemyDamage
                || !object.ReferenceEquals(target.gameObject, gameObject)
                || !object.ReferenceEquals(target.TargetCollider.gameObject, gameObject))
            {
                throw new ArgumentException(
                    "Enemy contact requires a configured damage target on the same explicit GameObject.",
                    nameof(target));
            }

            EnemyActorState state;
            if (!actorAuthority.TryReadState(out state)
                || state == null
                || state.ActorId != target.TargetId)
            {
                throw new ArgumentException(
                    "Enemy contact authority must expose the target actor identity.",
                    nameof(actorAuthority));
            }

            if (IsConfigured)
            {
                if (object.ReferenceEquals(enemyTarget, target)
                    && object.ReferenceEquals(authority, actorAuthority)
                    && expectedMoverId == moverId
                    && expectedMoverWeight == moverWeight
                    && maximumMoverColliders == moverColliderCapacity)
                {
                    return;
                }

                throw new InvalidOperationException(
                    "EnemyContact2DAdapter is already configured with different dependencies.");
            }

            enemyTarget = target;
            authority = actorAuthority;
            expectedMoverId = moverId;
            expectedMoverWeight = moverWeight;
            maximumMoverColliders = moverColliderCapacity;
            currentFixedStep = -1L;
            BeginFixedStep(0L);
        }

        public EnemyContact2DRegistrationStatus RegisterMoverCollider(
            Collider2D moverCollider,
            StableId moverId,
            CombatWeightClass moverWeight)
        {
            if (!IsConfigured
                || moverCollider == null
                || moverId == null
                || !Enum.IsDefined(typeof(CombatWeightClass), moverWeight))
            {
                return EnemyContact2DRegistrationStatus.InvalidInput;
            }

            if (moverId != expectedMoverId || moverWeight != expectedMoverWeight)
            {
                return EnemyContact2DRegistrationStatus.Ambiguous;
            }

            int instanceId = moverCollider.GetInstanceID();
            MoverBinding existing;
            if (moversByInstanceId.TryGetValue(instanceId, out existing))
            {
                if (existing.Collider == moverCollider
                    && existing.MoverId == moverId
                    && existing.MoverWeight == moverWeight)
                {
                    return EnemyContact2DRegistrationStatus.AlreadyRegistered;
                }

                return EnemyContact2DRegistrationStatus.Ambiguous;
            }

            if (moversByInstanceId.Count >= maximumMoverColliders)
            {
                return EnemyContact2DRegistrationStatus.CapacityReached;
            }

            moversByInstanceId.Add(
                instanceId,
                new MoverBinding(moverCollider, moverId, moverWeight));
            return EnemyContact2DRegistrationStatus.Registered;
        }

        public bool UnregisterMoverCollider(
            Collider2D moverCollider,
            StableId moverId)
        {
            if (moverCollider == null || moverId == null)
            {
                return false;
            }

            int instanceId = moverCollider.GetInstanceID();
            MoverBinding existing;
            if (!moversByInstanceId.TryGetValue(instanceId, out existing)
                || existing.Collider != moverCollider
                || existing.MoverId != moverId)
            {
                return false;
            }

            return moversByInstanceId.Remove(instanceId);
        }

        public bool Activate()
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException(
                    "EnemyContact2DAdapter must be configured before activation.");
            }

            if (activeRequested)
            {
                return false;
            }

            activeRequested = true;
            return true;
        }

        public bool Deactivate()
        {
            bool changed = activeRequested;
            activeRequested = false;
            processedCallbacks.Clear();
            return changed;
        }

        public void ResetSession()
        {
            processedCallbacks.Clear();
            currentFixedStep = -1L;
            generation = generation == long.MaxValue ? long.MaxValue : generation + 1L;
            BeginFixedStep(0L);
        }

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
            processedCallbacks.Clear();
            return true;
        }

        public EnemyContact2DApplication TryProcessContact(
            Collider2D moverCollider,
            ContactClassification classification,
            double observedAtSeconds)
        {
            if (!IsConfigured)
            {
                return Result(EnemyContact2DStatus.NotConfigured, null, null, null, null);
            }

            if (!IsActive)
            {
                return Result(EnemyContact2DStatus.AdapterInactive, null, null, null, null);
            }

            if (moverCollider == null
                || !Enum.IsDefined(typeof(ContactClassification), classification)
                || double.IsNaN(observedAtSeconds)
                || double.IsInfinity(observedAtSeconds)
                || observedAtSeconds < 0d
                || currentFixedStep < 0L)
            {
                return Result(EnemyContact2DStatus.InvalidInput, null, null, null, null);
            }

            MoverBinding mover;
            if (!moversByInstanceId.TryGetValue(moverCollider.GetInstanceID(), out mover)
                || mover.Collider != moverCollider
                || mover.MoverId != expectedMoverId
                || mover.MoverWeight != expectedMoverWeight)
            {
                return Result(EnemyContact2DStatus.UnknownMover, null, null, null, null);
            }

            EnemyActorState state;
            try
            {
                if (!TryReadState(out state))
                {
                    return Result(
                        EnemyContact2DStatus.AuthorityUnavailable,
                        null,
                        null,
                        null,
                        null);
                }
            }
            catch (ArgumentException)
            {
                return Result(EnemyContact2DStatus.AuthorityUnavailable, null, null, null, null);
            }
            catch (InvalidOperationException)
            {
                return Result(EnemyContact2DStatus.AuthorityUnavailable, null, null, null, null);
            }

            StableId eventId = CreateContactEventId(
                generation,
                currentFixedStep,
                mover.MoverId,
                state.ActorId,
                classification);
            ContactCallbackKey callbackKey = new ContactCallbackKey(
                mover.MoverId,
                classification);

            WeightMessage weightMessage;
            try
            {
                weightMessage = CreateWeightMessage(eventId, state, mover);
            }
            catch (ArgumentException)
            {
                return Result(EnemyContact2DStatus.InvalidInput, null, null, null, null);
            }

            if (!processedCallbacks.Add(callbackKey))
            {
                ContactMessage duplicate = new ContactMessage(
                    eventId,
                    mover.MoverId,
                    state.ActorId,
                    CombatChannel.Contact,
                    classification,
                    ContactResult.DuplicateEventIgnored);
                return Result(
                    EnemyContact2DStatus.DuplicateIgnored,
                    duplicate,
                    weightMessage,
                    null,
                    null);
            }

            EnemyActorStepResult applied;
            try
            {
                applied = authority.Apply(
                    EnemyActorCommand.Contact(
                        currentFixedStep,
                        eventId,
                        mover.MoverId,
                        observedAtSeconds,
                        (int)classification,
                        (int)mover.MoverWeight));
            }
            catch (ArgumentException)
            {
                processedCallbacks.Remove(callbackKey);
                return Result(EnemyContact2DStatus.InvalidInput, null, weightMessage, null, null);
            }
            catch (InvalidOperationException)
            {
                processedCallbacks.Remove(callbackKey);
                return Result(
                    EnemyContact2DStatus.AuthorityUnavailable,
                    null,
                    weightMessage,
                    null,
                    null);
            }

            if (applied == null || applied.State == null)
            {
                processedCallbacks.Remove(callbackKey);
                return Result(
                    EnemyContact2DStatus.AuthorityUnavailable,
                    null,
                    weightMessage,
                    applied,
                    null);
            }

            EnemyContactNotification notification = FindContactNotification(applied, eventId);
            if (notification == null
                || !Enum.IsDefined(typeof(ContactResult), notification.ResultValue))
            {
                processedCallbacks.Remove(callbackKey);
                return Result(
                    EnemyContact2DStatus.InvalidDomainResult,
                    null,
                    weightMessage,
                    applied,
                    notification);
            }

            ContactResult contactResult = (ContactResult)notification.ResultValue;
            ContactMessage contactMessage = new ContactMessage(
                eventId,
                mover.MoverId,
                state.ActorId,
                CombatChannel.Contact,
                classification,
                contactResult);

            EnemyContact2DStatus status;
            if (contactResult == ContactResult.Accepted)
            {
                status = EnemyContact2DStatus.Accepted;
            }
            else if (contactResult == ContactResult.DuplicateEventIgnored)
            {
                status = EnemyContact2DStatus.DuplicateIgnored;
            }
            else if (contactResult == ContactResult.TargetAlreadyDestroyed)
            {
                status = EnemyContact2DStatus.TargetAlreadyDestroyed;
            }
            else
            {
                status = EnemyContact2DStatus.GraceIgnored;
            }

            return Result(status, contactMessage, weightMessage, applied, notification);
        }

        /// <summary>
        /// MT-009 consumes this projection to apply its own accepted player movement
        /// contact policy. This method reports identity/weight only and cannot apply
        /// enemy damage or change any player velocity.
        /// </summary>
        public bool TryDescribeMovementContact(
            out MovementContact2DDescriptor descriptor)
        {
            descriptor = null;
            if (!IsConfigured || !IsActive)
            {
                return false;
            }

            EnemyActorState state;
            try
            {
                if (!TryReadState(out state) || !state.IsActive)
                {
                    return false;
                }

                StableId eventId = CreateBoundaryEventId(
                    generation,
                    expectedMoverId,
                    state.ActorId);
                WeightMessage weight = new WeightMessage(
                    eventId,
                    expectedMoverId,
                    state.ActorId,
                    CombatChannel.Contact,
                    expectedMoverWeight,
                    (CombatWeightClass)state.WeightClassValue,
                    WeightMessage.DetermineResult(
                        expectedMoverWeight,
                        (CombatWeightClass)state.WeightClassValue));
                descriptor = MovementContact2DDescriptor.Enemy(state.ActorId, weight);
                return true;
            }
            catch (ArgumentException)
            {
                descriptor = null;
                return false;
            }
            catch (InvalidOperationException)
            {
                descriptor = null;
                return false;
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision != null && collision.collider != null)
            {
                TryProcessContact(
                    collision.collider,
                    ContactClassification.BodyImpact,
                    Time.fixedTimeAsDouble);
            }
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (collision != null && collision.collider != null)
            {
                TryProcessContact(
                    collision.collider,
                    ContactClassification.SustainedBodyContact,
                    Time.fixedTimeAsDouble);
            }
        }

        private void OnDisable()
        {
            Deactivate();
        }

        private void OnDestroy()
        {
            activeRequested = false;
            processedCallbacks.Clear();
            moversByInstanceId.Clear();
            enemyTarget = null;
            authority = null;
            expectedMoverId = null;
        }

        private bool TryReadState(out EnemyActorState state)
        {
            state = null;
            return authority != null
                && authority.TryReadState(out state)
                && state != null
                && enemyTarget != null
                && state.ActorId == enemyTarget.TargetId;
        }

        private static WeightMessage CreateWeightMessage(
            StableId eventId,
            EnemyActorState state,
            MoverBinding mover)
        {
            CombatWeightClass enemyWeight =
                (CombatWeightClass)state.WeightClassValue;
            return new WeightMessage(
                eventId,
                mover.MoverId,
                state.ActorId,
                CombatChannel.Contact,
                mover.MoverWeight,
                enemyWeight,
                WeightMessage.DetermineResult(mover.MoverWeight, enemyWeight));
        }

        private static EnemyContactNotification FindContactNotification(
            EnemyActorStepResult result,
            StableId eventId)
        {
            for (int index = 0; index < result.Notifications.Count; index++)
            {
                EnemyContactNotification notification =
                    result.Notifications[index] as EnemyContactNotification;
                if (notification != null && notification.EventId == eventId)
                {
                    return notification;
                }
            }

            return null;
        }

        private static StableId CreateContactEventId(
            long generationValue,
            long fixedStep,
            StableId moverId,
            StableId actorId,
            ContactClassification classification)
        {
            string canonical = "enemy-contact|"
                + generationValue.ToString(CultureInfo.InvariantCulture)
                + "|"
                + fixedStep.ToString(CultureInfo.InvariantCulture)
                + "|"
                + moverId
                + "|"
                + actorId
                + "|"
                + ((int)classification).ToString(CultureInfo.InvariantCulture);
            return StableId.Create("event", "enemy-contact-" + Hash(canonical));
        }

        private static StableId CreateBoundaryEventId(
            long generationValue,
            StableId moverId,
            StableId actorId)
        {
            string canonical = "enemy-movement-boundary|"
                + generationValue.ToString(CultureInfo.InvariantCulture)
                + "|"
                + moverId
                + "|"
                + actorId;
            return StableId.Create("event", "enemy-weight-" + Hash(canonical));
        }

        private static string Hash(string text)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis;
            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= prime;
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private static EnemyContact2DApplication Result(
            EnemyContact2DStatus status,
            ContactMessage contactMessage,
            WeightMessage weightMessage,
            EnemyActorStepResult domainResult,
            EnemyContactNotification notification)
        {
            return new EnemyContact2DApplication(
                status,
                contactMessage,
                weightMessage,
                domainResult,
                notification);
        }

        private readonly struct ContactCallbackKey : IEquatable<ContactCallbackKey>
        {
            private readonly StableId moverId;
            private readonly ContactClassification classification;

            public ContactCallbackKey(
                StableId moverId,
                ContactClassification classification)
            {
                this.moverId = moverId;
                this.classification = classification;
            }

            public bool Equals(ContactCallbackKey other)
            {
                return moverId == other.moverId
                    && classification == other.classification;
            }

            public override bool Equals(object obj)
            {
                return obj is ContactCallbackKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((moverId == null ? 0 : moverId.GetHashCode()) * 397)
                        ^ classification.GetHashCode();
                }
            }
        }
    }
}
