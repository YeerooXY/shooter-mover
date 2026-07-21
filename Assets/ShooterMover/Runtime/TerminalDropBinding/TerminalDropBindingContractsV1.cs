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
    public static class TerminalDropFactKindIdsV1
    {
        public static readonly StableId EnemyDeath =
            StableId.Parse("terminal-drop-fact.enemy-death");
        public static readonly StableId PropDestruction =
            StableId.Parse("terminal-drop-fact.prop-destruction");
    }

    public enum TerminalDropBindingStatusV1
    {
        Accepted = 1,
        ExplicitNoDrop = 2,
        ExactReplay = 3,
        Rejected = 4,
        ConflictingDuplicate = 5,
    }

    public enum TerminalDropRejectionCodeV1
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

    public sealed class TerminalDropSourceFactV1
    {
        public TerminalDropSourceFactV1(
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
            Fingerprint = TerminalDropCanonicalV1.Hash(ToCanonicalString());
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
            TerminalDropCanonicalV1.Append(builder, "kind", FactKindStableId);
            TerminalDropCanonicalV1.Append(builder, "terminal-event", TerminalEventStableId);
            TerminalDropCanonicalV1.Append(builder, "triggering-event", TriggeringEventStableId);
            TerminalDropCanonicalV1.Append(builder, "run", RunStableId);
            TerminalDropCanonicalV1.Append(builder, "run-generation", RunLifecycleGeneration);
            TerminalDropCanonicalV1.Append(builder, "source-entity", SourceEntityStableId);
            TerminalDropCanonicalV1.Append(builder, "source-placement", SourcePlacementStableId);
            TerminalDropCanonicalV1.Append(builder, "source-generation", SourceLifecycleGeneration);
            TerminalDropCanonicalV1.Append(builder, "definition", SourceDefinitionStableId);
            TerminalDropCanonicalV1.Append(builder, "participant", AttributedParticipantStableId);
            TerminalDropCanonicalV1.Append(builder, "damage-source", DamageSourceStableId);
            TerminalDropCanonicalV1.Append(builder, "damage-channel", DamageChannelStableId);
            TerminalDropCanonicalV1.Append(builder, "declared-profile", DeclaredDropProfileStableId);
            TerminalDropCanonicalV1.Append(builder, "source-context", SourceContextFingerprint);
            TerminalDropCanonicalV1.Append(builder, "definition-fingerprint", DefinitionFingerprint);
            TerminalDropCanonicalV1.Append(builder, "upstream-fingerprint", UpstreamFactFingerprint);
            return builder.ToString();
        }

        private static void RequireFingerprint(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("A deterministic fingerprint is required.", parameterName);
        }
    }

    public sealed class TerminalDropAdaptationResultV1
    {
        private TerminalDropAdaptationResultV1(
            TerminalDropSourceFactV1 sourceFact,
            TerminalDropRejectionCodeV1 rejectionCode,
            string diagnostic)
        {
            SourceFact = sourceFact;
            RejectionCode = rejectionCode;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public TerminalDropSourceFactV1 SourceFact { get; }
        public TerminalDropRejectionCodeV1 RejectionCode { get; }
        public string Diagnostic { get; }
        public bool Succeeded { get { return SourceFact != null; } }

        public static TerminalDropAdaptationResultV1 Accepted(
            TerminalDropSourceFactV1 sourceFact)
        {
            return new TerminalDropAdaptationResultV1(
                sourceFact ?? throw new ArgumentNullException(nameof(sourceFact)),
                TerminalDropRejectionCodeV1.None,
                string.Empty);
        }

        public static TerminalDropAdaptationResultV1 Rejected(
            TerminalDropRejectionCodeV1 code,
            string diagnostic)
        {
            if (code == TerminalDropRejectionCodeV1.None)
                throw new ArgumentException("A rejection requires a non-success code.", nameof(code));
            return new TerminalDropAdaptationResultV1(null, code, diagnostic);
        }
    }

    public interface ITerminalDropFactAdapterV1
    {
        StableId FactKindStableId { get; }
        Type FactType { get; }
        TerminalDropAdaptationResultV1 Adapt(object terminalFact);
    }

    public interface IRewardProfileResolverV1
    {
        bool TryResolve(StableId profileStableId, out RewardProfileV1 profile);
        string Fingerprint { get; }
    }

    public sealed class TerminalDropRunGenerationContextV1
    {
        public TerminalDropRunGenerationContextV1(
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
            Fingerprint = TerminalDropCanonicalV1.Hash(ToCanonicalString());
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
            TerminalDropCanonicalV1.Append(builder, "run", RunStableId);
            TerminalDropCanonicalV1.Append(builder, "generation", LifecycleGeneration);
            TerminalDropCanonicalV1.Append(builder, "seed", RootSeed);
            TerminalDropCanonicalV1.Append(builder, "algorithm", GenerationAlgorithmVersion);
            TerminalDropCanonicalV1.Append(
                builder,
                "progression-context",
                ProgressionContext.Fingerprint);
            TerminalDropCanonicalV1.Append(
                builder,
                "event-context",
                EventModifierContextFingerprint);
            return builder.ToString();
        }
    }

    public interface ITerminalDropRunContextResolverV1
    {
        bool TryResolve(
            StableId runStableId,
            long expectedLifecycleGeneration,
            out TerminalDropRunGenerationContextV1 context,
            out TerminalDropRejectionCodeV1 rejectionCode,
            out string diagnostic);
    }

    public interface IRewardGenerationExecutorV1
    {
        RewardGenerationResultEnvelopeV1 Generate(RewardGenerationRequestV1 request);
    }

    public sealed class ExistingRewardGenerationExecutorV1 : IRewardGenerationExecutorV1
    {
        private readonly RewardGenerationServiceV1 service;

        public ExistingRewardGenerationExecutorV1(RewardGenerationServiceV1 service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public RewardGenerationResultEnvelopeV1 Generate(RewardGenerationRequestV1 request)
        {
            return service.GenerateReward(request);
        }
    }

    public sealed class GeneratedTerminalDropRewardV1
    {
        public GeneratedTerminalDropRewardV1(
            StableId rewardInstanceStableId,
            int ordinal,
            StableId sourceGrantStableId,
            RewardGrantKindV1 kind,
            StableId contentStableId,
            long quantity)
        {
            RewardInstanceStableId = rewardInstanceStableId
                ?? throw new ArgumentNullException(nameof(rewardInstanceStableId));
            if (ordinal < 0) throw new ArgumentOutOfRangeException(nameof(ordinal));
            SourceGrantStableId = sourceGrantStableId
                ?? throw new ArgumentNullException(nameof(sourceGrantStableId));
            if (!Enum.IsDefined(typeof(RewardGrantKindV1), kind))
                throw new ArgumentOutOfRangeException(nameof(kind));
            ContentStableId = contentStableId
                ?? throw new ArgumentNullException(nameof(contentStableId));
            if (quantity < 1L) throw new ArgumentOutOfRangeException(nameof(quantity));
            Ordinal = ordinal;
            Kind = kind;
            Quantity = quantity;
            Fingerprint = TerminalDropCanonicalV1.Hash(ToCanonicalString());
        }

        public StableId RewardInstanceStableId { get; }
        public int Ordinal { get; }
        public StableId SourceGrantStableId { get; }
        public RewardGrantKindV1 Kind { get; }
        public StableId ContentStableId { get; }
        public long Quantity { get; }
        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            var builder = new StringBuilder("schema=generated-terminal-drop-reward-v1");
            TerminalDropCanonicalV1.Append(builder, "instance", RewardInstanceStableId);
            TerminalDropCanonicalV1.Append(builder, "ordinal", Ordinal);
            TerminalDropCanonicalV1.Append(builder, "grant", SourceGrantStableId);
            TerminalDropCanonicalV1.Append(builder, "kind", (int)Kind);
            TerminalDropCanonicalV1.Append(builder, "content", ContentStableId);
            TerminalDropCanonicalV1.Append(builder, "quantity", Quantity);
            return builder.ToString();
        }
    }

    public sealed class GeneratedTerminalDropResultV1
    {
        private readonly ReadOnlyCollection<GeneratedTerminalDropRewardV1> rewards;

        internal GeneratedTerminalDropResultV1(
            TerminalDropBindingStatusV1 status,
            TerminalDropRejectionCodeV1 rejectionCode,
            TerminalDropSourceFactV1 sourceFact,
            StableId resolvedDropProfileStableId,
            RewardOperationRequestV1 operationRequest,
            ulong generationSeed,
            RewardGenerationResultEnvelopeV1 generatedBatch,
            IEnumerable<GeneratedTerminalDropRewardV1> generatedRewards,
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
            var copy = new List<GeneratedTerminalDropRewardV1>();
            if (generatedRewards != null)
            {
                foreach (GeneratedTerminalDropRewardV1 reward in generatedRewards)
                {
                    if (reward == null)
                        throw new ArgumentException("Generated rewards cannot contain null.", nameof(generatedRewards));
                    copy.Add(reward);
                }
            }
            copy.Sort((left, right) => left.Ordinal.CompareTo(right.Ordinal));
            rewards = new ReadOnlyCollection<GeneratedTerminalDropRewardV1>(copy);
            Fingerprint = canonicalBatchFingerprint ?? string.Empty;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public TerminalDropBindingStatusV1 Status { get; }
        public TerminalDropRejectionCodeV1 RejectionCode { get; }
        public TerminalDropSourceFactV1 SourceFact { get; }
        public StableId ResolvedDropProfileStableId { get; }
        public RewardOperationRequestV1 OperationRequest { get; }
        public ulong GenerationSeed { get; }
        public RewardGenerationResultEnvelopeV1 GeneratedBatch { get; }
        public IReadOnlyList<GeneratedTerminalDropRewardV1> GeneratedRewards { get { return rewards; } }
        public string Fingerprint { get; }
        public string Diagnostic { get; }
        public bool IsAccepted
        {
            get
            {
                return Status == TerminalDropBindingStatusV1.Accepted
                    || Status == TerminalDropBindingStatusV1.ExplicitNoDrop
                    || Status == TerminalDropBindingStatusV1.ExactReplay;
            }
        }

        internal GeneratedTerminalDropResultV1 AsExactReplay()
        {
            return new GeneratedTerminalDropResultV1(
                TerminalDropBindingStatusV1.ExactReplay,
                TerminalDropRejectionCodeV1.None,
                SourceFact,
                ResolvedDropProfileStableId,
                OperationRequest,
                GenerationSeed,
                GeneratedBatch,
                rewards,
                Fingerprint,
                "terminal-drop-exact-replay");
        }

        internal static GeneratedTerminalDropResultV1 Rejected(
            TerminalDropRejectionCodeV1 code,
            TerminalDropSourceFactV1 sourceFact,
            string diagnostic,
            bool conflict = false)
        {
            return new GeneratedTerminalDropResultV1(
                conflict
                    ? TerminalDropBindingStatusV1.ConflictingDuplicate
                    : TerminalDropBindingStatusV1.Rejected,
                code,
                sourceFact,
                null,
                null,
                0UL,
                null,
                Array.Empty<GeneratedTerminalDropRewardV1>(),
                string.Empty,
                diagnostic);
        }
    }

    internal static class TerminalDropCanonicalV1
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
