using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Persistence.Components
{
    internal static class HoldingProvenanceV1
    {
        public static ShooterMover.Domain.Holdings.HoldingProvenanceV1 Create(
            StableId grantStableId,
            StableId sourceStableId)
        {
            return ShooterMover.Domain.Holdings.HoldingProvenanceV1.Create(
                grantStableId,
                sourceStableId);
        }
    }
}
