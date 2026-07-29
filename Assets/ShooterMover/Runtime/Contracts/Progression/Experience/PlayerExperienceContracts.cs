using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Progression.Context;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Experience;

namespace ShooterMover.Contracts.Progression.Experience
{
    public enum PlayerExperienceGrantStatus
    {
        Applied = 1,
        DuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        InvalidRequest = 4,
        InvalidAmount = 5,
        ArithmeticOverflow = 6,
    }

    public enum PlayerExperienceImportStatus
    {
        Imported = 1,
        DuplicateNoChange = 2,
        ValidationRejected = 3,
        UnsupportedSchemaVersion = 4,
        AuthorityMismatch = 5,
        CurveMismatch = 6,
        FingerprintMismatch = 7,
    }

    /// <summary>
    /// One exactly-once XP grant keyed by its permanent source-operation identity.
    /// </summary>
    public sealed class PlayerExperienceGrantRequest
    {
        private const string SchemaId = "player-experience-grant-v1";

        public PlayerExperienceGrantRequest(
            StableId sourceOperationStableId,
            long amount)
        {
            SourceOperationStableId = sourceOperationStableId;
            Amount = amount;
            CommandFingerprint = ComputeCommandFingerprint(
                sourceOperationStableId,
                amount);
        }

        public StableId SourceOperationStableId { get; }

        public long Amount { get; }

        public string CommandFingerprint { get; }

        public static string ComputeCommandFingerprint(
            StableId sourceOperationStableId,
            long amount)
        {
            var builder = new StringBuilder();
            PlayerExperienceFormat.AppendToken(builder, "schema", SchemaId);
            PlayerExperienceFormat.AppendToken(
                builder,
                "source_operation_stable_id",
                sourceOperationStableId == null
                    ? string.Empty
                    : sourceOperationStableId.ToString());
            PlayerExperienceFormat.AppendToken(
                builder,
                "amount",
                amount.ToString(CultureInfo.InvariantCulture));
            return PlayerExperienceFormat.ComputeSha256(builder.ToString());
        }
    }

    /// <summary>
    /// Immutable event fact for one crossed level boundary. Multi-level grants
    /// return one ordered fact per boundary and therefore one skill point each.
    /// </summary>
    public sealed class PlayerLevelUpFact
    {
        public PlayerLevelUpFact(
            StableId sourceOperationStableId,
            int previousLevel,
            int currentLevel,
            long cumulativeThreshold,
            int skillPointsGranted,
            int totalSkillPointsAfter)
        {
            SourceOperationStableId = sourceOperationStableId
                ?? throw new ArgumentNullException(nameof(sourceOperationStableId));
            if (currentLevel != previousLevel + 1)
            {
                throw new ArgumentException(
                    "A level-up fact must cross exactly one level boundary.",
                    nameof(currentLevel));
            }

            if (previousLevel < PlayerExperienceCurve.MinimumLevel
                || currentLevel > PlayerExperienceCurve.MaximumLevel)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentLevel),
                    "Level-up facts must stay inside levels 1 through 100.");
            }

            if (cumulativeThreshold < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(cumulativeThreshold));
            }

            if (skillPointsGranted != 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(skillPointsGranted),
                    "XP-001 awards exactly one skill point per player level.");
            }

            if (totalSkillPointsAfter != currentLevel)
            {
                throw new ArgumentException(
                    "Total awarded skill points must equal the reached player level.",
                    nameof(totalSkillPointsAfter));
            }

            PreviousLevel = previousLevel;
            CurrentLevel = currentLevel;
            CumulativeThreshold = cumulativeThreshold;
            SkillPointsGranted = skillPointsGranted;
            TotalSkillPointsAfter = totalSkillPointsAfter;
        }

        public StableId SourceOperationStableId { get; }

        public int PreviousLevel { get; }

        public int CurrentLevel { get; }

        public long CumulativeThreshold { get; }

        public int SkillPointsGranted { get; }

        public int TotalSkillPointsAfter { get; }
    }

    /// <summary>
    /// Persistence record for one accepted source operation.
    /// </summary>
    public sealed class PlayerExperienceGrantSnapshot
    {
        public PlayerExperienceGrantSnapshot(
            string sourceOperationStableId,
            long amount,
            string commandFingerprint,
            long appliedSequence)
        {
            SourceOperationStableId = sourceOperationStableId;
            Amount = amount;
            CommandFingerprint = commandFingerprint;
            AppliedSequence = appliedSequence;
        }

        public string SourceOperationStableId { get; }

        public long Amount { get; }

        public string CommandFingerprint { get; }

        public long AppliedSequence { get; }

        public static PlayerExperienceGrantSnapshot Create(
            StableId sourceOperationStableId,
            long amount,
            long appliedSequence)
        {
            if (sourceOperationStableId == null)
            {
                throw new ArgumentNullException(nameof(sourceOperationStableId));
            }

            if (amount <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (appliedSequence <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(appliedSequence));
            }

            return new PlayerExperienceGrantSnapshot(
                sourceOperationStableId.ToString(),
                amount,
                PlayerExperienceGrantRequest.ComputeCommandFingerprint(
                    sourceOperationStableId,
                    amount),
                appliedSequence);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            PlayerExperienceFormat.AppendToken(
                builder,
                "source_operation_stable_id",
                SourceOperationStableId);
            PlayerExperienceFormat.AppendToken(
                builder,
                "amount",
                Amount.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormat.AppendToken(
                builder,
                "command_fingerprint",
                CommandFingerprint);
            PlayerExperienceFormat.AppendToken(
                builder,
                "applied_sequence",
                AppliedSequence.ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    /// <summary>
    /// Canonical engine-independent export of XP authority state and replay facts.
    /// </summary>
    public sealed class PlayerExperienceSnapshot
    {
        private const string SchemaId = "player-experience-snapshot-v1";

        public const int CurrentSchemaVersion = 1;

        public PlayerExperienceSnapshot(
            int schemaVersion,
            string authorityStableId,
            long sequence,
            string curveFingerprint,
            long cumulativeExperience,
            ProgressionContext progressionContext,
            IEnumerable<PlayerExperienceGrantSnapshot> grants,
            string fingerprint)
        {
            SchemaVersion = schemaVersion;
            AuthorityStableId = authorityStableId;
            Sequence = sequence;
            CurveFingerprint = curveFingerprint;
            CumulativeExperience = cumulativeExperience;
            ProgressionContext = progressionContext;
            Grants = CopyAndOrder(grants);
            Fingerprint = fingerprint;
        }

        public int SchemaVersion { get; }

        public string AuthorityStableId { get; }

        public long Sequence { get; }

        public string CurveFingerprint { get; }

        public long CumulativeExperience { get; }

        public ProgressionContext ProgressionContext { get; }

        public IReadOnlyList<PlayerExperienceGrantSnapshot> Grants { get; }

        public string Fingerprint { get; }

        public static PlayerExperienceSnapshot CreateCanonical(
            long sequence,
            string curveFingerprint,
            long cumulativeExperience,
            ProgressionContext progressionContext,
            IEnumerable<PlayerExperienceGrantSnapshot> grants)
        {
            var provisional = new PlayerExperienceSnapshot(
                CurrentSchemaVersion,
                PlayerExperienceIds.AuthorityStableId.ToString(),
                sequence,
                curveFingerprint,
                cumulativeExperience,
                progressionContext,
                grants,
                string.Empty);
            return new PlayerExperienceSnapshot(
                provisional.SchemaVersion,
                provisional.AuthorityStableId,
                provisional.Sequence,
                provisional.CurveFingerprint,
                provisional.CumulativeExperience,
                provisional.ProgressionContext,
                provisional.Grants,
                ComputeFingerprint(provisional));
        }

        public bool HasValidFingerprint()
        {
            return string.Equals(
                Fingerprint,
                ComputeFingerprint(this),
                StringComparison.Ordinal);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            PlayerExperienceFormat.AppendToken(builder, "schema", SchemaId);
            PlayerExperienceFormat.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormat.AppendToken(
                builder,
                "authority_stable_id",
                AuthorityStableId);
            PlayerExperienceFormat.AppendToken(
                builder,
                "sequence",
                Sequence.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormat.AppendToken(
                builder,
                "curve_fingerprint",
                CurveFingerprint);
            PlayerExperienceFormat.AppendToken(
                builder,
                "cumulative_experience",
                CumulativeExperience.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormat.AppendToken(
                builder,
                "progression_context",
                ProgressionContext == null
                    ? string.Empty
                    : ProgressionContext.ToCanonicalString());
            PlayerExperienceFormat.AppendToken(
                builder,
                "grant_count",
                Grants.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < Grants.Count; index++)
            {
                PlayerExperienceGrantSnapshot grant = Grants[index];
                PlayerExperienceFormat.AppendToken(
                    builder,
                    "grant_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    grant == null ? string.Empty : grant.ToCanonicalString());
            }

            return builder.ToString();
        }

        private static string ComputeFingerprint(
            PlayerExperienceSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return PlayerExperienceFormat.ComputeSha256(
                snapshot.ToCanonicalString());
        }

        private static IReadOnlyList<PlayerExperienceGrantSnapshot>
            CopyAndOrder(IEnumerable<PlayerExperienceGrantSnapshot> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<PlayerExperienceGrantSnapshot>(source);
            copy.Sort(CompareGrants);
            return new ReadOnlyCollection<PlayerExperienceGrantSnapshot>(copy);
        }

        private static int CompareGrants(
            PlayerExperienceGrantSnapshot left,
            PlayerExperienceGrantSnapshot right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            return string.CompareOrdinal(
                left.SourceOperationStableId,
                right.SourceOperationStableId);
        }
    }

    /// <summary>
    /// UI/application-ready terminal fact for one grant attempt.
    /// </summary>
    public sealed class PlayerExperienceGrantFact
    {
        public PlayerExperienceGrantFact(
            StableId sourceOperationStableId,
            long amount,
            string commandFingerprint,
            PlayerExperienceGrantStatus status,
            PlayerExperienceGrantStatus originalStatus,
            string rejectionCode,
            PlayerExperienceState previousState,
            PlayerExperienceState currentState,
            PlayerExperienceSnapshot previousSnapshot,
            PlayerExperienceSnapshot currentSnapshot,
            IEnumerable<PlayerLevelUpFact> levelUpFacts)
        {
            SourceOperationStableId = sourceOperationStableId;
            Amount = amount;
            CommandFingerprint = commandFingerprint ?? string.Empty;
            Status = status;
            OriginalStatus = originalStatus;
            RejectionCode = rejectionCode ?? string.Empty;
            PreviousState = previousState
                ?? throw new ArgumentNullException(nameof(previousState));
            CurrentState = currentState
                ?? throw new ArgumentNullException(nameof(currentState));
            PreviousSnapshot = previousSnapshot
                ?? throw new ArgumentNullException(nameof(previousSnapshot));
            CurrentSnapshot = currentSnapshot
                ?? throw new ArgumentNullException(nameof(currentSnapshot));
            if (levelUpFacts == null)
            {
                throw new ArgumentNullException(nameof(levelUpFacts));
            }

            LevelUpFacts = new ReadOnlyCollection<PlayerLevelUpFact>(
                new List<PlayerLevelUpFact>(levelUpFacts));
        }

        public StableId SourceOperationStableId { get; }

        public long Amount { get; }

        public string CommandFingerprint { get; }

        public PlayerExperienceGrantStatus Status { get; }

        public PlayerExperienceGrantStatus OriginalStatus { get; }

        public string RejectionCode { get; }

        public PlayerExperienceState PreviousState { get; }

        public PlayerExperienceState CurrentState { get; }

        public PlayerExperienceSnapshot PreviousSnapshot { get; }

        public PlayerExperienceSnapshot CurrentSnapshot { get; }

        public IReadOnlyList<PlayerLevelUpFact> LevelUpFacts { get; }

        public bool Changed =>
            Status == PlayerExperienceGrantStatus.Applied;
    }

    public sealed class PlayerExperienceImportResult
    {
        public PlayerExperienceImportResult(
            PlayerExperienceImportStatus status,
            string rejectionCode,
            PlayerExperienceSnapshot previousSnapshot,
            PlayerExperienceSnapshot currentSnapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            PreviousSnapshot = previousSnapshot
                ?? throw new ArgumentNullException(nameof(previousSnapshot));
            CurrentSnapshot = currentSnapshot
                ?? throw new ArgumentNullException(nameof(currentSnapshot));
        }

        public PlayerExperienceImportStatus Status { get; }

        public string RejectionCode { get; }

        public PlayerExperienceSnapshot PreviousSnapshot { get; }

        public PlayerExperienceSnapshot CurrentSnapshot { get; }

        public bool Changed =>
            Status == PlayerExperienceImportStatus.Imported;
    }

    public interface IPlayerExperienceState : IProgressionContextProvider
    {
        PlayerExperienceState CurrentState { get; }

        PlayerExperienceSnapshot CurrentSnapshot { get; }

        PlayerExperienceGrantFact Grant(
            PlayerExperienceGrantRequest request);

        PlayerExperienceSnapshot ExportSnapshot();

        PlayerExperienceImportResult TryImport(
            PlayerExperienceSnapshot snapshot);
    }
}
