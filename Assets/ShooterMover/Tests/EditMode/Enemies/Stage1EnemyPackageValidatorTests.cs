using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Contracts.Content;
using ShooterMover.Contracts.Encounters;
using ShooterMover.Contracts.Identity;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Enemies
{
    /// <summary>
    /// ContentPackages currently compile into Unity's predefined Assembly-CSharp,
    /// which an asmdef-backed test assembly cannot reference statically. This narrow
    /// bridge exercises the real production types without adding an out-of-scope
    /// assembly asset or copying package logic into the fixture.
    /// </summary>
    public sealed class Stage1EnemyPackageValidatorTests
    {
        private const int OrdinaryClassification = 1;
        private const int EliteClassification = 2;

        private const ulong DirectPursuit = 1UL << 0;
        private const ulong OrdinaryContactDamage = 1UL << 1;
        private const ulong DisposableImpactAttack = 1UL << 2;
        private const ulong MobilePositioning = 1UL << 3;
        private const ulong StationaryPositioning = 1UL << 4;
        private const ulong BlasterProjectile = 1UL << 5;
        private const ulong FourBlasterOrigins = 1UL << 6;
        private const ulong MildBoundedSpread = 1UL << 7;
        private const ulong SafeRecoveryWindow = 1UL << 8;
        private const ulong LineOfFireTelegraph = 1UL << 9;
        private const ulong PhaseTransition = 1UL << 16;
        private const ulong DenialPulse = 1UL << 17;
        private const ulong MortarAttack = 1UL << 18;
        private const ulong ReinforcementCall = 1UL << 19;
        private const ulong Teleport = 1UL << 20;
        private const ulong ComplexRepositioning = 1UL << 21;
        private const ulong BulletHell = 1UL << 22;

        private static readonly StableId PursuerDroneId =
            StableId.Parse("enemy.pursuer-drone");
        private static readonly StableId RamDroidId =
            StableId.Parse("enemy.ram-droid");
        private static readonly StableId MobileBlasterDroidId =
            StableId.Parse("enemy.mobile-blaster-droid");
        private static readonly StableId BlasterTurretId =
            StableId.Parse("enemy.blaster-turret");
        private static readonly StableId FourBlasterEliteId =
            StableId.Parse("enemy.four-blaster-elite");
        private static readonly StableId BlasterMachineGunId =
            StableId.Parse("weapon.blaster-machine-gun");

        [Test]
        public void AcceptedRoster_ValidatesExactlyFourOrdinaryAndOneElite()
        {
            object result = Validate(AcceptedRoster());
            List<object> packages = GetObjectList(result, "Packages");
            List<StableId> declaredIds = GetStaticEnumerable<StableId>(
                RuntimeTypes.Descriptor,
                "AcceptedEnemyIds");

            Assert.That(GetProperty<bool>(result, "IsValid"), Is.True, Canonical(result));
            Assert.That(GetObjectList(result, "Errors"), Is.Empty);
            Assert.That(packages, Has.Count.EqualTo(5));
            Assert.That(
                packages.Count(package => GetEnumInt(package, "Classification") == OrdinaryClassification),
                Is.EqualTo(4));
            Assert.That(
                packages.Count(package => GetEnumInt(package, "Classification") == EliteClassification),
                Is.EqualTo(1));
            Assert.That(
                declaredIds,
                Is.EqualTo(
                    new[]
                    {
                        PursuerDroneId,
                        RamDroidId,
                        MobileBlasterDroidId,
                        BlasterTurretId,
                        FourBlasterEliteId,
                    }));
        }

        [Test]
        public void Validation_ShuffledInputProducesIdenticalCanonicalResult()
        {
            object[] ordered = AcceptedRoster();
            object[] shuffled =
            {
                ordered[4],
                ordered[1],
                ordered[3],
                ordered[0],
                ordered[2],
            };

            object first = Validate(ordered);
            object second = Validate(shuffled);

            Assert.That(Canonical(first), Is.EqualTo(Canonical(second)));
            Assert.That(
                GetRegistryInputs(first).Select(input => input.DefinitionId),
                Is.EqualTo(GetRegistryInputs(second).Select(input => input.DefinitionId)));
        }

        [Test]
        public void DuplicateIdAndMissingRole_AreReportedDeterministically()
        {
            object[] accepted = AcceptedRoster();
            object[] firstInput =
            {
                accepted[0],
                accepted[0],
                accepted[2],
                accepted[3],
                accepted[4],
            };
            object[] secondInput =
            {
                accepted[4],
                accepted[3],
                accepted[0],
                accepted[2],
                accepted[0],
            };

            object first = Validate(firstInput);
            object second = Validate(secondInput);

            Assert.That(GetProperty<bool>(first, "IsValid"), Is.False);
            Assert.That(Canonical(first), Is.EqualTo(Canonical(second)));
            Assert.That(HasError(first, "DuplicatePackageId", PursuerDroneId), Is.True);
            Assert.That(HasError(first, "MissingPackage", RamDroidId), Is.True);
            Assert.Throws<InvalidOperationException>(() => GetRegistryInputs(first));
        }

        [Test]
        public void UnknownId_DoesNotSatisfyTheAcceptedRoster()
        {
            object[] roster = AcceptedRoster();
            object template = roster[0];
            StableId unknownId = StableId.Parse("enemy.unapproved-sentinel");
            ContentReference movement = GetProperty<ContentReference>(template, "MovementReference");
            ContentReference attack = GetProperty<ContentReference>(template, "AttackReference");
            ContentReference telegraph = GetProperty<ContentReference>(template, "TelegraphReference");
            ContentDefinitionDescriptor unknownContent = ContentDefinitionDescriptor.Create(
                unknownId,
                ContentDefinitionKind.Enemy,
                ContentReference.SupportedDefinitionVersion,
                StableId.Parse("provenance.unapproved-sentinel"),
                false,
                movement,
                attack,
                telegraph);
            roster[0] = CreateDescriptor(
                1,
                unknownContent,
                OrdinaryClassification,
                CombatChannel.Contact,
                CombatWeightClass.Standard,
                movement,
                attack,
                telegraph,
                GetCapabilities(template));

            object result = Validate(roster);

            Assert.That(GetProperty<bool>(result, "IsValid"), Is.False);
            Assert.That(HasError(result, "UnknownPackageId", unknownId), Is.True);
            Assert.That(HasError(result, "MissingPackage", PursuerDroneId), Is.True);
        }

        [Test]
        public void OrdinaryAndEliteClassificationMismatches_AreRejected()
        {
            object[] roster = AcceptedRoster();
            roster[0] = WithClassification(roster[0], EliteClassification);
            roster[4] = WithClassification(roster[4], OrdinaryClassification);

            object result = Validate(roster);

            Assert.That(GetProperty<bool>(result, "IsValid"), Is.False);
            Assert.That(HasError(result, "ClassificationMismatch", PursuerDroneId), Is.True);
            Assert.That(HasError(result, "ClassificationMismatch", FourBlasterEliteId), Is.True);
        }

        [Test]
        public void MalformedDescriptor_ReportsStableStructuredFailures()
        {
            object[] roster = AcceptedRoster();
            ContentReference wrongAttack = ContentReference.Create(
                StableId.Parse("module.enemy-ordinary-contact"),
                ContentDefinitionKind.Weapon,
                ContentReference.SupportedDefinitionVersion);
            ContentReference unsupportedTelegraph = ContentReference.Create(
                StableId.Parse("module.enemy-contact-telegraph"),
                ContentDefinitionKind.SharedModule,
                2);
            ContentDefinitionDescriptor malformedContent = ContentDefinitionDescriptor.Create(
                PursuerDroneId,
                ContentDefinitionKind.Encounter,
                2,
                null,
                true,
                wrongAttack,
                unsupportedTelegraph);
            roster[0] = CreateDescriptor(
                2,
                malformedContent,
                99,
                CombatChannel.System,
                (CombatWeightClass)99,
                null,
                wrongAttack,
                unsupportedTelegraph,
                1UL << 63);

            object result = Validate(roster);

            Assert.That(GetProperty<bool>(result, "IsValid"), Is.False);
            Assert.That(HasError(result, "UnsupportedDescriptorVersion"), Is.True);
            Assert.That(HasError(result, "WrongDefinitionKind"), Is.True);
            Assert.That(HasError(result, "UnsupportedDefinitionVersion"), Is.True);
            Assert.That(HasError(result, "MissingProvenance"), Is.True);
            Assert.That(HasError(result, "PrototypeOnlyDefinition"), Is.True);
            Assert.That(HasError(result, "InvalidClassification"), Is.True);
            Assert.That(HasError(result, "InvalidCombatChannel"), Is.True);
            Assert.That(HasError(result, "InvalidWeightClass"), Is.True);
            Assert.That(HasError(result, "MissingMovementReference"), Is.True);
            Assert.That(HasError(result, "WrongReferenceKind"), Is.True);
            Assert.That(HasError(result, "UnsupportedReferenceVersion"), Is.True);
            Assert.That(HasError(result, "OutOfBoundRegistryReference"), Is.True);
            Assert.That(HasError(result, "UnknownCapability"), Is.True);

            int[] orderedCodes = GetObjectList(result, "Errors")
                .Select(error => GetEnumInt(error, "Code"))
                .ToArray();
            Assert.That(orderedCodes, Is.EqualTo(orderedCodes.OrderBy(code => code).ToArray()));
        }

        [Test]
        public void MissingTelegraphReference_IsRejected()
        {
            object[] roster = AcceptedRoster();
            object template = roster[0];
            ContentReference movement = GetProperty<ContentReference>(template, "MovementReference");
            ContentReference attack = GetProperty<ContentReference>(template, "AttackReference");
            ContentDefinitionDescriptor templateContent =
                GetProperty<ContentDefinitionDescriptor>(template, "ContentDefinition");
            ContentDefinitionDescriptor content = ContentDefinitionDescriptor.Create(
                PursuerDroneId,
                ContentDefinitionKind.Enemy,
                ContentReference.SupportedDefinitionVersion,
                templateContent.ProvenanceId,
                false,
                movement,
                attack);
            roster[0] = CreateDescriptor(
                1,
                content,
                OrdinaryClassification,
                CombatChannel.Contact,
                CombatWeightClass.Standard,
                movement,
                attack,
                null,
                GetCapabilities(template));

            object result = Validate(roster);

            Assert.That(GetProperty<bool>(result, "IsValid"), Is.False);
            Assert.That(HasError(result, "MissingTelegraphReference", PursuerDroneId), Is.True);
        }

        [TestCase(PhaseTransition)]
        [TestCase(DenialPulse)]
        [TestCase(MortarAttack)]
        [TestCase(ReinforcementCall)]
        [TestCase(Teleport)]
        [TestCase(ComplexRepositioning)]
        [TestCase(BulletHell)]
        public void FourBlasterElite_RejectsEveryForbiddenCapability(ulong forbiddenCapability)
        {
            object[] roster = AcceptedRoster();
            roster[4] = WithCapabilities(
                roster[4],
                GetCapabilities(roster[4]) | forbiddenCapability);

            object result = Validate(roster);

            Assert.That(GetProperty<bool>(result, "IsValid"), Is.False);
            Assert.That(
                HasError(result, "ForbiddenEliteCapability", FourBlasterEliteId),
                Is.True,
                Canonical(result));
        }

        [Test]
        public void OrdinaryRole_RejectsBehaviorOutsideItsAcceptedBoundary()
        {
            object[] roster = AcceptedRoster();
            roster[0] = WithCapabilities(
                roster[0],
                GetCapabilities(roster[0]) | BlasterProjectile);

            object result = Validate(roster);

            Assert.That(GetProperty<bool>(result, "IsValid"), Is.False);
            Assert.That(HasError(result, "OutOfBoundCapability", PursuerDroneId), Is.True);
        }

        [Test]
        public void AcceptedPackages_ProjectIntoEncounterAndGeneratedRegistryContracts()
        {
            object result = Validate(AcceptedRoster());
            Assert.That(GetProperty<bool>(result, "IsValid"), Is.True, Canonical(result));

            List<object> packages = GetObjectList(result, "Packages");
            for (int index = 0; index < packages.Count; index++)
            {
                object package = packages[index];
                ContentReference enemyReference = (ContentReference)InvokeInstance(
                    package,
                    "CreateEnemyReference");
                EncounterParticipantEntry entry = (EncounterParticipantEntry)InvokeInstance(
                    package,
                    "CreateEncounterParticipantEntry",
                    StableId.Parse("entry.fixture-" + index),
                    StableId.Parse("actor.fixture-" + index),
                    index);
                StableId definitionId = GetProperty<StableId>(package, "DefinitionId");

                Assert.That(enemyReference.DefinitionId, Is.EqualTo(definitionId));
                Assert.That(enemyReference.ExpectedKind, Is.EqualTo(ContentDefinitionKind.Enemy));
                Assert.That(entry.RoleId, Is.EqualTo(definitionId));
                Assert.That(entry.Order, Is.EqualTo(index));
            }

            List<ContentDefinitionDescriptor> registryInputs = GetRegistryInputs(result);
            List<ContentDefinitionDescriptor> catalog =
                new List<ContentDefinitionDescriptor>(registryInputs);
            catalog.AddRange(CreateSupportingDefinitions(registryInputs));

            GeneratedMachineRegistry registry = GeneratedMachineRegistry.Create(
                1,
                catalog,
                ContentValidationMode.Release);
            GeneratedRegistryReviewSnapshot review =
                GeneratedRegistryReviewSnapshot.Create(registry);
            ContentVersion contentVersion = registry.ContentVersion;

            Assert.That(contentVersion.CatalogVersion, Is.EqualTo(1));
            Assert.That(contentVersion.DefinitionFingerprint, Does.StartWith("sha256:"));
            Assert.That(registry.RegistryFingerprint, Does.StartWith("sha256:"));
            Assert.That(review.SnapshotFingerprint, Does.StartWith("sha256:"));
            Assert.That(
                registry.Entries.Count(entry => entry.Kind == ContentDefinitionKind.Enemy),
                Is.EqualTo(5));
        }

        [Test]
        public void Descriptor_PublicStateIsImmutable()
        {
            PropertyInfo[] properties = RuntimeTypes.Descriptor.GetProperties(
                BindingFlags.Instance | BindingFlags.Public);
            FieldInfo[] fields = RuntimeTypes.Descriptor.GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(properties, Is.Not.Empty);
            Assert.That(properties.All(property => property.SetMethod == null), Is.True);
            Assert.That(fields, Is.Not.Empty);
            Assert.That(fields.All(field => field.IsInitOnly), Is.True);
        }

        private static object[] AcceptedRoster()
        {
            return new[]
            {
                CreateAcceptedPackage(PursuerDroneId),
                CreateAcceptedPackage(RamDroidId),
                CreateAcceptedPackage(MobileBlasterDroidId),
                CreateAcceptedPackage(BlasterTurretId),
                CreateAcceptedPackage(FourBlasterEliteId),
            };
        }

        private static object CreateAcceptedPackage(StableId packageId)
        {
            ContentReference movement;
            ContentReference attack;
            ContentReference telegraph;
            int classification;
            CombatChannel channel;
            CombatWeightClass weight;
            ulong capabilities;

            if (packageId.Equals(PursuerDroneId))
            {
                movement = SharedModule("module.enemy-direct-pursuit");
                attack = SharedModule("module.enemy-ordinary-contact");
                telegraph = SharedModule("module.enemy-contact-telegraph");
                classification = OrdinaryClassification;
                channel = CombatChannel.Contact;
                weight = CombatWeightClass.Standard;
                capabilities = DirectPursuit | OrdinaryContactDamage;
            }
            else if (packageId.Equals(RamDroidId))
            {
                movement = SharedModule("module.enemy-direct-pursuit");
                attack = SharedModule("module.enemy-disposable-impact");
                telegraph = SharedModule("module.enemy-impact-telegraph");
                classification = OrdinaryClassification;
                channel = CombatChannel.Contact;
                weight = CombatWeightClass.Light;
                capabilities = DirectPursuit | DisposableImpactAttack;
            }
            else if (packageId.Equals(MobileBlasterDroidId))
            {
                movement = SharedModule("module.enemy-mobile-positioning");
                attack = BlasterReference();
                telegraph = SharedModule("module.enemy-blaster-telegraph");
                classification = OrdinaryClassification;
                channel = CombatChannel.Kinetic;
                weight = CombatWeightClass.Standard;
                capabilities = MobilePositioning | BlasterProjectile | SafeRecoveryWindow;
            }
            else if (packageId.Equals(BlasterTurretId))
            {
                movement = SharedModule("module.enemy-stationary-positioning");
                attack = BlasterReference();
                telegraph = SharedModule("module.enemy-line-of-fire-telegraph");
                classification = OrdinaryClassification;
                channel = CombatChannel.Kinetic;
                weight = CombatWeightClass.Immovable;
                capabilities = StationaryPositioning
                    | BlasterProjectile
                    | SafeRecoveryWindow
                    | LineOfFireTelegraph;
            }
            else if (packageId.Equals(FourBlasterEliteId))
            {
                movement = SharedModule("module.enemy-simple-elite-positioning");
                attack = BlasterReference();
                telegraph = SharedModule("module.enemy-four-blaster-telegraph");
                classification = EliteClassification;
                channel = CombatChannel.Kinetic;
                weight = CombatWeightClass.Heavy;
                capabilities = BlasterProjectile
                    | FourBlasterOrigins
                    | MildBoundedSpread
                    | SafeRecoveryWindow;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(packageId), packageId, "Unknown fixture ID.");
            }

            ContentDefinitionDescriptor content = ContentDefinitionDescriptor.Create(
                packageId,
                ContentDefinitionKind.Enemy,
                ContentReference.SupportedDefinitionVersion,
                StableId.Create("provenance", packageId.Value + "-approved"),
                false,
                movement,
                attack,
                telegraph);
            return CreateDescriptor(
                1,
                content,
                classification,
                channel,
                weight,
                movement,
                attack,
                telegraph,
                capabilities);
        }

        private static object CreateDescriptor(
            int descriptorVersion,
            ContentDefinitionDescriptor contentDefinition,
            int classification,
            CombatChannel damageChannel,
            CombatWeightClass weightClass,
            ContentReference movementReference,
            ContentReference attackReference,
            ContentReference telegraphReference,
            ulong capabilities)
        {
            return InvokeStatic(
                RuntimeTypes.Descriptor,
                "Create",
                descriptorVersion,
                contentDefinition,
                Enum.ToObject(RuntimeTypes.Classification, classification),
                damageChannel,
                weightClass,
                movementReference,
                attackReference,
                telegraphReference,
                Enum.ToObject(RuntimeTypes.Capability, capabilities));
        }

        private static object WithClassification(object source, int classification)
        {
            return CreateDescriptor(
                GetProperty<int>(source, "DescriptorVersion"),
                GetProperty<ContentDefinitionDescriptor>(source, "ContentDefinition"),
                classification,
                GetProperty<CombatChannel>(source, "DamageChannel"),
                GetProperty<CombatWeightClass>(source, "WeightClass"),
                GetProperty<ContentReference>(source, "MovementReference"),
                GetProperty<ContentReference>(source, "AttackReference"),
                GetProperty<ContentReference>(source, "TelegraphReference"),
                GetCapabilities(source));
        }

        private static object WithCapabilities(object source, ulong capabilities)
        {
            return CreateDescriptor(
                GetProperty<int>(source, "DescriptorVersion"),
                GetProperty<ContentDefinitionDescriptor>(source, "ContentDefinition"),
                GetEnumInt(source, "Classification"),
                GetProperty<CombatChannel>(source, "DamageChannel"),
                GetProperty<CombatWeightClass>(source, "WeightClass"),
                GetProperty<ContentReference>(source, "MovementReference"),
                GetProperty<ContentReference>(source, "AttackReference"),
                GetProperty<ContentReference>(source, "TelegraphReference"),
                capabilities);
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

        private static bool HasError(object result, string codeName, StableId packageId = null)
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
            return ((IEnumerable)GetProperty<object>(instance, propertyName))
                .Cast<object>()
                .ToList();
        }

        private static List<T> GetStaticEnumerable<T>(Type type, string propertyName)
        {
            PropertyInfo property = RequireProperty(type, propertyName, BindingFlags.Public | BindingFlags.Static);
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

        private static ulong GetCapabilities(object descriptor)
        {
            return Convert.ToUInt64(GetProperty<object>(descriptor, "Capabilities"));
        }

        private static object InvokeStatic(Type type, string methodName, params object[] arguments)
        {
            MethodInfo method = RequireMethod(
                type,
                methodName,
                BindingFlags.Public | BindingFlags.Static,
                arguments.Length);
            return Invoke(method, null, arguments);
        }

        private static object InvokeInstance(object instance, string methodName, params object[] arguments)
        {
            MethodInfo method = RequireMethod(
                instance.GetType(),
                methodName,
                BindingFlags.Public | BindingFlags.Instance,
                arguments.Length);
            return Invoke(method, instance, arguments);
        }

        private static object Invoke(MethodInfo method, object instance, object[] arguments)
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

        private static ContentReference SharedModule(string id)
        {
            return ContentReference.Create(
                StableId.Parse(id),
                ContentDefinitionKind.SharedModule,
                ContentReference.SupportedDefinitionVersion);
        }

        private static ContentReference BlasterReference()
        {
            return ContentReference.Create(
                BlasterMachineGunId,
                ContentDefinitionKind.Weapon,
                ContentReference.SupportedDefinitionVersion);
        }

        private static IEnumerable<ContentDefinitionDescriptor> CreateSupportingDefinitions(
            IEnumerable<ContentDefinitionDescriptor> enemyDefinitions)
        {
            Dictionary<StableId, ContentReference> uniqueReferences =
                new Dictionary<StableId, ContentReference>();
            foreach (ContentDefinitionDescriptor enemy in enemyDefinitions)
            {
                for (int index = 0; index < enemy.References.Count; index++)
                {
                    ContentReference reference = enemy.References[index];
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

            List<ContentDefinitionDescriptor> definitions =
                new List<ContentDefinitionDescriptor>();
            foreach (ContentReference reference in uniqueReferences.Values)
            {
                definitions.Add(
                    ContentDefinitionDescriptor.Create(
                        reference.DefinitionId,
                        reference.ExpectedKind,
                        reference.ExpectedVersion,
                        StableId.Create(
                            "provenance",
                            reference.DefinitionId.Value + "-fixture"),
                        false,
                        Array.Empty<ContentReference>()));
            }

            return definitions;
        }

        private static class RuntimeTypes
        {
            public static readonly Type Descriptor = Find(
                "ShooterMover.ContentPackages.Enemies.Stage1.Stage1EnemyPackageDescriptor");
            public static readonly Type Validator = Find(
                "ShooterMover.ContentPackages.Enemies.Stage1.Stage1EnemyPackageValidator");
            public static readonly Type Classification = Find(
                "ShooterMover.ContentPackages.Enemies.Stage1.Stage1EnemyPackageClassification");
            public static readonly Type Capability = Find(
                "ShooterMover.ContentPackages.Enemies.Stage1.Stage1EnemyCapability");

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
