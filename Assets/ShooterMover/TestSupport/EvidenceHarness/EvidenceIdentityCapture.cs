using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ShooterMover.Contracts.Identity;
using ShooterMover.Domain.Common;

namespace ShooterMover.TestSupport.EvidenceHarness
{
    public enum EvidenceDirtyStatePolicy
    {
        RejectDirty = 0,
        AllowDirtyDevelopment = 1,
    }

    /// <summary>
    /// Immutable deterministic identity attached to one local evidence session.
    /// </summary>
    public sealed class EvidenceIdentityRecord
    {
        public const int CurrentSchemaVersion = 1;
        public const string FingerprintPrefix = "sha256:";

        private readonly string canonicalPayload;
        private readonly string canonicalText;

        private EvidenceIdentityRecord(
            BuildIdentity buildIdentity,
            ContentVersion contentVersion,
            EvidenceDirtyStatePolicy dirtyStatePolicy,
            string buildTarget,
            string buildConfiguration,
            StableId tuningProfileId)
        {
            SchemaVersion = CurrentSchemaVersion;
            BuildIdentityKind = buildIdentity.Kind;
            SourceCommit = buildIdentity.SourceCommit;
            IsDirty = buildIdentity.IsDirty;
            DirtyStatePolicy = dirtyStatePolicy;
            UnityVersion = buildIdentity.UnityVersion;
            PackageLockFingerprint = buildIdentity.PackageLockFingerprint;
            BuildContentFingerprint = buildIdentity.ContentFingerprint;
            ContentCatalogVersion = contentVersion.CatalogVersion;
            ContentDefinitionFingerprint = contentVersion.DefinitionFingerprint;
            SaveSchemaVersion = buildIdentity.SaveSchema;
            ArtifactChecksum = buildIdentity.ArtifactChecksum;
            BuildTarget = buildTarget;
            BuildConfiguration = buildConfiguration;
            TuningProfileId = tuningProfileId.ToString();

            canonicalPayload = BuildCanonicalPayload();
            Fingerprint = ComputeSha256(canonicalPayload);
            canonicalText = canonicalPayload + "\nrecord_fingerprint=" + Fingerprint;
        }

        public int SchemaVersion { get; }

        public BuildIdentityKind BuildIdentityKind { get; }

        public string SourceCommit { get; }

        public bool IsDirty { get; }

        public EvidenceDirtyStatePolicy DirtyStatePolicy { get; }

        public string UnityVersion { get; }

        public string PackageLockFingerprint { get; }

        public string BuildContentFingerprint { get; }

        public int ContentCatalogVersion { get; }

        public string ContentDefinitionFingerprint { get; }

        public int SaveSchemaVersion { get; }

        public string ArtifactChecksum { get; }

        public string BuildTarget { get; }

        public string BuildConfiguration { get; }

        public string TuningProfileId { get; }

        public string Fingerprint { get; }

        internal static EvidenceIdentityRecord Create(
            BuildIdentity buildIdentity,
            ContentVersion contentVersion,
            EvidenceDirtyStatePolicy dirtyStatePolicy,
            string buildTarget,
            string buildConfiguration,
            StableId tuningProfileId)
        {
            return new EvidenceIdentityRecord(
                buildIdentity,
                contentVersion,
                dirtyStatePolicy,
                buildTarget,
                buildConfiguration,
                tuningProfileId);
        }

        public string ToCanonicalString()
        {
            return canonicalText;
        }

        public override string ToString()
        {
            return canonicalText;
        }

        private string BuildCanonicalPayload()
        {
            return "evidence_identity_schema="
                + SchemaVersion.ToString(CultureInfo.InvariantCulture)
                + "\nbuild_identity_kind="
                + FormatBuildIdentityKind(BuildIdentityKind)
                + "\nsource_commit="
                + SourceCommit
                + "\nsource_state="
                + (IsDirty ? "dirty" : "clean")
                + "\ndirty_state_policy="
                + FormatDirtyStatePolicy(DirtyStatePolicy)
                + "\nunity_version="
                + UnityVersion
                + "\npackage_lock_fingerprint="
                + PackageLockFingerprint
                + "\nbuild_content_fingerprint="
                + BuildContentFingerprint
                + "\ncontent_catalog_version="
                + ContentCatalogVersion.ToString(CultureInfo.InvariantCulture)
                + "\ncontent_definition_fingerprint="
                + ContentDefinitionFingerprint
                + "\nsave_schema_version="
                + SaveSchemaVersion.ToString(CultureInfo.InvariantCulture)
                + "\nartifact_checksum="
                + ArtifactChecksum
                + "\nbuild_target="
                + BuildTarget
                + "\nbuild_configuration="
                + BuildConfiguration
                + "\ntuning_profile_id="
                + TuningProfileId;
        }

        private static string FormatBuildIdentityKind(BuildIdentityKind kind)
        {
            return kind == BuildIdentityKind.FormalRelease
                ? "formal-release"
                : "development";
        }

        private static string FormatDirtyStatePolicy(EvidenceDirtyStatePolicy policy)
        {
            return policy == EvidenceDirtyStatePolicy.RejectDirty
                ? "reject-dirty"
                : "allow-dirty-development";
        }

        private static string ComputeSha256(string canonicalPayloadText)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(canonicalPayloadText);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create())
            {
                digest = sha256.ComputeHash(bytes);
            }

            StringBuilder builder = new StringBuilder(FingerprintPrefix.Length + 64);
            builder.Append(FingerprintPrefix);
            for (int index = 0; index < digest.Length; index++)
            {
                builder.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// Fail-closed result. Invalid inputs never produce a partial record.
    /// </summary>
    public sealed class EvidenceIdentityCaptureResult
    {
        private EvidenceIdentityCaptureResult(
            bool isValid,
            EvidenceIdentityRecord record,
            string errorCode,
            string errorMessage)
        {
            IsValid = isValid;
            Record = record;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool IsValid { get; }

        public EvidenceIdentityRecord Record { get; }

        public string ErrorCode { get; }

        public string ErrorMessage { get; }

        internal static EvidenceIdentityCaptureResult Valid(EvidenceIdentityRecord record)
        {
            if (record == null)
            {
                throw new ArgumentNullException(nameof(record));
            }

            return new EvidenceIdentityCaptureResult(true, record, null, null);
        }

        internal static EvidenceIdentityCaptureResult Invalid(string errorCode, string errorMessage)
        {
            if (string.IsNullOrEmpty(errorCode))
            {
                throw new ArgumentException("Invalid evidence requires a deterministic error code.", nameof(errorCode));
            }

            if (string.IsNullOrEmpty(errorMessage))
            {
                throw new ArgumentException(
                    "Invalid evidence requires a deterministic error message.",
                    nameof(errorMessage));
            }

            return new EvidenceIdentityCaptureResult(false, null, errorCode, errorMessage);
        }
    }

    /// <summary>
    /// Local, deterministic adapter from Identity v1 contracts to one evidence record.
    /// It performs no file, Git, network, credential, telemetry, or build-pipeline access.
    /// </summary>
    public static class EvidenceIdentityCapture
    {
        public const string RejectDirtyPolicy = "reject-dirty";
        public const string AllowDirtyDevelopmentPolicy = "allow-dirty-development";

        public static EvidenceIdentityCaptureResult Capture(
            string buildIdentityCanonical,
            string contentVersionCanonical,
            string dirtyStatePolicy,
            string buildTarget,
            string buildConfiguration,
            string tuningProfileId)
        {
            if (string.IsNullOrEmpty(buildIdentityCanonical))
            {
                return Invalid("missing-build-identity", "BuildIdentity v1 canonical text is required.");
            }

            BuildIdentity buildIdentity;
            try
            {
                buildIdentity = BuildIdentity.ParseCanonical(buildIdentityCanonical);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is FormatException)
            {
                return Invalid("malformed-build-identity", exception.Message);
            }

            if (buildIdentity.ArtifactChecksum == null)
            {
                return Invalid(
                    "provisional-build-identity",
                    "Evidence requires a final artifact checksum; development artifact_checksum=null is provisional.");
            }

            if (string.IsNullOrEmpty(contentVersionCanonical))
            {
                return Invalid("missing-content-version", "ContentVersion v1 canonical text is required.");
            }

            ContentVersion contentVersion;
            try
            {
                contentVersion = ContentVersion.ParseCanonical(contentVersionCanonical);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is FormatException)
            {
                return Invalid("malformed-content-version", exception.Message);
            }

            EvidenceDirtyStatePolicy parsedPolicy;
            if (string.Equals(dirtyStatePolicy, RejectDirtyPolicy, StringComparison.Ordinal))
            {
                parsedPolicy = EvidenceDirtyStatePolicy.RejectDirty;
            }
            else if (string.Equals(
                dirtyStatePolicy,
                AllowDirtyDevelopmentPolicy,
                StringComparison.Ordinal))
            {
                parsedPolicy = EvidenceDirtyStatePolicy.AllowDirtyDevelopment;
            }
            else
            {
                return Invalid(
                    string.IsNullOrEmpty(dirtyStatePolicy)
                        ? "missing-dirty-state-policy"
                        : "unsupported-dirty-state-policy",
                    "dirty_state_policy must be reject-dirty or allow-dirty-development.");
            }

            if (parsedPolicy == EvidenceDirtyStatePolicy.RejectDirty && buildIdentity.IsDirty)
            {
                return Invalid(
                    "inconsistent-dirty-state-policy",
                    "reject-dirty cannot accept a dirty BuildIdentity.");
            }

            if (parsedPolicy == EvidenceDirtyStatePolicy.AllowDirtyDevelopment
                && (!buildIdentity.IsDirty || buildIdentity.Kind != BuildIdentityKind.Development))
            {
                return Invalid(
                    "inconsistent-dirty-state-policy",
                    "allow-dirty-development requires a dirty development BuildIdentity.");
            }

            string tokenError;
            if (!TryValidateEvidenceToken(buildTarget, out tokenError))
            {
                return Invalid(
                    string.IsNullOrEmpty(buildTarget)
                        ? "missing-build-target"
                        : "malformed-build-target",
                    "build_target " + tokenError);
            }

            if (IsProvisionalValue(buildTarget))
            {
                return Invalid("provisional-build-target", "build_target is provisional or a placeholder.");
            }

            if (!TryValidateEvidenceToken(buildConfiguration, out tokenError))
            {
                return Invalid(
                    string.IsNullOrEmpty(buildConfiguration)
                        ? "missing-build-configuration"
                        : "malformed-build-configuration",
                    "build_configuration " + tokenError);
            }

            if (IsProvisionalValue(buildConfiguration))
            {
                return Invalid(
                    "provisional-build-configuration",
                    "build_configuration is provisional or a placeholder.");
            }

            if (string.IsNullOrEmpty(tuningProfileId))
            {
                return Invalid("missing-tuning-profile-id", "A canonical tuning-profile StableId is required.");
            }

            StableId parsedTuningProfileId;
            try
            {
                parsedTuningProfileId = StableId.Parse(tuningProfileId);
            }
            catch (Exception exception) when (
                exception is ArgumentException
                || exception is FormatException)
            {
                return Invalid("malformed-tuning-profile-id", exception.Message);
            }

            if (IsProvisionalValue(parsedTuningProfileId.Namespace)
                || IsProvisionalValue(parsedTuningProfileId.Value))
            {
                return Invalid(
                    "provisional-tuning-profile-id",
                    "The tuning-profile StableId contains a provisional or placeholder component.");
            }

            EvidenceIdentityRecord record = EvidenceIdentityRecord.Create(
                buildIdentity,
                contentVersion,
                parsedPolicy,
                buildTarget,
                buildConfiguration,
                parsedTuningProfileId);

            return EvidenceIdentityCaptureResult.Valid(record);
        }

        private static EvidenceIdentityCaptureResult Invalid(string code, string message)
        {
            return EvidenceIdentityCaptureResult.Invalid(code, message);
        }

        private static bool TryValidateEvidenceToken(string value, out string error)
        {
            if (string.IsNullOrEmpty(value))
            {
                error = "is required.";
                return false;
            }

            if (value.Length > 64)
            {
                error = "must not exceed 64 characters.";
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                bool isAsciiLetter = (current >= 'a' && current <= 'z')
                    || (current >= 'A' && current <= 'Z');
                bool isAsciiDigit = current >= '0' && current <= '9';
                bool isSeparator = current == '-' || current == '_' || current == '.';
                if (!isAsciiLetter && !isAsciiDigit && !isSeparator)
                {
                    error = "must contain only ASCII letters, digits, hyphen, underscore, or dot.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool IsProvisionalValue(string value)
        {
            string lowered = value.ToLowerInvariant();
            return lowered == "unknown"
                || lowered == "unset"
                || lowered == "todo"
                || lowered == "tbd"
                || lowered == "null"
                || lowered == "none"
                || lowered.IndexOf("provisional", StringComparison.Ordinal) >= 0
                || lowered.IndexOf("placeholder", StringComparison.Ordinal) >= 0;
        }
    }
}
