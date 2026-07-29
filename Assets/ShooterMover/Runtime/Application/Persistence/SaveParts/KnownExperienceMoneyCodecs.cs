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
    public sealed class PlayerXPCodec :
        ExplicitSavePartCodec<PlayerExperienceSnapshot>
    {
        public PlayerXPCodec()
            : base("player-experience-explicit-v1")
        {
        }

        public override SavePartValidationResult Validate(
            PlayerExperienceSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SavePartValidationResult.Reject(
                    "player-experience-snapshot-null");
            }
            if (snapshot.SchemaVersion
                != PlayerExperienceSnapshot.CurrentSchemaVersion)
            {
                return SavePartValidationResult.Reject(
                    "player-experience-schema-unsupported");
            }
            return FingerprintResult(
                snapshot.HasValidFingerprint(),
                "player-experience-fingerprint-mismatch");
        }

        protected override Node EncodeNode(
            PlayerExperienceSnapshot snapshot)
        {
            return Node.Object(
                Value.Field("schema_version", Value.Int32(snapshot.SchemaVersion)),
                Value.Field("authority_id", Value.RequiredString(snapshot.AuthorityStableId)),
                Value.Field("sequence", Value.Int64(snapshot.Sequence)),
                Value.Field("curve_fingerprint", Value.RequiredString(snapshot.CurveFingerprint)),
                Value.Field("cumulative_experience", Value.Int64(snapshot.CumulativeExperience)),
                Value.Field("progression_context", EncodeProgressionContext(snapshot.ProgressionContext)),
                Value.Field("grants", ExplicitCodecValues.EncodeList(snapshot.Grants, EncodeGrant)));
        }

        protected override PlayerExperienceSnapshot DecodeNode(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "schema_version",
                "authority_id",
                "sequence",
                "curve_fingerprint",
                "cumulative_experience",
                "progression_context",
                "grants");
            int schema = Value.ReadInt32(reader.Next("schema_version"));
            string authority = Value.ReadRequiredString(reader.Next("authority_id"));
            if (schema != PlayerExperienceSnapshot.CurrentSchemaVersion
                || !string.Equals(
                    authority,
                    PlayerExperienceIds.AuthorityStableId.ToString(),
                    StringComparison.Ordinal))
            {
                throw new PayloadException(
                    "player-experience-schema-or-authority-invalid");
            }
            return PlayerExperienceSnapshot.CreateCanonical(
                Value.ReadInt64(reader.Next("sequence")),
                Value.ReadRequiredString(reader.Next("curve_fingerprint")),
                Value.ReadInt64(reader.Next("cumulative_experience")),
                DecodeProgressionContext(reader.Next("progression_context")),
                ExplicitCodecValues.DecodeList(
                    reader.Next("grants"),
                    DecodeGrant));
        }

        internal static Node EncodeProgressionContext(
            ProgressionContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            return Node.Object(
                Value.Field("character_level", Value.Int32(context.CharacterLevel)),
                Value.Field("region_level", Value.Int32(context.RegionLevel)),
                Value.Field("difficulty_id", ExplicitCodecValues.RequiredIdNode(context.DifficultyId)),
                Value.Field("difficulty_value", Value.Int32(context.DifficultyValue)),
                Value.Field("tags", ExplicitCodecValues.EncodeList(
                    context.ProgressionTags,
                    ExplicitCodecValues.RequiredIdNode)));
        }

        internal static ProgressionContext DecodeProgressionContext(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "character_level",
                "region_level",
                "difficulty_id",
                "difficulty_value",
                "tags");
            return ProgressionContext.Create(
                Value.ReadInt32(reader.Next("character_level")),
                Value.ReadInt32(reader.Next("region_level")),
                ExplicitCodecValues.RequiredId(reader.Next("difficulty_id")),
                Value.ReadInt32(reader.Next("difficulty_value")),
                ExplicitCodecValues.DecodeList(
                    reader.Next("tags"),
                    ExplicitCodecValues.RequiredId));
        }

        private static Node EncodeGrant(
            PlayerExperienceGrantSnapshot grant)
        {
            return Node.Object(
                Value.Field("source_operation_id", Value.RequiredString(grant.SourceOperationStableId)),
                Value.Field("amount", Value.Int64(grant.Amount)),
                Value.Field("command_fingerprint", Value.RequiredString(grant.CommandFingerprint)),
                Value.Field("applied_sequence", Value.Int64(grant.AppliedSequence)));
        }

        private static PlayerExperienceGrantSnapshot DecodeGrant(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "source_operation_id",
                "amount",
                "command_fingerprint",
                "applied_sequence");
            string sourceId = Value.ReadRequiredString(
                reader.Next("source_operation_id"));
            long amount = Value.ReadInt64(reader.Next("amount"));
            string commandFingerprint = Value.ReadRequiredString(
                reader.Next("command_fingerprint"));
            long appliedSequence = Value.ReadInt64(
                reader.Next("applied_sequence"));
            StableId parsed;
            if (!StableId.TryParse(sourceId, out parsed)
                || !string.Equals(
                    commandFingerprint,
                    PlayerExperienceGrantRequest.ComputeCommandFingerprint(
                        parsed,
                        amount),
                    StringComparison.Ordinal))
            {
                throw new PayloadException(
                    "player-experience-grant-invalid");
            }
            return new PlayerExperienceGrantSnapshot(
                sourceId,
                amount,
                commandFingerprint,
                appliedSequence);
        }
    }

    public sealed class WalletCodec :
        ExplicitSavePartCodec<MoneyWalletSnapshot>
    {
        public WalletCodec()
            : base("money-wallet-explicit-v1")
        {
        }

        public override SavePartValidationResult Validate(
            MoneyWalletSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SavePartValidationResult.Reject(
                    "money-wallet-snapshot-null");
            }
            if (snapshot.SchemaVersion != MoneyWalletSnapshot.CurrentSchemaVersion)
            {
                return SavePartValidationResult.Reject(
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
                return SavePartValidationResult.Reject(
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

        protected override Node EncodeNode(
            MoneyWalletSnapshot snapshot)
        {
            return Node.Object(
                Value.Field("schema_version", Value.Int32(snapshot.SchemaVersion)),
                Value.Field("sequence", Value.Int64(snapshot.Sequence)),
                Value.Field("contributions", ExplicitCodecValues.EncodeList(snapshot.Contributions, EncodeContribution)),
                Value.Field("transactions", ExplicitCodecValues.EncodeList(snapshot.Transactions, EncodeTransaction)));
        }

        protected override MoneyWalletSnapshot DecodeNode(Node node)
        {
            var reader = new ObjectReader(
                node,
                "schema_version",
                "sequence",
                "contributions",
                "transactions");
            int schema = Value.ReadInt32(reader.Next("schema_version"));
            if (schema != MoneyWalletSnapshot.CurrentSchemaVersion)
            {
                throw new PayloadException(
                    "money-wallet-schema-unsupported");
            }
            return MoneyWalletSnapshot.CreateCanonical(
                schema,
                Value.ReadInt64(reader.Next("sequence")),
                ExplicitCodecValues.DecodeList(
                    reader.Next("contributions"),
                    DecodeContribution),
                ExplicitCodecValues.DecodeList(
                    reader.Next("transactions"),
                    DecodeTransaction));
        }

        private static Node EncodeContribution(
            MoneyWalletContributionSnapshot value)
        {
            return Node.Object(
                Value.Field("currency_id", Value.RequiredString(value.CurrencyStableId)),
                Value.Field("command_fingerprint", Value.RequiredString(value.CommandFingerprint)),
                Value.Field("quantity", Value.Int64(value.Quantity)));
        }

        private static MoneyWalletContributionSnapshot DecodeContribution(
            Node node)
        {
            var reader = new ObjectReader(
                node,
                "currency_id",
                "command_fingerprint",
                "quantity");
            return new MoneyWalletContributionSnapshot(
                Value.ReadRequiredString(reader.Next("currency_id")),
                Value.ReadRequiredString(reader.Next("command_fingerprint")),
                Value.ReadInt64(reader.Next("quantity")));
        }

        private static Node EncodeTransaction(
            MoneyWalletTransactionSnapshot value)
        {
            return Node.Object(
                Value.Field("transaction_id", Value.RequiredString(value.TransactionStableId)),
                Value.Field("currency_id", Value.RequiredString(value.CurrencyStableId)),
                Value.Field("command_fingerprint", Value.RequiredString(value.CommandFingerprint)),
                Value.Field("quantity_delta", Value.Int64(value.QuantityDelta)),
                Value.Field("expected_sequence", Value.OptionalInt64(value.ExpectedSequence)),
                Value.Field("mutation_fingerprint", Value.RequiredString(value.MutationFingerprint)),
                Value.Field("recorded_outcome", ExplicitCodecValues.EnumNode(value.RecordedOutcome)),
                Value.Field("sequence_before", Value.Int64(value.SequenceBefore)),
                Value.Field("sequence_after", Value.Int64(value.SequenceAfter)),
                Value.Field("previous_contribution", Value.Int64(value.PreviousContribution)),
                Value.Field("current_contribution", Value.Int64(value.CurrentContribution)),
                Value.Field("rejection_code", Value.String(value.RejectionCode)));
        }

        private static MoneyWalletTransactionSnapshot DecodeTransaction(
            Node node)
        {
            var reader = new ObjectReader(
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
                Value.ReadRequiredString(reader.Next("transaction_id")),
                Value.ReadRequiredString(reader.Next("currency_id")),
                Value.ReadRequiredString(reader.Next("command_fingerprint")),
                Value.ReadInt64(reader.Next("quantity_delta")),
                Value.ReadOptionalInt64(reader.Next("expected_sequence")),
                Value.ReadRequiredString(reader.Next("mutation_fingerprint")),
                ExplicitCodecValues.EnumValue<MoneyWalletRecordedOutcome>(reader.Next("recorded_outcome")),
                Value.ReadInt64(reader.Next("sequence_before")),
                Value.ReadInt64(reader.Next("sequence_after")),
                Value.ReadInt64(reader.Next("previous_contribution")),
                Value.ReadInt64(reader.Next("current_contribution")),
                Value.ReadOptionalString(reader.Next("rejection_code")));
        }
    }

}
