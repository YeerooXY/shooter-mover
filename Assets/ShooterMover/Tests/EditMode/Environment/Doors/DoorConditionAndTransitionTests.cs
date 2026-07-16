#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Rooms;
using ShooterMover.Domain.Common;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.EnvironmentPackages.Doors
{
    public sealed class DoorConditionAndTransitionTests
    {
        private static readonly Type RequirementType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorConditionRequirement");
        private static readonly Type ConditionSetType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorConditionSet");
        private static readonly Type ContextType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorConditionEvaluationContext");
        private static readonly Type SnapshotType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorConditionFactSnapshot");
        private static readonly Type CompositionType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorConditionComposition");
        private static readonly Type TransitionDefinitionType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorTransitionDefinition");

        [Test]
        public void AllAndAnyComposition_EvaluateTypedFactsDeterministically()
        {
            RequirePackageTypes();
            object snapshot = Activator.CreateInstance(SnapshotType);
            StableId encounterId = StableId.Parse("encounter.door-test");
            StableId targetId = StableId.Parse("target.door-test");
            StableId currencyId = StableId.Parse("currency.money");
            StableId keyId = StableId.Parse("key.blue");

            Invoke(snapshot, "SetEncounterResolved", encounterId, true);
            Invoke(snapshot, "SetTargetDestroyed", targetId, false);
            Invoke(snapshot, "SetWalletAmount", currencyId, 50L);
            Invoke(snapshot, "SetKeyOwned", keyId, true);

            Array requirements = TypedArray(
                RequirementType,
                InvokeStatic(RequirementType, "Always"),
                InvokeStatic(RequirementType, "TriggerEntered"),
                InvokeStatic(RequirementType, "InteractionRequested"),
                InvokeStatic(
                    RequirementType,
                    "EncounterResolved",
                    encounterId),
                InvokeStatic(
                    RequirementType,
                    "TargetDestroyed",
                    targetId),
                InvokeStatic(
                    RequirementType,
                    "WalletAmountAtLeast",
                    currencyId,
                    25L),
                InvokeStatic(
                    RequirementType,
                    "KeyOwned",
                    keyId));

            object all = Activator.CreateInstance(
                ConditionSetType,
                Enum.Parse(CompositionType, "All"),
                requirements);
            object context = Activator.CreateInstance(
                ContextType,
                true,
                false,
                snapshot,
                snapshot,
                snapshot,
                snapshot);
            object allResult = Invoke(all, "Evaluate", context);
            object repeated = Invoke(all, "Evaluate", context);

            Assert.That(Read<bool>(allResult, "IsConfigurationValid"), Is.True);
            Assert.That(Read<bool>(allResult, "IsSatisfied"), Is.False);
            Assert.That(
                Read<string>(allResult, "DiagnosticFingerprint"),
                Is.EqualTo(Read<string>(repeated, "DiagnosticFingerprint")));
            Assert.That(
                ReadCollectionCount(allResult, "LeafResults"),
                Is.EqualTo(7));

            object any = Activator.CreateInstance(
                ConditionSetType,
                Enum.Parse(CompositionType, "Any"),
                requirements);
            object anyResult = Invoke(any, "Evaluate", context);
            Assert.That(Read<bool>(anyResult, "IsConfigurationValid"), Is.True);
            Assert.That(Read<bool>(anyResult, "IsSatisfied"), Is.True);
        }

        [Test]
        public void MissingReaderAndEmptySet_FailClosedWithDeterministicDiagnostics()
        {
            RequirePackageTypes();
            StableId targetId = StableId.Parse("target.missing-reader");
            Array targetOnly = TypedArray(
                RequirementType,
                InvokeStatic(
                    RequirementType,
                    "TargetDestroyed",
                    targetId));
            object all = Activator.CreateInstance(
                ConditionSetType,
                Enum.Parse(CompositionType, "All"),
                targetOnly);
            object noReaders = Activator.CreateInstance(
                ContextType,
                false,
                false,
                null,
                null,
                null,
                null);
            object missingReader = Invoke(all, "Evaluate", noReaders);

            Assert.That(
                Read<bool>(missingReader, "IsConfigurationValid"),
                Is.False);
            Assert.That(Read<bool>(missingReader, "IsSatisfied"), Is.False);
            object missingLeaf = ReadIndexed(missingReader, "LeafResults", 0);
            Assert.That(
                Read<object>(missingLeaf, "DiagnosticCode").ToString(),
                Is.EqualTo("MissingReader"));

            Array empty = Array.CreateInstance(RequirementType, 0);
            object emptySet = Activator.CreateInstance(
                ConditionSetType,
                Enum.Parse(CompositionType, "Any"),
                empty);
            object emptyResult = Invoke(emptySet, "Evaluate", noReaders);
            Assert.That(Read<bool>(emptyResult, "IsConfigurationValid"), Is.False);
            Assert.That(Read<bool>(emptyResult, "IsSatisfied"), Is.False);
            object emptyLeaf = ReadIndexed(emptyResult, "LeafResults", 0);
            Assert.That(
                Read<object>(emptyLeaf, "DiagnosticCode").ToString(),
                Is.EqualTo("EmptyConditionSet"));
        }

        [Test]
        public void WalletCondition_RejectsImpossibleNonPositiveThreshold()
        {
            RequirePackageTypes();
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => InvokeStatic(
                    RequirementType,
                    "WalletAmountAtLeast",
                    StableId.Parse("currency.money"),
                    0L));
            Assert.That(exception.InnerException, Is.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TransitionSockets_RequireExplicitCompatibleEndpoints()
        {
            RequirePackageTypes();
            RoomSocket source = new RoomSocket(
                new RoomProjectionIdentity(
                    StableId.Parse("room.alpha"),
                    StableId.Parse("projection.alpha")),
                StableId.Parse("socket.exit"),
                RoomSocketDirection.Outbound);
            RoomSocket destination = new RoomSocket(
                new RoomProjectionIdentity(
                    StableId.Parse("room.beta"),
                    StableId.Parse("projection.beta")),
                StableId.Parse("socket.entry"),
                RoomSocketDirection.Inbound);

            object valid = Activator.CreateInstance(
                TransitionDefinitionType,
                source,
                destination);
            object validResult = Read<object>(valid, "Validation");
            Assert.That(Read<bool>(validResult, "IsValid"), Is.True);

            object missing = Activator.CreateInstance(
                TransitionDefinitionType,
                null,
                destination);
            object missingResult = Read<object>(missing, "Validation");
            Assert.That(Read<bool>(missingResult, "IsValid"), Is.False);
            Assert.That(
                Read<object>(missingResult, "Code").ToString(),
                Is.EqualTo("MissingSourceSocket"));

            RoomSocket incompatible = new RoomSocket(
                new RoomProjectionIdentity(
                    StableId.Parse("room.gamma"),
                    StableId.Parse("projection.gamma")),
                StableId.Parse("socket.bad"),
                RoomSocketDirection.Outbound);
            object invalid = Activator.CreateInstance(
                TransitionDefinitionType,
                source,
                incompatible);
            object invalidResult = Read<object>(invalid, "Validation");
            Assert.That(Read<bool>(invalidResult, "IsValid"), Is.False);
            Assert.That(
                Read<object>(invalidResult, "Code").ToString(),
                Is.EqualTo("IncompatibleSockets"));
        }

        [Test]
        public void ProductionSources_AvoidGlobalDiscoveryAndForeignAuthorities()
        {
            string root = Path.Combine(
                UnityEngine.Application.dataPath,
                "ShooterMover/ContentPackages/Environment/Doors");
            Assert.That(Directory.Exists(root), Is.True);

            string source = string.Join(
                "\n",
                Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .Select(File.ReadAllText));

            string[] forbidden =
            {
                "FindFirstObjectByType",
                "FindObjectsByType",
                "FindObjectOfType",
                "GameObject.Find",
                "Stage1VisibleSliceController",
                "SceneManager.LoadScene",
                "UnityEngine.SceneManagement",
                "PlayerPrefs",
                "UnityEngine.Random",
            };

            for (int index = 0; index < forbidden.Length; index++)
            {
                Assert.That(
                    source,
                    Does.Not.Contain(forbidden[index]),
                    forbidden[index]);
            }

            Assert.That(source, Does.Contain("PlacedObjectAuthoring2D"));
            Assert.That(source, Does.Contain("IRestartParticipant"));
            Assert.That(source, Does.Contain("IDoorWalletReadPort"));
            Assert.That(source, Does.Contain("IDoorKeyReadPort"));
            Assert.That(source, Does.Contain("IDoorTransitionAuthorizationPort"));
        }

        private static void RequirePackageTypes()
        {
            Assert.That(RequirementType, Is.Not.Null);
            Assert.That(ConditionSetType, Is.Not.Null);
            Assert.That(ContextType, Is.Not.Null);
            Assert.That(SnapshotType, Is.Not.Null);
            Assert.That(CompositionType, Is.Not.Null);
            Assert.That(TransitionDefinitionType, Is.Not.Null);
        }

        private static Array TypedArray(Type elementType, params object[] values)
        {
            Array array = Array.CreateInstance(elementType, values.Length);
            for (int index = 0; index < values.Length; index++)
            {
                array.SetValue(values[index], index);
            }

            return array;
        }

        private static Type Find(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(fullName, false))
                .FirstOrDefault(type => type != null);
        }

        private static object InvokeStatic(
            Type type,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, type.FullName + "." + methodName);
            return method.Invoke(null, arguments);
        }

        private static object Invoke(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(
                method,
                Is.Not.Null,
                target.GetType().FullName + "." + methodName);
            return method.Invoke(target, arguments);
        }

        private static T Read<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(
                property,
                Is.Not.Null,
                target.GetType().FullName + "." + propertyName);
            return (T)property.GetValue(target);
        }

        private static int ReadCollectionCount(
            object target,
            string propertyName)
        {
            object collection = Read<object>(target, propertyName);
            PropertyInfo count = collection.GetType().GetProperty("Count");
            Assert.That(count, Is.Not.Null);
            return (int)count.GetValue(collection);
        }

        private static object ReadIndexed(
            object target,
            string propertyName,
            int index)
        {
            object collection = Read<object>(target, propertyName);
            PropertyInfo item = collection.GetType().GetProperty("Item");
            Assert.That(item, Is.Not.Null);
            return item.GetValue(collection, new object[] { index });
        }
    }
}
#endif
