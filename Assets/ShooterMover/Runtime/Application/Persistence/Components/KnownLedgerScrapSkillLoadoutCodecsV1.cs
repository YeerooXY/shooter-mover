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
    internal static class LedgerSnapshotCodecV1
    {
        public static CanonicalNodeV1 Encode<TVocabulary>(
            LedgerSnapshot<TVocabulary> snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("schema_version", CanonicalValueV1.Int32(snapshot.SchemaVersion)),
                CanonicalValueV1.Field("sequence", CanonicalValueV1.Int64(snapshot.Sequence)),
                CanonicalValueV1.Field("entries", ExplicitCodecValuesV1.EncodeList(snapshot.Entries, EncodeEntry)),
                CanonicalValueV1.Field("transactions", ExplicitCodecValuesV1.EncodeList(snapshot.Transactions, EncodeTransaction)));
        }

        public static LedgerSnapshot<TVocabulary> Decode<TVocabulary>(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "schema_version",
                "sequence",
                "entries",
                "transactions");
            int schema = CanonicalValueV1.ReadInt32(reader.Next("schema_version"));
            if (schema != LedgerSnapshot<TVocabulary>.CurrentSchemaVersion)
            {
                throw new CanonicalPayloadExceptionV1(
                    "ledger-snapshot-schema-unsupported");
            }
            return LedgerSnapshot<TVocabulary>.CreateCanonical(
                schema,
                CanonicalValueV1.ReadInt64(reader.Next("sequence")),
                ExplicitCodecValuesV1.DecodeList(
                    reader.Next("entries"),
                    DecodeEntry),
                ExplicitCodecValuesV1.DecodeList(
                    reader.Next("transactions"),
                    DecodeTransaction));
        }

        public static bool IsCanonical<TVocabulary>(
            LedgerSnapshot<TVocabulary> snapshot)
        {
            if (snapshot == null
                || snapshot.SchemaVersion
                    != LedgerSnapshot<TVocabulary>.CurrentSchemaVersion)
            {
                return false;
            }
            try
            {
                LedgerSnapshot<TVocabulary> canonical =
                    LedgerSnapshot<TVocabulary>.CreateCanonical(
                        snapshot.SchemaVersion,
                        snapshot.Sequence,
                        snapshot.Entries,
                        snapshot.Transactions);
                return string.Equals(
                    canonical.Fingerprint,
                    snapshot.Fingerprint,
                    StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        private static CanonicalNodeV1 EncodeEntry(LedgerSnapshotEntry value)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("entry_type_id", CanonicalValueV1.RequiredString(value.EntryTypeId)),
                CanonicalValueV1.Field("target_id", CanonicalValueV1.RequiredString(value.TargetId)),
                CanonicalValueV1.Field("canonical_payload", CanonicalValueV1.RequiredString(value.CanonicalPayload)),
                CanonicalValueV1.Field("quantity", CanonicalValueV1.Int64(value.Quantity)));
        }

        private static LedgerSnapshotEntry DecodeEntry(CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "entry_type_id",
                "target_id",
                "canonical_payload",
                "quantity");
            return new LedgerSnapshotEntry(
                CanonicalValueV1.ReadRequiredString(reader.Next("entry_type_id")),
                CanonicalValueV1.ReadRequiredString(reader.Next("target_id")),
                CanonicalValueV1.ReadRequiredString(reader.Next("canonical_payload")),
                CanonicalValueV1.ReadInt64(reader.Next("quantity")));
        }

        private static CanonicalNodeV1 EncodeTransaction(
            LedgerTransactionSnapshot value)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("transaction_id", CanonicalValueV1.RequiredString(value.TransactionId)),
                CanonicalValueV1.Field("entry_type_id", CanonicalValueV1.RequiredString(value.EntryTypeId)),
                CanonicalValueV1.Field("target_id", CanonicalValueV1.RequiredString(value.TargetId)),
                CanonicalValueV1.Field("canonical_payload", CanonicalValueV1.RequiredString(value.CanonicalPayload)),
                CanonicalValueV1.Field("quantity_delta", CanonicalValueV1.Int64(value.QuantityDelta)),
                CanonicalValueV1.Field("expected_sequence", CanonicalValueV1.OptionalInt64(value.ExpectedSequence)),
                CanonicalValueV1.Field("payload_fingerprint", CanonicalValueV1.RequiredString(value.PayloadFingerprint)),
                CanonicalValueV1.Field("original_status", ExplicitCodecValuesV1.EnumNode(value.OriginalStatus)),
                CanonicalValueV1.Field("sequence_before", CanonicalValueV1.Int64(value.SequenceBefore)),
                CanonicalValueV1.Field("sequence_after", CanonicalValueV1.Int64(value.SequenceAfter)),
                CanonicalValueV1.Field("previous_quantity", CanonicalValueV1.Int64(value.PreviousQuantity)),
                CanonicalValueV1.Field("current_quantity", CanonicalValueV1.Int64(value.CurrentQuantity)),
                CanonicalValueV1.Field("rejection_code", CanonicalValueV1.String(value.RejectionCode)));
        }

        private static LedgerTransactionSnapshot DecodeTransaction(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "transaction_id",
                "entry_type_id",
                "target_id",
                "canonical_payload",
                "quantity_delta",
                "expected_sequence",
                "payload_fingerprint",
                "original_status",
                "sequence_before",
                "sequence_after",
                "previous_quantity",
                "current_quantity",
                "rejection_code");
            return new LedgerTransactionSnapshot(
                CanonicalValueV1.ReadRequiredString(reader.Next("transaction_id")),
                CanonicalValueV1.ReadRequiredString(reader.Next("entry_type_id")),
                CanonicalValueV1.ReadRequiredString(reader.Next("target_id")),
                CanonicalValueV1.ReadRequiredString(reader.Next("canonical_payload")),
                CanonicalValueV1.ReadInt64(reader.Next("quantity_delta")),
                CanonicalValueV1.ReadOptionalInt64(reader.Next("expected_sequence")),
                CanonicalValueV1.ReadRequiredString(reader.Next("payload_fingerprint")),
                ExplicitCodecValuesV1.EnumValue<LedgerMutationStatus>(reader.Next("original_status")),
                CanonicalValueV1.ReadInt64(reader.Next("sequence_before")),
                CanonicalValueV1.ReadInt64(reader.Next("sequence_after")),
                CanonicalValueV1.ReadInt64(reader.Next("previous_quantity")),
                CanonicalValueV1.ReadInt64(reader.Next("current_quantity")),
                CanonicalValueV1.ReadOptionalString(reader.Next("rejection_code")));
        }
    }

    public sealed class ScrapWalletComponentCodecV1 :
        ExplicitSaveComponentCodecV1<ScrapSnapshotV1>
    {
        public ScrapWalletComponentCodecV1()
            : base("scrap-wallet-explicit-v1")
        {
        }

        public override SaveComponentValidationResultV1 Validate(
            ScrapSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                return SaveComponentValidationResultV1.Reject(
                    "scrap-wallet-snapshot-null");
            }
            if (snapshot.SchemaVersion != ScrapSnapshotV1.CurrentSchemaVersion
                || !LedgerSnapshotCodecV1.IsCanonical(snapshot.LedgerSnapshot))
            {
                return SaveComponentValidationResultV1.Reject(
                    "scrap-wallet-schema-or-ledger-invalid");
            }
            string expected = ScrapSnapshotV1.ComputeFingerprint(
                snapshot.SchemaVersion,
                snapshot.AuthorityStableId,
                snapshot.CurrencyStableId,
                snapshot.Balance,
                snapshot.LedgerSnapshot);
            return FingerprintResult(
                string.Equals(
                    expected,
                    snapshot.Fingerprint,
                    StringComparison.Ordinal),
                "scrap-wallet-fingerprint-mismatch");
        }

        protected override CanonicalNodeV1 EncodeNode(ScrapSnapshotV1 snapshot)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("schema_version", CanonicalValueV1.Int32(snapshot.SchemaVersion)),
                CanonicalValueV1.Field("authority_id", CanonicalValueV1.RequiredString(snapshot.AuthorityStableId)),
                CanonicalValueV1.Field("currency_id", CanonicalValueV1.RequiredString(snapshot.CurrencyStableId)),
                CanonicalValueV1.Field("balance", CanonicalValueV1.Int64(snapshot.Balance)),
                CanonicalValueV1.Field("ledger", LedgerSnapshotCodecV1.Encode(snapshot.LedgerSnapshot)));
        }

        protected override ScrapSnapshotV1 DecodeNode(CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "schema_version",
                "authority_id",
                "currency_id",
                "balance",
                "ledger");
            int schema = CanonicalValueV1.ReadInt32(reader.Next("schema_version"));
            if (schema != ScrapSnapshotV1.CurrentSchemaVersion)
            {
                throw new CanonicalPayloadExceptionV1(
                    "scrap-wallet-schema-unsupported");
            }
            return ScrapSnapshotV1.CreateCanonical(
                ParseId(reader.Next("authority_id")),
                ParseId(reader.Next("currency_id")),
                CanonicalValueV1.ReadInt64(reader.Next("balance")),
                LedgerSnapshotCodecV1.Decode<ScrapLedgerVocabulary>(
                    reader.Next("ledger")));
        }

        private static StableId ParseId(CanonicalNodeV1 node)
        {
            StableId id;
            if (!StableId.TryParse(
                CanonicalValueV1.ReadRequiredString(node),
                out id))
            {
                throw new CanonicalPayloadExceptionV1(
                    "scrap-wallet-stable-id-invalid");
            }
            return id;
        }
    }

    public sealed class RankedSkillAllocationComponentCodecV1 :
        ExplicitSaveComponentCodecV1<RankedSkillAllocationSnapshotV2>
    {
        public RankedSkillAllocationComponentCodecV1()
            : base("ranked-skill-allocation-explicit-v2")
        {
        }

        public override SaveComponentValidationResultV1 Validate(
            RankedSkillAllocationSnapshotV2 snapshot)
        {
            if (snapshot == null)
            {
                return SaveComponentValidationResultV1.Reject(
                    "ranked-skill-allocation-null");
            }
            try
            {
                var canonical = new RankedSkillAllocationSnapshotV2(
                    snapshot.ProfileId,
                    snapshot.ClassId,
                    snapshot.Version,
                    snapshot.SchemaVersion,
                    snapshot.ContentVersion,
                    snapshot.Ranks.ToDictionary(
                        pair => pair.Key,
                        pair => pair.Value,
                        StringComparer.Ordinal));
                return FingerprintResult(
                    string.Equals(
                        canonical.Fingerprint,
                        snapshot.Fingerprint,
                        StringComparison.Ordinal),
                    "ranked-skill-allocation-fingerprint-mismatch");
            }
            catch
            {
                return SaveComponentValidationResultV1.Reject(
                    "ranked-skill-allocation-invalid");
            }
        }

        protected override CanonicalNodeV1 EncodeNode(
            RankedSkillAllocationSnapshotV2 snapshot)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("profile_id", CanonicalValueV1.RequiredString(snapshot.ProfileId)),
                CanonicalValueV1.Field("class_id", CanonicalValueV1.RequiredString(snapshot.ClassId)),
                CanonicalValueV1.Field("version", CanonicalValueV1.Int64(snapshot.Version)),
                CanonicalValueV1.Field("schema_version", CanonicalValueV1.RequiredString(snapshot.SchemaVersion)),
                CanonicalValueV1.Field("content_version", CanonicalValueV1.RequiredString(snapshot.ContentVersion)),
                CanonicalValueV1.Field("ranks", ExplicitCodecValuesV1.EncodeList(
                    snapshot.Ranks.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                    pair => CanonicalNodeV1.Object(
                        CanonicalValueV1.Field("skill_id", CanonicalValueV1.RequiredString(pair.Key)),
                        CanonicalValueV1.Field("rank", CanonicalValueV1.Int32(pair.Value))))));
        }

        protected override RankedSkillAllocationSnapshotV2 DecodeNode(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "profile_id",
                "class_id",
                "version",
                "schema_version",
                "content_version",
                "ranks");
            string profileId = CanonicalValueV1.ReadRequiredString(reader.Next("profile_id"));
            string classId = CanonicalValueV1.ReadRequiredString(reader.Next("class_id"));
            long version = CanonicalValueV1.ReadInt64(reader.Next("version"));
            string schemaVersion = CanonicalValueV1.ReadRequiredString(reader.Next("schema_version"));
            string contentVersion = CanonicalValueV1.ReadRequiredString(reader.Next("content_version"));
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            IReadOnlyList<CanonicalNodeV1> rankNodes = CanonicalValueV1.ReadList(
                reader.Next("ranks"));
            for (int index = 0; index < rankNodes.Count; index++)
            {
                var rankReader = new CanonicalObjectReaderV1(
                    rankNodes[index],
                    "skill_id",
                    "rank");
                string skillId = CanonicalValueV1.ReadRequiredString(
                    rankReader.Next("skill_id"));
                if (ranks.ContainsKey(skillId))
                {
                    throw new CanonicalPayloadExceptionV1(
                        "ranked-skill-allocation-duplicate-skill");
                }
                ranks.Add(
                    skillId,
                    CanonicalValueV1.ReadInt32(rankReader.Next("rank")));
            }
            return new RankedSkillAllocationSnapshotV2(
                profileId,
                classId,
                version,
                schemaVersion,
                contentVersion,
                ranks);
        }
    }

    public sealed class ExactInstanceLoadoutComponentCodecV1 :
        ExplicitSaveComponentCodecV1<InventoryLoadoutAuthoritySnapshotV1>
    {
        public ExactInstanceLoadoutComponentCodecV1()
            : base("inventory-loadout-explicit-v1")
        {
        }

        public override SaveComponentValidationResultV1 Validate(
            InventoryLoadoutAuthoritySnapshotV1 snapshot)
        {
            return FingerprintResult(
                snapshot != null && snapshot.HasValidFingerprint(),
                "inventory-loadout-fingerprint-mismatch");
        }

        protected override CanonicalNodeV1 EncodeNode(
            InventoryLoadoutAuthoritySnapshotV1 snapshot)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("sequence", CanonicalValueV1.Int64(snapshot.Sequence)),
                CanonicalValueV1.Field("bindings", ExplicitCodecValuesV1.EncodeList(
                    snapshot.Bindings,
                    binding => CanonicalNodeV1.Object(
                        CanonicalValueV1.Field("slot_id", ExplicitCodecValuesV1.RequiredIdNode(binding.SlotStableId)),
                        CanonicalValueV1.Field("equipment_instance_id", ExplicitCodecValuesV1.Id(binding.EquipmentInstanceStableId))))));
        }

        protected override InventoryLoadoutAuthoritySnapshotV1 DecodeNode(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "sequence",
                "bindings");
            return InventoryLoadoutAuthoritySnapshotV1.CreateCanonical(
                CanonicalValueV1.ReadInt64(reader.Next("sequence")),
                ExplicitCodecValuesV1.DecodeList(
                    reader.Next("bindings"),
                    bindingNode =>
                    {
                        var bindingReader = new CanonicalObjectReaderV1(
                            bindingNode,
                            "slot_id",
                            "equipment_instance_id");
                        return new InventoryLoadoutSlotBindingV1(
                            ExplicitCodecValuesV1.RequiredId(bindingReader.Next("slot_id")),
                            ExplicitCodecValuesV1.OptionalId(bindingReader.Next("equipment_instance_id")));
                    }));
        }
    }

}
