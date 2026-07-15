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
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Combat
{
    /// <summary>
    /// WP-009 validation matrix for the five concrete Stage 1 package inputs.
    /// The package implementation assembly is reached through reflection because the
    /// asmdef-backed EditMode tests cannot statically reference Assembly-CSharp.
    /// CS-011 remains the sole registry generator and writes only beneath temporary roots.
    /// </summary>
    public sealed class Stage1WeaponPackageRegistryTests
    {
        private const string FixtureRelativePath =
            "Assets/ShooterMover/Tests/EditMode/Combat/Fixtures/"
            + "stage1-weapon-registry-input-v1.json";
        private const string RegistryToolRelativePath =
            "tools/content-validation/content_registry.py";
        private const string DescriptorSchema =
            "urn:shooter-mover:schema:content-definition-descriptor-input:1";
        private const string FixtureSchema =
            "urn:shooter-mover:fixture:stage1-weapon-registry-input:1";

        private static readonly string[] GeneratedRelativePaths =
        {
            "Assets/ShooterMover/Generated/content-registry.json",
            "Assets/ShooterMover/Generated/content-review-snapshot.json",
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
        public void FiveRealPackages_GenerateByteStableRegistryFromShuffledInputs()
        {
            RegistryFixture fixture = LoadFixture();
            object[] roster = CreateProductionRoster(fixture);
            object validation = ValidateRoster(roster);

            Assert.That(GetProperty<bool>(validation, "IsValid"), Is.True, Canonical(validation));
            Assert.That(GetObjectList(validation, "Errors"), Is.Empty);
            Assert.That(GetObjectList(validation, "Packages"), Has.Count.EqualTo(5));
            Assert.That(WeaponRuntimeProfile.SupportedMountCount, Is.EqualTo(4));
            Assert.That(WeaponRuntimeProfile.NormalFireConsumesConsumable, Is.False);

            AssertFixtureMatchesProduction(fixture, roster);
            List<ContentDefinitionDescriptor> definitions =
                BuildRegistryDefinitions(fixture, validation);
            Assert.That(
                definitions.Count(definition => definition.Kind == ContentDefinitionKind.Weapon),
                Is.EqualTo(5));
            Assert.That(
                definitions.Select(definition => definition.DefinitionId).Distinct().Count(),
                Is.EqualTo(definitions.Count));

            using (TemporaryDirectory first = new TemporaryDirectory("wp009-first"))
            using (TemporaryDirectory second = new TemporaryDirectory("wp009-second"))
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
                Assert.That(CountOccurrences(machineJson, "\"definition_kind\": \"weapon\""),
                    Is.EqualTo(5));
                AssertWeaponOrder(machineJson);

                TestContext.Progress.WriteLine("stage1-weapon-package-registry: ok");
                TestContext.Progress.WriteLine(
                    "first_machine_sha256=" + firstRun.MachineChecksum);
                TestContext.Progress.WriteLine(
                    "second_machine_sha256=" + secondRun.MachineChecksum);
                TestContext.Progress.WriteLine(
                    "first_review_sha256=" + firstRun.ReviewChecksum);
                TestContext.Progress.WriteLine(
                    "second_review_sha256=" + secondRun.ReviewChecksum);
                TestContext.Progress.WriteLine(
                    "four_mount_invariant=" + WeaponRuntimeProfile.SupportedMountCount);
                TestContext.Progress.WriteLine("no_generated_output_write=true");
            }
        }

        [Test]
        public void DriftedTemporaryOutput_FailsWithVisibleChecksums()
        {
            RegistryFixture fixture = LoadFixture();
            object validation = ValidateRoster(CreateProductionRoster(fixture));
            Assert.That(GetProperty<bool>(validation, "IsValid"), Is.True, Canonical(validation));
            List<ContentDefinitionDescriptor> definitions =
                BuildRegistryDefinitions(fixture, validation);

            using (TemporaryDirectory temporary = new TemporaryDirectory("wp009-drift"))
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
        public void DuplicateUnknownModuleAndStaleFixtureInputs_FailVisibly()
        {
            RegistryFixture fixture = LoadFixture();
            object[] roster = CreateProductionRoster(fixture);
            object validation = ValidateRoster(roster);
            Assert.That(GetProperty<bool>(validation, "IsValid"), Is.True, Canonical(validation));
            List<ContentDefinitionDescriptor> definitions =
                BuildRegistryDefinitions(fixture, validation);

            using (TemporaryDirectory duplicateRoot =
                   new TemporaryDirectory("wp009-duplicate"))
            {
                List<ContentDefinitionDescriptor> duplicated =
                    new List<ContentDefinitionDescriptor>(definitions);
                duplicated.Add(
                    definitions.First(
                        definition => definition.Kind == ContentDefinitionKind.Weapon));
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
                    "duplicate_failure=" + FirstMatchingLine(
                        run.StandardError,
                        "code=duplicate-definition"));
            }

            using (TemporaryDirectory missingRoot =
                   new TemporaryDirectory("wp009-unknown-module"))
            {
                StableId omittedModuleId = StableId.Parse(fixture.packages[0].module_id);
                List<ContentDefinitionDescriptor> missingModule = definitions
                    .Where(definition => !definition.DefinitionId.Equals(omittedModuleId))
                    .ToList();
                string input = WriteCatalog(
                    missingRoot.Path,
                    missingModule,
                    Enumerable.Range(0, missingModule.Count).ToArray());
                RegistryRun run = RunRegistryTool(
                    "validate",
                    missingRoot.Path,
                    input,
                    fixture);

                Assert.That(run.ExitCode, Is.EqualTo(2), run.CombinedOutput);
                Assert.That(run.StandardError, Does.Contain("missing-definition"));
                Assert.That(run.StandardError, Does.Contain(omittedModuleId.ToString()));
                TestContext.Progress.WriteLine(
                    "unknown_module_failure=" + FirstMatchingLine(
                        run.StandardError,
                        "code=missing-definition"));
            }

            RegistryFixture staleFixture = LoadFixture();
            staleFixture.packages[0].module_id = "module.stale-stage1-input";
            InvalidOperationException stale = Assert.Throws<InvalidOperationException>(
                () => AssertFixtureMatchesProduction(staleFixture, roster));
            Assert.That(stale.Message, Does.StartWith("stale-package-input:"));
            TestContext.Progress.WriteLine(stale.Message);
        }

        [Test]
        public void BehaviorChangingEmpoweredProfile_FailsPackageValidation()
        {
            RegistryFixture fixture = LoadFixture();
            object[] roster = CreateProductionRoster(fixture);
            int shotgunIndex = Array.FindIndex(
                fixture.packages,
                package => string.Equals(
                    package.weapon_id,
                    "weapon.shotgun",
                    StringComparison.Ordinal));
            Assert.That(shotgunIndex, Is.GreaterThanOrEqualTo(0));

            object shotgun = roster[shotgunIndex];
            object normal = GetProperty<object>(shotgun, "NormalFire");
            object empowered = GetProperty<object>(shotgun, "EmpoweredFire");
            object changedTopology = InvokeStatic(
                RuntimeTypes.Topology,
                "Create",
                Enum.ToObject(RuntimeTypes.BehaviorKind, 1),
                0,
                0,
                0,
                false);
            object changedEmpowered = InvokeStatic(
                RuntimeTypes.FireProfile,
                "Create",
                GetProperty<WeaponRuntimeProfile>(empowered, "RuntimeProfile"),
                changedTopology,
                GetProperty<bool>(empowered, "ConsumesConsumableAmmunition"),
                ToRuntimeArray(
                    RuntimeTypes.Coefficient,
                    GetObjectList(empowered, "NumericCoefficients")));
            roster[shotgunIndex] = InvokeStatic(
                RuntimeTypes.Descriptor,
                "Create",
                GetProperty<int>(shotgun, "DescriptorVersion"),
                GetProperty<ContentDefinitionDescriptor>(shotgun, "ContentDefinition"),
                GetProperty<bool>(shotgun, "IsDefaultStartingWeapon"),
                normal,
                changedEmpowered);

            object result = ValidateRoster(roster);
            Assert.That(GetProperty<bool>(result, "IsValid"), Is.False);
            Assert.That(
                HasError(result, "EmpoweredBehaviorTopologyChanged"),
                Is.True,
                Canonical(result));
            Assert.That(
                HasError(result, "BehaviorKindMismatch"),
                Is.True,
                Canonical(result));
            TestContext.Progress.WriteLine(
                "invalid_empowered_profile_failure=empowered-behavior-topology-changed");
        }

        private static RegistryFixture LoadFixture()
        {
            string repositoryRoot = FindRepositoryRoot();
            string path = Path.Combine(repositoryRoot, FixtureRelativePath);
            Assert.That(File.Exists(path), Is.True, "Missing WP-009 registry input fixture.");

            string json = File.ReadAllText(path, Encoding.UTF8);
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
                fixture.packages.Select(package => package.weapon_id).Distinct().Count(),
                Is.EqualTo(5));
            Assert.That(
                fixture.packages.Select(package => package.module_id).Distinct().Count(),
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
                    "method",
                    StringComparison.Ordinal))
                {
                    roster[index] = InvokeStatic(
                        packageType,
                        package.descriptor_member);
                }
                else if (string.Equals(
                    package.descriptor_member_kind,
                    "property",
                    StringComparison.Ordinal))
                {
                    PropertyInfo property = packageType.GetProperty(
                        package.descriptor_member,
                        BindingFlags.Public | BindingFlags.Static);
                    if (property == null)
                    {
                        throw new InvalidOperationException(
                            "Missing package descriptor property "
                            + package.package_type
                            + "."
                            + package.descriptor_member
                            + ".");
                    }

                    roster[index] = property.GetValue(null, null);
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
                        "Package factory did not return a Stage1WeaponPackageDescriptor: "
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

            for (int index = 0; index < fixture.packages.Length; index++)
            {
                PackageFixture expected = fixture.packages[index];
                object descriptor;
                if (!descriptors.TryGetValue(expected.weapon_id, out descriptor))
                {
                    throw Stale(expected.weapon_id, "production descriptor is missing");
                }

                ContentDefinitionDescriptor content =
                    GetProperty<ContentDefinitionDescriptor>(
                        descriptor,
                        "ContentDefinition");
                if (!string.Equals(
                    content.DefinitionId.ToString(),
                    expected.weapon_id,
                    StringComparison.Ordinal))
                {
                    throw Stale(
                        expected.weapon_id,
                        "definition id changed to " + content.DefinitionId);
                }

                if (content.Kind != ContentDefinitionKind.Weapon
                    || content.DefinitionVersion
                    != ContentReference.SupportedDefinitionVersion
                    || content.ProvenanceId == null
                    || content.IsPrototypeOnly)
                {
                    throw Stale(expected.weapon_id, "content definition contract changed");
                }

                if (content.References.Count != 1)
                {
                    throw Stale(
                        expected.weapon_id,
                        "expected one shared-module reference; actual="
                        + content.References.Count.ToString(CultureInfo.InvariantCulture));
                }

                ContentReference reference = content.References[0];
                if (reference.ExpectedKind != ContentDefinitionKind.SharedModule
                    || reference.ExpectedVersion
                    != ContentReference.SupportedDefinitionVersion
                    || !string.Equals(
                        reference.DefinitionId.ToString(),
                        expected.module_id,
                        StringComparison.Ordinal))
                {
                    throw Stale(
                        expected.weapon_id,
                        "module reference changed; expected="
                        + expected.module_id
                        + ";actual="
                        + reference.DefinitionId);
                }

                bool actualDefault =
                    GetProperty<bool>(descriptor, "IsDefaultStartingWeapon");
                if (actualDefault != expected.is_default_starting_weapon)
                {
                    throw Stale(expected.weapon_id, "default-starting flag changed");
                }

                AssertProfileInvariant(
                    expected,
                    GetProperty<object>(descriptor, "NormalFire"),
                    false);
                AssertProfileInvariant(
                    expected,
                    GetProperty<object>(descriptor, "EmpoweredFire"),
                    true);
            }
        }

        private static void AssertProfileInvariant(
            PackageFixture package,
            object fireProfile,
            bool empowered)
        {
            if (fireProfile == null)
            {
                throw Stale(
                    package.weapon_id,
                    empowered ? "empowered profile is missing" : "normal profile is missing");
            }

            bool consumes =
                GetProperty<bool>(fireProfile, "ConsumesConsumableAmmunition");
            WeaponRuntimeProfile runtime =
                GetProperty<WeaponRuntimeProfile>(fireProfile, "RuntimeProfile");
            if (runtime == null
                || !runtime.HasIndependentPowerBank
                || runtime.PowerBankCapacityUnits <= 0d
                || runtime.EmpoweredCostUnits <= 0d
                || consumes)
            {
                throw Stale(
                    package.weapon_id,
                    (empowered ? "empowered" : "normal")
                    + " power/ammunition invariant changed");
            }

            if (runtime.BehaviorModuleCount != 1
                || !string.Equals(
                    runtime.GetBehaviorModuleId(0).ToString(),
                    package.module_id,
                    StringComparison.Ordinal))
            {
                throw Stale(
                    package.weapon_id,
                    (empowered ? "empowered" : "normal")
                    + " behavior-module list changed");
            }
        }

        private static InvalidOperationException Stale(
            string weaponId,
            string detail)
        {
            return new InvalidOperationException(
                "stale-package-input: weapon_id="
                + weaponId
                + ";detail="
                + detail);
        }

        private static List<ContentDefinitionDescriptor> BuildRegistryDefinitions(
            RegistryFixture fixture,
            object validation)
        {
            List<ContentDefinitionDescriptor> definitions =
                ((IEnumerable)InvokeInstance(validation, "GetRegistryInputs"))
                .Cast<ContentDefinitionDescriptor>()
                .ToList();

            for (int index = 0; index < fixture.packages.Length; index++)
            {
                PackageFixture package = fixture.packages[index];
                definitions.Add(
                    ContentDefinitionDescriptor.Create(
                        StableId.Parse(package.module_id),
                        ContentDefinitionKind.SharedModule,
                        ContentReference.SupportedDefinitionVersion,
                        StableId.Parse(package.module_provenance_id),
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
            arguments.AddRange(FindPython().PrefixArguments);
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
                FindPython().FileName,
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

        private static void AssertWeaponOrder(string machineJson)
        {
            string[] expected =
            {
                "weapon.arc-gun",
                "weapon.blaster-machine-gun",
                "weapon.ricochet-gun",
                "weapon.rocket-launcher",
                "weapon.shotgun",
            };
            int previous = -1;
            for (int index = 0; index < expected.Length; index++)
            {
                int current = machineJson.IndexOf(
                    "\"definition_id\": \"" + expected[index] + "\"",
                    StringComparison.Ordinal);
                Assert.That(current, Is.GreaterThan(previous), expected[index]);
                previous = current;
            }
        }

        private static bool HasError(object result, string codeName)
        {
            return GetObjectList(result, "Errors").Any(
                error => string.Equals(
                    GetProperty<object>(error, "Code").ToString(),
                    codeName,
                    StringComparison.Ordinal));
        }

        private static string Canonical(object result)
        {
            return (string)InvokeInstance(result, "ToCanonicalString");
        }

        private static List<object> GetObjectList(object instance, string propertyName)
        {
            object value = GetProperty<object>(instance, propertyName);
            return value == null
                ? null
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

        private static Array ToRuntimeArray(Type elementType, IReadOnlyList<object> values)
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
                "WP-009 requires Python 3 to invoke the accepted CS-011 generator. "
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
                        fileName + " did not exit within the WP-009 test timeout.");
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
            public string weapon_id;
            public string package_type;
            public string descriptor_member;
            public string descriptor_member_kind;
            public string module_id;
            public string module_provenance_id;
            public bool is_default_starting_weapon;
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
            public PythonCommand(string fileName, IEnumerable<string> prefixArguments)
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
                "ShooterMover.ContentPackages.Weapons.Stage1."
                + "Stage1WeaponPackageDescriptor");
            public static readonly Type Validator = FindType(
                "ShooterMover.ContentPackages.Weapons.Stage1."
                + "Stage1WeaponPackageValidator");
            public static readonly Type FireProfile = FindType(
                "ShooterMover.ContentPackages.Weapons.Stage1."
                + "Stage1WeaponFireProfile");
            public static readonly Type Topology = FindType(
                "ShooterMover.ContentPackages.Weapons.Stage1."
                + "Stage1WeaponBehaviorTopology");
            public static readonly Type Coefficient = FindType(
                "ShooterMover.ContentPackages.Weapons.Stage1."
                + "Stage1WeaponNumericCoefficient");
            public static readonly Type BehaviorKind = FindType(
                "ShooterMover.ContentPackages.Weapons.Stage1."
                + "Stage1WeaponBehaviorKind");
        }
    }
}
