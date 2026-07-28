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

    /// <summary>
    /// Validates that an accepted terminal-reward generation batch reached the pending
    /// admission authority without loss, substitution, or conflicting operation reuse.
    /// Explicit no-drop is a valid committed result; rejected generation is not.
    /// </summary>
    internal static class TerminalDropPendingPublicationPolicyV1
    {
        public static void Validate(
            TerminalPersonalRewardBatchV1 batch,
            IReadOnlyList<PendingTerminalDropAdmissionResultV1> admissions)
        {
            if (batch == null)
            {
                throw new InvalidOperationException(
                    "Terminal reward generation returned no batch.");
            }
            if (!batch.IsAccepted)
            {
                throw new InvalidOperationException(
                    "Terminal reward generation was rejected: "
                    + (string.IsNullOrWhiteSpace(batch.Diagnostic)
                        ? batch.Status.ToString()
                        : batch.Diagnostic));
            }

            int resultCount = batch.Results == null ? 0 : batch.Results.Count;
            int admissionCount = admissions == null ? 0 : admissions.Count;
            if (batch.Status == TerminalPersonalRewardBatchStatusV1.ExplicitNoDrop)
            {
                if (resultCount != 0 || admissionCount != 0)
                {
                    throw new InvalidOperationException(
                        "An explicit no-drop batch cannot contain generated results "
                        + "or pending admissions.");
                }
                return;
            }
            if (resultCount < 1)
            {
                throw new InvalidOperationException(
                    "A generated terminal reward batch must contain at least one result.");
            }
            if (admissionCount != resultCount)
            {
                throw new InvalidOperationException(
                    "Every generated terminal reward result must have one pending admission.");
            }

            for (int index = 0; index < resultCount; index++)
            {
                GeneratedTerminalDropResultV1 result = batch.Results[index];
                PendingTerminalDropAdmissionResultV1 admission = admissions[index];
                if (result == null
                    || !result.IsAccepted
                    || result.OperationRequest == null
                    || result.OperationRequest.SourceOperationStableId == null
                    || string.IsNullOrWhiteSpace(result.Fingerprint))
                {
                    throw new InvalidOperationException(
                        "A generated terminal reward result is missing accepted identity.");
                }
                if (admission == null
                    || !admission.IsAccepted
                    || admission.OperationStableId == null
                    || admission.PendingResult == null)
                {
                    throw new InvalidOperationException(
                        "A generated terminal reward result was not admitted to pending state.");
                }
                if (admission.OperationStableId
                        != result.OperationRequest.SourceOperationStableId
                    || !string.Equals(
                        admission.BatchFingerprint,
                        result.Fingerprint,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        admission.PendingResult.Fingerprint,
                        result.Fingerprint,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Pending terminal reward admission does not match its generated result.");
                }
            }
        }
    }

    public sealed class EnemyTerminalDropFactConsumerV1 : IEnemyDropFactConsumerV1
    {
        private readonly TerminalDropGenerationAuthorityV1 authority;
        private readonly IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission;
        private readonly IPendingTerminalDropAdmissionConsumerV1 admissionConsumer;
        private readonly bool requireAcceptedPublication;
        private ReadOnlyCollection<PendingTerminalDropAdmissionResultV1> lastAdmissions =
            EmptyAdmissions();
        private TerminalPersonalRewardBatchV1 lastBatch;

        public EnemyTerminalDropFactConsumerV1(
            TerminalDropGenerationAuthorityV1 authority,
            IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission,
            IPendingTerminalDropAdmissionConsumerV1 admissionConsumer = null,
            bool requireAcceptedPublication = false)
        {
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            this.pendingAdmission = pendingAdmission
                ?? throw new ArgumentNullException(nameof(pendingAdmission));
            this.admissionConsumer = admissionConsumer;
            this.requireAcceptedPublication = requireAcceptedPublication;
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

        public TerminalPersonalRewardBatchV1 LastBatch
        {
            get { return lastBatch; }
        }

        public void Consume(EnemyDeathFactV1 fact)
        {
            lastBatch = null;
            lastAdmissions = EmptyAdmissions();

            TerminalPersonalRewardBatchV1 generated = authority.GenerateBatch(fact);
            ReadOnlyCollection<PendingTerminalDropAdmissionResultV1> admitted =
                AdmitBatch(generated);
            lastBatch = generated;
            lastAdmissions = admitted;
            if (requireAcceptedPublication)
            {
                TerminalDropPendingPublicationPolicyV1.Validate(generated, admitted);
            }

            PublishAdmissions(admitted);
        }

        private ReadOnlyCollection<PendingTerminalDropAdmissionResultV1> AdmitBatch(
            TerminalPersonalRewardBatchV1 batch)
        {
            var values = new List<PendingTerminalDropAdmissionResultV1>();
            if (batch != null && batch.IsAccepted)
            {
                for (int index = 0; index < batch.Results.Count; index++)
                {
                    values.Add(pendingAdmission.Admit(batch.Results[index]));
                }
            }
            return new ReadOnlyCollection<PendingTerminalDropAdmissionResultV1>(values);
        }

        private void PublishAdmissions(
            IReadOnlyList<PendingTerminalDropAdmissionResultV1> admissions)
        {
            if (admissionConsumer == null || admissions == null) return;
            for (int index = 0; index < admissions.Count; index++)
            {
                PendingTerminalDropAdmissionResultV1 admission = admissions[index];
                if (admission != null)
                {
                    admissionConsumer.Consume(admission);
                }
            }
        }

        private static ReadOnlyCollection<PendingTerminalDropAdmissionResultV1>
            EmptyAdmissions()
        {
            return new ReadOnlyCollection<PendingTerminalDropAdmissionResultV1>(
                new List<PendingTerminalDropAdmissionResultV1>());
        }
    }

    public sealed class PropTerminalDropFactConsumerV1 : IPropTerminalDropFactConsumerV1
    {
        private readonly TerminalDropGenerationAuthorityV1 authority;
        private readonly IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission;
        private readonly IPendingTerminalDropAdmissionConsumerV1 admissionConsumer;
        private readonly bool requireAcceptedPublication;
        private ReadOnlyCollection<PendingTerminalDropAdmissionResultV1> lastAdmissions =
            EmptyAdmissions();
        private TerminalPersonalRewardBatchV1 lastBatch;

        public PropTerminalDropFactConsumerV1(
            TerminalDropGenerationAuthorityV1 authority,
            IGeneratedTerminalDropPendingAdmissionV1 pendingAdmission,
            IPendingTerminalDropAdmissionConsumerV1 admissionConsumer = null,
            bool requireAcceptedPublication = false)
        {
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            this.pendingAdmission = pendingAdmission
                ?? throw new ArgumentNullException(nameof(pendingAdmission));
            this.admissionConsumer = admissionConsumer;
            this.requireAcceptedPublication = requireAcceptedPublication;
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

        public TerminalPersonalRewardBatchV1 LastBatch
        {
            get { return lastBatch; }
        }

        public void Consume(PropFactBatchV1 fact)
        {
            lastBatch = null;
            lastAdmissions = EmptyAdmissions();

            TerminalPersonalRewardBatchV1 generated = authority.GenerateBatch(fact);
            ReadOnlyCollection<PendingTerminalDropAdmissionResultV1> admitted =
                AdmitBatch(generated);
            lastBatch = generated;
            lastAdmissions = admitted;
            if (requireAcceptedPublication)
            {
                TerminalDropPendingPublicationPolicyV1.Validate(generated, admitted);
            }

            PublishAdmissions(admitted);
        }

        private ReadOnlyCollection<PendingTerminalDropAdmissionResultV1> AdmitBatch(
            TerminalPersonalRewardBatchV1 batch)
        {
            var values = new List<PendingTerminalDropAdmissionResultV1>();
            if (batch != null && batch.IsAccepted)
            {
                for (int index = 0; index < batch.Results.Count; index++)
                {
                    values.Add(pendingAdmission.Admit(batch.Results[index]));
                }
            }
            return new ReadOnlyCollection<PendingTerminalDropAdmissionResultV1>(values);
        }

        private void PublishAdmissions(
            IReadOnlyList<PendingTerminalDropAdmissionResultV1> admissions)
        {
            if (admissionConsumer == null || admissions == null) return;
            for (int index = 0; index < admissions.Count; index++)
            {
                PendingTerminalDropAdmissionResultV1 admission = admissions[index];
                if (admission != null)
                {
                    admissionConsumer.Consume(admission);
                }
            }
        }

        private static ReadOnlyCollection<PendingTerminalDropAdmissionResultV1>
            EmptyAdmissions()
        {
            return new ReadOnlyCollection<PendingTerminalDropAdmissionResultV1>(
                new List<PendingTerminalDropAdmissionResultV1>());
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
                    admissionConsumer,
                    true),
                new PropTerminalDropFactConsumerV1(
                    authority,
                    pendingAdmission,
                    admissionConsumer,
                    true));
        }
    }
}
