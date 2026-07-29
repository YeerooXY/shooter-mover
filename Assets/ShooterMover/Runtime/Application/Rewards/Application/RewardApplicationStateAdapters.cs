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
    public sealed class MoneyRewardChildState : IRewardChildState
    {
        private readonly MoneyWalletActions wallet;

        public MoneyRewardChildState(MoneyWalletActions wallet)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        public StableId AuthorityStableId { get { return MoneyWalletIds.AuthorityStableId; } }
        public long Sequence { get { return wallet.Sequence; } }

        public RewardStatePreflightResult Preflight(
            IReadOnlyList<RewardChildGrantCommand> commands)
        {
            List<RewardChildGrantCommand> ordered = CopyForAdmission(commands);
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

            var facts = new List<RewardStatePreflightFact>(ordered.Count);
            for (int index = 0; index < ordered.Count; index++)
            {
                RewardChildGrantCommand child = ordered[index];
                if (child.GrantKind != RewardGrantKind.Money)
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.InvalidCommand, "money-kind-invalid"));
                    continue;
                }

                if (child.DestinationAuthorityStableId != AuthorityStableId)
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.AuthorityMismatch, "money-authority-mismatch"));
                    continue;
                }

                if (child.ContentStableId != MoneyWalletIds.CurrencyStableId)
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.InvalidCommand, "money-currency-mismatch"));
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
                        facts.Add(Fact(child, RewardStateAdmissionStatus.ConflictingDuplicate, "money-transaction-conflict"));
                    }
                    else if (existing.RecordedOutcome == MoneyWalletRecordedOutcome.Applied)
                    {
                        facts.Add(Fact(child, RewardStateAdmissionStatus.AlreadyApplied, existing.RejectionCode));
                    }
                    else
                    {
                        facts.Add(Fact(child, RewardStateAdmissionStatus.Rejected, existing.RejectionCode ?? "money-originally-rejected"));
                    }

                    continue;
                }

                if (child.ExpectedSequence.HasValue
                    && child.ExpectedSequence.Value != simulatedSequence)
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.ExpectedSequenceConflict, "money-expected-sequence-conflict"));
                    continue;
                }

                try
                {
                    simulatedBalance = checked(simulatedBalance + child.Quantity);
                    simulatedSequence = checked(simulatedSequence + 1L);
                }
                catch (OverflowException)
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.CapacityRejected, "money-balance-overflow"));
                    continue;
                }

                facts.Add(Fact(child, RewardStateAdmissionStatus.Accepted, null));
            }

            return new RewardStatePreflightResult(facts);
        }

        public RewardChildApplyResult Apply(RewardChildGrantCommand command)
        {
            if (command == null
                || command.GrantKind != RewardGrantKind.Money
                || command.DestinationAuthorityStableId != AuthorityStableId
                || command.ContentStableId != MoneyWalletIds.CurrencyStableId)
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
                    return ApplyResult(command, RewardChildApplyStatus.Applied, true, fact.RejectionCode);
                case MoneyWalletTransactionStatus.DuplicateNoChange:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatus.ExactDuplicateNoChange,
                        fact.OriginalStatus == MoneyWalletTransactionStatus.Applied,
                        fact.RejectionCode);
                case MoneyWalletTransactionStatus.ConflictingDuplicate:
                    return ApplyResult(command, RewardChildApplyStatus.ConflictingDuplicate, false, fact.RejectionCode);
                case MoneyWalletTransactionStatus.SequenceConflict:
                    return ApplyResult(command, RewardChildApplyStatus.ExpectedSequenceConflict, false, fact.RejectionCode);
                case MoneyWalletTransactionStatus.InsufficientFunds:
                    return ApplyResult(command, RewardChildApplyStatus.InsufficientFunds, false, fact.RejectionCode);
                case MoneyWalletTransactionStatus.InvalidAmount:
                    return ApplyResult(command, RewardChildApplyStatus.CapacityRejected, false, fact.RejectionCode);
                default:
                    return ApplyResult(command, RewardChildApplyStatus.Rejected, false, fact.RejectionCode);
            }
        }

        private static List<RewardChildGrantCommand> CopyForAdmission(
            IReadOnlyList<RewardChildGrantCommand> commands)
        {
            return RewardStateBridgeOrdering.CopyForAdmission(commands);
        }

        private static RewardStatePreflightFact Fact(
            RewardChildGrantCommand command,
            RewardStateAdmissionStatus status,
            string code)
        {
            return new RewardStatePreflightFact(command.TransactionStableId, status, code);
        }

        private static RewardChildApplyResult InvalidApply(
            RewardChildGrantCommand command,
            string code)
        {
            StableId id = command == null
                ? StableId.Parse("raptx.invalid")
                : command.TransactionStableId;
            return new RewardChildApplyResult(
                id,
                RewardChildApplyStatus.InvalidCommand,
                false,
                code);
        }

        private static RewardChildApplyResult ApplyResult(
            RewardChildGrantCommand command,
            RewardChildApplyStatus status,
            bool originalApplied,
            string code)
        {
            return new RewardChildApplyResult(
                command.TransactionStableId,
                status,
                originalApplied,
                code);
        }
    }

    public sealed class ScrapRewardChildState : IRewardChildState
    {
        private readonly ScrapWalletActions wallet;

        public ScrapRewardChildState(ScrapWalletActions wallet)
        {
            this.wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
        }

        public StableId AuthorityStableId { get { return wallet.AuthorityStableId; } }
        public long Sequence { get { return wallet.Sequence; } }

        public RewardStatePreflightResult Preflight(
            IReadOnlyList<RewardChildGrantCommand> commands)
        {
            List<RewardChildGrantCommand> ordered =
                RewardStateBridgeOrdering.CopyForAdmission(commands);
            ScrapSnapshot snapshot = wallet.ExportSnapshot();
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

            var facts = new List<RewardStatePreflightFact>(ordered.Count);
            for (int index = 0; index < ordered.Count; index++)
            {
                RewardChildGrantCommand child = ordered[index];
                if (child.GrantKind != RewardGrantKind.Scrap)
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.InvalidCommand, "scrap-kind-invalid"));
                    continue;
                }

                if (child.DestinationAuthorityStableId != AuthorityStableId)
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.AuthorityMismatch, "scrap-authority-mismatch"));
                    continue;
                }

                if (child.ContentStableId != wallet.CurrencyStableId)
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.InvalidCommand, "scrap-currency-mismatch"));
                    continue;
                }

                ScrapTransactionCommand typed = CreateTyped(child);
                var mutation = new LedgerMutation<ScrapLedgerVocabulary>(
                    typed.TransactionStableId,
                    new LedgerEntry<ScrapLedgerVocabulary>(
                        ScrapIdentity.BalanceEntryType,
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
                        facts.Add(Fact(child, RewardStateAdmissionStatus.ConflictingDuplicate, "scrap-transaction-conflict"));
                    }
                    else if (existing.OriginalStatus == LedgerMutationStatus.Applied)
                    {
                        facts.Add(Fact(child, RewardStateAdmissionStatus.AlreadyApplied, existing.RejectionCode));
                    }
                    else
                    {
                        facts.Add(Fact(child, RewardStateAdmissionStatus.Rejected, existing.RejectionCode ?? "scrap-originally-rejected"));
                    }

                    continue;
                }

                if (child.ExpectedSequence.HasValue
                    && child.ExpectedSequence.Value != simulatedSequence)
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.ExpectedSequenceConflict, "scrap-expected-sequence-conflict"));
                    continue;
                }

                try
                {
                    simulatedBalance = checked(simulatedBalance + child.Quantity);
                    simulatedSequence = checked(simulatedSequence + 1L);
                }
                catch (OverflowException)
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.CapacityRejected, "scrap-balance-overflow"));
                    continue;
                }

                facts.Add(Fact(child, RewardStateAdmissionStatus.Accepted, null));
            }

            return new RewardStatePreflightResult(facts);
        }

        public RewardChildApplyResult Apply(RewardChildGrantCommand command)
        {
            if (command == null
                || command.GrantKind != RewardGrantKind.Scrap
                || command.DestinationAuthorityStableId != AuthorityStableId
                || command.ContentStableId != wallet.CurrencyStableId)
            {
                return InvalidApply(command, "scrap-command-invalid");
            }

            ScrapTransactionResult result = wallet.Apply(CreateTyped(command));
            switch (result.Status)
            {
                case EconomyTransactionStatus.Applied:
                    return ApplyResult(command, RewardChildApplyStatus.Applied, true, result.ChangeFact.RejectionCode);
                case EconomyTransactionStatus.ExactDuplicateNoChange:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatus.ExactDuplicateNoChange,
                        result.ChangeFact.OriginalLedgerStatus == LedgerMutationStatus.Applied,
                        result.ChangeFact.RejectionCode);
                case EconomyTransactionStatus.ConflictingDuplicate:
                    return ApplyResult(command, RewardChildApplyStatus.ConflictingDuplicate, false, result.ChangeFact.RejectionCode);
                case EconomyTransactionStatus.ExpectedSequenceConflict:
                    return ApplyResult(command, RewardChildApplyStatus.ExpectedSequenceConflict, false, result.ChangeFact.RejectionCode);
                case EconomyTransactionStatus.InsufficientValue:
                    return ApplyResult(command, RewardChildApplyStatus.InsufficientFunds, false, result.ChangeFact.RejectionCode);
                case EconomyTransactionStatus.InsufficientCapacity:
                    return ApplyResult(command, RewardChildApplyStatus.CapacityRejected, false, result.ChangeFact.RejectionCode);
                default:
                    return ApplyResult(command, RewardChildApplyStatus.Rejected, false, result.ChangeFact.RejectionCode);
            }
        }

        private ScrapTransactionCommand CreateTyped(RewardChildGrantCommand command)
        {
            return new ScrapTransactionCommand(
                command.TransactionStableId,
                command.OperationStableId,
                AuthorityStableId,
                wallet.CurrencyStableId,
                ScrapMutationKind.Grant,
                command.Quantity,
                ScrapIdentity.RewardGrantReason,
                new ScrapProvenance(
                    ScrapIdentity.LootSourceKind,
                    command.SourceOperationStableId,
                    command.ClaimantStableId),
                command.ExpectedSequence);
        }

        private static RewardStatePreflightFact Fact(
            RewardChildGrantCommand command,
            RewardStateAdmissionStatus status,
            string code)
        {
            return new RewardStatePreflightFact(command.TransactionStableId, status, code);
        }

        private static RewardChildApplyResult InvalidApply(
            RewardChildGrantCommand command,
            string code)
        {
            StableId id = command == null
                ? StableId.Parse("raptx.invalid")
                : command.TransactionStableId;
            return new RewardChildApplyResult(id, RewardChildApplyStatus.InvalidCommand, false, code);
        }

        private static RewardChildApplyResult ApplyResult(
            RewardChildGrantCommand command,
            RewardChildApplyStatus status,
            bool originalApplied,
            string code)
        {
            return new RewardChildApplyResult(
                command.TransactionStableId,
                status,
                originalApplied,
                code);
        }
    }

    public sealed class PlayerHoldingsRewardChildState : IRewardChildState
    {
        private readonly IPlayerHoldingsState holdings;
        private readonly IEquipmentInstanceValidator equipmentValidator;

        public PlayerHoldingsRewardChildState(
            IPlayerHoldingsState holdings,
            IEquipmentInstanceValidator equipmentValidator)
        {
            this.holdings = holdings ?? throw new ArgumentNullException(nameof(holdings));
            this.equipmentValidator = equipmentValidator
                ?? throw new ArgumentNullException(nameof(equipmentValidator));
        }

        public StableId AuthorityStableId { get { return holdings.AuthorityStableId; } }
        public long Sequence { get { return holdings.Sequence; } }

        public RewardStatePreflightResult Preflight(
            IReadOnlyList<RewardChildGrantCommand> commands)
        {
            List<RewardChildGrantCommand> ordered =
                RewardStateBridgeOrdering.CopyForAdmission(commands);
            PlayerHoldingsSnapshot snapshot = holdings.ExportSnapshot();
            long simulatedSequence = snapshot.LedgerSnapshot.Sequence;
            var transactionRecords = new Dictionary<StableId, PlayerHoldingsTransactionRecord>();
            var usedUniqueIds = new HashSet<StableId>();
            var stackQuantities = new Dictionary<StableId, long>();
            var stackKinds = new Dictionary<StableId, RewardGrantKind>();

            for (int index = 0; index < snapshot.UniqueHoldings.Count; index++)
            {
                usedUniqueIds.Add(snapshot.UniqueHoldings[index].InstanceStableId);
            }

            for (int index = 0; index < snapshot.StackHoldings.Count; index++)
            {
                StackHoldingSnapshot stack = snapshot.StackHoldings[index];
                stackQuantities[stack.ItemStableId] = stack.Quantity;
                stackKinds[stack.ItemStableId] = stack.RewardKind;
            }

            for (int index = 0; index < snapshot.Transactions.Count; index++)
            {
                PlayerHoldingsTransactionRecord record = snapshot.Transactions[index];
                transactionRecords[record.Command.Transaction.TransactionStableId] = record;
                if (record.OriginalStatus == PlayerHoldingsMutationStatus.Applied)
                {
                    EconomyTransactionOperation operation = record.Command.Transaction.Operation;
                    if (operation == EconomyTransactionOperation.AddUnique)
                    {
                        usedUniqueIds.Add(record.Command.Transaction.InstanceStableId);
                    }

                    if (operation == EconomyTransactionOperation.AddStack
                        || operation == EconomyTransactionOperation.RemoveStack)
                    {
                        stackKinds[record.Command.Transaction.ResourceStableId] =
                            record.Command.RewardKind;
                    }
                }
            }

            var facts = new List<RewardStatePreflightFact>(ordered.Count);
            for (int index = 0; index < ordered.Count; index++)
            {
                RewardChildGrantCommand child = ordered[index];
                if (!IsSupportedKind(child.GrantKind))
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.InvalidCommand, "holdings-kind-invalid"));
                    continue;
                }

                if (child.DestinationAuthorityStableId != AuthorityStableId)
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.AuthorityMismatch, "holdings-authority-mismatch"));
                    continue;
                }

                PlayerHoldingsCommand typed;
                try
                {
                    typed = CreateTyped(child);
                }
                catch (ArgumentException)
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.InvalidCommand, "holdings-command-invalid"));
                    continue;
                }

                PlayerHoldingsTransactionRecord existing;
                if (transactionRecords.TryGetValue(
                    child.TransactionStableId,
                    out existing))
                {
                    if (!string.Equals(
                        existing.Command.PayloadFingerprint,
                        typed.PayloadFingerprint,
                        StringComparison.Ordinal))
                    {
                        facts.Add(Fact(child, RewardStateAdmissionStatus.ConflictingDuplicate, "holdings-transaction-conflict"));
                    }
                    else if (existing.OriginalStatus == PlayerHoldingsMutationStatus.Applied)
                    {
                        facts.Add(Fact(child, RewardStateAdmissionStatus.AlreadyApplied, existing.RejectionCode));
                    }
                    else
                    {
                        facts.Add(Fact(child, RewardStateAdmissionStatus.Rejected, existing.RejectionCode ?? "holdings-originally-rejected"));
                    }

                    continue;
                }

                if (child.ExpectedSequence.HasValue
                    && child.ExpectedSequence.Value != simulatedSequence)
                {
                    facts.Add(Fact(child, RewardStateAdmissionStatus.ExpectedSequenceConflict, "holdings-expected-sequence-conflict"));
                    continue;
                }

                if (child.GrantKind == RewardGrantKind.EquipmentReference)
                {
                    EquipmentInstanceValidationResponse validation =
                        equipmentValidator.Validate(
                            new EquipmentInstanceValidationRequest(child.EquipmentInstance));
                    if (validation == null || !validation.IsValid)
                    {
                        facts.Add(Fact(child, RewardStateAdmissionStatus.Rejected, "equipment-validation-rejected"));
                        continue;
                    }
                }

                if (child.GrantKind == RewardGrantKind.Strongbox
                    || child.GrantKind == RewardGrantKind.EquipmentReference)
                {
                    if (!usedUniqueIds.Add(child.InstanceStableId))
                    {
                        facts.Add(Fact(child, RewardStateAdmissionStatus.CapacityRejected, "unique-instance-collision"));
                        continue;
                    }
                }
                else
                {
                    RewardGrantKind historicalKind;
                    if (stackKinds.TryGetValue(child.ContentStableId, out historicalKind)
                        && historicalKind != child.GrantKind)
                    {
                        facts.Add(Fact(child, RewardStateAdmissionStatus.Rejected, "stack-type-mismatch"));
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
                        facts.Add(Fact(child, RewardStateAdmissionStatus.CapacityRejected, "stack-overflow"));
                        continue;
                    }

                    if (proposed > snapshot.MaximumStackQuantity)
                    {
                        facts.Add(Fact(child, RewardStateAdmissionStatus.CapacityRejected, "stack-capacity-rejected"));
                        continue;
                    }

                    stackQuantities[child.ContentStableId] = proposed;
                    stackKinds[child.ContentStableId] = child.GrantKind;
                }

                simulatedSequence = checked(simulatedSequence + 1L);
                facts.Add(Fact(child, RewardStateAdmissionStatus.Accepted, null));
            }

            return new RewardStatePreflightResult(facts);
        }

        public RewardChildApplyResult Apply(RewardChildGrantCommand command)
        {
            if (command == null
                || !IsSupportedKind(command.GrantKind)
                || command.DestinationAuthorityStableId != AuthorityStableId)
            {
                return InvalidApply(command, "holdings-command-invalid");
            }

            PlayerHoldingsMutationResult result = holdings.Apply(CreateTyped(command));
            switch (result.Status)
            {
                case PlayerHoldingsMutationStatus.Applied:
                    return ApplyResult(command, RewardChildApplyStatus.Applied, true, result.RejectionCode);
                case PlayerHoldingsMutationStatus.ExactDuplicateNoChange:
                    return ApplyResult(
                        command,
                        RewardChildApplyStatus.ExactDuplicateNoChange,
                        result.OriginalStatus == PlayerHoldingsMutationStatus.Applied,
                        result.RejectionCode);
                case PlayerHoldingsMutationStatus.ConflictingDuplicate:
                    return ApplyResult(command, RewardChildApplyStatus.ConflictingDuplicate, false, result.RejectionCode);
                case PlayerHoldingsMutationStatus.ExpectedSequenceConflict:
                    return ApplyResult(command, RewardChildApplyStatus.ExpectedSequenceConflict, false, result.RejectionCode);
                case PlayerHoldingsMutationStatus.InsufficientValue:
                    return ApplyResult(command, RewardChildApplyStatus.InsufficientFunds, false, result.RejectionCode);
                case PlayerHoldingsMutationStatus.InsufficientCapacity:
                case PlayerHoldingsMutationStatus.UniqueInstanceCollision:
                case PlayerHoldingsMutationStatus.ArithmeticOverflow:
                    return ApplyResult(command, RewardChildApplyStatus.CapacityRejected, false, result.RejectionCode);
                case PlayerHoldingsMutationStatus.WrongAuthority:
                    return ApplyResult(command, RewardChildApplyStatus.AuthorityMismatch, false, result.RejectionCode);
                case PlayerHoldingsMutationStatus.InvalidRequest:
                case PlayerHoldingsMutationStatus.WrongRewardType:
                case PlayerHoldingsMutationStatus.TypeMismatch:
                case PlayerHoldingsMutationStatus.EquipmentValidationRejected:
                    return ApplyResult(command, RewardChildApplyStatus.InvalidCommand, false, result.RejectionCode);
                default:
                    return ApplyResult(command, RewardChildApplyStatus.Rejected, false, result.RejectionCode);
            }
        }

        private PlayerHoldingsCommand CreateTyped(RewardChildGrantCommand command)
        {
            HoldingProvenance provenance = HoldingProvenance.Create(
                command.GrantStableId,
                command.SourceOperationStableId);
            switch (command.GrantKind)
            {
                case RewardGrantKind.EquipmentReference:
                    return PlayerHoldingsCommand.AddEquipment(
                        command.TransactionStableId,
                        command.OperationStableId,
                        AuthorityStableId,
                        command.EquipmentInstance,
                        provenance,
                        command.ExpectedSequence);
                case RewardGrantKind.Strongbox:
                    return PlayerHoldingsCommand.AddStrongbox(
                        command.TransactionStableId,
                        command.OperationStableId,
                        AuthorityStableId,
                        command.ContentStableId,
                        command.InstanceStableId,
                        provenance,
                        command.ExpectedSequence);
                case RewardGrantKind.PremiumAmmo:
                case RewardGrantKind.Miscellaneous:
                    return PlayerHoldingsCommand.AddStack(
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

        private static bool IsSupportedKind(RewardGrantKind kind)
        {
            return kind == RewardGrantKind.EquipmentReference
                || kind == RewardGrantKind.Strongbox
                || kind == RewardGrantKind.PremiumAmmo
                || kind == RewardGrantKind.Miscellaneous;
        }

        private static RewardStatePreflightFact Fact(
            RewardChildGrantCommand command,
            RewardStateAdmissionStatus status,
            string code)
        {
            return new RewardStatePreflightFact(command.TransactionStableId, status, code);
        }

        private static RewardChildApplyResult InvalidApply(
            RewardChildGrantCommand command,
            string code)
        {
            StableId id = command == null
                ? StableId.Parse("raptx.invalid")
                : command.TransactionStableId;
            return new RewardChildApplyResult(id, RewardChildApplyStatus.InvalidCommand, false, code);
        }

        private static RewardChildApplyResult ApplyResult(
            RewardChildGrantCommand command,
            RewardChildApplyStatus status,
            bool originalApplied,
            string code)
        {
            return new RewardChildApplyResult(
                command.TransactionStableId,
                status,
                originalApplied,
                code);
        }
    }

    internal static class RewardStateBridgeOrdering
    {
        public static List<RewardChildGrantCommand> CopyForAdmission(
            IReadOnlyList<RewardChildGrantCommand> commands)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            var copy = new List<RewardChildGrantCommand>(commands.Count);
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
            RewardChildGrantCommand left,
            RewardChildGrantCommand right)
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
