using System;
using System.Collections.Generic;
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
    /// equipment grant is confirmed applied. Durable BOX staging and transient Shop
    /// previews share the same compensated holdings transaction without allowing preview
    /// metadata to masquerade as character-owned state before purchase.
    /// </summary>
    public sealed class
        GeneratedAugmentSignaturePlayerHoldingsRewardChildState :
        IRewardChildState
    {
        private readonly PlayerHoldingsActions holdings;
        private readonly PlayerHoldingsRewardChildState inner;
        private readonly GeneratedEquipmentAugmentSignatureState signatures;
        private readonly GeneratedEquipmentAugmentSignatureState previews;

        public GeneratedAugmentSignaturePlayerHoldingsRewardChildState(
            PlayerHoldingsActions holdings,
            IEquipmentInstanceValidator equipmentValidator,
            GeneratedEquipmentAugmentSignatureState signatures)
            : this(
                holdings,
                equipmentValidator,
                signatures,
                null)
        {
        }

        public GeneratedAugmentSignaturePlayerHoldingsRewardChildState(
            PlayerHoldingsActions holdings,
            IEquipmentInstanceValidator equipmentValidator,
            GeneratedEquipmentAugmentSignatureState signatures,
            GeneratedEquipmentAugmentSignatureState previews)
        {
            this.holdings = holdings
                ?? throw new ArgumentNullException(nameof(holdings));
            inner = new PlayerHoldingsRewardChildState(
                holdings,
                equipmentValidator
                    ?? throw new ArgumentNullException(nameof(equipmentValidator)));
            this.signatures = signatures
                ?? throw new ArgumentNullException(nameof(signatures));
            this.previews = previews;
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
                    bool fromPreview;
                    if (!TryResolveSignature(
                            command.InstanceStableId,
                            out signature,
                            out fromPreview))
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
                bool fromPreview;
                if (!TryResolveSignature(
                        command.InstanceStableId,
                        out signature,
                        out fromPreview))
                {
                    return inner.Apply(command);
                }

                PlayerHoldingsSnapshot before;
                try
                {
                    before = holdings.ExportSnapshot();
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

                string diagnostic;
                bool committed = fromPreview
                    ? TryCommitPreview(signature, out diagnostic)
                    : TryCommitStaged(signature, out diagnostic);
                if (committed)
                {
                    return applied;
                }

                PlayerHoldingsImportResult compensation;
                try
                {
                    compensation = holdings.ImportSnapshot(before);
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
                if (compensation == null || !compensation.Succeeded)
                {
                    return Rejected(
                        command,
                        (string.IsNullOrWhiteSpace(diagnostic)
                            ? "generated-augment-signature-commit-rejected"
                            : diagnostic)
                        + ";holdings-compensation="
                        + (compensation == null
                            ? "result-null"
                            : compensation.RejectionCode));
                }
                return Rejected(
                    command,
                    string.IsNullOrWhiteSpace(diagnostic)
                        ? "generated-augment-signature-commit-rejected-compensated"
                        : diagnostic + ";holdings-compensated");
            }
        }

        private bool TryResolveSignature(
            StableId equipmentInstanceStableId,
            out GeneratedEquipmentAugmentSignature signature,
            out bool fromPreview)
        {
            bool committed;
            if (signatures.TryGetStagedOrCommitted(
                    equipmentInstanceStableId,
                    out signature,
                    out committed))
            {
                fromPreview = false;
                return true;
            }
            if (previews != null
                && previews.TryGetStagedOrCommitted(
                    equipmentInstanceStableId,
                    out signature,
                    out committed))
            {
                fromPreview = true;
                return true;
            }
            signature = null;
            fromPreview = false;
            return false;
        }

        private bool TryCommitStaged(
            GeneratedEquipmentAugmentSignature signature,
            out string diagnostic)
        {
            GeneratedEquipmentAugmentSignature committed;
            return signatures.TryCommitStaged(
                signature.EquipmentInstanceStableId,
                signature.Fingerprint,
                out committed,
                out diagnostic);
        }

        private bool TryCommitPreview(
            GeneratedEquipmentAugmentSignature signature,
            out string diagnostic)
        {
            IReadOnlyList<
                GeneratedEquipmentAugmentSignatureRecordResult> results;
            return signatures.TryRecordBatch(
                new[] { signature },
                out results,
                out diagnostic);
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
