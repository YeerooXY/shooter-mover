using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Domain.Common;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Durable downstream proof that exact collected reward identities were accepted into
    /// permanent character state. This is application history, not a second inventory.
    /// </summary>
    public sealed class CollectedRunRewardTransferReceiptSnapshot
    {
        private readonly ReadOnlyCollection<
            CollectedRunRewardTransferReceipt> receipts;
        private readonly ReadOnlyDictionary<StableId,
            CollectedRunRewardTransferReceipt> byOperation;
        private readonly ReadOnlyDictionary<StableId,
            CollectedRunRewardTransferReceipt> byReward;
        private readonly string canonicalText;

        public CollectedRunRewardTransferReceiptSnapshot(
            long revision,
            IEnumerable<CollectedRunRewardTransferReceipt>
                receipts)
        {
            if (revision < 0L)
                throw new ArgumentOutOfRangeException(
                    nameof(revision));
            var copy =
                new List<CollectedRunRewardTransferReceipt>(
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
                    CollectedRunRewardTransferReceipt>();
            var rewardIndex =
                new Dictionary<StableId,
                    CollectedRunRewardTransferReceipt>();
            for (int index = 0; index < copy.Count; index++)
            {
                CollectedRunRewardTransferReceipt receipt =
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
                    CollectedRunRewardTransferReceipt>(copy);
            byOperation =
                new ReadOnlyDictionary<StableId,
                    CollectedRunRewardTransferReceipt>(
                        operationIndex);
            byReward =
                new ReadOnlyDictionary<StableId,
                    CollectedRunRewardTransferReceipt>(
                        rewardIndex);

            var builder = new StringBuilder(
                "schema=collected-run-reward-transfer-receipt-snapshot-v1");
            CollectedRunRewardTransfer.Append(
                builder,
                "revision",
                Revision);
            CollectedRunRewardTransfer.Append(
                builder,
                "receipt-count",
                this.receipts.Count);
            for (int index = 0;
                index < this.receipts.Count;
                index++)
            {
                CollectedRunRewardTransfer.Append(
                    builder,
                    "receipt:"
                        + index.ToString(
                            CultureInfo.InvariantCulture),
                    this.receipts[index].Fingerprint);
            }
            canonicalText = builder.ToString();
            Fingerprint =
                CollectedRunRewardTransfer.Hash(
                    canonicalText);
        }

        public long Revision { get; }
        public IReadOnlyList<CollectedRunRewardTransferReceipt>
            Receipts
        {
            get { return receipts; }
        }
        public string Fingerprint { get; }

        public bool TryGetByOperation(
            StableId operationStableId,
            out CollectedRunRewardTransferReceipt receipt)
        {
            receipt = null;
            return operationStableId != null
                && byOperation.TryGetValue(
                    operationStableId,
                    out receipt);
        }

        public bool TryGetByReward(
            StableId rewardStableId,
            out CollectedRunRewardTransferReceipt receipt)
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

        public static CollectedRunRewardTransferReceiptSnapshot
            Empty()
        {
            return new CollectedRunRewardTransferReceiptSnapshot(
                0L,
                Array.Empty<
                    CollectedRunRewardTransferReceipt>());
        }
    }

    /// <summary>
    /// Owns only durable transfer receipts and their exact replay/overlap index. Money,
    /// scrap, equipment, holdings and strongbox state remain owned by their authorities.
    /// </summary>
    public sealed class CollectedRunRewardTransferReceiptState
    {
        private CollectedRunRewardTransferReceiptSnapshot
            snapshot;

        public CollectedRunRewardTransferReceiptState(
            CollectedRunRewardTransferReceiptSnapshot
                initialSnapshot = null)
        {
            snapshot = initialSnapshot
                ?? CollectedRunRewardTransferReceiptSnapshot
                    .Empty();
        }

        public CollectedRunRewardTransferReceiptSnapshot
            ExportSnapshot()
        {
            return snapshot;
        }

        public bool TryGetByOperation(
            StableId operationStableId,
            out CollectedRunRewardTransferReceipt receipt)
        {
            return snapshot.TryGetByOperation(
                operationStableId,
                out receipt);
        }

        public bool TryGetByReward(
            StableId rewardStableId,
            out CollectedRunRewardTransferReceipt receipt)
        {
            return snapshot.TryGetByReward(
                rewardStableId,
                out receipt);
        }

        public CollectedRunRewardTransferReceiptRecordResult
            Record(
                CollectedRunRewardTransferReceipt receipt)
        {
            if (receipt == null)
            {
                return new CollectedRunRewardTransferReceiptRecordResult(
                    CollectedRunRewardTransferStateStatus
                        .Rejected,
                    null,
                    "collected-run-transfer-receipt-null");
            }

            CollectedRunRewardTransferReceipt existing;
            if (snapshot.TryGetByOperation(
                receipt.OperationStableId,
                out existing))
            {
                return string.Equals(
                    existing.Fingerprint,
                    receipt.Fingerprint,
                    StringComparison.Ordinal)
                    ? new CollectedRunRewardTransferReceiptRecordResult(
                        CollectedRunRewardTransferStateStatus
                            .ExactReplay,
                        existing,
                        string.Empty)
                    : new CollectedRunRewardTransferReceiptRecordResult(
                        CollectedRunRewardTransferStateStatus
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
                    return new CollectedRunRewardTransferReceiptRecordResult(
                        CollectedRunRewardTransferStateStatus
                            .Rejected,
                        existing,
                        "collected-run-transfer-receipt-partial-overlap:"
                            + receipt.AppliedRewardStableIds[index]);
                }
            }

            var next =
                new List<CollectedRunRewardTransferReceipt>(
                    snapshot.Receipts);
            next.Add(receipt);
            snapshot =
                new CollectedRunRewardTransferReceiptSnapshot(
                    checked(snapshot.Revision + 1L),
                    next);
            return new CollectedRunRewardTransferReceiptRecordResult(
                CollectedRunRewardTransferStateStatus.Applied,
                receipt,
                string.Empty);
        }

        public SaveComponentApplyResult ImportSnapshot(
            CollectedRunRewardTransferReceiptSnapshot
                imported)
        {
            if (imported == null)
            {
                return SaveComponentApplyResult.Rejected(
                    "collected-run-transfer-receipt-snapshot-null");
            }
            snapshot = imported;
            return SaveComponentApplyResult.Applied();
        }
    }

    public static class
        CollectedRunRewardTransferReceiptSaveComponent
    {
        public const int SchemaVersion = 1;
        public const string ContentVersion =
            "collected-run-reward-transfer-receipts-explicit-v1";

        public static readonly StableId ComponentStableId =
            StableId.Parse(
                "save-component.collected-run-reward-transfer-receipts");

        public static SaveComponentDefinition Definition()
        {
            return new SaveComponentDefinition(
                ComponentStableId,
                SchemaVersion,
                ContentVersion,
                false,
                80);
        }

        public static ISaveComponentBridge CreateAdapter(
            CollectedRunRewardTransferReceiptState
                authority)
        {
            if (authority == null)
                throw new ArgumentNullException(nameof(authority));
            return new StateSnapshotSaveComponentBridge<
                CollectedRunRewardTransferReceiptSnapshot>(
                    Definition(),
                    new Codec(),
                    authority.ExportSnapshot,
                    Validate,
                    authority.ImportSnapshot);
        }

        private static SaveComponentValidationResult Validate(
            CollectedRunRewardTransferReceiptSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return SaveComponentValidationResult.Reject(
                    "collected-run-transfer-receipt-snapshot-null");
            }
            try
            {
                var rebuilt =
                    new CollectedRunRewardTransferReceiptSnapshot(
                        snapshot.Revision,
                        snapshot.Receipts);
                return string.Equals(
                    rebuilt.Fingerprint,
                    snapshot.Fingerprint,
                    StringComparison.Ordinal)
                    ? SaveComponentValidationResult.Accept()
                    : SaveComponentValidationResult.Reject(
                        "collected-run-transfer-receipt-snapshot-fingerprint-invalid");
            }
            catch (Exception exception)
            {
                return SaveComponentValidationResult.Reject(
                    "collected-run-transfer-receipt-snapshot-invalid:"
                        + exception.GetType().Name);
            }
        }

        private sealed class Codec :
            ISaveComponentPayloadCodec<
                CollectedRunRewardTransferReceiptSnapshot>
        {
            public string ContractId
            {
                get { return ContentVersion; }
            }

            public string Encode(
                CollectedRunRewardTransferReceiptSnapshot
                    snapshot)
            {
                SaveComponentValidationResult validation =
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
                out CollectedRunRewardTransferReceiptSnapshot
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
                            CollectedRunRewardTransferReceipt>(
                                count);
                    for (int index = 0;
                        index < count;
                        index++)
                    {
                        CollectedRunRewardTransferReceipt
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
                        new CollectedRunRewardTransferReceiptSnapshot(
                            revision,
                            receipts);
                    SaveComponentValidationResult validation =
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

            public SaveComponentValidationResult Validate(
                CollectedRunRewardTransferReceiptSnapshot
                    snapshot)
            {
                return CollectedRunRewardTransferReceiptSaveComponent
                    .Validate(snapshot);
            }

            private static void AppendReceipt(
                StringBuilder builder,
                CollectedRunRewardTransferReceipt receipt)
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
                out CollectedRunRewardTransferReceipt
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
                    new CollectedRunRewardTransferReceipt(
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
