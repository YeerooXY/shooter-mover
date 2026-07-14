using System;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    public sealed class EnemyTarget2DObservation
    {
        public EnemyTarget2DObservation(StableId targetId, double positionX, double positionY)
        {
            if (targetId == null)
            {
                throw new ArgumentNullException(nameof(targetId));
            }

            RequireFinite(positionX, nameof(positionX));
            RequireFinite(positionY, nameof(positionY));

            TargetId = targetId;
            PositionX = positionX;
            PositionY = positionY;
        }

        public StableId TargetId { get; }

        public double PositionX { get; }

        public double PositionY { get; }

        private static void RequireFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    "A 2D target position must be finite.");
            }
        }
    }

    public interface IEnemyTarget2DSource
    {
        bool TryReadTarget(out EnemyTarget2DObservation target);
    }

    public enum EnemyTarget2DHitStatus
    {
        Applied = 1,
        DuplicateIgnored = 2,
        TargetAlreadyDestroyed = 3,
        Disabled = 4,
        InvalidInput = 5,
        TargetMismatch = 6,
        TargetUnavailable = 7,
        AuthorityUnavailable = 8,
        NotDamageTarget = 9,
        IgnoredHitResult = 10,
    }

    public sealed class EnemyTarget2DHitApplication
    {
        internal EnemyTarget2DHitApplication(
            EnemyTarget2DHitStatus status,
            HitMessage message,
            EnemyActorStepResult domainResult,
            EnemyDamageNotification damageNotification)
        {
            Status = status;
            Message = message;
            DomainResult = domainResult;
            DamageNotification = damageNotification;
        }

        public EnemyTarget2DHitStatus Status { get; }

        public HitMessage Message { get; }

        public EnemyActorStepResult DomainResult { get; }

        public EnemyDamageNotification DamageNotification { get; }

        public bool Applied
        {
            get { return Status == EnemyTarget2DHitStatus.Applied; }
        }
    }

    /// <summary>
    /// Explicit Collider2D/identity projection used both as a deterministic target
    /// source and as the EN-002 damage intake. It never stores health or lifecycle
    /// truth; confirmed CB-009 messages are submitted to the configured authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyTarget2DAdapter : MonoBehaviour, IEnemyTarget2DSource
    {
        private StableId targetId;
        private Transform targetTransform;
        private Collider2D targetCollider;
        private IEnemyActor2DAuthority authority;
        private bool configured;

        public bool IsConfigured
        {
            get { return configured; }
        }

        public StableId TargetId
        {
            get { return targetId; }
        }

        public Collider2D TargetCollider
        {
            get { return targetCollider; }
        }

        public bool CanReceiveEnemyDamage
        {
            get { return authority != null; }
        }

        public void Configure(
            StableId stableTargetId,
            Transform observedTransform,
            Collider2D observedCollider,
            IEnemyActor2DAuthority targetAuthority = null)
        {
            if (stableTargetId == null)
            {
                throw new ArgumentNullException(nameof(stableTargetId));
            }

            if (observedTransform == null)
            {
                throw new ArgumentNullException(nameof(observedTransform));
            }

            if (observedCollider == null)
            {
                throw new ArgumentNullException(nameof(observedCollider));
            }

            if (!object.ReferenceEquals(observedTransform.gameObject, observedCollider.gameObject)
                || !object.ReferenceEquals(observedCollider.gameObject, gameObject))
            {
                throw new ArgumentException(
                    "The target transform, Collider2D, and adapter must belong to one explicit GameObject.");
            }

            if (configured)
            {
                if (targetId == stableTargetId
                    && object.ReferenceEquals(targetTransform, observedTransform)
                    && object.ReferenceEquals(targetCollider, observedCollider)
                    && object.ReferenceEquals(authority, targetAuthority))
                {
                    return;
                }

                throw new InvalidOperationException(
                    "EnemyTarget2DAdapter is already configured with different dependencies.");
            }

            if (targetAuthority != null)
            {
                EnemyActorState state;
                if (!targetAuthority.TryReadState(out state)
                    || state == null
                    || state.ActorId != stableTargetId)
                {
                    throw new ArgumentException(
                        "The supplied enemy authority must expose the same stable target identity.",
                        nameof(targetAuthority));
                }
            }

            targetId = stableTargetId;
            targetTransform = observedTransform;
            targetCollider = observedCollider;
            authority = targetAuthority;
            configured = true;
        }

        public bool TryReadTarget(out EnemyTarget2DObservation target)
        {
            target = null;
            if (this == null
                || !configured
                || !isActiveAndEnabled
                || targetTransform == null
                || targetCollider == null
                || !targetCollider.enabled
                || !targetCollider.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (authority != null)
            {
                EnemyActorState state;
                try
                {
                    if (!authority.TryReadState(out state)
                        || state == null
                        || state.ActorId != targetId
                        || !state.IsActive)
                    {
                        return false;
                    }
                }
                catch (ArgumentException)
                {
                    return false;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }

            target = new EnemyTarget2DObservation(
                targetId,
                targetTransform.position.x,
                targetTransform.position.y);
            return true;
        }

        public CombatHit2DTargetRegistrationStatus RegisterForCombatHits(
            CombatHit2DAdapter hitAdapter)
        {
            if (hitAdapter == null || !configured || targetCollider == null)
            {
                return CombatHit2DTargetRegistrationStatus.InvalidInput;
            }

            return hitAdapter.RegisterTarget(targetCollider, targetId);
        }

        public bool UnregisterFromCombatHits(CombatHit2DAdapter hitAdapter)
        {
            return hitAdapter != null
                && configured
                && targetCollider != null
                && hitAdapter.UnregisterTarget(targetCollider, targetId);
        }

        public EnemyTarget2DHitApplication ApplyHit(
            HitMessage message,
            double damageAmount,
            long order)
        {
            if (message == null
                || double.IsNaN(damageAmount)
                || double.IsInfinity(damageAmount)
                || damageAmount <= 0d
                || order < 0L
                || message.Channel == CombatChannel.System)
            {
                return Result(EnemyTarget2DHitStatus.InvalidInput, message, null, null);
            }

            if (!configured || authority == null)
            {
                return Result(EnemyTarget2DHitStatus.NotDamageTarget, message, null, null);
            }

            if (!isActiveAndEnabled)
            {
                return Result(EnemyTarget2DHitStatus.Disabled, message, null, null);
            }

            if (message.TargetId != targetId)
            {
                return Result(EnemyTarget2DHitStatus.TargetMismatch, message, null, null);
            }

            if (message.Result == HitResult.DuplicateEventIgnored)
            {
                return Result(EnemyTarget2DHitStatus.DuplicateIgnored, message, null, null);
            }

            if (message.Result == HitResult.TargetAlreadyDestroyed)
            {
                return Result(
                    EnemyTarget2DHitStatus.TargetAlreadyDestroyed,
                    message,
                    null,
                    null);
            }

            if (message.Result != HitResult.Confirmed)
            {
                return Result(EnemyTarget2DHitStatus.IgnoredHitResult, message, null, null);
            }

            if (targetTransform == null
                || targetCollider == null
                || !targetCollider.enabled
                || !targetCollider.gameObject.activeInHierarchy)
            {
                return Result(EnemyTarget2DHitStatus.TargetUnavailable, message, null, null);
            }

            EnemyActorState state;
            try
            {
                if (!authority.TryReadState(out state)
                    || state == null
                    || state.ActorId != targetId)
                {
                    return Result(
                        EnemyTarget2DHitStatus.AuthorityUnavailable,
                        message,
                        null,
                        null);
                }
            }
            catch (ArgumentException)
            {
                return Result(EnemyTarget2DHitStatus.AuthorityUnavailable, message, null, null);
            }
            catch (InvalidOperationException)
            {
                return Result(EnemyTarget2DHitStatus.AuthorityUnavailable, message, null, null);
            }

            if (state.IsDestroyed)
            {
                return Result(
                    EnemyTarget2DHitStatus.TargetAlreadyDestroyed,
                    message,
                    null,
                    null);
            }

            if (state.HasProcessed(message.EventId))
            {
                return Result(EnemyTarget2DHitStatus.DuplicateIgnored, message, null, null);
            }

            EnemyActorStepResult applied;
            try
            {
                applied = authority.Apply(
                    EnemyActorCommand.Damage(
                        order,
                        message.EventId,
                        message.SourceId,
                        (int)message.Channel,
                        damageAmount));
            }
            catch (ArgumentException)
            {
                return Result(EnemyTarget2DHitStatus.InvalidInput, message, null, null);
            }
            catch (InvalidOperationException)
            {
                return Result(EnemyTarget2DHitStatus.AuthorityUnavailable, message, null, null);
            }

            if (applied == null || applied.State == null)
            {
                return Result(EnemyTarget2DHitStatus.AuthorityUnavailable, message, null, null);
            }

            EnemyDamageNotification damage = FindDamageNotification(
                applied,
                message.EventId);
            if (damage == null)
            {
                return Result(EnemyTarget2DHitStatus.AuthorityUnavailable, message, applied, null);
            }

            if (damage.ResultValue == EnemyActorStepper.DamageDuplicateEventIgnoredResultValue)
            {
                return Result(EnemyTarget2DHitStatus.DuplicateIgnored, message, applied, damage);
            }

            if (damage.ResultValue == EnemyActorStepper.DamageTargetAlreadyDestroyedResultValue)
            {
                return Result(
                    EnemyTarget2DHitStatus.TargetAlreadyDestroyed,
                    message,
                    applied,
                    damage);
            }

            return Result(EnemyTarget2DHitStatus.Applied, message, applied, damage);
        }

        private static EnemyDamageNotification FindDamageNotification(
            EnemyActorStepResult result,
            StableId eventId)
        {
            for (int index = 0; index < result.Notifications.Count; index++)
            {
                EnemyDamageNotification notification =
                    result.Notifications[index] as EnemyDamageNotification;
                if (notification != null && notification.EventId == eventId)
                {
                    return notification;
                }
            }

            return null;
        }

        private static EnemyTarget2DHitApplication Result(
            EnemyTarget2DHitStatus status,
            HitMessage message,
            EnemyActorStepResult domainResult,
            EnemyDamageNotification notification)
        {
            return new EnemyTarget2DHitApplication(
                status,
                message,
                domainResult,
                notification);
        }

        private void OnDestroy()
        {
            configured = false;
            targetId = null;
            targetTransform = null;
            targetCollider = null;
            authority = null;
        }
    }
}
