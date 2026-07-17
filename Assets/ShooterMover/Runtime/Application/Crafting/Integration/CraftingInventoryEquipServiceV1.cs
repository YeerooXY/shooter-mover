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
    public enum CraftedEquipmentEquipStatusV1
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        RetryRequired = 4,
        Rejected = 5,
        InvalidCommand = 6,
    }

    public sealed class CraftedEquipmentEquipCommandV1 :
        IEquatable<CraftedEquipmentEquipCommandV1>
    {
        private readonly string canonicalText;

        public CraftedEquipmentEquipCommandV1(
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
            Fingerprint = CraftingCanonicalV1.Fingerprint(canonicalText);
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

        public bool Equals(CraftedEquipmentEquipCommandV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CraftedEquipmentEquipCommandV1);
        }

        public override int GetHashCode()
        {
            return CraftingCanonicalV1.DeterministicHash(canonicalText);
        }

        private static string Optional(long? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : "none";
        }
    }

    public sealed class CraftedEquipmentEquipResultV1
    {
        public CraftedEquipmentEquipResultV1(
            CraftedEquipmentEquipStatusV1 status,
            StableId transactionStableId,
            StableId operationStableId,
            StableId loadoutSlotStableId,
            StableId equipmentInstanceStableId,
            string commandFingerprint,
            long resultingSequence,
            bool originalApplied,
            string rejectionCode)
        {
            if (!Enum.IsDefined(typeof(CraftedEquipmentEquipStatusV1), status))
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

        public CraftedEquipmentEquipStatusV1 Status { get; }

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
            get { return Status == CraftedEquipmentEquipStatusV1.Applied; }
        }

        public bool Succeeded
        {
            get
            {
                return Status == CraftedEquipmentEquipStatusV1.Applied
                    || (Status
                            == CraftedEquipmentEquipStatusV1.ExactDuplicateNoChange
                        && OriginalApplied);
            }
        }

        public static CraftedEquipmentEquipResultV1 FromCommand(
            CraftedEquipmentEquipCommandV1 command,
            CraftedEquipmentEquipStatusV1 status,
            long resultingSequence,
            bool originalApplied,
            string rejectionCode)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return new CraftedEquipmentEquipResultV1(
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
    public interface ICraftedEquipmentLoadoutPortV1
    {
        StableId AuthorityStableId { get; }

        long Sequence { get; }

        CraftedEquipmentEquipResultV1 Apply(
            CraftedEquipmentEquipCommandV1 command);
    }

    public sealed class CraftAndEquipCommandV1 :
        IEquatable<CraftAndEquipCommandV1>
    {
        private readonly string canonicalText;

        public CraftAndEquipCommandV1(
            CraftEquipmentCommandV1 craftCommand,
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
            Fingerprint = CraftingCanonicalV1.Fingerprint(canonicalText);
        }

        public CraftEquipmentCommandV1 CraftCommand { get; }

        public StableId LoadoutSlotStableId { get; }

        public long? ExpectedLoadoutSequence { get; }

        public string Fingerprint { get; }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public bool Equals(CraftAndEquipCommandV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(
                    canonicalText,
                    other.canonicalText,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as CraftAndEquipCommandV1);
        }

        public override int GetHashCode()
        {
            return CraftingCanonicalV1.DeterministicHash(canonicalText);
        }

        private static string Optional(long? value)
        {
            return value.HasValue
                ? value.Value.ToString(CultureInfo.InvariantCulture)
                : "none";
        }
    }

    public enum CraftingInventoryEquipStatusV1
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

    public sealed class CraftingInventoryEquipResultV1
    {
        internal CraftingInventoryEquipResultV1(
            CraftingInventoryEquipStatusV1 status,
            string commandFingerprint,
            CraftingResultV1 craftingResult,
            UniqueHoldingSnapshotV1 craftedHolding,
            CraftedEquipmentEquipResultV1 equipResult,
            string rejectionCode)
        {
            Status = status;
            CommandFingerprint = commandFingerprint;
            CraftingResult = craftingResult;
            CraftedHolding = craftedHolding;
            EquipResult = equipResult;
            RejectionCode = rejectionCode;
        }

        public CraftingInventoryEquipStatusV1 Status { get; }

        public string CommandFingerprint { get; }

        public CraftingResultV1 CraftingResult { get; }

        public UniqueHoldingSnapshotV1 CraftedHolding { get; }

        public CraftedEquipmentEquipResultV1 EquipResult { get; }

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
                return Status == CraftingInventoryEquipStatusV1.Applied
                    || Status
                        == CraftingInventoryEquipStatusV1.ExactDuplicateNoChange;
            }
        }

        public bool ChangedState
        {
            get
            {
                return (CraftingResult != null
                        && CraftingResult.Status
                            == CraftingResultStatusV1.Crafted)
                    || (EquipResult != null && EquipResult.ChangedState);
            }
        }
    }

    public static class CraftingIntegrationIdentityV1
    {
        public static StableId EquipmentGrantStableId(
            CraftEquipmentCommandV1 command)
        {
            return Derive("craftgrant", command, "equipment-grant");
        }

        public static StableId SourceOperationStableId(
            CraftEquipmentCommandV1 command)
        {
            return Derive("craftop", command, "source-operation");
        }

        public static StableId EquipTransactionStableId(
            CraftEquipmentCommandV1 command)
        {
            return Derive("craftequiptx", command, "transaction");
        }

        public static StableId EquipOperationStableId(
            CraftEquipmentCommandV1 command)
        {
            return Derive("craftequipop", command, "operation");
        }

        private static StableId Derive(
            string namespaceName,
            CraftEquipmentCommandV1 command,
            string purpose)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return CraftingCanonicalV1.DeriveStableId(
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
    public sealed class CraftingInventoryEquipServiceV1
    {
        private readonly CraftingServiceV1 crafting;
        private readonly IPlayerHoldingsAuthorityV1 holdings;
        private readonly ICraftedEquipmentLoadoutPortV1 loadout;

        public CraftingInventoryEquipServiceV1(
            CraftingServiceV1 crafting,
            IPlayerHoldingsAuthorityV1 holdings,
            ICraftedEquipmentLoadoutPortV1 loadout)
        {
            this.crafting = crafting
                ?? throw new ArgumentNullException(nameof(crafting));
            this.holdings = holdings
                ?? throw new ArgumentNullException(nameof(holdings));
            this.loadout = loadout
                ?? throw new ArgumentNullException(nameof(loadout));
        }

        public CraftingInventoryEquipResultV1 CraftAndEquip(
            CraftAndEquipCommandV1 command)
        {
            if (command == null)
            {
                return Result(
                    CraftingInventoryEquipStatusV1.InvalidCommand,
                    null,
                    null,
                    null,
                    null,
                    "command-null");
            }

            CraftingResultV1 craft = crafting.Craft(command.CraftCommand);
            if (craft == null)
            {
                return Result(
                    CraftingInventoryEquipStatusV1.CraftRejected,
                    command,
                    null,
                    null,
                    null,
                    "craft-result-null");
            }

            if (craft.Status == CraftingResultStatusV1.ConflictingDuplicate)
            {
                return Result(
                    CraftingInventoryEquipStatusV1.ConflictingDuplicate,
                    command,
                    craft,
                    null,
                    null,
                    craft.RejectionCode ?? "craft-conflicting-duplicate");
            }

            if (craft.Status
                == CraftingResultStatusV1.RewardApplicationRetryRequired)
            {
                return Result(
                    CraftingInventoryEquipStatusV1.CraftRetryRequired,
                    command,
                    craft,
                    null,
                    null,
                    craft.RejectionCode ?? "craft-retry-required");
            }

            if (!craft.Succeeded)
            {
                return Result(
                    CraftingInventoryEquipStatusV1.CraftRejected,
                    command,
                    craft,
                    null,
                    null,
                    craft.RejectionCode ?? "craft-rejected");
            }

            UniqueHoldingSnapshotV1 craftedHolding;
            string projectionFailure;
            if (!TryResolveCraftedHolding(
                command.CraftCommand,
                craft,
                holdings.ExportSnapshot(),
                out craftedHolding,
                out projectionFailure))
            {
                return Result(
                    CraftingInventoryEquipStatusV1.InventoryProjectionRejected,
                    command,
                    craft,
                    null,
                    null,
                    projectionFailure);
            }

            var equipCommand = new CraftedEquipmentEquipCommandV1(
                CraftingIntegrationIdentityV1.EquipTransactionStableId(
                    command.CraftCommand),
                CraftingIntegrationIdentityV1.EquipOperationStableId(
                    command.CraftCommand),
                command.CraftCommand.CraftTransactionStableId,
                command.LoadoutSlotStableId,
                craft.EquipmentInstanceStableId,
                craft.EquipmentFingerprint,
                command.ExpectedLoadoutSequence);
            CraftedEquipmentEquipResultV1 equip =
                loadout.Apply(equipCommand);
            if (!Matches(equipCommand, equip))
            {
                return Result(
                    CraftingInventoryEquipStatusV1.EquipRejected,
                    command,
                    craft,
                    craftedHolding,
                    equip,
                    "loadout-result-mismatch");
            }

            switch (equip.Status)
            {
                case CraftedEquipmentEquipStatusV1.Applied:
                    return Result(
                        CraftingInventoryEquipStatusV1.Applied,
                        command,
                        craft,
                        craftedHolding,
                        equip,
                        null);
                case CraftedEquipmentEquipStatusV1.ExactDuplicateNoChange:
                    if (!equip.OriginalApplied)
                    {
                        return Result(
                            CraftingInventoryEquipStatusV1.EquipRejected,
                            command,
                            craft,
                            craftedHolding,
                            equip,
                            equip.RejectionCode
                                ?? "loadout-original-not-applied");
                    }
                    return Result(
                        craft.Status
                                == CraftingResultStatusV1.ExactDuplicateNoChange
                            ? CraftingInventoryEquipStatusV1
                                .ExactDuplicateNoChange
                            : CraftingInventoryEquipStatusV1.Applied,
                        command,
                        craft,
                        craftedHolding,
                        equip,
                        null);
                case CraftedEquipmentEquipStatusV1.ConflictingDuplicate:
                    return Result(
                        CraftingInventoryEquipStatusV1.ConflictingDuplicate,
                        command,
                        craft,
                        craftedHolding,
                        equip,
                        equip.RejectionCode
                            ?? "loadout-conflicting-duplicate");
                case CraftedEquipmentEquipStatusV1.RetryRequired:
                    return Result(
                        CraftingInventoryEquipStatusV1.EquipRetryRequired,
                        command,
                        craft,
                        craftedHolding,
                        equip,
                        equip.RejectionCode ?? "loadout-retry-required");
                default:
                    return Result(
                        CraftingInventoryEquipStatusV1.EquipRejected,
                        command,
                        craft,
                        craftedHolding,
                        equip,
                        equip.RejectionCode ?? "loadout-rejected");
            }
        }

        private static bool TryResolveCraftedHolding(
            CraftEquipmentCommandV1 command,
            CraftingResultV1 craft,
            PlayerHoldingsSnapshotV1 snapshot,
            out UniqueHoldingSnapshotV1 holding,
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
                UniqueHoldingSnapshotV1 candidate =
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
            if (holding.RewardKind != RewardGrantKindV1.EquipmentReference)
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
                CraftingIntegrationIdentityV1.EquipmentGrantStableId(command);
            StableId expectedSource =
                CraftingIntegrationIdentityV1.SourceOperationStableId(command);
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
            CraftedEquipmentEquipCommandV1 command,
            CraftedEquipmentEquipResultV1 result)
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

        private static CraftingInventoryEquipResultV1 Result(
            CraftingInventoryEquipStatusV1 status,
            CraftAndEquipCommandV1 command,
            CraftingResultV1 craftingResult,
            UniqueHoldingSnapshotV1 craftedHolding,
            CraftedEquipmentEquipResultV1 equipResult,
            string rejectionCode)
        {
            return new CraftingInventoryEquipResultV1(
                status,
                command == null ? null : command.Fingerprint,
                craftingResult,
                craftedHolding,
                equipResult,
                rejectionCode);
        }
    }
}
