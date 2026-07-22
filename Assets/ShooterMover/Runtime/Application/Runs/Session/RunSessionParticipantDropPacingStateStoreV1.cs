using System;
using ShooterMover.Application.Rewards.Drops;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Runs.Session
{
    /// <summary>
    /// Stores personal drop pacing on the exact transient run aggregate so service
    /// recreation during reconnect does not reset participant luck state.
    /// </summary>
    public sealed class RunSessionParticipantDropPacingStateStoreV1 :
        IParticipantDropPacingStateStoreV1
    {
        private readonly RunSessionAggregateV1 run;

        public RunSessionParticipantDropPacingStateStoreV1(
            RunSessionAggregateV1 run)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public bool TryLoad(
            StableId runStableId,
            int runLifecycleGeneration,
            StableId participantStableId,
            out ParticipantDropPacingStateV1 state)
        {
            return run.TryLoadRewardPacingState(
                runStableId,
                runLifecycleGeneration,
                participantStableId,
                out state);
        }

        public void Save(ParticipantDropPacingStateV1 state)
        {
            run.SaveRewardPacingState(state);
        }
    }
}
