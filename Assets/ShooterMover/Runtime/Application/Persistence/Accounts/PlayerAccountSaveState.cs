using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Persistence.Accounts
{
    public enum PlayerAccountSaveCommandKind
    {
        CreateCharacter = 1,
        UpsertCharacterComponent = 2,
        DeleteCharacter = 3,
        UpsertAccountComponent = 4,
    }

    public enum PlayerAccountSaveStatus
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        StaleRevision = 4,
        Rejected = 5,
    }

    public sealed class PlayerAccountSaveCommand
    {
        private PlayerAccountSaveCommand(
            StableId operationStableId,
            PlayerAccountSaveCommandKind kind,
            long expectedAccountRevision,
            int slotIndex,
            StableId expectedCharacterInstanceStableId,
            CharacterInstanceSnapshot character,
            SaveComponentSnapshot component)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            if (expectedAccountRevision < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedAccountRevision));
            }

            Kind = kind;
            ExpectedAccountRevision = expectedAccountRevision;
            SlotIndex = slotIndex;
            ExpectedCharacterInstanceStableId =
                expectedCharacterInstanceStableId;
            Character = character;
            Component = component;
            Fingerprint = SaveStateFingerprint.Hash(
                ToCanonicalString());
        }

        public StableId OperationStableId { get; }

        public PlayerAccountSaveCommandKind Kind { get; }

        public long ExpectedAccountRevision { get; }

        public int SlotIndex { get; }

        public StableId ExpectedCharacterInstanceStableId { get; }

        public CharacterInstanceSnapshot Character { get; }

        public SaveComponentSnapshot Component { get; }

        public string Fingerprint { get; }

        public static PlayerAccountSaveCommand CreateCharacter(
            StableId operationStableId,
            long expectedAccountRevision,
            CharacterInstanceSnapshot character)
        {
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }
            return new PlayerAccountSaveCommand(
                operationStableId,
                PlayerAccountSaveCommandKind.CreateCharacter,
                expectedAccountRevision,
                character.SlotIndex,
                null,
                character,
                null);
        }

        public static PlayerAccountSaveCommand UpsertCharacterComponent(
            StableId operationStableId,
            long expectedAccountRevision,
            int slotIndex,
            StableId expectedCharacterInstanceStableId,
            SaveComponentSnapshot component)
        {
            PlayerAccountSnapshot.ValidateSlotIndex(slotIndex);
            if (expectedCharacterInstanceStableId == null)
            {
                throw new ArgumentNullException(
                    nameof(expectedCharacterInstanceStableId));
            }
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }
            return new PlayerAccountSaveCommand(
                operationStableId,
                PlayerAccountSaveCommandKind.UpsertCharacterComponent,
                expectedAccountRevision,
                slotIndex,
                expectedCharacterInstanceStableId,
                null,
                component);
        }

        public static PlayerAccountSaveCommand DeleteCharacter(
            StableId operationStableId,
            long expectedAccountRevision,
            int slotIndex,
            StableId expectedCharacterInstanceStableId)
        {
            PlayerAccountSnapshot.ValidateSlotIndex(slotIndex);
            if (expectedCharacterInstanceStableId == null)
            {
                throw new ArgumentNullException(
                    nameof(expectedCharacterInstanceStableId));
            }
            return new PlayerAccountSaveCommand(
                operationStableId,
                PlayerAccountSaveCommandKind.DeleteCharacter,
                expectedAccountRevision,
                slotIndex,
                expectedCharacterInstanceStableId,
                null,
                null);
        }

        public static PlayerAccountSaveCommand UpsertAccountComponent(
            StableId operationStableId,
            long expectedAccountRevision,
            SaveComponentSnapshot component)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }
            return new PlayerAccountSaveCommand(
                operationStableId,
                PlayerAccountSaveCommandKind.UpsertAccountComponent,
                expectedAccountRevision,
                -1,
                null,
                null,
                component);
        }

        private string ToCanonicalString()
        {
            return OperationStableId
                + "|"
                + Kind
                + "|"
                + ExpectedAccountRevision.ToString(
                    CultureInfo.InvariantCulture)
                + "|"
                + SlotIndex.ToString(CultureInfo.InvariantCulture)
                + "|"
                + (ExpectedCharacterInstanceStableId == null
                    ? string.Empty
                    : ExpectedCharacterInstanceStableId.ToString())
                + "|"
                + (Character == null ? string.Empty : Character.Fingerprint)
                + "|"
                + (Component == null ? string.Empty : Component.Fingerprint);
        }
    }

    public sealed class PlayerAccountSaveResult
    {
        public PlayerAccountSaveResult(
            PlayerAccountSaveStatus status,
            string rejectionCode,
            PlayerAccountSnapshot snapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot
                ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public PlayerAccountSaveStatus Status { get; }

        public string RejectionCode { get; }

        public PlayerAccountSnapshot Snapshot { get; }
    }

    public sealed class PlayerAccountSaveReplayRecord
    {
        public PlayerAccountSaveReplayRecord(
            StableId operationStableId,
            string commandFingerprint,
            PlayerAccountSaveStatus status,
            string rejectionCode,
            PlayerAccountSnapshot resultSnapshot)
        {
            OperationStableId = operationStableId
                ?? throw new ArgumentNullException(nameof(operationStableId));
            if (string.IsNullOrWhiteSpace(commandFingerprint))
            {
                throw new ArgumentException(
                    "A command fingerprint is required.",
                    nameof(commandFingerprint));
            }
            CommandFingerprint = commandFingerprint.Trim();
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            ResultSnapshot = resultSnapshot
                ?? throw new ArgumentNullException(nameof(resultSnapshot));
            Fingerprint = SaveStateFingerprint.Hash(
                OperationStableId
                    + "|"
                    + CommandFingerprint
                    + "|"
                    + Status
                    + "|"
                    + RejectionCode
                    + "|"
                    + ResultSnapshot.Fingerprint);
        }

        public StableId OperationStableId { get; }

        public string CommandFingerprint { get; }

        public PlayerAccountSaveStatus Status { get; }

        public string RejectionCode { get; }

        public PlayerAccountSnapshot ResultSnapshot { get; }

        public string Fingerprint { get; }
    }

    public sealed class PlayerAccountSaveStateSnapshot
    {
        public PlayerAccountSaveStateSnapshot(
            PlayerAccountSnapshot account,
            IEnumerable<PlayerAccountSaveReplayRecord> replayRecords)
        {
            Account = account
                ?? throw new ArgumentNullException(nameof(account));
            var records = (replayRecords
                ?? Array.Empty<PlayerAccountSaveReplayRecord>()).ToList();
            if (records.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Replay records must be non-null.",
                    nameof(replayRecords));
            }
            if (records.Select(item => item.OperationStableId)
                .Distinct()
                .Count() != records.Count)
            {
                throw new ArgumentException(
                    "Replay operation identities must be unique.",
                    nameof(replayRecords));
            }
            if (records.Any(
                item => item.ResultSnapshot.AccountStableId
                    != Account.AccountStableId
                    || item.ResultSnapshot.Revision > Account.Revision))
            {
                throw new ArgumentException(
                    "Replay records must belong to the same account and cannot point beyond the current revision.",
                    nameof(replayRecords));
            }

            ReplayRecords = new ReadOnlyCollection<
                PlayerAccountSaveReplayRecord>(
                    records.OrderBy(
                            item => item.OperationStableId.ToString(),
                            StringComparer.Ordinal)
                        .ToList());
            Fingerprint = SaveStateFingerprint.Hash(
                Account.Fingerprint
                    + "|"
                    + string.Join(
                        ";",
                        ReplayRecords.Select(item => item.Fingerprint)));
        }

        public PlayerAccountSnapshot Account { get; }

        public IReadOnlyList<PlayerAccountSaveReplayRecord> ReplayRecords
        {
            get;
        }

        public string Fingerprint { get; }
    }

    /// <summary>
    /// Sole mutation boundary for the durable six-character aggregate. Subsystem
    /// authorities still own XP, holdings, wallets, skills, loadout, boxes, and future
    /// account services; this authority atomically installs their immutable snapshots.
    /// </summary>
    public sealed class PlayerAccountSaveState
    {
        private readonly Dictionary<StableId, PlayerAccountSaveReplayRecord>
            replay =
                new Dictionary<StableId, PlayerAccountSaveReplayRecord>();
        private PlayerAccountSnapshot account;

        public PlayerAccountSaveState(
            PlayerAccountSnapshot initialAccount)
        {
            account = initialAccount
                ?? throw new ArgumentNullException(nameof(initialAccount));
        }

        public PlayerAccountSnapshot Current
        {
            get { return account; }
        }

        public PlayerAccountSaveResult Apply(
            PlayerAccountSaveCommand command)
        {
            if (command == null)
            {
                return new PlayerAccountSaveResult(
                    PlayerAccountSaveStatus.Rejected,
                    "account-save-command-null",
                    account);
            }

            PlayerAccountSaveReplayRecord prior;
            if (replay.TryGetValue(command.OperationStableId, out prior))
            {
                if (!string.Equals(
                    prior.CommandFingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return new PlayerAccountSaveResult(
                        PlayerAccountSaveStatus.ConflictingDuplicate,
                        "account-save-operation-conflict",
                        account);
                }
                return new PlayerAccountSaveResult(
                    PlayerAccountSaveStatus.ExactDuplicateNoChange,
                    prior.RejectionCode,
                    prior.ResultSnapshot);
            }

            PlayerAccountSaveResult result;
            if (command.ExpectedAccountRevision != account.Revision)
            {
                result = new PlayerAccountSaveResult(
                    PlayerAccountSaveStatus.StaleRevision,
                    "account-save-revision-stale",
                    account);
            }
            else
            {
                result = Execute(command);
            }

            replay.Add(
                command.OperationStableId,
                new PlayerAccountSaveReplayRecord(
                    command.OperationStableId,
                    command.Fingerprint,
                    result.Status,
                    result.RejectionCode,
                    result.Snapshot));
            return result;
        }

        public PlayerAccountSaveStateSnapshot ExportSnapshot()
        {
            return new PlayerAccountSaveStateSnapshot(
                account,
                replay.Values);
        }

        public bool TryImport(
            PlayerAccountSaveStateSnapshot snapshot,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (snapshot == null)
            {
                rejectionCode = "account-save-import-null";
                return false;
            }
            if (snapshot.Account.AccountStableId != account.AccountStableId)
            {
                rejectionCode = "account-save-import-account-mismatch";
                return false;
            }

            var importedReplay = new Dictionary<
                StableId,
                PlayerAccountSaveReplayRecord>();
            foreach (PlayerAccountSaveReplayRecord record in
                snapshot.ReplayRecords)
            {
                importedReplay.Add(record.OperationStableId, record);
            }

            account = snapshot.Account;
            replay.Clear();
            foreach (KeyValuePair<
                StableId,
                PlayerAccountSaveReplayRecord> pair in importedReplay)
            {
                replay.Add(pair.Key, pair.Value);
            }
            return true;
        }

        private PlayerAccountSaveResult Execute(
            PlayerAccountSaveCommand command)
        {
            switch (command.Kind)
            {
                case PlayerAccountSaveCommandKind.CreateCharacter:
                    return CreateCharacter(command);
                case PlayerAccountSaveCommandKind.UpsertCharacterComponent:
                    return UpsertCharacterComponent(command);
                case PlayerAccountSaveCommandKind.DeleteCharacter:
                    return DeleteCharacter(command);
                case PlayerAccountSaveCommandKind.UpsertAccountComponent:
                    return UpsertAccountComponent(command);
                default:
                    return Reject("account-save-command-kind-unsupported");
            }
        }

        private PlayerAccountSaveResult CreateCharacter(
            PlayerAccountSaveCommand command)
        {
            if (command.Character == null)
            {
                return Reject("account-save-character-missing");
            }
            int slotIndex = command.Character.SlotIndex;
            if (account.CharacterAt(slotIndex) != null)
            {
                return Reject("account-save-character-slot-occupied");
            }
            if (account.CharacterSlots.Any(
                item => item != null
                    && item.CharacterInstanceStableId
                        == command.Character.CharacterInstanceStableId))
            {
                return Reject("account-save-character-id-duplicate");
            }

            account = account.WithCharacter(slotIndex, command.Character);
            return Applied();
        }

        private PlayerAccountSaveResult UpsertCharacterComponent(
            PlayerAccountSaveCommand command)
        {
            if (command.Component == null)
            {
                return Reject("account-save-component-missing");
            }
            CharacterInstanceSnapshot character =
                account.CharacterAt(command.SlotIndex);
            if (character == null)
            {
                return Reject("account-save-character-slot-empty");
            }
            if (character.CharacterInstanceStableId
                != command.ExpectedCharacterInstanceStableId)
            {
                return Reject("account-save-character-id-mismatch");
            }

            CharacterInstanceSnapshot nextCharacter =
                character.WithComponent(command.Component);
            account = account.WithCharacter(
                command.SlotIndex,
                nextCharacter);
            return Applied();
        }

        private PlayerAccountSaveResult DeleteCharacter(
            PlayerAccountSaveCommand command)
        {
            CharacterInstanceSnapshot character =
                account.CharacterAt(command.SlotIndex);
            if (character == null)
            {
                return Reject("account-save-character-slot-empty");
            }
            if (character.CharacterInstanceStableId
                != command.ExpectedCharacterInstanceStableId)
            {
                return Reject("account-save-character-id-mismatch");
            }

            account = account.WithoutCharacter(command.SlotIndex);
            return Applied();
        }

        private PlayerAccountSaveResult UpsertAccountComponent(
            PlayerAccountSaveCommand command)
        {
            if (command.Component == null)
            {
                return Reject("account-save-component-missing");
            }
            account = account.WithAccountComponent(command.Component);
            return Applied();
        }

        private PlayerAccountSaveResult Applied()
        {
            return new PlayerAccountSaveResult(
                PlayerAccountSaveStatus.Applied,
                string.Empty,
                account);
        }

        private PlayerAccountSaveResult Reject(string rejectionCode)
        {
            return new PlayerAccountSaveResult(
                PlayerAccountSaveStatus.Rejected,
                rejectionCode,
                account);
        }
    }

    internal static class SaveStateFingerprint
    {
        internal static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                return BitConverter.ToString(bytes)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }
    }
}
