using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Domain.Enemies.Catalog;
using ShooterMover.Domain.Props;
using ShooterMover.EnemyRuntimeComposition;

namespace ShooterMover.TerminalDropBinding
{
    public interface IPropTerminalDropFactConsumerV1
    {
        void Consume(PropFactBatchV1 fact);
    }

    /// <summary>
    /// Typed bridge for the existing generic enemy terminal consequence port. Every
    /// delivery reaches the idempotent pending admission boundary, including exact replay,
    /// so a lost first publication can be recovered without creating a second entry.
    /// </summary>
    public sealed class EnemyTerminalDropFactConsumerV1 : IEnemyDropFactConsumerV1
    {
        private readonly TerminalDropGenerationAuthorityV1 authority;
        private readonly IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission;

        public EnemyTerminalDropFactConsumerV1(
            TerminalDropGenerationAuthorityV1 authority,
            IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission)
        {
            this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
            this.pendingAdmission = pendingAdmission
                ?? throw new ArgumentNullException(nameof(pendingAdmission));
        }

        public PendingTerminalDropAdmissionResultV1 LastAdmission { get; private set; }

        public void Consume(EnemyDeathFactV1 fact)
        {
            LastAdmission = pendingAdmission.Admit(authority.Generate(fact));
        }
    }

    public sealed class PropTerminalDropFactConsumerV1 : IPropTerminalDropFactConsumerV1
    {
        private readonly TerminalDropGenerationAuthorityV1 authority;
        private readonly IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission;

        public PropTerminalDropFactConsumerV1(
            TerminalDropGenerationAuthorityV1 authority,
            IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission)
        {
            this.authority = authority ?? throw new ArgumentNullException(nameof(authority));
            this.pendingAdmission = pendingAdmission
                ?? throw new ArgumentNullException(nameof(pendingAdmission));
        }

        public PendingTerminalDropAdmissionResultV1 LastAdmission { get; private set; }

        public void Consume(PropFactBatchV1 fact)
        {
            LastAdmission = pendingAdmission.Admit(authority.Generate(fact));
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
            IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission,
            EnemyTerminalDropFactConsumerV1 enemyConsumer,
            PropTerminalDropFactConsumerV1 propConsumer)
        {
            Authority = authority;
            PendingAdmission = pendingAdmission;
            EnemyConsumer = enemyConsumer;
            PropConsumer = propConsumer;
        }

        public TerminalDropGenerationAuthorityV1 Authority { get; }
        public IGeneratedTerminalDropPendingAdmissionV1 PendingAdmission { get; }
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
            IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission,
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
            if (pendingAdmission == null)
                throw new ArgumentNullException(nameof(pendingAdmission));

            var adapters = new List<ITerminalDropFactAdapterV1>
            {
                new ContextResolvedEnemyDeathTerminalDropFactAdapterV1(
                    enemyCatalog,
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
                pendingAdmission,
                new EnemyTerminalDropFactConsumerV1(authority, pendingAdmission),
                new PropTerminalDropFactConsumerV1(authority, pendingAdmission));
        }
    }
}
