using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Missions.Rooms
{
    public sealed class EnemyVolatileExplosion
    {
        public EnemyVolatileExplosion(
            StableId eventStableId,
            StableId sourceEntityStableId,
            StableId sourceRunParticipantStableId,
            long sourceLifecycleGeneration,
            Vector2 position,
            double radius,
            double damage)
        {
            EventStableId = eventStableId
                ?? throw new ArgumentNullException(nameof(eventStableId));
            SourceEntityStableId = sourceEntityStableId
                ?? throw new ArgumentNullException(nameof(sourceEntityStableId));
            SourceRunParticipantStableId = sourceRunParticipantStableId
                ?? throw new ArgumentNullException(nameof(sourceRunParticipantStableId));
            if (sourceLifecycleGeneration <= 0L)
                throw new ArgumentOutOfRangeException(nameof(sourceLifecycleGeneration));
            if (float.IsNaN(position.x)
                || float.IsInfinity(position.x)
                || float.IsNaN(position.y)
                || float.IsInfinity(position.y))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }
            if (double.IsNaN(radius) || double.IsInfinity(radius) || radius <= 0d)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (double.IsNaN(damage) || double.IsInfinity(damage) || damage <= 0d)
                throw new ArgumentOutOfRangeException(nameof(damage));

            SourceLifecycleGeneration = sourceLifecycleGeneration;
            Position = position;
            Radius = radius;
            Damage = damage;
        }

        public StableId EventStableId { get; }
        public StableId SourceEntityStableId { get; }
        public StableId SourceRunParticipantStableId { get; }
        public long SourceLifecycleGeneration { get; }
        public Vector2 Position { get; }
        public double Radius { get; }
        public double Damage { get; }
    }

    /// <summary>
    /// Connects one room enemy GameObject to its enemy gameplay state.
    /// The enemy owns health, death, rewards, drops, and room-clear reporting.
    /// Projectiles only deliver a hit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Enemy : Damageable
    {
        private const double MinResistance = -1d;
        private const double MaxResistance = 0.95d;
        private readonly Dictionary<GunDamageCategory, double> resistances =
            new Dictionary<GunDamageCategory, double>();
        private EnemyInstance runtime;
        private RoomEnemyDeathRelay legacyRelay;
        private bool legacyRelayEnabled;
        private bool legacyRelayStateCaptured;
        private bool terminalPresentationDisabled;
        private bool volatileExplosionEmitted;

        public static event Action<Enemy, EnemyVolatileExplosion> VolatileExploded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeEvents()
        {
            VolatileExploded = null;
        }

        public bool IsBound
        {
            get { return runtime != null; }
        }

        public StableId PlacementStableId
        {
            get { return runtime == null ? null : runtime.PlacementStableId; }
        }

        public StableId ActorStableId
        {
            get { return runtime == null ? null : runtime.SpawnStableId; }
        }

        public int Level
        {
            get { return runtime == null ? 0 : runtime.Tier; }
        }

        public long LifecycleGeneration
        {
            get { return runtime == null ? 0L : runtime.LifecycleGeneration; }
        }

        public EnemyInstance Runtime
        {
            get { return runtime; }
        }

        public bool IsAlive
        {
            get { return runtime != null && runtime.ActorState.IsActive; }
        }

        public bool IsTerminalPresentationDisabled
        {
            get { return terminalPresentationDisabled; }
        }

        public override StableId DamageableStableId
        {
            get { return ActorStableId; }
        }

        public override long DamageableLifecycleGeneration
        {
            get { return LifecycleGeneration; }
        }

        public override bool CanTakeDamage
        {
            get { return IsBound && IsAlive; }
        }

        public double Resistance(GunDamageCategory category)
        {
            double value;
            if (!resistances.TryGetValue(category, out value)) return 0d;
            return Math.Max(MinResistance, Math.Min(MaxResistance, value));
        }

        public void AddResistance(GunDamageCategory category, double amount)
        {
            if (!Enum.IsDefined(typeof(GunDamageCategory), category)
                || double.IsNaN(amount)
                || double.IsInfinity(amount))
            {
                throw new ArgumentOutOfRangeException();
            }

            double current;
            resistances.TryGetValue(category, out current);
            resistances[category] = current + amount;
        }

        public bool AssignTrait(EnemyTrait trait)
        {
            if (runtime == null)
            {
                throw new InvalidOperationException(
                    "The room enemy actor is not bound to a live enemy runtime.");
            }
            if (!runtime.AssignTrait(trait)) return false;
            ApplyTrait(trait);
            return true;
        }

        internal void Bind(EnemyInstance value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (runtime != null)
            {
                throw new InvalidOperationException(
                    "A room enemy actor may only bind once per room presentation.");
            }

            bool reactivateAfterDeath = terminalPresentationDisabled;
            legacyRelay = GetComponent<RoomEnemyDeathRelay>();
            if (legacyRelay != null)
            {
                legacyRelayEnabled = legacyRelay.enabled;
                legacyRelayStateCaptured = true;
                legacyRelay.enabled = false;
            }

            runtime = value;
            resistances.Clear();
            volatileExplosionEmitted = false;
            ApplyTraits();
            terminalPresentationDisabled = false;
            if (reactivateAfterDeath && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void Unbind()
        {
            runtime = null;
            resistances.Clear();
            volatileExplosionEmitted = false;
            if (legacyRelayStateCaptured && legacyRelay != null)
            {
                legacyRelay.enabled = legacyRelayEnabled;
            }

            legacyRelay = null;
            legacyRelayEnabled = false;
            legacyRelayStateCaptured = false;
        }

        public override void TakeHit(Hit hit)
        {
            if (hit == null) throw new ArgumentNullException(nameof(hit));
            if (runtime == null)
            {
                throw new InvalidOperationException(
                    "The room enemy actor is not bound to a live enemy runtime.");
            }
            if (hit.TargetEntityStableId != runtime.SpawnStableId
                || hit.TargetLifecycleGeneration != runtime.LifecycleGeneration)
            {
                throw new InvalidOperationException(
                    "The direct hit does not match the bound enemy lifecycle.");
            }

            EnemyLiveDamageResult result;
            try
            {
                result = runtime.ApplyDamage(
                    new EnemyLiveDamageCommand(
                        hit.EventStableId,
                        hit.SourceEntityStableId,
                        hit.SourceRunParticipantStableId,
                        runtime.SpawnStableId,
                        runtime.LifecycleGeneration,
                        hit.Order,
                        hit.ChannelValue,
                        hit.Amount * (1d - Resistance(hit.DamageCategory))),
                    hit.OccurredAtSeconds);
            }
            finally
            {
                if (runtime != null && !runtime.ActorState.IsActive)
                {
                    DisableTerminalPresentation();
                }
            }

            if (result == null)
            {
                throw new InvalidOperationException(
                    "The enemy damage authority returned no result.");
            }
            if (result.Status == EnemyLiveOperationStatus.Rejected)
            {
                throw new InvalidOperationException(
                    "The enemy rejected a direct hit: " + result.Rejection + ".");
            }
            if (runtime.TerminalConsequenceFailureCount > 0)
            {
                Debug.LogError(
                    "enemy-death-action-failed:"
                    + runtime.LastTerminalConsequenceFailure,
                    this);
            }
        }

        public EnemyLiveDamageResult ApplyDamage(
            EnemyLiveDamageCommand command,
            double occurredAtSeconds)
        {
            if (runtime == null)
            {
                throw new InvalidOperationException(
                    "The room enemy actor is not bound to a live enemy runtime.");
            }

            return runtime.ApplyDamage(command, occurredAtSeconds);
        }

        internal void SetTerminal(EnemyTerminalCollisionFact fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            if (runtime == null
                || fact.EntityInstanceStableId != runtime.SpawnStableId
                || fact.LifecycleGeneration != runtime.LifecycleGeneration)
            {
                throw new InvalidOperationException(
                    "Enemy terminal collision facts must match the bound actor lifecycle.");
            }

            try
            {
                EmitVolatileExplosion();
            }
            finally
            {
                DisableTerminalPresentation();
            }
        }

        private void ApplyTraits()
        {
            if (runtime.HasTrait(EnemyTrait.EnergyShielded))
            {
                ApplyTrait(EnemyTrait.EnergyShielded);
            }
        }

        private void ApplyTrait(EnemyTrait trait)
        {
            if (trait == EnemyTrait.EnergyShielded)
            {
                AddResistance(GunDamageCategory.Energy, 0.2d);
            }
        }

        private void EmitVolatileExplosion()
        {
            if (volatileExplosionEmitted
                || runtime == null
                || !runtime.HasTrait(EnemyTrait.Volatile))
            {
                return;
            }
            if (runtime.PublishedDeath == null)
            {
                throw new InvalidOperationException(
                    "A volatile enemy requires its canonical death fact.");
            }

            volatileExplosionEmitted = true;
            Vector2 position = transform.position;
            StableId eventId = StableId.Create(
                "enemy-volatile-explosion",
                DeterministicEnemyLiveIdentityDeriver.Hash64(
                    runtime.SpawnStableId
                    + "|"
                    + runtime.LifecycleGeneration.ToString(
                        CultureInfo.InvariantCulture)
                    + "|"
                    + runtime.PublishedDeath.DeathEventStableId));
            var explosion = new EnemyVolatileExplosion(
                eventId,
                runtime.SpawnStableId,
                runtime.RunParticipantStableId,
                runtime.LifecycleGeneration,
                position,
                EnemyInstance.VolatileRadius,
                EnemyInstance.VolatileDamage);
            Action<Enemy, EnemyVolatileExplosion> handler = VolatileExploded;
            if (handler != null)
            {
                handler(this, explosion);
            }
        }

        private void DisableTerminalPresentation()
        {
            if (terminalPresentationDisabled) return;
            terminalPresentationDisabled = true;

            // An inactive GameObject receives no Update, FixedUpdate, rendering, or physics work.
            // The room spawner can still retain the object reference and reactivate it on the next bind.
            gameObject.SetActive(false);
        }
    }
}
