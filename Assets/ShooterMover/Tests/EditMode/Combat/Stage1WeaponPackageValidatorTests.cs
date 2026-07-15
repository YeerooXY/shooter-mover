using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Content;
using ShooterMover.Contracts.Identity;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Combat
{
    /// <summary>
    /// ContentPackages compile into Unity's predefined Assembly-CSharp, which the
    /// asmdef-backed EditMode tests cannot reference statically. This narrow bridge
    /// exercises the real production types without adding an out-of-scope asmdef.
    /// </summary>
    public sealed class Stage1WeaponPackageValidatorTests
    {
        private const int AutomaticProjectile = 1;
        private const int SpreadProjectile = 2;
        private const int RocketAreaDetonation = 3;
        private const int ArcChain = 4;
        private const int RicochetProjectile = 5;

        private const int Damage = 1;
        private const int ProjectileSpeed = 2;
        private const int ProjectileLifetimeSeconds = 3;
        private const int SpreadDegrees = 4;
        private const int AreaRadius = 5;
        private const int EffectRange = 6;

        private static readonly StableId BlasterMachineGunId =
            StableId.Parse("weapon.blaster-machine-gun");
        private static readonly StableId ShotgunId =
            StableId.Parse("weapon.shotgun");
        private static readonly StableId RocketLauncherId =
            StableId.Parse("weapon.rocket-launcher");
        private static readonly StableId ArcGunId =
            StableId.Parse("weapon.arc-gun");
        private static readonly StableId RicochetGunId =
            StableId.Parse("weapon.ricochet-gun");

        [Test]
        public void DescriptorIdentity_AcceptsExactlyTheAmendedRosterAndDefault()
        {
            object result = Validate(AcceptedRoster());
            List<object> packages = GetObjectList(result, "Packages");
            List<StableId> acceptedIds = GetStaticEnumerable<StableId>(
                RuntimeTypes.Descriptor,
                "AcceptedWeaponIds");

            Assert.That(GetProperty<bool>(result, "IsValid"), Is.True, Canonical(result));
            Assert.That(GetObjectList(result, "Errors"), Is.Empty);
            Assert.That(packages, Has.Count.EqualTo(5));
            Assert.That(
                acceptedIds,
                Is.EqualTo(
                    new[]
                    {
                        BlasterMachineGunId,
                        ShotgunId,
                        RocketLauncherId,
                        ArcGunId,
                        RicochetGunId,
                    }));

            object defaultWeapon = InvokeInstance(result, "GetDefaultStartingWeapon");
            Assert.That(
                GetProperty<StableId>(defaultWeapon, "DefinitionId"),
                Is.EqualTo(BlasterMachineGunId));
            Assert.That(GetProperty<bool>(defaultWeapon, "IsDefaultStartingWeapon"), Is.True);

            foreach (object package in packages)
            {
                ContentReference reference =
                    (ContentReference)InvokeInstance(package, "CreateWeaponReference");
                Assert.That(
                    reference.DefinitionId,
                    Is.EqualTo(GetProperty<StableId>(package, "DefinitionId")));
                Assert.That(reference.ExpectedKind, Is.EqualTo(ContentDefinitionKind.Weapon));
                Assert.That(
                    reference.ExpectedVersion,
                    Is.EqualTo(ContentReference.SupportedDefinitionVersion));
            }

            List<ContentDefinitionDescriptor> registryInputs = GetRegistryInputs(result);
            List<ContentDefinitionDescriptor> catalog =
                new List<ContentDefinitionDescriptor>(registryInputs);
            catalog.AddRange(CreateSupportingDefinitions(registryInputs));
            GeneratedMachineRegistry registry = GeneratedMachineRegistry.Create(
                1,
                catalog,
                ContentValidationMode.Release);
            ContentVersion version = registry.ContentVersion;

            Assert.That(version.CatalogVersion, Is.EqualTo(1));
            Assert.That(version.DefinitionFingerprint, Does.StartWith("sha256:"));
            Assert.That(
                registry.Entries.Count(entry => entry.Kind == ContentDefinitionKind.Weapon),
                Is.EqualTo(5));
        }

        [Test]
        public void DuplicateIds_FailDeterministically()
        {
            object[] accepted = AcceptedRoster();
            object[] first =
            {
                accepted[0],
                accepted[0],
                accepted[2],
                accepted[3],
                accepted[4],
            };
            object[] second =
            {
                accepted[4],
                accepted[3],
                accepted[0],
                accepted[2],
                accepted[0],
            };

            object firstResult = Validate(first);
            object secondResult = Validate(second);

            Assert.That(GetProperty<bool>(firstResult, "IsValid"), Is.False);
            Assert.That(Canonical(firstResult), Is.EqualTo(Canonical(secondResult)));
            Assert.That(
                HasError(firstResult, "DuplicatePackageId", BlasterMachineGunId),
                Is.True);
            Assert.That(HasError(firstResult, "MissingPackage", ShotgunId), Is.True);
            Assert.That(
                HasError(firstResult, "DuplicateDefaultStartingWeapon"),
                Is.True);
            Assert.Throws<InvalidOperationException>(
                () => InvokeInstance(firstResult, "GetRegistryInputs"));
        }

        [Test]
        public void NumericEmpowerment_AllowsOnlyExistingNumericValuesToChange()
        {
            object result = Validate(AcceptedRoster());
            Assert.That(GetProperty<bool>(result, "IsValid"), Is.True, Canonical(result));

            object package = GetPackage(result, BlasterMachineGunId);
            object normal = GetProperty<object>(package, "NormalFire");
            object empowered = GetProperty<object>(package, "EmpoweredFire");
            WeaponRuntimeProfile normalRuntime =
                GetProperty<WeaponRuntimeProfile>(normal, "RuntimeProfile");
            WeaponRuntimeProfile empoweredRuntime =
                GetProperty<WeaponRuntimeProfile>(empowered, "RuntimeProfile");

            Assert.That(empoweredRuntime.CadenceSeconds, Is.Not.EqualTo(normalRuntime.CadenceSeconds));
            Assert.That(empoweredRuntime.RecoverySeconds, Is.Not.EqualTo(normalRuntime.RecoverySeconds));
            Assert.That(
                GetCoefficientValue(empowered, Damage),
                Is.Not.EqualTo(GetCoefficientValue(normal, Damage)));
            Assert.That(
                GetProperty<object>(normal, "Topology").ToString(),
                Is.EqualTo(GetProperty<object>(empowered, "Topology").ToString()));
            Assert.That(
                Enumerable.Range(0, normalRuntime.BehaviorModuleCount)
                    .Select(normalRuntime.GetBehaviorModuleId),
                Is.EqualTo(
                    Enumerable.Range(0, empoweredRuntime.BehaviorModuleCount)
                        .Select(empoweredRuntime.GetBehaviorModuleId)));
            Assert.That(HasError(result, "EmpoweredBehaviorTopologyChanged"), Is.False);
            Assert.That(HasError(result, "EmpoweredBehaviorModulesChanged"), Is.False);
            Assert.That(HasError(result, "EmpoweredCoefficientSetChanged"), Is.False);
        }

        [Test]
        public void ArcTargetCap_RejectsMoreThanThreeAdditionalTargets()
        {
            object[] roster = AcceptedRoster();
            object source = roster[3];
            object topology = CreateTopology(ArcChain, 4, 0, 0, false);
            roster[3] = WithTopologies(source, topology, topology);

            object result = Validate(roster);

            Assert.That(GetProperty<bool>(result, "IsValid"), Is.False);
            Assert.That(
                HasError(result, "ArcAdditionalTargetLimitExceeded", ArcGunId),
                Is.True,
                Canonical(result));
        }

        [Test]
        public void RicochetBounceCap_RejectsMoreThanTwoWallBounces()
        {
            object[] roster = AcceptedRoster();
            object source = roster[4];
            object topology = CreateTopology(RicochetProjectile, 0, 3, 0, false);
            roster[4] = WithTopologies(source, topology, topology);

            object result = Validate(roster);

            Assert.That(GetProperty<bool>(result, "IsValid"), Is.False);
            Assert.That(
                HasError(result, "RicochetWallBounceLimitExceeded", RicochetGunId),
                Is.True,
                Canonical(result));
        }

        [Test]
        public void RocketTopology_RejectsFragmentationAndSecondDetonation()
        {
            object[] fragmentationRoster = AcceptedRoster();
            object fragmentationTopology =
                CreateTopology(RocketAreaDetonation, 0, 0, 1, true);
            fragmentationRoster[2] = WithTopologies(
                fragmentationRoster[2],
                fragmentationTopology,
                fragmentationTopology);

            object fragmentationResult = Validate(fragmentationRoster);
            Assert.That(
                HasError(
                    fragmentationResult,
                    "RocketFragmentationNotSupported",
                    RocketLauncherId),
                Is.True,
                Canonical(fragmentationResult));

            object[] secondDetonationRoster = AcceptedRoster();
            object secondDetonationTopology =
                CreateTopology(RocketAreaDetonation, 0, 0, 2, false);
            secondDetonationRoster[2] = WithTopologies(
                secondDetonationRoster[2],
                secondDetonationTopology,
                secondDetonationTopology);

            object secondDetonationResult = Validate(secondDetonationRoster);
            Assert.That(
                HasError(
                    secondDetonationResult,
                    "RocketSecondDetonationNotSupported",
                    RocketLauncherId),
                Is.True,
                Canonical(secondDetonationResult));
            Assert.That(
                HasError(
                    secondDetonationResult,
                    "RocketDetonationCountMismatch",
                    RocketLauncherId),
                Is.True);
        }

        [Test]
        public void UnlimitedNormalFire_RejectsConsumableAmmunition()
        {
            Assert.That(WeaponRuntimeProfile.NormalFireConsumesConsumable, Is.False);

            object[] roster = AcceptedRoster();
            object package = roster[0];
            object normal = GetProperty<object>(package, "NormalFire");
            object empowered = GetProperty<object>(package, "EmpoweredFire");
            object consumableNormal = CloneFireProfile(
                normal,
                null,
                null,
                true,
                null);
            roster[0] = WithFireProfiles(package, consumableNormal, empowered);

            object result = Validate(roster);

            Assert.That(GetProperty<bool>(result, "IsValid"), Is.False);
            Assert.That(
                HasError(result, "ConsumableNormalAmmunition", BlasterMachineGunId),
                Is.True,
                Canonical(result));
        }

        [Test]
        public void BehaviorChangingEmpowerment_FailsDeterministically()
        {
            object[] first = AcceptedRoster();
            object source = first[1];
            object normal = GetProperty<object>(source, "NormalFire");
            object empowered = GetProperty<object>(source, "EmpoweredFire");
            object changedTopology =
                CreateTopology(AutomaticProjectile, 0, 0, 0, false);
            object changedEmpowered = CloneFireProfile(
                empowered,
                null,
                changedTopology,
                null,
                null);
            first[1] = WithFireProfiles(source, normal, changedEmpowered);

            object[] second =
            {
                first[4],
                first[1],
                first[3],
                first[0],
                first[2],
            };

            object firstResult = Validate(first);
            object secondResult = Validate(second);

            Assert.That(Canonical(firstResult), Is.EqualTo(Canonical(secondResult)));
            Assert.That(
                HasError(
                    firstResult,
                    "EmpoweredBehaviorTopologyChanged",
                    ShotgunId),
                Is.True);
            Assert.That(
                HasError(firstResult, "BehaviorKindMismatch", ShotgunId),
                Is.True);
        }

        [Test]
        public void UnknownAndMalformedDescriptors_FailDeterministically()
        {
            object[] accepted = AcceptedRoster();
            object shotgun = accepted[1];
            ContentDefinitionDescriptor original =
                GetProperty<ContentDefinitionDescriptor>(shotgun, "ContentDefinition");
            StableId unknownId = StableId.Parse("weapon.unapproved-stage1-fixture");
            ContentDefinitionDescriptor unknownContent =
                ContentDefinitionDescriptor.Create(
                    unknownId,
                    ContentDefinitionKind.Weapon,
                    ContentReference.SupportedDefinitionVersion,
                    StableId.Parse("provenance.unapproved-stage1-fixture"),
                    false,
                    original.References);
            accepted[1] = CreateDescriptor(
                1,
                unknownContent,
                false,
                GetProperty<object>(shotgun, "NormalFire"),
                GetProperty<object>(shotgun, "EmpoweredFire"));

            object[] first =
            {
                accepted[0],
                null,
                accepted[1],
                accepted[2],
                accepted[3],
                accepted[4],
            };
            object[] second =
            {
                accepted[4],
                accepted[2],
                accepted[1],
                null,
                accepted[0],
                accepted[3],
            };

            object firstResult = Validate(first);
            object secondResult = Validate(second);

            Assert.That(Canonical(firstResult), Is.EqualTo(Canonical(secondResult)));
            Assert.That(HasError(firstResult, "NullDescriptor"), Is.True);
            Assert.That(HasError(firstResult, "UnknownPackageId", unknownId), Is.True);
            Assert.That(HasError(firstResult, "MissingPackage", ShotgunId), Is.True);
        }

        private static object[] AcceptedRoster()
        {
            return new[]
            {
                CreatePackage(
                    BlasterMachineGunId,
                    AutomaticProjectile,
                    0,
                    0,
                    0,
                    false,
                    true,
                    StableId.Parse("module.weapon-automatic-projectile"),
                    new[]
                    {
                        new CoefficientFixture(Damage, 10d),
                        new CoefficientFixture(ProjectileSpeed, 20d),
                    },
                    new[]
                    {
                        new CoefficientFixture(Damage, 15d),
                        new CoefficientFixture(ProjectileSpeed, 24d),
                    }),
                CreatePackage(
                    ShotgunId,
                    SpreadProjectile,
                    0,
                    0,
                    0,
                    false,
                    false,
                    StableId.Parse("module.weapon-spread-projectile"),
                    new[]
                    {
                        new CoefficientFixture(Damage, 5d),
                        new CoefficientFixture(ProjectileSpeed, 16d),
                        new CoefficientFixture(SpreadDegrees, 12d),
                    },
                    new[]
                    {
                        new CoefficientFixture(Damage, 7d),
                        new CoefficientFixture(ProjectileSpeed, 18d),
                        new CoefficientFixture(SpreadDegrees, 9d),
                    }),
                CreatePackage(
                    RocketLauncherId,
                    RocketAreaDetonation,
                    0,
                    0,
                    1,
                    false,
                    false,
                    StableId.Parse("module.weapon-rocket-area-detonation"),
                    new[]
                    {
                        new CoefficientFixture(Damage, 30d),
                        new CoefficientFixture(ProjectileSpeed, 8d),
                        new CoefficientFixture(AreaRadius, 2d),
                    },
                    new[]
                    {
                        new CoefficientFixture(Damage, 42d),
                        new CoefficientFixture(ProjectileSpeed, 10d),
                        new CoefficientFixture(AreaRadius, 2.5d),
                    }),
                CreatePackage(
                    ArcGunId,
                    ArcChain,
                    3,
                    0,
                    0,
                    false,
                    false,
                    StableId.Parse("module.weapon-arc-chain"),
                    new[]
                    {
                        new CoefficientFixture(Damage, 12d),
                        new CoefficientFixture(EffectRange, 6d),
                    },
                    new[]
                    {
                        new CoefficientFixture(Damage, 16d),
                        new CoefficientFixture(EffectRange, 7d),
                    }),
                CreatePackage(
                    RicochetGunId,
                    RicochetProjectile,
                    0,
                    2,
                    0,
                    false,
                    false,
                    StableId.Parse("module.weapon-ricochet-projectile"),
                    new[]
                    {
                        new CoefficientFixture(Damage, 9d),
                        new CoefficientFixture(ProjectileSpeed, 15d),
                        new CoefficientFixture(ProjectileLifetimeSeconds, 4d),
                    },
                    new[]
                    {
                        new CoefficientFixture(Damage, 13d),
                        new CoefficientFixture(ProjectileSpeed, 18d),
                        new CoefficientFixture(ProjectileLifetimeSeconds, 5d),
                    }),
            };
        }

        private static object CreatePackage(
            StableId weaponId,
            int behaviorKind,
            int additionalTargetCount,
            int wallBounceCount,
            int detonationCount,
            bool hasFragmentation,
            bool isDefault,
            StableId moduleId,
            CoefficientFixture[] normalCoefficients,
            CoefficientFixture[] empoweredCoefficients)
        {
            ContentReference moduleReference = ContentReference.Create(
                moduleId,
                ContentDefinitionKind.SharedModule,
                ContentReference.SupportedDefinitionVersion);
            ContentDefinitionDescriptor content = ContentDefinitionDescriptor.Create(
                weaponId,
                ContentDefinitionKind.Weapon,
                ContentReference.SupportedDefinitionVersion,
                StableId.Create("provenance", weaponId.Value),
                false,
                moduleReference);
            object topology = CreateTopology(
                behaviorKind,
                additionalTargetCount,
                wallBounceCount,
                detonationCount,
                hasFragmentation);
            WeaponRuntimeProfile normalRuntime =
                CreateRuntimeProfile(weaponId, moduleId, false);
            WeaponRuntimeProfile empoweredRuntime =
                CreateRuntimeProfile(weaponId, moduleId, true);
            object normal = CreateFireProfile(
                normalRuntime,
                topology,
                false,
                CreateCoefficients(normalCoefficients));
            object empowered = CreateFireProfile(
                empoweredRuntime,
                topology,
                false,
                CreateCoefficients(empoweredCoefficients));

            return CreateDescriptor(1, content, isDefault, normal, empowered);
        }

        private static WeaponRuntimeProfile CreateRuntimeProfile(
            StableId weaponId,
            StableId moduleId,
            bool empowered)
        {
            StableId profileId = StableId.Create(
                "weapon-profile",
                weaponId.Value + (empowered ? "-empowered" : "-normal"));
            StableId[] modules = { moduleId };
            return WeaponRuntimeProfile.Create(
                WeaponRuntimeProfile.CurrentProfileVersion,
                profileId,
                empowered ? 0.25d : 0.5d,
                1,
                0d,
                empowered ? 0.1d : 0.2d,
                WeaponCycleMode.None,
                0d,
                0d,
                0d,
                0d,
                true,
                empowered ? 12d : 10d,
                empowered ? 3d : 2d,
                0d,
                modules,
                modules,
                empowered ? 2 : 1);
        }

        private static object[] CreateCoefficients(
            IEnumerable<CoefficientFixture> fixtures)
        {
            return fixtures.Select(
                    fixture => InvokeStatic(
                        RuntimeTypes.Coefficient,
                        "Create",
                        Enum.ToObject(RuntimeTypes.CoefficientKind, fixture.Kind),
                        fixture.Value))
                .ToArray();
        }

        private static object CreateTopology(
            int behaviorKind,
            int additionalTargetCount,
            int wallBounceCount,
            int detonationCount,
            bool hasFragmentation)
        {
            return InvokeStatic(
                RuntimeTypes.Topology,
                "Create",
                Enum.ToObject(RuntimeTypes.BehaviorKind, behaviorKind),
                additionalTargetCount,
                wallBounceCount,
                detonationCount,
                hasFragmentation);
        }

        private static object CreateFireProfile(
            WeaponRuntimeProfile runtimeProfile,
            object topology,
            bool consumesConsumableAmmunition,
            IEnumerable<object> coefficients)
        {
            object[] copied = coefficients == null ? null : coefficients.ToArray();
            Array runtimeArray = null;
            if (copied != null)
            {
                runtimeArray = Array.CreateInstance(RuntimeTypes.Coefficient, copied.Length);
                for (int index = 0; index < copied.Length; index++)
                {
                    runtimeArray.SetValue(copied[index], index);
                }
            }

            return InvokeStatic(
                RuntimeTypes.FireProfile,
                "Create",
                runtimeProfile,
                topology,
                consumesConsumableAmmunition,
                runtimeArray);
        }

        private static object CreateDescriptor(
            int descriptorVersion,
            ContentDefinitionDescriptor contentDefinition,
            bool isDefaultStartingWeapon,
            object normalFire,
            object empoweredFire)
        {
            return InvokeStatic(
                RuntimeTypes.Descriptor,
                "Create",
                descriptorVersion,
                contentDefinition,
                isDefaultStartingWeapon,
                normalFire,
                empoweredFire);
        }

        private static object WithTopologies(
            object source,
            object normalTopology,
            object empoweredTopology)
        {
            object normal = CloneFireProfile(
                GetProperty<object>(source, "NormalFire"),
                null,
                normalTopology,
                null,
                null);
            object empowered = CloneFireProfile(
                GetProperty<object>(source, "EmpoweredFire"),
                null,
                empoweredTopology,
                null,
                null);
            return WithFireProfiles(source, normal, empowered);
        }

        private static object WithFireProfiles(
            object source,
            object normal,
            object empowered)
        {
            return CreateDescriptor(
                GetProperty<int>(source, "DescriptorVersion"),
                GetProperty<ContentDefinitionDescriptor>(source, "ContentDefinition"),
                GetProperty<bool>(source, "IsDefaultStartingWeapon"),
                normal,
                empowered);
        }

        private static object CloneFireProfile(
            object source,
            WeaponRuntimeProfile runtimeOverride,
            object topologyOverride,
            bool? ammoOverride,
            object[] coefficientOverride)
        {
            return CreateFireProfile(
                runtimeOverride ?? GetProperty<WeaponRuntimeProfile>(source, "RuntimeProfile"),
                topologyOverride ?? GetProperty<object>(source, "Topology"),
                ammoOverride ?? GetProperty<bool>(source, "ConsumesConsumableAmmunition"),
                coefficientOverride
                    ?? GetObjectList(source, "NumericCoefficients").ToArray());
        }

        private static object Validate(IEnumerable<object> descriptors)
        {
            List<object> copied = descriptors.ToList();
            Array runtimeArray = Array.CreateInstance(RuntimeTypes.Descriptor, copied.Count);
            for (int index = 0; index < copied.Count; index++)
            {
                runtimeArray.SetValue(copied[index], index);
            }

            return InvokeStatic(RuntimeTypes.Validator, "Validate", runtimeArray);
        }

        private static object GetPackage(object result, StableId packageId)
        {
            object[] arguments = { packageId, null };
            bool found = (bool)InvokeInstanceWithArguments(
                result,
                "TryGetPackage",
                arguments);
            Assert.That(found, Is.True);
            return arguments[1];
        }

        private static double GetCoefficientValue(object fireProfile, int kind)
        {
            return GetObjectList(fireProfile, "NumericCoefficients")
                .Where(coefficient => GetEnumInt(coefficient, "Kind") == kind)
                .Select(coefficient => GetProperty<double>(coefficient, "Value"))
                .Single();
        }

        private static string Canonical(object result)
        {
            return (string)InvokeInstance(result, "ToCanonicalString");
        }

        private static List<ContentDefinitionDescriptor> GetRegistryInputs(object result)
        {
            return ((IEnumerable)InvokeInstance(result, "GetRegistryInputs"))
                .Cast<ContentDefinitionDescriptor>()
                .ToList();
        }

        private static bool HasError(
            object result,
            string codeName,
            StableId packageId = null)
        {
            return GetObjectList(result, "Errors").Any(
                error => string.Equals(
                        GetProperty<object>(error, "Code").ToString(),
                        codeName,
                        StringComparison.Ordinal)
                    && (packageId == null
                        || Equals(GetProperty<StableId>(error, "PackageId"), packageId)));
        }

        private static List<object> GetObjectList(object instance, string propertyName)
        {
            object value = GetProperty<object>(instance, propertyName);
            if (value == null)
            {
                return null;
            }

            return ((IEnumerable)value).Cast<object>().ToList();
        }

        private static List<T> GetStaticEnumerable<T>(Type type, string propertyName)
        {
            PropertyInfo property = RequireProperty(
                type,
                propertyName,
                BindingFlags.Public | BindingFlags.Static);
            return ((IEnumerable)property.GetValue(null, null)).Cast<T>().ToList();
        }

        private static T GetProperty<T>(object instance, string propertyName)
        {
            PropertyInfo property = RequireProperty(
                instance.GetType(),
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
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
            return InvokeInstanceWithArguments(instance, methodName, arguments);
        }

        private static object InvokeInstanceWithArguments(
            object instance,
            string methodName,
            object[] arguments)
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
                .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal))
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
                    + argumentCount
                    + " parameters, found "
                    + matches.Length
                    + ".");
            }

            return matches[0];
        }

        private static PropertyInfo RequireProperty(
            Type type,
            string propertyName,
            BindingFlags flags)
        {
            PropertyInfo property = type.GetProperty(propertyName, flags);
            if (property == null)
            {
                throw new InvalidOperationException(
                    "Missing property " + type.FullName + "." + propertyName + ".");
            }

            return property;
        }

        private static IEnumerable<ContentDefinitionDescriptor>
            CreateSupportingDefinitions(
                IEnumerable<ContentDefinitionDescriptor> weaponDefinitions)
        {
            Dictionary<StableId, ContentReference> uniqueReferences =
                new Dictionary<StableId, ContentReference>();
            foreach (ContentDefinitionDescriptor weapon in weaponDefinitions)
            {
                for (int index = 0; index < weapon.References.Count; index++)
                {
                    ContentReference reference = weapon.References[index];
                    uniqueReferences[reference.DefinitionId] = reference;
                }
            }

            return uniqueReferences.Values.Select(
                reference => ContentDefinitionDescriptor.Create(
                    reference.DefinitionId,
                    reference.ExpectedKind,
                    reference.ExpectedVersion,
                    StableId.Create(
                        "provenance",
                        reference.DefinitionId.Value + "-fixture"),
                    false,
                    Array.Empty<ContentReference>()));
        }

        private sealed class CoefficientFixture
        {
            public CoefficientFixture(int kind, double value)
            {
                Kind = kind;
                Value = value;
            }

            public int Kind { get; }

            public double Value { get; }
        }

        private static class RuntimeTypes
        {
            public static readonly Type Descriptor = Find(
                "ShooterMover.ContentPackages.Weapons.Stage1.Stage1WeaponPackageDescriptor");
            public static readonly Type Validator = Find(
                "ShooterMover.ContentPackages.Weapons.Stage1.Stage1WeaponPackageValidator");
            public static readonly Type FireProfile = Find(
                "ShooterMover.ContentPackages.Weapons.Stage1.Stage1WeaponFireProfile");
            public static readonly Type Topology = Find(
                "ShooterMover.ContentPackages.Weapons.Stage1.Stage1WeaponBehaviorTopology");
            public static readonly Type Coefficient = Find(
                "ShooterMover.ContentPackages.Weapons.Stage1.Stage1WeaponNumericCoefficient");
            public static readonly Type BehaviorKind = Find(
                "ShooterMover.ContentPackages.Weapons.Stage1.Stage1WeaponBehaviorKind");
            public static readonly Type CoefficientKind = Find(
                "ShooterMover.ContentPackages.Weapons.Stage1.Stage1WeaponNumericCoefficientKind");

            private static Type Find(string fullName)
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
                    "Production type was not loaded from the Unity project: " + fullName + ".");
            }
        }
    }
}
