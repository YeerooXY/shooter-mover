using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    public sealed class RunLocalStateSnapshot
    {
        public RunLocalStateSnapshot(
            long runCash,
            IDictionary<string, long> temporaryPickups,
            IDictionary<string, long> counters,
            IDictionary<string, long> missionStatistics)
        {
            if (runCash < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(runCash));
            }
            RunCash = runCash;
            TemporaryPickups = Freeze(temporaryPickups, nameof(temporaryPickups));
            Counters = Freeze(counters, nameof(counters));
            MissionStatistics = Freeze(
                missionStatistics,
                nameof(missionStatistics));
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public long RunCash { get; }
        public IReadOnlyDictionary<string, long> TemporaryPickups { get; }
        public IReadOnlyDictionary<string, long> Counters { get; }
        public IReadOnlyDictionary<string, long> MissionStatistics { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(builder, "run-cash", RunCash);
            AppendMap(builder, "pickup", TemporaryPickups);
            AppendMap(builder, "counter", Counters);
            AppendMap(builder, "statistic", MissionStatistics);
            return builder.ToString();
        }

        private static IReadOnlyDictionary<string, long> Freeze(
            IDictionary<string, long> source,
            string parameterName)
        {
            var copy = new SortedDictionary<string, long>(
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, long> pair in source
                ?? new Dictionary<string, long>())
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0L)
                {
                    throw new ArgumentException(
                        "Run-local snapshot keys must be non-empty and values non-negative.",
                        parameterName);
                }
                copy.Add(pair.Key.Trim(), pair.Value);
            }
            return new ReadOnlyDictionary<string, long>(copy);
        }

        private static void AppendMap(
            StringBuilder builder,
            string prefix,
            IReadOnlyDictionary<string, long> values)
        {
            foreach (KeyValuePair<string, long> pair in values)
            {
                RunSessionFingerprint.Append(
                    builder,
                    prefix + ":" + pair.Key,
                    pair.Value);
            }
        }
    }

    public sealed class RunHudSnapshot
    {
        public RunHudSnapshot(
            StableId runStableId,
            StableId selectedCharacterStableId,
            StableId participantStableId,
            RunSessionLifecycleState lifecycleState,
            long lifecycleGeneration,
            double currentHealth,
            double maximumHealth,
            long runCash,
            long collectedStrongboxCount,
            string combatProfileFingerprint)
        {
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            SelectedCharacterStableId = selectedCharacterStableId
                ?? throw new ArgumentNullException(
                    nameof(selectedCharacterStableId));
            ParticipantStableId = participantStableId
                ?? throw new ArgumentNullException(nameof(participantStableId));
            if (!Enum.IsDefined(
                typeof(RunSessionLifecycleState),
                lifecycleState))
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleState));
            }
            if (lifecycleGeneration < 0L
                || runCash < 0L
                || collectedStrongboxCount < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            if (string.IsNullOrWhiteSpace(combatProfileFingerprint))
            {
                throw new ArgumentException(
                    "A combat-profile fingerprint is required.",
                    nameof(combatProfileFingerprint));
            }
            LifecycleState = lifecycleState;
            LifecycleGeneration = lifecycleGeneration;
            CurrentHealth = currentHealth;
            MaximumHealth = maximumHealth;
            RunCash = runCash;
            CollectedStrongboxCount = collectedStrongboxCount;
            CombatProfileFingerprint = combatProfileFingerprint.Trim();
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public StableId RunStableId { get; }
        public StableId SelectedCharacterStableId { get; }
        public StableId ParticipantStableId { get; }
        public RunSessionLifecycleState LifecycleState { get; }
        public long LifecycleGeneration { get; }
        public double CurrentHealth { get; }
        public double MaximumHealth { get; }
        public long RunCash { get; }
        public long CollectedStrongboxCount { get; }
        public string CombatProfileFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(builder, "run", RunStableId);
            RunSessionFingerprint.Append(
                builder,
                "character",
                SelectedCharacterStableId);
            RunSessionFingerprint.Append(
                builder,
                "participant",
                ParticipantStableId);
            RunSessionFingerprint.Append(
                builder,
                "state",
                (int)LifecycleState);
            RunSessionFingerprint.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprint.Append(builder, "health", CurrentHealth);
            RunSessionFingerprint.Append(
                builder,
                "maximum-health",
                MaximumHealth);
            RunSessionFingerprint.Append(builder, "run-cash", RunCash);
            RunSessionFingerprint.Append(
                builder,
                "strongboxes",
                CollectedStrongboxCount);
            RunSessionFingerprint.Append(
                builder,
                "combat-profile",
                CombatProfileFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunDebugSnapshot
    {
        private readonly ReadOnlyDictionary<string, string> portFingerprints;

        public RunDebugSnapshot(
            StableId runStableId,
            RunSessionLifecycleState lifecycleState,
            long lifecycleGeneration,
            long authoritativeTick,
            string startCommandFingerprint,
            string frozenInputFingerprint,
            string localStateFingerprint,
            IDictionary<string, string> runtimePortFingerprints,
            string terminalResultFingerprint)
        {
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (!Enum.IsDefined(
                typeof(RunSessionLifecycleState),
                lifecycleState))
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleState));
            }
            if (lifecycleGeneration < 0L || authoritativeTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            if (string.IsNullOrWhiteSpace(startCommandFingerprint)
                || string.IsNullOrWhiteSpace(frozenInputFingerprint)
                || string.IsNullOrWhiteSpace(localStateFingerprint))
            {
                throw new ArgumentException(
                    "Debug snapshots require deterministic command, input, and local-state fingerprints.");
            }
            var copy = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in runtimePortFingerprints
                ?? throw new ArgumentNullException(nameof(runtimePortFingerprints)))
            {
                if (string.IsNullOrWhiteSpace(pair.Key)
                    || string.IsNullOrWhiteSpace(pair.Value))
                {
                    throw new ArgumentException(
                        "Runtime-port identities and fingerprints must be non-empty.",
                        nameof(runtimePortFingerprints));
                }
                copy.Add(pair.Key.Trim(), pair.Value.Trim());
            }

            LifecycleState = lifecycleState;
            LifecycleGeneration = lifecycleGeneration;
            AuthoritativeTick = authoritativeTick;
            StartCommandFingerprint = startCommandFingerprint.Trim();
            FrozenInputFingerprint = frozenInputFingerprint.Trim();
            LocalStateFingerprint = localStateFingerprint.Trim();
            portFingerprints = new ReadOnlyDictionary<string, string>(copy);
            TerminalResultFingerprint = terminalResultFingerprint ?? string.Empty;
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public StableId RunStableId { get; }
        public RunSessionLifecycleState LifecycleState { get; }
        public long LifecycleGeneration { get; }
        public long AuthoritativeTick { get; }
        public string StartCommandFingerprint { get; }
        public string FrozenInputFingerprint { get; }
        public string LocalStateFingerprint { get; }
        public IReadOnlyDictionary<string, string> RuntimePortFingerprints
        {
            get { return portFingerprints; }
        }
        public string TerminalResultFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(builder, "run", RunStableId);
            RunSessionFingerprint.Append(builder, "state", (int)LifecycleState);
            RunSessionFingerprint.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprint.Append(
                builder,
                "tick",
                AuthoritativeTick);
            RunSessionFingerprint.Append(
                builder,
                "start-command",
                StartCommandFingerprint);
            RunSessionFingerprint.Append(
                builder,
                "frozen-inputs",
                FrozenInputFingerprint);
            RunSessionFingerprint.Append(
                builder,
                "local-state",
                LocalStateFingerprint);
            foreach (KeyValuePair<string, string> pair in portFingerprints)
            {
                RunSessionFingerprint.Append(
                    builder,
                    "port:" + pair.Key,
                    pair.Value);
            }
            RunSessionFingerprint.Append(
                builder,
                "terminal-result",
                TerminalResultFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunRecoveryDiagnosticSnapshot
    {
        public RunRecoveryDiagnosticSnapshot(
            RunDebugSnapshot debug,
            string permanentCharacterFingerprint,
            long permanentCharacterRevision,
            bool isPermanentCharacterTruth)
        {
            Debug = debug ?? throw new ArgumentNullException(nameof(debug));
            if (string.IsNullOrWhiteSpace(permanentCharacterFingerprint))
            {
                throw new ArgumentException(
                    "A permanent-character fingerprint is required.",
                    nameof(permanentCharacterFingerprint));
            }
            if (permanentCharacterRevision < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(permanentCharacterRevision));
            }
            PermanentCharacterFingerprint =
                permanentCharacterFingerprint.Trim();
            PermanentCharacterRevision = permanentCharacterRevision;
            IsPermanentCharacterTruth = isPermanentCharacterTruth;
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public RunDebugSnapshot Debug { get; }
        public string PermanentCharacterFingerprint { get; }
        public long PermanentCharacterRevision { get; }
        public bool IsPermanentCharacterTruth { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(builder, "debug", Debug.Fingerprint);
            RunSessionFingerprint.Append(
                builder,
                "permanent-character",
                PermanentCharacterFingerprint);
            RunSessionFingerprint.Append(
                builder,
                "permanent-revision",
                PermanentCharacterRevision);
            RunSessionFingerprint.Append(
                builder,
                "is-permanent-truth",
                IsPermanentCharacterTruth);
            return builder.ToString();
        }
    }

    public sealed class RunCheckpoint
    {
        public const int CurrentSchemaVersion = 1;

        public RunCheckpoint(
            RunRecoveryDiagnosticSnapshot recovery,
            RunLocalStateSnapshot localState,
            int schemaVersion = CurrentSchemaVersion)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }
            SchemaVersion = schemaVersion;
            Recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
            LocalState = localState ?? throw new ArgumentNullException(nameof(localState));
            if (Recovery.IsPermanentCharacterTruth)
            {
                throw new ArgumentException(
                    "A transient run checkpoint cannot be permanent character truth.",
                    nameof(recovery));
            }
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public int SchemaVersion { get; }
        public RunRecoveryDiagnosticSnapshot Recovery { get; }
        public RunLocalStateSnapshot LocalState { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(builder, "schema", SchemaVersion);
            RunSessionFingerprint.Append(
                builder,
                "recovery",
                Recovery.Fingerprint);
            RunSessionFingerprint.Append(
                builder,
                "local-state",
                LocalState.Fingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunSessionStartResult
    {
        public RunSessionStartResult(
            RunSessionStartStatus status,
            StableId operationStableId,
            string commandFingerprint,
            StableId runStableId,
            string runSnapshotFingerprint,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(RunSessionStartStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            OperationStableId = operationStableId;
            CommandFingerprint = commandFingerprint ?? string.Empty;
            RunStableId = runStableId;
            RunSnapshotFingerprint = runSnapshotFingerprint ?? string.Empty;
            RejectionCode = rejectionCode ?? string.Empty;
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public RunSessionStartStatus Status { get; }
        public StableId OperationStableId { get; }
        public string CommandFingerprint { get; }
        public StableId RunStableId { get; }
        public string RunSnapshotFingerprint { get; }
        public string RejectionCode { get; }
        public string Fingerprint { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RunSessionStartStatus.Started
                    || Status == RunSessionStartStatus.ExactReplay;
            }
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(builder, "status", (int)Status);
            RunSessionFingerprint.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprint.Append(
                builder,
                "command",
                CommandFingerprint);
            RunSessionFingerprint.Append(builder, "run", RunStableId);
            RunSessionFingerprint.Append(
                builder,
                "run-snapshot",
                RunSnapshotFingerprint);
            RunSessionFingerprint.Append(
                builder,
                "rejection",
                RejectionCode);
            return builder.ToString();
        }
    }

    public sealed class RunSessionRestartResult
    {
        public RunSessionRestartResult(
            RunSessionRestartStatus status,
            RestartRunSessionCommand command,
            long lifecycleGeneration,
            string snapshotFingerprint,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(RunSessionRestartStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            }
            Status = status;
            Command = command;
            LifecycleGeneration = lifecycleGeneration;
            SnapshotFingerprint = snapshotFingerprint ?? string.Empty;
            RejectionCode = rejectionCode ?? string.Empty;
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public RunSessionRestartStatus Status { get; }
        public RestartRunSessionCommand Command { get; }
        public long LifecycleGeneration { get; }
        public string SnapshotFingerprint { get; }
        public string RejectionCode { get; }
        public string Fingerprint { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RunSessionRestartStatus.Applied
                    || Status == RunSessionRestartStatus.ExactReplay;
            }
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(builder, "status", (int)Status);
            RunSessionFingerprint.Append(
                builder,
                "command",
                Command == null ? string.Empty : Command.Fingerprint);
            RunSessionFingerprint.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprint.Append(
                builder,
                "snapshot",
                SnapshotFingerprint);
            RunSessionFingerprint.Append(
                builder,
                "rejection",
                RejectionCode);
            return builder.ToString();
        }
    }

    public sealed class RunSessionEndReceipt
    {
        public RunSessionEndReceipt(
            StableId runStableId,
            StableId selectedCharacterStableId,
            long expectedCharacterRevision,
            string expectedCharacterFingerprint,
            StableId missionLayoutStableId,
            StableId difficultyStableId,
            long deterministicSeed,
            string frozenInputFingerprint,
            string combatProfileFingerprint,
            RunLocalStateSnapshot localState,
            MissionResultPayload missionResult)
        {
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            SelectedCharacterStableId = selectedCharacterStableId
                ?? throw new ArgumentNullException(
                    nameof(selectedCharacterStableId));
            if (expectedCharacterRevision < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedCharacterRevision));
            }
            if (string.IsNullOrWhiteSpace(expectedCharacterFingerprint)
                || string.IsNullOrWhiteSpace(frozenInputFingerprint)
                || string.IsNullOrWhiteSpace(combatProfileFingerprint))
            {
                throw new ArgumentException(
                    "End-run receipts require complete frozen-input fingerprints.");
            }
            MissionLayoutStableId = missionLayoutStableId
                ?? throw new ArgumentNullException(nameof(missionLayoutStableId));
            DifficultyStableId = difficultyStableId
                ?? throw new ArgumentNullException(nameof(difficultyStableId));
            LocalState = localState
                ?? throw new ArgumentNullException(nameof(localState));
            MissionResult = missionResult
                ?? throw new ArgumentNullException(nameof(missionResult));
            if (MissionResult.RunStableId != RunStableId
                || MissionResult.RoutePayload.SelectedCharacterStableId
                    != SelectedCharacterStableId)
            {
                throw new ArgumentException(
                    "Existing mission-result identity must match the frozen run and character.",
                    nameof(missionResult));
            }

            ExpectedCharacterRevision = expectedCharacterRevision;
            ExpectedCharacterFingerprint = expectedCharacterFingerprint.Trim();
            DeterministicSeed = deterministicSeed;
            FrozenInputFingerprint = frozenInputFingerprint.Trim();
            CombatProfileFingerprint = combatProfileFingerprint.Trim();
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public StableId RunStableId { get; }
        public StableId SelectedCharacterStableId { get; }
        public long ExpectedCharacterRevision { get; }
        public string ExpectedCharacterFingerprint { get; }
        public StableId MissionLayoutStableId { get; }
        public StableId DifficultyStableId { get; }
        public long DeterministicSeed { get; }
        public string FrozenInputFingerprint { get; }
        public string CombatProfileFingerprint { get; }
        public RunLocalStateSnapshot LocalState { get; }
        public MissionResultPayload MissionResult { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(builder, "run", RunStableId);
            RunSessionFingerprint.Append(
                builder,
                "character",
                SelectedCharacterStableId);
            RunSessionFingerprint.Append(
                builder,
                "character-revision",
                ExpectedCharacterRevision);
            RunSessionFingerprint.Append(
                builder,
                "character-fingerprint",
                ExpectedCharacterFingerprint);
            RunSessionFingerprint.Append(
                builder,
                "mission",
                MissionLayoutStableId);
            RunSessionFingerprint.Append(
                builder,
                "difficulty",
                DifficultyStableId);
            RunSessionFingerprint.Append(builder, "seed", DeterministicSeed);
            RunSessionFingerprint.Append(
                builder,
                "frozen-inputs",
                FrozenInputFingerprint);
            RunSessionFingerprint.Append(
                builder,
                "combat-profile",
                CombatProfileFingerprint);
            RunSessionFingerprint.Append(
                builder,
                "local-state",
                LocalState.Fingerprint);
            RunSessionFingerprint.Append(
                builder,
                "mission-result",
                MissionResult.Fingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunSessionEndResult
    {
        public RunSessionEndResult(
            RunSessionEndStatus status,
            EndRunSessionCommand command,
            RunSessionEndReceipt receipt,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(RunSessionEndStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Command = command;
            Receipt = receipt;
            RejectionCode = rejectionCode ?? string.Empty;
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public RunSessionEndStatus Status { get; }
        public EndRunSessionCommand Command { get; }
        public RunSessionEndReceipt Receipt { get; }
        public string RejectionCode { get; }
        public string Fingerprint { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RunSessionEndStatus.Ended
                    || Status == RunSessionEndStatus.ExactReplay;
            }
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(builder, "status", (int)Status);
            RunSessionFingerprint.Append(
                builder,
                "command",
                Command == null ? string.Empty : Command.Fingerprint);
            RunSessionFingerprint.Append(
                builder,
                "receipt",
                Receipt == null ? string.Empty : Receipt.Fingerprint);
            RunSessionFingerprint.Append(
                builder,
                "rejection",
                RejectionCode);
            return builder.ToString();
        }
    }

    public sealed class RunSessionFactAdmissionResult
    {
        public RunSessionFactAdmissionResult(
            RunSessionFactAdmissionStatus status,
            RunSessionFactEnvelope fact,
            string rejectionCode)
        {
            Status = status;
            Fact = fact;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public RunSessionFactAdmissionStatus Status { get; }
        public RunSessionFactEnvelope Fact { get; }
        public string RejectionCode { get; }
        public bool Accepted
        {
            get
            {
                return Status == RunSessionFactAdmissionStatus.Accepted
                    || Status == RunSessionFactAdmissionStatus.ExactReplay;
            }
        }
    }

    public sealed class RunLocalMutationResult
    {
        public RunLocalMutationResult(
            bool accepted,
            bool exactReplay,
            bool conflictingDuplicate,
            RunLocalMutationCommand command,
            RunLocalStateSnapshot state,
            string rejectionCode)
        {
            Accepted = accepted;
            ExactReplay = exactReplay;
            ConflictingDuplicate = conflictingDuplicate;
            Command = command;
            State = state;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public bool Accepted { get; }
        public bool ExactReplay { get; }
        public bool ConflictingDuplicate { get; }
        public RunLocalMutationCommand Command { get; }
        public RunLocalStateSnapshot State { get; }
        public string RejectionCode { get; }
    }
}
