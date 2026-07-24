using System;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public static partial class WeaponImpactDecisionLogic
    {
        private static WeaponImpactDecision EvaluateRangeExpiry(
            WeaponImpactRequest request,
            DeterministicRandom random)
        {
            if (!request.ImpactSpec.HandlesRangeExpiry)
            {
                return Build(
                    request,
                    WeaponImpactDecisionKind.Ignored,
                    WeaponImpactContinuation.Continue,
                    WeaponExplosionTriggerReason.None,
                    false,
                    false,
                    request.IncomingDirection,
                    request.Speed,
                    0d,
                    request.RicochetState,
                    random);
            }

            WeaponExplosionTriggerReason explosionReasons = ResolveExplosionReasons(
                request.ImpactSpec.ExplosionTrigger,
                WeaponExplosionTriggerReason.RangeExpiry,
                true);
            return Build(
                request,
                WeaponImpactDecisionKind.Terminate,
                WeaponImpactContinuation.Terminate,
                explosionReasons,
                false,
                false,
                request.IncomingDirection,
                request.Speed,
                0d,
                request.RicochetState,
                random);
        }

        private static WeaponImpactDecision EvaluateTermination(
            WeaponImpactRequest request,
            DeterministicRandom random)
        {
            if (!request.ImpactSpec.HandlesTermination)
            {
                return Build(
                    request,
                    WeaponImpactDecisionKind.Ignored,
                    WeaponImpactContinuation.Continue,
                    WeaponExplosionTriggerReason.None,
                    false,
                    false,
                    request.IncomingDirection,
                    request.Speed,
                    0d,
                    request.RicochetState,
                    random);
            }

            WeaponExplosionTriggerReason explosionReasons = ResolveExplosionReasons(
                request.ImpactSpec.ExplosionTrigger,
                WeaponExplosionTriggerReason.None,
                true);
            return Build(
                request,
                WeaponImpactDecisionKind.Terminate,
                WeaponImpactContinuation.Terminate,
                explosionReasons,
                false,
                false,
                request.IncomingDirection,
                request.Speed,
                0d,
                request.RicochetState,
                random);
        }

        private static WeaponImpactDecision BuildWallFallback(
            WeaponImpactRequest request,
            WeaponRicochetRuntimeState state,
            DeterministicRandom random)
        {
            WeaponExplosionTriggerReason explosionReasons = ResolveExplosionReasons(
                request.ImpactSpec.ExplosionTrigger,
                WeaponExplosionTriggerReason.WallImpact,
                true);
            return Build(
                request,
                WeaponImpactDecisionKind.Terminate,
                WeaponImpactContinuation.Terminate,
                explosionReasons,
                false,
                false,
                request.IncomingDirection,
                request.Speed,
                0d,
                state,
                random);
        }

        private static WeaponExplosionTriggerReason ResolveExplosionReasons(
            WeaponExplosionTriggerSpec trigger,
            WeaponExplosionTriggerReason eventReason,
            bool terminates)
        {
            if (trigger == null)
            {
                return WeaponExplosionTriggerReason.None;
            }

            WeaponExplosionTriggerReason reasons = WeaponExplosionTriggerReason.None;
            switch (eventReason)
            {
                case WeaponExplosionTriggerReason.None:
                    break;
                case WeaponExplosionTriggerReason.EnemyImpact:
                    if (trigger.OnEnemyImpact)
                    {
                        reasons |= WeaponExplosionTriggerReason.EnemyImpact;
                    }
                    break;
                case WeaponExplosionTriggerReason.WallImpact:
                    if (trigger.OnWallImpact)
                    {
                        reasons |= WeaponExplosionTriggerReason.WallImpact;
                    }
                    break;
                case WeaponExplosionTriggerReason.RangeExpiry:
                    if (trigger.OnRangeExpiry)
                    {
                        reasons |= WeaponExplosionTriggerReason.RangeExpiry;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(eventReason));
            }

            if (terminates && trigger.OnTermination)
            {
                reasons |= WeaponExplosionTriggerReason.Termination;
            }
            return reasons;
        }

        private static WeaponVector2 Reflect(
            WeaponVector2 incomingDirection,
            WeaponVector2 wallNormal)
        {
            WeaponVector2 direction = incomingDirection.Normalized;
            WeaponVector2 normal = wallNormal.Normalized;
            double dot = (direction.X * normal.X) + (direction.Y * normal.Y);
            return new WeaponVector2(
                direction.X - (2d * dot * normal.X),
                direction.Y - (2d * dot * normal.Y)).Normalized;
        }

        private static WeaponImpactDecision Build(
            WeaponImpactRequest request,
            WeaponImpactDecisionKind kind,
            WeaponImpactContinuation continuation,
            WeaponExplosionTriggerReason explosionReasons,
            bool consumesPierce,
            bool consumesBounceOpportunity,
            WeaponVector2 directionAfterImpact,
            double speedAfterImpact,
            double homingPauseSeconds,
            WeaponRicochetRuntimeState state,
            DeterministicRandom random)
        {
            return new WeaponImpactDecision(
                request.ProjectileIdentity,
                request.ImpactOrdinal,
                request.EventKind,
                kind,
                continuation,
                explosionReasons,
                consumesPierce,
                consumesBounceOpportunity,
                directionAfterImpact,
                speedAfterImpact,
                homingPauseSeconds,
                state,
                random);
        }
    }
}
