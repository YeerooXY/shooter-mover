using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public interface IPendingTerminalDropAdmissionConsumerV1
    {
        void Consume(PendingTerminalDropAdmissionResultV1 admission);
    }

    public sealed class EnemyTerminalDropFactConsumerV1 : IEnemyDropFactConsumerV1
    {
        private readonly TerminalDropGenerationAuthorityV1 authority;
        private readonly IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission;
        private readonly IPendingTerminalDropAdmissionConsumerV1 admissionConsumer;
        private ReadOnlyCollection<PendingTerminalDropAdmissionResultV1> lastAdmissions =
            new ReadOnlyCollection<PendingTerminalDropAdmissionResultV1>(
                new List<PendingTerminalDropAdmissionResultV1>());

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

        public PendingTerminalDropAdmissionResultV1 LastAdmission
        {
            get
            {
                return lastAdmissions.Count == 0
                    ? null
                    : lastAdmissions[lastAdmissions.Count - 1];
            }
        }

        public IReadOnlyList<PendingTerminalDropAdmissionResultV1> LastAdmissions
        {
            get { return lastAdmissions; }
        }

        public void Consume(EnemyDeathFactV1 fact)
        {
            lastAdmissions = AdmitBatch(authority.GenerateBatch(fact));
        }

        private ReadOnlyCollection<PendingTerminalDropAdmissionResultV1> AdmitBatch(
            TerminalPersonalRewardBatchV1 batch)
        {
            var values = new List<PendingTerminalDropAdmissionResultV1>();
            if (batch != null && batch.IsAccepted)
            {
                for (int index = 0; index < batch.Results.Count; index++)
                {
                    PendingTerminalDropAdmissionResultV1 admission =
                        pendingAdmission.Admit(batch.Results[index]);
                    values.Add(admission);
                    if (admissionConsumer != null && admission != null)
                    {
                        admissionConsumer.Consume(admission);
                    }
                }
            }
            return new ReadOnlyCollection<PendingTerminalDropAdmissionResultV1>(values);
        }
    }

    public sealed class PropTerminalDropFactConsumerV1 : IPropTerminalDropFactConsumerV1
    {
        private readonly TerminalDropGenerationAuthorityV1 authority;
        private readonly IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission;
        private readonly IPendingTerminalDropAdmissionConsumerV1 admissionConsumer;
        private ReadOnlyCollection<PendingTerminalDropAdmissionResultV1> lastAdmissions =
            new ReadOnlyCollection<PendingTerminalDropAdmissionResultV1>(
                new List<PendingTerminalDropAdmissionResultV1>());

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

        public PendingTerminalDropAdmissionResultV1 LastAdmission
        {
            get
            {
                return lastAdmissions.Count == 0
                    ? null
                    : lastAdmissions[lastAdmissions.Count - 1];
            }
        }

        public IReadOnlyList<PendingTerminalDropAdmissionResultV1> LastAdmissions
        {
            get { return lastAdmissions; }
        }

        public void Consume(PropFactBatchV1 fact)
        {
            lastAdmissions = AdmitBatch(authority.GenerateBatch(fact));
        }

        private ReadOnlyCollection<PendingTerminalDropAdmissionResultV1> AdmitBatch(
            TerminalPersonalRewardBatchV1 batch)
        {
            var values = new List<PendingTerminalDropAdmissionResultV1>();
            if (batch != null && batch.IsAccepted)
            {
                for (int index = 0; index < batch.Results.Count; index++)
                {
                    PendingTerminalDropAdmissionResultV1 admission =
                        pendingAdmission.Admit(batch.Results[index]);
                    values.Add(admission);
                    if (admissionConsumer != null && admission != null)
                    {
                        admissionConsumer.Consume(admission);
                    }
                }
            }
            return new ReadOnlyCollection<PendingTerminalDropAdmissionResultV1>(values);
        }
    }

    public sealed class TerminalDropBindingCompositionV1
    {
        private TerminalDropBindingCompositionV1(
            TerminalDropGenerationAuthorityV1 authority,
            TerminalRunMinimumGenerationAuthorityV1 runMinimumAuthority,
            IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission,
            EnemyTerminalDropFactConsumerV1 enemyConsumer,
            PropTerminalDropFactConsumerV1 propConsumer)
        {
            Authority = authority;
            RunMinimumAuthority = runMinimumAuthority;
            PendingAdmission = pendingAdmission;
            EnemyConsumer = enemyConsumer;
            PropConsumer = propConsumer;
        }

        public TerminalDropGenerationAuthorityV1 Authority { get; }
        public TerminalRunMinimumGenerationAuthorityV1 RunMinimumAuthority { get; }
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
            PersonalRewardGenerationServiceV1 personalGenerationService = null,
            ITerminalRewardParticipantResolverV1 participantResolver = null,
            ITerminalRewardEnvironmentResolverV1 environmentResolver = null,
            ITerminalRewardOverrideResolverV1 overrideResolver = null,
            IPersonalRewardDeliveryOutboxV1 deliveryOutbox = null)
        {
            if (enemyCatalog == null)
                throw new ArgumentNullException(nameof(enemyCatalog));
            if (enemySourceContexts == null)
                throw new ArgumentNullException(nameof(enemySourceContexts));
            if (propCatalog == null)
                throw new ArgumentNullException(nameof(propCatalog));
            if (propSourceContexts == null)
                throw new ArgumentNullException(nameof(propSourceContexts));
            if (runContexts == null)
                throw new ArgumentNullException(nameof(runContexts));
            if (pendingAdmission == null)
                throw new ArgumentNullException(nameof(pendingAdmission));

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

            _ = legacyRewardProfiles;
            _ = legacyRewardGenerationService;
            var registry = new TerminalDropFactAdapterRegistryV1(adapters);
            PersonalRewardGenerationServiceV1 generation =
                personalGenerationService
                ?? new PersonalRewardGenerationServiceV1(
                    new ParticipantDropPacingAuthorityV1());
            ITerminalRewardParticipantResolverV1 resolvedParticipants =
                participantResolver
                ?? new AttributedTerminalRewardParticipantResolverV1();
            ITerminalRewardEnvironmentResolverV1 resolvedEnvironment =
                environmentResolver
                ?? new DefaultTerminalRewardEnvironmentResolverV1();
            ITerminalRewardOverrideResolverV1 resolvedOverrides =
                overrideResolver
                ?? new EmptyTerminalRewardOverrideResolverV1();
            var profileResolver = new RewardProfileResolverV1();
            var personal = new TerminalPersonalRewardGenerationAuthorityV1(
                registry,
                runContexts,
                resolvedParticipants,
                resolvedEnvironment,
                resolvedOverrides,
                profileResolver,
                generation,
                deliveryOutbox);
            var authority = new TerminalDropGenerationAuthorityV1(registry, personal);
            var runMinimum = new TerminalRunMinimumGenerationAuthorityV1(
                runContexts,
                resolvedParticipants,
                resolvedEnvironment,
                profileResolver,
                generation,
                deliveryOutbox);
            return new TerminalDropBindingCompositionV1(
                authority,
                runMinimum,
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
