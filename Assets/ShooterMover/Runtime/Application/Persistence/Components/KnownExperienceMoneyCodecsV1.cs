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
    public sealed class PlayerExperienceComponentCodecV1 :
        ExplicitSaveComponentCodecV1<PlayerExperienceSnapshotV1>
    {
        public PlayerExperienceComponentCodecV1()
            : base("player-experience-explicit-v1")
        {
        }

        public override SaveComponentValidationResultV1 Validate(
            PlayerExperienceSnapshotV1 snapshot)
        {
            if (snapshot == null)
            {
                return SaveComponentValidationResultV1.Reject(
                    "player-experience-snapshot-null");
            }
            if (snapshot.SchemaVersion
                != PlayerExperienceSnapshotV1.CurrentSchemaVersion)
            {
                return SaveComponentValidationResultV1.Reject(
                    "player-experience-schema-unsupported");
            }
            return FingerprintResult(
                snapshot.HasValidFingerprint(),
                "player-experience-fingerprint-mismatch");
        }

        protected override CanonicalNodeV1 EncodeNode(
            PlayerExperienceSnapshotV1 snapshot)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("schema_version", CanonicalValueV1.Int32(snapshot.SchemaVersion)),
                CanonicalValueV1.Field("authority_id", CanonicalValueV1.RequiredString(snapshot.AuthorityStableId)),
                CanonicalValueV1.Field("sequence", CanonicalValueV1.Int64(snapshot.Sequence)),
                CanonicalValueV1.Field("curve_fingerprint", CanonicalValueV1.RequiredString(snapshot.CurveFingerprint)),
                CanonicalValueV1.Field("cumulative_experience", CanonicalValueV1.Int64(snapshot.CumulativeExperience)),
                CanonicalValueV1.Field("progression_context", EncodeProgressionContext(snapshot.ProgressionContext)),
                CanonicalValueV1.Field("grants", ExplicitCodecValuesV1.EncodeList(snapshot.Grants, EncodeGrant)));
        }

        protected override PlayerExperienceSnapshotV1 DecodeNode(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "schema_version",
                "authority_id",
                "sequence",
                "curve_fingerprint",
                "cumulative_experience",
                "progression_context",
                "grants");
            int schema = CanonicalValueV1.ReadInt32(reader.Next("schema_version"));
            string authority = CanonicalValueV1.ReadRequiredString(reader.Next("authority_id"));
            if (schema != PlayerExperienceSnapshotV1.CurrentSchemaVersion
                || !string.Equals(
                    authority,
                    PlayerExperienceIdsV1.AuthorityStableId.ToString(),
                    StringComparison.Ordinal))
            {
                throw new CanonicalPayloadExceptionV1(
                    "player-experience-schema-or-authority-invalid");
            }
            return PlayerExperienceSnapshotV1.CreateCanonical(
                CanonicalValueV1.ReadInt64(reader.Next("sequence")),
                CanonicalValueV1.ReadRequiredString(reader.Next("curve_fingerprint")),
                CanonicalValueV1.ReadInt64(reader.Next("cumulative_experience")),
                DecodeProgressionContext(reader.Next("progression_context")),
                ExplicitCodecValuesV1.DecodeList(
                    reader.Next("grants"),
                    DecodeGrant));
        }

        internal static CanonicalNodeV1 EncodeProgressionContext(
            ProgressionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("character_level", CanonicalValueV1.Int32(context.CharacterLevel)),
                CanonicalValueV1.Field("region_level", CanonicalValueV1.Int32(context.RegionLevel)),
                CanonicalValueV1.Field("difficulty_id", ExplicitCodecValuesV1.RequiredIdNode(context.DifficultyId)),
                CanonicalValueV1.Field("difficulty_value", CanonicalValueV1.Int32(context.DifficultyValue)),
                CanonicalValueV1.Field("tags", ExplicitCodecValuesV1.EncodeList(
                    context.ProgressionTags,
                    ExplicitCodecValuesV1.RequiredIdNode)));
        }

        internal static ProgressionContext DecodeProgressionContext(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "character_level",
                "region_level",
                "difficulty_id",
                "difficulty_value",
                "tags");
            return ProgressionContext.Create(
                CanonicalValueV1.ReadInt32(reader.Next("character_level")),
                CanonicalValueV1.ReadInt32(reader.Next("region_level")),
                ExplicitCodecValuesV1.RequiredId(reader.Next("difficulty_id")),
                CanonicalValueV1.ReadInt32(reader.Next("difficulty_value")),
                ExplicitCodecValuesV1.DecodeList(
                    reader.Next("tags"),
                    ExplicitCodecValuesV1.RequiredId));
        }

        private static CanonicalNodeV1 EncodeGrant(
            PlayerExperienceGrantSnapshotV1 grant)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("source_operation_id", CanonicalValueV1.RequiredString(grant.SourceOperationStableId)),
                CanonicalValueV1.Field("amount", CanonicalValueV1.Int64(grant.Amount)),
                CanonicalValueV1.Field("command_fingerprint", CanonicalValueV1.RequiredString(grant.CommandFingerprint)),
                CanonicalValueV1.Field("applied_sequence", CanonicalValueV1.Int64(grant.AppliedSequence)));
        }

        private static PlayerExperienceGrantSnapshotV1 DecodeGrant(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "source_operation_id",
                "amount",
                "command_fingerprint",
                "applied_sequence");
            string sourceId = CanonicalValueV1.ReadRequiredString(
                reader.Next("source_operation_id"));
            long amount = CanonicalValueV1.ReadInt64(reader.Next("amount"));
            string commandFingerprint = CanonicalValueV1.ReadRequiredString(
                reader.Next("command_fingerprint"));
            long appliedSequence = CanonicalValueV1.ReadInt64(
                reader.Next("applied_sequence"));
            StableId parsed;
            if (!StableId.TryParse(sourceId, out parsed)
                || !string.Equals(
                    commandFingerprint,
                    PlayerExperienceGrantRequestV1.ComputeCommandFingerprint(
                        parsed,
                        amount),
                    StringComparison.Ordinal))
            {
                throw new CanonicalPayloadExceptionV1(
                    "player-experience-grant-invalid");
            }
            return new PlayerExperienceGrantSnapshotV1(
                sourceId,
                amount,
                commandFingerprint,
                appliedSequence);
        }
    }

    public sealed class MoneyWalletComponentCodecV1 :
        ExplicitSaveComponentCodecV1<MoneyWalletSnapshot>
    {
        public MoneyWalletComponentCodecV1()
            : base("money-wallet-explicit-v1")
        {
        }

        public override SaveComponentValidationResultV1 Validate(
            MoneyWalletSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SaveComponentValidationResultV1.Reject(
                    "money-wallet-snapshot-null");
            }
            if (snapshot.SchemaVersion != MoneyWalletSnapshot.CurrentSchemaVersion)
            {
                return SaveComponentValidationResultV1.Reject(
                    "money-wallet-schema-unsupported");
            }
            MoneyWalletSnapshot canonical;
            try
            {
                canonical = MoneyWalletSnapshot.CreateCanonical(
                    snapshot.SchemaVersion,
                    snapshot.Sequence,
                    snapshot.Contributions,
                    snapshot.Transactions);
            }
            catch
            {
                return SaveComponentValidationResultV1.Reject(
                    "money-wallet-snapshot-invalid");
            }
            return FingerprintResult(
                canonical.Balance == snapshot.Balance
                    && string.Equals(
                        canonical.Fingerprint,
                        snapshot.Fingerprint,
                        StringComparison.Ordinal),
                "money-wallet-fingerprint-mismatch");
        }

        protected override CanonicalNodeV1 EncodeNode(
            MoneyWalletSnapshot snapshot)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("schema_version", CanonicalValueV1.Int32(snapshot.SchemaVersion)),
                CanonicalValueV1.Field("sequence", CanonicalValueV1.Int64(snapshot.Sequence)),
                CanonicalValueV1.Field("contributions", ExplicitCodecValuesV1.EncodeList(snapshot.Contributions, EncodeContribution)),
                CanonicalValueV1.Field("transactions", ExplicitCodecValuesV1.EncodeList(snapshot.Transactions, EncodeTransaction)));
        }

        protected override MoneyWalletSnapshot DecodeNode(CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "schema_version",
                "sequence",
                "contributions",
                "transactions");
            int schema = CanonicalValueV1.ReadInt32(reader.Next("schema_version"));
            if (schema != MoneyWalletSnapshot.CurrentSchemaVersion)
            {
                throw new CanonicalPayloadExceptionV1(
                    "money-wallet-schema-unsupported");
            }
            return MoneyWalletSnapshot.CreateCanonical(
                schema,
                CanonicalValueV1.ReadInt64(reader.Next("sequence")),
                ExplicitCodecValuesV1.DecodeList(
                    reader.Next("contributions"),
                    DecodeContribution),
                ExplicitCodecValuesV1.DecodeList(
                    reader.Next("transactions"),
                    DecodeTransaction));
        }

        private static CanonicalNodeV1 EncodeContribution(
            MoneyWalletContributionSnapshot value)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("currency_id", CanonicalValueV1.RequiredString(value.CurrencyStableId)),
                CanonicalValueV1.Field("command_fingerprint", CanonicalValueV1.RequiredString(value.CommandFingerprint)),
                CanonicalValueV1.Field("quantity", CanonicalValueV1.Int64(value.Quantity)));
        }

        private static MoneyWalletContributionSnapshot DecodeContribution(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "currency_id",
                "command_fingerprint",
                "quantity");
            return new MoneyWalletContributionSnapshot(
                CanonicalValueV1.ReadRequiredString(reader.Next("currency_id")),
                CanonicalValueV1.ReadRequiredString(reader.Next("command_fingerprint")),
                CanonicalValueV1.ReadInt64(reader.Next("quantity")));
        }

        private static CanonicalNodeV1 EncodeTransaction(
            MoneyWalletTransactionSnapshot value)
        {
            return CanonicalNodeV1.Object(
                CanonicalValueV1.Field("transaction_id", CanonicalValueV1.RequiredString(value.TransactionStableId)),
                CanonicalValueV1.Field("currency_id", CanonicalValueV1.RequiredString(value.CurrencyStableId)),
                CanonicalValueV1.Field("command_fingerprint", CanonicalValueV1.RequiredString(value.CommandFingerprint)),
                CanonicalValueV1.Field("quantity_delta", CanonicalValueV1.Int64(value.QuantityDelta)),
                CanonicalValueV1.Field("expected_sequence", CanonicalValueV1.OptionalInt64(value.ExpectedSequence)),
                CanonicalValueV1.Field("mutation_fingerprint", CanonicalValueV1.RequiredString(value.MutationFingerprint)),
                CanonicalValueV1.Field("recorded_outcome", ExplicitCodecValuesV1.EnumNode(value.RecordedOutcome)),
                CanonicalValueV1.Field("sequence_before", CanonicalValueV1.Int64(value.SequenceBefore)),
                CanonicalValueV1.Field("sequence_after", CanonicalValueV1.Int64(value.SequenceAfter)),
                CanonicalValueV1.Field("previous_contribution", CanonicalValueV1.Int64(value.PreviousContribution)),
                CanonicalValueV1.Field("current_contribution", CanonicalValueV1.Int64(value.CurrentContribution)),
                CanonicalValueV1.Field("rejection_code", CanonicalValueV1.String(value.RejectionCode)));
        }

        private static MoneyWalletTransactionSnapshot DecodeTransaction(
            CanonicalNodeV1 node)
        {
            var reader = new CanonicalObjectReaderV1(
                node,
                "transaction_id",
                "currency_id",
                "command_fingerprint",
                "quantity_delta",
                "expected_sequence",
                "mutation_fingerprint",
                "recorded_outcome",
                "sequence_before",
                "sequence_after",
                "previous_contribution",
                "current_contribution",
                "rejection_code");
            return new MoneyWalletTransactionSnapshot(
                CanonicalValueV1.ReadRequiredString(reader.Next("transaction_id")),
                CanonicalValueV1.ReadRequiredString(reader.Next("currency_id")),
                CanonicalValueV1.ReadRequiredString(reader.Next("command_fingerprint")),
                CanonicalValueV1.ReadInt64(reader.Next("quantity_delta")),
                CanonicalValueV1.ReadOptionalInt64(reader.Next("expected_sequence")),
                CanonicalValueV1.ReadRequiredString(reader.Next("mutation_fingerprint")),
                ExplicitCodecValuesV1.EnumValue<MoneyWalletRecordedOutcome>(reader.Next("recorded_outcome")),
                CanonicalValueV1.ReadInt64(reader.Next("sequence_before")),
                CanonicalValueV1.ReadInt64(reader.Next("sequence_after")),
                CanonicalValueV1.ReadInt64(reader.Next("previous_contribution")),
                CanonicalValueV1.ReadInt64(reader.Next("current_contribution")),
                CanonicalValueV1.ReadOptionalString(reader.Next("rejection_code")));
        }
    }

}
