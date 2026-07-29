using System;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public enum GunDamageOverTimeApplicationStatus
    {
        AppliedStack = 1,
        RefreshedAtCapacity = 2,
        DuplicateSuppressed = 3,
        AtCapacitySuppressed = 4,
    }

    public sealed class GunDamageOverTimeResolutionRequest
    {
        public GunDamageOverTimeResolutionRequest(
            GunEffectSourceContext source,
            GunTargetReference target,
            GunDamageSpec damage,
            GunDamageOverTimeEffect effect,
            GunDamageOverTimeStateSnapshot currentState,
            IGunEffectApplicationHistory applicationHistory)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Damage = damage ?? throw new ArgumentNullException(nameof(damage));
            if (!damage.HasDamageOverTime)
            {
                throw new ArgumentException(
                    "Damage-over-time resolution requires positive authored magnitude and duration.",
                    nameof(damage));
            }
            Effect = effect ?? throw new ArgumentNullException(nameof(effect));
            CurrentState = currentState ?? throw new ArgumentNullException(nameof(currentState));
            if (currentState.StackCount > effect.MaximumStacks)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentState),
                    "Current stack count cannot exceed the authored maximum.");
            }
            ApplicationHistory = applicationHistory
                ?? throw new ArgumentNullException(nameof(applicationHistory));
        }

        public GunEffectSourceContext Source { get; }
        public GunTargetReference Target { get; }
        public GunDamageSpec Damage { get; }
        public GunDamageOverTimeEffect Effect { get; }
        public GunDamageOverTimeStateSnapshot CurrentState { get; }
        public IGunEffectApplicationHistory ApplicationHistory { get; }
    }

    public sealed class GunDamageOverTimeResolution
    {
        internal GunDamageOverTimeResolution(
            GunDamageOverTimeApplicationStatus status,
            GunEffectApplicationKey applicationKey,
            GunDamageOverTimeApplicationDecision decision)
        {
            Status = status;
            ApplicationKey = applicationKey
                ?? throw new ArgumentNullException(nameof(applicationKey));
            Decision = decision;
        }

        public GunDamageOverTimeApplicationStatus Status { get; }
        public GunEffectApplicationKey ApplicationKey { get; }
        public GunDamageOverTimeApplicationDecision Decision { get; }
        public bool EmitsDecision { get { return Decision != null; } }
    }

    public sealed class DamageOverTime
    {
        public GunDamageOverTimeResolution Resolve(
            GunDamageOverTimeResolutionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            GunEffectApplicationKey key = GunEffectApplicationKey.ForDamageOverTime(
                request.Source,
                request.Target);
            if (request.ApplicationHistory.Contains(key))
            {
                return new GunDamageOverTimeResolution(
                    GunDamageOverTimeApplicationStatus.DuplicateSuppressed,
                    key,
                    null);
            }

            if (request.CurrentState.StackCount < request.Effect.MaximumStacks)
            {
                int resultingStacks = request.CurrentState.StackCount + 1;
                bool refreshesExistingDuration = request.CurrentState.StackCount > 0
                    && request.Effect.RefreshesDuration;
                double resultingDuration = request.CurrentState.StackCount == 0
                    || refreshesExistingDuration
                        ? request.Damage.DamageOverTimeDurationSeconds
                        : request.CurrentState.RemainingDurationSeconds;
                return Emit(
                    request,
                    key,
                    GunDamageOverTimeApplicationStatus.AppliedStack,
                    resultingStacks,
                    resultingDuration,
                    refreshesExistingDuration);
            }

            if (request.Effect.RefreshesDuration)
            {
                return Emit(
                    request,
                    key,
                    GunDamageOverTimeApplicationStatus.RefreshedAtCapacity,
                    request.CurrentState.StackCount,
                    request.Damage.DamageOverTimeDurationSeconds,
                    true);
            }

            return new GunDamageOverTimeResolution(
                GunDamageOverTimeApplicationStatus.AtCapacitySuppressed,
                key,
                null);
        }

        private static GunDamageOverTimeResolution Emit(
            GunDamageOverTimeResolutionRequest request,
            GunEffectApplicationKey key,
            GunDamageOverTimeApplicationStatus status,
            int resultingStacks,
            double resultingDuration,
            bool refreshedDuration)
        {
            GunDamageOverTimeApplicationDecision decision =
                new GunDamageOverTimeApplicationDecision(
                    request.Source,
                    request.Target,
                    key,
                    request.Damage.Category,
                    request.Damage.DamageOverTimePerSecond,
                    request.Damage.DamageOverTimeDurationSeconds,
                    request.Effect.TicksPerSecond,
                    resultingStacks,
                    resultingDuration,
                    refreshedDuration);
            return new GunDamageOverTimeResolution(status, key, decision);
        }
    }
}
