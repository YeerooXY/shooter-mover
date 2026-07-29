namespace ShooterMover.Application.Guns.Execution
{
    internal static class GunFireStateCompatibility
    {
        internal static GunFiringSessionState WithTransition(
            this GunFiringSessionState state,
            int clockTicksPerSecond,
            GunFiringTrackState updatedTrack,
            GunFiringReplayRecord acceptedReplay)
        {
            return state.WithTransition(
                clockTicksPerSecond,
                GunFiringScheduler.DefaultReplayRetentionCapacity,
                updatedTrack,
                acceptedReplay);
        }
    }
}
