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
    public sealed class PlayerHoldingsComponentCodec :
        ExplicitSaveComponentCodec<PlayerHoldingsSnapshot>
    {
        public PlayerHoldingsComponentCodec()
            : base("player-holdings-explicit-v1")
        {
        }

        public override SaveComponentValidationResult Validate(
            PlayerHoldingsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SaveComponentValidationResult.Reject(
                    "player-holdings-snapshot-null");
            }
            if (snapshot.SchemaVersion
                != PlayerHoldingsSnapshot.CurrentSchemaVersion
                || !LedgerSnapshotCodec.IsCanonical(snapshot.LedgerSnapshot))
            {
                return SaveComponentValidationResult.Reject(
                    "player-holdings-schema-or-ledger-invalid");
            }
            try
            {
                PlayerHoldingsSnapshot canonical =
                    PlayerHoldingsSnapshot.CreateCanonical(
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
                return SaveComponentValidationResult.Reject(
                    "player-holdings-snapshot-invalid");
            }
        }

        protected override Node EncodeNode(
            PlayerHoldingsSnapshot snapshot)
        {
            return Node.Object(
                Value.Field("schema_version", Value.Int32(snapshot.SchemaVersion)),
                Value.Field("authority_id", ExplicitCodecValues.RequiredIdNode(snapshot.AuthorityStableId)),
                Value.Field("maximum_stack_quantity", Value.Int64(snapshot.MaximumStackQuantity)),
                Value.Field("ledger", LedgerSnapshotCodec.Encode(snapshot.LedgerSnapshot)),
                Value.Field("unique_holdings", ExplicitCodecValues.EncodeList(snapshot.UniqueHoldings, EncodeUniqueHolding)),
                Value.Field("stack_holdings", ExplicitCodecValues.EncodeList(snapshot.StackHoldings, EncodeStackHolding)),
                Value.Field("transactions", ExplicitCodecValues.EncodeList(snapshot.Transactions, EncodeTransactionRecord)));
        }

        protected override PlayerHoldingsSnapshot DecodeNode(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "schema_version",
                "authority_id",
                "maximum_stack_quantity",
                "ledger",
                "unique_holdings",
                "stack_holdings",
                "transactions");
            int schema = Value.ReadInt32(reader.Next("schema_version"));
            if (schema != PlayerHoldingsSnapshot.CurrentSchemaVersion)
            {
                throw new PayloadException(
                    "player-holdings-schema-unsupported");
            }
            return PlayerHoldingsSnapshot.CreateCanonical(
                schema,
                ExplicitCodecValues.RequiredId(reader.Next("authority_id")),
                Value.ReadInt64(reader.Next("maximum_stack_quantity")),
                LedgerSnapshotCodec.Decode<HoldingsLedgerVocabulary>(reader.Next("ledger")),
                ExplicitCodecValues.DecodeList(reader.Next("unique_holdings"), DecodeUniqueHolding),
                ExplicitCodecValues.DecodeList(reader.Next("stack_holdings"), DecodeStackHolding),
                ExplicitCodecValues.DecodeList(reader.Next("transactions"), DecodeTransactionRecord));
        }

        internal static Node EncodeEquipment(
            EquipmentInstance equipment)
        {
            return Node.Object(
                Value.Field("instance_id", ExplicitCodecValues.RequiredIdNode(equipment.InstanceId)),
                Value.Field("definition_id", ExplicitCodecValues.RequiredIdNode(equipment.DefinitionId)),
                Value.Field("item_level", Value.Int32(equipment.ItemLevel)),
                Value.Field("quality_id", ExplicitCodecValues.RequiredIdNode(equipment.QualityId)),
                Value.Field("augments", ExplicitCodecValues.EncodeList(equipment.Augments, EncodeAugment)));
        }

        internal static EquipmentInstance DecodeEquipment(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "instance_id",
                "definition_id",
                "item_level",
                "quality_id",
                "augments");
            return EquipmentInstance.Create(
                ExplicitCodecValues.RequiredId(reader.Next("instance_id")),
                ExplicitCodecValues.RequiredId(reader.Next("definition_id")),
                Value.ReadInt32(reader.Next("item_level")),
                ExplicitCodecValues.RequiredId(reader.Next("quality_id")),
                ExplicitCodecValues.DecodeList(reader.Next("augments"), DecodeAugment));
        }

        private static Node EncodeAugment(AugmentInstance augment)
        {
            return Node.Object(
                Value.Field("instance_id", ExplicitCodecValues.RequiredIdNode(augment.InstanceId)),
                Value.Field("definition_id", ExplicitCodecValues.RequiredIdNode(augment.DefinitionId)),
                Value.Field("tier", Value.Int32(augment.Tier)),
                Value.Field("level", Value.Int32(augment.Level)));
        }

        private static AugmentInstance DecodeAugment(Node node)
        {
            var reader = new ObjectReader(
                node,
                "instance_id",
                "definition_id",
                "tier",
                "level");
            return AugmentInstance.Create(
                ExplicitCodecValues.RequiredId(reader.Next("instance_id")),
                ExplicitCodecValues.RequiredId(reader.Next("definition_id")),
                Value.ReadInt32(reader.Next("tier")),
                Value.ReadInt32(reader.Next("level")));
        }

        internal static Node EncodeProvenance(
            HoldingProvenance provenance)
        {
            return Node.Object(
                Value.Field("grant_id", ExplicitCodecValues.RequiredIdNode(provenance.GrantStableId)),
                Value.Field("source_id", ExplicitCodecValues.RequiredIdNode(provenance.SourceStableId)));
        }

        internal static HoldingProvenance DecodeProvenance(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "grant_id",
                "source_id");
            return HoldingProvenance.Create(
                ExplicitCodecValues.RequiredId(reader.Next("grant_id")),
                ExplicitCodecValues.RequiredId(reader.Next("source_id")));
        }

        private static Node EncodeUniqueHolding(
            UniqueHoldingSnapshot holding)
        {
            return Node.Object(
                Value.Field("reward_kind", ExplicitCodecValues.EnumNode(holding.RewardKind)),
                Value.Field("definition_id", ExplicitCodecValues.RequiredIdNode(holding.DefinitionStableId)),
                Value.Field("instance_id", ExplicitCodecValues.RequiredIdNode(holding.InstanceStableId)),
                Value.Field("equipment", ExplicitCodecValues.OptionalObject(holding.EquipmentInstance, EncodeEquipment)),
                Value.Field("provenance", EncodeProvenance(holding.Provenance)));
        }

        private static UniqueHoldingSnapshot DecodeUniqueHolding(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "reward_kind",
                "definition_id",
                "instance_id",
                "equipment",
                "provenance");
            return UniqueHoldingSnapshot.Create(
                ExplicitCodecValues.EnumValue<RewardGrantKind>(reader.Next("reward_kind")),
                ExplicitCodecValues.RequiredId(reader.Next("definition_id")),
                ExplicitCodecValues.RequiredId(reader.Next("instance_id")),
                ExplicitCodecValues.OptionalObjectValue(reader.Next("equipment"), DecodeEquipment),
                DecodeProvenance(reader.Next("provenance")));
        }

        private static Node EncodeStackHolding(
            StackHoldingSnapshot holding)
        {
            return Node.Object(
                Value.Field("reward_kind", ExplicitCodecValues.EnumNode(holding.RewardKind)),
                Value.Field("item_id", ExplicitCodecValues.RequiredIdNode(holding.ItemStableId)),
                Value.Field("quantity", Value.Int64(holding.Quantity)));
        }

        private static StackHoldingSnapshot DecodeStackHolding(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "reward_kind",
                "item_id",
                "quantity");
            return StackHoldingSnapshot.Create(
                ExplicitCodecValues.EnumValue<RewardGrantKind>(reader.Next("reward_kind")),
                ExplicitCodecValues.RequiredId(reader.Next("item_id")),
                Value.ReadInt64(reader.Next("quantity")));
        }

        internal static Node EncodeEconomyCommand(
            EconomyTransactionCommand command)
        {
            return Node.Object(
                Value.Field("transaction_id", ExplicitCodecValues.RequiredIdNode(command.TransactionStableId)),
                Value.Field("operation_id", ExplicitCodecValues.RequiredIdNode(command.OperationStableId)),
                Value.Field("authority_id", ExplicitCodecValues.RequiredIdNode(command.AuthorityStableId)),
                Value.Field("operation", ExplicitCodecValues.EnumNode(command.Operation)),
                Value.Field("resource_kind", ExplicitCodecValues.EnumNode(command.ResourceKind)),
                Value.Field("resource_id", ExplicitCodecValues.RequiredIdNode(command.ResourceStableId)),
                Value.Field("instance_id", ExplicitCodecValues.Id(command.InstanceStableId)),
                Value.Field("quantity", Value.Int64(command.Quantity)),
                Value.Field("expected_sequence", Value.OptionalInt64(command.ExpectedSequence)));
        }

        internal static EconomyTransactionCommand DecodeEconomyCommand(
            Node node)
        {
            var reader = new ObjectReader(
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
            return EconomyTransactionCommand.Create(
                ExplicitCodecValues.RequiredId(reader.Next("transaction_id")),
                ExplicitCodecValues.RequiredId(reader.Next("operation_id")),
                ExplicitCodecValues.RequiredId(reader.Next("authority_id")),
                ExplicitCodecValues.EnumValue<EconomyTransactionOperation>(reader.Next("operation")),
                ExplicitCodecValues.EnumValue<EconomyResourceKind>(reader.Next("resource_kind")),
                ExplicitCodecValues.RequiredId(reader.Next("resource_id")),
                ExplicitCodecValues.OptionalId(reader.Next("instance_id")),
                Value.ReadInt64(reader.Next("quantity")),
                Value.ReadOptionalInt64(reader.Next("expected_sequence")));
        }

        internal static Node EncodeHoldingsCommand(
            PlayerHoldingsCommand command)
        {
            return Node.Object(
                Value.Field("transaction", EncodeEconomyCommand(command.Transaction)),
                Value.Field("reward_kind", ExplicitCodecValues.EnumNode(command.RewardKind)),
                Value.Field("provenance", EncodeProvenance(command.Provenance)),
                Value.Field("equipment", ExplicitCodecValues.OptionalObject(command.EquipmentInstance, EncodeEquipment)));
        }

        internal static PlayerHoldingsCommand DecodeHoldingsCommand(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "transaction",
                "reward_kind",
                "provenance",
                "equipment");
            return PlayerHoldingsCommand.Create(
                DecodeEconomyCommand(reader.Next("transaction")),
                ExplicitCodecValues.EnumValue<RewardGrantKind>(reader.Next("reward_kind")),
                DecodeProvenance(reader.Next("provenance")),
                ExplicitCodecValues.OptionalObjectValue(reader.Next("equipment"), DecodeEquipment));
        }

        private static Node EncodeTransactionRecord(
            PlayerHoldingsTransactionRecord value)
        {
            return Node.Object(
                Value.Field("command", EncodeHoldingsCommand(value.Command)),
                Value.Field("original_status", ExplicitCodecValues.EnumNode(value.OriginalStatus)),
                Value.Field("ledger_original_status", ExplicitCodecValues.EnumNode(value.LedgerOriginalStatus)),
                Value.Field("sequence_before", Value.Int64(value.SequenceBefore)),
                Value.Field("sequence_after", Value.Int64(value.SequenceAfter)),
                Value.Field("ledger_previous_quantity", Value.Int64(value.LedgerPreviousQuantity)),
                Value.Field("ledger_current_quantity", Value.Int64(value.LedgerCurrentQuantity)),
                Value.Field("holding_previous_quantity", Value.Int64(value.HoldingPreviousQuantity)),
                Value.Field("holding_current_quantity", Value.Int64(value.HoldingCurrentQuantity)),
                Value.Field("rejection_code", Value.String(value.RejectionCode)));
        }

        private static PlayerHoldingsTransactionRecord DecodeTransactionRecord(
            Node node)
        {
            var reader = new ObjectReader(
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
            return PlayerHoldingsTransactionRecord.Create(
                DecodeHoldingsCommand(reader.Next("command")),
                ExplicitCodecValues.EnumValue<PlayerHoldingsMutationStatus>(reader.Next("original_status")),
                ExplicitCodecValues.EnumValue<LedgerMutationStatus>(reader.Next("ledger_original_status")),
                Value.ReadInt64(reader.Next("sequence_before")),
                Value.ReadInt64(reader.Next("sequence_after")),
                Value.ReadInt64(reader.Next("ledger_previous_quantity")),
                Value.ReadInt64(reader.Next("ledger_current_quantity")),
                Value.ReadInt64(reader.Next("holding_previous_quantity")),
                Value.ReadInt64(reader.Next("holding_current_quantity")),
                Value.ReadOptionalString(reader.Next("rejection_code")));
        }
    }

}
