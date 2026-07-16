using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Contracts.Economy;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Contracts.Holdings
{
    public enum PlayerHoldingsMutationStatusV1
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        InvalidRequest = 4,
        WrongAuthority = 5,
        WrongRewardType = 6,
        TypeMismatch = 7,
        UniqueInstanceCollision = 8,
        MissingItem = 9,
        InsufficientValue = 10,
        InsufficientCapacity = 11,
        EquipmentValidationRejected = 12,
        ExpectedSequenceConflict = 13,
        ArithmeticOverflow = 14,
    }

    public enum PlayerHoldingsImportStatusV1
    {
        Imported = 1,
        InvalidSnapshot = 2,
        UnsupportedSchemaVersion = 3,
        FingerprintMismatch = 4,
    }

    /// <summary>
    /// Immutable typed command accepted by the sole player-holdings authority.
    /// The wrapped economy command supplies transaction/operation identity while
    /// provenance supplies durable grant/source identity.
    /// </summary>
    public sealed class PlayerHoldingsCommandV1 : IEquatable<PlayerHoldingsCommandV1>
    {
        private readonly string canonicalText;

        private PlayerHoldingsCommandV1(
            EconomyTransactionCommandV1 transaction,
            RewardGrantKindV1 rewardKind,
            HoldingProvenanceV1 provenance,
            EquipmentInstance equipmentInstance)
        {
            Transaction = transaction
                ?? throw new ArgumentNullException(nameof(transaction));
            Provenance = provenance
                ?? throw new ArgumentNullException(nameof(provenance));

            if (!Enum.IsDefined(typeof(RewardGrantKindV1), rewardKind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rewardKind),
                    rewardKind,
                    "Reward kind must be defined.");
            }

            if (rewardKind == RewardGrantKindV1.EquipmentReference)
            {
                if (transaction.Operation == EconomyTransactionOperationV1.AddUnique)
                {
                    EquipmentInstance = equipmentInstance
                        ?? throw new ArgumentNullException(nameof(equipmentInstance));
                }
                else if (equipmentInstance != null)
                {
                    throw new ArgumentException(
                        "Equipment-removal commands must not duplicate the stored immutable equipment payload.",
                        nameof(equipmentInstance));
                }
            }
            else if (equipmentInstance != null)
            {
                throw new ArgumentException(
                    "Only equipment-add commands may carry an equipment instance.",
                    nameof(equipmentInstance));
            }

            RewardKind = rewardKind;

            var builder = new StringBuilder();
            HoldingsCanonicalV1.AppendToken(
                builder,
                "transaction",
                Transaction.ToCanonicalString());
            HoldingsCanonicalV1.AppendToken(
                builder,
                "reward_kind",
                ((int)RewardKind).ToString(CultureInfo.InvariantCulture));
            HoldingsCanonicalV1.AppendToken(
                builder,
                "provenance",
                Provenance.ToCanonicalString());
            HoldingsCanonicalV1.AppendToken(
                builder,
                "equipment_instance",
                EquipmentInstance == null
                    ? "none"
                    : EquipmentInstance.ToCanonicalString());
            canonicalText = builder.ToString();
            PayloadFingerprint = HoldingsCanonicalV1.ComputeSha256(canonicalText);
        }

        public EconomyTransactionCommandV1 Transaction { get; }

        public RewardGrantKindV1 RewardKind { get; }

        public HoldingProvenanceV1 Provenance { get; }

        public EquipmentInstance EquipmentInstance { get; }

        public string PayloadFingerprint { get; }

        public static PlayerHoldingsCommandV1 Create(
            EconomyTransactionCommandV1 transaction,
            RewardGrantKindV1 rewardKind,
            HoldingProvenanceV1 provenance,
            EquipmentInstance equipmentInstance = null)
        {
            return new PlayerHoldingsCommandV1(
                transaction,
                rewardKind,
                provenance,
                equipmentInstance);
        }

        public static PlayerHoldingsCommandV1 AddEquipment(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            EquipmentInstance equipmentInstance,
            HoldingProvenanceV1 provenance,
            long? expectedSequence = null)
        {
            if (equipmentInstance == null)
            {
                throw new ArgumentNullException(nameof(equipmentInstance));
            }

            return Create(
                EconomyTransactionCommandV1.Create(
                    transactionStableId,
                    operationStableId,
                    authorityStableId,
                    EconomyTransactionOperationV1.AddUnique,
                    EconomyResourceKindV1.EquipmentReference,
                    equipmentInstance.DefinitionId,
                    equipmentInstance.InstanceId,
                    1L,
                    expectedSequence),
                RewardGrantKindV1.EquipmentReference,
                provenance,
                equipmentInstance);
        }

        public static PlayerHoldingsCommandV1 RemoveEquipment(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            StableId equipmentDefinitionStableId,
            StableId equipmentInstanceStableId,
            HoldingProvenanceV1 provenance,
            long? expectedSequence = null)
        {
            return Create(
                EconomyTransactionCommandV1.Create(
                    transactionStableId,
                    operationStableId,
                    authorityStableId,
                    EconomyTransactionOperationV1.RemoveUnique,
                    EconomyResourceKindV1.EquipmentReference,
                    equipmentDefinitionStableId,
                    equipmentInstanceStableId,
                    1L,
                    expectedSequence),
                RewardGrantKindV1.EquipmentReference,
                provenance);
        }

        public static PlayerHoldingsCommandV1 AddStrongbox(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            StableId strongboxDefinitionStableId,
            StableId strongboxInstanceStableId,
            HoldingProvenanceV1 provenance,
            long? expectedSequence = null)
        {
            return Create(
                EconomyTransactionCommandV1.Create(
                    transactionStableId,
                    operationStableId,
                    authorityStableId,
                    EconomyTransactionOperationV1.AddUnique,
                    EconomyResourceKindV1.Strongbox,
                    strongboxDefinitionStableId,
                    strongboxInstanceStableId,
                    1L,
                    expectedSequence),
                RewardGrantKindV1.Strongbox,
                provenance);
        }

        public static PlayerHoldingsCommandV1 RemoveStrongbox(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            StableId strongboxDefinitionStableId,
            StableId strongboxInstanceStableId,
            HoldingProvenanceV1 provenance,
            long? expectedSequence = null)
        {
            return Create(
                EconomyTransactionCommandV1.Create(
                    transactionStableId,
                    operationStableId,
                    authorityStableId,
                    EconomyTransactionOperationV1.RemoveUnique,
                    EconomyResourceKindV1.Strongbox,
                    strongboxDefinitionStableId,
                    strongboxInstanceStableId,
                    1L,
                    expectedSequence),
                RewardGrantKindV1.Strongbox,
                provenance);
        }

        public static PlayerHoldingsCommandV1 AddStack(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            RewardGrantKindV1 rewardKind,
            StableId itemStableId,
            long quantity,
            HoldingProvenanceV1 provenance,
            long? expectedSequence = null)
        {
            return Create(
                EconomyTransactionCommandV1.Create(
                    transactionStableId,
                    operationStableId,
                    authorityStableId,
                    EconomyTransactionOperationV1.AddStack,
                    EconomyResourceKindV1.Item,
                    itemStableId,
                    null,
                    quantity,
                    expectedSequence),
                rewardKind,
                provenance);
        }

        public static PlayerHoldingsCommandV1 RemoveStack(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            RewardGrantKindV1 rewardKind,
            StableId itemStableId,
            long quantity,
            HoldingProvenanceV1 provenance,
            long? expectedSequence = null)
        {
            return Create(
                EconomyTransactionCommandV1.Create(
                    transactionStableId,
                    operationStableId,
                    authorityStableId,
                    EconomyTransactionOperationV1.RemoveStack,
                    EconomyResourceKindV1.Item,
                    itemStableId,
                    null,
                    quantity,
                    expectedSequence),
                rewardKind,
                provenance);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(PlayerHoldingsCommandV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerHoldingsCommandV1);
        }

        public override int GetHashCode()
        {
            return HoldingsCanonicalV1.DeterministicHash(canonicalText);
        }

        public override string ToString()
        {
            return canonicalText;
        }
    }

}
