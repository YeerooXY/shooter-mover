using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.EditorTools.BalanceSimulator
{
    public sealed class RewardSimulationParticipantInput :
        IComparable<RewardSimulationParticipantInput>
    {
        public RewardSimulationParticipantInput(
            StableId participantStableId,
            int playerLevel)
        {
            ParticipantStableId = participantStableId
                ?? throw new ArgumentNullException(nameof(participantStableId));
            if (playerLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }
            PlayerLevel = playerLevel;
        }

        public StableId ParticipantStableId { get; }
        public int PlayerLevel { get; }

        public int CompareTo(RewardSimulationParticipantInput other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : ParticipantStableId.CompareTo(other.ParticipantStableId);
        }
    }
}
