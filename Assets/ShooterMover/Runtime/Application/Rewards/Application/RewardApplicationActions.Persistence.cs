using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;

namespace ShooterMover.Application.Rewards.Application
{
    public sealed partial class RewardApplicationActions
    {
        public RewardApplicationSnapshot ExportSnapshot()
        {
            lock (sync)
            {
                var snapshots = new List<RewardCommitmentSnapshot>(commitments.Count);
                foreach (KeyValuePair<StableId, CommitmentRecord> pair in commitments)
                {
                    snapshots.Add(pair.Value.ToSnapshot());
                }

                snapshots.Sort();
                return RewardApplicationSnapshot.CreateCanonical(
                    AuthorityStableId,
                    sequence,
                    snapshots);
            }
        }

        public RewardApplicationImportResult ImportSnapshot(
            RewardApplicationSnapshot snapshot)
        {
            lock (sync)
            {
                RewardApplicationImportStatus status;
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
                    return new RewardApplicationImportResult(
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
                return new RewardApplicationImportResult(
                    RewardApplicationImportStatus.Imported,
                    null,
                    sequence);
            }
        }

        private bool TryValidateSnapshot(
            RewardApplicationSnapshot snapshot,
            out RewardApplicationImportStatus status,
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
                status = RewardApplicationImportStatus.SnapshotRejected;
                rejectionCode = "snapshot-null";
                return false;
            }

            if (snapshot.SchemaVersion != RewardApplicationSnapshot.CurrentSchemaVersion)
            {
                status = RewardApplicationImportStatus.UnsupportedSchemaVersion;
                rejectionCode = "snapshot-schema-unsupported";
                return false;
            }

            if (snapshot.AuthorityStableId != AuthorityStableId)
            {
                status = RewardApplicationImportStatus.AuthorityMismatch;
                rejectionCode = "snapshot-authority-mismatch";
                return false;
            }

            if (snapshot.Sequence < 0L)
            {
                status = RewardApplicationImportStatus.SnapshotRejected;
                rejectionCode = "snapshot-sequence-negative";
                return false;
            }

            if (!RewardApplication.IsCanonicalFingerprint(snapshot.Fingerprint)
                || !string.Equals(
                    snapshot.Fingerprint,
                    RewardApplicationSnapshot.ComputeFingerprint(snapshot),
                    StringComparison.Ordinal))
            {
                status = RewardApplicationImportStatus.FingerprintMismatch;
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
                RewardCommitmentSnapshot commitment = snapshot.Commitments[index];
                if (!TryValidateCommitmentSnapshot(
                    commitment,
                    candidateCommitments,
                    candidateSources,
                    candidateProjections,
                    candidateClaims,
                    candidateCancellations,
                    out rejectionCode))
                {
                    status = RewardApplicationImportStatus.SnapshotRejected;
                    return false;
                }
            }

            long minimumSequence;
            if (!TryComputeMinimumSequence(snapshot, out minimumSequence)
                || snapshot.Sequence < minimumSequence
                || (snapshot.Commitments.Count == 0 && snapshot.Sequence != 0L))
            {
                status = RewardApplicationImportStatus.SnapshotRejected;
                rejectionCode = "snapshot-sequence-inconsistent";
                return false;
            }

            importedCommitments = candidateCommitments;
            importedSources = candidateSources;
            importedProjections = candidateProjections;
            importedClaims = candidateClaims;
            importedCancellations = candidateCancellations;
            status = RewardApplicationImportStatus.Imported;
            rejectionCode = null;
            return true;
        }

        private bool TryValidateCommitmentSnapshot(
            RewardCommitmentSnapshot snapshot,
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

            if (!RewardApplication.IsCanonicalFingerprint(snapshot.Fingerprint)
                || !string.Equals(
                    snapshot.Fingerprint,
                    RewardCommitmentSnapshot.ComputeFingerprint(snapshot),
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
                RewardProjectCommand projection = snapshot.Projections[index];
                if (projection.CommitmentStableId != commitmentId
                    || candidateProjections.ContainsKey(projection.ProjectionStableId))
                {
                    rejectionCode = "projection-snapshot-invalid";
                    return false;
                }
            }

            bool requiresClaim = snapshot.State == RewardCommitmentState.Claimed
                || snapshot.State == RewardCommitmentState.Applied;
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

                List<RewardChildGrantCommand> expected = BuildChildPlan(
                    snapshot.CommitCommand,
                    snapshot.ClaimCommand);
                if (expected.Count != snapshot.Children.Count)
                {
                    rejectionCode = "child-count-mismatch";
                    return false;
                }

                var expectedById = new Dictionary<StableId, RewardChildGrantCommand>();
                for (int index = 0; index < expected.Count; index++)
                {
                    expectedById.Add(expected[index].TransactionStableId, expected[index]);
                }

                bool allApplied = true;
                for (int index = 0; index < snapshot.Children.Count; index++)
                {
                    RewardChildApplicationSnapshot child = snapshot.Children[index];
                    RewardChildGrantCommand planned;
                    if (!expectedById.TryGetValue(
                        child.Command.TransactionStableId,
                        out planned)
                        || !planned.Equals(child.Command))
                    {
                        rejectionCode = "child-command-mismatch";
                        return false;
                    }

                    if (child.ResolutionState == RewardChildResolutionState.Applied)
                    {
                        if (!child.LastApplyStatus.HasValue
                            || (child.LastApplyStatus.Value
                                    != RewardChildApplyStatus.Applied
                                && child.LastApplyStatus.Value
                                    != RewardChildApplyStatus.ExactDuplicateNoChange))
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
                                == RewardChildApplyStatus.Applied)
                        {
                            rejectionCode = "pending-child-terminal-fact-invalid";
                            return false;
                        }
                    }
                }

                if (snapshot.State == RewardCommitmentState.Applied && !allApplied)
                {
                    rejectionCode = "applied-commitment-has-pending-child";
                    return false;
                }

                if (snapshot.State == RewardCommitmentState.Claimed && allApplied)
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

            if (snapshot.State == RewardCommitmentState.Generated
                && snapshot.Projections.Count != 0)
            {
                rejectionCode = "generated-commitment-has-projection";
                return false;
            }

            if (snapshot.State == RewardCommitmentState.Projected
                && snapshot.Projections.Count == 0)
            {
                rejectionCode = "projected-commitment-has-no-projection";
                return false;
            }

            bool cancelled = snapshot.State == RewardCommitmentState.Cancelled;
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
                RewardProjectCommand projection = snapshot.Projections[index];
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
            RewardApplicationSnapshot snapshot,
            out long minimumSequence)
        {
            try
            {
                long result = 0L;
                for (int index = 0; index < snapshot.Commitments.Count; index++)
                {
                    RewardCommitmentSnapshot commitment = snapshot.Commitments[index];
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

                    if (commitment.State == RewardCommitmentState.Applied)
                    {
                        result = checked(result + 1L);
                    }

                    for (int childIndex = 0;
                        childIndex < commitment.Children.Count;
                        childIndex++)
                    {
                        RewardChildApplicationSnapshot child =
                            commitment.Children[childIndex];
                        if (child.ResolutionState == RewardChildResolutionState.Pending
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
