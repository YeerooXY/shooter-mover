using System;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Missions.Rooms
{
    /// <summary>
    /// Binds the room-owned enemy presentation object to one factory-created runtime.
    /// Health, damage, lifecycle, and death remain authoritative in that runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoomEnemyActor2D : MonoBehaviour
    {
        private EnemyPlacementRuntimeInstanceV1 runtime;
        private RoomOccupantTerminalRelay2D legacyRelay;
        private bool legacyRelayEnabled;
        private bool legacyRelayStateCaptured;

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

        public EnemyPlacementRuntimeInstanceV1 Runtime
        {
            get { return runtime; }
        }

        public bool IsAlive
        {
            get { return runtime != null && runtime.ActorState.IsActive; }
        }

        internal void Bind(EnemyPlacementRuntimeInstanceV1 value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (runtime != null)
            {
                throw new InvalidOperationException(
                    "A room enemy actor may only bind once per room presentation.");
            }

            legacyRelay = GetComponent<RoomOccupantTerminalRelay2D>();
            if (legacyRelay != null)
            {
                legacyRelayEnabled = legacyRelay.enabled;
                legacyRelayStateCaptured = true;
                legacyRelay.enabled = false;
            }

            runtime = value;
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

        public EnemyRuntimeDamageResultV1 ApplyDamage(
            EnemyRuntimeDamageCommandV1 command,
            double occurredAtSeconds)
        {
            if (runtime == null)
            {
                throw new InvalidOperationException(
                    "The room enemy actor is not bound to a live enemy runtime.");
            }

            return runtime.ApplyDamage(command, occurredAtSeconds);
        }

        internal void SetTerminal(EnemyTerminalCollisionFactV1 fact)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));
            if (runtime == null
                || fact.EntityInstanceStableId != runtime.SpawnStableId
                || fact.LifecycleGeneration != runtime.LifecycleGeneration)
            {
                throw new InvalidOperationException(
                    "Enemy terminal collision facts must match the bound actor lifecycle.");
            }

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider2D collider = colliders[index];
                if (collider != null && collider.enabled)
                {
                    collider.enabled = false;
                }
            }

            Rigidbody2D[] bodies = GetComponentsInChildren<Rigidbody2D>(true);
            for (int index = 0; index < bodies.Length; index++)
            {
                Rigidbody2D body = bodies[index];
                if (body == null) continue;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
            }
        }
    }
}
