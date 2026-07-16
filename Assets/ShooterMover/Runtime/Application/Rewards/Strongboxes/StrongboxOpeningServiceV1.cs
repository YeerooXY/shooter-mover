using System;
using System.Collections.Generic;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    /// <summary>
    /// Durable strongbox orchestration authority. It freezes one GEN result, rolls
    /// reward application forward through RAP, and removes the owned INV instance
    /// only after RAP confirms the immutable reward as Applied.
    /// </summary>
    public sealed class StrongboxOpeningServiceV1
    {
        private readonly object sync = new object();
        private readonly StrongboxDefinitionCatalogV1 catalog;
        private readonly IStrongboxRewardGeneratorV1 generator;
        private readonly IPlayerHoldingsAuthorityV1 holdings;
        private readonly RewardApplicationServiceV1 rewardApplication;
        private readonly IStrongboxGrantPayloadResolverV1 payloadResolver;
        private Dictionary<StableId, StrongboxInstanceContextV1> contexts;
        private Dictionary<StableId, OpeningRecord> openings;
        private Dictionary<StableId, StableId> openingByBox;
        private long sequence;

        public StrongboxOpeningServiceV1(
            StrongboxDefinitionCatalogV1 catalog,
            IStrongboxRewardGeneratorV1 generator,
            IPlayerHoldingsAuthorityV1 holdings,
            RewardApplicationServiceV1 rewardApplication,
            IStrongboxGrantPayloadResolverV1 payloadResolver)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.holdings = holdings ?? throw new ArgumentNullException(nameof(holdings));
            this.rewardApplication = rewardApplication ?? throw new ArgumentNullException(nameof(rewardApplication));
            this.payloadResolver = payloadResolver ?? throw new ArgumentNullException(nameof(payloadResolver));
            contexts = new Dictionary<StableId, StrongboxInstanceContextV1>();
            openings = new Dictionary<StableId, OpeningRecord>();
            openingByBox = new Dictionary<StableId, StableId>();
        }

        public long Sequence
        {
            get { lock (sync) { return sequence; } }
        }

        public StrongboxRegistrationResultV1 RegisterInstance(StrongboxInstanceContextV1 context)
        {
            lock (sync)
            {
                if (context == null)
                {
                    return new StrongboxRegistrationResultV1(
                        StrongboxRegistrationStatusV1.InvalidContext,
                        null,
                        null,
                        "context-null");
                }

                StrongboxInstanceContextV1 existing;
                if (contexts.TryGetValue(context.InstanceStableId, out existing))
                {
                    if (string.Equals(existing.Fingerprint, context.Fingerprint, StringComparison.Ordinal))
                    {
                        return new StrongboxRegistrationResultV1(
                            StrongboxRegistrationStatusV1.ExactDuplicateNoChange,
                            context.InstanceStableId,
                            existing.Fingerprint,
                            null);
                    }

                    return new StrongboxRegistrationResultV1(
                        StrongboxRegistrationStatusV1.ConflictingDuplicate,
                        context.InstanceStableId,
                        existing.Fingerprint,
                        "strongbox-instance-conflicting-duplicate");
                }

                StrongboxDefinitionV1 definition;
                if (!catalog.TryGet(context.TierStableId, out definition))
                {
                    return new StrongboxRegistrationResultV1(
                        StrongboxRegistrationStatusV1.UnknownDefinition,
                        context.InstanceStableId,
                        context.Fingerprint,
                        "strongbox-tier-unknown");
                }
                if (context.AlgorithmContentFingerprint != null
                    && !string.Equals(
                        context.AlgorithmContentFingerprint,
                        definition.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return new StrongboxRegistrationResultV1(
                        StrongboxRegistrationStatusV1.InvalidContext,
                        context.InstanceStableId,
                        context.Fingerprint,
                        "strongbox-context-content-fingerprint-mismatch");
                }

                contexts.Add(context.InstanceStableId, context);
                return new StrongboxRegistrationResultV1(
                    StrongboxRegistrationStatusV1.Registered,
                    context.InstanceStableId,
                    context.Fingerprint,
                    null);
            }
        }

        public StrongboxOpeningResultRuntimeV1 Open(StrongboxOpenCommandV1 command)
        {
            lock (sync)
            {
                long before = sequence;
                if (command == null)
                {
                    return RuntimeResult(
                        StrongboxOpeningRuntimeStatusV1.InvalidRequest,
                        null,
                        before,
                        before,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        "opening-command-null");
                }

                OpeningRecord existing;
                if (openings.TryGetValue(command.OpeningStableId, out existing))
                {
                    if (!string.Equals(existing.Command.Fingerprint, command.Fingerprint, StringComparison.Ordinal))
                    {
                        return Rejected(
                            command,
                            StrongboxOpeningRuntimeStatusV1.ConflictingDuplicate,
                            StrongboxOpeningStatusV1.ConflictingDuplicate,
                            before,
                            existing,
                            "opening-identity-conflicting-duplicate");
                    }

                    if (existing.Stage == StrongboxOpeningStageV1.Opened)
                    {
                        StrongboxOpeningResultV1 replay = StrongboxOpeningResultV1.Create(
                            existing.GeneratedOutcome.Operation.SourceOperationStableId,
                            StrongboxOpeningStatusV1.ExactDuplicateNoChange,
                            existing.GeneratedOutcome.OpeningRequest.Fingerprint,
                            existing.GeneratedOutcome.RewardResult,
                            existing.GeneratedOutcome.RewardTrace,
                            before,
                            before);
                        return RuntimeResult(
                            StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange,
                            command.OpeningStableId,
                            before,
                            before,
                            command.Fingerprint,
                            existing.GeneratedOutcome,
                            existing.TerminalFact,
                            replay,
                            null,
                            null,
                            null);
                    }

                    if (existing.Stage == StrongboxOpeningStageV1.GeneratorRejected)
                    {
                        return Rejected(
                            command,
                            StrongboxOpeningRuntimeStatusV1.GeneratorRejected,
                            StrongboxOpeningStatusV1.InvalidRequest,
                            before,
                            existing,
                            existing.RejectionCode);
                    }

                    if (existing.Stage == StrongboxOpeningStageV1.PayloadRejected)
                    {
                        return Rejected(
                            command,
                            StrongboxOpeningRuntimeStatusV1.RewardRejected,
                            StrongboxOpeningStatusV1.InvalidRequest,
                            before,
                            existing,
                            existing.RejectionCode);
                    }

                    return Continue(existing, before);
                }

                StableId boundOpeningStableId;
                if (openingByBox.TryGetValue(
                    command.StrongboxInstanceStableId,
                    out boundOpeningStableId))
                {
                    return Rejected(
                        command,
                        StrongboxOpeningRuntimeStatusV1.ConflictingDuplicate,
                        StrongboxOpeningStatusV1.ConflictingDuplicate,
                        before,
                        null,
                        "strongbox-instance-opening-already-bound-" + boundOpeningStableId);
                }

                if (command.ExpectedOpeningSequence.HasValue
                    && command.ExpectedOpeningSequence.Value != sequence)
                {
                    return Rejected(
                        command,
                        StrongboxOpeningRuntimeStatusV1.ExpectedSequenceConflict,
                        StrongboxOpeningStatusV1.ExpectedSequenceConflict,
                        before,
                        null,
                        "opening-expected-sequence-conflict");
                }

                StrongboxInstanceContextV1 context;
                if (!contexts.TryGetValue(command.StrongboxInstanceStableId, out context))
                {
                    return Rejected(
                        command,
                        StrongboxOpeningRuntimeStatusV1.UnknownBoxInstance,
                        StrongboxOpeningStatusV1.StrongboxNotOwned,
                        before,
                        null,
                        "strongbox-instance-unknown");
                }

                StrongboxDefinitionV1 definition;
                if (!catalog.TryGet(context.TierStableId, out definition))
                {
                    return Rejected(
                        command,
                        StrongboxOpeningRuntimeStatusV1.InvalidDefinition,
                        StrongboxOpeningStatusV1.InvalidRequest,
                        before,
                        null,
                        "strongbox-tier-unknown");
                }

                if (context.AlgorithmContentFingerprint != null
                    && !string.Equals(context.AlgorithmContentFingerprint, definition.Fingerprint, StringComparison.Ordinal))
                {
                    return Rejected(
                        command,
                        StrongboxOpeningRuntimeStatusV1.InvalidDefinition,
                        StrongboxOpeningStatusV1.InvalidRequest,
                        before,
                        null,
                        "strongbox-definition-fingerprint-mismatch");
                }

                UniqueHoldingSnapshotV1 owned;
                if (!TryFindOwnedStrongbox(context.InstanceStableId, out owned))
                {
                    return Rejected(
                        command,
                        StrongboxOpeningRuntimeStatusV1.StrongboxNotOwned,
                        StrongboxOpeningStatusV1.StrongboxNotOwned,
                        before,
                        null,
                        "strongbox-not-owned");
                }

                if (owned.DefinitionStableId != context.TierStableId)
                {
                    return Rejected(
                        command,
                        StrongboxOpeningRuntimeStatusV1.InvalidDefinition,
                        StrongboxOpeningStatusV1.InvalidRequest,
                        before,
                        null,
                        "strongbox-owned-definition-mismatch");
                }

                OpeningRecord prepared = Prepare(command, context, definition);
                openings.Add(command.OpeningStableId, prepared);
                openingByBox.Add(command.StrongboxInstanceStableId, command.OpeningStableId);
                if (prepared.Stage == StrongboxOpeningStageV1.GeneratorRejected)
                {
                    return Rejected(
                        command,
                        StrongboxOpeningRuntimeStatusV1.GeneratorRejected,
                        StrongboxOpeningStatusV1.InvalidRequest,
                        before,
                        prepared,
                        prepared.RejectionCode);
                }
                if (prepared.Stage == StrongboxOpeningStageV1.PayloadRejected)
                {
                    return Rejected(
                        command,
                        StrongboxOpeningRuntimeStatusV1.RewardRejected,
                        StrongboxOpeningStatusV1.InvalidRequest,
                        before,
                        prepared,
                        prepared.RejectionCode);
                }

                return Continue(prepared, before);
            }
        }

        public StrongboxOpeningSnapshotV1 ExportSnapshot()
        {
            lock (sync)
            {
                List<StrongboxInstanceContextV1> contextList = new List<StrongboxInstanceContextV1>(contexts.Values);
                List<StrongboxOpeningRecordSnapshotV1> openingList = new List<StrongboxOpeningRecordSnapshotV1>();
                foreach (OpeningRecord record in openings.Values)
                {
                    openingList.Add(record.ToSnapshot());
                }
                return StrongboxOpeningSnapshotV1.CreateCanonical(
                    catalog.Fingerprint,
                    sequence,
                    contextList,
                    openingList);
            }
        }

        public StrongboxOpeningImportResultV1 ImportSnapshot(StrongboxOpeningSnapshotV1 snapshot)
        {
            lock (sync)
            {
                if (snapshot == null)
                {
                    return new StrongboxOpeningImportResultV1(
                        StrongboxOpeningImportStatusV1.InvalidSnapshot,
                        "snapshot-null",
                        sequence);
                }
                if (snapshot.SchemaVersion != StrongboxOpeningSnapshotV1.CurrentSchemaVersion)
                {
                    return new StrongboxOpeningImportResultV1(
                        StrongboxOpeningImportStatusV1.UnsupportedSchemaVersion,
                        "snapshot-schema-unsupported",
                        sequence);
                }
                if (!string.Equals(snapshot.DefinitionCatalogFingerprint, catalog.Fingerprint, StringComparison.Ordinal))
                {
                    return new StrongboxOpeningImportResultV1(
                        StrongboxOpeningImportStatusV1.CatalogMismatch,
                        "snapshot-catalog-mismatch",
                        sequence);
                }
                if (!StrongboxCanonicalV1.IsFingerprint(snapshot.Fingerprint)
                    || !string.Equals(snapshot.Fingerprint, StrongboxOpeningSnapshotV1.ComputeFingerprint(snapshot), StringComparison.Ordinal))
                {
                    return new StrongboxOpeningImportResultV1(
                        StrongboxOpeningImportStatusV1.FingerprintMismatch,
                        "snapshot-fingerprint-mismatch",
                        sequence);
                }

                Dictionary<StableId, StrongboxInstanceContextV1> importedContexts =
                    new Dictionary<StableId, StrongboxInstanceContextV1>();
                for (int index = 0; index < snapshot.Contexts.Count; index++)
                {
                    StrongboxInstanceContextV1 context = snapshot.Contexts[index];
                    if (context == null || importedContexts.ContainsKey(context.InstanceStableId))
                    {
                        return InvalidImport("snapshot-context-duplicate-or-null");
                    }
                    StrongboxDefinitionV1 importedDefinition;
                    if (!catalog.TryGet(context.TierStableId, out importedDefinition)
                        || (context.AlgorithmContentFingerprint != null
                            && !string.Equals(
                                context.AlgorithmContentFingerprint,
                                importedDefinition.Fingerprint,
                                StringComparison.Ordinal)))
                    {
                        return InvalidImport("snapshot-context-definition-invalid");
                    }
                    importedContexts.Add(context.InstanceStableId, context);
                }

                Dictionary<StableId, OpeningRecord> importedOpenings =
                    new Dictionary<StableId, OpeningRecord>();
                Dictionary<StableId, StableId> importedOpeningByBox =
                    new Dictionary<StableId, StableId>();
                long openedCount = 0L;
                for (int index = 0; index < snapshot.Openings.Count; index++)
                {
                    StrongboxOpeningRecordSnapshotV1 opening = snapshot.Openings[index];
                    if (opening == null
                        || importedOpenings.ContainsKey(opening.Command.OpeningStableId)
                        || importedOpeningByBox.ContainsKey(opening.Command.StrongboxInstanceStableId)
                        || !importedContexts.ContainsKey(opening.Command.StrongboxInstanceStableId))
                    {
                        return InvalidImport("snapshot-opening-invalid");
                    }
                    if (opening.Stage == StrongboxOpeningStageV1.Opened)
                    {
                        if (opening.TerminalFact == null
                            || opening.GeneratedOutcome == null
                            || opening.TerminalFact.Status != StrongboxOpeningStatusV1.Opened)
                        {
                            return InvalidImport("snapshot-opened-shape-invalid");
                        }
                        openedCount++;
                    }
                    importedOpenings.Add(opening.Command.OpeningStableId, OpeningRecord.FromSnapshot(opening));
                    importedOpeningByBox.Add(
                        opening.Command.StrongboxInstanceStableId,
                        opening.Command.OpeningStableId);
                }

                if (openedCount != snapshot.Sequence)
                {
                    return InvalidImport("snapshot-sequence-opened-count-mismatch");
                }

                contexts = importedContexts;
                openings = importedOpenings;
                openingByBox = importedOpeningByBox;
                sequence = snapshot.Sequence;
                return new StrongboxOpeningImportResultV1(
                    StrongboxOpeningImportStatusV1.Imported,
                    null,
                    sequence);
            }
        }

        private StrongboxOpeningImportResultV1 InvalidImport(string code)
        {
            return new StrongboxOpeningImportResultV1(
                StrongboxOpeningImportStatusV1.InvalidSnapshot,
                code,
                sequence);
        }

        private OpeningRecord Prepare(
            StrongboxOpenCommandV1 command,
            StrongboxInstanceContextV1 context,
            StrongboxDefinitionV1 definition)
        {
            StableId operationId = StrongboxCanonicalV1.DeriveId(
                "boxop",
                command.OpeningStableId.ToString(),
                context.InstanceStableId.ToString(),
                context.Fingerprint);
            StableId commitmentId = StrongboxCanonicalV1.DeriveId(
                "boxcommit",
                operationId.ToString(),
                context.Fingerprint);
            StableId effectiveProfileId = StrongboxCanonicalV1.DeriveId(
                "boxprofile",
                definition.TierStableId.ToString(),
                definition.Fingerprint);
            StableId scrapGrantId = StrongboxCanonicalV1.DeriveId(
                "boxscrap",
                definition.TierStableId.ToString(),
                definition.MandatoryScrapPolicy.Fingerprint);
            RewardProfileV1 effectiveProfile;
            try
            {
                effectiveProfile = definition.BaseRewardProfile.AppendGuaranteed(
                    effectiveProfileId,
                    new[] { definition.MandatoryScrapPolicy.CreateGrant(scrapGrantId) });
            }
            catch (Exception exception)
            {
                return OpeningRecord.Rejected(
                    command,
                    StrongboxOpeningStageV1.GeneratorRejected,
                    "effective-profile-invalid-" + exception.GetType().Name.ToLowerInvariant());
            }

            RewardOperationRequestV1 operation = RewardOperationRequestV1.Create(
                command.RunStableId,
                context.InstanceStableId,
                operationId,
                commitmentId,
                effectiveProfile.ProfileStableId,
                definition.Fingerprint);
            StableId openingTransactionId = StrongboxCanonicalV1.DeriveId(
                "boxtx",
                operationId.ToString(),
                context.InstanceStableId.ToString());
            StrongboxOpeningRequestV1 openingRequest = StrongboxOpeningRequestV1.Create(
                command.RunStableId,
                operationId,
                openingTransactionId,
                context.InstanceStableId,
                definition.TierStableId,
                commitmentId,
                effectiveProfile.ProfileStableId,
                definition.Fingerprint,
                command.ExpectedOpeningSequence);
            long exceptionalValue;
            try
            {
                exceptionalValue = checked(definition.QualityBias + definition.ExceptionalRollBias);
            }
            catch (OverflowException)
            {
                return OpeningRecord.Rejected(command, StrongboxOpeningStageV1.GeneratorRejected, "strongbox-bias-overflow");
            }
            RewardGenerationRequestV1 generationRequest = RewardGenerationRequestV1.Create(
                operation,
                effectiveProfile,
                context.ProgressionContext,
                context.RootSeed,
                context.AlgorithmVersion,
                new[]
                {
                    RewardGenerationScalingValueV1.Create(definition.TierScalingInputStableId, definition.GenerationBias),
                    RewardGenerationScalingValueV1.Create(definition.ExceptionalScalingInputStableId, exceptionalValue),
                });

            RewardGenerationResultEnvelopeV1 generated;
            try
            {
                generated = generator.Generate(generationRequest);
            }
            catch (Exception exception)
            {
                return OpeningRecord.Rejected(
                    command,
                    StrongboxOpeningStageV1.GeneratorRejected,
                    "generator-exception-" + exception.GetType().Name.ToLowerInvariant());
            }
            if (generated == null
                || !generated.IsSuccess
                || generated.Result == null
                || generated.RewardTrace == null
                || generated.GenerationTrace == null)
            {
                return OpeningRecord.Rejected(
                    command,
                    StrongboxOpeningStageV1.GeneratorRejected,
                    generated == null ? "generator-result-null" : "generator-rejected-" + generated.FailureReason);
            }
            if (!definition.RewardCountPolicy.Accepts(generated.Result.Grants.Count))
            {
                return OpeningRecord.Rejected(command, StrongboxOpeningStageV1.GeneratorRejected, "generated-reward-count-outside-policy");
            }
            if (!ContainsPositiveMandatoryScrap(generated.Result, definition.MandatoryScrapPolicy.CurrencyStableId))
            {
                return OpeningRecord.Rejected(command, StrongboxOpeningStageV1.GeneratorRejected, "generated-mandatory-scrap-missing");
            }

            StrongboxGrantPayloadResolutionV1 resolved;
            try
            {
                resolved = payloadResolver.Resolve(
                    definition,
                    context,
                    operation,
                    generated.Result);
            }
            catch (Exception exception)
            {
                return OpeningRecord.Rejected(
                    command,
                    StrongboxOpeningStageV1.PayloadRejected,
                    "payload-resolution-exception-" + exception.GetType().Name.ToLowerInvariant());
            }
            if (resolved == null || !resolved.Succeeded)
            {
                return OpeningRecord.Rejected(
                    command,
                    StrongboxOpeningStageV1.PayloadRejected,
                    resolved == null ? "payload-resolution-null" : resolved.RejectionCode);
            }

            StrongboxGeneratedOutcomeV1 outcome = new StrongboxGeneratedOutcomeV1(
                openingRequest,
                operation,
                generated.Result,
                generated.RewardTrace,
                generated.GenerationTrace,
                generated.GenerationTrace.Fingerprint,
                resolved.Payloads);
            RewardCommitCommandV1 commit = RewardCommitCommandV1.Create(
                operation,
                generated.Result,
                generated.GenerationTrace.Fingerprint,
                resolved.Payloads);
            StableId claimId = StrongboxCanonicalV1.DeriveId(
                "boxclaim",
                operationId.ToString(),
                commitmentId.ToString(),
                command.ClaimantStableId.ToString());
            RewardClaimCommandV1 claim = RewardClaimCommandV1.Create(
                claimId,
                commitmentId,
                command.ClaimantStableId,
                command.MoneyAuthorityStableId,
                command.ScrapAuthorityStableId,
                command.HoldingsAuthorityStableId);
            StableId consumeTransaction = StrongboxCanonicalV1.DeriveId(
                "boxconsume",
                operationId.ToString(),
                context.InstanceStableId.ToString());
            StableId consumeOperation = StrongboxCanonicalV1.DeriveId(
                "boxconsumeop",
                operationId.ToString(),
                context.InstanceStableId.ToString());
            StableId consumeGrant = StrongboxCanonicalV1.DeriveId(
                "boxconsumegrant",
                operationId.ToString(),
                context.InstanceStableId.ToString());
            PlayerHoldingsCommandV1 consume = PlayerHoldingsCommandV1.RemoveStrongbox(
                consumeTransaction,
                consumeOperation,
                command.HoldingsAuthorityStableId,
                definition.TierStableId,
                context.InstanceStableId,
                HoldingProvenanceV1.Create(consumeGrant, operationId));
            return OpeningRecord.Prepared(command, outcome, commit, claim, consume);
        }

        private StrongboxOpeningResultRuntimeV1 Continue(OpeningRecord record, long before)
        {
            RewardApplicationResultV1 rapResult = null;
            if (record.Stage == StrongboxOpeningStageV1.Prepared)
            {
                rapResult = rewardApplication.Commit(record.CommitCommand);
                if (rapResult.Status != RewardApplicationResultStatusV1.Generated
                    && rapResult.Status != RewardApplicationResultStatusV1.ExactDuplicateNoChange)
                {
                    return RuntimeResult(
                        StrongboxOpeningRuntimeStatusV1.RewardRejected,
                        record.Command.OpeningStableId,
                        before,
                        sequence,
                        record.Command.Fingerprint,
                        record.GeneratedOutcome,
                        record.TerminalFact,
                        null,
                        rapResult,
                        null,
                        rapResult.RejectionCode ?? "reward-commit-rejected");
                }
                if (rapResult.CommitmentState == RewardCommitmentStateV1.Applied)
                {
                    record.Stage = StrongboxOpeningStageV1.RewardApplied;
                }
                else if (rapResult.CommitmentState == RewardCommitmentStateV1.Claimed)
                {
                    record.Stage = StrongboxOpeningStageV1.RewardClaimedPending;
                }
                else
                {
                    record.Stage = StrongboxOpeningStageV1.RewardCommitted;
                }
            }

            if (record.Stage == StrongboxOpeningStageV1.RewardCommitted)
            {
                RewardCommitmentSnapshotV1 currentCommitment;
                if (rewardApplication.TryGetCommitment(
                    record.CommitCommand.CommitmentStableId,
                    out currentCommitment))
                {
                    if (currentCommitment.State == RewardCommitmentStateV1.Applied)
                    {
                        record.Stage = StrongboxOpeningStageV1.RewardApplied;
                    }
                    else if (currentCommitment.State == RewardCommitmentStateV1.Claimed)
                    {
                        record.Stage = StrongboxOpeningStageV1.RewardClaimedPending;
                    }
                }
            }

            if (record.Stage == StrongboxOpeningStageV1.RewardCommitted)
            {
                rapResult = rewardApplication.Claim(record.ClaimCommand);
                if (rapResult.Status == RewardApplicationResultStatusV1.Applied
                    || rapResult.Status == RewardApplicationResultStatusV1.AlreadyAppliedNoChange)
                {
                    record.Stage = StrongboxOpeningStageV1.RewardApplied;
                }
                else if (rapResult.Status == RewardApplicationResultStatusV1.ClaimedPendingApplication)
                {
                    record.Stage = StrongboxOpeningStageV1.RewardClaimedPending;
                    return RuntimeResult(
                        StrongboxOpeningRuntimeStatusV1.ClaimedPendingApplication,
                        record.Command.OpeningStableId,
                        before,
                        sequence,
                        record.Command.Fingerprint,
                        record.GeneratedOutcome,
                        null,
                        null,
                        rapResult,
                        null,
                        rapResult.RejectionCode);
                }
                else
                {
                    return RuntimeResult(
                        MapRapFailure(rapResult.Status),
                        record.Command.OpeningStableId,
                        before,
                        sequence,
                        record.Command.Fingerprint,
                        record.GeneratedOutcome,
                        null,
                        null,
                        rapResult,
                        null,
                        rapResult.RejectionCode ?? "reward-claim-rejected");
                }
            }

            if (record.Stage == StrongboxOpeningStageV1.RewardClaimedPending)
            {
                rapResult = rewardApplication.Retry(
                    RewardRetryClaimCommandV1.Create(
                        record.CommitCommand.CommitmentStableId,
                        record.ClaimCommand.ClaimStableId));
                if (rapResult.Status == RewardApplicationResultStatusV1.Applied
                    || rapResult.Status == RewardApplicationResultStatusV1.AlreadyAppliedNoChange)
                {
                    record.Stage = StrongboxOpeningStageV1.RewardApplied;
                }
                else
                {
                    return RuntimeResult(
                        rapResult.Status == RewardApplicationResultStatusV1.ClaimedPendingApplication
                            ? StrongboxOpeningRuntimeStatusV1.ClaimedPendingApplication
                            : MapRapFailure(rapResult.Status),
                        record.Command.OpeningStableId,
                        before,
                        sequence,
                        record.Command.Fingerprint,
                        record.GeneratedOutcome,
                        null,
                        null,
                        rapResult,
                        null,
                        rapResult.RejectionCode);
                }
            }

            if (record.Stage == StrongboxOpeningStageV1.RewardApplied)
            {
                PlayerHoldingsMutationResultV1 consumeResult;
                try
                {
                    consumeResult = holdings.Apply(record.ConsumeCommand);
                }
                catch (Exception exception)
                {
                    return RuntimeResult(
                        StrongboxOpeningRuntimeStatusV1.ConsumePending,
                        record.Command.OpeningStableId,
                        before,
                        sequence,
                        record.Command.Fingerprint,
                        record.GeneratedOutcome,
                        null,
                        null,
                        rapResult,
                        null,
                        "consume-exception-" + exception.GetType().Name.ToLowerInvariant());
                }
                bool consumed = consumeResult != null
                    && (consumeResult.Status == PlayerHoldingsMutationStatusV1.Applied
                        || (consumeResult.Status == PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange
                            && consumeResult.OriginalStatus == PlayerHoldingsMutationStatusV1.Applied));
                if (!consumed)
                {
                    return RuntimeResult(
                        StrongboxOpeningRuntimeStatusV1.ConsumePending,
                        record.Command.OpeningStableId,
                        before,
                        sequence,
                        record.Command.Fingerprint,
                        record.GeneratedOutcome,
                        null,
                        null,
                        rapResult,
                        consumeResult,
                        consumeResult == null ? "consume-result-null" : consumeResult.RejectionCode ?? "consume-rejected");
                }

                long previous = sequence;
                sequence++;
                record.Stage = StrongboxOpeningStageV1.Opened;
                record.TerminalFact = StrongboxOpeningResultV1.Create(
                    record.GeneratedOutcome.Operation.SourceOperationStableId,
                    StrongboxOpeningStatusV1.Opened,
                    record.GeneratedOutcome.OpeningRequest.Fingerprint,
                    record.GeneratedOutcome.RewardResult,
                    record.GeneratedOutcome.RewardTrace,
                    previous,
                    sequence);
                return RuntimeResult(
                    StrongboxOpeningRuntimeStatusV1.Opened,
                    record.Command.OpeningStableId,
                    before,
                    sequence,
                    record.Command.Fingerprint,
                    record.GeneratedOutcome,
                    record.TerminalFact,
                    record.TerminalFact,
                    rapResult,
                    consumeResult,
                    null);
            }

            return RuntimeResult(
                StrongboxOpeningRuntimeStatusV1.InvalidRequest,
                record.Command.OpeningStableId,
                before,
                sequence,
                record.Command.Fingerprint,
                record.GeneratedOutcome,
                record.TerminalFact,
                null,
                rapResult,
                null,
                "opening-stage-invalid");
        }

        private bool TryFindOwnedStrongbox(StableId instanceStableId, out UniqueHoldingSnapshotV1 holding)
        {
            PlayerHoldingsSnapshotV1 snapshot;
            try
            {
                snapshot = holdings.ExportSnapshot();
            }
            catch
            {
                holding = null;
                return false;
            }
            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshotV1 candidate = snapshot.UniqueHoldings[index];
                if (candidate.InstanceStableId == instanceStableId
                    && candidate.RewardKind == RewardGrantKindV1.Strongbox)
                {
                    holding = candidate;
                    return true;
                }
            }
            holding = null;
            return false;
        }

        private static bool ContainsPositiveMandatoryScrap(RewardResultV1 result, StableId currencyId)
        {
            for (int index = 0; index < result.Grants.Count; index++)
            {
                RewardGrantV1 grant = result.Grants[index];
                if (grant.Kind == RewardGrantKindV1.Scrap
                    && grant.ContentStableId == currencyId
                    && grant.Quantity > 0L)
                {
                    return true;
                }
            }
            return false;
        }

        private static StrongboxOpeningRuntimeStatusV1 MapRapFailure(RewardApplicationResultStatusV1 status)
        {
            if (status == RewardApplicationResultStatusV1.ExpectedSequenceConflict)
            {
                return StrongboxOpeningRuntimeStatusV1.ExpectedSequenceConflict;
            }
            return StrongboxOpeningRuntimeStatusV1.RewardRejected;
        }

        private StrongboxOpeningResultRuntimeV1 Rejected(
            StrongboxOpenCommandV1 command,
            StrongboxOpeningRuntimeStatusV1 runtimeStatus,
            StrongboxOpeningStatusV1 contractStatus,
            long before,
            OpeningRecord record,
            string rejectionCode)
        {
            StableId operationId = record != null && record.GeneratedOutcome != null
                ? record.GeneratedOutcome.Operation.SourceOperationStableId
                : StrongboxCanonicalV1.DeriveId(
                    "boxop",
                    command.OpeningStableId.ToString(),
                    command.StrongboxInstanceStableId.ToString(),
                    command.Fingerprint);
            string requestFingerprint = record != null && record.GeneratedOutcome != null
                ? record.GeneratedOutcome.OpeningRequest.Fingerprint
                : command.Fingerprint;
            StrongboxOpeningResultV1 envelope = StrongboxOpeningResultV1.Create(
                operationId,
                contractStatus,
                requestFingerprint,
                contractStatus == StrongboxOpeningStatusV1.ExactDuplicateNoChange && record != null
                    ? record.GeneratedOutcome.RewardResult : null,
                contractStatus == StrongboxOpeningStatusV1.ExactDuplicateNoChange && record != null
                    ? record.GeneratedOutcome.RewardTrace : null,
                before,
                before);
            return RuntimeResult(
                runtimeStatus,
                command.OpeningStableId,
                before,
                before,
                command.Fingerprint,
                record == null ? null : record.GeneratedOutcome,
                record == null ? null : record.TerminalFact,
                envelope,
                null,
                null,
                rejectionCode);
        }

        private static StrongboxOpeningResultRuntimeV1 RuntimeResult(
            StrongboxOpeningRuntimeStatusV1 status,
            StableId openingStableId,
            long previousSequence,
            long currentSequence,
            string requestFingerprint,
            StrongboxGeneratedOutcomeV1 outcome,
            StrongboxOpeningResultV1 terminalFact,
            StrongboxOpeningResultV1 replayEnvelope,
            RewardApplicationResultV1 rapResult,
            PlayerHoldingsMutationResultV1 consumeResult,
            string rejectionCode)
        {
            return new StrongboxOpeningResultRuntimeV1(
                status,
                openingStableId,
                previousSequence,
                currentSequence,
                requestFingerprint,
                outcome,
                terminalFact,
                replayEnvelope,
                rapResult,
                consumeResult,
                rejectionCode);
        }

        private sealed class OpeningRecord
        {
            private OpeningRecord(
                StrongboxOpenCommandV1 command,
                StrongboxOpeningStageV1 stage,
                StrongboxGeneratedOutcomeV1 generatedOutcome,
                RewardCommitCommandV1 commitCommand,
                RewardClaimCommandV1 claimCommand,
                PlayerHoldingsCommandV1 consumeCommand,
                StrongboxOpeningResultV1 terminalFact,
                string rejectionCode)
            {
                Command = command;
                Stage = stage;
                GeneratedOutcome = generatedOutcome;
                CommitCommand = commitCommand;
                ClaimCommand = claimCommand;
                ConsumeCommand = consumeCommand;
                TerminalFact = terminalFact;
                RejectionCode = rejectionCode;
            }
            public StrongboxOpenCommandV1 Command { get; }
            public StrongboxOpeningStageV1 Stage { get; set; }
            public StrongboxGeneratedOutcomeV1 GeneratedOutcome { get; }
            public RewardCommitCommandV1 CommitCommand { get; }
            public RewardClaimCommandV1 ClaimCommand { get; }
            public PlayerHoldingsCommandV1 ConsumeCommand { get; }
            public StrongboxOpeningResultV1 TerminalFact { get; set; }
            public string RejectionCode { get; }

            public static OpeningRecord Prepared(
                StrongboxOpenCommandV1 command,
                StrongboxGeneratedOutcomeV1 outcome,
                RewardCommitCommandV1 commit,
                RewardClaimCommandV1 claim,
                PlayerHoldingsCommandV1 consume)
            {
                return new OpeningRecord(command, StrongboxOpeningStageV1.Prepared, outcome, commit, claim, consume, null, null);
            }
            public static OpeningRecord Rejected(
                StrongboxOpenCommandV1 command,
                StrongboxOpeningStageV1 stage,
                string rejectionCode)
            {
                return new OpeningRecord(command, stage, null, null, null, null, null, rejectionCode);
            }
            public StrongboxOpeningRecordSnapshotV1 ToSnapshot()
            {
                return new StrongboxOpeningRecordSnapshotV1(
                    Command,
                    Stage,
                    GeneratedOutcome,
                    CommitCommand,
                    ClaimCommand,
                    ConsumeCommand,
                    TerminalFact,
                    RejectionCode);
            }
            public static OpeningRecord FromSnapshot(StrongboxOpeningRecordSnapshotV1 snapshot)
            {
                return new OpeningRecord(
                    snapshot.Command,
                    snapshot.Stage,
                    snapshot.GeneratedOutcome,
                    snapshot.CommitCommand,
                    snapshot.ClaimCommand,
                    snapshot.ConsumeCommand,
                    snapshot.TerminalFact,
                    snapshot.RejectionCode);
            }
        }
    }
}
