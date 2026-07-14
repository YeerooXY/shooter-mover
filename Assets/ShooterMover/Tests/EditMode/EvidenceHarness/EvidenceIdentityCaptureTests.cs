using System;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Identity;

namespace ShooterMover.Tests.EditMode.EvidenceHarness
{
    public sealed class EvidenceIdentityCaptureTests
    {
        private const string CaptureTypeName =
            "ShooterMover.TestSupport.EvidenceHarness.EvidenceIdentityCapture";
        private const string UseDefaultCanonical = "<use-default-canonical>";
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
        private const string OtherPackageLockFingerprint =
            "sha256:5e3a0d1f7c9b2e4a6d8f0b1c3e5a7d9f2b4c6e8a0d1f3b5c7e9a2d4f6b8c0e1a";
        private const string ArtifactChecksum =
            "sha256:e3b7c1d95f0a2c4e6b8d1f3a7c9e0b2d4f6a8c1e3b5d7f9a0c2e4b6d8f1a3c5e";
        private const string BuildTarget = "StandaloneWindows64";
        private const string BuildConfiguration = "Development";
        private const string TuningProfileId = "movement-tuning.stage1-baseline";

        [Test]
        public void SameFrozenInputs_SerializeIdenticallyAndHaveSameFingerprint()
        {
            object first = Capture();
            object second = Capture();

            AssertValid(first);
            AssertValid(second);

            object firstRecord = GetPropertyValue<object>(first, "Record");
            object secondRecord = GetPropertyValue<object>(second, "Record");
            string firstCanonical = InvokeCanonical(firstRecord);
            string secondCanonical = InvokeCanonical(secondRecord);

            Assert.That(firstCanonical, Is.EqualTo(secondCanonical));
            Assert.That(
                GetPropertyValue<string>(firstRecord, "Fingerprint"),
                Is.EqualTo(GetPropertyValue<string>(secondRecord, "Fingerprint")));

            string[] lines = firstCanonical.Split('\n');
            Assert.That(lines, Has.Length.EqualTo(15));
            Assert.That(lines[0], Is.EqualTo("evidence_identity_schema=1"));
            Assert.That(lines[1], Is.EqualTo("build_identity_kind=formal-release"));
            Assert.That(lines[2], Is.EqualTo("source_commit=" + SourceCommit));
            Assert.That(lines[3], Is.EqualTo("source_state=clean"));
            Assert.That(lines[4], Is.EqualTo("dirty_state_policy=reject-dirty"));
            Assert.That(lines[5], Is.EqualTo("unity_version=6000.3.19f1"));
            Assert.That(
                lines[6],
                Is.EqualTo("package_lock_fingerprint=" + PackageLockFingerprint));
            Assert.That(lines[7], Is.EqualTo("content_catalog_version=1"));
            Assert.That(
                lines[8],
                Is.EqualTo("content_definition_fingerprint=" + DefinitionFingerprint));
            Assert.That(lines[9], Is.EqualTo("save_schema_version=1"));
            Assert.That(lines[10], Is.EqualTo("artifact_checksum=" + ArtifactChecksum));
            Assert.That(lines[11], Is.EqualTo("build_target=" + BuildTarget));
            Assert.That(lines[12], Is.EqualTo("build_configuration=" + BuildConfiguration));
            Assert.That(lines[13], Is.EqualTo("tuning_profile_id=" + TuningProfileId));
            Assert.That(lines[14], Does.StartWith("record_fingerprint=sha256:"));
        }

        [Test]
        public void ChangingAnyRequiredIdentityInput_ChangesFingerprint()
        {
            string baseline = CaptureFingerprint(Capture());

            object[] changedRecords =
            {
                Capture(buildIdentityCanonical: CreateBuildIdentity(sourceCommit: OtherSourceCommit)),
                Capture(buildIdentityCanonical: CreateBuildIdentity(unityVersion: "6000.3.20f1")),
                Capture(
                    buildIdentityCanonical: CreateBuildIdentity(
                        packageLockFingerprint: OtherPackageLockFingerprint)),
                Capture(
                    buildIdentityCanonical: CreateBuildIdentity(
                        contentFingerprint: OtherDefinitionFingerprint),
                    contentVersionCanonical: CreateContentVersion(
                        definitionFingerprint: OtherDefinitionFingerprint)),
                Capture(buildIdentityCanonical: CreateBuildIdentity(saveSchema: 2)),
                Capture(buildTarget: "StandaloneLinux64"),
                Capture(buildConfiguration: "ReleaseCandidate"),
                Capture(tuningProfileId: "movement-tuning.stage1-alt"),
                Capture(
                    buildIdentityCanonical: CreateBuildIdentity(
                        development: true,
                        dirty: true),
                    dirtyStatePolicy: "allow-dirty-development"),
            };

            foreach (object changed in changedRecords)
            {
                AssertValid(changed);
                Assert.That(CaptureFingerprint(changed), Is.Not.EqualTo(baseline));
            }
        }

        [Test]
        public void DirtySource_WithRejectDirtyPolicy_IsInvalidEvidence()
        {
            object result = Capture(
                buildIdentityCanonical: CreateBuildIdentity(development: true, dirty: true),
                dirtyStatePolicy: "reject-dirty");

            AssertInvalid(result, "inconsistent-dirty-state-policy");
        }

        [Test]
        public void AllowDirtyPolicy_RequiresDirtyDevelopmentIdentity()
        {
            AssertInvalid(
                Capture(dirtyStatePolicy: "allow-dirty-development"),
                "inconsistent-dirty-state-policy");

            AssertInvalid(
                Capture(
                    buildIdentityCanonical: CreateBuildIdentity(
                        development: true,
                        dirty: false),
                    dirtyStatePolicy: "allow-dirty-development"),
                "inconsistent-dirty-state-policy");
        }

        [Test]
        public void DevelopmentIdentityWithoutFinalChecksum_IsProvisionalAndInvalid()
        {
            object result = Capture(
                buildIdentityCanonical: BuildIdentity.CreateDevelopment(
                    SourceCommit,
                    "6000.3.19f1",
                    PackageLockFingerprint,
                    DefinitionFingerprint,
                    1,
                    false).ToCanonicalString());

            AssertInvalid(result, "provisional-build-identity");
        }

        [Test]
        public void MissingMalformedProvisionalAndInconsistentInputs_FailClosed()
        {
            AssertInvalid(Capture(buildIdentityCanonical: null), "missing-build-identity");
            AssertInvalid(Capture(buildIdentityCanonical: "identity_kind=development"), "malformed-build-identity");
            AssertInvalid(Capture(contentVersionCanonical: null), "missing-content-version");
            AssertInvalid(Capture(contentVersionCanonical: "catalog_version=1"), "malformed-content-version");
            AssertInvalid(Capture(dirtyStatePolicy: "unknown"), "unsupported-dirty-state-policy");
            AssertInvalid(Capture(buildTarget: "Standalone Windows 64"), "malformed-build-target");
            AssertInvalid(Capture(buildConfiguration: "provisional"), "provisional-build-configuration");
            AssertInvalid(Capture(tuningProfileId: null), "missing-tuning-profile-id");
            AssertInvalid(Capture(tuningProfileId: "Movement.Stage1"), "malformed-tuning-profile-id");
            AssertInvalid(
                Capture(tuningProfileId: "movement-tuning.provisional"),
                "provisional-tuning-profile-id");
            AssertInvalid(
                Capture(
                    buildIdentityCanonical: CreateBuildIdentity(
                        contentFingerprint: OtherDefinitionFingerprint)),
                "inconsistent-content-version");
        }

        [Test]
        public void ValidRecord_IsSealedAndExposesNoWritablePublicProperties()
        {
            object result = Capture();
            AssertValid(result);
            object record = GetPropertyValue<object>(result, "Record");
            Type recordType = record.GetType();

            Assert.That(recordType.IsSealed, Is.True);
            foreach (PropertyInfo property in recordType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                Assert.That(property.CanWrite, Is.False, property.Name);
            }
        }

        private static object Capture(
            string buildIdentityCanonical = UseDefaultCanonical,
            string contentVersionCanonical = UseDefaultCanonical,
            string dirtyStatePolicy = "reject-dirty",
            string buildTarget = BuildTarget,
            string buildConfiguration = BuildConfiguration,
            string tuningProfileId = TuningProfileId)
        {
            Type captureType = FindCaptureType();
            MethodInfo captureMethod = captureType.GetMethod(
                "Capture",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(captureMethod, Is.Not.Null);

            return captureMethod.Invoke(
                null,
                new object[]
                {
                    buildIdentityCanonical == UseDefaultCanonical
                        ? CreateBuildIdentity()
                        : buildIdentityCanonical,
                    contentVersionCanonical == UseDefaultCanonical
                        ? CreateContentVersion()
                        : contentVersionCanonical,
                    dirtyStatePolicy,
                    buildTarget,
                    buildConfiguration,
                    tuningProfileId,
                });
        }

        private static Type FindCaptureType()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(CaptureTypeName, false);
                if (type != null)
                {
                    return type;
                }
            }

            Assembly predefinedAssembly = Assembly.Load("Assembly-CSharp");
            Type loadedType = predefinedAssembly.GetType(CaptureTypeName, false);
            Assert.That(loadedType, Is.Not.Null);
            return loadedType;
        }

        private static string CreateBuildIdentity(
            string sourceCommit = SourceCommit,
            string unityVersion = "6000.3.19f1",
            string packageLockFingerprint = PackageLockFingerprint,
            string contentFingerprint = DefinitionFingerprint,
            int saveSchema = 1,
            bool development = false,
            bool dirty = false)
        {
            BuildIdentity identity = development
                ? BuildIdentity.CreateDevelopment(
                    sourceCommit,
                    unityVersion,
                    packageLockFingerprint,
                    contentFingerprint,
                    saveSchema,
                    dirty,
                    ArtifactChecksum)
                : BuildIdentity.CreateFormal(
                    sourceCommit,
                    unityVersion,
                    packageLockFingerprint,
                    contentFingerprint,
                    saveSchema,
                    ArtifactChecksum);

            return identity.ToCanonicalString();
        }

        private static string CreateContentVersion(
            int catalogVersion = 1,
            string definitionFingerprint = DefinitionFingerprint)
        {
            return ContentVersion.Create(catalogVersion, definitionFingerprint).ToCanonicalString();
        }

        private static void AssertValid(object result)
        {
            Assert.That(GetPropertyValue<bool>(result, "IsValid"), Is.True);
            Assert.That(GetPropertyValue<object>(result, "Record"), Is.Not.Null);
            Assert.That(GetPropertyValue<string>(result, "ErrorCode"), Is.Null);
        }

        private static void AssertInvalid(object result, string expectedErrorCode)
        {
            Assert.That(GetPropertyValue<bool>(result, "IsValid"), Is.False);
            Assert.That(GetPropertyValue<object>(result, "Record"), Is.Null);
            Assert.That(GetPropertyValue<string>(result, "ErrorCode"), Is.EqualTo(expectedErrorCode));
            Assert.That(GetPropertyValue<string>(result, "ErrorMessage"), Is.Not.Empty);
        }

        private static string CaptureFingerprint(object result)
        {
            AssertValid(result);
            object record = GetPropertyValue<object>(result, "Record");
            return GetPropertyValue<string>(record, "Fingerprint");
        }

        private static string InvokeCanonical(object record)
        {
            MethodInfo method = record.GetType().GetMethod(
                "ToCanonicalString",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(record, null);
        }

        private static T GetPropertyValue<T>(object target, string propertyName)
        {
            Assert.That(target, Is.Not.Null);
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null);
            return (T)property.GetValue(target, null);
        }
    }
}
