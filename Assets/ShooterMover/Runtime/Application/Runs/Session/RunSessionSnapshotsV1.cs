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
    public sealed class RunLocalStateSnapshotV1
    {
        public RunLocalStateSnapshotV1(
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public long RunCash { get; }
        public IReadOnlyDictionary<string, long> TemporaryPickups { get; }
        public IReadOnlyDictionary<string, long> Counters { get; }
        public IReadOnlyDictionary<string, long> MissionStatistics { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(builder, "run-cash", RunCash);
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
                RunSessionFingerprintV1.Append(
                    builder,
                    prefix + ":" + pair.Key,
                    pair.Value);
            }
        }
    }

    public sealed class RunHudSnapshotV1
    {
        public RunHudSnapshotV1(
            StableId runStableId,
            StableId selectedCharacterStableId,
            StableId participantStableId,
            RunSessionLifecycleStateV1 lifecycleState,
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
                typeof(RunSessionLifecycleStateV1),
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public StableId RunStableId { get; }
        public StableId SelectedCharacterStableId { get; }
        public StableId ParticipantStableId { get; }
        public RunSessionLifecycleStateV1 LifecycleState { get; }
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
            RunSessionFingerprintV1.Append(builder, "run", RunStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "character",
                SelectedCharacterStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "participant",
                ParticipantStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "state",
                (int)LifecycleState);
            RunSessionFingerprintV1.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprintV1.Append(builder, "health", CurrentHealth);
            RunSessionFingerprintV1.Append(
                builder,
                "maximum-health",
                MaximumHealth);
            RunSessionFingerprintV1.Append(builder, "run-cash", RunCash);
            RunSessionFingerprintV1.Append(
                builder,
                "strongboxes",
                CollectedStrongboxCount);
            RunSessionFingerprintV1.Append(
                builder,
                "combat-profile",
                CombatProfileFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunDebugSnapshotV1
    {
        private readonly ReadOnlyDictionary<string, string> portFingerprints;

        public RunDebugSnapshotV1(
            StableId runStableId,
            RunSessionLifecycleStateV1 lifecycleState,
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
                typeof(RunSessionLifecycleStateV1),
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public StableId RunStableId { get; }
        public RunSessionLifecycleStateV1 LifecycleState { get; }
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
            RunSessionFingerprintV1.Append(builder, "run", RunStableId);
            RunSessionFingerprintV1.Append(builder, "state", (int)LifecycleState);
            RunSessionFingerprintV1.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprintV1.Append(
                builder,
                "tick",
                AuthoritativeTick);
            RunSessionFingerprintV1.Append(
                builder,
                "start-command",
                StartCommandFingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "frozen-inputs",
                FrozenInputFingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "local-state",
                LocalStateFingerprint);
            foreach (KeyValuePair<string, string> pair in portFingerprints)
            {
                RunSessionFingerprintV1.Append(
                    builder,
                    "port:" + pair.Key,
                    pair.Value);
            }
            RunSessionFingerprintV1.Append(
                builder,
                "terminal-result",
                TerminalResultFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunRecoveryDiagnosticSnapshotV1
    {
        public RunRecoveryDiagnosticSnapshotV1(
            RunDebugSnapshotV1 debug,
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public RunDebugSnapshotV1 Debug { get; }
        public string PermanentCharacterFingerprint { get; }
        public long PermanentCharacterRevision { get; }
        public bool IsPermanentCharacterTruth { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(builder, "debug", Debug.Fingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "permanent-character",
                PermanentCharacterFingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "permanent-revision",
                PermanentCharacterRevision);
            RunSessionFingerprintV1.Append(
                builder,
                "is-permanent-truth",
                IsPermanentCharacterTruth);
            return builder.ToString();
        }
    }

    public sealed class RunCheckpointV1
    {
        public const int CurrentSchemaVersion = 1;

        public RunCheckpointV1(
            RunRecoveryDiagnosticSnapshotV1 recovery,
            RunLocalStateSnapshotV1 localState,
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public int SchemaVersion { get; }
        public RunRecoveryDiagnosticSnapshotV1 Recovery { get; }
        public RunLocalStateSnapshotV1 LocalState { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(builder, "schema", SchemaVersion);
            RunSessionFingerprintV1.Append(
                builder,
                "recovery",
                Recovery.Fingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "local-state",
                LocalState.Fingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunSessionStartResultV1
    {
        public RunSessionStartResultV1(
            RunSessionStartStatusV1 status,
            StableId operationStableId,
            string commandFingerprint,
            StableId runStableId,
            string runSnapshotFingerprint,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(RunSessionStartStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            OperationStableId = operationStableId;
            CommandFingerprint = commandFingerprint ?? string.Empty;
            RunStableId = runStableId;
            RunSnapshotFingerprint = runSnapshotFingerprint ?? string.Empty;
            RejectionCode = rejectionCode ?? string.Empty;
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public RunSessionStartStatusV1 Status { get; }
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
                return Status == RunSessionStartStatusV1.Started
                    || Status == RunSessionStartStatusV1.ExactReplay;
            }
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(builder, "status", (int)Status);
            RunSessionFingerprintV1.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "command",
                CommandFingerprint);
            RunSessionFingerprintV1.Append(builder, "run", RunStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "run-snapshot",
                RunSnapshotFingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "rejection",
                RejectionCode);
            return builder.ToString();
        }
    }

    public sealed class RunSessionRestartResultV1
    {
        public RunSessionRestartResultV1(
            RunSessionRestartStatusV1 status,
            RestartRunSessionCommandV1 command,
            long lifecycleGeneration,
            string snapshotFingerprint,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(RunSessionRestartStatusV1), status))
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public RunSessionRestartStatusV1 Status { get; }
        public RestartRunSessionCommandV1 Command { get; }
        public long LifecycleGeneration { get; }
        public string SnapshotFingerprint { get; }
        public string RejectionCode { get; }
        public string Fingerprint { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RunSessionRestartStatusV1.Applied
                    || Status == RunSessionRestartStatusV1.ExactReplay;
            }
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(builder, "status", (int)Status);
            RunSessionFingerprintV1.Append(
                builder,
                "command",
                Command == null ? string.Empty : Command.Fingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprintV1.Append(
                builder,
                "snapshot",
                SnapshotFingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "rejection",
                RejectionCode);
            return builder.ToString();
        }
    }

    public sealed class RunSessionEndReceiptV1
    {
        public RunSessionEndReceiptV1(
            StableId runStableId,
            StableId selectedCharacterStableId,
            long expectedCharacterRevision,
            string expectedCharacterFingerprint,
            StableId missionLayoutStableId,
            StableId difficultyStableId,
            long deterministicSeed,
            string frozenInputFingerprint,
            string combatProfileFingerprint,
            RunLocalStateSnapshotV1 localState,
            MissionResultPayloadV1 missionResult)
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
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
        public RunLocalStateSnapshotV1 LocalState { get; }
        public MissionResultPayloadV1 MissionResult { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(builder, "run", RunStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "character",
                SelectedCharacterStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "character-revision",
                ExpectedCharacterRevision);
            RunSessionFingerprintV1.Append(
                builder,
                "character-fingerprint",
                ExpectedCharacterFingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "mission",
                MissionLayoutStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "difficulty",
                DifficultyStableId);
            RunSessionFingerprintV1.Append(builder, "seed", DeterministicSeed);
            RunSessionFingerprintV1.Append(
                builder,
                "frozen-inputs",
                FrozenInputFingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "combat-profile",
                CombatProfileFingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "local-state",
                LocalState.Fingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "mission-result",
                MissionResult.Fingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunSessionEndResultV1
    {
        public RunSessionEndResultV1(
            RunSessionEndStatusV1 status,
            EndRunSessionCommandV1 command,
            RunSessionEndReceiptV1 receipt,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(RunSessionEndStatusV1), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            Status = status;
            Command = command;
            Receipt = receipt;
            RejectionCode = rejectionCode ?? string.Empty;
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public RunSessionEndStatusV1 Status { get; }
        public EndRunSessionCommandV1 Command { get; }
        public RunSessionEndReceiptV1 Receipt { get; }
        public string RejectionCode { get; }
        public string Fingerprint { get; }
        public bool Succeeded
        {
            get
            {
                return Status == RunSessionEndStatusV1.Ended
                    || Status == RunSessionEndStatusV1.ExactReplay;
            }
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(builder, "status", (int)Status);
            RunSessionFingerprintV1.Append(
                builder,
                "command",
                Command == null ? string.Empty : Command.Fingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "receipt",
                Receipt == null ? string.Empty : Receipt.Fingerprint);
            RunSessionFingerprintV1.Append(
                builder,
                "rejection",
                RejectionCode);
            return builder.ToString();
        }
    }

    public sealed class RunSessionFactAdmissionResultV1
    {
        public RunSessionFactAdmissionResultV1(
            RunSessionFactAdmissionStatusV1 status,
            RunSessionFactEnvelopeV1 fact,
            string rejectionCode)
        {
            Status = status;
            Fact = fact;
            RejectionCode = rejectionCode ?? string.Empty;
        }

        public RunSessionFactAdmissionStatusV1 Status { get; }
        public RunSessionFactEnvelopeV1 Fact { get; }
        public string RejectionCode { get; }
        public bool Accepted
        {
            get
            {
                return Status == RunSessionFactAdmissionStatusV1.Accepted
                    || Status == RunSessionFactAdmissionStatusV1.ExactReplay;
            }
        }
    }

    public sealed class RunLocalMutationResultV1
    {
        public RunLocalMutationResultV1(
            bool accepted,
            bool exactReplay,
            bool conflictingDuplicate,
            RunLocalMutationCommandV1 command,
            RunLocalStateSnapshotV1 state,
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
        public RunLocalMutationCommandV1 Command { get; }
        public RunLocalStateSnapshotV1 State { get; }
        public string RejectionCode { get; }
    }
}
