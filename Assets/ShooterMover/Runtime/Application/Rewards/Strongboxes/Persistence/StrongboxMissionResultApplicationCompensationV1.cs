using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Application.Persistence.Components;
using ShooterMover.Application.Persistence.Composition;
using ShooterMover.Contracts.Holdings;
using ShooterMover.Contracts.Missions.Results;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;
using ShooterMover.Domain.Rewards.Strongboxes;

namespace ShooterMover.Application.Rewards.Strongboxes.Persistence
{
    public sealed partial class StrongboxMissionResultApplicationCoordinatorV1
    {
        private StrongboxMissionResultApplicationResultV1 CompensateAndReject(
            StrongboxMissionResultApplicationCommandV1 command,
            TransferPlan plan,
            string rejection)
        {
            List<string> compensationErrors = RestoreExact(command, plan);
            bool compensated = compensationErrors.Count == 0;
            return Reject(
                command,
                compensated
                    ? rejection
                    : rejection + ";compensation="
                        + string.Join(",", compensationErrors),
                compensated);
        }

        private List<string> RestoreExact(
            StrongboxMissionResultApplicationCommandV1 command,
            TransferPlan plan)
        {
            var errors = new List<string>();
            try
            {
                StrongboxOpeningImportResultV1 boxRestore =
                    plan.AuthorityPort.ImportStrongboxes(
                        plan.BeforeStrongboxes);
                if (boxRestore == null || !boxRestore.Succeeded)
                {
                    errors.Add("box:"
                        + (boxRestore == null
                            ? "null"
                            : boxRestore.RejectionCode));
                }
            }
            catch (Exception exception)
            {
                errors.Add("box-exception:"
                    + exception.GetType().Name.ToLowerInvariant());
            }

            try
            {
                PlayerHoldingsImportResultV1 holdingsRestore =
                    plan.AuthorityPort.ImportHoldings(
                        plan.BeforeHoldings);
                if (holdingsRestore == null || !holdingsRestore.Succeeded)
                {
                    errors.Add("holdings:"
                        + (holdingsRestore == null
                            ? "null"
                            : holdingsRestore.RejectionCode));
                }
            }
            catch (Exception exception)
            {
                errors.Add("holdings-exception:"
                    + exception.GetType().Name.ToLowerInvariant());
            }

            try
            {
                PlayerHoldingsSnapshotV1 holdings =
                    plan.AuthorityPort.ExportHoldings();
                if (holdings == null
                    || !string.Equals(
                        holdings.Fingerprint,
                        plan.BeforeHoldings.Fingerprint,
                        StringComparison.Ordinal))
                {
                    errors.Add("holdings-fingerprint-mismatch");
                }
            }
            catch (Exception exception)
            {
                errors.Add("holdings-verify-exception:"
                    + exception.GetType().Name.ToLowerInvariant());
            }

            try
            {
                StrongboxOpeningSnapshotV1 strongboxes =
                    plan.AuthorityPort.ExportStrongboxes();
                if (strongboxes == null
                    || !string.Equals(
                        strongboxes.Fingerprint,
                        plan.BeforeStrongboxes.Fingerprint,
                        StringComparison.Ordinal))
                {
                    errors.Add("box-fingerprint-mismatch");
                }
            }
            catch (Exception exception)
            {
                errors.Add("box-verify-exception:"
                    + exception.GetType().Name.ToLowerInvariant());
            }

            if (errors.Count == 0)
            {
                RestoreDurableCharacterIfRequired(command, plan, errors);
            }
            return errors;
        }

        private void RestoreDurableCharacterIfRequired(
            StrongboxMissionResultApplicationCommandV1 command,
            TransferPlan plan,
            ICollection<string> errors)
        {
            PlayerAccountSnapshotV1 current;
            try
            {
                current = composition.Account;
            }
            catch (Exception exception)
            {
                errors.Add("account-read-exception:"
                    + exception.GetType().Name.ToLowerInvariant());
                return;
            }
            if (current != null
                && string.Equals(
                    current.Fingerprint,
                    plan.BeforeAccount.Fingerprint,
                    StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                IReadOnlyList<SaveComponentSnapshotV1> restoredComponents =
                    PlayerAccountRestoreCoordinatorV1.ExportComponents(
                        plan.Graph.SaveAdapters);
                StableId rollbackOperation = StrongboxCanonicalV1.DeriveId(
                    "boxpersistrollback",
                    command.OperationStableId.ToString(),
                    command.Fingerprint,
                    plan.BeforeAccount.Fingerprint,
                    plan.BeforeCharacter.Fingerprint);
                CharacterCompositionResultV1 restored =
                    composition.PersistActive(rollbackOperation);
                if (restored == null || !restored.Succeeded
                    || restored.Character == null
                    || !ComponentsMatch(
                        restored.Character,
                        restoredComponents))
                {
                    errors.Add("durable:"
                        + (restored == null
                            ? "null"
                            : restored.Diagnostic));
                }
            }
            catch (Exception exception)
            {
                errors.Add("durable-exception:"
                    + exception.GetType().Name.ToLowerInvariant());
            }
        }

        private static bool ComponentsMatch(
            CharacterInstanceSnapshotV1 character,
            IReadOnlyList<SaveComponentSnapshotV1> expected)
        {
            if (character == null || expected == null)
            {
                return false;
            }
            for (int index = 0; index < expected.Count; index++)
            {
                SaveComponentSnapshotV1 actual;
                if (!character.TryGetComponent(
                        expected[index].ComponentStableId,
                        out actual)
                    || actual.SchemaVersion != expected[index].SchemaVersion
                    || !string.Equals(
                        actual.ContentVersion,
                        expected[index].ContentVersion,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        actual.CanonicalPayload,
                        expected[index].CanonicalPayload,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private StrongboxMissionResultApplicationResultV1 RejectRetryable(
            StrongboxMissionResultApplicationCommandV1 command,
            string rejection)
        {
            return Reject(command, rejection, true);
        }

        private StrongboxMissionResultApplicationResultV1 Reject(
            StrongboxMissionResultApplicationCommandV1 command,
            string rejection,
            bool exactRetryAllowed = false)
        {
            return Reject(
                command == null ? null : command.OperationStableId,
                command == null ? string.Empty : command.Fingerprint,
                command == null
                    ? string.Empty
                    : command.TerminalResult.Fingerprint,
                rejection,
                exactRetryAllowed);
        }

        private StrongboxMissionResultApplicationResultV1 Reject(
            StableId operation,
            string commandFingerprint,
            string resultFingerprint,
            string rejection,
            bool exactRetryAllowed = false)
        {
            PlayerAccountSnapshotV1 account = null;
            string accountReadError = string.Empty;
            try
            {
                account = composition.Account;
            }
            catch (Exception exception)
            {
                accountReadError = ";account-read-exception="
                    + exception.GetType().Name.ToLowerInvariant();
            }
            return new StrongboxMissionResultApplicationResultV1(
                StrongboxMissionResultApplicationStatusV1.Rejected,
                operation,
                commandFingerprint,
                resultFingerprint,
                0,
                string.Empty,
                string.Empty,
                account == null ? string.Empty : account.Fingerprint,
                (rejection ?? string.Empty) + accountReadError,
                exactRetryAllowed && accountReadError.Length == 0);
        }

        private static StableId DerivedId(
            string scope,
            StrongboxMissionResultApplicationCommandV1 command,
            MissionRunStrongboxCollectionV1 collection,
            int index)
        {
            return StrongboxCanonicalV1.DeriveId(
                scope,
                command.OperationStableId.ToString(),
                command.RunStableId.ToString(),
                command.TerminalResult.Fingerprint,
                collection.Fingerprint,
                index.ToString(CultureInfo.InvariantCulture));
        }

        private sealed class TransferItem
        {
            public TransferItem(
                MissionRunStrongboxCollectionV1 collection,
                StrongboxInstanceContextV1 context,
                bool holdingAlreadyPresent,
                bool contextAlreadyPresent,
                bool terminallyOpenedAlready)
            {
                Collection = collection;
                Context = context;
                HoldingAlreadyPresent = holdingAlreadyPresent;
                ContextAlreadyPresent = contextAlreadyPresent;
                TerminallyOpenedAlready = terminallyOpenedAlready;
            }
            public MissionRunStrongboxCollectionV1 Collection { get; }
            public StrongboxInstanceContextV1 Context { get; }
            public bool HoldingAlreadyPresent { get; }
            public bool ContextAlreadyPresent { get; }
            public bool TerminallyOpenedAlready { get; }
        }
    }
}
