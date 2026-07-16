using System;
using System.Collections.Generic;
using ShooterMover.Application.Economy.Money;
using ShooterMover.Application.Economy.Scrap;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Equipment;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Economy.Ledger;
using ShooterMover.Domain.Economy.Money;
using ShooterMover.Domain.Economy.Scrap;
using ShooterMover.Domain.Holdings;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.Application
{
    public sealed class MoneyRewardChildAuthorityV1 : IRewardChildAuthorityV1
    {
        private readonly MoneyWalletService wallet;

        public MoneyRewardChildAuthorityV1(MoneyWalletService wallet)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        public StableId AuthorityStableId { get { return MoneyWalletIdsV1.AuthorityStableId; } }
        public long Sequence { get { return wallet.Sequence; } }

        public RewardAuthorityPreflightResultV1 Preflight(
            IReadOnlyList<RewardChildGrantCommandV1> commands)
        {
            List<RewardChildGrantCommandV1> ordered = CopyForAdmission(commands);
            MoneyWalletSnapshot snapshot = wallet.CurrentSnapshot;
            long simulatedSequence = snapshot.Sequence;
            long simulatedBalance = snapshot.Balance;
            var transactions = new Dictionary<string, MoneyWalletTransactionSnapshot>(
                StringComparer.Ordinal);
            for (int index = 0; index < snapshot.Transactions.Count; index++)
            {
                transactions[snapshot.Transactions[index].TransactionStableId] =
                    snapshot.Transactions[index];
            }

            var facts = new List<RewardAuthorityPreflightFactV1>(ordered.Count);
            for (int index = 0; index < ordered.Count; index++)
            {
                RewardChildGrantCommandV1 child = ordered[index];
                if (child.GrantKind != RewardGrantKindV1.Money)
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.InvalidCommand, "money-kind-invalid"));
                    continue;
                }

                if (child.DestinationAuthorityStableId != AuthorityStableId)
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.AuthorityMismatch, "money-authority-mismatch"));
                    continue;
                }

                if (child.ContentStableId != MoneyWalletIdsV1.CurrencyStableId)
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.InvalidCommand, "money-currency-mismatch"));
                    continue;
                }

                MoneyTransactionCommand typed = MoneyTransactionCommand.CreateGrant(
                    child.TransactionStableId,
                    child.OperationStableId,
                    child.ContentStableId,
                    child.Quantity,
                    child.ExpectedSequence);
                MoneyWalletTransactionSnapshot existing;
                if (transactions.TryGetValue(
                    child.TransactionStableId.ToString(),
                    out existing))
                {
                    if (!string.Equals(
                        existing.CommandFingerprint,
                        typed.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.ConflictingDuplicate, "money-transaction-conflict"));
                    }
                    else if (existing.RecordedOutcome == MoneyWalletRecordedOutcome.Applied)
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.AlreadyApplied, existing.RejectionCode));
                    }
                    else
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.Rejected, existing.RejectionCode ?? "money-originally-rejected"));
                    }

                    continue;
                }

                if (child.ExpectedSequence.HasValue
                    && child.ExpectedSequence.Value != simulatedSequence)
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.ExpectedSequenceConflict, "money-expected-sequence-conflict"));
                    continue;
                }

                try
                {
                    simulatedBalance = checked(simulatedBalance + child.Quantity);
                    simulatedSequence = checked(simulatedSequence + 1L);
                }
                catch (OverflowException)
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.CapacityRejected, "money-balance-overflow"));
                    continue;
                }

                facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.Accepted, null));
            }

            return new RewardAuthorityPreflightResultV1(facts);
        }

        public RewardChildApplyResultV1 Apply(RewardChildGrantCommandV1 command)
        {
            if (command == null
                || command.GrantKind != RewardGrantKindV1.Money
                || command.DestinationAuthorityStableId != AuthorityStableId
                || command.ContentStableId != MoneyWalletIdsV1.CurrencyStableId)
            {
                return InvalidApply(command, "money-command-invalid");
            }

            MoneyWalletChangeFact fact = wallet.Apply(
                MoneyTransactionCommand.CreateGrant(
                    command.TransactionStableId,
                    command.OperationStableId,
                    command.ContentStableId,
                    command.Quantity,
                    command.ExpectedSequence));
            switch (fact.Status)
            {
                case MoneyWalletTransactionStatus.Applied:
                    return ApplyResult(command, RewardChildApplyStatusV1.Applied, true, fact.RejectionCode);
                case MoneyWalletTransactionStatus.DuplicateNoChange:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatusV1.ExactDuplicateNoChange,
                        fact.OriginalStatus == MoneyWalletTransactionStatus.Applied,
                        fact.RejectionCode);
                case MoneyWalletTransactionStatus.ConflictingDuplicate:
                    return ApplyResult(command, RewardChildApplyStatusV1.ConflictingDuplicate, false, fact.RejectionCode);
                case MoneyWalletTransactionStatus.SequenceConflict:
                    return ApplyResult(command, RewardChildApplyStatusV1.ExpectedSequenceConflict, false, fact.RejectionCode);
                case MoneyWalletTransactionStatus.InsufficientFunds:
                    return ApplyResult(command, RewardChildApplyStatusV1.InsufficientFunds, false, fact.RejectionCode);
                case MoneyWalletTransactionStatus.InvalidAmount:
                    return ApplyResult(command, RewardChildApplyStatusV1.CapacityRejected, false, fact.RejectionCode);
                default:
                    return ApplyResult(command, RewardChildApplyStatusV1.Rejected, false, fact.RejectionCode);
            }
        }

        private static List<RewardChildGrantCommandV1> CopyForAdmission(
            IReadOnlyList<RewardChildGrantCommandV1> commands)
        {
            return RewardAuthorityAdapterOrderingV1.CopyForAdmission(commands);
        }

        private static RewardAuthorityPreflightFactV1 Fact(
            RewardChildGrantCommandV1 command,
            RewardAuthorityAdmissionStatusV1 status,
            string code)
        {
            return new RewardAuthorityPreflightFactV1(command.TransactionStableId, status, code);
        }

        private static RewardChildApplyResultV1 InvalidApply(
            RewardChildGrantCommandV1 command,
            string code)
        {
            StableId id = command == null
                ? StableId.Parse("raptx.invalid")
                : command.TransactionStableId;
            return new RewardChildApplyResultV1(
                id,
                RewardChildApplyStatusV1.InvalidCommand,
                false,
                code);
        }

        private static RewardChildApplyResultV1 ApplyResult(
            RewardChildGrantCommandV1 command,
            RewardChildApplyStatusV1 status,
            bool originalApplied,
            string code)
        {
            return new RewardChildApplyResultV1(
                command.TransactionStableId,
                status,
                originalApplied,
                code);
        }
    }

    public sealed class ScrapRewardChildAuthorityV1 : IRewardChildAuthorityV1
    {
        private readonly ScrapWalletServiceV1 wallet;

        public ScrapRewardChildAuthorityV1(ScrapWalletServiceV1 wallet)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        public StableId AuthorityStableId { get { return wallet.AuthorityStableId; } }
        public long Sequence { get { return wallet.Sequence; } }

        public RewardAuthorityPreflightResultV1 Preflight(
            IReadOnlyList<RewardChildGrantCommandV1> commands)
        {
            List<RewardChildGrantCommandV1> ordered =
                RewardAuthorityAdapterOrderingV1.CopyForAdmission(commands);
            ScrapSnapshotV1 snapshot = wallet.ExportSnapshot();
            long simulatedSequence = snapshot.LedgerSnapshot.Sequence;
            long simulatedBalance = snapshot.Balance;
            var transactions = new Dictionary<string, LedgerTransactionSnapshot>(
                StringComparer.Ordinal);
            for (int index = 0; index < snapshot.LedgerSnapshot.Transactions.Count; index++)
            {
                LedgerTransactionSnapshot transaction =
                    snapshot.LedgerSnapshot.Transactions[index];
                transactions[transaction.TransactionId] = transaction;
            }

            var facts = new List<RewardAuthorityPreflightFactV1>(ordered.Count);
            for (int index = 0; index < ordered.Count; index++)
            {
                RewardChildGrantCommandV1 child = ordered[index];
                if (child.GrantKind != RewardGrantKindV1.Scrap)
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.InvalidCommand, "scrap-kind-invalid"));
                    continue;
                }

                if (child.DestinationAuthorityStableId != AuthorityStableId)
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.AuthorityMismatch, "scrap-authority-mismatch"));
                    continue;
                }

                if (child.ContentStableId != wallet.CurrencyStableId)
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.InvalidCommand, "scrap-currency-mismatch"));
                    continue;
                }

                ScrapTransactionCommandV1 typed = CreateTyped(child);
                var mutation = new LedgerMutation<ScrapLedgerVocabulary>(
                    typed.TransactionStableId,
                    new LedgerEntry<ScrapLedgerVocabulary>(
                        ScrapIdentityV1.BalanceEntryType,
                        typed.CurrencyStableId,
                        typed.LedgerPayload),
                    typed.GetAdmissionDelta(),
                    typed.ExpectedSequence);
                LedgerTransactionSnapshot existing;
                if (transactions.TryGetValue(
                    child.TransactionStableId.ToString(),
                    out existing))
                {
                    if (!string.Equals(
                        existing.PayloadFingerprint,
                        mutation.PayloadFingerprint,
                        StringComparison.Ordinal))
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.ConflictingDuplicate, "scrap-transaction-conflict"));
                    }
                    else if (existing.OriginalStatus == LedgerMutationStatus.Applied)
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.AlreadyApplied, existing.RejectionCode));
                    }
                    else
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.Rejected, existing.RejectionCode ?? "scrap-originally-rejected"));
                    }

                    continue;
                }

                if (child.ExpectedSequence.HasValue
                    && child.ExpectedSequence.Value != simulatedSequence)
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.ExpectedSequenceConflict, "scrap-expected-sequence-conflict"));
                    continue;
                }

                try
                {
                    simulatedBalance = checked(simulatedBalance + child.Quantity);
                    simulatedSequence = checked(simulatedSequence + 1L);
                }
                catch (OverflowException)
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.CapacityRejected, "scrap-balance-overflow"));
                    continue;
                }

                facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.Accepted, null));
            }

            return new RewardAuthorityPreflightResultV1(facts);
        }

        public RewardChildApplyResultV1 Apply(RewardChildGrantCommandV1 command)
        {
            if (command == null
                || command.GrantKind != RewardGrantKindV1.Scrap
                || command.DestinationAuthorityStableId != AuthorityStableId
                || command.ContentStableId != wallet.CurrencyStableId)
            {
                return InvalidApply(command, "scrap-command-invalid");
            }

            ScrapTransactionResultV1 result = wallet.Apply(CreateTyped(command));
            switch (result.Status)
            {
                case EconomyTransactionStatusV1.Applied:
                    return ApplyResult(command, RewardChildApplyStatusV1.Applied, true, result.ChangeFact.RejectionCode);
                case EconomyTransactionStatusV1.ExactDuplicateNoChange:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatusV1.ExactDuplicateNoChange,
                        result.ChangeFact.OriginalLedgerStatus == LedgerMutationStatus.Applied,
                        result.ChangeFact.RejectionCode);
                case EconomyTransactionStatusV1.ConflictingDuplicate:
                    return ApplyResult(command, RewardChildApplyStatusV1.ConflictingDuplicate, false, result.ChangeFact.RejectionCode);
                case EconomyTransactionStatusV1.ExpectedSequenceConflict:
                    return ApplyResult(command, RewardChildApplyStatusV1.ExpectedSequenceConflict, false, result.ChangeFact.RejectionCode);
                case EconomyTransactionStatusV1.InsufficientValue:
                    return ApplyResult(command, RewardChildApplyStatusV1.InsufficientFunds, false, result.ChangeFact.RejectionCode);
                case EconomyTransactionStatusV1.InsufficientCapacity:
                    return ApplyResult(command, RewardChildApplyStatusV1.CapacityRejected, false, result.ChangeFact.RejectionCode);
                default:
                    return ApplyResult(command, RewardChildApplyStatusV1.Rejected, false, result.ChangeFact.RejectionCode);
            }
        }

        private ScrapTransactionCommandV1 CreateTyped(RewardChildGrantCommandV1 command)
        {
            return new ScrapTransactionCommandV1(
                command.TransactionStableId,
                command.OperationStableId,
                AuthorityStableId,
                wallet.CurrencyStableId,
                ScrapMutationKindV1.Grant,
                command.Quantity,
                ScrapIdentityV1.RewardGrantReason,
                new ScrapProvenanceV1(
                    ScrapIdentityV1.RewardSourceKind,
                    command.SourceOperationStableId,
                    command.ClaimantStableId),
                command.ExpectedSequence);
        }

        private static RewardAuthorityPreflightFactV1 Fact(
            RewardChildGrantCommandV1 command,
            RewardAuthorityAdmissionStatusV1 status,
            string code)
        {
            return new RewardAuthorityPreflightFactV1(command.TransactionStableId, status, code);
        }

        private static RewardChildApplyResultV1 InvalidApply(
            RewardChildGrantCommandV1 command,
            string code)
        {
            StableId id = command == null
                ? StableId.Parse("raptx.invalid")
                : command.TransactionStableId;
            return new RewardChildApplyResultV1(id, RewardChildApplyStatusV1.InvalidCommand, false, code);
        }

        private static RewardChildApplyResultV1 ApplyResult(
            RewardChildGrantCommandV1 command,
            RewardChildApplyStatusV1 status,
            bool originalApplied,
            string code)
        {
            return new RewardChildApplyResultV1(
                command.TransactionStableId,
                status,
                originalApplied,
                code);
        }
    }

    public sealed class PlayerHoldingsRewardChildAuthorityV1 : IRewardChildAuthorityV1
    {
        private readonly IPlayerHoldingsAuthorityV1 holdings;
        private readonly IEquipmentInstanceValidator equipmentValidator;

        public PlayerHoldingsRewardChildAuthorityV1(
            IPlayerHoldingsAuthorityV1 holdings,
            IEquipmentInstanceValidator equipmentValidator)
        {
            this.holdings = holdings ?? throw new ArgumentNullException(nameof(holdings));
            this.equipmentValidator = equipmentValidator
                ?? throw new ArgumentNullException(nameof(equipmentValidator));
        }

        public StableId AuthorityStableId { get { return holdings.AuthorityStableId; } }
        public long Sequence { get { return holdings.Sequence; } }

        public RewardAuthorityPreflightResultV1 Preflight(
            IReadOnlyList<RewardChildGrantCommandV1> commands)
        {
            List<RewardChildGrantCommandV1> ordered =
                RewardAuthorityAdapterOrderingV1.CopyForAdmission(commands);
            PlayerHoldingsSnapshotV1 snapshot = holdings.ExportSnapshot();
            long simulatedSequence = snapshot.LedgerSnapshot.Sequence;
            var transactionRecords = new Dictionary<StableId, PlayerHoldingsTransactionRecordV1>();
            var usedUniqueIds = new HashSet<StableId>();
            var stackQuantities = new Dictionary<StableId, long>();
            var stackKinds = new Dictionary<StableId, RewardGrantKindV1>();

            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                usedUniqueIds.Add(snapshot.UniqueHoldings[index].InstanceStableId);
            }

            for (int index = 0; index < snapshot.StackHoldings.Count; index++)
            {
                StackHoldingSnapshotV1 stack = snapshot.StackHoldings[index];
                stackQuantities[stack.ItemStableId] = stack.Quantity;
                stackKinds[stack.ItemStableId] = stack.RewardKind;
            }

            for (int index = 0; index < snapshot.Transactions.Count; index++)
            {
                PlayerHoldingsTransactionRecordV1 record = snapshot.Transactions[index];
                transactionRecords[record.Command.Transaction.TransactionStableId] = record;
                if (record.OriginalStatus == PlayerHoldingsMutationStatusV1.Applied)
                {
                    EconomyTransactionOperationV1 operation = record.Command.Transaction.Operation;
                    if (operation == EconomyTransactionOperationV1.AddUnique)
                    {
                        usedUniqueIds.Add(record.Command.Transaction.InstanceStableId);
                    }

                    if (operation == EconomyTransactionOperationV1.AddStack
                        || operation == EconomyTransactionOperationV1.RemoveStack)
                    {
                        stackKinds[record.Command.Transaction.ResourceStableId] =
                            record.Command.RewardKind;
                    }
                }
            }

            var facts = new List<RewardAuthorityPreflightFactV1>(ordered.Count);
            for (int index = 0; index < ordered.Count; index++)
            {
                RewardChildGrantCommandV1 child = ordered[index];
                if (!IsSupportedKind(child.GrantKind))
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.InvalidCommand, "holdings-kind-invalid"));
                    continue;
                }

                if (child.DestinationAuthorityStableId != AuthorityStableId)
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.AuthorityMismatch, "holdings-authority-mismatch"));
                    continue;
                }

                PlayerHoldingsCommandV1 typed;
                try
                {
                    typed = CreateTyped(child);
                }
                catch (ArgumentException)
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.InvalidCommand, "holdings-command-invalid"));
                    continue;
                }

                PlayerHoldingsTransactionRecordV1 existing;
                if (transactionRecords.TryGetValue(
                    child.TransactionStableId,
                    out existing))
                {
                    if (!string.Equals(
                        existing.Command.PayloadFingerprint,
                        typed.PayloadFingerprint,
                        StringComparison.Ordinal))
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.ConflictingDuplicate, "holdings-transaction-conflict"));
                    }
                    else if (existing.OriginalStatus == PlayerHoldingsMutationStatusV1.Applied)
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.AlreadyApplied, existing.RejectionCode));
                    }
                    else
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.Rejected, existing.RejectionCode ?? "holdings-originally-rejected"));
                    }

                    continue;
                }

                if (child.ExpectedSequence.HasValue
                    && child.ExpectedSequence.Value != simulatedSequence)
                {
                    facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.ExpectedSequenceConflict, "holdings-expected-sequence-conflict"));
                    continue;
                }

                if (child.GrantKind == RewardGrantKindV1.EquipmentReference)
                {
                    EquipmentInstanceValidationResponse validation =
                        equipmentValidator.Validate(
                            new EquipmentInstanceValidationRequest(child.EquipmentInstance));
                    if (validation == null || !validation.IsValid)
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.Rejected, "equipment-validation-rejected"));
                        continue;
                    }
                }

                if (child.GrantKind == RewardGrantKindV1.Strongbox
                    || child.GrantKind == RewardGrantKindV1.EquipmentReference)
                {
                    if (!usedUniqueIds.Add(child.InstanceStableId))
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.CapacityRejected, "unique-instance-collision"));
                        continue;
                    }
                }
                else
                {
                    RewardGrantKindV1 historicalKind;
                    if (stackKinds.TryGetValue(child.ContentStableId, out historicalKind)
                        && historicalKind != child.GrantKind)
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.Rejected, "stack-type-mismatch"));
                        continue;
                    }

                    long current;
                    stackQuantities.TryGetValue(child.ContentStableId, out current);
                    long proposed;
                    try
                    {
                        proposed = checked(current + child.Quantity);
                    }
                    catch (OverflowException)
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.CapacityRejected, "stack-overflow"));
                        continue;
                    }

                    if (proposed > snapshot.MaximumStackQuantity)
                    {
                        facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.CapacityRejected, "stack-capacity-rejected"));
                        continue;
                    }

                    stackQuantities[child.ContentStableId] = proposed;
                    stackKinds[child.ContentStableId] = child.GrantKind;
                }

                simulatedSequence = checked(simulatedSequence + 1L);
                facts.Add(Fact(child, RewardAuthorityAdmissionStatusV1.Accepted, null));
            }

            return new RewardAuthorityPreflightResultV1(facts);
        }

        public RewardChildApplyResultV1 Apply(RewardChildGrantCommandV1 command)
        {
            if (command == null
                || !IsSupportedKind(command.GrantKind)
                || command.DestinationAuthorityStableId != AuthorityStableId)
            {
                return InvalidApply(command, "holdings-command-invalid");
            }

            PlayerHoldingsMutationResultV1 result = holdings.Apply(CreateTyped(command));
            switch (result.Status)
            {
                case PlayerHoldingsMutationStatusV1.Applied:
                    return ApplyResult(command, RewardChildApplyStatusV1.Applied, true, result.RejectionCode);
                case PlayerHoldingsMutationStatusV1.ExactDuplicateNoChange:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatusV1.ExactDuplicateNoChange,
                        result.OriginalStatus == PlayerHoldingsMutationStatusV1.Applied,
                        result.RejectionCode);
                case PlayerHoldingsMutationStatusV1.ConflictingDuplicate:
                    return ApplyResult(command, RewardChildApplyStatusV1.ConflictingDuplicate, false, result.RejectionCode);
                case PlayerHoldingsMutationStatusV1.ExpectedSequenceConflict:
                    return ApplyResult(command, RewardChildApplyStatusV1.ExpectedSequenceConflict, false, result.RejectionCode);
                case PlayerHoldingsMutationStatusV1.InsufficientValue:
                    return ApplyResult(command, RewardChildApplyStatusV1.InsufficientFunds, false, result.RejectionCode);
                case PlayerHoldingsMutationStatusV1.InsufficientCapacity:
                case PlayerHoldingsMutationStatusV1.UniqueInstanceCollision:
                case PlayerHoldingsMutationStatusV1.ArithmeticOverflow:
                    return ApplyResult(command, RewardChildApplyStatusV1.CapacityRejected, false, result.RejectionCode);
                case PlayerHoldingsMutationStatusV1.WrongAuthority:
                    return ApplyResult(command, RewardChildApplyStatusV1.AuthorityMismatch, false, result.RejectionCode);
                case PlayerHoldingsMutationStatusV1.InvalidRequest:
                case PlayerHoldingsMutationStatusV1.WrongRewardType:
                case PlayerHoldingsMutationStatusV1.TypeMismatch:
                case PlayerHoldingsMutationStatusV1.EquipmentValidationRejected:
                    return ApplyResult(command, RewardChildApplyStatusV1.InvalidCommand, false, result.RejectionCode);
                default:
                    return ApplyResult(command, RewardChildApplyStatusV1.Rejected, false, result.RejectionCode);
            }
        }

        private PlayerHoldingsCommandV1 CreateTyped(RewardChildGrantCommandV1 command)
        {
            HoldingProvenanceV1 provenance = HoldingProvenanceV1.Create(
                command.GrantStableId,
                command.SourceOperationStableId);
            switch (command.GrantKind)
            {
                case RewardGrantKindV1.EquipmentReference:
                    return PlayerHoldingsCommandV1.AddEquipment(
                        command.TransactionStableId,
                        command.OperationStableId,
                        AuthorityStableId,
                        command.EquipmentInstance,
                        provenance,
                        command.ExpectedSequence);
                case RewardGrantKindV1.Strongbox:
                    return PlayerHoldingsCommandV1.AddStrongbox(
                        command.TransactionStableId,
                        command.OperationStableId,
                        AuthorityStableId,
                        command.ContentStableId,
                        command.InstanceStableId,
                        provenance,
                        command.ExpectedSequence);
                case RewardGrantKindV1.PremiumAmmo:
                case RewardGrantKindV1.Miscellaneous:
                    return PlayerHoldingsCommandV1.AddStack(
                        command.TransactionStableId,
                        command.OperationStableId,
                        AuthorityStableId,
                        command.GrantKind,
                        command.ContentStableId,
                        command.Quantity,
                        provenance,
                        command.ExpectedSequence);
                default:
                    throw new ArgumentException("Unsupported holdings grant kind.", nameof(command));
            }
        }

        private static bool IsSupportedKind(RewardGrantKindV1 kind)
        {
            return kind == RewardGrantKindV1.EquipmentReference
                || kind == RewardGrantKindV1.Strongbox
                || kind == RewardGrantKindV1.PremiumAmmo
                || kind == RewardGrantKindV1.Miscellaneous;
        }

        private static RewardAuthorityPreflightFactV1 Fact(
            RewardChildGrantCommandV1 command,
            RewardAuthorityAdmissionStatusV1 status,
            string code)
        {
            return new RewardAuthorityPreflightFactV1(command.TransactionStableId, status, code);
        }

        private static RewardChildApplyResultV1 InvalidApply(
            RewardChildGrantCommandV1 command,
            string code)
        {
            StableId id = command == null
                ? StableId.Parse("raptx.invalid")
                : command.TransactionStableId;
            return new RewardChildApplyResultV1(id, RewardChildApplyStatusV1.InvalidCommand, false, code);
        }

        private static RewardChildApplyResultV1 ApplyResult(
            RewardChildGrantCommandV1 command,
            RewardChildApplyStatusV1 status,
            bool originalApplied,
            string code)
        {
            return new RewardChildApplyResultV1(
                command.TransactionStableId,
                status,
                originalApplied,
                code);
        }
    }

    internal static class RewardAuthorityAdapterOrderingV1
    {
        public static List<RewardChildGrantCommandV1> CopyForAdmission(
            IReadOnlyList<RewardChildGrantCommandV1> commands)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            var copy = new List<RewardChildGrantCommandV1>(commands.Count);
            for (int index = 0; index < commands.Count; index++)
            {
                if (commands[index] == null)
                {
                    throw new ArgumentException(
                        "Authority command batches must not contain null entries.",
                        nameof(commands));
                }

                copy.Add(commands[index]);
            }

            copy.Sort(Compare);
            return copy;
        }

        private static int Compare(
            RewardChildGrantCommandV1 left,
            RewardChildGrantCommandV1 right)
        {
            if (left.ExpectedSequence.HasValue && right.ExpectedSequence.HasValue)
            {
                int sequenceComparison = left.ExpectedSequence.Value.CompareTo(
                    right.ExpectedSequence.Value);
                if (sequenceComparison != 0)
                {
                    return sequenceComparison;
                }
            }
            else if (left.ExpectedSequence.HasValue)
            {
                return -1;
            }
            else if (right.ExpectedSequence.HasValue)
            {
                return 1;
            }

            int grantComparison = left.GrantStableId.CompareTo(right.GrantStableId);
            if (grantComparison != 0)
            {
                return grantComparison;
            }

            return left.TransactionStableId.CompareTo(right.TransactionStableId);
        }
    }
}
