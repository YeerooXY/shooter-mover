using System;

namespace ShooterMover.Domain.Progression.Skills
{
    public static class MedicHealing
    {
        public const string SkillId = "medic.healing";
        public const int BaseHealth = 25;
        public const int HealthPerRank = 25;

        public static int HealthAtRank(int rank)
        {
            if (rank < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rank));
            }

            return checked(BaseHealth + rank * HealthPerRank);
        }
    }
}
