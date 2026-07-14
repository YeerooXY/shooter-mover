using System;
using System.Globalization;

namespace ShooterMover.Contracts.Identity
{
    public enum BuildIdentityKind
    {
        FormalRelease = 0,
        Development = 1,
    }

    /// <summary>
    /// Immutable, engine-independent identity for one produced build artifact.
    /// </summary>
    public sealed class BuildIdentity : IEquatable<BuildIdentity>
    {
        private BuildIdentity(
            BuildIdentityKind kind,
            bool isDirty,
            string sourceCommit,
            string unityVersion,
            string packageLockFingerprint,
            string contentFingerprint,
            int saveSchema,
            string artifactChecksum)
        {
            if (kind != BuildIdentityKind.FormalRelease
                && kind != BuildIdentityKind.Development)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown build identity kind.");
            }

            string validatedArtifactChecksum = artifactChecksum == null
                ? null
                : IdentityContractFormat.RequireSha256(
                    artifactChecksum,
                    nameof(artifactChecksum));

            if (kind == BuildIdentityKind.FormalRelease)
            {
                if (isDirty)
                {
                    throw new FormatException(
                        "A formal release identity must reference a clean source tree.");
                }

                if (validatedArtifactChecksum == null)
                {
                    throw new FormatException(
                        "A formal release identity requires a final artifact checksum.");
                }
            }

            Kind = kind;
            IsDirty = isDirty;
            SourceCommit = IdentityContractFormat.RequireSourceCommit(
                sourceCommit,
                nameof(sourceCommit));
            UnityVersion = IdentityContractFormat.RequireUnityVersion(
                unityVersion,
                nameof(unityVersion));
            PackageLockFingerprint = IdentityContractFormat.RequireSha256(
                packageLockFingerprint,
                nameof(packageLockFingerprint));
            ContentFingerprint = IdentityContractFormat.RequireSha256(
                contentFingerprint,
                nameof(contentFingerprint));
            SaveSchema = IdentityContractFormat.RequirePositiveVersion(
                saveSchema,
                nameof(saveSchema));
            ArtifactChecksum = validatedArtifactChecksum;
        }

        public BuildIdentityKind Kind { get; }

        public bool IsDirty { get; }

        public bool IsFormalRelease
        {
            get { return Kind == BuildIdentityKind.FormalRelease; }
        }

        public string SourceCommit { get; }

        public string UnityVersion { get; }

        public string PackageLockFingerprint { get; }

        public string ContentFingerprint { get; }

        public int SaveSchema { get; }

        public string ArtifactChecksum { get; }

        public static BuildIdentity CreateFormal(
            string sourceCommit,
            string unityVersion,
            string packageLockFingerprint,
            string contentFingerprint,
            int saveSchema,
            string artifactChecksum)
        {
            return new BuildIdentity(
                BuildIdentityKind.FormalRelease,
                false,
                sourceCommit,
                unityVersion,
                packageLockFingerprint,
                contentFingerprint,
                saveSchema,
                artifactChecksum);
        }

        public static BuildIdentity CreateDevelopment(
            string sourceCommit,
            string unityVersion,
            string packageLockFingerprint,
            string contentFingerprint,
            int saveSchema,
            bool isDirty,
            string artifactChecksum = null)
        {
            return new BuildIdentity(
                BuildIdentityKind.Development,
                isDirty,
                sourceCommit,
                unityVersion,
                packageLockFingerprint,
                contentFingerprint,
                saveSchema,
                artifactChecksum);
        }

        public static BuildIdentity ParseCanonical(string text)
        {
            string[] lines = IdentityContractFormat.SplitCanonicalLines(
                text,
                8,
                nameof(BuildIdentity));

            string kindText = IdentityContractFormat.ReadField(lines[0], "identity_kind");
            string sourceStateText = IdentityContractFormat.ReadField(lines[1], "source_state");
            string sourceCommit = IdentityContractFormat.ReadField(lines[2], "source_commit");
            string unityVersion = IdentityContractFormat.ReadField(lines[3], "unity_version");
            string packageLockFingerprint = IdentityContractFormat.ReadField(
                lines[4],
                "package_lock_fingerprint");
            string contentFingerprint = IdentityContractFormat.ReadField(
                lines[5],
                "content_fingerprint");
            int saveSchema = IdentityContractFormat.ParsePositiveVersion(
                IdentityContractFormat.ReadField(lines[6], "save_schema"),
                "save_schema");
            string artifactChecksumText = IdentityContractFormat.ReadField(
                lines[7],
                "artifact_checksum");

            BuildIdentityKind kind;
            if (string.Equals(kindText, "formal-release", StringComparison.Ordinal))
            {
                kind = BuildIdentityKind.FormalRelease;
            }
            else if (string.Equals(kindText, "development", StringComparison.Ordinal))
            {
                kind = BuildIdentityKind.Development;
            }
            else
            {
                throw new FormatException(
                    "identity_kind must be formal-release or development.");
            }

            bool isDirty;
            if (string.Equals(sourceStateText, "clean", StringComparison.Ordinal))
            {
                isDirty = false;
            }
            else if (string.Equals(sourceStateText, "dirty", StringComparison.Ordinal))
            {
                isDirty = true;
            }
            else
            {
                throw new FormatException("source_state must be clean or dirty.");
            }

            string artifactChecksum = string.Equals(
                artifactChecksumText,
                IdentityContractFormat.NullToken,
                StringComparison.Ordinal)
                ? null
                : artifactChecksumText;

            if (kind == BuildIdentityKind.FormalRelease)
            {
                return new BuildIdentity(
                    kind,
                    isDirty,
                    sourceCommit,
                    unityVersion,
                    packageLockFingerprint,
                    contentFingerprint,
                    saveSchema,
                    artifactChecksum);
            }

            return CreateDevelopment(
                sourceCommit,
                unityVersion,
                packageLockFingerprint,
                contentFingerprint,
                saveSchema,
                isDirty,
                artifactChecksum);
        }

        public string ToCanonicalString()
        {
            string kindText = Kind == BuildIdentityKind.FormalRelease
                ? "formal-release"
                : "development";
            string sourceStateText = IsDirty ? "dirty" : "clean";
            string artifactChecksumText = ArtifactChecksum ?? IdentityContractFormat.NullToken;

            return "identity_kind="
                + kindText
                + "\nsource_state="
                + sourceStateText
                + "\nsource_commit="
                + SourceCommit
                + "\nunity_version="
                + UnityVersion
                + "\npackage_lock_fingerprint="
                + PackageLockFingerprint
                + "\ncontent_fingerprint="
                + ContentFingerprint
                + "\nsave_schema="
                + SaveSchema.ToString(CultureInfo.InvariantCulture)
                + "\nartifact_checksum="
                + artifactChecksumText;
        }

        public bool Equals(BuildIdentity other)
        {
            return !ReferenceEquals(other, null)
                && Kind == other.Kind
                && IsDirty == other.IsDirty
                && string.Equals(SourceCommit, other.SourceCommit, StringComparison.Ordinal)
                && string.Equals(UnityVersion, other.UnityVersion, StringComparison.Ordinal)
                && string.Equals(
                    PackageLockFingerprint,
                    other.PackageLockFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    ContentFingerprint,
                    other.ContentFingerprint,
                    StringComparison.Ordinal)
                && SaveSchema == other.SaveSchema
                && string.Equals(
                    ArtifactChecksum,
                    other.ArtifactChecksum,
                    StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BuildIdentity);
        }

        public override int GetHashCode()
        {
            return IdentityContractFormat.DeterministicHash(ToCanonicalString());
        }

        public override string ToString()
        {
            return ToCanonicalString();
        }

        public static bool operator ==(BuildIdentity left, BuildIdentity right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(BuildIdentity left, BuildIdentity right)
        {
            return !(left == right);
        }
    }
}
