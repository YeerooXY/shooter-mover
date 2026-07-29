using System;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Generation;

namespace ShooterMover.Domain.Rewards.Drops
{
    /// <summary>Immutable per-participant run pacing snapshot for reconnect and host migration.</summary>
    public sealed class ParticipantDropPacingState : IEquatable<ParticipantDropPacingState>
    {
        private readonly string canonicalText;
        public ParticipantDropPacingState(StableId participantStableId, StableId runStableId, int runLifecycleGeneration, StableId roomStableId, int roomLifecycleGeneration, int consecutiveEligibleRandomBoxFailures, int randomBoxesInCurrentRoom, int randomBoxesInRun, int guaranteedBoxesInRun)
        {
            ParticipantStableId = participantStableId ?? throw new ArgumentNullException(nameof(participantStableId));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            RoomStableId = roomStableId ?? throw new ArgumentNullException(nameof(roomStableId));
            if (runLifecycleGeneration < 1 || roomLifecycleGeneration < 1 || consecutiveEligibleRandomBoxFailures < 0 || randomBoxesInCurrentRoom < 0 || randomBoxesInRun < 0 || guaranteedBoxesInRun < 0) throw new ArgumentOutOfRangeException(nameof(runLifecycleGeneration));
            RunLifecycleGeneration = runLifecycleGeneration;
            RoomLifecycleGeneration = roomLifecycleGeneration;
            ConsecutiveEligibleRandomBoxFailures = consecutiveEligibleRandomBoxFailures;
            RandomBoxesInCurrentRoom = randomBoxesInCurrentRoom;
            RandomBoxesInRun = randomBoxesInRun;
            GuaranteedBoxesInRun = guaranteedBoxesInRun;
            var builder = new StringBuilder("schema=participant-drop-pacing-state-v1");
            builder.Append("\nparticipant_id=").Append(ParticipantStableId).Append("\nrun_id=").Append(RunStableId)
                .Append("\nrun_lifecycle=").Append(RunLifecycleGeneration.ToString(CultureInfo.InvariantCulture))
                .Append("\nroom_id=").Append(RoomStableId).Append("\nroom_lifecycle=").Append(RoomLifecycleGeneration.ToString(CultureInfo.InvariantCulture))
                .Append("\nconsecutive_random_box_failures=").Append(ConsecutiveEligibleRandomBoxFailures.ToString(CultureInfo.InvariantCulture))
                .Append("\nrandom_boxes_in_room=").Append(RandomBoxesInCurrentRoom.ToString(CultureInfo.InvariantCulture))
                .Append("\nrandom_boxes_in_run=").Append(RandomBoxesInRun.ToString(CultureInfo.InvariantCulture))
                .Append("\nguaranteed_boxes_in_run=").Append(GuaranteedBoxesInRun.ToString(CultureInfo.InvariantCulture));
            canonicalText = builder.ToString();
            Fingerprint = RewardGenerationFingerprint.Compute(canonicalText);
        }
        public StableId ParticipantStableId { get; }
        public StableId RunStableId { get; }
        public int RunLifecycleGeneration { get; }
        public StableId RoomStableId { get; }
        public int RoomLifecycleGeneration { get; }
        public int ConsecutiveEligibleRandomBoxFailures { get; }
        public int RandomBoxesInCurrentRoom { get; }
        public int RandomBoxesInRun { get; }
        public int GuaranteedBoxesInRun { get; }
        public int TotalBoxesInRun { get { return RandomBoxesInRun + GuaranteedBoxesInRun; } }
        public string Fingerprint { get; }
        public static ParticipantDropPacingState Start(StableId participantStableId, StableId runStableId, int runLifecycleGeneration, StableId roomStableId, int roomLifecycleGeneration) { return new ParticipantDropPacingState(participantStableId, runStableId, runLifecycleGeneration, roomStableId, roomLifecycleGeneration, 0, 0, 0, 0); }
        public ParticipantDropPacingState EnterRoom(StableId roomStableId, int roomLifecycleGeneration)
        {
            if (roomStableId == RoomStableId && roomLifecycleGeneration == RoomLifecycleGeneration) return this;
            return new ParticipantDropPacingState(ParticipantStableId, RunStableId, RunLifecycleGeneration, roomStableId, roomLifecycleGeneration, ConsecutiveEligibleRandomBoxFailures, 0, RandomBoxesInRun, GuaranteedBoxesInRun);
        }
        public ParticipantDropPacingState RecordRandomAttempt(bool succeeded, int generatedBoxCount)
        {
            if (generatedBoxCount < 0 || (succeeded != (generatedBoxCount > 0))) throw new ArgumentOutOfRangeException(nameof(generatedBoxCount));
            return new ParticipantDropPacingState(ParticipantStableId, RunStableId, RunLifecycleGeneration, RoomStableId, RoomLifecycleGeneration, succeeded ? 0 : checked(ConsecutiveEligibleRandomBoxFailures + 1), checked(RandomBoxesInCurrentRoom + generatedBoxCount), checked(RandomBoxesInRun + generatedBoxCount), GuaranteedBoxesInRun);
        }
        public ParticipantDropPacingState RecordGuaranteedBoxes(int generatedBoxCount, bool resetPity)
        {
            if (generatedBoxCount < 0) throw new ArgumentOutOfRangeException(nameof(generatedBoxCount));
            return new ParticipantDropPacingState(ParticipantStableId, RunStableId, RunLifecycleGeneration, RoomStableId, RoomLifecycleGeneration, resetPity ? 0 : ConsecutiveEligibleRandomBoxFailures, RandomBoxesInCurrentRoom, RandomBoxesInRun, checked(GuaranteedBoxesInRun + generatedBoxCount));
        }
        public string ToCanonicalString() { return canonicalText; }
        public bool Equals(ParticipantDropPacingState other) { return !ReferenceEquals(other, null) && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal); }
        public override bool Equals(object obj) { return Equals(obj as ParticipantDropPacingState); }
        public override int GetHashCode() { return StringComparer.Ordinal.GetHashCode(canonicalText); }
    }
}
