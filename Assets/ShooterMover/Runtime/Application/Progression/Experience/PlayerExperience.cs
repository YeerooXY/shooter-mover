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
    public sealed class PlayerExperience :
        IPlayerExperience
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
        private readonly PlayerExperienceCurve curve;
        private readonly Dictionary<string, AppliedGrant> grantsBySource;

        private long sequence;
        private PlayerExperienceState currentState;
        private ProgressionContext currentContext;
        private PlayerExperienceSnapshot currentSnapshot;

        public PlayerExperience(
            PlayerExperienceCurve curve,
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

        public PlayerExperienceCurve Curve => curve;

        public PlayerExperienceState CurrentState
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

        public PlayerExperienceSnapshot CurrentSnapshot
        {
            get
            {
                lock (syncRoot)
                {
                    return currentSnapshot;
                }
            }
        }

        public PlayerExperienceGrantFact Grant(
            PlayerExperienceGrantRequest request)
        {
            lock (syncRoot)
            {
                PlayerExperienceState previousState = currentState;
                PlayerExperienceSnapshot previousSnapshot = currentSnapshot;
                if (request == null)
                {
                    return NoChangeFact(
                        null,
                        0L,
                        string.Empty,
                        PlayerExperienceGrantStatus.InvalidRequest,
                        PlayerExperienceGrantStatus.InvalidRequest,
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
                        PlayerExperienceGrantStatus.InvalidRequest,
                        PlayerExperienceGrantStatus.InvalidRequest,
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
                        PlayerExperienceGrantStatus.InvalidAmount,
                        PlayerExperienceGrantStatus.InvalidAmount,
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
                            ? PlayerExperienceGrantStatus.DuplicateNoChange
                            : PlayerExperienceGrantStatus.ConflictingDuplicate,
                        PlayerExperienceGrantStatus.Applied,
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
                        PlayerExperienceGrantStatus.ArithmeticOverflow,
                        PlayerExperienceGrantStatus.ArithmeticOverflow,
                        "xp-cumulative-or-sequence-overflow",
                        previousState,
                        previousSnapshot);
                }

                PlayerExperienceState nextState = curve.Evaluate(nextCumulative);
                ProgressionContext nextContext =
                    nextState.ProjectContext(currentContext);
                IReadOnlyList<PlayerLevelUpFact> levelUpFacts =
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

                return new PlayerExperienceGrantFact(
                    request.SourceOperationStableId,
                    request.Amount,
                    request.CommandFingerprint,
                    PlayerExperienceGrantStatus.Applied,
                    PlayerExperienceGrantStatus.Applied,
                    string.Empty,
                    previousState,
                    currentState,
                    previousSnapshot,
                    currentSnapshot,
                    levelUpFacts);
            }
        }

        public PlayerExperienceSnapshot ExportSnapshot()
        {
            lock (syncRoot)
            {
                return currentSnapshot;
            }
        }

        public PlayerExperienceImportResult TryImport(
            PlayerExperienceSnapshot snapshot)
        {
            lock (syncRoot)
            {
                PlayerExperienceSnapshot previous = currentSnapshot;
                Dictionary<string, AppliedGrant> importedGrants;
                PlayerExperienceState importedState;
                PlayerExperienceImportStatus failureStatus;
                string rejectionCode;
                if (!TryValidateSnapshot(
                    snapshot,
                    out importedGrants,
                    out importedState,
                    out failureStatus,
                    out rejectionCode))
                {
                    return new PlayerExperienceImportResult(
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
                    return new PlayerExperienceImportResult(
                        PlayerExperienceImportStatus.DuplicateNoChange,
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
                return new PlayerExperienceImportResult(
                    PlayerExperienceImportStatus.Imported,
                    string.Empty,
                    previous,
                    currentSnapshot);
            }
        }

        private PlayerExperienceSnapshot BuildSnapshot()
        {
            var snapshots =
                new List<PlayerExperienceGrantSnapshot>(grantsBySource.Count);
            foreach (AppliedGrant grant in grantsBySource.Values)
            {
                snapshots.Add(new PlayerExperienceGrantSnapshot(
                    grant.SourceOperationStableId.ToString(),
                    grant.Amount,
                    grant.CommandFingerprint,
                    grant.AppliedSequence));
            }

            return PlayerExperienceSnapshot.CreateCanonical(
                sequence,
                curve.Fingerprint,
                currentState.CumulativeExperience,
                currentContext,
                snapshots);
        }

        private IReadOnlyList<PlayerLevelUpFact> BuildLevelUpFacts(
            StableId sourceOperationStableId,
            int previousLevel,
            int currentLevel)
        {
            if (currentLevel <= previousLevel)
            {
                return Array.Empty<PlayerLevelUpFact>();
            }

            var facts = new List<PlayerLevelUpFact>(
                currentLevel - previousLevel);
            for (int reachedLevel = previousLevel + 1;
                reachedLevel <= currentLevel;
                reachedLevel++)
            {
                facts.Add(new PlayerLevelUpFact(
                    sourceOperationStableId,
                    reachedLevel - 1,
                    reachedLevel,
                    curve.GetCumulativeExperienceForLevel(reachedLevel),
                    1,
                    reachedLevel));
            }

            return facts;
        }

        private PlayerExperienceGrantFact NoChangeFact(
            StableId sourceOperationStableId,
            long amount,
            string commandFingerprint,
            PlayerExperienceGrantStatus status,
            PlayerExperienceGrantStatus originalStatus,
            string rejectionCode,
            PlayerExperienceState state,
            PlayerExperienceSnapshot snapshot)
        {
            return new PlayerExperienceGrantFact(
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
                Array.Empty<PlayerLevelUpFact>());
        }

        private bool TryValidateSnapshot(
            PlayerExperienceSnapshot snapshot,
            out Dictionary<string, AppliedGrant> importedGrants,
            out PlayerExperienceState importedState,
            out PlayerExperienceImportStatus failureStatus,
            out string rejectionCode)
        {
            importedGrants = null;
            importedState = null;
            failureStatus = PlayerExperienceImportStatus.ValidationRejected;
            rejectionCode = string.Empty;

            if (snapshot == null)
            {
                rejectionCode = "xp-snapshot-null";
                return false;
            }

            if (snapshot.SchemaVersion
                != PlayerExperienceSnapshot.CurrentSchemaVersion)
            {
                failureStatus =
                    PlayerExperienceImportStatus.UnsupportedSchemaVersion;
                rejectionCode = "xp-snapshot-schema-unsupported";
                return false;
            }

            if (!string.Equals(
                snapshot.AuthorityStableId,
                PlayerExperienceIds.AuthorityStableId.ToString(),
                StringComparison.Ordinal))
            {
                failureStatus = PlayerExperienceImportStatus.AuthorityMismatch;
                rejectionCode = "xp-snapshot-authority-mismatch";
                return false;
            }

            if (!string.Equals(
                snapshot.CurveFingerprint,
                curve.Fingerprint,
                StringComparison.Ordinal))
            {
                failureStatus = PlayerExperienceImportStatus.CurveMismatch;
                rejectionCode = "xp-snapshot-curve-mismatch";
                return false;
            }

            if (!snapshot.HasValidFingerprint())
            {
                failureStatus = PlayerExperienceImportStatus.FingerprintMismatch;
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
                PlayerExperienceGrantSnapshot grant = snapshot.Grants[index];
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
                    PlayerExperienceGrantRequest.ComputeCommandFingerprint(
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

            failureStatus = PlayerExperienceImportStatus.Imported;
            rejectionCode = string.Empty;
            return true;
        }
    }
}
