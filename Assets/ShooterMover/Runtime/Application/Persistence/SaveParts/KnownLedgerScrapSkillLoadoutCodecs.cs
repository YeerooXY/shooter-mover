using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
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
    internal static class LedgerSnapshotCodec
    {
        public static Node Encode<TVocabulary>(
            LedgerSnapshot<TVocabulary> snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return Node.Object(
                Value.Field("schema_version", Value.Int32(snapshot.SchemaVersion)),
                Value.Field("sequence", Value.Int64(snapshot.Sequence)),
                Value.Field("entries", ExplicitCodecValues.EncodeList(snapshot.Entries, EncodeEntry)),
                Value.Field("transactions", ExplicitCodecValues.EncodeList(snapshot.Transactions, EncodeTransaction)));
        }

        public static LedgerSnapshot<TVocabulary> Decode<TVocabulary>(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "schema_version",
                "sequence",
                "entries",
                "transactions");
            int schema = Value.ReadInt32(reader.Next("schema_version"));
            if (schema != LedgerSnapshot<TVocabulary>.CurrentSchemaVersion)
            {
                throw new PayloadException(
                    "ledger-snapshot-schema-unsupported");
            }
            return LedgerSnapshot<TVocabulary>.CreateCanonical(
                schema,
                Value.ReadInt64(reader.Next("sequence")),
                ExplicitCodecValues.DecodeList(
                    reader.Next("entries"),
                    DecodeEntry),
                ExplicitCodecValues.DecodeList(
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

        private static Node EncodeEntry(LedgerSnapshotEntry value)
        {
            return Node.Object(
                Value.Field("entry_type_id", Value.RequiredString(value.EntryTypeId)),
                Value.Field("target_id", Value.RequiredString(value.TargetId)),
                Value.Field("canonical_payload", Value.RequiredString(value.CanonicalPayload)),
                Value.Field("quantity", Value.Int64(value.Quantity)));
        }

        private static LedgerSnapshotEntry DecodeEntry(Node node)
        {
            var reader = new ObjectReader(
                node,
                "entry_type_id",
                "target_id",
                "canonical_payload",
                "quantity");
            return new LedgerSnapshotEntry(
                Value.ReadRequiredString(reader.Next("entry_type_id")),
                Value.ReadRequiredString(reader.Next("target_id")),
                Value.ReadRequiredString(reader.Next("canonical_payload")),
                Value.ReadInt64(reader.Next("quantity")));
        }

        private static Node EncodeTransaction(
            LedgerTransactionSnapshot value)
        {
            return Node.Object(
                Value.Field("transaction_id", Value.RequiredString(value.TransactionId)),
                Value.Field("entry_type_id", Value.RequiredString(value.EntryTypeId)),
                Value.Field("target_id", Value.RequiredString(value.TargetId)),
                Value.Field("canonical_payload", Value.RequiredString(value.CanonicalPayload)),
                Value.Field("quantity_delta", Value.Int64(value.QuantityDelta)),
                Value.Field("expected_sequence", Value.OptionalInt64(value.ExpectedSequence)),
                Value.Field("payload_fingerprint", Value.RequiredString(value.PayloadFingerprint)),
                Value.Field("original_status", ExplicitCodecValues.EnumNode(value.OriginalStatus)),
                Value.Field("sequence_before", Value.Int64(value.SequenceBefore)),
                Value.Field("sequence_after", Value.Int64(value.SequenceAfter)),
                Value.Field("previous_quantity", Value.Int64(value.PreviousQuantity)),
                Value.Field("current_quantity", Value.Int64(value.CurrentQuantity)),
                Value.Field("rejection_code", Value.String(value.RejectionCode)));
        }

        private static LedgerTransactionSnapshot DecodeTransaction(
            Node node)
        {
            var reader = new ObjectReader(
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
                Value.ReadRequiredString(reader.Next("transaction_id")),
                Value.ReadRequiredString(reader.Next("entry_type_id")),
                Value.ReadRequiredString(reader.Next("target_id")),
                Value.ReadRequiredString(reader.Next("canonical_payload")),
                Value.ReadInt64(reader.Next("quantity_delta")),
                Value.ReadOptionalInt64(reader.Next("expected_sequence")),
                Value.ReadRequiredString(reader.Next("payload_fingerprint")),
                ExplicitCodecValues.EnumValue<LedgerMutationStatus>(reader.Next("original_status")),
                Value.ReadInt64(reader.Next("sequence_before")),
                Value.ReadInt64(reader.Next("sequence_after")),
                Value.ReadInt64(reader.Next("previous_quantity")),
                Value.ReadInt64(reader.Next("current_quantity")),
                Value.ReadOptionalString(reader.Next("rejection_code")));
        }
    }

    public sealed class ScrapWalletCodec :
        ExplicitSavePartCodec<ScrapSnapshot>
    {
        public ScrapWalletCodec()
            : base("scrap-wallet-explicit-v1")
        {
        }

        public override SavePartValidationResult Validate(
            ScrapSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SavePartValidationResult.Reject(
                    "scrap-wallet-snapshot-null");
            }
            if (snapshot.SchemaVersion != ScrapSnapshot.CurrentSchemaVersion
                || !LedgerSnapshotCodec.IsCanonical(snapshot.LedgerSnapshot))
            {
                return SavePartValidationResult.Reject(
                    "scrap-wallet-schema-or-ledger-invalid");
            }
            string expected = ScrapSnapshot.ComputeFingerprint(
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

        protected override Node EncodeNode(ScrapSnapshot snapshot)
        {
            return Node.Object(
                Value.Field("schema_version", Value.Int32(snapshot.SchemaVersion)),
                Value.Field("authority_id", Value.RequiredString(snapshot.AuthorityStableId)),
                Value.Field("currency_id", Value.RequiredString(snapshot.CurrencyStableId)),
                Value.Field("balance", Value.Int64(snapshot.Balance)),
                Value.Field("ledger", LedgerSnapshotCodec.Encode(snapshot.LedgerSnapshot)));
        }

        protected override ScrapSnapshot DecodeNode(Node node)
        {
            var reader = new ObjectReader(
                node,
                "schema_version",
                "authority_id",
                "currency_id",
                "balance",
                "ledger");
            int schema = Value.ReadInt32(reader.Next("schema_version"));
            if (schema != ScrapSnapshot.CurrentSchemaVersion)
            {
                throw new PayloadException(
                    "scrap-wallet-schema-unsupported");
            }
            return ScrapSnapshot.CreateCanonical(
                ParseId(reader.Next("authority_id")),
                ParseId(reader.Next("currency_id")),
                Value.ReadInt64(reader.Next("balance")),
                LedgerSnapshotCodec.Decode<ScrapLedgerVocabulary>(
                    reader.Next("ledger")));
        }

        private static StableId ParseId(Node node)
        {
            StableId id;
            if (!StableId.TryParse(
                Value.ReadRequiredString(node),
                out id))
            {
                throw new PayloadException(
                    "scrap-wallet-stable-id-invalid");
            }
            return id;
        }
    }

    public sealed class SkillsCodec :
        ExplicitSavePartCodec<RankedSkillAllocationSnapshot>
    {
        public SkillsCodec()
            : base("ranked-skill-allocation-explicit-v2")
        {
        }

        public override SavePartValidationResult Validate(
            RankedSkillAllocationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SavePartValidationResult.Reject(
                    "ranked-skill-allocation-null");
            }
            try
            {
                var canonical = new RankedSkillAllocationSnapshot(
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
                return SavePartValidationResult.Reject(
                    "ranked-skill-allocation-invalid");
            }
        }

        protected override Node EncodeNode(
            RankedSkillAllocationSnapshot snapshot)
        {
            return Node.Object(
                Value.Field("profile_id", Value.RequiredString(snapshot.ProfileId)),
                Value.Field("class_id", Value.RequiredString(snapshot.ClassId)),
                Value.Field("version", Value.Int64(snapshot.Version)),
                Value.Field("schema_version", Value.RequiredString(snapshot.SchemaVersion)),
                Value.Field("content_version", Value.RequiredString(snapshot.ContentVersion)),
                Value.Field("ranks", ExplicitCodecValues.EncodeList(
                    snapshot.Ranks.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                    pair => Node.Object(
                        Value.Field("skill_id", Value.RequiredString(pair.Key)),
                        Value.Field("rank", Value.Int32(pair.Value))))));
        }

        protected override RankedSkillAllocationSnapshot DecodeNode(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "profile_id",
                "class_id",
                "version",
                "schema_version",
                "content_version",
                "ranks");
            string profileId = Value.ReadRequiredString(reader.Next("profile_id"));
            string classId = Value.ReadRequiredString(reader.Next("class_id"));
            long version = Value.ReadInt64(reader.Next("version"));
            string schemaVersion = Value.ReadRequiredString(reader.Next("schema_version"));
            string contentVersion = Value.ReadRequiredString(reader.Next("content_version"));
            var ranks = new Dictionary<string, int>(StringComparer.Ordinal);
            IReadOnlyList<Node> rankNodes = Value.ReadList(
                reader.Next("ranks"));
            for (int index = 0; index < rankNodes.Count; index++)
            {
                var rankReader = new ObjectReader(
                    rankNodes[index],
                    "skill_id",
                    "rank");
                string skillId = Value.ReadRequiredString(
                    rankReader.Next("skill_id"));
                if (ranks.ContainsKey(skillId))
                {
                    throw new PayloadException(
                        "ranked-skill-allocation-duplicate-skill");
                }
                ranks.Add(
                    skillId,
                    Value.ReadInt32(rankReader.Next("rank")));
            }
            return new RankedSkillAllocationSnapshot(
                profileId,
                classId,
                version,
                schemaVersion,
                contentVersion,
                ranks);
        }
    }

}
