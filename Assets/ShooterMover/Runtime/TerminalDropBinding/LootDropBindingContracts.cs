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

namespace ShooterMover.LootDropBinding
{
    public static class LootDropFactKindIds
    {
        public static readonly StableId EnemyDeath =
            StableId.Parse("terminal-drop-fact.enemy-death");
        public static readonly StableId PropDestruction =
            StableId.Parse("terminal-drop-fact.prop-destruction");
    }

    public enum LootDropBindingStatus
    {
        Accepted = 1,
        ExplicitNoDrop = 2,
        ExactReplay = 3,
        Rejected = 4,
        ConflictingDuplicate = 5,
    }

    public enum LootDropRejectionCode
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

    public sealed class LootDropSourceFact
    {
        public LootDropSourceFact(
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
            Fingerprint = LootDrop.Hash(ToCanonicalString());
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
            LootDrop.Append(builder, "kind", FactKindStableId);
            LootDrop.Append(builder, "terminal-event", TerminalEventStableId);
            LootDrop.Append(builder, "triggering-event", TriggeringEventStableId);
            LootDrop.Append(builder, "run", RunStableId);
            LootDrop.Append(builder, "run-generation", RunLifecycleGeneration);
            LootDrop.Append(builder, "source-entity", SourceEntityStableId);
            LootDrop.Append(builder, "source-placement", SourcePlacementStableId);
            LootDrop.Append(builder, "source-generation", SourceLifecycleGeneration);
            LootDrop.Append(builder, "definition", SourceDefinitionStableId);
            LootDrop.Append(builder, "participant", AttributedParticipantStableId);
            LootDrop.Append(builder, "damage-source", DamageSourceStableId);
            LootDrop.Append(builder, "damage-channel", DamageChannelStableId);
            LootDrop.Append(builder, "declared-profile", DeclaredDropProfileStableId);
            LootDrop.Append(builder, "source-context", SourceContextFingerprint);
            LootDrop.Append(builder, "definition-fingerprint", DefinitionFingerprint);
            LootDrop.Append(builder, "upstream-fingerprint", UpstreamFactFingerprint);
            return builder.ToString();
        }

        private static void RequireFingerprint(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A deterministic fingerprint is required.", parameterName);
        }
    }

    public sealed class LootDropAdaptationResult
    {
        private LootDropAdaptationResult(
            LootDropSourceFact sourceFact,
            LootDropRejectionCode rejectionCode,
            string diagnostic)
        {
            SourceFact = sourceFact;
            RejectionCode = rejectionCode;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public LootDropSourceFact SourceFact { get; }
        public LootDropRejectionCode RejectionCode { get; }
        public string Diagnostic { get; }
        public bool Succeeded { get { return SourceFact != null; } }

        public static LootDropAdaptationResult Accepted(
            LootDropSourceFact sourceFact)
        {
            return new LootDropAdaptationResult(
                sourceFact ?? throw new ArgumentNullException(nameof(sourceFact)),
                LootDropRejectionCode.None,
                string.Empty);
        }

        public static LootDropAdaptationResult Rejected(
            LootDropRejectionCode code,
            string diagnostic)
        {
            if (code == LootDropRejectionCode.None)
                throw new ArgumentException("A rejection requires a non-success code.", nameof(code));
            return new LootDropAdaptationResult(null, code, diagnostic);
        }
    }

    public interface ILootDropFactBridge
    {
        StableId FactKindStableId { get; }
        Type FactType { get; }
        LootDropAdaptationResult Adapt(object terminalFact);
    }

    public interface IRewardProfileResolver
    {
        bool TryResolve(StableId profileStableId, out RewardProfile profile);
        string Fingerprint { get; }
    }

    public sealed class LootDropRunGenerationContext
    {
        public LootDropRunGenerationContext(
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
            Fingerprint = LootDrop.Hash(ToCanonicalString());
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
            LootDrop.Append(builder, "run", RunStableId);
            LootDrop.Append(builder, "generation", LifecycleGeneration);
            LootDrop.Append(builder, "seed", RootSeed);
            LootDrop.Append(builder, "algorithm", GenerationAlgorithmVersion);
            LootDrop.Append(
                builder,
                "progression-context",
                ProgressionContext.Fingerprint);
            LootDrop.Append(
                builder,
                "event-context",
                EventModifierContextFingerprint);
            return builder.ToString();
        }
    }

    public interface ILootDropRunContextResolver
    {
        bool TryResolve(
            StableId runStableId,
            long expectedLifecycleGeneration,
            out LootDropRunGenerationContext context,
            out LootDropRejectionCode rejectionCode,
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

    public sealed class GeneratedLootDropReward
    {
        public GeneratedLootDropReward(
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
            Fingerprint = LootDrop.Hash(ToCanonicalString());
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
            LootDrop.Append(builder, "instance", RewardInstanceStableId);
            LootDrop.Append(builder, "ordinal", Ordinal);
            LootDrop.Append(builder, "grant", SourceGrantStableId);
            LootDrop.Append(builder, "kind", (int)Kind);
            LootDrop.Append(builder, "content", ContentStableId);
            LootDrop.Append(builder, "quantity", Quantity);
            return builder.ToString();
        }
    }

    public sealed class GeneratedLootDropResult
    {
        private readonly ReadOnlyCollection<GeneratedLootDropReward> rewards;

        internal GeneratedLootDropResult(
            LootDropBindingStatus status,
            LootDropRejectionCode rejectionCode,
            LootDropSourceFact sourceFact,
            StableId resolvedDropProfileStableId,
            RewardOperationRequest operationRequest,
            ulong generationSeed,
            RewardGenerationResultEnvelope generatedBatch,
            IEnumerable<GeneratedLootDropReward> generatedRewards,
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
            var copy = new List<GeneratedLootDropReward>();
            if (generatedRewards != null)
            {
                foreach (GeneratedLootDropReward reward in generatedRewards)
                {
                    if (reward == null)
                        throw new ArgumentException("Generated rewards cannot contain null.", nameof(generatedRewards));
                    copy.Add(reward);
                }
            }
            copy.Sort((left, right) => left.Ordinal.CompareTo(right.Ordinal));
            rewards = new ReadOnlyCollection<GeneratedLootDropReward>(copy);
            Fingerprint = canonicalBatchFingerprint ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public LootDropBindingStatus Status { get; }
        public LootDropRejectionCode RejectionCode { get; }
        public LootDropSourceFact SourceFact { get; }
        public StableId ResolvedDropProfileStableId { get; }
        public RewardOperationRequest OperationRequest { get; }
        public ulong GenerationSeed { get; }
        public RewardGenerationResultEnvelope GeneratedBatch { get; }
        public IReadOnlyList<GeneratedLootDropReward> GeneratedRewards { get { return rewards; } }
        public string Fingerprint { get; }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == LootDropBindingStatus.Accepted
                    || Status == LootDropBindingStatus.ExplicitNoDrop
                    || Status == LootDropBindingStatus.ExactReplay;
            }
        }

        internal GeneratedLootDropResult AsExactReplay()
        {
            return new GeneratedLootDropResult(
                LootDropBindingStatus.ExactReplay,
                LootDropRejectionCode.None,
                SourceFact,
                ResolvedDropProfileStableId,
                OperationRequest,
                GenerationSeed,
                GeneratedBatch,
                rewards,
                Fingerprint,
                "terminal-drop-exact-replay");
        }

        internal static GeneratedLootDropResult Rejected(
            LootDropRejectionCode code,
            LootDropSourceFact sourceFact,
            string diagnostic,
            bool conflict = false)
        {
            return new GeneratedLootDropResult(
                conflict
                    ? LootDropBindingStatus.ConflictingDuplicate
                    : LootDropBindingStatus.Rejected,
                code,
                sourceFact,
                null,
                null,
                0UL,
                null,
                Array.Empty<GeneratedLootDropReward>(),
                string.Empty,
                diagnostic);
        }
    }

    internal static class LootDrop
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
