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
    public sealed class RunSessionParticipantDropPacingStateStore :
        IParticipantDropPacingStateStore
    {
        private readonly RunSessionAggregate run;

        public RunSessionParticipantDropPacingStateStore(
            RunSessionAggregate run)
        {
            this.run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public bool TryLoad(
            StableId runStableId,
            int runLifecycleGeneration,
            StableId participantStableId,
            out ParticipantDropPacingState state)
        {
            return run.TryLoadRewardPacingState(
                runStableId,
                runLifecycleGeneration,
                participantStableId,
                out state);
        }

        public void Save(ParticipantDropPacingState state)
        {
            run.SaveRewardPacingState(state);
        }
    }
}
