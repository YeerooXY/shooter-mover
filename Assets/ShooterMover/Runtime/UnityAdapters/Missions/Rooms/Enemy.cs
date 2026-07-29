using System;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Missions.Rooms
{
    /// <summary>
    /// Connects one room enemy GameObject to its enemy gameplay state.
    /// The enemy owns health, death, rewards, drops, and room-clear reporting.
    /// Projectiles only deliver a hit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Enemy : Damageable
    {
        private EnemyInstance runtime;
        private RoomEnemyDeathRelay legacyRelay;
        private bool legacyRelayEnabled;
        private bool legacyRelayStateCaptured;
        private bool terminalPresentationDisabled;

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
            get { return runtime == null ? 0 : runtime.Level; }
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
            terminalPresentationDisabled = false;
            if (reactivateAfterDeath && !gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void Unbind()
        {
            runtime = null;
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
                        hit.Amount),
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

            DisableTerminalPresentation();
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
