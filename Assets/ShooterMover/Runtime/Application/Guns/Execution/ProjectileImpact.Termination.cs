using System;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public static partial class ProjectileImpact
    {
        private static GunImpactDecision EvaluateRangeExpiry(
            GunImpactRequest request,
            DeterministicRandom random)
        {
            if (!request.ImpactSpec.HandlesRangeExpiry)
            {
                return Build(
                    request,
                    GunImpactDecisionKind.Ignored,
                    GunImpactContinuation.Continue,
                    GunExplosionTriggerReason.None,
                    false,
                    false,
                    request.IncomingDirection,
                    request.Speed,
                    0d,
                    request.RicochetState,
                    random);
            }

            GunExplosionTriggerReason explosionReasons = ResolveExplosionReasons(
                request.ImpactSpec.ExplosionTrigger,
                GunExplosionTriggerReason.RangeExpiry,
                true);
            return Build(
                request,
                GunImpactDecisionKind.Terminate,
                GunImpactContinuation.Terminate,
                explosionReasons,
                false,
                false,
                request.IncomingDirection,
                request.Speed,
                0d,
                request.RicochetState,
                random);
        }

        private static GunImpactDecision EvaluateTermination(
            GunImpactRequest request,
            DeterministicRandom random)
        {
            if (!request.ImpactSpec.HandlesTermination)
            {
                return Build(
                    request,
                    GunImpactDecisionKind.Ignored,
                    GunImpactContinuation.Continue,
                    GunExplosionTriggerReason.None,
                    false,
                    false,
                    request.IncomingDirection,
                    request.Speed,
                    0d,
                    request.RicochetState,
                    random);
            }

            GunExplosionTriggerReason explosionReasons = ResolveExplosionReasons(
                request.ImpactSpec.ExplosionTrigger,
                GunExplosionTriggerReason.None,
                true);
            return Build(
                request,
                GunImpactDecisionKind.Terminate,
                GunImpactContinuation.Terminate,
                explosionReasons,
                false,
                false,
                request.IncomingDirection,
                request.Speed,
                0d,
                request.RicochetState,
                random);
        }

        private static GunImpactDecision BuildWallFallback(
            GunImpactRequest request,
            RicochetState state,
            DeterministicRandom random)
        {
            GunExplosionTriggerReason explosionReasons = ResolveExplosionReasons(
                request.ImpactSpec.ExplosionTrigger,
                GunExplosionTriggerReason.WallImpact,
                true);
            return Build(
                request,
                GunImpactDecisionKind.Terminate,
                GunImpactContinuation.Terminate,
                explosionReasons,
                false,
                false,
                request.IncomingDirection,
                request.Speed,
                0d,
                state,
                random);
        }

        private static GunExplosionTriggerReason ResolveExplosionReasons(
            GunExplosionTriggerSpec trigger,
            GunExplosionTriggerReason eventReason,
            bool terminates)
        {
            if (trigger == null)
            {
                return GunExplosionTriggerReason.None;
            }

            GunExplosionTriggerReason reasons = GunExplosionTriggerReason.None;
            switch (eventReason)
            {
                case GunExplosionTriggerReason.None:
                    break;
                case GunExplosionTriggerReason.EnemyImpact:
                    if (trigger.OnEnemyImpact)
                    {
                        reasons |= GunExplosionTriggerReason.EnemyImpact;
                    }
                    break;
                case GunExplosionTriggerReason.WallImpact:
                    if (trigger.OnWallImpact)
                    {
                        reasons |= GunExplosionTriggerReason.WallImpact;
                    }
                    break;
                case GunExplosionTriggerReason.RangeExpiry:
                    if (trigger.OnRangeExpiry)
                    {
                        reasons |= GunExplosionTriggerReason.RangeExpiry;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(eventReason));
            }

            if (terminates && trigger.OnTermination)
            {
                reasons |= GunExplosionTriggerReason.Termination;
            }
            return reasons;
        }

        private static GunVector2 Reflect(
            GunVector2 incomingDirection,
            GunVector2 wallNormal)
        {
            GunVector2 direction = incomingDirection.Normalized;
            GunVector2 normal = wallNormal.Normalized;
            double dot = (direction.X * normal.X) + (direction.Y * normal.Y);
            return new GunVector2(
                direction.X - (2d * dot * normal.X),
                direction.Y - (2d * dot * normal.Y)).Normalized;
        }

        private static GunImpactDecision Build(
            GunImpactRequest request,
            GunImpactDecisionKind kind,
            GunImpactContinuation continuation,
            GunExplosionTriggerReason explosionReasons,
            bool consumesPierce,
            bool consumesBounceOpportunity,
            GunVector2 directionAfterImpact,
            double speedAfterImpact,
            double homingPauseSeconds,
            RicochetState state,
            DeterministicRandom random)
        {
            return new GunImpactDecision(
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
