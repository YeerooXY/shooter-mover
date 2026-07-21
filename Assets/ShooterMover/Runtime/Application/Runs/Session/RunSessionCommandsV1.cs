using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Runs.Session
{
    public enum RunSessionLifecycleStateV1
    {
        Active = 1,
        Ended = 2,
    }

    public enum RunSessionStartStatusV1
    {
        Started = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
    }

    public enum RunSessionRestartStatusV1
    {
        Applied = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
    }

    public enum RunSessionEndStatusV1
    {
        Ended = 1,
        ExactReplay = 2,
        Rejected = 3,
        ConflictingDuplicate = 4,
    }

    public enum RunSessionFactKindV1
    {
        Damage = 1,
        Projectile = 2,
        StatusEffect = 3,
        AbilityCast = 4,
        Contact = 5,
    }

    public enum RunSessionFactAdmissionStatusV1
    {
        Accepted = 1,
        ExactReplay = 2,
        WrongRun = 3,
        StaleLifecycle = 4,
        RunEnded = 5,
        ConflictingDuplicate = 6,
    }

    public enum RunLocalMutationKindV1
    {
        AddTemporaryPickup = 1,
        AddRunCash = 2,
        IncrementCounter = 3,
        IncrementStatistic = 4,
    }

    public sealed class StartRunSessionCommandV1
    {
        public const int CurrentSchemaVersion = 1;

        public StartRunSessionCommandV1(
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
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
            RunSessionFingerprintV1.Append(builder, "schema", SchemaVersion);
            RunSessionFingerprintV1.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "requested-run",
                RequestedRunStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "run-material",
                RunInstanceIdentityMaterial);
            RunSessionFingerprintV1.Append(
                builder,
                "character",
                SelectedCharacterInstanceStableId);
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
                "mission-layout",
                MissionLayoutStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "difficulty",
                DifficultyStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "seed",
                DeterministicSeed);
            RunSessionFingerprintV1.Append(
                builder,
                "initial-tick",
                AuthoritativeInitialTick);
            RunSessionFingerprintV1.Append(
                builder,
                "event-context",
                EventModifierContextFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunRestartPolicyV1
    {
        public RunRestartPolicyV1(
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public string PolicyId { get; }
        public bool RetainMissionStatistics { get; }
        public bool RetainRunCounters { get; }
        public bool RetainRunCash { get; }
        public bool RetainTemporaryPickups { get; }
        public string Fingerprint { get; }

        public static RunRestartPolicyV1 FullTransientReset()
        {
            return new RunRestartPolicyV1(
                "run-restart.full-transient-reset-v1",
                false,
                false,
                false,
                false);
        }

        public static RunRestartPolicyV1 RespawnPreservingMissionProgress()
        {
            return new RunRestartPolicyV1(
                "run-restart.respawn-preserve-progress-v1",
                true,
                true,
                true,
                true);
        }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(builder, "policy", PolicyId);
            RunSessionFingerprintV1.Append(
                builder,
                "retain-statistics",
                RetainMissionStatistics);
            RunSessionFingerprintV1.Append(
                builder,
                "retain-counters",
                RetainRunCounters);
            RunSessionFingerprintV1.Append(
                builder,
                "retain-cash",
                RetainRunCash);
            RunSessionFingerprintV1.Append(
                builder,
                "retain-pickups",
                RetainTemporaryPickups);
            return builder.ToString();
        }
    }

    public sealed class RestartRunSessionCommandV1
    {
        public RestartRunSessionCommandV1(
            StableId operationStableId,
            StableId runStableId,
            long retiringLifecycleGeneration,
            long replacementLifecycleGeneration,
            long authoritativeTick,
            RunRestartPolicyV1 policy)
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public long RetiringLifecycleGeneration { get; }
        public long ReplacementLifecycleGeneration { get; }
        public long AuthoritativeTick { get; }
        public RunRestartPolicyV1 Policy { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprintV1.Append(builder, "run", RunStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "retiring-generation",
                RetiringLifecycleGeneration);
            RunSessionFingerprintV1.Append(
                builder,
                "replacement-generation",
                ReplacementLifecycleGeneration);
            RunSessionFingerprintV1.Append(
                builder,
                "tick",
                AuthoritativeTick);
            RunSessionFingerprintV1.Append(
                builder,
                "policy",
                Policy.Fingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunSessionFactEnvelopeV1
    {
        public RunSessionFactEnvelopeV1(
            StableId operationStableId,
            StableId runStableId,
            long lifecycleGeneration,
            RunSessionFactKindV1 kind,
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
            if (!Enum.IsDefined(typeof(RunSessionFactKindV1), kind))
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public RunSessionFactKindV1 Kind { get; }
        public string FactFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprintV1.Append(builder, "run", RunStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprintV1.Append(
                builder,
                "kind",
                (int)Kind);
            RunSessionFingerprintV1.Append(
                builder,
                "fact",
                FactFingerprint);
            return builder.ToString();
        }
    }

    public sealed class RunStrongboxCollectionRequestV1
    {
        public RunStrongboxCollectionRequestV1(
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
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
            RunSessionFingerprintV1.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprintV1.Append(builder, "run", RunStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprintV1.Append(
                builder,
                "definition",
                DefinitionStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "instance",
                InstanceStableId);
            RunSessionFingerprintV1.Append(builder, "grant", GrantStableId);
            RunSessionFingerprintV1.Append(builder, "source", SourceStableId);
            return builder.ToString();
        }
    }

    public sealed class RunLocalMutationCommandV1
    {
        public RunLocalMutationCommandV1(
            StableId operationStableId,
            StableId runStableId,
            long lifecycleGeneration,
            RunLocalMutationKindV1 kind,
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
            if (!Enum.IsDefined(typeof(RunLocalMutationKindV1), kind))
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public RunLocalMutationKindV1 Kind { get; }
        public string Key { get; }
        public long Amount { get; }
        public string ProvenanceFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprintV1.Append(builder, "run", RunStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprintV1.Append(
                builder,
                "kind",
                (int)Kind);
            RunSessionFingerprintV1.Append(builder, "key", Key);
            RunSessionFingerprintV1.Append(builder, "amount", Amount);
            RunSessionFingerprintV1.Append(
                builder,
                "provenance",
                ProvenanceFingerprint);
            return builder.ToString();
        }
    }

    public sealed class EndRunSessionCommandV1
    {
        public EndRunSessionCommandV1(
            StableId operationStableId,
            StableId runStableId,
            long lifecycleGeneration,
            MissionRunCompletionStateV1 completionState,
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
                typeof(MissionRunCompletionStateV1),
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
            Fingerprint = RunSessionFingerprintV1.Hash(ToCanonicalString());
        }

        public StableId OperationStableId { get; }
        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public MissionRunCompletionStateV1 CompletionState { get; }
        public long AuthoritativeTick { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder();
            RunSessionFingerprintV1.Append(
                builder,
                "operation",
                OperationStableId);
            RunSessionFingerprintV1.Append(builder, "run", RunStableId);
            RunSessionFingerprintV1.Append(
                builder,
                "generation",
                LifecycleGeneration);
            RunSessionFingerprintV1.Append(
                builder,
                "completion",
                (int)CompletionState);
            RunSessionFingerprintV1.Append(
                builder,
                "tick",
                AuthoritativeTick);
            return builder.ToString();
        }
    }

    internal static class RunSessionFingerprintV1
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
