using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Drops;

namespace ShooterMover.Application.Rewards.Drops
{
    public static class ProductionRunDropPacingCatalogV1
    {
        public static readonly StableId DefaultPolicyId = StableId.Parse("drop-pacing.default");
        public static readonly StableId NoMinimumPolicyId = StableId.Parse("drop-pacing.no-run-minimum");
        private static readonly RunDropPacingPolicyV1 DefaultPolicyValue = Create(DefaultPolicyId, 1);
        private static readonly RunDropPacingPolicyV1 NoMinimumPolicyValue = Create(NoMinimumPolicyId, 0);
        public static RunDropPacingPolicyV1 Default { get { return DefaultPolicyValue; } }
        public static RunDropPacingPolicyV1 Resolve(StableId gameModeStableId, RunDropPacingPolicyV1 missionOverride)
        {
            if (missionOverride != null) return missionOverride;
            return gameModeStableId == StableId.Parse("game-mode.no-run-minimum") ? NoMinimumPolicyValue : DefaultPolicyValue;
        }
        private static RunDropPacingPolicyV1 Create(StableId policyId, int minimumBoxes)
        {
            return new RunDropPacingPolicyV1(policyId, minimumBoxes, 10, 4000, 80000, false,
                new[] { new DropSaturationBandV1(0,1000000), new DropSaturationBandV1(1,1000000), new DropSaturationBandV1(2,750000), new DropSaturationBandV1(3,500000), new DropSaturationBandV1(4,300000), new DropSaturationBandV1(5,150000) },
                new[] { new DropSaturationBandV1(0,1000000) });
        }
    }
}
