using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Durable downstream proof that exact collected reward identities were accepted into
    /// permanent character state. This is application history, not a second inventory.
    /// </summary>
    public sealed class RewardClaimTransferReceiptSnapshot
    {
        private readonly ReadOnlyCollection<
            RewardClaimTransferReceipt> receipts;
        private readonly ReadOnlyDictionary<StableId,
            RewardClaimTransferReceipt> byOperation;
        private readonly ReadOnlyDictionary<StableId,
            RewardClaimTransferReceipt> byReward;
        private readonly string canonicalText;

        public RewardClaimTransferReceiptSnapshot(
            long revision,
            IEnumerable<RewardClaimTransferReceipt>
                receipts)
        {
            if (revision < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(revision));
            var copy =
                new List<RewardClaimTransferReceipt>(
                    receipts
                    ?? throw new ArgumentNullException(
                        nameof(receipts)));
            if (copy.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Transfer receipt snapshots cannot contain null.",
                    nameof(receipts));
            }
            copy.Sort((left, right) =>
                string.CompareOrdinal(
                    left.OperationStableId.ToString(),
                    right.OperationStableId.ToString()));

            var operationIndex =
                new Dictionary<StableId,
                    RewardClaimTransferReceipt>();
            var rewardIndex =
                new Dictionary<StableId,
                    RewardClaimTransferReceipt>();
            for (int index = 0; index < copy.Count; index++)
            {
                RewardClaimTransferReceipt receipt =
                    copy[index];
                if (operationIndex.ContainsKey(
                    receipt.OperationStableId))
                {
                    throw new ArgumentException(
                        "A transfer receipt operation identity cannot appear twice.",
                        nameof(receipts));
                }
                operationIndex.Add(
                    receipt.OperationStableId,
                    receipt);
                for (int rewardIndexValue = 0;
                    rewardIndexValue
                        < receipt.AppliedRewardStableIds.Count;
                    rewardIndexValue++)
                {
                    StableId rewardId =
                        receipt.AppliedRewardStableIds[
                            rewardIndexValue];
                    if (rewardIndex.ContainsKey(rewardId))
                    {
                        throw new ArgumentException(
                            "A permanently transferred reward identity cannot appear in two receipts.",
                            nameof(receipts));
                    }
                    rewardIndex.Add(rewardId, receipt);
                }
            }

            Revision = revision;
            this.receipts =
                new ReadOnlyCollection<
                    RewardClaimTransferReceipt>(copy);
            byOperation =
                new ReadOnlyDictionary<StableId,
                    RewardClaimTransferReceipt>(
                        operationIndex);
            byReward =
                new ReadOnlyDictionary<StableId,
                    RewardClaimTransferReceipt>(
                        rewardIndex);

            var builder = new StringBuilder(
                "schema=reward-claim-transfer-receipt-snapshot-v1");
            RewardClaimTransfer.Append(
                builder,
                "revision",
                Revision);
            RewardClaimTransfer.Append(
                builder,
                "receipt-count",
                this.receipts.Count);
            for (int index = 0;
                index < this.receipts.Count;
                index++)
            {
                RewardClaimTransfer.Append(
                    builder,
                    "receipt:"
                        + index.ToString(
                            CultureInfo.InvariantCulture),
                    this.receipts[index].Fingerprint);
            }
            canonicalText = builder.ToString();
            Fingerprint =
                RewardClaimTransfer.Hash(
                    canonicalText);
        }

        public long Revision { get; }
        public IReadOnlyList<RewardClaimTransferReceipt>
            Receipts
        {
            get { return receipts; }
        }
        public string Fingerprint { get; }

        public bool TryGetByOperation(
            StableId operationStableId,
            out RewardClaimTransferReceipt receipt)
        {
            receipt = null;
            return operationStableId != null
                && byOperation.TryGetValue(
                    operationStableId,
                    out receipt);
        }

        public bool TryGetByReward(
            StableId rewardStableId,
            out RewardClaimTransferReceipt receipt)
        {
            receipt = null;
            return rewardStableId != null
                && byReward.TryGetValue(
                    rewardStableId,
                    out receipt);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public static RewardClaimTransferReceiptSnapshot
            Empty()
        {
            return new RewardClaimTransferReceiptSnapshot(
                0L,
                Array.Empty<
                    RewardClaimTransferReceipt>());
        }
    }

    /// <summary>
    /// Owns only durable transfer receipts and their exact replay/overlap index. Money,
    /// scrap, equipment, holdings and strongbox state remain owned by their authorities.
    /// </summary>
    public sealed class RewardClaimTransferReceiptState
    {
        private RewardClaimTransferReceiptSnapshot
            snapshot;

        public RewardClaimTransferReceiptState(
            RewardClaimTransferReceiptSnapshot
                initialSnapshot = null)
        {
            snapshot = initialSnapshot
                ?? RewardClaimTransferReceiptSnapshot
                    .Empty();
        }

        public RewardClaimTransferReceiptSnapshot
            ExportSnapshot()
        {
            return snapshot;
        }

        public bool TryGetByOperation(
            StableId operationStableId,
            out RewardClaimTransferReceipt receipt)
        {
            return snapshot.TryGetByOperation(
                operationStableId,
                out receipt);
        }

        public bool TryGetByReward(
            StableId rewardStableId,
            out RewardClaimTransferReceipt receipt)
        {
            return snapshot.TryGetByReward(
                rewardStableId,
                out receipt);
        }

        public RewardClaimTransferReceiptRecordResult
            Record(
                RewardClaimTransferReceipt receipt)
        {
            if (receipt == null)
            {
                return new RewardClaimTransferReceiptRecordResult(
                    RewardClaimTransferStateStatus
                        .Rejected,
                    null,
                    "collected-run-transfer-receipt-null");
            }

            RewardClaimTransferReceipt existing;
            if (snapshot.TryGetByOperation(
                receipt.OperationStableId,
                out existing))
            {
                return string.Equals(
                    existing.Fingerprint,
                    receipt.Fingerprint,
                    StringComparison.Ordinal)
                    ? new RewardClaimTransferReceiptRecordResult(
                        RewardClaimTransferStateStatus
                            .ExactReplay,
                        existing,
                        string.Empty)
                    : new RewardClaimTransferReceiptRecordResult(
                        RewardClaimTransferStateStatus
                            .ConflictingDuplicate,
                        existing,
                        "collected-run-transfer-receipt-operation-conflict");
            }

            for (int index = 0;
                index < receipt.AppliedRewardStableIds.Count;
                index++)
            {
                if (snapshot.TryGetByReward(
                    receipt.AppliedRewardStableIds[index],
                    out existing))
                {
                    return new RewardClaimTransferReceiptRecordResult(
                        RewardClaimTransferStateStatus
                            .Rejected,
                        existing,
                        "collected-run-transfer-receipt-partial-overlap:"
                            + receipt.AppliedRewardStableIds[index]);
                }
            }

            var next =
                new List<RewardClaimTransferReceipt>(
                    snapshot.Receipts);
            next.Add(receipt);
            snapshot =
                new RewardClaimTransferReceiptSnapshot(
                    checked(snapshot.Revision + 1L),
                    next);
            return new RewardClaimTransferReceiptRecordResult(
                RewardClaimTransferStateStatus.Applied,
                receipt,
                string.Empty);
        }

        public SavePartApplyResult ImportSnapshot(
            RewardClaimTransferReceiptSnapshot
                imported)
        {
            if (imported == null)
            {
                return SavePartApplyResult.Rejected(
                    "collected-run-transfer-receipt-snapshot-null");
            }
            snapshot = imported;
            return SavePartApplyResult.Applied();
        }
    }

    public static class
        RewardClaimTransferReceiptSavePart
    {
        public const int SchemaVersion = 1;
        public const string ContentVersion =
            "reward-claim-transfer-receipts-explicit-v1";

        public static readonly StableId ComponentStableId =
            StableId.Parse(
                "save-part.reward-claim-transfer-receipts");

        public static SavePartDefinition Definition()
        {
            return new SavePartDefinition(
                ComponentStableId,
                SchemaVersion,
                ContentVersion,
                false,
                80);
        }

        public static ISavePart CreateAdapter(
            RewardClaimTransferReceiptState
                authority)
        {
            if (authority == null)
                throw new ArgumentNullException(nameof(authority));
            return new SnapshotSavePart<
                RewardClaimTransferReceiptSnapshot>(
                    Definition(),
                    new Codec(),
                    authority.ExportSnapshot,
                    Validate,
                    authority.ImportSnapshot);
        }

        private static SavePartValidationResult Validate(
            RewardClaimTransferReceiptSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SavePartValidationResult.Reject(
                    "collected-run-transfer-receipt-snapshot-null");
            }
            try
            {
                var rebuilt =
                    new RewardClaimTransferReceiptSnapshot(
                        snapshot.Revision,
                        snapshot.Receipts);
                return string.Equals(
                    rebuilt.Fingerprint,
                    snapshot.Fingerprint,
                    StringComparison.Ordinal)
                    ? SavePartValidationResult.Accept()
                    : SavePartValidationResult.Reject(
                        "collected-run-transfer-receipt-snapshot-fingerprint-invalid");
            }
            catch (Exception exception)
            {
                return SavePartValidationResult.Reject(
                    "collected-run-transfer-receipt-snapshot-invalid:"
                        + exception.GetType().Name);
            }
        }

        private sealed class Codec :
            ISavePartFormat<
                RewardClaimTransferReceiptSnapshot>
        {
            public string ContractId
            {
                get { return ContentVersion; }
            }

            public string Encode(
                RewardClaimTransferReceiptSnapshot
                    snapshot)
            {
                SavePartValidationResult validation =
                    Validate(snapshot);
                if (!validation.Succeeded)
                {
                    throw new InvalidOperationException(
                        validation.RejectionCode);
                }

                var builder = new StringBuilder();
                builder.Append(ContentVersion);
                builder.Append('\n');
                builder.Append(
                    snapshot.Revision.ToString(
                        CultureInfo.InvariantCulture));
                builder.Append('\n');
                builder.Append(
                    snapshot.Receipts.Count.ToString(
                        CultureInfo.InvariantCulture));
                builder.Append('\n');
                for (int index = 0;
                    index < snapshot.Receipts.Count;
                    index++)
                {
                    AppendReceipt(
                        builder,
                        snapshot.Receipts[index]);
                    builder.Append('\n');
                }
                return builder.ToString();
            }

            public bool TryDecode(
                string canonicalPayload,
                out RewardClaimTransferReceiptSnapshot
                    snapshot,
                out string rejectionCode)
            {
                snapshot = null;
                rejectionCode = string.Empty;
                try
                {
                    if (canonicalPayload == null)
                    {
                        rejectionCode =
                            "collected-run-transfer-receipt-payload-null";
                        return false;
                    }
                    string[] lines = canonicalPayload.Replace(
                        "\r\n",
                        "\n").Split('\n');
                    int lineCount = lines.Length;
                    while (lineCount > 0
                        && lines[lineCount - 1].Length == 0)
                    {
                        lineCount--;
                    }
                    if (lineCount < 3
                        || !string.Equals(
                            lines[0],
                            ContentVersion,
                            StringComparison.Ordinal))
                    {
                        rejectionCode =
                            "collected-run-transfer-receipt-payload-version-invalid";
                        return false;
                    }

                    long revision;
                    int count;
                    if (!long.TryParse(
                        lines[1],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture,
                        out revision)
                        || revision < 0L
                        || !int.TryParse(
                            lines[2],
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out count)
                        || count < 0
                        || lineCount != count + 3)
                    {
                        rejectionCode =
                            "collected-run-transfer-receipt-payload-header-invalid";
                        return false;
                    }

                    var receipts =
                        new List<
                            RewardClaimTransferReceipt>(
                                count);
                    for (int index = 0;
                        index < count;
                        index++)
                    {
                        RewardClaimTransferReceipt
                            receipt;
                        string itemError;
                        if (!TryParseReceipt(
                            lines[index + 3],
                            out receipt,
                            out itemError))
                        {
                            rejectionCode = itemError;
                            return false;
                        }
                        receipts.Add(receipt);
                    }

                    snapshot =
                        new RewardClaimTransferReceiptSnapshot(
                            revision,
                            receipts);
                    SavePartValidationResult validation =
                        Validate(snapshot);
                    if (!validation.Succeeded)
                    {
                        rejectionCode =
                            validation.RejectionCode;
                        snapshot = null;
                        return false;
                    }
                    return true;
                }
                catch (Exception exception)
                {
                    rejectionCode =
                        "collected-run-transfer-receipt-payload-invalid:"
                        + exception.GetType().Name;
                    snapshot = null;
                    return false;
                }
            }

            public SavePartValidationResult Validate(
                RewardClaimTransferReceiptSnapshot
                    snapshot)
            {
                return RewardClaimTransferReceiptSavePart
                    .Validate(snapshot);
            }

            private static void AppendReceipt(
                StringBuilder builder,
                RewardClaimTransferReceipt receipt)
            {
                var fields = new List<string>
                {
                    EncodeText(
                        receipt.OperationStableId.ToString()),
                    EncodeText(receipt.BatchFingerprint),
                    EncodeText(receipt.RunStableId.ToString()),
                    receipt.AcceptedLifecycleGeneration
                        .ToString(CultureInfo.InvariantCulture),
                    EncodeText(
                        receipt.MissionResultStableId.ToString()),
                    EncodeText(
                        receipt.MissionResultFingerprint),
                    EncodeText(
                        receipt.SelectedCharacterStableId
                            .ToString()),
                    receipt.AppliedRewardStableIds.Count
                        .ToString(CultureInfo.InvariantCulture),
                };
                for (int index = 0;
                    index
                        < receipt.AppliedRewardStableIds.Count;
                    index++)
                {
                    fields.Add(EncodeText(
                        receipt.AppliedRewardStableIds[index]
                            .ToString()));
                }
                fields.Add(
                    receipt.AuthorityFingerprints.Count
                        .ToString(CultureInfo.InvariantCulture));
                foreach (KeyValuePair<string, string> pair in
                    receipt.AuthorityFingerprints)
                {
                    fields.Add(EncodeText(pair.Key));
                    fields.Add(EncodeText(pair.Value));
                }
                fields.Add(EncodeText(receipt.Fingerprint));
                builder.Append(string.Join("|", fields));
            }

            private static bool TryParseReceipt(
                string line,
                out RewardClaimTransferReceipt
                    receipt,
                out string rejectionCode)
            {
                receipt = null;
                rejectionCode =
                    "collected-run-transfer-receipt-record-invalid";
                string[] fields = (line ?? string.Empty)
                    .Split('|');
                if (fields.Length < 10)
                    return false;

                int cursor = 0;
                StableId operation =
                    StableId.Parse(DecodeText(
                        fields[cursor++]));
                string batch = DecodeText(fields[cursor++]);
                StableId run =
                    StableId.Parse(DecodeText(
                        fields[cursor++]));
                long lifecycle;
                if (!long.TryParse(
                    fields[cursor++],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out lifecycle)
                    || lifecycle < 0L)
                {
                    return false;
                }
                StableId resultId =
                    StableId.Parse(DecodeText(
                        fields[cursor++]));
                string resultFingerprint =
                    DecodeText(fields[cursor++]);
                StableId character =
                    StableId.Parse(DecodeText(
                        fields[cursor++]));

                int rewardCount;
                if (!int.TryParse(
                    fields[cursor++],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out rewardCount)
                    || rewardCount < 0
                    || fields.Length
                        < cursor + rewardCount + 2)
                {
                    return false;
                }
                var rewardIds =
                    new List<StableId>(rewardCount);
                for (int index = 0;
                    index < rewardCount;
                    index++)
                {
                    rewardIds.Add(StableId.Parse(
                        DecodeText(fields[cursor++])));
                }

                int authorityCount;
                if (!int.TryParse(
                    fields[cursor++],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out authorityCount)
                    || authorityCount < 0
                    || fields.Length
                        != cursor
                            + (authorityCount * 2)
                            + 1)
                {
                    return false;
                }
                var authorityFingerprints =
                    new Dictionary<string, string>(
                        StringComparer.Ordinal);
                for (int index = 0;
                    index < authorityCount;
                    index++)
                {
                    authorityFingerprints.Add(
                        DecodeText(fields[cursor++]),
                        DecodeText(fields[cursor++]));
                }
                string expectedFingerprint =
                    DecodeText(fields[cursor]);

                var parsed =
                    new RewardClaimTransferReceipt(
                        operation,
                        batch,
                        run,
                        lifecycle,
                        resultId,
                        resultFingerprint,
                        character,
                        rewardIds,
                        authorityFingerprints);
                if (!string.Equals(
                    parsed.Fingerprint,
                    expectedFingerprint,
                    StringComparison.Ordinal))
                {
                    rejectionCode =
                        "collected-run-transfer-receipt-record-fingerprint-invalid";
                    return false;
                }
                receipt = parsed;
                rejectionCode = string.Empty;
                return true;
            }

            private static string EncodeText(string value)
            {
                return Convert.ToBase64String(
                    Encoding.UTF8.GetBytes(
                        value ?? string.Empty));
            }

            private static string DecodeText(string value)
            {
                return Encoding.UTF8.GetString(
                    Convert.FromBase64String(
                        value ?? string.Empty));
            }
        }
    }
}
