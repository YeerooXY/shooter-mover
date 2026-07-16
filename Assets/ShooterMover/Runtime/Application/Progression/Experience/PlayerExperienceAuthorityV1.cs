using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Experience;

namespace ShooterMover.Application.Progression.Experience
{
    /// <summary>
    /// Sole mutable player-XP authority. It accepts positive grants exactly once by
    /// source-operation identity and projects the resulting player level into the
    /// existing immutable ProgressionContext.
    /// </summary>
    public sealed class PlayerExperienceAuthorityV1 :
        IPlayerExperienceAuthorityV1
    {
        private sealed class AppliedGrant
        {
            public AppliedGrant(
                StableId sourceOperationStableId,
                long amount,
                string commandFingerprint,
                long appliedSequence)
            {
                SourceOperationStableId = sourceOperationStableId;
                Amount = amount;
                CommandFingerprint = commandFingerprint;
                AppliedSequence = appliedSequence;
            }

            public StableId SourceOperationStableId { get; }

            public long Amount { get; }

            public string CommandFingerprint { get; }

            public long AppliedSequence { get; }
        }

        private readonly object syncRoot = new object();
        private readonly PlayerExperienceCurveV1 curve;
        private readonly Dictionary<string, AppliedGrant> grantsBySource;

        private long sequence;
        private PlayerExperienceStateV1 currentState;
        private ProgressionContext currentContext;
        private PlayerExperienceSnapshotV1 currentSnapshot;

        public PlayerExperienceAuthorityV1(
            PlayerExperienceCurveV1 curve,
            ProgressionContext initialContext)
        {
            this.curve = curve
                ?? throw new ArgumentNullException(nameof(curve));
            if (initialContext == null)
            {
                throw new ArgumentNullException(nameof(initialContext));
            }

            grantsBySource = new Dictionary<string, AppliedGrant>(
                StringComparer.Ordinal);
            sequence = 0L;
            currentState = curve.Evaluate(0L);
            currentContext = currentState.ProjectContext(initialContext);
            currentSnapshot = BuildSnapshot();
        }

        public PlayerExperienceCurveV1 Curve => curve;

        public PlayerExperienceStateV1 CurrentState
        {
            get
            {
                lock (syncRoot)
                {
                    return currentState;
                }
            }
        }

        public ProgressionContext CurrentContext
        {
            get
            {
                lock (syncRoot)
                {
                    return currentContext;
                }
            }
        }

        public PlayerExperienceSnapshotV1 CurrentSnapshot
        {
            get
            {
                lock (syncRoot)
                {
                    return currentSnapshot;
                }
            }
        }

        public PlayerExperienceGrantFactV1 Grant(
            PlayerExperienceGrantRequestV1 request)
        {
            lock (syncRoot)
            {
                PlayerExperienceStateV1 previousState = currentState;
                PlayerExperienceSnapshotV1 previousSnapshot = currentSnapshot;
                if (request == null)
                {
                    return NoChangeFact(
                        null,
                        0L,
                        string.Empty,
                        PlayerExperienceGrantStatusV1.InvalidRequest,
                        PlayerExperienceGrantStatusV1.InvalidRequest,
                        "xp-request-null",
                        previousState,
                        previousSnapshot);
                }

                if (request.SourceOperationStableId == null)
                {
                    return NoChangeFact(
                        null,
                        request.Amount,
                        request.CommandFingerprint,
                        PlayerExperienceGrantStatusV1.InvalidRequest,
                        PlayerExperienceGrantStatusV1.InvalidRequest,
                        "xp-source-operation-missing",
                        previousState,
                        previousSnapshot);
                }

                if (request.Amount <= 0L)
                {
                    return NoChangeFact(
                        request.SourceOperationStableId,
                        request.Amount,
                        request.CommandFingerprint,
                        PlayerExperienceGrantStatusV1.InvalidAmount,
                        PlayerExperienceGrantStatusV1.InvalidAmount,
                        "xp-amount-not-positive",
                        previousState,
                        previousSnapshot);
                }

                string sourceKey = request.SourceOperationStableId.ToString();
                AppliedGrant existing;
                if (grantsBySource.TryGetValue(sourceKey, out existing))
                {
                    bool exact = string.Equals(
                        existing.CommandFingerprint,
                        request.CommandFingerprint,
                        StringComparison.Ordinal);
                    return NoChangeFact(
                        request.SourceOperationStableId,
                        request.Amount,
                        request.CommandFingerprint,
                        exact
                            ? PlayerExperienceGrantStatusV1.DuplicateNoChange
                            : PlayerExperienceGrantStatusV1.ConflictingDuplicate,
                        PlayerExperienceGrantStatusV1.Applied,
                        exact
                            ? string.Empty
                            : "xp-source-operation-conflict",
                        previousState,
                        previousSnapshot);
                }

                long nextCumulative;
                long nextSequence;
                try
                {
                    nextCumulative = checked(
                        currentState.CumulativeExperience + request.Amount);
                    nextSequence = checked(sequence + 1L);
                }
                catch (OverflowException)
                {
                    return NoChangeFact(
                        request.SourceOperationStableId,
                        request.Amount,
                        request.CommandFingerprint,
                        PlayerExperienceGrantStatusV1.ArithmeticOverflow,
                        PlayerExperienceGrantStatusV1.ArithmeticOverflow,
                        "xp-cumulative-or-sequence-overflow",
                        previousState,
                        previousSnapshot);
                }

                PlayerExperienceStateV1 nextState = curve.Evaluate(nextCumulative);
                ProgressionContext nextContext =
                    nextState.ProjectContext(currentContext);
                IReadOnlyList<PlayerLevelUpFactV1> levelUpFacts =
                    BuildLevelUpFacts(
                        request.SourceOperationStableId,
                        previousState.Level,
                        nextState.Level);

                var applied = new AppliedGrant(
                    request.SourceOperationStableId,
                    request.Amount,
                    request.CommandFingerprint,
                    nextSequence);
                grantsBySource.Add(sourceKey, applied);
                sequence = nextSequence;
                currentState = nextState;
                currentContext = nextContext;
                currentSnapshot = BuildSnapshot();

                return new PlayerExperienceGrantFactV1(
                    request.SourceOperationStableId,
                    request.Amount,
                    request.CommandFingerprint,
                    PlayerExperienceGrantStatusV1.Applied,
                    PlayerExperienceGrantStatusV1.Applied,
                    string.Empty,
                    previousState,
                    currentState,
                    previousSnapshot,
                    currentSnapshot,
                    levelUpFacts);
            }
        }

        public PlayerExperienceSnapshotV1 ExportSnapshot()
        {
            lock (syncRoot)
            {
                return currentSnapshot;
            }
        }

        public PlayerExperienceImportResultV1 TryImport(
            PlayerExperienceSnapshotV1 snapshot)
        {
            lock (syncRoot)
            {
                PlayerExperienceSnapshotV1 previous = currentSnapshot;
                Dictionary<string, AppliedGrant> importedGrants;
                PlayerExperienceStateV1 importedState;
                PlayerExperienceImportStatusV1 failureStatus;
                string rejectionCode;
                if (!TryValidateSnapshot(
                    snapshot,
                    out importedGrants,
                    out importedState,
                    out failureStatus,
                    out rejectionCode))
                {
                    return new PlayerExperienceImportResultV1(
                        failureStatus,
                        rejectionCode,
                        previous,
                        previous);
                }

                if (string.Equals(
                    previous.Fingerprint,
                    snapshot.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return new PlayerExperienceImportResultV1(
                        PlayerExperienceImportStatusV1.DuplicateNoChange,
                        string.Empty,
                        previous,
                        previous);
                }

                grantsBySource.Clear();
                foreach (KeyValuePair<string, AppliedGrant> entry in importedGrants)
                {
                    grantsBySource.Add(entry.Key, entry.Value);
                }

                sequence = snapshot.Sequence;
                currentState = importedState;
                currentContext = snapshot.ProgressionContext;
                currentSnapshot = snapshot;
                return new PlayerExperienceImportResultV1(
                    PlayerExperienceImportStatusV1.Imported,
                    string.Empty,
                    previous,
                    currentSnapshot);
            }
        }

        private PlayerExperienceSnapshotV1 BuildSnapshot()
        {
            var snapshots =
                new List<PlayerExperienceGrantSnapshotV1>(grantsBySource.Count);
            foreach (AppliedGrant grant in grantsBySource.Values)
            {
                snapshots.Add(new PlayerExperienceGrantSnapshotV1(
                    grant.SourceOperationStableId.ToString(),
                    grant.Amount,
                    grant.CommandFingerprint,
                    grant.AppliedSequence));
            }

            return PlayerExperienceSnapshotV1.CreateCanonical(
                sequence,
                curve.Fingerprint,
                currentState.CumulativeExperience,
                currentContext,
                snapshots);
        }

        private IReadOnlyList<PlayerLevelUpFactV1> BuildLevelUpFacts(
            StableId sourceOperationStableId,
            int previousLevel,
            int currentLevel)
        {
            if (currentLevel <= previousLevel)
            {
                return Array.Empty<PlayerLevelUpFactV1>();
            }

            var facts = new List<PlayerLevelUpFactV1>(
                currentLevel - previousLevel);
            for (int reachedLevel = previousLevel + 1;
                reachedLevel <= currentLevel;
                reachedLevel++)
            {
                facts.Add(new PlayerLevelUpFactV1(
                    sourceOperationStableId,
                    reachedLevel - 1,
                    reachedLevel,
                    curve.GetCumulativeExperienceForLevel(reachedLevel),
                    1,
                    reachedLevel));
            }

            return facts;
        }

        private PlayerExperienceGrantFactV1 NoChangeFact(
            StableId sourceOperationStableId,
            long amount,
            string commandFingerprint,
            PlayerExperienceGrantStatusV1 status,
            PlayerExperienceGrantStatusV1 originalStatus,
            string rejectionCode,
            PlayerExperienceStateV1 state,
            PlayerExperienceSnapshotV1 snapshot)
        {
            return new PlayerExperienceGrantFactV1(
                sourceOperationStableId,
                amount,
                commandFingerprint,
                status,
                originalStatus,
                rejectionCode,
                state,
                state,
                snapshot,
                snapshot,
                Array.Empty<PlayerLevelUpFactV1>());
        }

        private bool TryValidateSnapshot(
            PlayerExperienceSnapshotV1 snapshot,
            out Dictionary<string, AppliedGrant> importedGrants,
            out PlayerExperienceStateV1 importedState,
            out PlayerExperienceImportStatusV1 failureStatus,
            out string rejectionCode)
        {
            importedGrants = null;
            importedState = null;
            failureStatus = PlayerExperienceImportStatusV1.ValidationRejected;
            rejectionCode = string.Empty;

            if (snapshot == null)
            {
                rejectionCode = "xp-snapshot-null";
                return false;
            }

            if (snapshot.SchemaVersion
                != PlayerExperienceSnapshotV1.CurrentSchemaVersion)
            {
                failureStatus =
                    PlayerExperienceImportStatusV1.UnsupportedSchemaVersion;
                rejectionCode = "xp-snapshot-schema-unsupported";
                return false;
            }

            if (!string.Equals(
                snapshot.AuthorityStableId,
                PlayerExperienceIdsV1.AuthorityStableId.ToString(),
                StringComparison.Ordinal))
            {
                failureStatus = PlayerExperienceImportStatusV1.AuthorityMismatch;
                rejectionCode = "xp-snapshot-authority-mismatch";
                return false;
            }

            if (!string.Equals(
                snapshot.CurveFingerprint,
                curve.Fingerprint,
                StringComparison.Ordinal))
            {
                failureStatus = PlayerExperienceImportStatusV1.CurveMismatch;
                rejectionCode = "xp-snapshot-curve-mismatch";
                return false;
            }

            if (!snapshot.HasValidFingerprint())
            {
                failureStatus = PlayerExperienceImportStatusV1.FingerprintMismatch;
                rejectionCode = "xp-snapshot-fingerprint-mismatch";
                return false;
            }

            if (snapshot.Sequence < 0L)
            {
                rejectionCode = "xp-snapshot-sequence-negative";
                return false;
            }

            if (snapshot.CumulativeExperience < 0L)
            {
                rejectionCode = "xp-snapshot-cumulative-negative";
                return false;
            }

            if (snapshot.ProgressionContext == null)
            {
                rejectionCode = "xp-snapshot-context-missing";
                return false;
            }

            try
            {
                importedState = curve.Evaluate(snapshot.CumulativeExperience);
            }
            catch (ArgumentOutOfRangeException)
            {
                rejectionCode = "xp-snapshot-cumulative-invalid";
                return false;
            }

            if (snapshot.ProgressionContext.CharacterLevel
                != importedState.Level)
            {
                rejectionCode = "xp-snapshot-context-level-mismatch";
                return false;
            }

            if (snapshot.Sequence != snapshot.Grants.Count)
            {
                rejectionCode = "xp-snapshot-sequence-count-mismatch";
                return false;
            }

            importedGrants = new Dictionary<string, AppliedGrant>(
                StringComparer.Ordinal);
            var seenSequences = new bool[snapshot.Grants.Count + 1];
            long cumulative = 0L;
            for (int index = 0; index < snapshot.Grants.Count; index++)
            {
                PlayerExperienceGrantSnapshotV1 grant = snapshot.Grants[index];
                if (grant == null)
                {
                    rejectionCode = "xp-snapshot-grant-null";
                    return false;
                }

                StableId sourceOperationStableId;
                if (!StableId.TryParse(
                    grant.SourceOperationStableId,
                    out sourceOperationStableId))
                {
                    rejectionCode = "xp-snapshot-source-operation-invalid";
                    return false;
                }

                if (grant.Amount <= 0L)
                {
                    rejectionCode = "xp-snapshot-grant-amount-invalid";
                    return false;
                }

                if (grant.AppliedSequence <= 0L
                    || grant.AppliedSequence > snapshot.Sequence)
                {
                    rejectionCode = "xp-snapshot-grant-sequence-invalid";
                    return false;
                }

                int sequenceIndex = checked((int)grant.AppliedSequence);
                if (seenSequences[sequenceIndex])
                {
                    rejectionCode = "xp-snapshot-grant-sequence-duplicate";
                    return false;
                }

                string expectedFingerprint =
                    PlayerExperienceGrantRequestV1.ComputeCommandFingerprint(
                        sourceOperationStableId,
                        grant.Amount);
                if (!string.Equals(
                    expectedFingerprint,
                    grant.CommandFingerprint,
                    StringComparison.Ordinal))
                {
                    rejectionCode = "xp-snapshot-command-fingerprint-invalid";
                    return false;
                }

                string sourceKey = sourceOperationStableId.ToString();
                if (importedGrants.ContainsKey(sourceKey))
                {
                    rejectionCode = "xp-snapshot-source-operation-duplicate";
                    return false;
                }

                try
                {
                    cumulative = checked(cumulative + grant.Amount);
                }
                catch (OverflowException)
                {
                    rejectionCode = "xp-snapshot-cumulative-overflow";
                    return false;
                }

                seenSequences[sequenceIndex] = true;
                importedGrants.Add(
                    sourceKey,
                    new AppliedGrant(
                        sourceOperationStableId,
                        grant.Amount,
                        grant.CommandFingerprint,
                        grant.AppliedSequence));
            }

            for (int index = 1; index < seenSequences.Length; index++)
            {
                if (!seenSequences[index])
                {
                    rejectionCode = "xp-snapshot-grant-sequence-gap";
                    return false;
                }
            }

            if (cumulative != snapshot.CumulativeExperience)
            {
                rejectionCode = "xp-snapshot-cumulative-mismatch";
                return false;
            }

            failureStatus = PlayerExperienceImportStatusV1.Imported;
            rejectionCode = string.Empty;
            return true;
        }
    }
}
