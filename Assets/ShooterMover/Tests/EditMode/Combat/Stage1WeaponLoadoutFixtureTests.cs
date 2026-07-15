#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Identity;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Combat
{
    public sealed class Stage1WeaponLoadoutFixtureTests
    {
        private const string RuntimePath =
            "Assets/ShooterMover/ContentPackages/Weapons/Stage1Loadouts/Stage1WeaponLoadoutFixtures.cs";
        private const string DocumentationPath =
            "Assets/ShooterMover/ContentPackages/Weapons/Stage1Loadouts/STAGE1_WEAPON_LOADOUTS_V1.md";
        private const string ManifestPath =
            "Assets/ShooterMover/Tests/PlayMode/Combat/Fixtures/Stage1WeaponLoadouts/stage1-weapon-loadouts-v1.json";

        private const string BlasterId = "weapon.blaster-machine-gun";
        private const string ShotgunId = "weapon.shotgun";
        private const string RocketId = "weapon.rocket-launcher";
        private const string ArcId = "weapon.arc-gun";
        private const string RicochetId = "weapon.ricochet-gun";
        private const int CanonicalEvidenceSeed = 104729;

        private const string SourceCommit =
            "807f74e614509c4e6d9ca0d09b7276353cbfc00d";
        private const string PackageLockFingerprint =
            "sha256:1111111111111111111111111111111111111111111111111111111111111111";
        private const string ContentFingerprint =
            "sha256:2222222222222222222222222222222222222222222222222222222222222222";
        private const string ExpectedDefaultChecksum =
            "sha256:050ff39c21815ab0e3f3b41ff074aef86bbdb6103691dbc49aaf9cd7cc0d320e";
        private const string ExpectedRicochetChecksum =
            "sha256:11c952ba7fcb44ad24eed162e34536a38fa6375c42166b3b947c8bac5204a26f";
        private const string ExpectedSeededLoadoutChecksum =
            "sha256:13e182ba0c2fe6f67ff68f059842b924fa1c45b10b710c58f8a9cb70efb67803";
        private const string ExpectedSeededSessionChecksum =
            "sha256:6969084b932809eb16814ab5377d12f04599701e03fca624389d9c16f0e2722d";

        [Test]
        public void ApprovedCatalog_UsesExactlyFiveValidatedDescriptors()
        {
            object catalog = GetStaticProperty<object>(RuntimeTypes.Catalog, "Approved");
            List<object> descriptors = GetObjectList(catalog, "PackageDescriptors");
            string[] ids = descriptors
                .Select(descriptor => GetProperty<StableId>(descriptor, "DefinitionId").ToString())
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            Assert.That(descriptors, Has.Count.EqualTo(5));
            Assert.That(
                ids,
                Is.EqualTo(
                    new[]
                    {
                        ArcId,
                        BlasterId,
                        RicochetId,
                        RocketId,
                        ShotgunId,
                    }));
            Assert.That(
                descriptors.Count(
                    descriptor => GetProperty<bool>(
                        descriptor,
                        "IsDefaultStartingWeapon")),
                Is.EqualTo(1));
            object defaultDescriptor = descriptors.Single(
                descriptor => GetProperty<bool>(
                    descriptor,
                    "IsDefaultStartingWeapon"));
            Assert.That(
                GetProperty<StableId>(defaultDescriptor, "DefinitionId").ToString(),
                Is.EqualTo(BlasterId));

            TestContext.WriteLine(
                "descriptor-roster count=5 unique=true validated=true default=weapon.blaster-machine-gun");
        }

        [Test]
        public void FixedFixtures_HaveExactlyFourCanonicalDistinctApprovedSlots()
        {
            object catalog = GetStaticProperty<object>(RuntimeTypes.Catalog, "Approved");
            List<object> fixtures = GetObjectList(catalog, "FixedFixtures");

            Assert.That(fixtures, Has.Count.EqualTo(2));
            foreach (object fixture in fixtures)
            {
                Assert.That(GetProperty<int>(fixture, "Count"), Is.EqualTo(4));
                HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < 4; index++)
                {
                    object slot = Invoke(fixture, "GetByHudIndex", index);
                    Assert.That(
                        Convert.ToInt32(GetProperty<object>(slot, "Slot")),
                        Is.EqualTo(index + 1));
                    string weaponId = GetProperty<StableId>(slot, "WeaponId").ToString();
                    Assert.That(ids.Add(weaponId), Is.True);
                    Assert.That(
                        Invoke(
                            catalog,
                            "ResolveDescriptor",
                            StableId.Parse(weaponId)),
                        Is.Not.Null);
                }
            }

            TestContext.WriteLine(
                "fixed-fixtures count=2 slots-per-fixture=4 canonical-order=true duplicate-ids=false");
        }

        [Test]
        public void DefaultComparisonIncludesBlasterAndMatrixCoversAllFive()
        {
            object catalog = GetStaticProperty<object>(RuntimeTypes.Catalog, "Approved");
            object defaultFixture = GetProperty<object>(catalog, "DefaultFixture");
            Assert.That(
                (bool)Invoke(
                    defaultFixture,
                    "ContainsWeapon",
                    StableId.Parse(BlasterId)),
                Is.True);

            HashSet<string> covered = new HashSet<string>(StringComparer.Ordinal);
            foreach (object fixture in GetObjectList(catalog, "FixedFixtures"))
            {
                for (int index = 0; index < 4; index++)
                {
                    object slot = Invoke(fixture, "GetByHudIndex", index);
                    covered.Add(GetProperty<StableId>(slot, "WeaponId").ToString());
                }
            }

            Assert.That(
                covered.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                Is.EqualTo(
                    new[]
                    {
                        ArcId,
                        BlasterId,
                        RicochetId,
                        RocketId,
                        ShotgunId,
                    }));

            TestContext.WriteLine(
                "matrix default-has-blaster=true all-five-coverage=true scene-edit-required=false");
        }

        [Test]
        public void UnknownWeaponId_IsRejected()
        {
            Array slots = CreateSlots(
                BlasterId,
                ShotgunId,
                RocketId,
                "weapon.not-approved");

            TargetInvocationException exception =
                Assert.Throws<TargetInvocationException>(
                    () => InvokeStatic(
                        RuntimeTypes.Fixture,
                        "Create",
                        StableId.Parse("loadout.invalid-unknown"),
                        slots));
            Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(
                exception.InnerException.Message,
                Does.Contain("approved Stage 1 weapon IDs"));

            TestContext.WriteLine("unknown-id rejected=true");
        }

        [Test]
        public void DuplicateWeaponId_IsRejected()
        {
            Array slots = CreateSlots(
                BlasterId,
                ShotgunId,
                RocketId,
                RocketId);

            TargetInvocationException exception =
                Assert.Throws<TargetInvocationException>(
                    () => InvokeStatic(
                        RuntimeTypes.Fixture,
                        "Create",
                        StableId.Parse("loadout.invalid-duplicate"),
                        slots));
            Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(
                exception.InnerException.Message,
                Does.Contain("cannot repeat a weapon identity"));

            TestContext.WriteLine("duplicate-weapon-id rejected=true");
        }

        [Test]
        public void CanonicalEvidenceSeed_ReproducesOrderedLoadoutAndChecksums()
        {
            BuildIdentity buildIdentity = CreateBuildIdentity(ContentFingerprint);
            ContentVersion contentVersion =
                ContentVersion.Create(1, ContentFingerprint);

            object first = CreateSession(
                CanonicalEvidenceSeed,
                buildIdentity,
                contentVersion);
            object replay = CreateSession(
                CanonicalEvidenceSeed,
                buildIdentity,
                contentVersion);

            Assert.That(
                Invoke(first, "ToCanonicalString"),
                Is.EqualTo(Invoke(replay, "ToCanonicalString")));
            Assert.That(
                GetProperty<string>(first, "Checksum"),
                Is.EqualTo(ExpectedSeededSessionChecksum));
            Assert.That(
                GetProperty<string>(replay, "Checksum"),
                Is.EqualTo(ExpectedSeededSessionChecksum));

            object loadout = GetProperty<object>(first, "Loadout");
            Assert.That(
                GetProperty<string>(loadout, "Checksum"),
                Is.EqualTo(ExpectedSeededLoadoutChecksum));
            Assert.That(
                CopyWeaponIds(loadout),
                Is.EqualTo(
                    new[]
                    {
                        BlasterId,
                        ArcId,
                        ShotgunId,
                        RocketId,
                    }));

            TestContext.WriteLine(
                "seed=104729 ordered=[blaster,arc,shotgun,rocket] loadout-checksum="
                + ExpectedSeededLoadoutChecksum
                + " session-checksum="
                + ExpectedSeededSessionChecksum);
        }

        [Test]
        public void BuildAndContentFingerprintMismatch_IsRejected()
        {
            BuildIdentity buildIdentity = CreateBuildIdentity(ContentFingerprint);
            ContentVersion staleContent = ContentVersion.Create(
                1,
                "sha256:3333333333333333333333333333333333333333333333333333333333333333");

            TargetInvocationException exception =
                Assert.Throws<TargetInvocationException>(
                    () => CreateSession(
                        CanonicalEvidenceSeed,
                        buildIdentity,
                        staleContent));
            Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
            Assert.That(
                exception.InnerException.Message,
                Does.Contain("same content fingerprint"));

            TestContext.WriteLine("identity-content-drift rejected=true");
        }

        [Test]
        public void SeededWrapper_HasNoPersistenceOrRandomizedModifierCapability()
        {
            object session = CreateSession(
                CanonicalEvidenceSeed,
                CreateBuildIdentity(ContentFingerprint),
                ContentVersion.Create(1, ContentFingerprint));

            Assert.That(GetProperty<bool>(session, "HasPersistentInventory"), Is.False);
            Assert.That(GetProperty<bool>(session, "CanPersistRewards"), Is.False);
            Assert.That(GetProperty<bool>(session, "CreatesRandomizedModifiers"), Is.False);

            PropertyInfo[] properties = RuntimeTypes.Session.GetProperties(
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(properties.All(property => !property.CanWrite), Is.True);
            Assert.That(
                RuntimeTypes.Session.GetFields(
                    BindingFlags.Instance | BindingFlags.Public),
                Is.Empty);

            TestContext.WriteLine(
                "evidence-wrapper immutable=true inventory-persistence=false reward-persistence=false randomized-modifiers=false");
        }

        [Test]
        public void FixedFixtureChecksums_AreStable()
        {
            object catalog = GetStaticProperty<object>(RuntimeTypes.Catalog, "Approved");
            Dictionary<string, string> checksums = GetObjectList(catalog, "FixedFixtures")
                .ToDictionary(
                    fixture => GetProperty<StableId>(fixture, "FixtureId").ToString(),
                    fixture => GetProperty<string>(fixture, "Checksum"),
                    StringComparer.Ordinal);

            Assert.That(
                checksums["loadout.stage1-default-comparison"],
                Is.EqualTo(ExpectedDefaultChecksum));
            Assert.That(
                checksums["loadout.stage1-ricochet-comparison"],
                Is.EqualTo(ExpectedRicochetChecksum));

            TestContext.WriteLine(
                "fixed-checksums default="
                + ExpectedDefaultChecksum
                + " ricochet="
                + ExpectedRicochetChecksum);
        }

        [Test]
        public void TrackedManifestAndDocumentation_DescribeSelectableNonPersistentComparisons()
        {
            string manifest = ReadProjectFile(ManifestPath);
            string documentation = ReadProjectFile(DocumentationPath);

            Assert.That(manifest, Does.Contain("\"runSeed\": 104729"));
            Assert.That(manifest, Does.Contain(ExpectedDefaultChecksum));
            Assert.That(manifest, Does.Contain(ExpectedRicochetChecksum));
            Assert.That(manifest, Does.Contain(ExpectedSeededLoadoutChecksum));
            Assert.That(manifest, Does.Contain(ExpectedSeededSessionChecksum));
            Assert.That(manifest, Does.Contain("\"persistence\": \"none\""));
            Assert.That(manifest, Does.Contain("\"randomizedModifiers\": \"none\""));
            Assert.That(documentation, Does.Contain("Stage1WeaponLoadoutCatalog.Approved"));
            Assert.That(documentation, Does.Contain("GetFixedFixture"));
            Assert.That(documentation, Does.Contain("No serialized scene edit"));
            Assert.That(documentation, Does.Contain("Stage 2"));

            TestContext.WriteLine(
                "fixture-manifest present=true manual-selection-documented=true persistence=none");
        }

        [Test]
        public void RuntimeSource_ContainsNoRandomPersistenceOrModifierSubsystem()
        {
            string source = ReadProjectFile(RuntimePath);
            string[] forbiddenTokens =
            {
                "System.Random",
                "UnityEngine.Random",
                "PlayerPrefs",
                "File.Write",
                "Directory.Create",
                "InventoryService",
                "RewardService",
                "SaveService",
                "WeaponModifier",
                "UnityEngine.SceneManagement",
            };

            foreach (string token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), "Forbidden token: " + token);
            }

            Assert.That(source, Does.Contain("BuildIdentity"));
            Assert.That(source, Does.Contain("ContentVersion"));
            Assert.That(source, Does.Contain("WeaponMountContractRules.MountCount"));
            Assert.That(source, Does.Contain("Stage1WeaponPackageValidator.Validate"));

            TestContext.WriteLine(
                "source-surface random-api=false save-api=false inventory=false rewards=false modifiers=false");
        }

        private static object CreateSession(
            int runSeed,
            BuildIdentity buildIdentity,
            ContentVersion contentVersion)
        {
            return InvokeStatic(
                RuntimeTypes.Session,
                "Create",
                runSeed,
                buildIdentity,
                contentVersion);
        }

        private static BuildIdentity CreateBuildIdentity(string contentFingerprint)
        {
            return BuildIdentity.CreateDevelopment(
                SourceCommit,
                "6000.3.19f1",
                PackageLockFingerprint,
                contentFingerprint,
                1,
                false);
        }

        private static Array CreateSlots(
            string mountOne,
            string mountTwo,
            string mountThree,
            string mountFour)
        {
            string[] weaponIds = { mountOne, mountTwo, mountThree, mountFour };
            Array slots = Array.CreateInstance(RuntimeTypes.Slot, 4);
            for (int index = 0; index < weaponIds.Length; index++)
            {
                object slot = InvokeStatic(
                    RuntimeTypes.Slot,
                    "Create",
                    (WeaponMountSlot)(index + 1),
                    StableId.Parse(weaponIds[index]));
                slots.SetValue(slot, index);
            }

            return slots;
        }

        private static string[] CopyWeaponIds(object fixture)
        {
            string[] result = new string[4];
            for (int index = 0; index < result.Length; index++)
            {
                object slot = Invoke(fixture, "GetByHudIndex", index);
                result[index] = GetProperty<StableId>(slot, "WeaponId").ToString();
            }

            return result;
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(instance, null);
        }

        private static T GetStaticProperty<T>(Type type, string propertyName)
        {
            PropertyInfo property = type.GetProperty(
                propertyName,
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(null, null);
        }

        private static List<object> GetObjectList(
            object instance,
            string propertyName)
        {
            IEnumerable enumerable = GetProperty<IEnumerable>(
                instance,
                propertyName);
            return enumerable.Cast<object>().ToList();
        }

        private static object Invoke(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = instance.GetType().GetMethods(
                    BindingFlags.Instance | BindingFlags.Public)
                .Single(
                    candidate => candidate.Name == methodName
                        && candidate.GetParameters().Length == arguments.Length);
            return method.Invoke(instance, arguments);
        }

        private static object InvokeStatic(
            Type type,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = type.GetMethods(
                    BindingFlags.Static | BindingFlags.Public)
                .Single(
                    candidate => candidate.Name == methodName
                        && candidate.GetParameters().Length == arguments.Length);
            return method.Invoke(null, arguments);
        }

        private static string ReadProjectFile(string assetPath)
        {
            string projectRoot = Directory.GetParent(
                UnityEngine.Application.dataPath).FullName;
            return File.ReadAllText(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        private static class RuntimeTypes
        {
            public static readonly Type Catalog = Find(
                "ShooterMover.ContentPackages.Weapons.Stage1Loadouts.Stage1WeaponLoadoutCatalog");
            public static readonly Type Fixture = Find(
                "ShooterMover.ContentPackages.Weapons.Stage1Loadouts.Stage1WeaponLoadoutFixture");
            public static readonly Type Slot = Find(
                "ShooterMover.ContentPackages.Weapons.Stage1Loadouts.Stage1WeaponLoadoutSlot");
            public static readonly Type Session = Find(
                "ShooterMover.ContentPackages.Weapons.Stage1Loadouts.Stage1WeaponLoadoutEvidenceSession");

            private static Type Find(string fullName)
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(fullName, false))
                    .FirstOrDefault(candidate => candidate != null);
                if (type == null)
                {
                    throw new InvalidOperationException(
                        "Runtime type not found: " + fullName);
                }

                return type;
            }
        }
    }
}
#endif
