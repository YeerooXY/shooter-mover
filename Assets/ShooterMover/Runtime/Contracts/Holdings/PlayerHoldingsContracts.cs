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
    public enum PlayerHoldingsMutationStatus
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

    public enum PlayerHoldingsImportStatus
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
    public sealed class PlayerHoldingsCommand : IEquatable<PlayerHoldingsCommand>
    {
        private readonly string canonicalText;

        private PlayerHoldingsCommand(
            EconomyTransactionCommand transaction,
            RewardGrantKind rewardKind,
            HoldingProvenance provenance,
            EquipmentInstance equipmentInstance)
        {
            Transaction = transaction
                ?? throw new ArgumentNullException(nameof(transaction));
            Provenance = provenance
                ?? throw new ArgumentNullException(nameof(provenance));

            if (!Enum.IsDefined(typeof(RewardGrantKind), rewardKind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rewardKind),
                    rewardKind,
                    "Reward kind must be defined.");
            }

            if (rewardKind == RewardGrantKind.EquipmentReference)
            {
                if (transaction.Operation == EconomyTransactionOperation.AddUnique)
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
            HoldingsFormat.AppendToken(
                builder,
                "transaction",
                Transaction.ToCanonicalString());
            HoldingsFormat.AppendToken(
                builder,
                "reward_kind",
                ((int)RewardKind).ToString(CultureInfo.InvariantCulture));
            HoldingsFormat.AppendToken(
                builder,
                "provenance",
                Provenance.ToCanonicalString());
            HoldingsFormat.AppendToken(
                builder,
                "equipment_instance",
                EquipmentInstance == null
                    ? "none"
                    : EquipmentInstance.ToCanonicalString());
            canonicalText = builder.ToString();
            PayloadFingerprint = HoldingsFormat.ComputeSha256(canonicalText);
        }

        public EconomyTransactionCommand Transaction { get; }

        public RewardGrantKind RewardKind { get; }

        public HoldingProvenance Provenance { get; }

        public EquipmentInstance EquipmentInstance { get; }

        public string PayloadFingerprint { get; }

        public static PlayerHoldingsCommand Create(
            EconomyTransactionCommand transaction,
            RewardGrantKind rewardKind,
            HoldingProvenance provenance,
            EquipmentInstance equipmentInstance = null)
        {
            return new PlayerHoldingsCommand(
                transaction,
                rewardKind,
                provenance,
                equipmentInstance);
        }

        public static PlayerHoldingsCommand AddEquipment(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            EquipmentInstance equipmentInstance,
            HoldingProvenance provenance,
            long? expectedSequence = null)
        {
            if (equipmentInstance == null)
            {
                throw new ArgumentNullException(nameof(equipmentInstance));
            }

            return Create(
                EconomyTransactionCommand.Create(
                    transactionStableId,
                    operationStableId,
                    authorityStableId,
                    EconomyTransactionOperation.AddUnique,
                    EconomyResourceKind.EquipmentReference,
                    equipmentInstance.DefinitionId,
                    equipmentInstance.InstanceId,
                    1L,
                    expectedSequence),
                RewardGrantKind.EquipmentReference,
                provenance,
                equipmentInstance);
        }

        public static PlayerHoldingsCommand RemoveEquipment(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            StableId equipmentDefinitionStableId,
            StableId equipmentInstanceStableId,
            HoldingProvenance provenance,
            long? expectedSequence = null)
        {
            return Create(
                EconomyTransactionCommand.Create(
                    transactionStableId,
                    operationStableId,
                    authorityStableId,
                    EconomyTransactionOperation.RemoveUnique,
                    EconomyResourceKind.EquipmentReference,
                    equipmentDefinitionStableId,
                    equipmentInstanceStableId,
                    1L,
                    expectedSequence),
                RewardGrantKind.EquipmentReference,
                provenance);
        }

        public static PlayerHoldingsCommand AddStrongbox(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            StableId strongboxDefinitionStableId,
            StableId strongboxInstanceStableId,
            HoldingProvenance provenance,
            long? expectedSequence = null)
        {
            return Create(
                EconomyTransactionCommand.Create(
                    transactionStableId,
                    operationStableId,
                    authorityStableId,
                    EconomyTransactionOperation.AddUnique,
                    EconomyResourceKind.Strongbox,
                    strongboxDefinitionStableId,
                    strongboxInstanceStableId,
                    1L,
                    expectedSequence),
                RewardGrantKind.Strongbox,
                provenance);
        }

        public static PlayerHoldingsCommand RemoveStrongbox(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            StableId strongboxDefinitionStableId,
            StableId strongboxInstanceStableId,
            HoldingProvenance provenance,
            long? expectedSequence = null)
        {
            return Create(
                EconomyTransactionCommand.Create(
                    transactionStableId,
                    operationStableId,
                    authorityStableId,
                    EconomyTransactionOperation.RemoveUnique,
                    EconomyResourceKind.Strongbox,
                    strongboxDefinitionStableId,
                    strongboxInstanceStableId,
                    1L,
                    expectedSequence),
                RewardGrantKind.Strongbox,
                provenance);
        }

        public static PlayerHoldingsCommand AddStack(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            RewardGrantKind rewardKind,
            StableId itemStableId,
            long quantity,
            HoldingProvenance provenance,
            long? expectedSequence = null)
        {
            return Create(
                EconomyTransactionCommand.Create(
                    transactionStableId,
                    operationStableId,
                    authorityStableId,
                    EconomyTransactionOperation.AddStack,
                    EconomyResourceKind.Item,
                    itemStableId,
                    null,
                    quantity,
                    expectedSequence),
                rewardKind,
                provenance);
        }

        public static PlayerHoldingsCommand RemoveStack(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            RewardGrantKind rewardKind,
            StableId itemStableId,
            long quantity,
            HoldingProvenance provenance,
            long? expectedSequence = null)
        {
            return Create(
                EconomyTransactionCommand.Create(
                    transactionStableId,
                    operationStableId,
                    authorityStableId,
                    EconomyTransactionOperation.RemoveStack,
                    EconomyResourceKind.Item,
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

        public bool Equals(PlayerHoldingsCommand other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerHoldingsCommand);
        }

        public override int GetHashCode()
        {
            return HoldingsFormat.DeterministicHash(canonicalText);
        }

        public override string ToString()
        {
            return canonicalText;
        }
    }

}
