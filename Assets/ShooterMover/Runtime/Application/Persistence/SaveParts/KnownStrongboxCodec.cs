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

namespace ShooterMover.Application.Persistence.SaveParts
{
    public sealed class StrongboxOpeningCodec :
        ExplicitSavePartCodec<StrongboxOpeningSnapshot>
    {
        public StrongboxOpeningCodec()
            : base("strongbox-opening-explicit-v1")
        {
        }

        public override SavePartValidationResult Validate(
            StrongboxOpeningSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SavePartValidationResult.Reject(
                    "strongbox-opening-snapshot-null");
            }
            if (snapshot.SchemaVersion
                != StrongboxOpeningSnapshot.CurrentSchemaVersion)
            {
                return SavePartValidationResult.Reject(
                    "strongbox-opening-schema-unsupported");
            }
            try
            {
                string expected = StrongboxOpeningSnapshot.ComputeFingerprint(
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
                return SavePartValidationResult.Reject(
                    "strongbox-opening-snapshot-invalid");
            }
        }

        protected override Node EncodeNode(
            StrongboxOpeningSnapshot snapshot)
        {
            return Node.Object(
                Value.Field("schema_version", Value.Int32(snapshot.SchemaVersion)),
                Value.Field("definition_catalog_fingerprint", Value.RequiredString(snapshot.DefinitionCatalogFingerprint)),
                Value.Field("sequence", Value.Int64(snapshot.Sequence)),
                Value.Field("contexts", ExplicitCodecValues.EncodeList(snapshot.Contexts, EncodeContext)),
                Value.Field("openings", ExplicitCodecValues.EncodeList(snapshot.Openings, EncodeOpeningRecord)));
        }

        protected override StrongboxOpeningSnapshot DecodeNode(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "schema_version",
                "definition_catalog_fingerprint",
                "sequence",
                "contexts",
                "openings");
            int schema = Value.ReadInt32(reader.Next("schema_version"));
            if (schema != StrongboxOpeningSnapshot.CurrentSchemaVersion)
            {
                throw new PayloadException(
                    "strongbox-opening-schema-unsupported");
            }
            return StrongboxOpeningSnapshot.CreateCanonical(
                Value.ReadRequiredString(reader.Next("definition_catalog_fingerprint")),
                Value.ReadInt64(reader.Next("sequence")),
                ExplicitCodecValues.DecodeList(reader.Next("contexts"), DecodeContext),
                ExplicitCodecValues.DecodeList(reader.Next("openings"), DecodeOpeningRecord));
        }

        private static Node EncodeContext(
            StrongboxInstanceContext context)
        {
            return Node.Object(
                Value.Field("instance_id", ExplicitCodecValues.RequiredIdNode(context.InstanceStableId)),
                Value.Field("tier_id", ExplicitCodecValues.RequiredIdNode(context.TierStableId)),
                Value.Field("root_seed", Value.UInt64(context.RootSeed)),
                Value.Field("algorithm_version", Value.Int32(context.AlgorithmVersion)),
                Value.Field("progression_context", PlayerXPCodec.EncodeProgressionContext(context.ProgressionContext)),
                Value.Field("source_context_id", ExplicitCodecValues.RequiredIdNode(context.SourceContextStableId)),
                Value.Field("collection_provenance_id", ExplicitCodecValues.RequiredIdNode(context.CollectionProvenanceStableId)),
                Value.Field("algorithm_content_fingerprint", Value.String(context.AlgorithmContentFingerprint)));
        }

        private static StrongboxInstanceContext DecodeContext(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "instance_id",
                "tier_id",
                "root_seed",
                "algorithm_version",
                "progression_context",
                "source_context_id",
                "collection_provenance_id",
                "algorithm_content_fingerprint");
            return StrongboxInstanceContext.Create(
                ExplicitCodecValues.RequiredId(reader.Next("instance_id")),
                ExplicitCodecValues.RequiredId(reader.Next("tier_id")),
                Value.ReadUInt64(reader.Next("root_seed")),
                Value.ReadInt32(reader.Next("algorithm_version")),
                PlayerXPCodec.DecodeProgressionContext(reader.Next("progression_context")),
                ExplicitCodecValues.RequiredId(reader.Next("source_context_id")),
                ExplicitCodecValues.RequiredId(reader.Next("collection_provenance_id")),
                Value.ReadOptionalString(reader.Next("algorithm_content_fingerprint")));
        }

        private static Node EncodeOpeningRecord(
            StrongboxOpeningRecordSnapshot record)
        {
            return Node.Object(
                Value.Field("command", EncodeOpenCommand(record.Command)),
                Value.Field("stage", ExplicitCodecValues.EnumNode(record.Stage)),
                Value.Field("generated_outcome", ExplicitCodecValues.OptionalObject(record.GeneratedOutcome, EncodeGeneratedOutcome)),
                Value.Field("commit_command", ExplicitCodecValues.OptionalObject(record.CommitCommand, EncodeCommitCommand)),
                Value.Field("claim_command", ExplicitCodecValues.OptionalObject(record.ClaimCommand, EncodeClaimCommand)),
                Value.Field("consume_command", ExplicitCodecValues.OptionalObject(record.ConsumeCommand, InventoryCodec.EncodeHoldingsCommand)),
                Value.Field("terminal_fact", ExplicitCodecValues.OptionalObject(record.TerminalFact, EncodeOpeningResult)),
                Value.Field("rejection_code", Value.String(record.RejectionCode)));
        }

        private static StrongboxOpeningRecordSnapshot DecodeOpeningRecord(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "command",
                "stage",
                "generated_outcome",
                "commit_command",
                "claim_command",
                "consume_command",
                "terminal_fact",
                "rejection_code");
            return new StrongboxOpeningRecordSnapshot(
                DecodeOpenCommand(reader.Next("command")),
                ExplicitCodecValues.EnumValue<StrongboxOpeningStage>(reader.Next("stage")),
                ExplicitCodecValues.OptionalObjectValue(reader.Next("generated_outcome"), DecodeGeneratedOutcome),
                ExplicitCodecValues.OptionalObjectValue(reader.Next("commit_command"), DecodeCommitCommand),
                ExplicitCodecValues.OptionalObjectValue(reader.Next("claim_command"), DecodeClaimCommand),
                ExplicitCodecValues.OptionalObjectValue(reader.Next("consume_command"), InventoryCodec.DecodeHoldingsCommand),
                ExplicitCodecValues.OptionalObjectValue(reader.Next("terminal_fact"), DecodeOpeningResult),
                Value.ReadOptionalString(reader.Next("rejection_code")));
        }

        private static Node EncodeOpenCommand(
            StrongboxOpenCommand command)
        {
            return Node.Object(
                Value.Field("opening_id", ExplicitCodecValues.RequiredIdNode(command.OpeningStableId)),
                Value.Field("run_id", ExplicitCodecValues.RequiredIdNode(command.RunStableId)),
                Value.Field("box_instance_id", ExplicitCodecValues.RequiredIdNode(command.StrongboxInstanceStableId)),
                Value.Field("claimant_id", ExplicitCodecValues.RequiredIdNode(command.ClaimantStableId)),
                Value.Field("money_authority_id", ExplicitCodecValues.RequiredIdNode(command.MoneyAuthorityStableId)),
                Value.Field("scrap_authority_id", ExplicitCodecValues.RequiredIdNode(command.ScrapAuthorityStableId)),
                Value.Field("holdings_authority_id", ExplicitCodecValues.RequiredIdNode(command.HoldingsAuthorityStableId)),
                Value.Field("expected_opening_sequence", Value.OptionalInt64(command.ExpectedOpeningSequence)));
        }

        private static StrongboxOpenCommand DecodeOpenCommand(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "opening_id",
                "run_id",
                "box_instance_id",
                "claimant_id",
                "money_authority_id",
                "scrap_authority_id",
                "holdings_authority_id",
                "expected_opening_sequence");
            return StrongboxOpenCommand.Create(
                ExplicitCodecValues.RequiredId(reader.Next("opening_id")),
                ExplicitCodecValues.RequiredId(reader.Next("run_id")),
                ExplicitCodecValues.RequiredId(reader.Next("box_instance_id")),
                ExplicitCodecValues.RequiredId(reader.Next("claimant_id")),
                ExplicitCodecValues.RequiredId(reader.Next("money_authority_id")),
                ExplicitCodecValues.RequiredId(reader.Next("scrap_authority_id")),
                ExplicitCodecValues.RequiredId(reader.Next("holdings_authority_id")),
                Value.ReadOptionalInt64(reader.Next("expected_opening_sequence")));
        }

        private static Node EncodeGeneratedOutcome(
            StrongboxGeneratedOutcome outcome)
        {
            return Node.Object(
                Value.Field("opening_request", EncodeOpeningRequest(outcome.OpeningRequest)),
                Value.Field("operation", EncodeRewardOperation(outcome.Operation)),
                Value.Field("reward_result", EncodeRewardResult(outcome.RewardResult)),
                Value.Field("reward_trace", EncodeRewardTrace(outcome.RewardTrace)),
                Value.Field("generation_trace", EncodeGenerationTrace(outcome.GenerationTrace)),
                Value.Field("generation_fingerprint", Value.RequiredString(outcome.GenerationFingerprint)),
                Value.Field("payloads", ExplicitCodecValues.EncodeList(outcome.Payloads, EncodeGrantPayload)));
        }

        private static StrongboxGeneratedOutcome DecodeGeneratedOutcome(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "opening_request",
                "operation",
                "reward_result",
                "reward_trace",
                "generation_trace",
                "generation_fingerprint",
                "payloads");
            return new StrongboxGeneratedOutcome(
                DecodeOpeningRequest(reader.Next("opening_request")),
                DecodeRewardOperation(reader.Next("operation")),
                DecodeRewardResult(reader.Next("reward_result")),
                DecodeRewardTrace(reader.Next("reward_trace")),
                DecodeGenerationTrace(reader.Next("generation_trace")),
                Value.ReadRequiredString(reader.Next("generation_fingerprint")),
                ExplicitCodecValues.DecodeList(reader.Next("payloads"), DecodeGrantPayload));
        }

        private static Node EncodeOpeningRequest(
            StrongboxOpeningRequest request)
        {
            return Node.Object(
                Value.Field("run_id", ExplicitCodecValues.RequiredIdNode(request.RunStableId)),
                Value.Field("opening_operation_id", ExplicitCodecValues.RequiredIdNode(request.OpeningOperationStableId)),
                Value.Field("transaction_id", ExplicitCodecValues.RequiredIdNode(request.TransactionStableId)),
                Value.Field("box_instance_id", ExplicitCodecValues.RequiredIdNode(request.StrongboxInstanceStableId)),
                Value.Field("box_definition_id", ExplicitCodecValues.RequiredIdNode(request.StrongboxDefinitionStableId)),
                Value.Field("commitment_id", ExplicitCodecValues.RequiredIdNode(request.CommitmentStableId)),
                Value.Field("reward_profile_id", ExplicitCodecValues.RequiredIdNode(request.RewardProfileStableId)),
                Value.Field("content_fingerprint", Value.RequiredString(request.ContentFingerprint)),
                Value.Field("expected_sequence", Value.OptionalInt64(request.ExpectedSequence)));
        }

        private static StrongboxOpeningRequest DecodeOpeningRequest(
            Node node)
        {
            var reader = new ObjectReader(
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
            return StrongboxOpeningRequest.Create(
                ExplicitCodecValues.RequiredId(reader.Next("run_id")),
                ExplicitCodecValues.RequiredId(reader.Next("opening_operation_id")),
                ExplicitCodecValues.RequiredId(reader.Next("transaction_id")),
                ExplicitCodecValues.RequiredId(reader.Next("box_instance_id")),
                ExplicitCodecValues.RequiredId(reader.Next("box_definition_id")),
                ExplicitCodecValues.RequiredId(reader.Next("commitment_id")),
                ExplicitCodecValues.RequiredId(reader.Next("reward_profile_id")),
                Value.ReadRequiredString(reader.Next("content_fingerprint")),
                Value.ReadOptionalInt64(reader.Next("expected_sequence")));
        }

        private static Node EncodeRewardOperation(
            RewardOperationRequest operation)
        {
            return Node.Object(
                Value.Field("run_id", ExplicitCodecValues.RequiredIdNode(operation.RunStableId)),
                Value.Field("source_instance_id", ExplicitCodecValues.RequiredIdNode(operation.SourceInstanceStableId)),
                Value.Field("source_operation_id", ExplicitCodecValues.RequiredIdNode(operation.SourceOperationStableId)),
                Value.Field("commitment_id", ExplicitCodecValues.RequiredIdNode(operation.CommitmentStableId)),
                Value.Field("reward_profile_id", ExplicitCodecValues.RequiredIdNode(operation.RewardProfileStableId)),
                Value.Field("content_fingerprint", Value.RequiredString(operation.ContentFingerprint)));
        }

        private static RewardOperationRequest DecodeRewardOperation(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "run_id",
                "source_instance_id",
                "source_operation_id",
                "commitment_id",
                "reward_profile_id",
                "content_fingerprint");
            return RewardOperationRequest.Create(
                ExplicitCodecValues.RequiredId(reader.Next("run_id")),
                ExplicitCodecValues.RequiredId(reader.Next("source_instance_id")),
                ExplicitCodecValues.RequiredId(reader.Next("source_operation_id")),
                ExplicitCodecValues.RequiredId(reader.Next("commitment_id")),
                ExplicitCodecValues.RequiredId(reader.Next("reward_profile_id")),
                Value.ReadRequiredString(reader.Next("content_fingerprint")));
        }

        private static Node EncodeRewardGrant(RewardGrant grant)
        {
            return Node.Object(
                Value.Field("grant_id", ExplicitCodecValues.RequiredIdNode(grant.GrantStableId)),
                Value.Field("kind", ExplicitCodecValues.EnumNode(grant.Kind)),
                Value.Field("content_id", ExplicitCodecValues.RequiredIdNode(grant.ContentStableId)),
                Value.Field("quantity", Value.Int64(grant.Quantity)));
        }

        private static RewardGrant DecodeRewardGrant(Node node)
        {
            var reader = new ObjectReader(
                node,
                "grant_id",
                "kind",
                "content_id",
                "quantity");
            return RewardGrant.Create(
                ExplicitCodecValues.RequiredId(reader.Next("grant_id")),
                ExplicitCodecValues.EnumValue<RewardGrantKind>(reader.Next("kind")),
                ExplicitCodecValues.RequiredId(reader.Next("content_id")),
                Value.ReadInt64(reader.Next("quantity")));
        }

        private static Node EncodeRewardResult(RewardResult result)
        {
            return Node.Object(
                Value.Field("commitment_id", ExplicitCodecValues.RequiredIdNode(result.CommitmentStableId)),
                Value.Field("source_operation_id", ExplicitCodecValues.RequiredIdNode(result.SourceOperationStableId)),
                Value.Field("disposition", ExplicitCodecValues.EnumNode(result.Disposition)),
                Value.Field("grants", ExplicitCodecValues.EncodeList(result.Grants, EncodeRewardGrant)));
        }

        private static RewardResult DecodeRewardResult(Node node)
        {
            var reader = new ObjectReader(
                node,
                "commitment_id",
                "source_operation_id",
                "disposition",
                "grants");
            StableId commitment = ExplicitCodecValues.RequiredId(reader.Next("commitment_id"));
            StableId source = ExplicitCodecValues.RequiredId(reader.Next("source_operation_id"));
            RewardResultDisposition disposition =
                ExplicitCodecValues.EnumValue<RewardResultDisposition>(reader.Next("disposition"));
            List<RewardGrant> grants = ExplicitCodecValues.DecodeList(
                reader.Next("grants"),
                DecodeRewardGrant);
            return disposition == RewardResultDisposition.Grants
                ? RewardResult.CreateGrants(commitment, source, grants)
                : RewardResult.CreateExplicitNoDrop(commitment, source);
        }

        private static Node EncodeRewardTraceEntry(
            RewardTraceEntry entry)
        {
            return Node.Object(
                Value.Field("entry_id", ExplicitCodecValues.RequiredIdNode(entry.TraceEntryStableId)),
                Value.Field("ordinal", Value.Int32(entry.Ordinal)),
                Value.Field("step_id", ExplicitCodecValues.RequiredIdNode(entry.StepStableId)),
                Value.Field("subject_id", ExplicitCodecValues.RequiredIdNode(entry.SubjectStableId)),
                Value.Field("decision_kind", ExplicitCodecValues.EnumNode(entry.DecisionKind)),
                Value.Field("input_value", Value.Int64(entry.InputValue)),
                Value.Field("output_value", Value.Int64(entry.OutputValue)));
        }

        private static RewardTraceEntry DecodeRewardTraceEntry(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "entry_id",
                "ordinal",
                "step_id",
                "subject_id",
                "decision_kind",
                "input_value",
                "output_value");
            return RewardTraceEntry.Create(
                ExplicitCodecValues.RequiredId(reader.Next("entry_id")),
                Value.ReadInt32(reader.Next("ordinal")),
                ExplicitCodecValues.RequiredId(reader.Next("step_id")),
                ExplicitCodecValues.RequiredId(reader.Next("subject_id")),
                ExplicitCodecValues.EnumValue<RewardTraceDecisionKind>(reader.Next("decision_kind")),
                Value.ReadInt64(reader.Next("input_value")),
                Value.ReadInt64(reader.Next("output_value")));
        }

        private static Node EncodeRewardTrace(RewardTrace trace)
        {
            return Node.Object(
                Value.Field("source_operation_id", ExplicitCodecValues.RequiredIdNode(trace.SourceOperationStableId)),
                Value.Field("entries", ExplicitCodecValues.EncodeList(trace.Entries, EncodeRewardTraceEntry)));
        }

        private static RewardTrace DecodeRewardTrace(Node node)
        {
            var reader = new ObjectReader(
                node,
                "source_operation_id",
                "entries");
            return RewardTrace.Create(
                ExplicitCodecValues.RequiredId(reader.Next("source_operation_id")),
                ExplicitCodecValues.DecodeList(reader.Next("entries"), DecodeRewardTraceEntry));
        }

        private static Node EncodeGenerationTraceEntry(
            RewardGenerationTraceEntry entry)
        {
            return Node.Object(
                Value.Field("ordinal", Value.Int32(entry.Ordinal)),
                Value.Field("step_id", ExplicitCodecValues.RequiredIdNode(entry.StepId)),
                Value.Field("subject_id", ExplicitCodecValues.RequiredIdNode(entry.SubjectId)),
                Value.Field("decision", ExplicitCodecValues.EnumNode(entry.Decision)),
                Value.Field("substream_purpose_id", ExplicitCodecValues.Id(entry.SubstreamPurposeId)),
                Value.Field("substream_ordinal", Value.UInt64(entry.SubstreamOrdinal)),
                Value.Field("samples_consumed", Value.UInt64(entry.SamplesConsumed)),
                Value.Field("input_value", Value.Int64(entry.InputValue)),
                Value.Field("output_value", Value.Int64(entry.OutputValue)),
                Value.Field("detail", Value.RequiredString(entry.Detail)));
        }

        private static RewardGenerationTraceEntry DecodeGenerationTraceEntry(
            Node node)
        {
            var reader = new ObjectReader(
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
            return new RewardGenerationTraceEntry(
                Value.ReadInt32(reader.Next("ordinal")),
                ExplicitCodecValues.RequiredId(reader.Next("step_id")),
                ExplicitCodecValues.RequiredId(reader.Next("subject_id")),
                ExplicitCodecValues.EnumValue<RewardGenerationTraceDecision>(reader.Next("decision")),
                ExplicitCodecValues.OptionalId(reader.Next("substream_purpose_id")),
                Value.ReadUInt64(reader.Next("substream_ordinal")),
                Value.ReadUInt64(reader.Next("samples_consumed")),
                Value.ReadInt64(reader.Next("input_value")),
                Value.ReadInt64(reader.Next("output_value")),
                Value.ReadRequiredString(reader.Next("detail")));
        }

        private static Node EncodeGenerationTrace(
            RewardGenerationTrace trace)
        {
            return Node.Object(
                Value.Field("algorithm_version", Value.Int32(trace.AlgorithmVersion)),
                Value.Field("root_seed", Value.UInt64(trace.RootSeed)),
                Value.Field("content_fingerprint", Value.RequiredString(trace.ContentFingerprint)),
                Value.Field("context_fingerprint", Value.RequiredString(trace.ContextFingerprint)),
                Value.Field("result_fingerprint", Value.RequiredString(trace.ResultFingerprint)),
                Value.Field("entries", ExplicitCodecValues.EncodeList(trace.Entries, EncodeGenerationTraceEntry)));
        }

        private static RewardGenerationTrace DecodeGenerationTrace(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "algorithm_version",
                "root_seed",
                "content_fingerprint",
                "context_fingerprint",
                "result_fingerprint",
                "entries");
            return new RewardGenerationTrace(
                Value.ReadInt32(reader.Next("algorithm_version")),
                Value.ReadUInt64(reader.Next("root_seed")),
                Value.ReadRequiredString(reader.Next("content_fingerprint")),
                Value.ReadRequiredString(reader.Next("context_fingerprint")),
                Value.ReadRequiredString(reader.Next("result_fingerprint")),
                ExplicitCodecValues.DecodeList(reader.Next("entries"), DecodeGenerationTraceEntry));
        }

        private static Node EncodeGrantPayload(
            RewardGrantApplicationPayload payload)
        {
            return Node.Object(
                Value.Field("grant", EncodeRewardGrant(payload.Grant)),
                Value.Field("instance_ids", ExplicitCodecValues.EncodeList(payload.InstanceStableIds, ExplicitCodecValues.RequiredIdNode)),
                Value.Field("equipment", ExplicitCodecValues.EncodeList(payload.EquipmentInstances, InventoryCodec.EncodeEquipment)));
        }

        private static RewardGrantApplicationPayload DecodeGrantPayload(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "grant",
                "instance_ids",
                "equipment");
            RewardGrant grant = DecodeRewardGrant(reader.Next("grant"));
            List<StableId> instanceIds = ExplicitCodecValues.DecodeList(
                reader.Next("instance_ids"),
                ExplicitCodecValues.RequiredId);
            List<EquipmentInstance> equipment = ExplicitCodecValues.DecodeList(
                reader.Next("equipment"),
                InventoryCodec.DecodeEquipment);
            switch (grant.Kind)
            {
                case RewardGrantKind.Money:
                case RewardGrantKind.Scrap:
                case RewardGrantKind.PremiumAmmo:
                case RewardGrantKind.Miscellaneous:
                    if (instanceIds.Count != 0 || equipment.Count != 0)
                    {
                        throw new PayloadException(
                            "strongbox-value-payload-shape-invalid");
                    }
                    return RewardGrantApplicationPayload.ForValue(grant);
                case RewardGrantKind.Strongbox:
                    if (equipment.Count != 0)
                    {
                        throw new PayloadException(
                            "strongbox-child-payload-shape-invalid");
                    }
                    return RewardGrantApplicationPayload.ForStrongboxes(
                        grant,
                        instanceIds);
                case RewardGrantKind.EquipmentReference:
                    if (instanceIds.Count != equipment.Count)
                    {
                        throw new PayloadException(
                            "strongbox-equipment-payload-shape-invalid");
                    }
                    return RewardGrantApplicationPayload.ForEquipment(
                        grant,
                        equipment);
                default:
                    throw new PayloadException(
                        "strongbox-grant-kind-invalid");
            }
        }

        private static Node EncodeCommitCommand(
            RewardCommitCommand command)
        {
            return Node.Object(
                Value.Field("operation", EncodeRewardOperation(command.Operation)),
                Value.Field("generated_reward", EncodeRewardResult(command.GeneratedReward)),
                Value.Field("generation_fingerprint", Value.RequiredString(command.GenerationFingerprint)),
                Value.Field("payloads", ExplicitCodecValues.EncodeList(command.GrantPayloads, EncodeGrantPayload)));
        }

        private static RewardCommitCommand DecodeCommitCommand(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "operation",
                "generated_reward",
                "generation_fingerprint",
                "payloads");
            return RewardCommitCommand.Create(
                DecodeRewardOperation(reader.Next("operation")),
                DecodeRewardResult(reader.Next("generated_reward")),
                Value.ReadRequiredString(reader.Next("generation_fingerprint")),
                ExplicitCodecValues.DecodeList(reader.Next("payloads"), DecodeGrantPayload));
        }

        private static Node EncodeClaimCommand(
            RewardClaimCommand command)
        {
            return Node.Object(
                Value.Field("claim_id", ExplicitCodecValues.RequiredIdNode(command.ClaimStableId)),
                Value.Field("commitment_id", ExplicitCodecValues.RequiredIdNode(command.CommitmentStableId)),
                Value.Field("claimant_id", ExplicitCodecValues.RequiredIdNode(command.ClaimantStableId)),
                Value.Field("money_authority_id", ExplicitCodecValues.RequiredIdNode(command.MoneyAuthorityStableId)),
                Value.Field("scrap_authority_id", ExplicitCodecValues.RequiredIdNode(command.ScrapAuthorityStableId)),
                Value.Field("holdings_authority_id", ExplicitCodecValues.RequiredIdNode(command.HoldingsAuthorityStableId)),
                Value.Field("expected_money_sequence", Value.OptionalInt64(command.ExpectedMoneySequence)),
                Value.Field("expected_scrap_sequence", Value.OptionalInt64(command.ExpectedScrapSequence)),
                Value.Field("expected_holdings_sequence", Value.OptionalInt64(command.ExpectedHoldingsSequence)));
        }

        private static RewardClaimCommand DecodeClaimCommand(
            Node node)
        {
            var reader = new ObjectReader(
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
            return RewardClaimCommand.Create(
                ExplicitCodecValues.RequiredId(reader.Next("claim_id")),
                ExplicitCodecValues.RequiredId(reader.Next("commitment_id")),
                ExplicitCodecValues.RequiredId(reader.Next("claimant_id")),
                ExplicitCodecValues.RequiredId(reader.Next("money_authority_id")),
                ExplicitCodecValues.RequiredId(reader.Next("scrap_authority_id")),
                ExplicitCodecValues.RequiredId(reader.Next("holdings_authority_id")),
                Value.ReadOptionalInt64(reader.Next("expected_money_sequence")),
                Value.ReadOptionalInt64(reader.Next("expected_scrap_sequence")),
                Value.ReadOptionalInt64(reader.Next("expected_holdings_sequence")));
        }

        private static Node EncodeOpeningResult(
            StrongboxOpeningResult result)
        {
            return Node.Object(
                Value.Field("opening_operation_id", ExplicitCodecValues.RequiredIdNode(result.OpeningOperationStableId)),
                Value.Field("status", ExplicitCodecValues.EnumNode(result.Status)),
                Value.Field("request_fingerprint", Value.RequiredString(result.RequestFingerprint)),
                Value.Field("reward_result", ExplicitCodecValues.OptionalObject(result.RewardResult, EncodeRewardResult)),
                Value.Field("reward_trace", ExplicitCodecValues.OptionalObject(result.Trace, EncodeRewardTrace)),
                Value.Field("previous_sequence", Value.Int64(result.PreviousSequence)),
                Value.Field("current_sequence", Value.Int64(result.CurrentSequence)));
        }

        private static StrongboxOpeningResult DecodeOpeningResult(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "opening_operation_id",
                "status",
                "request_fingerprint",
                "reward_result",
                "reward_trace",
                "previous_sequence",
                "current_sequence");
            return StrongboxOpeningResult.Create(
                ExplicitCodecValues.RequiredId(reader.Next("opening_operation_id")),
                ExplicitCodecValues.EnumValue<StrongboxOpeningStatus>(reader.Next("status")),
                Value.ReadRequiredString(reader.Next("request_fingerprint")),
                ExplicitCodecValues.OptionalObjectValue(reader.Next("reward_result"), DecodeRewardResult),
                ExplicitCodecValues.OptionalObjectValue(reader.Next("reward_trace"), DecodeRewardTrace),
                Value.ReadInt64(reader.Next("previous_sequence")),
                Value.ReadInt64(reader.Next("current_sequence")));
        }
    }

}
