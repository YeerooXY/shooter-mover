using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Application.Weapons.Execution
{
    /// <summary>
    /// Immutable per-projectile ricochet state. It is deliberately supplied by the caller so
    /// impact decisions remain pure and do not require a registry or another runtime service.
    /// Canonical fixed-point state remains separate from the legacy maximum/chance path.
    /// </summary>
    public sealed class WeaponRicochetRuntimeState
    {
        private static readonly WeaponRicochetRuntimeState InitialState =
            new WeaponRicochetRuntimeState(
                0,
                null,
                -1L,
                new WeaponWallContactId[0]);

        private readonly ReadOnlyCollection<WeaponWallContactId> processedWallContactIds;

        private WeaponRicochetRuntimeState(
            int successfulBounceCount,
            RicochetValue? remainingFixedPointBudget,
            long wallContactSimulationStep,
            IEnumerable<WeaponWallContactId> processedWallContactIds)
        {
            if (successfulBounceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(successfulBounceCount));
            }
            if (processedWallContactIds == null)
            {
                throw new ArgumentNullException(nameof(processedWallContactIds));
            }

            List<WeaponWallContactId> copy = new List<WeaponWallContactId>();
            foreach (WeaponWallContactId contactId in processedWallContactIds)
            {
                if (contactId == null)
                {
                    throw new ArgumentException(
                        "Processed wall contact identities cannot contain null values.",
                        nameof(processedWallContactIds));
                }
                copy.Add(contactId);
            }

            SuccessfulBounceCount = successfulBounceCount;
            RemainingFixedPointBudget = remainingFixedPointBudget;
            WallContactSimulationStep = wallContactSimulationStep;
            this.processedWallContactIds = copy.AsReadOnly();
        }

        public static WeaponRicochetRuntimeState Initial
        {
            get { return InitialState; }
        }

        public int SuccessfulBounceCount { get; }
        public RicochetValue? RemainingFixedPointBudget { get; }
        public bool HasCanonicalFixedPointBudget
        {
            get { return RemainingFixedPointBudget.HasValue; }
        }
        public long WallContactSimulationStep { get; }
        public IReadOnlyList<WeaponWallContactId> ProcessedWallContactIds
        {
            get { return processedWallContactIds; }
        }
        public bool HasProcessedWallContact
        {
            get { return processedWallContactIds.Count > 0; }
        }

        public bool IsDuplicateWallContact(
            long simulationStep,
            WeaponWallContactId wallContactId)
        {
            if (wallContactId == null)
            {
                throw new ArgumentNullException(nameof(wallContactId));
            }
            if (!HasProcessedWallContact || WallContactSimulationStep != simulationStep)
            {
                return false;
            }

            for (int index = 0; index < processedWallContactIds.Count; index++)
            {
                if (processedWallContactIds[index].Equals(wallContactId))
                {
                    return true;
                }
            }
            return false;
        }

        internal WeaponRicochetRuntimeState BeginCanonicalBudget(RicochetValue authoredBudget)
        {
            if (HasCanonicalFixedPointBudget)
            {
                return this;
            }
            if (SuccessfulBounceCount != 0 || HasProcessedWallContact)
            {
                throw new InvalidOperationException(
                    "Legacy ricochet runtime state cannot be reinterpreted as canonical fixed-point state.");
            }

            return new WeaponRicochetRuntimeState(
                SuccessfulBounceCount,
                authoredBudget,
                WallContactSimulationStep,
                processedWallContactIds);
        }

        internal WeaponRicochetRuntimeState AfterCanonicalWallContact(
            long simulationStep,
            WeaponWallContactId wallContactId,
            WeaponRicochetCollisionResolution resolution)
        {
            if (!HasCanonicalFixedPointBudget)
            {
                throw new InvalidOperationException(
                    "Canonical ricochet resolution requires initialized fixed-point runtime state.");
            }

            return AfterWallContactCore(
                simulationStep,
                wallContactId,
                resolution.Bounces,
                resolution.Remaining);
        }

        internal WeaponRicochetRuntimeState AfterWallContact(
            long simulationStep,
            WeaponWallContactId wallContactId,
            bool successfulBounce)
        {
            if (HasCanonicalFixedPointBudget)
            {
                throw new InvalidOperationException(
                    "Canonical fixed-point ricochet state cannot enter the legacy maximum/chance path.");
            }

            return AfterWallContactCore(
                simulationStep,
                wallContactId,
                successfulBounce,
                null);
        }

        private WeaponRicochetRuntimeState AfterWallContactCore(
            long simulationStep,
            WeaponWallContactId wallContactId,
            bool successfulBounce,
            RicochetValue? remainingFixedPointBudget)
        {
            if (simulationStep < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(simulationStep));
            }
            if (wallContactId == null)
            {
                throw new ArgumentNullException(nameof(wallContactId));
            }
            if (IsDuplicateWallContact(simulationStep, wallContactId))
            {
                return this;
            }

            List<WeaponWallContactId> nextContacts =
                WallContactSimulationStep == simulationStep
                    ? new List<WeaponWallContactId>(processedWallContactIds)
                    : new List<WeaponWallContactId>();
            nextContacts.Add(wallContactId);

            return new WeaponRicochetRuntimeState(
                checked(SuccessfulBounceCount + (successfulBounce ? 1 : 0)),
                remainingFixedPointBudget,
                simulationStep,
                nextContacts);
        }
    }
}
