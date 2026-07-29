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
    public interface IStrongboxRewardGenerator
    {
        RewardGenerationResultEnvelope Generate(RewardGenerationRequest request);
    }

    public sealed class SharedStrongboxRewardGenerator : IStrongboxRewardGenerator
    {
        private readonly RewardGenerationActions generator;
        public SharedStrongboxRewardGenerator(RewardGenerationActions generator)
        {
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }
        public RewardGenerationResultEnvelope Generate(RewardGenerationRequest request)
        {
            return generator.GenerateReward(request);
        }
    }

    public interface IStrongboxEquipmentPayloadResolver
    {
        bool TryResolve(
            StrongboxDefinition definition,
            StrongboxInstanceContext boxContext,
            RewardOperationRequest operation,
            RewardGrant equipmentGrant,
            out IReadOnlyList<EquipmentInstance> equipmentInstances,
            out string rejectionCode);
    }

    public sealed class StrongboxGrantPayloadResolution
    {
        private readonly ReadOnlyCollection<RewardGrantApplicationPayload> payloads;
        private StrongboxGrantPayloadResolution(
            bool succeeded,
            IEnumerable<RewardGrantApplicationPayload> payloads,
            string rejectionCode)
        {
            Succeeded = succeeded;
            this.payloads = new ReadOnlyCollection<RewardGrantApplicationPayload>(
                new List<RewardGrantApplicationPayload>(payloads ?? Array.Empty<RewardGrantApplicationPayload>()));
            RejectionCode = rejectionCode;
        }
        public bool Succeeded { get; }
        public IReadOnlyList<RewardGrantApplicationPayload> Payloads { get { return payloads; } }
        public string RejectionCode { get; }
        public static StrongboxGrantPayloadResolution Success(IEnumerable<RewardGrantApplicationPayload> payloads)
        {
            return new StrongboxGrantPayloadResolution(true, payloads, null);
        }
        public static StrongboxGrantPayloadResolution Rejected(string rejectionCode)
        {
            return new StrongboxGrantPayloadResolution(false, Array.Empty<RewardGrantApplicationPayload>(), rejectionCode ?? "payload-resolution-rejected");
        }
    }

    public interface IStrongboxGrantPayloadResolver
    {
        StrongboxGrantPayloadResolution Resolve(
            StrongboxDefinition definition,
            StrongboxInstanceContext boxContext,
            RewardOperationRequest operation,
            RewardResult rewardResult);
    }

    public sealed class DeterministicStrongboxGrantPayloadResolver : IStrongboxGrantPayloadResolver
    {
        private readonly IStrongboxEquipmentPayloadResolver equipmentResolver;
        public DeterministicStrongboxGrantPayloadResolver(IStrongboxEquipmentPayloadResolver equipmentResolver = null)
        {
            this.equipmentResolver = equipmentResolver;
        }

        public StrongboxGrantPayloadResolution Resolve(
            StrongboxDefinition definition,
            StrongboxInstanceContext boxContext,
            RewardOperationRequest operation,
            RewardResult rewardResult)
        {
            if (definition == null || boxContext == null || operation == null || rewardResult == null)
            {
                return StrongboxGrantPayloadResolution.Rejected("payload-input-null");
            }

            List<RewardGrantApplicationPayload> payloads = new List<RewardGrantApplicationPayload>();
            for (int grantIndex = 0; grantIndex < rewardResult.Grants.Count; grantIndex++)
            {
                RewardGrant grant = rewardResult.Grants[grantIndex];
                switch (grant.Kind)
                {
                    case RewardGrantKind.Money:
                    case RewardGrantKind.Scrap:
                    case RewardGrantKind.PremiumAmmo:
                    case RewardGrantKind.Miscellaneous:
                        payloads.Add(RewardGrantApplicationPayload.ForValue(grant));
                        break;
                    case RewardGrantKind.Strongbox:
                        List<StableId> boxIds = new List<StableId>();
                        for (long unit = 0L; unit < grant.Quantity; unit++)
                        {
                            boxIds.Add(Strongbox.DeriveId(
                                "boxchild",
                                operation.SourceOperationStableId.ToString(),
                                grant.GrantStableId.ToString(),
                                unit.ToString(CultureInfo.InvariantCulture)));
                        }
                        payloads.Add(RewardGrantApplicationPayload.ForStrongboxes(grant, boxIds));
                        break;
                    case RewardGrantKind.EquipmentReference:
                        if (equipmentResolver == null)
                        {
                            return StrongboxGrantPayloadResolution.Rejected("equipment-payload-resolver-required");
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
                            return StrongboxGrantPayloadResolution.Rejected(rejection ?? "equipment-payload-resolution-rejected");
                        }
                        payloads.Add(RewardGrantApplicationPayload.ForEquipment(grant, equipment));
                        break;
                    default:
                        return StrongboxGrantPayloadResolution.Rejected("grant-kind-unsupported");
                }
            }

            return StrongboxGrantPayloadResolution.Success(payloads);
        }
    }

    public enum StrongboxRegistrationStatus
    {
        Registered = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        InvalidContext = 4,
        UnknownDefinition = 5,
    }

    public sealed class StrongboxRegistrationResult
    {
        public StrongboxRegistrationResult(
            StrongboxRegistrationStatus status,
            StableId instanceStableId,
            string contextFingerprint,
            string rejectionCode)
        {
            Status = status;
            InstanceStableId = instanceStableId;
            ContextFingerprint = contextFingerprint;
            RejectionCode = rejectionCode;
        }
        public StrongboxRegistrationStatus Status { get; }
        public StableId InstanceStableId { get; }
        public string ContextFingerprint { get; }
        public string RejectionCode { get; }
    }

    public sealed class StrongboxOpenCommand : IEquatable<StrongboxOpenCommand>
    {
        private readonly string canonicalText;
        private StrongboxOpenCommand(
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
            Strongbox.AppendToken(builder, "opening_stable_id", OpeningStableId.ToString());
            Strongbox.AppendToken(builder, "run_stable_id", RunStableId.ToString());
            Strongbox.AppendToken(builder, "strongbox_instance_stable_id", StrongboxInstanceStableId.ToString());
            Strongbox.AppendToken(builder, "claimant_stable_id", ClaimantStableId.ToString());
            Strongbox.AppendToken(builder, "money_authority_stable_id", MoneyAuthorityStableId.ToString());
            Strongbox.AppendToken(builder, "scrap_authority_stable_id", ScrapAuthorityStableId.ToString());
            Strongbox.AppendToken(builder, "holdings_authority_stable_id", HoldingsAuthorityStableId.ToString());
            Strongbox.AppendToken(builder, "expected_opening_sequence", ExpectedOpeningSequence.HasValue
                ? ExpectedOpeningSequence.Value.ToString(CultureInfo.InvariantCulture) : "none");
            canonicalText = builder.ToString();
            Fingerprint = Strongbox.Fingerprint(canonicalText);
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

        public static StrongboxOpenCommand Create(
            StableId openingStableId,
            StableId runStableId,
            StableId strongboxInstanceStableId,
            StableId claimantStableId,
            StableId moneyAuthorityStableId,
            StableId scrapAuthorityStableId,
            StableId holdingsAuthorityStableId,
            long? expectedOpeningSequence = null)
        {
            return new StrongboxOpenCommand(
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
        public bool Equals(StrongboxOpenCommand other)
        {
            return !ReferenceEquals(other, null) && string.Equals(canonicalText, other.canonicalText, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) { return Equals(obj as StrongboxOpenCommand); }
        public override int GetHashCode() { return Strongbox.DeterministicHash(canonicalText); }
    }

    public enum StrongboxOpeningLiveStatus
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

    public enum StrongboxOpeningStage
    {
        Prepared = 1,
        RewardCommitted = 2,
        RewardClaimedPending = 3,
        RewardApplied = 4,
        Opened = 5,
        GeneratorRejected = 6,
        PayloadRejected = 7,
    }

    public sealed class StrongboxGeneratedOutcome
    {
        private readonly string canonicalText;
        public StrongboxGeneratedOutcome(
            StrongboxOpeningRequest openingRequest,
            RewardOperationRequest operation,
            RewardResult rewardResult,
            RewardTrace rewardTrace,
            RewardGenerationTrace generationTrace,
            string generationFingerprint,
            IEnumerable<RewardGrantApplicationPayload> payloads)
        {
            OpeningRequest = openingRequest ?? throw new ArgumentNullException(nameof(openingRequest));
            Operation = operation ?? throw new ArgumentNullException(nameof(operation));
            RewardResult = rewardResult ?? throw new ArgumentNullException(nameof(rewardResult));
            RewardTrace = rewardTrace ?? throw new ArgumentNullException(nameof(rewardTrace));
            GenerationTrace = generationTrace ?? throw new ArgumentNullException(nameof(generationTrace));
            if (!Strongbox.IsFingerprint(generationFingerprint))
            {
                throw new ArgumentException("Generation fingerprint must be canonical.", nameof(generationFingerprint));
            }
            GenerationFingerprint = generationFingerprint;
            List<RewardGrantApplicationPayload> copy = new List<RewardGrantApplicationPayload>(
                payloads ?? throw new ArgumentNullException(nameof(payloads)));
            copy.Sort();
            Payloads = new ReadOnlyCollection<RewardGrantApplicationPayload>(copy);
            StringBuilder builder = new StringBuilder();
            Strongbox.AppendToken(builder, "opening_request", OpeningRequest.ToCanonicalString());
            Strongbox.AppendToken(builder, "operation", Operation.ToCanonicalString());
            Strongbox.AppendToken(builder, "reward_result", RewardResult.ToCanonicalString());
            Strongbox.AppendToken(builder, "reward_trace", RewardTrace.ToCanonicalString());
            Strongbox.AppendToken(builder, "generation_trace", GenerationTrace.ToCanonicalString());
            Strongbox.AppendToken(builder, "generation_fingerprint", GenerationFingerprint);
            Strongbox.AppendToken(builder, "payload_count", copy.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < copy.Count; index++)
            {
                Strongbox.AppendToken(builder, "payload_" + index.ToString("D4", CultureInfo.InvariantCulture), copy[index].ToCanonicalString());
            }
            canonicalText = builder.ToString();
            Fingerprint = Strongbox.Fingerprint(canonicalText);
        }
        public StrongboxOpeningRequest OpeningRequest { get; }
        public RewardOperationRequest Operation { get; }
        public RewardResult RewardResult { get; }
        public RewardTrace RewardTrace { get; }
        public RewardGenerationTrace GenerationTrace { get; }
        public string GenerationFingerprint { get; }
        public IReadOnlyList<RewardGrantApplicationPayload> Payloads { get; }
        public string Fingerprint { get; }
        public string ToCanonicalString() { return canonicalText; }
    }

    public sealed class StrongboxOpeningResultLive
    {
        public StrongboxOpeningResultLive(
            StrongboxOpeningLiveStatus status,
            StableId openingStableId,
            long previousSequence,
            long currentSequence,
            string requestFingerprint,
            StrongboxGeneratedOutcome generatedOutcome,
            StrongboxOpeningResult terminalFact,
            StrongboxOpeningResult replayEnvelope,
            RewardApplicationResult rewardApplicationResult,
            PlayerHoldingsMutationResult consumeResult,
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
        public StrongboxOpeningLiveStatus Status { get; }
        public StableId OpeningStableId { get; }
        public long PreviousSequence { get; }
        public long CurrentSequence { get; }
        public string RequestFingerprint { get; }
        public StrongboxGeneratedOutcome GeneratedOutcome { get; }
        public StrongboxOpeningResult TerminalFact { get; }
        public StrongboxOpeningResult ReplayEnvelope { get; }
        public RewardApplicationResult RewardApplicationResult { get; }
        public PlayerHoldingsMutationResult ConsumeResult { get; }
        public string RejectionCode { get; }
        public bool Succeeded { get { return Status == StrongboxOpeningLiveStatus.Opened || Status == StrongboxOpeningLiveStatus.ExactDuplicateNoChange; } }
    }

    public sealed class StrongboxOpeningRecordSnapshot : IComparable<StrongboxOpeningRecordSnapshot>
    {
        private readonly string canonicalText;
        public StrongboxOpeningRecordSnapshot(
            StrongboxOpenCommand command,
            StrongboxOpeningStage stage,
            StrongboxGeneratedOutcome generatedOutcome,
            RewardCommitCommand commitCommand,
            RewardClaimCommand claimCommand,
            PlayerHoldingsCommand consumeCommand,
            StrongboxOpeningResult terminalFact,
            string rejectionCode)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
            if (!Enum.IsDefined(typeof(StrongboxOpeningStage), stage)) { throw new ArgumentOutOfRangeException(nameof(stage)); }
            Stage = stage;
            GeneratedOutcome = generatedOutcome;
            CommitCommand = commitCommand;
            ClaimCommand = claimCommand;
            ConsumeCommand = consumeCommand;
            TerminalFact = terminalFact;
            RejectionCode = rejectionCode;
            StringBuilder builder = new StringBuilder();
            Strongbox.AppendToken(builder, "command", Command.ToCanonicalString());
            Strongbox.AppendToken(builder, "stage", ((int)Stage).ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "generated_outcome", GeneratedOutcome == null ? "none" : GeneratedOutcome.ToCanonicalString());
            Strongbox.AppendToken(builder, "commit_command", CommitCommand == null ? "none" : CommitCommand.ToCanonicalString());
            Strongbox.AppendToken(builder, "claim_command", ClaimCommand == null ? "none" : ClaimCommand.ToCanonicalString());
            Strongbox.AppendToken(builder, "consume_command", ConsumeCommand == null ? "none" : ConsumeCommand.ToCanonicalString());
            Strongbox.AppendToken(builder, "terminal_fact", TerminalFact == null ? "none" : TerminalFact.ToCanonicalString());
            Strongbox.AppendToken(builder, "rejection_code", RejectionCode ?? "none");
            canonicalText = builder.ToString();
            Fingerprint = Strongbox.Fingerprint(canonicalText);
        }
        public StrongboxOpenCommand Command { get; }
        public StrongboxOpeningStage Stage { get; }
        public StrongboxGeneratedOutcome GeneratedOutcome { get; }
        public RewardCommitCommand CommitCommand { get; }
        public RewardClaimCommand ClaimCommand { get; }
        public PlayerHoldingsCommand ConsumeCommand { get; }
        public StrongboxOpeningResult TerminalFact { get; }
        public string RejectionCode { get; }
        public string Fingerprint { get; }
        public string ToCanonicalString() { return canonicalText; }
        public int CompareTo(StrongboxOpeningRecordSnapshot other)
        {
            return ReferenceEquals(other, null) ? 1 : Command.OpeningStableId.CompareTo(other.Command.OpeningStableId);
        }
    }

    public sealed class StrongboxOpeningSnapshot
    {
        public const int CurrentSchemaVersion = 1;
        private readonly ReadOnlyCollection<StrongboxInstanceContext> contexts;
        private readonly ReadOnlyCollection<StrongboxOpeningRecordSnapshot> openings;
        public StrongboxOpeningSnapshot(
            int schemaVersion,
            string definitionCatalogFingerprint,
            long sequence,
            IEnumerable<StrongboxInstanceContext> contexts,
            IEnumerable<StrongboxOpeningRecordSnapshot> openings,
            string fingerprint)
        {
            SchemaVersion = schemaVersion;
            if (!Strongbox.IsFingerprint(definitionCatalogFingerprint))
            {
                throw new ArgumentException("Catalog fingerprint must be canonical.", nameof(definitionCatalogFingerprint));
            }
            DefinitionCatalogFingerprint = definitionCatalogFingerprint;
            if (sequence < 0L) { throw new ArgumentOutOfRangeException(nameof(sequence)); }
            Sequence = sequence;
            List<StrongboxInstanceContext> contextCopy = new List<StrongboxInstanceContext>(contexts ?? throw new ArgumentNullException(nameof(contexts)));
            contextCopy.Sort();
            this.contexts = new ReadOnlyCollection<StrongboxInstanceContext>(contextCopy);
            List<StrongboxOpeningRecordSnapshot> openingCopy = new List<StrongboxOpeningRecordSnapshot>(openings ?? throw new ArgumentNullException(nameof(openings)));
            openingCopy.Sort();
            this.openings = new ReadOnlyCollection<StrongboxOpeningRecordSnapshot>(openingCopy);
            Fingerprint = fingerprint;
        }
        public int SchemaVersion { get; }
        public string DefinitionCatalogFingerprint { get; }
        public long Sequence { get; }
        public IReadOnlyList<StrongboxInstanceContext> Contexts { get { return contexts; } }
        public IReadOnlyList<StrongboxOpeningRecordSnapshot> Openings { get { return openings; } }
        public string Fingerprint { get; }

        public static StrongboxOpeningSnapshot CreateCanonical(
            string definitionCatalogFingerprint,
            long sequence,
            IEnumerable<StrongboxInstanceContext> contexts,
            IEnumerable<StrongboxOpeningRecordSnapshot> openings)
        {
            StrongboxOpeningSnapshot provisional = new StrongboxOpeningSnapshot(
                CurrentSchemaVersion, definitionCatalogFingerprint, sequence, contexts, openings, string.Empty);
            string fingerprint = ComputeFingerprint(provisional);
            return new StrongboxOpeningSnapshot(
                provisional.SchemaVersion,
                provisional.DefinitionCatalogFingerprint,
                provisional.Sequence,
                provisional.Contexts,
                provisional.Openings,
                fingerprint);
        }

        public static string ComputeFingerprint(StrongboxOpeningSnapshot snapshot)
        {
            if (snapshot == null) { throw new ArgumentNullException(nameof(snapshot)); }
            StringBuilder builder = new StringBuilder();
            Strongbox.AppendToken(builder, "schema_version", snapshot.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "definition_catalog_fingerprint", snapshot.DefinitionCatalogFingerprint);
            Strongbox.AppendToken(builder, "sequence", snapshot.Sequence.ToString(CultureInfo.InvariantCulture));
            Strongbox.AppendToken(builder, "context_count", snapshot.Contexts.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.Contexts.Count; index++)
            {
                Strongbox.AppendToken(builder, "context_" + index.ToString("D4", CultureInfo.InvariantCulture), snapshot.Contexts[index].ToCanonicalString());
            }
            Strongbox.AppendToken(builder, "opening_count", snapshot.Openings.Count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < snapshot.Openings.Count; index++)
            {
                Strongbox.AppendToken(builder, "opening_" + index.ToString("D4", CultureInfo.InvariantCulture), snapshot.Openings[index].ToCanonicalString());
            }
            return Strongbox.Fingerprint(builder.ToString());
        }
    }

    public enum StrongboxOpeningImportStatus
    {
        Imported = 1,
        InvalidSnapshot = 2,
        UnsupportedSchemaVersion = 3,
        CatalogMismatch = 4,
        FingerprintMismatch = 5,
    }

    public sealed class StrongboxOpeningImportResult
    {
        public StrongboxOpeningImportResult(StrongboxOpeningImportStatus status, string rejectionCode, long importedSequence)
        {
            Status = status;
            RejectionCode = rejectionCode;
            ImportedSequence = importedSequence;
        }
        public StrongboxOpeningImportStatus Status { get; }
        public string RejectionCode { get; }
        public long ImportedSequence { get; }
        public bool Succeeded { get { return Status == StrongboxOpeningImportStatus.Imported; } }
    }
}
