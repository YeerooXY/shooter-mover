using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    /// <summary>
    /// Immutable run-local eligibility state for one personal reward participant.
    /// Networking may update these facts without changing reward-generation code.
    /// </summary>
    public sealed class RunRewardParticipantState :
        IComparable<RunRewardParticipantState>
    {
        private readonly string canonicalText;

        public RunRewardParticipantState(
            StableId participantStableId,
            int playerLevel,
            bool activeInRun,
            bool connectedOrReconnectReserved,
            bool presentInCurrentRoom,
            bool contributionEligible,
            bool spectator)
        {
            ParticipantStableId = participantStableId
                ?? throw new ArgumentNullException(nameof(participantStableId));
            if (playerLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerLevel));
            }
            PlayerLevel = playerLevel;
            ActiveInRun = activeInRun;
            ConnectedOrReconnectReserved = connectedOrReconnectReserved;
            PresentInCurrentRoom = presentInCurrentRoom;
            ContributionEligible = contributionEligible;
            Spectator = spectator;

            var builder = new StringBuilder(
                "schema=run-reward-participant-state-v1");
            builder.Append("\nparticipant_id=").Append(ParticipantStableId)
                .Append("\nplayer_level=")
                .Append(PlayerLevel.ToString(CultureInfo.InvariantCulture))
                .Append("\nactive_in_run=").Append(ActiveInRun ? "1" : "0")
                .Append("\nconnected_or_reserved=")
                .Append(ConnectedOrReconnectReserved ? "1" : "0")
                .Append("\npresent_in_room=")
                .Append(PresentInCurrentRoom ? "1" : "0")
                .Append("\ncontribution_eligible=")
                .Append(ContributionEligible ? "1" : "0")
                .Append("\nspectator=").Append(Spectator ? "1" : "0");
            canonicalText = builder.ToString();
            Fingerprint = RunSessionFingerprint.Hash(canonicalText);
        }

        public StableId ParticipantStableId { get; }
        public int PlayerLevel { get; }
        public bool ActiveInRun { get; }
        public bool ConnectedOrReconnectReserved { get; }
        public bool PresentInCurrentRoom { get; }
        public bool ContributionEligible { get; }
        public bool Spectator { get; }
        public string Fingerprint { get; }

        public int CompareTo(RunRewardParticipantState other)
        {
            return ReferenceEquals(other, null)
                ? 1
                : ParticipantStableId.CompareTo(other.ParticipantStableId);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }
    }
}
