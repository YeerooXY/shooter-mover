using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Rewards.CollectedRunTransfers
{
    /// <summary>
    /// Uses the existing CharacterCompositionCoordinator and atomic account store. Exact
    /// custody/receipt component fingerprints are installed as validator expectations before
    /// PersistActive, so verification occurs before replacement and during active read-back.
    /// </summary>
    public sealed class ProductionCollectedRunRewardPersistenceV2 :
        ICollectedRunRewardTransferPersistencePortV1
    {
        private readonly CharacterCompositionCoordinatorV1 composition;
        private readonly CollectedRunRewardPreparedTransferAuthorityV1 prepared;
        private readonly CollectedRunRewardTransferReceiptAuthorityV1 receipts;
        private readonly StableId selectedCharacterStableId;

        public ProductionCollectedRunRewardPersistenceV2(
            CharacterCompositionCoordinatorV1 composition,
            CollectedRunRewardPreparedTransferAuthorityV1 prepared,
            CollectedRunRewardTransferReceiptAuthorityV1 receipts,
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

        public CollectedRunRewardTransferPersistenceResultV1
            PersistPreparedCustody(
                CollectedRunRewardPreparedTransferV1 preparedTransfer)
        {
            if (!IsAvailable
                || preparedTransfer == null
                || preparedTransfer.SelectedCharacterStableId
                    != selectedCharacterStableId)
            {
                return Rejected(
                    "collected-run-transfer-custody-persistence-context-invalid");
            }

            CollectedRunRewardPreparedTransferSnapshotV1 rollback =
                prepared.ExportSnapshot();
            string upsertDiagnostic;
            CollectedRunRewardTransferAuthorityStatusV1 upsert =
                prepared.Upsert(preparedTransfer, out upsertDiagnostic);
            if (upsert != CollectedRunRewardTransferAuthorityStatusV1.Applied
                && upsert
                    != CollectedRunRewardTransferAuthorityStatusV1.ExactReplay)
            {
                return Rejected(
                    "collected-run-transfer-custody-upsert-rejected:"
                    + upsertDiagnostic);
            }

            SaveComponentSnapshotV1 preparedComponent =
                PreparedComponent(prepared.ExportSnapshot());
            var expected = new Dictionary<StableId, string>
            {
                {
                    CollectedRunRewardPreparedTransferSaveComponentV1
                        .ComponentStableId,
                    preparedComponent.Fingerprint
                },
            };
            CharacterCompositionResultV1 persisted;
            try
            {
                using (CollectedRunRewardPersistenceExpectationV1.Begin(
                    selectedCharacterStableId,
                    expected))
                {
                    persisted = composition.PersistActive(
                        CustodySaveOperation(preparedTransfer));
                }
            }
            catch (Exception exception)
            {
                // This phase has not mutated permanent reward authorities. Even if the
                // custody component reached disk, the same state/fingerprint operation can
                // be retried without applying loot.
                prepared.ImportSnapshot(rollback);
                return Rejected(
                    "collected-run-transfer-custody-persist-threw:"
                    + exception.GetType().Name);
            }

            if (persisted == null || !persisted.Succeeded)
            {
                if (IsDurableStateUncertain(persisted))
                    return Uncertain(persisted, "custody");
                prepared.ImportSnapshot(rollback);
                return Rejected(
                    persisted == null
                        ? "collected-run-transfer-custody-persist-result-null"
                        : "collected-run-transfer-custody-persist-rejected:"
                            + persisted.Diagnostic);
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
                CollectedRunRewardTransferPersistenceStatusV1
                    .PreparedAndVerified);
        }

        public CollectedRunRewardTransferPersistenceResultV1
            PersistAppliedAndVerify(
                CollectedRunRewardPreparedTransferV1 persistedTransfer,
                CollectedRunRewardTransferReceiptV1 receipt)
        {
            if (!IsAvailable
                || persistedTransfer == null
                || persistedTransfer.State
                    != CollectedRunRewardPreparedTransferStateV1.Persisted
                || receipt == null
                || persistedTransfer.TransferOperationStableId
                    != receipt.OperationStableId
                || !string.Equals(
                    persistedTransfer.PersistedReceiptFingerprint,
                    receipt.Fingerprint,
                    StringComparison.Ordinal))
            {
                return Rejected(
                    "collected-run-transfer-final-persistence-context-invalid");
            }

            string upsertDiagnostic;
            CollectedRunRewardTransferAuthorityStatusV1 upsert =
                prepared.Upsert(persistedTransfer, out upsertDiagnostic);
            if (upsert != CollectedRunRewardTransferAuthorityStatusV1.Applied
                && upsert
                    != CollectedRunRewardTransferAuthorityStatusV1.ExactReplay)
            {
                return Rejected(
                    "collected-run-transfer-persisted-custody-upsert-rejected:"
                    + upsertDiagnostic);
            }

            CollectedRunRewardTransferReceiptV1 exactReceipt;
            if (!receipts.TryGetByOperation(
                    receipt.OperationStableId,
                    out exactReceipt)
                || exactReceipt == null
                || !string.Equals(
                    exactReceipt.Fingerprint,
                    receipt.Fingerprint,
                    StringComparison.Ordinal))
            {
                return Rejected(
                    "collected-run-transfer-final-receipt-live-mismatch");
            }

            SaveComponentSnapshotV1 preparedComponent =
                PreparedComponent(prepared.ExportSnapshot());
            SaveComponentSnapshotV1 receiptComponent =
                ReceiptComponent(receipts.ExportSnapshot());
            var expected = new Dictionary<StableId, string>
            {
                {
                    CollectedRunRewardPreparedTransferSaveComponentV1
                        .ComponentStableId,
                    preparedComponent.Fingerprint
                },
                {
                    CollectedRunRewardTransferReceiptSaveComponentV1
                        .ComponentStableId,
                    receiptComponent.Fingerprint
                },
            };

            CharacterCompositionResultV1 persisted;
            try
            {
                using (CollectedRunRewardPersistenceExpectationV1.Begin(
                    selectedCharacterStableId,
                    expected))
                {
                    persisted = composition.PersistActive(
                        CollectedRunRewardTransferCanonicalV1.DeriveStableId(
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
                return IsDurableStateUncertain(persisted)
                    ? Uncertain(persisted, "final")
                    : Rejected(
                        persisted == null
                            ? "collected-run-transfer-final-persist-result-null"
                            : "collected-run-transfer-final-persist-rejected:"
                                + persisted.Diagnostic);
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
                persisted.Status == CharacterCompositionStatusV1.ExactNoChange
                    ? CollectedRunRewardTransferPersistenceStatusV1
                        .AlreadyPersisted
                    : CollectedRunRewardTransferPersistenceStatusV1
                        .PersistedAndVerified);
        }

        private static StableId CustodySaveOperation(
            CollectedRunRewardPreparedTransferV1 transfer)
        {
            return CollectedRunRewardTransferCanonicalV1.DeriveStableId(
                "operation",
                "collected-run-custody-save",
                transfer.CustodyStableId.ToString(),
                ((int)transfer.State).ToString(
                    CultureInfo.InvariantCulture),
                transfer.Fingerprint);
        }

        private static SaveComponentSnapshotV1 PreparedComponent(
            CollectedRunRewardPreparedTransferSnapshotV1 snapshot)
        {
            SaveComponentDefinitionV1 definition =
                CollectedRunRewardPreparedTransferSaveComponentV1.Definition();
            return new SaveComponentSnapshotV1(
                definition.ComponentStableId,
                definition.SchemaVersion,
                definition.ContentVersion,
                CollectedRunRewardPreparedTransferSaveComponentV1.Codec.Instance
                    .Encode(snapshot));
        }

        private static SaveComponentSnapshotV1 ReceiptComponent(
            CollectedRunRewardTransferReceiptSnapshotV1 snapshot)
        {
            SaveComponentDefinitionV1 definition =
                CollectedRunRewardTransferReceiptSaveComponentV1.Definition();
            ISaveComponentAdapterV1 adapter =
                CollectedRunRewardTransferReceiptSaveComponentV1.CreateAdapter(
                    new CollectedRunRewardTransferReceiptAuthorityV1(snapshot));
            SaveComponentSnapshotV1 component = adapter.ExportComponent();
            if (component.ComponentStableId != definition.ComponentStableId)
                throw new InvalidOperationException(
                    "Collected-run receipt adapter identity mismatch.");
            return component;
        }

        private static bool HasExactComponent(
            CharacterInstanceSnapshotV1 character,
            SaveComponentSnapshotV1 expected)
        {
            SaveComponentSnapshotV1 actual;
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

        private static bool IsDurableStateUncertain(
            CharacterCompositionResultV1 result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.Diagnostic))
                return false;
            return result.Diagnostic.IndexOf(
                    "active-readback-",
                    StringComparison.Ordinal) >= 0
                || result.Diagnostic.IndexOf(
                    "account-save-io-failure",
                    StringComparison.Ordinal) >= 0;
        }

        private static CollectedRunRewardTransferPersistenceResultV1 Success(
            CharacterCompositionResultV1 result,
            CollectedRunRewardTransferPersistenceStatusV1 status)
        {
            return new CollectedRunRewardTransferPersistenceResultV1(
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

        private static CollectedRunRewardTransferPersistenceResultV1 Uncertain(
            CharacterCompositionResultV1 result,
            string boundary)
        {
            return new CollectedRunRewardTransferPersistenceResultV1(
                CollectedRunRewardTransferPersistenceStatusV1
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

        private static CollectedRunRewardTransferPersistenceResultV1 Rejected(
            string diagnostic)
        {
            return new CollectedRunRewardTransferPersistenceResultV1(
                CollectedRunRewardTransferPersistenceStatusV1.Rejected,
                0L,
                string.Empty,
                0L,
                string.Empty,
                diagnostic);
        }
    }

    public sealed class ProductionCollectedRunRewardTransferServiceV2
    {
        private readonly CollectedRunRewardAtomicPlanV2 plan;
        private readonly CollectedRunRewardTransferCoordinatorV2 coordinator;

        public ProductionCollectedRunRewardTransferServiceV2(
            CollectedRunRewardAtomicPlanV2 plan,
            ICollectedRunRewardAtomicBatchAuthorityPortV1 authority,
            ICollectedRunRewardTransferPersistencePortV1 persistence)
        {
            this.plan = plan
                ?? throw new ArgumentNullException(nameof(plan));
            coordinator = new CollectedRunRewardTransferCoordinatorV2(
                authority
                    ?? throw new ArgumentNullException(nameof(authority)),
                persistence
                    ?? throw new ArgumentNullException(nameof(persistence)));
        }

        public CollectedRunRewardTransferResultV1 Apply()
        {
            return coordinator.Apply(plan);
        }
    }
}
