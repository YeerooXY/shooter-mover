using System;
using ShooterMover.Domain.Common;

namespace ShooterMover.EditorTools.BalanceSimulator
{
    public sealed class RewardSimulationParticipantInputV1 :
        IComparable<RewardSimulationParticipantInputV1>
    {
        public RewardSimulationParticipantInputV1(
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

        public int CompareTo(RewardSimulationParticipantInputV1 other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : ParticipantStableId.CompareTo(other.ParticipantStableId);
        }
    }
}
