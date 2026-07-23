using System;
using ShooterMover.Domain.Rewards.Model;
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
    public sealed class TransactionalStrongboxGrantPayloadResolverV1 :
        IStrongboxGrantPayloadResolverV1
    {
        private readonly IStrongboxGrantPayloadResolverV1 inner;
        private readonly GeneratedEquipmentAugmentSignatureAuthorityV1 signatures;

        public TransactionalStrongboxGrantPayloadResolverV1(
            IStrongboxGrantPayloadResolverV1 inner,
            GeneratedEquipmentAugmentSignatureAuthorityV1 signatures)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.signatures = signatures
                ?? throw new ArgumentNullException(nameof(signatures));
        }

        public StrongboxGrantPayloadResolutionV1 Resolve(
            StrongboxDefinitionV1 definition,
            StrongboxInstanceContextV1 boxContext,
            RewardOperationRequestV1 operation,
            RewardResultV1 rewardResult)
        {
            lock (signatures)
            {
                GeneratedEquipmentAugmentSignatureSnapshotV1 before =
                    signatures.ExportDurableSnapshot();
                StrongboxGrantPayloadResolutionV1 result;
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

        private StrongboxGrantPayloadResolutionV1 RestoreAndReject(
            GeneratedEquipmentAugmentSignatureSnapshotV1 before,
            string rejectionCode)
        {
            try
            {
                signatures.RestoreDurableSnapshot(before);
            }
            catch (Exception exception)
            {
                return StrongboxGrantPayloadResolutionV1.Rejected(
                    (string.IsNullOrWhiteSpace(rejectionCode)
                        ? "strongbox-payload-resolution-rejected"
                        : rejectionCode)
                    + ";signature-rollback-exception="
                    + exception.GetType().Name.ToLowerInvariant());
            }
            return StrongboxGrantPayloadResolutionV1.Rejected(
                string.IsNullOrWhiteSpace(rejectionCode)
                    ? "strongbox-payload-resolution-rejected"
                    : rejectionCode);
        }
    }
}
