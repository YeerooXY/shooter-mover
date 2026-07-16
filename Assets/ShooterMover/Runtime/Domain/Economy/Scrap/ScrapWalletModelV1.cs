using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;

namespace ShooterMover.Domain.Economy.Scrap
{
    public sealed class ScrapLedgerVocabulary
    {
    }

    public enum ScrapMutationKindV1
    {
        Grant = 1,
        Spend = 2,
    }

    public static class ScrapIdentityV1
    {
        public static readonly StableId BalanceEntryType =
            StableId.Create("scrap-entry", "balance");

        public static readonly StableId RewardGrantReason =
            StableId.Create("scrap-reason", "reward-grant");

        public static readonly StableId StrongboxOpeningReason =
            StableId.Create("scrap-reason", "strongbox-opening");

        public static readonly StableId FutureSalvageReason =
            StableId.Create("scrap-reason", "future-salvage");

        public static readonly StableId CraftingSpendReason =
            StableId.Create("scrap-reason", "crafting-spend");

        public static readonly StableId RewardSourceKind =
            StableId.Create("scrap-source", "reward");

        public static readonly StableId StrongboxSourceKind =
            StableId.Create("scrap-source", "strongbox");

        public static readonly StableId EquipmentSourceKind =
            StableId.Create("scrap-source", "equipment");

        public static readonly StableId CraftingSourceKind =
            StableId.Create("scrap-source", "crafting");

        public static bool TryGetExpectedSourceKind(
            StableId reasonStableId,
            out StableId expectedSourceKind)
        {
            if (reasonStableId == RewardGrantReason)
            {
                expectedSourceKind = RewardSourceKind;
                return true;
            }

            if (reasonStableId == StrongboxOpeningReason)
            {
                expectedSourceKind = StrongboxSourceKind;
                return true;
            }

            if (reasonStableId == FutureSalvageReason)
            {
                expectedSourceKind = EquipmentSourceKind;
                return true;
            }

            if (reasonStableId == CraftingSpendReason)
            {
                expectedSourceKind = CraftingSourceKind;
                return true;
            }

            expectedSourceKind = null;
            return false;
        }
    }

    public sealed class ScrapProvenanceV1
    {
        public ScrapProvenanceV1(
            StableId sourceKindStableId,
            StableId sourceOperationStableId,
            StableId subjectStableId)
        {
            SourceKindStableId = sourceKindStableId;
            SourceOperationStableId = sourceOperationStableId;
            SubjectStableId = subjectStableId;
        }

        public StableId SourceKindStableId { get; }

        public StableId SourceOperationStableId { get; }

        public StableId SubjectStableId { get; }

        public bool TryValidateFor(
            StableId reasonStableId,
            out string rejectionCode)
        {
            StableId expectedSourceKind;
            if (!ScrapIdentityV1.TryGetExpectedSourceKind(
                reasonStableId,
                out expectedSourceKind))
            {
                rejectionCode = "reason-invalid";
                return false;
            }

            if (SourceKindStableId == null)
            {
                rejectionCode = "provenance-source-kind-null";
                return false;
            }

            if (SourceOperationStableId == null)
            {
                rejectionCode = "provenance-operation-id-null";
                return false;
            }

            if (SubjectStableId == null)
            {
                rejectionCode = "provenance-subject-id-null";
                return false;
            }

            if (SourceKindStableId != expectedSourceKind)
            {
                rejectionCode = "provenance-source-kind-mismatch";
                return false;
            }

            rejectionCode = null;
            return true;
        }
    }

    public sealed class ScrapTransactionCommandV1
    {
        private readonly string _canonicalText;
        private readonly string _ledgerPayload;
        private readonly string _fingerprint;

        public ScrapTransactionCommandV1(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            StableId currencyStableId,
            ScrapMutationKindV1 mutationKind,
            long amount,
            StableId reasonStableId,
            ScrapProvenanceV1 provenance,
            long? expectedSequence = null)
        {
            TransactionStableId = transactionStableId;
            OperationStableId = operationStableId;
            AuthorityStableId = authorityStableId;
            CurrencyStableId = currencyStableId;
            MutationKind = mutationKind;
            Amount = amount;
            ReasonStableId = reasonStableId;
            Provenance = provenance;
            ExpectedSequence = expectedSequence;

            _ledgerPayload = ScrapLedgerPayloadV1.BuildCanonical(
                operationStableId,
                authorityStableId,
                mutationKind,
                amount,
                reasonStableId,
                provenance);
            _canonicalText = "format=scrap-command-v1"
                + "\ntransaction_id=" + CanonicalId(transactionStableId)
                + "\ncurrency_id=" + CanonicalId(currencyStableId)
                + "\n" + _ledgerPayload
                + "\nexpected_sequence="
                + (expectedSequence.HasValue
                    ? expectedSequence.Value.ToString(CultureInfo.InvariantCulture)
                    : "none");
            _fingerprint = ScrapFingerprintV1.Compute(_canonicalText);
        }

        public StableId TransactionStableId { get; }

        public StableId OperationStableId { get; }

        public StableId AuthorityStableId { get; }

        public StableId CurrencyStableId { get; }

        public ScrapMutationKindV1 MutationKind { get; }

        public long Amount { get; }

        public StableId ReasonStableId { get; }

        public ScrapProvenanceV1 Provenance { get; }

        public long? ExpectedSequence { get; }

        public string Fingerprint => _fingerprint;

        public string LedgerPayload => _ledgerPayload;

        public string ToCanonicalString()
        {
            return _canonicalText;
        }

        public long GetAdmissionDelta()
        {
            if (!Enum.IsDefined(typeof(ScrapMutationKindV1), MutationKind)
                || Amount <= 0L)
            {
                return 1L;
            }

            return MutationKind == ScrapMutationKindV1.Grant
                ? Amount
                : -Amount;
        }

        private static string CanonicalId(StableId stableId)
        {
            return stableId == null ? "none" : stableId.ToString();
        }
    }

    public sealed class ScrapLedgerPayloadV1
    {
        private const string FormatField = "format=scrap-wallet-v1";
        private const char FieldSeparator = '|';
        private const int FieldCount = 9;

        private ScrapLedgerPayloadV1(
            StableId operationStableId,
            StableId authorityStableId,
            ScrapMutationKindV1 mutationKind,
            long amount,
            StableId reasonStableId,
            ScrapProvenanceV1 provenance,
            string canonicalText)
        {
            OperationStableId = operationStableId;
            AuthorityStableId = authorityStableId;
            MutationKind = mutationKind;
            Amount = amount;
            ReasonStableId = reasonStableId;
            Provenance = provenance;
            CanonicalText = canonicalText;
        }

        public StableId OperationStableId { get; }

        public StableId AuthorityStableId { get; }

        public ScrapMutationKindV1 MutationKind { get; }

        public long Amount { get; }

        public StableId ReasonStableId { get; }

        public ScrapProvenanceV1 Provenance { get; }

        public string CanonicalText { get; }

        public static string BuildCanonical(
            StableId operationStableId,
            StableId authorityStableId,
            ScrapMutationKindV1 mutationKind,
            long amount,
            StableId reasonStableId,
            ScrapProvenanceV1 provenance)
        {
            return FormatField
                + "|operation_id=" + CanonicalId(operationStableId)
                + "|authority_id=" + CanonicalId(authorityStableId)
                + "|kind=" + ((int)mutationKind).ToString(CultureInfo.InvariantCulture)
                + "|amount=" + amount.ToString(CultureInfo.InvariantCulture)
                + "|reason_id=" + CanonicalId(reasonStableId)
                + "|source_kind_id="
                + CanonicalId(provenance == null ? null : provenance.SourceKindStableId)
                + "|source_operation_id="
                + CanonicalId(provenance == null ? null : provenance.SourceOperationStableId)
                + "|subject_id="
                + CanonicalId(provenance == null ? null : provenance.SubjectStableId);
        }

        public static bool TryParse(
            string canonicalText,
            out ScrapLedgerPayloadV1 payload,
            out string rejectionCode)
        {
            payload = null;
            if (!IsPrintableSingleLineAscii(canonicalText))
            {
                rejectionCode = canonicalText == null
                    ? "payload-null"
                    : "payload-not-printable-single-line-ascii";
                return false;
            }

            string[] fields = canonicalText.Split(FieldSeparator);
            if (fields.Length != FieldCount
                || !string.Equals(fields[0], FormatField, StringComparison.Ordinal))
            {
                rejectionCode = "payload-format-invalid";
                return false;
            }

            string operationText;
            string authorityText;
            string kindText;
            string amountText;
            string reasonText;
            string sourceKindText;
            string sourceOperationText;
            string subjectText;
            if (!TryRead(fields[1], "operation_id=", out operationText)
                || !TryRead(fields[2], "authority_id=", out authorityText)
                || !TryRead(fields[3], "kind=", out kindText)
                || !TryRead(fields[4], "amount=", out amountText)
                || !TryRead(fields[5], "reason_id=", out reasonText)
                || !TryRead(fields[6], "source_kind_id=", out sourceKindText)
                || !TryRead(fields[7], "source_operation_id=", out sourceOperationText)
                || !TryRead(fields[8], "subject_id=", out subjectText))
            {
                rejectionCode = "payload-field-invalid";
                return false;
            }

            StableId operationStableId;
            StableId authorityStableId;
            StableId reasonStableId;
            StableId sourceKindStableId;
            StableId sourceOperationStableId;
            StableId subjectStableId;
            int kindValue;
            long amount;
            if (!TryParseOptionalId(operationText, out operationStableId)
                || !TryParseOptionalId(authorityText, out authorityStableId)
                || !int.TryParse(
                    kindText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out kindValue)
                || !long.TryParse(
                    amountText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out amount)
                || !TryParseOptionalId(reasonText, out reasonStableId)
                || !TryParseOptionalId(sourceKindText, out sourceKindStableId)
                || !TryParseOptionalId(sourceOperationText, out sourceOperationStableId)
                || !TryParseOptionalId(subjectText, out subjectStableId))
            {
                rejectionCode = "payload-value-invalid";
                return false;
            }

            var provenance = new ScrapProvenanceV1(
                sourceKindStableId,
                sourceOperationStableId,
                subjectStableId);
            var parsed = new ScrapLedgerPayloadV1(
                operationStableId,
                authorityStableId,
                (ScrapMutationKindV1)kindValue,
                amount,
                reasonStableId,
                provenance,
                canonicalText);
            string rebuilt = BuildCanonical(
                parsed.OperationStableId,
                parsed.AuthorityStableId,
                parsed.MutationKind,
                parsed.Amount,
                parsed.ReasonStableId,
                parsed.Provenance);
            if (!string.Equals(rebuilt, canonicalText, StringComparison.Ordinal))
            {
                rejectionCode = "payload-not-canonical";
                return false;
            }

            payload = parsed;
            rejectionCode = null;
            return true;
        }

        public long GetAdmissionDelta()
        {
            if (!Enum.IsDefined(typeof(ScrapMutationKindV1), MutationKind)
                || Amount <= 0L)
            {
                return 1L;
            }

            return MutationKind == ScrapMutationKindV1.Grant
                ? Amount
                : -Amount;
        }

        private static bool IsPrintableSingleLineAscii(string value)
        {
            if (value == null
                || value.Length == 0
                || value.Length > LedgerEntry<ScrapLedgerVocabulary>.MaximumCanonicalPayloadLength)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character < ' ' || character > '~')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryRead(string field, string prefix, out string value)
        {
            if (field == null || !field.StartsWith(prefix, StringComparison.Ordinal))
            {
                value = null;
                return false;
            }

            value = field.Substring(prefix.Length);
            return true;
        }

        private static bool TryParseOptionalId(string text, out StableId stableId)
        {
            if (string.Equals(text, "none", StringComparison.Ordinal))
            {
                stableId = null;
                return true;
            }

            return StableId.TryParse(text, out stableId);
        }

        private static string CanonicalId(StableId stableId)
        {
            return stableId == null ? "none" : stableId.ToString();
        }
    }

    public sealed class ScrapSnapshotV1
    {
        public const int CurrentSchemaVersion = 1;

        public ScrapSnapshotV1(
            int schemaVersion,
            string authorityStableId,
            string currencyStableId,
            long balance,
            LedgerSnapshot<ScrapLedgerVocabulary> ledgerSnapshot,
            string fingerprint)
        {
            SchemaVersion = schemaVersion;
            AuthorityStableId = authorityStableId;
            CurrencyStableId = currencyStableId;
            Balance = balance;
            LedgerSnapshot = ledgerSnapshot;
            Fingerprint = fingerprint;
        }

        public int SchemaVersion { get; }

        public string AuthorityStableId { get; }

        public string CurrencyStableId { get; }

        public long Balance { get; }

        public LedgerSnapshot<ScrapLedgerVocabulary> LedgerSnapshot { get; }

        public string Fingerprint { get; }

        public static ScrapSnapshotV1 CreateCanonical(
            StableId authorityStableId,
            StableId currencyStableId,
            long balance,
            LedgerSnapshot<ScrapLedgerVocabulary> ledgerSnapshot)
        {
            if (authorityStableId == null)
            {
                throw new ArgumentNullException(nameof(authorityStableId));
            }

            if (currencyStableId == null)
            {
                throw new ArgumentNullException(nameof(currencyStableId));
            }

            if (balance < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(balance));
            }

            if (ledgerSnapshot == null)
            {
                throw new ArgumentNullException(nameof(ledgerSnapshot));
            }

            string fingerprint = ComputeFingerprint(
                CurrentSchemaVersion,
                authorityStableId.ToString(),
                currencyStableId.ToString(),
                balance,
                ledgerSnapshot);
            return new ScrapSnapshotV1(
                CurrentSchemaVersion,
                authorityStableId.ToString(),
                currencyStableId.ToString(),
                balance,
                ledgerSnapshot,
                fingerprint);
        }

        public static string ComputeFingerprint(
            int schemaVersion,
            string authorityStableId,
            string currencyStableId,
            long balance,
            LedgerSnapshot<ScrapLedgerVocabulary> ledgerSnapshot)
        {
            string canonical = "format=scrap-snapshot-v1"
                + "\nschema_version=" + schemaVersion.ToString(CultureInfo.InvariantCulture)
                + "\nauthority_id=" + (authorityStableId ?? "none")
                + "\ncurrency_id=" + (currencyStableId ?? "none")
                + "\nbalance=" + balance.ToString(CultureInfo.InvariantCulture)
                + "\nledger_fingerprint="
                + (ledgerSnapshot == null ? "none" : ledgerSnapshot.Fingerprint);
            return ScrapFingerprintV1.Compute(canonical);
        }
    }

    public static class ScrapFingerprintV1
    {
        public static string Compute(string canonicalText)
        {
            if (canonicalText == null)
            {
                throw new ArgumentNullException(nameof(canonicalText));
            }

            byte[] bytes = Encoding.UTF8.GetBytes(canonicalText);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }

            var builder = new StringBuilder("sha256:", 71);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        public static bool IsCanonical(string fingerprint)
        {
            if (fingerprint == null || fingerprint.Length != 71
                || !fingerprint.StartsWith("sha256:", StringComparison.Ordinal))
            {
                return false;
            }

            for (int index = 7; index < fingerprint.Length; index++)
            {
                char value = fingerprint[index];
                if (!((value >= '0' && value <= '9') || (value >= 'a' && value <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
