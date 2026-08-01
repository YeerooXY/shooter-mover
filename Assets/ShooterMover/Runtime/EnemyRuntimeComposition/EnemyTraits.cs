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
    }

    public sealed partial class EnemyInstance
    {
        private readonly List<EnemyTrait> traits = new List<EnemyTrait>();

        public bool HasTrait(EnemyTrait trait)
        {
            return traits.Contains(trait);
        }

        public bool AssignTrait(EnemyTrait trait)
        {
            if (!Enum.IsDefined(typeof(EnemyTrait), trait))
                throw new ArgumentOutOfRangeException(nameof(trait));
            if (!actorState.IsActive || actorState.ProcessedEventIds.Count != 0)
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
