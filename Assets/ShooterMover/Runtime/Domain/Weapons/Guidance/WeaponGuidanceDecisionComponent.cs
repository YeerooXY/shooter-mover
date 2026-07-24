using System;
using System.Collections.Generic;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Domain.Weapons.Guidance
{
    /// <summary>
    /// Reusable deterministic guidance policy. It selects exact target snapshots and returns a
    /// turn-rate-limited direction without moving projectiles or interacting with Unity physics.
    /// </summary>
    public sealed class WeaponGuidanceDecisionComponent
    {
        public WeaponGuidanceDecision Decide(
            WeaponGuidanceSpec guidance,
            WeaponGuidanceState state,
            WeaponVector2 projectilePosition,
            double deltaSeconds,
            IWeaponGuidanceTargetSnapshotSource targetSource)
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
                    "Weapon guidance projectile positions must be finite.",
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

            if (guidance.Mode == WeaponGuidanceMode.Unguided)
            {
                WeaponGuidanceState unguidedState = state.Advance(
                    state.Direction,
                    elapsedSeconds,
                    pauseRemainingSeconds,
                    null);
                return new WeaponGuidanceDecision(
                    WeaponGuidanceDecisionStatus.Unguided,
                    unguidedState,
                    null);
            }
            if (guidance.Mode != WeaponGuidanceMode.Homing)
            {
                throw new ArgumentOutOfRangeException(nameof(guidance));
            }

            double activationStartOffset = Clamp(
                guidance.ActivationDelaySeconds - state.ElapsedSeconds,
                0d,
                deltaSeconds);
            if (elapsedSeconds < guidance.ActivationDelaySeconds)
            {
                WeaponGuidanceState waitingState = state.Advance(
                    state.Direction,
                    elapsedSeconds,
                    pauseRemainingSeconds,
                    state.TrackedTarget);
                return new WeaponGuidanceDecision(
                    WeaponGuidanceDecisionStatus.WaitingForActivation,
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
                WeaponGuidanceState pausedState = state.Advance(
                    state.Direction,
                    elapsedSeconds,
                    pauseRemainingSeconds,
                    state.TrackedTarget);
                return new WeaponGuidanceDecision(
                    WeaponGuidanceDecisionStatus.Paused,
                    pausedState,
                    null);
            }

            IReadOnlyList<WeaponGuidanceTargetSnapshot> snapshots =
                WeaponGuidanceTargetSelector.Freeze(targetSource);
            double acquisitionRangeSquared =
                guidance.AcquisitionRange * guidance.AcquisitionRange;

            WeaponGuidanceTargetSnapshot resolvedTarget;
            WeaponGuidanceTargetReference nextTrackedTarget = state.TrackedTarget;
            bool hasTarget = WeaponGuidanceTargetSelector.TryResolveExact(
                snapshots,
                state.TrackedTarget,
                projectilePosition,
                acquisitionRangeSquared,
                out resolvedTarget);

            if (!hasTarget)
            {
                bool maySelect = state.TrackedTarget == null
                    || guidance.Reacquisition == WeaponReacquisitionMode.ReuseTargetPolicy;
                if (maySelect)
                {
                    hasTarget = WeaponGuidanceTargetSelector.TrySelect(
                        snapshots,
                        guidance.TargetPolicy,
                        state.TrackedTarget,
                        projectilePosition,
                        state.AcquisitionAimDirection,
                        acquisitionRangeSquared,
                        out resolvedTarget);
                }

                if (hasTarget)
                {
                    nextTrackedTarget = resolvedTarget.Target;
                }
                else if (guidance.TargetPolicy == WeaponTargetPolicy.CurrentLockedTarget
                    && guidance.Reacquisition == WeaponReacquisitionMode.ReuseTargetPolicy)
                {
                    nextTrackedTarget = state.TrackedTarget;
                }
                else
                {
                    nextTrackedTarget = null;
                }
            }

            if (!hasTarget)
            {
                WeaponGuidanceState noTargetState = state.Advance(
                    state.Direction,
                    elapsedSeconds,
                    pauseRemainingSeconds,
                    nextTrackedTarget);
                return new WeaponGuidanceDecision(
                    WeaponGuidanceDecisionStatus.NoTarget,
                    noTargetState,
                    null);
            }

            WeaponVector2 desiredDirection = WeaponGuidanceGeometry.Difference(
                resolvedTarget.Position,
                projectilePosition).Normalized;
            double maximumTurnDegrees =
                guidance.TurnRateDegreesPerSecond * guidanceSeconds;
            WeaponVector2 direction = WeaponGuidanceGeometry.RotateTowards(
                state.Direction,
                desiredDirection,
                maximumTurnDegrees);
            WeaponGuidanceState trackingState = state.Advance(
                direction,
                elapsedSeconds,
                pauseRemainingSeconds,
                nextTrackedTarget);

            return new WeaponGuidanceDecision(
                WeaponGuidanceDecisionStatus.Tracking,
                trackingState,
                resolvedTarget);
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
