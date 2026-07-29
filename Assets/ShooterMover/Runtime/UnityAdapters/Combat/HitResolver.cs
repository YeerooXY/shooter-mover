using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Combat
{
    public enum HitTargetRegistrationStatus
    {
        Registered = 1,
        AlreadyRegistered = 2,
        InvalidInput = 3,
        Ambiguous = 4,
    }

    public enum HitTranslationStatus
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
    public sealed class HitTranslationResult
    {
        internal HitTranslationResult(
            HitTranslationStatus status,
            HitMessage message)
        {
            Status = status;
            Message = message;
        }

        public HitTranslationStatus Status { get; }

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
    public sealed class HitResolver
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

        public event Action<HitTranslationResult> HitTranslated;

        public HitResolver(StableId sourceId)
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
        public HitTargetRegistrationStatus RegisterTarget(
            Collider2D targetCollider,
            StableId targetId)
        {
            if (targetCollider == null || targetId == null)
            {
                return HitTargetRegistrationStatus.InvalidInput;
            }

            int instanceId = targetCollider.GetInstanceID();
            TargetBinding existing;
            if (targetsByInstanceId.TryGetValue(instanceId, out existing))
            {
                if (existing.Collider == targetCollider && existing.TargetId == targetId)
                {
                    return HitTargetRegistrationStatus.AlreadyRegistered;
                }

                return HitTargetRegistrationStatus.Ambiguous;
            }

            targetsByInstanceId.Add(instanceId, new TargetBinding(targetCollider, targetId));
            return HitTargetRegistrationStatus.Registered;
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
        public HitTranslationResult TranslateConfirmedHit(
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
                return Result(HitTranslationStatus.InvalidInput, null);
            }

            TargetBinding target;
            if (!targetsByInstanceId.TryGetValue(targetCollider.GetInstanceID(), out target)
                || target.Collider != targetCollider
                || target.TargetId == null)
            {
                return Result(HitTranslationStatus.UnknownTarget, null);
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
                return Result(HitTranslationStatus.InvalidInput, null);
            }

            HitMessage firstMessage;
            if (firstMessagesByEventId.TryGetValue(eventId, out firstMessage))
            {
                CombatEventIdentityResult identity =
                    CombatEventIdentity.Classify(firstMessage, candidate);
                if (identity == CombatEventIdentityResult.ConflictingDuplicate)
                {
                    return Result(
                        HitTranslationStatus.ConflictingDuplicate,
                        null);
                }

                HitMessage duplicate = new HitMessage(
                    eventId,
                    sourceId,
                    target.TargetId,
                    channel,
                    HitResult.DuplicateEventIgnored);
                return Result(HitTranslationStatus.DuplicateIgnored, duplicate);
            }

            firstMessagesByEventId.Add(eventId, candidate);
            HitTranslationResult result = Result(
                targetAlreadyDestroyed
                    ? HitTranslationStatus.TargetAlreadyDestroyed
                    : HitTranslationStatus.Confirmed,
                candidate);
            Action<HitTranslationResult> translated = HitTranslated;
            if (translated != null)
            {
                translated(result);
            }

            return result;
        }

        private static HitTranslationResult Result(
            HitTranslationStatus status,
            HitMessage message)
        {
            return new HitTranslationResult(status, message);
        }
    }
}
