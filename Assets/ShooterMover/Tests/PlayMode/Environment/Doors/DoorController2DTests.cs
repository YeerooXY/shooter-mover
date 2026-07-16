#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Objects;
using ShooterMover.Contracts.Rooms;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Authoring;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.EnvironmentPackages.Doors
{
    public sealed class DoorController2DTests
    {
        private static readonly Type ControllerType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorController2D");
        private static readonly Type RequirementType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorConditionRequirement");
        private static readonly Type SnapshotType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorConditionFactSnapshot");
        private static readonly Type AuthorizationStubType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorTransitionAuthorizationStub");
        private static readonly Type InitialStateType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorInitialState");
        private static readonly Type CompositionType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorConditionComposition");
        private static readonly Type OneWayPolicyType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorOneWayPolicy");
        private static readonly Type TravelDirectionType = Find(
            "ShooterMover.ContentPackages.Environment.Doors.DoorTravelDirection");

        private readonly List<UnityEngine.Object> created =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(created[index]);
                }
            }

            created.Clear();
        }

        [UnityTest]
        public IEnumerator ColliderPresentationAndRestart_RestoreAndReevaluate()
        {
            RequirePackageTypes();
            GameplaySceneScope2D scope = CreateScope();
            Transform container = Track(new GameObject("DoorContainer")).transform;
            container.SetParent(scope.transform);

            DoorFixture fixture = CreateDoorFixture(
                container,
                "placed.restart-door");
            StableId targetId = StableId.Parse("target.restart-door");
            Array requirements = TypedArray(
                RequirementType,
                InvokeStatic(RequirementType, "InteractionRequested"),
                InvokeStatic(RequirementType, "TargetDestroyed", targetId));
            object snapshot = Activator.CreateInstance(SnapshotType);
            Invoke(snapshot, "SetTargetDestroyed", targetId, false);

            ConfigureDoor(
                fixture,
                "Closed",
                "Any",
                requirements,
                "Bidirectional",
                null,
                null,
                false);
            Invoke(
                fixture.Controller,
                "SetConditionPortsForTests",
                snapshot,
                snapshot,
                snapshot,
                snapshot);

            fixture.Root.SetActive(true);
            object validation = Invoke(fixture.Controller, "TryInitialize");
            Assert.That(Read<bool>(validation, "IsValid"), Is.True);
            Assert.That(Read<bool>(fixture.Controller, "IsOpen"), Is.False);
            Assert.That(fixture.Collider.enabled, Is.True);
            Assert.That(fixture.ClosedPresentation.activeSelf, Is.True);
            Assert.That(fixture.OpenPresentation.activeSelf, Is.False);
            Assert.That(scope.RegisteredParticipantCount, Is.EqualTo(1));
            Assert.That(scope.RegisteredRestartParticipantCount, Is.EqualTo(1));

            Assert.That(
                (bool)Invoke(fixture.Controller, "NotifyInteractionRequested"),
                Is.True);
            Assert.That(Read<bool>(fixture.Controller, "IsOpen"), Is.True);
            Assert.That(fixture.Collider.enabled, Is.False);
            Assert.That(fixture.ClosedPresentation.activeSelf, Is.False);
            Assert.That(fixture.OpenPresentation.activeSelf, Is.True);

            scope.RunRestart(1);
            Assert.That(Read<bool>(fixture.Controller, "IsOpen"), Is.False);
            Assert.That(fixture.Collider.enabled, Is.True);
            Assert.That(fixture.ClosedPresentation.activeSelf, Is.True);
            Assert.That(fixture.OpenPresentation.activeSelf, Is.False);
            Assert.That(scope.RegisteredParticipantCount, Is.EqualTo(1));
            Assert.That(scope.RegisteredRestartParticipantCount, Is.EqualTo(1));
            Assert.That(Read<int>(fixture.Controller, "RestartCount"), Is.EqualTo(1));

            Invoke(snapshot, "SetTargetDestroyed", targetId, true);
            scope.RunRestart(2);
            Assert.That(Read<bool>(fixture.Controller, "IsOpen"), Is.True);
            Assert.That(fixture.Collider.enabled, Is.False);
            Assert.That(Read<int>(fixture.Controller, "RestartCount"), Is.EqualTo(2));

            yield return null;
        }

        [UnityTest]
        public IEnumerator RenameAndReparent_PreserveAuthoredIdentityAndRegistration()
        {
            RequirePackageTypes();
            GameplaySceneScope2D scope = CreateScope();
            Transform firstParent = Track(new GameObject("FirstParent")).transform;
            Transform secondParent = Track(new GameObject("SecondParent")).transform;
            firstParent.SetParent(scope.transform);
            secondParent.SetParent(scope.transform);

            DoorFixture fixture = CreateDoorFixture(
                firstParent,
                "placed.rename-door");
            Array requirements = TypedArray(
                RequirementType,
                InvokeStatic(RequirementType, "Always"));
            ConfigureDoor(
                fixture,
                "Closed",
                "All",
                requirements,
                "Bidirectional",
                null,
                null,
                false);

            fixture.Root.SetActive(true);
            object firstValidation = Invoke(fixture.Controller, "TryInitialize");
            Assert.That(Read<bool>(firstValidation, "IsValid"), Is.True);
            StableId original = Read<StableId>(
                fixture.Controller,
                "DoorPlacedInstanceId");
            Assert.That(original, Is.EqualTo(StableId.Parse("placed.rename-door")));
            Assert.That(scope.RegisteredParticipantCount, Is.EqualTo(1));
            Assert.That(scope.RegisteredRestartParticipantCount, Is.EqualTo(1));

            fixture.Root.SetActive(false);
            Assert.That(scope.RegisteredParticipantCount, Is.EqualTo(0));
            Assert.That(scope.RegisteredRestartParticipantCount, Is.EqualTo(0));

            fixture.Root.name = "RenamedDoorAnywhere";
            fixture.Root.transform.SetParent(secondParent);
            fixture.Root.SetActive(true);
            object secondValidation = Invoke(fixture.Controller, "TryInitialize");
            Assert.That(Read<bool>(secondValidation, "IsValid"), Is.True);

            StableId rebound = Read<StableId>(
                fixture.Controller,
                "DoorPlacedInstanceId");
            Assert.That(rebound, Is.EqualTo(original));
            Assert.That(scope.RegisteredParticipantCount, Is.EqualTo(1));
            Assert.That(scope.RegisteredRestartParticipantCount, Is.EqualTo(1));

            yield return null;
        }

        [UnityTest]
        public IEnumerator OneWayTransition_RequiresTypedAuthorizationAndSockets()
        {
            RequirePackageTypes();
            GameplaySceneScope2D scope = CreateScope();
            Transform container = Track(new GameObject("TransitionContainer")).transform;
            container.SetParent(scope.transform);
            DoorFixture fixture = CreateDoorFixture(
                container,
                "placed.transition-door");

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
            Array requirements = TypedArray(
                RequirementType,
                InvokeStatic(RequirementType, "Always"));
            ConfigureDoor(
                fixture,
                "Closed",
                "All",
                requirements,
                "ForwardOnly",
                source,
                destination,
                true);

            object authorization = Activator.CreateInstance(AuthorizationStubType);
            Invoke(
                authorization,
                "SetAuthorized",
                false,
                "Route is not currently authorized.");
            Invoke(
                fixture.Controller,
                "SetTransitionAuthorizationForTests",
                authorization);

            fixture.Root.SetActive(true);
            object validation = Invoke(fixture.Controller, "TryInitialize");
            Assert.That(Read<bool>(validation, "IsValid"), Is.True);
            Assert.That(Read<bool>(fixture.Controller, "IsOpen"), Is.True);

            object reverse = Invoke(
                fixture.Controller,
                "TryRequestTransition",
                Enum.Parse(TravelDirectionType, "Reverse"));
            Assert.That(
                Read<object>(reverse, "Status").ToString(),
                Is.EqualTo("RejectedByOneWayPolicy"));
            Assert.That(Read<int>(authorization, "RequestCount"), Is.EqualTo(0));

            object denied = Invoke(
                fixture.Controller,
                "TryRequestTransition",
                Enum.Parse(TravelDirectionType, "Forward"));
            Assert.That(
                Read<object>(denied, "Status").ToString(),
                Is.EqualTo("AuthorizationDenied"));
            Assert.That(Read<int>(authorization, "RequestCount"), Is.EqualTo(1));

            Invoke(
                authorization,
                "SetAuthorized",
                true,
                "Route is authorized.");
            object allowed = Invoke(
                fixture.Controller,
                "TryRequestTransition",
                Enum.Parse(TravelDirectionType, "Forward"));
            Assert.That(
                Read<object>(allowed, "Status").ToString(),
                Is.EqualTo("Authorized"));
            Assert.That(Read<bool>(allowed, "IsAuthorized"), Is.True);
            Assert.That(Read<int>(authorization, "RequestCount"), Is.EqualTo(2));

            yield return null;
        }

        private GameplaySceneScope2D CreateScope()
        {
            GameObject root = Track(new GameObject("GameplayScope"));
            GameplaySceneScope2D scope =
                root.AddComponent<GameplaySceneScope2D>();
            scope.ConfigureForTests(
                "scope.door-tests",
                "scope.gameplay",
                "projection.door-tests",
                "run.door-tests",
                0);
            return scope;
        }

        private DoorFixture CreateDoorFixture(
            Transform parent,
            string placedId)
        {
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameObject root = Track(new GameObject("ReusableDoor"));
            root.SetActive(false);
            root.transform.SetParent(parent);

            BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
            GameObject closedPresentation =
                Track(new GameObject("ClosedPresentation"));
            GameObject openPresentation =
                Track(new GameObject("OpenPresentation"));
            closedPresentation.transform.SetParent(root.transform);
            openPresentation.transform.SetParent(root.transform);

            PlacedObjectAuthoring2D placed =
                root.AddComponent<PlacedObjectAuthoring2D>();
            placed.ConfigureForTests(
                placedId,
                family,
                "variant.standard-door",
                null,
                "scope.gameplay",
                Array.Empty<CapabilityOverrideAuthoring>());
            Component controller = root.AddComponent(ControllerType);
            return new DoorFixture(
                root,
                placed,
                controller,
                collider,
                closedPresentation,
                openPresentation);
        }

        private ObjectFamilyDefinitionAsset CreateFamily()
        {
            ObjectCapabilityDefinitionAsset presentation = Track(
                ObjectCapabilityDefinitionAsset.CreateRuntime(
                    "capability.presentation",
                    new CapabilityFieldAuthoring(
                        "field.mode",
                        CapabilityFieldValue.FromText("door"))));
            ObjectCapabilityDefinitionAsset collision = Track(
                ObjectCapabilityDefinitionAsset.CreateRuntime(
                    "capability.collision",
                    new CapabilityFieldAuthoring(
                        "field.blocks",
                        CapabilityFieldValue.FromBoolean(true))));
            ObjectCapabilityDefinitionAsset door = Track(
                ObjectCapabilityDefinitionAsset.CreateRuntime(
                    "capability.door",
                    new CapabilityFieldAuthoring(
                        "field.reusable",
                        CapabilityFieldValue.FromBoolean(true))));
            ObjectCapabilityDefinitionAsset lifecycle = Track(
                ObjectCapabilityDefinitionAsset.CreateRuntime(
                    "capability.lifecycle",
                    new CapabilityFieldAuthoring(
                        "field.restart",
                        CapabilityFieldValue.FromBoolean(true))));

            return Track(
                ObjectFamilyDefinitionAsset.CreateRuntime(
                    "family.reusable-door",
                    "Reusable Door",
                    "variant.standard-door",
                    new[] { presentation, collision, door, lifecycle },
                    new ObjectVariantAuthoring(
                        "variant.standard-door",
                        null,
                        ObjectCapabilitySelectionAuthoring.Inherit(
                            "capability.presentation"),
                        ObjectCapabilitySelectionAuthoring.Inherit(
                            "capability.collision"),
                        ObjectCapabilitySelectionAuthoring.Inherit(
                            "capability.door"),
                        ObjectCapabilitySelectionAuthoring.Inherit(
                            "capability.lifecycle"))));
        }

        private static void ConfigureDoor(
            DoorFixture fixture,
            string initialState,
            string composition,
            Array requirements,
            string oneWayPolicy,
            RoomSocket source,
            RoomSocket destination,
            bool transitionEnabled)
        {
            Invoke(
                fixture.Controller,
                "ConfigureForTests",
                fixture.Placed,
                Enum.Parse(InitialStateType, initialState),
                Enum.Parse(CompositionType, composition),
                requirements,
                new Collider2D[] { fixture.Collider },
                fixture.ClosedPresentation,
                fixture.OpenPresentation,
                Enum.Parse(OneWayPolicyType, oneWayPolicy),
                source,
                destination,
                transitionEnabled);
        }

        private static void RequirePackageTypes()
        {
            Assert.That(ControllerType, Is.Not.Null);
            Assert.That(RequirementType, Is.Not.Null);
            Assert.That(SnapshotType, Is.Not.Null);
            Assert.That(AuthorizationStubType, Is.Not.Null);
            Assert.That(InitialStateType, Is.Not.Null);
            Assert.That(CompositionType, Is.Not.Null);
            Assert.That(OneWayPolicyType, Is.Not.Null);
            Assert.That(TravelDirectionType, Is.Not.Null);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
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

        private sealed class DoorFixture
        {
            public DoorFixture(
                GameObject root,
                PlacedObjectAuthoring2D placed,
                Component controller,
                BoxCollider2D collider,
                GameObject closedPresentation,
                GameObject openPresentation)
            {
                Root = root;
                Placed = placed;
                Controller = controller;
                Collider = collider;
                ClosedPresentation = closedPresentation;
                OpenPresentation = openPresentation;
            }

            public GameObject Root { get; }

            public PlacedObjectAuthoring2D Placed { get; }

            public Component Controller { get; }

            public BoxCollider2D Collider { get; }

            public GameObject ClosedPresentation { get; }

            public GameObject OpenPresentation { get; }
        }
    }
}
#endif
