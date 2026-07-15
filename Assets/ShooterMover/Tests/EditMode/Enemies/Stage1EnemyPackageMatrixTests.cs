using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using ShooterMover.Contracts.Content;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Enemies
{
    /// <summary>
    /// EN-009 package and registry-input validation for the five concrete Stage 1 enemies.
    /// Package implementations compile into Assembly-CSharp, so this asmdef-backed fixture
    /// resolves their descriptor factories through a narrow reflection bridge. CS-011 stays
    /// the sole generator and every generated artifact used here lives under a temporary root.
    /// </summary>
    public sealed class Stage1EnemyPackageMatrixTests
    {
        private const int OrdinaryClassification = 1;
        private const int EliteClassification = 2;

        private const string FixtureRelativePath =
            "Assets/ShooterMover/Tests/EditMode/Enemies/Fixtures/"
            + "stage1-enemy-registry-input-v1.json";
        private const string RegistryToolRelativePath =
            "tools/content-validation/content_registry.py";
        private const string DescriptorSchema =
            "urn:shooter-mover:schema:content-definition-descriptor-input:1";
        private const string FixtureSchema =
            "urn:shooter-mover:fixture:stage1-enemy-registry-input:1";
        private const string FixtureSha256 =
            "3dd27a97474939c408d9676c0c81f9b41bf2052b38b037aade35d8a2d0b43d8f";

        private static readonly string[] GeneratedRelativePaths =
        {
            "Assets/ShooterMover/Generated/content-registry.json",
            "Assets/ShooterMover/Generated/content-review-snapshot.json",
        };

        private static readonly string[] ExpectedEnemyOrder =
        {
            "enemy.blaster-turret",
            "enemy.four-blaster-elite",
            "enemy.mobile-blaster-droid",
            "enemy.pursuer-drone",
            "enemy.ram-droid",
        };

        private Dictionary<string, FileSnapshot> generatedSnapshots;

        [SetUp]
        public void CaptureCommittedGeneratedOutputs()
        {
            string repositoryRoot = FindRepositoryRoot();
            generatedSnapshots = GeneratedRelativePaths.ToDictionary(
                relativePath => relativePath,
                relativePath => FileSnapshot.Capture(
                    Path.Combine(repositoryRoot, relativePath)));
        }

        [TearDown]
        public void ConfirmCommittedGeneratedOutputsWereNotTouched()
        {
            string repositoryRoot = FindRepositoryRoot();
            foreach (KeyValuePair<string, FileSnapshot> pair in generatedSnapshots)
            {
                pair.Value.AssertUnchanged(
                    Path.Combine(repositoryRoot, pair.Key),
                    pair.Key);
            }
        }

        [Test]
        public void FiveRealPackages_ValidateFourOrdinaryOneEliteAndStableOrdering()
        {
            RegistryFixture fixture = LoadFixture();
            object[] roster = CreateProductionRoster(fixture);
            object first = ValidateRoster(roster);
            object[] shuffled =
            {
                roster[4],
                roster[1],
                roster[3],
                roster[0],
                roster[2],
            };
            object second = ValidateRoster(shuffled);

            Assert.That(GetProperty<bool>(first, "IsValid"), Is.True, Canonical(first));
            Assert.That(GetObjectList(first, "Errors"), Is.Empty);
            Assert.That(GetObjectList(first, "Packages"), Has.Count.EqualTo(5));
            Assert.That(
                GetObjectList(first, "Packages").Count(
                    package => GetEnumInt(package, "Classification") == OrdinaryClassification),
                Is.EqualTo(4));
            Assert.That(
                GetObjectList(first, "Packages").Count(
                    package => GetEnumInt(package, "Classification") == EliteClassification),
                Is.EqualTo(1));
            Assert.That(Canonical(first), Is.EqualTo(Canonical(second)));

            AssertFixtureMatchesProduction(fixture, roster);
            List<ContentDefinitionDescriptor> registryInputs = GetRegistryInputs(first);
            Assert.That(registryInputs, Has.Count.EqualTo(5));
            Assert.That(
                registryInputs.All(input => input.Kind == ContentDefinitionKind.Enemy),
                Is.True);
            Assert.That(
                registryInputs.Select(input => input.DefinitionId.ToString()),
                Is.EqualTo(ExpectedEnemyOrder));
            Assert.That(
                GetRegistryInputs(second).Select(input => input.DefinitionId.ToString()),
                Is.EqualTo(ExpectedEnemyOrder));

            TestContext.Progress.WriteLine("stage1-enemy-package-matrix: ok");
            TestContext.Progress.WriteLine("ordinary_role_count=4");
            TestContext.Progress.WriteLine("elite_role_count=1");
            TestContext.Progress.WriteLine("stable_enemy_order=" + string.Join(",", ExpectedEnemyOrder));
            TestContext.Progress.WriteLine("fixture_sha256=" + FixtureSha256);
            TestContext.Progress.WriteLine("no_generated_output_write=true");
        }

        [Test]
        public void DuplicateMissingAndUnexpectedSixthRoles_FailVisibly()
        {
            RegistryFixture fixture = LoadFixture();
            object[] roster = CreateProductionRoster(fixture);

            object duplicateAndMissing = ValidateRoster(
                new[]
                {
                    roster[0],
                    roster[0],
                    roster[2],
                    roster[3],
                    roster[4],
                });
            Assert.That(GetProperty<bool>(duplicateAndMissing, "IsValid"), Is.False);
            Assert.That(
                HasError(duplicateAndMissing, "DuplicatePackageId", "enemy.pursuer-drone"),
                Is.True,
                Canonical(duplicateAndMissing));
            Assert.That(
                HasError(duplicateAndMissing, "MissingPackage", "enemy.ram-droid"),
                Is.True,
                Canonical(duplicateAndMissing));
            Assert.Throws<InvalidOperationException>(
                () => GetRegistryInputs(duplicateAndMissing));

            object missing = ValidateRoster(roster.Take(4));
            Assert.That(GetProperty<bool>(missing, "IsValid"), Is.False);
            Assert.That(
                HasError(missing, "MissingPackage", "enemy.four-blaster-elite"),
                Is.True,
                Canonical(missing));

            object unexpected = CreateUnexpectedSixthRole(roster[0]);
            object sixthRole = ValidateRoster(roster.Concat(new[] { unexpected }));
            Assert.That(GetProperty<bool>(sixthRole, "IsValid"), Is.False);
            Assert.That(GetObjectList(sixthRole, "Packages"), Has.Count.EqualTo(6));
            Assert.That(
                HasError(sixthRole, "UnknownPackageId", "enemy.unexpected-sixth-role"),
                Is.True,
                Canonical(sixthRole));
            Assert.Throws<InvalidOperationException>(() => GetRegistryInputs(sixthRole));

            TestContext.Progress.WriteLine(
                "duplicate_role_failure="
                + FirstMatchingLine(Canonical(duplicateAndMissing), "code=duplicate-package-id"));
            TestContext.Progress.WriteLine(
                "missing_role_failure="
                + FirstMatchingLine(Canonical(missing), "code=missing-package"));
            TestContext.Progress.WriteLine(
                "unexpected_sixth_role_failure="
                + FirstMatchingLine(Canonical(sixthRole), "code=unknown-package-id"));
        }

        [Test]
        public void ShuffledRegistryInputs_GenerateByteStableOutputs()
        {
            RegistryFixture fixture = LoadFixture();
            object validation = ValidateRoster(CreateProductionRoster(fixture));
            Assert.That(GetProperty<bool>(validation, "IsValid"), Is.True, Canonical(validation));
            List<ContentDefinitionDescriptor> definitions =
                BuildRegistryDefinitions(validation);

            using (TemporaryDirectory first = new TemporaryDirectory("en009-first"))
            using (TemporaryDirectory second = new TemporaryDirectory("en009-second"))
            {
                string firstInput = WriteCatalog(
                    first.Path,
                    definitions,
                    Enumerable.Range(0, definitions.Count).ToArray());
                int[] shuffledOrder = Enumerable.Range(0, definitions.Count).ToArray();
                Shuffle(shuffledOrder, 9009);
                string secondInput = WriteCatalog(second.Path, definitions, shuffledOrder);

                RegistryRun firstRun = RunRegistryTool(
                    "generate",
                    first.Path,
                    firstInput,
                    fixture);
                RegistryRun secondRun = RunRegistryTool(
                    "generate",
                    second.Path,
                    secondInput,
                    fixture);

                AssertSuccessful(firstRun);
                AssertSuccessful(secondRun);
                Assert.That(firstRun.MachineChecksum, Is.EqualTo(secondRun.MachineChecksum));
                Assert.That(firstRun.ReviewChecksum, Is.EqualTo(secondRun.ReviewChecksum));
                CollectionAssert.AreEqual(
                    File.ReadAllBytes(firstRun.RegistryPath),
                    File.ReadAllBytes(secondRun.RegistryPath));
                CollectionAssert.AreEqual(
                    File.ReadAllBytes(firstRun.ReviewPath),
                    File.ReadAllBytes(secondRun.ReviewPath));

                string machineJson = File.ReadAllText(firstRun.RegistryPath, Encoding.UTF8);
                Assert.That(
                    CountOccurrences(machineJson, "\"definition_kind\": \"enemy\""),
                    Is.EqualTo(5));
                AssertEnemyOrder(machineJson);

                TestContext.Progress.WriteLine("stage1-enemy-registry-input: ok");
                TestContext.Progress.WriteLine(
                    "first_machine_sha256=" + firstRun.MachineChecksum);
                TestContext.Progress.WriteLine(
                    "second_machine_sha256=" + secondRun.MachineChecksum);
                TestContext.Progress.WriteLine(
                    "first_review_sha256=" + firstRun.ReviewChecksum);
                TestContext.Progress.WriteLine(
                    "second_review_sha256=" + secondRun.ReviewChecksum);
                TestContext.Progress.WriteLine(
                    "registry_input_definition_count="
                    + definitions.Count.ToString(CultureInfo.InvariantCulture));
                TestContext.Progress.WriteLine("no_generated_output_write=true");
            }
        }

        [Test]
        public void DuplicateDefinitionMissingReferenceAndFixtureDrift_FailVisibly()
        {
            RegistryFixture fixture = LoadFixture();
            object[] roster = CreateProductionRoster(fixture);
            object validation = ValidateRoster(roster);
            Assert.That(GetProperty<bool>(validation, "IsValid"), Is.True, Canonical(validation));
            List<ContentDefinitionDescriptor> definitions =
                BuildRegistryDefinitions(validation);

            using (TemporaryDirectory duplicateRoot =
                   new TemporaryDirectory("en009-duplicate"))
            {
                List<ContentDefinitionDescriptor> duplicated =
                    new List<ContentDefinitionDescriptor>(definitions);
                duplicated.Add(
                    definitions.First(
                        definition => definition.Kind == ContentDefinitionKind.Enemy));
                string input = WriteCatalog(
                    duplicateRoot.Path,
                    duplicated,
                    Enumerable.Range(0, duplicated.Count).ToArray());
                RegistryRun run = RunRegistryTool(
                    "validate",
                    duplicateRoot.Path,
                    input,
                    fixture);

                Assert.That(run.ExitCode, Is.EqualTo(2), run.CombinedOutput);
                Assert.That(run.StandardError, Does.Contain("duplicate-definition"));
                TestContext.Progress.WriteLine(
                    "duplicate_registry_input_failure="
                    + FirstMatchingLine(run.StandardError, "code=duplicate-definition"));
            }

            using (TemporaryDirectory missingRoot =
                   new TemporaryDirectory("en009-missing-reference"))
            {
                const string omittedId = "module.enemy-four-origin-telegraph";
                List<ContentDefinitionDescriptor> missingReference = definitions
                    .Where(
                        definition => !string.Equals(
                            definition.DefinitionId.ToString(),
                            omittedId,
                            StringComparison.Ordinal))
                    .ToList();
                string input = WriteCatalog(
                    missingRoot.Path,
                    missingReference,
                    Enumerable.Range(0, missingReference.Count).ToArray());
                RegistryRun run = RunRegistryTool(
                    "validate",
                    missingRoot.Path,
                    input,
                    fixture);

                Assert.That(run.ExitCode, Is.EqualTo(2), run.CombinedOutput);
                Assert.That(run.StandardError, Does.Contain("missing-definition"));
                Assert.That(run.StandardError, Does.Contain(omittedId));
                TestContext.Progress.WriteLine(
                    "missing_registry_reference_failure="
                    + FirstMatchingLine(run.StandardError, "code=missing-definition"));
            }

            RegistryFixture staleFixture = LoadFixture();
            staleFixture.packages[0].movement_id = "module.enemy-stale-pursuit";
            InvalidOperationException stale = Assert.Throws<InvalidOperationException>(
                () => AssertFixtureMatchesProduction(staleFixture, roster));
            Assert.That(stale.Message, Does.StartWith("stale-package-input:"));
            TestContext.Progress.WriteLine(stale.Message);
        }

        [Test]
        public void DriftedTemporaryOutput_FailsWithVisibleChecksums()
        {
            RegistryFixture fixture = LoadFixture();
            object validation = ValidateRoster(CreateProductionRoster(fixture));
            Assert.That(GetProperty<bool>(validation, "IsValid"), Is.True, Canonical(validation));
            List<ContentDefinitionDescriptor> definitions =
                BuildRegistryDefinitions(validation);

            using (TemporaryDirectory temporary =
                   new TemporaryDirectory("en009-registry-drift"))
            {
                string input = WriteCatalog(
                    temporary.Path,
                    definitions,
                    Enumerable.Range(0, definitions.Count).ToArray());
                RegistryRun generated = RunRegistryTool(
                    "generate",
                    temporary.Path,
                    input,
                    fixture);
                AssertSuccessful(generated);

                File.AppendAllText(generated.RegistryPath, " ", new UTF8Encoding(false));
                RegistryRun checkedRun = RunRegistryTool(
                    "check",
                    temporary.Path,
                    input,
                    fixture);

                Assert.That(checkedRun.ExitCode, Is.EqualTo(3), checkedRun.CombinedOutput);
                Assert.That(checkedRun.StandardError, Does.Contain("registry-drift: failed"));
                Assert.That(checkedRun.StandardError, Does.Contain("expected_sha256="));
                Assert.That(checkedRun.StandardError, Does.Contain("actual_sha256="));
                TestContext.Progress.WriteLine(
                    "drift_failure_exit_code=" + checkedRun.ExitCode);
                TestContext.Progress.WriteLine(checkedRun.StandardError.Trim());
            }
        }

        [Test]
        public void OwnershipDeclarations_ArePackageLocalDisjointAndDebtExplicit()
        {
            RegistryFixture fixture = LoadFixture();
            string repositoryRoot = FindRepositoryRoot();
            HashSet<string> serializedOwners = new HashSet<string>(StringComparer.Ordinal);
            int serializedAssetCount = 0;

            for (int index = 0; index < fixture.packages.Length; index++)
            {
                PackageFixture package = fixture.packages[index];
                string normalizedRoot = NormalizeRelativePath(package.package_root);
                Assert.That(
                    Directory.Exists(Path.Combine(repositoryRoot, normalizedRoot)),
                    Is.True,
                    "Missing package root: " + normalizedRoot);
                Assert.That(
                    package.temporary_presentation_debt,
                    Is.Not.Null.And.Not.Empty,
                    package.enemy_id + " must declare temporary-presentation debt.");
                Assert.That(package.serialized_assets, Is.Not.Null);

                bool isElite = string.Equals(
                    package.classification,
                    "elite",
                    StringComparison.Ordinal);
                Assert.That(
                    package.serialized_assets.Length,
                    Is.EqualTo(isElite ? 0 : 2),
                    package.enemy_id);

                for (int assetIndex = 0;
                     assetIndex < package.serialized_assets.Length;
                     assetIndex++)
                {
                    string assetPath =
                        NormalizeRelativePath(package.serialized_assets[assetIndex]);
                    Assert.That(
                        IsSameOrChildPath(normalizedRoot, assetPath),
                        Is.True,
                        package.enemy_id + " claims an asset outside its package root: " + assetPath);
                    Assert.That(
                        File.Exists(Path.Combine(repositoryRoot, assetPath)),
                        Is.True,
                        "Missing serialized asset: " + assetPath);
                    Assert.That(
                        Path.GetExtension(assetPath),
                        Is.EqualTo(".asset").Or.EqualTo(".prefab"),
                        assetPath);
                    Assert.That(
                        serializedOwners.Add(assetPath),
                        Is.True,
                        "Duplicate serialized-asset ownership: " + assetPath);
                    Assert.That(
                        IsSameOrChildPath("Assets/ShooterMover/Generated", assetPath),
                        Is.False,
                        "Package ownership may not reach generated outputs.");
                    serializedAssetCount++;
                }
            }

            for (int left = 0; left < fixture.packages.Length; left++)
            {
                for (int right = left + 1; right < fixture.packages.Length; right++)
                {
                    string leftRoot =
                        NormalizeRelativePath(fixture.packages[left].package_root);
                    string rightRoot =
                        NormalizeRelativePath(fixture.packages[right].package_root);
                    Assert.That(
                        IsSameOrChildPath(leftRoot, rightRoot)
                            || IsSameOrChildPath(rightRoot, leftRoot),
                        Is.False,
                        "Package roots overlap: " + leftRoot + " <-> " + rightRoot);
                }
            }

            Assert.That(serializedAssetCount, Is.EqualTo(8));
            TestContext.Progress.WriteLine("ownership_audit=pass");
            TestContext.Progress.WriteLine("package_root_count=5");
            TestContext.Progress.WriteLine("serialized_asset_count=8");
            TestContext.Progress.WriteLine("root_overlap_count=0");
            TestContext.Progress.WriteLine("temporary_presentation_debt_count=5");
        }

        private static RegistryFixture LoadFixture()
        {
            string repositoryRoot = FindRepositoryRoot();
            string path = Path.Combine(repositoryRoot, FixtureRelativePath);
            Assert.That(File.Exists(path), Is.True, "Missing EN-009 registry input fixture.");

            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(ComputeSha256(bytes), Is.EqualTo(FixtureSha256));
            string json = new UTF8Encoding(false, true).GetString(bytes);
            Assert.That(
                json,
                Does.Contain("\"$schema\": \"" + FixtureSchema + "\""));
            RegistryFixture fixture = JsonUtility.FromJson<RegistryFixture>(json);
            Assert.That(fixture, Is.Not.Null);
            Assert.That(fixture.schema_version, Is.EqualTo(1));
            Assert.That(fixture.catalog_version, Is.EqualTo(1));
            Assert.That(fixture.validation_mode, Is.EqualTo("release"));
            Assert.That(fixture.packages, Is.Not.Null);
            Assert.That(fixture.packages, Has.Length.EqualTo(5));
            Assert.That(
                fixture.packages.Select(package => package.enemy_id).Distinct().Count(),
                Is.EqualTo(5));
            Assert.That(
                fixture.packages.Count(
                    package => string.Equals(
                        package.classification,
                        "ordinary",
                        StringComparison.Ordinal)),
                Is.EqualTo(4));
            Assert.That(
                fixture.packages.Count(
                    package => string.Equals(
                        package.classification,
                        "elite",
                        StringComparison.Ordinal)),
                Is.EqualTo(1));
            Assert.That(
                fixture.packages.Select(package => package.package_root).Distinct().Count(),
                Is.EqualTo(5));
            return fixture;
        }

        private static object[] CreateProductionRoster(RegistryFixture fixture)
        {
            object[] roster = new object[fixture.packages.Length];
            for (int index = 0; index < fixture.packages.Length; index++)
            {
                PackageFixture package = fixture.packages[index];
                Type packageType = FindType(package.package_type);
                if (string.Equals(
                    package.descriptor_member_kind,
                    "static-method",
                    StringComparison.Ordinal))
                {
                    roster[index] = InvokeStatic(
                        packageType,
                        package.descriptor_member);
                }
                else if (string.Equals(
                    package.descriptor_member_kind,
                    "scriptable-object-method",
                    StringComparison.Ordinal))
                {
                    if (!typeof(ScriptableObject).IsAssignableFrom(packageType))
                    {
                        throw new InvalidOperationException(
                            "Descriptor owner is not a ScriptableObject: "
                            + package.package_type);
                    }

                    ScriptableObject definition = ScriptableObject.CreateInstance(packageType);
                    try
                    {
                        roster[index] = InvokeInstance(
                            definition,
                            package.descriptor_member);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(definition);
                    }
                }
                else
                {
                    throw new InvalidOperationException(
                        "Unsupported descriptor_member_kind: "
                        + package.descriptor_member_kind);
                }

                if (roster[index] == null
                    || !RuntimeTypes.Descriptor.IsInstanceOfType(roster[index]))
                {
                    throw new InvalidOperationException(
                        "Package factory did not return a Stage1EnemyPackageDescriptor: "
                        + package.package_type);
                }
            }

            return roster;
        }

        private static object ValidateRoster(IEnumerable<object> descriptors)
        {
            List<object> copied = descriptors.ToList();
            return InvokeStatic(
                RuntimeTypes.Validator,
                "Validate",
                ToRuntimeArray(RuntimeTypes.Descriptor, copied));
        }

        private static void AssertFixtureMatchesProduction(
            RegistryFixture fixture,
            IReadOnlyList<object> roster)
        {
            Dictionary<string, object> descriptors = roster.ToDictionary(
                descriptor => GetProperty<StableId>(descriptor, "DefinitionId").ToString(),
                descriptor => descriptor,
                StringComparer.Ordinal);
            if (descriptors.Count != fixture.packages.Length)
            {
                throw new InvalidOperationException(
                    "stale-package-input: descriptor-count="
                    + descriptors.Count.ToString(CultureInfo.InvariantCulture)
                    + ";fixture-count="
                    + fixture.packages.Length.ToString(CultureInfo.InvariantCulture));
            }

            for (int index = 0; index < fixture.packages.Length; index++)
            {
                PackageFixture expected = fixture.packages[index];
                object descriptor;
                if (!descriptors.TryGetValue(expected.enemy_id, out descriptor))
                {
                    throw Stale(expected.enemy_id, "production descriptor is missing");
                }

                ContentDefinitionDescriptor content =
                    GetProperty<ContentDefinitionDescriptor>(
                        descriptor,
                        "ContentDefinition");
                if (content == null
                    || content.Kind != ContentDefinitionKind.Enemy
                    || content.DefinitionVersion
                    != ContentReference.SupportedDefinitionVersion
                    || content.ProvenanceId == null
                    || content.IsPrototypeOnly)
                {
                    throw Stale(expected.enemy_id, "content definition contract changed");
                }

                string actualClassification = ToClassificationToken(
                    GetEnumInt(descriptor, "Classification"));
                if (!string.Equals(
                    actualClassification,
                    expected.classification,
                    StringComparison.Ordinal))
                {
                    throw Stale(
                        expected.enemy_id,
                        "classification changed; expected="
                        + expected.classification
                        + ";actual="
                        + actualClassification);
                }

                AssertReference(
                    expected,
                    "movement",
                    GetProperty<ContentReference>(descriptor, "MovementReference"),
                    "shared-module",
                    expected.movement_id);
                AssertReference(
                    expected,
                    "attack",
                    GetProperty<ContentReference>(descriptor, "AttackReference"),
                    expected.attack_kind,
                    expected.attack_id);
                AssertReference(
                    expected,
                    "telegraph",
                    GetProperty<ContentReference>(descriptor, "TelegraphReference"),
                    "shared-module",
                    expected.telegraph_id);

                string[] expectedReferences =
                {
                    "shared-module|" + expected.movement_id + "|1",
                    expected.attack_kind + "|" + expected.attack_id + "|1",
                    "shared-module|" + expected.telegraph_id + "|1",
                };
                string[] actualReferences = content.References
                    .Select(ToReferenceToken)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                if (!actualReferences.SequenceEqual(
                    expectedReferences.OrderBy(value => value, StringComparer.Ordinal)))
                {
                    throw Stale(
                        expected.enemy_id,
                        "declared registry references changed; actual="
                        + string.Join(",", actualReferences));
                }
            }
        }

        private static void AssertReference(
            PackageFixture package,
            string fieldName,
            ContentReference reference,
            string expectedKind,
            string expectedId)
        {
            if (reference == null)
            {
                throw Stale(package.enemy_id, fieldName + " reference is missing");
            }

            string actualKind = ToKindToken(reference.ExpectedKind);
            if (!string.Equals(actualKind, expectedKind, StringComparison.Ordinal)
                || !string.Equals(
                    reference.DefinitionId.ToString(),
                    expectedId,
                    StringComparison.Ordinal)
                || reference.ExpectedVersion
                != ContentReference.SupportedDefinitionVersion)
            {
                throw Stale(
                    package.enemy_id,
                    fieldName
                    + " reference changed; expected="
                    + expectedKind
                    + "|"
                    + expectedId
                    + "|1;actual="
                    + ToReferenceToken(reference));
            }
        }

        private static InvalidOperationException Stale(string enemyId, string detail)
        {
            return new InvalidOperationException(
                "stale-package-input: enemy_id="
                + enemyId
                + ";detail="
                + detail);
        }

        private static object CreateUnexpectedSixthRole(object source)
        {
            ContentReference movement =
                GetProperty<ContentReference>(source, "MovementReference");
            ContentReference attack =
                GetProperty<ContentReference>(source, "AttackReference");
            ContentReference telegraph =
                GetProperty<ContentReference>(source, "TelegraphReference");
            ContentDefinitionDescriptor content = ContentDefinitionDescriptor.Create(
                StableId.Parse("enemy.unexpected-sixth-role"),
                ContentDefinitionKind.Enemy,
                ContentReference.SupportedDefinitionVersion,
                StableId.Parse("provenance.en-009-unexpected-sixth-role"),
                false,
                movement,
                attack,
                telegraph);
            return InvokeStatic(
                RuntimeTypes.Descriptor,
                "Create",
                GetProperty<int>(source, "DescriptorVersion"),
                content,
                GetProperty<object>(source, "Classification"),
                GetProperty<object>(source, "DamageChannel"),
                GetProperty<object>(source, "WeightClass"),
                movement,
                attack,
                telegraph,
                GetProperty<object>(source, "Capabilities"));
        }

        private static List<ContentDefinitionDescriptor> GetRegistryInputs(object validation)
        {
            return ((IEnumerable)InvokeInstance(validation, "GetRegistryInputs"))
                .Cast<ContentDefinitionDescriptor>()
                .ToList();
        }

        private static List<ContentDefinitionDescriptor> BuildRegistryDefinitions(
            object validation)
        {
            List<ContentDefinitionDescriptor> enemyDefinitions =
                GetRegistryInputs(validation);
            List<ContentDefinitionDescriptor> definitions =
                new List<ContentDefinitionDescriptor>(enemyDefinitions);
            Dictionary<StableId, ContentReference> uniqueReferences =
                new Dictionary<StableId, ContentReference>();

            for (int enemyIndex = 0;
                 enemyIndex < enemyDefinitions.Count;
                 enemyIndex++)
            {
                ContentDefinitionDescriptor enemy = enemyDefinitions[enemyIndex];
                for (int referenceIndex = 0;
                     referenceIndex < enemy.References.Count;
                     referenceIndex++)
                {
                    ContentReference reference = enemy.References[referenceIndex];
                    ContentReference existing;
                    if (uniqueReferences.TryGetValue(reference.DefinitionId, out existing))
                    {
                        Assert.That(existing.ExpectedKind, Is.EqualTo(reference.ExpectedKind));
                        Assert.That(existing.ExpectedVersion, Is.EqualTo(reference.ExpectedVersion));
                    }
                    else
                    {
                        uniqueReferences.Add(reference.DefinitionId, reference);
                    }
                }
            }

            foreach (ContentReference reference in uniqueReferences.Values
                .OrderBy(
                    value => value.DefinitionId.ToString(),
                    StringComparer.Ordinal))
            {
                definitions.Add(
                    ContentDefinitionDescriptor.Create(
                        reference.DefinitionId,
                        reference.ExpectedKind,
                        reference.ExpectedVersion,
                        StableId.Create(
                            "provenance",
                            "en009-" + reference.DefinitionId.Value + "-fixture"),
                        false,
                        Array.Empty<ContentReference>()));
            }

            return definitions;
        }

        private static string WriteCatalog(
            string root,
            IReadOnlyList<ContentDefinitionDescriptor> definitions,
            IReadOnlyList<int> order)
        {
            string inputRoot = Path.Combine(root, "descriptor-inputs");
            Directory.CreateDirectory(inputRoot);
            for (int outputIndex = 0; outputIndex < order.Count; outputIndex++)
            {
                ContentDefinitionDescriptor descriptor = definitions[order[outputIndex]];
                string path = Path.Combine(
                    inputRoot,
                    "input-"
                    + outputIndex.ToString("D3", CultureInfo.InvariantCulture)
                    + ".content-descriptor.json");
                File.WriteAllText(
                    path,
                    RenderDescriptorInput(descriptor),
                    new UTF8Encoding(false));
            }

            return inputRoot;
        }

        private static string RenderDescriptorInput(
            ContentDefinitionDescriptor descriptor)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{\n")
                .Append("  \"$schema\": ")
                .Append(JsonString(DescriptorSchema))
                .Append(",\n  \"definition_kind\": ")
                .Append(JsonString(ToKindToken(descriptor.Kind)))
                .Append(",\n  \"definition_id\": ")
                .Append(JsonString(descriptor.DefinitionId.ToString()))
                .Append(",\n  \"definition_version\": ")
                .Append(descriptor.DefinitionVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\n  \"provenance_id\": ")
                .Append(descriptor.ProvenanceId == null
                    ? "null"
                    : JsonString(descriptor.ProvenanceId.ToString()))
                .Append(",\n  \"prototype_only\": ")
                .Append(descriptor.IsPrototypeOnly ? "true" : "false")
                .Append(",\n  \"references\": [");

            if (descriptor.References.Count > 0)
            {
                builder.Append('\n');
            }

            for (int index = 0; index < descriptor.References.Count; index++)
            {
                ContentReference reference = descriptor.References[index];
                builder.Append("    {\n")
                    .Append("      \"definition_kind\": ")
                    .Append(JsonString(ToKindToken(reference.ExpectedKind)))
                    .Append(",\n      \"definition_id\": ")
                    .Append(JsonString(reference.DefinitionId.ToString()))
                    .Append(",\n      \"definition_version\": ")
                    .Append(reference.ExpectedVersion.ToString(CultureInfo.InvariantCulture))
                    .Append("\n    }");
                if (index + 1 < descriptor.References.Count)
                {
                    builder.Append(',');
                }

                builder.Append('\n');
            }

            return builder.Append("  ]\n}\n").ToString();
        }

        private static RegistryRun RunRegistryTool(
            string command,
            string temporaryRoot,
            string descriptorRoot,
            RegistryFixture fixture)
        {
            string repositoryRoot = FindRepositoryRoot();
            string toolPath = Path.Combine(repositoryRoot, RegistryToolRelativePath);
            Assert.That(File.Exists(toolPath), Is.True, "Missing CS-011 registry tool.");

            string outputRoot = Path.Combine(temporaryRoot, "generated-output");
            string registryPath = Path.Combine(outputRoot, "content-registry.json");
            string reviewPath = Path.Combine(outputRoot, "content-review-snapshot.json");
            List<string> arguments = new List<string>();
            PythonCommand python = FindPython();
            arguments.AddRange(python.PrefixArguments);
            arguments.Add(toolPath);
            arguments.Add(command);
            arguments.Add("--root");
            arguments.Add(temporaryRoot);
            arguments.Add("--descriptor-root");
            arguments.Add(descriptorRoot);
            arguments.Add("--catalog-version");
            arguments.Add(fixture.catalog_version.ToString(CultureInfo.InvariantCulture));
            arguments.Add("--mode");
            arguments.Add(fixture.validation_mode);
            arguments.Add("--registry-output");
            arguments.Add(registryPath);
            arguments.Add("--review-output");
            arguments.Add(reviewPath);

            ProcessResult process = RunProcess(
                python.FileName,
                arguments,
                repositoryRoot,
                60000);
            return new RegistryRun(
                process.ExitCode,
                process.StandardOutput,
                process.StandardError,
                registryPath,
                reviewPath);
        }

        private static void AssertSuccessful(RegistryRun run)
        {
            Assert.That(run.ExitCode, Is.EqualTo(0), run.CombinedOutput);
            Assert.That(run.StandardOutput, Does.Contain("registry-generate: ok"));
            Assert.That(File.Exists(run.RegistryPath), Is.True);
            Assert.That(File.Exists(run.ReviewPath), Is.True);
            Assert.That(
                ComputeSha256(File.ReadAllBytes(run.RegistryPath)),
                Is.EqualTo(run.MachineChecksum));
            Assert.That(
                ComputeSha256(File.ReadAllBytes(run.ReviewPath)),
                Is.EqualTo(run.ReviewChecksum));
        }

        private static void AssertEnemyOrder(string machineJson)
        {
            int previous = -1;
            for (int index = 0; index < ExpectedEnemyOrder.Length; index++)
            {
                int current = machineJson.IndexOf(
                    "\"definition_id\": \"" + ExpectedEnemyOrder[index] + "\"",
                    StringComparison.Ordinal);
                Assert.That(current, Is.GreaterThan(previous), ExpectedEnemyOrder[index]);
                previous = current;
            }
        }

        private static bool HasError(
            object result,
            string codeName,
            string packageId = null)
        {
            return GetObjectList(result, "Errors").Any(
                error => string.Equals(
                        GetProperty<object>(error, "Code").ToString(),
                        codeName,
                        StringComparison.Ordinal)
                    && (packageId == null
                        || string.Equals(
                            GetProperty<StableId>(error, "PackageId")?.ToString(),
                            packageId,
                            StringComparison.Ordinal)));
        }

        private static string Canonical(object result)
        {
            return (string)InvokeInstance(result, "ToCanonicalString");
        }

        private static List<object> GetObjectList(object instance, string propertyName)
        {
            object value = GetProperty<object>(instance, propertyName);
            return value == null
                ? new List<object>()
                : ((IEnumerable)value).Cast<object>().ToList();
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                throw new InvalidOperationException(
                    "Missing property "
                    + instance.GetType().FullName
                    + "."
                    + propertyName
                    + ".");
            }

            return (T)property.GetValue(instance, null);
        }

        private static int GetEnumInt(object instance, string propertyName)
        {
            return Convert.ToInt32(GetProperty<object>(instance, propertyName));
        }

        private static object InvokeStatic(
            Type type,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = RequireMethod(
                type,
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                arguments.Length);
            return Invoke(method, null, arguments);
        }

        private static object InvokeInstance(
            object instance,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = RequireMethod(
                instance.GetType(),
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                arguments.Length);
            return Invoke(method, instance, arguments);
        }

        private static object Invoke(
            MethodInfo method,
            object instance,
            object[] arguments)
        {
            try
            {
                return method.Invoke(instance, arguments);
            }
            catch (TargetInvocationException exception)
            {
                if (exception.InnerException != null)
                {
                    throw exception.InnerException;
                }

                throw;
            }
        }

        private static MethodInfo RequireMethod(
            Type type,
            string methodName,
            BindingFlags flags,
            int argumentCount)
        {
            MethodInfo[] matches = type.GetMethods(flags)
                .Where(method => string.Equals(
                    method.Name,
                    methodName,
                    StringComparison.Ordinal))
                .Where(method => method.GetParameters().Length == argumentCount)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    "Expected one "
                    + type.FullName
                    + "."
                    + methodName
                    + " overload with "
                    + argumentCount.ToString(CultureInfo.InvariantCulture)
                    + " parameters, found "
                    + matches.Length.ToString(CultureInfo.InvariantCulture)
                    + ".");
            }

            return matches[0];
        }

        private static Array ToRuntimeArray(
            Type elementType,
            IReadOnlyList<object> values)
        {
            Array array = Array.CreateInstance(elementType, values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                array.SetValue(values[index], index);
            }

            return array;
        }

        private static Type FindType(string fullName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++)
            {
                Type type = assemblies[index].GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }

            throw new InvalidOperationException(
                "Production type was not loaded from the Unity project: "
                + fullName
                + ".");
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, RegistryToolRelativePath))
                    && Directory.Exists(Path.Combine(directory.FullName, "Assets")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the Shooter Mover repository root.");
        }

        private static PythonCommand pythonCommand;

        private static PythonCommand FindPython()
        {
            if (pythonCommand != null)
            {
                return pythonCommand;
            }

            List<PythonCommand> candidates = new List<PythonCommand>();
            string configured = Environment.GetEnvironmentVariable("SHOOTER_MOVER_PYTHON");
            if (!string.IsNullOrWhiteSpace(configured))
            {
                candidates.Add(new PythonCommand(configured, Array.Empty<string>()));
            }

            if (Path.DirectorySeparatorChar == '\\')
            {
                candidates.Add(new PythonCommand("python", Array.Empty<string>()));
                candidates.Add(new PythonCommand("py", new[] { "-3" }));
            }
            else
            {
                candidates.Add(new PythonCommand("python3", Array.Empty<string>()));
                candidates.Add(new PythonCommand("python", Array.Empty<string>()));
            }

            for (int index = 0; index < candidates.Count; index++)
            {
                PythonCommand candidate = candidates[index];
                try
                {
                    List<string> arguments =
                        new List<string>(candidate.PrefixArguments) { "--version" };
                    ProcessResult probe = RunProcess(
                        candidate.FileName,
                        arguments,
                        FindRepositoryRoot(),
                        10000);
                    if (probe.ExitCode == 0
                        && probe.CombinedOutput.IndexOf(
                            "Python 3.",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pythonCommand = candidate;
                        return pythonCommand;
                    }
                }
                catch (Exception)
                {
                    // Try the next explicit candidate.
                }
            }

            Assert.Fail(
                "EN-009 requires Python 3 to invoke the accepted CS-011 generator. "
                + "Set SHOOTER_MOVER_PYTHON when it is not on PATH.");
            return null;
        }

        private static ProcessResult RunProcess(
            string fileName,
            IEnumerable<string> arguments,
            string workingDirectory,
            int timeoutMilliseconds)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = string.Join(" ", arguments.Select(QuoteArgument)),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.EnvironmentVariables["PYTHONDONTWRITEBYTECODE"] = "1";
            startInfo.EnvironmentVariables["PYTHONUTF8"] = "1";

            using (Process process = new Process { StartInfo = startInfo })
            {
                process.Start();
                string standardOutput = process.StandardOutput.ReadToEnd();
                string standardError = process.StandardError.ReadToEnd();
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                    }

                    throw new TimeoutException(
                        fileName + " did not exit within the EN-009 test timeout.");
                }

                return new ProcessResult(
                    process.ExitCode,
                    standardOutput,
                    standardError);
            }
        }

        private static string QuoteArgument(string value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            if (value.Length > 0
                && value.All(character =>
                    !char.IsWhiteSpace(character)
                    && character != '"'))
            {
                return value;
            }

            StringBuilder builder = new StringBuilder();
            builder.Append('"');
            int backslashes = 0;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (character == '"')
                {
                    builder.Append('\\', (backslashes * 2) + 1);
                    builder.Append('"');
                    backslashes = 0;
                    continue;
                }

                builder.Append('\\', backslashes);
                backslashes = 0;
                builder.Append(character);
            }

            builder.Append('\\', backslashes * 2);
            builder.Append('"');
            return builder.ToString();
        }

        private static string ToClassificationToken(int classification)
        {
            if (classification == OrdinaryClassification)
            {
                return "ordinary";
            }

            if (classification == EliteClassification)
            {
                return "elite";
            }

            return classification.ToString(CultureInfo.InvariantCulture);
        }

        private static string ToKindToken(ContentDefinitionKind kind)
        {
            switch (kind)
            {
                case ContentDefinitionKind.Weapon:
                    return "weapon";
                case ContentDefinitionKind.Enemy:
                    return "enemy";
                case ContentDefinitionKind.Room:
                    return "room";
                case ContentDefinitionKind.Encounter:
                    return "encounter";
                case ContentDefinitionKind.Environment:
                    return "environment";
                case ContentDefinitionKind.SharedModule:
                    return "shared-module";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static string ToReferenceToken(ContentReference reference)
        {
            return ToKindToken(reference.ExpectedKind)
                + "|"
                + reference.DefinitionId
                + "|"
                + reference.ExpectedVersion.ToString(CultureInfo.InvariantCulture);
        }

        private static string JsonString(string value)
        {
            return "\""
                + value.Replace("\\", "\\\\").Replace("\"", "\\\"")
                + "\"";
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(bytes);
                StringBuilder builder = new StringBuilder(digest.Length * 2);
                for (int index = 0; index < digest.Length; index++)
                {
                    builder.Append(
                        digest[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static int CountOccurrences(string text, string value)
        {
            int count = 0;
            int offset = 0;
            while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += value.Length;
            }

            return count;
        }

        private static string FirstMatchingLine(string text, string value)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index].IndexOf(value, StringComparison.Ordinal) >= 0)
                {
                    return lines[index];
                }
            }

            return "missing";
        }

        private static void Shuffle(int[] values, int seed)
        {
            System.Random random = new System.Random(seed);
            for (int index = values.Length - 1; index > 0; index--)
            {
                int selected = random.Next(index + 1);
                int temporary = values[index];
                values[index] = values[selected];
                values[selected] = temporary;
            }
        }

        private static string NormalizeRelativePath(string path)
        {
            return (path ?? string.Empty)
                .Replace('\\', '/')
                .TrimEnd('/');
        }

        private static bool IsSameOrChildPath(string parent, string candidate)
        {
            string normalizedParent = NormalizeRelativePath(parent);
            string normalizedCandidate = NormalizeRelativePath(candidate);
            return string.Equals(
                    normalizedParent,
                    normalizedCandidate,
                    StringComparison.Ordinal)
                || normalizedCandidate.StartsWith(
                    normalizedParent + "/",
                    StringComparison.Ordinal);
        }

        [Serializable]
        private sealed class RegistryFixture
        {
            public int schema_version;
            public int catalog_version;
            public string validation_mode;
            public PackageFixture[] packages;
        }

        [Serializable]
        private sealed class PackageFixture
        {
            public string enemy_id;
            public string classification;
            public string package_root;
            public string package_type;
            public string descriptor_member;
            public string descriptor_member_kind;
            public string movement_id;
            public string attack_kind;
            public string attack_id;
            public string telegraph_id;
            public string[] serialized_assets;
            public string temporary_presentation_debt;
        }

        private sealed class FileSnapshot
        {
            private FileSnapshot(bool existed, byte[] bytes)
            {
                Existed = existed;
                Bytes = bytes;
            }

            public bool Existed { get; }

            public byte[] Bytes { get; }

            public static FileSnapshot Capture(string path)
            {
                return new FileSnapshot(
                    File.Exists(path),
                    File.Exists(path) ? File.ReadAllBytes(path) : null);
            }

            public void AssertUnchanged(string path, string displayPath)
            {
                Assert.That(
                    File.Exists(path),
                    Is.EqualTo(Existed),
                    "Generated output existence changed: " + displayPath);
                if (Existed)
                {
                    CollectionAssert.AreEqual(
                        Bytes,
                        File.ReadAllBytes(path),
                        "Generated output bytes changed: " + displayPath);
                }
            }
        }

        private sealed class TemporaryDirectory : IDisposable
        {
            public TemporaryDirectory(string prefix)
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "shooter-mover-"
                    + prefix
                    + "-"
                    + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);
                }
            }
        }

        private sealed class PythonCommand
        {
            public PythonCommand(
                string fileName,
                IEnumerable<string> prefixArguments)
            {
                FileName = fileName;
                PrefixArguments = prefixArguments.ToArray();
            }

            public string FileName { get; }

            public IReadOnlyList<string> PrefixArguments { get; }
        }

        private class ProcessResult
        {
            public ProcessResult(
                int exitCode,
                string standardOutput,
                string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput ?? string.Empty;
                StandardError = standardError ?? string.Empty;
            }

            public int ExitCode { get; }

            public string StandardOutput { get; }

            public string StandardError { get; }

            public string CombinedOutput
            {
                get { return StandardOutput + "\n" + StandardError; }
            }
        }

        private sealed class RegistryRun : ProcessResult
        {
            public RegistryRun(
                int exitCode,
                string standardOutput,
                string standardError,
                string registryPath,
                string reviewPath)
                : base(exitCode, standardOutput, standardError)
            {
                RegistryPath = registryPath;
                ReviewPath = reviewPath;
            }

            public string RegistryPath { get; }

            public string ReviewPath { get; }

            public string MachineChecksum
            {
                get { return ReadValue(StandardOutput, "machine_sha256"); }
            }

            public string ReviewChecksum
            {
                get { return ReadValue(StandardOutput, "review_sha256"); }
            }

            private static string ReadValue(string text, string key)
            {
                string prefix = key + "=";
                string[] lines = text.Replace("\r\n", "\n").Split('\n');
                for (int index = 0; index < lines.Length; index++)
                {
                    if (lines[index].StartsWith(prefix, StringComparison.Ordinal))
                    {
                        return lines[index].Substring(prefix.Length);
                    }
                }

                return null;
            }
        }

        private static class RuntimeTypes
        {
            public static readonly Type Descriptor = FindType(
                "ShooterMover.ContentPackages.Enemies.Stage1."
                + "Stage1EnemyPackageDescriptor");
            public static readonly Type Validator = FindType(
                "ShooterMover.ContentPackages.Enemies.Stage1."
                + "Stage1EnemyPackageValidator");
        }
    }
}
