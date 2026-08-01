using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Combat
{
    /// <summary>
    /// Immutable description of one direct hit delivered by a projectile or another scene adapter.
    /// The struck object owns health, terminal state, rewards, drops, and presentation cleanup.
    /// </summary>
    public sealed class Hit
    {
        public Hit(
            StableId eventStableId,
            StableId sourceEntityStableId,
            StableId sourceRunParticipantStableId,
            StableId targetEntityStableId,
            long targetLifecycleGeneration,
            long order,
            int channelValue,
            double amount,
            double occurredAtSeconds)
        {
            EventStableId = eventStableId
                ?? throw new ArgumentNullException(nameof(eventStableId));
            SourceEntityStableId = sourceEntityStableId
                ?? throw new ArgumentNullException(nameof(sourceEntityStableId));
            SourceRunParticipantStableId = sourceRunParticipantStableId
                ?? throw new ArgumentNullException(nameof(sourceRunParticipantStableId));
            TargetEntityStableId = targetEntityStableId
                ?? throw new ArgumentNullException(nameof(targetEntityStableId));
            if (targetLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(targetLifecycleGeneration));
            if (order < 0L) throw new ArgumentOutOfRangeException(nameof(order));
            if (channelValue <= 0) throw new ArgumentOutOfRangeException(nameof(channelValue));
            if (double.IsNaN(amount) || double.IsInfinity(amount) || amount <= 0d)
                throw new ArgumentOutOfRangeException(nameof(amount));
            if (double.IsNaN(occurredAtSeconds)
                || double.IsInfinity(occurredAtSeconds)
                || occurredAtSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(occurredAtSeconds));
            }

            TargetLifecycleGeneration = targetLifecycleGeneration;
            Order = order;
            ChannelValue = channelValue;
            Amount = amount;
            OccurredAtSeconds = occurredAtSeconds;
        }

        public StableId EventStableId { get; }
        public StableId SourceEntityStableId { get; }
        public StableId SourceRunParticipantStableId { get; }
        public StableId TargetEntityStableId { get; }
        public long TargetLifecycleGeneration { get; }
        public long Order { get; }
        public int ChannelValue { get; }
        public GunDamageCategory DamageCategory
        {
            get { return (GunDamageCategory)ChannelValue; }
        }
        public double Amount { get; }
        public double OccurredAtSeconds { get; }
    }

    /// <summary>
    /// Shared Unity collision boundary for enemies, props, and future damageable objects.
    /// Implementations own all health and death behavior. Callers deliver one hit and retain no
    /// target-owned state.
    /// </summary>
    public abstract class Damageable : MonoBehaviour
    {
        public abstract StableId DamageableStableId { get; }
        public abstract long DamageableLifecycleGeneration { get; }
        public abstract bool CanTakeDamage { get; }
        public abstract void TakeHit(Hit hit);
    }

    public static class HitDelivery
    {
        public static void Deliver(Damageable target, Hit hit)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (hit == null) throw new ArgumentNullException(nameof(hit));
            if (target.DamageableStableId == null
                || target.DamageableStableId != hit.TargetEntityStableId
                || target.DamageableLifecycleGeneration
                    != hit.TargetLifecycleGeneration)
            {
                throw new InvalidOperationException(
                    "A direct hit must match the target's current stable identity and lifecycle.");
            }

            target.TakeHit(hit);
        }
    }
}
