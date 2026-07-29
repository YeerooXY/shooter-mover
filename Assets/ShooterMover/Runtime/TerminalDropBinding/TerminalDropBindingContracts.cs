using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.TerminalDropBinding
{
    public static class TerminalDropFactKindIds
    {
        public static readonly StableId EnemyDeath =
            StableId.Parse("terminal-drop-fact.enemy-death");
        public static readonly StableId PropDestruction =
            StableId.Parse("terminal-drop-fact.prop-destruction");
    }

    public enum TerminalDropBindingStatus
    {
        Accepted = 1,
        ExplicitNoDrop = 2,
        ExactReplay = 3,
        Rejected = 4,
        ConflictingDuplicate = 5,
    }

    public enum TerminalDropRejectionCode
    {
        None = 0,
        NullFact = 1,
        UnsupportedFactType = 2,
        InvalidTerminalFact = 3,
        MissingDefinition = 4,
        DefinitionMismatch = 5,
        MissingDropProfile = 6,
        DropProfileMismatch = 7,
        UnattributedTerminalFact = 8,
        MissingRun = 9,
        WrongRunLifecycle = 10,
        RunEnded = 11,
        MissingSourceContext = 12,
        GenerationFailed = 13,
        InvalidGeneratedBatch = 14,
    }

    public sealed class TerminalDropSourceFact
    {
        public TerminalDropSourceFact(
            StableId factKindStableId,
            StableId terminalEventStableId,
            StableId triggeringEventStableId,
            StableId runStableId,
            long runLifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            long sourceLifecycleGeneration,
            StableId sourceDefinitionStableId,
            StableId attributedParticipantStableId,
            StableId damageSourceStableId,
            StableId damageChannelStableId,
            StableId declaredDropProfileStableId,
            string sourceContextFingerprint,
            string definitionFingerprint,
            string upstreamFactFingerprint)
        {
            FactKindStableId = factKindStableId
                ?? throw new ArgumentNullException(nameof(factKindStableId));
            TerminalEventStableId = terminalEventStableId
                ?? throw new ArgumentNullException(nameof(terminalEventStableId));
            RunStableId = runStableId
                ?? throw new ArgumentNullException(nameof(runStableId));
            if (runLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(nameof(runLifecycleGeneration));
            SourceEntityStableId = sourceEntityStableId
                ?? throw new ArgumentNullException(nameof(sourceEntityStableId));
            if (sourceLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(nameof(sourceLifecycleGeneration));
            SourceDefinitionStableId = sourceDefinitionStableId
                ?? throw new ArgumentNullException(nameof(sourceDefinitionStableId));
            RequireFingerprint(sourceContextFingerprint, nameof(sourceContextFingerprint));
            RequireFingerprint(definitionFingerprint, nameof(definitionFingerprint));
            RequireFingerprint(upstreamFactFingerprint, nameof(upstreamFactFingerprint));

            TriggeringEventStableId = triggeringEventStableId;
            RunLifecycleGeneration = runLifecycleGeneration;
            SourcePlacementStableId = sourcePlacementStableId;
            SourceLifecycleGeneration = sourceLifecycleGeneration;
            AttributedParticipantStableId = attributedParticipantStableId;
            DamageSourceStableId = damageSourceStableId;
            DamageChannelStableId = damageChannelStableId;
            DeclaredDropProfileStableId = declaredDropProfileStableId;
            SourceContextFingerprint = sourceContextFingerprint.Trim();
            DefinitionFingerprint = definitionFingerprint.Trim();
            UpstreamFactFingerprint = upstreamFactFingerprint.Trim();
            Fingerprint = TerminalDrop.Hash(ToCanonicalString());
        }

        public StableId FactKindStableId { get; }
        public StableId TerminalEventStableId { get; }
        public StableId TriggeringEventStableId { get; }
        public StableId RunStableId { get; }
        public long RunLifecycleGeneration { get; }
        public StableId SourceEntityStableId { get; }
        public StableId SourcePlacementStableId { get; }
        public long SourceLifecycleGeneration { get; }
        public StableId SourceDefinitionStableId { get; }
        public StableId AttributedParticipantStableId { get; }
        public StableId DamageSourceStableId { get; }
        public StableId DamageChannelStableId { get; }
        public StableId DeclaredDropProfileStableId { get; }
        public string SourceContextFingerprint { get; }
        public string DefinitionFingerprint { get; }
        public string UpstreamFactFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=terminal-drop-source-fact-v1");
            TerminalDrop.Append(builder, "kind", FactKindStableId);
            TerminalDrop.Append(builder, "terminal-event", TerminalEventStableId);
            TerminalDrop.Append(builder, "triggering-event", TriggeringEventStableId);
            TerminalDrop.Append(builder, "run", RunStableId);
            TerminalDrop.Append(builder, "run-generation", RunLifecycleGeneration);
            TerminalDrop.Append(builder, "source-entity", SourceEntityStableId);
            TerminalDrop.Append(builder, "source-placement", SourcePlacementStableId);
            TerminalDrop.Append(builder, "source-generation", SourceLifecycleGeneration);
            TerminalDrop.Append(builder, "definition", SourceDefinitionStableId);
            TerminalDrop.Append(builder, "participant", AttributedParticipantStableId);
            TerminalDrop.Append(builder, "damage-source", DamageSourceStableId);
            TerminalDrop.Append(builder, "damage-channel", DamageChannelStableId);
            TerminalDrop.Append(builder, "declared-profile", DeclaredDropProfileStableId);
            TerminalDrop.Append(builder, "source-context", SourceContextFingerprint);
            TerminalDrop.Append(builder, "definition-fingerprint", DefinitionFingerprint);
            TerminalDrop.Append(builder, "upstream-fingerprint", UpstreamFactFingerprint);
            return builder.ToString();
        }

        private static void RequireFingerprint(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A deterministic fingerprint is required.", parameterName);
        }
    }

    public sealed class TerminalDropAdaptationResult
    {
        private TerminalDropAdaptationResult(
            TerminalDropSourceFact sourceFact,
            TerminalDropRejectionCode rejectionCode,
            string diagnostic)
        {
            SourceFact = sourceFact;
            RejectionCode = rejectionCode;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public TerminalDropSourceFact SourceFact { get; }
        public TerminalDropRejectionCode RejectionCode { get; }
        public string Diagnostic { get; }
        public bool Succeeded { get { return SourceFact != null; } }

        public static TerminalDropAdaptationResult Accepted(
            TerminalDropSourceFact sourceFact)
        {
            return new TerminalDropAdaptationResult(
                sourceFact ?? throw new ArgumentNullException(nameof(sourceFact)),
                TerminalDropRejectionCode.None,
                string.Empty);
        }

        public static TerminalDropAdaptationResult Rejected(
            TerminalDropRejectionCode code,
            string diagnostic)
        {
            if (code == TerminalDropRejectionCode.None)
                throw new ArgumentException("A rejection requires a non-success code.", nameof(code));
            return new TerminalDropAdaptationResult(null, code, diagnostic);
        }
    }

    public interface ITerminalDropFactBridge
    {
        StableId FactKindStableId { get; }
        Type FactType { get; }
        TerminalDropAdaptationResult Adapt(object terminalFact);
    }

    public interface IRewardProfileResolver
    {
        bool TryResolve(StableId profileStableId, out RewardProfile profile);
        string Fingerprint { get; }
    }

    public sealed class TerminalDropRunGenerationContext
    {
        public TerminalDropRunGenerationContext(
            StableId runStableId,
            long lifecycleGeneration,
            ulong rootSeed,
            int generationAlgorithmVersion,
            ProgressionContext progressionContext,
            string eventModifierContextFingerprint)
        {
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            if (lifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(nameof(lifecycleGeneration));
            if (generationAlgorithmVersion < 1)
                throw new ArgumentOutOfRangeException(nameof(generationAlgorithmVersion));
            ProgressionContext = progressionContext
                ?? throw new ArgumentNullException(nameof(progressionContext));
            if (string.IsNullOrWhiteSpace(eventModifierContextFingerprint))
                throw new ArgumentException(
                    "The frozen event/modifier context fingerprint is required.",
                    nameof(eventModifierContextFingerprint));
            LifecycleGeneration = lifecycleGeneration;
            RootSeed = rootSeed;
            GenerationAlgorithmVersion = generationAlgorithmVersion;
            EventModifierContextFingerprint = eventModifierContextFingerprint.Trim();
            Fingerprint = TerminalDrop.Hash(ToCanonicalString());
        }

        public StableId RunStableId { get; }
        public long LifecycleGeneration { get; }
        public ulong RootSeed { get; }
        public int GenerationAlgorithmVersion { get; }
        public ProgressionContext ProgressionContext { get; }
        public string EventModifierContextFingerprint { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=terminal-drop-run-context-v1");
            TerminalDrop.Append(builder, "run", RunStableId);
            TerminalDrop.Append(builder, "generation", LifecycleGeneration);
            TerminalDrop.Append(builder, "seed", RootSeed);
            TerminalDrop.Append(builder, "algorithm", GenerationAlgorithmVersion);
            TerminalDrop.Append(
                builder,
                "progression-context",
                ProgressionContext.Fingerprint);
            TerminalDrop.Append(
                builder,
                "event-context",
                EventModifierContextFingerprint);
            return builder.ToString();
        }
    }

    public interface ITerminalDropRunContextResolver
    {
        bool TryResolve(
            StableId runStableId,
            long expectedLifecycleGeneration,
            out TerminalDropRunGenerationContext context,
            out TerminalDropRejectionCode rejectionCode,
            out string diagnostic);
    }

    public interface IRewardGenerationExecutor
    {
        RewardGenerationResultEnvelope Generate(RewardGenerationRequest request);
    }

    public sealed class ExistingRewardGenerationExecutor : IRewardGenerationExecutor
    {
        private readonly RewardGenerationActions service;

        public ExistingRewardGenerationExecutor(RewardGenerationActions service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public RewardGenerationResultEnvelope Generate(RewardGenerationRequest request)
        {
            return service.GenerateReward(request);
        }
    }

    public sealed class GeneratedTerminalDropReward
    {
        public GeneratedTerminalDropReward(
            StableId rewardInstanceStableId,
            int ordinal,
            StableId sourceGrantStableId,
            RewardGrantKind kind,
            StableId contentStableId,
            long quantity)
        {
            RewardInstanceStableId = rewardInstanceStableId
                ?? throw new ArgumentNullException(nameof(rewardInstanceStableId));
            if (ordinal < 0) throw new ArgumentOutOfRangeException(nameof(ordinal));
            SourceGrantStableId = sourceGrantStableId
                ?? throw new ArgumentNullException(nameof(sourceGrantStableId));
            if (!Enum.IsDefined(typeof(RewardGrantKind), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            ContentStableId = contentStableId
                ?? throw new ArgumentNullException(nameof(contentStableId));
            if (quantity < 1L) throw new ArgumentOutOfRangeException(nameof(quantity));
            Ordinal = ordinal;
            Kind = kind;
            Quantity = quantity;
            Fingerprint = TerminalDrop.Hash(ToCanonicalString());
        }

        public StableId RewardInstanceStableId { get; }
        public int Ordinal { get; }
        public StableId SourceGrantStableId { get; }
        public RewardGrantKind Kind { get; }
        public StableId ContentStableId { get; }
        public long Quantity { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=generated-terminal-drop-reward-v1");
            TerminalDrop.Append(builder, "instance", RewardInstanceStableId);
            TerminalDrop.Append(builder, "ordinal", Ordinal);
            TerminalDrop.Append(builder, "grant", SourceGrantStableId);
            TerminalDrop.Append(builder, "kind", (int)Kind);
            TerminalDrop.Append(builder, "content", ContentStableId);
            TerminalDrop.Append(builder, "quantity", Quantity);
            return builder.ToString();
        }
    }

    public sealed class GeneratedTerminalDropResult
    {
        private readonly ReadOnlyCollection<GeneratedTerminalDropReward> rewards;

        internal GeneratedTerminalDropResult(
            TerminalDropBindingStatus status,
            TerminalDropRejectionCode rejectionCode,
            TerminalDropSourceFact sourceFact,
            StableId resolvedDropProfileStableId,
            RewardOperationRequest operationRequest,
            ulong generationSeed,
            RewardGenerationResultEnvelope generatedBatch,
            IEnumerable<GeneratedTerminalDropReward> generatedRewards,
            string canonicalBatchFingerprint,
            string diagnostic)
        {
            Status = status;
            RejectionCode = rejectionCode;
            SourceFact = sourceFact;
            ResolvedDropProfileStableId = resolvedDropProfileStableId;
            OperationRequest = operationRequest;
            GenerationSeed = generationSeed;
            GeneratedBatch = generatedBatch;
            var copy = new List<GeneratedTerminalDropReward>();
            if (generatedRewards != null)
            {
                foreach (GeneratedTerminalDropReward reward in generatedRewards)
                {
                    if (reward == null)
                        throw new ArgumentException("Generated rewards cannot contain null.", nameof(generatedRewards));
                    copy.Add(reward);
                }
            }
            copy.Sort((left, right) => left.Ordinal.CompareTo(right.Ordinal));
            rewards = new ReadOnlyCollection<GeneratedTerminalDropReward>(copy);
            Fingerprint = canonicalBatchFingerprint ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public TerminalDropBindingStatus Status { get; }
        public TerminalDropRejectionCode RejectionCode { get; }
        public TerminalDropSourceFact SourceFact { get; }
        public StableId ResolvedDropProfileStableId { get; }
        public RewardOperationRequest OperationRequest { get; }
        public ulong GenerationSeed { get; }
        public RewardGenerationResultEnvelope GeneratedBatch { get; }
        public IReadOnlyList<GeneratedTerminalDropReward> GeneratedRewards { get { return rewards; } }
        public string Fingerprint { get; }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == TerminalDropBindingStatus.Accepted
                    || Status == TerminalDropBindingStatus.ExplicitNoDrop
                    || Status == TerminalDropBindingStatus.ExactReplay;
            }
        }

        internal GeneratedTerminalDropResult AsExactReplay()
        {
            return new GeneratedTerminalDropResult(
                TerminalDropBindingStatus.ExactReplay,
                TerminalDropRejectionCode.None,
                SourceFact,
                ResolvedDropProfileStableId,
                OperationRequest,
                GenerationSeed,
                GeneratedBatch,
                rewards,
                Fingerprint,
                "terminal-drop-exact-replay");
        }

        internal static GeneratedTerminalDropResult Rejected(
            TerminalDropRejectionCode code,
            TerminalDropSourceFact sourceFact,
            string diagnostic,
            bool conflict = false)
        {
            return new GeneratedTerminalDropResult(
                conflict
                    ? TerminalDropBindingStatus.ConflictingDuplicate
                    : TerminalDropBindingStatus.Rejected,
                code,
                sourceFact,
                null,
                null,
                0UL,
                null,
                Array.Empty<GeneratedTerminalDropReward>(),
                string.Empty,
                diagnostic);
        }
    }

    internal static class TerminalDrop
    {
        public static void Append(StringBuilder builder, string name, object value)
        {
            string text = value == null
                ? "none"
                : Convert.ToString(value, CultureInfo.InvariantCulture);
            builder.Append('\n')
                .Append(name.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(name)
                .Append('=')
                .Append(text.Length.ToString(CultureInfo.InvariantCulture))
                .Append(':').Append(text);
        }

        public static string Hash(string canonicalText)
        {
            byte[] input = Encoding.UTF8.GetBytes(canonicalText ?? string.Empty);
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(input);
            }
            var builder = new StringBuilder("sha256:", 71);
            for (int index = 0; index < digest.Length; index++)
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        public static ulong DeriveSeed(ulong rootSeed, string material)
        {
            const ulong offsetBasis = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offsetBasis ^ rootSeed;
            string value = material ?? string.Empty;
            for (int index = 0; index < value.Length; index++)
            {
                hash ^= value[index];
                hash *= prime;
            }
            return hash;
        }
    }
}
