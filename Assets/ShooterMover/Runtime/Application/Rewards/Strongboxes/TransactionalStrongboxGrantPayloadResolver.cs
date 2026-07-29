using System;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    /// <summary>
    /// Makes all staged augment-signature changes from one complete payload-resolution
    /// call transactional. A rejected or exceptional resolution restores the exact prior
    /// committed/staged snapshot, so preparation cannot leak orphan opening intent.
    /// RAP equipment application uses the same authority monitor while committing staged
    /// metadata, preventing rollback from racing a successful equipment grant.
    /// </summary>
    public sealed class TransactionalStrongboxGrantPayloadResolver :
        IStrongboxGrantPayloadResolver
    {
        private readonly IStrongboxGrantPayloadResolver inner;
        private readonly GeneratedEquipmentAugmentSignatureState signatures;

        public TransactionalStrongboxGrantPayloadResolver(
            IStrongboxGrantPayloadResolver inner,
            GeneratedEquipmentAugmentSignatureState signatures)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.signatures = signatures
                ?? throw new ArgumentNullException(nameof(signatures));
        }

        public StrongboxGrantPayloadResolution Resolve(
            StrongboxDefinition definition,
            StrongboxInstanceContext boxContext,
            RewardOperationRequest operation,
            RewardResult rewardResult)
        {
            lock (signatures)
            {
                GeneratedEquipmentAugmentSignatureSnapshot before =
                    signatures.ExportDurableSnapshot();
                StrongboxGrantPayloadResolution result;
                try
                {
                    result = inner.Resolve(
                        definition,
                        boxContext,
                        operation,
                        rewardResult);
                }
                catch (Exception exception)
                {
                    return RestoreAndReject(
                        before,
                        "strongbox-payload-resolution-exception-"
                            + exception.GetType().Name.ToLowerInvariant());
                }

                if (result != null && result.Succeeded)
                {
                    return result;
                }
                return RestoreAndReject(
                    before,
                    result == null
                        ? "strongbox-payload-resolution-result-null"
                        : result.RejectionCode);
            }
        }

        private StrongboxGrantPayloadResolution RestoreAndReject(
            GeneratedEquipmentAugmentSignatureSnapshot before,
            string rejectionCode)
        {
            try
            {
                signatures.RestoreDurableSnapshot(before);
            }
            catch (Exception exception)
            {
                return StrongboxGrantPayloadResolution.Rejected(
                    (string.IsNullOrWhiteSpace(rejectionCode)
                        ? "strongbox-payload-resolution-rejected"
                        : rejectionCode)
                    + ";signature-rollback-exception="
                    + exception.GetType().Name.ToLowerInvariant());
            }
            return StrongboxGrantPayloadResolution.Rejected(
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "strongbox-payload-resolution-rejected"
                    : rejectionCode);
        }
    }
}
