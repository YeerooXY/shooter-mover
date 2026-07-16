using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    public interface IStrongboxRewardGeneratorV1
    {
        RewardGenerationResultEnvelopeV1 Generate(RewardGenerationRequestV1 request);
    }

    public sealed class SharedStrongboxRewardGeneratorV1 : IStrongboxRewardGeneratorV1
    {
        private readonly RewardGenerationServiceV1 generator;
        public SharedStrongboxRewardGeneratorV1(RewardGenerationServiceV1 generator)
        {
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }
        public RewardGenerationResultEnvelopeV1 Generate(RewardGenerationRequestV1 request)
        {
            return generator.GenerateReward(request);
        }
    }

    public interface IStrongboxEquipmentPayloadResolverV1
    {
        bool TryResolve(
            StrongboxDefinitionV1 definition,
            StrongboxInstanceContextV1 boxContext,
            RewardOperationRequestV1 operation,
            RewardGrantV1 equipmentGrant,
            out IReadOnlyList<EquipmentInstance> equipmentInstances,
            out string rejectionCode);
    }

    public sealed class StrongboxGrantPayloadResolutionV1
    {
        private readonly ReadOnlyCollection<RewardGrantApplicationPayloadV1> payloads;
        private StrongboxGrantPayloadResolutionV1(
            bool succeeded,
            IEnumerable<RewardGrantApplicationPayloadV1> payloads,
            string rejectionCode)
        {
            Succeeded = succeeded;
            this.payloads = new ReadOnlyCollection<RewardGrantApplicationPayloadV1>(
                new List<RewardGrantApplicationPayloadV1>(payloads ?? Array.Empty<RewardGrantApplicationPayloadV1>()));
            RejectionCode = rejectionCode;
        }
        public bool Succeeded { get; }
        public IReadOnlyList<RewardGrantApplicationPayloadV1> Payloads { get { return payloads; } }
        public string RejectionCode { get; }
        public static StrongboxGrantPayloadResolutionV1 Success(IEnumerable<RewardGrantApplicationPayloadV1> payloads)
        {
            return new StrongboxGrantPayloadResolutionV1(true, payloads, null);
        }
        public static StrongboxGrantPayloadResolutionV1 Rejected(string rejectionCode)
        {
            return new StrongboxGrantPayloadResolutionV1(false, Array.Empty<RewardGrantApplicationPayloadV1>(), rejectionCode ?? "payload-resolution-rejected");
        }
    }

    public interface IStrongboxGrantPayloadResolverV1
    {
        StrongboxGrantPayloadResolutionV1 Resolve(
            StrongboxDefinitionV1 definition,
            StrongboxInstanceContextV1 boxContext,
            RewardOperationRequestV1 operation,
            RewardResultV1 rewardResult);
    }

    public sealed class DeterministicStrongboxGrantPayloadResolverV1 : IStrongboxGrantPayloadResolverV1
    {
        private readonly IStrongboxEquipmentPayloadResolverV1 equipmentResolver;
        public DeterministicStrongboxGrantPayloadResolverV1(IStrongboxEquipmentPayloadResolverV1 equipmentResolver = null)
        {
            this.equipmentResolver = equipmentResolver;
        }

        public StrongboxGrantPayloadResolutionV1 Resolve(
            StrongboxDefinitionV1 definition,
            StrongboxInstanceContextV1 boxContext,
            RewardOperationRequestV1 operation,
            RewardResultV1 rewardResult)
        {
            if (definition == null || boxContext == null || operation == null || rewardResult == null)
            {
                return StrongboxGrantPayloadResolutionV1.Rejected("payload-input-null");
            }

            List<RewardGrantApplicationPayloadV1> payloads = new List<RewardGrantApplicationPayloadV1>();
            for (int grantIndex = 0; grantIndex < rewardResult.Grants.Count; grantIndex++)
            {
                RewardGrantV1 grant = rewardResult.Grants[grantIndex];
                switch (grant.Kind)
                {
                    case RewardGrantKindV1.Money:
                    case RewardGrantKindV1.Scrap:
                    case RewardGrantKindV1.PremiumAmmo:
                    case RewardGrantKindV1.Miscellaneous:
                        payloads.Add(RewardGrantApplicationPayloadV1.ForValue(grant));
                        break;
                    case RewardGrantKindV1.Strongbox:
                        List<StableId> boxIds = new List<StableId>();
                        for (long unit = 0L; unit < grant.Quantity; unit++)
                        {
                            boxIds.Add(StrongboxCanonicalV1.DeriveId(
                                "boxchild",
                                operation.SourceOperationStableId.ToString(),
                                grant.GrantStableId.ToString(),
                                unit.ToString(CultureInfo.InvariantCulture)));
                        }
                        payloads.Add(RewardGrantApplicationPayloadV1.ForStrongboxes(grant, boxIds));
                        break;
                    case RewardGrantKindV1.EquipmentReference:
                        if (equipmentResolver == null)
                        {
                            return StrongboxGrantPayloadResolutionV1.Rejected("equipment-payload-resolver-required");
                        }
                        IReadOnlyList<EquipmentInstance> equipment;
                        string rejection;
                        if (!equipmentResolver.TryResolve(
                            definition,
                            boxContext,
                            operation,
                            grant,
                            out equipment,
                            out rejection))
                        {
                            return StrongboxGrantPayloadResolutionV1.Rejected(rejection ?? "equipment-payload-resolution-rejected");
                        }
                        payloads.Add(RewardGrantApplicationPayloadV1.ForEquipment(grant, equipment));
                        break;
                    default:
                        return StrongboxGrantPayloadResolutionV1.Rejected("grant-kind-unsupported");
                }
            }

            return StrongboxGrantPayloadResolutionV1.Success(payloads);
        }
    }

    public enum StrongboxRegistrationStatusV1
    {
        Registered = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        InvalidContext = 4,
        UnknownDefinition = 5,
    }

    public sealed class StrongboxRegistrationResultV1
    {
        public StrongboxRegistrationResultV1(
            StrongboxRegistrationStatusV1 status,
            StableId instanceStableId,
            string contextFingerprint,
            string rejectionCode)
        {
            Status = status;
            InstanceStableId = instanceStableId;
            ContextFingerprint = contextFingerprint;
            RejectionCode = rejectionCode;
        }
        public StrongboxRegistrationStatusV1 Status { get; }
        public StableId InstanceStableId { get; }
        public string ContextFingerprint { get; }
        public string RejectionCode { get; }
    }

    public sealed class StrongboxOpenCommandV1 : IEquatable<StrongboxOpenCommandV1>
    {
        private readonly string canonicalText;
        private StrongboxOpenCommandV1(
            StableId openingStableId,
            StableId runStableId,
            StableId strongboxInstanceStableId,
            StableId claimantStableId,
            StableId moneyAuthorityStableId,
            StableId scrapAuthorityStableId,
            StableId holdingsAuthorityStableId,
            long? expectedOpeningSequence)
        {
            OpeningStableId = openingStableId ?? throw new ArgumentNullException(nameof(openingStableId));
            RunStableId = runStableId ?? throw new ArgumentNullException(nameof(runStableId));
            StrongboxInstanceStableId = strongboxInstanceStableId ?? throw new ArgumentNullException(nameof(strongboxInstanceStableId));
            ClaimantStableId = claimantStableId ?? throw new ArgumentNullException(nameof(claimantStableId));
            MoneyAuthorityStableId = moneyAuthorityStableId ?? throw new ArgumentNullException(nameof(moneyAuthorityStableId));
            ScrapAuthorityStableId = scrapAuthorityStableId ?? throw new ArgumentNullException(nameof(scrapAuthorityStableId));
            HoldingsAuthorityStableId = holdingsAuthorityStableId ?? throw new ArgumentNullException(nameof(holdingsAuthorityStableId));
            if (expectedOpeningSequence.HasValue && expectedOpeningSequence.Value < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedOpeningSequence));
            }
            ExpectedOpeningSequence = expectedOpeningSequence;
            StringBuilder builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "opening_stable_id", OpeningStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "run_stable_id", RunStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "strongbox_instance_stable_id", StrongboxInstanceStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "claimant_stable_id", ClaimantStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "money_authority_stable_id", MoneyAuthorityStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "scrap_authority_stable_id", ScrapAuthorityStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "holdings_authority_stable_id", HoldingsAuthorityStableId.ToString());
            StrongboxCanonicalV1.AppendToken(builder, "expected_opening_sequence", ExpectedOpeningSequence.HasValue
                ? ExpectedOpeningSequence.Value.ToString(CultureInfo.InvariantCulture) : "none");
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }

        public StableId OpeningStableId { get; }
        public StableId RunStableId { get; }
        public StableId StrongboxInstanceStableId { get; }
        public StableId ClaimantStableId { get; }
        public StableId MoneyAuthorityStableId { get; }
        public StableId ScrapAuthorityStableId { get; }
        public StableId HoldingsAuthorityStableId { get; }
        public long? ExpectedOpeningSequence { get; }
        public string Fingerprint { get; }

        public static StrongboxOpenCommandV1 Create(
            StableId openingStableId,
            StableId runStableId,
            StableId strongboxInstanceStableId,
            StableId claimantStableId,
            StableId moneyAuthorityStableId,
            StableId scrapAuthorityStableId,
            StableId holdingsAuthorityStableId,
            long? expectedOpeningSequence = null)
        {
            return new StrongboxOpenCommandV1(
                openingStableId,
                runStableId,
                strongboxInstanceStableId,
                claimantStableId,
                moneyAuthorityStableId,
                scrapAuthorityStableId,
                holdingsAuthorityStableId,
                expectedOpeningSequence);
        }

        public string ToCanonicalString() { return canonicalText; }
        public bool Equals(StrongboxOpenCommandV1 other)
        {
            return !ReferenceEquals(other, null) && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as StrongboxOpenCommandV1); }
        public override int GetHashCode() { return StrongboxCanonicalV1.DeterministicHash(canonicalText); }
    }

    public enum StrongboxOpeningRuntimeStatusV1
    {
        Opened = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        InvalidRequest = 4,
        UnknownBoxInstance = 5,
        StrongboxNotOwned = 6,
        InvalidDefinition = 7,
        GeneratorRejected = 8,
        RewardRejected = 9,
        ClaimedPendingApplication = 10,
        ConsumePending = 11,
        ExpectedSequenceConflict = 12,
        SnapshotRejected = 13,
    }

    public enum StrongboxOpeningStageV1
    {
        Prepared = 1,
        RewardCommitted = 2,
        RewardClaimedPending = 3,
        RewardApplied = 4,
        Opened = 5,
        GeneratorRejected = 6,
        PayloadRejected = 7,
    }

    public sealed class StrongboxGeneratedOutcomeV1
    {
        private readonly string canonicalText;
        public StrongboxGeneratedOutcomeV1(
            StrongboxOpeningRequestV1 openingRequest,
            RewardOperationRequestV1 operation,
            RewardResultV1 rewardResult,
            RewardTraceV1 rewardTrace,
            RewardGenerationTraceV1 generationTrace,
            string generationFingerprint,
            IEnumerable<RewardGrantApplicationPayloadV1> payloads)
        {
            OpeningRequest = openingRequest ?? throw new ArgumentNullException(nameof(openingRequest));
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            RewardResult = rewardResult ?? throw new ArgumentNullException(nameof(rewardResult));
            RewardTrace = rewardTrace ?? throw new ArgumentNullException(nameof(rewardTrace));
            GenerationTrace = generationTrace ?? throw new ArgumentNullException(nameof(generationTrace));
            if (!StrongboxCanonicalV1.IsFingerprint(generationFingerprint))
            {
                throw new ArgumentException("Generation fingerprint must be canonical.", nameof(generationFingerprint));
            }
            GenerationFingerprint = generationFingerprint;
            List<RewardGrantApplicationPayloadV1> copy = new List<RewardGrantApplicationPayloadV1>(
                payloads ?? throw new ArgumentNullException(nameof(payloads)));
            copy.Sort();
            Payloads = new ReadOnlyCollection<RewardGrantApplicationPayloadV1>(copy);
            StringBuilder builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "opening_request", OpeningRequest.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "operation", Operation.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "reward_result", RewardResult.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "reward_trace", RewardTrace.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "generation_trace", GenerationTrace.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "generation_fingerprint", GenerationFingerprint);
            StrongboxCanonicalV1.AppendToken(builder, "payload_count", copy.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < copy.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(builder, "payload_" + index.ToString("D4", CultureInfo.InvariantCulture), copy[index].ToCanonicalString());
            }
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }
        public StrongboxOpeningRequestV1 OpeningRequest { get; }
        public RewardOperationRequestV1 Operation { get; }
        public RewardResultV1 RewardResult { get; }
        public RewardTraceV1 RewardTrace { get; }
        public RewardGenerationTraceV1 GenerationTrace { get; }
        public string GenerationFingerprint { get; }
        public IReadOnlyList<RewardGrantApplicationPayloadV1> Payloads { get; }
        public string Fingerprint { get; }
        public string ToCanonicalString() { return canonicalText; }
    }

    public sealed class StrongboxOpeningResultRuntimeV1
    {
        public StrongboxOpeningResultRuntimeV1(
            StrongboxOpeningRuntimeStatusV1 status,
            StableId openingStableId,
            long previousSequence,
            long currentSequence,
            string requestFingerprint,
            StrongboxGeneratedOutcomeV1 generatedOutcome,
            StrongboxOpeningResultV1 terminalFact,
            StrongboxOpeningResultV1 replayEnvelope,
            RewardApplicationResultV1 rewardApplicationResult,
            PlayerHoldingsMutationResultV1 consumeResult,
            string rejectionCode)
        {
            Status = status;
            OpeningStableId = openingStableId;
            PreviousSequence = previousSequence;
            CurrentSequence = currentSequence;
            RequestFingerprint = requestFingerprint;
            GeneratedOutcome = generatedOutcome;
            TerminalFact = terminalFact;
            ReplayEnvelope = replayEnvelope;
            RewardApplicationResult = rewardApplicationResult;
            ConsumeResult = consumeResult;
            RejectionCode = rejectionCode;
        }
        public StrongboxOpeningRuntimeStatusV1 Status { get; }
        public StableId OpeningStableId { get; }
        public long PreviousSequence { get; }
        public long CurrentSequence { get; }
        public string RequestFingerprint { get; }
        public StrongboxGeneratedOutcomeV1 GeneratedOutcome { get; }
        public StrongboxOpeningResultV1 TerminalFact { get; }
        public StrongboxOpeningResultV1 ReplayEnvelope { get; }
        public RewardApplicationResultV1 RewardApplicationResult { get; }
        public PlayerHoldingsMutationResultV1 ConsumeResult { get; }
        public string RejectionCode { get; }
        public bool Succeeded { get { return Status == StrongboxOpeningRuntimeStatusV1.Opened || Status == StrongboxOpeningRuntimeStatusV1.ExactDuplicateNoChange; } }
    }

    public sealed class StrongboxOpeningRecordSnapshotV1 : IComparable<StrongboxOpeningRecordSnapshotV1>
    {
        private readonly string canonicalText;
        public StrongboxOpeningRecordSnapshotV1(
            StrongboxOpenCommandV1 command,
            StrongboxOpeningStageV1 stage,
            StrongboxGeneratedOutcomeV1 generatedOutcome,
            RewardCommitCommandV1 commitCommand,
            RewardClaimCommandV1 claimCommand,
            PlayerHoldingsCommandV1 consumeCommand,
            StrongboxOpeningResultV1 terminalFact,
            string rejectionCode)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            if (!Enum.IsDefined(typeof(StrongboxOpeningStageV1), stage)) { throw new ArgumentOutOfRangeException(nameof(stage)); }
            Stage = stage;
            GeneratedOutcome = generatedOutcome;
            CommitCommand = commitCommand;
            ClaimCommand = claimCommand;
            ConsumeCommand = consumeCommand;
            TerminalFact = terminalFact;
            RejectionCode = rejectionCode;
            StringBuilder builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "command", Command.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "stage", ((int)Stage).ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "generated_outcome", GeneratedOutcome == null ? "none" : GeneratedOutcome.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "commit_command", CommitCommand == null ? "none" : CommitCommand.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "claim_command", ClaimCommand == null ? "none" : ClaimCommand.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "consume_command", ConsumeCommand == null ? "none" : ConsumeCommand.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "terminal_fact", TerminalFact == null ? "none" : TerminalFact.ToCanonicalString());
            StrongboxCanonicalV1.AppendToken(builder, "rejection_code", RejectionCode ?? "none");
            canonicalText = builder.ToString();
            Fingerprint = StrongboxCanonicalV1.Fingerprint(canonicalText);
        }
        public StrongboxOpenCommandV1 Command { get; }
        public StrongboxOpeningStageV1 Stage { get; }
        public StrongboxGeneratedOutcomeV1 GeneratedOutcome { get; }
        public RewardCommitCommandV1 CommitCommand { get; }
        public RewardClaimCommandV1 ClaimCommand { get; }
        public PlayerHoldingsCommandV1 ConsumeCommand { get; }
        public StrongboxOpeningResultV1 TerminalFact { get; }
        public string RejectionCode { get; }
        public string Fingerprint { get; }
        public string ToCanonicalString() { return canonicalText; }
        public int CompareTo(StrongboxOpeningRecordSnapshotV1 other)
        {
            return ReferenceEquals(other, null) ? 1 : Command.OpeningStableId.CompareTo(other.Command.OpeningStableId);
        }
    }

    public sealed class StrongboxOpeningSnapshotV1
    {
        public const int CurrentSchemaVersion = 1;
        private readonly ReadOnlyCollection<StrongboxInstanceContextV1> contexts;
        private readonly ReadOnlyCollection<StrongboxOpeningRecordSnapshotV1> openings;
        public StrongboxOpeningSnapshotV1(
            int schemaVersion,
            string definitionCatalogFingerprint,
            long sequence,
            IEnumerable<StrongboxInstanceContextV1> contexts,
            IEnumerable<StrongboxOpeningRecordSnapshotV1> openings,
            string fingerprint)
        {
            SchemaVersion = schemaVersion;
            if (!StrongboxCanonicalV1.IsFingerprint(definitionCatalogFingerprint))
            {
                throw new ArgumentException("Catalog fingerprint must be canonical.", nameof(definitionCatalogFingerprint));
            }
            DefinitionCatalogFingerprint = definitionCatalogFingerprint;
            if (sequence < 0L) { throw new ArgumentOutOfRangeException(nameof(sequence)); }
            Sequence = sequence;
            List<StrongboxInstanceContextV1> contextCopy = new List<StrongboxInstanceContextV1>(contexts ?? throw new ArgumentNullException(nameof(contexts)));
            contextCopy.Sort();
            this.contexts = new ReadOnlyCollection<StrongboxInstanceContextV1>(contextCopy);
            List<StrongboxOpeningRecordSnapshotV1> openingCopy = new List<StrongboxOpeningRecordSnapshotV1>(openings ?? throw new ArgumentNullException(nameof(openings)));
            openingCopy.Sort();
            this.openings = new ReadOnlyCollection<StrongboxOpeningRecordSnapshotV1>(openingCopy);
            Fingerprint = fingerprint;
        }
        public int SchemaVersion { get; }
        public string DefinitionCatalogFingerprint { get; }
        public long Sequence { get; }
        public IReadOnlyList<StrongboxInstanceContextV1> Contexts { get { return contexts; } }
        public IReadOnlyList<StrongboxOpeningRecordSnapshotV1> Openings { get { return openings; } }
        public string Fingerprint { get; }

        public static StrongboxOpeningSnapshotV1 CreateCanonical(
            string definitionCatalogFingerprint,
            long sequence,
            IEnumerable<StrongboxInstanceContextV1> contexts,
            IEnumerable<StrongboxOpeningRecordSnapshotV1> openings)
        {
            StrongboxOpeningSnapshotV1 provisional = new StrongboxOpeningSnapshotV1(
                CurrentSchemaVersion, definitionCatalogFingerprint, sequence, contexts, openings, string.Empty);
            string fingerprint = ComputeFingerprint(provisional);
            return new StrongboxOpeningSnapshotV1(
                provisional.SchemaVersion,
                provisional.DefinitionCatalogFingerprint,
                provisional.Sequence,
                provisional.Contexts,
                provisional.Openings,
                fingerprint);
        }

        public static string ComputeFingerprint(StrongboxOpeningSnapshotV1 snapshot)
        {
            if (snapshot == null) { throw new ArgumentNullException(nameof(snapshot)); }
            StringBuilder builder = new StringBuilder();
            StrongboxCanonicalV1.AppendToken(builder, "schema_version", snapshot.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "definition_catalog_fingerprint", snapshot.DefinitionCatalogFingerprint);
            StrongboxCanonicalV1.AppendToken(builder, "sequence", snapshot.Sequence.ToString(CultureInfo.InvariantCulture));
            StrongboxCanonicalV1.AppendToken(builder, "context_count", snapshot.Contexts.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.Contexts.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(builder, "context_" + index.ToString("D4", CultureInfo.InvariantCulture), snapshot.Contexts[index].ToCanonicalString());
            }
            StrongboxCanonicalV1.AppendToken(builder, "opening_count", snapshot.Openings.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.Openings.Count; index++)
            {
                StrongboxCanonicalV1.AppendToken(builder, "opening_" + index.ToString("D4", CultureInfo.InvariantCulture), snapshot.Openings[index].ToCanonicalString());
            }
            return StrongboxCanonicalV1.Fingerprint(builder.ToString());
        }
    }

    public enum StrongboxOpeningImportStatusV1
    {
        Imported = 1,
        InvalidSnapshot = 2,
        UnsupportedSchemaVersion = 3,
        CatalogMismatch = 4,
        FingerprintMismatch = 5,
    }

    public sealed class StrongboxOpeningImportResultV1
    {
        public StrongboxOpeningImportResultV1(StrongboxOpeningImportStatusV1 status, string rejectionCode, long importedSequence)
        {
            Status = status;
            RejectionCode = rejectionCode;
            ImportedSequence = importedSequence;
        }
        public StrongboxOpeningImportStatusV1 Status { get; }
        public string RejectionCode { get; }
        public long ImportedSequence { get; }
        public bool Succeeded { get { return Status == StrongboxOpeningImportStatusV1.Imported; } }
    }
}
