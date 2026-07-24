using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShooterMover.Application.Weapons.Execution
{
    /// <summary>
    /// Immutable per-projectile ricochet state. It is deliberately supplied by the caller so
    /// impact decisions remain pure and do not require a registry or another runtime service.
    /// </summary>
    public sealed class WeaponRicochetRuntimeState
    {
        private static readonly WeaponRicochetRuntimeState InitialState =
            new WeaponRicochetRuntimeState(
                0,
                -1L,
                new WeaponWallContactId[0]);

        private readonly ReadOnlyCollection<WeaponWallContactId> processedWallContactIds;

        private WeaponRicochetRuntimeState(
            int successfulBounceCount,
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
            WallContactSimulationStep = wallContactSimulationStep;
            this.processedWallContactIds = copy.AsReadOnly();
        }

        public static WeaponRicochetRuntimeState Initial
        {
            get { return InitialState; }
        }

        public int SuccessfulBounceCount { get; }
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

        internal WeaponRicochetRuntimeState AfterWallContact(
            long simulationStep,
            WeaponWallContactId wallContactId,
            bool successfulBounce)
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
                simulationStep,
                nextContacts);
        }
    }
}
