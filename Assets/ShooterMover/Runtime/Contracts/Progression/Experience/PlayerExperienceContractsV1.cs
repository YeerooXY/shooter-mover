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
    public enum PlayerExperienceGrantStatusV1
    {
        Applied = 1,
        DuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        InvalidRequest = 4,
        InvalidAmount = 5,
        ArithmeticOverflow = 6,
    }

    public enum PlayerExperienceImportStatusV1
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
    public sealed class PlayerExperienceGrantRequestV1
    {
        private const string SchemaId = "player-experience-grant-v1";

        public PlayerExperienceGrantRequestV1(
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
            PlayerExperienceFormatV1.AppendToken(builder, "schema", SchemaId);
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "source_operation_stable_id",
                sourceOperationStableId == null
                    ? string.Empty
                    : sourceOperationStableId.ToString());
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "amount",
                amount.ToString(CultureInfo.InvariantCulture));
            return PlayerExperienceFormatV1.ComputeSha256(builder.ToString());
        }
    }

    /// <summary>
    /// Immutable event fact for one crossed level boundary. Multi-level grants
    /// return one ordered fact per boundary and therefore one skill point each.
    /// </summary>
    public sealed class PlayerLevelUpFactV1
    {
        public PlayerLevelUpFactV1(
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

            if (previousLevel < PlayerExperienceCurveV1.MinimumLevel
                || currentLevel > PlayerExperienceCurveV1.MaximumLevel)
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
    public sealed class PlayerExperienceGrantSnapshotV1
    {
        public PlayerExperienceGrantSnapshotV1(
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

        public static PlayerExperienceGrantSnapshotV1 Create(
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

            return new PlayerExperienceGrantSnapshotV1(
                sourceOperationStableId.ToString(),
                amount,
                PlayerExperienceGrantRequestV1.ComputeCommandFingerprint(
                    sourceOperationStableId,
                    amount),
                appliedSequence);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "source_operation_stable_id",
                SourceOperationStableId);
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "amount",
                Amount.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "command_fingerprint",
                CommandFingerprint);
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "applied_sequence",
                AppliedSequence.ToString(CultureInfo.InvariantCulture));
            return builder.ToString();
        }
    }

    /// <summary>
    /// Canonical engine-independent export of XP authority state and replay facts.
    /// </summary>
    public sealed class PlayerExperienceSnapshotV1
    {
        private const string SchemaId = "player-experience-snapshot-v1";

        public const int CurrentSchemaVersion = 1;

        public PlayerExperienceSnapshotV1(
            int schemaVersion,
            string authorityStableId,
            long sequence,
            string curveFingerprint,
            long cumulativeExperience,
            ProgressionContext progressionContext,
            IEnumerable<PlayerExperienceGrantSnapshotV1> grants,
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

        public IReadOnlyList<PlayerExperienceGrantSnapshotV1> Grants { get; }

        public string Fingerprint { get; }

        public static PlayerExperienceSnapshotV1 CreateCanonical(
            long sequence,
            string curveFingerprint,
            long cumulativeExperience,
            ProgressionContext progressionContext,
            IEnumerable<PlayerExperienceGrantSnapshotV1> grants)
        {
            var provisional = new PlayerExperienceSnapshotV1(
                CurrentSchemaVersion,
                PlayerExperienceIdsV1.AuthorityStableId.ToString(),
                sequence,
                curveFingerprint,
                cumulativeExperience,
                progressionContext,
                grants,
                string.Empty);
            return new PlayerExperienceSnapshotV1(
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
            PlayerExperienceFormatV1.AppendToken(builder, "schema", SchemaId);
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "schema_version",
                SchemaVersion.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "authority_stable_id",
                AuthorityStableId);
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "sequence",
                Sequence.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "curve_fingerprint",
                CurveFingerprint);
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "cumulative_experience",
                CumulativeExperience.ToString(CultureInfo.InvariantCulture));
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "progression_context",
                ProgressionContext == null
                    ? string.Empty
                    : ProgressionContext.ToCanonicalString());
            PlayerExperienceFormatV1.AppendToken(
                builder,
                "grant_count",
                Grants.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < Grants.Count; index++)
            {
                PlayerExperienceGrantSnapshotV1 grant = Grants[index];
                PlayerExperienceFormatV1.AppendToken(
                    builder,
                    "grant_" + index.ToString("D4", CultureInfo.InvariantCulture),
                    grant == null ? string.Empty : grant.ToCanonicalString());
            }

            return builder.ToString();
        }

        private static string ComputeFingerprint(
            PlayerExperienceSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return PlayerExperienceFormatV1.ComputeSha256(
                snapshot.ToCanonicalString());
        }

        private static IReadOnlyList<PlayerExperienceGrantSnapshotV1>
            CopyAndOrder(IEnumerable<PlayerExperienceGrantSnapshotV1> source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var copy = new List<PlayerExperienceGrantSnapshotV1>(source);
            copy.Sort(CompareGrants);
            return new ReadOnlyCollection<PlayerExperienceGrantSnapshotV1>(copy);
        }

        private static int CompareGrants(
            PlayerExperienceGrantSnapshotV1 left,
            PlayerExperienceGrantSnapshotV1 right)
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
    public sealed class PlayerExperienceGrantFactV1
    {
        public PlayerExperienceGrantFactV1(
            StableId sourceOperationStableId,
            long amount,
            string commandFingerprint,
            PlayerExperienceGrantStatusV1 status,
            PlayerExperienceGrantStatusV1 originalStatus,
            string rejectionCode,
            PlayerExperienceStateV1 previousState,
            PlayerExperienceStateV1 currentState,
            PlayerExperienceSnapshotV1 previousSnapshot,
            PlayerExperienceSnapshotV1 currentSnapshot,
            IEnumerable<PlayerLevelUpFactV1> levelUpFacts)
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

            LevelUpFacts = new ReadOnlyCollection<PlayerLevelUpFactV1>(
                new List<PlayerLevelUpFactV1>(levelUpFacts));
        }

        public StableId SourceOperationStableId { get; }

        public long Amount { get; }

        public string CommandFingerprint { get; }

        public PlayerExperienceGrantStatusV1 Status { get; }

        public PlayerExperienceGrantStatusV1 OriginalStatus { get; }

        public string RejectionCode { get; }

        public PlayerExperienceStateV1 PreviousState { get; }

        public PlayerExperienceStateV1 CurrentState { get; }

        public PlayerExperienceSnapshotV1 PreviousSnapshot { get; }

        public PlayerExperienceSnapshotV1 CurrentSnapshot { get; }

        public IReadOnlyList<PlayerLevelUpFactV1> LevelUpFacts { get; }

        public bool Changed =>
            Status == PlayerExperienceGrantStatusV1.Applied;
    }

    public sealed class PlayerExperienceImportResultV1
    {
        public PlayerExperienceImportResultV1(
            PlayerExperienceImportStatusV1 status,
            string rejectionCode,
            PlayerExperienceSnapshotV1 previousSnapshot,
            PlayerExperienceSnapshotV1 currentSnapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            PreviousSnapshot = previousSnapshot
                ?? throw new ArgumentNullException(nameof(previousSnapshot));
            CurrentSnapshot = currentSnapshot
                ?? throw new ArgumentNullException(nameof(currentSnapshot));
        }

        public PlayerExperienceImportStatusV1 Status { get; }

        public string RejectionCode { get; }

        public PlayerExperienceSnapshotV1 PreviousSnapshot { get; }

        public PlayerExperienceSnapshotV1 CurrentSnapshot { get; }

        public bool Changed =>
            Status == PlayerExperienceImportStatusV1.Imported;
    }

    public interface IPlayerExperienceAuthorityV1 : IProgressionContextProvider
    {
        PlayerExperienceStateV1 CurrentState { get; }

        PlayerExperienceSnapshotV1 CurrentSnapshot { get; }

        PlayerExperienceGrantFactV1 Grant(
            PlayerExperienceGrantRequestV1 request);

        PlayerExperienceSnapshotV1 ExportSnapshot();

        PlayerExperienceImportResultV1 TryImport(
            PlayerExperienceSnapshotV1 snapshot);
    }
}
