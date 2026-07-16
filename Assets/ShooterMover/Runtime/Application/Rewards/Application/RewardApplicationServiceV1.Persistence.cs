using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;

namespace ShooterMover.Application.Rewards.Application
{
    public sealed partial class RewardApplicationServiceV1
    {
        public RewardApplicationSnapshotV1 ExportSnapshot()
        {
            lock (sync)
            {
                var snapshots = new List<RewardCommitmentSnapshotV1>(commitments.Count);
                foreach (KeyValuePair<StableId, CommitmentRecord> pair in commitments)
                {
                    snapshots.Add(pair.Value.ToSnapshot());
                }

                snapshots.Sort();
                return RewardApplicationSnapshotV1.CreateCanonical(
                    AuthorityStableId,
                    sequence,
                    snapshots);
            }
        }

        public RewardApplicationImportResultV1 ImportSnapshot(
            RewardApplicationSnapshotV1 snapshot)
        {
            lock (sync)
            {
                RewardApplicationImportStatusV1 status;
                string rejectionCode;
                Dictionary<StableId, CommitmentRecord> importedCommitments;
                Dictionary<StableId, IdentityRecord> importedSources;
                Dictionary<StableId, IdentityRecord> importedProjections;
                Dictionary<StableId, IdentityRecord> importedClaims;
                Dictionary<StableId, IdentityRecord> importedCancellations;
                if (!TryValidateSnapshot(
                    snapshot,
                    out status,
                    out rejectionCode,
                    out importedCommitments,
                    out importedSources,
                    out importedProjections,
                    out importedClaims,
                    out importedCancellations))
                {
                    return new RewardApplicationImportResultV1(
                        status,
                        rejectionCode,
                        sequence);
                }

                commitments = importedCommitments;
                sourceOperations = importedSources;
                projections = importedProjections;
                claims = importedClaims;
                cancellations = importedCancellations;
                sequence = snapshot.Sequence;
                return new RewardApplicationImportResultV1(
                    RewardApplicationImportStatusV1.Imported,
                    null,
                    sequence);
            }
        }

        private bool TryValidateSnapshot(
            RewardApplicationSnapshotV1 snapshot,
            out RewardApplicationImportStatusV1 status,
            out string rejectionCode,
            out Dictionary<StableId, CommitmentRecord> importedCommitments,
            out Dictionary<StableId, IdentityRecord> importedSources,
            out Dictionary<StableId, IdentityRecord> importedProjections,
            out Dictionary<StableId, IdentityRecord> importedClaims,
            out Dictionary<StableId, IdentityRecord> importedCancellations)
        {
            importedCommitments = null;
            importedSources = null;
            importedProjections = null;
            importedClaims = null;
            importedCancellations = null;
            if (snapshot == null)
            {
                status = RewardApplicationImportStatusV1.SnapshotRejected;
                rejectionCode = "snapshot-null";
                return false;
            }

            if (snapshot.SchemaVersion != RewardApplicationSnapshotV1.CurrentSchemaVersion)
            {
                status = RewardApplicationImportStatusV1.UnsupportedSchemaVersion;
                rejectionCode = "snapshot-schema-unsupported";
                return false;
            }

            if (snapshot.AuthorityStableId != AuthorityStableId)
            {
                status = RewardApplicationImportStatusV1.AuthorityMismatch;
                rejectionCode = "snapshot-authority-mismatch";
                return false;
            }

            if (snapshot.Sequence < 0L)
            {
                status = RewardApplicationImportStatusV1.SnapshotRejected;
                rejectionCode = "snapshot-sequence-negative";
                return false;
            }

            if (!RewardApplicationCanonicalV1.IsCanonicalFingerprint(snapshot.Fingerprint)
                || !string.Equals(
                    snapshot.Fingerprint,
                    RewardApplicationSnapshotV1.ComputeFingerprint(snapshot),
                    StringComparison.Ordinal))
            {
                status = RewardApplicationImportStatusV1.FingerprintMismatch;
                rejectionCode = "snapshot-fingerprint-mismatch";
                return false;
            }

            var candidateCommitments = new Dictionary<StableId, CommitmentRecord>();
            var candidateSources = new Dictionary<StableId, IdentityRecord>();
            var candidateProjections = new Dictionary<StableId, IdentityRecord>();
            var candidateClaims = new Dictionary<StableId, IdentityRecord>();
            var candidateCancellations = new Dictionary<StableId, IdentityRecord>();
            for (int index = 0; index < snapshot.Commitments.Count; index++)
            {
                RewardCommitmentSnapshotV1 commitment = snapshot.Commitments[index];
                if (!TryValidateCommitmentSnapshot(
                    commitment,
                    candidateCommitments,
                    candidateSources,
                    candidateProjections,
                    candidateClaims,
                    candidateCancellations,
                    out rejectionCode))
                {
                    status = RewardApplicationImportStatusV1.SnapshotRejected;
                    return false;
                }
            }

            long minimumSequence;
            if (!TryComputeMinimumSequence(snapshot, out minimumSequence)
                || snapshot.Sequence < minimumSequence
                || (snapshot.Commitments.Count == 0 && snapshot.Sequence != 0L))
            {
                status = RewardApplicationImportStatusV1.SnapshotRejected;
                rejectionCode = "snapshot-sequence-inconsistent";
                return false;
            }

            importedCommitments = candidateCommitments;
            importedSources = candidateSources;
            importedProjections = candidateProjections;
            importedClaims = candidateClaims;
            importedCancellations = candidateCancellations;
            status = RewardApplicationImportStatusV1.Imported;
            rejectionCode = null;
            return true;
        }

        private bool TryValidateCommitmentSnapshot(
            RewardCommitmentSnapshotV1 snapshot,
            Dictionary<StableId, CommitmentRecord> candidateCommitments,
            Dictionary<StableId, IdentityRecord> candidateSources,
            Dictionary<StableId, IdentityRecord> candidateProjections,
            Dictionary<StableId, IdentityRecord> candidateClaims,
            Dictionary<StableId, IdentityRecord> candidateCancellations,
            out string rejectionCode)
        {
            if (snapshot == null || snapshot.CommitCommand == null)
            {
                rejectionCode = "commitment-snapshot-null";
                return false;
            }

            if (!RewardApplicationCanonicalV1.IsCanonicalFingerprint(snapshot.Fingerprint)
                || !string.Equals(
                    snapshot.Fingerprint,
                    RewardCommitmentSnapshotV1.ComputeFingerprint(snapshot),
                    StringComparison.Ordinal))
            {
                rejectionCode = "commitment-fingerprint-mismatch";
                return false;
            }

            StableId commitmentId = snapshot.CommitCommand.CommitmentStableId;
            if (candidateCommitments.ContainsKey(commitmentId))
            {
                rejectionCode = "commitment-duplicate";
                return false;
            }

            StableId sourceOperationId = snapshot.CommitCommand.SourceOperationStableId;
            if (candidateSources.ContainsKey(sourceOperationId))
            {
                rejectionCode = "source-operation-duplicate";
                return false;
            }

            for (int index = 0; index < snapshot.Projections.Count; index++)
            {
                RewardProjectCommandV1 projection = snapshot.Projections[index];
                if (projection.CommitmentStableId != commitmentId
                    || candidateProjections.ContainsKey(projection.ProjectionStableId))
                {
                    rejectionCode = "projection-snapshot-invalid";
                    return false;
                }
            }

            bool requiresClaim = snapshot.State == RewardCommitmentStateV1.Claimed
                || snapshot.State == RewardCommitmentStateV1.Applied;
            if (requiresClaim != (snapshot.ClaimCommand != null))
            {
                rejectionCode = "claim-state-shape-invalid";
                return false;
            }

            if (snapshot.ClaimCommand != null)
            {
                if (snapshot.ClaimCommand.CommitmentStableId != commitmentId
                    || candidateClaims.ContainsKey(snapshot.ClaimCommand.ClaimStableId))
                {
                    rejectionCode = "claim-snapshot-invalid";
                    return false;
                }

                if (snapshot.ClaimCommand.MoneyAuthorityStableId
                        != moneyAuthority.AuthorityStableId
                    || snapshot.ClaimCommand.ScrapAuthorityStableId
                        != scrapAuthority.AuthorityStableId
                    || snapshot.ClaimCommand.HoldingsAuthorityStableId
                        != holdingsAuthority.AuthorityStableId)
                {
                    rejectionCode = "claim-authority-snapshot-mismatch";
                    return false;
                }

                List<RewardChildGrantCommandV1> expected = BuildChildPlan(
                    snapshot.CommitCommand,
                    snapshot.ClaimCommand);
                if (expected.Count != snapshot.Children.Count)
                {
                    rejectionCode = "child-count-mismatch";
                    return false;
                }

                var expectedById = new Dictionary<StableId, RewardChildGrantCommandV1>();
                for (int index = 0; index < expected.Count; index++)
                {
                    expectedById.Add(expected[index].TransactionStableId, expected[index]);
                }

                bool allApplied = true;
                for (int index = 0; index < snapshot.Children.Count; index++)
                {
                    RewardChildApplicationSnapshotV1 child = snapshot.Children[index];
                    RewardChildGrantCommandV1 planned;
                    if (!expectedById.TryGetValue(
                        child.Command.TransactionStableId,
                        out planned)
                        || !planned.Equals(child.Command))
                    {
                        rejectionCode = "child-command-mismatch";
                        return false;
                    }

                    if (child.ResolutionState == RewardChildResolutionStateV1.Applied)
                    {
                        if (!child.LastApplyStatus.HasValue
                            || (child.LastApplyStatus.Value
                                    != RewardChildApplyStatusV1.Applied
                                && child.LastApplyStatus.Value
                                    != RewardChildApplyStatusV1.ExactDuplicateNoChange))
                        {
                            rejectionCode = "applied-child-terminal-fact-invalid";
                            return false;
                        }
                    }
                    else
                    {
                        allApplied = false;
                        if (!child.LastApplyStatus.HasValue
                            || child.LastApplyStatus.Value
                                == RewardChildApplyStatusV1.Applied)
                        {
                            rejectionCode = "pending-child-terminal-fact-invalid";
                            return false;
                        }
                    }
                }

                if (snapshot.State == RewardCommitmentStateV1.Applied && !allApplied)
                {
                    rejectionCode = "applied-commitment-has-pending-child";
                    return false;
                }

                if (snapshot.State == RewardCommitmentStateV1.Claimed && allApplied)
                {
                    rejectionCode = "claimed-commitment-has-no-pending-child";
                    return false;
                }
            }
            else if (snapshot.Children.Count != 0)
            {
                rejectionCode = "unclaimed-commitment-has-children";
                return false;
            }

            if (snapshot.State == RewardCommitmentStateV1.Generated
                && snapshot.Projections.Count != 0)
            {
                rejectionCode = "generated-commitment-has-projection";
                return false;
            }

            if (snapshot.State == RewardCommitmentStateV1.Projected
                && snapshot.Projections.Count == 0)
            {
                rejectionCode = "projected-commitment-has-no-projection";
                return false;
            }

            bool cancelled = snapshot.State == RewardCommitmentStateV1.Cancelled;
            if (cancelled != (snapshot.CancelCommand != null))
            {
                rejectionCode = "cancellation-state-shape-invalid";
                return false;
            }

            if (snapshot.CancelCommand != null)
            {
                if (snapshot.CancelCommand.CommitmentStableId != commitmentId
                    || candidateCancellations.ContainsKey(
                        snapshot.CancelCommand.CancellationStableId))
                {
                    rejectionCode = "cancellation-snapshot-invalid";
                    return false;
                }
            }

            var record = CommitmentRecord.FromSnapshot(snapshot);
            OrderChildrenForExecution(record.Children);
            candidateCommitments.Add(commitmentId, record);
            candidateSources.Add(
                sourceOperationId,
                new IdentityRecord(commitmentId, snapshot.CommitCommand.Fingerprint));
            for (int index = 0; index < snapshot.Projections.Count; index++)
            {
                RewardProjectCommandV1 projection = snapshot.Projections[index];
                candidateProjections.Add(
                    projection.ProjectionStableId,
                    new IdentityRecord(commitmentId, projection.Fingerprint));
            }

            if (snapshot.ClaimCommand != null)
            {
                candidateClaims.Add(
                    snapshot.ClaimCommand.ClaimStableId,
                    new IdentityRecord(commitmentId, snapshot.ClaimCommand.Fingerprint));
            }

            if (snapshot.CancelCommand != null)
            {
                candidateCancellations.Add(
                    snapshot.CancelCommand.CancellationStableId,
                    new IdentityRecord(commitmentId, snapshot.CancelCommand.Fingerprint));
            }

            rejectionCode = null;
            return true;
        }

        private static bool TryComputeMinimumSequence(
            RewardApplicationSnapshotV1 snapshot,
            out long minimumSequence)
        {
            try
            {
                long result = 0L;
                for (int index = 0; index < snapshot.Commitments.Count; index++)
                {
                    RewardCommitmentSnapshotV1 commitment = snapshot.Commitments[index];
                    result = checked(result + 1L);
                    result = checked(result + commitment.Projections.Count);
                    if (commitment.ClaimCommand != null)
                    {
                        result = checked(result + 1L);
                    }

                    if (commitment.CancelCommand != null)
                    {
                        result = checked(result + 1L);
                    }

                    if (commitment.State == RewardCommitmentStateV1.Applied)
                    {
                        result = checked(result + 1L);
                    }

                    for (int childIndex = 0;
                        childIndex < commitment.Children.Count;
                        childIndex++)
                    {
                        RewardChildApplicationSnapshotV1 child =
                            commitment.Children[childIndex];
                        if (child.ResolutionState == RewardChildResolutionStateV1.Pending
                            && child.LastApplyStatus.HasValue)
                        {
                            result = checked(result + 1L);
                        }
                    }
                }

                minimumSequence = result;
                return true;
            }
            catch (OverflowException)
            {
                minimumSequence = 0L;
                return false;
            }
        }
    }
}
