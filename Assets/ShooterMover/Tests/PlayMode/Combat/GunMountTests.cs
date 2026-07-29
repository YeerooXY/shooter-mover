#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.UnityAdapters.Combat;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Combat
{
    public sealed class GunMountTests
    {
        private const string MountAdapterPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Combat/GunMount.cs";
        private const string HitAdapterPath =
            "Assets/ShooterMover/Runtime/UnityAdapters/Combat/HitResolver.cs";

        private static readonly StableId SourceId = StableId.Parse("actor.player-one");
        private static readonly StableId GunId = StableId.Parse("gun.synthetic");
        private static readonly StableId MountId = StableId.Parse("gun-mount.mount-one");
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
            GunMount adapter = CreateMountAdapter(
                secondHandler,
                firstHandler);
            GunFireExecutionPlan plan = BuildPlan(
                GunId,
                MountId,
                Operation(FirstKind, "operation.first"),
                Operation(SecondKind, "operation.second"),
                Operation(FirstKind, "operation.third"));

            GunMountExecutionResult result = adapter.ExecutePlan(plan);

            Assert.That(result.Status, Is.EqualTo(GunMountExecutionStatus.Executed));
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
            GunMountExecutionContext context = firstHandler.Contexts[0];
            Assert.That(context.PhysicsScene.IsValid(), Is.True);
            Assert.That(context.SourceId, Is.EqualTo(SourceId));
            Assert.That(context.CombatEventId, Is.EqualTo(plan.CombatEventId));
            Assert.That(context.GunId, Is.EqualTo(GunId));
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
            GunMount adapter = CreateMountAdapter(handler);

            GunMountExecutionResult nullPlan = adapter.ExecutePlan(null);
            Assert.That(nullPlan.Status, Is.EqualTo(GunMountExecutionStatus.InvalidPlan));

            GunFireExecutionPlan wrongGun = BuildPlan(
                StableId.Parse("gun.other"),
                MountId,
                Operation(FirstKind, "operation.wrong-gun"));
            GunMountExecutionResult mismatched = adapter.ExecutePlan(wrongGun);
            Assert.That(mismatched.Status, Is.EqualTo(GunMountExecutionStatus.InvalidPlan));

            GunFireExecutionPlan missingHandler = BuildPlan(
                GunId,
                MountId,
                Operation(SecondKind, "operation.unregistered"));
            GunMountExecutionResult missing = adapter.ExecutePlan(missingHandler);
            Assert.That(missing.Status, Is.EqualTo(GunMountExecutionStatus.MissingHandler));
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
            GunMount adapter = CreateMountAdapter(handler);
            GunFireExecutionPlan plan = BuildPlan(
                GunId,
                MountId,
                Operation(FirstKind, "operation.disabled"));

            adapter.enabled = false;
            GunMountExecutionResult result = adapter.ExecutePlan(plan);

            Assert.That(result.Status, Is.EqualTo(GunMountExecutionStatus.Disabled));
            Assert.That(result.ExecutedOperationCount, Is.Zero);
            Assert.That(executionOrder, Is.Empty);
        }

        [Test]
        public void DuplicateHandlerKinds_AreRejectedBeforeConfigurationChanges()
        {
            GameObject gameObject = CreateObject("Ambiguous Gun Mount 2D Adapter");
            GunMount adapter = gameObject.AddComponent<GunMount>();
            List<StableId> log = new List<StableId>();

            Assert.Throws<ArgumentException>(
                () => adapter.Configure(
                    SourceId,
                    GunId,
                    MountId,
                    new IGunFireExecutionHandler[]
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
            GunFireExecutionPlan plan = BuildPlan(
                GunId,
                MountId,
                Operation(FirstKind, "operation.handler-boundary"));

            RecordingHandler rejecting = new RecordingHandler(
                FirstKind,
                new List<StableId>(),
                false,
                false);
            GunMountExecutionResult rejected =
                CreateMountAdapter(rejecting).ExecutePlan(plan);
            Assert.That(rejected.Status, Is.EqualTo(GunMountExecutionStatus.HandlerRejected));
            Assert.That(rejected.ExecutedOperationCount, Is.Zero);

            RecordingHandler throwing = new RecordingHandler(
                FirstKind,
                new List<StableId>(),
                true,
                true);
            GunMountExecutionResult faulted =
                CreateMountAdapter(throwing).ExecutePlan(plan);
            Assert.That(faulted.Status, Is.EqualTo(GunMountExecutionStatus.HandlerFaulted));
            Assert.That(faulted.ExecutedOperationCount, Is.Zero);
        }

        [Test]
        public void ConfirmedHit_MapsToImmutableCs004MessageWithStableIdentity()
        {
            Collider2D targetCollider = CreateTarget("Confirmed Target");
            StableId targetId = StableId.Parse("enemy.confirmed-target");
            StableId eventId = StableId.Parse("combat-event.confirmed-hit");
            HitResolver adapter = new HitResolver(SourceId);

            Assert.That(
                adapter.RegisterTarget(targetCollider, targetId),
                Is.EqualTo(HitTargetRegistrationStatus.Registered));
            HitTranslationResult translated = adapter.TranslateConfirmedHit(
                eventId,
                targetCollider,
                CombatChannel.Kinetic,
                false);

            Assert.That(translated.Status, Is.EqualTo(HitTranslationStatus.Confirmed));
            Assert.That(translated.HasMessage, Is.True);
            Assert.That(translated.Message.EventId, Is.EqualTo(eventId));
            Assert.That(translated.Message.SourceId, Is.EqualTo(SourceId));
            Assert.That(translated.Message.TargetId, Is.EqualTo(targetId));
            Assert.That(translated.Message.Channel, Is.EqualTo(CombatChannel.Kinetic));
            Assert.That(translated.Message.Result, Is.EqualTo(HitResult.Confirmed));
            Assert.That(adapter.ProcessedEventCount, Is.EqualTo(1));

            AssertGetterOnly(typeof(HitTranslationResult));
            AssertGetterOnly(typeof(HitMessage));
        }

        [Test]
        public void DuplicateCallback_ProducesExactlyOneConfirmedHitThenDuplicateIgnored()
        {
            Collider2D targetCollider = CreateTarget("Duplicate Target");
            StableId targetId = StableId.Parse("enemy.duplicate-target");
            StableId eventId = StableId.Parse("combat-event.duplicate-hit");
            HitResolver adapter = new HitResolver(SourceId);
            adapter.RegisterTarget(targetCollider, targetId);

            HitTranslationResult first = adapter.TranslateConfirmedHit(
                eventId,
                targetCollider,
                CombatChannel.Thermal,
                false);
            HitTranslationResult duplicate = adapter.TranslateConfirmedHit(
                eventId,
                targetCollider,
                CombatChannel.Thermal,
                false);

            Assert.That(first.Message.Result, Is.EqualTo(HitResult.Confirmed));
            Assert.That(
                duplicate.Status,
                Is.EqualTo(HitTranslationStatus.DuplicateIgnored));
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
            HitResolver adapter = new HitResolver(SourceId);

            Assert.That(
                adapter.RegisterTarget(firstCollider, firstTarget),
                Is.EqualTo(HitTargetRegistrationStatus.Registered));
            Assert.That(
                adapter.RegisterTarget(firstCollider, secondTarget),
                Is.EqualTo(HitTargetRegistrationStatus.Ambiguous));
            Assert.That(
                adapter.RegisterTarget(secondCollider, secondTarget),
                Is.EqualTo(HitTargetRegistrationStatus.Registered));

            HitTranslationResult unknown = adapter.TranslateConfirmedHit(
                StableId.Parse("combat-event.unknown-target"),
                unknownCollider,
                CombatChannel.Kinetic,
                false);
            Assert.That(unknown.Status, Is.EqualTo(HitTranslationStatus.UnknownTarget));
            Assert.That(unknown.HasMessage, Is.False);

            HitTranslationResult first = adapter.TranslateConfirmedHit(
                eventId,
                firstCollider,
                CombatChannel.Kinetic,
                false);
            HitTranslationResult conflict = adapter.TranslateConfirmedHit(
                eventId,
                secondCollider,
                CombatChannel.Kinetic,
                false);
            Assert.That(first.Status, Is.EqualTo(HitTranslationStatus.Confirmed));
            Assert.That(
                conflict.Status,
                Is.EqualTo(HitTranslationStatus.ConflictingDuplicate));
            Assert.That(conflict.HasMessage, Is.False);
            Assert.That(adapter.ProcessedEventCount, Is.EqualTo(1));

            HitTranslationResult invalidChannel = adapter.TranslateConfirmedHit(
                StableId.Parse("combat-event.invalid-channel"),
                firstCollider,
                CombatChannel.System,
                false);
            Assert.That(
                invalidChannel.Status,
                Is.EqualTo(HitTranslationStatus.InvalidInput));
            Assert.That(invalidChannel.HasMessage, Is.False);
        }

        [Test]
        public void DestroyedTargetBeforeConfirmation_MapsToTargetAlreadyDestroyed()
        {
            Collider2D targetCollider = CreateTarget("Destroyed Target");
            StableId targetId = StableId.Parse("enemy.destroyed-target");
            HitResolver adapter = new HitResolver(SourceId);
            adapter.RegisterTarget(targetCollider, targetId);

            HitTranslationResult result = adapter.TranslateConfirmedHit(
                StableId.Parse("combat-event.destroyed-target-hit"),
                targetCollider,
                CombatChannel.Explosive,
                true);

            Assert.That(
                result.Status,
                Is.EqualTo(HitTranslationStatus.TargetAlreadyDestroyed));
            Assert.That(result.Message.Result, Is.EqualTo(HitResult.TargetAlreadyDestroyed));
            Assert.That(result.Message.TargetId, Is.EqualTo(targetId));
            Assert.That(adapter.ProcessedEventCount, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeSurface_Is2DOnlyAndContainsNoSceneSearchOrDamageAuthority()
        {
            Type[] inspectedTypes =
            {
                typeof(GunMount),
                typeof(GunMountExecutionContext),
                typeof(IGunFireExecutionHandler),
                typeof(HitResolver),
                typeof(HitTranslationResult),
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
                "RaycastHit",
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

            string[] forbiddenThreeDimensionalTypePatterns =
            {
                @"\bPhysicsScene\b\s+[A-Za-z_]",
                @"\bCollider\b\s+[A-Za-z_]",
                @"\bRigidbody\b\s+[A-Za-z_]",
                @"\bCollision\b\s+[A-Za-z_]",
            };

            foreach (string pattern in forbiddenThreeDimensionalTypePatterns)
            {
                Assert.That(
                    Regex.IsMatch(source, pattern),
                    Is.False,
                    "Forbidden 3D type pattern: " + pattern);
            }

            Assert.That(source, Does.Contain("PhysicsScene2D"));
            Assert.That(source, Does.Contain("Collider2D"));
            Assert.That(source, Does.Contain("HitMessage"));
        }

        private GunMount CreateMountAdapter(
            params IGunFireExecutionHandler[] handlers)
        {
            GameObject gameObject = CreateObject("Gun Mount 2D Adapter");
            GunMount adapter = gameObject.AddComponent<GunMount>();
            adapter.Configure(SourceId, GunId, MountId, handlers);
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

        private static GunFireExecutionPlan BuildPlan(
            StableId gunId,
            StableId mountId,
            params SyntheticOperation[] operations)
        {
            GunLiveProfile profile = BuildProfile(ModuleId);
            SyntheticModule module = new SyntheticModule(ModuleId, operations);
            GunBehaviorPipeline pipeline = new GunBehaviorPipeline(
                new IGunBehaviorModule[] { module });
            GunBehaviorInput input = new GunBehaviorInput(
                StableId.Parse("combat-event.cb009-plan"),
                gunId,
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

        private static GunLiveProfile BuildProfile(params StableId[] moduleIds)
        {
            StableId[] copied = (StableId[])moduleIds.Clone();
            return GunLiveProfile.Create(
                GunLiveProfile.CurrentProfileVersion,
                StableId.Parse("gun-profile.cb009-fixture"),
                0.1d,
                1,
                0d,
                0d,
                GunCycleMode.None,
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
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return File.ReadAllText(
                Path.Combine(
                    projectRoot,
                    assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private sealed class SyntheticOperation : IGunFireExecutionOperation
        {
            public SyntheticOperation(StableId operationKindId, StableId operationId)
            {
                OperationKindId = operationKindId;
                OperationId = operationId;
            }

            public StableId OperationKindId { get; }

            public StableId OperationId { get; }
        }

        private sealed class SyntheticModule : IGunBehaviorModule
        {
            private readonly IGunFireExecutionOperation[] operations;

            public SyntheticModule(
                StableId moduleId,
                params IGunFireExecutionOperation[] operations)
            {
                ModuleId = moduleId;
                this.operations = (IGunFireExecutionOperation[])operations.Clone();
            }

            public StableId ModuleId { get; }

            public GunBehaviorModulePlan BuildExecutionPlan(GunBehaviorInput input)
            {
                return new GunBehaviorModulePlan(ModuleId, operations);
            }
        }

        private sealed class RecordingHandler : IGunFireExecutionHandler
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
                Contexts = new List<GunMountExecutionContext>();
            }

            public StableId OperationKindId { get; }

            public List<GunMountExecutionContext> Contexts { get; }

            public bool TryExecute(
                GunFireExecutionOperationEntry operation,
                GunMountExecutionContext context)
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
