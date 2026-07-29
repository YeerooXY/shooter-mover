using System;
using System.Globalization;
using System.Text;
using ShooterMover.Application.Crafting;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Crafting;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Crafting.Integration
{
    public enum CraftedEquipmentEquipStatus
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        RetryRequired = 4,
        Rejected = 5,
        InvalidCommand = 6,
    }

    public sealed class CraftedEquipmentEquipCommand :
        IEquatable<CraftedEquipmentEquipCommand>
    {
        private readonly string canonicalText;

        public CraftedEquipmentEquipCommand(
            StableId transactionStableId,
            StableId operationStableId,
            StableId craftTransactionStableId,
            StableId loadoutSlotStableId,
            StableId equipmentInstanceStableId,
            string equipmentFingerprint,
            long? expectedLoadoutSequence = null)
        {
            TransactionStableId = transactionStableId
                ?? throw new ArgumentNullException(nameof(transactionStableId));
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            CraftTransactionStableId = craftTransactionStableId
                ?? throw new ArgumentNullException(nameof(craftTransactionStableId));
            LoadoutSlotStableId = loadoutSlotStableId
                ?? throw new ArgumentNullException(nameof(loadoutSlotStableId));
            EquipmentInstanceStableId = equipmentInstanceStableId
                ?? throw new ArgumentNullException(nameof(equipmentInstanceStableId));
            if (string.IsNullOrWhiteSpace(equipmentFingerprint))
            {
                throw new ArgumentException(
                    "Equipment fingerprint is required.",
                    nameof(equipmentFingerprint));
            }
            if (expectedLoadoutSequence.HasValue
                && expectedLoadoutSequence.Value < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedLoadoutSequence));
            }

            EquipmentFingerprint = equipmentFingerprint;
            ExpectedLoadoutSequence = expectedLoadoutSequence;
            canonicalText = "schema=crafted-equipment-equip-command-v1"
                + "\ntransaction_id=" + TransactionStableId
                + "\noperation_id=" + OperationStableId
                + "\ncraft_transaction_id=" + CraftTransactionStableId
                + "\nloadout_slot_id=" + LoadoutSlotStableId
                + "\nequipment_instance_id=" + EquipmentInstanceStableId
                + "\nequipment_fingerprint=" + EquipmentFingerprint
                + "\nexpected_loadout_sequence="
                + Optional(ExpectedLoadoutSequence);
            Fingerprint = CraftingFormat.Fingerprint(canonicalText);
        }

        public StableId TransactionStableId { get; }

        public StableId OperationStableId { get; }

        public StableId CraftTransactionStableId { get; }

        public StableId LoadoutSlotStableId { get; }

        public StableId EquipmentInstanceStableId { get; }

        public string EquipmentFingerprint { get; }

        public long? ExpectedLoadoutSequence { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(CraftedEquipmentEquipCommand other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CraftedEquipmentEquipCommand);
        }

        public override int GetHashCode()
        {
            return CraftingFormat.DeterministicHash(canonicalText);
        }

        private static string Optional(long? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : "none";
        }
    }

    public sealed class CraftedEquipmentEquipResult
    {
        public CraftedEquipmentEquipResult(
            CraftedEquipmentEquipStatus status,
            StableId transactionStableId,
            StableId operationStableId,
            StableId loadoutSlotStableId,
            StableId equipmentInstanceStableId,
            string commandFingerprint,
            long resultingSequence,
            bool originalApplied,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(CraftedEquipmentEquipStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }
            if (resultingSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(resultingSequence));
            }

            Status = status;
            TransactionStableId = transactionStableId
                ?? throw new ArgumentNullException(nameof(transactionStableId));
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            LoadoutSlotStableId = loadoutSlotStableId
                ?? throw new ArgumentNullException(nameof(loadoutSlotStableId));
            EquipmentInstanceStableId = equipmentInstanceStableId
                ?? throw new ArgumentNullException(nameof(equipmentInstanceStableId));
            CommandFingerprint = commandFingerprint
                ?? throw new ArgumentNullException(nameof(commandFingerprint));
            ResultingSequence = resultingSequence;
            OriginalApplied = originalApplied;
            RejectionCode = rejectionCode;
        }

        public CraftedEquipmentEquipStatus Status { get; }

        public StableId TransactionStableId { get; }

        public StableId OperationStableId { get; }

        public StableId LoadoutSlotStableId { get; }

        public StableId EquipmentInstanceStableId { get; }

        public string CommandFingerprint { get; }

        public long ResultingSequence { get; }

        public bool OriginalApplied { get; }

        public string RejectionCode { get; }

        public bool ChangedState
        {
            get { return Status == CraftedEquipmentEquipStatus.Applied; }
        }

        public bool Succeeded
        {
            get
            {
                return Status == CraftedEquipmentEquipStatus.Applied
                    || (Status
                            == CraftedEquipmentEquipStatus.ExactDuplicateNoChange
                        && OriginalApplied);
            }
        }

        public static CraftedEquipmentEquipResult FromCommand(
            CraftedEquipmentEquipCommand command,
            CraftedEquipmentEquipStatus status,
            long resultingSequence,
            bool originalApplied,
            string rejectionCode)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return new CraftedEquipmentEquipResult(
                status,
                command.TransactionStableId,
                command.OperationStableId,
                command.LoadoutSlotStableId,
                command.EquipmentInstanceStableId,
                command.Fingerprint,
                resultingSequence,
                originalApplied,
                rejectionCode);
        }
    }

    /// <summary>
    /// Adapter boundary to the existing loadout authority. CRAFT-002 never owns
    /// slot truth; production composition supplies the real loadout path.
    /// </summary>
    public interface ICraftedEquipmentLoadoutPort
    {
        StableId AuthorityStableId { get; }

        long Sequence { get; }

        CraftedEquipmentEquipResult Apply(
            CraftedEquipmentEquipCommand command);
    }

    public sealed class CraftAndEquipCommand :
        IEquatable<CraftAndEquipCommand>
    {
        private readonly string canonicalText;

        public CraftAndEquipCommand(
            CraftEquipmentCommand craftCommand,
            StableId loadoutSlotStableId,
            long? expectedLoadoutSequence = null)
        {
            CraftCommand = craftCommand
                ?? throw new ArgumentNullException(nameof(craftCommand));
            LoadoutSlotStableId = loadoutSlotStableId
                ?? throw new ArgumentNullException(nameof(loadoutSlotStableId));
            if (expectedLoadoutSequence.HasValue
                && expectedLoadoutSequence.Value < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedLoadoutSequence));
            }

            ExpectedLoadoutSequence = expectedLoadoutSequence;
            canonicalText = "schema=craft-and-equip-command-v1"
                + "\ncraft_command_fingerprint=" + CraftCommand.Fingerprint
                + "\nloadout_slot_id=" + LoadoutSlotStableId
                + "\nexpected_loadout_sequence="
                + Optional(ExpectedLoadoutSequence);
            Fingerprint = CraftingFormat.Fingerprint(canonicalText);
        }

        public CraftEquipmentCommand CraftCommand { get; }

        public StableId LoadoutSlotStableId { get; }

        public long? ExpectedLoadoutSequence { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(CraftAndEquipCommand other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CraftAndEquipCommand);
        }

        public override int GetHashCode()
        {
            return CraftingFormat.DeterministicHash(canonicalText);
        }

        private static string Optional(long? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : "none";
        }
    }

    public enum CraftingInventoryEquipStatus
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        CraftRetryRequired = 3,
        CraftRejected = 4,
        InventoryProjectionRejected = 5,
        EquipRetryRequired = 6,
        EquipRejected = 7,
        ConflictingDuplicate = 8,
        InvalidCommand = 9,
    }

    public sealed class CraftingInventoryEquipResult
    {
        internal CraftingInventoryEquipResult(
            CraftingInventoryEquipStatus status,
            string commandFingerprint,
            CraftingResult craftingResult,
            UniqueHoldingSnapshot craftedHolding,
            CraftedEquipmentEquipResult equipResult,
            string rejectionCode)
        {
            Status = status;
            CommandFingerprint = commandFingerprint;
            CraftingResult = craftingResult;
            CraftedHolding = craftedHolding;
            EquipResult = equipResult;
            RejectionCode = rejectionCode;
        }

        public CraftingInventoryEquipStatus Status { get; }

        public string CommandFingerprint { get; }

        public CraftingResult CraftingResult { get; }

        public UniqueHoldingSnapshot CraftedHolding { get; }

        public CraftedEquipmentEquipResult EquipResult { get; }

        public string RejectionCode { get; }

        public StableId EquipmentInstanceStableId
        {
            get
            {
                return CraftingResult == null
                    ? null
                    : CraftingResult.EquipmentInstanceStableId;
            }
        }

        public string EquipmentFingerprint
        {
            get
            {
                return CraftingResult == null
                    ? null
                    : CraftingResult.EquipmentFingerprint;
            }
        }

        public bool Succeeded
        {
            get
            {
                return Status == CraftingInventoryEquipStatus.Applied
                    || Status
                        == CraftingInventoryEquipStatus.ExactDuplicateNoChange;
            }
        }

        public bool ChangedState
        {
            get
            {
                return (CraftingResult != null
                        && CraftingResult.Status
                            == CraftingResultStatus.Crafted)
                    || (EquipResult != null && EquipResult.ChangedState);
            }
        }
    }

    public static class CraftingIntegrationIdentity
    {
        public static StableId EquipmentGrantStableId(
            CraftEquipmentCommand command)
        {
            return Derive("craftgrant", command, "equipment-grant");
        }

        public static StableId SourceOperationStableId(
            CraftEquipmentCommand command)
        {
            return Derive("craftop", command, "source-operation");
        }

        public static StableId EquipTransactionStableId(
            CraftEquipmentCommand command)
        {
            return Derive("craftequiptx", command, "transaction");
        }

        public static StableId EquipOperationStableId(
            CraftEquipmentCommand command)
        {
            return Derive("craftequipop", command, "operation");
        }

        private static StableId Derive(
            string namespaceName,
            CraftEquipmentCommand command,
            string purpose)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return CraftingFormat.DeriveStableId(
                namespaceName,
                purpose,
                command.CraftTransactionStableId.ToString());
        }
    }

    /// <summary>
    /// Roll-forward integration over CRA-001, INV-001 and the injected loadout
    /// path. Crafting remains the sole scrap/inventory mutation; equip is invoked
    /// only after the exact crafted instance is visible with crafting provenance.
    /// </summary>
    public sealed class CraftingInventoryEquipActions
    {
        private readonly CraftingActions crafting;
        private readonly IPlayerHoldingsState holdings;
        private readonly ICraftedEquipmentLoadoutPort loadout;

        public CraftingInventoryEquipActions(
            CraftingActions crafting,
            IPlayerHoldingsState holdings,
            ICraftedEquipmentLoadoutPort loadout)
        {
            this.crafting = crafting
                ?? throw new ArgumentNullException(nameof(crafting));
            this.holdings = holdings
                ?? throw new ArgumentNullException(nameof(holdings));
            this.loadout = loadout
                ?? throw new ArgumentNullException(nameof(loadout));
        }

        public CraftingInventoryEquipResult CraftAndEquip(
            CraftAndEquipCommand command)
        {
            if (command == null)
            {
                return Result(
                    CraftingInventoryEquipStatus.InvalidCommand,
                    null,
                    null,
                    null,
                    null,
                    "command-null");
            }

            CraftingResult craft = crafting.Craft(command.CraftCommand);
            if (craft == null)
            {
                return Result(
                    CraftingInventoryEquipStatus.CraftRejected,
                    command,
                    null,
                    null,
                    null,
                    "craft-result-null");
            }

            if (craft.Status == CraftingResultStatus.ConflictingDuplicate)
            {
                return Result(
                    CraftingInventoryEquipStatus.ConflictingDuplicate,
                    command,
                    craft,
                    null,
                    null,
                    craft.RejectionCode ?? "craft-conflicting-duplicate");
            }

            if (craft.Status
                == CraftingResultStatus.RewardApplicationRetryRequired)
            {
                return Result(
                    CraftingInventoryEquipStatus.CraftRetryRequired,
                    command,
                    craft,
                    null,
                    null,
                    craft.RejectionCode ?? "craft-retry-required");
            }

            if (!craft.Succeeded)
            {
                return Result(
                    CraftingInventoryEquipStatus.CraftRejected,
                    command,
                    craft,
                    null,
                    null,
                    craft.RejectionCode ?? "craft-rejected");
            }

            UniqueHoldingSnapshot craftedHolding;
            string projectionFailure;
            if (!TryResolveCraftedHolding(
                command.CraftCommand,
                craft,
                holdings.ExportSnapshot(),
                out craftedHolding,
                out projectionFailure))
            {
                return Result(
                    CraftingInventoryEquipStatus.InventoryProjectionRejected,
                    command,
                    craft,
                    null,
                    null,
                    projectionFailure);
            }

            var equipCommand = new CraftedEquipmentEquipCommand(
                CraftingIntegrationIdentity.EquipTransactionStableId(
                    command.CraftCommand),
                CraftingIntegrationIdentity.EquipOperationStableId(
                    command.CraftCommand),
                command.CraftCommand.CraftTransactionStableId,
                command.LoadoutSlotStableId,
                craft.EquipmentInstanceStableId,
                craft.EquipmentFingerprint,
                command.ExpectedLoadoutSequence);
            CraftedEquipmentEquipResult equip =
                loadout.Apply(equipCommand);
            if (!Matches(equipCommand, equip))
            {
                return Result(
                    CraftingInventoryEquipStatus.EquipRejected,
                    command,
                    craft,
                    craftedHolding,
                    equip,
                    "loadout-result-mismatch");
            }

            switch (equip.Status)
            {
                case CraftedEquipmentEquipStatus.Applied:
                    return Result(
                        CraftingInventoryEquipStatus.Applied,
                        command,
                        craft,
                        craftedHolding,
                        equip,
                        null);
                case CraftedEquipmentEquipStatus.ExactDuplicateNoChange:
                    if (!equip.OriginalApplied)
                    {
                        return Result(
                            CraftingInventoryEquipStatus.EquipRejected,
                            command,
                            craft,
                            craftedHolding,
                            equip,
                            equip.RejectionCode
                                ?? "loadout-original-not-applied");
                    }
                    return Result(
                        craft.Status
                                == CraftingResultStatus.ExactDuplicateNoChange
                            ? CraftingInventoryEquipStatus
                                .ExactDuplicateNoChange
                            : CraftingInventoryEquipStatus.Applied,
                        command,
                        craft,
                        craftedHolding,
                        equip,
                        null);
                case CraftedEquipmentEquipStatus.ConflictingDuplicate:
                    return Result(
                        CraftingInventoryEquipStatus.ConflictingDuplicate,
                        command,
                        craft,
                        craftedHolding,
                        equip,
                        equip.RejectionCode
                            ?? "loadout-conflicting-duplicate");
                case CraftedEquipmentEquipStatus.RetryRequired:
                    return Result(
                        CraftingInventoryEquipStatus.EquipRetryRequired,
                        command,
                        craft,
                        craftedHolding,
                        equip,
                        equip.RejectionCode ?? "loadout-retry-required");
                default:
                    return Result(
                        CraftingInventoryEquipStatus.EquipRejected,
                        command,
                        craft,
                        craftedHolding,
                        equip,
                        equip.RejectionCode ?? "loadout-rejected");
            }
        }

        private static bool TryResolveCraftedHolding(
            CraftEquipmentCommand command,
            CraftingResult craft,
            PlayerHoldingsSnapshot snapshot,
            out UniqueHoldingSnapshot holding,
            out string failure)
        {
            holding = null;
            if (snapshot == null)
            {
                failure = "holdings-snapshot-null";
                return false;
            }
            if (craft.Equipment == null
                || craft.EquipmentInstanceStableId == null
                || string.IsNullOrWhiteSpace(craft.EquipmentFingerprint))
            {
                failure = "crafted-equipment-payload-missing";
                return false;
            }

            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                UniqueHoldingSnapshot candidate =
                    snapshot.UniqueHoldings[index];
                if (candidate != null
                    && Equals(
                        candidate.InstanceStableId,
                        craft.EquipmentInstanceStableId))
                {
                    holding = candidate;
                    break;
                }
            }

            if (holding == null)
            {
                failure = "crafted-holding-missing";
                return false;
            }
            if (holding.RewardKind != RewardGrantKind.EquipmentReference)
            {
                failure = "crafted-holding-kind-invalid";
                return false;
            }
            if (holding.EquipmentInstance == null)
            {
                failure = "crafted-holding-payload-missing";
                return false;
            }
            if (!Equals(
                    holding.DefinitionStableId,
                    craft.Equipment.DefinitionId)
                || !string.Equals(
                    holding.EquipmentInstance.Fingerprint,
                    craft.EquipmentFingerprint,
                    StringComparison.Ordinal))
            {
                failure = "crafted-holding-fingerprint-mismatch";
                return false;
            }

            StableId expectedGrant =
                CraftingIntegrationIdentity.EquipmentGrantStableId(command);
            StableId expectedSource =
                CraftingIntegrationIdentity.SourceOperationStableId(command);
            if (holding.Provenance == null
                || !Equals(
                    holding.Provenance.GrantStableId,
                    expectedGrant)
                || !Equals(
                    holding.Provenance.SourceStableId,
                    expectedSource))
            {
                failure = "crafted-holding-provenance-mismatch";
                return false;
            }

            failure = null;
            return true;
        }

        private static bool Matches(
            CraftedEquipmentEquipCommand command,
            CraftedEquipmentEquipResult result)
        {
            return result != null
                && Equals(
                    result.TransactionStableId,
                    command.TransactionStableId)
                && Equals(
                    result.OperationStableId,
                    command.OperationStableId)
                && Equals(
                    result.LoadoutSlotStableId,
                    command.LoadoutSlotStableId)
                && Equals(
                    result.EquipmentInstanceStableId,
                    command.EquipmentInstanceStableId)
                && string.Equals(
                    result.CommandFingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal);
        }

        private static CraftingInventoryEquipResult Result(
            CraftingInventoryEquipStatus status,
            CraftAndEquipCommand command,
            CraftingResult craftingResult,
            UniqueHoldingSnapshot craftedHolding,
            CraftedEquipmentEquipResult equipResult,
            string rejectionCode)
        {
            return new CraftingInventoryEquipResult(
                status,
                command == null ? null : command.Fingerprint,
                craftingResult,
                craftedHolding,
                equipResult,
                rejectionCode);
        }
    }
}
