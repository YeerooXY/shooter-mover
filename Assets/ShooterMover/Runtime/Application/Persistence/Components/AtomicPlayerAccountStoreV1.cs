using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Persistence.Components
{
    public interface IAtomicSaveFilePortV1
    {
        bool Exists(string path);

        string ReadAllText(string path);

        void WriteAllText(string path, string contents);

        void Move(string sourcePath, string destinationPath);

        /// <summary>
        /// Atomically replaces destinationPath with sourcePath and stores the previous
        /// destination at backupPath. Implementations must not expose a partially-written
        /// destination if the operation fails.
        /// </summary>
        void Replace(
            string sourcePath,
            string destinationPath,
            string backupPath);

        void Delete(string path);
    }

    public enum PlayerAccountStoreStatusV1
    {
        Saved = 1,
        Loaded = 2,
        RecoveredLastKnownGood = 3,
        NotFound = 4,
        ValidationRejected = 5,
        IoFailure = 6,
    }

    public sealed class PlayerAccountStoreResultV1
    {
        public PlayerAccountStoreResultV1(
            PlayerAccountStoreStatusV1 status,
            string rejectionCode,
            PlayerAccountSnapshotV1 snapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot;
        }

        public PlayerAccountStoreStatusV1 Status { get; }

        public string RejectionCode { get; }

        public PlayerAccountSnapshotV1 Snapshot { get; }

        public bool Succeeded
        {
            get
            {
                return Status == PlayerAccountStoreStatusV1.Saved
                    || Status == PlayerAccountStoreStatusV1.Loaded
                    || Status
                        == PlayerAccountStoreStatusV1.RecoveredLastKnownGood;
            }
        }
    }

    public static class PlayerAccountFileCodecV1
    {
        private const string Format = "player-account-file-v1";
        private const int SchemaVersion = 1;

        public static string Encode(PlayerAccountSnapshotV1 account)
        {
            SaveComponentValidationResultV1 integrity =
                PlayerAccountAggregateCodecV1.Validate(account);
            if (!integrity.Succeeded)
            {
                throw new ArgumentException(
                    integrity.RejectionCode,
                    nameof(account));
            }

            string payload = PlayerAccountAggregateCodecV1.Encode(account);
            if (Encoding.UTF8.GetByteCount(payload)
                > SavePersistenceLimitsV1.MaximumAccountPayloadBytes)
            {
                throw new ArgumentException(
                    "account-payload-too-large",
                    nameof(account));
            }
            string payloadBase64 = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(payload));
            string body = "format=" + Format
                + "\nschema_version="
                + SchemaVersion.ToString(CultureInfo.InvariantCulture)
                + "\naccount_fingerprint=" + account.Fingerprint
                + "\npayload_base64=" + payloadBase64;
            string output = body + "\nfile_fingerprint=" + Hash(body);
            if (Encoding.UTF8.GetByteCount(output)
                > SavePersistenceLimitsV1.MaximumAccountFileBytes)
            {
                throw new ArgumentException(
                    "account-file-too-large",
                    nameof(account));
            }
            return output;
        }

        public static bool TryDecode(
            string text,
            out PlayerAccountSnapshotV1 account,
            out string rejectionCode)
        {
            account = null;
            if (text == null)
            {
                rejectionCode = "account-file-null";
                return false;
            }
            if (Encoding.UTF8.GetByteCount(text)
                > SavePersistenceLimitsV1.MaximumAccountFileBytes)
            {
                rejectionCode = "account-file-too-large";
                return false;
            }

            int first = text.IndexOf('\n');
            int second = first < 0 ? -1 : text.IndexOf('\n', first + 1);
            int third = second < 0 ? -1 : text.IndexOf('\n', second + 1);
            int fourth = third < 0 ? -1 : text.IndexOf('\n', third + 1);
            if (first < 0 || second < 0 || third < 0 || fourth < 0
                || text.IndexOf('\n', fourth + 1) >= 0)
            {
                rejectionCode = "account-file-format-invalid";
                return false;
            }

            string formatLine = text.Substring(0, first);
            string schemaLine = text.Substring(first + 1, second - first - 1);
            string accountLine = text.Substring(second + 1, third - second - 1);
            string payloadLine = text.Substring(third + 1, fourth - third - 1);
            string fileLine = text.Substring(fourth + 1);
            if (!string.Equals(
                formatLine,
                "format=" + Format,
                StringComparison.Ordinal))
            {
                rejectionCode = "account-file-format-invalid";
                return false;
            }

            string schemaText;
            string accountFingerprint;
            string payloadBase64;
            string fileFingerprint;
            if (!TryRead(schemaLine, "schema_version=", out schemaText)
                || !TryRead(
                    accountLine,
                    "account_fingerprint=",
                    out accountFingerprint)
                || !TryRead(payloadLine, "payload_base64=", out payloadBase64)
                || !TryRead(
                    fileLine,
                    "file_fingerprint=",
                    out fileFingerprint))
            {
                rejectionCode = "account-file-field-invalid";
                return false;
            }

            int schemaVersion;
            if (!int.TryParse(
                schemaText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out schemaVersion)
                || schemaVersion != SchemaVersion)
            {
                rejectionCode = "account-file-schema-unsupported";
                return false;
            }

            string body = text.Substring(0, fourth);
            if (!string.Equals(
                Hash(body),
                fileFingerprint,
                StringComparison.Ordinal))
            {
                rejectionCode = "account-file-fingerprint-mismatch";
                return false;
            }

            byte[] payloadBytes;
            try
            {
                payloadBytes = Convert.FromBase64String(payloadBase64);
            }
            catch (FormatException)
            {
                rejectionCode = "account-file-payload-base64-invalid";
                return false;
            }
            if (payloadBytes.Length
                > SavePersistenceLimitsV1.MaximumAccountPayloadBytes)
            {
                rejectionCode = "account-payload-too-large";
                return false;
            }

            string payload = Encoding.UTF8.GetString(payloadBytes);
            if (!PlayerAccountAggregateCodecV1.TryDecode(
                payload,
                out account,
                out rejectionCode))
            {
                return false;
            }
            if (!string.Equals(
                account.Fingerprint,
                accountFingerprint,
                StringComparison.Ordinal))
            {
                account = null;
                rejectionCode = "account-snapshot-fingerprint-mismatch";
                return false;
            }

            rejectionCode = string.Empty;
            return true;
        }

        private static bool TryRead(
            string field,
            string prefix,
            out string value)
        {
            if (field == null
                || !field.StartsWith(prefix, StringComparison.Ordinal))
            {
                value = null;
                return false;
            }
            value = field.Substring(prefix.Length);
            return true;
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(
                    Encoding.UTF8.GetBytes(value ?? string.Empty));
                var builder = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                {
                    builder.Append(digest[index].ToString(
                        "x2",
                        CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }
    }

    /// <summary>
    /// Engine-neutral two-phase file protocol. It writes only a temporary candidate,
    /// decodes and validates the exact read-back bytes, then asks the filesystem port
    /// for one atomic active/backup replacement. It never uses PlayerPrefs.
    /// </summary>
    public sealed class AtomicPlayerAccountStoreV1
    {
        private readonly IAtomicSaveFilePortV1 files;
        private readonly string activePath;
        private readonly string temporaryPath;
        private readonly string backupPath;
        private readonly Func<PlayerAccountSnapshotV1,
            SaveComponentValidationResultV1> validateAccount;

        public AtomicPlayerAccountStoreV1(
            IAtomicSaveFilePortV1 files,
            string activePath,
            string temporaryPath,
            string backupPath,
            Func<PlayerAccountSnapshotV1, SaveComponentValidationResultV1>
                validateAccount = null)
        {
            this.files = files ?? throw new ArgumentNullException(nameof(files));
            this.activePath = RequirePath(activePath, nameof(activePath));
            this.temporaryPath = RequirePath(
                temporaryPath,
                nameof(temporaryPath));
            this.backupPath = RequirePath(backupPath, nameof(backupPath));
            if (string.Equals(
                this.activePath,
                this.temporaryPath,
                StringComparison.Ordinal)
                || string.Equals(
                    this.activePath,
                    this.backupPath,
                    StringComparison.Ordinal)
                || string.Equals(
                    this.temporaryPath,
                    this.backupPath,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Active, temporary, and backup paths must be distinct.");
            }
            this.validateAccount = validateAccount
                ?? PlayerAccountAggregateCodecV1.Validate;
        }

        public PlayerAccountStoreResultV1 Save(
            PlayerAccountSnapshotV1 account)
        {
            SaveComponentValidationResultV1 validation =
                account == null ? null : validateAccount(account);
            if (validation == null || !validation.Succeeded)
            {
                return new PlayerAccountStoreResultV1(
                    PlayerAccountStoreStatusV1.ValidationRejected,
                    validation == null
                        ? "account-save-validation-result-null"
                        : validation.RejectionCode,
                    null);
            }

            try
            {
                if (files.Exists(temporaryPath))
                {
                    files.Delete(temporaryPath);
                }

                string encoded = PlayerAccountFileCodecV1.Encode(account);
                files.WriteAllText(temporaryPath, encoded);

                string temporaryText = files.ReadAllText(temporaryPath);
                if (temporaryText == null
                    || Encoding.UTF8.GetByteCount(temporaryText)
                        > SavePersistenceLimitsV1.MaximumAccountFileBytes)
                {
                    SafeDeleteTemporary();
                    return new PlayerAccountStoreResultV1(
                        PlayerAccountStoreStatusV1.ValidationRejected,
                        temporaryText == null
                            ? "temporary-readback-null"
                            : "account-file-too-large",
                        null);
                }

                PlayerAccountSnapshotV1 readBack;
                string rejectionCode;
                if (!PlayerAccountFileCodecV1.TryDecode(
                    temporaryText,
                    out readBack,
                    out rejectionCode))
                {
                    SafeDeleteTemporary();
                    return new PlayerAccountStoreResultV1(
                        PlayerAccountStoreStatusV1.ValidationRejected,
                        "temporary-readback-invalid:" + rejectionCode,
                        null);
                }

                validation = validateAccount(readBack);
                if (validation == null || !validation.Succeeded
                    || !string.Equals(
                        readBack.Fingerprint,
                        account.Fingerprint,
                        StringComparison.Ordinal))
                {
                    SafeDeleteTemporary();
                    return new PlayerAccountStoreResultV1(
                        PlayerAccountStoreStatusV1.ValidationRejected,
                        validation == null
                            ? "temporary-readback-validation-result-null"
                            : !validation.Succeeded
                                ? validation.RejectionCode
                                : "temporary-readback-account-mismatch",
                        null);
                }

                if (files.Exists(activePath))
                {
                    files.Replace(temporaryPath, activePath, backupPath);
                }
                else
                {
                    files.Move(temporaryPath, activePath);
                }

                PlayerAccountSnapshotV1 active;
                if (!PlayerAccountFileCodecV1.TryDecode(
                    files.ReadAllText(activePath),
                    out active,
                    out rejectionCode)
                    || !string.Equals(
                        active.Fingerprint,
                        account.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return new PlayerAccountStoreResultV1(
                        PlayerAccountStoreStatusV1.IoFailure,
                        "active-readback-invalid-after-atomic-replace:"
                            + rejectionCode,
                        null);
                }

                return new PlayerAccountStoreResultV1(
                    PlayerAccountStoreStatusV1.Saved,
                    string.Empty,
                    active);
            }
            catch (Exception exception)
            {
                SafeDeleteTemporary();
                return new PlayerAccountStoreResultV1(
                    PlayerAccountStoreStatusV1.IoFailure,
                    "account-save-io-failure:"
                        + exception.GetType().Name,
                    null);
            }
        }

        public PlayerAccountStoreResultV1 Load()
        {
            PlayerAccountSnapshotV1 snapshot;
            string rejectionCode;
            if (TryReadValid(activePath, out snapshot, out rejectionCode))
            {
                return new PlayerAccountStoreResultV1(
                    PlayerAccountStoreStatusV1.Loaded,
                    string.Empty,
                    snapshot);
            }

            string activeError = rejectionCode;
            if (TryReadValid(backupPath, out snapshot, out rejectionCode))
            {
                return new PlayerAccountStoreResultV1(
                    PlayerAccountStoreStatusV1.RecoveredLastKnownGood,
                    activeError,
                    snapshot);
            }

            if (!files.Exists(activePath) && !files.Exists(backupPath))
            {
                return new PlayerAccountStoreResultV1(
                    PlayerAccountStoreStatusV1.NotFound,
                    "account-save-not-found",
                    null);
            }

            return new PlayerAccountStoreResultV1(
                PlayerAccountStoreStatusV1.ValidationRejected,
                "active=" + activeError + ";backup=" + rejectionCode,
                null);
        }

        private bool TryReadValid(
            string path,
            out PlayerAccountSnapshotV1 snapshot,
            out string rejectionCode)
        {
            snapshot = null;
            if (!files.Exists(path))
            {
                rejectionCode = "file-not-found";
                return false;
            }
            try
            {
                string text = files.ReadAllText(path);
                if (text == null
                    || Encoding.UTF8.GetByteCount(text)
                        > SavePersistenceLimitsV1.MaximumAccountFileBytes)
                {
                    rejectionCode = text == null
                        ? "account-file-null"
                        : "account-file-too-large";
                    return false;
                }
                if (!PlayerAccountFileCodecV1.TryDecode(
                    text,
                    out snapshot,
                    out rejectionCode))
                {
                    return false;
                }
                SaveComponentValidationResultV1 validation =
                    validateAccount(snapshot);
                if (validation == null || !validation.Succeeded)
                {
                    snapshot = null;
                    rejectionCode = validation == null
                        ? "account-load-validation-result-null"
                        : validation.RejectionCode;
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                snapshot = null;
                rejectionCode = "account-load-io-failure:"
                    + exception.GetType().Name;
                return false;
            }
        }

        private void SafeDeleteTemporary()
        {
            try
            {
                if (files.Exists(temporaryPath))
                {
                    files.Delete(temporaryPath);
                }
            }
            catch
            {
                // The active and backup files are intentionally left untouched.
            }
        }

        private static string RequirePath(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    "A save path is required.",
                    parameterName);
            }
            return value.Trim();
        }
    }
}
