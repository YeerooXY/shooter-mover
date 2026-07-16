using System;
using System.Globalization;
using ShooterMover.Contracts;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Economy
{
    public enum EconomyTransactionOperationV1
    {
        Credit = 1,
        Debit = 2,
        AddStack = 3,
        RemoveStack = 4,
        AddUnique = 5,
        RemoveUnique = 6,
    }

    public enum EconomyResourceKindV1
    {
        Currency = 1,
        Item = 2,
        Strongbox = 3,
        EquipmentReference = 4,
    }

    /// <summary>
    /// Immutable generic transaction command consumed later by money, scrap, and
    /// holdings authorities. It defines identity and payload but mutates nothing.
    /// </summary>
    public sealed class EconomyTransactionCommandV1 : IEquatable<EconomyTransactionCommandV1>
    {
        private readonly string canonicalText;
        private readonly string payloadFingerprint;

        private EconomyTransactionCommandV1(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            EconomyTransactionOperationV1 operation,
            EconomyResourceKindV1 resourceKind,
            StableId resourceStableId,
            StableId instanceStableId,
            long quantity,
            long? expectedSequence)
        {
            this.TransactionStableId = RewardContractFormatV1.RequireStableId(
                transactionStableId,
                nameof(transactionStableId));
            this.OperationStableId = RewardContractFormatV1.RequireStableId(
                operationStableId,
                nameof(operationStableId));
            this.AuthorityStableId = RewardContractFormatV1.RequireStableId(
                authorityStableId,
                nameof(authorityStableId));
            RewardContractFormatV1.RequireDefinedEnum(operation, nameof(operation));
            RewardContractFormatV1.RequireDefinedEnum(resourceKind, nameof(resourceKind));
            this.Operation = operation;
            this.ResourceKind = resourceKind;
            this.ResourceStableId = RewardContractFormatV1.RequireStableId(
                resourceStableId,
                nameof(resourceStableId));
            if (quantity < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    quantity,
                    "Economy transaction quantities must be positive.");
            }

            if (expectedSequence.HasValue && expectedSequence.Value < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedSequence),
                    expectedSequence,
                    "Expected sequence must be non-negative when supplied.");
            }

            bool isUniqueOperation = operation == EconomyTransactionOperationV1.AddUnique
                || operation == EconomyTransactionOperationV1.RemoveUnique;
            if (isUniqueOperation && instanceStableId == null)
            {
                throw new ArgumentNullException(
                    nameof(instanceStableId),
                    "Unique holdings transactions require an instance StableId.");
            }

            if (!isUniqueOperation && instanceStableId != null)
            {
                throw new ArgumentException(
                    "Non-unique economy transactions must not carry an instance StableId.",
                    nameof(instanceStableId));
            }

            if (isUniqueOperation && quantity != 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    quantity,
                    "Unique holdings transactions must use quantity one.");
            }

            bool isCurrencyOperation = operation == EconomyTransactionOperationV1.Credit
                || operation == EconomyTransactionOperationV1.Debit;
            if ((resourceKind == EconomyResourceKindV1.Currency) != isCurrencyOperation)
            {
                throw new ArgumentException(
                    "Currency resources require credit/debit operations and non-currency resources require holdings operations.");
            }

            bool isStackOperation = operation == EconomyTransactionOperationV1.AddStack
                || operation == EconomyTransactionOperationV1.RemoveStack;
            if (resourceKind == EconomyResourceKindV1.Item && !isStackOperation)
            {
                throw new ArgumentException("Item resources require stack operations.");
            }

            if ((resourceKind == EconomyResourceKindV1.Strongbox
                || resourceKind == EconomyResourceKindV1.EquipmentReference)
                && !isUniqueOperation)
            {
                throw new ArgumentException(
                    "Strongbox and equipment-reference resources require unique operations.");
            }

            this.InstanceStableId = instanceStableId;
            this.Quantity = quantity;
            this.ExpectedSequence = expectedSequence;
            this.canonicalText = "transaction_stable_id="
                + this.TransactionStableId
                + "\noperation_stable_id="
                + this.OperationStableId
                + "\nauthority_stable_id="
                + this.AuthorityStableId
                + "\noperation="
                + ((int)this.Operation).ToString(CultureInfo.InvariantCulture)
                + "\nresource_kind="
                + ((int)this.ResourceKind).ToString(CultureInfo.InvariantCulture)
                + "\nresource_stable_id="
                + this.ResourceStableId
                + "\ninstance_stable_id="
                + (this.InstanceStableId == null ? "null" : this.InstanceStableId.ToString())
                + "\nquantity="
                + this.Quantity.ToString(CultureInfo.InvariantCulture)
                + "\nexpected_sequence="
                + (this.ExpectedSequence.HasValue
                    ? this.ExpectedSequence.Value.ToString(CultureInfo.InvariantCulture)
                    : "none");
            this.payloadFingerprint = RewardContractFormatV1.Fingerprint(this.canonicalText);
        }

        public StableId TransactionStableId { get; }

        public StableId OperationStableId { get; }

        public StableId AuthorityStableId { get; }

        public EconomyTransactionOperationV1 Operation { get; }

        public EconomyResourceKindV1 ResourceKind { get; }

        /// <summary>
        /// Currency, item, strongbox definition, or equipment definition identifier.
        /// </summary>
        public StableId ResourceStableId { get; }

        public StableId InstanceStableId { get; }

        public long Quantity { get; }

        public long? ExpectedSequence { get; }

        public string PayloadFingerprint
        {
            get { return this.payloadFingerprint; }
        }

        public static EconomyTransactionCommandV1 Create(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            EconomyTransactionOperationV1 operation,
            EconomyResourceKindV1 resourceKind,
            StableId resourceStableId,
            StableId instanceStableId,
            long quantity,
            long? expectedSequence)
        {
            return new EconomyTransactionCommandV1(
                transactionStableId,
                operationStableId,
                authorityStableId,
                operation,
                resourceKind,
                resourceStableId,
                instanceStableId,
                quantity,
                expectedSequence);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(EconomyTransactionCommandV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as EconomyTransactionCommandV1);
        }

        public override int GetHashCode()
        {
            return RewardContractFormatV1.DeterministicHash(this.canonicalText);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }

    public enum EconomyTransactionStatusV1
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        InvalidRequest = 4,
        InsufficientValue = 5,
        InsufficientCapacity = 6,
        ExpectedSequenceConflict = 7,
    }

    public enum EconomyTransactionIdentityComparisonV1
    {
        DistinctTransaction = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
    }

    /// <summary>
    /// Pure duplicate classification. Authorities later persist prior fingerprints;
    /// this helper only defines the comparison semantics.
    /// </summary>
    public static class EconomyTransactionIdentityV1
    {
        public static EconomyTransactionIdentityComparisonV1 Classify(
            EconomyTransactionCommandV1 existingCommand,
            EconomyTransactionCommandV1 incomingCommand)
        {
            if (existingCommand == null)
            {
                throw new ArgumentNullException(nameof(existingCommand));
            }

            if (incomingCommand == null)
            {
                throw new ArgumentNullException(nameof(incomingCommand));
            }

            if (existingCommand.TransactionStableId != incomingCommand.TransactionStableId)
            {
                return EconomyTransactionIdentityComparisonV1.DistinctTransaction;
            }

            if (string.Equals(
                existingCommand.PayloadFingerprint,
                incomingCommand.PayloadFingerprint,
                StringComparison.Ordinal))
            {
                return EconomyTransactionIdentityComparisonV1.ExactDuplicateNoChange;
            }

            return EconomyTransactionIdentityComparisonV1.ConflictingDuplicate;
        }
    }

    /// <summary>
    /// Immutable duplicate-safe transaction result vocabulary. No ledger or authority
    /// behavior is implemented here.
    /// </summary>
    public sealed class EconomyTransactionResultV1 : IEquatable<EconomyTransactionResultV1>
    {
        private readonly string canonicalText;
        private readonly string fingerprint;

        private EconomyTransactionResultV1(
            StableId transactionStableId,
            EconomyTransactionStatusV1 status,
            string commandFingerprint,
            long previousSequence,
            long currentSequence,
            long resultingValue)
        {
            this.TransactionStableId = RewardContractFormatV1.RequireStableId(
                transactionStableId,
                nameof(transactionStableId));
            RewardContractFormatV1.RequireDefinedEnum(status, nameof(status));
            this.Status = status;
            this.CommandFingerprint = RewardContractFormatV1.RequireFingerprint(
                commandFingerprint,
                nameof(commandFingerprint));
            if (previousSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(previousSequence));
            }

            if (currentSequence < previousSequence)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentSequence),
                    currentSequence,
                    "Current sequence must not precede previous sequence.");
            }

            if (resultingValue < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resultingValue),
                    resultingValue,
                    "Resulting value or quantity must be non-negative.");
            }

            bool changed = status == EconomyTransactionStatusV1.Applied;
            if (changed && currentSequence != previousSequence + 1L)
            {
                throw new ArgumentException(
                    "Applied transactions must advance sequence by exactly one.");
            }

            if (!changed && currentSequence != previousSequence)
            {
                throw new ArgumentException(
                    "Rejected and duplicate transactions must not advance sequence.");
            }

            this.PreviousSequence = previousSequence;
            this.CurrentSequence = currentSequence;
            this.ResultingValue = resultingValue;
            this.canonicalText = "transaction_stable_id="
                + this.TransactionStableId
                + "\nstatus="
                + ((int)this.Status).ToString(CultureInfo.InvariantCulture)
                + "\ncommand_fingerprint="
                + this.CommandFingerprint
                + "\nprevious_sequence="
                + this.PreviousSequence.ToString(CultureInfo.InvariantCulture)
                + "\ncurrent_sequence="
                + this.CurrentSequence.ToString(CultureInfo.InvariantCulture)
                + "\nresulting_value="
                + this.ResultingValue.ToString(CultureInfo.InvariantCulture);
            this.fingerprint = RewardContractFormatV1.Fingerprint(this.canonicalText);
        }

        public StableId TransactionStableId { get; }

        public EconomyTransactionStatusV1 Status { get; }

        public string CommandFingerprint { get; }

        public long PreviousSequence { get; }

        public long CurrentSequence { get; }

        public long ResultingValue { get; }

        public string Fingerprint
        {
            get { return this.fingerprint; }
        }

        public static EconomyTransactionResultV1 Create(
            StableId transactionStableId,
            EconomyTransactionStatusV1 status,
            string commandFingerprint,
            long previousSequence,
            long currentSequence,
            long resultingValue)
        {
            return new EconomyTransactionResultV1(
                transactionStableId,
                status,
                commandFingerprint,
                previousSequence,
                currentSequence,
                resultingValue);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(EconomyTransactionResultV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as EconomyTransactionResultV1);
        }

        public override int GetHashCode()
        {
            return RewardContractFormatV1.DeterministicHash(this.canonicalText);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }
}
