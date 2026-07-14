using System;
using NUnit.Framework;
using ShooterMover.Contracts.Identity;

namespace ShooterMover.Tests.EditMode.Contracts
{
    public sealed class IdentityContractTests
    {
        private const string SourceCommit =
            "eb80374cb669f0f8c9e36210c45f935f28c3acc2";
        private const string OtherSourceCommit =
            "46d230aaa39ee81063249c02d0a77c549b53007d";
        private const string DefinitionFingerprint =
            "sha256:8c1e3a5f7b9d0f2a4c6e8b1d3f5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a";
        private const string OtherDefinitionFingerprint =
            "sha256:1d3f5a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a3c5e7b9d0f2a4c6e8b1d3f";
        private const string PackageLockFingerprint =
            "sha256:4d2f9c0e6b8a1d3f5e7c9a0b2d4f6e8c1a3b5d7f9e0c2a4b6d8f1e3c5a7b9d0f";
        private const string ContentFingerprint =
            "sha256:c7a91e4d2f6b8c0a5d3e7f1b9a4c6e8d0f2b5d7a9c1e3f6b8d0a2c4e7f9b1d3a";
        private const string OtherContentFingerprint =
            "sha256:d8b02f5e3a7c9d1f4b6e8a0c2d5f7b9e1a3c6d8f0b2e4a7c9d1f3b5e6a8c0d2f";
        private const string ArtifactChecksum =
            "sha256:e3b7c1d95f0a2c4e6b8d1f3a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a3c5e";
        private const string AllZeroSha256 =
            "sha256:0000000000000000000000000000000000000000000000000000000000000000";

        [Test]
        public void ContentVersion_EqualValues_AreEqualAndHaveDeterministicHashes()
        {
            ContentVersion first = ContentVersion.Create(1, DefinitionFingerprint);
            ContentVersion second = ContentVersion.ParseCanonical(first.ToCanonicalString());

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void ContentVersion_SerializesFieldsInCanonicalOrder()
        {
            ContentVersion version = ContentVersion.Create(7, DefinitionFingerprint);

            Assert.That(
                version.ToCanonicalString(),
                Is.EqualTo(
                    "catalog_version=7\n"
                    + "definition_fingerprint="
                    + DefinitionFingerprint));
        }

        [Test]
        public void ContentVersion_FingerprintChange_ChangesIdentity()
        {
            ContentVersion first = ContentVersion.Create(1, DefinitionFingerprint);
            ContentVersion second = ContentVersion.Create(1, OtherDefinitionFingerprint);

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first.ToCanonicalString(), Is.Not.EqualTo(second.ToCanonicalString()));
        }

        [Test]
        public void ContentVersion_MissingReorderedOrNonCanonicalFields_AreRejected()
        {
            Assert.Throws<FormatException>(
                () => ContentVersion.ParseCanonical("catalog_version=1"));
            Assert.Throws<FormatException>(
                () => ContentVersion.ParseCanonical(
                    "definition_fingerprint="
                    + DefinitionFingerprint
                    + "\ncatalog_version=1"));
            Assert.Throws<FormatException>(
                () => ContentVersion.ParseCanonical(
                    "catalog_version=01\ndefinition_fingerprint="
                    + DefinitionFingerprint));
            Assert.Throws<FormatException>(
                () => ContentVersion.ParseCanonical(
                    "catalog_version=1\r\ndefinition_fingerprint="
                    + DefinitionFingerprint));
        }

        [Test]
        public void ContentVersion_NullEmptyMalformedAndPlaceholderValues_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => ContentVersion.ParseCanonical(null));
            Assert.Throws<FormatException>(() => ContentVersion.ParseCanonical(string.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ContentVersion.Create(0, DefinitionFingerprint));
            Assert.Throws<ArgumentNullException>(() => ContentVersion.Create(1, null));
            Assert.Throws<FormatException>(() => ContentVersion.Create(1, string.Empty));
            Assert.Throws<FormatException>(
                () => ContentVersion.Create(1, "sha256:ABCDEF"));
            Assert.Throws<FormatException>(
                () => ContentVersion.Create(1, AllZeroSha256));
        }

        [Test]
        public void FormalBuildIdentity_EqualValues_AreEqualAndRoundTrip()
        {
            BuildIdentity first = CreateFormal();
            BuildIdentity second = BuildIdentity.ParseCanonical(first.ToCanonicalString());

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first == second, Is.True);
            Assert.That(first != second, Is.False);
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(second.IsFormalRelease, Is.True);
            Assert.That(second.IsDirty, Is.False);
        }

        [Test]
        public void FormalBuildIdentity_SerializesEveryAcceptedFieldInCanonicalOrder()
        {
            string[] lines = CreateFormal().ToCanonicalString().Split('\n');

            Assert.That(lines, Has.Length.EqualTo(8));
            Assert.That(lines[0], Is.EqualTo("identity_kind=formal-release"));
            Assert.That(lines[1], Is.EqualTo("source_state=clean"));
            Assert.That(lines[2], Is.EqualTo("source_commit=" + SourceCommit));
            Assert.That(lines[3], Is.EqualTo("unity_version=6000.3.19f1"));
            Assert.That(
                lines[4],
                Is.EqualTo("package_lock_fingerprint=" + PackageLockFingerprint));
            Assert.That(lines[5], Is.EqualTo("content_fingerprint=" + ContentFingerprint));
            Assert.That(lines[6], Is.EqualTo("save_schema=1"));
            Assert.That(lines[7], Is.EqualTo("artifact_checksum=" + ArtifactChecksum));
        }

        [Test]
        public void BuildIdentity_AnyAcceptedFingerprintOrCommitChange_ChangesIdentity()
        {
            BuildIdentity baseline = CreateFormal();
            BuildIdentity changedCommit = BuildIdentity.CreateFormal(
                OtherSourceCommit,
                "6000.3.19f1",
                PackageLockFingerprint,
                ContentFingerprint,
                1,
                ArtifactChecksum);
            BuildIdentity changedContent = BuildIdentity.CreateFormal(
                SourceCommit,
                "6000.3.19f1",
                PackageLockFingerprint,
                OtherContentFingerprint,
                1,
                ArtifactChecksum);

            Assert.That(changedCommit, Is.Not.EqualTo(baseline));
            Assert.That(changedContent, Is.Not.EqualTo(baseline));
        }

        [Test]
        public void DevelopmentIdentity_WithoutChecksum_IsExplicitAndCannotMasqueradeAsFormal()
        {
            BuildIdentity development = BuildIdentity.CreateDevelopment(
                SourceCommit,
                "6000.3.19f1",
                PackageLockFingerprint,
                ContentFingerprint,
                1,
                true);

            Assert.That(development.Kind, Is.EqualTo(BuildIdentityKind.Development));
            Assert.That(development.IsFormalRelease, Is.False);
            Assert.That(development.IsDirty, Is.True);
            Assert.That(development.ArtifactChecksum, Is.Null);
            Assert.That(
                development.ToCanonicalString(),
                Does.StartWith("identity_kind=development\nsource_state=dirty\n"));
            Assert.That(
                development.ToCanonicalString(),
                Does.EndWith("artifact_checksum=null"));
            Assert.That(
                BuildIdentity.ParseCanonical(development.ToCanonicalString()),
                Is.EqualTo(development));
        }

        [Test]
        public void CleanDevelopmentIdentity_WithChecksum_RemainsNonFormal()
        {
            BuildIdentity development = BuildIdentity.CreateDevelopment(
                SourceCommit,
                "6000.3.19f1",
                PackageLockFingerprint,
                ContentFingerprint,
                1,
                false,
                ArtifactChecksum);

            Assert.That(development.IsDirty, Is.False);
            Assert.That(development.ArtifactChecksum, Is.EqualTo(ArtifactChecksum));
            Assert.That(development.IsFormalRelease, Is.False);
            Assert.That(
                development.ToCanonicalString(),
                Does.StartWith("identity_kind=development\nsource_state=clean\n"));
        }

        [Test]
        public void FormalIdentity_DirtySourceOrAbsentChecksum_IsRejected()
        {
            string canonical = CreateFormal().ToCanonicalString();

            Assert.Throws<FormatException>(
                () => BuildIdentity.ParseCanonical(
                    canonical.Replace("source_state=clean", "source_state=dirty")));
            Assert.Throws<FormatException>(
                () => BuildIdentity.ParseCanonical(
                    canonical.Replace(
                        "artifact_checksum=" + ArtifactChecksum,
                        "artifact_checksum=null")));
            Assert.Throws<FormatException>(
                () => BuildIdentity.CreateFormal(
                    SourceCommit,
                    "6000.3.19f1",
                    PackageLockFingerprint,
                    ContentFingerprint,
                    1,
                    null));
        }

        [Test]
        public void BuildIdentity_MissingReorderedUnknownOrTrailingFields_AreRejected()
        {
            string canonical = CreateFormal().ToCanonicalString();
            string missingChecksum = canonical.Substring(
                0,
                canonical.LastIndexOf("\nartifact_checksum=", StringComparison.Ordinal));
            string reordered = canonical.Replace(
                "source_commit=" + SourceCommit + "\nunity_version=6000.3.19f1",
                "unity_version=6000.3.19f1\nsource_commit=" + SourceCommit);

            Assert.Throws<FormatException>(
                () => BuildIdentity.ParseCanonical(missingChecksum));
            Assert.Throws<FormatException>(() => BuildIdentity.ParseCanonical(reordered));
            Assert.Throws<FormatException>(
                () => BuildIdentity.ParseCanonical(canonical + "\nunknown=value"));
            Assert.Throws<FormatException>(
                () => BuildIdentity.ParseCanonical(canonical + "\n"));
        }

        [TestCase("")]
        [TestCase("unknown")]
        [TestCase("0000000000000000000000000000000000000000")]
        [TestCase("EB80374CB669F0F8C9E36210C45F935F28C3ACC2")]
        public void BuildIdentity_MalformedOrPlaceholderSourceCommit_IsRejected(string sourceCommit)
        {
            Assert.Throws<FormatException>(
                () => BuildIdentity.CreateFormal(
                    sourceCommit,
                    "6000.3.19f1",
                    PackageLockFingerprint,
                    ContentFingerprint,
                    1,
                    ArtifactChecksum));
        }

        [TestCase("")]
        [TestCase("unknown")]
        [TestCase("6000.3")]
        [TestCase("6000.3.19")]
        [TestCase("6000.3.19F1")]
        [TestCase("6000.3.19f1 (7689f4515d75)")]
        public void BuildIdentity_MalformedOrAmbiguousUnityVersion_IsRejected(string unityVersion)
        {
            Assert.Throws<FormatException>(
                () => BuildIdentity.CreateFormal(
                    SourceCommit,
                    unityVersion,
                    PackageLockFingerprint,
                    ContentFingerprint,
                    1,
                    ArtifactChecksum));
        }

        [Test]
        public void BuildIdentity_NullMalformedAndPlaceholderFormalFields_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => BuildIdentity.CreateFormal(
                    null,
                    "6000.3.19f1",
                    PackageLockFingerprint,
                    ContentFingerprint,
                    1,
                    ArtifactChecksum));
            Assert.Throws<ArgumentNullException>(
                () => BuildIdentity.CreateFormal(
                    SourceCommit,
                    null,
                    PackageLockFingerprint,
                    ContentFingerprint,
                    1,
                    ArtifactChecksum));
            Assert.Throws<FormatException>(
                () => BuildIdentity.CreateFormal(
                    SourceCommit,
                    "6000.3.19f1",
                    AllZeroSha256,
                    ContentFingerprint,
                    1,
                    ArtifactChecksum));
            Assert.Throws<FormatException>(
                () => BuildIdentity.CreateFormal(
                    SourceCommit,
                    "6000.3.19f1",
                    PackageLockFingerprint,
                    "sha256:not-a-fingerprint",
                    1,
                    ArtifactChecksum));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BuildIdentity.CreateFormal(
                    SourceCommit,
                    "6000.3.19f1",
                    PackageLockFingerprint,
                    ContentFingerprint,
                    0,
                    ArtifactChecksum));
            Assert.Throws<FormatException>(
                () => BuildIdentity.CreateFormal(
                    SourceCommit,
                    "6000.3.19f1",
                    PackageLockFingerprint,
                    ContentFingerprint,
                    1,
                    AllZeroSha256));
        }

        [Test]
        public void BuildIdentity_ParserRejectsNullEmptyAndNonCanonicalStateTokens()
        {
            string canonical = CreateFormal().ToCanonicalString();

            Assert.Throws<ArgumentNullException>(() => BuildIdentity.ParseCanonical(null));
            Assert.Throws<FormatException>(() => BuildIdentity.ParseCanonical(string.Empty));
            Assert.Throws<FormatException>(
                () => BuildIdentity.ParseCanonical(
                    canonical.Replace("identity_kind=formal-release", "identity_kind=release")));
            Assert.Throws<FormatException>(
                () => BuildIdentity.ParseCanonical(
                    canonical.Replace("source_state=clean", "source_state=unknown")));
            Assert.Throws<FormatException>(
                () => BuildIdentity.ParseCanonical(
                    canonical.Replace("save_schema=1", "save_schema=01")));
        }

        private static BuildIdentity CreateFormal()
        {
            return BuildIdentity.CreateFormal(
                SourceCommit,
                "6000.3.19f1",
                PackageLockFingerprint,
                ContentFingerprint,
                1,
                ArtifactChecksum);
        }
    }
}
