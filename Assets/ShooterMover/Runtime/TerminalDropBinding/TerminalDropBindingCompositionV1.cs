using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Props;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.TerminalDropBinding
{
    public interface IGeneratedTerminalDropResultSinkV1
    {
        void Publish(GeneratedTerminalDropResultV1 result);
    }

    public interface IPropTerminalDropFactConsumerV1
    {
        void Consume(PropFactBatchV1 fact);
    }

    /// <summary>
    /// Typed bridge for the existing generic enemy terminal consequence port.
    /// Multiple bridge deliveries remain safe because the authority owns replay.
    /// </summary>
    public sealed class EnemyTerminalDropFactConsumerV1 : IEnemyDropFactConsumerV1
    {
        private readonly TerminalDropGenerationAuthorityV1 authority;
        private readonly IGeneratedTerminalDropResultSinkV1 sink;

        public EnemyTerminalDropFactConsumerV1(
            TerminalDropGenerationAuthorityV1 authority,
            IGeneratedTerminalDropResultSinkV1 sink)
        {
            this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public void Consume(EnemyDeathFactV1 fact)
        {
            sink.Publish(authority.Generate(fact));
        }
    }

    public sealed class PropTerminalDropFactConsumerV1 : IPropTerminalDropFactConsumerV1
    {
        private readonly TerminalDropGenerationAuthorityV1 authority;
        private readonly IGeneratedTerminalDropResultSinkV1 sink;

        public PropTerminalDropFactConsumerV1(
            TerminalDropGenerationAuthorityV1 authority,
            IGeneratedTerminalDropResultSinkV1 sink)
        {
            this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
            this.sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public void Consume(PropFactBatchV1 fact)
        {
            sink.Publish(authority.Generate(fact));
        }
    }

    /// <summary>
    /// Additive typed composition. It reuses existing catalogs and GEN and does not
    /// create a runtime bootstrap or mutate Run Session/permanent state.
    /// </summary>
    public sealed class TerminalDropBindingCompositionV1
    {
        private TerminalDropBindingCompositionV1(
            TerminalDropGenerationAuthorityV1 authority,
            EnemyTerminalDropFactConsumerV1 enemyConsumer,
            PropTerminalDropFactConsumerV1 propConsumer)
        {
            Authority = authority;
            EnemyConsumer = enemyConsumer;
            PropConsumer = propConsumer;
        }

        public TerminalDropGenerationAuthorityV1 Authority { get; }
        public EnemyTerminalDropFactConsumerV1 EnemyConsumer { get; }
        public PropTerminalDropFactConsumerV1 PropConsumer { get; }

        public static TerminalDropBindingCompositionV1 Create(
            EnemyCatalogV1 enemyCatalog,
            IEnemyTerminalSourceContextResolverV1 enemySourceContexts,
            PropCatalogV1 propCatalog,
            IPropTerminalSourceContextResolverV1 propSourceContexts,
            ITerminalDropRunContextResolverV1 runContexts,
            IRewardProfileResolverV1 rewardProfiles,
            RewardGenerationServiceV1 rewardGenerationService,
            IGeneratedTerminalDropResultSinkV1 resultSink,
            IEnumerable<ITerminalDropFactAdapterV1> additionalAdapters = null)
        {
            if (enemyCatalog == null) throw new ArgumentNullException(nameof(enemyCatalog));
            if (enemySourceContexts == null)
                throw new ArgumentNullException(nameof(enemySourceContexts));
            if (propCatalog == null) throw new ArgumentNullException(nameof(propCatalog));
            if (propSourceContexts == null)
                throw new ArgumentNullException(nameof(propSourceContexts));
            if (runContexts == null) throw new ArgumentNullException(nameof(runContexts));
            if (rewardProfiles == null) throw new ArgumentNullException(nameof(rewardProfiles));
            if (rewardGenerationService == null)
                throw new ArgumentNullException(nameof(rewardGenerationService));
            if (resultSink == null) throw new ArgumentNullException(nameof(resultSink));

            var adapters = new List<ITerminalDropFactAdapterV1>
            {
                new ContextResolvedEnemyDeathTerminalDropFactAdapterV1(
                    new EnemyDeathTerminalDropFactAdapterV1(enemyCatalog),
                    enemySourceContexts),
                new PropDestructionTerminalDropFactAdapterV1(
                    propCatalog,
                    propSourceContexts)
            };
            if (additionalAdapters != null)
            {
                foreach (ITerminalDropFactAdapterV1 adapter in additionalAdapters)
                {
                    if (adapter == null)
                        throw new ArgumentException(
                            "Additional terminal-drop adapters cannot contain null.",
                            nameof(additionalAdapters));
                    adapters.Add(adapter);
                }
            }

            var authority = new TerminalDropGenerationAuthorityV1(
                new TerminalDropFactAdapterRegistryV1(adapters),
                runContexts,
                rewardProfiles,
                new ExistingRewardGenerationExecutorV1(rewardGenerationService));
            return new TerminalDropBindingCompositionV1(
                authority,
                new EnemyTerminalDropFactConsumerV1(authority, resultSink),
                new PropTerminalDropFactConsumerV1(authority, resultSink));
        }
    }
}
