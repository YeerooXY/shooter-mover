using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    public enum RunSessionLifecycleState
    {
        Active = 1,
        Ended = 2,
    }

    public enum RunSessionStartStatus
    {
        Started = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
    }

    public enum RunSessionRestartStatus
    {
        Applied = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
    }

    public enum RunSessionEndStatus
    {
        Ended = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
    }

    public enum RunSessionFactKind
    {
        Damage = 1,
        Projectile = 2,
        StatusEffect = 3,
        AbilityCast = 4,
        Contact = 5,
    }

    public enum RunSessionFactAdmissionStatus
    {
        Accepted = 1,
        ExactReplay = 2,
        WrongRun = 3,
        StaleLifecycle = 4,
        RunEnded = 5,
        ConflictingDuplicate = 6,
    }

    public enum RunLocalMutationKind
    {
        AddTemporaryPickup = 1,
        AddRunCash = 2,
        IncrementCounter = 3,
        IncrementStatistic = 4,
    }

    public sealed class StartRunSessionCommand
    {
        public const int CurrentSchemaVersion = 1;

        public StartRunSessionCommand(
            StableId operationStableId,
            StableId requestedRunStableId,
            string runInstanceIdentityMaterial,
            StableId selectedCharacterInstanceStableId,
            long expectedCharacterRevision,
            string expectedCharacterFingerprint,
            StableId missionLayoutStableId,
            StableId difficultyStableId,
            long deterministicSeed,
            long authoritativeInitialTick,
            string eventModifierContextFingerprint,
            int schemaVersion = CurrentSchemaVersion)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                throw new ArgumentOutOfRangeException(nameof(schemaVersion));
            }
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            if (requestedRunStableId == null
                && string.IsNullOrWhiteSpace(runInstanceIdentityMaterial))
            {
                throw new ArgumentException(
                    "A requested run identity or explicit run-instance identity material is required.",
                    nameof(runInstanceIdentityMaterial));
            }
            SelectedCharacterInstanceStableId = selectedCharacterInstanceStableId
                ?? throw new ArgumentNullException(
                    nameof(selectedCharacterInstanceStableId));
            if (expectedCharacterRevision < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedCharacterRevision));
            }
            if (string.IsNullOrWhiteSpace(expectedCharacterFingerprint))
            {
                throw new ArgumentException(
                    "An expected permanent-character fingerprint is required.",
                    nameof(expectedCharacterFingerprint));
            }
            MissionLayoutStableId = missionLayoutStableId
                ?? throw new ArgumentNullException(nameof(missionLayoutStableId));
            DifficultyStableId = difficultyStableId
                ?? throw new ArgumentNullException(nameof(difficultyStableId));
            if (authoritativeInitialTick < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(authoritativeInitialTick));
            }
            if (string.IsNullOrWhiteSpace(eventModifierContextFingerprint))
            {
                throw new ArgumentException(
                    "An explicit event/modifier context fingerprint is required.",
                    nameof(eventModifierContextFingerprint));
            }

            SchemaVersion = schemaVersion;
            RequestedRunStableId = requestedRunStableId;
            RunInstanceIdentityMaterial =
                (runInstanceIdentityMaterial ?? string.Empty).Trim();
            ExpectedCharacterRevision = expectedCharacterRevision;
            ExpectedCharacterFingerprint = expectedCharacterFingerprint.Trim();
            DeterministicSeed = deterministicSeed;
            AuthoritativeInitialTick = authoritativeInitialTick;
            EventModifierContextFingerprint =
                eventModifierContextFingerprint.Trim();
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public int SchemaVersion { get; }
        public StableId OperationStableId { get; }
        public StableId RequestedRunStableId { get; }
        public string RunInstanceIdentityMaterial { get; }
        public StableId SelectedCharacterInstanceStableId { get; }
        public long ExpectedCharacterRevision { get; }
        public string ExpectedCharacterFingerprint { get; }
        public StableId MissionLayoutStableId { get; }
        public StableId DifficultyStableId { get; }
        public long DeterministicSeed { get; }
        public long AuthoritativeInitialTick { get; }
        public string EventModifierContextFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(builder, "schema", SchemaVersion);
            RunSessionFingerprint.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprint.Append(
                builder,
                "requested-run",
                RequestedRunStableId);
            RunSessionFingerprint.Append(
                builder,
                "run-material",
                RunInstanceIdentityMaterial);
            RunSessionFingerprint.Append(
                builder,
                "character",
                SelectedCharacterInstanceStableId);
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
                "mission-layout",
                MissionLayoutStableId);
            RunSessionFingerprint.Append(
                builder,
                "difficulty",
                DifficultyStableId);
            RunSessionFingerprint.Append(
                builder,
                "seed",
                DeterministicSeed);
            RunSessionFingerprint.Append(
                builder,
                "initial-tick",
                AuthoritativeInitialTick);
            RunSessionFingerprint.Append(
                builder,
                "event-context",
                EventModifierContextFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunRestartPolicy
    {
        public RunRestartPolicy(
            string policyId,
            bool retainMissionStatistics,
            bool retainRunCounters,
            bool retainRunCash,
            bool retainTemporaryPickups)
        {
            if (string.IsNullOrWhiteSpace(policyId))
            {
                throw new ArgumentException(
                    "A restart-policy identity is required.",
                    nameof(policyId));
            }
            PolicyId = policyId.Trim();
            RetainMissionStatistics = retainMissionStatistics;
            RetainRunCounters = retainRunCounters;
            RetainRunCash = retainRunCash;
            RetainTemporaryPickups = retainTemporaryPickups;
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public string PolicyId { get; }
        public bool RetainMissionStatistics { get; }
        public bool RetainRunCounters { get; }
        public bool RetainRunCash { get; }
        public bool RetainTemporaryPickups { get; }
        public string Fingerprint { get; }

        public static RunRestartPolicy FullTransientReset()
        {
            return new RunRestartPolicy(
                "run-restart.full-transient-reset-v1",
                false,
                false,
                false,
                false);
        }

        public static RunRestartPolicy RespawnPreservingMissionProgress()
        {
            return new RunRestartPolicy(
                "run-restart.respawn-preserve-progress-v1",
                true,
                true,
                true,
                true);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(builder, "policy", PolicyId);
            RunSessionFingerprint.Append(
                builder,
                "retain-statistics",
                RetainMissionStatistics);
            RunSessionFingerprint.Append(
                builder,
                "retain-counters",
                RetainRunCounters);
            RunSessionFingerprint.Append(
                builder,
                "retain-cash",
                RetainRunCash);
            RunSessionFingerprint.Append(
                builder,
                "retain-pickups",
                RetainTemporaryPickups);
            return builder.ToString();
        }
    }

    public sealed class RestartRunSessionCommand
    {
        public RestartRunSessionCommand(
            StableId operationStableId,
            StableId runStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick,
            RunRestartPolicy policy)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (retiringLifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(retiringLifecycleGeneration));
            }
            if (replacementLifecycleGeneration
                != retiringLifecycleGeneration + 1L)
            {
                throw new ArgumentException(
                    "A run restart must increment lifecycle generation exactly once.",
                    nameof(replacementLifecycleGeneration));
            }
            if (authoritativeTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(authoritativeTick));
            }
            RetiringLifecycleGeneration = retiringLifecycleGeneration;
            ReplacementLifecycleGeneration = replacementLifecycleGeneration;
            AuthoritativeTick = authoritativeTick;
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public long RetiringLifecycleGeneration { get; }
        public long ReplacementLifecycleGeneration { get; }
        public long AuthoritativeTick { get; }
        public RunRestartPolicy Policy { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprint.Append(builder, "run", RunStableId);
            RunSessionFingerprint.Append(
                builder,
                "retiring-generation",
                RetiringLifecycleGeneration);
            RunSessionFingerprint.Append(
                builder,
                "replacement-generation",
                ReplacementLifecycleGeneration);
            RunSessionFingerprint.Append(
                builder,
                "tick",
                AuthoritativeTick);
            RunSessionFingerprint.Append(
                builder,
                "policy",
                Policy.Fingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunSessionFactEnvelope
    {
        public RunSessionFactEnvelope(
            StableId operationStableId,
            StableId runStableId,
            long lifecycleGeneration,
            RunSessionFactKind kind,
            string factFingerprint)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifecycleGeneration));
            }
            if (!Enum.IsDefined(typeof(RunSessionFactKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (string.IsNullOrWhiteSpace(factFingerprint))
            {
                throw new ArgumentException(
                    "An immutable upstream fact fingerprint is required.",
                    nameof(factFingerprint));
            }
            LifecycleGeneration = lifecycleGeneration;
            Kind = kind;
            FactFingerprint = factFingerprint.Trim();
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public RunSessionFactKind Kind { get; }
        public string FactFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprint.Append(builder, "run", RunStableId);
            RunSessionFingerprint.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprint.Append(
                builder,
                "kind",
                (int)Kind);
            RunSessionFingerprint.Append(
                builder,
                "fact",
                FactFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunStrongboxCollectionRequest
    {
        public RunStrongboxCollectionRequest(
            StableId operationStableId,
            StableId runStableId,
            long lifecycleGeneration,
            StableId definitionStableId,
            StableId instanceStableId,
            StableId grantStableId,
            StableId sourceStableId)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifecycleGeneration));
            }
            DefinitionStableId = definitionStableId
                ?? throw new ArgumentNullException(nameof(definitionStableId));
            InstanceStableId = instanceStableId
                ?? throw new ArgumentNullException(nameof(instanceStableId));
            GrantStableId = grantStableId
                ?? throw new ArgumentNullException(nameof(grantStableId));
            SourceStableId = sourceStableId
                ?? throw new ArgumentNullException(nameof(sourceStableId));
            LifecycleGeneration = lifecycleGeneration;
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public StableId DefinitionStableId { get; }
        public StableId InstanceStableId { get; }
        public StableId GrantStableId { get; }
        public StableId SourceStableId { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprint.Append(builder, "run", RunStableId);
            RunSessionFingerprint.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprint.Append(
                builder,
                "definition",
                DefinitionStableId);
            RunSessionFingerprint.Append(
                builder,
                "instance",
                InstanceStableId);
            RunSessionFingerprint.Append(builder, "grant", GrantStableId);
            RunSessionFingerprint.Append(builder, "source", SourceStableId);
            return builder.ToString();
        }
    }

    public sealed class RunLocalMutationCommand
    {
        public RunLocalMutationCommand(
            StableId operationStableId,
            StableId runStableId,
            long lifecycleGeneration,
            RunLocalMutationKind kind,
            string key,
            long amount,
            string provenanceFingerprint)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifecycleGeneration));
            }
            if (!Enum.IsDefined(typeof(RunLocalMutationKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException(
                    "A run-local mutation key is required.",
                    nameof(key));
            }
            if (amount <= 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }
            if (string.IsNullOrWhiteSpace(provenanceFingerprint))
            {
                throw new ArgumentException(
                    "Run-local state changes require explicit provenance.",
                    nameof(provenanceFingerprint));
            }
            LifecycleGeneration = lifecycleGeneration;
            Kind = kind;
            Key = key.Trim();
            Amount = amount;
            ProvenanceFingerprint = provenanceFingerprint.Trim();
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public RunLocalMutationKind Kind { get; }
        public string Key { get; }
        public long Amount { get; }
        public string ProvenanceFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprint.Append(builder, "run", RunStableId);
            RunSessionFingerprint.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprint.Append(
                builder,
                "kind",
                (int)Kind);
            RunSessionFingerprint.Append(builder, "key", Key);
            RunSessionFingerprint.Append(builder, "amount", Amount);
            RunSessionFingerprint.Append(
                builder,
                "provenance",
                ProvenanceFingerprint);
            return builder.ToString();
        }
    }

    public sealed class EndRunSessionCommand
    {
        public EndRunSessionCommand(
            StableId operationStableId,
            StableId runStableId,
            long lifecycleGeneration,
            MissionRunCompletionState completionState,
            long authoritativeTick)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (lifecycleGeneration < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lifecycleGeneration));
            }
            if (!Enum.IsDefined(
                typeof(MissionRunCompletionState),
                completionState))
            {
                throw new ArgumentOutOfRangeException(nameof(completionState));
            }
            if (authoritativeTick < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(authoritativeTick));
            }
            LifecycleGeneration = lifecycleGeneration;
            CompletionState = completionState;
            AuthoritativeTick = authoritativeTick;
            Fingerprint = RunSessionFingerprint.Hash(ToCanonicalString());
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public MissionRunCompletionState CompletionState { get; }
        public long AuthoritativeTick { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprint.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprint.Append(builder, "run", RunStableId);
            RunSessionFingerprint.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprint.Append(
                builder,
                "completion",
                (int)CompletionState);
            RunSessionFingerprint.Append(
                builder,
                "tick",
                AuthoritativeTick);
            return builder.ToString();
        }
    }

    internal static class RunSessionFingerprint
    {
        internal static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(bytes)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        internal static void Append(
            StringBuilder builder,
            string name,
            object value)
        {
            string safe;
            if (value == null)
            {
                safe = string.Empty;
            }
            else if (value is IFormattable)
            {
                safe = ((IFormattable)value).ToString(
                    null,
                    CultureInfo.InvariantCulture);
            }
            else
            {
                safe = value.ToString();
            }
            builder.Append(name)
                .Append('=')
                .Append(safe.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':')
                .Append(safe)
                .Append('\n');
        }
    }
}
