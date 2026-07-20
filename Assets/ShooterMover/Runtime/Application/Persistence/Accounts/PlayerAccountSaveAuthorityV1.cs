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
    public enum PlayerAccountSaveCommandKindV1
    {
        CreateCharacter = 1,
        UpsertCharacterComponent = 2,
        DeleteCharacter = 3,
        UpsertAccountComponent = 4,
    }

    public enum PlayerAccountSaveStatusV1
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        StaleRevision = 4,
        Rejected = 5,
    }

    public sealed class PlayerAccountSaveCommandV1
    {
        private PlayerAccountSaveCommandV1(
            StableId operationStableId,
            PlayerAccountSaveCommandKindV1 kind,
            long expectedAccountRevision,
            int slotIndex,
            StableId expectedCharacterInstanceStableId,
            CharacterInstanceSnapshotV1 character,
            SaveComponentSnapshotV1 component)
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
            Fingerprint = SaveAuthorityFingerprintV1.Hash(
                ToCanonicalString());
        }

        public StableId OperationStableId { get; }

        public PlayerAccountSaveCommandKindV1 Kind { get; }

        public long ExpectedAccountRevision { get; }

        public int SlotIndex { get; }

        public StableId ExpectedCharacterInstanceStableId { get; }

        public CharacterInstanceSnapshotV1 Character { get; }

        public SaveComponentSnapshotV1 Component { get; }

        public string Fingerprint { get; }

        public static PlayerAccountSaveCommandV1 CreateCharacter(
            StableId operationStableId,
            long expectedAccountRevision,
            CharacterInstanceSnapshotV1 character)
        {
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }
            return new PlayerAccountSaveCommandV1(
                operationStableId,
                PlayerAccountSaveCommandKindV1.CreateCharacter,
                expectedAccountRevision,
                character.SlotIndex,
                null,
                character,
                null);
        }

        public static PlayerAccountSaveCommandV1 UpsertCharacterComponent(
            StableId operationStableId,
            long expectedAccountRevision,
            int slotIndex,
            StableId expectedCharacterInstanceStableId,
            SaveComponentSnapshotV1 component)
        {
            PlayerAccountSnapshotV1.ValidateSlotIndex(slotIndex);
            if (expectedCharacterInstanceStableId == null)
            {
                throw new ArgumentNullException(
                    nameof(expectedCharacterInstanceStableId));
            }
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }
            return new PlayerAccountSaveCommandV1(
                operationStableId,
                PlayerAccountSaveCommandKindV1.UpsertCharacterComponent,
                expectedAccountRevision,
                slotIndex,
                expectedCharacterInstanceStableId,
                null,
                component);
        }

        public static PlayerAccountSaveCommandV1 DeleteCharacter(
            StableId operationStableId,
            long expectedAccountRevision,
            int slotIndex,
            StableId expectedCharacterInstanceStableId)
        {
            PlayerAccountSnapshotV1.ValidateSlotIndex(slotIndex);
            if (expectedCharacterInstanceStableId == null)
            {
                throw new ArgumentNullException(
                    nameof(expectedCharacterInstanceStableId));
            }
            return new PlayerAccountSaveCommandV1(
                operationStableId,
                PlayerAccountSaveCommandKindV1.DeleteCharacter,
                expectedAccountRevision,
                slotIndex,
                expectedCharacterInstanceStableId,
                null,
                null);
        }

        public static PlayerAccountSaveCommandV1 UpsertAccountComponent(
            StableId operationStableId,
            long expectedAccountRevision,
            SaveComponentSnapshotV1 component)
        {
            if (component == null)
            {
                throw new ArgumentNullException(nameof(component));
            }
            return new PlayerAccountSaveCommandV1(
                operationStableId,
                PlayerAccountSaveCommandKindV1.UpsertAccountComponent,
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

    public sealed class PlayerAccountSaveResultV1
    {
        public PlayerAccountSaveResultV1(
            PlayerAccountSaveStatusV1 status,
            string rejectionCode,
            PlayerAccountSnapshotV1 snapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot
                ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public PlayerAccountSaveStatusV1 Status { get; }

        public string RejectionCode { get; }

        public PlayerAccountSnapshotV1 Snapshot { get; }
    }

    public sealed class PlayerAccountSaveReplayRecordV1
    {
        public PlayerAccountSaveReplayRecordV1(
            StableId operationStableId,
            string commandFingerprint,
            PlayerAccountSaveStatusV1 status,
            string rejectionCode,
            PlayerAccountSnapshotV1 resultSnapshot)
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
            Fingerprint = SaveAuthorityFingerprintV1.Hash(
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

        public PlayerAccountSaveStatusV1 Status { get; }

        public string RejectionCode { get; }

        public PlayerAccountSnapshotV1 ResultSnapshot { get; }

        public string Fingerprint { get; }
    }

    public sealed class PlayerAccountSaveAuthoritySnapshotV1
    {
        public PlayerAccountSaveAuthoritySnapshotV1(
            PlayerAccountSnapshotV1 account,
            IEnumerable<PlayerAccountSaveReplayRecordV1> replayRecords)
        {
            Account = account
                ?? throw new ArgumentNullException(nameof(account));
            var records = (replayRecords
                ?? Array.Empty<PlayerAccountSaveReplayRecordV1>()).ToList();
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
                PlayerAccountSaveReplayRecordV1>(
                    records.OrderBy(
                            item => item.OperationStableId.ToString(),
                            StringComparer.Ordinal)
                        .ToList());
            Fingerprint = SaveAuthorityFingerprintV1.Hash(
                Account.Fingerprint
                    + "|"
                    + string.Join(
                        ";",
                        ReplayRecords.Select(item => item.Fingerprint)));
        }

        public PlayerAccountSnapshotV1 Account { get; }

        public IReadOnlyList<PlayerAccountSaveReplayRecordV1> ReplayRecords
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
    public sealed class PlayerAccountSaveAuthorityV1
    {
        private readonly Dictionary<StableId, PlayerAccountSaveReplayRecordV1>
            replay =
                new Dictionary<StableId, PlayerAccountSaveReplayRecordV1>();
        private PlayerAccountSnapshotV1 account;

        public PlayerAccountSaveAuthorityV1(
            PlayerAccountSnapshotV1 initialAccount)
        {
            account = initialAccount
                ?? throw new ArgumentNullException(nameof(initialAccount));
        }

        public PlayerAccountSnapshotV1 Current
        {
            get { return account; }
        }

        public PlayerAccountSaveResultV1 Apply(
            PlayerAccountSaveCommandV1 command)
        {
            if (command == null)
            {
                return new PlayerAccountSaveResultV1(
                    PlayerAccountSaveStatusV1.Rejected,
                    "account-save-command-null",
                    account);
            }

            PlayerAccountSaveReplayRecordV1 prior;
            if (replay.TryGetValue(command.OperationStableId, out prior))
            {
                if (!string.Equals(
                    prior.CommandFingerprint,
                    command.Fingerprint,
                    StringComparison.Ordinal))
                {
                    return new PlayerAccountSaveResultV1(
                        PlayerAccountSaveStatusV1.ConflictingDuplicate,
                        "account-save-operation-conflict",
                        account);
                }
                return new PlayerAccountSaveResultV1(
                    PlayerAccountSaveStatusV1.ExactDuplicateNoChange,
                    prior.RejectionCode,
                    prior.ResultSnapshot);
            }

            PlayerAccountSaveResultV1 result;
            if (command.ExpectedAccountRevision != account.Revision)
            {
                result = new PlayerAccountSaveResultV1(
                    PlayerAccountSaveStatusV1.StaleRevision,
                    "account-save-revision-stale",
                    account);
            }
            else
            {
                result = Execute(command);
            }

            replay.Add(
                command.OperationStableId,
                new PlayerAccountSaveReplayRecordV1(
                    command.OperationStableId,
                    command.Fingerprint,
                    result.Status,
                    result.RejectionCode,
                    result.Snapshot));
            return result;
        }

        public PlayerAccountSaveAuthoritySnapshotV1 ExportSnapshot()
        {
            return new PlayerAccountSaveAuthoritySnapshotV1(
                account,
                replay.Values);
        }

        public bool TryImport(
            PlayerAccountSaveAuthoritySnapshotV1 snapshot,
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
                PlayerAccountSaveReplayRecordV1>();
            foreach (PlayerAccountSaveReplayRecordV1 record in
                snapshot.ReplayRecords)
            {
                importedReplay.Add(record.OperationStableId, record);
            }

            account = snapshot.Account;
            replay.Clear();
            foreach (KeyValuePair<
                StableId,
                PlayerAccountSaveReplayRecordV1> pair in importedReplay)
            {
                replay.Add(pair.Key, pair.Value);
            }
            return true;
        }

        private PlayerAccountSaveResultV1 Execute(
            PlayerAccountSaveCommandV1 command)
        {
            switch (command.Kind)
            {
                case PlayerAccountSaveCommandKindV1.CreateCharacter:
                    return CreateCharacter(command);
                case PlayerAccountSaveCommandKindV1.UpsertCharacterComponent:
                    return UpsertCharacterComponent(command);
                case PlayerAccountSaveCommandKindV1.DeleteCharacter:
                    return DeleteCharacter(command);
                case PlayerAccountSaveCommandKindV1.UpsertAccountComponent:
                    return UpsertAccountComponent(command);
                default:
                    return Reject("account-save-command-kind-unsupported");
            }
        }

        private PlayerAccountSaveResultV1 CreateCharacter(
            PlayerAccountSaveCommandV1 command)
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

        private PlayerAccountSaveResultV1 UpsertCharacterComponent(
            PlayerAccountSaveCommandV1 command)
        {
            if (command.Component == null)
            {
                return Reject("account-save-component-missing");
            }
            CharacterInstanceSnapshotV1 character =
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

            CharacterInstanceSnapshotV1 nextCharacter =
                character.WithComponent(command.Component);
            account = account.WithCharacter(
                command.SlotIndex,
                nextCharacter);
            return Applied();
        }

        private PlayerAccountSaveResultV1 DeleteCharacter(
            PlayerAccountSaveCommandV1 command)
        {
            CharacterInstanceSnapshotV1 character =
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

        private PlayerAccountSaveResultV1 UpsertAccountComponent(
            PlayerAccountSaveCommandV1 command)
        {
            if (command.Component == null)
            {
                return Reject("account-save-component-missing");
            }
            account = account.WithAccountComponent(command.Component);
            return Applied();
        }

        private PlayerAccountSaveResultV1 Applied()
        {
            return new PlayerAccountSaveResultV1(
                PlayerAccountSaveStatusV1.Applied,
                string.Empty,
                account);
        }

        private PlayerAccountSaveResultV1 Reject(string rejectionCode)
        {
            return new PlayerAccountSaveResultV1(
                PlayerAccountSaveStatusV1.Rejected,
                rejectionCode,
                account);
        }
    }

    internal static class SaveAuthorityFingerprintV1
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
