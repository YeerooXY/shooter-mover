using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.LootDropBinding
{
    /// <summary>
    /// Immutable projection of the source/run facts that are owned by production
    /// composition rather than by <see cref="EnemyDeathFact"/> itself.
    /// </summary>
    public sealed class EnemyTerminalSourceContext
    {
        public EnemyTerminalSourceContext(
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

    public interface IEnemyTerminalSourceContextResolver
    {
        bool TryResolve(
            EnemyDeathFact terminalFact,
            out EnemyTerminalSourceContext context,
            out string diagnostic);
    }

    /// <summary>
    /// The only built-in complete enemy terminal-drop adapter. It combines the internal
    /// definition/profile projection with production-owned Run Session lifecycle context.
    /// </summary>
    public sealed class ContextResolvedEnemyDeathLootDropFactBridge :
        ILootDropFactBridge
    {
        private readonly EnemyDeathLootDropDefinitionProjector definitionProjector;
        private readonly IEnemyTerminalSourceContextResolver sourceContexts;

        public ContextResolvedEnemyDeathLootDropFactBridge(
            EnemyCatalog catalog,
            IEnemyTerminalSourceContextResolver sourceContexts)
        {
            definitionProjector = new EnemyDeathLootDropDefinitionProjector(
                catalog ?? throw new ArgumentNullException(nameof(catalog)));
            this.sourceContexts = sourceContexts
                ?? throw new ArgumentNullException(nameof(sourceContexts));
        }

        public StableId FactKindStableId
        {
            get { return LootDropFactKindIds.EnemyDeath; }
        }

        public Type FactType { get { return typeof(EnemyDeathFact); } }

        public LootDropAdaptationResult Adapt(object terminalFact)
        {
            EnemyDeathFact fact = terminalFact as EnemyDeathFact;
            if (fact == null)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.InvalidTerminalFact,
                    "enemy-death-fact-type-mismatch");
            }

            EnemyDeathLootDropDefinitionViewResult definitionResult;
            try
            {
                definitionResult = definitionProjector.Project(fact);
            }
            catch (Exception exception)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.InvalidTerminalFact,
                    "enemy-definition-projection-exception:"
                        + exception.GetType().Name + ":" + exception.Message);
            }
            if (definitionResult == null || !definitionResult.Succeeded)
            {
                return definitionResult == null
                    ? LootDropAdaptationResult.Rejected(
                        LootDropRejectionCode.InvalidTerminalFact,
                        "enemy-definition-projector-returned-null")
                    : LootDropAdaptationResult.Rejected(
                        definitionResult.RejectionCode,
                        definitionResult.Diagnostic);
            }

            EnemyTerminalSourceContext context;
            string diagnostic;
            bool resolved;
            try
            {
                resolved = sourceContexts.TryResolve(fact, out context, out diagnostic);
            }
            catch (Exception exception)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.MissingSourceContext,
                    "enemy-source-context-exception:"
                        + exception.GetType().Name + ":" + exception.Message);
            }
            if (!resolved || context == null)
            {
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.MissingSourceContext,
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
                return LootDropAdaptationResult.Rejected(
                    LootDropRejectionCode.InvalidTerminalFact,
                    "enemy-source-context-does-not-match-death-fact");
            }

            EnemyDeathLootDropDefinitionView projection =
                definitionResult.Projection;
            return LootDropAdaptationResult.Accepted(
                new LootDropSourceFact(
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
