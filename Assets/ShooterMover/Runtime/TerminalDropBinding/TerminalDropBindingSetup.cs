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
    public interface IPropTerminalDropFactConsumer
    {
        void Consume(PropFactBatch fact);
    }

    public interface IPendingTerminalDropAdmissionConsumer
    {
        void Consume(PendingTerminalDropAdmissionResult admission);
    }

    /// <summary>
    /// Validates that an accepted terminal-reward generation batch reached the pending
    /// admission authority without loss, substitution, or conflicting operation reuse.
    /// Explicit no-drop is a valid committed result; rejected generation is not.
    /// </summary>
    internal static class TerminalDropPendingPublicationPolicy
    {
        public static void Validate(
            TerminalPersonalRewardBatch batch,
            IReadOnlyList<PendingTerminalDropAdmissionResult> admissions)
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
            if (batch.Status
                == TerminalPersonalRewardBatchStatus.NoEligibleParticipants)
            {
                if (resultCount != 0 || admissionCount != 0)
                {
                    throw new InvalidOperationException(
                        "A no-eligible-participants batch cannot contain generated "
                        + "results or pending admissions.");
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
                GeneratedTerminalDropResult result = batch.Results[index];
                PendingTerminalDropAdmissionResult admission = admissions[index];
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

    public sealed class EnemyTerminalDropFactConsumer : IEnemyDropFactConsumer
    {
        private readonly TerminalDropGenerationState authority;
        private readonly IGeneratedTerminalDropPendingAdmission pendingAdmission;
        private readonly IPendingTerminalDropAdmissionConsumer admissionConsumer;
        private readonly bool requireAcceptedPublication;
        private ReadOnlyCollection<PendingTerminalDropAdmissionResult> lastAdmissions =
            EmptyAdmissions();
        private TerminalPersonalRewardBatch lastBatch;

        public EnemyTerminalDropFactConsumer(
            TerminalDropGenerationState authority,
            IGeneratedTerminalDropPendingAdmission pendingAdmission,
            IPendingTerminalDropAdmissionConsumer admissionConsumer = null,
            bool requireAcceptedPublication = false)
        {
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            this.pendingAdmission = pendingAdmission
                ?? throw new ArgumentNullException(nameof(pendingAdmission));
            this.admissionConsumer = admissionConsumer;
            this.requireAcceptedPublication = requireAcceptedPublication;
        }

        public PendingTerminalDropAdmissionResult LastAdmission
        {
            get
            {
                return lastAdmissions.Count == 0
                    ? null
                    : lastAdmissions[lastAdmissions.Count - 1];
            }
        }

        public IReadOnlyList<PendingTerminalDropAdmissionResult> LastAdmissions
        {
            get { return lastAdmissions; }
        }

        public TerminalPersonalRewardBatch LastBatch
        {
            get { return lastBatch; }
        }

        public void Consume(EnemyDeathFact fact)
        {
            lastBatch = null;
            lastAdmissions = EmptyAdmissions();

            lastBatch = authority.GenerateBatch(fact);
            lastAdmissions = AdmitBatch(lastBatch);
            if (requireAcceptedPublication)
            {
                TerminalDropPendingPublicationPolicy.Validate(
                    lastBatch,
                    lastAdmissions);
            }

            PublishAdmissions(lastAdmissions);
        }

        private ReadOnlyCollection<PendingTerminalDropAdmissionResult> AdmitBatch(
            TerminalPersonalRewardBatch batch)
        {
            var values = new List<PendingTerminalDropAdmissionResult>();
            var view = new ReadOnlyCollection<PendingTerminalDropAdmissionResult>(
                values);
            lastAdmissions = view;
            if (batch != null && batch.IsAccepted)
            {
                for (int index = 0; index < batch.Results.Count; index++)
                {
                    values.Add(pendingAdmission.Admit(batch.Results[index]));
                }
            }
            return view;
        }

        private void PublishAdmissions(
            IReadOnlyList<PendingTerminalDropAdmissionResult> admissions)
        {
            if (admissionConsumer == null || admissions == null) return;
            for (int index = 0; index < admissions.Count; index++)
            {
                PendingTerminalDropAdmissionResult admission = admissions[index];
                if (admission != null)
                {
                    admissionConsumer.Consume(admission);
                }
            }
        }

        private static ReadOnlyCollection<PendingTerminalDropAdmissionResult>
            EmptyAdmissions()
        {
            return new ReadOnlyCollection<PendingTerminalDropAdmissionResult>(
                new List<PendingTerminalDropAdmissionResult>());
        }
    }

    public sealed class PropTerminalDropFactConsumer : IPropTerminalDropFactConsumer
    {
        private readonly TerminalDropGenerationState authority;
        private readonly IGeneratedTerminalDropPendingAdmission pendingAdmission;
        private readonly IPendingTerminalDropAdmissionConsumer admissionConsumer;
        private readonly bool requireAcceptedPublication;
        private ReadOnlyCollection<PendingTerminalDropAdmissionResult> lastAdmissions =
            EmptyAdmissions();
        private TerminalPersonalRewardBatch lastBatch;

        public PropTerminalDropFactConsumer(
            TerminalDropGenerationState authority,
            IGeneratedTerminalDropPendingAdmission pendingAdmission,
            IPendingTerminalDropAdmissionConsumer admissionConsumer = null,
            bool requireAcceptedPublication = false)
        {
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            this.pendingAdmission = pendingAdmission
                ?? throw new ArgumentNullException(nameof(pendingAdmission));
            this.admissionConsumer = admissionConsumer;
            this.requireAcceptedPublication = requireAcceptedPublication;
        }

        public PendingTerminalDropAdmissionResult LastAdmission
        {
            get
            {
                return lastAdmissions.Count == 0
                    ? null
                    : lastAdmissions[lastAdmissions.Count - 1];
            }
        }

        public IReadOnlyList<PendingTerminalDropAdmissionResult> LastAdmissions
        {
            get { return lastAdmissions; }
        }

        public TerminalPersonalRewardBatch LastBatch
        {
            get { return lastBatch; }
        }

        public void Consume(PropFactBatch fact)
        {
            lastBatch = null;
            lastAdmissions = EmptyAdmissions();

            lastBatch = authority.GenerateBatch(fact);
            lastAdmissions = AdmitBatch(lastBatch);
            if (requireAcceptedPublication)
            {
                TerminalDropPendingPublicationPolicy.Validate(
                    lastBatch,
                    lastAdmissions);
            }

            PublishAdmissions(lastAdmissions);
        }

        private ReadOnlyCollection<PendingTerminalDropAdmissionResult> AdmitBatch(
            TerminalPersonalRewardBatch batch)
        {
            var values = new List<PendingTerminalDropAdmissionResult>();
            var view = new ReadOnlyCollection<PendingTerminalDropAdmissionResult>(
                values);
            lastAdmissions = view;
            if (batch != null && batch.IsAccepted)
            {
                for (int index = 0; index < batch.Results.Count; index++)
                {
                    values.Add(pendingAdmission.Admit(batch.Results[index]));
                }
            }
            return view;
        }

        private void PublishAdmissions(
            IReadOnlyList<PendingTerminalDropAdmissionResult> admissions)
        {
            if (admissionConsumer == null || admissions == null) return;
            for (int index = 0; index < admissions.Count; index++)
            {
                PendingTerminalDropAdmissionResult admission = admissions[index];
                if (admission != null)
                {
                    admissionConsumer.Consume(admission);
                }
            }
        }

        private static ReadOnlyCollection<PendingTerminalDropAdmissionResult>
            EmptyAdmissions()
        {
            return new ReadOnlyCollection<PendingTerminalDropAdmissionResult>(
                new List<PendingTerminalDropAdmissionResult>());
        }
    }

    public sealed class TerminalDropBindingSetup
    {
        private TerminalDropBindingSetup(
            TerminalDropGenerationState authority,
            TerminalRunMinimumGenerationState runMinimumAuthority,
            IGeneratedTerminalDropPendingAdmission pendingAdmission,
            EnemyTerminalDropFactConsumer enemyConsumer,
            PropTerminalDropFactConsumer propConsumer)
        {
            Authority = authority;
            RunMinimumAuthority = runMinimumAuthority;
            PendingAdmission = pendingAdmission;
            EnemyConsumer = enemyConsumer;
            PropConsumer = propConsumer;
        }

        public TerminalDropGenerationState Authority { get; }
        public TerminalRunMinimumGenerationState RunMinimumAuthority { get; }
        public IGeneratedTerminalDropPendingAdmission PendingAdmission { get; }
        public EnemyTerminalDropFactConsumer EnemyConsumer { get; }
        public PropTerminalDropFactConsumer PropConsumer { get; }

        public static TerminalDropBindingSetup Create(
            EnemyCatalog enemyCatalog,
            IEnemyTerminalSourceContextResolver enemySourceContexts,
            PropCatalog propCatalog,
            IPropTerminalSourceContextResolver propSourceContexts,
            ITerminalDropRunContextResolver runContexts,
            IRewardProfileResolver legacyRewardProfiles,
            RewardGenerationActions legacyRewardGenerationService,
            IGeneratedTerminalDropPendingAdmission pendingAdmission,
            IEnumerable<ITerminalDropFactBridge> additionalAdapters = null,
            IPendingTerminalDropAdmissionConsumer admissionConsumer = null,
            PersonalRewardGenerationActions personalGenerationService = null,
            ITerminalRewardParticipantResolver participantResolver = null,
            ITerminalRewardEnvironmentResolver environmentResolver = null,
            ITerminalRewardOverrideResolver overrideResolver = null,
            IPersonalRewardDeliveryOutbox deliveryOutbox = null,
            bool requireAcceptedPublication = false)
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

            var adapters = new List<ITerminalDropFactBridge>
            {
                new ContextResolvedEnemyDeathTerminalDropFactBridge(
                    enemyCatalog,
                    enemySourceContexts),
                new PropDestructionTerminalDropFactBridge(
                    propCatalog,
                    propSourceContexts),
            };
            if (additionalAdapters != null)
            {
                foreach (ITerminalDropFactBridge adapter in additionalAdapters)
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
            var registry = new TerminalDropFactBridgeRegistry(adapters);
            PersonalRewardGenerationActions generation =
                personalGenerationService
                ?? new PersonalRewardGenerationActions(
                    new ParticipantDropPacing());
            ITerminalRewardParticipantResolver resolvedParticipants =
                participantResolver
                ?? new AttributedTerminalRewardParticipantResolver();
            ITerminalRewardEnvironmentResolver resolvedEnvironment =
                environmentResolver
                ?? new DefaultTerminalRewardEnvironmentResolver();
            ITerminalRewardOverrideResolver resolvedOverrides =
                overrideResolver
                ?? new EmptyTerminalRewardOverrideResolver();
            var profileResolver = new RewardProfileResolver();
            var personal = new TerminalPersonalRewardGenerationState(
                registry,
                runContexts,
                resolvedParticipants,
                resolvedEnvironment,
                resolvedOverrides,
                profileResolver,
                generation,
                deliveryOutbox);
            var authority = new TerminalDropGenerationState(registry, personal);
            var runMinimum = new TerminalRunMinimumGenerationState(
                runContexts,
                resolvedParticipants,
                resolvedEnvironment,
                profileResolver,
                generation,
                deliveryOutbox);
            return new TerminalDropBindingSetup(
                authority,
                runMinimum,
                pendingAdmission,
                new EnemyTerminalDropFactConsumer(
                    authority,
                    pendingAdmission,
                    admissionConsumer,
                    requireAcceptedPublication),
                new PropTerminalDropFactConsumer(
                    authority,
                    pendingAdmission,
                    admissionConsumer,
                    requireAcceptedPublication));
        }
    }
}
