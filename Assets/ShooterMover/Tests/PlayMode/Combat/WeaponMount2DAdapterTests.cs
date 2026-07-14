#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Combat
{
    public sealed class WeaponMount2DAdapterTests
    {
        private const string MountAdapterPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Combat/WeaponMount2DAdapter.cs";
        private const string HitAdapterPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Combat/CombatHit2DAdapter.cs";

        private static readonly StableId SourceId = StableId.Parse("actor.player-one");
        private static readonly StableId WeaponId = StableId.Parse("weapon.synthetic");
        private static readonly StableId MountId = StableId.Parse("weapon-mount.mount-one");
        private static readonly StableId ModuleId = StableId.Parse("behavior.cb009-fixture");
        private static readonly StableId FirstKind = StableId.Parse("operation-kind.first-2d");
        private static readonly StableId SecondKind = StableId.Parse("operation-kind.second-2d");

        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void ValidatedPlan_ExecutesInCanonicalOrderWithStable2DContext()
        {
            List<StableId> executionOrder = new List<StableId>();
            RecordingHandler firstHandler = new RecordingHandler(FirstKind, executionOrder);
            RecordingHandler secondHandler = new RecordingHandler(SecondKind, executionOrder);
            WeaponMount2DAdapter adapter = CreateMountAdapter(
                secondHandler,
                firstHandler);
            WeaponFireExecutionPlan plan = BuildPlan(
                WeaponId,
                MountId,
                Operation(FirstKind, "operation.first"),
                Operation(SecondKind, "operation.second"),
                Operation(FirstKind, "operation.third"));

            WeaponMount2DExecutionResult result = adapter.ExecutePlan(plan);

            Assert.That(result.Status, Is.EqualTo(WeaponMount2DExecutionStatus.Executed));
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.ExecutedOperationCount, Is.EqualTo(3));
            Assert.That(result.FailedOperationIndex, Is.EqualTo(-1));
            Assert.That(result.PlanId, Is.EqualTo(plan.DeterministicIdentity));
            CollectionAssert.AreEqual(
                new[]
                {
                    StableId.Parse("operation.first"),
                    StableId.Parse("operation.second"),
                    StableId.Parse("operation.third"),
                },
                executionOrder);

            Assert.That(firstHandler.Contexts.Count, Is.EqualTo(2));
            WeaponMount2DExecutionContext context = firstHandler.Contexts[0];
            Assert.That(context.PhysicsScene.IsValid(), Is.True);
            Assert.That(context.SourceId, Is.EqualTo(SourceId));
            Assert.That(context.CombatEventId, Is.EqualTo(plan.CombatEventId));
            Assert.That(context.WeaponId, Is.EqualTo(WeaponId));
            Assert.That(context.MountId, Is.EqualTo(MountId));
            Assert.That(context.PlanId, Is.EqualTo(plan.DeterministicIdentity));
            Assert.That(context.Origin, Is.EqualTo(new Vector2(2f, -1f)));
            Assert.That(context.Direction, Is.EqualTo(new Vector2(3f, 4f)));
            Assert.That(context.PlanOperationIndex, Is.Zero);
        }

        [Test]
        public void InvalidOrUnregisteredPlan_FailsBeforeAnyHandlerRuns()
        {
            List<StableId> executionOrder = new List<StableId>();
            RecordingHandler handler = new RecordingHandler(FirstKind, executionOrder);
            WeaponMount2DAdapter adapter = CreateMountAdapter(handler);

            WeaponMount2DExecutionResult nullPlan = adapter.ExecutePlan(null);
            Assert.That(nullPlan.Status, Is.EqualTo(WeaponMount2DExecutionStatus.InvalidPlan));

            WeaponFireExecutionPlan wrongWeapon = BuildPlan(
                StableId.Parse("weapon.other"),
                MountId,
                Operation(FirstKind, "operation.wrong-weapon"));
            WeaponMount2DExecutionResult mismatched = adapter.ExecutePlan(wrongWeapon);
            Assert.That(mismatched.Status, Is.EqualTo(WeaponMount2DExecutionStatus.InvalidPlan));

            WeaponFireExecutionPlan missingHandler = BuildPlan(
                WeaponId,
                MountId,
                Operation(SecondKind, "operation.unregistered"));
            WeaponMount2DExecutionResult missing = adapter.ExecutePlan(missingHandler);
            Assert.That(missing.Status, Is.EqualTo(WeaponMount2DExecutionStatus.MissingHandler));
            Assert.That(missing.ExecutedOperationCount, Is.Zero);
            Assert.That(missing.FailedOperationIndex, Is.Zero);
            Assert.That(
                missing.FailedOperationId,
                Is.EqualTo(StableId.Parse("operation.unregistered")));
            Assert.That(executionOrder, Is.Empty);
        }

        [Test]
        public void DisabledAdapter_DoesNotExecuteValidatedPlan()
        {
            List<StableId> executionOrder = new List<StableId>();
            RecordingHandler handler = new RecordingHandler(FirstKind, executionOrder);
            WeaponMount2DAdapter adapter = CreateMountAdapter(handler);
            WeaponFireExecutionPlan plan = BuildPlan(
                WeaponId,
                MountId,
                Operation(FirstKind, "operation.disabled"));

            adapter.enabled = false;
            WeaponMount2DExecutionResult result = adapter.ExecutePlan(plan);

            Assert.That(result.Status, Is.EqualTo(WeaponMount2DExecutionStatus.Disabled));
            Assert.That(result.ExecutedOperationCount, Is.Zero);
            Assert.That(executionOrder, Is.Empty);
        }

        [Test]
        public void DuplicateHandlerKinds_AreRejectedBeforeConfigurationChanges()
        {
            GameObject gameObject = CreateObject("Ambiguous Weapon Mount 2D Adapter");
            WeaponMount2DAdapter adapter = gameObject.AddComponent<WeaponMount2DAdapter>();
            List<StableId> log = new List<StableId>();

            Assert.Throws<ArgumentException>(
                () => adapter.Configure(
                    SourceId,
                    WeaponId,
                    MountId,
                    new IWeaponFireExecutionOperation2DHandler[]
                    {
                        new RecordingHandler(FirstKind, log),
                        new RecordingHandler(FirstKind, log),
                    }));
            Assert.That(adapter.IsConfigured, Is.False);
            Assert.That(adapter.RegisteredHandlerCount, Is.Zero);
        }

        [Test]
        public void HandlerRejectionAndFault_AreClassifiedWithoutEscapingExceptions()
        {
            WeaponFireExecutionPlan plan = BuildPlan(
                WeaponId,
                MountId,
                Operation(FirstKind, "operation.handler-boundary"));

            RecordingHandler rejecting = new RecordingHandler(
                FirstKind,
                new List<StableId>(),
                false,
                false);
            WeaponMount2DExecutionResult rejected =
                CreateMountAdapter(rejecting).ExecutePlan(plan);
            Assert.That(rejected.Status, Is.EqualTo(WeaponMount2DExecutionStatus.HandlerRejected));
            Assert.That(rejected.ExecutedOperationCount, Is.Zero);

            RecordingHandler throwing = new RecordingHandler(
                FirstKind,
                new List<StableId>(),
                true,
                true);
            WeaponMount2DExecutionResult faulted =
                CreateMountAdapter(throwing).ExecutePlan(plan);
            Assert.That(faulted.Status, Is.EqualTo(WeaponMount2DExecutionStatus.HandlerFaulted));
            Assert.That(faulted.ExecutedOperationCount, Is.Zero);
        }

        [Test]
        public void ConfirmedHit_MapsToImmutableCs004MessageWithStableIdentity()
        {
            Collider2D targetCollider = CreateTarget("Confirmed Target");
            StableId targetId = StableId.Parse("enemy.confirmed-target");
            StableId eventId = StableId.Parse("combat-event.confirmed-hit");
            CombatHit2DAdapter adapter = new CombatHit2DAdapter(SourceId);

            Assert.That(
                adapter.RegisterTarget(targetCollider, targetId),
                Is.EqualTo(CombatHit2DTargetRegistrationStatus.Registered));
            CombatHit2DTranslationResult translated = adapter.TranslateConfirmedHit(
                eventId,
                targetCollider,
                CombatChannel.Kinetic,
                false);

            Assert.That(translated.Status, Is.EqualTo(CombatHit2DTranslationStatus.Confirmed));
            Assert.That(translated.HasMessage, Is.True);
            Assert.That(translated.Message.EventId, Is.EqualTo(eventId));
            Assert.That(translated.Message.SourceId, Is.EqualTo(SourceId));
            Assert.That(translated.Message.TargetId, Is.EqualTo(targetId));
            Assert.That(translated.Message.Channel, Is.EqualTo(CombatChannel.Kinetic));
            Assert.That(translated.Message.Result, Is.EqualTo(HitResult.Confirmed));
            Assert.That(adapter.ProcessedEventCount, Is.EqualTo(1));

            AssertGetterOnly(typeof(CombatHit2DTranslationResult));
            AssertGetterOnly(typeof(HitMessage));
        }

        [Test]
        public void DuplicateCallback_ProducesExactlyOneConfirmedHitThenDuplicateIgnored()
        {
            Collider2D targetCollider = CreateTarget("Duplicate Target");
            StableId targetId = StableId.Parse("enemy.duplicate-target");
            StableId eventId = StableId.Parse("combat-event.duplicate-hit");
            CombatHit2DAdapter adapter = new CombatHit2DAdapter(SourceId);
            adapter.RegisterTarget(targetCollider, targetId);

            CombatHit2DTranslationResult first = adapter.TranslateConfirmedHit(
                eventId,
                targetCollider,
                CombatChannel.Thermal,
                false);
            CombatHit2DTranslationResult duplicate = adapter.TranslateConfirmedHit(
                eventId,
                targetCollider,
                CombatChannel.Thermal,
                false);

            Assert.That(first.Message.Result, Is.EqualTo(HitResult.Confirmed));
            Assert.That(
                duplicate.Status,
                Is.EqualTo(CombatHit2DTranslationStatus.DuplicateIgnored));
            Assert.That(duplicate.Message.Result, Is.EqualTo(HitResult.DuplicateEventIgnored));
            Assert.That(duplicate.Message.SourceId, Is.EqualTo(first.Message.SourceId));
            Assert.That(duplicate.Message.TargetId, Is.EqualTo(first.Message.TargetId));
            Assert.That(adapter.ProcessedEventCount, Is.EqualTo(1));
        }

        [Test]
        public void UnknownAmbiguousAndConflictingTargets_FailClosed()
        {
            Collider2D firstCollider = CreateTarget("First Target");
            Collider2D secondCollider = CreateTarget("Second Target");
            Collider2D unknownCollider = CreateTarget("Unknown Target");
            StableId firstTarget = StableId.Parse("enemy.first-target");
            StableId secondTarget = StableId.Parse("enemy.second-target");
            StableId eventId = StableId.Parse("combat-event.conflicting-hit");
            CombatHit2DAdapter adapter = new CombatHit2DAdapter(SourceId);

            Assert.That(
                adapter.RegisterTarget(firstCollider, firstTarget),
                Is.EqualTo(CombatHit2DTargetRegistrationStatus.Registered));
            Assert.That(
                adapter.RegisterTarget(firstCollider, secondTarget),
                Is.EqualTo(CombatHit2DTargetRegistrationStatus.Ambiguous));
            Assert.That(
                adapter.RegisterTarget(secondCollider, secondTarget),
                Is.EqualTo(CombatHit2DTargetRegistrationStatus.Registered));

            CombatHit2DTranslationResult unknown = adapter.TranslateConfirmedHit(
                StableId.Parse("combat-event.unknown-target"),
                unknownCollider,
                CombatChannel.Kinetic,
                false);
            Assert.That(unknown.Status, Is.EqualTo(CombatHit2DTranslationStatus.UnknownTarget));
            Assert.That(unknown.HasMessage, Is.False);

            CombatHit2DTranslationResult first = adapter.TranslateConfirmedHit(
                eventId,
                firstCollider,
                CombatChannel.Kinetic,
                false);
            CombatHit2DTranslationResult conflict = adapter.TranslateConfirmedHit(
                eventId,
                secondCollider,
                CombatChannel.Kinetic,
                false);
            Assert.That(first.Status, Is.EqualTo(CombatHit2DTranslationStatus.Confirmed));
            Assert.That(
                conflict.Status,
                Is.EqualTo(CombatHit2DTranslationStatus.ConflictingDuplicate));
            Assert.That(conflict.HasMessage, Is.False);
            Assert.That(adapter.ProcessedEventCount, Is.EqualTo(1));

            CombatHit2DTranslationResult invalidChannel = adapter.TranslateConfirmedHit(
                StableId.Parse("combat-event.invalid-channel"),
                firstCollider,
                CombatChannel.System,
                false);
            Assert.That(
                invalidChannel.Status,
                Is.EqualTo(CombatHit2DTranslationStatus.InvalidInput));
            Assert.That(invalidChannel.HasMessage, Is.False);
        }

        [Test]
        public void DestroyedTargetBeforeConfirmation_MapsToTargetAlreadyDestroyed()
        {
            Collider2D targetCollider = CreateTarget("Destroyed Target");
            StableId targetId = StableId.Parse("enemy.destroyed-target");
            CombatHit2DAdapter adapter = new CombatHit2DAdapter(SourceId);
            adapter.RegisterTarget(targetCollider, targetId);

            CombatHit2DTranslationResult result = adapter.TranslateConfirmedHit(
                StableId.Parse("combat-event.destroyed-target-hit"),
                targetCollider,
                CombatChannel.Explosive,
                true);

            Assert.That(
                result.Status,
                Is.EqualTo(CombatHit2DTranslationStatus.TargetAlreadyDestroyed));
            Assert.That(result.Message.Result, Is.EqualTo(HitResult.TargetAlreadyDestroyed));
            Assert.That(result.Message.TargetId, Is.EqualTo(targetId));
            Assert.That(adapter.ProcessedEventCount, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeSurface_Is2DOnlyAndContainsNoSceneSearchOrDamageAuthority()
        {
            Type[] inspectedTypes =
            {
                typeof(WeaponMount2DAdapter),
                typeof(WeaponMount2DExecutionContext),
                typeof(IWeaponFireExecutionOperation2DHandler),
                typeof(CombatHit2DAdapter),
                typeof(CombatHit2DTranslationResult),
            };
            Type[] forbiddenTypes =
            {
                typeof(Collider),
                typeof(Rigidbody),
                typeof(Collision),
                typeof(RaycastHit),
                typeof(PhysicsScene),
                typeof(Vector3),
                typeof(Quaternion),
            };

            foreach (Type inspected in inspectedTypes)
            {
                foreach (Type exposed in GetDeclaredPublicMemberTypes(inspected))
                {
                    Assert.That(
                        forbiddenTypes.Contains(Unwrap(exposed)),
                        Is.False,
                        inspected.FullName + " exposes forbidden 3D type " + exposed.FullName);
                }
            }

            string source = ReadProjectFile(MountAdapterPath)
                + "\n"
                + ReadProjectFile(HitAdapterPath);
            string[] forbiddenTokens =
            {
                "Physics.Raycast",
                "PhysicsScene ",
                "Collider ",
                "Rigidbody ",
                "RaycastHit",
                "Collision ",
                "Vector3",
                "Quaternion",
                "FindObject",
                "GameObject.Find",
                "FindWithTag",
                "Camera.main",
                "DamageMessage",
                "VitalState",
            };

            foreach (string token in forbiddenTokens)
            {
                Assert.That(source, Does.Not.Contain(token), "Forbidden token: " + token);
            }

            Assert.That(source, Does.Contain("PhysicsScene2D"));
            Assert.That(source, Does.Contain("Collider2D"));
            Assert.That(source, Does.Contain("HitMessage"));
        }

        private WeaponMount2DAdapter CreateMountAdapter(
            params IWeaponFireExecutionOperation2DHandler[] handlers)
        {
            GameObject gameObject = CreateObject("Weapon Mount 2D Adapter");
            WeaponMount2DAdapter adapter = gameObject.AddComponent<WeaponMount2DAdapter>();
            adapter.Configure(SourceId, WeaponId, MountId, handlers);
            return adapter;
        }

        private Collider2D CreateTarget(string name)
        {
            return CreateObject(name).AddComponent<BoxCollider2D>();
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static WeaponFireExecutionPlan BuildPlan(
            StableId weaponId,
            StableId mountId,
            params SyntheticOperation[] operations)
        {
            WeaponRuntimeProfile profile = BuildProfile(ModuleId);
            SyntheticModule module = new SyntheticModule(ModuleId, operations);
            WeaponBehaviorPipeline pipeline = new WeaponBehaviorPipeline(
                new IWeaponBehaviorModule[] { module });
            WeaponBehaviorInput input = new WeaponBehaviorInput(
                StableId.Parse("combat-event.cb009-plan"),
                weaponId,
                mountId,
                9L,
                profile,
                false,
                2d,
                -1d,
                3d,
                4d,
                1d);
            return pipeline.BuildExecutionPlan(input);
        }

        private static WeaponRuntimeProfile BuildProfile(params StableId[] moduleIds)
        {
            StableId[] copied = (StableId[])moduleIds.Clone();
            return WeaponRuntimeProfile.Create(
                WeaponRuntimeProfile.CurrentProfileVersion,
                StableId.Parse("weapon-profile.cb009-fixture"),
                0.1d,
                1,
                0d,
                0d,
                WeaponCycleMode.None,
                0d,
                0d,
                0d,
                0d,
                false,
                0d,
                0d,
                0.25d,
                copied,
                copied,
                0);
        }

        private static SyntheticOperation Operation(
            StableId operationKindId,
            string operationId)
        {
            return new SyntheticOperation(operationKindId, StableId.Parse(operationId));
        }

        private static void AssertGetterOnly(Type type)
        {
            Assert.That(
                type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.CanWrite),
                Is.Empty,
                type.FullName + " exposes a writable public property.");
        }

        private static IEnumerable<Type> GetDeclaredPublicMemberTypes(Type type)
        {
            BindingFlags flags = BindingFlags.Public
                | BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.DeclaredOnly;

            foreach (PropertyInfo property in type.GetProperties(flags))
            {
                yield return property.PropertyType;
            }

            foreach (FieldInfo field in type.GetFields(flags))
            {
                yield return field.FieldType;
            }

            foreach (ConstructorInfo constructor in type.GetConstructors(flags))
            {
                foreach (ParameterInfo parameter in constructor.GetParameters())
                {
                    yield return parameter.ParameterType;
                }
            }

            foreach (MethodInfo method in type.GetMethods(flags))
            {
                yield return method.ReturnType;
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    yield return parameter.ParameterType;
                }
            }
        }

        private static Type Unwrap(Type type)
        {
            if (type.IsByRef || type.IsArray)
            {
                return Unwrap(type.GetElementType());
            }

            if (type.IsGenericType)
            {
                Type[] arguments = type.GetGenericArguments();
                if (arguments.Length == 1)
                {
                    return Unwrap(arguments[0]);
                }
            }

            return type;
        }

        private static string ReadProjectFile(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return File.ReadAllText(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private sealed class SyntheticOperation : IWeaponFireExecutionOperation
        {
            public SyntheticOperation(StableId operationKindId, StableId operationId)
            {
                OperationKindId = operationKindId;
                OperationId = operationId;
            }

            public StableId OperationKindId { get; }

            public StableId OperationId { get; }
        }

        private sealed class SyntheticModule : IWeaponBehaviorModule
        {
            private readonly IWeaponFireExecutionOperation[] operations;

            public SyntheticModule(
                StableId moduleId,
                params IWeaponFireExecutionOperation[] operations)
            {
                ModuleId = moduleId;
                this.operations = (IWeaponFireExecutionOperation[])operations.Clone();
            }

            public StableId ModuleId { get; }

            public WeaponBehaviorModulePlan BuildExecutionPlan(WeaponBehaviorInput input)
            {
                return new WeaponBehaviorModulePlan(ModuleId, operations);
            }
        }

        private sealed class RecordingHandler : IWeaponFireExecutionOperation2DHandler
        {
            private readonly List<StableId> executionOrder;
            private readonly bool accept;
            private readonly bool throwOnExecute;

            public RecordingHandler(
                StableId operationKindId,
                List<StableId> executionOrder,
                bool accept = true,
                bool throwOnExecute = false)
            {
                OperationKindId = operationKindId;
                this.executionOrder = executionOrder;
                this.accept = accept;
                this.throwOnExecute = throwOnExecute;
                Contexts = new List<WeaponMount2DExecutionContext>();
            }

            public StableId OperationKindId { get; }

            public List<WeaponMount2DExecutionContext> Contexts { get; }

            public bool TryExecute(
                WeaponFireExecutionOperationEntry operation,
                WeaponMount2DExecutionContext context)
            {
                executionOrder.Add(operation.OperationId);
                Contexts.Add(context);
                if (throwOnExecute)
                {
                    throw new InvalidOperationException("Synthetic handler fault.");
                }

                return accept;
            }
        }
    }
}
#endif
