using System;

namespace ShooterMover.Domain.Weapons.Execution
{
    /// <summary>
    /// PierceValue represents additional enemy-hit continuations after the primary hit.
    /// Guaranteed continuations are consumed before the one optional fractional continuation.
    /// </summary>
    public sealed class ProjectilePierceState
    {
        private ProjectilePierceState(
            PierceValue authoredValue,
            int remainingGuaranteedAdditionalHits,
            int fractionalChanceTenths,
            ProjectileFractionalPierceRollState fractionalRollState,
            int successfulEnemyImpacts)
        {
            AuthoredValue = authoredValue;
            RemainingGuaranteedAdditionalHits = remainingGuaranteedAdditionalHits;
            FractionalChanceTenths = fractionalChanceTenths;
            FractionalRollState = fractionalRollState;
            SuccessfulEnemyImpacts = successfulEnemyImpacts;
        }

        public PierceValue AuthoredValue { get; }
        public int RemainingGuaranteedAdditionalHits { get; }
        public int FractionalChanceTenths { get; }
        public ProjectileFractionalPierceRollState FractionalRollState { get; }
        public int SuccessfulEnemyImpacts { get; }
        public bool HasGuaranteedContinuation
        {
            get { return RemainingGuaranteedAdditionalHits > 0; }
        }
        public bool FractionalRollPending
        {
            get { return FractionalRollState == ProjectileFractionalPierceRollState.Pending; }
        }

        public static ProjectilePierceState Create(PierceValue authoredValue)
        {
            int fractionalTenths = authoredValue.Tenths % 10;
            return new ProjectilePierceState(
                authoredValue,
                authoredValue.GuaranteedHits,
                fractionalTenths,
                fractionalTenths == 0
                    ? ProjectileFractionalPierceRollState.NotApplicable
                    : ProjectileFractionalPierceRollState.Pending,
                0);
        }

        public ProjectilePierceState RecordSuccessfulEnemyImpact()
        {
            return new ProjectilePierceState(
                AuthoredValue,
                RemainingGuaranteedAdditionalHits,
                FractionalChanceTenths,
                FractionalRollState,
                checked(SuccessfulEnemyImpacts + 1));
        }

        public ProjectilePierceState ConsumeGuaranteedContinuation()
        {
            if (!HasGuaranteedContinuation)
            {
                throw new InvalidOperationException(
                    "No guaranteed projectile-pierce continuation remains.");
            }

            return new ProjectilePierceState(
                AuthoredValue,
                RemainingGuaranteedAdditionalHits - 1,
                FractionalChanceTenths,
                FractionalRollState,
                SuccessfulEnemyImpacts);
        }

        public ProjectilePierceState ResolveFractionalContinuation(bool granted)
        {
            if (!FractionalRollPending)
            {
                throw new InvalidOperationException(
                    "The fractional projectile-pierce roll was already resolved or does not exist.");
            }

            return new ProjectilePierceState(
                AuthoredValue,
                RemainingGuaranteedAdditionalHits,
                FractionalChanceTenths,
                granted
                    ? ProjectileFractionalPierceRollState.Granted
                    : ProjectileFractionalPierceRollState.Denied,
                SuccessfulEnemyImpacts);
        }
    }
}
