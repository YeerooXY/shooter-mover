using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.GameplayEntities.Enemies;
using ShooterMover.UnityAdapters.Missions.Rooms;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies.Presentation
{
    /// <summary>
    /// Read-only projection of accepted canonical enemy attack sequences. It mirrors facing,
    /// movement intent, wind-up and emission timing through IEnemyViewCommands;
    /// it never schedules attacks, emits projectiles, applies damage or owns replay admission.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class AttackView : MonoBehaviour
    {
        private readonly List<PendingPulse> pending = new List<PendingPulse>();
        private readonly HashSet<StableId> acceptedEmissionIds = new HashSet<StableId>();

        private Enemy actor;
        private MonoBehaviour sinkBehaviour;
        private IEnemyViewCommands sink;
        private Rigidbody2D body;
        private long revision;
        private bool bound;

        public bool IsBound
        {
            get
            {
                return bound
                    && actor != null
                    && actor.IsBound
                    && actor.LifecycleGeneration == revision
                    && sinkBehaviour != null
                    && sinkBehaviour.isActiveAndEnabled
                    && sink != null;
            }
        }

        public void Bind(Enemy configuredActor, long lifecycleRevision)
        {
            if (configuredActor == null)
                throw new ArgumentNullException(nameof(configuredActor));
            if (lifecycleRevision <= 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleRevision));
            if (!configuredActor.IsBound
                || configuredActor.Runtime == null
                || configuredActor.LifecycleGeneration != lifecycleRevision)
            {
                throw new InvalidOperationException(
                    "enemy-attack-presentation-requires-current-actor");
            }

            MonoBehaviour resolvedBehaviour;
            IEnemyViewCommands resolvedSink;
            ResolveExactlyOneSink(
                configuredActor.gameObject,
                out resolvedBehaviour,
                out resolvedSink);
            if (resolvedSink.ShotOrigin == null)
            {
                throw new InvalidOperationException(
                    "enemy-attack-presentation-shot-origin-missing");
            }

            Clear();
            actor = configuredActor;
            sinkBehaviour = resolvedBehaviour;
            sink = resolvedSink;
            body = configuredActor.GetComponent<Rigidbody2D>();
            revision = lifecycleRevision;
            bound = true;
            sink.SetFacing(configuredActor.transform.right);
            enabled = true;
        }

        public bool TryCreatePlan(
            EnemyAttackSequenceDispatch sequence,
            out EnemyAttackPresentationPlan plan,
            out string diagnostic)
        {
            plan = null;
            diagnostic = string.Empty;
            if (!IsBound)
            {
                diagnostic = "enemy-attack-presentation-binding-stale";
                return false;
            }
            if (sequence == null || sequence.Emissions == null || sequence.Emissions.Count == 0)
            {
                diagnostic = "enemy-attack-presentation-sequence-invalid";
                return false;
            }
            if (sequence.Execution.Identity.EntityInstanceId != actor.ActorStableId
                || sequence.Execution.LifecycleGeneration != revision)
            {
                diagnostic = "enemy-attack-presentation-source-mismatch";
                return false;
            }

            var pulses = new List<EnemyAttackPresentationPulse>(
                sequence.Emissions.Count);
            var incomingIds = new HashSet<StableId>();
            Vector2 committedFacing = Vector2.zero;
            float firstDelay = float.MaxValue;
            for (int index = 0; index < sequence.Emissions.Count; index++)
            {
                EnemyAttackEffectEmission emission = sequence.Emissions[index];
                if (emission == null
                    || emission.EmissionStableId == null
                    || emission.CommittedIntent == null
                    || emission.SourceEntityStableId != actor.ActorStableId
                    || emission.SourceLifecycleGeneration != revision
                    || acceptedEmissionIds.Contains(emission.EmissionStableId)
                    || !incomingIds.Add(emission.EmissionStableId))
                {
                    diagnostic = "enemy-attack-presentation-emission-invalid";
                    return false;
                }

                Vector2 direction = ToUnity(
                    emission.CommittedIntent.CommittedDirection);
                if (direction.sqrMagnitude <= 0.000001f)
                {
                    diagnostic = "enemy-attack-presentation-direction-zero";
                    return false;
                }
                direction.Normalize();
                if (index == 0) committedFacing = direction;

                double rawDelay = emission.ScheduledAtSeconds
                    - sequence.Sequence.StartedAtSeconds;
                if (double.IsNaN(rawDelay)
                    || double.IsInfinity(rawDelay)
                    || rawDelay < 0d
                    || rawDelay > float.MaxValue)
                {
                    diagnostic = "enemy-attack-presentation-time-invalid";
                    return false;
                }
                float delay = (float)rawDelay;
                firstDelay = Mathf.Min(firstDelay, delay);
                pulses.Add(new EnemyAttackPresentationPulse(
                    emission.EmissionStableId,
                    delay));
            }

            pulses.Sort(EnemyAttackPresentationPulse.Compare);
            plan = new EnemyAttackPresentationPlan(
                sequence.DispatchStableId,
                committedFacing,
                firstDelay,
                pulses);
            return true;
        }

        public void Apply(EnemyAttackPresentationPlan plan)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (!IsBound)
            {
                throw new InvalidOperationException(
                    "enemy-attack-presentation-binding-stale");
            }

            sink.SetFacing(plan.CommittedFacing);
            if (plan.FirstDelaySeconds > 0f)
            {
                sink.BeginAttackWindUp(plan.FirstDelaySeconds);
            }

            for (int index = 0; index < plan.Pulses.Count; index++)
            {
                EnemyAttackPresentationPulse pulse = plan.Pulses[index];
                if (!acceptedEmissionIds.Add(pulse.EmissionStableId))
                {
                    throw new InvalidOperationException(
                        "enemy-attack-presentation-emission-duplicate");
                }
                pending.Add(new PendingPulse(
                    pulse.EmissionStableId,
                    pulse.DelaySeconds));
            }
            pending.Sort(PendingPulse.Compare);
        }

        public void Rollback(EnemyAttackPresentationPlan plan)
        {
            if (plan == null) return;
            var ids = new HashSet<StableId>();
            for (int index = 0; index < plan.Pulses.Count; index++)
            {
                StableId id = plan.Pulses[index].EmissionStableId;
                ids.Add(id);
                acceptedEmissionIds.Remove(id);
            }
            for (int index = pending.Count - 1; index >= 0; index--)
            {
                if (ids.Contains(pending[index].EmissionStableId))
                {
                    pending.RemoveAt(index);
                }
            }
        }

        public void Cancel(EnemyAttackSequenceCancellationFact cancellation)
        {
            if (cancellation == null) throw new ArgumentNullException(nameof(cancellation));
            if (!bound
                || actor == null
                || cancellation.SourceEntityStableId != actor.ActorStableId
                || cancellation.SourceLifecycleGeneration != revision)
            {
                return;
            }

            var ids = new HashSet<StableId>(
                cancellation.CancelledProjectileStableIds);
            for (int index = pending.Count - 1; index >= 0; index--)
            {
                if (ids.Contains(pending[index].EmissionStableId))
                {
                    pending.RemoveAt(index);
                }
            }
            foreach (StableId id in ids)
            {
                acceptedEmissionIds.Remove(id);
            }
        }

        private void FixedUpdate()
        {
            if (!IsBound || !actor.IsAlive)
            {
                Clear();
                enabled = false;
                return;
            }

            // Mobile bodies project their authoritative transform rotation continuously. An
            // authored Static body cannot rotate, so its last committed canonical attack-facing
            // must remain on the visual aiming root instead of being overwritten every tick.
            if (body == null || body.bodyType != RigidbodyType2D.Static)
            {
                sink.SetFacing(actor.transform.right);
            }
            sink.SetMovementIntent(body == null ? Vector2.zero : body.linearVelocity);

            float delta = Time.fixedDeltaTime;
            for (int index = 0; index < pending.Count; index++)
            {
                pending[index].RemainingSeconds = Mathf.Max(
                    0f,
                    pending[index].RemainingSeconds - delta);
            }
            pending.Sort(PendingPulse.Compare);
            while (pending.Count > 0 && pending[0].RemainingSeconds <= 0f)
            {
                pending.RemoveAt(0);
                sink.SignalAttackOrigin();
            }
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void Clear()
        {
            pending.Clear();
            acceptedEmissionIds.Clear();
            actor = null;
            sinkBehaviour = null;
            sink = null;
            body = null;
            revision = 0L;
            bound = false;
        }

        private static void ResolveExactlyOneSink(
            GameObject root,
            out MonoBehaviour sinkComponent,
            out IEnemyViewCommands sink)
        {
            sinkComponent = null;
            sink = null;
            int count = 0;
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<
                MonoBehaviour>(true);
            for (int index = 0; index < behaviours.Length; index++)
            {
                MonoBehaviour behaviour = behaviours[index];
                IEnemyViewCommands candidate =
                    behaviour as IEnemyViewCommands;
                if (behaviour == null || candidate == null) continue;
                count++;
                sinkComponent = behaviour;
                sink = candidate;
            }

            if (count == 0)
            {
                throw new InvalidOperationException(
                    "enemy-attack-presentation-sink-missing");
            }
            if (count > 1)
            {
                throw new InvalidOperationException(
                    "enemy-attack-presentation-sink-ambiguous");
            }
        }

        private static Vector2 ToUnity(EnemyVector2 value)
        {
            return new Vector2((float)value.X, (float)value.Y);
        }

        private sealed class PendingPulse
        {
            public PendingPulse(StableId emissionStableId, float remainingSeconds)
            {
                EmissionStableId = emissionStableId
                    ?? throw new ArgumentNullException(nameof(emissionStableId));
                RemainingSeconds = remainingSeconds;
            }

            public StableId EmissionStableId { get; }
            public float RemainingSeconds { get; set; }

            public static int Compare(PendingPulse left, PendingPulse right)
            {
                int time = left.RemainingSeconds.CompareTo(right.RemainingSeconds);
                return time != 0
                    ? time
                    : left.EmissionStableId.CompareTo(right.EmissionStableId);
            }
        }
    }

    internal sealed class EnemyAttackPresentationPlan
    {
        public EnemyAttackPresentationPlan(
            StableId dispatchStableId,
            Vector2 committedFacing,
            float firstDelaySeconds,
            IReadOnlyList<EnemyAttackPresentationPulse> pulses)
        {
            DispatchStableId = dispatchStableId
                ?? throw new ArgumentNullException(nameof(dispatchStableId));
            if (committedFacing.sqrMagnitude <= 0.000001f)
                throw new ArgumentOutOfRangeException(nameof(committedFacing));
            if (float.IsNaN(firstDelaySeconds)
                || float.IsInfinity(firstDelaySeconds)
                || firstDelaySeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(firstDelaySeconds));
            Pulses = pulses ?? throw new ArgumentNullException(nameof(pulses));
            CommittedFacing = committedFacing.normalized;
            FirstDelaySeconds = firstDelaySeconds;
        }

        public StableId DispatchStableId { get; }
        public Vector2 CommittedFacing { get; }
        public float FirstDelaySeconds { get; }
        public IReadOnlyList<EnemyAttackPresentationPulse> Pulses { get; }
    }

    internal sealed class EnemyAttackPresentationPulse
    {
        public EnemyAttackPresentationPulse(
            StableId emissionStableId,
            float delaySeconds)
        {
            EmissionStableId = emissionStableId
                ?? throw new ArgumentNullException(nameof(emissionStableId));
            if (float.IsNaN(delaySeconds)
                || float.IsInfinity(delaySeconds)
                || delaySeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(delaySeconds));
            DelaySeconds = delaySeconds;
        }

        public StableId EmissionStableId { get; }
        public float DelaySeconds { get; }

        public static int Compare(
            EnemyAttackPresentationPulse left,
            EnemyAttackPresentationPulse right)
        {
            int time = left.DelaySeconds.CompareTo(right.DelaySeconds);
            return time != 0
                ? time
                : left.EmissionStableId.CompareTo(right.EmissionStableId);
        }
    }
}
