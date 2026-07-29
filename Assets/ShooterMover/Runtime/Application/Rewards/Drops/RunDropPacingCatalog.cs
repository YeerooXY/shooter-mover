using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Rewards.Drops
{
    public static class RunDropPacingCatalog
    {
        public static readonly StableId DefaultPolicyId = StableId.Parse("drop-pacing.default");
        public static readonly StableId NoMinimumPolicyId = StableId.Parse("drop-pacing.no-run-minimum");
        private static readonly RunDropPacingPolicy DefaultPolicyValue = Create(DefaultPolicyId, 1);
        private static readonly RunDropPacingPolicy NoMinimumPolicyValue = Create(NoMinimumPolicyId, 0);
        public static RunDropPacingPolicy Default { get { return DefaultPolicyValue; } }
        public static RunDropPacingPolicy Resolve(StableId gameModeStableId, RunDropPacingPolicy missionOverride)
        {
            if (missionOverride != null) return missionOverride;
            return gameModeStableId == StableId.Parse("game-mode.no-run-minimum") ? NoMinimumPolicyValue : DefaultPolicyValue;
        }
        private static RunDropPacingPolicy Create(StableId policyId, int minimumBoxes)
        {
            return new RunDropPacingPolicy(policyId, minimumBoxes, 10, 4000, 80000, false,
                new[] { new DropSaturationBand(0,1000000), new DropSaturationBand(1,1000000), new DropSaturationBand(2,750000), new DropSaturationBand(3,500000), new DropSaturationBand(4,300000), new DropSaturationBand(5,150000) },
                new[] { new DropSaturationBand(0,1000000) });
        }
    }
}
