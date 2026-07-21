using System;
using ShooterMover.Domain.Common;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.TerminalDropBinding
{
    /// <summary>
    /// Immutable projection of the source/run facts that are owned by production
    /// composition rather than by <see cref="EnemyDeathFactV1"/> itself.
    /// </summary>
    public sealed class EnemyTerminalSourceContextV1
    {
        public EnemyTerminalSourceContextV1(
            StableId runStableId,
            long runLifecycleGeneration,
            StableId sourceEntityStableId,
            StableId sourcePlacementStableId,
            long sourceLifecycleGeneration,
            string fingerprint)
        {
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            if (runLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(nameof(runLifecycleGeneration));
            SourceEntityStableId = sourceEntityStableId
                ?? throw new ArgumentNullException(nameof(sourceEntityStableId));
            if (sourceLifecycleGeneration < 0L)
                throw new ArgumentOutOfRangeException(nameof(sourceLifecycleGeneration));
            if (string.IsNullOrWhiteSpace(fingerprint))
                throw new ArgumentException(
                    "A deterministic enemy source-context fingerprint is required.",
                    nameof(fingerprint));

            RunLifecycleGeneration = runLifecycleGeneration;
            SourcePlacementStableId = sourcePlacementStableId;
            SourceLifecycleGeneration = sourceLifecycleGeneration;
            Fingerprint = fingerprint.Trim();
        }

        public StableId RunStableId { get; }
        public long RunLifecycleGeneration { get; }
        public StableId SourceEntityStableId { get; }
        public StableId SourcePlacementStableId { get; }
        public long SourceLifecycleGeneration { get; }
        public string Fingerprint { get; }
    }

    public interface IEnemyTerminalSourceContextResolverV1
    {
        bool TryResolve(
            EnemyDeathFactV1 terminalFact,
            out EnemyTerminalSourceContextV1 context,
            out string diagnostic);
    }

    /// <summary>
    /// Production-safe enemy adapter that keeps Run Session lifecycle and enemy
    /// lifecycle distinct. Definition/profile validation remains delegated to the
    /// existing generic enemy adapter.
    /// </summary>
    public sealed class ContextResolvedEnemyDeathTerminalDropFactAdapterV1 :
        ITerminalDropFactAdapterV1
    {
        private readonly EnemyDeathTerminalDropFactAdapterV1 definitionAdapter;
        private readonly IEnemyTerminalSourceContextResolverV1 sourceContexts;

        public ContextResolvedEnemyDeathTerminalDropFactAdapterV1(
            EnemyDeathTerminalDropFactAdapterV1 definitionAdapter,
            IEnemyTerminalSourceContextResolverV1 sourceContexts)
        {
            this.definitionAdapter = definitionAdapter
                ?? throw new ArgumentNullException(nameof(definitionAdapter));
            this.sourceContexts = sourceContexts
                ?? throw new ArgumentNullException(nameof(sourceContexts));
        }

        public StableId FactKindStableId { get { return definitionAdapter.FactKindStableId; } }
        public Type FactType { get { return definitionAdapter.FactType; } }

        public TerminalDropAdaptationResultV1 Adapt(object terminalFact)
        {
            EnemyDeathFactV1 fact = terminalFact as EnemyDeathFactV1;
            if (fact == null)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "enemy-death-fact-type-mismatch");
            }

            TerminalDropAdaptationResultV1 definitionResult =
                definitionAdapter.Adapt(fact);
            if (definitionResult == null || !definitionResult.Succeeded)
            {
                return definitionResult
                    ?? TerminalDropAdaptationResultV1.Rejected(
                        TerminalDropRejectionCodeV1.InvalidTerminalFact,
                        "enemy-definition-adapter-returned-null");
            }

            EnemyTerminalSourceContextV1 context;
            string diagnostic;
            if (!sourceContexts.TryResolve(fact, out context, out diagnostic)
                || context == null)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.MissingSourceContext,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "enemy-source-context-missing"
                        : diagnostic);
            }

            if (fact.Identity == null
                || context.RunStableId != fact.Identity.RunStableId
                || context.SourceEntityStableId != fact.Identity.EntityInstanceId
                || context.SourcePlacementStableId != fact.Identity.PlacementStableId
                || context.SourceLifecycleGeneration != fact.LifecycleGeneration)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "enemy-source-context-does-not-match-death-fact");
            }

            TerminalDropSourceFactV1 source = definitionResult.SourceFact;
            return TerminalDropAdaptationResultV1.Accepted(
                new TerminalDropSourceFactV1(
                    source.FactKindStableId,
                    source.TerminalEventStableId,
                    source.TriggeringEventStableId,
                    context.RunStableId,
                    context.RunLifecycleGeneration,
                    context.SourceEntityStableId,
                    context.SourcePlacementStableId,
                    context.SourceLifecycleGeneration,
                    source.SourceDefinitionStableId,
                    source.AttributedParticipantStableId,
                    source.DamageSourceStableId,
                    source.DamageChannelStableId,
                    source.DeclaredDropProfileStableId,
                    context.Fingerprint,
                    source.DefinitionFingerprint,
                    source.UpstreamFactFingerprint));
        }
    }
}
