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
    public sealed class StrongboxOpeningActions
    {
        private readonly object sync = new object();
        private readonly StrongboxDefinitionCatalog catalog;
        private readonly IStrongboxRewardGenerator generator;
        private readonly IPlayerHoldingsState holdings;
        private readonly RewardApplicationActions rewardApplication;
        private readonly IStrongboxGrantPayloadResolver payloadResolver;
        private Dictionary<StableId, StrongboxInstanceContext> contexts;
        private Dictionary<StableId, OpeningRecord> openings;
        private Dictionary<StableId, StableId> openingByBox;
        private long sequence;

        public StrongboxOpeningActions(
            StrongboxDefinitionCatalog catalog,
            IStrongboxRewardGenerator generator,
            IPlayerHoldingsState holdings,
            RewardApplicationActions rewardApplication,
            IStrongboxGrantPayloadResolver payloadResolver)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
            this.holdings = holdings ?? throw new ArgumentNullException(nameof(holdings));
            this.rewardApplication = rewardApplication ?? throw new ArgumentNullException(nameof(rewardApplication));
            this.payloadResolver = payloadResolver ?? throw new ArgumentNullException(nameof(payloadResolver));
            contexts = new Dictionary<StableId, StrongboxInstanceContext>();
            openings = new Dictionary<StableId, OpeningRecord>();
            openingByBox = new Dictionary<StableId, StableId>();
        }

        public long Sequence
        {
            get { lock (sync) { return sequence; } }
        }

        public StrongboxRegistrationResult RegisterInstance(StrongboxInstanceContext context)
        {
            lock (sync)
            {
                if (context == null)
                {
                    return new StrongboxRegistrationResult(
                        StrongboxRegistrationStatus.InvalidContext,
                        null,
                        null,
                        "context-null");
                }

                StrongboxInstanceContext existing;
                if (contexts.TryGetValue(context.InstanceStableId, out existing))
                {
                    if (string.Equals(existing.Fingerprint, context.Fingerprint, StringComparison.Ordinal))
                    {
                        return new StrongboxRegistrationResult(
                            StrongboxRegistrationStatus.ExactDuplicateNoChange,
                            context.InstanceStableId,
                            existing.Fingerprint,
                            null);
                    }

                    return new StrongboxRegistrationResult(
                        StrongboxRegistrationStatus.ConflictingDuplicate,
                        context.InstanceStableId,
                        existing.Fingerprint,
                        "strongbox-instance-conflicting-duplicate");
                }

                StrongboxDefinition definition;
                if (!catalog.TryGet(context.TierStableId, out definition))
                {
                    return new StrongboxRegistrationResult(
                        StrongboxRegistrationStatus.UnknownDefinition,
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
                    return new StrongboxRegistrationResult(
                        StrongboxRegistrationStatus.InvalidContext,
                        context.InstanceStableId,
                        context.Fingerprint,
                        "strongbox-context-content-fingerprint-mismatch");
                }

                contexts.Add(context.InstanceStableId, context);
                return new StrongboxRegistrationResult(
                    StrongboxRegistrationStatus.Registered,
                    context.InstanceStableId,
                    context.Fingerprint,
                    null);
            }
        }

        public StrongboxOpeningResultLive Open(StrongboxOpenCommand command)
        {
            lock (sync)
            {
                long before = sequence;
                if (command == null)
                {
                    return RuntimeResult(
                        StrongboxOpeningLiveStatus.InvalidRequest,
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
                            StrongboxOpeningLiveStatus.ConflictingDuplicate,
                            StrongboxOpeningStatus.ConflictingDuplicate,
                            before,
                            existing,
                            "opening-identity-conflicting-duplicate");
                    }

                    if (existing.Stage == StrongboxOpeningStage.Opened)
                    {
                        StrongboxOpeningResult replay = StrongboxOpeningResult.Create(
                            existing.GeneratedOutcome.Operation.SourceOperationStableId,
                            StrongboxOpeningStatus.ExactDuplicateNoChange,
                            existing.GeneratedOutcome.OpeningRequest.Fingerprint,
                            existing.GeneratedOutcome.RewardResult,
                            existing.GeneratedOutcome.RewardTrace,
                            before,
                            before);
                        return RuntimeResult(
                            StrongboxOpeningLiveStatus.ExactDuplicateNoChange,
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

                    if (existing.Stage == StrongboxOpeningStage.GeneratorRejected)
                    {
                        return Rejected(
                            command,
                            StrongboxOpeningLiveStatus.GeneratorRejected,
                            StrongboxOpeningStatus.InvalidRequest,
                            before,
                            existing,
                            existing.RejectionCode);
                    }

                    if (existing.Stage == StrongboxOpeningStage.PayloadRejected)
                    {
                        return Rejected(
                            command,
                            StrongboxOpeningLiveStatus.RewardRejected,
                            StrongboxOpeningStatus.InvalidRequest,
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
                        StrongboxOpeningLiveStatus.ConflictingDuplicate,
                        StrongboxOpeningStatus.ConflictingDuplicate,
                        before,
                        null,
                        "strongbox-instance-opening-already-bound-" + boundOpeningStableId);
                }

                if (command.ExpectedOpeningSequence.HasValue
                    && command.ExpectedOpeningSequence.Value != sequence)
                {
                    return Rejected(
                        command,
                        StrongboxOpeningLiveStatus.ExpectedSequenceConflict,
                        StrongboxOpeningStatus.ExpectedSequenceConflict,
                        before,
                        null,
                        "opening-expected-sequence-conflict");
                }

                StrongboxInstanceContext context;
                if (!contexts.TryGetValue(command.StrongboxInstanceStableId, out context))
                {
                    return Rejected(
                        command,
                        StrongboxOpeningLiveStatus.UnknownBoxInstance,
                        StrongboxOpeningStatus.StrongboxNotOwned,
                        before,
                        null,
                        "strongbox-instance-unknown");
                }

                StrongboxDefinition definition;
                if (!catalog.TryGet(context.TierStableId, out definition))
                {
                    return Rejected(
                        command,
                        StrongboxOpeningLiveStatus.InvalidDefinition,
                        StrongboxOpeningStatus.InvalidRequest,
                        before,
                        null,
                        "strongbox-tier-unknown");
                }

                if (context.AlgorithmContentFingerprint != null
                    && !string.Equals(context.AlgorithmContentFingerprint, definition.Fingerprint, StringComparison.Ordinal))
                {
                    return Rejected(
                        command,
                        StrongboxOpeningLiveStatus.InvalidDefinition,
                        StrongboxOpeningStatus.InvalidRequest,
                        before,
                        null,
                        "strongbox-definition-fingerprint-mismatch");
                }

                UniqueHoldingSnapshot owned;
                if (!TryFindOwnedStrongbox(context.InstanceStableId, out owned))
                {
                    return Rejected(
                        command,
                        StrongboxOpeningLiveStatus.StrongboxNotOwned,
                        StrongboxOpeningStatus.StrongboxNotOwned,
                        before,
                        null,
                        "strongbox-not-owned");
                }

                if (owned.DefinitionStableId != context.TierStableId)
                {
                    return Rejected(
                        command,
                        StrongboxOpeningLiveStatus.InvalidDefinition,
                        StrongboxOpeningStatus.InvalidRequest,
                        before,
                        null,
                        "strongbox-owned-definition-mismatch");
                }

                OpeningRecord prepared = Prepare(command, context, definition);
                openings.Add(command.OpeningStableId, prepared);
                openingByBox.Add(command.StrongboxInstanceStableId, command.OpeningStableId);
                if (prepared.Stage == StrongboxOpeningStage.GeneratorRejected)
                {
                    return Rejected(
                        command,
                        StrongboxOpeningLiveStatus.GeneratorRejected,
                        StrongboxOpeningStatus.InvalidRequest,
                        before,
                        prepared,
                        prepared.RejectionCode);
                }
                if (prepared.Stage == StrongboxOpeningStage.PayloadRejected)
                {
                    return Rejected(
                        command,
                        StrongboxOpeningLiveStatus.RewardRejected,
                        StrongboxOpeningStatus.InvalidRequest,
                        before,
                        prepared,
                        prepared.RejectionCode);
                }

                return Continue(prepared, before);
            }
        }

        public StrongboxOpeningSnapshot ExportSnapshot()
        {
            lock (sync)
            {
                List<StrongboxInstanceContext> contextList = new List<StrongboxInstanceContext>(contexts.Values);
                List<StrongboxOpeningRecordSnapshot> openingList = new List<StrongboxOpeningRecordSnapshot>();
                foreach (OpeningRecord record in openings.Values)
                {
                    openingList.Add(record.ToSnapshot());
                }
                return StrongboxOpeningSnapshot.CreateCanonical(
                    catalog.Fingerprint,
                    sequence,
                    contextList,
                    openingList);
            }
        }

        public StrongboxOpeningImportResult ImportSnapshot(StrongboxOpeningSnapshot snapshot)
        {
            lock (sync)
            {
                if (snapshot == null)
                {
                    return new StrongboxOpeningImportResult(
                        StrongboxOpeningImportStatus.InvalidSnapshot,
                        "snapshot-null",
                        sequence);
                }
                if (snapshot.SchemaVersion != StrongboxOpeningSnapshot.CurrentSchemaVersion)
                {
                    return new StrongboxOpeningImportResult(
                        StrongboxOpeningImportStatus.UnsupportedSchemaVersion,
                        "snapshot-schema-unsupported",
                        sequence);
                }
                if (!string.Equals(snapshot.DefinitionCatalogFingerprint, catalog.Fingerprint, StringComparison.Ordinal))
                {
                    return new StrongboxOpeningImportResult(
                        StrongboxOpeningImportStatus.CatalogMismatch,
                        "snapshot-catalog-mismatch",
                        sequence);
                }
                if (!Strongbox.IsFingerprint(snapshot.Fingerprint)
                    || !string.Equals(snapshot.Fingerprint, StrongboxOpeningSnapshot.ComputeFingerprint(snapshot), StringComparison.Ordinal))
                {
                    return new StrongboxOpeningImportResult(
                        StrongboxOpeningImportStatus.FingerprintMismatch,
                        "snapshot-fingerprint-mismatch",
                        sequence);
                }

                Dictionary<StableId, StrongboxInstanceContext> importedContexts =
                    new Dictionary<StableId, StrongboxInstanceContext>();
                for (int index = 0; index < snapshot.Contexts.Count; index++)
                {
                    StrongboxInstanceContext context = snapshot.Contexts[index];
                    if (context == null || importedContexts.ContainsKey(context.InstanceStableId))
                    {
                        return InvalidImport("snapshot-context-duplicate-or-null");
                    }
                    StrongboxDefinition importedDefinition;
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
                    StrongboxOpeningRecordSnapshot opening = snapshot.Openings[index];
                    if (opening == null
                        || importedOpenings.ContainsKey(opening.Command.OpeningStableId)
                        || importedOpeningByBox.ContainsKey(opening.Command.StrongboxInstanceStableId)
                        || !importedContexts.ContainsKey(opening.Command.StrongboxInstanceStableId))
                    {
                        return InvalidImport("snapshot-opening-invalid");
                    }
                    if (opening.Stage == StrongboxOpeningStage.Opened)
                    {
                        if (opening.TerminalFact == null
                            || opening.GeneratedOutcome == null
                            || opening.TerminalFact.Status != StrongboxOpeningStatus.Opened)
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
                return new StrongboxOpeningImportResult(
                    StrongboxOpeningImportStatus.Imported,
                    null,
                    sequence);
            }
        }

        private StrongboxOpeningImportResult InvalidImport(string code)
        {
            return new StrongboxOpeningImportResult(
                StrongboxOpeningImportStatus.InvalidSnapshot,
                code,
                sequence);
        }

        private OpeningRecord Prepare(
            StrongboxOpenCommand command,
            StrongboxInstanceContext context,
            StrongboxDefinition definition)
        {
            StableId operationId = Strongbox.DeriveId(
                "boxop",
                command.OpeningStableId.ToString(),
                context.InstanceStableId.ToString(),
                context.Fingerprint);
            StableId commitmentId = Strongbox.DeriveId(
                "boxcommit",
                operationId.ToString(),
                context.Fingerprint);
            StableId effectiveProfileId = Strongbox.DeriveId(
                "boxprofile",
                definition.TierStableId.ToString(),
                definition.Fingerprint);
            StableId scrapGrantId = Strongbox.DeriveId(
                "boxscrap",
                definition.TierStableId.ToString(),
                definition.MandatoryScrapPolicy.Fingerprint);
            RewardProfile effectiveProfile;
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
                    StrongboxOpeningStage.GeneratorRejected,
                    "effective-profile-invalid-" + exception.GetType().Name.ToLowerInvariant());
            }

            RewardOperationRequest operation = RewardOperationRequest.Create(
                command.RunStableId,
                context.InstanceStableId,
                operationId,
                commitmentId,
                effectiveProfile.ProfileStableId,
                definition.Fingerprint);
            StableId openingTransactionId = Strongbox.DeriveId(
                "boxtx",
                operationId.ToString(),
                context.InstanceStableId.ToString());
            StrongboxOpeningRequest openingRequest = StrongboxOpeningRequest.Create(
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
                return OpeningRecord.Rejected(command, StrongboxOpeningStage.GeneratorRejected, "strongbox-bias-overflow");
            }
            RewardGenerationRequest generationRequest = RewardGenerationRequest.Create(
                operation,
                effectiveProfile,
                context.ProgressionContext,
                context.RootSeed,
                context.AlgorithmVersion,
                new[]
                {
                    RewardGenerationScalingValue.Create(definition.TierScalingInputStableId, definition.GenerationBias),
                    RewardGenerationScalingValue.Create(definition.ExceptionalScalingInputStableId, exceptionalValue),
                });

            RewardGenerationResultEnvelope generated;
            try
            {
                generated = generator.Generate(generationRequest);
            }
            catch (Exception exception)
            {
                return OpeningRecord.Rejected(
                    command,
                    StrongboxOpeningStage.GeneratorRejected,
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
                    StrongboxOpeningStage.GeneratorRejected,
                    generated == null ? "generator-result-null" : "generator-rejected-" + generated.FailureReason);
            }
            if (!definition.RewardCountPolicy.Accepts(generated.Result.Grants.Count))
            {
                return OpeningRecord.Rejected(command, StrongboxOpeningStage.GeneratorRejected, "generated-reward-count-outside-policy");
            }
            if (!ContainsPositiveMandatoryScrap(generated.Result, definition.MandatoryScrapPolicy.CurrencyStableId))
            {
                return OpeningRecord.Rejected(command, StrongboxOpeningStage.GeneratorRejected, "generated-mandatory-scrap-missing");
            }

            StrongboxGrantPayloadResolution resolved;
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
                    StrongboxOpeningStage.PayloadRejected,
                    "payload-resolution-exception-" + exception.GetType().Name.ToLowerInvariant());
            }
            if (resolved == null || !resolved.Succeeded)
            {
                return OpeningRecord.Rejected(
                    command,
                    StrongboxOpeningStage.PayloadRejected,
                    resolved == null ? "payload-resolution-null" : resolved.RejectionCode);
            }

            StrongboxGeneratedOutcome outcome = new StrongboxGeneratedOutcome(
                openingRequest,
                operation,
                generated.Result,
                generated.RewardTrace,
                generated.GenerationTrace,
                generated.GenerationTrace.Fingerprint,
                resolved.Payloads);
            RewardCommitCommand commit = RewardCommitCommand.Create(
                operation,
                generated.Result,
                generated.GenerationTrace.Fingerprint,
                resolved.Payloads);
            StableId claimId = Strongbox.DeriveId(
                "boxclaim",
                operationId.ToString(),
                commitmentId.ToString(),
                command.ClaimantStableId.ToString());
            RewardClaimCommand claim = RewardClaimCommand.Create(
                claimId,
                commitmentId,
                command.ClaimantStableId,
                command.MoneyAuthorityStableId,
                command.ScrapAuthorityStableId,
                command.HoldingsAuthorityStableId);
            StableId consumeTransaction = Strongbox.DeriveId(
                "boxconsume",
                operationId.ToString(),
                context.InstanceStableId.ToString());
            StableId consumeOperation = Strongbox.DeriveId(
                "boxconsumeop",
                operationId.ToString(),
                context.InstanceStableId.ToString());
            StableId consumeGrant = Strongbox.DeriveId(
                "boxconsumegrant",
                operationId.ToString(),
                context.InstanceStableId.ToString());
            PlayerHoldingsCommand consume = PlayerHoldingsCommand.RemoveStrongbox(
                consumeTransaction,
                consumeOperation,
                command.HoldingsAuthorityStableId,
                definition.TierStableId,
                context.InstanceStableId,
                HoldingProvenance.Create(consumeGrant, operationId));
            return OpeningRecord.Prepared(command, outcome, commit, claim, consume);
        }

        private StrongboxOpeningResultLive Continue(OpeningRecord record, long before)
        {
            RewardApplicationResult rapResult = null;
            if (record.Stage == StrongboxOpeningStage.Prepared)
            {
                rapResult = rewardApplication.Commit(record.CommitCommand);
                if (rapResult.Status != RewardApplicationResultStatus.Generated
                    && rapResult.Status != RewardApplicationResultStatus.ExactDuplicateNoChange)
                {
                    return RuntimeResult(
                        StrongboxOpeningLiveStatus.RewardRejected,
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
                if (rapResult.CommitmentState == RewardCommitmentState.Applied)
                {
                    record.Stage = StrongboxOpeningStage.RewardApplied;
                }
                else if (rapResult.CommitmentState == RewardCommitmentState.Claimed)
                {
                    record.Stage = StrongboxOpeningStage.RewardClaimedPending;
                }
                else
                {
                    record.Stage = StrongboxOpeningStage.RewardCommitted;
                }
            }

            if (record.Stage == StrongboxOpeningStage.RewardCommitted)
            {
                RewardCommitmentSnapshot currentCommitment;
                if (rewardApplication.TryGetCommitment(
                    record.CommitCommand.CommitmentStableId,
                    out currentCommitment))
                {
                    if (currentCommitment.State == RewardCommitmentState.Applied)
                    {
                        record.Stage = StrongboxOpeningStage.RewardApplied;
                    }
                    else if (currentCommitment.State == RewardCommitmentState.Claimed)
                    {
                        record.Stage = StrongboxOpeningStage.RewardClaimedPending;
                    }
                }
            }

            if (record.Stage == StrongboxOpeningStage.RewardCommitted)
            {
                rapResult = rewardApplication.Claim(record.ClaimCommand);
                if (rapResult.Status == RewardApplicationResultStatus.Applied
                    || rapResult.Status == RewardApplicationResultStatus.AlreadyAppliedNoChange)
                {
                    record.Stage = StrongboxOpeningStage.RewardApplied;
                }
                else if (rapResult.Status == RewardApplicationResultStatus.ClaimedPendingApplication)
                {
                    record.Stage = StrongboxOpeningStage.RewardClaimedPending;
                    return RuntimeResult(
                        StrongboxOpeningLiveStatus.ClaimedPendingApplication,
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

            if (record.Stage == StrongboxOpeningStage.RewardClaimedPending)
            {
                rapResult = rewardApplication.Retry(
                    RewardRetryClaimCommand.Create(
                        record.CommitCommand.CommitmentStableId,
                        record.ClaimCommand.ClaimStableId));
                if (rapResult.Status == RewardApplicationResultStatus.Applied
                    || rapResult.Status == RewardApplicationResultStatus.AlreadyAppliedNoChange)
                {
                    record.Stage = StrongboxOpeningStage.RewardApplied;
                }
                else
                {
                    return RuntimeResult(
                        rapResult.Status == RewardApplicationResultStatus.ClaimedPendingApplication
                            ? StrongboxOpeningLiveStatus.ClaimedPendingApplication
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

            if (record.Stage == StrongboxOpeningStage.RewardApplied)
            {
                PlayerHoldingsMutationResult consumeResult;
                try
                {
                    consumeResult = holdings.Apply(record.ConsumeCommand);
                }
                catch (Exception exception)
                {
                    return RuntimeResult(
                        StrongboxOpeningLiveStatus.ConsumePending,
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
                    && (consumeResult.Status == PlayerHoldingsMutationStatus.Applied
                        || (consumeResult.Status == PlayerHoldingsMutationStatus.ExactDuplicateNoChange
                            && consumeResult.OriginalStatus == PlayerHoldingsMutationStatus.Applied));
                if (!consumed)
                {
                    return RuntimeResult(
                        StrongboxOpeningLiveStatus.ConsumePending,
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
                record.Stage = StrongboxOpeningStage.Opened;
                record.TerminalFact = StrongboxOpeningResult.Create(
                    record.GeneratedOutcome.Operation.SourceOperationStableId,
                    StrongboxOpeningStatus.Opened,
                    record.GeneratedOutcome.OpeningRequest.Fingerprint,
                    record.GeneratedOutcome.RewardResult,
                    record.GeneratedOutcome.RewardTrace,
                    previous,
                    sequence);
                return RuntimeResult(
                    StrongboxOpeningLiveStatus.Opened,
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
                StrongboxOpeningLiveStatus.InvalidRequest,
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

        private bool TryFindOwnedStrongbox(StableId instanceStableId, out UniqueHoldingSnapshot holding)
        {
            PlayerHoldingsSnapshot snapshot;
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
                UniqueHoldingSnapshot candidate = snapshot.UniqueHoldings[index];
                if (candidate.InstanceStableId == instanceStableId
                    && candidate.RewardKind == RewardGrantKind.Strongbox)
                {
                    holding = candidate;
                    return true;
                }
            }
            holding = null;
            return false;
        }

        private static bool ContainsPositiveMandatoryScrap(RewardResult result, StableId currencyId)
        {
            for (int index = 0; index < result.Grants.Count; index++)
            {
                RewardGrant grant = result.Grants[index];
                if (grant.Kind == RewardGrantKind.Scrap
                    && grant.ContentStableId == currencyId
                    && grant.Quantity > 0L)
                {
                    return true;
                }
            }
            return false;
        }

        private static StrongboxOpeningLiveStatus MapRapFailure(RewardApplicationResultStatus status)
        {
            if (status == RewardApplicationResultStatus.ExpectedSequenceConflict)
            {
                return StrongboxOpeningLiveStatus.ExpectedSequenceConflict;
            }
            return StrongboxOpeningLiveStatus.RewardRejected;
        }

        private StrongboxOpeningResultLive Rejected(
            StrongboxOpenCommand command,
            StrongboxOpeningLiveStatus runtimeStatus,
            StrongboxOpeningStatus contractStatus,
            long before,
            OpeningRecord record,
            string rejectionCode)
        {
            StableId operationId = record != null && record.GeneratedOutcome != null
                ? record.GeneratedOutcome.Operation.SourceOperationStableId
                : Strongbox.DeriveId(
                    "boxop",
                    command.OpeningStableId.ToString(),
                    command.StrongboxInstanceStableId.ToString(),
                    command.Fingerprint);
            string requestFingerprint = record != null && record.GeneratedOutcome != null
                ? record.GeneratedOutcome.OpeningRequest.Fingerprint
                : command.Fingerprint;
            StrongboxOpeningResult envelope = StrongboxOpeningResult.Create(
                operationId,
                contractStatus,
                requestFingerprint,
                contractStatus == StrongboxOpeningStatus.ExactDuplicateNoChange && record != null
                    ? record.GeneratedOutcome.RewardResult : null,
                contractStatus == StrongboxOpeningStatus.ExactDuplicateNoChange && record != null
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

        private static StrongboxOpeningResultLive RuntimeResult(
            StrongboxOpeningLiveStatus status,
            StableId openingStableId,
            long previousSequence,
            long currentSequence,
            string requestFingerprint,
            StrongboxGeneratedOutcome outcome,
            StrongboxOpeningResult terminalFact,
            StrongboxOpeningResult replayEnvelope,
            RewardApplicationResult rapResult,
            PlayerHoldingsMutationResult consumeResult,
            string rejectionCode)
        {
            return new StrongboxOpeningResultLive(
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
                StrongboxOpenCommand command,
                StrongboxOpeningStage stage,
                StrongboxGeneratedOutcome generatedOutcome,
                RewardCommitCommand commitCommand,
                RewardClaimCommand claimCommand,
                PlayerHoldingsCommand consumeCommand,
                StrongboxOpeningResult terminalFact,
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
            public StrongboxOpenCommand Command { get; }
            public StrongboxOpeningStage Stage { get; set; }
            public StrongboxGeneratedOutcome GeneratedOutcome { get; }
            public RewardCommitCommand CommitCommand { get; }
            public RewardClaimCommand ClaimCommand { get; }
            public PlayerHoldingsCommand ConsumeCommand { get; }
            public StrongboxOpeningResult TerminalFact { get; set; }
            public string RejectionCode { get; }

            public static OpeningRecord Prepared(
                StrongboxOpenCommand command,
                StrongboxGeneratedOutcome outcome,
                RewardCommitCommand commit,
                RewardClaimCommand claim,
                PlayerHoldingsCommand consume)
            {
                return new OpeningRecord(command, StrongboxOpeningStage.Prepared, outcome, commit, claim, consume, null, null);
            }
            public static OpeningRecord Rejected(
                StrongboxOpenCommand command,
                StrongboxOpeningStage stage,
                string rejectionCode)
            {
                return new OpeningRecord(command, stage, null, null, null, null, null, rejectionCode);
            }
            public StrongboxOpeningRecordSnapshot ToSnapshot()
            {
                return new StrongboxOpeningRecordSnapshot(
                    Command,
                    Stage,
                    GeneratedOutcome,
                    CommitCommand,
                    ClaimCommand,
                    ConsumeCommand,
                    TerminalFact,
                    RejectionCode);
            }
            public static OpeningRecord FromSnapshot(StrongboxOpeningRecordSnapshot snapshot)
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
