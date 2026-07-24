using System;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    /// <summary>
    /// Pure impact policy for WeaponImpactSpec. The caller owns projectile movement, pierce
    /// accounting, effect emission, and storage of the returned immutable state.
    /// </summary>
    public static partial class WeaponImpactDecisionLogic
    {
        public static WeaponImpactDecision Evaluate(
            WeaponImpactRequest request,
            DeterministicRandom random)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            switch (request.EventKind)
            {
                case WeaponImpactEventKind.EnemyImpact:
                    return EvaluateEnemyImpact(request, random);
                case WeaponImpactEventKind.WallImpact:
                    return EvaluateWallImpact(request, random);
                case WeaponImpactEventKind.RangeExpiry:
                    return EvaluateRangeExpiry(request, random);
                case WeaponImpactEventKind.Termination:
                    return EvaluateTermination(request, random);
                default:
                    throw new ArgumentOutOfRangeException(nameof(request));
            }
        }

        private static WeaponImpactDecision EvaluateEnemyImpact(
            WeaponImpactRequest request,
            DeterministicRandom random)
        {
            if (!request.ImpactSpec.HandlesEnemyImpact)
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
                WeaponExplosionTriggerReason.EnemyImpact,
                false);
            return Build(
                request,
                WeaponImpactDecisionKind.Continue,
                WeaponImpactContinuation.Continue,
                explosionReasons,
                true,
                false,
                request.IncomingDirection,
                request.Speed,
                0d,
                request.RicochetState,
                random);
        }

        private static WeaponImpactDecision EvaluateWallImpact(
            WeaponImpactRequest request,
            DeterministicRandom random)
        {
            if (!request.ImpactSpec.HandlesWallImpact)
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

            WeaponRicochetSpec ricochet = request.ImpactSpec.Ricochet;
            if (ricochet == null)
            {
                return BuildWallFallback(
                    request,
                    request.RicochetState,
                    random);
            }

            if (request.RicochetState.IsDuplicateWallContact(
                    request.SimulationStep,
                    request.WallContactId))
            {
                return Build(
                    request,
                    WeaponImpactDecisionKind.DuplicateWallContact,
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

            if (request.RicochetState.SuccessfulBounceCount
                >= ricochet.MaximumSuccessfulBounces)
            {
                WeaponRicochetRuntimeState exhaustedState =
                    request.RicochetState.AfterWallContact(
                        request.SimulationStep,
                        request.WallContactId,
                        false);
                return BuildWallFallback(request, exhaustedState, random);
            }

            double chanceRoll;
            DeterministicRandom nextRandom = random.NextUnitInterval(out chanceRoll);
            if (chanceRoll >= ricochet.BounceChance)
            {
                WeaponRicochetRuntimeState failedState =
                    request.RicochetState.AfterWallContact(
                        request.SimulationStep,
                        request.WallContactId,
                        false);
                return BuildWallFallback(request, failedState, nextRandom);
            }

            WeaponVector2 reflected = Reflect(
                request.IncomingDirection,
                request.WallNormal);
            if (ricochet.RandomAngleDegrees > 0d)
            {
                double angleRoll;
                nextRandom = nextRandom.NextUnitInterval(out angleRoll);
                double angle = ((angleRoll * 2d) - 1d)
                    * ricochet.RandomAngleDegrees;
                reflected = reflected.RotateDegrees(angle).Normalized;
            }

            WeaponRicochetRuntimeState bouncedState =
                request.RicochetState.AfterWallContact(
                    request.SimulationStep,
                    request.WallContactId,
                    true);
            WeaponExplosionTriggerReason explosionReasons = ResolveExplosionReasons(
                request.ImpactSpec.ExplosionTrigger,
                WeaponExplosionTriggerReason.WallImpact,
                false);
            return Build(
                request,
                WeaponImpactDecisionKind.Ricochet,
                WeaponImpactContinuation.Continue,
                explosionReasons,
                false,
                true,
                reflected,
                request.Speed * ricochet.RetainedSpeedPerRicochet,
                ricochet.PostBounceHomingPauseSeconds,
                bouncedState,
                nextRandom);
        }
    }
}
