using System;
using System.Collections.Generic;
using ShooterMover.Domain.Enemies;

namespace ShooterMover.EnemyRuntimeComposition
{
    public enum EnemyTrait
    {
        EnergyShielded = 1,
        Fortified = 2,
        Golden = 3,
        Swift = 4,
        Overclocked = 5,
        Volatile = 6,
    }

    public sealed partial class EnemyInstance
    {
        public const double VolatileDamage = 20d;
        public const double VolatileRadius = 2d;

        private readonly List<EnemyTrait> traits = new List<EnemyTrait>();

        public double MovementSpeedMultiplier
        {
            get { return HasTrait(EnemyTrait.Swift) ? 1.25d : 1d; }
        }

        public double AttackCooldownMultiplier
        {
            get { return HasTrait(EnemyTrait.Overclocked) ? 0.75d : 1d; }
        }

        public bool HasTrait(EnemyTrait trait)
        {
            return traits.Contains(trait);
        }

        public bool AssignTrait(EnemyTrait trait)
        {
            if (!Enum.IsDefined(typeof(EnemyTrait), trait))
                throw new ArgumentOutOfRangeException(nameof(trait));
            if (!actorState.IsActive
                || actorState.ProcessedEventIds.Count != 0
                || issuedDecisions.Count != 0
                || attackReplay.Count != 0)
            {
                throw new InvalidOperationException(
                    "Enemy traits must be assigned before combat begins.");
            }
            if (traits.Contains(trait)) return false;

            if (trait == EnemyTrait.Fortified)
            {
                actorState = EnemyActorState.Create(
                    actorState.ActorId,
                    actorState.RoleId,
                    actorState.MaximumHealth * 2d,
                    actorState.WeightClassValue,
                    actorState.ContactPolicy);
            }

            traits.Add(trait);
            traits.Sort();
            return true;
        }
    }
}
