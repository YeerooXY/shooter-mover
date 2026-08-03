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
    public sealed class VolatileBlast
    {
        public VolatileBlast(
            StableId eventId,
            StableId enemyId,
            StableId runParticipantId,
            long generation,
            Vector2 position,
            double radius,
            double damage)
        {
            EventId = eventId ?? throw new ArgumentNullException(nameof(eventId));
            EnemyId = enemyId ?? throw new ArgumentNullException(nameof(enemyId));
            RunParticipantId = runParticipantId
                ?? throw new ArgumentNullException(nameof(runParticipantId));
            if (generation <= 0L)
                throw new ArgumentOutOfRangeException(nameof(generation));
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

            Generation = generation;
            Position = position;
            Radius = radius;
            Damage = damage;
        }

        public StableId EventId { get; }
        public StableId EnemyId { get; }
        public StableId RunParticipantId { get; }
        public long Generation { get; }
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
        private static readonly TraitRules Rules = TraitRules.Default;

        private readonly Dictionary<GunDamageCategory, double> resistances =
            new Dictionary<GunDamageCategory, double>();
        private EnemyInstance runtime;
        private bool terminalPresentationDisabled;
        private bool volatileBlastEmitted;

        public static event Action<Enemy, VolatileBlast> VolatileExploded;
        public static event Action<Enemy> Bound;
        public static event Action<Enemy> TraitsChanged;
        public static event Action<Enemy> Unbound;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeEvents()
        {
            VolatileExploded = null;
            Bound = null;
            TraitsChanged = null;
            Unbound = null;
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
            Action<Enemy> handler = TraitsChanged;
            if (handler != null) handler(this);
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

            TraitRoller.Roll(value, Rules);

            bool reactivateAfterDeath = terminalPresentationDisabled;
            runtime = value;
            resistances.Clear();
            volatileBlastEmitted = false;
            ApplyTraits();
            terminalPresentationDisabled = false;
            if (reactivateAfterDeath && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            Action<Enemy> handler = Bound;
            if (handler != null) handler(this);
        }

        public void Unbind()
        {
            Action<Enemy> handler = Unbound;
            if (handler != null) handler(this);
            runtime = null;
            resistances.Clear();
            volatileBlastEmitted = false;
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
                EmitVolatileBlast();
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

        private void EmitVolatileBlast()
        {
            if (volatileBlastEmitted
                || runtime == null
                || !runtime.HasTrait(EnemyTrait.Volatile))
            {
                return;
            }
            if (runtime.PublishedDeath == null)
            {
                throw new InvalidOperationException(
                    "A volatile enemy requires a confirmed death.");
            }

            volatileBlastEmitted = true;
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
            var blast = new VolatileBlast(
                eventId,
                runtime.SpawnStableId,
                runtime.RunParticipantStableId,
                runtime.LifecycleGeneration,
                position,
                EnemyInstance.VolatileRadius,
                EnemyInstance.VolatileDamage);
            Action<Enemy, VolatileBlast> handler = VolatileExploded;
            if (handler != null)
            {
                handler(this, blast);
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
