using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ShooterMover.Contracts.Combat;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Movement;
using ShooterMover.UnityAdapters.Physics;
using UnityEngine;

namespace ShooterMover.Tests.PlayMode.Movement
{
    public sealed class MovementContact2DAdapterTests
    {
        private const float Tolerance = 0.00001f;
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
        public void WallContact_UsesMT005AndAppliesReflectedOutcomeOnce()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            FakeMovementContactAuthority authority = CreateAuthority(tuning, -10d, 0d);
            Rigidbody2D body;
            MovementContact2DAdapter adapter = CreateAdapter(authority, out body);
            Collider2D wall = CreateContactCollider(
                "MT-009 wall",
                MovementContact2DDescriptor.Wall());

            MovementContact2DProcessResult result = adapter.TryProcessContact(
                wall,
                Vector2.right,
                1d);

            Assert.That(result, Is.EqualTo(MovementContact2DProcessResult.WallReflected));
            Assert.That(authority.WallApplyCount, Is.EqualTo(1));
            Assert.That(authority.LastWallResult, Is.Not.Null);
            Assert.That(authority.LastWallResult.WasReflected, Is.True);
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
            AssertVector(
                body.linearVelocity,
                (float)authority.LastWallResult.OutgoingVelocityX,
                (float)authority.LastWallResult.OutgoingVelocityY);
        }

        [Test]
        public void LightEnemy_UsesCS004WeightAndShovesThrough()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            FakeMovementContactAuthority authority = CreateAuthority(tuning, -10d, 0d);
            Rigidbody2D body;
            MovementContact2DAdapter adapter = CreateAdapter(authority, out body);
            StableId enemyId = StableId.Parse("enemy.light.mt-009");
            Collider2D enemy = CreateContactCollider(
                "MT-009 light enemy",
                CreateEnemyDescriptor(
                    enemyId,
                    CombatWeightClass.Heavy,
                    CombatWeightClass.Light));

            MovementContact2DProcessResult result = adapter.TryProcessContact(
                enemy,
                Vector2.right,
                2d);

            Assert.That(result, Is.EqualTo(MovementContact2DProcessResult.EnemyResolved));
            Assert.That(authority.EnemyApplyCount, Is.EqualTo(1));
            Assert.That(authority.EnemyResolutionCount, Is.EqualTo(1));
            Assert.That(authority.LastEnemyResolution.AllowsShoveThrough, Is.True);
            Assert.That(body.linearVelocity.x, Is.LessThan(0f));
            Assert.That(Math.Abs(body.linearVelocity.x), Is.LessThan(10f));
            Assert.That(authority.LastRegistration.EnemyId, Is.EqualTo(enemyId));
            Assert.That(authority.LastRegistration.ContactAccepted, Is.True);
        }

        [Test]
        public void HeavyEnemy_BlocksInwardVelocity()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            FakeMovementContactAuthority authority = CreateAuthority(tuning, -8d, 3d);
            Rigidbody2D body;
            MovementContact2DAdapter adapter = CreateAdapter(authority, out body);
            StableId enemyId = StableId.Parse("enemy.heavy.mt-009");
            Collider2D enemy = CreateContactCollider(
                "MT-009 heavy enemy",
                CreateEnemyDescriptor(
                    enemyId,
                    CombatWeightClass.Light,
                    CombatWeightClass.Heavy));

            MovementContact2DProcessResult result = adapter.TryProcessContact(
                enemy,
                Vector2.right,
                3d);

            Assert.That(result, Is.EqualTo(MovementContact2DProcessResult.EnemyResolved));
            Assert.That(authority.LastEnemyResolution.BlocksApproach, Is.True);
            Assert.That(authority.LastEnemyResolution.Outcome, Is.EqualTo(MovementContactOutcome.BlockedByWeight));
            Assert.That(body.linearVelocity.x, Is.EqualTo(0f).Within(Tolerance));
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        }

        [Test]
        public void UnknownContactWithoutExplicitContract_FailsSafely()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            FakeMovementContactAuthority authority = CreateAuthority(tuning, -5d, 0d);
            Rigidbody2D body;
            MovementContact2DAdapter adapter = CreateAdapter(authority, out body);
            Collider2D unknown = CreateBareCollider("MT-009 unknown");
            body.linearVelocity = new Vector2(7f, -2f);

            MovementContact2DProcessResult result = adapter.TryProcessContact(
                unknown,
                Vector2.right,
                4d);

            Assert.That(result, Is.EqualTo(MovementContact2DProcessResult.UnknownContactIgnored));
            Assert.That(authority.SnapshotReadCount, Is.Zero);
            Assert.That(authority.TotalApplyCount, Is.Zero);
            AssertVector(body.linearVelocity, 7f, -2f);
        }

        [Test]
        public void CornerNormals_AreAppliedIndependentlyWithinOneFixedStep()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            FakeMovementContactAuthority authority = CreateAuthority(tuning, -6d, -8d);
            Rigidbody2D body;
            MovementContact2DAdapter adapter = CreateAdapter(authority, out body);
            Collider2D wall = CreateContactCollider(
                "MT-009 corner wall",
                MovementContact2DDescriptor.Wall());

            MovementContact2DProcessResult first = adapter.TryProcessContact(
                wall,
                Vector2.right,
                5d);
            MovementContact2DProcessResult second = adapter.TryProcessContact(
                wall,
                Vector2.up,
                5d);

            Assert.That(first, Is.EqualTo(MovementContact2DProcessResult.WallReflected));
            Assert.That(second, Is.EqualTo(MovementContact2DProcessResult.WallReflected));
            Assert.That(authority.WallApplyCount, Is.EqualTo(2));
            Assert.That(adapter.WallContactsProcessed, Is.EqualTo(2));
            Assert.That(body.linearVelocity.x, Is.GreaterThan(0f));
            Assert.That(body.linearVelocity.y, Is.GreaterThan(0f));
        }

        [Test]
        public void DuplicateCallback_SameColliderAndNormalProducesNoDuplicateOutcome()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            FakeMovementContactAuthority authority = CreateAuthority(tuning, -9d, 0d);
            Rigidbody2D body;
            MovementContact2DAdapter adapter = CreateAdapter(authority, out body);
            Collider2D wall = CreateContactCollider(
                "MT-009 duplicate wall",
                MovementContact2DDescriptor.Wall());

            MovementContact2DProcessResult first = adapter.TryProcessContact(
                wall,
                Vector2.right,
                6d);
            Vector2 afterFirst = body.linearVelocity;
            MovementContact2DProcessResult duplicate = adapter.TryProcessContact(
                wall,
                new Vector2(2f, 0f),
                6d);

            Assert.That(first, Is.EqualTo(MovementContact2DProcessResult.WallReflected));
            Assert.That(duplicate, Is.EqualTo(MovementContact2DProcessResult.DuplicateCallbackIgnored));
            Assert.That(authority.WallApplyCount, Is.EqualTo(1));
            Assert.That(adapter.WallContactsProcessed, Is.EqualTo(1));
            AssertVector(body.linearVelocity, afterFirst.x, afterFirst.y);
        }

        [Test]
        public void EnemyRepeatWithDistinctNormal_IsSuppressedByMT006Grace()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            FakeMovementContactAuthority authority = CreateAuthority(tuning, -7d, -2d);
            Rigidbody2D body;
            MovementContact2DAdapter adapter = CreateAdapter(authority, out body);
            StableId enemyId = StableId.Parse("enemy.grace.mt-009");
            Collider2D enemy = CreateContactCollider(
                "MT-009 grace enemy",
                CreateEnemyDescriptor(
                    enemyId,
                    CombatWeightClass.Heavy,
                    CombatWeightClass.Light));

            MovementContact2DProcessResult first = adapter.TryProcessContact(
                enemy,
                Vector2.right,
                7d);
            Vector2 afterFirst = body.linearVelocity;
            MovementContact2DProcessResult repeat = adapter.TryProcessContact(
                enemy,
                Vector2.up,
                7d);

            Assert.That(first, Is.EqualTo(MovementContact2DProcessResult.EnemyResolved));
            Assert.That(repeat, Is.EqualTo(MovementContact2DProcessResult.EnemyGraceIgnored));
            Assert.That(authority.EnemyApplyCount, Is.EqualTo(2));
            Assert.That(authority.EnemyResolutionCount, Is.EqualTo(1));
            Assert.That(
                authority.LastRegistration.Decision,
                Is.EqualTo(ContactGraceDecision.DuplicateWithinSimultaneousWindow));
            AssertVector(body.linearVelocity, afterFirst.x, afterFirst.y);
        }

        [Test]
        public void ZeroNormal_TunnelingSentinelFailsSafelyWithoutMutation()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            FakeMovementContactAuthority authority = CreateAuthority(tuning, -4d, 1d);
            Rigidbody2D body;
            MovementContact2DAdapter adapter = CreateAdapter(authority, out body);
            Collider2D wall = CreateContactCollider(
                "MT-009 tunneling sentinel",
                MovementContact2DDescriptor.Wall());
            body.linearVelocity = new Vector2(3f, 4f);

            MovementContact2DProcessResult result = adapter.TryProcessContact(
                wall,
                Vector2.zero,
                8d);

            Assert.That(result, Is.EqualTo(MovementContact2DProcessResult.TunnelingSentinelIgnored));
            Assert.That(authority.SnapshotReadCount, Is.Zero);
            Assert.That(authority.TotalApplyCount, Is.Zero);
            AssertVector(body.linearVelocity, 3f, 4f);
        }

        [Test]
        public void DestroyedColliderBeforeProcessing_FailsSafely()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            FakeMovementContactAuthority authority = CreateAuthority(tuning, -4d, 0d);
            Rigidbody2D body;
            MovementContact2DAdapter adapter = CreateAdapter(authority, out body);
            Collider2D wall = CreateContactCollider(
                "MT-009 destroyed collider",
                MovementContact2DDescriptor.Wall());
            UnityEngine.Object.DestroyImmediate(wall.gameObject);

            MovementContact2DProcessResult result = adapter.TryProcessContact(
                wall,
                Vector2.right,
                9d);

            Assert.That(result, Is.EqualTo(MovementContact2DProcessResult.InvalidContactIgnored));
            Assert.That(authority.TotalApplyCount, Is.Zero);
        }

        [Test]
        public void NewFixedStep_AllowsAValidSustainedContactExactlyOnceAgain()
        {
            MovementThrusterTuningProfile tuning = BuildTuning();
            FakeMovementContactAuthority authority = CreateAuthority(tuning, -6d, 0d);
            Rigidbody2D body;
            MovementContact2DAdapter adapter = CreateAdapter(authority, out body);
            Collider2D wall = CreateContactCollider(
                "MT-009 sustained wall",
                MovementContact2DDescriptor.Wall());

            Assert.That(
                adapter.TryProcessContact(wall, Vector2.right, 10d),
                Is.EqualTo(MovementContact2DProcessResult.WallReflected));
            Assert.That(
                adapter.TryProcessContact(wall, Vector2.right, 10d),
                Is.EqualTo(MovementContact2DProcessResult.DuplicateCallbackIgnored));

            authority.SetMovement(-6d, 0d);
            Assert.That(adapter.BeginFixedStep(1L), Is.True);
            Assert.That(
                adapter.TryProcessContact(wall, Vector2.right, 11d),
                Is.EqualTo(MovementContact2DProcessResult.WallReflected));
            Assert.That(authority.WallApplyCount, Is.EqualTo(2));
        }

        [Test]
        public void AdapterSurface_ContainsOnlyUnity2DPhysicsTypes()
        {
            Type[] inspectedTypes =
            {
                typeof(MovementContactClassifier),
                typeof(IMovementContact2DContract),
                typeof(MovementContact2DAdapter),
                typeof(IMovementContactAuthority),
            };

            for (int index = 0; index < inspectedTypes.Length; index++)
            {
                AssertDeclaredMembersContainNo3DPhysics(inspectedTypes[index]);
            }
        }

        private MovementContact2DAdapter CreateAdapter(
            FakeMovementContactAuthority authority,
            out Rigidbody2D body)
        {
            GameObject mover = new GameObject("MT-009 mover");
            createdObjects.Add(mover);
            body = mover.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            MovementContact2DAdapter adapter = mover.AddComponent<MovementContact2DAdapter>();
            adapter.Configure(body, authority);
            return adapter;
        }

        private Collider2D CreateContactCollider(
            string name,
            MovementContact2DDescriptor descriptor)
        {
            GameObject contactObject = new GameObject(name);
            createdObjects.Add(contactObject);
            BoxCollider2D collider = contactObject.AddComponent<BoxCollider2D>();
            TestMovementContact2DContract contract =
                contactObject.AddComponent<TestMovementContact2DContract>();
            contract.Descriptor = descriptor;
            return collider;
        }

        private Collider2D CreateBareCollider(string name)
        {
            GameObject contactObject = new GameObject(name);
            createdObjects.Add(contactObject);
            return contactObject.AddComponent<BoxCollider2D>();
        }

        private static MovementContact2DDescriptor CreateEnemyDescriptor(
            StableId enemyId,
            CombatWeightClass sourceWeight,
            CombatWeightClass targetWeight)
        {
            WeightResult result = WeightMessage.DetermineResult(sourceWeight, targetWeight);
            WeightMessage message = new WeightMessage(
                StableId.Parse("event." + enemyId.Value),
                StableId.Parse("actor.player.mt-009"),
                enemyId,
                CombatChannel.Contact,
                sourceWeight,
                targetWeight,
                result);
            return MovementContact2DDescriptor.Enemy(enemyId, message);
        }

        private static FakeMovementContactAuthority CreateAuthority(
            MovementThrusterTuningProfile tuning,
            double velocityX,
            double velocityY)
        {
            return new FakeMovementContactAuthority(tuning, velocityX, velocityY);
        }

        private static MovementThrusterTuningProfile BuildTuning()
        {
            return MovementThrusterTuningProfile.Create(
                MovementThrusterTuningProfile.CurrentProfileVersion,
                StableId.Parse("tuning.mt-009-tests"),
                12d,
                50d,
                60d,
                90d,
                1.25d,
                2,
                1,
                1.75d,
                2.5d,
                0.3d,
                0.1d,
                0.05d,
                120d,
                0.04d,
                0.2d,
                0.75d,
                2d,
                0.8d,
                0.15d,
                5d,
                4,
                0.8d,
                0.9d,
                0.1d,
                0.5d,
                0.02d,
                128);
        }

        private static void AssertVector(Vector2 actual, float expectedX, float expectedY)
        {
            Assert.That(actual.x, Is.EqualTo(expectedX).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expectedY).Within(Tolerance));
        }

        private static void AssertDeclaredMembersContainNo3DPhysics(Type type)
        {
            BindingFlags flags = BindingFlags.Instance
                | BindingFlags.Static
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly;

            FieldInfo[] fields = type.GetFields(flags);
            for (int index = 0; index < fields.Length; index++)
            {
                AssertNot3DPhysicsType(fields[index].FieldType, type, fields[index].Name);
            }

            PropertyInfo[] properties = type.GetProperties(flags);
            for (int index = 0; index < properties.Length; index++)
            {
                AssertNot3DPhysicsType(properties[index].PropertyType, type, properties[index].Name);
            }

            MethodInfo[] methods = type.GetMethods(flags);
            for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
            {
                MethodInfo method = methods[methodIndex];
                AssertNot3DPhysicsType(method.ReturnType, type, method.Name + " return");
                ParameterInfo[] parameters = method.GetParameters();
                for (int parameterIndex = 0; parameterIndex < parameters.Length; parameterIndex++)
                {
                    AssertNot3DPhysicsType(
                        parameters[parameterIndex].ParameterType,
                        type,
                        method.Name + "." + parameters[parameterIndex].Name);
                }
            }
        }

        private static void AssertNot3DPhysicsType(
            Type candidate,
            Type declaringType,
            string memberName)
        {
            if (candidate.IsByRef)
            {
                candidate = candidate.GetElementType();
            }

            Type[] forbiddenTypes =
            {
                typeof(Rigidbody),
                typeof(Collider),
                typeof(Collision),
                typeof(Joint),
                typeof(UnityEngine.Physics),
                typeof(PhysicsScene),
                typeof(RaycastHit),
                typeof(ContactPoint),
                typeof(Vector3),
            };

            for (int index = 0; index < forbiddenTypes.Length; index++)
            {
                Assert.That(
                    candidate,
                    Is.Not.EqualTo(forbiddenTypes[index]),
                    declaringType.FullName + "." + memberName + " references a 3D physics type.");
            }
        }

        private sealed class FakeMovementContactAuthority : IMovementContactAuthority
        {
            private readonly MovementThrusterTuningProfile tuning;
            private ThrusterBurstState movement;
            private PerContactGraceTracker graceTracker;

            public FakeMovementContactAuthority(
                MovementThrusterTuningProfile tuning,
                double velocityX,
                double velocityY)
            {
                this.tuning = tuning;
                graceTracker = PerContactGraceTracker.Create(tuning);
                SetMovement(velocityX, velocityY);
            }

            public int SnapshotReadCount { get; private set; }

            public int WallApplyCount { get; private set; }

            public int EnemyApplyCount { get; private set; }

            public int EnemyResolutionCount { get; private set; }

            public int TotalApplyCount
            {
                get { return WallApplyCount + EnemyApplyCount; }
            }

            public WallReflectionResult LastWallResult { get; private set; }

            public ContactGraceRegistration LastRegistration { get; private set; }

            public MovementContactResolution LastEnemyResolution { get; private set; }

            public bool TryReadContactSnapshot(out MovementContactStateSnapshot snapshot)
            {
                SnapshotReadCount++;
                snapshot = new MovementContactStateSnapshot(
                    movement,
                    tuning,
                    0d,
                    0d,
                    graceTracker);
                return true;
            }

            public void ApplyWallContact(WallReflectionResult result)
            {
                WallApplyCount++;
                LastWallResult = result;
                movement = result.State;
            }

            public void ApplyEnemyContact(
                ContactGraceRegistration registration,
                MovementContactResolution resolution,
                PerContactGraceTracker nextGraceTracker)
            {
                EnemyApplyCount++;
                LastRegistration = registration;
                LastEnemyResolution = resolution;
                graceTracker = nextGraceTracker;
                if (resolution != null)
                {
                    EnemyResolutionCount++;
                }
            }

            public void SetMovement(double velocityX, double velocityY)
            {
                movement = ThrusterBurstState.CreateReady(
                    BaseLocomotionState.Create(velocityX, velocityY),
                    tuning);
            }
        }
    }

    public sealed class TestMovementContact2DContract : MonoBehaviour, IMovementContact2DContract
    {
        public MovementContact2DDescriptor Descriptor { get; set; }

        public bool TryDescribeMovementContact(out MovementContact2DDescriptor descriptor)
        {
            descriptor = Descriptor;
            return descriptor != null;
        }
    }
}
