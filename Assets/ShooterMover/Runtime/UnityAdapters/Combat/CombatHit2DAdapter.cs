using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Combat
{
    public enum CombatHit2DTargetRegistrationStatus
    {
        Registered = 1,
        AlreadyRegistered = 2,
        InvalidInput = 3,
        Ambiguous = 4,
    }

    public enum CombatHit2DTranslationStatus
    {
        Confirmed = 1,
        DuplicateIgnored = 2,
        TargetAlreadyDestroyed = 3,
        InvalidInput = 4,
        UnknownTarget = 5,
        ConflictingDuplicate = 6,
    }

    /// <summary>
    /// Immutable outcome from translating one confirmed Physics2D callback into
    /// the shared CS-004 hit-message contract.
    /// </summary>
    public sealed class CombatHit2DTranslationResult
    {
        internal CombatHit2DTranslationResult(
            CombatHit2DTranslationStatus status,
            HitMessage message)
        {
            Status = status;
            Message = message;
        }

        public CombatHit2DTranslationStatus Status { get; }

        public HitMessage Message { get; }

        public bool HasMessage
        {
            get { return Message != null; }
        }
    }

    /// <summary>
    /// Explicit session-local Physics2D hit boundary. Target identities are registered
    /// by the lifecycle owner; no scene search, tags, names, singleton, or service
    /// locator is used. The adapter translates facts only and applies no damage.
    /// </summary>
    public sealed class CombatHit2DAdapter
    {
        private sealed class TargetBinding
        {
            public TargetBinding(Collider2D collider, StableId targetId)
            {
                Collider = collider;
                TargetId = targetId;
            }

            public Collider2D Collider { get; }

            public StableId TargetId { get; }
        }

        private readonly StableId sourceId;
        private readonly Dictionary<int, TargetBinding> targetsByInstanceId =
            new Dictionary<int, TargetBinding>();
        private readonly Dictionary<StableId, HitMessage> firstMessagesByEventId =
            new Dictionary<StableId, HitMessage>();

        public CombatHit2DAdapter(StableId sourceId)
        {
            if (sourceId == null)
            {
                throw new ArgumentNullException(nameof(sourceId));
            }

            this.sourceId = sourceId;
        }

        public StableId SourceId
        {
            get { return sourceId; }
        }

        public int RegisteredTargetCount
        {
            get { return targetsByInstanceId.Count; }
        }

        public int ProcessedEventCount
        {
            get { return firstMessagesByEventId.Count; }
        }

        /// <summary>
        /// Registers an explicit Collider2D-to-StableId binding. Repeating the exact
        /// same binding is idempotent; reusing one collider for another identity is
        /// reported as ambiguous and does not alter the existing registration.
        /// </summary>
        public CombatHit2DTargetRegistrationStatus RegisterTarget(
            Collider2D targetCollider,
            StableId targetId)
        {
            if (targetCollider == null || targetId == null)
            {
                return CombatHit2DTargetRegistrationStatus.InvalidInput;
            }

            int instanceId = targetCollider.GetInstanceID();
            TargetBinding existing;
            if (targetsByInstanceId.TryGetValue(instanceId, out existing))
            {
                if (existing.Collider == targetCollider && existing.TargetId == targetId)
                {
                    return CombatHit2DTargetRegistrationStatus.AlreadyRegistered;
                }

                return CombatHit2DTargetRegistrationStatus.Ambiguous;
            }

            targetsByInstanceId.Add(instanceId, new TargetBinding(targetCollider, targetId));
            return CombatHit2DTargetRegistrationStatus.Registered;
        }

        public bool UnregisterTarget(Collider2D targetCollider, StableId targetId)
        {
            if (targetCollider == null || targetId == null)
            {
                return false;
            }

            int instanceId = targetCollider.GetInstanceID();
            TargetBinding existing;
            if (!targetsByInstanceId.TryGetValue(instanceId, out existing)
                || existing.Collider != targetCollider
                || existing.TargetId != targetId)
            {
                return false;
            }

            return targetsByInstanceId.Remove(instanceId);
        }

        public void ClearTargets()
        {
            targetsByInstanceId.Clear();
        }

        public void ResetProcessedEvents()
        {
            firstMessagesByEventId.Clear();
        }

        /// <summary>
        /// Translates one already confirmed 2D hit. The first valid event envelope
        /// produces Confirmed or TargetAlreadyDestroyed. An exact callback retry
        /// produces DuplicateEventIgnored; conflicting event-ID reuse fails closed.
        /// </summary>
        public CombatHit2DTranslationResult TranslateConfirmedHit(
            StableId eventId,
            Collider2D targetCollider,
            CombatChannel channel,
            bool targetAlreadyDestroyed)
        {
            if (eventId == null
                || targetCollider == null
                || !Enum.IsDefined(typeof(CombatChannel), channel)
                || channel == CombatChannel.System)
            {
                return Result(CombatHit2DTranslationStatus.InvalidInput, null);
            }

            TargetBinding target;
            if (!targetsByInstanceId.TryGetValue(targetCollider.GetInstanceID(), out target)
                || target.Collider != targetCollider
                || target.TargetId == null)
            {
                return Result(CombatHit2DTranslationStatus.UnknownTarget, null);
            }

            HitResult firstResult = targetAlreadyDestroyed
                ? HitResult.TargetAlreadyDestroyed
                : HitResult.Confirmed;
            HitMessage candidate;
            try
            {
                candidate = new HitMessage(
                    eventId,
                    sourceId,
                    target.TargetId,
                    channel,
                    firstResult);
            }
            catch (Exception)
            {
                return Result(CombatHit2DTranslationStatus.InvalidInput, null);
            }

            HitMessage firstMessage;
            if (firstMessagesByEventId.TryGetValue(eventId, out firstMessage))
            {
                CombatEventIdentityResult identity =
                    CombatEventIdentity.Classify(firstMessage, candidate);
                if (identity == CombatEventIdentityResult.ConflictingDuplicate)
                {
                    return Result(
                        CombatHit2DTranslationStatus.ConflictingDuplicate,
                        null);
                }

                HitMessage duplicate = new HitMessage(
                    eventId,
                    sourceId,
                    target.TargetId,
                    channel,
                    HitResult.DuplicateEventIgnored);
                return Result(CombatHit2DTranslationStatus.DuplicateIgnored, duplicate);
            }

            firstMessagesByEventId.Add(eventId, candidate);
            return Result(
                targetAlreadyDestroyed
                    ? CombatHit2DTranslationStatus.TargetAlreadyDestroyed
                    : CombatHit2DTranslationStatus.Confirmed,
                candidate);
        }

        private static CombatHit2DTranslationResult Result(
            CombatHit2DTranslationStatus status,
            HitMessage message)
        {
            return new CombatHit2DTranslationResult(status, message);
        }
    }
}
