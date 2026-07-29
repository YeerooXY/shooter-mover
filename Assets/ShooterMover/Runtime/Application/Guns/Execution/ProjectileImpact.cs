using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Common.Random;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    /// <summary>
    /// Pure impact policy for GunImpactSpec. The caller owns projectile movement, pierce
    /// accounting, effect emission, and storage of the returned immutable state.
    /// </summary>
    public static partial class ProjectileImpact
    {
        private static readonly StableId CanonicalRicochetDecisionPurpose =
            StableId.Parse("gun.ricochet-final-bounce");
        private static readonly StableId RicochetProjectileOrdinalPurpose =
            StableId.Parse("gun.ricochet-projectile-ordinal");

        public static GunImpactDecision Evaluate(
            GunImpactRequest request,
            DeterministicRandom random)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            switch (request.EventKind)
            {
                case GunImpactEventKind.EnemyImpact:
                    return EvaluateEnemyImpact(request, random);
                case GunImpactEventKind.WallImpact:
                    return EvaluateWallImpact(request, random);
                case GunImpactEventKind.RangeExpiry:
                    return EvaluateRangeExpiry(request, random);
                case GunImpactEventKind.Termination:
                    return EvaluateTermination(request, random);
                default:
                    throw new ArgumentOutOfRangeException(nameof(request));
            }
        }

        private static GunImpactDecision EvaluateEnemyImpact(
            GunImpactRequest request,
            DeterministicRandom random)
        {
            if (!request.ImpactSpec.HandlesEnemyImpact)
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
                GunExplosionTriggerReason.EnemyImpact,
                false);
            return Build(
                request,
                GunImpactDecisionKind.Continue,
                GunImpactContinuation.Continue,
                explosionReasons,
                true,
                false,
                request.IncomingDirection,
                request.Speed,
                0d,
                request.RicochetState,
                random);
        }

        private static GunImpactDecision EvaluateWallImpact(
            GunImpactRequest request,
            DeterministicRandom random)
        {
            if (!request.ImpactSpec.HandlesWallImpact)
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

            GunRicochetSpec ricochet = request.ImpactSpec.Ricochet;
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
                    GunImpactDecisionKind.DuplicateWallContact,
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

            return ricochet.HasCanonicalFixedPointBudget
                ? EvaluateCanonicalWallImpact(request, ricochet, random)
                : EvaluateLegacyWallImpact(request, ricochet, random);
        }

        private static GunImpactDecision EvaluateCanonicalWallImpact(
            GunImpactRequest request,
            GunRicochetSpec ricochet,
            DeterministicRandom random)
        {
            RicochetValue authoredBudget = ricochet.FixedPointBudget.Value;
            RicochetState state =
                request.RicochetState.BeginCanonicalBudget(authoredBudget);
            RicochetValue remaining = state.RemainingFixedPointBudget.Value;

            bool fractionalRollSucceeded = false;
            if (GunFixedPointBudgetRules.RequiresFractionalRicochetRoll(remaining))
            {
                DeterministicRandom decisionStream = CreateCanonicalRicochetDecisionStream(
                    request,
                    random);
                decisionStream.NextChance(
                    checked((ulong)remaining.FractionalTenths),
                    10UL,
                    out fractionalRollSucceeded);
            }

            GunRicochetCollisionResolution resolution =
                GunFixedPointBudgetRules.ResolveEligibleRicochetCollision(
                    remaining,
                    fractionalRollSucceeded);
            RicochetState resolvedState = state.AfterCanonicalWallContact(
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

        private static GunImpactDecision EvaluateLegacyWallImpact(
            GunImpactRequest request,
            GunRicochetSpec ricochet,
            DeterministicRandom random)
        {
            if (request.RicochetState.SuccessfulBounceCount
                >= ricochet.MaximumSuccessfulBounces)
            {
                RicochetState exhaustedState =
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
                RicochetState failedState =
                    request.RicochetState.AfterWallContact(
                        request.SimulationStep,
                        request.WallContactId,
                        false);
                return BuildWallFallback(request, failedState, nextRandom);
            }

            RicochetState bouncedState =
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

        private static GunImpactDecision BuildSuccessfulBounce(
            GunImpactRequest request,
            GunRicochetSpec ricochet,
            RicochetState bouncedState,
            DeterministicRandom random)
        {
            GunVector2 reflected = Reflect(
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

            GunExplosionTriggerReason explosionReasons = ResolveExplosionReasons(
                request.ImpactSpec.ExplosionTrigger,
                GunExplosionTriggerReason.WallImpact,
                false);
            return Build(
                request,
                GunImpactDecisionKind.Ricochet,
                GunImpactContinuation.Continue,
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
            GunImpactRequest request,
            DeterministicRandom random)
        {
            GunEffectIdentity identity = request.ProjectileIdentity;
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
                request.WallContactId.Value,
                checked((ulong)request.SimulationStep));
        }
    }
}
