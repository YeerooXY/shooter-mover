using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Drops;
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
    /// Downstream observer of the exact pending-admission result. Observers receive the
    /// admitted batch itself; they never trigger reward generation again.
    /// </summary>
    public interface IPendingTerminalDropAdmissionConsumerV1
    {
        void Consume(PendingTerminalDropAdmissionResultV1 admission);
    }

    public sealed class EnemyTerminalDropFactConsumerV1 : IEnemyDropFactConsumerV1
    {
        private readonly TerminalDropGenerationAuthorityV1 authority;
        private readonly IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission;
        private readonly IPendingTerminalDropAdmissionConsumerV1 admissionConsumer;

        public EnemyTerminalDropFactConsumerV1(
            TerminalDropGenerationAuthorityV1 authority,
            IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission,
            IPendingTerminalDropAdmissionConsumerV1 admissionConsumer = null)
        {
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            this.pendingAdmission = pendingAdmission
                ?? throw new ArgumentNullException(nameof(pendingAdmission));
            this.admissionConsumer = admissionConsumer;
        }

        public PendingTerminalDropAdmissionResultV1 LastAdmission { get; private set; }

        public void Consume(EnemyDeathFactV1 fact)
        {
            LastAdmission = pendingAdmission.Admit(authority.Generate(fact));
            if (admissionConsumer != null && LastAdmission != null)
            {
                admissionConsumer.Consume(LastAdmission);
            }
        }
    }

    public sealed class PropTerminalDropFactConsumerV1 : IPropTerminalDropFactConsumerV1
    {
        private readonly TerminalDropGenerationAuthorityV1 authority;
        private readonly IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission;
        private readonly IPendingTerminalDropAdmissionConsumerV1 admissionConsumer;

        public PropTerminalDropFactConsumerV1(
            TerminalDropGenerationAuthorityV1 authority,
            IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission,
            IPendingTerminalDropAdmissionConsumerV1 admissionConsumer = null)
        {
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            this.pendingAdmission = pendingAdmission
                ?? throw new ArgumentNullException(nameof(pendingAdmission));
            this.admissionConsumer = admissionConsumer;
        }

        public PendingTerminalDropAdmissionResultV1 LastAdmission { get; private set; }

        public void Consume(PropFactBatchV1 fact)
        {
            LastAdmission = pendingAdmission.Admit(authority.Generate(fact));
            if (admissionConsumer != null && LastAdmission != null)
            {
                admissionConsumer.Consume(LastAdmission);
            }
        }
    }

    /// <summary>
    /// Typed terminal-to-pickup composition. Legacy REW-001 inputs remain optional
    /// migration parameters, but every live roll is owned by one shared personal
    /// generation service and one pending-admission authority.
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
            IRewardProfileResolverV1 legacyRewardProfiles,
            RewardGenerationServiceV1 legacyRewardGenerationService,
            IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission,
            IEnumerable<ITerminalDropFactAdapterV1> additionalAdapters = null,
            IPendingTerminalDropAdmissionConsumerV1 admissionConsumer = null,
            PersonalRewardGenerationServiceV1 personalGenerationService = null)
        {
            if (enemyCatalog == null)
            {
                throw new ArgumentNullException(nameof(enemyCatalog));
            }
            if (enemySourceContexts == null)
            {
                throw new ArgumentNullException(nameof(enemySourceContexts));
            }
            if (propCatalog == null)
            {
                throw new ArgumentNullException(nameof(propCatalog));
            }
            if (propSourceContexts == null)
            {
                throw new ArgumentNullException(nameof(propSourceContexts));
            }
            if (runContexts == null)
            {
                throw new ArgumentNullException(nameof(runContexts));
            }
            if (pendingAdmission == null)
            {
                throw new ArgumentNullException(nameof(pendingAdmission));
            }

            var adapters = new List<ITerminalDropFactAdapterV1>
            {
                new ContextResolvedEnemyDeathTerminalDropFactAdapterV1(
                    enemyCatalog,
                    enemySourceContexts),
                new PropDestructionTerminalDropFactAdapterV1(
                    propCatalog,
                    propSourceContexts),
            };
            if (additionalAdapters != null)
            {
                foreach (ITerminalDropFactAdapterV1 adapter in additionalAdapters)
                {
                    if (adapter == null)
                    {
                        throw new ArgumentException(
                            "Additional terminal-drop adapters cannot contain null.",
                            nameof(additionalAdapters));
                    }
                    adapters.Add(adapter);
                }
            }

            IRewardGenerationExecutorV1 legacyExecutor =
                legacyRewardGenerationService == null
                    ? null
                    : new ExistingRewardGenerationExecutorV1(
                        legacyRewardGenerationService);
            var authority = new TerminalDropGenerationAuthorityV1(
                new TerminalDropFactAdapterRegistryV1(adapters),
                runContexts,
                legacyRewardProfiles,
                legacyExecutor,
                personalGenerationService);
            return new TerminalDropBindingCompositionV1(
                authority,
                pendingAdmission,
                new EnemyTerminalDropFactConsumerV1(
                    authority,
                    pendingAdmission,
                    admissionConsumer),
                new PropTerminalDropFactConsumerV1(
                    authority,
                    pendingAdmission,
                    admissionConsumer));
        }
    }
}
