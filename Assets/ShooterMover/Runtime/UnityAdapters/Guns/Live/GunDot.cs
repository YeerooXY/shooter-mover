using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Guns.Execution;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    /// <summary>
    /// Target-owned live realization of the canonical damage-over-time resolver.
    /// One exact gun instance shares one stack group on one exact target lifecycle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GunDot : MonoBehaviour
    {
        private sealed class Stack
        {
            public Stack(string key)
            {
                Key = key;
                Accepted = new HashSet<GunEffectApplicationKey>();
            }

            public string Key { get; }
            public HashSet<GunEffectApplicationKey> Accepted { get; }
            public GunEffectSourceContext Source { get; set; }
            public GunDamageCategory Category { get; set; }
            public int Count { get; set; }
            public double DamagePerSecond { get; set; }
            public double TicksPerSecond { get; set; }
            public double ExpiresAt { get; set; }
            public double NextTickAt { get; set; }
            public long BaseOrder { get; set; }
            public long TickNo { get; set; }
        }

        private readonly DamageOverTime resolver = new DamageOverTime();
        private readonly List<Stack> stacks = new List<Stack>();
        private Damageable target;
        private StableId targetId;
        private long targetLife;

        public int StackGroupCount { get { return stacks.Count; } }

        public void Apply(
            ProjectileEffectEmission emission,
            Damageable exactTarget)
        {
            if (emission == null
                || emission.Kind != ProjectileEffectEmissionKind.EnemyImpact
                || emission.Lifecycle == null
                || emission.Damage == null
                || !emission.Damage.HasDamageOverTime
                || emission.Effects == null
                || emission.Effects.DamageOverTime == null)
            {
                throw new ArgumentException(
                    "A direct canonical damage-over-time emission is required.",
                    nameof(emission));
            }
            if (exactTarget == null
                || !exactTarget.CanTakeDamage
                || exactTarget.DamageableStableId == null
                || exactTarget.DamageableLifecycleGeneration <= 0L)
            {
                return;
            }

            Bind(exactTarget);
            GunEffectIdentity identity =
                emission.Lifecycle.Identity.SourceIdentity;
            string key = GroupKey(identity);
            Stack stack = Find(key);
            double now = Time.timeAsDouble;
            if (stack != null && stack.ExpiresAt <= now)
            {
                stacks.Remove(stack);
                stack = null;
            }

            GunTargetReference targetReference = new GunTargetReference(
                new GunActorInstanceId(targetId),
                new LifecycleGeneration(targetLife));
            GunEffectSourceContext source = new GunEffectSourceContext(
                identity,
                emission.EventOrdinal);
            GunDamageOverTimeStateSnapshot current = stack == null
                ? GunDamageOverTimeStateSnapshot.None()
                : new GunDamageOverTimeStateSnapshot(
                    stack.Count,
                    Math.Max(0.0000001d, stack.ExpiresAt - now));
            GunEffectApplicationHistory history = stack == null
                ? GunEffectApplicationHistory.Empty
                : new GunEffectApplicationHistory(stack.Accepted);
            GunDamageOverTimeResolution resolution = resolver.Resolve(
                new GunDamageOverTimeResolutionRequest(
                    source,
                    targetReference,
                    emission.Damage,
                    emission.Effects.DamageOverTime,
                    current,
                    history));
            if (!resolution.EmitsDecision) return;

            GunDamageOverTimeApplicationDecision decision =
                resolution.Decision;
            bool existing = stack != null;
            if (!existing)
            {
                stack = new Stack(key);
                stacks.Add(stack);
            }

            stack.Accepted.Add(resolution.ApplicationKey);
            stack.Source = source;
            stack.Category = decision.DamageCategory;
            stack.Count = decision.ResultingStackCount;
            stack.DamagePerSecond = decision.DamagePerSecondPerStack;
            stack.TicksPerSecond = decision.TicksPerSecond;
            stack.ExpiresAt = now
                + decision.ResultingRemainingDurationSeconds;
            long baseOrder = checked(
                emission.Lifecycle.LaunchSimulationTick * 4096L
                + (emission.EventOrdinal * 64L));
            stack.BaseOrder = Math.Max(stack.BaseOrder, baseOrder);

            double interval = 1d / stack.TicksPerSecond;
            if (!existing || stack.NextTickAt <= 0d)
            {
                stack.NextTickAt = now + interval;
            }
        }

        private void Update()
        {
            if (stacks.Count == 0) return;
            if (!TargetIsCurrent())
            {
                Clear();
                return;
            }

            double now = Time.timeAsDouble;
            for (int index = stacks.Count - 1; index >= 0; index--)
            {
                Stack stack = stacks[index];
                double interval = 1d / stack.TicksPerSecond;
                while (stack.NextTickAt <= now
                    && stack.NextTickAt <= stack.ExpiresAt + 0.0000001d)
                {
                    if (!ApplyTick(stack, stack.NextTickAt))
                    {
                        stacks.RemoveAt(index);
                        stack = null;
                        break;
                    }
                    stack.NextTickAt += interval;
                }

                if (stack != null && now >= stack.ExpiresAt)
                {
                    stacks.RemoveAt(index);
                }
            }
        }

        private bool ApplyTick(Stack stack, double occurredAt)
        {
            if (stack == null
                || stack.Source == null
                || stack.Count < 1
                || stack.TicksPerSecond <= 0d
                || !TargetIsCurrent())
            {
                return false;
            }

            stack.TickNo = checked(stack.TickNo + 1L);
            GunEffectIdentity identity = stack.Source.Identity;
            double amount = stack.DamagePerSecond
                * stack.Count
                / stack.TicksPerSecond;
            StableId eventId = StableId.Create(
                "damage-over-time-operation",
                "canonical-player-gun-"
                + Hash64(
                    stack.Key
                    + "|" + identity.FireOperationId
                    + "|" + identity.ShotSequence.ToString(
                        CultureInfo.InvariantCulture)
                    + "|" + targetId
                    + "|" + targetLife.ToString(
                        CultureInfo.InvariantCulture)
                    + "|" + stack.TickNo.ToString(
                        CultureInfo.InvariantCulture)));
            var hit = new Hit(
                eventId,
                identity.ActorId.Value,
                identity.ParticipantId.Value,
                targetId,
                targetLife,
                checked(stack.BaseOrder + stack.TickNo),
                (int)stack.Category,
                amount,
                occurredAt);

            try
            {
                HitDelivery.Deliver(target, hit);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "gun-dot-tick-failed:" + exception.Message,
                    this);
                return false;
            }
        }

        private void Bind(Damageable exactTarget)
        {
            if (target != null
                && (target.DamageableStableId
                        != exactTarget.DamageableStableId
                    || target.DamageableLifecycleGeneration
                        != exactTarget.DamageableLifecycleGeneration))
            {
                Clear();
            }
            target = exactTarget;
            targetId = exactTarget.DamageableStableId;
            targetLife = exactTarget.DamageableLifecycleGeneration;
        }

        private bool TargetIsCurrent()
        {
            return target != null
                && target.isActiveAndEnabled
                && target.gameObject.activeInHierarchy
                && target.CanTakeDamage
                && target.DamageableStableId == targetId
                && target.DamageableLifecycleGeneration == targetLife;
        }

        private Stack Find(string key)
        {
            for (int index = 0; index < stacks.Count; index++)
            {
                if (string.Equals(
                        stacks[index].Key,
                        key,
                        StringComparison.Ordinal))
                {
                    return stacks[index];
                }
            }
            return null;
        }

        private void Clear()
        {
            stacks.Clear();
            target = null;
            targetId = null;
            targetLife = 0L;
        }

        private static string GroupKey(GunEffectIdentity identity)
        {
            return identity.ActorId + "|"
                + identity.ParticipantId + "|"
                + identity.EquipmentInstanceId + "|"
                + identity.GunDefinitionId;
        }

        private static string Hash64(string value)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            string text = value ?? string.Empty;
            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= prime;
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
