using System;
using ShooterMover.Domain.Common;
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
        private static readonly StableId CanonicalRicochetDecisionPurpose =
            StableId.Parse("weapon.ricochet-final-bounce");
        private static readonly StableId RicochetProjectileOrdinalPurpose =
            StableId.Parse("weapon.ricochet-projectile-ordinal");
        private static readonly StableId RicochetWallContactPurpose =
            StableId.Parse("weapon.ricochet-wall-contact");

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

            return ricochet.HasCanonicalFixedPointBudget
                ? EvaluateCanonicalWallImpact(request, ricochet, random)
                : EvaluateLegacyWallImpact(request, ricochet, random);
        }

        private static WeaponImpactDecision EvaluateCanonicalWallImpact(
            WeaponImpactRequest request,
            WeaponRicochetSpec ricochet,
            DeterministicRandom random)
        {
            RicochetValue authoredBudget = ricochet.FixedPointBudget.Value;
            WeaponRicochetRuntimeState state =
                request.RicochetState.BeginCanonicalBudget(authoredBudget);
            RicochetValue remaining = state.RemainingFixedPointBudget.Value;

            bool fractionalRollSucceeded = false;
            if (WeaponFixedPointBudgetRules.RequiresFractionalRicochetRoll(remaining))
            {
                DeterministicRandom decisionStream = CreateCanonicalRicochetDecisionStream(
                    request,
                    random);
                decisionStream.NextChance(
                    checked((ulong)remaining.FractionalTenths),
                    10UL,
                    out fractionalRollSucceeded);
            }

            WeaponRicochetCollisionResolution resolution =
                WeaponFixedPointBudgetRules.ResolveEligibleRicochetCollision(
                    remaining,
                    fractionalRollSucceeded);
            WeaponRicochetRuntimeState resolvedState = state.AfterCanonicalWallContact(
                request.SimulationStep,
                request.WallContactId,
                resolution);

            if (!resolution.Bounces)
            {
                return BuildWallFallback(request, resolvedState, random);
            }

            return BuildSuccessfulBounce(
                request,
                ricochet,
                resolvedState,
                random);
        }

        private static WeaponImpactDecision EvaluateLegacyWallImpact(
            WeaponImpactRequest request,
            WeaponRicochetSpec ricochet,
            DeterministicRandom random)
        {
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

            WeaponRicochetRuntimeState bouncedState =
                request.RicochetState.AfterWallContact(
                    request.SimulationStep,
                    request.WallContactId,
                    true);
            return BuildSuccessfulBounce(
                request,
                ricochet,
                bouncedState,
                nextRandom);
        }

        private static WeaponImpactDecision BuildSuccessfulBounce(
            WeaponImpactRequest request,
            WeaponRicochetSpec ricochet,
            WeaponRicochetRuntimeState bouncedState,
            DeterministicRandom random)
        {
            WeaponVector2 reflected = Reflect(
                request.IncomingDirection,
                request.WallNormal);
            DeterministicRandom nextRandom = random;
            if (ricochet.RandomAngleDegrees > 0d)
            {
                double angleRoll;
                nextRandom = nextRandom.NextUnitInterval(out angleRoll);
                double angle = ((angleRoll * 2d) - 1d)
                    * ricochet.RandomAngleDegrees;
                reflected = reflected.RotateDegrees(angle).Normalized;
            }

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

        private static DeterministicRandom CreateCanonicalRicochetDecisionStream(
            WeaponImpactRequest request,
            DeterministicRandom random)
        {
            WeaponEffectIdentity identity = request.ProjectileIdentity;
            DeterministicRandom stream = DeterministicRandom.CreateSubstream(
                random.StreamSeed,
                random.AlgorithmVersion,
                CanonicalRicochetDecisionPurpose,
                checked((ulong)identity.ShotSequence));
            stream = DeterministicRandom.CreateSubstream(
                stream.StreamSeed,
                stream.AlgorithmVersion,
                RicochetProjectileOrdinalPurpose,
                checked((ulong)identity.ProjectileOrdinal.Value));
            stream = DeterministicRandom.CreateSubstream(
                stream.StreamSeed,
                stream.AlgorithmVersion,
                identity.ActorId.Value,
                checked((ulong)identity.LifecycleGeneration.Value));
            stream = DeterministicRandom.CreateSubstream(
                stream.StreamSeed,
                stream.AlgorithmVersion,
                identity.ParticipantId.Value,
                0UL);
            stream = DeterministicRandom.CreateSubstream(
                stream.StreamSeed,
                stream.AlgorithmVersion,
                identity.EquipmentInstanceId.Value,
                0UL);
            stream = DeterministicRandom.CreateSubstream(
                stream.StreamSeed,
                stream.AlgorithmVersion,
                identity.FireOperationId.Value,
                checked((ulong)request.ImpactOrdinal));
            return DeterministicRandom.CreateSubstream(
                stream.StreamSeed,
                stream.AlgorithmVersion,
                RicochetWallContactPurpose,
                checked((ulong)request.WallContactId.Value.GetHashCode()));
        }
    }
}
