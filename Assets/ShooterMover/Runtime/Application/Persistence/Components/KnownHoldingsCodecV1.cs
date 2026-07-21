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
    public sealed class PlayerHoldingsComponentCodecV1 :
        ExplicitSaveComponentCodecV1<PlayerHoldingsSnapshotV1>
    {
        public PlayerHoldingsComponentCodecV1()
            : base("player-holdings-explicit-v1")
        {
        }

        public override SaveComponentValidationResultV1 Validate(
            PlayerHoldingsSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                return SaveComponentValidationResultV1.Reject(
                    "player-holdings-snapshot-null");
            }
            if (snapshot.SchemaVersion
                != PlayerHoldingsSnapshotV1.CurrentSchemaVersion
                || !LedgerSnapshotCodecV1.IsCanonical(snapshot.LedgerSnapshot))
            {
                return SaveComponentValidationResultV1.Reject(
                    "player-holdings-schema-or-ledger-invalid");
            }
            try
            {
                PlayerHoldingsSnapshotV1 canonical =
                    PlayerHoldingsSnapshotV1.CreateCanonical(
                        snapshot.SchemaVersion,
                        snapshot.AuthorityStableId,
                        snapshot.MaximumStackQuantity,
                        snapshot.LedgerSnapshot,
                        snapshot.UniqueHoldings,
                        snapshot.StackHoldings,
                        snapshot.Transactions);
                return FingerprintResult(
                    string.Equals(
                        canonical.Fingerprint,
                        snapshot.Fingerprint,
                        StringComparison.Ordinal),
                    "player-holdings-fingerprint-mismatch");
            }
            catch
            {
                return SaveComponentValidationResultV1.Reject(
                    "player-holdings-snapshot-invalid");
            }
        }

        protected override CanonicalNodeV1 EncodeNode(
            PlayerHoldingsSnapshotV1 snapshot)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("schema_version", CanonicalValueV1.Int32(snapshot.SchemaVersion)),
                CanonicalValueV1.Field("authority_id", ExplicitCodecValuesV1.RequiredIdNode(snapshot.AuthorityStableId)),
                CanonicalValueV1.Field("maximum_stack_quantity", CanonicalValueV1.Int64(snapshot.MaximumStackQuantity)),
                CanonicalValueV1.Field("ledger", LedgerSnapshotCodecV1.Encode(snapshot.LedgerSnapshot)),
                CanonicalValueV1.Field("unique_holdings", ExplicitCodecValuesV1.EncodeList(snapshot.UniqueHoldings, EncodeUniqueHolding)),
                CanonicalValueV1.Field("stack_holdings", ExplicitCodecValuesV1.EncodeList(snapshot.StackHoldings, EncodeStackHolding)),
                CanonicalValueV1.Field("transactions", ExplicitCodecValuesV1.EncodeList(snapshot.Transactions, EncodeTransactionRecord)));
        }

        protected override PlayerHoldingsSnapshotV1 DecodeNode(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "schema_version",
                "authority_id",
                "maximum_stack_quantity",
                "ledger",
                "unique_holdings",
                "stack_holdings",
                "transactions");
            int schema = CanonicalValueV1.ReadInt32(reader.Next("schema_version"));
            if (schema != PlayerHoldingsSnapshotV1.CurrentSchemaVersion)
            {
                throw new CanonicalPayloadExceptionV1(
                    "player-holdings-schema-unsupported");
            }
            return PlayerHoldingsSnapshotV1.CreateCanonical(
                schema,
                ExplicitCodecValuesV1.RequiredId(reader.Next("authority_id")),
                CanonicalValueV1.ReadInt64(reader.Next("maximum_stack_quantity")),
                LedgerSnapshotCodecV1.Decode<HoldingsLedgerVocabularyV1>(reader.Next("ledger")),
                ExplicitCodecValuesV1.DecodeList(reader.Next("unique_holdings"), DecodeUniqueHolding),
                ExplicitCodecValuesV1.DecodeList(reader.Next("stack_holdings"), DecodeStackHolding),
                ExplicitCodecValuesV1.DecodeList(reader.Next("transactions"), DecodeTransactionRecord));
        }

        internal static CanonicalNodeV1 EncodeEquipment(
            EquipmentInstance equipment)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("instance_id", ExplicitCodecValuesV1.RequiredIdNode(equipment.InstanceId)),
                CanonicalValueV1.Field("definition_id", ExplicitCodecValuesV1.RequiredIdNode(equipment.DefinitionId)),
                CanonicalValueV1.Field("item_level", CanonicalValueV1.Int32(equipment.ItemLevel)),
                CanonicalValueV1.Field("quality_id", ExplicitCodecValuesV1.RequiredIdNode(equipment.QualityId)),
                CanonicalValueV1.Field("augments", ExplicitCodecValuesV1.EncodeList(equipment.Augments, EncodeAugment)));
        }

        internal static EquipmentInstance DecodeEquipment(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "instance_id",
                "definition_id",
                "item_level",
                "quality_id",
                "augments");
            return EquipmentInstance.Create(
                ExplicitCodecValuesV1.RequiredId(reader.Next("instance_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("definition_id")),
                CanonicalValueV1.ReadInt32(reader.Next("item_level")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("quality_id")),
                ExplicitCodecValuesV1.DecodeList(reader.Next("augments"), DecodeAugment));
        }

        private static CanonicalNodeV1 EncodeAugment(AugmentInstance augment)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("instance_id", ExplicitCodecValuesV1.RequiredIdNode(augment.InstanceId)),
                CanonicalValueV1.Field("definition_id", ExplicitCodecValuesV1.RequiredIdNode(augment.DefinitionId)),
                CanonicalValueV1.Field("tier", CanonicalValueV1.Int32(augment.Tier)),
                CanonicalValueV1.Field("level", CanonicalValueV1.Int32(augment.Level)));
        }

        private static AugmentInstance DecodeAugment(CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "instance_id",
                "definition_id",
                "tier",
                "level");
            return AugmentInstance.Create(
                ExplicitCodecValuesV1.RequiredId(reader.Next("instance_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("definition_id")),
                CanonicalValueV1.ReadInt32(reader.Next("tier")),
                CanonicalValueV1.ReadInt32(reader.Next("level")));
        }

        internal static CanonicalNodeV1 EncodeProvenance(
            HoldingProvenanceV1 provenance)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("grant_id", ExplicitCodecValuesV1.RequiredIdNode(provenance.GrantStableId)),
                CanonicalValueV1.Field("source_id", ExplicitCodecValuesV1.RequiredIdNode(provenance.SourceStableId)));
        }

        internal static HoldingProvenanceV1 DecodeProvenance(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "grant_id",
                "source_id");
            return HoldingProvenanceV1.Create(
                ExplicitCodecValuesV1.RequiredId(reader.Next("grant_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("source_id")));
        }

        private static CanonicalNodeV1 EncodeUniqueHolding(
            UniqueHoldingSnapshotV1 holding)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("reward_kind", ExplicitCodecValuesV1.EnumNode(holding.RewardKind)),
                CanonicalValueV1.Field("definition_id", ExplicitCodecValuesV1.RequiredIdNode(holding.DefinitionStableId)),
                CanonicalValueV1.Field("instance_id", ExplicitCodecValuesV1.RequiredIdNode(holding.InstanceStableId)),
                CanonicalValueV1.Field("equipment", ExplicitCodecValuesV1.OptionalObject(holding.EquipmentInstance, EncodeEquipment)),
                CanonicalValueV1.Field("provenance", EncodeProvenance(holding.Provenance)));
        }

        private static UniqueHoldingSnapshotV1 DecodeUniqueHolding(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "reward_kind",
                "definition_id",
                "instance_id",
                "equipment",
                "provenance");
            return UniqueHoldingSnapshotV1.Create(
                ExplicitCodecValuesV1.EnumValue<RewardGrantKindV1>(reader.Next("reward_kind")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("definition_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("instance_id")),
                ExplicitCodecValuesV1.OptionalObjectValue(reader.Next("equipment"), DecodeEquipment),
                DecodeProvenance(reader.Next("provenance")));
        }

        private static CanonicalNodeV1 EncodeStackHolding(
            StackHoldingSnapshotV1 holding)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("reward_kind", ExplicitCodecValuesV1.EnumNode(holding.RewardKind)),
                CanonicalValueV1.Field("item_id", ExplicitCodecValuesV1.RequiredIdNode(holding.ItemStableId)),
                CanonicalValueV1.Field("quantity", CanonicalValueV1.Int64(holding.Quantity)));
        }

        private static StackHoldingSnapshotV1 DecodeStackHolding(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "reward_kind",
                "item_id",
                "quantity");
            return StackHoldingSnapshotV1.Create(
                ExplicitCodecValuesV1.EnumValue<RewardGrantKindV1>(reader.Next("reward_kind")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("item_id")),
                CanonicalValueV1.ReadInt64(reader.Next("quantity")));
        }

        internal static CanonicalNodeV1 EncodeEconomyCommand(
            EconomyTransactionCommandV1 command)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("transaction_id", ExplicitCodecValuesV1.RequiredIdNode(command.TransactionStableId)),
                CanonicalValueV1.Field("operation_id", ExplicitCodecValuesV1.RequiredIdNode(command.OperationStableId)),
                CanonicalValueV1.Field("authority_id", ExplicitCodecValuesV1.RequiredIdNode(command.AuthorityStableId)),
                CanonicalValueV1.Field("operation", ExplicitCodecValuesV1.EnumNode(command.Operation)),
                CanonicalValueV1.Field("resource_kind", ExplicitCodecValuesV1.EnumNode(command.ResourceKind)),
                CanonicalValueV1.Field("resource_id", ExplicitCodecValuesV1.RequiredIdNode(command.ResourceStableId)),
                CanonicalValueV1.Field("instance_id", ExplicitCodecValuesV1.Id(command.InstanceStableId)),
                CanonicalValueV1.Field("quantity", CanonicalValueV1.Int64(command.Quantity)),
                CanonicalValueV1.Field("expected_sequence", CanonicalValueV1.OptionalInt64(command.ExpectedSequence)));
        }

        internal static EconomyTransactionCommandV1 DecodeEconomyCommand(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "transaction_id",
                "operation_id",
                "authority_id",
                "operation",
                "resource_kind",
                "resource_id",
                "instance_id",
                "quantity",
                "expected_sequence");
            return EconomyTransactionCommandV1.Create(
                ExplicitCodecValuesV1.RequiredId(reader.Next("transaction_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("operation_id")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("authority_id")),
                ExplicitCodecValuesV1.EnumValue<EconomyTransactionOperationV1>(reader.Next("operation")),
                ExplicitCodecValuesV1.EnumValue<EconomyResourceKindV1>(reader.Next("resource_kind")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("resource_id")),
                ExplicitCodecValuesV1.OptionalId(reader.Next("instance_id")),
                CanonicalValueV1.ReadInt64(reader.Next("quantity")),
                CanonicalValueV1.ReadOptionalInt64(reader.Next("expected_sequence")));
        }

        internal static CanonicalNodeV1 EncodeHoldingsCommand(
            PlayerHoldingsCommandV1 command)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("transaction", EncodeEconomyCommand(command.Transaction)),
                CanonicalValueV1.Field("reward_kind", ExplicitCodecValuesV1.EnumNode(command.RewardKind)),
                CanonicalValueV1.Field("provenance", EncodeProvenance(command.Provenance)),
                CanonicalValueV1.Field("equipment", ExplicitCodecValuesV1.OptionalObject(command.EquipmentInstance, EncodeEquipment)));
        }

        internal static PlayerHoldingsCommandV1 DecodeHoldingsCommand(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "transaction",
                "reward_kind",
                "provenance",
                "equipment");
            return PlayerHoldingsCommandV1.Create(
                DecodeEconomyCommand(reader.Next("transaction")),
                ExplicitCodecValuesV1.EnumValue<RewardGrantKindV1>(reader.Next("reward_kind")),
                DecodeProvenance(reader.Next("provenance")),
                ExplicitCodecValuesV1.OptionalObjectValue(reader.Next("equipment"), DecodeEquipment));
        }

        private static CanonicalNodeV1 EncodeTransactionRecord(
            PlayerHoldingsTransactionRecordV1 value)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("command", EncodeHoldingsCommand(value.Command)),
                CanonicalValueV1.Field("original_status", ExplicitCodecValuesV1.EnumNode(value.OriginalStatus)),
                CanonicalValueV1.Field("ledger_original_status", ExplicitCodecValuesV1.EnumNode(value.LedgerOriginalStatus)),
                CanonicalValueV1.Field("sequence_before", CanonicalValueV1.Int64(value.SequenceBefore)),
                CanonicalValueV1.Field("sequence_after", CanonicalValueV1.Int64(value.SequenceAfter)),
                CanonicalValueV1.Field("ledger_previous_quantity", CanonicalValueV1.Int64(value.LedgerPreviousQuantity)),
                CanonicalValueV1.Field("ledger_current_quantity", CanonicalValueV1.Int64(value.LedgerCurrentQuantity)),
                CanonicalValueV1.Field("holding_previous_quantity", CanonicalValueV1.Int64(value.HoldingPreviousQuantity)),
                CanonicalValueV1.Field("holding_current_quantity", CanonicalValueV1.Int64(value.HoldingCurrentQuantity)),
                CanonicalValueV1.Field("rejection_code", CanonicalValueV1.String(value.RejectionCode)));
        }

        private static PlayerHoldingsTransactionRecordV1 DecodeTransactionRecord(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "command",
                "original_status",
                "ledger_original_status",
                "sequence_before",
                "sequence_after",
                "ledger_previous_quantity",
                "ledger_current_quantity",
                "holding_previous_quantity",
                "holding_current_quantity",
                "rejection_code");
            return PlayerHoldingsTransactionRecordV1.Create(
                DecodeHoldingsCommand(reader.Next("command")),
                ExplicitCodecValuesV1.EnumValue<PlayerHoldingsMutationStatusV1>(reader.Next("original_status")),
                ExplicitCodecValuesV1.EnumValue<LedgerMutationStatus>(reader.Next("ledger_original_status")),
                CanonicalValueV1.ReadInt64(reader.Next("sequence_before")),
                CanonicalValueV1.ReadInt64(reader.Next("sequence_after")),
                CanonicalValueV1.ReadInt64(reader.Next("ledger_previous_quantity")),
                CanonicalValueV1.ReadInt64(reader.Next("ledger_current_quantity")),
                CanonicalValueV1.ReadInt64(reader.Next("holding_previous_quantity")),
                CanonicalValueV1.ReadInt64(reader.Next("holding_current_quantity")),
                CanonicalValueV1.ReadOptionalString(reader.Next("rejection_code")));
        }
    }

}
