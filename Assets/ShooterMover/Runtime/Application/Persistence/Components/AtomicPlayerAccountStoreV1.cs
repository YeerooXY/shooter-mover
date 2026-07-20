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
                CanonicalSnapshotIntegrityV1.Validate(account);
            if (!integrity.Succeeded)
            {
                throw new ArgumentException(
                    integrity.RejectionCode,
                    nameof(account));
            }

            string payload = CanonicalSnapshotCodecV1.Serialize(account);
            string payloadBase64 = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(payload));
            string body = "format=" + Format
                + "\nschema_version="
                + SchemaVersion.ToString(CultureInfo.InvariantCulture)
                + "\naccount_fingerprint=" + account.Fingerprint
                + "\npayload_base64=" + payloadBase64;
            return body + "\nfile_fingerprint=" + Hash(body);
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

            string[] lines = text.Split('\n');
            if (lines.Length != 5
                || !string.Equals(
                    lines[0],
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
            if (!TryRead(lines[1], "schema_version=", out schemaText)
                || !TryRead(
                    lines[2],
                    "account_fingerprint=",
                    out accountFingerprint)
                || !TryRead(lines[3], "payload_base64=", out payloadBase64)
                || !TryRead(
                    lines[4],
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

            string body = string.Join("\n", lines, 0, 4);
            if (!string.Equals(
                Hash(body),
                fileFingerprint,
                StringComparison.Ordinal))
            {
                rejectionCode = "account-file-fingerprint-mismatch";
                return false;
            }

            string payload;
            try
            {
                payload = Encoding.UTF8.GetString(
                    Convert.FromBase64String(payloadBase64));
            }
            catch (FormatException)
            {
                rejectionCode = "account-file-payload-base64-invalid";
                return false;
            }

            string payloadError;
            if (!CanonicalSnapshotCodecV1.TryDeserialize(
                payload,
                out account,
                out payloadError))
            {
                rejectionCode = payloadError;
                return false;
            }

            SaveComponentValidationResultV1 integrity =
                CanonicalSnapshotIntegrityV1.Validate(account);
            if (!integrity.Succeeded)
            {
                account = null;
                rejectionCode = integrity.RejectionCode;
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
                ?? (snapshot => CanonicalSnapshotIntegrityV1.Validate(snapshot));
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

                PlayerAccountSnapshotV1 readBack;
                string rejectionCode;
                if (!PlayerAccountFileCodecV1.TryDecode(
                    files.ReadAllText(temporaryPath),
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
                if (!PlayerAccountFileCodecV1.TryDecode(
                    files.ReadAllText(path),
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
