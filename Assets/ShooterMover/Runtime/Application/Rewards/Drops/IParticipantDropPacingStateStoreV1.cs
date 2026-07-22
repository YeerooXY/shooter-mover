using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Rewards.Drops
{
    /// <summary>
    /// Run-local persistence boundary for personal drop pacing. Implementations may
    /// retain the state in a live run aggregate, reconnect snapshot or host-migration
    /// payload; permanent character state must not own it.
    /// </summary>
    public interface IParticipantDropPacingStateStoreV1
    {
        bool TryLoad(
            StableId runStableId,
            int runLifecycleGeneration,
            StableId participantStableId,
            out ParticipantDropPacingStateV1 state);

        void Save(ParticipantDropPacingStateV1 state);
    }
}
