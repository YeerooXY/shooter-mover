using System;
using System.Collections.Generic;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;
using UnityEngine;

namespace ShooterMover.UnityAdapters.Enemies
{
    /// <summary>
    /// Reusable pounce/ram motion projection for schema-v2 melee windows. The immutable committed
    /// origin, direction, lunge distance and active-window times determine every position; Unity
    /// frame delta is never accumulated as attack timing authority.
    /// </summary>
    public sealed class RigidbodyEnemyAttackPatternPounceMotion2D :
        IEnemyAttackPatternPounceMotionV1
    {
        private sealed class MotionState
        {
            public MotionState(
                Vector2 origin,
                Vector2 direction,
                float distance)
            {
                Origin = origin;
                Direction = direction.normalized;
                Distance = distance;
            }

            public Vector2 Origin { get; }
            public Vector2 Direction { get; }
            public float Distance { get; }
        }

        private readonly Rigidbody2D body;
        private readonly Dictionary<StableId, MotionState> states =
            new Dictionary<StableId, MotionState>();

        public RigidbodyEnemyAttackPatternPounceMotion2D(Rigidbody2D body)
        {
            this.body = body ?? throw new ArgumentNullException(nameof(body));
        }

        public int ActiveMotionCount
        {
            get { return states.Count; }
        }

        public void Open(
            EnemyAttackEffectEmissionV1 emission,
            Vector2 committedOrigin,
            Vector2 committedDirection,
            float lungeDistance)
        {
            ValidateEmission(emission);
            if (!Finite(committedOrigin)
                || !Finite(committedDirection)
                || committedDirection.sqrMagnitude <= 0.000001f
                || float.IsNaN(lungeDistance)
                || float.IsInfinity(lungeDistance)
                || lungeDistance <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(lungeDistance));
            }

            MotionState existing;
            if (states.TryGetValue(emission.EmissionStableId, out existing))
            {
                if (existing.Origin != committedOrigin
                    || Vector2.Dot(
                        existing.Direction,
                        committedDirection.normalized) < 0.99999f
                    || !Mathf.Approximately(existing.Distance, lungeDistance))
                {
                    throw new InvalidOperationException(
                        "A pounce emission cannot be reopened with conflicting motion facts.");
                }
                return;
            }
            states.Add(
                emission.EmissionStableId,
                new MotionState(
                    committedOrigin,
                    committedDirection,
                    lungeDistance));
        }

        public void Tick(
            EnemyAttackEffectEmissionV1 emission,
            double authoritativeTimeSeconds)
        {
            ValidateEmission(emission);
            if (double.IsNaN(authoritativeTimeSeconds)
                || double.IsInfinity(authoritativeTimeSeconds)
                || authoritativeTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(authoritativeTimeSeconds));
            }

            MotionState state;
            if (!states.TryGetValue(emission.EmissionStableId, out state))
            {
                return;
            }
            double duration = emission.ActiveUntilSeconds
                - emission.ScheduledAtSeconds;
            double normalized = duration <= 0d
                ? 1d
                : (authoritativeTimeSeconds - emission.ScheduledAtSeconds)
                    / duration;
            float progress = Mathf.Clamp01((float)normalized);
            body.MovePosition(
                state.Origin
                + (state.Direction * state.Distance * progress));
        }

        public void Close(
            EnemyAttackEffectEmissionV1 emission,
            bool cancelled)
        {
            if (emission != null)
            {
                states.Remove(emission.EmissionStableId);
            }
        }

        public void Clear()
        {
            states.Clear();
        }

        private static void ValidateEmission(
            EnemyAttackEffectEmissionV1 emission)
        {
            if (emission == null
                || emission.Kind
                    != EnemyAttackEffectEmissionKindV1.MeleeStrike
                || emission.MeleeStrike == null
                || emission.MeleeStrike.Pattern.LungeDistance <= 0d)
            {
                throw new ArgumentException(
                    "A schema-v2 pounce melee emission is required.",
                    nameof(emission));
            }
        }

        private static bool Finite(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.y);
        }
    }
}
