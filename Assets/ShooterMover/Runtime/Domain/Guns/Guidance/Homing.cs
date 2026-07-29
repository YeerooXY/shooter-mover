using System;
using System.Collections.Generic;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Domain.Guns.Guidance
{
    /// <summary>
    /// Reusable deterministic guidance policy. It selects exact target snapshots and returns a
    /// turn-rate-limited direction without moving projectiles or interacting with Unity physics.
    /// </summary>
    public sealed class Homing
    {
        public GunGuidanceDecision Decide(
            GunGuidanceSpec guidance,
            GunGuidanceState state,
            GunVector2 projectilePosition,
            double deltaSeconds,
            IGunGuidanceTargetSnapshotSource targetSource)
        {
            if (guidance == null)
            {
                throw new ArgumentNullException(nameof(guidance));
            }
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (projectilePosition == null)
            {
                throw new ArgumentNullException(nameof(projectilePosition));
            }
            if (!projectilePosition.IsFinite)
            {
                throw new ArgumentException(
                    "Gun guidance projectile positions must be finite.",
                    nameof(projectilePosition));
            }
            if (double.IsNaN(deltaSeconds)
                || double.IsInfinity(deltaSeconds)
                || deltaSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            double elapsedSeconds = state.ElapsedSeconds + deltaSeconds;
            if (double.IsInfinity(elapsedSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }
            double pauseRemainingSeconds = Math.Max(
                0d,
                state.PauseRemainingSeconds - deltaSeconds);

            if (guidance.Mode == GunGuidanceMode.Unguided)
            {
                GunGuidanceState unguidedState = state.Advance(
                    state.Direction,
                    elapsedSeconds,
                    pauseRemainingSeconds,
                    state.TrackedTarget);
                return new GunGuidanceDecision(
                    GunGuidanceDecisionStatus.Unguided,
                    unguidedState,
                    null);
            }
            if (guidance.Mode != GunGuidanceMode.Homing)
            {
                throw new ArgumentOutOfRangeException(nameof(guidance));
            }

            double activationStartOffset = Clamp(
                guidance.ActivationDelaySeconds - state.ElapsedSeconds,
                0d,
                deltaSeconds);
            if (elapsedSeconds < guidance.ActivationDelaySeconds)
            {
                GunGuidanceState waitingState = state.Advance(
                    state.Direction,
                    elapsedSeconds,
                    pauseRemainingSeconds,
                    state.TrackedTarget);
                return new GunGuidanceDecision(
                    GunGuidanceDecisionStatus.WaitingForActivation,
                    waitingState,
                    null);
            }

            double pauseEndOffset = Math.Min(state.PauseRemainingSeconds, deltaSeconds);
            double guidanceStartOffset = Math.Max(activationStartOffset, pauseEndOffset);
            double guidanceSeconds = Math.Max(0d, deltaSeconds - guidanceStartOffset);
            bool remainsPaused = pauseRemainingSeconds > 0d
                || (deltaSeconds == 0d && state.PauseRemainingSeconds > 0d);
            if (remainsPaused)
            {
                GunGuidanceState pausedState = state.Advance(
                    state.Direction,
                    elapsedSeconds,
                    pauseRemainingSeconds,
                    state.TrackedTarget);
                return new GunGuidanceDecision(
                    GunGuidanceDecisionStatus.Paused,
                    pausedState,
                    null);
            }

            IReadOnlyList<GunGuidanceTargetSnapshot> snapshots =
                GunGuidanceTargetSelector.Freeze(targetSource);
            double acquisitionRangeSquared =
                guidance.AcquisitionRange * guidance.AcquisitionRange;

            GunGuidanceTargetSnapshot resolvedTarget = null;
            GunTargetReference nextTrackedTarget = state.TrackedTarget;
            GunGuidanceAcquisitionState nextAcquisitionState = state.AcquisitionState;
            bool hasTarget = state.AcquisitionState
                    != GunGuidanceAcquisitionState.LostWithoutReacquisition
                && GunGuidanceTargetSelector.TryResolveExact(
                    snapshots,
                    state.TrackedTarget,
                    projectilePosition,
                    acquisitionRangeSquared,
                    out resolvedTarget);

            if (hasTarget)
            {
                nextTrackedTarget = resolvedTarget.Target;
                nextAcquisitionState = GunGuidanceAcquisitionState.Tracking;
            }
            else
            {
                bool maySelect = false;
                switch (state.AcquisitionState)
                {
                    case GunGuidanceAcquisitionState.NotAcquired:
                        maySelect = true;
                        nextTrackedTarget = null;
                        nextAcquisitionState = GunGuidanceAcquisitionState.NotAcquired;
                        break;

                    case GunGuidanceAcquisitionState.Tracking:
                    case GunGuidanceAcquisitionState.WaitingForReacquisition:
                        if (guidance.Reacquisition
                            == GunReacquisitionMode.ReuseTargetPolicy)
                        {
                            maySelect = true;
                            nextAcquisitionState =
                                GunGuidanceAcquisitionState.WaitingForReacquisition;
                            if (guidance.TargetPolicy
                                != GunTargetPolicy.CurrentLockedTarget)
                            {
                                nextTrackedTarget = null;
                            }
                        }
                        else
                        {
                            nextTrackedTarget = null;
                            nextAcquisitionState =
                                GunGuidanceAcquisitionState.LostWithoutReacquisition;
                        }
                        break;

                    case GunGuidanceAcquisitionState.LostWithoutReacquisition:
                        nextTrackedTarget = null;
                        nextAcquisitionState =
                            GunGuidanceAcquisitionState.LostWithoutReacquisition;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(state));
                }

                if (maySelect)
                {
                    hasTarget = GunGuidanceTargetSelector.TrySelect(
                        snapshots,
                        guidance.TargetPolicy,
                        nextTrackedTarget,
                        projectilePosition,
                        state.AcquisitionAimDirection,
                        acquisitionRangeSquared,
                        out resolvedTarget);
                    if (hasTarget)
                    {
                        nextTrackedTarget = resolvedTarget.Target;
                        nextAcquisitionState = GunGuidanceAcquisitionState.Tracking;
                    }
                }
            }

            if (!hasTarget)
            {
                GunGuidanceState noTargetState = state.Advance(
                    state.Direction,
                    elapsedSeconds,
                    pauseRemainingSeconds,
                    nextAcquisitionState,
                    nextTrackedTarget);
                return new GunGuidanceDecision(
                    GunGuidanceDecisionStatus.NoTarget,
                    noTargetState,
                    null);
            }

            GunVector2 desiredDirection = GunGuidanceGeometry.Difference(
                resolvedTarget.Position,
                projectilePosition).Normalized;
            double maximumTurnDegrees =
                guidance.TurnRateDegreesPerSecond * guidanceSeconds;
            GunVector2 direction = GunGuidanceGeometry.RotateTowards(
                state.Direction,
                desiredDirection,
                maximumTurnDegrees);
            GunGuidanceState trackingState = state.Advance(
                direction,
                elapsedSeconds,
                pauseRemainingSeconds,
                GunGuidanceAcquisitionState.Tracking,
                nextTrackedTarget);

            return new GunGuidanceDecision(
                GunGuidanceDecisionStatus.Tracking,
                trackingState,
                resolvedTarget);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
