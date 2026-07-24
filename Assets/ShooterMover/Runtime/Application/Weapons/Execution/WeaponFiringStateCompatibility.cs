namespace ShooterMover.Application.Weapons.Execution
{
    internal static class WeaponFiringStateCompatibility
    {
        internal static WeaponFiringSessionState WithTransition(
            this WeaponFiringSessionState state,
            int clockTicksPerSecond,
            WeaponFiringTrackState updatedTrack,
            WeaponFiringReplayRecord acceptedReplay)
        {
            return state.WithTransition(
                clockTicksPerSecond,
                WeaponFiringScheduler.DefaultReplayRetentionCapacity,
                updatedTrack,
                acceptedReplay);
        }
    }
}
