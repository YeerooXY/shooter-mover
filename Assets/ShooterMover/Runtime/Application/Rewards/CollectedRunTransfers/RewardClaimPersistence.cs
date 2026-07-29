using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Persistence.SaveParts;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Uses the existing CharacterCompositionCoordinator and atomic account store. Exact
    /// custody/receipt component fingerprints are installed as validator expectations before
    /// PersistActive, so verification occurs before replacement and during active read-back.
    ///
    /// Persistence certainty is explicit at this boundary. RejectedBeforeReplacement is
    /// returned only before PersistActive is invoked. Once the account-save callback may have
    /// run, a throw, null result, rejected result, or read-back mismatch is
    /// DurableStateUncertain. No diagnostic substring is used to infer whether replacement
    /// happened.
    /// </summary>
    public sealed class RewardClaimPersistence :
        IRewardClaimTransferPersistencePort
    {
        private readonly CharacterSetupFlow composition;
        private readonly RewardClaimPreparedTransferStore prepared;
        private readonly RewardClaimTransferReceiptState receipts;
        private readonly StableId selectedCharacterStableId;

        public RewardClaimPersistence(
            CharacterSetupFlow composition,
            RewardClaimPreparedTransferStore prepared,
            RewardClaimTransferReceiptState receipts,
            StableId selectedCharacterStableId)
        {
            this.composition = composition
                ?? throw new ArgumentNullException(nameof(composition));
            this.prepared = prepared
                ?? throw new ArgumentNullException(nameof(prepared));
            this.receipts = receipts
                ?? throw new ArgumentNullException(nameof(receipts));
            this.selectedCharacterStableId = selectedCharacterStableId
                ?? throw new ArgumentNullException(
                    nameof(selectedCharacterStableId));
        }

        public bool IsAvailable
        {
            get
            {
                return composition.ActiveRuntime != null
                    && !composition.ActiveRuntime.IsDisposed
                    && composition.ActiveRuntime.Character
                        .CharacterInstanceStableId
                        == selectedCharacterStableId;
            }
        }

        public RewardClaimTransferPersistenceResult
            PersistPreparedCustody(
                RewardClaimPreparedTransfer preparedTransfer)
        {
            if (!IsAvailable
                || preparedTransfer == null
                || preparedTransfer.SelectedCharacterStableId
                    != selectedCharacterStableId)
            {
                return RejectedBeforeReplacement(
                    "collected-run-transfer-custody-persistence-context-invalid");
            }

            string transitionDiagnostic;
            if (!ValidateExactTransition(
                preparedTransfer,
                out transitionDiagnostic))
            {
                return RejectedBeforeReplacement(transitionDiagnostic);
            }

            string upsertDiagnostic;
            RewardClaimTransferStateStatus upsert =
                prepared.Upsert(preparedTransfer, out upsertDiagnostic);
            if (upsert != RewardClaimTransferStateStatus.Applied
                && upsert
                    != RewardClaimTransferStateStatus.ExactReplay)
            {
                return RejectedBeforeReplacement(
                    "collected-run-transfer-custody-upsert-rejected:"
                    + upsertDiagnostic);
            }

            SavePartSnapshot preparedComponent =
                PreparedComponent(prepared.ExportSnapshot());
            var expected = new Dictionary<StableId, string>
            {
                {
                    RewardClaimPreparedTransferSavePart
                        .ComponentStableId,
                    preparedComponent.Fingerprint
                },
            };

            CharacterSetupResult persisted;
            try
            {
                using (RewardClaimPersistenceExpectation.Begin(
                    selectedCharacterStableId,
                    expected))
                {
                    persisted = composition.PersistActive(
                        CustodySaveOperation(preparedTransfer));
                }
            }
            catch (Exception exception)
            {
                return Uncertain(
                    null,
                    "custody-persist-threw-"
                    + exception.GetType().Name);
            }

            if (persisted == null || !persisted.Succeeded)
            {
                return Uncertain(persisted, "custody-persist-not-verified");
            }
            if (!HasExactComponent(
                persisted.Character,
                preparedComponent))
            {
                return Uncertain(
                    persisted,
                    "custody-active-component-mismatch");
            }
            return Success(
                persisted,
                RewardClaimTransferPersistenceStatus
                    .PreparedAndVerified);
        }

        public RewardClaimTransferPersistenceResult
            PersistAppliedAndVerify(
                RewardClaimPreparedTransfer persistedTransfer,
                RewardClaimTransferReceipt receipt)
        {
            if (!IsAvailable
                || persistedTransfer == null
                || persistedTransfer.State
                    != RewardClaimPreparedTransferState.Persisted
                || receipt == null
                || persistedTransfer.TransferOperationStableId
                    != receipt.OperationStableId
                || !string.Equals(
                    persistedTransfer.PersistedReceiptFingerprint,
                    receipt.Fingerprint,
                    StringComparison.Ordinal))
            {
                return RejectedBeforeReplacement(
                    "collected-run-transfer-final-persistence-context-invalid");
            }

            string transitionDiagnostic;
            if (!ValidateExactTransition(
                persistedTransfer,
                out transitionDiagnostic))
            {
                return RejectedBeforeReplacement(transitionDiagnostic);
            }

            string upsertDiagnostic;
            RewardClaimTransferStateStatus upsert =
                prepared.Upsert(persistedTransfer, out upsertDiagnostic);
            if (upsert != RewardClaimTransferStateStatus.Applied
                && upsert
                    != RewardClaimTransferStateStatus.ExactReplay)
            {
                return RejectedBeforeReplacement(
                    "collected-run-transfer-persisted-custody-upsert-rejected:"
                    + upsertDiagnostic);
            }

            RewardClaimTransferReceipt exactReceipt;
            if (!receipts.TryGetByOperation(
                    receipt.OperationStableId,
                    out exactReceipt)
                || exactReceipt == null
                || !string.Equals(
                    exactReceipt.Fingerprint,
                    receipt.Fingerprint,
                    StringComparison.Ordinal))
            {
                return RejectedBeforeReplacement(
                    "collected-run-transfer-final-receipt-live-mismatch");
            }

            SavePartSnapshot preparedComponent =
                PreparedComponent(prepared.ExportSnapshot());
            SavePartSnapshot receiptComponent =
                ReceiptComponent(receipts.ExportSnapshot());
            var expected = new Dictionary<StableId, string>
            {
                {
                    RewardClaimPreparedTransferSavePart
                        .ComponentStableId,
                    preparedComponent.Fingerprint
                },
                {
                    RewardClaimTransferReceiptSavePart
                        .ComponentStableId,
                    receiptComponent.Fingerprint
                },
            };

            CharacterSetupResult persisted;
            try
            {
                using (RewardClaimPersistenceExpectation.Begin(
                    selectedCharacterStableId,
                    expected))
                {
                    persisted = composition.PersistActive(
                        RewardClaimTransfer.DeriveStableId(
                            "operation",
                            "collected-run-transfer-final-save",
                            persistedTransfer.ApplicationPlanFingerprint));
                }
            }
            catch (Exception exception)
            {
                return Uncertain(
                    null,
                    "final-persist-threw-"
                    + exception.GetType().Name);
            }

            if (persisted == null || !persisted.Succeeded)
            {
                return Uncertain(persisted, "final-persist-not-verified");
            }
            if (!HasExactComponent(persisted.Character, preparedComponent)
                || !HasExactComponent(persisted.Character, receiptComponent))
            {
                return Uncertain(
                    persisted,
                    "final-active-component-mismatch");
            }
            return Success(
                persisted,
                persisted.Status == CharacterSetupStatus.ExactNoChange
                    ? RewardClaimTransferPersistenceStatus
                        .AlreadyPersisted
                    : RewardClaimTransferPersistenceStatus
                        .PersistedAndVerified);
        }

        private bool ValidateExactTransition(
            RewardClaimPreparedTransfer incoming,
            out string diagnostic)
        {
            diagnostic = string.Empty;
            RewardClaimPreparedTransfer existing;
            if (!prepared.TryGetByCustody(
                incoming.CustodyStableId,
                out existing))
            {
                if (incoming.State
                    != RewardClaimPreparedTransferState
                        .AwaitingAcceptedEnd)
                {
                    diagnostic =
                        "collected-run-transfer-custody-missing-base-state";
                    return false;
                }
                return true;
            }
            if (string.Equals(
                existing.Fingerprint,
                incoming.Fingerprint,
                StringComparison.Ordinal))
            {
                return true;
            }

            RewardClaimPreparedTransfer expected;
            if (existing.State
                    == RewardClaimPreparedTransferState
                        .AwaitingAcceptedEnd
                && incoming.State
                    == RewardClaimPreparedTransferState.Prepared)
            {
                expected = existing.AcceptEnd(
                    incoming.TransferOperationStableId,
                    incoming.AcceptedMissionResultStableId,
                    incoming.AcceptedMissionResultFingerprint,
                    incoming.BatchFingerprint,
                    incoming.ApplicationPlanFingerprint);
            }
            else if (existing.State
                    == RewardClaimPreparedTransferState.Prepared
                && incoming.State
                    == RewardClaimPreparedTransferState.Persisted)
            {
                expected = existing.MarkPersisted(
                    incoming.PersistedReceiptFingerprint);
            }
            else
            {
                diagnostic =
                    "collected-run-transfer-custody-transition-invalid:"
                    + existing.State
                    + "->"
                    + incoming.State;
                return false;
            }

            if (!string.Equals(
                expected.Fingerprint,
                incoming.Fingerprint,
                StringComparison.Ordinal))
            {
                diagnostic =
                    "collected-run-transfer-custody-transition-content-conflict";
                return false;
            }
            return true;
        }

        private static StableId CustodySaveOperation(
            RewardClaimPreparedTransfer transfer)
        {
            return RewardClaimTransfer.DeriveStableId(
                "operation",
                "collected-run-custody-save",
                transfer.CustodyStableId.ToString()
                    + "|"
                    + ((int)transfer.State).ToString(
                    CultureInfo.InvariantCulture)
                    + "|"
                    + transfer.Fingerprint);
        }

        private static SavePartSnapshot PreparedComponent(
            RewardClaimPreparedTransferSnapshot snapshot)
        {
            SavePartDefinition definition =
                RewardClaimPreparedTransferSavePart.Definition();
            return new SavePartSnapshot(
                definition.ComponentStableId,
                definition.SchemaVersion,
                definition.ContentVersion,
                RewardClaimPreparedTransferSavePart.Codec.Instance
                    .Encode(snapshot));
        }

        private static SavePartSnapshot ReceiptComponent(
            RewardClaimTransferReceiptSnapshot snapshot)
        {
            SavePartDefinition definition =
                RewardClaimTransferReceiptSavePart.Definition();
            ISavePart adapter =
                RewardClaimTransferReceiptSavePart.CreateAdapter(
                    new RewardClaimTransferReceiptState(snapshot));
            SavePartSnapshot component = adapter.ExportComponent();
            if (component.ComponentStableId != definition.ComponentStableId)
                throw new InvalidOperationException(
                    "Collected-run receipt adapter identity mismatch.");
            return component;
        }

        private static bool HasExactComponent(
            CharacterInstanceSnapshot character,
            SavePartSnapshot expected)
        {
            SavePartSnapshot actual;
            return character != null
                && expected != null
                && character.TryGetComponent(
                    expected.ComponentStableId,
                    out actual)
                && actual != null
                && string.Equals(
                    actual.Fingerprint,
                    expected.Fingerprint,
                    StringComparison.Ordinal);
        }

        private static RewardClaimTransferPersistenceResult Success(
            CharacterSetupResult result,
            RewardClaimTransferPersistenceStatus status)
        {
            return new RewardClaimTransferPersistenceResult(
                status,
                result.Account == null ? 0L : result.Account.Revision,
                result.Account == null
                    ? string.Empty
                    : result.Account.Fingerprint,
                result.Character == null ? 0L : result.Character.Revision,
                result.Character == null
                    ? string.Empty
                    : result.Character.Fingerprint,
                string.Empty);
        }

        private static RewardClaimTransferPersistenceResult Uncertain(
            CharacterSetupResult result,
            string boundary)
        {
            return new RewardClaimTransferPersistenceResult(
                RewardClaimTransferPersistenceStatus
                    .DurableStateUncertain,
                result == null || result.Account == null
                    ? 0L
                    : result.Account.Revision,
                result == null || result.Account == null
                    ? string.Empty
                    : result.Account.Fingerprint,
                result == null || result.Character == null
                    ? 0L
                    : result.Character.Revision,
                result == null || result.Character == null
                    ? string.Empty
                    : result.Character.Fingerprint,
                "collected-run-transfer-"
                    + boundary
                    + "-durable-state-uncertain:"
                    + (result == null
                        ? "result-unavailable"
                        : result.Diagnostic));
        }

        private static RewardClaimTransferPersistenceResult
            RejectedBeforeReplacement(string diagnostic)
        {
            return new RewardClaimTransferPersistenceResult(
                RewardClaimTransferPersistenceStatus
                    .RejectedBeforeReplacement,
                0L,
                string.Empty,
                0L,
                string.Empty,
                diagnostic);
        }
    }

    public sealed class RewardClaimTransferActions
    {
        private readonly RewardClaimAtomicPlan plan;
        private readonly RewardClaimTransferFlow coordinator;

        public RewardClaimTransferActions(
            RewardClaimAtomicPlan plan,
            IRewardClaimAtomicBatchStatePort authority,
            IRewardClaimTransferPersistencePort persistence)
        {
            this.plan = plan
                ?? throw new ArgumentNullException(nameof(plan));
            coordinator = new RewardClaimTransferFlow(
                authority
                    ?? throw new ArgumentNullException(nameof(authority)),
                persistence
                    ?? throw new ArgumentNullException(nameof(persistence)));
        }

        public RewardClaimTransferResult Apply()
        {
            return coordinator.Apply(plan);
        }
    }
}
