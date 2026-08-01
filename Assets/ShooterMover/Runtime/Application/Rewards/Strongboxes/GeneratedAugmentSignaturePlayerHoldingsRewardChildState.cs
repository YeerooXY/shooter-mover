using System;
using System.Collections.Generic;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Application.Holdings;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.Strongboxes
{
    /// <summary>
    /// RAP holdings child that commits generated augment metadata only after the exact
    /// equipment grant is confirmed applied. A commit conflict compensates the holdings
    /// mutation from the captured immutable snapshot, so an opening cannot leave either
    /// an orphan signature or signature-less hybrid equipment.
    /// </summary>
    public sealed class
        GeneratedAugmentSignaturePlayerHoldingsRewardChildState :
        IRewardChildState
    {
        private readonly IPlayerHoldingsState holdings;
        private readonly PlayerHoldingsRewardChildState inner;
        private readonly GeneratedEquipmentAugmentSignatureState signatures;
        private readonly GunInventoryState gunInventory;

        public GeneratedAugmentSignaturePlayerHoldingsRewardChildState(
            IPlayerHoldingsState holdings,
            IEquipmentInstanceValidator equipmentValidator,
            GeneratedEquipmentAugmentSignatureState signatures,
            GunInventoryState gunInventory = null)
        {
            this.holdings = holdings
                ?? throw new ArgumentNullException(nameof(holdings));
            inner = new PlayerHoldingsRewardChildState(
                holdings,
                equipmentValidator
                    ?? throw new ArgumentNullException(nameof(equipmentValidator)));
            this.signatures = signatures
                ?? throw new ArgumentNullException(nameof(signatures));
            this.gunInventory = gunInventory;
        }

        public StableId AuthorityStableId
        {
            get { return inner.AuthorityStableId; }
        }

        public long Sequence
        {
            get { return inner.Sequence; }
        }

        public RewardStatePreflightResult Preflight(
            IReadOnlyList<RewardChildGrantCommand> commands)
        {
            lock (signatures)
            {
                RewardStatePreflightResult result =
                    inner.Preflight(commands);
                if (result == null)
                {
                    return null;
                }

                var byTransaction = new Dictionary<
                    StableId,
                    RewardChildGrantCommand>();
                for (int index = 0; index < commands.Count; index++)
                {
                    byTransaction[commands[index].TransactionStableId] =
                        commands[index];
                }

                var facts = new List<RewardStatePreflightFact>(
                    result.Facts.Count);
                for (int index = 0; index < result.Facts.Count; index++)
                {
                    RewardStatePreflightFact fact = result.Facts[index];
                    RewardChildGrantCommand command;
                    if (!fact.CanProceed
                        || !byTransaction.TryGetValue(
                            fact.TransactionStableId,
                            out command)
                        || command.GrantKind
                            != RewardGrantKind.EquipmentReference)
                    {
                        facts.Add(fact);
                        continue;
                    }

                    GeneratedEquipmentAugmentSignature signature;
                    bool committed;
                    if (!signatures.TryGetStagedOrCommitted(
                            command.InstanceStableId,
                            out signature,
                            out committed))
                    {
                        // Non-hybrid equipment grants share this RAP authority and do not
                        // require generated augment metadata.
                        facts.Add(fact);
                        continue;
                    }
                    if (command.EquipmentInstance == null
                        || command.EquipmentInstance.InstanceId
                            != command.InstanceStableId
                        || signature.EquipmentInstanceStableId
                            != command.InstanceStableId)
                    {
                        facts.Add(new RewardStatePreflightFact(
                            fact.TransactionStableId,
                            RewardStateAdmissionStatus.InvalidCommand,
                            "generated-augment-signature-equipment-identity-mismatch"));
                        continue;
                    }
                    facts.Add(fact);
                }
                return new RewardStatePreflightResult(facts);
            }
        }

        public RewardChildApplyResult Apply(
            RewardChildGrantCommand command)
        {
            lock (signatures)
            {
                if (command == null
                    || command.GrantKind
                        != RewardGrantKind.EquipmentReference)
                {
                    return inner.Apply(command);
                }

                GeneratedEquipmentAugmentSignature signature;
                bool alreadyCommitted;
                if (!signatures.TryGetStagedOrCommitted(
                        command.InstanceStableId,
                        out signature,
                        out alreadyCommitted))
                {
                    return inner.Apply(command);
                }

                PlayerHoldingsSnapshot before;
                GunInventorySnapshot gunsBefore;
                try
                {
                    before = holdings.ExportSnapshot();
                    gunsBefore = gunInventory == null
                        ? null
                        : gunInventory.ExportSnapshot();
                }
                catch (Exception exception)
                {
                    return Rejected(
                        command,
                        "generated-augment-signature-holdings-snapshot-exception-"
                            + exception.GetType().Name.ToLowerInvariant());
                }

                RewardChildApplyResult applied = inner.Apply(command);
                if (applied == null || !applied.IsConfirmedApplied)
                {
                    return applied;
                }

                GeneratedEquipmentAugmentSignature committed;
                string diagnostic;
                if (signatures.TryCommitStaged(
                        command.InstanceStableId,
                        signature.Fingerprint,
                        out committed,
                        out diagnostic))
                {
                    return applied;
                }

                PlayerHoldingsImportResult compensation;
                GunInventoryImportResult gunCompensation = null;
                try
                {
                    compensation = holdings.ImportSnapshot(before);
                    if (gunInventory != null)
                    {
                        gunCompensation = gunInventory.ImportSnapshot(
                            gunsBefore);
                    }
                }
                catch (Exception exception)
                {
                    return Rejected(
                        command,
                        (string.IsNullOrWhiteSpace(diagnostic)
                            ? "generated-augment-signature-commit-rejected"
                            : diagnostic)
                        + ";holdings-compensation-exception="
                        + exception.GetType().Name.ToLowerInvariant());
                }
                bool holdingsRestored = compensation != null
                    && compensation.Succeeded;
                bool gunsRestored = gunInventory == null
                    || gunCompensation != null
                    && gunCompensation.Succeeded;
                if (!holdingsRestored || !gunsRestored)
                {
                    return Rejected(
                        command,
                        (string.IsNullOrWhiteSpace(diagnostic)
                            ? "generated-augment-signature-commit-rejected"
                            : diagnostic)
                        + ";holdings-compensation="
                        + (compensation == null
                            ? "result-null"
                            : compensation.RejectionCode)
                        + ";gun-inventory-compensation="
                        + (gunInventory == null
                            ? "not-required"
                            : gunCompensation == null
                                ? "result-null"
                                : gunCompensation.RejectionCode));
                }
                return Rejected(
                    command,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "generated-augment-signature-commit-rejected-compensated"
                        : diagnostic + ";holdings-compensated");
            }
        }

        private static RewardChildApplyResult Rejected(
            RewardChildGrantCommand command,
            string diagnostic)
        {
            return new RewardChildApplyResult(
                command == null
                    ? StableId.Parse("raptx.invalid")
                    : command.TransactionStableId,
                RewardChildApplyStatus.Rejected,
                false,
                diagnostic);
        }
    }
}
