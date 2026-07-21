#if UNITY_EDITOR
using System;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.EnemyRuntimeComposition;
using ShooterMover.TerminalDropBinding;

namespace ShooterMover.Tests.EditMode.TerminalDropBinding
{
    /// <summary>
    /// Test-only compatibility fixture. Existing integration tests now reach the
    /// production context-resolved enemy route rather than a catalog-only adapter.
    /// </summary>
    internal sealed class EnemyDeathTerminalDropFactAdapterV1 :
        ITerminalDropFactAdapterV1
    {
        private sealed class FixtureContextResolver :
            IEnemyTerminalSourceContextResolverV1
        {
            public bool TryResolve(
                EnemyDeathFactV1 fact,
                out EnemyTerminalSourceContextV1 context,
                out string diagnostic)
            {
                if (fact == null || fact.Identity == null)
                {
                    context = null;
                    diagnostic = "test-enemy-source-context-missing";
                    return false;
                }

                context = new EnemyTerminalSourceContextV1(
                    fact.Identity.RunStableId,
                    fact.LifecycleGeneration,
                    fact.Identity.EntityInstanceId,
                    fact.Identity.PlacementStableId,
                    fact.LifecycleGeneration,
                    "test-enemy-context:" + fact.Identity.EntityInstanceId
                        + ":" + fact.LifecycleGeneration);
                diagnostic = string.Empty;
                return true;
            }
        }

        private readonly ContextResolvedEnemyDeathTerminalDropFactAdapterV1 inner;

        public EnemyDeathTerminalDropFactAdapterV1(EnemyCatalogV1 catalog)
        {
            inner = new ContextResolvedEnemyDeathTerminalDropFactAdapterV1(
                catalog ?? throw new ArgumentNullException(nameof(catalog)),
                new FixtureContextResolver());
        }

        public StableId FactKindStableId { get { return inner.FactKindStableId; } }
        public Type FactType { get { return inner.FactType; } }

        public TerminalDropAdaptationResultV1 Adapt(object terminalFact)
        {
            return inner.Adapt(terminalFact);
        }
    }
}
#endif
