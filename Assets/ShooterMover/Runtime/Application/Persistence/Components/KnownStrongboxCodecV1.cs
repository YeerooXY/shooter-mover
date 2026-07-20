using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Application.Inventory.LoadoutScreen;
using ShooterMover.Application.Rewards.Generation;
using ShooterMover.Application.Rewards.Strongboxes;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Contracts.Progression.Experience;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Progression.Context;
using ShooterMover.Domain.Progression.Experience;
using ShooterMover.Domain.Progression.Skills;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Generation;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Persistence.Components
{
    public sealed class StrongboxOpeningComponentCodecV1 :
        ExplicitSaveComponentCodecV1<StrongboxOpeningSnapshotV1>
    {
        public StrongboxOpeningComponentCodecV1()
            : base("strongbox-opening-explicit-v1")
        {
        }

        public override SaveComponentValidationResultV1 Validate(
            StrongboxOpeningSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                return SaveComponentValidationResultV1.Reject(
                    "strongbox-opening-snapshot-null");
            }
            if (snapshot.SchemaVersion
                != StrongboxOpeningSnapshotV1.CurrentSchemaVersion)
            {
                return SaveComponentValidationResultV1.Reject(
                    "strongbox-opening-schema-unsupported");
            }
            try
            {
                string expected = StrongboxOpeningSnapshotV1.ComputeFingerprint(
                    snapshot);
                return FingerprintResult(
                    string.Equals(
                        expected,
                        snapshot.Fingerprint,
                        StringComparison.Ordinal),
                    "strongbox-opening-fingerprint-mismatch");
            }
            catch
            {
                return SaveComponentValidationResultV1.Reject(
                    "strongbox-opening-snapshot-invalid");
            }
        }

        protected override CanonicalNodeV1 EncodeNode(
            StrongboxOpeningSnapshotV1 snapshot)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("schema_version", CanonicalValueV1.Int32(snapshot.SchemaVersion)),
                CanonicalValueV1.Field("definition_catalog_fingerprint", CanonicalValueV1.RequiredString(snapshot.DefinitionCatalogFingerprint)),
                CanonicalValueV1.Field("sequence", CanonicalValueV1.Int64(snapshot.Sequence)),
                CanonicalValueV1.Field("contexts", ExplicitCodecValuesV1.EncodeList(snapshot.Contexts, EncodeContext)),
                CanonicalValueV1.Field("openings", ExplicitCodecValuesV1.EncodeList(snapshot.Openings, EncodeOpeningRecord)));
        }

        protected override StrongboxOpeningSnapshotV1 DecodeNode(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "schema_version",
                "definition_catalog_fingerprint",
                "sequence",
                "contexts",
                "openings");
            int schema = CanonicalValueV1.ReadInt32(reader.Next("schema_version"));
            if (schema != StrongboxOpeningSnapshotV1.CurrentSchemaVersion)
            {
                throw new CanonicalPayloadExceptionV1(
                    "strongbox-opening-schema-unsupported");
            }
            return StrongboxOpeningSnapshotV1.CreateCanonical(
                CanonicalValueV1.ReadRequiredString(reader.Next("definition_catalog_fingerprint")),
                CanonicalValueV1.ReadInt64(reader.Next("sequence")),
                ExplicitCodecValuesV1.DecodeList(reader.Next("contexts"), DecodeContext),
                ExplicitCodecValuesV1.DecodeList(reader.Next("openings"), DecodeOpeningRecord));
        }

        private static CanonicalNodeV1 EncodeContext(
            StrongboxInstanceContextV1 context)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("instance_id", ExplicitCodecValuesV1.RequiredIdNode(context.InstanceStableId)),
                CanonicalValueV1.Field("tier_id", ExplicitCodecValuesV1.RequiredIdNode(context.TierStableId)),
                CanonicalValueV1.Field("root_seed", CanonicalValueV1.UInt64(context.RootSeed)),
                CanonicalValueV1.Field("algorithm_version", CanonicalValueV1.Int32(context.AlgorithmVersion)),
                CanonicalValueV1.Field("progression_context", PlayerExperienceComponentCodecV1.EncodeProgressionContext(context.ProgressionContext)),
                CanonicalValueV1.Field("source_context_id", ExplicitCodecValuesV1.RequiredIdNode(context.SourceContextStableId)),
                CanonicalValueV1.Field("collection_provenance_id", ExplicitCodecValuesV1.RequiredIdNode(context.CollectionProvenanceStableId)),
                CanonicalValueV1.Field("algorithm_content_fingerprint", CanonicalValueV1.String(context.AlgorithmContentFingerprint)));
        }

        private static StrongboxInstanceContextV1 DecodeContext(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "instance_id",
                "tier_id",
                "root_seed",
                "algorithm_version",
                "progression_context",
                "source_context_id",
                "collection_provenance_id",
                "algorithm_content_fingerprint");
            return StrongboxInstanceContextV1.Create(
                ExplicitCodecValuesV1.RequiredId(reader.Next("instance_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("tier_id")),
                CanonicalValueV1.ReadUInt64(reader.Next("root_seed")),
                CanonicalValueV1.ReadInt32(reader.Next("algorithm_version")),
                PlayerExperienceComponentCodecV1.DecodeProgressionContext(reader.Next("progression_context")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("source_context_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("collection_provenance_id")),
                CanonicalValueV1.ReadOptionalString(reader.Next("algorithm_content_fingerprint")));
        }

        private static CanonicalNodeV1 EncodeOpeningRecord(
            StrongboxOpeningRecordSnapshotV1 record)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("command", EncodeOpenCommand(record.Command)),
                CanonicalValueV1.Field("stage", ExplicitCodecValuesV1.EnumNode(record.Stage)),
                CanonicalValueV1.Field("generated_outcome", ExplicitCodecValuesV1.OptionalObject(record.GeneratedOutcome, EncodeGeneratedOutcome)),
                CanonicalValueV1.Field("commit_command", ExplicitCodecValuesV1.OptionalObject(record.CommitCommand, EncodeCommitCommand)),
                CanonicalValueV1.Field("claim_command", ExplicitCodecValuesV1.OptionalObject(record.ClaimCommand, EncodeClaimCommand)),
                CanonicalValueV1.Field("consume_command", ExplicitCodecValuesV1.OptionalObject(record.ConsumeCommand, PlayerHoldingsComponentCodecV1.EncodeHoldingsCommand)),
                CanonicalValueV1.Field("terminal_fact", ExplicitCodecValuesV1.OptionalObject(record.TerminalFact, EncodeOpeningResult)),
                CanonicalValueV1.Field("rejection_code", CanonicalValueV1.String(record.RejectionCode)));
        }

        private static StrongboxOpeningRecordSnapshotV1 DecodeOpeningRecord(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "command",
                "stage",
                "generated_outcome",
                "commit_command",
                "claim_command",
                "consume_command",
                "terminal_fact",
                "rejection_code");
            return new StrongboxOpeningRecordSnapshotV1(
                DecodeOpenCommand(reader.Next("command")),
                ExplicitCodecValuesV1.EnumValue<StrongboxOpeningStageV1>(reader.Next("stage")),
                ExplicitCodecValuesV1.OptionalObjectValue(reader.Next("generated_outcome"), DecodeGeneratedOutcome),
                ExplicitCodecValuesV1.OptionalObjectValue(reader.Next("commit_command"), DecodeCommitCommand),
                ExplicitCodecValuesV1.OptionalObjectValue(reader.Next("claim_command"), DecodeClaimCommand),
                ExplicitCodecValuesV1.OptionalObjectValue(reader.Next("consume_command"), PlayerHoldingsComponentCodecV1.DecodeHoldingsCommand),
                ExplicitCodecValuesV1.OptionalObjectValue(reader.Next("terminal_fact"), DecodeOpeningResult),
                CanonicalValueV1.ReadOptionalString(reader.Next("rejection_code")));
        }

        private static CanonicalNodeV1 EncodeOpenCommand(
            StrongboxOpenCommandV1 command)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("opening_id", ExplicitCodecValuesV1.RequiredIdNode(command.OpeningStableId)),
                CanonicalValueV1.Field("run_id", ExplicitCodecValuesV1.RequiredIdNode(command.RunStableId)),
                CanonicalValueV1.Field("box_instance_id", ExplicitCodecValuesV1.RequiredIdNode(command.StrongboxInstanceStableId)),
                CanonicalValueV1.Field("claimant_id", ExplicitCodecValuesV1.RequiredIdNode(command.ClaimantStableId)),
                CanonicalValueV1.Field("money_authority_id", ExplicitCodecValuesV1.RequiredIdNode(command.MoneyAuthorityStableId)),
                CanonicalValueV1.Field("scrap_authority_id", ExplicitCodecValuesV1.RequiredIdNode(command.ScrapAuthorityStableId)),
                CanonicalValueV1.Field("holdings_authority_id", ExplicitCodecValuesV1.RequiredIdNode(command.HoldingsAuthorityStableId)),
                CanonicalValueV1.Field("expected_opening_sequence", CanonicalValueV1.OptionalInt64(command.ExpectedOpeningSequence)));
        }

        private static StrongboxOpenCommandV1 DecodeOpenCommand(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "opening_id",
                "run_id",
                "box_instance_id",
                "claimant_id",
                "money_authority_id",
                "scrap_authority_id",
                "holdings_authority_id",
                "expected_opening_sequence");
            return StrongboxOpenCommandV1.Create(
                ExplicitCodecValuesV1.RequiredId(reader.Next("opening_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("run_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("box_instance_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("claimant_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("money_authority_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("scrap_authority_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("holdings_authority_id")),
                CanonicalValueV1.ReadOptionalInt64(reader.Next("expected_opening_sequence")));
        }

        private static CanonicalNodeV1 EncodeGeneratedOutcome(
            StrongboxGeneratedOutcomeV1 outcome)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("opening_request", EncodeOpeningRequest(outcome.OpeningRequest)),
                CanonicalValueV1.Field("operation", EncodeRewardOperation(outcome.Operation)),
                CanonicalValueV1.Field("reward_result", EncodeRewardResult(outcome.RewardResult)),
                CanonicalValueV1.Field("reward_trace", EncodeRewardTrace(outcome.RewardTrace)),
                CanonicalValueV1.Field("generation_trace", EncodeGenerationTrace(outcome.GenerationTrace)),
                CanonicalValueV1.Field("generation_fingerprint", CanonicalValueV1.RequiredString(outcome.GenerationFingerprint)),
                CanonicalValueV1.Field("payloads", ExplicitCodecValuesV1.EncodeList(outcome.Payloads, EncodeGrantPayload)));
        }

        private static StrongboxGeneratedOutcomeV1 DecodeGeneratedOutcome(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "opening_request",
                "operation",
                "reward_result",
                "reward_trace",
                "generation_trace",
                "generation_fingerprint",
                "payloads");
            return new StrongboxGeneratedOutcomeV1(
                DecodeOpeningRequest(reader.Next("opening_request")),
                DecodeRewardOperation(reader.Next("operation")),
                DecodeRewardResult(reader.Next("reward_result")),
                DecodeRewardTrace(reader.Next("reward_trace")),
                DecodeGenerationTrace(reader.Next("generation_trace")),
                CanonicalValueV1.ReadRequiredString(reader.Next("generation_fingerprint")),
                ExplicitCodecValuesV1.DecodeList(reader.Next("payloads"), DecodeGrantPayload));
        }

        private static CanonicalNodeV1 EncodeOpeningRequest(
            StrongboxOpeningRequestV1 request)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("run_id", ExplicitCodecValuesV1.RequiredIdNode(request.RunStableId)),
                CanonicalValueV1.Field("opening_operation_id", ExplicitCodecValuesV1.RequiredIdNode(request.OpeningOperationStableId)),
                CanonicalValueV1.Field("transaction_id", ExplicitCodecValuesV1.RequiredIdNode(request.TransactionStableId)),
                CanonicalValueV1.Field("box_instance_id", ExplicitCodecValuesV1.RequiredIdNode(request.StrongboxInstanceStableId)),
                CanonicalValueV1.Field("box_definition_id", ExplicitCodecValuesV1.RequiredIdNode(request.StrongboxDefinitionStableId)),
                CanonicalValueV1.Field("commitment_id", ExplicitCodecValuesV1.RequiredIdNode(request.CommitmentStableId)),
                CanonicalValueV1.Field("reward_profile_id", ExplicitCodecValuesV1.RequiredIdNode(request.RewardProfileStableId)),
                CanonicalValueV1.Field("content_fingerprint", CanonicalValueV1.RequiredString(request.ContentFingerprint)),
                CanonicalValueV1.Field("expected_sequence", CanonicalValueV1.OptionalInt64(request.ExpectedSequence)));
        }

        private static StrongboxOpeningRequestV1 DecodeOpeningRequest(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "run_id",
                "opening_operation_id",
                "transaction_id",
                "box_instance_id",
                "box_definition_id",
                "commitment_id",
                "reward_profile_id",
                "content_fingerprint",
                "expected_sequence");
            return StrongboxOpeningRequestV1.Create(
                ExplicitCodecValuesV1.RequiredId(reader.Next("run_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("opening_operation_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("transaction_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("box_instance_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("box_definition_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("commitment_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("reward_profile_id")),
                CanonicalValueV1.ReadRequiredString(reader.Next("content_fingerprint")),
                CanonicalValueV1.ReadOptionalInt64(reader.Next("expected_sequence")));
        }

        private static CanonicalNodeV1 EncodeRewardOperation(
            RewardOperationRequestV1 operation)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("run_id", ExplicitCodecValuesV1.RequiredIdNode(operation.RunStableId)),
                CanonicalValueV1.Field("source_instance_id", ExplicitCodecValuesV1.RequiredIdNode(operation.SourceInstanceStableId)),
                CanonicalValueV1.Field("source_operation_id", ExplicitCodecValuesV1.RequiredIdNode(operation.SourceOperationStableId)),
                CanonicalValueV1.Field("commitment_id", ExplicitCodecValuesV1.RequiredIdNode(operation.CommitmentStableId)),
                CanonicalValueV1.Field("reward_profile_id", ExplicitCodecValuesV1.RequiredIdNode(operation.RewardProfileStableId)),
                CanonicalValueV1.Field("content_fingerprint", CanonicalValueV1.RequiredString(operation.ContentFingerprint)));
        }

        private static RewardOperationRequestV1 DecodeRewardOperation(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "run_id",
                "source_instance_id",
                "source_operation_id",
                "commitment_id",
                "reward_profile_id",
                "content_fingerprint");
            return RewardOperationRequestV1.Create(
                ExplicitCodecValuesV1.RequiredId(reader.Next("run_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("source_instance_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("source_operation_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("commitment_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("reward_profile_id")),
                CanonicalValueV1.ReadRequiredString(reader.Next("content_fingerprint")));
        }

        private static CanonicalNodeV1 EncodeRewardGrant(RewardGrantV1 grant)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("grant_id", ExplicitCodecValuesV1.RequiredIdNode(grant.GrantStableId)),
                CanonicalValueV1.Field("kind", ExplicitCodecValuesV1.EnumNode(grant.Kind)),
                CanonicalValueV1.Field("content_id", ExplicitCodecValuesV1.RequiredIdNode(grant.ContentStableId)),
                CanonicalValueV1.Field("quantity", CanonicalValueV1.Int64(grant.Quantity)));
        }

        private static RewardGrantV1 DecodeRewardGrant(CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "grant_id",
                "kind",
                "content_id",
                "quantity");
            return RewardGrantV1.Create(
                ExplicitCodecValuesV1.RequiredId(reader.Next("grant_id")),
                ExplicitCodecValuesV1.EnumValue<RewardGrantKindV1>(reader.Next("kind")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("content_id")),
                CanonicalValueV1.ReadInt64(reader.Next("quantity")));
        }

        private static CanonicalNodeV1 EncodeRewardResult(RewardResultV1 result)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("commitment_id", ExplicitCodecValuesV1.RequiredIdNode(result.CommitmentStableId)),
                CanonicalValueV1.Field("source_operation_id", ExplicitCodecValuesV1.RequiredIdNode(result.SourceOperationStableId)),
                CanonicalValueV1.Field("disposition", ExplicitCodecValuesV1.EnumNode(result.Disposition)),
                CanonicalValueV1.Field("grants", ExplicitCodecValuesV1.EncodeList(result.Grants, EncodeRewardGrant)));
        }

        private static RewardResultV1 DecodeRewardResult(CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "commitment_id",
                "source_operation_id",
                "disposition",
                "grants");
            StableId commitment = ExplicitCodecValuesV1.RequiredId(reader.Next("commitment_id"));
            StableId source = ExplicitCodecValuesV1.RequiredId(reader.Next("source_operation_id"));
            RewardResultDispositionV1 disposition =
                ExplicitCodecValuesV1.EnumValue<RewardResultDispositionV1>(reader.Next("disposition"));
            List<RewardGrantV1> grants = ExplicitCodecValuesV1.DecodeList(
                reader.Next("grants"),
                DecodeRewardGrant);
            return disposition == RewardResultDispositionV1.Grants
                ? RewardResultV1.CreateGrants(commitment, source, grants)
                : RewardResultV1.CreateExplicitNoDrop(commitment, source);
        }

        private static CanonicalNodeV1 EncodeRewardTraceEntry(
            RewardTraceEntryV1 entry)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("entry_id", ExplicitCodecValuesV1.RequiredIdNode(entry.TraceEntryStableId)),
                CanonicalValueV1.Field("ordinal", CanonicalValueV1.Int32(entry.Ordinal)),
                CanonicalValueV1.Field("step_id", ExplicitCodecValuesV1.RequiredIdNode(entry.StepStableId)),
                CanonicalValueV1.Field("subject_id", ExplicitCodecValuesV1.RequiredIdNode(entry.SubjectStableId)),
                CanonicalValueV1.Field("decision_kind", ExplicitCodecValuesV1.EnumNode(entry.DecisionKind)),
                CanonicalValueV1.Field("input_value", CanonicalValueV1.Int64(entry.InputValue)),
                CanonicalValueV1.Field("output_value", CanonicalValueV1.Int64(entry.OutputValue)));
        }

        private static RewardTraceEntryV1 DecodeRewardTraceEntry(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "entry_id",
                "ordinal",
                "step_id",
                "subject_id",
                "decision_kind",
                "input_value",
                "output_value");
            return RewardTraceEntryV1.Create(
                ExplicitCodecValuesV1.RequiredId(reader.Next("entry_id")),
                CanonicalValueV1.ReadInt32(reader.Next("ordinal")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("step_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("subject_id")),
                ExplicitCodecValuesV1.EnumValue<RewardTraceDecisionKindV1>(reader.Next("decision_kind")),
                CanonicalValueV1.ReadInt64(reader.Next("input_value")),
                CanonicalValueV1.ReadInt64(reader.Next("output_value")));
        }

        private static CanonicalNodeV1 EncodeRewardTrace(RewardTraceV1 trace)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("source_operation_id", ExplicitCodecValuesV1.RequiredIdNode(trace.SourceOperationStableId)),
                CanonicalValueV1.Field("entries", ExplicitCodecValuesV1.EncodeList(trace.Entries, EncodeRewardTraceEntry)));
        }

        private static RewardTraceV1 DecodeRewardTrace(CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "source_operation_id",
                "entries");
            return RewardTraceV1.Create(
                ExplicitCodecValuesV1.RequiredId(reader.Next("source_operation_id")),
                ExplicitCodecValuesV1.DecodeList(reader.Next("entries"), DecodeRewardTraceEntry));
        }

        private static CanonicalNodeV1 EncodeGenerationTraceEntry(
            RewardGenerationTraceEntryV1 entry)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("ordinal", CanonicalValueV1.Int32(entry.Ordinal)),
                CanonicalValueV1.Field("step_id", ExplicitCodecValuesV1.RequiredIdNode(entry.StepId)),
                CanonicalValueV1.Field("subject_id", ExplicitCodecValuesV1.RequiredIdNode(entry.SubjectId)),
                CanonicalValueV1.Field("decision", ExplicitCodecValuesV1.EnumNode(entry.Decision)),
                CanonicalValueV1.Field("substream_purpose_id", ExplicitCodecValuesV1.Id(entry.SubstreamPurposeId)),
                CanonicalValueV1.Field("substream_ordinal", CanonicalValueV1.UInt64(entry.SubstreamOrdinal)),
                CanonicalValueV1.Field("samples_consumed", CanonicalValueV1.UInt64(entry.SamplesConsumed)),
                CanonicalValueV1.Field("input_value", CanonicalValueV1.Int64(entry.InputValue)),
                CanonicalValueV1.Field("output_value", CanonicalValueV1.Int64(entry.OutputValue)),
                CanonicalValueV1.Field("detail", CanonicalValueV1.RequiredString(entry.Detail)));
        }

        private static RewardGenerationTraceEntryV1 DecodeGenerationTraceEntry(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "ordinal",
                "step_id",
                "subject_id",
                "decision",
                "substream_purpose_id",
                "substream_ordinal",
                "samples_consumed",
                "input_value",
                "output_value",
                "detail");
            return new RewardGenerationTraceEntryV1(
                CanonicalValueV1.ReadInt32(reader.Next("ordinal")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("step_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("subject_id")),
                ExplicitCodecValuesV1.EnumValue<RewardGenerationTraceDecisionV1>(reader.Next("decision")),
                ExplicitCodecValuesV1.OptionalId(reader.Next("substream_purpose_id")),
                CanonicalValueV1.ReadUInt64(reader.Next("substream_ordinal")),
                CanonicalValueV1.ReadUInt64(reader.Next("samples_consumed")),
                CanonicalValueV1.ReadInt64(reader.Next("input_value")),
                CanonicalValueV1.ReadInt64(reader.Next("output_value")),
                CanonicalValueV1.ReadRequiredString(reader.Next("detail")));
        }

        private static CanonicalNodeV1 EncodeGenerationTrace(
            RewardGenerationTraceV1 trace)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("algorithm_version", CanonicalValueV1.Int32(trace.AlgorithmVersion)),
                CanonicalValueV1.Field("root_seed", CanonicalValueV1.UInt64(trace.RootSeed)),
                CanonicalValueV1.Field("content_fingerprint", CanonicalValueV1.RequiredString(trace.ContentFingerprint)),
                CanonicalValueV1.Field("context_fingerprint", CanonicalValueV1.RequiredString(trace.ContextFingerprint)),
                CanonicalValueV1.Field("result_fingerprint", CanonicalValueV1.RequiredString(trace.ResultFingerprint)),
                CanonicalValueV1.Field("entries", ExplicitCodecValuesV1.EncodeList(trace.Entries, EncodeGenerationTraceEntry)));
        }

        private static RewardGenerationTraceV1 DecodeGenerationTrace(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "algorithm_version",
                "root_seed",
                "content_fingerprint",
                "context_fingerprint",
                "result_fingerprint",
                "entries");
            return new RewardGenerationTraceV1(
                CanonicalValueV1.ReadInt32(reader.Next("algorithm_version")),
                CanonicalValueV1.ReadUInt64(reader.Next("root_seed")),
                CanonicalValueV1.ReadRequiredString(reader.Next("content_fingerprint")),
                CanonicalValueV1.ReadRequiredString(reader.Next("context_fingerprint")),
                CanonicalValueV1.ReadRequiredString(reader.Next("result_fingerprint")),
                ExplicitCodecValuesV1.DecodeList(reader.Next("entries"), DecodeGenerationTraceEntry));
        }

        private static CanonicalNodeV1 EncodeGrantPayload(
            RewardGrantApplicationPayloadV1 payload)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("grant", EncodeRewardGrant(payload.Grant)),
                CanonicalValueV1.Field("instance_ids", ExplicitCodecValuesV1.EncodeList(payload.InstanceStableIds, ExplicitCodecValuesV1.RequiredIdNode)),
                CanonicalValueV1.Field("equipment", ExplicitCodecValuesV1.EncodeList(payload.EquipmentInstances, PlayerHoldingsComponentCodecV1.EncodeEquipment)));
        }

        private static RewardGrantApplicationPayloadV1 DecodeGrantPayload(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "grant",
                "instance_ids",
                "equipment");
            RewardGrantV1 grant = DecodeRewardGrant(reader.Next("grant"));
            List<StableId> instanceIds = ExplicitCodecValuesV1.DecodeList(
                reader.Next("instance_ids"),
                ExplicitCodecValuesV1.RequiredId);
            List<EquipmentInstance> equipment = ExplicitCodecValuesV1.DecodeList(
                reader.Next("equipment"),
                PlayerHoldingsComponentCodecV1.DecodeEquipment);
            switch (grant.Kind)
            {
                case RewardGrantKindV1.Money:
                case RewardGrantKindV1.Scrap:
                case RewardGrantKindV1.PremiumAmmo:
                case RewardGrantKindV1.Miscellaneous:
                    if (instanceIds.Count != 0 || equipment.Count != 0)
                    {
                        throw new CanonicalPayloadExceptionV1(
                            "strongbox-value-payload-shape-invalid");
                    }
                    return RewardGrantApplicationPayloadV1.ForValue(grant);
                case RewardGrantKindV1.Strongbox:
                    if (equipment.Count != 0)
                    {
                        throw new CanonicalPayloadExceptionV1(
                            "strongbox-child-payload-shape-invalid");
                    }
                    return RewardGrantApplicationPayloadV1.ForStrongboxes(
                        grant,
                        instanceIds);
                case RewardGrantKindV1.EquipmentReference:
                    if (instanceIds.Count != equipment.Count)
                    {
                        throw new CanonicalPayloadExceptionV1(
                            "strongbox-equipment-payload-shape-invalid");
                    }
                    return RewardGrantApplicationPayloadV1.ForEquipment(
                        grant,
                        equipment);
                default:
                    throw new CanonicalPayloadExceptionV1(
                        "strongbox-grant-kind-invalid");
            }
        }

        private static CanonicalNodeV1 EncodeCommitCommand(
            RewardCommitCommandV1 command)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("operation", EncodeRewardOperation(command.Operation)),
                CanonicalValueV1.Field("generated_reward", EncodeRewardResult(command.GeneratedReward)),
                CanonicalValueV1.Field("generation_fingerprint", CanonicalValueV1.RequiredString(command.GenerationFingerprint)),
                CanonicalValueV1.Field("payloads", ExplicitCodecValuesV1.EncodeList(command.GrantPayloads, EncodeGrantPayload)));
        }

        private static RewardCommitCommandV1 DecodeCommitCommand(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "operation",
                "generated_reward",
                "generation_fingerprint",
                "payloads");
            return RewardCommitCommandV1.Create(
                DecodeRewardOperation(reader.Next("operation")),
                DecodeRewardResult(reader.Next("generated_reward")),
                CanonicalValueV1.ReadRequiredString(reader.Next("generation_fingerprint")),
                ExplicitCodecValuesV1.DecodeList(reader.Next("payloads"), DecodeGrantPayload));
        }

        private static CanonicalNodeV1 EncodeClaimCommand(
            RewardClaimCommandV1 command)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("claim_id", ExplicitCodecValuesV1.RequiredIdNode(command.ClaimStableId)),
                CanonicalValueV1.Field("commitment_id", ExplicitCodecValuesV1.RequiredIdNode(command.CommitmentStableId)),
                CanonicalValueV1.Field("claimant_id", ExplicitCodecValuesV1.RequiredIdNode(command.ClaimantStableId)),
                CanonicalValueV1.Field("money_authority_id", ExplicitCodecValuesV1.RequiredIdNode(command.MoneyAuthorityStableId)),
                CanonicalValueV1.Field("scrap_authority_id", ExplicitCodecValuesV1.RequiredIdNode(command.ScrapAuthorityStableId)),
                CanonicalValueV1.Field("holdings_authority_id", ExplicitCodecValuesV1.RequiredIdNode(command.HoldingsAuthorityStableId)),
                CanonicalValueV1.Field("expected_money_sequence", CanonicalValueV1.OptionalInt64(command.ExpectedMoneySequence)),
                CanonicalValueV1.Field("expected_scrap_sequence", CanonicalValueV1.OptionalInt64(command.ExpectedScrapSequence)),
                CanonicalValueV1.Field("expected_holdings_sequence", CanonicalValueV1.OptionalInt64(command.ExpectedHoldingsSequence)));
        }

        private static RewardClaimCommandV1 DecodeClaimCommand(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "claim_id",
                "commitment_id",
                "claimant_id",
                "money_authority_id",
                "scrap_authority_id",
                "holdings_authority_id",
                "expected_money_sequence",
                "expected_scrap_sequence",
                "expected_holdings_sequence");
            return RewardClaimCommandV1.Create(
                ExplicitCodecValuesV1.RequiredId(reader.Next("claim_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("commitment_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("claimant_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("money_authority_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("scrap_authority_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("holdings_authority_id")),
                CanonicalValueV1.ReadOptionalInt64(reader.Next("expected_money_sequence")),
                CanonicalValueV1.ReadOptionalInt64(reader.Next("expected_scrap_sequence")),
                CanonicalValueV1.ReadOptionalInt64(reader.Next("expected_holdings_sequence")));
        }

        private static CanonicalNodeV1 EncodeOpeningResult(
            StrongboxOpeningResultV1 result)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("opening_operation_id", ExplicitCodecValuesV1.RequiredIdNode(result.OpeningOperationStableId)),
                CanonicalValueV1.Field("status", ExplicitCodecValuesV1.EnumNode(result.Status)),
                CanonicalValueV1.Field("request_fingerprint", CanonicalValueV1.RequiredString(result.RequestFingerprint)),
                CanonicalValueV1.Field("reward_result", ExplicitCodecValuesV1.OptionalObject(result.RewardResult, EncodeRewardResult)),
                CanonicalValueV1.Field("reward_trace", ExplicitCodecValuesV1.OptionalObject(result.Trace, EncodeRewardTrace)),
                CanonicalValueV1.Field("previous_sequence", CanonicalValueV1.Int64(result.PreviousSequence)),
                CanonicalValueV1.Field("current_sequence", CanonicalValueV1.Int64(result.CurrentSequence)));
        }

        private static StrongboxOpeningResultV1 DecodeOpeningResult(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "opening_operation_id",
                "status",
                "request_fingerprint",
                "reward_result",
                "reward_trace",
                "previous_sequence",
                "current_sequence");
            return StrongboxOpeningResultV1.Create(
                ExplicitCodecValuesV1.RequiredId(reader.Next("opening_operation_id")),
                ExplicitCodecValuesV1.EnumValue<StrongboxOpeningStatusV1>(reader.Next("status")),
                CanonicalValueV1.ReadRequiredString(reader.Next("request_fingerprint")),
                ExplicitCodecValuesV1.OptionalObjectValue(reader.Next("reward_result"), DecodeRewardResult),
                ExplicitCodecValuesV1.OptionalObjectValue(reader.Next("reward_trace"), DecodeRewardTrace),
                CanonicalValueV1.ReadInt64(reader.Next("previous_sequence")),
                CanonicalValueV1.ReadInt64(reader.Next("current_sequence")));
        }
    }

}
