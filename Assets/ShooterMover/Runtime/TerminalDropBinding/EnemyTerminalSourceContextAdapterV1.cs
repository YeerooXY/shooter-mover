using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
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
    /// The only built-in complete enemy terminal-drop adapter. It combines the internal
    /// definition/profile projection with production-owned Run Session lifecycle context.
    /// </summary>
    public sealed class ContextResolvedEnemyDeathTerminalDropFactAdapterV1 :
        ITerminalDropFactAdapterV1
    {
        private readonly EnemyDeathTerminalDropDefinitionProjectorV1 definitionProjector;
        private readonly IEnemyTerminalSourceContextResolverV1 sourceContexts;

        public ContextResolvedEnemyDeathTerminalDropFactAdapterV1(
            EnemyCatalogV1 catalog,
            IEnemyTerminalSourceContextResolverV1 sourceContexts)
        {
            definitionProjector = new EnemyDeathTerminalDropDefinitionProjectorV1(
                catalog ?? throw new ArgumentNullException(nameof(catalog)));
            this.sourceContexts = sourceContexts
                ?? throw new ArgumentNullException(nameof(sourceContexts));
        }

        public StableId FactKindStableId
        {
            get { return TerminalDropFactKindIdsV1.EnemyDeath; }
        }

        public Type FactType { get { return typeof(EnemyDeathFactV1); } }

        public TerminalDropAdaptationResultV1 Adapt(object terminalFact)
        {
            EnemyDeathFactV1 fact = terminalFact as EnemyDeathFactV1;
            if (fact == null)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "enemy-death-fact-type-mismatch");
            }

            EnemyDeathTerminalDropDefinitionProjectionResultV1 definitionResult;
            try
            {
                definitionResult = definitionProjector.Project(fact);
            }
            catch (Exception exception)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.InvalidTerminalFact,
                    "enemy-definition-projection-exception:"
                        + exception.GetType().Name + ":" + exception.Message);
            }
            if (definitionResult == null || !definitionResult.Succeeded)
            {
                return definitionResult == null
                    ? TerminalDropAdaptationResultV1.Rejected(
                        TerminalDropRejectionCodeV1.InvalidTerminalFact,
                        "enemy-definition-projector-returned-null")
                    : TerminalDropAdaptationResultV1.Rejected(
                        definitionResult.RejectionCode,
                        definitionResult.Diagnostic);
            }

            EnemyTerminalSourceContextV1 context;
            string diagnostic;
            bool resolved;
            try
            {
                resolved = sourceContexts.TryResolve(fact, out context, out diagnostic);
            }
            catch (Exception exception)
            {
                return TerminalDropAdaptationResultV1.Rejected(
                    TerminalDropRejectionCodeV1.MissingSourceContext,
                    "enemy-source-context-exception:"
                        + exception.GetType().Name + ":" + exception.Message);
            }
            if (!resolved || context == null)
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

            EnemyDeathTerminalDropDefinitionProjectionV1 projection =
                definitionResult.Projection;
            return TerminalDropAdaptationResultV1.Accepted(
                new TerminalDropSourceFactV1(
                    FactKindStableId,
                    fact.DeathEventStableId,
                    fact.TriggeringEventStableId,
                    context.RunStableId,
                    context.RunLifecycleGeneration,
                    fact.Identity.EntityInstanceId,
                    fact.Identity.PlacementStableId,
                    context.SourceLifecycleGeneration,
                    fact.DefinitionStableId,
                    fact.KillerRunParticipantStableId,
                    fact.KillerEntityStableId,
                    null,
                    projection.DeclaredDropProfileStableId,
                    context.Fingerprint,
                    projection.DefinitionFingerprint,
                    projection.UpstreamFactFingerprint));
        }
    }
}
