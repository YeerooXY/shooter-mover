using System;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public enum WeaponDamageOverTimeApplicationStatus
    {
        AppliedStack = 1,
        RefreshedAtCapacity = 2,
        DuplicateSuppressed = 3,
        AtCapacitySuppressed = 4,
    }

    public sealed class WeaponDamageOverTimeResolutionRequest
    {
        public WeaponDamageOverTimeResolutionRequest(
            WeaponEffectSourceContext source,
            WeaponTargetReference target,
            WeaponDamageSpec damage,
            WeaponDamageOverTimeEffect effect,
            WeaponDamageOverTimeStateSnapshot currentState,
            IWeaponEffectApplicationHistory applicationHistory)
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

        public WeaponEffectSourceContext Source { get; }
        public WeaponTargetReference Target { get; }
        public WeaponDamageSpec Damage { get; }
        public WeaponDamageOverTimeEffect Effect { get; }
        public WeaponDamageOverTimeStateSnapshot CurrentState { get; }
        public IWeaponEffectApplicationHistory ApplicationHistory { get; }
    }

    public sealed class WeaponDamageOverTimeResolution
    {
        internal WeaponDamageOverTimeResolution(
            WeaponDamageOverTimeApplicationStatus status,
            WeaponEffectApplicationKey applicationKey,
            WeaponDamageOverTimeApplicationDecision decision)
        {
            Status = status;
            ApplicationKey = applicationKey
                ?? throw new ArgumentNullException(nameof(applicationKey));
            Decision = decision;
        }

        public WeaponDamageOverTimeApplicationStatus Status { get; }
        public WeaponEffectApplicationKey ApplicationKey { get; }
        public WeaponDamageOverTimeApplicationDecision Decision { get; }
        public bool EmitsDecision { get { return Decision != null; } }
    }

    public sealed class WeaponDamageOverTimeResolver
    {
        public WeaponDamageOverTimeResolution Resolve(
            WeaponDamageOverTimeResolutionRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            WeaponEffectApplicationKey key = WeaponEffectApplicationKey.ForDamageOverTime(
                request.Source,
                request.Target);
            if (request.ApplicationHistory.Contains(key))
            {
                return new WeaponDamageOverTimeResolution(
                    WeaponDamageOverTimeApplicationStatus.DuplicateSuppressed,
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
                    WeaponDamageOverTimeApplicationStatus.AppliedStack,
                    resultingStacks,
                    resultingDuration,
                    refreshesExistingDuration);
            }

            if (request.Effect.RefreshesDuration)
            {
                return Emit(
                    request,
                    key,
                    WeaponDamageOverTimeApplicationStatus.RefreshedAtCapacity,
                    request.CurrentState.StackCount,
                    request.Damage.DamageOverTimeDurationSeconds,
                    true);
            }

            return new WeaponDamageOverTimeResolution(
                WeaponDamageOverTimeApplicationStatus.AtCapacitySuppressed,
                key,
                null);
        }

        private static WeaponDamageOverTimeResolution Emit(
            WeaponDamageOverTimeResolutionRequest request,
            WeaponEffectApplicationKey key,
            WeaponDamageOverTimeApplicationStatus status,
            int resultingStacks,
            double resultingDuration,
            bool refreshedDuration)
        {
            WeaponDamageOverTimeApplicationDecision decision =
                new WeaponDamageOverTimeApplicationDecision(
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
            return new WeaponDamageOverTimeResolution(status, key, decision);
        }
    }
}
