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
    /// equipment grant is confirmed applied. A commit conflict compensates the holdings
    /// mutation from the captured immutable snapshot, so an opening cannot leave either
    /// an orphan signature or signature-less hybrid equipment.
    /// </summary>
    public sealed class
        GeneratedAugmentSignaturePlayerHoldingsRewardChildAuthorityV1 :
        IRewardChildAuthorityV1
    {
        private readonly PlayerHoldingsService holdings;
        private readonly PlayerHoldingsRewardChildAuthorityV1 inner;
        private readonly GeneratedEquipmentAugmentSignatureAuthorityV1 signatures;

        public GeneratedAugmentSignaturePlayerHoldingsRewardChildAuthorityV1(
            PlayerHoldingsService holdings,
            IEquipmentInstanceValidator equipmentValidator,
            GeneratedEquipmentAugmentSignatureAuthorityV1 signatures)
        {
            this.holdings = holdings
                ?? throw new ArgumentNullException(nameof(holdings));
            inner = new PlayerHoldingsRewardChildAuthorityV1(
                holdings,
                equipmentValidator
                    ?? throw new ArgumentNullException(nameof(equipmentValidator)));
            this.signatures = signatures
                ?? throw new ArgumentNullException(nameof(signatures));
        }

        public StableId AuthorityStableId
        {
            get { return inner.AuthorityStableId; }
        }

        public long Sequence
        {
            get { return inner.Sequence; }
        }

        public RewardAuthorityPreflightResultV1 Preflight(
            IReadOnlyList<RewardChildGrantCommandV1> commands)
        {
            lock (signatures)
            {
                RewardAuthorityPreflightResultV1 result =
                    inner.Preflight(commands);
                if (result == null)
                {
                    return null;
                }

                var byTransaction = new Dictionary<
                    StableId,
                    RewardChildGrantCommandV1>();
                for (int index = 0; index < commands.Count; index++)
                {
                    byTransaction[commands[index].TransactionStableId] =
                        commands[index];
                }

                var facts = new List<RewardAuthorityPreflightFactV1>(
                    result.Facts.Count);
                for (int index = 0; index < result.Facts.Count; index++)
                {
                    RewardAuthorityPreflightFactV1 fact = result.Facts[index];
                    RewardChildGrantCommandV1 command;
                    if (!fact.CanProceed
                        || !byTransaction.TryGetValue(
                            fact.TransactionStableId,
                            out command)
                        || command.GrantKind
                            != RewardGrantKindV1.EquipmentReference)
                    {
                        facts.Add(fact);
                        continue;
                    }

                    GeneratedEquipmentAugmentSignatureV1 signature;
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
                        facts.Add(new RewardAuthorityPreflightFactV1(
                            fact.TransactionStableId,
                            RewardAuthorityAdmissionStatusV1.InvalidCommand,
                            "generated-augment-signature-equipment-identity-mismatch"));
                        continue;
                    }
                    facts.Add(fact);
                }
                return new RewardAuthorityPreflightResultV1(facts);
            }
        }

        public RewardChildApplyResultV1 Apply(
            RewardChildGrantCommandV1 command)
        {
            lock (signatures)
            {
                if (command == null
                    || command.GrantKind
                        != RewardGrantKindV1.EquipmentReference)
                {
                    return inner.Apply(command);
                }

                GeneratedEquipmentAugmentSignatureV1 signature;
                bool alreadyCommitted;
                if (!signatures.TryGetStagedOrCommitted(
                        command.InstanceStableId,
                        out signature,
                        out alreadyCommitted))
                {
                    return inner.Apply(command);
                }

                PlayerHoldingsSnapshotV1 before;
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

                RewardChildApplyResultV1 applied = inner.Apply(command);
                if (applied == null || !applied.IsConfirmedApplied)
                {
                    return applied;
                }

                GeneratedEquipmentAugmentSignatureV1 committed;
                string diagnostic;
                if (signatures.TryCommitStaged(
                        command.InstanceStableId,
                        signature.Fingerprint,
                        out committed,
                        out diagnostic))
                {
                    return applied;
                }

                PlayerHoldingsImportResultV1 compensation;
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

        private static RewardChildApplyResultV1 Rejected(
            RewardChildGrantCommandV1 command,
            string diagnostic)
        {
            return new RewardChildApplyResultV1(
                command == null
                    ? StableId.Parse("raptx.invalid")
                    : command.TransactionStableId,
                RewardChildApplyStatusV1.Rejected,
                false,
                diagnostic);
        }
    }
}
