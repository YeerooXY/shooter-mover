using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Domain.Persistence.Accounts;

namespace ShooterMover.Application.Persistence.Components
{
    public interface IAtomicSaveFilePort
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

    public enum PlayerAccountStoreStatus
    {
        Saved = 1,
        Loaded = 2,
        RecoveredLastKnownGood = 3,
        NotFound = 4,
        ValidationRejected = 5,
        IoFailure = 6,
    }

    public sealed class PlayerAccountStoreResult
    {
        public PlayerAccountStoreResult(
            PlayerAccountStoreStatus status,
            string rejectionCode,
            PlayerAccountSnapshot snapshot)
        {
            Status = status;
            RejectionCode = rejectionCode ?? string.Empty;
            Snapshot = snapshot;
        }

        public PlayerAccountStoreStatus Status { get; }

        public string RejectionCode { get; }

        public PlayerAccountSnapshot Snapshot { get; }

        public bool Succeeded
        {
            get
            {
                return Status == PlayerAccountStoreStatus.Saved
                    || Status == PlayerAccountStoreStatus.Loaded
                    || Status
                        == PlayerAccountStoreStatus.RecoveredLastKnownGood;
            }
        }
    }

    public static class PlayerAccountFileCodec
    {
        private const string Format = "player-account-file-v1";
        private const int SchemaVersion = 1;

        public static string Encode(PlayerAccountSnapshot account)
        {
            SaveComponentValidationResult integrity =
                PlayerAccountAggregateCodec.Validate(account);
            if (!integrity.Succeeded)
            {
                throw new ArgumentException(
                    integrity.RejectionCode,
                    nameof(account));
            }

            string payload = PlayerAccountAggregateCodec.Encode(account);
            if (Encoding.UTF8.GetByteCount(payload)
                > SavePersistenceLimits.MaximumAccountPayloadBytes)
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
                > SavePersistenceLimits.MaximumAccountFileBytes)
            {
                throw new ArgumentException(
                    "account-file-too-large",
                    nameof(account));
            }
            return output;
        }

        public static bool TryDecode(
            string text,
            out PlayerAccountSnapshot account,
            out string rejectionCode)
        {
            account = null;
            if (text == null)
            {
                rejectionCode = "account-file-null";
                return false;
            }
            if (Encoding.UTF8.GetByteCount(text)
                > SavePersistenceLimits.MaximumAccountFileBytes)
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
                > SavePersistenceLimits.MaximumAccountPayloadBytes)
            {
                rejectionCode = "account-payload-too-large";
                return false;
            }

            string payload = Encoding.UTF8.GetString(payloadBytes);
            if (!PlayerAccountAggregateCodec.TryDecode(
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
    /// Aggregate integrity and known component versions are always validated before
    /// optional product-specific semantics.
    /// </summary>
    public sealed class AtomicPlayerAccountStore
    {
        private readonly IAtomicSaveFilePort files;
        private readonly string activePath;
        private readonly string temporaryPath;
        private readonly string backupPath;
        private readonly Func<PlayerAccountSnapshot,
            SaveComponentValidationResult> validateAdditionalSemantics;

        public AtomicPlayerAccountStore(
            IAtomicSaveFilePort files,
            string activePath,
            string temporaryPath,
            string backupPath,
            Func<PlayerAccountSnapshot, SaveComponentValidationResult>
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
            validateAdditionalSemantics = validateAccount;
        }

        public PlayerAccountStoreResult Save(
            PlayerAccountSnapshot account)
        {
            SaveComponentValidationResult validation = ValidateForUse(
                account,
                "account-save-validation-result-null");
            if (!validation.Succeeded)
            {
                return new PlayerAccountStoreResult(
                    PlayerAccountStoreStatus.ValidationRejected,
                    validation.RejectionCode,
                    null);
            }

            try
            {
                if (files.Exists(temporaryPath))
                {
                    files.Delete(temporaryPath);
                }

                string encoded = PlayerAccountFileCodec.Encode(account);
                files.WriteAllText(temporaryPath, encoded);

                string temporaryText = files.ReadAllText(temporaryPath);
                if (temporaryText == null
                    || Encoding.UTF8.GetByteCount(temporaryText)
                        > SavePersistenceLimits.MaximumAccountFileBytes)
                {
                    SafeDeleteTemporary();
                    return new PlayerAccountStoreResult(
                        PlayerAccountStoreStatus.ValidationRejected,
                        temporaryText == null
                            ? "temporary-readback-null"
                            : "account-file-too-large",
                        null);
                }

                PlayerAccountSnapshot readBack;
                string rejectionCode;
                if (!PlayerAccountFileCodec.TryDecode(
                    temporaryText,
                    out readBack,
                    out rejectionCode))
                {
                    SafeDeleteTemporary();
                    return new PlayerAccountStoreResult(
                        PlayerAccountStoreStatus.ValidationRejected,
                        "temporary-readback-invalid:" + rejectionCode,
                        null);
                }

                validation = ValidateForUse(
                    readBack,
                    "temporary-readback-validation-result-null");
                if (!validation.Succeeded
                    || !string.Equals(
                        readBack.Fingerprint,
                        account.Fingerprint,
                        StringComparison.Ordinal))
                {
                    SafeDeleteTemporary();
                    return new PlayerAccountStoreResult(
                        PlayerAccountStoreStatus.ValidationRejected,
                        !validation.Succeeded
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

                PlayerAccountSnapshot active;
                if (!PlayerAccountFileCodec.TryDecode(
                    files.ReadAllText(activePath),
                    out active,
                    out rejectionCode)
                    || !string.Equals(
                        active.Fingerprint,
                        account.Fingerprint,
                        StringComparison.Ordinal))
                {
                    return new PlayerAccountStoreResult(
                        PlayerAccountStoreStatus.IoFailure,
                        "active-readback-invalid-after-atomic-replace:"
                            + rejectionCode,
                        null);
                }

                validation = ValidateForUse(
                    active,
                    "active-readback-validation-result-null");
                if (!validation.Succeeded)
                {
                    return new PlayerAccountStoreResult(
                        PlayerAccountStoreStatus.IoFailure,
                        "active-readback-validation-failed-after-atomic-replace:"
                            + validation.RejectionCode,
                        null);
                }

                return new PlayerAccountStoreResult(
                    PlayerAccountStoreStatus.Saved,
                    string.Empty,
                    active);
            }
            catch (Exception exception)
            {
                SafeDeleteTemporary();
                return new PlayerAccountStoreResult(
                    PlayerAccountStoreStatus.IoFailure,
                    "account-save-io-failure:"
                        + exception.GetType().Name,
                    null);
            }
        }

        public PlayerAccountStoreResult Load()
        {
            PlayerAccountSnapshot snapshot;
            string rejectionCode;
            if (TryReadValid(activePath, out snapshot, out rejectionCode))
            {
                return new PlayerAccountStoreResult(
                    PlayerAccountStoreStatus.Loaded,
                    string.Empty,
                    snapshot);
            }

            string activeError = rejectionCode;
            if (TryReadValid(backupPath, out snapshot, out rejectionCode))
            {
                return new PlayerAccountStoreResult(
                    PlayerAccountStoreStatus.RecoveredLastKnownGood,
                    activeError,
                    snapshot);
            }

            if (!files.Exists(activePath) && !files.Exists(backupPath))
            {
                return new PlayerAccountStoreResult(
                    PlayerAccountStoreStatus.NotFound,
                    "account-save-not-found",
                    null);
            }

            return new PlayerAccountStoreResult(
                PlayerAccountStoreStatus.ValidationRejected,
                "active=" + activeError + ";backup=" + rejectionCode,
                null);
        }

        private bool TryReadValid(
            string path,
            out PlayerAccountSnapshot snapshot,
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
                        > SavePersistenceLimits.MaximumAccountFileBytes)
                {
                    rejectionCode = text == null
                        ? "account-file-null"
                        : "account-file-too-large";
                    return false;
                }
                if (!PlayerAccountFileCodec.TryDecode(
                    text,
                    out snapshot,
                    out rejectionCode))
                {
                    return false;
                }
                SaveComponentValidationResult validation = ValidateForUse(
                    snapshot,
                    "account-load-validation-result-null");
                if (!validation.Succeeded)
                {
                    snapshot = null;
                    rejectionCode = validation.RejectionCode;
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

        private SaveComponentValidationResult ValidateForUse(
            PlayerAccountSnapshot account,
            string additionalNullCode)
        {
            SaveComponentValidationResult mandatory =
                KnownSaveComponentVersionGuard.Validate(account);
            if (mandatory == null || !mandatory.Succeeded)
            {
                return mandatory
                    ?? SaveComponentValidationResult.Reject(
                        "mandatory-account-validation-result-null");
            }

            if (validateAdditionalSemantics == null)
            {
                return SaveComponentValidationResult.Accept();
            }

            SaveComponentValidationResult additional =
                validateAdditionalSemantics(account);
            return additional
                ?? SaveComponentValidationResult.Reject(additionalNullCode);
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
