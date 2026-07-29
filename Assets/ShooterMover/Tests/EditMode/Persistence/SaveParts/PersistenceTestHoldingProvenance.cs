using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Persistence.SaveParts
{
    internal static class HoldingProvenance
    {
        public static ShooterMover.Domain.Holdings.HoldingProvenance Create(
            StableId grantStableId,
            StableId sourceStableId)
        {
            return ShooterMover.Domain.Holdings.HoldingProvenance.Create(
                grantStableId,
                sourceStableId);
        }
    }
}
